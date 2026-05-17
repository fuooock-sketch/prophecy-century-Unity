using System;
using System.Collections.Generic;
using System.Linq;
using ProphecyCentury.Core;
using ProphecyCentury.Data;
using ProphecyCentury.Model;

namespace ProphecyCentury.Systems
{
    public sealed class ManageEventResolver
    {
        private const int HandMaxCount = 9;
        private readonly Random _random = new Random();
        private readonly ShopSystem _shopSystem;
        private bool _abilityTriggered;
        private readonly List<DevourShopEventState> _devourShopEvents = new List<DevourShopEventState>();
        private readonly ManageFeedbackEventsState _feedbackEvents = new ManageFeedbackEventsState();

        public ManageEventResolver(ShopSystem shopSystem)
        {
            _shopSystem = shopSystem;
        }

        public void ResolveRoundStart(RunState runState)
        {
            if (runState == null)
            {
                return;
            }

            runState.manageResources.forestGiftRoundActions = 0;
            runState.manageResources.forestGiftRoundTotal = 0;
            foreach (var unit in runState.boardUnits)
            {
                unit.roundTempAttack = 0;
                unit.roundTempPower = 0;
                unit.roundTempMorale = 0;
                unit.manageAttackGainBucket = 0;
                unit.manageRoundAttackRewardTriggered = false;
            }

            Dispatch(runState, "on_round_start", null, "round_start", null, 0, new HashSet<string>());
        }

        public void ResolveRoundEnd(RunState runState)
        {
            Dispatch(runState, "on_round_end", null, "round_end", null, 0, new HashSet<string>());
        }

        public void ResolveEntry(RunState runState, BoardUnitState target, string reason = "deploy")
        {
            Dispatch(runState, "on_entry", target, reason, null, 0, new HashSet<string>());
        }

        public void ResolveLeave(RunState runState, BoardUnitState target, string reason = "leave")
        {
            Dispatch(runState, "on_leave", target, reason, null, 0, new HashSet<string>());
        }

        public void ResolveSell(RunState runState, UnitCardState target)
        {
            Dispatch(runState, "on_sell", target, "sell", null, 0, new HashSet<string>());
        }

        public void ResolveGainUnit(RunState runState, UnitCardState target)
        {
            Dispatch(runState, "on_gain_unit", target, "gain_unit", null, 0, new HashSet<string>());
        }

        public bool ConsumeAbilityTriggered()
        {
            var triggered = _abilityTriggered;
            _abilityTriggered = false;
            return triggered;
        }

        public List<DevourShopEventState> ConsumeDevourShopEvents()
        {
            var events = new List<DevourShopEventState>(_devourShopEvents);
            _devourShopEvents.Clear();
            return events;
        }

        public ManageFeedbackEventsState ConsumeFeedbackEvents()
        {
            var events = new ManageFeedbackEventsState();
            events.forestGemGiftEvents.AddRange(_feedbackEvents.forestGemGiftEvents);
            events.evolveEvents.AddRange(_feedbackEvents.evolveEvents);
            events.shopBuffEvents.AddRange(_feedbackEvents.shopBuffEvents);
            _feedbackEvents.forestGemGiftEvents.Clear();
            _feedbackEvents.evolveEvents.Clear();
            _feedbackEvents.shopBuffEvents.Clear();
            return events;
        }

        private void Dispatch(RunState runState, string eventType, UnitCardState target, string reason, UnitCardState source, int value, HashSet<string> processed, int depth = 0)
        {
            if (runState == null || string.IsNullOrWhiteSpace(eventType) || depth > 8)
            {
                return;
            }

            var owners = GetOwnersForEvent(runState, eventType, target).ToList();
            if (eventType == "on_round_end")
            {
                owners.Sort((left, right) => BoardOrderIndex(left).CompareTo(BoardOrderIndex(right)));
            }

            foreach (var owner in owners)
            {
                foreach (var talent in GetTalents(owner))
                {
                    if (!Handles(talent, eventType))
                    {
                        continue;
                    }

                    var targetSlot = target is BoardUnitState targetBoard ? targetBoard.boardSlotId : string.Empty;
                    var key = $"{eventType}|{reason}|{owner.boardSlotId}|{owner.unitId}|{talent.kind}|{target?.unitId}|{targetSlot}";
                    if (processed.Contains(key))
                    {
                        continue;
                    }

                    processed.Add(key);
                    _abilityTriggered = true;
                    HandleTalent(runState, owner, talent, eventType, target, reason, source, value, processed, depth);
                }
            }
        }

        private static IEnumerable<BoardUnitState> GetOwnersForEvent(RunState runState, string eventType, UnitCardState target)
        {
            foreach (var unit in runState.boardUnits)
            {
                if (unit != null)
                {
                    yield return unit;
                }
            }

            if (eventType == "on_leave" && target is BoardUnitState leaving && !runState.boardUnits.Contains(leaving))
            {
                yield return leaving;
            }
        }

