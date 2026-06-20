using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ProphecyCentury.Core;
using ProphecyCentury.Data;
using ProphecyCentury.Model;
using UnityEngine;

namespace ProphecyCentury.Systems
{
    public static class CustomChallengeSystem
    {
        private const string FileName = "custom_challenges.json";
        private const string CustomPrefix = "custom_challenge_";

        [Serializable]
        private sealed class CustomChallengeStore
        {
            public List<CustomChallengeCampaignState> challenges = new List<CustomChallengeCampaignState>();
        }

        private static string SavePath => Path.Combine(Application.persistentDataPath, FileName);

        public static bool IsCustomChallengeId(string campaignId)
        {
            return !string.IsNullOrWhiteSpace(campaignId) && campaignId.StartsWith(CustomPrefix, StringComparison.Ordinal);
        }

        public static List<CustomChallengeCampaignState> LoadAll()
        {
            try
            {
                if (!File.Exists(SavePath))
                {
                    return new List<CustomChallengeCampaignState>();
                }

                var store = JsonUtility.FromJson<CustomChallengeStore>(File.ReadAllText(SavePath));
                return NormalizeChallenges(store?.challenges);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Failed to load custom challenges: {ex.Message}");
                return new List<CustomChallengeCampaignState>();
            }
        }

        public static bool SaveAll(List<CustomChallengeCampaignState> challenges)
        {
            try
            {
                var store = new CustomChallengeStore { challenges = NormalizeChallenges(challenges) };
                File.WriteAllText(SavePath, JsonUtility.ToJson(store, true));
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Failed to save custom challenges: {ex.Message}");
                return false;
            }
        }

        public static bool TryGetChallenge(string challengeId, out CustomChallengeCampaignState challenge)
        {
            challenge = LoadAll().FirstOrDefault(item => item != null && item.id == challengeId);
            return challenge != null;
        }

        public static bool TryGetRound(string challengeId, int round, out CustomChallengeRoundState challengeRound)
        {
            challengeRound = null;
            if (!TryGetChallenge(challengeId, out var challenge))
            {
                return false;
            }

            challengeRound = challenge.rounds?.FirstOrDefault(item => item != null && item.round == round);
            return challengeRound != null && challengeRound.units != null && challengeRound.units.Count > 0;
        }

        public static CustomChallengeCampaignState CreateFromRun(RunState run)
        {
            if (run == null || run.customChallengeGenerated || run.campaignRoundLimit != 20 || run.campaignWins != 20 || run.campaignLosses != 0)
            {
                return null;
            }

            var rounds = NormalizeRounds(run.customChallengeCaptureRounds);
            if (rounds.Count < 20 || rounds.Any(item => item.units == null || item.units.Count == 0))
            {
                return null;
            }

            var challenges = LoadAll();
            var nextNumber = challenges.Count + 1;
            while (challenges.Any(item => item != null && item.name == $"我的通关阵型 #{nextNumber}"))
            {
                nextNumber += 1;
            }

            var now = DateTime.Now;
            var challenge = new CustomChallengeCampaignState
            {
                id = CustomPrefix + now.ToString("yyyyMMddHHmmss"),
                name = $"我的通关阵型 #{nextNumber}",
                createdLabel = $"通关挑战 {now:yyyy-MM-dd HH:mm}",
                sourceCampaignId = run.campaignId,
                sourceCampaignName = ResolveCampaignName(run.campaignId),
                rounds = rounds.Take(20).ToList()
            };

            challenges.Add(challenge);
            if (SaveAll(challenges))
            {
                run.customChallengeGenerated = true;
                run.customChallengeId = challenge.id;
                return challenge;
            }

            return null;
        }

        public static void CaptureRound(RunState run, int completedRound)
        {
            if (run == null || completedRound < 1 || completedRound > 20 || run.campaignRoundLimit != 20 || run.customChallengeGenerated)
            {
                return;
            }

            if (run.customChallengeCaptureRounds == null)
            {
                run.customChallengeCaptureRounds = new List<CustomChallengeRoundState>();
            }

            run.customChallengeCaptureRounds.RemoveAll(item => item != null && item.round == completedRound);
            var snapshot = new CustomChallengeRoundState
            {
                round = completedRound,
                units = (run.boardUnits ?? new List<BoardUnitState>())
                    .Where(unit => unit != null && !string.IsNullOrWhiteSpace(unit.unitId) && !string.IsNullOrWhiteSpace(unit.boardSlotId))
                    .OrderBy(unit => unit.boardSlotId)
                    .Select(ToChallengeUnit)
                    .ToList()
            };

            if (snapshot.units.Count > 0)
            {
                run.customChallengeCaptureRounds.Add(snapshot);
                run.customChallengeCaptureRounds = NormalizeRounds(run.customChallengeCaptureRounds);
            }
        }

