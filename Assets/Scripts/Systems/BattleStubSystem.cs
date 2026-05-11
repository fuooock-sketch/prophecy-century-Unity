using System;
using System.Collections.Generic;
using System.Linq;
using ProphecyCentury.Core;
using ProphecyCentury.Data;
using ProphecyCentury.Model;

namespace ProphecyCentury.Systems
{
    public sealed class BattleStubSystem
    {
        private const float StepSeconds = 0.1f;

        public BattleStubResult Resolve(RunState runState)
        {
            var random = new Random(runState.round * 7919 + runState.boardUnits.Count * 131);
            var config = ProphecyGameSession.Instance.Data.Config;
            var battleTime = Math.Max(5, config?.battleTime ?? 20);
            var players = BuildPlayerUnits(runState);
            var enemies = BuildEnemyUnits(runState, random);
            ResolveBattleStart(players, enemies, random);
            ResolveBattleStart(enemies, players, random);
            ApplyContinuousAuras(players);
            ApplyContinuousAuras(enemies);
            var playerScore = EstimateScore(players);
            var enemyScore = EstimateScore(enemies);

            if (players.Count == 0)
            {
                return Finish(runState, false, playerScore, enemyScore, 15, 0, players, enemies);
            }

            var elapsed = 0f;
            var attacks = 0;
            while (elapsed < battleTime && players.Any(unit => unit.IsAlive) && enemies.Any(unit => unit.IsAlive))
            {
                TickTimedSkills(players, enemies, random);
                TickTimedSkills(enemies, players, random);
                TickSide(players, enemies, random, ref attacks);
                TickSide(enemies, players, random, ref attacks);
                TickBattleState(players);
                TickBattleState(enemies);
                ApplyContinuousAuras(players);
                ApplyContinuousAuras(enemies);
                elapsed += StepSeconds;
            }

            var playerAlive = players.Any(unit => unit.IsAlive);
            var enemyAlive = enemies.Any(unit => unit.IsAlive);
            var victory = playerAlive && !enemyAlive;
            if (playerAlive == enemyAlive)
            {
                victory = TotalAliveHp(players) >= TotalAliveHp(enemies);
            }

            var damage = victory ? 0 : CalculateHpLoss(runState, enemies);
            return Finish(runState, victory, playerScore, enemyScore, damage, attacks, players, enemies);
        }

        public static int EstimatePlayerScore(RunState runState)
        {
            var units = BuildPlayerUnits(runState);
            ResolveBattleStart(units, new List<BattleRuntimeUnit>(), new Random(17));
            ApplyContinuousAuras(units);
            return EstimateScore(units);
        }

        public static int EstimateEnemyScore(RunState runState)
        {
            var random = new Random(runState.round * 7919 + runState.boardUnits.Count * 131);
            var units = BuildEnemyUnits(runState, random);
            ResolveBattleStart(units, new List<BattleRuntimeUnit>(), random);
            ApplyContinuousAuras(units);
            return EstimateScore(units);
        }

        private static void TickSide(List<BattleRuntimeUnit> attackers, List<BattleRuntimeUnit> defenders, Random random, ref int attacks)
        {
            foreach (var attacker in attackers.Where(unit => unit.IsAlive))
            {
                attacker.Cooldown -= StepSeconds;
                if (attacker.Cooldown > 0f)
                {
                    continue;
                }

                if (attacker.StunRemaining > 0f)
                {
                    attacker.Cooldown += StepSeconds;
                    continue;
                }

                var target = PickTarget(attacker, defenders);
                if (target == null)
                {
                    continue;
                }

                ApplyAttack(attacker, target, attackers, defenders, random, false, false, true);
                attacks += 1;
                attacker.Cooldown += Math.Max(0.2f, attacker.AttackInterval);

                var moraleExtraChance = Clamp01(attacker.Morale * (ProphecyGameSession.Instance.Data.Config?.moraleExtraAttackRate ?? 0.06f));
                if (target.IsAlive && random.NextDouble() < moraleExtraChance)
                {
                    ApplyAttack(attacker, target, attackers, defenders, random, false, true, false);
                    attacker.MoraleExtraCount += 1;
                    attacks += 1;
                }

                if (target.IsAlive)
                {
                    var counterChance = Clamp01(target.Morale * (ProphecyGameSession.Instance.Data.Config?.moraleCounterRate ?? 0.04f));
                    if (random.NextDouble() < counterChance)
                    {
                        ApplyAttack(target, attacker, defenders, attackers, random, true, false, false);
                        target.CounterCount += 1;
                        attacks += 1;
                    }
                }
            }
        }

        private static BattleRuntimeUnit PickTarget(BattleRuntimeUnit attacker, IEnumerable<BattleRuntimeUnit> defenders)
        {
            return defenders
                .Where(unit => unit.IsAlive)
                .OrderBy(unit => attacker.PreferLowestHp ? unit.CurrentHp : 0)
                .ThenByDescending(unit => attacker.PreferBackline ? unit.Row : 0)
                .ThenBy(unit => Distance(attacker, unit))
                .ThenByDescending(unit => unit.Row)
                .ThenBy(unit => unit.CurrentHp)
                .FirstOrDefault();
        }

        private static void ApplyAttack(BattleRuntimeUnit attacker, BattleRuntimeUnit target, List<BattleRuntimeUnit> allies, List<BattleRuntimeUnit> enemies, Random random, bool isCounter, bool isMoraleExtra, bool isPrimaryAttack)
        {
            if (attacker == null || target == null || !attacker.IsAlive || !target.IsAlive)
            {
                return;
            }

            ResolvePreAttack(attacker, random, out var forceCrit, out var critMultiplier);
            var damage = Math.Max(1, attacker.Attack + attacker.Power * 8 - target.Defense);
            var critRate = Math.Min(ProphecyGameSession.Instance.Data.Config?.critRateCap ?? 0.6f, Math.Max(0f, attacker.Luck * 0.025f));
            var didCrit = forceCrit || random.NextDouble() < critRate;
            if (didCrit)
            {
                damage = (int)Math.Ceiling(damage * Math.Max(critMultiplier, ProphecyGameSession.Instance.Data.Config?.critDamageMultiple ?? 1.5f));
                foreach (var ally in allies.Where(unit => unit.IsAlive && unit != attacker))
                {
                    foreach (var skill in GetBattleSkills(ally).Where(skill => skill.kind == "on_ally_crit_self_temp_power"))
                    {
                        ally.Power += Math.Max(1, skill.value);
                        ally.SkillTriggers += 1;
                    }
                }
            }

            var actualDamage = DealDamage(attacker, target, damage, allies, enemies, random);
            if (actualDamage <= 0)
            {
                return;
            }

            attacker.AttackCount += isPrimaryAttack ? 1 : 0;
            ResolveOnAttack(attacker, target, allies, enemies, random, actualDamage, isPrimaryAttack);
        }