        private void HandleTalent(RunState runState, BoardUnitState owner, SkillDefinition talent, string eventType, UnitCardState target, string reason, UnitCardState source, int eventValue, HashSet<string> processed, int depth)
        {
            switch (talent.kind)
            {
                case "while_on_board_on_entry_race_self_gain_attack":
                    if (eventType == "on_entry" && target != owner && CountsAsRace(runState, target, talent.race))
                    {
                        AddStat(runState, owner, "attack", Value(talent, owner), owner, processed, depth);
                    }
                    break;
                case "while_on_board_on_entry_race_gain_stats":
                    if (eventType == "on_entry" && target != owner && CountsAsRace(runState, target, talent.race))
                    {
                        AddStat(runState, target, "defense", owner.isGolden ? NonZero(talent.goldDefense, talent.defense) : talent.defense, owner, processed, depth);
                        AddStat(runState, target, "attack", owner.isGolden ? NonZero(talent.goldAttack, talent.attack) : talent.attack, owner, processed, depth);
                    }
                    break;
                case "round_end_if_adjacent_faith_self_gain_attack":
                    if (eventType == "on_round_end" && SideAdjacent(runState, owner).Any(unit => UnitDef(unit)?.faith == talent.faith))
                    {
                        AddStat(runState, owner, "attack", Value(talent, owner), owner, processed, depth);
                    }
                    break;
                case "round_end_self_gain_attack_per_faith_count":
                    if (eventType == "on_round_end")
                    {
                        var faith = string.IsNullOrWhiteSpace(talent.faith) ? UnitDef(owner)?.faith : talent.faith;
                        AddStat(runState, owner, "attack", Value(talent, owner) * runState.boardUnits.Count(unit => UnitDef(unit)?.faith == faith), owner, processed, depth);
                    }
                    break;
                case "round_start_retrigger_race_round_end_talents":
                    if (eventType == "on_round_start")
                    {
                        RetriggerRaceRoundEndTalents(runState, owner, talent, processed, depth);
                    }
                    break;
                case "round_start_if_race_count_temp_power":
                    if (eventType == "on_round_start" && CountRace(runState, talent.race, UnitDef(owner)?.race) >= Math.Max(1, talent.threshold))
                    {
                        owner.roundTempPower += Value(talent, owner);
                    }
                    break;
                case "while_on_board_on_entry_tagged_units_gain_attack":
                    if (eventType == "on_entry" && target != owner && CountsAsRace(runState, target, talent.entryRace))
                    {
                        foreach (var unit in runState.boardUnits.Where(unit => HasTag(unit, talent.targetTag)))
                        {
                            AddStat(runState, unit, "attack", Value(talent, owner), owner, processed, depth);
                        }
                    }
                    break;
                case "on_entry_random_race_units_gain_power":
                    if (eventType == "on_entry" && target == owner)
                    {
                        PickRandom(runState.boardUnits.Where(unit => unit != owner && CountsAsRace(runState, unit, talent.race)).ToList(), Count(talent, owner))
                            .ForEach(unit => AddStat(runState, unit, "power", Power(talent, owner, 1), owner, processed, depth));
                    }
                    break;
                case "while_on_board_every_n_entry_race_add_random_unit_to_hand":
                    if (eventType == "on_entry" && target != owner && CountsAsRace(runState, target, talent.race))
                    {
                        owner.manageEntryEffectTriggerCount += 1;
                        if (owner.manageEntryEffectTriggerCount >= Math.Max(1, talent.threshold))
                        {
                            owner.manageEntryEffectTriggerCount = 0;
                            AddRandomUnitsToHand(runState, unit => !unit.hidden && unit.race == talent.race, Count(talent, owner));
                        }
                    }
                    break;
                case "while_on_board_on_entry_same_id_tagged_units_gain_power":
                    if (eventType == "on_entry" && target != owner && (string.IsNullOrWhiteSpace(talent.tag) || HasTag(target, talent.tag)))
                    {
                        foreach (var unit in runState.boardUnits.Where(unit => unit.unitId == target.unitId))
                        {
                            AddStat(runState, unit, "power", Power(talent, owner, 1), owner, processed, depth);
                        }
                    }
                    break;
                case "on_gain_power_self_gain_attack":
                    if (eventType == "on_gain_power" && target == owner)
                    {
                        AddStat(runState, owner, "attack", Value(talent, owner), owner, processed, depth);
                    }
                    break;
                case "on_gain_defense_team_gain_attack":
                    if (eventType == "on_gain_defense" && target == owner)
                    {
                        foreach (var unit in runState.boardUnits)
                        {
                            AddStat(runState, unit, "attack", Value(talent, owner, 1), owner, processed, depth);
                        }
                    }
                    break;
                case "while_on_board_any_ally_gain_stat_extra_defense":
                    if ((eventType == "on_gain_attack" || eventType == "on_gain_defense" || eventType == "on_gain_power") && target is BoardUnitState)
                    {
                        AddStat(runState, target, "defense", Defense(talent, owner, 1), owner, processed, depth);
                        AddStat(runState, target, "hp", Hp(talent, owner, 0), owner, processed, depth);
                    }
                    break;
                case "while_on_board_attack_gain_threshold_add_random_unit_to_hand":
                    if (eventType == "on_gain_attack" && target is BoardUnitState && eventValue > 0 && !owner.manageRoundAttackRewardTriggered)
                    {
                        owner.manageAttackGainBucket += eventValue;
                        if (owner.manageAttackGainBucket >= Math.Max(1, talent.threshold))
                        {
                            owner.manageRoundAttackRewardTriggered = AddRandomUnitsToHand(runState, unit => !unit.hidden && unit.race == talent.race, Count(talent, owner)) > 0;
                        }
                    }
                    break;
                case "while_on_board_attack_gain_threshold_evolve":
                    if (eventType == "on_gain_attack" && target is BoardUnitState && eventValue > 0)
                    {
                        owner.manageAttackGainBucket += eventValue;
                        if (owner.manageAttackGainBucket >= Math.Max(1, talent.threshold))
                        {
                            owner.manageAttackGainBucket = 0;
                            Evolve(runState, owner, talent.targetUnitId, target, processed, depth);
                        }
                    }
                    break;
                case "while_on_board_self_gain_stat_team_gain_power":
                    if ((eventType == "on_gain_power" || eventType == "on_gain_attack") && target == owner)
                    {
                        foreach (var unit in runState.boardUnits)
                        {
                            AddStat(runState, unit, "power", Value(talent, owner, 1), owner, processed, depth);
                        }
                    }
                    break;
                case "on_gain_power_convert_to_attack_random_board_unit":
                    if (eventType == "on_gain_power" && target == owner && eventValue > 0)
                    {
                        AddStat(runState, owner, "power", -eventValue, owner, processed, depth);
                        var recipient = PickRandom(runState.boardUnits.ToList(), 1).FirstOrDefault();
                        AddStat(runState, recipient, "attack", eventValue * Value(talent, owner, 10), owner, processed, depth);
                    }
                    break;
                case "on_gain_stat_retrigger_side_adjacent_entry_effects":
                    if ((eventType == "on_gain_attack" || eventType == "on_gain_defense") && target == owner)
                    {
                        foreach (var unit in SideAdjacent(runState, owner).Where(unit => HasEntryTalent(unit)).ToList())
                        {
                            Dispatch(runState, "on_entry", unit, "gain_stat_retrigger_adjacent_entry", owner, 0, processed, depth + 1);
                        }
                    }
                    break;
                case "enter_if_board_faith_count_discover":
                    if (eventType == "on_entry" && target == owner && runState.boardUnits.Count(unit => UnitDef(unit)?.faith == talent.faith) > Math.Max(0, talent.threshold))
                    {
                        AddRandomUnitsToHand(runState, unit => unit.faith == talent.faith && !unit.hidden, Count(talent, owner));
                    }
                    break;
                case "round_end_tagged_units_gain_attack_and_defense":
                    if (eventType == "on_round_end")
                    {
                        foreach (var unit in runState.boardUnits.Where(unit => HasTag(unit, talent.targetTag)))
                        {
                            AddStat(runState, unit, "attack", owner.isGolden ? talent.goldAttackValue : talent.attackValue, owner, processed, depth);
                            AddStat(runState, unit, "defense", owner.isGolden ? talent.goldDefenseValue : talent.defenseValue, owner, processed, depth);
                        }
                    }
                    break;
                case "leave_board_gain_gold":
                    if (eventType == "on_leave" && target == owner)
                    {
                        runState.gold += Value(talent, owner);
                        var recipient = PickRandom(runState.boardUnits.ToList(), 1).FirstOrDefault();
                        AddStat(runState, recipient, "power", talent.power, owner, processed, depth);
                    }
                    break;
                case "leave_board_tagged_units_gain_stats":
                    if (eventType == "on_leave" && target == owner)
                    {
                        ApplyLeaveTaggedStats(runState, owner, talent, processed, depth);
                    }
                    break;
                case "on_leave_tag_count_tagged_units_gain_power":
                    if (eventType == "on_leave" && target == owner)
                    {
                        var tagCount = runState.boardUnits.Count(unit => HasTag(unit, talent.targetTag));
                        foreach (var unit in runState.boardUnits.Where(unit => HasTag(unit, talent.targetTag)))
                        {
                            AddStat(runState, unit, "power", tagCount * Power(talent, owner, 1), owner, processed, depth);
                        }
                    }
                    break;
                case "on_leave_retrigger_random_race_entry_effects":
                    if (eventType == "on_leave" && target == owner)
                    {
                        var excluded = new HashSet<string>(talent.excludeUnitIds ?? Array.Empty<string>());
                        var candidates = runState.boardUnits
                            .Where(unit => unit != owner && CountsAsRace(runState, unit, talent.race) && !excluded.Contains(unit.unitId) && HasEntryTalent(unit))
                            .ToList();
                        foreach (var unit in PickRandom(candidates, Count(talent, owner)))
                        {
                            Dispatch(runState, "on_entry", unit, "leave_retrigger_entry_effect", owner, 0, processed, depth + 1);
                        }
                    }
                    break;
                case "round_end_gain_forest_gem_self":
                    if (eventType == "on_round_end")
                    {
                        GainForestGem(runState, owner, Value(talent, owner), owner, processed, depth);
                    }
                    break;
                case "on_entry_gain_forest_gem_self":
                    if (eventType == "on_entry" && target == owner)
                    {
                        GainForestGem(runState, owner, Value(talent, owner), owner, processed, depth);
                    }
                    break;
                case "on_entry_any_unit_gain_forest_gem_self":
                    if (eventType == "on_entry" && target != owner)
                    {
                        GainForestGem(runState, owner, Value(talent, owner), owner, processed, depth);
                    }
                    break;
                case "on_leave_gift_forest_gem_team":
                    if (eventType == "on_leave" && target == owner)
                    {
                        foreach (var unit in runState.boardUnits)
                        {
                            GiftForestGem(runState, owner, unit, Value(talent, owner), processed, depth);
                        }
                    }
                    break;
                case "round_end_self_gain_attack":
                    if (eventType == "on_round_end")
                    {
                        AddStat(runState, owner, "attack", Value(talent, owner), owner, processed, depth);
                    }
                    break;
                case "round_end_self_temp_morale_per_race_count":
                    if (eventType == "on_round_end")
                    {
                        owner.roundTempMorale += Value(talent, owner) * CountRace(runState, talent.race, UnitDef(owner)?.race);
                    }
                    break;
                case "on_receive_gift_self_gain_attack":
                    if (eventType == "on_receive_gift" && target == owner)
                    {
                        AddStat(runState, owner, "attack", Value(talent, owner) * Math.Max(0, eventValue), owner, processed, depth);
                    }
                    break;
                case "on_receive_gift_self_evolve":
                    if (eventType == "on_receive_gift" && target == owner && owner.forestGemsReceived >= talent.threshold)
                    {
                        Evolve(runState, owner, talent.targetUnitId, source, processed, depth);
                    }
                    break;
                case "on_receive_gift_total_discover_race_unit_once":
                    if (eventType == "on_receive_gift" && target == owner && !owner.manageReceiveGiftDiscoverTriggered && owner.forestGemsReceived >= talent.threshold)
                    {
                        owner.manageReceiveGiftDiscoverTriggered = AddRandomUnitsToHand(runState, unit => !unit.hidden && unit.race == (string.IsNullOrWhiteSpace(talent.race) ? UnitDef(owner)?.race : talent.race), Count(talent, owner)) > 0;
                    }
                    break;
                case "on_gain_forest_gem_self_gain_attack":
                    if (eventType == "on_gain_forest_gem" && target == owner)
                    {
                        AddStat(runState, owner, "attack", Value(talent, owner) * Math.Max(0, eventValue), owner, processed, depth);
                    }
                    break;
                case "round_end_if_no_forest_gem_in_hand_self_gain_attack":
                    if (eventType == "on_round_end" && Math.Max(0, runState.manageResources.forestGems) <= 0)
                    {
                        AddStat(runState, owner, "attack", Attack(talent, owner, Value(talent, owner)), owner, processed, depth);
                    }
                    break;
                case "on_gain_forest_gem_auto_gift_self_team_attack":
                    if (eventType == "on_gain_forest_gem")
                    {
                        var amount = Math.Min(runState.manageResources.forestGems, Math.Max(0, eventValue));
                        if (amount > 0)
                        {
                            runState.manageResources.forestGems -= amount;
                            GiftForestGem(runState, owner, owner, amount, processed, depth);
                        }

                        foreach (var unit in runState.boardUnits)
                        {
                            AddStat(runState, unit, "attack", Attack(talent, owner, 0), owner, processed, depth);
                        }
                    }
                    break;
                case "round_end_forward_adjacent_units_gain_attack_and_gift":
                    if (eventType == "on_round_end")
                    {
                        foreach (var unit in ForwardAdjacent(runState, owner, talent.targetMode))
                        {
                            AddStat(runState, unit, "attack", Attack(talent, owner, 0), owner, processed, depth);
                            GiftForestGem(runState, owner, unit, Gift(talent, owner), processed, depth);
                        }
                    }
                    break;
                case "on_entry_side_units_gift_forest_gem":
                    if (eventType == "on_entry" && target == owner)
                    {
                        foreach (var unit in SideAdjacent(runState, owner))
                        {
                            GiftForestGem(runState, owner, unit, Value(talent, owner), processed, depth);
                        }
                    }
                    break;
                case "round_end_self_gift_forest_gem":
                    if (eventType == "on_round_end")
                    {
                        GiftForestGem(runState, owner, owner, Value(talent, owner), processed, depth);
                    }
                    break;
                case "on_gift_action_team_gain_attack_every_n":
                    if (eventType == "on_gift_action" && talent.threshold > 0)
                    {
                        var bucket = runState.manageResources.forestGiftActions / talent.threshold;
                        if (bucket > owner.manageGiftActionBucket)
                        {
                            var gain = (bucket - owner.manageGiftActionBucket) * Attack(talent, owner, 0);
                            owner.manageGiftActionBucket = bucket;
                            foreach (var unit in runState.boardUnits)
                            {
                                AddStat(runState, unit, "attack", gain, owner, processed, depth);
                            }
                        }
                    }
                    break;
                case "on_other_sell_absorb_attached_gems_and_gain_attack":
                    if (eventType == "on_sell" && target != null && target != owner)
                    {
                        var absorb = Math.Max(0, target.forestGemsAttached);
                        if (absorb > 0)
                        {
                            target.forestGemsAttached = 0;
                            GiftForestGem(runState, owner, owner, absorb, processed, depth);
                        }

                        AddStat(runState, owner, "attack", Value(talent, owner), owner, processed, depth);
                    }
                    break;
                case "on_receive_gift_total_team_gain_power_every_n":
                    if (eventType == "on_receive_gift" && target == owner && talent.threshold > 0)
                    {
                        var bucket = owner.forestGemsReceived / talent.threshold;
                        if (bucket > owner.manageReceiveGiftPowerBucket)
                        {
                            var gain = (bucket - owner.manageReceiveGiftPowerBucket) * Power(talent, owner, 0);
                            owner.manageReceiveGiftPowerBucket = bucket;
                            foreach (var unit in runState.boardUnits)
                            {
                                AddStat(runState, unit, "power", gain, owner, processed, depth);
                            }
                        }
                    }
                    break;
                case "on_entry_devour_random_shop_gain_stats":
                    if (eventType == "on_entry" && target == owner)
                    {
                        DevourShopCard(runState, owner, talent, false, processed, depth);
                    }
                    break;
                case "round_end_devour_shop_highest_attack_gain_attack":
                    if (eventType == "on_round_end")
                    {
                        DevourShopCard(runState, owner, talent, true, processed, depth);
                    }
                    break;
                case "while_on_board_on_entry_race_devour_shop_gain_attack":
                    if (eventType == "on_entry" && target != owner && CountsAsRace(runState, target, talent.race))
                    {
                        DevourShopCard(runState, owner, talent, false, processed, depth);
                    }
                    break;
                case "on_entry_race_units_devour_shop_gain_attack":
                    if (eventType == "on_entry" && target == owner)
                    {
                        foreach (var unit in runState.boardUnits.Where(unit => CountsAsRace(runState, unit, talent.race)).ToList())
                        {
                            DevourShopCard(runState, unit, talent, false, processed, depth);
                        }
                    }
                    break;
                case "round_end_tagged_units_devour_shop_gain_attack":
                    if (eventType == "on_round_end")
                    {
                        foreach (var unit in runState.boardUnits.Where(unit => HasTag(unit, talent.targetTag)).ToList())
                        {
                            DevourShopCard(runState, unit, talent, false, processed, depth);
                        }
                    }
                    break;
                case "on_devour_team_gain_attack":
                    if (eventType == "on_devour")
                    {
                        foreach (var unit in runState.boardUnits)
                        {
                            AddStat(runState, unit, "attack", Attack(talent, owner, 0), owner, processed, depth);
                        }
                    }
                    break;
                case "on_entry_add_unit_to_hand":
                    if (eventType == "on_entry" && target == owner)
                    {
                        for (var i = 0; i < Count(talent, owner); i += 1)
                        {
                            AddUnitToHand(runState, talent.unitId);
                        }
                    }
                    break;
                case "on_entry_shop_cards_gain_attack":
                    if (eventType == "on_entry" && target == owner)
                    {
                        var gain = Attack(talent, owner, 0);
                        runState.manageResources.shopGeneratedBuffAttack += gain;
                        var buffEvent = new ShopBuffEventState
                        {
                            sourceSlotId = owner.boardSlotId,
                            sourceName = owner.name,
                            attack = gain
                        };
                        foreach (var card in runState.shopCards.Where(card => card != null))
                        {
                            card.shopBuffAttack += gain;
                        }

                        for (var i = 0; i < runState.shopCards.Count; i += 1)
                        {
                            if (runState.shopCards[i] != null)
                            {
                                buffEvent.shopIndices.Add(i);
                            }
                        }

                        if (buffEvent.attack > 0 && buffEvent.shopIndices.Count > 0)
                        {
                            _feedbackEvents.shopBuffEvents.Add(buffEvent);
                        }
                    }
                    break;
                case "on_any_entry_effect_triggered_self_gain_attack":
                    if (eventType == "on_entry" && target != owner && HasEntryTalent(target))
                    {
                        AddStat(runState, owner, "attack", Attack(talent, owner, 0), owner, processed, depth);
                    }
                    break;
                case "round_end_temp_gain_adjacent_attack":
                    if (eventType == "on_round_end")
                    {
                        var adjacent = SideAdjacent(runState, owner).Select(EffectiveAttack).ToList();
                        if (adjacent.Count > 0)
                        {
                            owner.roundTempAttack += talent.mode == "sum" ? adjacent.Sum() : adjacent.Max();
                        }
                    }
                    break;
                case "on_entry_board_tagged_units_gain_attack":
                    if (eventType == "on_entry" && target == owner)
                    {
                        foreach (var unit in runState.boardUnits.Where(unit => unit != owner && HasTag(unit, talent.targetTag)))
                        {
                            AddStat(runState, unit, "attack", Attack(talent, owner, 0), owner, processed, depth);
                        }
                    }
                    break;
                case "on_any_entry_effect_count_evolve":
                    if (eventType == "on_entry" && target != owner && HasEntryTalent(target))
                    {
                        owner.manageEntryEffectTriggerCount += 1;
                        if (owner.manageEntryEffectTriggerCount >= Math.Max(1, talent.threshold))
                        {
                            owner.manageEntryEffectTriggerCount = 0;
                            Evolve(runState, owner, talent.targetUnitId, target, processed, depth);
                        }
                    }
                    break;
                case "enter_target_unit_permanent_power":
                    if (eventType == "on_entry" && target == owner && reason != null && reason.Contains("retrigger"))
                    {
                        var unit = PickRandom(runState.boardUnits.ToList(), 1).FirstOrDefault();
                        AddStat(runState, unit, "power", Value(talent, owner), owner, processed, depth);
                    }
                    break;
                case "while_on_board_entry_effect_self_and_rear_gain_attack":
                    if (eventType == "on_entry" && target != owner && HasEntryTalent(target))
                    {
                        foreach (var unit in SelfAndRearRow(runState, owner))
                        {
                            AddStat(runState, unit, "attack", Attack(talent, owner, 0), owner, processed, depth);
                        }
                    }
                    break;
                case "on_instant_evolve_self_gift_and_gain_attack":
                    if (eventType == "on_instant_evolve" && target == owner)
                    {
                        GiftForestGem(runState, owner, owner, Gift(talent, owner), processed, depth);
                        GainForestGem(runState, owner, owner.isGolden ? talent.goldGain : talent.gain, owner, processed, depth);
                        AddStat(runState, owner, "attack", Attack(talent, owner, 0), owner, processed, depth);
                    }
                    break;
                case "on_entry_devour_board_tag_copy_gain_attack":
                    if (eventType == "on_entry" && target == owner)
                    {
                        foreach (var unit in PickRandom(runState.boardUnits.Where(unit => unit != owner && HasTag(unit, talent.targetTag)).ToList(), Count(talent, owner)))
                        {
                            AddStat(runState, owner, "attack", EffectiveAttack(unit), owner, processed, depth);
                            Dispatch(runState, "on_devour", owner, "devour_board_tag_copy", owner, EffectiveAttack(unit), processed, depth + 1);
                        }
                    }
                    break;
            }
        }

