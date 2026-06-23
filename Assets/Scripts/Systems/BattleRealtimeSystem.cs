using System;
using System.Collections.Generic;
using System.Linq;
using ProphecyCentury.Core;
using ProphecyCentury.Data;

namespace ProphecyCentury.Systems
{
    public sealed class BattleRealtimeSystem
    {
        private const float StepSeconds = 0.05f;
        private const float TargetSearchInterval = 1f;
        private const float AttackRangeSlack = 36f;
        private const int MaxBattleSafetySteps = 200000;
        private const int MaxBattleEvents = 800;
        private const float LUCK_CRIT_CHANCE_PER_POINT = 0.06f;
        private const float LUCK_CRIT_DAMAGE_MULTIPLIER = 1.5f;
        private const float MORALE_EXTRA_ATTACK_CHANCE_PER_POINT = 0.04f;
        private const float GridDistancePixels = 80f;
        private const string FirstAttackBacklineSnipeKind = "first_attack_backline_snipe";

        public BattleRealtimeResult Resolve(IReadOnlyList<BattleUnitSnapshot> playerSnapshots, IReadOnlyList<BattleUnitSnapshot> enemySnapshots)
        {
            var random = new Random(ProphecyGameSession.Instance.CurrentRun.round * 104729 + (playerSnapshots?.Count ?? 0) * 379);
            var players = CreateUnits(playerSnapshots, true);
            var enemies = CreateUnits(enemySnapshots, false);
            var initialPlayerUnits = (playerSnapshots ?? Array.Empty<BattleUnitSnapshot>()).ToList();
            var initialEnemyUnits = (enemySnapshots ?? Array.Empty<BattleUnitSnapshot>()).ToList();
            var events = new List<BattleEvent>();
            var elapsed = 0f;

            AddEvent(events, 0f, "start", null, null, 0, "Realtime battle start");
            ResolveBattleStart(players, enemies, random, events, 0f);
            ResolveBattleStart(enemies, players, random, events, 0f);
            ApplyContinuousAuras(players);
            ApplyContinuousAuras(enemies);
            var playerAreaEffects = new List<RealtimeAreaEffect>();
            var enemyAreaEffects = new List<RealtimeAreaEffect>();

            var safetySteps = 0;
            while (players.Any(unit => unit.IsAlive) && enemies.Any(unit => unit.IsAlive))
            {
                safetySteps += 1;
                if (safetySteps > MaxBattleSafetySteps)
                {
                    AddEvent(events, elapsed, "safety_limit", null, null, 0, $"Realtime battle safety limit reached after {MaxBattleSafetySteps} steps");
                    break;
                }

                TickTimedSkills(players, enemies, random, playerAreaEffects, elapsed, events);
                TickTimedSkills(enemies, players, random, enemyAreaEffects, elapsed, events);
                TickSide(players, enemies, random, playerAreaEffects, elapsed, events);
                TickSide(enemies, players, random, enemyAreaEffects, elapsed, events);
                TickState(players);
                TickState(enemies);
                ResolveCollisions(players, enemies);
                elapsed += StepSeconds;
            }

            var playerAlive = players.Any(unit => unit.IsAlive);
            var enemyAlive = enemies.Any(unit => unit.IsAlive);
            var victory = playerAlive && !enemyAlive;
            if (playerAlive == enemyAlive)
            {
                victory = players.Where(unit => unit.IsAlive).Sum(unit => unit.Hp) >= enemies.Where(unit => unit.IsAlive).Sum(unit => unit.Hp);
            }

            var playerDamage = players.Sum(unit => unit.DamageDone);
            var enemyDamage = enemies.Sum(unit => unit.DamageDone);
            var playerAliveCount = players.Count(unit => unit.IsAlive);
            var enemyAliveCount = enemies.Count(unit => unit.IsAlive);
            var summary = victory
                ? $"Realtime battle win. Player units alive: {playerAliveCount}, damage dealt: {playerDamage}."
                : $"Realtime battle loss. Enemy units alive: {enemyAliveCount}, damage taken: {enemyDamage}.";
            AddEvent(events, elapsed, victory ? "victory" : "defeat", null, null, 0, summary);

            return new BattleRealtimeResult
            {
                Victory = victory,
                BattleTime = elapsed,
                PlayerDamage = playerDamage,
                EnemyDamage = enemyDamage,
                Summary = summary,
                Events = events,
                InitialPlayerUnits = initialPlayerUnits,
                InitialEnemyUnits = initialEnemyUnits,
                PlayerUnits = players.Select(CreateSnapshot).ToList(),
                EnemyUnits = enemies.Select(CreateSnapshot).ToList()
            };
        }

        private static List<RealtimeBattleUnit> CreateUnits(IReadOnlyList<BattleUnitSnapshot> snapshots, bool playerSide)
        {
            return (snapshots ?? Array.Empty<BattleUnitSnapshot>())
                .Where(unit => unit != null)
                .Select(unit => new RealtimeBattleUnit(unit, playerSide))
                .ToList();
        }