        private static int DealDamage(BattleRuntimeUnit source, BattleRuntimeUnit target, int damage, List<BattleRuntimeUnit> sourceAllies, List<BattleRuntimeUnit> targetAllies, Random random)
        {
            if (target.ShieldLayers > 0)
            {
                target.ShieldLayers -= 1;
                return 0;
            }

            if (target.InvincibleRemaining > 0f)
            {
                return 0;
            }

            var before = target.CurrentHp;
            target.CurrentHp = Math.Max(0, target.CurrentHp - Math.Max(1, damage));
            var actualDamage = before - target.CurrentHp;
            source.DamageDone += actualDamage;
            if (actualDamage > 0)
            {
                target.DamagedCount += 1;
                ResolveDamaged(target);
            }

            if (target.CurrentHp <= 0 && before > 0)
            {
                source.KillCount += 1;
                ResolveKill(source);
                ResolveDeath(target, source, targetAllies, sourceAllies, random);
            }

            return actualDamage;
        }

        private static BattleStubResult Finish(RunState runState, bool victory, int playerScore, int enemyScore, int hpLoss, int attacks, IReadOnlyList<BattleRuntimeUnit> players, IReadOnlyList<BattleRuntimeUnit> enemies)
        {
            if (hpLoss > 0)
            {
                runState.playerHp -= hpLoss;
            }

            ApplyPostBattleRewards(runState, victory, players);
            var playerAlive = players.Count(unit => unit.IsAlive);
            var enemyAlive = enemies.Count(unit => unit.IsAlive);
            var playerDamage = players.Sum(unit => unit.DamageDone);
            var enemyDamage = enemies.Sum(unit => unit.DamageDone);
            var summary = victory
                ? $"胜利。我方战力 {playerScore}，敌方战力 {enemyScore}，剩余 {playerAlive} 个单位，总攻击次数 {attacks}。"
                : $"失败。我方战力 {playerScore}，敌方战力 {enemyScore}，敌方剩余 {enemyAlive} 个单位，失去 {hpLoss} 点生命。";
            return new BattleStubResult
            {
                Victory = victory,
                PlayerScore = playerScore,
                EnemyScore = enemyScore,
                HpDelta = -hpLoss,
                PlayerDamage = playerDamage,
                EnemyDamage = enemyDamage,
                Summary = summary
            };
        }

        private static List<BattleRuntimeUnit> BuildPlayerUnits(RunState runState)
        {
            return runState.boardUnits
                .Select(unit => CreateRuntimeUnit(unit, true, UnitDef(unit), unit.boardSlotId, 1f))
                .Where(unit => unit != null)
                .ToList();
        }

        private static List<BattleRuntimeUnit> BuildEnemyUnits(RunState runState, Random random)
        {
            var data = ProphecyGameSession.Instance.Data;
            var campaignMultiplier = CampaignEnemyMultiplier(runState.campaignId);
            var milestone = runState.round % 5 == 0;
            var budget = Math.Max(6, (int)Math.Round((runState.round * 3 + (milestone ? runState.round : 0)) * campaignMultiplier));
            var maxStar = Math.Min(6, 1 + runState.round / 3 + (milestone ? 1 : 0));
            var pool = data.Units
                .Where(unit => unit != null && !unit.hidden && unit.star <= maxStar && unit.id != "light_illusion")
                .OrderBy(unit => unit.star)
                .ToList();
            var slots = data.Config?.GetBoardOrder() ?? new List<string>();
            var enemies = new List<BattleRuntimeUnit>();
            var roundLimit = runState.campaignRoundLimit > 0 ? runState.campaignRoundLimit : 20;
            var progress = Math.Min(1f, runState.round / (float)Math.Max(1, roundLimit));
            var multiplier = (0.72f + runState.round * 0.045f + progress * 0.35f) * campaignMultiplier;

            while (budget > 0 && pool.Count > 0 && enemies.Count < Math.Min(10, slots.Count))
            {
                var affordable = pool.Where(unit => Math.Max(1, unit.star) <= budget).ToList();
                if (affordable.Count == 0)
                {
                    break;
                }

                var picked = affordable[random.Next(affordable.Count)];
                budget -= Math.Max(1, picked.star);
                var slot = enemies.Count < slots.Count ? slots[slots.Count - 1 - enemies.Count] : $"{enemies.Count + 1}-1";
                var runtime = CreateRuntimeUnit(null, false, picked, slot, multiplier);
                if (runtime != null)
                {
                    enemies.Add(runtime);
                }
            }

            if (enemies.Count == 0 && pool.Count > 0)
            {
                enemies.Add(CreateRuntimeUnit(null, false, pool[0], "4-1", multiplier));
            }

            return enemies;
        }

        private static float CampaignEnemyMultiplier(string campaignId)
        {
            switch (campaignId)
            {
                case "snow_peak_defense":
                    return 1.12f;
                case "song_of_sang_city":
                    return 1.24f;
                default:
                    return 1f;
            }
        }