        private void RetriggerRaceRoundEndTalents(RunState runState, BoardUnitState owner, SkillDefinition talent, HashSet<string> processed, int depth)
        {
            var times = Math.Max(1, owner.isGolden ? NonZero(talent.goldTimes, talent.times, 1) : NonZero(talent.times, 1));
            var targets = runState.boardUnits.Where(unit => unit != owner && CountsAsRace(runState, unit, talent.race)).ToList();
            for (var i = 0; i < times; i += 1)
            {
                foreach (var target in targets)
                {
                    foreach (var roundEndTalent in GetTalents(target).Where(item => Handles(item, "on_round_end") && item.kind != talent.kind))
                    {
                        HandleTalent(runState, target, roundEndTalent, "on_round_end", target, "round_start_retrigger", owner, 0, processed, depth + 1);
                    }
                }
            }
        }

        private void ApplyLeaveTaggedStats(RunState runState, BoardUnitState owner, SkillDefinition talent, HashSet<string> processed, int depth)
        {
            var tags = talent.targetTags ?? Array.Empty<string>();
            foreach (var unit in runState.boardUnits.Where(unit => tags.Any(tag => HasTag(unit, tag))))
            {
                AddStat(runState, unit, "attack", talent.attack, owner, processed, depth);
                AddStat(runState, unit, "power", talent.power, owner, processed, depth);
            }

            foreach (var card in runState.handCards.Where(card => tags.Any(tag => HasTag(card, tag))))
            {
                AddStat(runState, card, "attack", talent.attack, owner, processed, depth);
                AddStat(runState, card, "power", talent.power, owner, processed, depth);
            }

            foreach (var card in runState.shopCards.Where(card => card != null && tags.Any(tag => HasTag(card, tag))))
            {
                card.shopBuffAttack += talent.attack;
                card.shopBuffPower += talent.power;
            }
        }