        public static bool RenameChallenge(string challengeId, string newName)
        {
            var clean = string.IsNullOrWhiteSpace(newName) ? null : newName.Trim();
            if (string.IsNullOrWhiteSpace(challengeId) || string.IsNullOrWhiteSpace(clean))
            {
                return false;
            }

            var challenges = LoadAll();
            var challenge = challenges.FirstOrDefault(item => item != null && item.id == challengeId);
            if (challenge == null)
            {
                return false;
            }

            challenge.name = clean;
            return SaveAll(challenges);
        }

        public static bool DeleteChallenge(string challengeId)
        {
            if (string.IsNullOrWhiteSpace(challengeId))
            {
                return false;
            }

            var challenges = LoadAll();
            var removed = challenges.RemoveAll(item => item != null && item.id == challengeId) > 0;
            return removed && SaveAll(challenges);
        }

        public static WorldMapDefinition ResolveCustomChallengeMap(GameDataRepository data)
        {
            return data?.FindWorldMap("snow_peak_defense_map") ?? data?.WorldMaps?.FirstOrDefault();
        }

        public static void Normalize(RunState run)
        {
            if (run == null)
            {
                return;
            }

            if (run.customChallengeCaptureRounds == null)
            {
                run.customChallengeCaptureRounds = new List<CustomChallengeRoundState>();
            }

            if (string.IsNullOrWhiteSpace(run.customChallengeId) && IsCustomChallengeId(run.campaignId))
            {
                run.customChallengeId = run.campaignId;
            }

            run.customChallengeCaptureRounds = NormalizeRounds(run.customChallengeCaptureRounds);
        }

        private static CustomChallengeUnitState ToChallengeUnit(BoardUnitState unit)
        {
            var definition = ProphecyGameSession.Instance?.Data?.FindUnit(unit.unitId);
            var startCount = ResolveStartCount(definition);
            return new CustomChallengeUnitState
            {
                unitId = unit.unitId,
                name = string.IsNullOrWhiteSpace(unit.name) ? definition?.name : unit.name,
                star = unit.star > 0 ? unit.star : definition?.star ?? 1,
                isGolden = unit.isGolden,
                slotId = unit.boardSlotId,
                count = Math.Max(1, (unit.baseCount > 0 ? unit.baseCount : startCount) + unit.roundTempCount)
            };
        }

        private static List<CustomChallengeCampaignState> NormalizeChallenges(List<CustomChallengeCampaignState> challenges)
        {
            return (challenges ?? new List<CustomChallengeCampaignState>())
                .Where(item => item != null && !string.IsNullOrWhiteSpace(item.id))
                .Select(item =>
                {
                    item.rounds = NormalizeRounds(item.rounds);
                    if (string.IsNullOrWhiteSpace(item.name))
                    {
                        item.name = item.id;
                    }

                    return item;
                })
                .ToList();
        }

        private static List<CustomChallengeRoundState> NormalizeRounds(List<CustomChallengeRoundState> rounds)
        {
            return (rounds ?? new List<CustomChallengeRoundState>())
                .Where(item => item != null && item.round >= 1 && item.round <= 20)
                .GroupBy(item => item.round)
                .Select(group => group.Last())
                .OrderBy(item => item.round)
                .ToList();
        }

        private static string ResolveCampaignName(string campaignId)
        {
            var campaign = ProphecyGameSession.Instance?.Data?.FindCampaign(campaignId);
            return string.IsNullOrWhiteSpace(campaign?.name) ? campaignId : campaign.name;
        }

        private static int ResolveStartCount(UnitDefinition definition)
        {
            if (definition == null)
            {
                return 1;
            }

            return Math.Max(1, definition.defaultCount > 0 ? definition.defaultCount : definition.startCount > 0 ? definition.startCount : definition.baseCount > 0 ? definition.baseCount : 1);
        }
    }
}
