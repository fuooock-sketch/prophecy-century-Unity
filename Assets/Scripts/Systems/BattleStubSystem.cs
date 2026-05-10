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
                TickSide(players, enemies, random, ref attacks);
                TickSide(enemies, players, random, ref attacks);
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
            ApplyContinuousAuras(units);
            return EstimateScore(units);
        }

        public static int EstimateEnemyScore(RunState runState)
        {
            var random = new Random(runState.round * 7919 + runState.boardUnits.Count * 131);
            var units = BuildEnemyUnits(runState, random);
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

                var target = PickTarget(attacker, defenders);
                if (target == null)
                {
                    continue;
                }

                ApplyAttack(attacker, target, random);
                attacks += 1;
                attacker.Cooldown += Math.Max(0.2f, attacker.AttackInterval);

                var moraleExtraChance = Clamp01(attacker.Morale * (ProphecyGameSession.Instance.Data.Config?.moraleExtraAttackRate ?? 0.06f));
                if (target.IsAlive && random.NextDouble() < moraleExtraChance)
                {
                    ApplyAttack(attacker, target, random);
                    attacks += 1;
                }

                if (target.IsAlive)
                {
                    var counterChance = Clamp01(target.Morale * (ProphecyGameSession.Instance.Data.Config?.moraleCounterRate ?? 0.04f));
                    if (random.NextDouble() < counterChance)
                    {
                        ApplyAttack(target, attacker, random);
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
                .ThenByDescending(unit => unit.Row)
                .ThenBy(unit => unit.CurrentHp)
                .FirstOrDefault();
        }

        private static void ApplyAttack(BattleRuntimeUnit attacker, BattleRuntimeUnit target, Random random)
        {
            var damage = Math.Max(1, attacker.Attack + attacker.Power * 8 - target.Defense);
            var critRate = Math.Min(ProphecyGameSession.Instance.Data.Config?.critRateCap ?? 0.6f, Math.Max(0f, attacker.Luck * 0.025f));
            if (random.NextDouble() < critRate)
            {
                damage = (int)Math.Ceiling(damage * (ProphecyGameSession.Instance.Data.Config?.critDamageMultiple ?? 1.5f));
            }

            target.CurrentHp = Math.Max(0, target.CurrentHp - damage);
            attacker.DamageDone += damage;
        }

        private static BattleStubResult Finish(RunState runState, bool victory, int playerScore, int enemyScore, int hpLoss, int attacks, IReadOnlyList<BattleRuntimeUnit> players, IReadOnlyList<BattleRuntimeUnit> enemies)
        {
            if (hpLoss > 0)
            {
                runState.playerHp -= hpLoss;
            }

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
            var budget = Math.Max(6, runState.round * 3);
            var maxStar = Math.Min(6, 1 + runState.round / 3);
            var pool = data.Units
                .Where(unit => unit != null && !unit.hidden && unit.star <= maxStar && unit.id != "light_illusion")
                .OrderBy(unit => unit.star)
                .ToList();
            var slots = data.Config?.GetBoardOrder() ?? new List<string>();
            var enemies = new List<BattleRuntimeUnit>();
            var multiplier = 0.72f + runState.round * 0.045f;

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
                Cooldown = Math.Max(0.05f, interval * 0.5f)
            };
        }

        private static void ApplyContinuousAuras(IReadOnlyList<BattleRuntimeUnit> units)
        {
            var highestById = units
                .Where(unit => unit.IsAlive)
                .GroupBy(unit => unit.UnitId)
                .ToDictionary(group => group.Key, group => group.Max(unit => unit.Attack));
            foreach (var unit in units)
            {
                if (highestById.TryGetValue(unit.UnitId, out var highest))
                {
                    unit.Attack = Math.Max(unit.Attack, highest);
                }
            }
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
        public bool IsAlive => CurrentHp > 0;
    }
}