        private void DevourShopCard(RunState runState, BoardUnitState owner, SkillDefinition talent, bool highestAttack, HashSet<string> processed, int depth)
        {
            var candidates = runState.shopCards
                .Select((card, index) => new { card, index })
                .Where(entry => entry.card != null)
                .ToList();
            if (candidates.Count == 0)
            {
                return;
            }

            var picked = highestAttack
                ? candidates.OrderByDescending(entry => EffectiveAttack(entry.card)).First()
                : candidates[_random.Next(candidates.Count)];
            var card = _shopSystem.RemoveShopCardForDevour(runState, picked.index);
            if (card == null)
            {
                return;
            }

            _devourShopEvents.Add(new DevourShopEventState
            {
                shopIndex = picked.index,
                devourerSlotId = owner.boardSlotId,
                devourerUnitId = owner.unitId,
                devourerName = owner.name,
                devouredCard = CloneCard(card)
            });

            var stats = talent.stats;
            if (stats != null)
            {
                var ratio = talent.ratio > 0f ? talent.ratio : 1f;
                AddStat(runState, owner, "hp", (int)Math.Round(EffectiveHp(card) * stats.hp * ratio), owner, processed, depth);
                AddStat(runState, owner, "attack", (int)Math.Round(EffectiveAttack(card) * stats.attack * ratio), owner, processed, depth);
                AddStat(runState, owner, "defense", (int)Math.Round(EffectiveDefense(card) * stats.defense * ratio), owner, processed, depth);
                AddStat(runState, owner, "power", (int)Math.Round(EffectivePower(card) * stats.power * ratio), owner, processed, depth);
                AddStat(runState, owner, "speed", (int)Math.Round(EffectiveSpeed(card) * stats.speed * ratio), owner, processed, depth);
                AddStat(runState, owner, "morale", (int)Math.Round(EffectiveMorale(card) * stats.morale * ratio), owner, processed, depth);
            }
            else
            {
                AddStat(runState, owner, "attack", EffectiveAttack(card) * Math.Max(1, talent.multiplier), owner, processed, depth);
            }

            Dispatch(runState, "on_devour", owner, "devour_shop", owner, 0, processed, depth + 1);
        }

