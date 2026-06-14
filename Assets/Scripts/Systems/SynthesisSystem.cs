using System;
using System.Collections.Generic;
using System.Linq;
using ProphecyCentury.Core;
using ProphecyCentury.Data;
using ProphecyCentury.Model;

namespace ProphecyCentury.Systems
{
public sealed class SynthesisSystem
    {
        private static readonly int[] CountLossByStar = { 0, 3, 2, 2, 1, 1, 0 };

        public bool TrySynthesizeAll(RunState runState)
        {
            var didSynthesize = false;
            var synthesizeCount = ProphecyGameSession.Instance.Data.Config?.synthesizeCount ?? 3;
            if (synthesizeCount <= 1)
            {
                synthesizeCount = 3;
            }

            while (true)
            {
                var candidates = CollectCandidates(runState);
                var entry = candidates
                    .GroupBy(item => item.Card.unitId)
                    .FirstOrDefault(group => group.Count() >= synthesizeCount);
                if (entry == null)
                {
                    break;
                }

                var picked = entry.Take(synthesizeCount).ToList();
                var pickedCards = picked.Select(item => item.Card).ToList();
                var unitId = entry.Key;
                RemovePicked(runState, picked);
                runState.handCards.Add(CreateGoldenUnit(unitId, pickedCards));
                didSynthesize = true;
            }

            return didSynthesize;
        }

        private static List<SynthesisCandidate> CollectCandidates(RunState runState)
        {
            var candidates = new List<SynthesisCandidate>();
            for (var i = 0; i < runState.handCards.Count; i += 1)
            {
                var card = runState.handCards[i];
                if (IsSynthesizable(card))
                {
                    candidates.Add(new SynthesisCandidate(card, false, i, null));
                }
            }

            for (var i = 0; i < runState.boardUnits.Count; i += 1)
            {
                var unit = runState.boardUnits[i];
                if (IsSynthesizable(unit))
                {
                    candidates.Add(new SynthesisCandidate(unit, true, i, unit.boardSlotId));
                }
            }

            return candidates;
        }

        private static bool IsSynthesizable(UnitCardState card)
        {
            return card != null && !card.isGolden && !ManageEventResolver.IsForestGemCard(card);
        }

        private static void RemovePicked(RunState runState, IReadOnlyList<SynthesisCandidate> picked)
        {
            foreach (var handIndex in picked.Where(item => !item.IsBoard).Select(item => item.Index).OrderByDescending(index => index))
            {
                if (handIndex >= 0 && handIndex < runState.handCards.Count)
                {
                    runState.handCards.RemoveAt(handIndex);
                }
            }

            var boardSlots = new HashSet<string>(picked
                .Where(item => item.IsBoard)
                .Select(item => item.BoardSlotId)
                .Where(slot => !string.IsNullOrWhiteSpace(slot)));
            runState.boardUnits.RemoveAll(unit => boardSlots.Contains(unit.boardSlotId));
        }

        private static UnitCardState CreateGoldenUnit(string unitId, IReadOnlyList<UnitCardState> sourceUnits)
        {
            var definition = ProphecyGameSession.Instance.Data.FindUnit(unitId);
            var inherited = ComputeGoldInheritedStats(definition, sourceUnits);
            return new UnitCardState
            {
                unitId = unitId,
                name = definition?.name ?? sourceUnits.FirstOrDefault()?.name ?? unitId,
                star = definition?.star ?? sourceUnits.FirstOrDefault()?.star ?? 1,
                isGolden = true,
                shopPoolCost = sourceUnits.Sum(GetPoolCost),
                shopPoolContribution = sourceUnits.Sum(unit => Math.Max(0, unit.shopPoolContribution)),
                fromShopPurchase = sourceUnits.Any(unit => unit.fromShopPurchase || unit.shopPoolContribution > 0),
                shopBuffHp = inherited.hp - (definition?.hp ?? inherited.hp),
                shopBuffAttack = inherited.attack - (definition?.attack ?? inherited.attack),
                shopBuffDefense = inherited.defense - (definition?.defense ?? inherited.defense),
                shopBuffPower = inherited.power - (definition?.power ?? inherited.power),
                shopBuffSpeed = inherited.speed - (definition?.speed ?? inherited.speed),
                shopBuffLuck = inherited.luck - (definition?.luck ?? inherited.luck),
                shopBuffMorale = inherited.morale - (definition?.morale ?? inherited.morale),
                baseCount = ResolveSynthesisCount(definition, sourceUnits),
                maxCount = 0,
                forestGemCount = sourceUnits.Sum(unit => Math.Max(0, unit.forestGemCount)),
                forestGemsAttached = sourceUnits.Sum(unit => Math.Max(0, unit.forestGemsAttached)),
                forestGemsReceived = sourceUnits.Sum(unit => Math.Max(0, unit.forestGemsReceived)),
                manageRoundEntryEffectTriggerCount = sourceUnits.Max(unit => unit.manageRoundEntryEffectTriggerCount),
                manageRoundStatRetriggerTriggered = sourceUnits.Any(unit => unit.manageRoundStatRetriggerTriggered),
                manageGiftActionBucket = sourceUnits.Max(unit => unit.manageGiftActionBucket),
                manageAttackGainBucket = sourceUnits.Max(unit => unit.manageAttackGainBucket),
                manageReceiveGiftPowerBucket = sourceUnits.Max(unit => unit.manageReceiveGiftPowerBucket),
                manageReceiveGiftDiscoverTriggered = sourceUnits.Any(unit => unit.manageReceiveGiftDiscoverTriggered),
                manageRoundAttackRewardTriggered = sourceUnits.Any(unit => unit.manageRoundAttackRewardTriggered),
                battleProgressCounters = MergeBattleProgress(sourceUnits)
            };
        }

