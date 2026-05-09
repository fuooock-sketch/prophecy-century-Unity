using System.Collections.Generic;
using System.Linq;
using ProphecyCentury.Core;
using ProphecyCentury.Model;

namespace ProphecyCentury.Systems
{
    public sealed class BoardSystem
    {
        public bool DeployFromHand(RunState runState, int handIndex, string boardSlotId = null)
        {
            if (handIndex < 0 || handIndex >= runState.handCards.Count)
            {
                return false;
            }

            var targetSlot = string.IsNullOrWhiteSpace(boardSlotId) ? FirstOpenSlot(runState) : boardSlotId;
            if (string.IsNullOrWhiteSpace(targetSlot))
            {
                return false;
            }

            if (runState.boardUnits.Any(unit => unit.boardSlotId == targetSlot))
            {
                return false;
            }

            var card = runState.handCards[handIndex];
            runState.boardUnits.Add(new BoardUnitState
            {
                unitId = card.unitId,
                name = card.name,
                star = card.star,
                isGolden = card.isGolden,
                boardSlotId = targetSlot
            });
            runState.handCards.RemoveAt(handIndex);
            return true;
        }

        public string FirstOpenSlot(RunState runState)
        {
            var session = ProphecyGameSession.Instance;
            var order = session.Data.Config?.GetBoardOrder() ?? new List<string>();
            return order.FirstOrDefault(slot => runState.boardUnits.All(unit => unit.boardSlotId != slot));
        }

        public bool SellFromHand(RunState runState, int handIndex)
        {
            if (handIndex < 0 || handIndex >= runState.handCards.Count)
            {
                return false;
            }

            runState.handCards.RemoveAt(handIndex);
            runState.gold += ProphecyGameSession.Instance.Data.Config?.unitSellReward ?? 1;
            return true;
        }

        public bool SellFromBoard(RunState runState, string boardSlotId)
        {
            var unit = runState.boardUnits.FirstOrDefault(item => item.boardSlotId == boardSlotId);
            if (unit == null)
            {
                return false;
            }

            runState.boardUnits.Remove(unit);
            runState.gold += ProphecyGameSession.Instance.Data.Config?.unitSellReward ?? 1;
            return true;
        }
    }
}