        private static UnitCardState CloneCard(UnitCardState card)
        {
            if (card == null)
            {
                return null;
            }

            return new UnitCardState
            {
                unitId = card.unitId,
                name = card.name,
                star = card.star,
                isGolden = card.isGolden,
                shopPoolCost = card.shopPoolCost,
                shopPoolReserved = card.shopPoolReserved,
                shopPoolContribution = card.shopPoolContribution,
                fromShopPurchase = card.fromShopPurchase,
                shopBuffHp = card.shopBuffHp,
                shopBuffAttack = card.shopBuffAttack,
                shopBuffDefense = card.shopBuffDefense,
                shopBuffPower = card.shopBuffPower,
                shopBuffSpeed = card.shopBuffSpeed,
                shopBuffLuck = card.shopBuffLuck,
                shopBuffMorale = card.shopBuffMorale,
                roundTempAttack = card.roundTempAttack,
                roundTempPower = card.roundTempPower,
                roundTempMorale = card.roundTempMorale,
                forestGemsAttached = card.forestGemsAttached,
                forestGemsReceived = card.forestGemsReceived
            };
        }

        private void GainForestGem(RunState runState, UnitCardState owner, int amount, UnitCardState source, HashSet<string> processed, int depth)
        {
            if (amount <= 0)
            {
                return;
            }

            runState.manageResources.forestGems += amount;
            Dispatch(runState, "on_gain_forest_gem", owner, "gain_forest_gem", source, amount, processed, depth + 1);
        }

