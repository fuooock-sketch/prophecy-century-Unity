using System;
using System.Collections.Generic;
using System.Linq;
using ProphecyCentury.Core;
using ProphecyCentury.Data;
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
            var definition = ProphecyGameSession.Instance.Data.FindUnit(card.unitId);
            var startCount = ResolveStartCount(definition);
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
                boardAuraAttack = card.boardAuraAttack,
                shopBuffDefense = card.shopBuffDefense,
                shopBuffPower = card.shopBuffPower,
                shopBuffSpeed = card.shopBuffSpeed,
                shopBuffLuck = card.shopBuffLuck,
                shopBuffMorale = card.shopBuffMorale,
                baseCount = Math.Max(1, card.baseCount > 0 ? card.baseCount : startCount),
                maxCount = 0,
                forestGemCount = card.forestGemCount,
                roundTempCount = card.roundTempCount,
                roundTempAttack = card.roundTempAttack,
                roundTempPower = card.roundTempPower,
                roundTempMorale = card.roundTempMorale,
                forestGemsAttached = card.forestGemsAttached,
                forestGemsReceived = card.forestGemsReceived,
                manageEntryEffectTriggerCount = card.manageEntryEffectTriggerCount,
                manageRoundEntryEffectTriggerCount = card.manageRoundEntryEffectTriggerCount,
                manageRoundForestGemGiftBonusCount = card.manageRoundForestGemGiftBonusCount,
                manageRoundStatRetriggerTriggered = card.manageRoundStatRetriggerTriggered,
                manageGiftActionBucket = card.manageGiftActionBucket,
                manageAttackGainBucket = card.manageAttackGainBucket,
                manageSellCountBucket = card.manageSellCountBucket,
                manageReceiveGiftPowerBucket = card.manageReceiveGiftPowerBucket,
                manageReceiveGiftDiscoverTriggered = card.manageReceiveGiftDiscoverTriggered,
                manageRoundAttackRewardTriggered = card.manageRoundAttackRewardTriggered,
                pendingNextRoundTempCount = card.pendingNextRoundTempCount,
                battleProgressCounters = card.battleProgressCounters?.Select(counter => new BattleProgressCounterState { key = counter.key, value = counter.value }).ToList() ?? new System.Collections.Generic.List<BattleProgressCounterState>(),
                boardSlotId = boardSlotId
            };
        }

        private static int ResolveStartCount(UnitDefinition definition)
        {
            if (definition == null)
            {
                return 1;
            }

            return Math.Max(1, definition.defaultCount > 0 ? definition.defaultCount : definition.startCount > 0 ? definition.startCount : definition.baseCount > 0 ? definition.baseCount : 1);
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
            var priceTalent = talents?.FirstOrDefault(talent => talent.kind == "on_sell_price_if_count_threshold");
            if (priceTalent == null)
            {
                return fallback;
            }

            var startCount = ResolveStartCount(definition);
            var count = Math.Max(1, unit.baseCount > 0 ? unit.baseCount : startCount)
                + Math.Max(0, unit.roundTempCount);
            if (count < Math.Max(1, priceTalent.threshold))
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