        private static BattleRuntimeUnit CreateRuntimeUnit(UnitCardState state, bool playerSide, UnitDefinition definition, string slotId, float multiplier)
        {
            if (definition == null)
            {
                return null;
            }

            TryParseSlot(slotId, out var row, out var col);
            var hp = Scale(definition.hp + (state?.shopBuffHp ?? 0), multiplier);
            var attack = Scale(definition.attack + (state?.shopBuffAttack ?? 0) + (state?.roundTempAttack ?? 0), multiplier);
            var defense = Scale(definition.defense + (state?.shopBuffDefense ?? 0), multiplier);
            var power = Math.Max(0, definition.power + (state?.shopBuffPower ?? 0) + (state?.roundTempPower ?? 0));
            var speed = Math.Max(0, definition.speed + (state?.shopBuffSpeed ?? 0));
            var morale = Math.Max(0, definition.morale + (state?.shopBuffMorale ?? 0) + (state?.roundTempMorale ?? 0));
            var luck = Math.Max(0, definition.luck + (state?.shopBuffLuck ?? 0));
            var interval = definition.attackInterval > 0 ? definition.attackInterval : 1f;
            interval = Math.Max(0.2f, interval * (100f / (100f + speed * 2f)));

            return new BattleRuntimeUnit
            {
                UnitId = definition.id,
                Name = definition.name,
                Race = definition.race,
                Faith = definition.faith,
                Type = definition.type,
                Tags = definition.tags ?? Array.Empty<string>(),
                Definition = definition,
                IsGolden = state?.isGolden ?? false,
                SourceState = state,
                PlayerSide = playerSide,
                SlotId = slotId,
                Row = row,
                Col = playerSide ? col : -col,
                MaxHp = Math.Max(1, hp),
                CurrentHp = Math.Max(1, hp),
                Attack = Math.Max(1, attack),
                Defense = Math.Max(0, defense),
                Power = power,
                Speed = speed,
                Luck = luck,
                Morale = morale,
                AttackInterval = interval,
                Cooldown = Math.Max(0.05f, interval * 0.5f),
                TeamForestGiftTotal = ProphecyGameSession.Instance.CurrentRun?.manageResources?.forestGiftTotal ?? 0
            };
        }

        private static void TransformRuntimeUnit(BattleRuntimeUnit unit, UnitDefinition definition)
        {
            if (unit == null || definition == null)
            {
                return;
            }

            unit.UnitId = definition.id;
            unit.Name = definition.name;
            unit.Race = definition.race;
            unit.Faith = definition.faith;
            unit.Type = definition.type;
            unit.Tags = definition.tags ?? Array.Empty<string>();
            unit.Definition = definition;
            unit.MaxHp = Math.Max(unit.CurrentHp, unit.MaxHp + Math.Max(0, definition.hp / 4));
            unit.CurrentHp = Math.Max(1, unit.CurrentHp + Math.Max(0, definition.hp / 4));
            unit.Attack += Math.Max(0, definition.attack / 2);
            unit.Defense += Math.Max(0, definition.defense / 2);
            unit.Power += Math.Max(0, definition.power);
            unit.Speed = Math.Max(unit.Speed, definition.speed);
            unit.AttackInterval = definition.attackInterval > 0 ? Math.Max(0.2f, definition.attackInterval) : unit.AttackInterval;
        }