        private void GiftForestGem(RunState runState, UnitCardState source, UnitCardState target, int amount, HashSet<string> processed, int depth)
        {
            if (target == null || amount <= 0)
            {
                return;
            }

            target.forestGemsAttached += amount;
            target.forestGemsReceived += amount;
            runState.manageResources.forestGiftActions += 1;
            runState.manageResources.forestGiftTotal += amount;
            runState.manageResources.forestGiftRoundActions += 1;
            runState.manageResources.forestGiftRoundTotal += amount;
            _feedbackEvents.forestGemGiftEvents.Add(new ForestGemGiftEventState
            {
                sourceSlotId = source is BoardUnitState sourceBoard ? sourceBoard.boardSlotId : string.Empty,
                sourceName = source?.name,
                targetSlotId = target is BoardUnitState targetBoard ? targetBoard.boardSlotId : string.Empty,
                targetName = target.name,
                amount = amount
            });
            Dispatch(runState, "on_gift_action", target, "gift_forest_gem", source, amount, processed, depth + 1);
            Dispatch(runState, "on_receive_gift", target, "receive_gift", source, amount, processed, depth + 1);
        }

        private void AddStat(RunState runState, UnitCardState target, string stat, int amount, UnitCardState source, HashSet<string> processed, int depth)
        {
            if (target == null || amount == 0)
            {
                return;
            }

            switch (stat)
            {
                case "hp":
                    target.shopBuffHp += amount;
                    break;
                case "attack":
                    target.shopBuffAttack += amount;
                    break;
                case "defense":
                    target.shopBuffDefense += amount;
                    break;
                case "power":
                    target.shopBuffPower += amount;
                    break;
                case "speed":
                    target.shopBuffSpeed += amount;
                    break;
                case "luck":
                    target.shopBuffLuck += amount;
                    break;
                case "morale":
                    target.shopBuffMorale += amount;
                    break;
                default:
                    return;
            }

            var eventType = stat == "attack" ? "on_gain_attack" : stat == "defense" ? "on_gain_defense" : stat == "power" ? "on_gain_power" : null;
            if (eventType != null && amount > 0)
            {
                Dispatch(runState, eventType, target, $"gain_{stat}", source, amount, processed, depth + 1);
            }
        }

        private bool AddUnitToHand(RunState runState, string unitId)
        {
            if (runState.handCards.Count >= HandMaxCount || string.IsNullOrWhiteSpace(unitId))
            {
                return false;
            }

            var unit = ProphecyGameSession.Instance.Data.FindUnit(unitId);
            if (unit == null)
            {
                return false;
            }

            runState.handCards.Add(new UnitCardState { unitId = unit.id, name = unit.name, star = unit.star });
            return true;
        }

        private int AddRandomUnitsToHand(RunState runState, Func<UnitDefinition, bool> predicate, int count)
        {
            var pool = ProphecyGameSession.Instance.Data.Units.Where(predicate).ToList();
            var added = 0;
            for (var i = 0; i < count && runState.handCards.Count < HandMaxCount && pool.Count > 0; i += 1)
            {
                var picked = pool[_random.Next(pool.Count)];
                if (AddUnitToHand(runState, picked.id))
                {
                    added += 1;
                }
            }

            return added;
        }

