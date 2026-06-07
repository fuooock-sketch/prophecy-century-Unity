using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ProphecyCentury.Data
{
    public sealed class GameDataRepository
    {
        public GameConfigData Config { get; private set; }
        public IReadOnlyList<CampaignDefinition> Campaigns { get; private set; } = new List<CampaignDefinition>();
        public IReadOnlyList<HeroDefinition> Heroes { get; private set; } = new List<HeroDefinition>();
        public IReadOnlyList<UnitDefinition> Units { get; private set; } = new List<UnitDefinition>();
        public IReadOnlyList<WorldMapDefinition> WorldMaps { get; private set; } = new List<WorldMapDefinition>();
        public IReadOnlyList<TreasureDefinition> Treasures { get; private set; } = new List<TreasureDefinition>();
        public IReadOnlyList<EnemyPresetDefinition> EnemyPresets { get; private set; } = new List<EnemyPresetDefinition>();
        public RunFlowConfigDefinition RunFlowConfig { get; private set; }

        private Dictionary<string, UnitDefinition> _unitsById = new Dictionary<string, UnitDefinition>();
        private Dictionary<string, HeroDefinition> _heroesById = new Dictionary<string, HeroDefinition>();
        private Dictionary<string, WorldMapDefinition> _worldMapsById = new Dictionary<string, WorldMapDefinition>();
        private Dictionary<string, TreasureDefinition> _treasuresById = new Dictionary<string, TreasureDefinition>();
        private Dictionary<string, EnemyPresetDefinition> _enemyPresetsById = new Dictionary<string, EnemyPresetDefinition>();

        public void LoadAll()
        {
            Config = LoadObject<GameConfigData>("Data/unity_game_config");
            Campaigns = LoadArray<CampaignDefinition>("Data/campaigns");
            Heroes = LoadArray<HeroDefinition>("Data/heroes");
            Units = LoadArray<UnitDefinition>("Data/unit_data");
            WorldMaps = LoadArray<WorldMapDefinition>("Data/world_maps");
            Treasures = LoadArray<TreasureDefinition>("Data/treasures");
            EnemyPresets = LoadArray<EnemyPresetDefinition>("Data/boss_enemies");
            RunFlowConfig = LoadObject<RunFlowConfigDefinition>("Data/run_flow_config");

            _unitsById = Units.Where(unit => !string.IsNullOrWhiteSpace(unit.id)).ToDictionary(unit => unit.id, unit => unit);
            _heroesById = Heroes.Where(hero => !string.IsNullOrWhiteSpace(hero.id)).ToDictionary(hero => hero.id, hero => hero);
            _worldMapsById = WorldMaps.Where(map => !string.IsNullOrWhiteSpace(map.id)).ToDictionary(map => map.id, map => map);
            _treasuresById = Treasures.Where(treasure => !string.IsNullOrWhiteSpace(treasure.id)).ToDictionary(treasure => treasure.id, treasure => treasure);
            _enemyPresetsById = EnemyPresets.Where(preset => !string.IsNullOrWhiteSpace(preset.id)).ToDictionary(preset => preset.id, preset => preset);
        }

        public UnitDefinition FindUnit(string unitId)
        {
            if (string.IsNullOrWhiteSpace(unitId))
            {
                return null;
            }

            _unitsById.TryGetValue(unitId, out var unit);
            return unit;
        }

        public HeroDefinition FindHero(string heroId)
        {
            if (string.IsNullOrWhiteSpace(heroId))
            {
                return null;
            }

            _heroesById.TryGetValue(heroId, out var hero);
            return hero;
        }

        public CampaignDefinition FindCampaign(string campaignId)
        {
            if (string.IsNullOrWhiteSpace(campaignId))
            {
                return null;
            }

            return Campaigns.FirstOrDefault(campaign => campaign.id == campaignId);
        }

        public WorldMapDefinition FindWorldMap(string mapId)
        {
            if (string.IsNullOrWhiteSpace(mapId))
            {
                return null;
            }

            _worldMapsById.TryGetValue(mapId, out var map);
            return map;
        }

        public TreasureDefinition FindTreasure(string treasureId)
        {
            if (string.IsNullOrWhiteSpace(treasureId))
            {
                return null;
            }

            _treasuresById.TryGetValue(treasureId, out var treasure);
            return treasure;
        }

        public EnemyPresetDefinition FindEnemyPreset(string presetId)
        {
            if (string.IsNullOrWhiteSpace(presetId))
            {
                return null;
            }

            _enemyPresetsById.TryGetValue(presetId, out var preset);
            return preset;
        }

        public RunFlowPhaseDefinition FindRunFlowPhase(string phase, string state = null)
        {
            var phases = RunFlowConfig?.phases;
            if (phases == null || phases.Length == 0)
            {
                return null;
            }

            return phases.FirstOrDefault(item => item != null
                    && item.phase == phase
                    && (string.IsNullOrWhiteSpace(state) || item.state == state))
                ?? phases.FirstOrDefault(item => item != null && item.phase == phase);
        }

        private static T LoadObject<T>(string resourcePath)
        {
            var textAsset = Resources.Load<TextAsset>(resourcePath);
            return textAsset == null ? default : JsonUtility.FromJson<T>(textAsset.text);
        }

        private static IReadOnlyList<T> LoadArray<T>(string resourcePath)
        {
            var textAsset = Resources.Load<TextAsset>(resourcePath);
            return textAsset == null ? new List<T>() : JsonArrayUtility.FromJsonArray<T>(textAsset.text);
        }
    }
}