        private static void ResolveBattleStart(List<BattleRuntimeUnit> allies, List<BattleRuntimeUnit> enemies, Random random)
        {
            for (var i = 0; i < allies.Count; i += 1)
            {
                var unit = allies[i];
                if (unit == null || !unit.IsAlive)
                {
                    continue;
                }

                foreach (var skill in GetBattleSkills(unit))
                {
                    switch (skill.kind)
                    {
                        case "battle_start_team_shield":
                            foreach (var ally in allies.Where(ally => ally.IsAlive))
                            {
                                ally.ShieldLayers += Math.Max(1, skill.layers);
                            }
                            unit.SkillTriggers += 1;
                            break;
                        case "battle_start_self_refreshing_shield":
                            unit.ShieldLayers += Math.Max(1, skill.layers);
                            unit.ShieldRefreshInterval = Math.Max(0.1f, skill.duration > 0f ? skill.duration : 5f);
                            unit.ShieldRefreshTimer = unit.ShieldRefreshInterval;
                            unit.SkillTriggers += 1;
                            break;
                        case "battle_start_self_attack_per_faith_count":
                            AddAttack(unit, CountFaith(allies, skill.faith, unit.Faith) * Math.Max(1, skill.value));
                            break;
                        case "battle_start_team_attack_per_faith_count":
                            var teamAttack = CountFaith(allies, skill.faith, "\u83b1\u7279") * Math.Max(1, skill.valuePerFaith);
                            foreach (var ally in allies.Where(ally => ally.IsAlive))
                            {
                                AddAttack(ally, teamAttack);
                            }
                            unit.SkillTriggers += teamAttack > 0 ? 1 : 0;
                            break;
                        case "battle_start_self_stats_per_faith_count":
                            var faithCount = CountFaith(allies, skill.faith, unit.Faith);
                            AddBattleStats(unit, skill, faithCount);
                            break;
                        case "battle_start_speedup_first_attack_crit_stun_restore":
                            unit.OriginalSpeed = unit.Speed;
                            unit.OriginalAttackInterval = unit.AttackInterval;
                            unit.Speed = (int)Math.Round(unit.Speed * Math.Max(1f, skill.speedMultiplier > 0f ? skill.speedMultiplier : 2f));
                            unit.AttackInterval = Math.Max(0.2f, unit.AttackInterval / Math.Max(1f, skill.speedMultiplier > 0f ? skill.speedMultiplier : 2f));
                            unit.FirstAttackForceCrit = true;
                            unit.FirstAttackCritMultiplier = ProphecyGameSession.Instance.Data.Config?.critDamageMultiple ?? 1.5f;
                            unit.SkillTriggers += 1;
                            break;
                        case "battle_start_stealth":
                        case "battle_start_stealth_assassinate_lowest_hp":
                            unit.FirstAttackForceCrit = true;
                            unit.FirstAttackCritMultiplier = ProphecyGameSession.Instance.Data.Config?.critDamageMultiple ?? 1.5f;
                            unit.PreferLowestHp = skill.kind == "battle_start_stealth_assassinate_lowest_hp";
                            unit.DelayedSnipeTimer = skill.kind == "battle_start_stealth_assassinate_lowest_hp" ? Math.Max(0f, skill.delay) : 0f;
                            unit.DelayedSnipeMultiplier = Math.Max(1f, skill.attackMultiplier);
                            unit.SkillTriggers += 1;
                            break;
                        case "battle_start_if_adjacent_faith_gain_attack":
                            var adjacent = allies.Count(ally => ally != unit && ally.IsAlive && ally.Faith == (skill.faith ?? unit.Faith) && Math.Abs(ally.Row - unit.Row) <= 1 && Math.Abs(ally.Col - unit.Col) <= 1);
                            AddAttack(unit, adjacent * Math.Max(1, skill.value));
                            break;
                        case "battle_start_speed_threshold_attack_interval_half":
                            foreach (var ally in allies.Where(ally => ally.IsAlive && ally.Speed > Math.Max(0, skill.threshold)))
                            {
                                ally.AttackInterval = Math.Max(0.2f, ally.AttackInterval * 0.5f);
                            }
                            unit.SkillTriggers += 1;
                            break;
                        case "battle_start_speed_threshold_attack_interval_reduce":
                            foreach (var ally in allies.Where(ally => ally.IsAlive && ally.Speed > Math.Max(0, skill.threshold)))
                            {
                                ally.AttackInterval = Math.Max(0.2f, ally.AttackInterval - Math.Max(0.05f, skill.reduce));
                            }
                            unit.SkillTriggers += 1;
                            break;
                        case "battle_start_speed_threshold_attack_interval_reduce_ratio":
                            foreach (var ally in allies.Where(ally => ally.IsAlive && ally.Speed > Math.Max(0, skill.threshold)))
                            {
                                ally.AttackInterval = Math.Max(0.2f, ally.AttackInterval * (1f - Clamp01(skill.ratio)));
                            }
                            unit.SkillTriggers += 1;
                            break;
                        case "battle_start_lowest_power_ally_gain_source_power":
                            var target = allies.Where(ally => ally.IsAlive).OrderBy(ally => ally.Power).ThenBy(ally => Distance(unit, ally)).FirstOrDefault();
                            if (target != null)
                            {
                                target.Power += unit.Power * Math.Max(1, skill.multiplier == 0 ? 1 : skill.multiplier);
                                unit.SkillTriggers += 1;
                            }
                            break;
                        case "battle_start_delay_snipe_backline":
                            unit.DelayedSnipeTimer = Math.Max(0.1f, skill.delay);
                            unit.DelayedSnipeMultiplier = Math.Max(1f, skill.attackMultiplier);
                            unit.PreferBackline = true;
                            unit.SkillTriggers += 1;
                            break;
                        case "battle_start_mount_nearest_unit_transform_until_end":
                            var mount = allies
                                .Where(ally => ally != unit && ally.IsAlive && ally.UnitId == skill.targetUnitId)
                                .OrderBy(ally => Distance(unit, ally))
                                .FirstOrDefault();
                            var transform = ProphecyGameSession.Instance.Data.FindUnit(skill.transformUnitId);
                            if (mount != null && transform != null)
                            {
                                unit.Attack += Math.Max(0, mount.Attack / 2);
                                unit.Defense += Math.Max(0, mount.Defense / 2);
                                unit.Power += Math.Max(0, mount.Power);
                                mount.CurrentHp = 0;
                                TransformRuntimeUnit(unit, transform);
                                unit.SkillTriggers += 1;
                            }
                            break;
                        case "battle_start_self_temp_morale":
                            unit.Morale += Math.Max(0, skill.value);
                            unit.SkillTriggers += skill.value > 0 ? 1 : 0;
                            break;
                        case "battle_start_if_team_faith_count_next_round_discover":
                            var discoverFaith = string.IsNullOrWhiteSpace(skill.faith) ? unit.Faith : skill.faith;
                            if (CountFaith(allies, discoverFaith, unit.Faith) >= Math.Max(1, skill.threshold))
                            {
                                unit.PendingDiscoverFaith = discoverFaith;
                                unit.PendingDiscoverRace = skill.race;
                                unit.PendingDiscoverCount += Math.Max(1, skill.count);
                                unit.SkillTriggers += 1;
                            }
                            break;
                        case "battle_start_team_temp_defense_if_win_next_round_self_hp":
                            foreach (var ally in allies.Where(ally => ally.IsAlive))
                            {
                                ally.Defense += Math.Max(0, skill.defense);
                            }

                            unit.PendingWinPermanentHp += Math.Max(0, skill.hp);
                            unit.SkillTriggers += 1;
                            break;
                        case "battle_start_summon_units":
                        case "battle_start_and_death_summon_units":
                            SummonUnits(allies, unit, skill, random);
                            unit.SkillTriggers += 1;
                            break;
                        case "battle_start_summon_and_buff_type":
                            SummonUnits(allies, unit, skill, random);
                            foreach (var ally in allies.Where(ally => ally.IsAlive && (ally.Type == skill.type || HasTag(ally, skill.type))))
                            {
                                AddBattleStats(ally, skill, 1);
                            }
                            unit.SkillTriggers += 1;
                            break;
                        case "battle_start_pounce_nearest_damage":
                            var pounceTarget = enemies.Where(enemy => enemy.IsAlive).OrderBy(enemy => Distance(unit, enemy)).FirstOrDefault();
                            if (pounceTarget != null)
                            {
                                if (skill.invincibleSeconds > 0f)
                                {
                                    unit.InvincibleRemaining = Math.Max(unit.InvincibleRemaining, skill.invincibleSeconds);
                                }

                                var multiplier = skill.attackMultiplier > 0f ? skill.attackMultiplier : 3f;
                                var damage = Math.Max(1, (int)Math.Round(unit.Attack * multiplier + unit.Power * 8 - pounceTarget.Defense));
                                if (skill.forceCrit)
                                {
                                    damage = (int)Math.Ceiling(damage * (ProphecyGameSession.Instance.Data.Config?.critDamageMultiple ?? 1.5f));
                                }

                                DealDamage(unit, pounceTarget, damage, allies, enemies, random);
                                pounceTarget.StunRemaining = Math.Max(pounceTarget.StunRemaining, skill.stunSeconds);
                                unit.SkillTriggers += 1;
                            }
                            break;
                        case "battle_start_lock_highest_hp_targets":
                            foreach (var locked in enemies.Where(enemy => enemy.IsAlive).OrderByDescending(enemy => enemy.CurrentHp).Take(Math.Max(1, skill.count)))
                            {
                                locked.StunRemaining = Math.Max(locked.StunRemaining, skill.duration);
                            }
                            unit.SkillTriggers += 1;
                            break;
                    }
                }
            }
        }