        private void Evolve(RunState runState, UnitCardState unit, string targetUnitId, UnitCardState source, HashSet<string> processed, int depth)
        {
            var definition = ProphecyGameSession.Instance.Data.FindUnit(targetUnitId);
            if (unit == null || definition == null)
            {
                return;
            }

            var oldName = unit.name;
            unit.unitId = definition.id;
            unit.name = definition.name;
            unit.star = definition.star;
            _feedbackEvents.evolveEvents.Add(new UnitEvolveEventState
            {
                slotId = unit is BoardUnitState board ? board.boardSlotId : string.Empty,
                oldName = oldName,
                newName = unit.name
            });
            Dispatch(runState, "on_instant_evolve", unit, "instant_evolve", source, 0, processed, depth + 1);
        }

        private static bool Handles(SkillDefinition talent, string eventType)
        {
            if (talent == null || string.IsNullOrWhiteSpace(talent.kind))
            {
                return false;
            }

            switch (talent.kind)
            {
                case "while_on_board_on_entry_race_self_gain_attack":
                case "while_on_board_on_entry_race_gain_stats":
                case "while_on_board_on_entry_tagged_units_gain_attack":
                case "on_entry_random_race_units_gain_power":
                case "while_on_board_on_entry_same_id_tagged_units_gain_power":
                case "while_on_board_every_n_entry_race_add_random_unit_to_hand":
                case "enter_if_board_faith_count_discover":
                case "on_entry_gain_forest_gem_self":
                case "on_entry_any_unit_gain_forest_gem_self":
                case "on_entry_side_units_gift_forest_gem":
                case "on_entry_devour_random_shop_gain_stats":
                case "while_on_board_on_entry_race_devour_shop_gain_attack":
                case "on_entry_race_units_devour_shop_gain_attack":
                case "on_entry_add_unit_to_hand":
                case "on_entry_shop_cards_gain_attack":
                case "on_any_entry_effect_triggered_self_gain_attack":
                case "on_entry_board_tagged_units_gain_attack":
                case "on_any_entry_effect_count_evolve":
                case "on_entry_devour_board_tag_copy_gain_attack":
                case "enter_target_unit_permanent_power":
                case "while_on_board_entry_effect_self_and_rear_gain_attack":
                    return eventType == "on_entry";
                case "round_end_if_adjacent_faith_self_gain_attack":
                case "round_end_self_gain_attack_per_faith_count":
                case "round_end_tagged_units_gain_attack_and_defense":
                case "round_end_gain_forest_gem_self":
                case "round_end_self_gain_attack":
                case "round_end_self_temp_morale_per_race_count":
                case "round_end_forward_adjacent_units_gain_attack_and_gift":
                case "round_end_self_gift_forest_gem":
                case "round_end_temp_gain_adjacent_attack":
                case "round_end_devour_shop_highest_attack_gain_attack":
                case "round_end_tagged_units_devour_shop_gain_attack":
                case "round_end_if_no_forest_gem_in_hand_self_gain_attack":
                    return eventType == "on_round_end";
                case "round_start_retrigger_race_round_end_talents":
                case "round_start_if_race_count_temp_power":
                    return eventType == "on_round_start";
                case "leave_board_gain_gold":
                case "leave_board_tagged_units_gain_stats":
                case "on_leave_tag_count_tagged_units_gain_power":
                case "on_leave_gift_forest_gem_team":
                case "on_leave_retrigger_random_race_entry_effects":
                    return eventType == "on_leave";
                case "on_gain_power_self_gain_attack":
                case "on_gain_power_convert_to_attack_random_board_unit":
                    return eventType == "on_gain_power";
                case "on_gain_defense_team_gain_attack":
                    return eventType == "on_gain_defense";
                case "on_gain_stat_retrigger_side_adjacent_entry_effects":
                    return eventType == "on_gain_attack" || eventType == "on_gain_defense";
                case "while_on_board_any_ally_gain_stat_extra_defense":
                case "while_on_board_attack_gain_threshold_add_random_unit_to_hand":
                case "while_on_board_attack_gain_threshold_evolve":
                    return eventType == "on_gain_attack" || eventType == "on_gain_defense" || eventType == "on_gain_power";
                case "while_on_board_self_gain_stat_team_gain_power":
                    return eventType == "on_gain_attack" || eventType == "on_gain_power";
                case "on_receive_gift_self_gain_attack":
                case "on_receive_gift_self_evolve":
                case "on_receive_gift_total_team_gain_power_every_n":
                case "on_receive_gift_total_discover_race_unit_once":
                    return eventType == "on_receive_gift";
                case "on_gain_forest_gem_self_gain_attack":
                case "on_gain_forest_gem_auto_gift_self_team_attack":
                    return eventType == "on_gain_forest_gem";
                case "on_gift_action_team_gain_attack_every_n":
                    return eventType == "on_gift_action";
                case "on_other_sell_absorb_attached_gems_and_gain_attack":
                    return eventType == "on_sell";
                case "on_instant_evolve_self_gift_and_gain_attack":
                    return eventType == "on_instant_evolve";
                case "on_devour_team_gain_attack":
                    return eventType == "on_devour";
                default:
                    return false;
            }
        }

        private static IEnumerable<SkillDefinition> GetTalents(UnitCardState unit)
        {
            var definition = UnitDef(unit);
            if (definition == null)
            {
                return Array.Empty<SkillDefinition>();
            }

            return unit.isGolden ? definition.goldTalents ?? definition.talents ?? Array.Empty<SkillDefinition>() : definition.talents ?? Array.Empty<SkillDefinition>();
        }

        private static bool HasEntryTalent(UnitCardState unit)
        {
            return GetTalents(unit).Any(talent => Handles(talent, "on_entry"));
        }

        private List<T> PickRandom<T>(List<T> candidates, int count)
        {
            var picked = new List<T>();
            for (var i = 0; i < count && candidates.Count > 0; i += 1)
            {
                var index = _random.Next(candidates.Count);
                picked.Add(candidates[index]);
                candidates.RemoveAt(index);
            }

            return picked;
        }

        private static IEnumerable<BoardUnitState> SideAdjacent(RunState runState, BoardUnitState owner)
        {
            if (!TryParseSlot(owner?.boardSlotId, out var row, out var col))
            {
                return Enumerable.Empty<BoardUnitState>();
            }

            return runState.boardUnits.Where(unit =>
                TryParseSlot(unit.boardSlotId, out var targetRow, out var targetCol)
                && targetRow == row
                && Math.Abs(targetCol - col) == 1);
        }

