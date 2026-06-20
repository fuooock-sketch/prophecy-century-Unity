using System;
using System.Collections.Generic;
using System.Linq;
using ProphecyCentury.Core;
using ProphecyCentury.Data;
using ProphecyCentury.Model;

namespace ProphecyCentury.Systems
{
    public static class CampaignFormationPreviewSystem
    {
        private static readonly string[] PreviewSlotOrder =
        {
            "1-1", "2-1", "2-2", "3-1", "3-2", "3-3", "4-1", "4-2", "4-3", "4-4"
        };

        public static List<CampaignFormationPreviewRound> BuildPreviewRounds(string campaignId)
        {
            var data = ProphecyGameSession.Instance?.Data;
            if (data == null || string.IsNullOrWhiteSpace(campaignId))
            {
                return new List<CampaignFormationPreviewRound>();
            }

            if (CustomChallengeSystem.IsCustomChallengeId(campaignId))
            {
                return BuildCustomChallengePreviewRounds(campaignId, data);
            }

            return BuildConfiguredCampaignPreviewRounds(campaignId, data);
        }

        public static bool HasPreviewRounds(string campaignId)
        {
            return BuildPreviewRounds(campaignId).Count > 0;
        }

        public static CampaignFormationPreviewSummary BuildPreviewSummary(string campaignId)
        {
            var rounds = BuildPreviewRounds(campaignId);
            foreach (var round in rounds)
            {
                round.RoundScore = CalculateRoundScore(round);
            }

            return new CampaignFormationPreviewSummary
            {
                Rounds = rounds,
                DifficultyScore = CalculateCampaignScore(rounds)
            };
        }

        private static List<CampaignFormationPreviewRound> BuildCustomChallengePreviewRounds(string campaignId, GameDataRepository data)
        {
            if (!CustomChallengeSystem.TryGetChallenge(campaignId, out var challenge) || challenge?.rounds == null)
            {
                return new List<CampaignFormationPreviewRound>();
            }

            return challenge.rounds
                .Where(round => round?.units != null && round.units.Count > 0)
                .OrderBy(round => round.round)
                .Select(round => new CampaignFormationPreviewRound
                {
                    Round = round.round,
                    SourceName = challenge.name,
                    Units = round.units
                        .Where(unit => unit != null && !string.IsNullOrWhiteSpace(unit.unitId))
                        .Select(unit => ToPreviewUnit(unit, data))
                        .Where(unit => unit != null)
                        .ToList()
                })
                .Select(ScoreRound)
                .Where(round => round.Units.Count > 0)
                .ToList();
        }

        private static List<CampaignFormationPreviewRound> BuildConfiguredCampaignPreviewRounds(string campaignId, GameDataRepository data)
        {
            var campaign = data.FindCampaign(campaignId);
            var map = data.FindWorldMap(campaign?.mapId);
            if (map?.nodes == null)
            {
                return new List<CampaignFormationPreviewRound>();
            }

            var rounds = new List<CampaignFormationPreviewRound>();
            foreach (var node in map.nodes
                         .Where(node => node != null && !string.IsNullOrWhiteSpace(node.enemyPresetId))
                         .OrderBy(node => node.layer)
                         .ThenBy(node => node.id, StringComparer.Ordinal))
            {
                var preset = data.FindEnemyPreset(node.enemyPresetId);
                if (preset?.units == null || preset.units.Length == 0)
                {
                    continue;
                }

                var units = preset.units
                    .Where(unit => unit != null && !string.IsNullOrWhiteSpace(unit.unitId))
                    .Select(unit => ToPreviewUnit(unit, data))
                    .Where(unit => unit != null)
                    .ToList();
                if (units.Count == 0)
                {
                    continue;
                }

                rounds.Add(new CampaignFormationPreviewRound
                {
                    Round = rounds.Count + 1,
                    NodeId = node.id,
                    PresetId = preset.id,
                    SourceName = string.IsNullOrWhiteSpace(preset.name) ? node.name : preset.name,
                    Units = units
                });
            }

            return rounds.Select(ScoreRound).ToList();
        }

        private static CampaignFormationPreviewUnit ToPreviewUnit(EnemyPresetUnitDefinition unit, GameDataRepository data)
        {
            var definition = data.FindUnit(unit.unitId);
            if (definition == null)
            {
                return null;
            }

            return new CampaignFormationPreviewUnit
            {
                UnitId = definition.id,
                Name = definition.name,
                Star = unit.star > 0 ? unit.star : definition.star,
                Count = unit.count > 0 ? unit.count : Math.Max(1, definition.startCount > 0 ? definition.startCount : definition.defaultCount),
                SlotId = NormalizePreviewSlot(unit.slotId),
                IsGolden = false,
                StaticPower = CalculateUnitPower(definition, unit.star > 0 ? unit.star : definition.star, unit.count > 0 ? unit.count : Math.Max(1, definition.startCount > 0 ? definition.startCount : definition.defaultCount), false)
            };
        }