        private static void ResolveOnAttack(BattleRuntimeUnit attacker, BattleRuntimeUnit target, List<BattleRuntimeUnit> allies, List<BattleRuntimeUnit> enemies, Random random, int damage, bool isPrimaryAttack)
        {
            if (!isPrimaryAttack)
            {
                return;
            }

            foreach (var skill in GetBattleSkills(attacker))
            {
                switch (skill.kind)
                {
                    case "on_attack_chance_self_shield_no_stack":
                        if (attacker.ShieldLayers <= 0 && random.NextDouble() < Math.Max(0f, skill.chance))
                        {
                            attacker.ShieldLayers += Math.Max(1, skill.layers);
                            attacker.SkillTriggers += 1;
                        }
                        break;
                    case "on_attack_count_summon":
                        if (IncrementSkillCounter(attacker, skill.kind) >= Math.Max(1, skill.count))
                        {
                            attacker.SkillCounters[skill.kind] = 0;
                            SummonUnits(allies, attacker, skill, random);
                            attacker.SkillTriggers += 1;
                        }
                        break;
                    case "on_attack_multi_nearest_targets":
                        foreach (var extraTarget in enemies.Where(enemy => enemy.IsAlive && enemy != target).OrderBy(enemy => Distance(target, enemy)).Take(Math.Max(0, skill.targets - 1)).ToList())
                        {
                            var extraDamage = Math.Max(1, attacker.Attack + attacker.Power * 8 - extraTarget.Defense);
                            DealDamage(attacker, extraTarget, extraDamage, allies, enemies, random);
                        }
                        attacker.SkillTriggers += Math.Max(0, skill.targets - 1) > 0 ? 1 : 0;
                        break;
                    case "on_attack_count_fire_rain_area_dot":
                        if (IncrementSkillCounter(attacker, skill.kind) >= Math.Max(1, skill.count))
                        {
                            attacker.SkillCounters[skill.kind] = 0;
                            foreach (var areaTarget in enemies.Where(enemy => enemy.IsAlive && Distance(enemy, target) <= Math.Max(1f, skill.radius)).ToList())
                            {
                                var areaDamage = Math.Max(1, (int)Math.Round((attacker.Attack + attacker.Power * 8) * Math.Max(0.35f, skill.attackMultiplier)));
                                DealDamage(attacker, areaTarget, areaDamage, allies, enemies, random);
                            }

                            attacker.SkillTriggers += 1;
                        }
                        break;
                    case "on_attack_if_team_gift_total_aoe":
                        if (attacker.TeamForestGiftTotal >= Math.Max(1, skill.threshold))
                        {
                            foreach (var areaTarget in enemies.Where(enemy => enemy.IsAlive && enemy != target && Distance(enemy, target) <= Math.Max(1f, skill.radius)).ToList())
                            {
                                DealDamage(attacker, areaTarget, Math.Max(1, damage), allies, enemies, random);
                            }

                            attacker.SkillTriggers += 1;
                        }
                        break;
                    case "on_attack_mark_target_next_round_forest_gem_on_death":
                        target.ForestGemDeathMarkSource = attacker;
                        target.ForestGemDeathMarkAmount = Math.Max(1, skill.value);
                        attacker.SkillTriggers += 1;
                        break;
                }
            }
        }

        private static void ResolvePreAttack(BattleRuntimeUnit attacker, Random random, out bool forceCrit, out float critMultiplier)
        {
            forceCrit = false;
            critMultiplier = 0f;
            if (attacker.FirstAttackForceCrit)
            {
                forceCrit = true;
                critMultiplier = attacker.FirstAttackCritMultiplier;
                attacker.FirstAttackForceCrit = false;
                if (attacker.OriginalSpeed > 0)
                {
                    attacker.Speed = attacker.OriginalSpeed;
                    attacker.AttackInterval = attacker.OriginalAttackInterval;
                }
            }

            foreach (var skill in GetBattleSkills(attacker))
            {
                if (skill.kind == "passive_every_nth_attack_force_crit" && attacker.AttackCount > 0 && attacker.AttackCount % Math.Max(1, skill.count) == Math.Max(1, skill.count) - 1)
                {
                    forceCrit = true;
                    critMultiplier = Math.Max(critMultiplier, attacker.Power >= Math.Max(0, skill.threshold) ? Math.Max(1.5f, skill.multiplier) : 0f);
                    attacker.SkillTriggers += 1;
                }

                if (skill.kind == "on_attack_chance_force_crit" && random.NextDouble() < Math.Max(0f, skill.chance))
                {
                    forceCrit = true;
                    attacker.SkillTriggers += 1;
                }
            }
        }

        private static void ResolveDeath(BattleRuntimeUnit unit, BattleRuntimeUnit killer, List<BattleRuntimeUnit> allies, List<BattleRuntimeUnit> enemies, Random random)
        {
            if (unit.DeathProcessed)
            {
                return;
            }

            unit.DeathProcessed = true;
            if (unit.ForestGemDeathMarkSource != null && unit.ForestGemDeathMarkSource.PlayerSide)
            {
                unit.ForestGemDeathMarkSource.PendingRoundForestGems += Math.Max(1, unit.ForestGemDeathMarkAmount);
            }

            foreach (var skill in GetBattleSkills(unit))
            {
                switch (skill.kind)
                {
                    case "battle_start_and_death_summon_units":
                        SummonUnits(allies, unit, skill, random);
                        unit.SkillTriggers += 1;
                        break;
                    case "on_death_explode":
                    case "on_death_explode_if_hits_next_round_team_attack":
                        var hitCount = 0;
                        foreach (var enemy in enemies.Where(enemy => enemy.IsAlive && Distance(unit, enemy) <= Math.Max(1, skill.radius)).ToList())
                        {
                            var damage = Math.Max(1, skill.damage > 0 ? skill.damage : (int)Math.Round((unit.Attack + unit.Power * 8) * Math.Max(1f, skill.attackMultiplier)));
                            DealDamage(unit, enemy, damage, allies, enemies, random);
                            hitCount += 1;
                        }

                        if (unit.PlayerSide && skill.kind == "on_death_explode_if_hits_next_round_team_attack" && hitCount >= Math.Max(1, skill.hitThreshold))
                        {
                            foreach (var ally in allies.Where(ally => ally.SourceState != null))
                            {
                                ally.PendingRoundTempAttack += Math.Max(0, skill.nextRoundAttack);
                            }
                        }

                        unit.SkillTriggers += 1;
                        break;
                    case "on_death_next_round_shop_cards_gain_attack":
                        if (unit.PlayerSide)
                        {
                            unit.PendingNextRoundShopBuffAttack += Math.Max(0, skill.attack);
                        }

                        unit.SkillTriggers += 1;
                        break;
                    case "on_death_next_round_forest_gem":
                        if (unit.PlayerSide)
                        {
                            unit.PendingRoundForestGems += Math.Max(0, skill.value);
                        }

                        unit.SkillTriggers += 1;
                        break;
                }
            }
        }