        private static IEnumerable<BoardUnitState> ForwardAdjacent(RunState runState, BoardUnitState owner, string targetMode)
        {
            if (!TryParseSlot(owner?.boardSlotId, out var row, out var col))
            {
                return Enumerable.Empty<BoardUnitState>();
            }

            var forwardRow = row - 1;
            return targetMode == "forward_row_all"
                ? runState.boardUnits.Where(unit => TryParseSlot(unit.boardSlotId, out var unitRow, out _) && unitRow == forwardRow)
                : runState.boardUnits.Where(unit => TryParseSlot(unit.boardSlotId, out var unitRow, out var unitCol) && unitRow == forwardRow && Math.Abs(unitCol - col) <= 1);
        }

        private static IEnumerable<BoardUnitState> SelfAndRearRow(RunState runState, BoardUnitState owner)
        {
            if (!TryParseSlot(owner?.boardSlotId, out var row, out _))
            {
                return owner == null ? Enumerable.Empty<BoardUnitState>() : new[] { owner };
            }

            return runState.boardUnits.Where(unit =>
                unit == owner || (TryParseSlot(unit.boardSlotId, out var unitRow, out _) && unitRow == row + 1));
        }

        private static bool TryParseSlot(string slotId, out int row, out int col)
        {
            row = 0;
            col = 0;
            if (string.IsNullOrWhiteSpace(slotId))
            {
                return false;
            }

            var parts = slotId.Split('-');
            return parts.Length == 2 && int.TryParse(parts[0], out row) && int.TryParse(parts[1], out col);
        }

        private static int BoardOrderIndex(BoardUnitState unit)
        {
            var order = ProphecyGameSession.Instance.Data.Config?.GetBoardOrder() ?? new List<string>();
            for (var i = 0; i < order.Count; i += 1)
            {
                if (order[i] == unit?.boardSlotId)
                {
                    return i;
                }
            }

            return 999;
        }

        private static UnitDefinition UnitDef(UnitCardState unit)
        {
            return unit == null ? null : ProphecyGameSession.Instance.Data.FindUnit(unit.unitId);
        }

        private static bool CountsAsRace(RunState runState, UnitCardState unit, string race)
        {
            if (unit == null || string.IsNullOrWhiteSpace(race))
            {
                return false;
            }

            if (UnitDef(unit)?.race == race)
            {
                return true;
            }

            if (runState == null || !(unit is BoardUnitState boardUnit) || !TryParseSlot(boardUnit.boardSlotId, out var unitRow, out _))
            {
                return false;
            }

            return runState.boardUnits.Any(owner =>
                owner != null
                && TryParseSlot(owner.boardSlotId, out var ownerRow, out _)
                && ownerRow == unitRow
                && GetTalents(owner).Any(talent => talent.kind == "same_row_units_count_as_race" && (string.IsNullOrWhiteSpace(talent.race) || talent.race == race)));
        }

        private static int CountRace(RunState runState, string race, string fallbackRace)
        {
            var targetRace = string.IsNullOrWhiteSpace(race) ? fallbackRace : race;
            return runState.boardUnits.Count(unit => CountsAsRace(runState, unit, targetRace));
        }

        private static bool HasTag(UnitCardState unit, string tag)
        {
            if (unit == null || string.IsNullOrWhiteSpace(tag))
            {
                return false;
            }

            var definition = UnitDef(unit);
            return definition?.tags != null && definition.tags.Contains(tag);
        }

        private static int EffectiveHp(UnitCardState unit) => (UnitDef(unit)?.hp ?? 0) + (unit?.shopBuffHp ?? 0);
        private static int EffectiveAttack(UnitCardState unit) => (UnitDef(unit)?.attack ?? 0) + (unit?.shopBuffAttack ?? 0) + (unit?.roundTempAttack ?? 0);
        private static int EffectiveDefense(UnitCardState unit) => (UnitDef(unit)?.defense ?? 0) + (unit?.shopBuffDefense ?? 0);
        private static int EffectivePower(UnitCardState unit) => (UnitDef(unit)?.power ?? 0) + (unit?.shopBuffPower ?? 0) + (unit?.roundTempPower ?? 0);
        private static int EffectiveSpeed(UnitCardState unit) => (UnitDef(unit)?.speed ?? 0) + (unit?.shopBuffSpeed ?? 0);
        private static int EffectiveMorale(UnitCardState unit) => (UnitDef(unit)?.morale ?? 0) + (unit?.shopBuffMorale ?? 0) + (unit?.roundTempMorale ?? 0);

        private static int Value(SkillDefinition talent, UnitCardState owner, int fallback = 0) => owner.isGolden ? NonZero(talent.goldValue, talent.value, fallback) : NonZero(talent.value, fallback);
        private static int Attack(SkillDefinition talent, UnitCardState owner, int fallback) => owner.isGolden ? NonZero(talent.goldAttack, talent.attack, fallback) : NonZero(talent.attack, fallback);
        private static int Defense(SkillDefinition talent, UnitCardState owner, int fallback) => owner.isGolden ? NonZero(talent.goldDefense, talent.defense, fallback) : NonZero(talent.defense, fallback);
        private static int Hp(SkillDefinition talent, UnitCardState owner, int fallback) => NonZero(talent.hp, fallback);
        private static int Power(SkillDefinition talent, UnitCardState owner, int fallback) => owner.isGolden ? NonZero(talent.goldPower, talent.power, fallback) : NonZero(talent.power, fallback);
        private static int Count(SkillDefinition talent, UnitCardState owner) => owner.isGolden ? NonZero(talent.goldCount, talent.count, 1) : NonZero(talent.count, 1);
        private static int Gift(SkillDefinition talent, UnitCardState owner) => owner.isGolden ? NonZero(talent.goldGift, talent.gift, 0) : NonZero(talent.gift, 0);

        private static int NonZero(params int[] values)
        {
            foreach (var value in values)
            {
                if (value != 0)
                {
                    return value;
                }
            }

            return 0;
        }
    }
}