        private static CampaignFormationPreviewUnit ToPreviewUnit(CustomChallengeUnitState unit, GameDataRepository data)
        {
            var definition = data.FindUnit(unit.unitId);
            if (definition == null)
            {
                return null;
            }

            return new CampaignFormationPreviewUnit
            {
                UnitId = definition.id,
                Name = string.IsNullOrWhiteSpace(unit.name) ? definition.name : unit.name,
                Star = unit.star > 0 ? unit.star : definition.star,
                Count = Math.Max(1, unit.count),
                SlotId = NormalizePreviewSlot(unit.slotId),
                IsGolden = unit.isGolden,
                StaticPower = CalculateUnitPower(definition, unit.star > 0 ? unit.star : definition.star, Math.Max(1, unit.count), unit.isGolden)
            };
        }

        private static CampaignFormationPreviewRound ScoreRound(CampaignFormationPreviewRound round)
        {
            if (round != null)
            {
                round.RoundScore = CalculateRoundScore(round);
            }

            return round;
        }

        private static int CalculateCampaignScore(IReadOnlyList<CampaignFormationPreviewRound> rounds)
        {
            if (rounds == null || rounds.Count == 0)
            {
                return 0;
            }

            var weightedTotal = 0f;
            var weightTotal = 0f;
            for (var i = 0; i < rounds.Count; i += 1)
            {
                var weight = 1f + i * 0.035f;
                weightedTotal += Math.Max(0, rounds[i].RoundScore) * weight;
                weightTotal += weight;
            }

            var weightedAverage = weightTotal <= 0f ? 0f : weightedTotal / weightTotal;
            var peak = rounds.Max(round => Math.Max(0, round.RoundScore));
            return Math.Min(100, Math.Max(0, (int)Math.Round(weightedAverage * 0.7f + peak * 0.3f)));
        }

        private static int CalculateRoundScore(CampaignFormationPreviewRound round)
        {
            if (round?.Units == null || round.Units.Count == 0)
            {
                return 0;
            }

            var totalPower = round.Units.Sum(unit => Math.Max(0, unit.StaticPower));
            var formationBonus = Math.Min(12, Math.Max(0, round.Units.Count - 1) * 2);
            return Math.Min(100, Math.Max(1, (int)Math.Round(Math.Sqrt(totalPower) * 1.95f + formationBonus)));
        }

        private static int CalculateUnitPower(UnitDefinition definition, int star, int count, bool golden)
        {
            if (definition == null)
            {
                return 0;
            }

            var hpPerUnit = Math.Max(1, definition.hpPerUnit > 0 ? definition.hpPerUnit : definition.hp);
            var damage = Math.Max(1, definition.damageMax > 0 || definition.damageMin > 0
                ? (definition.damageMin + definition.damageMax) / 2
                : definition.attack);
            var statPower = damage * 1.7f
                + Math.Max(0, definition.defense) * 1.2f
                + Math.Max(0, definition.power) * 1.35f
                + Math.Max(0, definition.initiative) * 0.7f
                + Math.Max(0, definition.speed) * 0.55f
                + Math.Max(0, definition.range) * 1.8f
                + hpPerUnit * 0.33f;
            var starMultiplier = 1f + Math.Max(0, star - 1) * 0.18f;
            var goldenMultiplier = golden ? 1.25f : 1f;
            return Math.Max(1, (int)Math.Round(Math.Max(1, count) * statPower * starMultiplier * goldenMultiplier));
        }

        private static string NormalizePreviewSlot(string slotId)
        {
            if (IsBoardSlot(slotId))
            {
                return slotId;
            }

            if (!string.IsNullOrWhiteSpace(slotId)
                && slotId.StartsWith("enemy_", StringComparison.Ordinal)
                && int.TryParse(slotId.Substring("enemy_".Length), out var enemyIndex)
                && enemyIndex >= 1
                && enemyIndex <= PreviewSlotOrder.Length)
            {
                return PreviewSlotOrder[enemyIndex - 1];
            }

            return "1-1";
        }

        private static bool IsBoardSlot(string slotId)
        {
            return PreviewSlotOrder.Contains(slotId);
        }
    }

    public sealed class CampaignFormationPreviewRound
    {
        public int Round;
        public string NodeId;
        public string PresetId;
        public string SourceName;
        public int RoundScore;
        public List<CampaignFormationPreviewUnit> Units = new List<CampaignFormationPreviewUnit>();
    }

    public sealed class CampaignFormationPreviewUnit
    {
        public string UnitId;
        public string Name;
        public int Star;
        public bool IsGolden;
        public int Count;
        public string SlotId;
        public int StaticPower;
    }

    public sealed class CampaignFormationPreviewSummary
    {
        public int DifficultyScore;
        public List<CampaignFormationPreviewRound> Rounds = new List<CampaignFormationPreviewRound>();
    }
}