        private static void ResolveDamaged(BattleRuntimeUnit unit)
        {
            foreach (var skill in GetBattleSkills(unit))
            {
                if (skill.kind == "on_damaged_count_temp_morale" && unit.DamagedCount >= Math.Max(1, skill.count))
                {
                    unit.DamagedCount = 0;
                    unit.Morale += Math.Max(1, skill.value);
                    unit.SkillTriggers += 1;
                }
            }
        }

        private static void TickTimedSkills(List<BattleRuntimeUnit> units, List<BattleRuntimeUnit> enemies, Random random)
        {
            foreach (var unit in units.Where(unit => unit.IsAlive).ToList())
            {
                if (unit.SummonDuration > 0f)
                {
                    unit.SummonDuration -= StepSeconds;
                    if (unit.SummonDuration <= 0f)
                    {
                        unit.CurrentHp = 0;
                        continue;
                    }
                }

                if (unit.DelayedSnipeTimer > 0f)
                {
                    unit.DelayedSnipeTimer -= StepSeconds;
                    if (unit.DelayedSnipeTimer <= 0f)
                    {
                        var target = enemies.Where(enemy => enemy.IsAlive)
                            .OrderBy(enemy => unit.PreferLowestHp ? enemy.CurrentHp : 0)
                            .ThenByDescending(enemy => enemy.Row)
                            .FirstOrDefault();
                        if (target != null)
                        {
                            var damage = Math.Max(1, (int)Math.Round((unit.Attack + unit.Power * 8 - target.Defense) * Math.Max(1f, unit.DelayedSnipeMultiplier)));
                            DealDamage(unit, target, damage, units, enemies, random);
                            unit.SkillTriggers += 1;
                        }
                    }
                }

                foreach (var skill in GetBattleSkills(unit))
                {
                    if (skill.kind == "battle_periodic_temp_power")
                    {
                        if (TickSkillTimer(unit, skill.kind, Math.Max(0.1f, skill.interval <= 0f ? 3f : skill.interval)))
                        {
                            unit.Power += Math.Max(1, skill.value);
                            unit.SkillTriggers += 1;
                        }
                    }
                    else if (skill.kind == "battle_periodic_self_hp_loss_team_temp_attack")
                    {
                        if (TickSkillTimer(unit, skill.kind, Math.Max(0.1f, skill.interval <= 0f ? 1f : skill.interval)))
                        {
                            var loss = Math.Max(1, skill.selfHpLoss > 0 ? skill.selfHpLoss : skill.damage > 0 ? skill.damage : skill.hp > 0 ? skill.hp : 3);
                            unit.CurrentHp = Math.Max(0, unit.CurrentHp - loss);
                            foreach (var ally in units.Where(ally => ally.IsAlive && ally != unit))
                            {
                                ally.Attack += Math.Max(1, skill.attack);
                            }
                            unit.SkillTriggers += 1;
                        }
                    }
                    else if (skill.kind == "battle_periodic_nearby_enemies_attack_and_death_explode")
                    {
                        if (TickSkillTimer(unit, skill.kind, Math.Max(0.1f, skill.interval <= 0f ? 1f : skill.interval)))
                        {
                            foreach (var target in enemies.Where(enemy => enemy.IsAlive && Distance(unit, enemy) <= Math.Max(1f, skill.radius)).ToList())
                            {
                                var damage = Math.Max(1, (int)Math.Round((unit.Attack + unit.Power * 8 - target.Defense) * Math.Max(1f, skill.attackMultiplier)));
                                DealDamage(unit, target, damage, units, enemies, random);
                            }
                            unit.SkillTriggers += 1;
                        }
                    }
                }
            }
        }

        private static bool TickSkillTimer(BattleRuntimeUnit unit, string key, float interval)
        {
            if (!unit.SkillTimers.TryGetValue(key, out var timer))
            {
                timer = interval;
            }

            timer -= StepSeconds;
            if (timer <= 0f)
            {
                unit.SkillTimers[key] = interval;
                return true;
            }

            unit.SkillTimers[key] = timer;
            return false;
        }

        private static void ResolveKill(BattleRuntimeUnit attacker)
        {
            if (attacker == null)
            {
                return;
            }

            foreach (var skill in GetBattleSkills(attacker))
            {
                if (skill.kind == "on_kill_count_next_round_evolve"
                    && !string.IsNullOrWhiteSpace(skill.targetUnitId))
                {
                    var key = $"{skill.kind}:{skill.targetUnitId}";
                    var count = IncrementSourceProgress(attacker.SourceState, key);
                    if (count >= Math.Max(1, skill.threshold))
                    {
                        SetSourceProgress(attacker.SourceState, key, 0);
                        attacker.PendingRoundEvolveTo = skill.targetUnitId;
                        attacker.SkillTriggers += 1;
                    }
                }
            }
        }

        private static int IncrementSourceProgress(UnitCardState source, string key)
        {
            if (source == null || string.IsNullOrWhiteSpace(key))
            {
                return 0;
            }

            if (source.battleProgressCounters == null)
            {
                source.battleProgressCounters = new List<BattleProgressCounterState>();
            }
            var counter = source.battleProgressCounters.FirstOrDefault(item => item.key == key);
            if (counter == null)
            {
                counter = new BattleProgressCounterState { key = key };
                source.battleProgressCounters.Add(counter);
            }

            counter.value += 1;
            return counter.value;
        }

        private static void SetSourceProgress(UnitCardState source, string key, int value)
        {
            if (source == null || source.battleProgressCounters == null)
            {
                return;
            }

            var counter = source.battleProgressCounters.FirstOrDefault(item => item.key == key);
            if (counter != null)
            {
                counter.value = Math.Max(0, value);
            }
        }

