using System.Linq;
using ProphecyCentury.Model;

namespace ProphecyCentury.Systems
{
    public sealed class BattleStubSystem
    {
        public BattleStubResult Resolve(RunState runState)
        {
            var unitCount = runState.boardUnits.Count;
            var totalAttack = runState.boardUnits.Sum(unit => unit.star * 50 + 80);
            var totalHealth = runState.boardUnits.Sum(unit => unit.star * 30 + 60);
            var playerScore = totalAttack + totalHealth + unitCount * 25;
            var enemyScore = 180 + (runState.round - 1) * 90;
            var win = playerScore >= enemyScore;

            if (win)
            {
                return new BattleStubResult
                {
                    Victory = true,
                    PlayerScore = playerScore,
                    EnemyScore = enemyScore,
                    HpDelta = 0,
                    Summary = $"Victory. Player score {playerScore} vs enemy score {enemyScore}."
                };
            }

            var damage = unitCount == 0 ? 15 : 8;
            runState.playerHp -= damage;
            return new BattleStubResult
            {
                Victory = false,
                PlayerScore = playerScore,
                EnemyScore = enemyScore,
                HpDelta = -damage,
                Summary = $"Defeat. Player score {playerScore} vs enemy score {enemyScore}. Lost {damage} HP."
            };
        }
    }

    public sealed class BattleStubResult
    {
        public bool Victory;
        public int PlayerScore;
        public int EnemyScore;
        public int HpDelta;
        public string Summary;
    }
}
