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
            if (!IsValidBoardSlot(targetSlot))
            {
                return false;
            }

            if (runState.boardUnits.Any(unit => unit.boardSlotId == targetSlot))
            {
                return false;
            }

            var card = runState.handCards[handIndex];
            runState.boardUnits.Add(CloneToBoardUnit(card, targetSlot));
            runState.handCards.RemoveAt(handIndex);
            return true;
        }

        public bool MoveBoardUnit(RunState runState, string fromSlotId, string toSlotId)
        {
            if (string.IsNullOrWhiteSpace(fromSlotId) || string.IsNullOrWhiteSpace(toSlotId) || fromSlotId == toSlotId)
            {
                return false;
            }

            if (!IsValidBoardSlot(fromSlotId) || !IsValidBoardSlot(toSlotId))
            {
                return false;
            }

            var moving = runState.boardUnits.FirstOrDefault(unit => unit.boardSlotId == fromSlotId);
            if (moving == null)
            {
                return false;
            }

            var target = runState.boardUnits.FirstOrDefault(unit => unit.boardSlotId == toSlotId);
            if (target != null)
            {
                target.boardSlotId = fromSlotId;
            }

            moving.boardSlotId = toSlotId;
            return true;
        }

        public string FirstOpenSlot(RunState runState)
        {
            var session = ProphecyGameSession.Instance;
            var order = session.Data.Config?.GetBoardOrder() ?? new List<string>();
            return order.FirstOrDefault(slot => runState.boardUnits.All(unit => unit.boardSlotId != slot));
        }

        public bool IsValidBoardSlot(string boardSlotId)
        {
            if (string.IsNullOrWhiteSpace(boardSlotId))
            {
                return false;
            }

            var session = ProphecyGameSession.Instance;
            var order = session.Data.Config?.GetBoardOrder() ?? new List<string>();
            return order.Contains(boardSlotId);
        }

        public bool SellFromHand(RunState runState, int handIndex)
        {
            if (handIndex < 0 || handIndex >= runState.handCards.Count)
            {
                return false;
            }

            var unit = runState.handCards[handIndex];
            runState.gold += GetUnitSellReward(unit);
            RefundShopPoolFromUnit(runState, unit);
            runState.handCards.RemoveAt(handIndex);
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
            runState.gold += GetUnitSellReward(unit);
            RefundShopPoolFromUnit(runState, unit);
            return true;
        }

        private static BoardUnitState CloneToBoardUnit(UnitCardState card, string boardSlotId)
        {
            return new BoardUnitState
            {
                unitId = card.unitId,
                name = card.name,
                star = card.star,
                isGolden = card.isGolden,
                shopPoolCost = card.shopPoolCost,
                shopPoolReserved = false,
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
                forestGemsReceived = card.forestGemsReceived,
                manageEntryEffectTriggerCount = card.manageEntryEffectTriggerCount,
                manageGiftActionBucket = card.manageGiftActionBucket,
                manageReceiveGiftPowerBucket = card.manageReceiveGiftPowerBucket,
                manageReceiveGiftDiscoverTriggered = card.manageReceiveGiftDiscoverTriggered,
                boardSlotId = boardSlotId
            };
        }

        private static int GetUnitSellReward(UnitCardState unit)
        {
            var fallback = ProphecyGameSession.Instance.Data.Config?.unitSellReward ?? 1;
            if (unit == null)
            {
                return fallback;
            }

            var definition = ProphecyGameSession.Instance.Data.FindUnit(unit.unitId);
            var talents = unit.isGolden ? definition?.goldTalents ?? definition?.talents : definition?.talents;
            var attack = (definition?.attack ?? 0) + unit.shopBuffAttack;
            var priceTalent = talents?.FirstOrDefault(talent => talent.kind == "on_sell_price_if_attack_threshold");
            if (priceTalent == null || attack < priceTalent.threshold)
            {
                return fallback;
            }

            return priceTalent.price > 0 ? priceTalent.price : fallback;
        }

        private static void RefundShopPoolFromUnit(RunState runState, UnitCardState unit)
        {
            if (unit == null || string.IsNullOrWhiteSpace(unit.unitId) || unit.shopPoolContribution <= 0)
            {
                return;
            }

            var entry = runState.shopPool.FirstOrDefault(item => item.unitId == unit.unitId);
            if (entry == null)
            {
                unit.shopPoolContribution = 0;
                unit.fromShopPurchase = false;
                return;
            }

            entry.remain = Clamp(entry.remain + unit.shopPoolContribution, 0, entry.baseLimit);
            unit.shopPoolContribution = 0;
            unit.fromShopPurchase = false;
        }

        private static int Clamp(int value, int min, int max)
        {
            if (value < min)
            {
                return min;
            }

            return value > max ? max : value;
        }
    }
}