        private static void ApplyPostBattleRewards(RunState runState, bool victory, IReadOnlyList<BattleRuntimeUnit> players)
        {
            if (runState == null)
            {
                return;
            }

            if (runState.pendingBattleRewards == null)
            {
                runState.pendingBattleRewards = new BattleRewardState();
            }
            var survivingPlayers = players.Count(unit => unit.IsAlive && unit.SourceState != null);
            foreach (var unit in players.Where(unit => unit.SourceState != null))
            {
                var source = unit.SourceState;
                source.pendingNextRoundTempAttack += Math.Max(0, unit.PendingRoundTempAttack);
                source.pendingNextRoundTempPower += Math.Max(0, unit.PendingRoundTempPower);
                source.pendingNextRoundPermanentHp += Math.Max(0, unit.PendingRoundPermanentHp);
                source.pendingNextRoundPermanentPower += Math.Max(0, unit.PendingRoundPermanentPower);
                source.pendingNextRoundPermanentLuck += Math.Max(0, unit.PendingRoundPermanentLuck);
                source.pendingNextRoundForestGems += Math.Max(0, unit.PendingRoundForestGems);
                if (!string.IsNullOrWhiteSpace(unit.PendingRoundEvolveTo))
                {
                    source.pendingNextRoundEvolveTo = unit.PendingRoundEvolveTo;
                }

                if (!string.IsNullOrWhiteSpace(unit.PendingDiscoverFaith) || !string.IsNullOrWhiteSpace(unit.PendingDiscoverRace))
                {
                    runState.pendingBattleRewards.discoverFaithRewards.Add(new DiscoverFaithRewardState
                    {
                        faith = unit.PendingDiscoverFaith,
                        race = unit.PendingDiscoverRace,
                        count = Math.Max(1, unit.PendingDiscoverCount),
                        label = unit.Name
                    });
                }

                foreach (var skill in GetBattleSkills(unit))
                {
                    if (skill.kind == "on_extra_attack_once_next_round_gold" && unit.MoraleExtraCount > 0)
                    {
                        runState.pendingBattleRewards.nextRoundGold += Math.Max(1, skill.value);
                    }

                    if (skill.kind == "on_counter_count_next_round_gain_forest_gem" && unit.CounterCount >= Math.Max(1, skill.count))
                    {
                        source.pendingNextRoundForestGems += Math.Max(1, skill.value);
                    }

                    if (skill.kind == "battle_end_survivors_next_round_team_temp_attack" && survivingPlayers > 0)
                    {
                        source.pendingNextRoundTempAttack += survivingPlayers * Math.Max(1, skill.value);
                    }

                    if (skill.kind == "battle_start_team_temp_defense_if_win_next_round_self_hp" && victory)
                    {
                        source.pendingNextRoundPermanentHp += Math.Max(0, unit.PendingWinPermanentHp);
                    }
                }

                runState.pendingBattleRewards.nextRoundShopBuffAttack += Math.Max(0, unit.PendingNextRoundShopBuffAttack);
            }
        }

        private static void TickBattleState(List<BattleRuntimeUnit> units)
        {
            foreach (var unit in units.Where(unit => unit.IsAlive))
            {
                unit.StunRemaining = Math.Max(0f, unit.StunRemaining - StepSeconds);
                unit.InvincibleRemaining = Math.Max(0f, unit.InvincibleRemaining - StepSeconds);
                if (unit.ShieldRefreshInterval > 0f && unit.ShieldLayers <= 0)
                {
                    unit.ShieldRefreshTimer -= StepSeconds;
                    if (unit.ShieldRefreshTimer <= 0f)
                    {
                        unit.ShieldLayers = 1;
                        unit.ShieldRefreshTimer = unit.ShieldRefreshInterval;
                    }
                }
            }
        }

        private static void ApplyContinuousAuras(IReadOnlyList<BattleRuntimeUnit> units)
        {
            foreach (var aura in units.Where(unit => unit.IsAlive).SelectMany(unit => GetBattleSkills(unit).Where(skill => skill.kind == "battle_aura_sync_unit_id_attack_to_highest")))
            {
                var targetUnitId = string.IsNullOrWhiteSpace(aura.targetUnitId) ? null : aura.targetUnitId;
                var targets = units.Where(unit => unit.IsAlive && (targetUnitId == null || unit.UnitId == targetUnitId)).ToList();
                if (targets.Count == 0)
                {
                    continue;
                }

                var highest = targets.Max(unit => unit.Attack);
                foreach (var target in targets)
                {
                    target.Attack = Math.Max(target.Attack, highest);
                }
            }
        }

        private static IEnumerable<SkillDefinition> GetBattleSkills(BattleRuntimeUnit unit)
        {
            if (unit?.Definition == null)
            {
                return Enumerable.Empty<SkillDefinition>();
            }

            return (unit.IsGolden ? unit.Definition.goldBattleSkills : unit.Definition.battleSkills) ?? Array.Empty<SkillDefinition>();
        }

        private static int CountFaith(IEnumerable<BattleRuntimeUnit> units, string skillFaith, string fallbackFaith)
        {
            var faith = string.IsNullOrWhiteSpace(skillFaith) ? fallbackFaith : skillFaith;
            return units.Count(unit => unit.IsAlive && unit.Faith == faith);
        }

        private static void AddAttack(BattleRuntimeUnit unit, int value)
        {
            if (unit == null || value <= 0)
            {
                return;
            }

            unit.Attack += value;
            unit.SkillTriggers += 1;
        }

        private static void AddBattleStats(BattleRuntimeUnit unit, SkillDefinition skill, int multiplier)
        {
            if (unit == null || skill == null || multiplier <= 0)
            {
                return;
            }

            var changed = false;
            if (skill.attack != 0)
            {
                unit.Attack += skill.attack * multiplier;
                changed = true;
            }

            if (skill.defense != 0)
            {
                unit.Defense += skill.defense * multiplier;
                changed = true;
            }

            if (skill.hp != 0)
            {
                var hpGain = skill.hp * multiplier;
                unit.MaxHp = Math.Max(1, unit.MaxHp + hpGain);
                unit.CurrentHp = Math.Max(1, unit.CurrentHp + hpGain);
                changed = true;
            }

            if (skill.power != 0)
            {
                unit.Power += skill.power * multiplier;
                changed = true;
            }

            if (skill.speed != 0)
            {
                unit.Speed += skill.speed * multiplier;
                changed = true;
            }

            if (skill.morale != 0)
            {
                unit.Morale += skill.morale * multiplier;
                changed = true;
            }

            if (changed)
            {
                unit.SkillTriggers += 1;
            }
        }

