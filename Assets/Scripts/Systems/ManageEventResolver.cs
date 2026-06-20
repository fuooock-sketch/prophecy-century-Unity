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
        public const string ForestGemCardId = "forest_gem";
        public const string ForestGemCardName = "密林宝钻";
        public const int ForestGemReinforceCount = 1;
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
                unit.roundTempCount = 0;
                unit.manageRoundEntryEffectTriggerCount = 0;
                unit.manageRoundForestGemGiftBonusCount = 0;
                unit.manageRoundStatRetriggerTriggered = false;
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

        public bool HasTargetedEntryPower(UnitCardState unit)
        {
            return GetTalents(unit).Any(talent => talent.kind == "enter_target_unit_permanent_power");
        }

        public int ResolveTargetedEntryPower(RunState runState, BoardUnitState source, BoardUnitState target)
        {
            if (runState == null || source == null || target == null)
            {
                return 0;
            }

            var talent = GetTalents(source).FirstOrDefault(item => item.kind == "enter_target_unit_permanent_power");
            if (talent == null)
            {
                return 0;
            }

            var value = Value(talent, source);
            _feedbackEvents.entryEffectEvents.Add(new EntryEffectEventState
            {
                targetSlotId = source.boardSlotId,
                targetName = source.name
            });
            GainCount(runState, target, value, source, new HashSet<string>(), 0);
            _abilityTriggered = true;
            return value;
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

        public void RefreshBoardAuras(RunState runState)
        {
            RecomputeBoardAuras(runState);
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
            events.countGainEvents.AddRange(_feedbackEvents.countGainEvents);
            events.entryEffectEvents.AddRange(_feedbackEvents.entryEffectEvents);
            events.handAddEvents.AddRange(_feedbackEvents.handAddEvents);
            events.attackChangeEvents.AddRange(_feedbackEvents.attackChangeEvents);
            events.shopBuffEvents.AddRange(_feedbackEvents.shopBuffEvents);
            _feedbackEvents.forestGemGiftEvents.Clear();
            _feedbackEvents.evolveEvents.Clear();
            _feedbackEvents.countGainEvents.Clear();
            _feedbackEvents.entryEffectEvents.Clear();
            _feedbackEvents.handAddEvents.Clear();
            _feedbackEvents.attackChangeEvents.Clear();
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

            if (eventType == "on_entry" && target is BoardUnitState entryTarget && HasSelfEntryTalent(entryTarget, reason))
            {
                _feedbackEvents.entryEffectEvents.Add(new EntryEffectEventState
                {
                    targetSlotId = entryTarget.boardSlotId,
                    targetName = entryTarget.name
                });
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

            if (depth == 0 && (eventType == "on_entry" || eventType == "on_leave" || eventType == "on_round_start"))
            {
                RecomputeBoardAuras(runState);
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
                        if (talent.count > 0 && owner.manageRoundEntryEffectTriggerCount >= talent.count)
                        {
                            break;
                        }

                        if (talent.count > 0)
                        {
                            owner.manageRoundEntryEffectTriggerCount += 1;
                        }

                        GainCount(runState, owner, Value(talent, owner), target, processed, depth);
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
                        GainCount(runState, owner, Value(talent, owner), owner, processed, depth);
                    }
                    break;
                case "round_end_self_gain_attack_per_faith_count":
                    if (eventType == "on_round_end")
                    {
                        var faith = string.IsNullOrWhiteSpace(talent.faith) ? UnitDef(owner)?.faith : talent.faith;
                        GainCount(runState, owner, Value(talent, owner) * runState.boardUnits.Count(unit => UnitDef(unit)?.faith == faith), owner, processed, depth);
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
                        GainCount(runState, owner, ResolveRoundCountGain(talent, owner), owner, processed, depth);
                    }
                    break;
                case "round_start_if_board_faith_count_discover":
                    if (eventType == "on_round_start" && runState.boardUnits.Count(unit => UnitDef(unit)?.faith == talent.faith) >= Math.Max(1, talent.threshold))
                    {
                        AddRandomUnitsToHand(runState, unit => unit.faith == talent.faith && !unit.hidden, Count(talent, owner), owner);
                    }
                    break;
                case "on_gain_race_unit_self_gain_count":
                    if (eventType == "on_gain_unit" && UnitDef(target)?.race == talent.race)
                    {
                        GainCount(runState, owner, Value(talent, owner), target, processed, depth);
                    }
                    break;
                case "while_on_board_on_entry_tagged_units_gain_attack":
                    if (eventType == "on_entry" && target != owner && CountsAsRace(runState, target, talent.entryRace))
                    {
                        foreach (var unit in runState.boardUnits.Where(unit => HasTag(unit, talent.targetTag)))
                        {
                            GainCount(runState, unit, Value(talent, owner), target, processed, depth);
                        }
                    }
                    break;
                case "on_entry_random_race_units_gain_power":
                    if (eventType == "on_entry" && target == owner)
                    {
                        PickRandom(runState.boardUnits.Where(unit => unit != owner && CountsAsRace(runState, unit, talent.race)).ToList(), Count(talent, owner))
                            .ForEach(unit => GainCount(runState, unit, Value(talent, owner, Power(talent, owner, 1)), owner, processed, depth));
                    }
                    break;
                case "while_on_board_every_n_entry_race_add_random_unit_to_hand":
                    if (eventType == "on_entry" && target != owner && CountsAsRace(runState, target, talent.race))
                    {
                        owner.manageEntryEffectTriggerCount += 1;
                        if (owner.manageEntryEffectTriggerCount >= Math.Max(1, talent.threshold))
                        {
                            owner.manageEntryEffectTriggerCount = 0;
                            AddRandomUnitsToHand(runState, unit => !unit.hidden && unit.race == talent.race, Count(talent, owner), owner);
                        }
                    }
                    break;
                case "while_on_board_on_entry_same_id_tagged_units_gain_power":
                    if (eventType == "on_entry" && target != owner && (string.IsNullOrWhiteSpace(talent.tag) || HasTag(target, talent.tag)))
                    {
                        foreach (var unit in runState.boardUnits.Where(unit => unit.unitId == target.unitId))
                        {
                            GainCount(runState, unit, Value(talent, owner, Power(talent, owner, 1)), target, processed, depth);
                        }
                    }
                    break;
                case "on_gain_power_self_gain_attack":
                    if (eventType == "on_gain_count" && target == owner)
                    {
                        GainCount(runState, owner, Value(talent, owner), owner, processed, depth);
                    }
                    break;
                case "on_faith_gain_count_threshold_self_gain_count":
                    if (eventType == "on_gain_count" && target is BoardUnitState && UnitDef(target)?.faith == talent.faith && eventValue > 0)
                    {
                        owner.manageFaithCountGainBucket += 1;
                        var threshold = Math.Max(1, talent.threshold);
                        while (owner.manageFaithCountGainBucket >= threshold)
                        {
                            owner.manageFaithCountGainBucket -= threshold;
                            GainCount(runState, owner, Value(talent, owner), owner, processed, depth);
                        }
                    }
                    break;
                case "on_gain_defense_team_gain_attack":
                    if (eventType == "on_gain_count" && target is BoardUnitState && source != owner)
                    {
                        foreach (var unit in runState.boardUnits)
                        {
                            GainCount(runState, unit, Value(talent, owner, 1), owner, processed, depth);
                        }
                    }
                    break;
                case "while_on_board_any_ally_gain_stat_extra_defense":
                    if (eventType == "on_gain_count" && target is BoardUnitState)
                    {
                        GainCount(runState, target, Value(talent, owner, Defense(talent, owner, 1)), owner, processed, depth);
                    }
                    break;
                case "while_on_board_attack_gain_threshold_add_random_unit_to_hand":
                    if (eventType == "on_gain_count" && target == owner && eventValue > 0 && !owner.manageRoundAttackRewardTriggered)
                    {
                        owner.manageAttackGainBucket += eventValue;
                        if (owner.manageAttackGainBucket >= Math.Max(1, talent.threshold))
                        {
                            owner.manageRoundAttackRewardTriggered = AddRandomUnitsToHand(runState, unit => !unit.hidden && unit.race == talent.race, Count(talent, owner), owner) > 0;
                        }
                    }
                    break;
                case "while_on_board_attack_gain_threshold_evolve":
                    if (eventType == "on_gain_count" && target is BoardUnitState && eventValue > 0)
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
                    if (eventType == "on_gain_count" && target == owner)
                    {
                        foreach (var unit in runState.boardUnits.Where(unit => unit != owner))
                        {
                            GainCount(runState, unit, Value(talent, owner, 1), owner, processed, depth);
                        }
                    }
                    break;
                case "on_gain_power_convert_to_attack_random_board_unit":
                    if (eventType == "on_gain_count" && target == owner && eventValue > 0)
                    {
                        var recipient = PickRandom(runState.boardUnits.ToList(), 1).FirstOrDefault();
                        GainCount(runState, recipient, eventValue * Value(talent, owner, 1), owner, processed, depth);
                    }
                    break;
                case "on_gain_count_transfer_to_random_other_allies":
                    if (eventType == "on_gain_count" && target == owner && eventValue > 0)
                    {
                        var recipients = runState.boardUnits.Where(unit => unit != owner).ToList();
                        if (recipients.Count == 0)
                        {
                            break;
                        }

                        RemoveGainedCount(owner, eventValue);
                        foreach (var recipient in PickRandom(recipients, Count(talent, owner)))
                        {
                            GainCount(runState, recipient, eventValue, owner, processed, depth);
                        }
                    }
                    break;
                case "on_gain_stat_retrigger_side_adjacent_entry_effects":
                    if (eventType == "on_gain_count" && target == owner && !owner.manageRoundStatRetriggerTriggered)
                    {
                        owner.manageRoundStatRetriggerTriggered = true;
                        foreach (var unit in PickRandom(SideAdjacent(runState, owner).Where(unit => HasEntryTalent(unit)).ToList(), Count(talent, owner)))
                        {
                            Dispatch(runState, "on_entry", unit, "gain_stat_retrigger_adjacent_entry", owner, 0, processed, depth + 1);
                        }
                    }
                    break;
                case "enter_if_board_faith_count_discover":
                    if (eventType == "on_entry" && target == owner && runState.boardUnits.Count(unit => UnitDef(unit)?.faith == talent.faith) >= Math.Max(1, talent.threshold))
                    {
                        AddRandomUnitsToHand(runState, unit => unit.faith == talent.faith && !unit.hidden, Count(talent, owner), owner);
                    }
                    break;
                case "round_end_tagged_units_gain_attack_and_defense":
                    if (eventType == "on_round_end")
                    {
                        foreach (var unit in runState.boardUnits.Where(unit => HasTag(unit, talent.targetTag)))
                        {
                            GainCount(runState, unit, Value(talent, owner, 1), owner, processed, depth);
                        }
                    }
                    break;
                case "round_end_tagged_units_gain_count":
                    if (eventType == "on_round_end")
                    {
                        foreach (var unit in runState.boardUnits.Where(unit => HasTag(runState, unit, talent.targetTag)))
                        {
                            GainCount(runState, unit, Value(talent, owner, 1), owner, processed, depth);
                        }
                    }
                    break;
                case "leave_board_gain_gold":
                    if (eventType == "on_leave" && target == owner)
                    {
                        runState.gold += Value(talent, owner);
                        var recipient = PickRandom(runState.boardUnits.ToList(), 1).FirstOrDefault();
                        GainCount(runState, recipient, Value(talent, owner, Power(talent, owner, 1)), owner, processed, depth);
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
                            GainCount(runState, unit, tagCount * Value(talent, owner, Power(talent, owner, 1)), owner, processed, depth);
                        }
                    }
                    break;
                case "on_leave_retrigger_random_race_entry_effects":
                    if (eventType == "on_leave" && target == owner)
                    {
                        var excluded = new HashSet<string>(talent.excludeUnitIds ?? Array.Empty<string>());
                        var candidates = runState.boardUnits
                            .Where(unit => unit != owner && CountsAsRace(runState, unit, talent.race) && !excluded.Contains(unit.unitId) && HasSelfEntryTalent(unit, "retrigger"))
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
                case "on_leave_gain_forest_gem_hand":
                    if (eventType == "on_leave" && target == owner)
                    {
                        GainForestGem(runState, owner, Value(talent, owner), owner, processed, depth);
                    }
                    break;
                case "round_end_self_gain_attack":
                    if (eventType == "on_round_end")
                    {
                        GainCount(runState, owner, Value(talent, owner), owner, processed, depth);
                    }
                    break;
                case "round_end_if_race_count_self_gain_attack":
                    if (eventType == "on_round_end" && CountRace(runState, talent.race, UnitDef(owner)?.race) >= Math.Max(1, talent.threshold))
                    {
                        GainCount(runState, owner, Value(talent, owner), owner, processed, depth);
                    }
                    break;
                case "round_end_if_race_count_self_gain_round_count":
                    if (eventType == "on_round_end" && CountRace(runState, talent.race, UnitDef(owner)?.race) >= Math.Max(1, talent.threshold))
                    {
                        var gain = ResolveRoundCountGain(talent, owner);
                        if (gain > 0)
                        {
                            owner.roundTempCount += gain;
                            AddCountGainFeedback(owner, owner, gain, "临时");
                        }
                    }
                    break;
                case "round_end_same_row_tagged_units_gain_count":
                    if (eventType == "on_round_end")
                    {
                        foreach (var unit in SameRow(runState, owner).Where(unit => HasTag(runState, unit, talent.targetTag)))
                        {
                            GainCount(runState, unit, Value(talent, owner, 1), owner, processed, depth);
                        }
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
                        GainCount(runState, owner, Value(talent, owner) * Math.Max(0, eventValue), owner, processed, depth);
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
                        owner.manageReceiveGiftDiscoverTriggered = AddRandomUnitsToHand(runState, unit => !unit.hidden && unit.race == (string.IsNullOrWhiteSpace(talent.race) ? UnitDef(owner)?.race : talent.race), Count(talent, owner), owner) > 0;
                    }
                    break;
                case "on_gain_forest_gem_self_gain_attack":
                    if (eventType == "on_gain_forest_gem" && target == owner)
                    {
                        GainCount(runState, owner, Value(talent, owner) * Math.Max(0, eventValue), owner, processed, depth);
                    }
                    break;
                case "round_end_if_no_forest_gem_in_hand_self_gain_attack":
                    if (eventType == "on_round_end" && !HasForestGemInHand(runState))
                    {
                        GainCount(runState, owner, Value(talent, owner, Attack(talent, owner, 1)), owner, processed, depth);
                    }
                    break;
                case "on_gain_forest_gem_auto_gift_self_team_attack":
                    if (eventType == "on_gain_forest_gem")
                    {
                        var amount = Math.Min(CountForestGemInHand(runState), Math.Max(0, eventValue));
                        if (amount > 0)
                        {
                            RemoveForestGemCards(runState, amount);
                            GiftForestGem(runState, owner, owner, amount, processed, depth);
                        }

                        foreach (var unit in runState.boardUnits)
                        {
                            GainCount(runState, unit, Value(talent, owner, Attack(talent, owner, 0)), owner, processed, depth);
                        }
                    }
                    break;
                case "round_end_forward_adjacent_units_gain_attack_and_gift":
                    if (eventType == "on_round_end")
                    {
                        foreach (var unit in ForwardAdjacent(runState, owner, talent.targetMode))
                        {
                            GainCount(runState, unit, Value(talent, owner, Attack(talent, owner, 0)), owner, processed, depth);
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
                case "round_end_self_and_rear_rows_gain_count_retrigger_tag_round_end":
                    if (eventType == "on_round_end")
                    {
                        foreach (var unit in SelfAndRearRows(runState, owner))
                        {
                            GainCount(runState, unit, Value(talent, owner, 1), owner, processed, depth);
                        }

                        RetriggerTaggedRoundEndTalents(runState, owner, talent, processed, depth);
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
                                GainCount(runState, unit, gain, owner, processed, depth);
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

                        GainCount(runState, owner, Value(talent, owner), owner, processed, depth);
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
                                GainCount(runState, unit, gain, owner, processed, depth);
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
                            GainCount(runState, unit, Value(talent, owner, Attack(talent, owner, 0)), owner, processed, depth);
                        }
                    }
                    break;
                case "on_entry_add_unit_to_hand":
                    if (eventType == "on_entry" && target == owner)
                    {
                        for (var i = 0; i < Count(talent, owner); i += 1)
                        {
                            AddUnitToHand(runState, talent.unitId, owner);
                        }
                    }
                    break;
                case "on_entry_shop_default_count_permanent":
                    if (eventType == "on_entry" && target == owner)
                    {
                        var bonus = Value(talent, owner);
                        runState.manageResources.shopGeneratedCountBonus += bonus;
                        var buffEvent = new ShopBuffEventState
                        {
                            sourceSlotId = owner.boardSlotId,
                            sourceName = owner.name,
                            count = bonus
                        };
                        foreach (var card in runState.shopCards.Where(card => card != null))
                        {
                            ReinforceUnit(card, bonus);
                        }

                        for (var i = 0; i < runState.shopCards.Count; i += 1)
                        {
                            if (runState.shopCards[i] != null)
                            {
                                buffEvent.shopIndices.Add(i);
                            }
                        }

                        if (buffEvent.count > 0 && buffEvent.shopIndices.Count > 0)
                        {
                            _feedbackEvents.shopBuffEvents.Add(buffEvent);
                        }
                    }
                    break;
                case "on_entry_shop_cards_gain_attack":
                    if (eventType == "on_entry" && target == owner)
                    {
                        var gain = Value(talent, owner, Attack(talent, owner, 1));
                        var buffEvent = new ShopBuffEventState
                        {
                            sourceSlotId = owner.boardSlotId,
                            sourceName = owner.name,
                            count = gain
                        };
                        foreach (var card in runState.shopCards.Where(card => card != null))
                        {
                            ReinforceUnit(card, gain);
                        }

                        for (var i = 0; i < runState.shopCards.Count; i += 1)
                        {
                            if (runState.shopCards[i] != null)
                            {
                                buffEvent.shopIndices.Add(i);
                            }
                        }

                        if (buffEvent.count > 0 && buffEvent.shopIndices.Count > 0)
                        {
                            _feedbackEvents.shopBuffEvents.Add(buffEvent);
                        }
                    }
                    break;
                case "on_any_entry_effect_triggered_self_gain_attack":
                    if (eventType == "on_entry" && target != owner && HasSelfEntryTalent(target, reason))
                    {
                        GainCount(runState, owner, Value(talent, owner, Attack(talent, owner, 0)), target, processed, depth);
                    }
                    break;
                case "round_end_temp_gain_adjacent_attack":
                    if (eventType == "on_round_end")
                    {
                        var gain = ResolveMushroomQuakuTempCount(runState, owner, talent);
                        if (gain > 0)
                        {
                            owner.roundTempCount += gain;
                            _feedbackEvents.countGainEvents.Add(new CountGainEventState
                            {
                                sourceSlotId = owner.boardSlotId,
                                sourceName = owner.name,
                                targetSlotId = owner.boardSlotId,
                                targetName = owner.name,
                                amount = gain
                            });
                        }
                    }
                    break;
                case "on_entry_board_tagged_units_gain_attack":
                    if (eventType == "on_entry" && target == owner)
                    {
                        foreach (var unit in runState.boardUnits.Where(unit => unit != owner && HasTag(unit, talent.targetTag)))
                        {
                            GainCount(runState, unit, Value(talent, owner, Attack(talent, owner, 0)), owner, processed, depth);
                        }
                    }
                    break;
                case "on_any_entry_effect_count_evolve":
                    if (eventType == "on_entry" && target != owner && HasSelfEntryTalent(target, reason))
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
                        GainCount(runState, unit, Value(talent, owner), owner, processed, depth);
                    }
                    break;
                case "while_on_board_entry_effect_self_and_rear_gain_attack":
                    if (eventType == "on_entry" && target != owner && HasSelfEntryTalent(target, reason))
                    {
                        foreach (var unit in SelfAndRearRow(runState, owner))
                        {
                            GainCount(runState, unit, Value(talent, owner, Attack(talent, owner, 0)), target, processed, depth);
                        }
                    }
                    break;
                case "on_instant_evolve_self_gift_and_gain_attack":
                    if (eventType == "on_instant_evolve" && target == owner)
                    {
                        GiftForestGem(runState, owner, owner, Gift(talent, owner), processed, depth);
                        GainForestGem(runState, owner, owner.isGolden ? talent.goldGain : talent.gain, owner, processed, depth);
                        GainCount(runState, owner, Value(talent, owner, Attack(talent, owner, 0)), owner, processed, depth);
                    }
                    break;
                case "on_entry_devour_board_tag_copy_gain_attack":
                    if (eventType == "on_entry" && target == owner)
                    {
                        foreach (var unit in PickRandom(runState.boardUnits.Where(unit => unit != owner && HasTag(unit, talent.targetTag)).ToList(), Count(talent, owner)))
                        {
                            var gain = Math.Max(1, unit.baseCount > 0 ? unit.baseCount : ResolveStartCount(UnitDef(unit)));
                            GainCount(runState, owner, gain, owner, processed, depth);
                            Dispatch(runState, "on_devour", owner, "devour_board_tag_copy", owner, gain, processed, depth + 1);
                        }
                    }
                    break;
            }
        }

        private void RetriggerRaceRoundEndTalents(RunState runState, BoardUnitState owner, SkillDefinition talent, HashSet<string> processed, int depth)
        {
            var times = Math.Max(1, owner.isGolden ? NonZero(talent.goldTimes, talent.times, 1) : NonZero(talent.times, 1));
            var targets = runState.boardUnits.Where(unit => unit != owner && CountsAsRace(runState, unit, talent.race) && HasSelfEntryTalent(unit, "retrigger")).ToList();
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

        private void RetriggerTaggedRoundEndTalents(RunState runState, BoardUnitState owner, SkillDefinition talent, HashSet<string> processed, int depth)
        {
            var times = Math.Max(1, owner.isGolden ? NonZero(talent.goldTimes, talent.times, 1) : NonZero(talent.times, 1));
            var targets = runState.boardUnits.Where(unit => HasTag(runState, unit, talent.targetTag)).ToList();
            for (var i = 0; i < times; i += 1)
            {
                foreach (var target in targets)
                {
                    foreach (var roundEndTalent in GetTalents(target).Where(item => Handles(item, "on_round_end") && item.kind != talent.kind))
                    {
                        HandleTalent(runState, target, roundEndTalent, "on_round_end", target, "round_end_retrigger_tagged", owner, 0, processed, depth + 1);
                    }
                }
            }
        }

        private void ApplyLeaveTaggedStats(RunState runState, BoardUnitState owner, SkillDefinition talent, HashSet<string> processed, int depth)
        {
            var tags = talent.targetTags ?? Array.Empty<string>();
            foreach (var unit in runState.boardUnits.Where(unit => tags.Any(tag => HasTag(unit, tag))))
            {
                GainCount(runState, unit, Value(talent, owner, NonZero(talent.power, talent.attack, 1)), owner, processed, depth);
            }

            foreach (var card in runState.handCards.Where(card => tags.Any(tag => HasTag(card, tag))))
            {
                ReinforceUnit(card, Value(talent, owner, NonZero(talent.power, talent.attack, 1)));
            }

            foreach (var card in runState.shopCards.Where(card => card != null && tags.Any(tag => HasTag(card, tag))))
            {
                ReinforceUnit(card, Value(talent, owner, NonZero(talent.power, talent.attack, 1)));
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

            var gainedCount = 0;
            var stats = talent.stats;
            if (stats != null)
            {
                var ratio = talent.ratio > 0f ? talent.ratio : 1f;
                var cardCount = Math.Max(1, card.baseCount > 0 ? card.baseCount : ResolveStartCount(UnitDef(card)));
                var multiplier = Math.Max(1, NonZero(stats.attack, stats.power, stats.hp, talent.multiplier, 1));
                gainedCount = Math.Max(1, (int)Math.Round(cardCount * multiplier * ratio));
                GainCount(runState, owner, gainedCount, owner, processed, depth, true);
                AddStat(runState, owner, "defense", (int)Math.Round(EffectiveDefense(card) * stats.defense * ratio), owner, processed, depth);
                AddStat(runState, owner, "speed", (int)Math.Round(EffectiveSpeed(card) * stats.speed * ratio), owner, processed, depth);
                AddStat(runState, owner, "morale", (int)Math.Round(EffectiveMorale(card) * stats.morale * ratio), owner, processed, depth);
            }
            else
            {
                var cardCount = Math.Max(1, card.baseCount > 0 ? card.baseCount : ResolveStartCount(UnitDef(card)));
                gainedCount = cardCount * Math.Max(1, talent.multiplier);
                GainCount(runState, owner, gainedCount, owner, processed, depth, true);
            }

            _devourShopEvents.Add(new DevourShopEventState
            {
                shopIndex = picked.index,
                devourerSlotId = owner.boardSlotId,
                devourerUnitId = owner.unitId,
                devourerName = owner.name,
                devouredCard = CloneCard(card),
                gainedCount = gainedCount
            });

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
                boardAuraAttack = card.boardAuraAttack,
                shopBuffDefense = card.shopBuffDefense,
                shopBuffPower = card.shopBuffPower,
                shopBuffSpeed = card.shopBuffSpeed,
                shopBuffLuck = card.shopBuffLuck,
                shopBuffMorale = card.shopBuffMorale,
                baseCount = card.baseCount,
                maxCount = card.maxCount,
                forestGemCount = card.forestGemCount,
                roundTempCount = card.roundTempCount,
                roundTempAttack = card.roundTempAttack,
                roundTempPower = card.roundTempPower,
                roundTempMorale = card.roundTempMorale,
                forestGemsAttached = card.forestGemsAttached,
                forestGemsReceived = card.forestGemsReceived,
                manageRoundEntryEffectTriggerCount = card.manageRoundEntryEffectTriggerCount,
                manageFaithCountGainBucket = card.manageFaithCountGainBucket,
                manageRoundForestGemGiftBonusCount = card.manageRoundForestGemGiftBonusCount,
                manageRoundStatRetriggerTriggered = card.manageRoundStatRetriggerTriggered,
                pendingNextRoundTempCount = card.pendingNextRoundTempCount
            };
        }

        private void GainForestGem(RunState runState, UnitCardState owner, int amount, UnitCardState source, HashSet<string> processed, int depth)
        {
            if (amount <= 0)
            {
                return;
            }

            var added = AddForestGemCardsToHand(runState, amount, source ?? owner);
            if (added > 0)
            {
                Dispatch(runState, "on_gain_forest_gem", owner, "gain_forest_gem", source, added, processed, depth + 1);
            }
        }

        private void GiftForestGem(RunState runState, UnitCardState source, UnitCardState target, int amount, HashSet<string> processed, int depth)
        {
            if (target == null || amount <= 0)
            {
                return;
            }

            var bonusCount = ResolveForestGemGiftCountBonus(runState, target, source, processed, depth);
            target.forestGemsAttached += amount;
            target.forestGemsReceived += amount;
            target.forestGemCount += amount;
            GainCount(runState, target, amount * (ForestGemReinforceCount + bonusCount), source, processed, depth, true);
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

        private int ResolveForestGemGiftCountBonus(RunState runState, UnitCardState target, UnitCardState source, HashSet<string> processed, int depth)
        {
            if (runState?.boardUnits == null)
            {
                return 0;
            }

            var bonus = 0;
            foreach (var owner in runState.boardUnits.Where(unit => unit != null))
            {
                foreach (var talent in GetTalents(owner).Where(talent => talent.kind == "forest_gem_gift_count_bonus_aura"))
                {
                    if (owner.manageRoundForestGemGiftBonusCount >= Math.Max(1, talent.count > 0 ? talent.count : 5))
                    {
                        continue;
                    }

                    owner.manageRoundForestGemGiftBonusCount += 1;
                    bonus += Math.Max(1, Value(talent, owner));
                    _abilityTriggered = true;
                }
            }

            return bonus;
        }

        public void ResolveHeroBoardLeave(RunState runState, BoardUnitState leavingUnit)
        {
            if (runState == null || leavingUnit == null || runState.heroId != "magic")
            {
                return;
            }

            var targets = PickRandom(runState.boardUnits.Where(unit => unit != null).ToList(), 3);
            if (targets.Count == 0)
            {
                return;
            }

            var processed = new HashSet<string>();
            foreach (var target in targets)
            {
                GainCount(runState, target, 1, leavingUnit, processed, 0);
            }

            _abilityTriggered = true;
        }

        public bool UseForestGemCardOnBoardUnit(RunState runState, int handIndex, string boardSlotId)
        {
            if (runState == null || handIndex < 0 || handIndex >= runState.handCards.Count || string.IsNullOrWhiteSpace(boardSlotId))
            {
                return false;
            }

            var card = runState.handCards[handIndex];
            if (!IsForestGemCard(card))
            {
                return false;
            }

            var target = runState.boardUnits.FirstOrDefault(unit => unit.boardSlotId == boardSlotId);
            if (target == null)
            {
                return false;
            }

            runState.handCards.RemoveAt(handIndex);
            GiftForestGem(runState, card, target, 1, new HashSet<string>(), 0);
            _abilityTriggered = true;
            return true;
        }

        public static bool IsForestGemCard(UnitCardState card)
        {
            return card != null && card.unitId == ForestGemCardId;
        }

        private static bool HasForestGemInHand(RunState runState)
        {
            return CountForestGemInHand(runState) > 0;
        }

        private static int CountForestGemInHand(RunState runState)
        {
            return runState?.handCards?.Count(IsForestGemCard) ?? 0;
        }

        private int AddForestGemCardsToHand(RunState runState, int amount, UnitCardState source)
        {
            if (runState?.handCards == null || amount <= 0)
            {
                return 0;
            }

            var added = 0;
            while (added < amount && runState.handCards.Count < HandMaxCount)
            {
                runState.handCards.Add(CreateForestGemCard());
                var boardSource = source as BoardUnitState;
                _feedbackEvents.handAddEvents.Add(new HandAddEventState
                {
                    sourceSlotId = boardSource != null ? boardSource.boardSlotId : string.Empty,
                    sourceName = source?.name,
                    unitId = ForestGemCardId,
                    unitName = ForestGemCardName,
                    handIndex = runState.handCards.Count - 1
                });
                added += 1;
            }

            return added;
        }

        private static int RemoveForestGemCards(RunState runState, int amount)
        {
            if (runState?.handCards == null || amount <= 0)
            {
                return 0;
            }

            var removed = 0;
            for (var i = runState.handCards.Count - 1; i >= 0 && removed < amount; i -= 1)
            {
                if (!IsForestGemCard(runState.handCards[i]))
                {
                    continue;
                }

                runState.handCards.RemoveAt(i);
                removed += 1;
            }

            return removed;
        }

        public static UnitCardState CreateForestGemCard()
        {
            return new UnitCardState
            {
                unitId = ForestGemCardId,
                name = ForestGemCardName,
                star = 0
            };
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

        private void GainCount(RunState runState, UnitCardState target, int amount, UnitCardState source, HashSet<string> processed, int depth, bool suppressFeedback = false)
        {
            if (target == null || amount <= 0)
            {
                return;
            }

            ReinforceUnit(target, amount);
            if (!suppressFeedback && target is BoardUnitState boardTarget)
            {
                AddCountGainFeedback(source, boardTarget, amount);
            }

            var totalGain = ApplyHeroCountGainBonuses(runState, target, amount, source, suppressFeedback);
            Dispatch(runState, "on_gain_count", target, "gain_count", source, totalGain, processed, depth + 1);
        }

        private int ApplyHeroCountGainBonuses(RunState runState, UnitCardState target, int amount, UnitCardState source, bool suppressFeedback)
        {
            if (runState == null || target == null || amount <= 0 || !(target is BoardUnitState boardTarget) || !runState.boardUnits.Contains(boardTarget))
            {
                return amount;
            }

            var totalGain = amount;
            if (runState.heroId == "james")
            {
                ReinforceUnit(target, 1);
                AddCountGainFeedback(source, boardTarget, 1, "詹姆士+1");
                totalGain += 1;
                _abilityTriggered = true;
            }

            if (runState.heroId == "shalame")
            {
                if (runState.heroState == null)
                {
                    runState.heroState = new HeroRuntimeState();
                }

                runState.heroState.countGainProgress += totalGain;
                var goldGain = runState.heroState.countGainProgress / 20;
                if (goldGain > 0)
                {
                    runState.heroState.countGainProgress %= 20;
                    runState.gold += goldGain;
                    runState.heroState.secondaryResource += goldGain;
                    _abilityTriggered = true;
                }

                runState.heroState.primaryResource = runState.heroState.countGainProgress;
            }

            return totalGain;
        }

        private void AddCountGainFeedback(UnitCardState source, BoardUnitState target, int amount, string label = null)
        {
            if (target == null || amount <= 0)
            {
                return;
            }

            var boardSource = source as BoardUnitState;
            _feedbackEvents.countGainEvents.Add(new CountGainEventState
            {
                sourceSlotId = boardSource != null ? boardSource.boardSlotId : string.Empty,
                sourceName = source?.name,
                targetSlotId = target.boardSlotId,
                targetName = target.name,
                amount = amount,
                label = label
            });
        }

        private void RecomputeBoardAuras(RunState runState)
        {
            if (runState?.boardUnits == null)
            {
                return;
            }

            var before = runState.boardUnits.ToDictionary(unit => unit, unit => unit.boardAuraAttack);
            foreach (var unit in runState.boardUnits)
            {
                unit.boardAuraAttack = 0;
            }

            foreach (var owner in runState.boardUnits)
            {
                foreach (var talent in GetTalents(owner).Where(item => item.kind == "while_on_board_per_ally_id_buff_type_attack"))
                {
                    var allyCount = runState.boardUnits.Count(unit => unit.unitId == talent.allyId);
                    if (allyCount <= 0)
                    {
                        continue;
                    }

                    var amount = NormalizeAuraAttack(Attack(talent, owner, 0)) * allyCount;
                    if (amount == 0)
                    {
                        continue;
                    }

                    foreach (var target in runState.boardUnits.Where(unit => HasTag(unit, talent.targetTag)))
                    {
                        target.boardAuraAttack += amount;
                    }
                }
            }

            foreach (var unit in runState.boardUnits)
            {
                before.TryGetValue(unit, out var oldValue);
                var delta = unit.boardAuraAttack - oldValue;
                if (delta != 0)
                {
                    _feedbackEvents.attackChangeEvents.Add(new AttackChangeEventState
                    {
                        targetSlotId = unit.boardSlotId,
                        targetName = unit.name,
                        amount = delta
                    });
                }
            }
        }

        private static int NormalizeAuraAttack(int raw)
        {
            if (raw == 0)
            {
                return 0;
            }

            return Math.Abs(raw) >= 10 && raw % 10 == 0 ? raw / 10 : raw;
        }

        private static int ResolveRoundCountGain(SkillDefinition talent, UnitCardState owner)
        {
            var baseValue = Value(talent, owner, 1);
            if (talent != null && talent.roundOffset != 0)
            {
                var currentRun = ProphecyGameSession.Instance.CurrentRun;
                var round = Math.Max(1, currentRun != null ? currentRun.round : 1);
                return Math.Max(1, Math.Abs(talent.roundOffset) * round);
            }

            return Math.Max(1, baseValue);
        }

        private static void ReinforceUnit(UnitCardState target, int amount)
        {
            if (target == null || amount <= 0)
            {
                return;
            }

            var definition = UnitDef(target);
            var startCount = ResolveStartCount(definition);
            var current = Math.Max(startCount, target.baseCount > 0 ? target.baseCount : startCount);
            target.baseCount = current + amount;
            target.maxCount = 0;
        }

        private static void RemoveGainedCount(UnitCardState target, int amount)
        {
            if (target == null || amount <= 0)
            {
                return;
            }

            var definition = UnitDef(target);
            var startCount = ResolveStartCount(definition);
            var current = Math.Max(startCount, target.baseCount > 0 ? target.baseCount : startCount);
            target.baseCount = Math.Max(startCount, current - amount);
            target.maxCount = 0;
        }

        private static int ResolveStartCount(UnitDefinition definition)
        {
            if (definition == null)
            {
                return 1;
            }

            return Math.Max(1, definition.defaultCount > 0 ? definition.defaultCount : definition.startCount > 0 ? definition.startCount : definition.baseCount > 0 ? definition.baseCount : 1);
        }

        private bool AddUnitToHand(RunState runState, string unitId, UnitCardState source = null)
        {
            if (runState == null || string.IsNullOrWhiteSpace(unitId))
            {
                return false;
            }

            var unit = ProphecyGameSession.Instance.Data.FindUnit(unitId);
            if (unit == null)
            {
                return false;
            }

            var card = new UnitCardState
            {
                unitId = unit.id,
                name = unit.name,
                star = unit.star,
                baseCount = ResolveStartCount(unit),
                maxCount = 0
            };
            if (runState.handCards == null)
            {
                runState.handCards = new List<UnitCardState>();
            }

            if (runState.pendingHandCards == null)
            {
                runState.pendingHandCards = new List<UnitCardState>();
            }

            if (runState.handCards.Count >= HandMaxCount)
            {
                runState.pendingHandCards.Add(card);
                return true;
            }

            runState.handCards.Add(card);
            var boardSource = source as BoardUnitState;
            _feedbackEvents.handAddEvents.Add(new HandAddEventState
            {
                sourceSlotId = boardSource != null ? boardSource.boardSlotId : string.Empty,
                sourceName = source?.name,
                unitId = unit.id,
                unitName = unit.name,
                handIndex = runState.handCards.Count - 1
            });
            Dispatch(runState, "on_gain_unit", card, "gain_unit", source, 0, new HashSet<string>());
            return true;
        }

        private int AddRandomUnitsToHand(RunState runState, Func<UnitDefinition, bool> predicate, int count, UnitCardState source = null)
        {
            var pool = ProphecyGameSession.Instance.Data.Units.Where(predicate).ToList();
            var added = 0;
            for (var i = 0; i < count && runState.handCards.Count < HandMaxCount && pool.Count > 0; i += 1)
            {
                var picked = pool[_random.Next(pool.Count)];
                if (AddUnitToHand(runState, picked.id, source))
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
                case "on_entry_shop_default_count_permanent":
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
                case "round_end_tagged_units_gain_count":
                case "round_end_gain_forest_gem_self":
                case "round_end_self_gain_attack":
                case "round_end_if_race_count_self_gain_attack":
                case "round_end_if_race_count_self_gain_round_count":
                case "round_end_same_row_tagged_units_gain_count":
                case "round_end_self_temp_morale_per_race_count":
                case "round_end_forward_adjacent_units_gain_attack_and_gift":
                case "round_end_self_gift_forest_gem":
                case "round_end_self_and_rear_rows_gain_count_retrigger_tag_round_end":
                case "round_end_temp_gain_adjacent_attack":
                case "round_end_devour_shop_highest_attack_gain_attack":
                case "round_end_tagged_units_devour_shop_gain_attack":
                case "round_end_if_no_forest_gem_in_hand_self_gain_attack":
                    return eventType == "on_round_end";
                case "round_start_retrigger_race_round_end_talents":
                case "round_start_if_race_count_temp_power":
                case "round_start_if_board_faith_count_discover":
                    return eventType == "on_round_start";
                case "leave_board_gain_gold":
                case "leave_board_tagged_units_gain_stats":
                case "on_leave_tag_count_tagged_units_gain_power":
                case "on_leave_gift_forest_gem_team":
                case "on_leave_gain_forest_gem_hand":
                case "on_leave_retrigger_random_race_entry_effects":
                    return eventType == "on_leave";
                case "on_gain_power_self_gain_attack":
                case "on_gain_power_convert_to_attack_random_board_unit":
                case "on_gain_count_transfer_to_random_other_allies":
                    return eventType == "on_gain_count";
                case "on_gain_race_unit_self_gain_count":
                    return eventType == "on_gain_unit";
                case "on_gain_defense_team_gain_attack":
                    return eventType == "on_gain_count";
                case "on_gain_stat_retrigger_side_adjacent_entry_effects":
                    return eventType == "on_gain_count";
                case "while_on_board_any_ally_gain_stat_extra_defense":
                case "while_on_board_attack_gain_threshold_add_random_unit_to_hand":
                case "while_on_board_attack_gain_threshold_evolve":
                case "on_faith_gain_count_threshold_self_gain_count":
                    return eventType == "on_gain_count";
                case "while_on_board_self_gain_stat_team_gain_power":
                    return eventType == "on_gain_count";
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

        private static bool HasSelfEntryTalent(UnitCardState unit, string reason)
        {
            return GetTalents(unit).Any(talent =>
            {
                switch (talent.kind)
                {
                    case "on_entry_random_race_units_gain_power":
                    case "enter_if_board_faith_count_discover":
                    case "on_entry_gain_forest_gem_self":
                    case "on_entry_side_units_gift_forest_gem":
                    case "on_entry_devour_random_shop_gain_stats":
                    case "on_entry_race_units_devour_shop_gain_attack":
                    case "on_entry_add_unit_to_hand":
                    case "on_entry_shop_cards_gain_attack":
                    case "on_entry_shop_default_count_permanent":
                    case "on_entry_board_tagged_units_gain_attack":
                    case "on_entry_devour_board_tag_copy_gain_attack":
                        return true;
                    case "enter_target_unit_permanent_power":
                        return reason != null && reason.Contains("retrigger");
                    default:
                        return false;
                }
            });
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

        private static IEnumerable<BoardUnitState> SameRow(RunState runState, BoardUnitState owner)
        {
            if (!TryParseSlot(owner?.boardSlotId, out var row, out _))
            {
                return Enumerable.Empty<BoardUnitState>();
            }

            return runState.boardUnits.Where(unit => TryParseSlot(unit.boardSlotId, out var unitRow, out _) && unitRow == row);
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

        private static int ResolveMushroomQuakuTempCount(RunState runState, BoardUnitState owner, SkillDefinition talent)
        {
            if (runState == null || owner == null)
            {
                return 0;
            }

            var candidates = (talent?.mode == "sum" ? SameAndForwardRows(runState, owner) : SideAdjacent(runState, owner))
                .Where(unit => unit != null && unit != owner)
                .Select(CurrentCount)
                .Where(count => count > 0)
                .ToList();
            return candidates.Count == 0 ? 0 : Math.Max(1, candidates.Max() / 2);
        }

        private static IEnumerable<BoardUnitState> SameAndForwardRows(RunState runState, BoardUnitState owner)
        {
            if (!TryParseSlot(owner?.boardSlotId, out var row, out _))
            {
                return Enumerable.Empty<BoardUnitState>();
            }

            return runState.boardUnits.Where(unit =>
                TryParseSlot(unit.boardSlotId, out var unitRow, out _)
                && (unitRow == row || unitRow == row - 1));
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

        private static IEnumerable<BoardUnitState> SelfAndRearRows(RunState runState, BoardUnitState owner)
        {
            if (!TryParseSlot(owner?.boardSlotId, out var row, out _))
            {
                return owner == null ? Enumerable.Empty<BoardUnitState>() : new[] { owner };
            }

            return runState.boardUnits.Where(unit => TryParseSlot(unit.boardSlotId, out var unitRow, out _) && unitRow >= row);
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
                && GetTalents(owner).Any(talent =>
                    talent.kind == "same_row_units_count_as_race"
                    && (string.IsNullOrWhiteSpace(talent.race) || talent.race == race)
                    && (ownerRow == unitRow || (talent.mode == "same_and_forward_rows" && unitRow == ownerRow - 1))));
        }

        private static int CountRace(RunState runState, string race, string fallbackRace)
        {
            var targetRace = string.IsNullOrWhiteSpace(race) ? fallbackRace : race;
            return runState.boardUnits.Count(unit => CountsAsRace(runState, unit, targetRace));
        }

        private static bool HasTag(UnitCardState unit, string tag)
        {
            return HasTag(null, unit, tag);
        }

        private static bool HasTag(RunState runState, UnitCardState unit, string tag)
        {
            if (unit == null || string.IsNullOrWhiteSpace(tag))
            {
                return false;
            }

            var definition = UnitDef(unit);
            if (definition != null
                && ((definition.tags != null && definition.tags.Contains(tag))
                    || definition.typeLabel == tag
                    || definition.type == tag
                    || definition.race == tag
                    || definition.faith == tag))
            {
                return true;
            }

            return CountsAsTag(runState, unit, tag);
        }

        private static bool CountsAsTag(RunState runState, UnitCardState unit, string tag)
        {
            if (runState == null || !(unit is BoardUnitState boardUnit) || !TryParseSlot(boardUnit.boardSlotId, out var unitRow, out _))
            {
                return false;
            }

            return runState.boardUnits.Any(owner =>
                owner != null
                && TryParseSlot(owner.boardSlotId, out var ownerRow, out _)
                && ownerRow == unitRow
                && GetTalents(owner).Any(talent =>
                    talent.kind == "same_row_units_count_as_tag"
                    && (string.IsNullOrWhiteSpace(talent.targetTag) || talent.targetTag == tag)));
        }

        private static int EffectiveHp(UnitCardState unit) => (UnitDef(unit)?.hp ?? 0) + (unit?.shopBuffHp ?? 0);
        private static int CurrentCount(UnitCardState unit)
        {
            if (unit == null)
            {
                return 0;
            }

            var startCount = ResolveStartCount(UnitDef(unit));
            return Math.Max(1, (unit.baseCount > 0 ? unit.baseCount : startCount) + unit.roundTempCount);
        }

        private static int EffectiveAttack(UnitCardState unit) => (UnitDef(unit)?.attack ?? 0) + (unit?.shopBuffAttack ?? 0) + (unit?.roundTempAttack ?? 0) + (unit?.boardAuraAttack ?? 0);
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