        private static void ResolveBattleStart(List<RealtimeBattleUnit> allies, List<RealtimeBattleUnit> enemies, Random random, List<BattleEvent> events, float elapsed)
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
                            AddEvent(events, elapsed, "skill", unit, null, 0, $"{unit.Name} shields the team");
                            break;
                        case "battle_start_front_occupied_rows_shield":
                            var frontRows = allies
                                .Where(ally => ally.IsAlive)
                                .Select(ally => ally.Row)
                                .Distinct()
                                .OrderBy(row => row)
                                .Take(Math.Max(1, skill.count));
                            var frontRowSet = new HashSet<int>(frontRows);
                            foreach (var ally in allies.Where(ally => ally.IsAlive && frontRowSet.Contains(ally.Row)))
                            {
                                ally.ShieldLayers += Math.Max(1, skill.layers);
                            }
                            if (frontRowSet.Count > 0)
                            {
                                AddEvent(events, elapsed, "skill", unit, null, 0, $"{unit.Name} shields the front rows");
                            }
                            break;
                        case "battle_start_self_refreshing_shield":
                            unit.ShieldLayers += Math.Max(1, skill.layers);
                            unit.ShieldRefreshInterval = Math.Max(0.1f, SkillRefreshSeconds(skill, 5f));
                            unit.ShieldRefreshTimer = unit.ShieldRefreshInterval;
                            AddEvent(events, elapsed, "skill", unit, unit, 0, $"{unit.Name} gains a refreshing shield");
                            break;
                        case "battle_start_team_attack_per_faith_count":
                            var attackGain = CountFaith(allies, skill.faith, unit.Faith) * Math.Max(1, skill.valuePerFaith);
                            foreach (var ally in allies.Where(ally => ally.IsAlive))
                            {
                                ally.Attack += attackGain;
                            }
                            if (attackGain > 0)
                            {
                                AddEvent(events, elapsed, "skill", unit, null, attackGain, $"{unit.Name} increases team attack");
                            }
                            break;
                        case "battle_start_self_attack_per_faith_count":
                            var selfAttackGain = CountFaith(allies, skill.faith, unit.Faith) * Math.Max(1, skill.value);
                            unit.Attack += selfAttackGain;
                            if (selfAttackGain > 0)
                            {
                                AddEvent(events, elapsed, "skill", unit, unit, selfAttackGain, $"{unit.Name} increases attack");
                            }
                            break;
                        case "battle_start_self_stats_per_faith_count":
                            var faithCount = CountFaith(allies, skill.faith, unit.Faith);
                            AddBattleStats(unit, skill, faithCount);
                            if (faithCount > 0)
                            {
                                AddEvent(events, elapsed, "skill", unit, unit, faithCount, $"{unit.Name} gains faith stats");
                            }
                            break;
                        case "battle_start_stealth":
                        case "battle_start_stealth_assassinate_lowest_hp":
                            unit.FirstAttackForceCrit = true;
                            unit.FirstAttackCritMultiplier = skill.kind == "battle_start_stealth_assassinate_lowest_hp"
                                ? Math.Max(1f, skill.attackMultiplier)
                                : ProphecyGameSession.Instance.Data.Config?.critDamageMultiple ?? 1.5f;
                            unit.PreferLowestHp = skill.kind == "battle_start_stealth_assassinate_lowest_hp";
                            AddEvent(events, elapsed, "skill", unit, unit, 0, $"{unit.Name} enters stealth");
                            break;
                        case FirstAttackBacklineSnipeKind:
                            unit.FirstAttackBacklineSnipeTarget = PickBacklineSnipeTarget(unit, enemies);
                            if (unit.FirstAttackBacklineSnipeTarget != null)
                            {
                                AddEvent(events, elapsed, "snipe_lock", unit, unit.FirstAttackBacklineSnipeTarget, Math.Max(1, (int)Math.Round(Math.Max(1f, skill.critMultiplier))), $"{unit.Name} locks {unit.FirstAttackBacklineSnipeTarget.Name}");
                            }
                            break;
                        case "battle_start_lowest_power_ally_gain_source_power":
                            var targetAlly = allies.Where(ally => ally.IsAlive).OrderBy(ally => ally.CurrentCount).FirstOrDefault();
                            if (targetAlly != null)
                            {
                                AddTemporaryCount(targetAlly, Math.Max(1, skill.value));
                                AddEvent(events, elapsed, "skill", unit, targetAlly, Math.Max(1, skill.value), $"{targetAlly.Name} 临时数量增加");
                            }
                            break;
                        case "battle_start_delay_snipe_backline":
                            unit.DelayedSnipeTimer = Math.Max(0.1f, skill.delay);
                            unit.DelayedSnipeCritDistance = Math.Max(0f, skill.distance);
                            unit.DelayedSnipeAttackMultiplier = Math.Max(1f, skill.attackMultiplier);
                            unit.DelayedSnipeCritMultiplier = Math.Max(1f, skill.critMultiplier);
                            unit.PreferBackline = true;
                            AddEvent(events, elapsed, "skill", unit, unit, 0, $"{unit.Name} prepares a backline snipe");
                            break;
                        case "battle_start_summon_units":
                        case "battle_start_and_death_summon_units":
                            SummonUnits(allies, unit, skill, events, elapsed);
                            break;
                        case "battle_start_summon_and_buff_type":
                            SummonUnits(allies, unit, skill, events, elapsed);
                            foreach (var ally in allies.Where(ally => ally.IsAlive && MatchesSkillTarget(ally, skill)))
                            {
                                AddBattleStats(ally, skill, 1);
                            }
                            AddEvent(events, elapsed, "skill", unit, null, 0, $"{unit.Name} summons and buffs allies");
                            break;
                        case "battle_start_pounce_nearest_damage":
                            var pounceTarget = PickTarget(unit, enemies);
                            if (pounceTarget != null)
                            {
                                unit.InvincibleRemaining = Math.Max(unit.InvincibleRemaining, skill.invincibleSeconds);
                                var pounceEvent = AddEvent(events, elapsed, "skill", unit, pounceTarget, 0, $"{unit.Name} pounces {pounceTarget.Name}");
                                MovePouncerNextToTarget(unit, pounceTarget);
                                if (pounceEvent != null)
                                {
                                    pounceEvent.DestinationSlotId = unit.SlotId;
                                }
                                var multiplier = skill.attackMultiplier > 0f ? skill.attackMultiplier : 3f;
                                var damage = Math.Max(1, (int)Math.Round(CalculateDamage(unit, pounceTarget, random) * multiplier));
                                for (var hit = 0; hit < Math.Max(1, skill.times); hit += 1)
                                {
                                    if (!pounceTarget.IsAlive)
                                    {
                                        break;
                                    }

                                    DealDamage(unit, pounceTarget, damage, allies, enemies, random, events, elapsed, skill.forceCrit);
                                }

                                var stunDuration = skill.stunTurns > 0 ? skill.stunTurns : skill.stunSeconds;
                                pounceTarget.StunRemaining = Math.Max(pounceTarget.StunRemaining, stunDuration);
                                if (skill.stunTurns > 0)
                                {
                                    AddEvent(events, elapsed, "control", unit, pounceTarget, skill.stunTurns, $"{pounceTarget.Name} stunned for {skill.stunTurns} turns");
                                }
                                else if (skill.stunSeconds > 0f)
                                {
                                    AddEvent(events, elapsed, "control", unit, pounceTarget, (int)Math.Round(skill.stunSeconds * 1000f), $"{pounceTarget.Name} stunned for {skill.stunSeconds:0.#}s");
                                }
                            }
                            break;
                        case "battle_start_lock_highest_hp_targets":
                            foreach (var locked in enemies.Where(enemy => enemy.IsAlive).OrderByDescending(enemy => enemy.Hp).Take(Math.Max(1, skill.count)))
                            {
                                if (skill.moveLockTurns > 0)
                                {
                                    locked.MoveLockRemaining = Math.Max(locked.MoveLockRemaining, skill.moveLockTurns);
                                    AddEvent(events, elapsed, "control", unit, locked, skill.moveLockTurns, $"{locked.Name} move locked for {skill.moveLockTurns} turns");
                                }
                                else
                                {
                                    locked.StunRemaining = Math.Max(locked.StunRemaining, skill.duration);
                                    AddEvent(events, elapsed, "control", unit, locked, (int)Math.Round(Math.Max(0.1f, skill.duration) * 1000f), $"{locked.Name} locked for {skill.duration:0.#}s");
                                }
                            }
                            break;
                        case "battle_start_self_temp_initiative":
                            unit.Initiative += Math.Max(0, skill.value);
                            AddEvent(events, elapsed, "skill", unit, unit, Math.Max(0, skill.value), $"{unit.Name} gains temporary initiative");
                            break;
                    }
                }
            }
        }

        private static void TickSide(List<RealtimeBattleUnit> attackers, List<RealtimeBattleUnit> defenders, Random random, List<RealtimeAreaEffect> areaEffects, float elapsed, List<BattleEvent> events)
        {
            foreach (var attacker in attackers.Where(unit => unit.IsAlive).ToList())
            {
                attacker.AttackTimer = Math.Max(0f, attacker.AttackTimer - StepSeconds);
                if (attacker.StunRemaining > 0f)
                {
                    continue;
                }

                ResolveDelayedSnipe(attacker, defenders, attackers, random, elapsed, events);
                if (!attacker.IsAlive)
                {
                    continue;
                }

                var target = ResolveTarget(attacker, defenders);
                if (target == null && attacker.HasStartedAttacking)
                {
                    target = PickTargetInAttackRange(attacker, defenders);
                    attacker.CurrentTarget = target;
                }

                if (target == null)
                {
                    continue;
                }

                if (attacker.HasStartedAttacking)
                {
                    var inRangeTarget = PickTargetInAttackRange(attacker, defenders);
                    if (inRangeTarget != null)
                    {
                        LockAttackPosition(attacker);
                        target = inRangeTarget;
                        attacker.CurrentTarget = target;
                    }
                    else
                    {
                        attacker.HasAttackAnchor = false;
                    }
                }

                var distance = Distance(attacker, target);
                var attackRange = AttackRange(attacker, target);
                if (distance > attackRange + AttackRangeSlack)
                {
                    if (attacker.MoveLockRemaining <= 0f)
                    {
                        MoveToTarget(attacker, target, distance, attackRange);
                    }
                    continue;
                }

                if (attacker.AttackTimer > 0f)
                {
                    continue;
                }

                ResolveActionSelfShieldIfNone(attacker, events, elapsed);
                attacker.AttackTimer = Math.Max(0.2f, attacker.AttackInterval);
                attacker.AttackCount += 1;
                attacker.HasStartedAttacking = true;
                attacker.HasAttackAnchor = true;
                attacker.AttackAnchorX = attacker.X;
                attacker.AttackAnchorY = attacker.Y;
                ResolveAllyActionTempCountBonuses(attacker, attackers, events, elapsed);
                var actualTarget = target;
                var actual = TryResolveFirstAttackBacklineSnipe(attacker, attackers, defenders, random, events, elapsed, out var snipeTarget, out var snipeDamage)
                    ? snipeDamage
                    : ResolveAttackDamage(attacker, target, attackers, defenders, random, events, elapsed, $"{attacker.Name} 攻击 {target.Name}");
                if (snipeTarget != null)
                {
                    actualTarget = snipeTarget;
                }

                if (actual > 0)
                {
                    ResolveOnAttack(attacker, actualTarget, attackers, defenders, random, areaEffects, actual, elapsed, events);
                }

                var moraleExtraTarget = actualTarget != null && actualTarget.IsAlive ? actualTarget : PickTarget(attacker, defenders);
                if (attacker.IsAlive && moraleExtraTarget != null && random.NextDouble() < MoraleChance(attacker.Morale, ProphecyGameSession.Instance.Data.Config?.moraleExtraAttackRate ?? 0.08f))
                {
                    attacker.CurrentTarget = moraleExtraTarget;
                    AddEvent(events, elapsed, "morale_extra", attacker, moraleExtraTarget, 0, $"{attacker.Name} 触发追击");
                    var extraActual = ResolveAttackDamage(attacker, moraleExtraTarget, attackers, defenders, random, events, elapsed, $"{attacker.Name} 追击 {moraleExtraTarget.Name}");
                    if (extraActual > 0)
                    {
                        ResolveOnAttack(attacker, moraleExtraTarget, attackers, defenders, random, areaEffects, extraActual, elapsed, events);
                    }

                    attacker.MoraleExtraCount += 1;
                    foreach (var skill in GetBattleSkills(attacker))
                    {
                        if (skill.kind == "on_extra_attack_once_next_round_gold" && !attacker.SkillCounters.ContainsKey(skill.kind))
                        {
                            attacker.SkillCounters[skill.kind] = 1;
                            AddEvent(events, elapsed, "skill", attacker, attacker, Math.Max(1, skill.value), $"{attacker.Name} grants next round gold");
                            break;
                        }
                    }
                }
            }
        }

        private static bool TryResolveFirstAttackBacklineSnipe(
            RealtimeBattleUnit attacker,
            List<RealtimeBattleUnit> allies,
            List<RealtimeBattleUnit> enemies,
            Random random,
            List<BattleEvent> events,
            float elapsed,
            out RealtimeBattleUnit target,
            out int actualDamage)
        {
            target = null;
            actualDamage = 0;
            if (attacker == null || attacker.AttackCount != 1 || attacker.SkillCounters.ContainsKey(FirstAttackBacklineSnipeKind))
            {
                return false;
            }

            var skill = GetBattleSkills(attacker).FirstOrDefault(item => item.kind == FirstAttackBacklineSnipeKind);
            if (skill == null)
            {
                return false;
            }

            target = attacker.FirstAttackBacklineSnipeTarget != null && attacker.FirstAttackBacklineSnipeTarget.IsAlive
                ? attacker.FirstAttackBacklineSnipeTarget
                : PickBacklineSnipeTarget(attacker, enemies);
            if (target == null)
            {
                return false;
            }

            attacker.SkillCounters[FirstAttackBacklineSnipeKind] = 1;
            var threshold = Math.Max(0f, skill.distance) * GridDistancePixels;
            var critical = threshold > 0f && Distance(attacker, target) >= threshold;
            var multiplier = critical ? Math.Max(1f, skill.critMultiplier) : Math.Max(1f, skill.attackMultiplier);
            var damage = Math.Max(1, (int)Math.Round(CalculateDamage(attacker, target, random) * multiplier));
            if (critical)
            {
                AddEvent(events, elapsed, "snipe_charge", attacker, target, Math.Max(1, (int)Math.Round(multiplier)), $"{attacker.Name} charges a critical snipe");
            }

            AddEvent(events, elapsed, "attack", attacker, target, 0, $"{attacker.Name} first snipe {target.Name}");
            if (critical)
            {
                var multiplierAmount = Math.Max(1, (int)Math.Round(multiplier));
                AddEvent(events, elapsed, "crit_multiplier", attacker, target, multiplierAmount, $"{multiplierAmount}倍暴击！");
                ResolveAllyCritTemporaryCount(attacker, allies, events, elapsed);
            }

            actualDamage = DealDamage(attacker, target, damage, allies, enemies, random, events, elapsed, critical);
            return true;
        }

        private static RealtimeBattleUnit PickBacklineSnipeTarget(RealtimeBattleUnit attacker, IEnumerable<RealtimeBattleUnit> enemies)
        {
            var aliveEnemies = (enemies ?? Enumerable.Empty<RealtimeBattleUnit>())
                .Where(enemy => enemy != null && enemy.IsAlive)
                .ToList();
            if (aliveEnemies.Count == 0)
            {
                return null;
            }

            var backRow = aliveEnemies.Max(enemy => enemy.Row);
            return aliveEnemies
                .Where(enemy => enemy.Row == backRow)
                .OrderBy(enemy => Distance(attacker, enemy))
                .ThenBy(enemy => enemy.Hp)
                .FirstOrDefault();
        }

        private static void ResolveActionSelfShieldIfNone(RealtimeBattleUnit attacker, List<BattleEvent> events, float elapsed)
        {
            if (attacker == null || !attacker.IsAlive || attacker.ShieldLayers > 0)
            {
                return;
            }

            foreach (var skill in GetBattleSkills(attacker))
            {
                switch (skill.kind)
                {
                    case "battle_action_self_shield_if_none":
                        attacker.ShieldLayers = Math.Max(attacker.ShieldLayers, Math.Max(1, skill.layers));
                        AddEvent(events, elapsed, "shield", attacker, attacker, attacker.ShieldLayers, $"{attacker.Name} gains action shield");
                        return;
                }
            }
        }

        private static void ResolveDelayedSnipe(RealtimeBattleUnit unit, List<RealtimeBattleUnit> enemies, List<RealtimeBattleUnit> allies, Random random, float elapsed, List<BattleEvent> events)
        {
            if (unit == null || !unit.IsAlive || unit.DelayedSnipeTimer <= 0f)
            {
                return;
            }

            unit.DelayedSnipeTimer -= StepSeconds;
            if (unit.DelayedSnipeTimer > 0f)
            {
                return;
            }

            var target = enemies
                .Where(enemy => enemy.IsAlive)
                .OrderBy(enemy => Distance(unit, enemy))
                .ThenBy(enemy => unit.PreferLowestHp ? enemy.Hp : 0)
                .ThenByDescending(enemy => unit.PreferBackline ? enemy.Row : 0)
                .FirstOrDefault();
            if (target == null)
            {
                return;
            }

            var forceCrit = unit.DelayedSnipeCritDistance > 0f && Distance(unit, target) >= unit.DelayedSnipeCritDistance;
            var multiplier = forceCrit ? Math.Max(1f, unit.DelayedSnipeCritMultiplier) : Math.Max(1f, unit.DelayedSnipeAttackMultiplier);
            var damage = Math.Max(1, (int)Math.Round(CalculateDamage(unit, target, random) * multiplier));
            AddEvent(events, elapsed, "skill", unit, target, 0, $"{unit.Name} 延迟狙击 {target.Name}");
            DealDamage(unit, target, damage, allies, enemies, random, events, elapsed, forceCrit);
        }

        private static void ResolveAllyActionTempCountBonuses(RealtimeBattleUnit actor, List<RealtimeBattleUnit> allies, List<BattleEvent> events, float elapsed)
        {
            if (actor == null || allies == null)
            {
                return;
            }

            foreach (var receiver in allies.Where(unit => unit.IsAlive))
            {
                foreach (var skill in GetBattleSkills(receiver))
                {
                    if (skill.kind != "battle_periodic_temp_power")
                    {
                        continue;
                    }

                    var gain = Math.Max(1, skill.value);
                    AddTemporaryCount(receiver, gain);
                    AddEvent(events, elapsed, "count_gain", actor, receiver, gain, $"{receiver.Name} 因 {actor.Name} 行动，临时数量 +{gain}");
                }
            }
        }

        private static int ResolveAttackDamage(RealtimeBattleUnit attacker, RealtimeBattleUnit target, List<RealtimeBattleUnit> allies, List<RealtimeBattleUnit> enemies, Random random, List<BattleEvent> events, float elapsed, string attackMessage, bool allowForcedCounterattack = true)
        {
            var damage = CalculateDamage(attacker, target, random);
            var forceCrit = ResolveForceCrit(attacker, random, out var critMultiplier);
            var luckyCrit = !forceCrit && random.NextDouble() < Math.Min(0.95f, Math.Max(0f, attacker.Luck * LUCK_CRIT_CHANCE_PER_POINT));
            var didCrit = forceCrit || luckyCrit;
            if (didCrit)
            {
                damage = (int)Math.Ceiling(damage * Math.Max(critMultiplier, LUCK_CRIT_DAMAGE_MULTIPLIER));
                ResolveAllyCritTemporaryCount(attacker, allies, events, elapsed);
            }

            if (luckyCrit)
            {
                AddEvent(events, elapsed, "lucky_crit", attacker, target, 0, $"{attacker.Name} 幸运！");
            }

            AddEvent(events, elapsed, "attack", attacker, target, 0, attackMessage);
            return DealDamage(attacker, target, damage, allies, enemies, random, events, elapsed, didCrit, allowForcedCounterattack);
        }

        private static void ResolveAllyCritTemporaryCount(RealtimeBattleUnit attacker, List<RealtimeBattleUnit> allies, List<BattleEvent> events, float elapsed)
        {
            if (attacker == null || allies == null)
            {
                return;
            }

            foreach (var ally in allies.Where(unit => unit != null && unit.IsAlive && unit != attacker))
            {
                foreach (var skill in GetBattleSkills(ally).Where(skill => skill.kind == "on_ally_crit_self_temp_power"))
                {
                    var gain = Math.Max(1, skill.value);
                    AddTemporaryCount(ally, gain);
                    AddEvent(events, elapsed, "count_gain", attacker, ally, gain, $"{ally.Name} 因友军暴击，临时数量 +{gain}");
                }
            }
        }

        private static void ResolveOnAttack(RealtimeBattleUnit attacker, RealtimeBattleUnit target, List<RealtimeBattleUnit> allies, List<RealtimeBattleUnit> enemies, Random random, List<RealtimeAreaEffect> areaEffects, int damage, float elapsed, List<BattleEvent> events)
        {
            foreach (var skill in GetBattleSkills(attacker))
            {
                switch (skill.kind)
                {
                    case "on_attack_chance_self_shield_no_stack":
                        if (attacker.ShieldLayers <= 0 && random.NextDouble() < Math.Max(0f, skill.chance))
                        {
                            attacker.ShieldLayers = Math.Max(1, skill.layers);
                            AddEvent(events, elapsed, "shield", attacker, attacker, attacker.ShieldLayers, $"{attacker.Name} 获得护盾");
                        }
                        break;
                    case "on_attack_count_summon":
                        if (IncrementSkillCounter(attacker, skill.kind) >= Math.Max(1, skill.count))
                        {
                            attacker.SkillCounters[skill.kind] = 0;
                            SummonUnits(allies, attacker, skill, events, elapsed);
                        }
                        break;
                    case "on_attack_multi_nearest_targets":
                        foreach (var extraTarget in enemies.Where(enemy => enemy.IsAlive && enemy != target).OrderBy(enemy => Distance(target, enemy)).Take(Math.Max(0, skill.targets - 1)).ToList())
                        {
                            AddEvent(events, elapsed, "skill", attacker, extraTarget, 0, $"{attacker.Name} 追加攻击 {extraTarget.Name}");
                            DealDamage(attacker, extraTarget, CalculateDamage(attacker, extraTarget), allies, enemies, random, events, elapsed);
                        }
                        break;
                    case "on_attack_count_fire_rain_area_dot":
                        if (IncrementSkillCounter(attacker, skill.kind) >= Math.Max(1, skill.count))
                        {
                            attacker.SkillCounters[skill.kind] = 0;
                            if (skill.duration <= 0f)
                            {
                                ResolveInstantFireRain(attacker, target, allies, enemies, random, skill, events, elapsed);
                            }
                            else
                            {
                                AddAreaEffect(areaEffects, attacker, target, skill, events, elapsed);
                            }
                        }
                        break;
                    case "on_attack_count_formula_aoe":
                        if (IncrementSkillCounter(attacker, skill.kind) >= Math.Max(1, skill.count))
                        {
                            attacker.SkillCounters[skill.kind] = 0;
                            var areaTargets = enemies.Where(enemy => enemy.IsAlive && Distance(enemy, target) <= Math.Max(1f, skill.radius)).ToList();
                            var areaDamages = areaTargets.Select(areaTarget => Math.Max(1, (int)Math.Round(CalculateDamage(attacker, areaTarget) * Math.Max(1f, skill.attackMultiplier)))).ToList();
                            DealAreaDamageSimultaneously(attacker, areaTargets, areaDamages, allies, enemies, random, events, elapsed, $"{attacker.Name} 触发范围攻击");
                        }
                        break;
                    case "on_attack_if_team_gift_total_aoe":
                        if (attacker.TeamForestGiftTotal >= Math.Max(1, skill.threshold))
                        {
                            var areaTargets = enemies.Where(enemy => enemy.IsAlive && enemy != target && Distance(enemy, target) <= Math.Max(1f, skill.radius)).ToList();
                            var areaDamages = areaTargets.Select(_ => Math.Max(1, damage)).ToList();
                            DealAreaDamageSimultaneously(attacker, areaTargets, areaDamages, allies, enemies, random, events, elapsed, $"{attacker.Name} triggers gift area attack");
                        }
                        break;
                    case "on_attack_self_count_loss_percent_aoe":
                        if (attacker.CurrentCount > 1)
                        {
                            var lossCount = CalculateSelfCountLoss(attacker, skill);
                            AddEvent(events, elapsed, "skill", attacker, attacker, lossCount, $"{attacker.Name} loses troops for area attack");
                            ApplySelfCountLoss(attacker, lossCount);
                        }

                        var selfLossTargets = enemies.Where(enemy => enemy.IsAlive && enemy != target && Distance(enemy, target) <= Math.Max(1f, skill.radius)).ToList();
                        var selfLossDamages = selfLossTargets.Select(_ => Math.Max(1, damage)).ToList();
                        DealAreaDamageSimultaneously(attacker, selfLossTargets, selfLossDamages, allies, enemies, random, events, elapsed, $"{attacker.Name} 触发范围攻击");
                        break;
                    case "on_attack_mark_target_next_round_forest_gem_on_death":
                        target.ForestGemDeathMarkSource = attacker;
                        target.ForestGemDeathMarkAmount = Math.Max(1, skill.value);
                        AddEvent(events, elapsed, "skill", attacker, target, target.ForestGemDeathMarkAmount, $"{attacker.Name} marks {target.Name} for next round forest gem");
                        break;
                }
            }
        }

        private static void TickTimedSkills(List<RealtimeBattleUnit> units, List<RealtimeBattleUnit> enemies, Random random, List<RealtimeAreaEffect> areaEffects, float elapsed, List<BattleEvent> events)
        {
            TickAreaEffects(areaEffects, units, enemies, random, events, elapsed);

            foreach (var unit in units.Where(unit => unit.IsAlive).ToList())
            {
                if (unit.SummonDuration > 0f)
                {
                    unit.SummonDuration -= StepSeconds;
                    if (unit.SummonDuration <= 0f)
                    {
                        unit.Hp = 0;
                        AddEvent(events, elapsed, "death", null, unit, 0, $"{unit.Name} 消散");
                        continue;
                    }
                }

                foreach (var skill in GetBattleSkills(unit))
                {
                    if (skill.kind == "battle_periodic_temp_power")
                    {
                        continue;
                    }
                    else if (skill.kind == "battle_round_self_hp_loss_team_temp_attack")
                    {
                        if (unit.CurrentCount <= 1 || !TickSkillTimer(unit, skill.kind, SkillIntervalSeconds(skill, 1f)))
                        {
                            continue;
                        }

                        var lossCount = CalculateSelfCountLoss(unit, skill);
                        AddEvent(events, elapsed, "skill", unit, unit, lossCount, $"{unit.Name} loses troops and rallies allies");
                        var hpLoss = ApplySelfCountLoss(unit, lossCount);
                        AddEvent(events, elapsed, "damage", unit, unit, hpLoss, $"{unit.Name} loses troops");
                        foreach (var ally in units.Where(ally => ally.IsAlive && ally != unit))
                        {
                            var attackGain = Math.Max(0, skill.attack);
                            ally.Attack += attackGain;
                            if (attackGain > 0)
                            {
                                AddEvent(events, elapsed, "buff_attack", unit, ally, attackGain, $"{ally.Name} 攻击提升");
                            }
                        }
                    }
                    else if (skill.kind == "battle_periodic_nearby_enemies_attack_and_death_explode" && TickSkillTimer(unit, skill.kind, Math.Max(0.1f, skill.interval <= 0f ? 1f : skill.interval)))
                    {
                        foreach (var target in enemies.Where(enemy => enemy.IsAlive && Distance(unit, enemy) <= Math.Max(1f, skill.radius * 80f)).ToList())
                        {
                            AddEvent(events, elapsed, "skill", unit, target, 0, $"{unit.Name} 周期攻击 {target.Name}");
                            DealDamage(unit, target, Math.Max(1, (int)Math.Round(CalculateDamage(unit, target) * Math.Max(1f, skill.attackMultiplier))), units, enemies, random, events, elapsed);
                        }
                    }
                }
            }
        }

        private static int DealDamage(RealtimeBattleUnit source, RealtimeBattleUnit target, int damage, List<RealtimeBattleUnit> sourceAllies, List<RealtimeBattleUnit> targetAllies, Random random, List<BattleEvent> events, float elapsed, bool critical = false, bool allowForcedCounterattack = true)
        {
            if (target == null || !target.IsAlive)
            {
                return 0;
            }

            if (target.ShieldLayers > 0)
            {
                target.ShieldLayers -= 1;
                AddEvent(events, elapsed, "block", source, target, 0, $"{target.Name}\'s shield blocks damage");
                return 0;
            }

            if (target.InvincibleRemaining > 0f)
            {
                AddEvent(events, elapsed, "immune", source, target, 0, $"{target.Name} is immune to damage");
                return 0;
            }

            var before = target.Hp;
            target.CurrentTotalHp = Math.Max(0, target.CurrentTotalHp - Math.Max(1, damage));
            target.CurrentCount = target.CurrentTotalHp <= 0 ? 0 : (int)Math.Ceiling(target.CurrentTotalHp / (float)Math.Max(1, target.HpPerUnit));
            target.Hp = target.CurrentTotalHp;
            target.MaxHp = Math.Max(target.MaxHp, target.BaseCount * Math.Max(1, target.HpPerUnit));
            var actual = before - target.Hp;
            if (source != null)
            {
                source.DamageDone += actual;
            }

            AddEvent(events, elapsed, critical ? "critical_damage" : "damage", source, target, actual, $"{source?.Name ?? "Effect"} deals {actual} damage to {target.Name}");
            if (actual > 0 && source != null && allowForcedCounterattack)
            {
                ResolveFirstHitsCounterattack(target, source, targetAllies, sourceAllies, random, events, elapsed);
            }

            if ((target.Hp <= 0 || target.CurrentCount <= 0) && before > 0)
            {
                if (source != null)
                {
                    source.Kills += 1;
                }

                AddEvent(events, elapsed, "death", source, target, 0, $"{target.Name} dies");
                ResolveDeath(target, source, targetAllies, sourceAllies, random, elapsed, events);
            }

            return actual;
        }

        private static void ResolveDeath(RealtimeBattleUnit unit, RealtimeBattleUnit killer, List<RealtimeBattleUnit> allies, List<RealtimeBattleUnit> enemies, Random random, float elapsed, List<BattleEvent> events)
        {
            if (unit.DeathProcessed)
            {
                return;
            }

            unit.DeathProcessed = true;
            if (unit.ForestGemDeathMarkSource != null && unit.ForestGemDeathMarkSource.PlayerSide)
            {
                AddEvent(events, elapsed, "skill", unit.ForestGemDeathMarkSource, unit, Math.Max(1, unit.ForestGemDeathMarkAmount), $"{unit.ForestGemDeathMarkSource.Name} gains next round forest gem");
            }

            foreach (var skill in GetBattleSkills(unit))
            {
                switch (skill.kind)
                {
                    case "battle_start_and_death_summon_units":
                        SummonUnits(allies, unit, skill, events, elapsed);
                        break;
                    case "battle_periodic_nearby_enemies_attack_and_death_explode":
                    case "on_death_explode":
                    case "on_death_explode_if_hits_next_round_team_attack":
                    case "on_death_explode_if_hits_next_round_team_count":
                        var hitCount = 0;
                        foreach (var enemy in enemies.Where(enemy => enemy.IsAlive && Distance(unit, enemy) <= Math.Max(1f, skill.radius * 80f)).ToList())
                        {
                            AddEvent(events, elapsed, "skill", unit, enemy, 0, $"{unit.Name} 死亡爆炸");
                            var multiplier = skill.kind == "battle_periodic_nearby_enemies_attack_and_death_explode"
                                ? SkillDeathAttackMultiplier(skill)
                                : Math.Max(1f, skill.attackMultiplier);
                            var explodeDamage = Math.Max(1, skill.damage > 0 ? skill.damage : (int)Math.Round(CalculateDamage(unit, enemy) * multiplier));
                            DealDamage(unit, enemy, explodeDamage, allies, enemies, random, events, elapsed);
                            hitCount += 1;
                        }

                        if (unit.PlayerSide
                            && (skill.kind == "on_death_explode_if_hits_next_round_team_count" || skill.kind == "on_death_explode_if_hits_next_round_team_attack")
                            && hitCount >= Math.Max(1, skill.hitThreshold))
                        {
                            AddEvent(events, elapsed, "skill", unit, unit, Math.Max(0, skill.nextRoundCount > 0 ? skill.nextRoundCount : skill.nextRoundAttack), $"{unit.Name} grants next round team count");
                        }
                        break;
                    case "on_death_next_round_shop_cards_gain_attack":
                        if (unit.PlayerSide)
                        {
                            AddEvent(events, elapsed, "skill", unit, unit, Math.Max(0, skill.attack), $"{unit.Name} 使下回合商店攻击提升");
                        }
                        break;
                    case "on_death_next_round_forest_gem":
                        if (unit.PlayerSide)
                        {
                            AddEvent(events, elapsed, "skill", unit, unit, Math.Max(0, skill.value), $"{unit.Name} grants next round forest gem");
                        }
                        break;
                }
            }
        }

        private static RealtimeBattleUnit PickTarget(RealtimeBattleUnit attacker, IEnumerable<RealtimeBattleUnit> defenders)
        {
            return defenders
                .Where(unit => unit.IsAlive)
                .OrderBy(unit => Distance(attacker, unit))
                .ThenBy(unit => attacker.PreferLowestHp ? unit.Hp : 0)
                .ThenByDescending(unit => unit.Row)
                .ThenBy(unit => unit.Hp)
                .FirstOrDefault();
        }

        private static RealtimeBattleUnit ResolveTarget(RealtimeBattleUnit attacker, IEnumerable<RealtimeBattleUnit> defenders)
        {
            attacker.TargetSearchTimer = Math.Max(0f, attacker.TargetSearchTimer - StepSeconds);
            if (attacker.CurrentTarget != null && attacker.CurrentTarget.IsAlive)
            {
                return attacker.CurrentTarget;
            }

            if (attacker.TargetSearchTimer > 0f)
            {
                return null;
            }

            attacker.CurrentTarget = PickTarget(attacker, defenders);
            attacker.TargetSearchTimer = TargetSearchInterval;
            return attacker.CurrentTarget;
        }

        private static RealtimeBattleUnit PickTargetInAttackRange(RealtimeBattleUnit attacker, IEnumerable<RealtimeBattleUnit> defenders)
        {
            return defenders
                .Where(unit => unit.IsAlive && Distance(attacker, unit) <= AttackRange(attacker, unit) + AttackRangeSlack)
                .OrderBy(unit => Distance(attacker, unit))
                .ThenBy(unit => attacker.PreferLowestHp ? unit.Hp : 0)
                .ThenBy(unit => unit.Hp)
                .FirstOrDefault();
        }

        private static void MoveToTarget(RealtimeBattleUnit unit, RealtimeBattleUnit target, float distance, float attackRange)
        {
            var dx = target.X - unit.X;
            var dy = target.Y - unit.Y;
            distance = Math.Max(0.001f, distance);
            var moveSpeed = Math.Max(45f, unit.Speed * 8.4f);
            var step = Math.Min(moveSpeed * StepSeconds, Math.Max(0f, distance - attackRange));
            unit.X += dx / distance * step;
            unit.Y += dy / distance * step;
        }

        private static void LockAttackPosition(RealtimeBattleUnit unit)
        {
            if (unit == null || !unit.HasAttackAnchor)
            {
                return;
            }

            unit.X = unit.AttackAnchorX;
            unit.Y = unit.AttackAnchorY;
        }

        private static void MovePouncerNextToTarget(RealtimeBattleUnit unit, RealtimeBattleUnit target)
        {
            if (unit == null || target == null)
            {
                return;
            }

            var direction = unit.PlayerSide ? -1f : 1f;
            unit.X = target.X + direction * Math.Max(8f, unit.Size + target.Size - 4f);
            unit.Y = target.Y;
            unit.CurrentTarget = target;
            unit.TargetSearchTimer = 0f;
        }

        private static void ResolveCollisions(IReadOnlyList<RealtimeBattleUnit> players, IReadOnlyList<RealtimeBattleUnit> enemies)
        {
            var units = players.Concat(enemies).Where(unit => unit.IsAlive).ToList();
            for (var i = 0; i < units.Count; i += 1)
            {
                for (var j = i + 1; j < units.Count; j += 1)
                {
                    var left = units[i];
                    var right = units[j];
                    var dx = right.X - left.X;
                    var dy = right.Y - left.Y;
                    var distance = Math.Max(0.001f, (float)Math.Sqrt(dx * dx + dy * dy));
                    var minDistance = left.Size + right.Size;
                    if (distance >= minDistance)
                    {
                        continue;
                    }

                    var overlap = minDistance - distance;
                    var leftLocked = left.HasStartedAttacking;
                    var rightLocked = right.HasStartedAttacking;
                    if (leftLocked && rightLocked)
                    {
                        continue;
                    }

                    if (!leftLocked && !rightLocked)
                    {
                        left.X -= dx / distance * overlap * 0.5f;
                        left.Y -= dy / distance * overlap * 0.5f;
                        right.X += dx / distance * overlap * 0.5f;
                        right.Y += dy / distance * overlap * 0.5f;
                    }
                    else if (leftLocked)
                    {
                        right.X += dx / distance * overlap;
                        right.Y += dy / distance * overlap;
                    }
                    else
                    {
                        left.X -= dx / distance * overlap;
                        left.Y -= dy / distance * overlap;
                    }
                }
            }
        }

        private static void TickState(IEnumerable<RealtimeBattleUnit> units)
        {
            foreach (var unit in units.Where(unit => unit.IsAlive))
            {
                unit.StunRemaining = Math.Max(0f, unit.StunRemaining - StepSeconds);
                unit.MoveLockRemaining = Math.Max(0f, unit.MoveLockRemaining - StepSeconds);
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

        private static void ApplyContinuousAuras(IReadOnlyList<RealtimeBattleUnit> units)
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

        private static int CalculateDamage(RealtimeBattleUnit attacker, RealtimeBattleUnit target, Random random = null)
        {
            var damageMin = Math.Max(1, attacker.DamageMin);
            var damageMax = Math.Max(damageMin, attacker.DamageMax);
            var unitDamage = random == null || damageMin == damageMax
                ? (damageMin + damageMax) * 0.5f
                : random.Next(damageMin, damageMax + 1);
            var attackFactor = (20f + Math.Max(0, attacker.Attack)) / Math.Max(1f, 20f + Math.Max(0, target.Defense));
            return Math.Max(1, (int)Math.Round(Math.Max(1, attacker.CurrentCount) * unitDamage * attackFactor));
        }

        private static bool ResolveForceCrit(RealtimeBattleUnit attacker, Random random, out float critMultiplier)
        {
            var forceCrit = false;
            critMultiplier = 0f;
            if (attacker.FirstAttackForceCrit)
            {
                forceCrit = true;
                critMultiplier = Math.Max(critMultiplier, attacker.FirstAttackCritMultiplier);
                attacker.FirstAttackForceCrit = false;
            }

            foreach (var skill in GetBattleSkills(attacker))
            {
                if (skill.kind == "passive_every_nth_attack_force_crit" && attacker.AttackCount > 0 && attacker.AttackCount % Math.Max(1, skill.count) == 0)
                {
                    forceCrit = true;
                    critMultiplier = Math.Max(critMultiplier, Math.Max(1.5f, skill.multiplier));
                }

                if (skill.kind == "on_attack_chance_force_crit" && random.NextDouble() < Math.Max(0f, skill.chance))
                {
                    forceCrit = true;
                }
            }

            return forceCrit;
        }

        private static void AddTemporaryCount(RealtimeBattleUnit unit, int amount)
        {
            if (unit == null || amount <= 0)
            {
                return;
            }

            var hpPerUnit = Math.Max(1, unit.HpPerUnit);
            unit.BaseCount = Math.Max(unit.BaseCount, unit.CurrentCount + amount);
            unit.CurrentCount += amount;
            unit.CurrentTotalHp += amount * hpPerUnit;
            unit.Hp = unit.CurrentTotalHp;
            unit.MaxHp = Math.Max(unit.MaxHp, unit.BaseCount * hpPerUnit);
        }

        private static void SummonUnits(List<RealtimeBattleUnit> allies, RealtimeBattleUnit source, SkillDefinition skill, List<BattleEvent> events, float elapsed)
        {
            var definition = ProphecyGameSession.Instance.Data.FindUnit(skill.summonUnitId);
            if (definition == null)
            {
                return;
            }

            for (var i = 0; i < Math.Max(1, skill.count); i += 1)
            {
                var startCount = ResolveStartCount(definition);
                var hpPerUnit = Math.Max(1, definition.hpPerUnit > 0 ? definition.hpPerUnit : definition.hp > 0 ? definition.hp : 1);
                var summonCount = ResolveSummonUnitCount(allies, definition, skill, startCount);
                var totalHp = Math.Max(1, summonCount * hpPerUnit);
                var snapshot = new BattleUnitSnapshot
                {
                    UnitId = definition.id,
                    Name = definition.name,
                    Star = definition.star,
                    SlotId = $"{source.Row}-{Math.Max(1, source.Col + i + 1)}",
                    MaxHp = totalHp,
                    CurrentHp = totalHp,
                    BaseCount = summonCount,
                    CurrentCount = summonCount,
                    MaxCount = summonCount,
                    HpPerUnit = hpPerUnit,
                    CurrentTotalHp = totalHp,
                    Attack = Math.Max(1, definition.attack),
                    Defense = Math.Max(0, definition.defense),
                    Power = Math.Max(1, definition.power),
                    DamageMin = Math.Max(1, definition.damageMin),
                    DamageMax = Math.Max(Math.Max(1, definition.damageMin), definition.damageMax),
                    Initiative = Math.Max(0, definition.initiative),
                    Speed = Math.Max(1, definition.speed),
                    Luck = Math.Max(0, definition.luck),
                    Morale = Math.Max(0, definition.morale),
                    Range = Math.Max(1f, definition.range),
                    Size = Math.Max(20, definition.size),
                    AttackInterval = Math.Max(0.2f, definition.attackInterval),
                    Summoned = true
                };
                var summoned = new RealtimeBattleUnit(snapshot, source.PlayerSide)
                {
                    Summoned = true,
                    SummonDuration = skill.duration > 0f ? skill.duration : 0f,
                    X = source.X + (source.PlayerSide ? -50f : 50f),
                    Y = source.Y + (i + 1) * 22f
                };
                allies.Add(summoned);
                AddEvent(events, elapsed, "summon", source, summoned, 0, $"{source.Name} summons {summoned.Name}");
            }
        }

        private static BattleEvent AddEvent(List<BattleEvent> events, float time, string kind, RealtimeBattleUnit source, RealtimeBattleUnit target, int amount, string message)
        {
            if (events == null || events.Count >= MaxBattleEvents)
            {
                return null;
            }

            var battleEvent = new BattleEvent
            {
                Time = Math.Max(0f, time),
                Kind = kind,
                SourceUnitId = source?.UnitId,
                SourceName = source?.Name,
                SourcePlayerSide = source?.PlayerSide ?? false,
                SourceSlotId = source?.SlotId,
                SourceHp = source?.Hp ?? 0,
                SourceMaxHp = source?.MaxHp ?? 0,
                SourceShieldLayers = source?.ShieldLayers ?? 0,
                TargetUnitId = target?.UnitId,
                TargetName = target?.Name,
                TargetPlayerSide = target?.PlayerSide ?? false,
                TargetSlotId = target?.SlotId,
                TargetHp = target?.Hp ?? 0,
                TargetMaxHp = target?.MaxHp ?? 0,
                TargetShieldLayers = target?.ShieldLayers ?? 0,
                Amount = amount,
                Message = message
            };
            events.Add(battleEvent);
            return battleEvent;
        }

        private static float Distance(RealtimeBattleUnit left, RealtimeBattleUnit right)
        {
            return Distance(left.X, left.Y, right.X, right.Y);
        }

        private static float AttackRange(RealtimeBattleUnit attacker, RealtimeBattleUnit target)
        {
            return attacker.Range * 60f + attacker.Size + target.Size;
        }

        private static float Distance(float leftX, float leftY, float rightX, float rightY)
        {
            var dx = leftX - rightX;
            var dy = leftY - rightY;
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }

        private static BattleUnitSnapshot CreateSnapshot(RealtimeBattleUnit unit)
        {
            return new BattleUnitSnapshot
            {
                UnitId = unit.UnitId,
                Name = unit.Name,
                Star = unit.Star,
                IsGolden = unit.IsGolden,
                SlotId = unit.SlotId,
                MaxHp = unit.MaxHp,
                CurrentHp = unit.Hp,
                BaseCount = unit.BaseCount,
                CurrentCount = unit.CurrentCount,
                MaxCount = unit.MaxCount,
                HpPerUnit = unit.HpPerUnit,
                CurrentTotalHp = unit.CurrentTotalHp,
                ShieldLayers = unit.ShieldLayers,
                Attack = unit.Attack,
                Defense = unit.Defense,
                Power = unit.Power,
                DamageMin = unit.DamageMin,
                DamageMax = unit.DamageMax,
                Initiative = unit.Initiative,
                Speed = unit.Speed,
                Luck = unit.Luck,
                Morale = unit.Morale,
                Range = unit.Range,
                Size = unit.Size,
                AttackInterval = unit.AttackInterval,
                DamageDone = unit.DamageDone,
                Kills = unit.Kills,
                Summoned = unit.Summoned
            };
        }

        private static IEnumerable<SkillDefinition> GetBattleSkills(RealtimeBattleUnit unit)
        {
            if (unit?.Definition == null)
            {
                return Enumerable.Empty<SkillDefinition>();
            }

            return (unit.IsGolden ? unit.Definition.goldBattleSkills : unit.Definition.battleSkills) ?? Array.Empty<SkillDefinition>();
        }

        private static int CountFaith(IEnumerable<RealtimeBattleUnit> units, string skillFaith, string fallbackFaith)
        {
            var faith = string.IsNullOrWhiteSpace(skillFaith) ? fallbackFaith : skillFaith;
            return units.Count(unit => unit.IsAlive && unit.Faith == faith);
        }

        private static float SkillRefreshSeconds(SkillDefinition skill, float fallback)
        {
            if (skill == null)
            {
                return fallback;
            }

            return skill.refreshSeconds > 0f ? skill.refreshSeconds : skill.refreshRounds > 0 ? skill.refreshRounds : skill.duration > 0f ? skill.duration : fallback;
        }

        private static float SkillIntervalSeconds(SkillDefinition skill, float fallback)
        {
            if (skill == null)
            {
                return fallback;
            }

            return skill.interval > 0f ? skill.interval : skill.intervalRounds > 0 ? skill.intervalRounds : fallback;
        }

        private static float SkillDeathAttackMultiplier(SkillDefinition skill)
        {
            if (skill == null)
            {
                return 1f;
            }

            return Math.Max(1f, skill.deathAttackMultiplier > 0f ? skill.deathAttackMultiplier : skill.attackMultiplier);
        }

        private static float MoraleChance(int morale, float rate)
        {
            return Math.Min(0.95f, Math.Max(0f, morale * MORALE_EXTRA_ATTACK_CHANCE_PER_POINT));
        }

        private static int ResolveStartCount(UnitDefinition definition)
        {
            if (definition == null)
            {
                return 1;
            }

            return Math.Max(1, definition.defaultCount > 0 ? definition.defaultCount : definition.startCount > 0 ? definition.startCount : definition.baseCount > 0 ? definition.baseCount : 1);
        }

        private static void AddBattleStats(RealtimeBattleUnit unit, SkillDefinition skill, int multiplier)
        {
            unit.Attack += skill.attack * multiplier;
            unit.Defense += skill.defense * multiplier;
            unit.MaxHp = Math.Max(1, unit.MaxHp + skill.hp * multiplier);
            unit.Hp = Math.Max(1, unit.Hp + skill.hp * multiplier);
            unit.Power += skill.power * multiplier;
            unit.Speed += skill.speed * multiplier;
        }

        private static bool HasTag(RealtimeBattleUnit unit, string tag)
        {
            return !string.IsNullOrWhiteSpace(tag) && unit.Tags != null && unit.Tags.Contains(tag);
        }

        private static bool MatchesSkillTarget(RealtimeBattleUnit unit, SkillDefinition skill)
        {
            if (unit == null || skill == null)
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(skill.targetId) && unit.UnitId == skill.targetId)
            {
                return true;
            }

            if (!string.IsNullOrWhiteSpace(skill.targetUnitId) && unit.UnitId == skill.targetUnitId)
            {
                return true;
            }

            if (!string.IsNullOrWhiteSpace(skill.type) && unit.Type == skill.type)
            {
                return true;
            }

            return HasTag(unit, skill.type) || HasTag(unit, skill.targetTag);
        }

        private static int IncrementSkillCounter(RealtimeBattleUnit unit, string key)
        {
            if (!unit.SkillCounters.TryGetValue(key, out var count))
            {
                count = 0;
            }

            count += 1;
            unit.SkillCounters[key] = count;
            return count;
        }

        private static bool TickSkillTimer(RealtimeBattleUnit unit, string key, float interval)
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

        private static int CalculateSelfCountLoss(RealtimeBattleUnit unit, SkillDefinition skill)
        {
            var ratio = skill.percent > 0f
                ? skill.percent
                : (skill.selfHpLoss > 0 ? skill.selfHpLoss : skill.damage > 0 ? skill.damage : skill.hp > 0 ? skill.hp : 25) / 100f;
            return Math.Max(1, Math.Min(unit.CurrentCount - 1, (int)Math.Ceiling(unit.CurrentCount * ratio)));
        }

        private static int ApplySelfCountLoss(RealtimeBattleUnit unit, int lossCount)
        {
            var beforeHp = unit.Hp;
            unit.CurrentCount = Math.Max(0, unit.CurrentCount - Math.Max(1, lossCount));
            unit.CurrentTotalHp = Math.Max(0, Math.Min(unit.CurrentTotalHp, unit.CurrentCount * Math.Max(1, unit.HpPerUnit)));
            unit.Hp = unit.CurrentTotalHp;
            return Math.Max(1, beforeHp - unit.Hp);
        }

        private static void AddAreaEffect(List<RealtimeAreaEffect> areaEffects, RealtimeBattleUnit source, RealtimeBattleUnit target, SkillDefinition skill, List<BattleEvent> events, float elapsed)
        {
            if (areaEffects == null || source == null || target == null || skill == null)
            {
                return;
            }

            var tick = skill.tick > 0f ? skill.tick : 0.5f;
            areaEffects.Add(new RealtimeAreaEffect
            {
                Source = source,
                CenterX = target.X,
                CenterY = target.Y,
                Radius = Math.Max(1f, skill.radius) * 80f,
                Remaining = Math.Max(tick, skill.duration > 0f ? skill.duration : tick),
                TickInterval = tick,
                TickTimer = tick,
                Attack = source.Attack,
                CurrentCount = Math.Max(1, source.CurrentCount),
                DamageMin = Math.Max(1, source.DamageMin),
                DamageMax = Math.Max(Math.Max(1, source.DamageMin), source.DamageMax),
                AttackMultiplier = skill.attackMultiplier > 0f ? skill.attackMultiplier : 1f
            });
            AddEvent(events, elapsed, "skill", source, target, 0, $"{source.Name} 召唤火雨");
        }

        private static void TickAreaEffects(List<RealtimeAreaEffect> areaEffects, List<RealtimeBattleUnit> sourceAllies, List<RealtimeBattleUnit> enemies, Random random, List<BattleEvent> events, float elapsed)
        {
            if (areaEffects == null || areaEffects.Count == 0)
            {
                return;
            }

            for (var i = areaEffects.Count - 1; i >= 0; i -= 1)
            {
                var effect = areaEffects[i];
                effect.Remaining -= StepSeconds;
                effect.TickTimer -= StepSeconds;
                if (effect.TickTimer <= 0f)
                {
                    effect.TickTimer += Math.Max(0.1f, effect.TickInterval);
                    var targets = enemies.Where(enemy => enemy.IsAlive && Distance(effect.CenterX, effect.CenterY, enemy.X, enemy.Y) <= effect.Radius).ToList();
                    var damages = targets.Select(target =>
                    {
                        var unitDamage = (Math.Max(1, effect.DamageMin) + Math.Max(Math.Max(1, effect.DamageMin), effect.DamageMax)) * 0.5f;
                        var factor = (20f + Math.Max(0, effect.Attack)) / Math.Max(1f, 20f + Math.Max(0, target.Defense));
                        return Math.Max(1, (int)Math.Round(Math.Max(1, effect.CurrentCount) * unitDamage * factor * Math.Max(0.1f, effect.AttackMultiplier)));
                    }).ToList();
                    DealAreaDamageSimultaneously(effect.Source, targets, damages, sourceAllies, enemies, random, events, elapsed, $"{effect.Source.Name} 火雨命中");
                }

                if (effect.Remaining <= 0f)
                {
                    areaEffects.RemoveAt(i);
                }
            }
        }

        private static int ResolveSummonUnitCount(IReadOnlyList<RealtimeBattleUnit> allies, UnitDefinition definition, SkillDefinition skill, int fallbackStartCount)
        {
            if (skill == null || definition == null)
            {
                return Math.Max(1, fallbackStartCount);
            }

            if (skill.mode == "highest_unit_count")
            {
                var targetUnitId = !string.IsNullOrWhiteSpace(skill.targetUnitId)
                    ? skill.targetUnitId
                    : !string.IsNullOrWhiteSpace(skill.targetId)
                        ? skill.targetId
                        : skill.summonUnitId;
                var highestCount = (allies ?? Array.Empty<RealtimeBattleUnit>())
                    .Where(ally => ally != null && ally.IsAlive && ally.UnitId == targetUnitId)
                    .Select(ally => Math.Max(0, ally.CurrentCount))
                    .DefaultIfEmpty(0)
                    .Max();
                var multiplier = skill.ratio > 0f ? skill.ratio : 1f;
                var scaledCount = (int)Math.Floor(highestCount * multiplier);
                return Math.Max(Math.Max(1, skill.threshold), scaledCount);
            }

            return Math.Max(1, skill.value > 0 ? skill.value : fallbackStartCount);
        }

        private static void ResolveInstantFireRain(RealtimeBattleUnit source, RealtimeBattleUnit centerTarget, List<RealtimeBattleUnit> sourceAllies, List<RealtimeBattleUnit> enemies, Random random, SkillDefinition skill, List<BattleEvent> events, float elapsed)
        {
            if (source == null || centerTarget == null || skill == null)
            {
                return;
            }

            AddEvent(events, elapsed, "skill", source, centerTarget, 0, $"{source.Name} 召唤火雨");
            var radius = Math.Max(1f, skill.radius) * 80f;
            var targets = enemies.Where(enemy => enemy.IsAlive && Distance(centerTarget.X, centerTarget.Y, enemy.X, enemy.Y) <= radius).ToList();
            var damages = targets.Select(target => Math.Max(1, (int)Math.Round(CalculateDamage(source, target, random) * Math.Max(1f, skill.attackMultiplier)))).ToList();
            DealAreaDamageSimultaneously(source, targets, damages, sourceAllies, enemies, random, events, elapsed, $"{source.Name} 火雨命中");
        }

        private static void DealAreaDamageSimultaneously(RealtimeBattleUnit source, List<RealtimeBattleUnit> targets, List<int> damages, List<RealtimeBattleUnit> sourceAllies, List<RealtimeBattleUnit> targetAllies, Random random, List<BattleEvent> events, float elapsed, string message)
        {
            if (source == null || targets == null || damages == null)
            {
                return;
            }

            var count = Math.Min(targets.Count, damages.Count);
            for (var index = 0; index < count; index += 1)
            {
                var target = targets[index];
                if (target != null && target.IsAlive)
                {
                    AddEvent(events, elapsed, "skill", source, target, 0, $"{message} {target.Name}");
                }
            }

            for (var index = 0; index < count; index += 1)
            {
                var target = targets[index];
                if (target != null && target.IsAlive)
                {
                    DealDamage(source, target, Math.Max(1, damages[index]), sourceAllies, targetAllies, random, events, elapsed);
                }
            }
        }

        private static void ResolveFirstHitsCounterattack(RealtimeBattleUnit target, RealtimeBattleUnit attacker, List<RealtimeBattleUnit> targetAllies, List<RealtimeBattleUnit> attackerAllies, Random random, List<BattleEvent> events, float elapsed)
        {
            if (target == null || attacker == null || !target.IsAlive || !attacker.IsAlive)
            {
                return;
            }

            foreach (var skill in GetBattleSkills(target).Where(skill => skill.kind == "first_hits_counterattack"))
            {
                var limit = skill.count > 0 ? skill.count : int.MaxValue;
                if (target.ForcedCounterattackTriggers >= limit)
                {
                    continue;
                }

                target.ForcedCounterattackTriggers += 1;
                var repeat = Math.Max(1, skill.repeat);
                for (var index = 0; index < repeat && target.IsAlive && attacker.IsAlive; index += 1)
                {
                    ResolveAttackDamage(target, attacker, targetAllies, attackerAllies, random, events, elapsed, $"{target.Name} counter {attacker.Name}", false);
                    target.CounterCount += 1;
                }
            }
        }

        private sealed class RealtimeBattleUnit
        {
            public RealtimeBattleUnit(BattleUnitSnapshot snapshot, bool playerSide)
            {
                UnitId = snapshot.UnitId;
                Name = snapshot.Name;
                Star = snapshot.Star;
                IsGolden = snapshot.IsGolden;
                SlotId = snapshot.SlotId;
                Definition = ProphecyGameSession.Instance.Data.FindUnit(snapshot.UnitId);
                Race = Definition?.race;
                Faith = Definition?.faith;
                Type = Definition?.type;
                Tags = Definition?.tags ?? Array.Empty<string>();
                BaseCount = Math.Max(1, snapshot.BaseCount > 0 ? snapshot.BaseCount : snapshot.CurrentCount > 0 ? snapshot.CurrentCount : ResolveStartCount(Definition));
                MaxCount = BaseCount;
                HpPerUnit = Math.Max(1, snapshot.HpPerUnit > 0 ? snapshot.HpPerUnit : Definition != null && Definition.hpPerUnit > 0 ? Definition.hpPerUnit : Definition != null && Definition.hp > 0 ? Definition.hp : 1);
                CurrentCount = Math.Max(0, snapshot.CurrentCount > 0 ? snapshot.CurrentCount : BaseCount);
                CurrentTotalHp = Math.Max(0, snapshot.CurrentTotalHp > 0 ? snapshot.CurrentTotalHp : CurrentCount * HpPerUnit);
                MaxHp = Math.Max(1, BaseCount * HpPerUnit);
                Hp = Math.Max(0, CurrentTotalHp);
                ShieldLayers = Math.Max(0, snapshot.ShieldLayers);
                Attack = Math.Max(1, snapshot.Attack);
                Defense = Math.Max(0, snapshot.Defense);
                Power = Math.Max(1, snapshot.Power);
                DamageMin = Math.Max(1, snapshot.DamageMin > 0 ? snapshot.DamageMin : Definition?.damageMin ?? 1);
                DamageMax = Math.Max(DamageMin, snapshot.DamageMax > 0 ? snapshot.DamageMax : Definition?.damageMax ?? DamageMin);
                Initiative = Math.Max(0, snapshot.Initiative > 0 ? snapshot.Initiative : Definition?.initiative ?? 0);
                Speed = Math.Max(1, snapshot.Speed);
                Luck = Math.Max(0, snapshot.Luck);
                Morale = Math.Max(0, snapshot.Morale);
                Range = Math.Max(1f, snapshot.Range);
                Size = Math.Max(20, snapshot.Size);
                AttackInterval = Math.Max(0.2f, snapshot.AttackInterval);
                PlayerSide = playerSide;
                Summoned = snapshot.Summoned;
                TeamForestGiftTotal = ProphecyGameSession.Instance.CurrentRun?.manageResources?.forestGiftTotal ?? 0;
                TryParseSlot(SlotId, out Row, out Col);
                X = playerSide ? 260f + (4 - Row) * 80f : 1540f - (4 - Row) * 80f;
                Y = 240f + (Col - 1) * 90f;
            }

            public string UnitId;
            public string Name;
            public int Star;
            public bool IsGolden;
            public string SlotId;
            public UnitDefinition Definition;
            public string Race;
            public string Faith;
            public string Type;
            public string[] Tags = Array.Empty<string>();
            public int MaxHp;
            public int Hp;
            public int BaseCount;
            public int CurrentCount;
            public int MaxCount;
            public int HpPerUnit;
            public int CurrentTotalHp;
            public int Attack;
            public int Defense;
            public int Power;
            public int DamageMin;
            public int DamageMax;
            public int Initiative;
            public int Speed;
            public int Luck;
            public int Morale;
            public float Range;
            public int Size;
            public float AttackInterval;
            public float AttackTimer;
            public bool PlayerSide;
            public int Row;
            public int Col;
            public float X;
            public float Y;
            public int DamageDone;
            public int AttackCount;
            public int Kills;
            public int CounterCount;
            public int MoraleExtraCount;
            public int ForcedCounterattackTriggers;
            public bool HasStartedAttacking;
            public bool HasAttackAnchor;
            public float AttackAnchorX;
            public float AttackAnchorY;
            public RealtimeBattleUnit CurrentTarget;
            public float TargetSearchTimer;
            public int ShieldLayers;
            public float ShieldRefreshInterval;
            public float ShieldRefreshTimer;
            public float StunRemaining;
            public float MoveLockRemaining;
            public float InvincibleRemaining;
            public bool Summoned;
            public float SummonDuration;
            public bool DeathProcessed;
            public int TeamForestGiftTotal;
            public bool FirstAttackForceCrit;
            public float FirstAttackCritMultiplier;
            public bool PreferLowestHp;
            public RealtimeBattleUnit FirstAttackBacklineSnipeTarget;
            public bool PreferBackline;
            public float DelayedSnipeTimer;
            public float DelayedSnipeAttackMultiplier;
            public float DelayedSnipeCritMultiplier;
            public float DelayedSnipeCritDistance;
            public RealtimeBattleUnit ForestGemDeathMarkSource;
            public int ForestGemDeathMarkAmount;
            public readonly Dictionary<string, int> SkillCounters = new Dictionary<string, int>();
            public readonly Dictionary<string, float> SkillTimers = new Dictionary<string, float>();
            public bool IsAlive => Hp > 0 && CurrentCount > 0;
        }

        private sealed class RealtimeAreaEffect
        {
            public RealtimeBattleUnit Source;
            public float CenterX;
            public float CenterY;
            public float Radius;
            public float Remaining;
            public float TickInterval;
            public float TickTimer;
            public int Attack;
            public int CurrentCount;
            public int DamageMin;
            public int DamageMax;
            public float AttackMultiplier;
        }

        private static void TryParseSlot(string slotId, out int row, out int col)
        {
            row = 2;
            col = 1;
            if (string.IsNullOrWhiteSpace(slotId))
            {
                return;
            }

            var parts = slotId.Split('-');
            if (parts.Length != 2)
            {
                return;
            }

            int.TryParse(parts[0], out row);
            int.TryParse(parts[1], out col);
            row = Math.Max(1, row);
            col = Math.Max(1, col);
        }
    }

    public sealed class BattleRealtimeResult
    {
        public bool Victory;
        public float BattleTime;
        public int PlayerDamage;
        public int EnemyDamage;
        public string Summary;
        public List<BattleEvent> Events = new List<BattleEvent>();
        public List<BattleUnitSnapshot> InitialPlayerUnits = new List<BattleUnitSnapshot>();
        public List<BattleUnitSnapshot> InitialEnemyUnits = new List<BattleUnitSnapshot>();
        public List<BattleUnitSnapshot> PlayerUnits = new List<BattleUnitSnapshot>();
        public List<BattleUnitSnapshot> EnemyUnits = new List<BattleUnitSnapshot>();
    }
}