        private static List<BattleProgressCounterState> MergeBattleProgress(IEnumerable<UnitCardState> sourceUnits)
        {
            return sourceUnits
                .Where(unit => unit.battleProgressCounters != null)
                .SelectMany(unit => unit.battleProgressCounters)
                .Where(counter => counter != null && !string.IsNullOrWhiteSpace(counter.key))
                .GroupBy(counter => counter.key)
                .Select(group => new BattleProgressCounterState { key = group.Key, value = group.Max(counter => counter.value) })
                .ToList();
        }

        private static GoldInheritedStats ComputeGoldInheritedStats(UnitDefinition definition, IReadOnlyList<UnitCardState> units)
        {
            var fallbackHp = definition?.hp ?? 1;
            var fallbackAttack = definition?.attack ?? 1;
            var fallbackPower = definition?.power ?? 1;
            var fallbackSpeed = definition?.speed ?? 1;
            var fallbackLuck = definition?.luck ?? 0;
            var fallbackMorale = definition?.morale ?? 0;

            return new GoldInheritedStats
            {
                hp = CeilMax(units, unit => (definition?.hp ?? fallbackHp) + unit.shopBuffHp, fallbackHp),
                attack = CeilMax(units, unit => (definition?.attack ?? fallbackAttack) + unit.shopBuffAttack, fallbackAttack),
                defense = 10,
                power = CeilMax(units, unit => (definition?.power ?? fallbackPower) + unit.shopBuffPower, fallbackPower),
                speed = CeilMax(units, unit => (definition?.speed ?? fallbackSpeed) + unit.shopBuffSpeed, fallbackSpeed),
                luck = CeilMax(units, unit => (definition?.luck ?? fallbackLuck) + unit.shopBuffLuck, fallbackLuck),
                morale = CeilMax(units, unit => (definition?.morale ?? fallbackMorale) + unit.shopBuffMorale, fallbackMorale)
            };
        }

        private static int CeilMax(IEnumerable<UnitCardState> units, Func<UnitCardState, int> getter, int fallback)
        {
            var max = units.Select(getter).DefaultIfEmpty(fallback).Max();
            return (int)Math.Ceiling(Math.Max(1, max) * 1.1f);
        }

        private static int GetPoolCost(UnitCardState unit)
        {
            if (unit == null)
            {
                return 0;
            }

            if (unit.shopPoolCost > 0)
            {
                return unit.shopPoolCost;
            }

            return unit.isGolden ? 3 : 1;
        }

        private static int ResolveBaseCount(UnitDefinition definition, UnitCardState unit)
        {
            if (unit == null)
            {
                return ResolveStartCount(definition);
            }

            return Math.Max(1, (unit.baseCount > 0 ? unit.baseCount : ResolveStartCount(definition)) + unit.roundTempCount);
        }

        private static int ResolveStartCount(UnitDefinition definition)
        {
            if (definition == null)
            {
                return 1;
            }

            return Math.Max(1, definition.defaultCount > 0 ? definition.defaultCount : definition.startCount > 0 ? definition.startCount : definition.baseCount > 0 ? definition.baseCount : 1);
        }

        private static int ResolveSynthesisCount(UnitDefinition definition, IReadOnlyList<UnitCardState> sourceUnits)
        {
            var total = sourceUnits?.Sum(unit => ResolveBaseCount(definition, unit)) ?? ResolveStartCount(definition);
            var star = Math.Max(1, Math.Min(6, definition?.star ?? sourceUnits?.FirstOrDefault()?.star ?? 1));
            var loss = CountLossByStar[star];
            return Math.Max(1, total - loss);
        }

        private struct SynthesisCandidate
        {
            public SynthesisCandidate(UnitCardState card, bool isBoard, int index, string boardSlotId)
            {
                Card = card;
                IsBoard = isBoard;
                Index = index;
                BoardSlotId = boardSlotId;
            }

            public UnitCardState Card { get; }
            public bool IsBoard { get; }
            public int Index { get; }
            public string BoardSlotId { get; }
        }

        private struct GoldInheritedStats
        {
            public int hp;
            public int attack;
            public int defense;
            public int power;
            public int speed;
            public int luck;
            public int morale;
        }
    }
}
