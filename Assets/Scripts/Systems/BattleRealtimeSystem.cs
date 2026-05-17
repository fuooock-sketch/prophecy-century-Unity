using System;
using System.Collections.Generic;
using System.Linq;
using ProphecyCentury.Core;
using ProphecyCentury.Data;

namespace ProphecyCentury.Systems
{
    public sealed class BattleRealtimeSystem
    {
        private const float MaxBattleTime = 20f;
        private const float StepSeconds = 0.05f;
        private const float TargetSearchInterval = 1f;
        private const int MaxBattleEvents = 800;

        public BattleRealtimeResult Resolve(IReadOnlyList<BattleUnitSnapshot> playerSnapshots, IReadOnlyList<BattleUnitSnapshot> enemySnapshots)
        {
            var random = new Random(ProphecyGameSession.Instance.CurrentRun.round * 104729 + (playerSnapshots?.Count ?? 0) * 379);
            var players = CreateUnits(playerSnapshots, true);
            var enemies = CreateUnits(enemySnapshots, false);
            var events = new List<BattleEvent>();
            var elapsed = 0f;

            AddEvent(events, 0f, "start", null, null, 0, "Realtime battle start");
            ResolveBattleStart(players, enemies, random, events, 0f);
            ResolveBattleStart(enemies, players, random, events, 0f);
            ApplyContinuousAuras(players);
            ApplyContinuousAuras(enemies);

            while (elapsed < MaxBattleTime && players.Any(unit => unit.IsAlive) && enemies.Any(unit => unit.IsAlive))
            {
                TickTimedSkills(players, enemies, random, elapsed, events);
                TickTimedSkills(enemies, players, random, elapsed, events);
                TickSide(players, enemies, random, elapsed, events);
                TickSide(enemies, players, random, elapsed, events);
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
                        case "battle_start_self_refreshing_shield":
                            unit.ShieldLayers += Math.Max(1, skill.layers);
                            unit.ShieldRefreshInterval = Math.Max(0.1f, skill.duration > 0f ? skill.duration : 5f);
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
                            unit.FirstAttackForceCrit = true;
                            unit.FirstAttackCritMultiplier = ProphecyGameSession.Instance.Data.Config?.critDamageMultiple ?? 1.5f;
                            AddEvent(events, elapsed, "skill", unit, unit, 0, $"{unit.Name} enters stealth");
                            break;
                        case "battle_start_lowest_power_ally_gain_source_power":
                            var targetAlly = allies.Where(ally => ally.IsAlive).OrderBy(ally => ally.Power).FirstOrDefault();
                            if (targetAlly != null)
                            {
                                targetAlly.Power += unit.Power * Math.Max(1, skill.multiplier == 0 ? 1 : skill.multiplier);
                                AddEvent(events, elapsed, "skill", unit, targetAlly, targetAlly.Power, $"{targetAlly.Name} 获得法强");
                            }
                            break;
                        case "battle_start_summon_units":
                        case "battle_start_and_death_summon_units":
                            SummonUnits(allies, unit, skill, events, elapsed);
                            break;
                        case "battle_start_summon_and_buff_type":
                            SummonUnits(allies, unit, skill, events, elapsed);
                            foreach (var ally in allies.Where(ally => ally.IsAlive && (ally.Type == skill.type || HasTag(ally, skill.type))))
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
                                var multiplier = skill.attackMultiplier > 0f ? skill.attackMultiplier : 3f;
                                DealDamage(unit, pounceTarget, Math.Max(1, (int)Math.Round(unit.Attack * multiplier + unit.Power * 8 - pounceTarget.Defense)), allies, enemies, random, events, elapsed, skill.forceCrit);
                                pounceTarget.StunRemaining = Math.Max(pounceTarget.StunRemaining, skill.stunSeconds);
                                if (skill.stunSeconds > 0f)
                                {
                                    AddEvent(events, elapsed, "control", unit, pounceTarget, (int)Math.Round(skill.stunSeconds * 1000f), $"{pounceTarget.Name} stunned for {skill.stunSeconds:0.#}s");
                                }
                            }
                            break;
                        case "battle_start_lock_highest_hp_targets":
                            foreach (var locked in enemies.Where(enemy => enemy.IsAlive).OrderByDescending(enemy => enemy.Hp).Take(Math.Max(1, skill.count)))
                            {
                                locked.StunRemaining = Math.Max(locked.StunRemaining, skill.duration);
                                AddEvent(events, elapsed, "control", unit, locked, (int)Math.Round(Math.Max(0.1f, skill.duration) * 1000f), $"{locked.Name} locked for {skill.duration:0.#}s");
                            }
                            break;
                    }
                }
            }
        }

        private static void TickSide(List<RealtimeBattleUnit> attackers, List<RealtimeBattleUnit> defenders, Random random, float elapsed, List<BattleEvent> events)
        {
            foreach (var attacker in attackers.Where(unit => unit.IsAlive).ToList())
            {
                attacker.AttackTimer = Math.Max(0f, attacker.AttackTimer - StepSeconds);
                if (attacker.StunRemaining > 0f)
                {
                    continue;
                }

                var target = ResolveTarget(attacker, defenders);
                if (target == null)
                {
                    continue;
                }

                var distance = Distance(attacker, target);
                var attackRange = attacker.Range * 60f + attacker.Size + target.Size;
                if (distance > attackRange)
                {
                    MoveToTarget(attacker, target);
                    continue;
                }

                if (attacker.AttackTimer > 0f)
                {
                    continue;
                }

                attacker.AttackTimer = Math.Max(0.2f, attacker.AttackInterval);
                attacker.AttackCount += 1;
                AddEvent(events, elapsed, "attack", attacker, target, 0, $"{attacker.Name} 攻击 {target.Name}");
                var damage = CalculateDamage(attacker, target);
                var didCrit = ResolveForceCrit(attacker, random, out var critMultiplier);
                if (didCrit)
                {
                    damage = (int)Math.Ceiling(damage * Math.Max(critMultiplier, ProphecyGameSession.Instance.Data.Config?.critDamageMultiple ?? 1.5f));
                }

                var actual = DealDamage(attacker, target, damage, attackers, defenders, random, events, elapsed, didCrit);
                if (actual > 0)
                {
                    ResolveOnAttack(attacker, target, attackers, defenders, random, actual, elapsed, events);
                }
            }
        }

        private static void ResolveOnAttack(RealtimeBattleUnit attacker, RealtimeBattleUnit target, List<RealtimeBattleUnit> allies, List<RealtimeBattleUnit> enemies, Random random, int damage, float elapsed, List<BattleEvent> events)
        {
            foreach (var skill in GetBattleSkills(attacker))
            {
                switch (skill.kind)
                {
                    case "on_attack_chance_self_shield_no_stack":
                        if (attacker.ShieldLayers <= 0 && random.NextDouble() < Math.Max(0f, skill.chance))
                        {
                            attacker.ShieldLayers = Math.Max(1, skill.layers);
                            AddEvent(events, elapsed, "skill", attacker, attacker, 0, $"{attacker.Name} 获得护盾");
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
                    case "on_attack_count_formula_aoe":
                    case "on_attack_count_fire_rain_area_dot":
                        if (IncrementSkillCounter(attacker, skill.kind) >= Math.Max(1, skill.count))
                        {
                            attacker.SkillCounters[skill.kind] = 0;
                            foreach (var areaTarget in enemies.Where(enemy => enemy.IsAlive && Distance(enemy, target) <= Math.Max(1f, skill.radius)).ToList())
                            {
                                AddEvent(events, elapsed, "skill", attacker, areaTarget, 0, $"{attacker.Name} 触发范围攻击");
                                DealDamage(attacker, areaTarget, Math.Max(1, (int)Math.Round(CalculateDamage(attacker, areaTarget) * Math.Max(1f, skill.attackMultiplier))), allies, enemies, random, events, elapsed);
                            }
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
                        }
                        break;
                }
            }
        }

        private static void TickTimedSkills(List<RealtimeBattleUnit> units, List<RealtimeBattleUnit> enemies, Random random, float elapsed, List<BattleEvent> events)
        {
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
                    if (skill.kind == "battle_periodic_temp_power" && TickSkillTimer(unit, skill.kind, Math.Max(0.1f, skill.interval <= 0f ? 3f : skill.interval)))
                    {
                        unit.Power += Math.Max(1, skill.value);
                        AddEvent(events, elapsed, "skill", unit, unit, skill.value, $"{unit.Name} gains temporary power");
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

        private static int DealDamage(RealtimeBattleUnit source, RealtimeBattleUnit target, int damage, List<RealtimeBattleUnit> sourceAllies, List<RealtimeBattleUnit> targetAllies, Random random, List<BattleEvent> events, float elapsed, bool critical = false)
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
            target.Hp = Math.Max(0, target.Hp - Math.Max(1, damage));
            var actual = before - target.Hp;
            if (source != null)
            {
                source.DamageDone += actual;
            }

            AddEvent(events, elapsed, critical ? "critical_damage" : "damage", source, target, actual, $"{source?.Name ?? "Effect"} deals {actual} damage to {target.Name}");
            if (target.Hp <= 0 && before > 0)
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
            foreach (var skill in GetBattleSkills(unit))
            {
                switch (skill.kind)
                {
                    case "battle_start_and_death_summon_units":
                        SummonUnits(allies, unit, skill, events, elapsed);
                        break;
                    case "on_death_explode":
                    case "on_death_explode_if_hits_next_round_team_attack":
                        foreach (var enemy in enemies.Where(enemy => enemy.IsAlive && Distance(unit, enemy) <= Math.Max(1f, skill.radius * 80f)).ToList())
                        {
                            AddEvent(events, elapsed, "skill", unit, enemy, 0, $"{unit.Name} 死亡爆炸");
                            var explodeDamage = Math.Max(1, skill.damage > 0 ? skill.damage : (int)Math.Round((unit.Attack + unit.Power * 8) * Math.Max(1f, skill.attackMultiplier)));
                            DealDamage(unit, enemy, explodeDamage, allies, enemies, random, events, elapsed);
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

        private static void MoveToTarget(RealtimeBattleUnit unit, RealtimeBattleUnit target)
        {
            var dx = target.X - unit.X;
            var dy = target.Y - unit.Y;
            var distance = Math.Max(0.001f, (float)Math.Sqrt(dx * dx + dy * dy));
            var moveSpeed = Math.Max(45f, unit.Speed * 8.4f);
            unit.X += dx / distance * moveSpeed * StepSeconds;
            unit.Y += dy / distance * moveSpeed * StepSeconds;
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
                    left.X -= dx / distance * overlap * 0.5f;
                    left.Y -= dy / distance * overlap * 0.5f;
                    right.X += dx / distance * overlap * 0.5f;
                    right.Y += dy / distance * overlap * 0.5f;
                }
            }
        }

        private static void TickState(IEnumerable<RealtimeBattleUnit> units)
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

        private static int CalculateDamage(RealtimeBattleUnit attacker, RealtimeBattleUnit target)
        {
            var defenseFactor = 1f - target.Defense / (float)Math.Max(1, target.Defense + Math.Max(1, attacker.Power));
            return Math.Max(1, (int)Math.Round(Math.Max(1, attacker.Attack) * defenseFactor));
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
                    if (attacker.Power >= Math.Max(0, skill.threshold))
                    {
                        critMultiplier = Math.Max(critMultiplier, Math.Max(1.5f, skill.multiplier));
                    }
                }

                if (skill.kind == "on_attack_chance_force_crit" && random.NextDouble() < Math.Max(0f, skill.chance))
                {
                    forceCrit = true;
                }
            }

            return forceCrit;
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
                var snapshot = new BattleUnitSnapshot
                {
                    UnitId = definition.id,
                    Name = definition.name,
                    Star = definition.star,
                    SlotId = $"{source.Row}-{Math.Max(1, source.Col + i + 1)}",
                    MaxHp = Math.Max(1, definition.hp),
                    CurrentHp = Math.Max(1, definition.hp),
                    Attack = Math.Max(1, definition.attack),
                    Defense = Math.Max(0, definition.defense),
                    Power = Math.Max(1, definition.power),
                    Speed = Math.Max(1, definition.speed),
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

        private static void AddEvent(List<BattleEvent> events, float time, string kind, RealtimeBattleUnit source, RealtimeBattleUnit target, int amount, string message)
        {
            if (events == null || events.Count >= MaxBattleEvents)
            {
                return;
            }

            events.Add(new BattleEvent
            {
                Time = Math.Max(0f, time),
                Kind = kind,
                SourceUnitId = source?.UnitId,
                SourceName = source?.Name,
                SourcePlayerSide = source?.PlayerSide ?? false,
                SourceSlotId = source?.SlotId,
                SourceHp = source?.Hp ?? 0,
                SourceMaxHp = source?.MaxHp ?? 0,
                TargetUnitId = target?.UnitId,
                TargetName = target?.Name,
                TargetPlayerSide = target?.PlayerSide ?? false,
                TargetSlotId = target?.SlotId,
                TargetHp = target?.Hp ?? 0,
                TargetMaxHp = target?.MaxHp ?? 0,
                Amount = amount,
                Message = message
            });
        }

        private static float Distance(RealtimeBattleUnit left, RealtimeBattleUnit right)
        {
            var dx = left.X - right.X;
            var dy = left.Y - right.Y;
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
                Attack = unit.Attack,
                Defense = unit.Defense,
                Power = unit.Power,
                Speed = unit.Speed,
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
                MaxHp = Math.Max(1, snapshot.MaxHp);
                Hp = Math.Max(1, snapshot.CurrentHp > 0 ? snapshot.CurrentHp : snapshot.MaxHp);
                Attack = Math.Max(1, snapshot.Attack);
                Defense = Math.Max(0, snapshot.Defense);
                Power = Math.Max(1, snapshot.Power);
                Speed = Math.Max(1, snapshot.Speed);
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
            public int Attack;
            public int Defense;
            public int Power;
            public int Speed;
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
            public RealtimeBattleUnit CurrentTarget;
            public float TargetSearchTimer;
            public int ShieldLayers;
            public float ShieldRefreshInterval;
            public float ShieldRefreshTimer;
            public float StunRemaining;
            public float InvincibleRemaining;
            public bool Summoned;
            public float SummonDuration;
            public bool DeathProcessed;
            public int TeamForestGiftTotal;
            public bool FirstAttackForceCrit;
            public float FirstAttackCritMultiplier;
            public readonly Dictionary<string, int> SkillCounters = new Dictionary<string, int>();
            public readonly Dictionary<string, float> SkillTimers = new Dictionary<string, float>();
            public bool IsAlive => Hp > 0;
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
        public List<BattleUnitSnapshot> PlayerUnits = new List<BattleUnitSnapshot>();
        public List<BattleUnitSnapshot> EnemyUnits = new List<BattleUnitSnapshot>();
    }
}
