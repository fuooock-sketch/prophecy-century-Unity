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
        private const float TargetSearchInterval = 1f;
        private const int MaxBattleSafetyRounds = 10000;
        private const int MaxBattleEvents = 12000;
        private const int BattleHexColumnCount = 13;
        private const int BattleHexMaxRows = 6;
        private const float LUCK_CRIT_CHANCE_PER_POINT = 0.06f;
        private const float LUCK_CRIT_DAMAGE_MULTIPLIER = 1.5f;
        private const float MORALE_EXTRA_ATTACK_CHANCE_PER_POINT = 0.04f;
        private static readonly int[] BattleHexRowsByColumn = { 6, 5, 6, 5, 6, 5, 6, 5, 6, 5, 6, 5, 6 };

        public BattleStubResult Resolve(RunState runState)
        {
            var random = new Random(runState.round * 7919 + runState.boardUnits.Count * 131);
            var players = BuildPlayerUnits(runState);
            var enemies = BuildEnemyUnits(runState, random);
            var initialPlayerUnits = players.Select(CreateSnapshot).ToList();
            var initialEnemyUnits = enemies.Select(CreateSnapshot).ToList();
            var events = new List<BattleEvent>();
            AddEvent(events, 0f, "start", null, null, 0, "Battle start");
            ResolveBattleStart(players, enemies, random, events, 0f);
            ResolveBattleStart(enemies, players, random, events, 0f);
            ApplyContinuousAuras(players);
            ApplyContinuousAuras(enemies);
            var playerScore = EstimateScore(players);
            var enemyScore = EstimateScore(enemies);

            if (players.Count == 0)
            {
                return Finish(runState, false, playerScore, enemyScore, 15, 0, players, enemies, events, initialPlayerUnits, initialEnemyUnits);
            }

            var attacks = 0;
            var elapsed = 0f;
            var safetyLimitReached = false;
            for (var round = 1; players.Any(unit => unit.IsAlive) && enemies.Any(unit => unit.IsAlive); round += 1)
            {
                if (round > MaxBattleSafetyRounds)
                {
                    safetyLimitReached = true;
                    AddEvent(events, elapsed, "safety_limit", null, null, round, $"Battle safety limit reached after {MaxBattleSafetyRounds} rounds");
                    break;
                }

                AddEvent(events, elapsed, "round", null, null, round, $"Round {round}");
                ResolveBattleRoundStart(players, enemies, round, random, events, elapsed);
                ResolveBattleRoundStart(enemies, players, round, random, events, elapsed);
                ApplyContinuousAuras(players);
                ApplyContinuousAuras(enemies);
                var turnOrder = players.Concat(enemies)
                    .Where(unit => unit.IsAlive)
                    .OrderByDescending(unit => unit.Initiative)
                    .ThenByDescending(unit => unit.Speed)
                    .ThenByDescending(unit => unit.Attack)
                    .ThenByDescending(unit => unit.CurrentHp)
                    .ThenBy(unit => unit.PlayerSide ? 0 : 1)
                    .ThenBy(unit => unit.SlotId)
                    .ToList();

                foreach (var unit in turnOrder)
                {
                    if (!players.Any(item => item.IsAlive) || !enemies.Any(item => item.IsAlive))
                    {
                        break;
                    }

                    if (!unit.IsAlive)
                    {
                        continue;
                    }

                    var allies = unit.PlayerSide ? players : enemies;
                    var defenders = unit.PlayerSide ? enemies : players;
                    TakeHexTurn(unit, allies, defenders, random, ref attacks, events, ref elapsed);
                    TickBattleState(players, events, elapsed);
                    TickBattleState(enemies, events, elapsed);
                    ApplyContinuousAuras(players);
                    ApplyContinuousAuras(enemies);
                }
            }

            var playerAlive = players.Any(unit => unit.IsAlive);
            var enemyAlive = enemies.Any(unit => unit.IsAlive);
            var victory = playerAlive && !enemyAlive;
            if (safetyLimitReached && playerAlive && enemyAlive)
            {
                var playerAliveCount = players.Count(unit => unit.IsAlive);
                var enemyAliveCount = enemies.Count(unit => unit.IsAlive);
                victory = playerAliveCount == enemyAliveCount
                    ? TotalAliveHp(players) >= TotalAliveHp(enemies)
                    : playerAliveCount > enemyAliveCount;
            }
            else if (playerAlive == enemyAlive)
            {
                victory = TotalAliveHp(players) >= TotalAliveHp(enemies);
            }

            var damage = victory
                ? 0
                : runState.isExplorationBattle
                    ? CalculateFateDamage(runState, enemies)
                    : CalculateHpLoss(runState, enemies);
            return Finish(runState, victory, playerScore, enemyScore, damage, attacks, players, enemies, events, initialPlayerUnits, initialEnemyUnits);
        }

        public BattlePreviewResult CreatePreview(RunState runState)
        {
            var random = new Random(runState.round * 7919 + runState.boardUnits.Count * 131);
            var players = BuildPlayerUnits(runState);
            var enemies = BuildEnemyUnits(runState, random);
            var initialPlayerUnits = players.Select(CreateSnapshot).ToList();
            var initialEnemyUnits = enemies.Select(CreateSnapshot).ToList();
            ResolveBattleStart(players, enemies, random);
            ResolveBattleStart(enemies, players, random);
            ApplyContinuousAuras(players);
            ApplyContinuousAuras(enemies);

            return new BattlePreviewResult
            {
                PlayerScore = EstimateScore(players),
                EnemyScore = EstimateScore(enemies),
                InitialPlayerUnits = initialPlayerUnits,
                InitialEnemyUnits = initialEnemyUnits,
                PlayerUnits = players.Select(CreateSnapshot).ToList(),
                EnemyUnits = enemies.Select(CreateSnapshot).ToList()
            };
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

        private static void TakeHexTurn(BattleRuntimeUnit actor, List<BattleRuntimeUnit> allies, List<BattleRuntimeUnit> defenders, Random random, ref int attacks, List<BattleEvent> events, ref float elapsed)
        {
            if (actor == null || !actor.IsAlive)
            {
                return;
            }

            AddEvent(events, elapsed, "turn", actor, null, 0, $"{actor.Name} turn");
            if (actor.StunTurns > 0)
            {
                actor.StunTurns -= 1;
                AddEvent(events, elapsed, "turn_skip", actor, null, 0, $"{actor.Name} skips stunned turn");
                elapsed += 0.2f;
                return;
            }

            if (actor.StunRemaining > 0f)
            {
                AddEvent(events, elapsed, "turn_skip", actor, null, 0, $"{actor.Name} skips action");
                elapsed += 0.2f;
                return;
            }

            ResolveActionSelfShieldIfNone(actor, events, elapsed);
            var target = PickHexTurnTarget(actor, defenders);
            if (target == null)
            {
                elapsed += 0.1f;
                return;
            }

            actor.CurrentTarget = target;
            if (IsInHexAttackRange(actor, target))
            {
                ResolveAllyActionTempCountBonuses(actor, allies, events, elapsed);
                ApplyTurnAttack(actor, target, allies, defenders, random, ref attacks, events, elapsed);
                elapsed += 0.35f;
                return;
            }

            if (actor.MoveLockTurns > 0)
            {
                actor.MoveLockTurns -= 1;
                AddEvent(events, elapsed, "turn_skip", actor, target, 0, $"{actor.Name} is move locked");
                elapsed += 0.2f;
                return;
            }

            var occupied = BuildOccupiedHexSet(allies, defenders, actor);
            var destination = PickHexMoveDestination(actor, target, occupied, random);
            if (!destination.HasValue)
            {
                elapsed += 0.2f;
                return;
            }

            var path = FindHexPath(actor.HexColumn, actor.HexRow, destination.Value.Column, destination.Value.Row, occupied);
            if (path == null || path.Count == 0)
            {
                elapsed += 0.2f;
                return;
            }

            var maxSteps = Math.Max(0, actor.Speed);
            var route = new List<HexCoord>();
            for (var i = 0; i < path.Count && i < maxSteps; i += 1)
            {
                var step = path[i];
                route.Add(step);
                if (Math.Max(1f, actor.Definition != null ? actor.Definition.range : 1f) >= HexDistance(step.Column, step.Row, target.HexColumn, target.HexRow))
                {
                    break;
                }
            }

            if (route.Count == 0)
            {
                elapsed += 0.12f;
                return;
            }

            var finalStep = route[route.Count - 1];
            var finalDestinationSlotId = FormatHexSlot(finalStep.Column, finalStep.Row);
            ResolveAllyActionTempCountBonuses(actor, allies, events, elapsed);
            AddEvent(
                events,
                elapsed,
                "move",
                actor,
                target,
                0,
                $"{actor.Name} moves",
                finalDestinationSlotId,
                string.Join("|", route.Select(step => FormatHexSlot(step.Column, step.Row))));
            actor.HexColumn = finalStep.Column;
            actor.HexRow = finalStep.Row;
            actor.Row = finalStep.Row;
            actor.Col = finalStep.Column;
            actor.SlotId = finalDestinationSlotId;
            elapsed += 0.18f * route.Count;

            if (target.IsAlive && IsInHexAttackRange(actor, target))
            {
                ApplyTurnAttack(actor, target, allies, defenders, random, ref attacks, events, elapsed);
                elapsed += 0.35f;
                return;
            }

            elapsed += 0.12f;
        }

        private static void ResolveAllyActionTempCountBonuses(BattleRuntimeUnit actor, List<BattleRuntimeUnit> allies, List<BattleEvent> events, float elapsed)
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
                    receiver.SkillTriggers += 1;
                    AddEvent(events, elapsed, "count_gain", actor, receiver, gain, $"{receiver.Name} 因 {actor.Name} 行动，临时数量 +{gain}");
                }
            }
        }

        private static void ResolveActionSelfShieldIfNone(BattleRuntimeUnit actor, List<BattleEvent> events, float elapsed)
        {
            if (actor == null || !actor.IsAlive || actor.ShieldLayers > 0)
            {
                return;
            }

            foreach (var skill in GetBattleSkills(actor))
            {
                switch (skill.kind)
                {
                    case "battle_action_self_shield_if_none":
                        actor.ShieldLayers = Math.Max(actor.ShieldLayers, Math.Max(1, skill.layers));
                        actor.SkillTriggers += 1;
                        AddEvent(events, elapsed, "shield", actor, actor, actor.ShieldLayers, $"{actor.Name} gains action shield");
                        return;
                }
            }
        }

        private static void ApplyTurnAttack(BattleRuntimeUnit attacker, BattleRuntimeUnit target, List<BattleRuntimeUnit> allies, List<BattleRuntimeUnit> defenders, Random random, ref int attacks, List<BattleEvent> events, float elapsed)
        {
            if (attacker == null || target == null || !attacker.IsAlive || !target.IsAlive)
            {
                return;
            }

            ApplyAttack(attacker, target, allies, defenders, random, null, false, false, true, events, elapsed);
            attacks += 1;
            TryApplyMoraleExtraAttack(attacker, target, allies, defenders, random, ref attacks, events, elapsed);
        }

        private static void TryApplyMoraleExtraAttack(BattleRuntimeUnit attacker, BattleRuntimeUnit target, List<BattleRuntimeUnit> allies, List<BattleRuntimeUnit> defenders, Random random, ref int attacks, List<BattleEvent> events, float elapsed)
        {
            if (attacker == null || !attacker.IsAlive)
            {
                return;
            }

            var moraleExtraTarget = target != null && target.IsAlive ? target : PickHexTurnTarget(attacker, defenders);
            var moraleExtraChance = MoraleChance(attacker.Morale, ProphecyGameSession.Instance.Data.Config?.moraleExtraAttackRate ?? 0.08f);
            var moraleExtraRoll = random.NextDouble();
            AddEvent(events, elapsed, "morale_check", attacker, moraleExtraTarget, (int)Math.Round(moraleExtraChance * 1000), $"{attacker.Name} morale {attacker.Morale} roll {moraleExtraRoll:0.000} chance {moraleExtraChance:0.000}");
            if (moraleExtraTarget == null || moraleExtraRoll >= moraleExtraChance)
            {
                return;
            }

            AddEvent(events, elapsed, "morale_extra", attacker, moraleExtraTarget, 0, $"{attacker.Name} 触发追击");
            ApplyAttack(attacker, moraleExtraTarget, allies, defenders, random, null, false, true, false, events, elapsed);
            attacker.MoraleExtraCount += 1;
            attacks += 1;
        }

        private static BattleRuntimeUnit PickHexTurnTarget(BattleRuntimeUnit attacker, IEnumerable<BattleRuntimeUnit> defenders)
        {
            return defenders
                .Where(unit => unit.IsAlive)
                .OrderBy(unit => HexDistance(attacker.HexColumn, attacker.HexRow, unit.HexColumn, unit.HexRow))
                .ThenBy(unit => unit.CurrentHp)
                .ThenBy(unit => unit.SlotId)
                .FirstOrDefault();
        }

        private static void ResolveBattleRoundStart(List<BattleRuntimeUnit> allies, List<BattleRuntimeUnit> enemies, int round, Random random, List<BattleEvent> events, float elapsed)
        {
            foreach (var unit in allies.Where(unit => unit.IsAlive).ToList())
            {
                foreach (var skill in GetBattleSkills(unit))
                {
                    switch (skill.kind)
                    {
                        case "battle_round_self_hp_loss_team_temp_attack":
                            if (!IsRoundInterval(round, skill))
                            {
                                break;
                            }

                            if (unit.CurrentCount <= 1 || !allies.Any(ally => ally.IsAlive && ally != unit))
                            {
                                break;
                            }

                            var lossCount = CalculateSelfCountLoss(unit, skill);
                            AddEvent(events, elapsed, "skill", unit, unit, lossCount, $"{unit.Name} loses troops and rallies allies");
                            var hpLoss = ApplySelfCountLoss(unit, lossCount);
                            AddEvent(events, elapsed, "damage", unit, unit, hpLoss, $"{unit.Name} loses troops");

                            foreach (var ally in allies.Where(ally => ally.IsAlive && ally != unit))
                            {
                                var attackGain = Math.Max(0, skill.attack);
                                ally.Attack += attackGain;
                                if (attackGain > 0)
                                {
                                    AddEvent(events, elapsed, "buff_attack", unit, ally, attackGain, $"{ally.Name} 攻击提升");
                                }
                            }
                            unit.SkillTriggers += 1;
                            break;
                        case "battle_start_self_refreshing_shield":
                            var refreshRounds = SkillRefreshRounds(skill);
                            if (refreshRounds > 0 && round > 1 && (round - 1) % refreshRounds == 0)
                            {
                                unit.ShieldLayers = Math.Max(unit.ShieldLayers, Math.Max(1, skill.layers));
                                AddEvent(events, elapsed, "shield", unit, unit, unit.ShieldLayers, $"{unit.Name} refreshes shield");
                                unit.SkillTriggers += 1;
                            }
                            break;
                    }
                }
            }
        }

        private static bool IsInHexAttackRange(BattleRuntimeUnit attacker, BattleRuntimeUnit target)
        {
            if (attacker == null || target == null)
            {
                return false;
            }

            var range = attacker.Definition != null ? attacker.Definition.range : 1f;
            return Math.Max(1f, range) >= HexDistance(attacker.HexColumn, attacker.HexRow, target.HexColumn, target.HexRow);
        }

        private static HashSet<string> BuildOccupiedHexSet(IEnumerable<BattleRuntimeUnit> allies, IEnumerable<BattleRuntimeUnit> defenders, BattleRuntimeUnit movingUnit)
        {
            return new HashSet<string>(allies.Concat(defenders)
                .Where(unit => unit != null && unit != movingUnit && unit.IsAlive)
                .Select(unit => HexKey(unit.HexColumn, unit.HexRow)));
        }

        private static HexCoord? PickHexMoveDestination(BattleRuntimeUnit actor, BattleRuntimeUnit target, HashSet<string> occupied, Random random)
        {
            var candidates = GetHexNeighbors(target.HexColumn, target.HexRow)
                .Where(coord => !occupied.Contains(HexKey(coord.Column, coord.Row)))
                .Select(coord => new
                {
                    Coord = coord,
                    Path = FindHexPath(actor.HexColumn, actor.HexRow, coord.Column, coord.Row, occupied)
                })
                .Where(item => item.Path != null)
                .ToList();

            if (candidates.Count == 0)
            {
                return null;
            }

            var bestDistance = candidates.Min(item => item.Path.Count);
            var best = candidates.Where(item => item.Path.Count == bestDistance).ToList();
            return best[random.Next(best.Count)].Coord;
        }

        private static void TickSide(List<BattleRuntimeUnit> attackers, List<BattleRuntimeUnit> defenders, Random random, List<BattleAreaEffect> areaEffects, ref int attacks, List<BattleEvent> events = null, float elapsed = 0f)
        {
            foreach (var attacker in attackers.Where(unit => unit.IsAlive))
            {
                var target = ResolveTarget(attacker, defenders);
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

                if (target == null)
                {
                    continue;
                }

                ApplyAttack(attacker, target, attackers, defenders, random, areaEffects, false, false, true, events, elapsed);
                attacks += 1;
                attacker.Cooldown += Math.Max(0.2f, attacker.AttackInterval);

                var moraleExtraTarget = target.IsAlive ? target : PickTarget(attacker, defenders);
                var moraleExtraChance = MoraleChance(attacker.Morale, ProphecyGameSession.Instance.Data.Config?.moraleExtraAttackRate ?? 0.08f);
                var moraleExtraRoll = random.NextDouble();
                AddEvent(events, elapsed, "morale_check", attacker, moraleExtraTarget, (int)Math.Round(moraleExtraChance * 1000), $"{attacker.Name} morale {attacker.Morale} roll {moraleExtraRoll:0.000} chance {moraleExtraChance:0.000}");
                if (attacker.IsAlive && moraleExtraTarget != null && moraleExtraRoll < moraleExtraChance)
                {
                    AddEvent(events, elapsed, "morale_extra", attacker, moraleExtraTarget, 0, $"{attacker.Name} 触发追击");
                    ApplyAttack(attacker, moraleExtraTarget, attackers, defenders, random, areaEffects, false, true, false, events, elapsed);
                    attacker.MoraleExtraCount += 1;
                    attacks += 1;
                }

                if (target.IsAlive)
                {
                    var counterChance = MoraleChance(target.Morale, ProphecyGameSession.Instance.Data.Config?.moraleCounterRate ?? 0.06f);
                    if (random.NextDouble() < counterChance)
                    {
                        ApplyAttack(target, attacker, defenders, attackers, random, null, true, false, false, events, elapsed);
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
                .OrderBy(unit => Distance(attacker, unit))
                .ThenBy(unit => attacker.PreferLowestHp ? unit.CurrentHp : 0)
                .ThenByDescending(unit => attacker.PreferBackline ? unit.Row : 0)
                .ThenByDescending(unit => unit.Row)
                .ThenBy(unit => unit.CurrentHp)
                .FirstOrDefault();
        }

        private static BattleRuntimeUnit ResolveTarget(BattleRuntimeUnit attacker, IEnumerable<BattleRuntimeUnit> defenders)
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

        private static void ApplyAttack(BattleRuntimeUnit attacker, BattleRuntimeUnit target, List<BattleRuntimeUnit> allies, List<BattleRuntimeUnit> enemies, Random random, List<BattleAreaEffect> areaEffects, bool isCounter, bool isMoraleExtra, bool isPrimaryAttack, List<BattleEvent> events = null, float elapsed = 0f)
        {
            if (attacker == null || target == null || !attacker.IsAlive || !target.IsAlive)
            {
                return;
            }

            var attackType = isCounter ? "counter" : isMoraleExtra ? "morale attack" : "attack";
            ResolvePreAttack(attacker, random, out var forceCrit, out var critMultiplier);
            var damage = CalculateDamage(attacker, target, random);
            var critRate = Math.Min(ProphecyGameSession.Instance.Data.Config?.critRateCap ?? 0.95f, Math.Max(0f, attacker.Luck * LUCK_CRIT_CHANCE_PER_POINT));
            var luckyCrit = !forceCrit && random.NextDouble() < critRate;
            var didCrit = forceCrit || luckyCrit;
            if (didCrit)
            {
                damage = (int)Math.Ceiling(damage * Math.Max(critMultiplier, LUCK_CRIT_DAMAGE_MULTIPLIER));
                foreach (var ally in allies.Where(unit => unit.IsAlive && unit != attacker))
                {
                    foreach (var skill in GetBattleSkills(ally).Where(skill => skill.kind == "on_ally_crit_self_temp_power"))
                    {
                        ally.Power += Math.Max(1, skill.value);
                        ally.SkillTriggers += 1;
                    }
                }
            }

            if (luckyCrit)
            {
                AddEvent(events, elapsed, "lucky_crit", attacker, target, 0, $"{attacker.Name} 幸运！");
            }

            AddEvent(events, elapsed, "attack", attacker, target, 0, $"{attacker.Name} {attackType} {target.Name}");
            var actualDamage = DealDamage(attacker, target, damage, allies, enemies, random, events, elapsed, didCrit);
            if (actualDamage <= 0)
            {
                return;
            }

            attacker.AttackCount += isPrimaryAttack ? 1 : 0;
            ResolveOnAttack(attacker, target, allies, enemies, random, areaEffects, actualDamage, isPrimaryAttack, events, elapsed);
        }

        private static int DealDamage(BattleRuntimeUnit source, BattleRuntimeUnit target, int damage, List<BattleRuntimeUnit> sourceAllies, List<BattleRuntimeUnit> targetAllies, Random random, List<BattleEvent> events = null, float elapsed = 0f, bool critical = false)
        {
            if (target.ShieldLayers > 0)
            {
                target.ShieldLayers -= 1;
                AddEvent(events, elapsed, "block", source, target, 0, $"{target.Name} shield blocks damage");
                return 0;
            }

            if (target.InvincibleRemaining > 0f)
            {
                AddEvent(events, elapsed, "immune", source, target, 0, $"{target.Name} is immune");
                return 0;
            }

            var before = target.CurrentHp;
            target.CurrentTotalHp = Math.Max(0, target.CurrentTotalHp - Math.Max(1, damage));
            target.CurrentCount = target.CurrentTotalHp <= 0 ? 0 : (int)Math.Ceiling(target.CurrentTotalHp / (float)Math.Max(1, target.HpPerUnit));
            target.CurrentHp = target.CurrentTotalHp;
            target.MaxHp = Math.Max(target.MaxHp, target.BaseCount * Math.Max(1, target.HpPerUnit));
            var actualDamage = before - target.CurrentHp;
            source.DamageDone += actualDamage;
            if (actualDamage > 0)
            {
                AddEvent(events, elapsed, critical ? "critical_damage" : "damage", source, target, actualDamage, $"{source.Name} deals {actualDamage} damage to {target.Name}");
                target.DamagedCount += 1;
                ResolveDamaged(target);
            }

            if ((target.CurrentHp <= 0 || target.CurrentCount <= 0) && before > 0)
            {
                source.KillCount += 1;
                ResolveKill(source);
                AddEvent(events, elapsed, "death", source, target, 0, $"{target.Name} dies");
                ResolveDeath(target, source, targetAllies, sourceAllies, random, events, elapsed);
            }

            return actualDamage;
        }

        private static BattleStubResult Finish(
            RunState runState,
            bool victory,
            int playerScore,
            int enemyScore,
            int hpLoss,
            int attacks,
            IReadOnlyList<BattleRuntimeUnit> players,
            IReadOnlyList<BattleRuntimeUnit> enemies,
            List<BattleEvent> events,
            List<BattleUnitSnapshot> initialPlayerUnits = null,
            List<BattleUnitSnapshot> initialEnemyUnits = null)
        {
            if (hpLoss > 0)
            {
                runState.playerHp -= hpLoss;
                if (runState.isExplorationBattle)
                {
                    runState.fateValue = Math.Max(0, runState.playerHp);
                }
            }

            ApplyPostBattleRewards(runState, victory, players, events);
            var playerAlive = players.Count(unit => unit.IsAlive);
            var enemyAlive = enemies.Count(unit => unit.IsAlive);
            var playerDamage = players.Sum(unit => unit.DamageDone);
            var enemyDamage = enemies.Sum(unit => unit.DamageDone);
            var lossLabel = runState.isExplorationBattle ? $"fate -{hpLoss}" : $"lost {hpLoss} HP";
            var summary = victory
                ? $"Victory. Player score {playerScore}, enemy score {enemyScore}, player units alive {playerAlive}, attacks {attacks}."
                : $"Defeat. Player score {playerScore}, enemy score {enemyScore}, enemy units alive {enemyAlive}, {lossLabel}.";
            var finishTime = events != null && events.Count > 0 ? events[events.Count - 1].Time + StepSeconds : 0f;
            AddEvent(events, finishTime, victory ? "victory" : "defeat", null, null, hpLoss, summary);
            return new BattleStubResult
            {
                Victory = victory,
                PlayerScore = playerScore,
                EnemyScore = enemyScore,
                HpDelta = -hpLoss,
                PlayerDamage = playerDamage,
                EnemyDamage = enemyDamage,
                Summary = summary,
                Events = events ?? new List<BattleEvent>(),
                InitialPlayerUnits = initialPlayerUnits ?? players.Select(CreateSnapshot).ToList(),
                InitialEnemyUnits = initialEnemyUnits ?? enemies.Select(CreateSnapshot).ToList(),
                PlayerUnits = players.Select(CreateSnapshot).ToList(),
                EnemyUnits = enemies.Select(CreateSnapshot).ToList()
            };
        }

        private static BattleEvent AddEvent(List<BattleEvent> events, float time, string kind, BattleRuntimeUnit source, BattleRuntimeUnit target, int amount, string message, string destinationSlotId = null, string routeSlotIds = null)
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
                SourceInstanceId = source?.InstanceId,
                SourceName = source?.Name,
                SourcePlayerSide = source?.PlayerSide ?? false,
                SourceSlotId = source?.SlotId,
                SourceHp = source?.CurrentHp ?? 0,
                SourceMaxHp = source?.MaxHp ?? 0,
                SourceShieldLayers = source?.ShieldLayers ?? 0,
                TargetUnitId = target?.UnitId,
                TargetInstanceId = target?.InstanceId,
                TargetName = target?.Name,
                TargetPlayerSide = target?.PlayerSide ?? false,
                TargetSlotId = target?.SlotId,
                TargetHp = target?.CurrentHp ?? 0,
                TargetMaxHp = target?.MaxHp ?? 0,
                TargetShieldLayers = target?.ShieldLayers ?? 0,
                DestinationSlotId = destinationSlotId,
                RouteSlotIds = routeSlotIds,
                Amount = amount,
                Message = message
            };
            events.Add(battleEvent);
            return battleEvent;
        }

        private static BattleUnitSnapshot CreateSnapshot(BattleRuntimeUnit unit)
        {
            return new BattleUnitSnapshot
            {
                UnitId = unit.UnitId,
                InstanceId = unit.InstanceId,
                Name = unit.Name,
                Star = unit.Definition?.star ?? 1,
                IsGolden = unit.IsGolden,
                SlotId = unit.SlotId,
                MaxHp = unit.MaxHp,
                CurrentHp = unit.CurrentHp,
                BaseCount = unit.BaseCount,
                InitialCount = unit.InitialCount,
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
                Range = Math.Max(1f, unit.Definition?.range ?? 1f),
                Size = Math.Max(20, unit.Definition?.size ?? 35),
                AttackInterval = unit.AttackInterval,
                DamageDone = unit.DamageDone,
                Kills = unit.KillCount,
                Summoned = unit.Summoned
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
            if (CustomChallengeSystem.IsCustomChallengeId(runState?.campaignId))
            {
                var customEnemies = BuildCustomChallengeEnemyRuntimeUnits(runState);
                if (customEnemies.Count > 0)
                {
                    return customEnemies;
                }
            }

            var preset = data.FindEnemyPreset(runState.explorationBattleEnemyPresetId);
            if (preset != null)
            {
                var presetUnits = BuildEnemyRuntimeUnitsFromPreset(preset, runState);
                if (presetUnits.Count > 0)
                {
                    return presetUnits;
                }
            }

            var campaignMultiplier = CampaignEnemyMultiplier(runState.campaignId);
            var budget = Math.Max(6, (int)Math.Round(GetCumulativeGoldForRound(runState.round) * 0.88f + runState.round * 0.7f));
            var maxStar = Math.Min(6, 1 + Math.Max(0, runState.round - 1) / 2);
            var maxUnits = Math.Min(10, Math.Max(2, 2 + (int)Math.Floor(runState.round * 0.85f)));
            var meleePool = data.Units
                .Where(unit => IsEnemyCandidate(unit, maxStar) && unit.type == "melee")
                .ToList();
            var rangedPool = data.Units
                .Where(unit => IsEnemyCandidate(unit, maxStar) && unit.type == "range")
                .ToList();
            var fallbackPool = data.Units
                .Where(unit => IsEnemyCandidate(unit, maxStar))
                .ToList();
            var frontPositions = new[] { "1-1", "2-1", "2-2", "3-2" };
            var rearPositions = new[] { "3-1", "3-3", "4-2", "4-1", "4-3", "4-4" };
            var enemies = new List<BattleRuntimeUnit>();
            var roster = new List<UnitDefinition>();
            var remaining = budget;
            var desiredTotal = Math.Min(maxUnits, Math.Max(2, (int)Math.Round(budget / 7.2f)));
            var desiredFront = Math.Max(1, (int)Math.Ceiling(desiredTotal * 0.45f));

            while (roster.Count < desiredTotal && remaining >= 3)
            {
                var fillingFront = roster.Count(unit => unit.type == "melee") < desiredFront;
                var pool = fillingFront
                    ? (meleePool.Count > 0 ? meleePool : fallbackPool)
                    : (rangedPool.Count > 0 ? rangedPool : meleePool.Count > 0 ? meleePool : fallbackPool);
                var picked = PickEnemyCandidate(pool, remaining, random);
                if (picked == null)
                {
                    break;
                }

                roster.Add(picked);
                remaining -= Math.Max(3, GetThreatCost(picked));
                if (remaining < 3 && roster.Count < 2)
                {
                    remaining = 3;
                }
            }

            if (roster.Count == 0)
            {
                roster.Add(data.FindUnit("bright_warrior") ?? fallbackPool.FirstOrDefault());
                roster.Add(data.FindUnit("monk") ?? fallbackPool.FirstOrDefault());
            }

            var ordered = roster
                .Where(unit => unit != null)
                .OrderBy(unit => unit.type == "melee" ? 0 : 1)
                .ToList();
            var usedSlots = new HashSet<string>();
            foreach (var picked in ordered)
            {
                var slotPool = picked.type == "range" ? rearPositions : frontPositions;
                var slot = slotPool.FirstOrDefault(position => usedSlots.Add(position));
                if (string.IsNullOrWhiteSpace(slot))
                {
                    slot = frontPositions.Concat(rearPositions).FirstOrDefault(position => usedSlots.Add(position)) ?? "4-1";
                }

                var runtime = CreateEnemyRuntimeUnit(picked, slot, runState.round, GetThreatCost(picked), campaignMultiplier);
                if (runtime != null)
                {
                    enemies.Add(runtime);
                }
            }

            return enemies;
        }

        private static List<BattleRuntimeUnit> BuildCustomChallengeEnemyRuntimeUnits(RunState runState)
        {
            var enemies = new List<BattleRuntimeUnit>();
            if (runState == null || !CustomChallengeSystem.TryGetRound(runState.customChallengeId, runState.round, out var challengeRound))
            {
                return enemies;
            }

            var usedSlots = new HashSet<string>();
            foreach (var unit in challengeRound.units ?? new List<CustomChallengeUnitState>())
            {
                if (unit == null || string.IsNullOrWhiteSpace(unit.unitId))
                {
                    continue;
                }

                var definition = ProphecyGameSession.Instance.Data.FindUnit(unit.unitId);
                if (definition == null)
                {
                    continue;
                }

                var slotId = IsSupportedBattleSlot(unit.slotId)
                    ? unit.slotId
                    : ResolvePresetFallbackSlot(definition, usedSlots);
                if (!usedSlots.Add(slotId))
                {
                    slotId = ResolvePresetFallbackSlot(definition, usedSlots);
                    usedSlots.Add(slotId);
                }

                var state = new UnitCardState
                {
                    unitId = definition.id,
                    name = string.IsNullOrWhiteSpace(unit.name) ? definition.name : unit.name,
                    star = unit.star > 0 ? unit.star : definition.star,
                    isGolden = unit.isGolden,
                    baseCount = Math.Max(1, unit.count),
                    maxCount = 0
                };
                var runtime = CreateRuntimeUnit(state, false, definition, slotId, 1f);
                if (runtime != null)
                {
                    enemies.Add(runtime);
                }
            }

            return enemies;
        }

        public static List<BattleUnitSnapshot> BuildEnemyUnitSnapshotsFromPreset(EnemyPresetDefinition preset)
        {
            return BuildEnemyRuntimeUnitsFromPreset(preset, null)
                .Select(CreateSnapshot)
                .ToList();
        }

        private static List<BattleRuntimeUnit> BuildEnemyRuntimeUnitsFromPreset(EnemyPresetDefinition preset, RunState runState)
        {
            var enemies = new List<BattleRuntimeUnit>();
            if (preset?.units == null)
            {
                return enemies;
            }

            var usedSlots = new HashSet<string>();
            foreach (var presetUnit in preset.units)
            {
                if (presetUnit == null || string.IsNullOrWhiteSpace(presetUnit.unitId))
                {
                    continue;
                }

                var definition = ProphecyGameSession.Instance.Data.FindUnit(presetUnit.unitId);
                if (definition == null)
                {
                    continue;
                }

                var slotId = IsSupportedBattleSlot(presetUnit.slotId)
                    ? presetUnit.slotId
                    : ResolvePresetFallbackSlot(definition, usedSlots);
                if (!usedSlots.Add(slotId))
                {
                    slotId = ResolvePresetFallbackSlot(definition, usedSlots);
                    usedSlots.Add(slotId);
                }

                var state = new UnitCardState
                {
                    unitId = definition.id,
                    name = definition.name,
                    star = presetUnit.star > 0 ? presetUnit.star : definition.star,
                    baseCount = presetUnit.count > 0 ? presetUnit.count : ResolveStartCount(definition),
                    maxCount = 0
                };
                var runtime = CreateRuntimeUnit(state, false, definition, slotId, 1f);
                if (runtime != null)
                {
                    enemies.Add(runtime);
                }
            }

            if (!IsFixedCapturedPreset(preset))
            {
                FillWorldMapPresetLineup(enemies, usedSlots, runState);
                ApplyWorldMapPresetScaling(enemies, runState);
            }

            return enemies;
        }

        private static bool IsFixedCapturedPreset(EnemyPresetDefinition preset)
        {
            return preset != null
                && !string.IsNullOrWhiteSpace(preset.id)
                && (preset.id.StartsWith("shadow_elemental_", StringComparison.Ordinal)
                    || preset.id.StartsWith("shadow_light_", StringComparison.Ordinal)
                    || preset.id.StartsWith("snow_peak_defense_", StringComparison.Ordinal));
        }

        private static void FillWorldMapPresetLineup(List<BattleRuntimeUnit> enemies, HashSet<string> usedSlots, RunState runState)
        {
            if (enemies == null || usedSlots == null || runState == null || !runState.isExplorationBattle)
            {
                return;
            }

            var targetCount = ResolveWorldMapMinEnemyUnits(runState);
            if (targetCount <= enemies.Count)
            {
                return;
            }

            var data = ProphecyGameSession.Instance?.Data;
            if (data?.Units == null)
            {
                return;
            }

            var day = Math.Max(runState.dayCount, runState.round);
            var maxStar = Math.Min(6, Math.Max(1, 1 + (day - 1) / 3));
            var existingIds = new HashSet<string>(enemies.Select(unit => unit.UnitId).Where(id => !string.IsNullOrWhiteSpace(id)));
            var pool = data.Units
                .Where(unit => IsEnemyCandidate(unit, maxStar))
                .OrderByDescending(unit => unit.star)
                .ThenBy(unit => unit.type == "range" ? 1 : 0)
                .ThenBy(unit => unit.id)
                .ToList();

            while (enemies.Count < targetCount && usedSlots.Count < 10)
            {
                var preferRange = enemies.Count(unit => unit.Type == "range") < Math.Max(1, targetCount / 3);
                var picked = pool.FirstOrDefault(unit => !existingIds.Contains(unit.id) && ((preferRange && unit.type == "range") || (!preferRange && unit.type != "range")))
                    ?? pool.FirstOrDefault(unit => !existingIds.Contains(unit.id))
                    ?? pool.FirstOrDefault();
                if (picked == null)
                {
                    return;
                }

                var slotId = ResolvePresetFallbackSlot(picked, usedSlots);
                if (!usedSlots.Add(slotId))
                {
                    return;
                }

                var state = new UnitCardState
                {
                    unitId = picked.id,
                    name = picked.name,
                    star = picked.star,
                    baseCount = Math.Max(1, Math.Min(4, ResolveStartCount(picked))),
                    maxCount = 0
                };
                var runtime = CreateRuntimeUnit(state, false, picked, slotId, 1f);
                if (runtime == null)
                {
                    continue;
                }

                enemies.Add(runtime);
                existingIds.Add(picked.id);
            }
        }

        private static void ApplyWorldMapPresetScaling(List<BattleRuntimeUnit> enemies, RunState runState)
        {
            if (enemies == null || enemies.Count == 0 || runState == null || !runState.isExplorationBattle)
            {
                return;
            }

            var baseScore = EstimateScore(enemies);
            if (baseScore <= 0)
            {
                return;
            }

            var scoreBasis = ResolveWorldMapExpectedPlayerScore(runState);
            if (scoreBasis <= 0)
            {
                return;
            }

            var targetScore = (int)Math.Round(scoreBasis * WorldMapEnemyTargetRatio(runState));
            if (targetScore <= baseScore)
            {
                return;
            }

            var countMultiplier = Math.Min(600f, targetScore / (float)baseScore);
            foreach (var enemy in enemies)
            {
                ScaleEnemyCount(enemy, countMultiplier);
            }
        }

        private static int ResolveWorldMapExpectedPlayerScore(RunState runState)
        {
            var day = Math.Max(runState?.dayCount ?? 1, runState?.round ?? 1);
            var curve = ProphecyGameSession.Instance?.Data?.Config?.worldMapExpectedPlayerScoreByDay;
            if (curve != null && curve.Length > 0)
            {
                var index = Math.Min(Math.Max(day, 1), curve.Length) - 1;
                return Math.Max(0, curve[index]);
            }

            var fallbackCurve = new[]
            {
                120, 250, 450, 800, 1200, 1700, 2300, 2900, 3600, 4300,
                5100, 5900, 6700, 7500, 8300, 9100, 10000, 10900, 11800, 12800
            };
            if (day <= fallbackCurve.Length)
            {
                return fallbackCurve[day - 1];
            }

            return fallbackCurve[fallbackCurve.Length - 1];
        }

        private static int ResolveWorldMapMinEnemyUnits(RunState runState)
        {
            var day = Math.Max(runState?.dayCount ?? 1, runState?.round ?? 1);
            var curve = ProphecyGameSession.Instance?.Data?.Config?.worldMapMinEnemyUnitsByDay;
            if (curve != null && curve.Length > 0)
            {
                var index = Math.Min(Math.Max(day, 1), curve.Length) - 1;
                return Math.Max(1, Math.Min(10, curve[index]));
            }

            if (runState?.explorationBattleNodeType == "boss" || runState?.explorationBattleNodeType == "boss_guard")
            {
                return 6;
            }

            return day <= 2 ? 2 : day <= 5 ? 3 : day <= 9 ? 4 : 5;
        }

        private static float WorldMapEnemyTargetRatio(RunState runState)
        {
            var day = Math.Max(runState?.dayCount ?? 1, runState?.round ?? 1);
            switch (runState?.explorationBattleNodeType)
            {
                case "boss":
                    return 1.20f;
                case "boss_guard":
                    return 1.10f;
                case "elite_battle":
                    return 1.05f;
                case "hard_battle":
                case "guard_battle":
                    return 0.95f;
                case "pressure_battle":
                    return day <= 4 ? 0.62f : day <= 8 ? 0.85f : 1.05f;
                case "normal_battle":
                    return day <= 2 ? 0.50f : Math.Min(0.68f, 0.42f + day * 0.018f);
                default:
                    return 0.55f;
            }
        }

        private static void ScaleEnemyCount(BattleRuntimeUnit enemy, float countMultiplier)
        {
            if (enemy == null || countMultiplier <= 1f)
            {
                return;
            }

            var count = Math.Max(1, (int)Math.Round(enemy.BaseCount * countMultiplier));
            var hpPerUnit = Math.Max(1, enemy.HpPerUnit);
            enemy.BaseCount = count;
            enemy.InitialCount = count;
            enemy.CurrentCount = count;
            enemy.MaxCount = count;
            enemy.MaxHp = Math.Max(1, count * hpPerUnit);
            enemy.CurrentHp = enemy.MaxHp;
            enemy.CurrentTotalHp = enemy.MaxHp;
        }

        private static bool IsSupportedBattleSlot(string slotId)
        {
            return !string.IsNullOrWhiteSpace(slotId)
                && TryMapInitialSlotToHex(slotId, false, out _, out _);
        }

        private static string ResolvePresetFallbackSlot(UnitDefinition definition, HashSet<string> usedSlots)
        {
            var frontPositions = new[] { "1-1", "2-1", "2-2", "3-2" };
            var rearPositions = new[] { "3-1", "3-3", "4-2", "4-1", "4-3", "4-4" };
            var preferred = definition != null && definition.type == "range" ? rearPositions : frontPositions;
            return preferred.Concat(frontPositions).Concat(rearPositions)
                .FirstOrDefault(slot => !usedSlots.Contains(slot)) ?? "4-1";
        }

        private static bool IsEnemyCandidate(UnitDefinition unit, int maxStar)
        {
            return unit != null && !unit.hidden && unit.star <= maxStar && unit.id != "light_illusion";
        }

        private static int GetCumulativeGoldForRound(int round)
        {
            return (int)Math.Round((round * (round + 5)) / 2f);
        }

        private static int GetThreatCost(UnitDefinition unit)
        {
            return Math.Max(3, Math.Max(1, unit?.star ?? 1) * 3);
        }

        private static UnitDefinition PickEnemyCandidate(IReadOnlyList<UnitDefinition> pool, int remaining, Random random)
        {
            if (pool == null || pool.Count == 0)
            {
                return null;
            }

            var source = pool.Where(unit => GetThreatCost(unit) <= Math.Max(remaining, 3)).ToList();
            if (source.Count == 0)
            {
                source = pool.ToList();
            }

            var weighted = new List<UnitDefinition>();
            foreach (var unit in source)
            {
                var starBias = Math.Max(1, unit.star);
                var rangeBias = unit.type == "range" ? 1 : 2;
                var valueBias = Math.Max(1, (int)Math.Round((remaining + 5) / (float)Math.Max(4, GetThreatCost(unit))));
                var weight = starBias + rangeBias + Math.Min(4, valueBias);
                for (var i = 0; i < weight; i += 1)
                {
                    weighted.Add(unit);
                }
            }

            return weighted.Count == 0 ? null : weighted[random.Next(weighted.Count)];
        }

        private static BattleRuntimeUnit CreateEnemyRuntimeUnit(UnitDefinition definition, string slotId, int round, int threatCost, float campaignMultiplier)
        {
            var enemy = CreateRuntimeUnit(null, false, definition, slotId, 1f);
            if (enemy == null)
            {
                return null;
            }

            // New unit model keeps per-unit stats fixed; encounter difficulty should come from lineup/value tuning.
            return enemy;
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
            if (!TryMapInitialSlotToHex(slotId, playerSide, out var hexColumn, out var hexRow))
            {
                hexColumn = playerSide ? 0 : BattleHexColumnCount - 1;
                hexRow = 0;
            }

            var baseCount = ResolveBaseCount(definition, state);
            var hpPerUnit = Math.Max(1, definition.hpPerUnit > 0 ? definition.hpPerUnit : definition.hp > 0 ? definition.hp : 1);
            var hp = Math.Max(1, baseCount * hpPerUnit);
            var attack = Scale(definition.attack + (state?.shopBuffAttack ?? 0) + (state?.roundTempAttack ?? 0) + (state?.boardAuraAttack ?? 0), multiplier);
            var defense = Scale(definition.defense + (state?.shopBuffDefense ?? 0), multiplier);
            var power = Math.Max(0, definition.power + (state?.shopBuffPower ?? 0) + (state?.roundTempPower ?? 0));
            var initiative = Math.Max(0, definition.initiative);
            var speed = Math.Max(0, definition.speed + (state?.shopBuffSpeed ?? 0));
            var morale = Math.Max(0, definition.morale + (state?.shopBuffMorale ?? 0) + (state?.roundTempMorale ?? 0));
            var luck = Math.Max(0, definition.luck + (state?.shopBuffLuck ?? 0));
            var interval = definition.attackInterval > 0 ? definition.attackInterval : 1f;
            interval = Math.Max(0.2f, interval * (100f / (100f + speed * 2f)));

            return new BattleRuntimeUnit
            {
                UnitId = definition.id,
                InstanceId = $"{(playerSide ? "P" : "E")}:{slotId}:{definition.id}",
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
                BoardRow = row,
                BoardCol = col,
                Row = hexRow,
                Col = hexColumn,
                HexColumn = hexColumn,
                HexRow = hexRow,
                MaxHp = Math.Max(1, hp),
                CurrentHp = Math.Max(1, hp),
                BaseCount = baseCount,
                InitialCount = baseCount,
                CurrentCount = baseCount,
                MaxCount = baseCount,
                HpPerUnit = hpPerUnit,
                CurrentTotalHp = hp,
                Attack = Math.Max(1, attack),
                Defense = Math.Max(0, defense),
                Power = power,
                DamageMin = Math.Max(1, definition.damageMin),
                DamageMax = Math.Max(Math.Max(1, definition.damageMin), definition.damageMax),
                Initiative = initiative,
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
            unit.Initiative = Math.Max(unit.Initiative, definition.initiative);
            unit.Speed = Math.Max(unit.Speed, definition.speed);
            unit.AttackInterval = definition.attackInterval > 0 ? Math.Max(0.2f, definition.attackInterval) : unit.AttackInterval;
        }

        private static void ResolveBattleStart(List<BattleRuntimeUnit> allies, List<BattleRuntimeUnit> enemies, Random random, List<BattleEvent> events = null, float elapsed = 0f)
        {
            var initialAllies = allies.ToList();
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
                            var shieldTargets = allies
                                .Where(ally => ally.IsAlive)
                                .OrderBy(_ => random.Next())
                                .Take(skill.count > 0 ? Math.Max(1, skill.count) : int.MaxValue);
                            foreach (var ally in shieldTargets)
                            {
                                ally.ShieldLayers += Math.Max(1, skill.layers);
                                AddEvent(events, elapsed, "shield", unit, ally, ally.ShieldLayers, $"{ally.Name} gains shield");
                            }
                            unit.SkillTriggers += 1;
                            break;
                        case "battle_start_front_occupied_rows_shield":
                            var frontRows = allies
                                .Where(ally => ally.IsAlive && ally.BoardRow > 0)
                                .Select(ally => ally.BoardRow)
                                .Distinct()
                                .OrderBy(row => row)
                                .Take(Math.Max(1, skill.count));
                            var frontRowSet = new HashSet<int>(frontRows);
                            foreach (var ally in allies.Where(ally => ally.IsAlive && frontRowSet.Contains(ally.BoardRow)))
                            {
                                ally.ShieldLayers += Math.Max(1, skill.layers);
                                AddEvent(events, elapsed, "shield", unit, ally, ally.ShieldLayers, $"{ally.Name} gains shield");
                            }
                            unit.SkillTriggers += frontRowSet.Count > 0 ? 1 : 0;
                            break;
                        case "battle_start_self_refreshing_shield":
                            unit.ShieldLayers += Math.Max(1, skill.layers);
                            if (skill.refreshRounds <= 0)
                            {
                                unit.ShieldRefreshInterval = Math.Max(0.1f, SkillRefreshSeconds(skill, 5f));
                                unit.ShieldRefreshTimer = unit.ShieldRefreshInterval;
                            }
                            unit.SkillTriggers += 1;
                            AddEvent(events, elapsed, "shield", unit, unit, unit.ShieldLayers, $"{unit.Name} gains shield");
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
                        case "battle_start_team_count_per_faith_count":
                            var teamCountGain = CountFaith(allies, skill.faith, unit.Faith) * Math.Max(1, skill.value);
                            foreach (var ally in allies.Where(ally => ally.IsAlive))
                            {
                                AddTemporaryCount(ally, teamCountGain);
                            }
                            unit.SkillTriggers += teamCountGain > 0 ? 1 : 0;
                            break;
                        case "battle_start_self_stats_per_faith_count":
                            var faithCount = CountFaith(allies, skill.faith, unit.Faith);
                            AddBattleStats(unit, skill, faithCount);
                            break;
                        case "battle_start_self_count_percent_per_faith_count":
                            var selfFaithCount = CountFaith(initialAllies, skill.faith, unit.Faith);
                            ApplySelfCountPercentPerFaithCount(unit, selfFaithCount, skill);
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
                            unit.DelayedSnipeCritical = SkillSnipeIsCritical(unit, skill);
                            unit.DelayedSnipeMultiplier = SkillSnipeMultiplier(unit, skill);
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
                            var target = allies.Where(ally => ally.IsAlive).OrderBy(ally => ally.CurrentCount).ThenBy(ally => Distance(unit, ally)).FirstOrDefault();
                            if (target != null)
                            {
                                AddTemporaryCount(target, Math.Max(1, skill.value));
                                unit.SkillTriggers += 1;
                            }
                            break;
                        case "battle_start_delay_snipe_backline":
                            unit.DelayedSnipeTimer = Math.Max(0.1f, skill.delay);
                            unit.DelayedSnipeCritical = SkillSnipeIsCritical(unit, skill);
                            unit.DelayedSnipeMultiplier = SkillSnipeMultiplier(unit, skill);
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
                            SummonUnits(allies, enemies, unit, skill, random, events, elapsed);
                            unit.SkillTriggers += 1;
                            break;
                        case "battle_start_summon_and_buff_type":
                            SummonUnits(allies, enemies, unit, skill, random, events, elapsed);
                            foreach (var ally in allies.Where(ally => ally.IsAlive && MatchesSkillTarget(ally, skill)))
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
                                var damage = Math.Max(1, (int)Math.Round(CalculateDamage(unit, pounceTarget, random) * multiplier));
                                if (skill.forceCrit)
                                {
                                    damage = (int)Math.Ceiling(damage * (ProphecyGameSession.Instance.Data.Config?.critDamageMultiple ?? 1.5f));
                                }

                                var pounceEvent = AddEvent(events, elapsed, "skill", unit, pounceTarget, 0, $"{unit.Name} pounces {pounceTarget.Name}");
                                MovePouncerNextToTarget(unit, pounceTarget, allies, enemies);
                                if (pounceEvent != null)
                                {
                                    pounceEvent.DestinationSlotId = unit.SlotId;
                                }
                                for (var hit = 0; hit < Math.Max(1, skill.times); hit += 1)
                                {
                                    if (!pounceTarget.IsAlive)
                                    {
                                        break;
                                    }

                                    DealDamage(unit, pounceTarget, damage, allies, enemies, random, events, elapsed, skill.forceCrit);
                                }

                                pounceTarget.StunTurns = Math.Max(pounceTarget.StunTurns, skill.stunTurns);
                                pounceTarget.StunRemaining = Math.Max(pounceTarget.StunRemaining, skill.stunSeconds);
                                if (skill.stunTurns > 0)
                                {
                                    AddEvent(events, elapsed, "control", unit, pounceTarget, skill.stunTurns, $"{pounceTarget.Name} stunned for {skill.stunTurns} turns");
                                }
                                else if (skill.stunSeconds > 0f)
                                {
                                    AddEvent(events, elapsed, "control", unit, pounceTarget, (int)Math.Round(skill.stunSeconds * 1000f), $"{pounceTarget.Name} stunned for {skill.stunSeconds:0.#}s");
                                }

                                unit.SkillTriggers += 1;
                            }
                            break;
                        case "battle_start_lock_highest_hp_targets":
                            foreach (var locked in enemies.Where(enemy => enemy.IsAlive).OrderByDescending(enemy => enemy.CurrentHp).Take(Math.Max(1, skill.count)))
                            {
                                if (skill.moveLockTurns > 0)
                                {
                                    locked.MoveLockTurns = Math.Max(locked.MoveLockTurns, skill.moveLockTurns);
                                    AddEvent(events, elapsed, "control", unit, locked, skill.moveLockTurns, $"{locked.Name} move locked for {skill.moveLockTurns} turns");
                                }
                                else
                                {
                                    locked.StunRemaining = Math.Max(locked.StunRemaining, skill.duration);
                                    AddEvent(events, elapsed, "control", unit, locked, (int)Math.Round(Math.Max(0.1f, skill.duration) * 1000f), $"{locked.Name} locked for {skill.duration:0.#}s");
                                }
                            }
                            unit.SkillTriggers += 1;
                            break;
                    }
                }
            }
        }

        private static void ResolveOnAttack(BattleRuntimeUnit attacker, BattleRuntimeUnit target, List<BattleRuntimeUnit> allies, List<BattleRuntimeUnit> enemies, Random random, List<BattleAreaEffect> areaEffects, int damage, bool isPrimaryAttack, List<BattleEvent> events = null, float elapsed = 0f)
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
                            AddEvent(events, elapsed, "shield", attacker, attacker, attacker.ShieldLayers, $"{attacker.Name} gains shield");
                        }
                        break;
                    case "on_attack_count_summon":
                        if (IncrementSkillCounter(attacker, skill.kind) >= Math.Max(1, skill.count))
                        {
                            attacker.SkillCounters[skill.kind] = 0;
                            SummonUnits(allies, enemies, attacker, skill, random, events, elapsed);
                            attacker.SkillTriggers += 1;
                        }
                        break;
                    case "on_attack_count_fire_rain_area_dot":
                        if (IncrementSkillCounter(attacker, skill.kind) >= Math.Max(1, skill.count))
                        {
                            attacker.SkillCounters[skill.kind] = 0;
                            AddAreaEffect(areaEffects, attacker, target, skill, events, elapsed);
                            attacker.SkillTriggers += 1;
                        }
                        break;
                    case "on_attack_count_formula_aoe":
                        if (IncrementSkillCounter(attacker, skill.kind) >= Math.Max(1, skill.count))
                        {
                            attacker.SkillCounters[skill.kind] = 0;
                            foreach (var areaTarget in enemies.Where(enemy => enemy.IsAlive && enemy != target && Distance(enemy, target) <= Math.Max(1f, skill.radius)).ToList())
                            {
                                var areaDamage = CalculateDamage(attacker, areaTarget, random);
                                AddEvent(events, elapsed, "skill", attacker, areaTarget, 0, $"{attacker.Name} 触发范围攻击");
                                DealDamage(attacker, areaTarget, areaDamage, allies, enemies, random, events, elapsed);
                            }

                            attacker.SkillTriggers += 1;
                        }
                        break;
                    case "on_attack_if_team_gift_total_aoe":
                        if (attacker.TeamForestGiftTotal >= Math.Max(1, skill.threshold))
                        {
                            foreach (var areaTarget in enemies.Where(enemy => enemy.IsAlive && enemy != target && Distance(enemy, target) <= Math.Max(1f, skill.radius)).ToList())
                            {
                                AddEvent(events, elapsed, "skill", attacker, areaTarget, 0, $"{attacker.Name} triggers gift area attack");
                                DealDamage(attacker, areaTarget, Math.Max(1, damage), allies, enemies, random, events, elapsed);
                            }

                            attacker.SkillTriggers += 1;
                        }
                        break;
                    case "on_attack_mark_target_next_round_forest_gem_on_death":
                        target.ForestGemDeathMarkSource = attacker;
                        target.ForestGemDeathMarkAmount = Math.Max(1, skill.value);
                        AddEvent(events, elapsed, "skill", attacker, target, target.ForestGemDeathMarkAmount, $"{attacker.Name} marks {target.Name} for next round forest gem");
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
                    critMultiplier = Math.Max(critMultiplier, Math.Max(1.5f, skill.multiplier));
                    attacker.SkillTriggers += 1;
                }

                if (skill.kind == "first_hits_force_crit_if_count_multiplier" && attacker.AttackCount < Math.Max(1, skill.count))
                {
                    forceCrit = true;
                    var initialCount = Math.Max(1, attacker.InitialCount);
                    var threshold = Math.Max(1, skill.threshold);
                    var multiplier = attacker.CurrentCount > initialCount * threshold
                        ? Math.Max(1.5f, skill.multiplier)
                        : ProphecyGameSession.Instance.Data.Config?.critDamageMultiple ?? 1.5f;
                    critMultiplier = Math.Max(critMultiplier, multiplier);
                    attacker.SkillTriggers += 1;
                }

                if (skill.kind == "on_attack_chance_force_crit" && random.NextDouble() < Math.Max(0f, skill.chance))
                {
                    forceCrit = true;
                    attacker.SkillTriggers += 1;
                }
            }
        }

        private static void ResolveDeath(BattleRuntimeUnit unit, BattleRuntimeUnit killer, List<BattleRuntimeUnit> allies, List<BattleRuntimeUnit> enemies, Random random, List<BattleEvent> events = null, float elapsed = 0f)
        {
            if (unit.DeathProcessed)
            {
                return;
            }

            unit.DeathProcessed = true;
            ResolveAllyDeathTaggedPower(unit, allies);
            if (unit.ForestGemDeathMarkSource != null && unit.ForestGemDeathMarkSource.PlayerSide)
            {
                unit.ForestGemDeathMarkSource.PendingRoundForestGems += Math.Max(1, unit.ForestGemDeathMarkAmount);
                AddEvent(events, elapsed, "skill", unit.ForestGemDeathMarkSource, unit, Math.Max(1, unit.ForestGemDeathMarkAmount), $"{unit.ForestGemDeathMarkSource.Name} gains next round forest gem");
            }

            foreach (var skill in GetBattleSkills(unit))
            {
                switch (skill.kind)
                {
                    case "battle_start_and_death_summon_units":
                        SummonUnits(allies, enemies, unit, skill, random, events, elapsed);
                        unit.SkillTriggers += 1;
                        break;
                    case "battle_periodic_nearby_enemies_attack_and_death_explode":
                    case "on_death_explode":
                    case "on_death_explode_if_hits_next_round_team_attack":
                    case "on_death_explode_if_hits_next_round_team_count":
                        var hitCount = 0;
                        foreach (var enemy in enemies.Where(enemy => enemy.IsAlive && Distance(unit, enemy) <= Math.Max(1, skill.radius)).ToList())
                        {
                            var multiplier = skill.kind == "battle_periodic_nearby_enemies_attack_and_death_explode"
                                ? SkillDeathAttackMultiplier(skill)
                                : Math.Max(1f, skill.attackMultiplier);
                            var damage = Math.Max(1, skill.damage > 0 ? skill.damage : (int)Math.Round(CalculateDamage(unit, enemy) * multiplier));
                            AddEvent(events, elapsed, "skill", unit, enemy, 0, $"{unit.Name} 死亡爆炸");
                            DealDamage(unit, enemy, damage, allies, enemies, random, events, elapsed);
                            hitCount += 1;
                        }

                        if (unit.PlayerSide
                            && (skill.kind == "on_death_explode_if_hits_next_round_team_count" || skill.kind == "on_death_explode_if_hits_next_round_team_attack")
                            && hitCount >= Math.Max(1, skill.hitThreshold))
                        {
                            foreach (var ally in allies.Where(ally => ally.SourceState != null))
                            {
                                ally.PendingRoundTempCount += Math.Max(0, skill.nextRoundCount > 0 ? skill.nextRoundCount : skill.nextRoundAttack);
                            }

                            AddEvent(events, elapsed, "skill", unit, unit, Math.Max(0, skill.nextRoundCount > 0 ? skill.nextRoundCount : skill.nextRoundAttack), $"{unit.Name} grants next round team count");
                        }

                        unit.SkillTriggers += 1;
                        break;
                    case "on_death_next_round_shop_cards_gain_attack":
                        if (unit.PlayerSide)
                        {
                            unit.PendingNextRoundShopBuffAttack += Math.Max(0, skill.attack);
                            AddEvent(events, elapsed, "skill", unit, unit, Math.Max(0, skill.attack), $"{unit.Name} 使下回合商店攻击提升");
                        }

                        unit.SkillTriggers += 1;
                        break;
                    case "on_death_next_round_forest_gem":
                        if (unit.PlayerSide)
                        {
                            unit.PendingRoundForestGems += Math.Max(0, skill.value);
                            AddEvent(events, elapsed, "skill", unit, unit, Math.Max(0, skill.value), $"{unit.Name} grants next round forest gem");
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

                if (skill.kind == "on_damaged_survive_next_round_forest_gem" && unit.IsAlive)
                {
                    unit.PendingRoundForestGems += Math.Max(1, skill.value);
                    unit.SkillTriggers += 1;
                }
            }
        }

        private static int CalculateDamage(BattleRuntimeUnit attacker, BattleRuntimeUnit target, Random random = null)
        {
            var damageMin = Math.Max(1, attacker.DamageMin);
            var damageMax = Math.Max(damageMin, attacker.DamageMax);
            var unitDamage = random == null || damageMin == damageMax
                ? (damageMin + damageMax) * 0.5f
                : random.Next(damageMin, damageMax + 1);
            var attackFactor = (20f + Math.Max(0, attacker.Attack)) / Math.Max(1f, 20f + Math.Max(0, target.Defense));
            return Math.Max(1, (int)Math.Round(Math.Max(1, attacker.CurrentCount) * unitDamage * attackFactor));
        }

        private static void TickTimedSkills(List<BattleRuntimeUnit> units, List<BattleRuntimeUnit> enemies, Random random, List<BattleAreaEffect> areaEffects, List<BattleEvent> events = null, float elapsed = 0f)
        {
            TickAreaEffects(areaEffects, units, enemies, random, events, elapsed);

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
                            .OrderBy(enemy => Distance(unit, enemy))
                            .ThenBy(enemy => unit.PreferLowestHp ? enemy.CurrentHp : 0)
                            .ThenByDescending(enemy => enemy.Row)
                            .FirstOrDefault();
                        if (target != null)
                        {
                            var damage = Math.Max(1, (int)Math.Round(CalculateDamage(unit, target, random) * Math.Max(1f, unit.DelayedSnipeMultiplier)));
                            AddEvent(events, elapsed, "skill", unit, target, 0, $"{unit.Name} 寤惰繜鐙欏嚮 {target.Name}");
                            DealDamage(unit, target, damage, units, enemies, random, events, elapsed, unit.DelayedSnipeCritical);
                            unit.SkillTriggers += 1;
                        }
                    }
                }

                foreach (var skill in GetBattleSkills(unit))
                {
                    if (skill.kind == "battle_periodic_temp_power")
                    {
                        continue;
                    }
                    else if (skill.kind == "battle_periodic_self_hp_loss_team_temp_attack")
                    {
                        if (TickSkillTimer(unit, skill.kind, Math.Max(0.1f, skill.interval <= 0f ? 1f : skill.interval)))
                        {
                            if (unit.CurrentCount <= 1 || !units.Any(ally => ally.IsAlive && ally != unit))
                            {
                                continue;
                            }

                            var lossCount = CalculateSelfCountLoss(unit, skill);
                            ApplySelfCountLoss(unit, lossCount);
                            foreach (var ally in units.Where(ally => ally.IsAlive && ally != unit))
                            {
                                ally.Attack += Math.Max(0, skill.attack);
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
                                var damage = Math.Max(1, (int)Math.Round(CalculateDamage(unit, target, random) * Math.Max(1f, skill.attackMultiplier)));
                                AddEvent(events, elapsed, "skill", unit, target, 0, $"{unit.Name} 周期攻击 {target.Name}");
                                DealDamage(unit, target, damage, units, enemies, random, events, elapsed);
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

        private static int CalculateSelfCountLoss(BattleRuntimeUnit unit, SkillDefinition skill)
        {
            var percent = skill.selfHpLoss > 0 ? skill.selfHpLoss : skill.damage > 0 ? skill.damage : skill.hp > 0 ? skill.hp : 25;
            return Math.Max(1, Math.Min(unit.CurrentCount - 1, (int)Math.Ceiling(unit.CurrentCount * (percent / 100f))));
        }

        private static int ApplySelfCountLoss(BattleRuntimeUnit unit, int lossCount)
        {
            var beforeHp = unit.CurrentHp;
            unit.CurrentCount = Math.Max(0, unit.CurrentCount - Math.Max(1, lossCount));
            unit.CurrentHp = Math.Max(0, Math.Min(unit.CurrentHp, unit.CurrentCount * Math.Max(1, unit.HpPerUnit)));
            return Math.Max(1, beforeHp - unit.CurrentHp);
        }

        private static void TickAreaEffects(List<BattleAreaEffect> areaEffects, List<BattleRuntimeUnit> sourceAllies, List<BattleRuntimeUnit> enemies, Random random, List<BattleEvent> events, float elapsed)
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
                    foreach (var target in enemies.Where(enemy => enemy.IsAlive && Distance(effect.CenterRow, effect.CenterCol, enemy.Row, enemy.Col) <= Math.Max(1f, effect.Radius)).ToList())
                    {
                        var unitDamage = (Math.Max(1, effect.DamageMin) + Math.Max(Math.Max(1, effect.DamageMin), effect.DamageMax)) * 0.5f;
                        var factor = (20f + Math.Max(0, effect.Attack)) / Math.Max(1f, 20f + Math.Max(0, target.Defense));
                        var damage = Math.Max(1, (int)Math.Round(Math.Max(1, effect.CurrentCount) * unitDamage * factor * Math.Max(0.1f, effect.AttackMultiplier)));
                        AddEvent(events, elapsed, "skill", effect.Source, target, 0, $"{effect.Source.Name} 火雨命中 {target.Name}");
                        DealDamage(effect.Source, target, damage, sourceAllies, enemies, random, events, elapsed);
                    }
                }

                if (effect.Remaining <= 0f)
                {
                    areaEffects.RemoveAt(i);
                }
            }
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

        private static void ApplyPostBattleRewards(RunState runState, bool victory, IReadOnlyList<BattleRuntimeUnit> players, List<BattleEvent> events)
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
                source.pendingNextRoundTempCount += Math.Max(0, unit.PendingRoundTempCount);
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
                        AddEvent(events, events?.LastOrDefault()?.Time ?? 0f, "skill", unit, unit, Math.Max(1, skill.value), $"{unit.Name} grants next round gold");
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

        private static void TickBattleState(List<BattleRuntimeUnit> units, List<BattleEvent> events = null, float elapsed = 0f)
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
                        AddEvent(events, elapsed, "shield", unit, unit, unit.ShieldLayers, $"{unit.Name} refreshes shield");
                    }
                }
            }
        }

        private static void ApplyContinuousAuras(IReadOnlyList<BattleRuntimeUnit> units)
        {
            foreach (var unit in units)
            {
                if (unit.AuraAttackBonus != 0)
                {
                    unit.Attack = Math.Max(1, unit.Attack - unit.AuraAttackBonus);
                    unit.AuraAttackBonus = 0;
                }

                if (unit.AuraSpeedBonus != 0)
                {
                    unit.Speed = Math.Max(0, unit.Speed - unit.AuraSpeedBonus);
                    unit.AuraSpeedBonus = 0;
                    unit.AttackInterval = AttackIntervalFor(unit.Definition, unit.Speed, unit.AttackInterval);
                }
            }

            foreach (var source in units.Where(unit => unit.IsAlive))
            {
                foreach (var aura in GetManageTalents(source))
                {
                    switch (aura.kind)
                    {
                        case "while_on_board_per_ally_id_buff_type_attack":
                            var allyCount = units.Count(unit => unit.IsAlive && unit.UnitId == aura.allyId);
                            var attackBonus = allyCount * Math.Max(0, aura.attack);
                            if (attackBonus <= 0)
                            {
                                continue;
                            }

                            foreach (var target in units.Where(unit => unit.IsAlive && HasTag(unit, aura.targetTag)))
                            {
                                target.Attack += attackBonus;
                                target.AuraAttackBonus += attackBonus;
                            }
                            break;
                        case "while_on_board_race_threshold_team_speed":
                            var race = string.IsNullOrWhiteSpace(aura.race) ? source.Race : aura.race;
                            if (units.Count(unit => unit.IsAlive && unit.Race == race) < Math.Max(1, aura.threshold))
                            {
                                continue;
                            }

                            foreach (var target in units.Where(unit => unit.IsAlive))
                            {
                                var speedBonus = Math.Max(0, aura.value);
                                if (speedBonus <= 0)
                                {
                                    continue;
                                }

                                target.Speed += speedBonus;
                                target.AuraSpeedBonus += speedBonus;
                                target.AttackInterval = AttackIntervalFor(target.Definition, target.Speed, target.AttackInterval);
                            }
                            break;
                    }
                }
            }

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

        private static IEnumerable<SkillDefinition> GetManageTalents(BattleRuntimeUnit unit)
        {
            if (unit?.Definition == null)
            {
                return Enumerable.Empty<SkillDefinition>();
            }

            return (unit.IsGolden ? unit.Definition.goldTalents : unit.Definition.talents) ?? Array.Empty<SkillDefinition>();
        }

        private static void ResolveFirstHitsCounterattack(BattleRuntimeUnit target, BattleRuntimeUnit attacker, List<BattleRuntimeUnit> targetAllies, List<BattleRuntimeUnit> attackerAllies, Random random, List<BattleEvent> events = null, float elapsed = 0f)
        {
            if (target == null || attacker == null || !target.IsAlive || !attacker.IsAlive)
            {
                return;
            }

            foreach (var skill in GetBattleSkills(target).Where(skill => skill.kind == "first_hits_counterattack"))
            {
                var limit = Math.Max(1, skill.count);
                if (target.ForcedCounterattackTriggers >= limit)
                {
                    continue;
                }

                target.ForcedCounterattackTriggers += 1;
                var repeat = Math.Max(1, skill.repeat);
                for (var index = 0; index < repeat && target.IsAlive && attacker.IsAlive; index += 1)
                {
                    ApplyAttack(target, attacker, targetAllies, attackerAllies, random, null, true, false, false, events, elapsed);
                    target.CounterCount += 1;
                }

                target.SkillTriggers += 1;
            }
        }

        private static void ResolveAllyDeathTaggedPower(BattleRuntimeUnit deadUnit, List<BattleRuntimeUnit> allies)
        {
            if (deadUnit == null || allies == null)
            {
                return;
            }

            foreach (var owner in allies.Where(unit => unit.IsAlive))
            {
                foreach (var skill in GetBattleSkills(owner).Where(skill => skill.kind == "on_ally_death_tagged_units_temp_power"))
                {
                    if (!HasTag(deadUnit, skill.deadTag))
                    {
                        continue;
                    }

                    var changed = false;
                    foreach (var target in allies.Where(unit => unit.IsAlive && HasTag(unit, skill.targetTag)))
                    {
                        target.Power += Math.Max(0, skill.power);
                        changed = true;
                    }

                    if (changed)
                    {
                        owner.SkillTriggers += 1;
                    }
                }
            }
        }

        private static int CountFaith(IEnumerable<BattleRuntimeUnit> units, string skillFaith, string fallbackFaith)
        {
            var faith = string.IsNullOrWhiteSpace(skillFaith) ? fallbackFaith : skillFaith;
            return units.Count(unit => unit.IsAlive && unit.Faith == faith);
        }

        private static int ApplySelfCountPercentPerFaithCount(BattleRuntimeUnit unit, int faithCount, SkillDefinition skill, List<BattleEvent> events = null, float elapsed = 0f)
        {
            if (unit == null || skill == null || faithCount <= 0)
            {
                return 0;
            }

            var percent = Math.Max(0, skill.value) * faithCount;
            var countGain = (int)Math.Ceiling(unit.CurrentCount * (percent / 100f));
            AddTemporaryCount(unit, countGain);
            if (countGain > 0)
            {
                unit.SkillTriggers += 1;
                AddEvent(events, elapsed, "skill", unit, unit, countGain, $"{unit.Name} gains faith count");
            }

            return countGain;
        }

        private static void ResolveFaithSummonCountBonuses(List<BattleRuntimeUnit> allies, BattleRuntimeUnit summoned, List<BattleEvent> events = null, float elapsed = 0f)
        {
            if (allies == null || summoned == null || !summoned.IsAlive || string.IsNullOrWhiteSpace(summoned.Faith))
            {
                return;
            }

            foreach (var ally in allies.Where(ally => ally != null && ally != summoned && ally.IsAlive).ToList())
            {
                foreach (var skill in GetBattleSkills(ally).Where(skill => skill.kind == "battle_start_self_count_percent_per_faith_count"))
                {
                    var faith = string.IsNullOrWhiteSpace(skill.faith) ? ally.Faith : skill.faith;
                    if (summoned.Faith != faith)
                    {
                        continue;
                    }

                    ApplySelfCountPercentPerFaithCount(ally, 1, skill, events, elapsed);
                }
            }
        }

        private static float SkillRefreshSeconds(SkillDefinition skill, float fallback)
        {
            if (skill == null)
            {
                return fallback;
            }

            return skill.refreshSeconds > 0f ? skill.refreshSeconds : skill.duration > 0f ? skill.duration : fallback;
        }

        private static int SkillRefreshRounds(SkillDefinition skill)
        {
            return skill == null ? 0 : Math.Max(0, skill.refreshRounds);
        }

        private static bool IsRoundInterval(int round, SkillDefinition skill)
        {
            var interval = Math.Max(1, skill?.intervalRounds ?? 1);
            return round > 0 && (round - 1) % interval == 0;
        }

        private static float SkillSnipeMultiplier(BattleRuntimeUnit unit, SkillDefinition skill)
        {
            if (skill == null)
            {
                return 1f;
            }

            if (SkillSnipeIsCritical(unit, skill))
            {
                return Math.Max(1f, skill.critMultiplier);
            }

            return Math.Max(1f, skill.attackMultiplier);
        }

        private static bool SkillSnipeIsCritical(BattleRuntimeUnit unit, SkillDefinition skill)
        {
            if (skill == null || skill.critMultiplier <= 0f)
            {
                return false;
            }

            var receivedGems = unit?.SourceState?.forestGemsReceived ?? 0;
            var attachedGems = unit?.SourceState?.forestGemsAttached ?? 0;
            return skill.giftThreshold <= 0 || receivedGems >= skill.giftThreshold || attachedGems >= skill.giftThreshold;
        }

        private static float SkillDeathAttackMultiplier(SkillDefinition skill)
        {
            if (skill == null)
            {
                return 1f;
            }

            return Math.Max(1f, skill.deathAttackMultiplier > 0f ? skill.deathAttackMultiplier : skill.attackMultiplier);
        }

        private static void AddAreaEffect(List<BattleAreaEffect> areaEffects, BattleRuntimeUnit source, BattleRuntimeUnit target, SkillDefinition skill, List<BattleEvent> events, float elapsed)
        {
            if (areaEffects == null || source == null || target == null || skill == null)
            {
                return;
            }

            var tick = skill.tick > 0f ? skill.tick : 0.5f;
            areaEffects.Add(new BattleAreaEffect
            {
                Source = source,
                CenterRow = target.Row,
                CenterCol = target.Col,
                Radius = Math.Max(1f, skill.radius),
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

        private static void AddAttack(BattleRuntimeUnit unit, int value)
        {
            if (unit == null || value <= 0)
            {
                return;
            }

            unit.Attack += value;
            unit.SkillTriggers += 1;
        }

        private static void AddTemporaryCount(BattleRuntimeUnit unit, int amount)
        {
            if (unit == null || amount <= 0)
            {
                return;
            }

            var hpPerUnit = Math.Max(1, unit.HpPerUnit);
            unit.BaseCount = Math.Max(unit.BaseCount, unit.CurrentCount + amount);
            unit.CurrentCount += amount;
            unit.CurrentTotalHp += amount * hpPerUnit;
            unit.CurrentHp = unit.CurrentTotalHp;
            unit.MaxHp = Math.Max(unit.MaxHp, unit.BaseCount * hpPerUnit);
        }

        private static void ApplyFixedSummonCount(BattleRuntimeUnit unit, int count)
        {
            if (unit == null || count <= 0)
            {
                return;
            }

            var hpPerUnit = Math.Max(1, unit.HpPerUnit);
            unit.BaseCount = Math.Max(1, count);
            unit.InitialCount = unit.BaseCount;
            unit.CurrentCount = unit.BaseCount;
            unit.CurrentTotalHp = unit.CurrentCount * hpPerUnit;
            unit.CurrentHp = unit.CurrentTotalHp;
            unit.MaxHp = unit.CurrentTotalHp;
            unit.MaxCount = unit.BaseCount;
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

        private static bool MatchesSkillTarget(BattleRuntimeUnit unit, SkillDefinition skill)
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

        private static void SummonUnits(List<BattleRuntimeUnit> allies, IReadOnlyList<BattleRuntimeUnit> enemies, BattleRuntimeUnit source, SkillDefinition skill, Random random, List<BattleEvent> events = null, float elapsed = 0f)
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
                var slot = FindSummonSlot(allies, enemies, source);
                var summoned = CreateRuntimeUnit(null, source.PlayerSide, definition, slot, 1f);
                if (summoned == null)
                {
                    continue;
                }

                ApplyFixedSummonCount(summoned, skill.value);
                summoned.Summoned = true;
                summoned.SummonDuration = skill.duration > 0f ? skill.duration : 0f;
                allies.Add(summoned);
                AddEvent(events, elapsed, "summon", source, summoned, 0, $"{source.Name} summons {summoned.Name}");
                ResolveFaithSummonCountBonuses(allies, summoned, events, elapsed);
            }
        }

        private static string FindSummonSlot(IReadOnlyList<BattleRuntimeUnit> allies, IReadOnlyList<BattleRuntimeUnit> enemies, BattleRuntimeUnit source)
        {
            if (source == null)
            {
                return FormatHexSlot(0, 0);
            }

            var occupied = new HashSet<string>((allies ?? Array.Empty<BattleRuntimeUnit>())
                .Concat(enemies ?? Array.Empty<BattleRuntimeUnit>())
                .Where(unit => unit != null && unit.IsAlive)
                .Select(unit => HexKey(unit.HexColumn, unit.HexRow)));
            var queue = new Queue<HexCoord>();
            var visited = new HashSet<string>();
            var start = new HexCoord(source.HexColumn, source.HexRow);
            visited.Add(HexKey(start.Column, start.Row));
            foreach (var neighbor in GetOrderedSummonNeighbors(source, start))
            {
                queue.Enqueue(neighbor);
                visited.Add(HexKey(neighbor.Column, neighbor.Row));
            }

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                var key = HexKey(current.Column, current.Row);
                if (!occupied.Contains(key))
                {
                    return FormatHexSlot(current.Column, current.Row);
                }

                foreach (var next in GetOrderedSummonNeighbors(source, current))
                {
                    var nextKey = HexKey(next.Column, next.Row);
                    if (visited.Add(nextKey))
                    {
                        queue.Enqueue(next);
                    }
                }
            }

            return FormatHexSlot(source.HexColumn, source.HexRow);
        }

        private static IEnumerable<HexCoord> GetOrderedSummonNeighbors(BattleRuntimeUnit source, HexCoord center)
        {
            return GetHexNeighbors(center.Column, center.Row)
                .OrderBy(coord => HexDistance(source.HexColumn, source.HexRow, coord.Column, coord.Row))
                .ThenBy(coord => Math.Abs(coord.Row - source.HexRow))
                .ThenBy(coord => source.PlayerSide ? coord.Column : -coord.Column)
                .ThenBy(coord => coord.Row);
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

        private static int CalculateFateDamage(RunState runState, IReadOnlyList<BattleRuntimeUnit> enemies)
        {
            if (WorldMapSystem.IsBossNodeType(runState?.explorationBattleNodeType))
            {
                return Math.Max(0, runState?.playerHp ?? 0);
            }

            var aliveStarSum = enemies?
                .Where(unit => unit != null && unit.IsAlive)
                .Sum(unit => Math.Max(1, unit.Definition?.star ?? 1)) ?? 0;
            var dayBonus = Math.Max(0, (runState?.dayCount ?? 0) / 5);
            return Math.Max(1, WorldMapSystem.GetBaseFateDamage(runState?.explorationBattleNodeType) + aliveStarSum + dayBonus);
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

        private static int ResolveBaseCount(UnitDefinition definition, UnitCardState state)
        {
            var startCount = ResolveStartCount(definition);
            return Math.Max(1, (state != null && state.baseCount > 0 ? state.baseCount : startCount) + (state?.roundTempCount ?? 0));
        }

        private static int ResolveStartCount(UnitDefinition definition)
        {
            if (definition == null)
            {
                return 1;
            }

            return Math.Max(1, definition.defaultCount > 0 ? definition.defaultCount : definition.startCount > 0 ? definition.startCount : definition.baseCount > 0 ? definition.baseCount : 1);
        }

        private static float AttackIntervalFor(UnitDefinition definition, int speed, float fallback)
        {
            var baseInterval = definition != null && definition.attackInterval > 0f ? definition.attackInterval : fallback;
            return Math.Max(0.2f, baseInterval * (100f / (100f + Math.Max(0, speed) * 2f)));
        }

        private static float Distance(BattleRuntimeUnit left, BattleRuntimeUnit right)
        {
            return HexDistance(left.HexColumn, left.HexRow, right.HexColumn, right.HexRow);
        }

        private static void MovePouncerNextToTarget(BattleRuntimeUnit unit, BattleRuntimeUnit target, IEnumerable<BattleRuntimeUnit> allies, IEnumerable<BattleRuntimeUnit> enemies)
        {
            if (unit == null || target == null)
            {
                return;
            }

            var occupied = BuildOccupiedHexSet(allies, enemies, unit);
            var destination = GetHexNeighbors(target.HexColumn, target.HexRow)
                .Where(coord => !occupied.Contains(HexKey(coord.Column, coord.Row)))
                .OrderBy(coord => HexDistance(unit.HexColumn, unit.HexRow, coord.Column, coord.Row))
                .Select(coord => (HexCoord?)coord)
                .FirstOrDefault()
                ?? GetHexNeighbors(target.HexColumn, target.HexRow)
                    .OrderBy(coord => HexDistance(unit.HexColumn, unit.HexRow, coord.Column, coord.Row))
                    .Select(coord => (HexCoord?)coord)
                    .FirstOrDefault();
            if (!destination.HasValue)
            {
                return;
            }

            unit.HexColumn = destination.Value.Column;
            unit.HexRow = destination.Value.Row;
            unit.Row = unit.HexRow;
            unit.Col = unit.HexColumn;
            unit.SlotId = FormatHexSlot(unit.HexColumn, unit.HexRow);
            unit.CurrentTarget = target;
            unit.TargetSearchTimer = 0f;
        }

        private static float Distance(int leftRow, int leftCol, int rightRow, int rightCol)
        {
            return HexDistance(leftCol, leftRow, rightCol, rightRow);
        }

        private static bool TryMapInitialSlotToHex(string slotId, bool playerSide, out int column, out int row)
        {
            column = 0;
            row = 0;
            switch (slotId)
            {
                case "4-1":
                    column = 0;
                    row = 1;
                    break;
                case "4-2":
                    column = 0;
                    row = 2;
                    break;
                case "4-3":
                    column = 0;
                    row = 3;
                    break;
                case "4-4":
                    column = 0;
                    row = 4;
                    break;
                case "3-1":
                    column = 1;
                    row = 1;
                    break;
                case "3-2":
                    column = 1;
                    row = 2;
                    break;
                case "3-3":
                    column = 1;
                    row = 3;
                    break;
                case "2-1":
                    column = 2;
                    row = 2;
                    break;
                case "2-2":
                    column = 2;
                    row = 3;
                    break;
                case "1-1":
                    column = 3;
                    row = 2;
                    break;
                default:
                    return TryParseHexSlot(slotId, out column, out row);
            }

            if (!playerSide)
            {
                column = BattleHexColumnCount - 1 - column;
            }

            return true;
        }

        private static List<HexCoord> FindHexPath(int startColumn, int startRow, int endColumn, int endRow, HashSet<string> occupied)
        {
            if (!IsValidHex(endColumn, endRow) || occupied.Contains(HexKey(endColumn, endRow)))
            {
                return null;
            }

            var start = new HexCoord(startColumn, startRow);
            var end = new HexCoord(endColumn, endRow);
            var queue = new Queue<HexCoord>();
            var visited = new HashSet<string> { HexKey(start.Column, start.Row) };
            var parent = new Dictionary<string, HexCoord>();
            queue.Enqueue(start);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                if (current.Column == end.Column && current.Row == end.Row)
                {
                    var path = new List<HexCoord>();
                    var cursor = end;
                    while (!(cursor.Column == start.Column && cursor.Row == start.Row))
                    {
                        path.Add(cursor);
                        cursor = parent[HexKey(cursor.Column, cursor.Row)];
                    }

                    path.Reverse();
                    return path;
                }

                foreach (var next in GetHexNeighbors(current.Column, current.Row))
                {
                    var key = HexKey(next.Column, next.Row);
                    if (visited.Contains(key) || occupied.Contains(key))
                    {
                        continue;
                    }

                    visited.Add(key);
                    parent[key] = current;
                    queue.Enqueue(next);
                }
            }

            return null;
        }

        private static IEnumerable<HexCoord> GetHexNeighbors(int column, int row)
        {
            if (!IsValidHex(column, row))
            {
                yield break;
            }

            var candidates = new List<HexCoord>
            {
                new HexCoord(column, row - 1),
                new HexCoord(column, row + 1)
            };

            if (BattleHexRowsByColumn[column] == BattleHexMaxRows)
            {
                candidates.Add(new HexCoord(column - 1, row - 1));
                candidates.Add(new HexCoord(column - 1, row));
                candidates.Add(new HexCoord(column + 1, row - 1));
                candidates.Add(new HexCoord(column + 1, row));
            }
            else
            {
                candidates.Add(new HexCoord(column - 1, row));
                candidates.Add(new HexCoord(column - 1, row + 1));
                candidates.Add(new HexCoord(column + 1, row));
                candidates.Add(new HexCoord(column + 1, row + 1));
            }

            foreach (var candidate in candidates)
            {
                if (IsValidHex(candidate.Column, candidate.Row))
                {
                    yield return candidate;
                }
            }
        }

        private static int HexDistance(int startColumn, int startRow, int endColumn, int endRow)
        {
            if (startColumn == endColumn && startRow == endRow)
            {
                return 0;
            }

            var queue = new Queue<HexCoord>();
            var distances = new Dictionary<string, int>();
            queue.Enqueue(new HexCoord(startColumn, startRow));
            distances[HexKey(startColumn, startRow)] = 0;

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                var currentDistance = distances[HexKey(current.Column, current.Row)];
                foreach (var next in GetHexNeighbors(current.Column, current.Row))
                {
                    var key = HexKey(next.Column, next.Row);
                    if (distances.ContainsKey(key))
                    {
                        continue;
                    }

                    var distance = currentDistance + 1;
                    if (next.Column == endColumn && next.Row == endRow)
                    {
                        return distance;
                    }

                    distances[key] = distance;
                    queue.Enqueue(next);
                }
            }

            return 999;
        }

        private static bool IsValidHex(int column, int row)
        {
            return column >= 0
                && column < BattleHexColumnCount
                && row >= 0
                && row < BattleHexRowsByColumn[column];
        }

        private static string HexKey(int column, int row)
        {
            return $"{column}:{row}";
        }

        private static string FormatHexSlot(int column, int row)
        {
            return $"h-{column + 1}-{row + 1}";
        }

        private static bool TryParseHexSlot(string slotId, out int column, out int row)
        {
            column = 0;
            row = 0;
            if (string.IsNullOrWhiteSpace(slotId) || !slotId.StartsWith("h-", StringComparison.Ordinal))
            {
                return false;
            }

            var parts = slotId.Split('-');
            if (parts.Length != 3 || !int.TryParse(parts[1], out var parsedColumn) || !int.TryParse(parts[2], out var parsedRow))
            {
                return false;
            }

            column = parsedColumn - 1;
            row = parsedRow - 1;
            return IsValidHex(column, row);
        }

        private static float Clamp01(float value)
        {
            if (value < 0f)
            {
                return 0f;
            }

            return value > 1f ? 1f : value;
        }

        private static float MoraleChance(int morale, float rate)
        {
            return Math.Min(0.95f, Math.Max(0f, morale * MORALE_EXTRA_ATTACK_CHANCE_PER_POINT));
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
        public List<BattleEvent> Events = new List<BattleEvent>();
        public List<BattleUnitSnapshot> InitialPlayerUnits = new List<BattleUnitSnapshot>();
        public List<BattleUnitSnapshot> InitialEnemyUnits = new List<BattleUnitSnapshot>();
        public List<BattleUnitSnapshot> PlayerUnits = new List<BattleUnitSnapshot>();
        public List<BattleUnitSnapshot> EnemyUnits = new List<BattleUnitSnapshot>();
    }

    public sealed class BattlePreviewResult
    {
        public int PlayerScore;
        public int EnemyScore;
        public List<BattleUnitSnapshot> InitialPlayerUnits = new List<BattleUnitSnapshot>();
        public List<BattleUnitSnapshot> InitialEnemyUnits = new List<BattleUnitSnapshot>();
        public List<BattleUnitSnapshot> PlayerUnits = new List<BattleUnitSnapshot>();
        public List<BattleUnitSnapshot> EnemyUnits = new List<BattleUnitSnapshot>();
    }

    public sealed class BattleEvent
    {
        public float Time;
        public string Kind;
        public string SourceUnitId;
        public string SourceInstanceId;
        public string SourceName;
        public bool SourcePlayerSide;
        public string SourceSlotId;
        public int SourceHp;
        public int SourceMaxHp;
        public int SourceShieldLayers;
        public string TargetUnitId;
        public string TargetInstanceId;
        public string TargetName;
        public bool TargetPlayerSide;
        public string TargetSlotId;
        public int TargetHp;
        public int TargetMaxHp;
        public int TargetShieldLayers;
        public string DestinationSlotId;
        public string RouteSlotIds;
        public int Amount;
        public string Message;
    }

    public sealed class BattleUnitSnapshot
    {
        public string UnitId;
        public string InstanceId;
        public string Name;
        public int Star;
        public bool IsGolden;
        public string SlotId;
        public int MaxHp;
        public int CurrentHp;
        public int BaseCount;
        public int InitialCount;
        public int CurrentCount;
        public int MaxCount;
        public int HpPerUnit;
        public int CurrentTotalHp;
        public int ShieldLayers;
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
        public int DamageDone;
        public int Kills;
        public bool Summoned;
    }

    internal struct HexCoord
    {
        public HexCoord(int column, int row)
        {
            Column = column;
            Row = row;
        }

        public int Column;
        public int Row;
    }

    internal sealed class BattleRuntimeUnit
    {
        public string UnitId;
        public string InstanceId;
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
        public int BoardRow;
        public int BoardCol;
        public int Row;
        public int Col;
        public int HexColumn;
        public int HexRow;
        public int MaxHp;
        public int CurrentHp;
        public int BaseCount;
        public int InitialCount;
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
        public float AttackInterval;
        public float Cooldown;
        public int DamageDone;
        public int AttackCount;
        public int KillCount;
        public BattleRuntimeUnit CurrentTarget;
        public float TargetSearchTimer;
        public int CounterCount;
        public int MoraleExtraCount;
        public int DamagedCount;
        public int ForcedCounterattackTriggers;
        public int ShieldLayers;
        public float StunRemaining;
        public int StunTurns;
        public int MoveLockTurns;
        public float InvincibleRemaining;
        public float ShieldRefreshInterval;
        public float ShieldRefreshTimer;
        public int OriginalSpeed;
        public float OriginalAttackInterval;
        public int AuraAttackBonus;
        public int AuraSpeedBonus;
        public bool FirstAttackForceCrit;
        public float FirstAttackCritMultiplier;
        public bool DeathProcessed;
        public bool Summoned;
        public float SummonDuration;
        public bool PreferLowestHp;
        public bool PreferBackline;
        public float DelayedSnipeTimer;
        public float DelayedSnipeMultiplier;
        public bool DelayedSnipeCritical;
        public int TeamForestGiftTotal;
        public int SkillTriggers;
        public int PendingRoundTempAttack;
        public int PendingRoundTempCount;
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
        public bool IsAlive => CurrentHp > 0 && CurrentCount > 0;
    }

    internal sealed class BattleAreaEffect
    {
        public BattleRuntimeUnit Source;
        public int CenterRow;
        public int CenterCol;
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
}
