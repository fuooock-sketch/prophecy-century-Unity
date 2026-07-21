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
            if (runState == null || handIndex < 0 || handIndex >= runState.handCards.Count)
            {
                return false;
            }

            var card = runState.handCards[handIndex];
            var targetSlot = string.IsNullOrWhiteSpace(boardSlotId) ? FirstOpenSlot(runState, card) : boardSlotId;
            if (!IsValidBoardSlot(targetSlot))
            {
                return false;
            }

            if (!CanPlaceCard(runState, card, targetSlot))
            {
                return false;
            }

            runState.boardUnits.Add(CloneToBoardUnit(card, targetSlot));
            runState.handCards.RemoveAt(handIndex);
            return true;
        }

        public bool MoveBoardUnit(RunState runState, string fromSlotId, string toSlotId)
        {
            if (runState == null || string.IsNullOrWhiteSpace(fromSlotId) || string.IsNullOrWhiteSpace(toSlotId) || fromSlotId == toSlotId)
            {
                return false;
            }

            if (!IsValidBoardSlot(fromSlotId) || !IsValidBoardSlot(toSlotId))
            {
                return false;
            }

            var moving = FindUnitOccupyingSlot(runState, fromSlotId);
            if (moving == null)
            {
                return false;
            }

            var target = FindUnitOccupyingSlot(runState, toSlotId);
            if (target == moving)
            {
                return false;
            }

            if (target != null && target != moving)
            {
                if (!TryResolveSwapAnchors(runState, moving, target, fromSlotId, toSlotId, out var movingAnchor, out var targetAnchor))
                {
                    return false;
                }

                moving.boardSlotId = movingAnchor;
                target.boardSlotId = targetAnchor;
                return true;
            }
            else if (!CanPlaceUnit(runState, moving, toSlotId, moving))
            {
                return false;
            }

            moving.boardSlotId = toSlotId;
            return true;
        }

        public string FirstOpenSlot(RunState runState, UnitCardState card = null)
        {
            var session = ProphecyGameSession.Instance;
            var order = session?.Data?.Config?.GetBoardOrder() ?? new List<string>();
            return order.FirstOrDefault(slot => CanPlaceCard(runState, card, slot));
        }

        public bool IsValidBoardSlot(string boardSlotId)
        {
            if (string.IsNullOrWhiteSpace(boardSlotId))
            {
                return false;
            }

            var session = ProphecyGameSession.Instance;
            var order = session?.Data?.Config?.GetBoardOrder() ?? new List<string>();
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
            var unit = FindUnitOccupyingSlot(runState, boardSlotId);
            if (unit == null)
            {
                return false;
            }

            runState.boardUnits.Remove(unit);
            runState.gold += GetUnitSellReward(unit);
            RefundShopPoolFromUnit(runState, unit);
            return true;
        }

        public bool CanPlaceCard(RunState runState, UnitCardState card, string boardSlotId)
        {
            var definition = card == null ? null : ProphecyGameSession.Instance?.Data?.FindUnit(card.unitId);
            return CanPlaceDefinition(runState, definition, boardSlotId, (BoardUnitState)null);
        }

        public bool CanMoveBoardUnit(RunState runState, string fromSlotId, string toSlotId)
        {
            if (runState == null || string.IsNullOrWhiteSpace(fromSlotId) || string.IsNullOrWhiteSpace(toSlotId) || fromSlotId == toSlotId)
            {
                return false;
            }

            if (!IsValidBoardSlot(fromSlotId) || !IsValidBoardSlot(toSlotId))
            {
                return false;
            }

            var moving = FindUnitOccupyingSlot(runState, fromSlotId);
            if (moving == null)
            {
                return false;
            }

            var target = FindUnitOccupyingSlot(runState, toSlotId);
            if (target == moving)
            {
                return false;
            }

            if (target != null && target != moving)
            {
                return TryResolveSwapAnchors(runState, moving, target, fromSlotId, toSlotId, out _, out _);
            }

            return CanPlaceUnit(runState, moving, toSlotId, moving);
        }

        public IReadOnlyList<string> GetMoveAffectedBoardSlots(RunState runState, string fromSlotId, string toSlotId)
        {
            if (runState == null || string.IsNullOrWhiteSpace(fromSlotId) || string.IsNullOrWhiteSpace(toSlotId))
            {
                return Array.Empty<string>();
            }

            var moving = FindUnitOccupyingSlot(runState, fromSlotId);
            if (moving == null)
            {
                return Array.Empty<string>();
            }

            var movingDefinition = ProphecyGameSession.Instance?.Data?.FindUnit(moving.unitId);
            var target = FindUnitOccupyingSlot(runState, toSlotId);
            if (target != null && target != moving)
            {
                if (!TryResolveSwapAnchors(runState, moving, target, fromSlotId, toSlotId, out var movingAnchor, out var targetAnchor))
                {
                    return Array.Empty<string>();
                }

                var targetDefinition = ProphecyGameSession.Instance?.Data?.FindUnit(target.unitId);
                return GetOccupiedBoardSlots(movingDefinition, movingAnchor)
                    .Concat(GetOccupiedBoardSlots(targetDefinition, targetAnchor))
                    .Distinct()
                    .ToList();
            }

            return CanPlaceUnit(runState, moving, toSlotId, moving)
                ? GetOccupiedBoardSlots(movingDefinition, toSlotId)
                : Array.Empty<string>();
        }

        public static BoardUnitState FindUnitOccupyingSlot(RunState runState, string boardSlotId)
        {
            if (runState?.boardUnits == null || string.IsNullOrWhiteSpace(boardSlotId))
            {
                return null;
            }

            return runState.boardUnits.FirstOrDefault(unit => GetOccupiedBoardSlots(unit).Contains(boardSlotId));
        }

        public static IReadOnlyList<string> GetOccupiedBoardSlots(BoardUnitState unit)
        {
            if (unit == null)
            {
                return Array.Empty<string>();
            }

            var definition = ProphecyGameSession.Instance?.Data?.FindUnit(unit.unitId);
            return GetOccupiedBoardSlots(definition, unit.boardSlotId);
        }

        public static IReadOnlyList<string> GetOccupiedBoardSlots(UnitDefinition definition, string anchorSlotId)
        {
            if (string.IsNullOrWhiteSpace(anchorSlotId))
            {
                return Array.Empty<string>();
            }

            if (ResolveBoardSize(definition) != 2 || !TryParseBoardSlot(anchorSlotId, out var row, out var column))
            {
                return new[] { anchorSlotId };
            }

            return new[]
            {
                anchorSlotId,
                $"{row}-{column - 1}"
            };
        }

        private bool CanPlaceUnit(RunState runState, BoardUnitState unit, string boardSlotId, BoardUnitState ignoreUnit)
        {
            var definition = unit == null ? null : ProphecyGameSession.Instance?.Data?.FindUnit(unit.unitId);
            return CanPlaceDefinition(runState, definition, boardSlotId, ignoreUnit);
        }

        private bool TryResolveSwapAnchors(
            RunState runState,
            BoardUnitState moving,
            BoardUnitState target,
            string fromSlotId,
            string toSlotId,
            out string movingAnchor,
            out string targetAnchor)
        {
            movingAnchor = null;
            targetAnchor = null;
            if (runState == null || moving == null || target == null || moving == target)
            {
                return false;
            }

            var movingDefinition = ProphecyGameSession.Instance?.Data?.FindUnit(moving.unitId);
            var targetDefinition = ProphecyGameSession.Instance?.Data?.FindUnit(target.unitId);
            foreach (var movingCandidate in GetAnchorCandidates(movingDefinition, toSlotId))
            {
                var movingSlots = GetOccupiedBoardSlots(movingDefinition, movingCandidate);
                if (!CanOccupySlotsIgnoring(runState, movingSlots, moving, target))
                {
                    continue;
                }

                foreach (var targetCandidate in GetAnchorCandidates(targetDefinition, fromSlotId))
                {
                    var targetSlots = GetOccupiedBoardSlots(targetDefinition, targetCandidate);
                    if (!CanOccupySlotsIgnoring(runState, targetSlots, moving, target)
                        || movingSlots.Any(targetSlots.Contains))
                    {
                        continue;
                    }

                    movingAnchor = movingCandidate;
                    targetAnchor = targetCandidate;
                    return true;
                }
            }

            return false;
        }

        private bool CanPlaceDefinition(RunState runState, UnitDefinition definition, string boardSlotId, BoardUnitState ignoreUnit)
        {
            return CanPlaceDefinition(runState, definition, boardSlotId, ignoreUnit == null ? Array.Empty<BoardUnitState>() : new[] { ignoreUnit });
        }

        private bool CanPlaceDefinition(RunState runState, UnitDefinition definition, string boardSlotId, IReadOnlyCollection<BoardUnitState> ignoreUnits)
        {
            if (runState == null || !IsValidBoardSlot(boardSlotId))
            {
                return false;
            }

            var occupiedSlots = GetOccupiedBoardSlots(definition, boardSlotId);
            if (occupiedSlots.Count == 0 || occupiedSlots.Any(slot => !IsValidBoardSlot(slot)))
            {
                return false;
            }

            return runState.boardUnits
                .Where(unit => unit != null && (ignoreUnits == null || !ignoreUnits.Contains(unit)))
                .All(unit => !GetOccupiedBoardSlots(unit).Any(occupiedSlots.Contains));
        }

        private bool CanOccupySlotsIgnoring(RunState runState, IReadOnlyCollection<string> occupiedSlots, params BoardUnitState[] ignoreUnits)
        {
            if (runState == null || occupiedSlots == null || occupiedSlots.Count == 0 || occupiedSlots.Any(slot => !IsValidBoardSlot(slot)))
            {
                return false;
            }

            return runState.boardUnits
                .Where(unit => unit != null && (ignoreUnits == null || !ignoreUnits.Contains(unit)))
                .All(unit => !GetOccupiedBoardSlots(unit).Any(occupiedSlots.Contains));
        }

        private static int ResolveBoardSize(UnitDefinition definition)
        {
            return definition != null && definition.size == 2 ? 2 : 1;
        }

        private static IReadOnlyList<string> GetAnchorCandidates(UnitDefinition definition, string requiredSlotId)
        {
            if (string.IsNullOrWhiteSpace(requiredSlotId))
            {
                return Array.Empty<string>();
            }

            var candidates = new List<string> { requiredSlotId };
            if (ResolveBoardSize(definition) == 2 && TryParseBoardSlot(requiredSlotId, out var row, out var column))
            {
                candidates.Add($"{row}-{column + 1}");
            }

            return candidates.Distinct().ToList();
        }

        private static bool TryParseBoardSlot(string slotId, out int row, out int column)
        {
            row = 0;
            column = 0;
            if (string.IsNullOrWhiteSpace(slotId))
            {
                return false;
            }

            var parts = slotId.Split('-');
            return parts.Length == 2
                && int.TryParse(parts[0], out row)
                && int.TryParse(parts[1], out column);
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
                manageCountGainEventProgress = card.manageCountGainEventProgress,
                manageSellCountBucket = card.manageSellCountBucket,
                manageReceiveGiftPowerBucket = card.manageReceiveGiftPowerBucket,
                manageReceiveGiftDiscoverTriggered = card.manageReceiveGiftDiscoverTriggered,
                manageRoundAttackRewardTriggered = card.manageRoundAttackRewardTriggered,
                pendingNextRoundTempCount = card.pendingNextRoundTempCount,
                pendingNextRoundPermanentCount = card.pendingNextRoundPermanentCount,
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