        private static bool HasTag(BattleRuntimeUnit unit, string tag)
        {
            return !string.IsNullOrWhiteSpace(tag) && unit.Tags != null && unit.Tags.Contains(tag);
        }

        private static int IncrementSkillCounter(BattleRuntimeUnit unit, string key)
        {
            if (!unit.SkillCounters.TryGetValue(key, out var count))
            {
                count = 0;
            }

            count += 1;
            unit.SkillCounters[key] = count;
            return count;
        }

        private static void SummonUnits(List<BattleRuntimeUnit> allies, BattleRuntimeUnit source, SkillDefinition skill, Random random)
        {
            if (allies == null || source == null || skill == null || string.IsNullOrWhiteSpace(skill.summonUnitId))
            {
                return;
            }

            var definition = ProphecyGameSession.Instance.Data.FindUnit(skill.summonUnitId);
            if (definition == null)
            {
                return;
            }

            var count = Math.Max(1, skill.count);
            for (var i = 0; i < count; i += 1)
            {
                var slot = FindSummonSlot(allies, source, i);
                var summoned = CreateRuntimeUnit(null, source.PlayerSide, definition, slot, 1f);
                if (summoned == null)
                {
                    continue;
                }

                summoned.Summoned = true;
                summoned.SummonDuration = skill.duration > 0f ? skill.duration : 0f;
                allies.Add(summoned);
            }
        }

        private static string FindSummonSlot(IReadOnlyList<BattleRuntimeUnit> allies, BattleRuntimeUnit source, int offset)
        {
            var occupied = new HashSet<string>(allies.Where(unit => unit.IsAlive).Select(unit => unit.SlotId));
            var configOrder = ProphecyGameSession.Instance.Data.Config?.GetBoardOrder() ?? new List<string>();
            foreach (var slot in configOrder)
            {
                if (!occupied.Contains(slot))
                {
                    return slot;
                }
            }

            return $"{Math.Max(1, source.Row)}-{Math.Max(1, source.Col + offset + 1)}";
        }

        private static int EstimateScore(IEnumerable<BattleRuntimeUnit> units)
        {
            return (int)Math.Round(units.Sum(unit =>
                unit.Attack * 1.85f
                + unit.Defense * 1.25f
                + unit.Power * 18f
                + unit.MaxHp * 0.58f
                + unit.Speed * 0.72f
                + unit.Morale * 9f
                + unit.Luck * 5f));
        }

        private static int CalculateHpLoss(RunState runState, IReadOnlyList<BattleRuntimeUnit> enemies)
        {
            var alive = enemies.Where(unit => unit.IsAlive).ToList();
            if (alive.Count == 0)
            {
                return 0;
            }

            var pressure = alive.Sum(unit => Math.Max(1, unit.MaxHp / 120 + unit.Attack / 180));
            return Math.Max(1, Math.Min(20, 2 + runState.round / 2 + pressure));
        }

        private static int TotalAliveHp(IEnumerable<BattleRuntimeUnit> units)
        {
            return units.Where(unit => unit.IsAlive).Sum(unit => unit.CurrentHp);
        }

        private static UnitDefinition UnitDef(UnitCardState unit)
        {
            return unit == null ? null : ProphecyGameSession.Instance.Data.FindUnit(unit.unitId);
        }

        private static int Scale(int value, float multiplier)
        {
            return Math.Max(1, (int)Math.Round(value * multiplier));
        }

        private static float Distance(BattleRuntimeUnit left, BattleRuntimeUnit right)
        {
            var row = left.Row - right.Row;
            var col = left.Col - right.Col;
            return (float)Math.Sqrt(row * row + col * col);
        }

        private static float Clamp01(float value)
        {
            if (value < 0f)
            {
                return 0f;
            }

            return value > 1f ? 1f : value;
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
    }

    public sealed class BattleStubResult
    {
        public bool Victory;
        public int PlayerScore;
        public int EnemyScore;
        public int HpDelta;
        public int PlayerDamage;
        public int EnemyDamage;
        public string Summary;
    }

    internal sealed class BattleRuntimeUnit
    {
        public string UnitId;
        public string Name;
        public string Race;
        public string Faith;
        public string Type;
        public string[] Tags = Array.Empty<string>();
        public UnitDefinition Definition;
        public bool IsGolden;
        public UnitCardState SourceState;
        public bool PlayerSide;
        public string SlotId;
        public int Row;
        public int Col;
        public int MaxHp;
        public int CurrentHp;
        public int Attack;
        public int Defense;
        public int Power;
        public int Speed;
        public int Luck;
        public int Morale;
        public float AttackInterval;
        public float Cooldown;
        public int DamageDone;
        public int AttackCount;
        public int KillCount;
        public int CounterCount;
        public int MoraleExtraCount;
        public int DamagedCount;
        public int ShieldLayers;
        public float StunRemaining;
        public float InvincibleRemaining;
        public float ShieldRefreshInterval;
        public float ShieldRefreshTimer;
        public int OriginalSpeed;
        public float OriginalAttackInterval;
        public bool FirstAttackForceCrit;
        public float FirstAttackCritMultiplier;
        public bool DeathProcessed;
        public bool Summoned;
        public float SummonDuration;
        public bool PreferLowestHp;
        public bool PreferBackline;
        public float DelayedSnipeTimer;
        public float DelayedSnipeMultiplier;
        public int TeamForestGiftTotal;
        public int SkillTriggers;
        public int PendingRoundTempAttack;
        public int PendingRoundTempPower;
        public int PendingRoundPermanentHp;
        public int PendingRoundPermanentPower;
        public int PendingRoundPermanentLuck;
        public int PendingRoundForestGems;
        public string PendingRoundEvolveTo;
        public int PendingNextRoundShopBuffAttack;
        public int PendingWinPermanentHp;
        public string PendingDiscoverFaith;
        public string PendingDiscoverRace;
        public int PendingDiscoverCount;
        public BattleRuntimeUnit ForestGemDeathMarkSource;
        public int ForestGemDeathMarkAmount;
        public readonly Dictionary<string, int> SkillCounters = new Dictionary<string, int>();
        public readonly Dictionary<string, float> SkillTimers = new Dictionary<string, float>();
        public bool IsAlive => CurrentHp > 0;
    }
}
