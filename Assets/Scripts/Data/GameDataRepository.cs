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

        private Dictionary<string, UnitDefinition> _unitsById = new Dictionary<string, UnitDefinition>();
        private Dictionary<string, HeroDefinition> _heroesById = new Dictionary<string, HeroDefinition>();

        public void LoadAll()
        {
            Config = LoadObject<GameConfigData>("Data/unity_game_config");
            Campaigns = LoadArray<CampaignDefinition>("Data/campaigns");
            Heroes = LoadArray<HeroDefinition>("Data/heroes");
            Units = LoadArray<UnitDefinition>("Data/unit_data");

            _unitsById = Units.Where(unit => !string.IsNullOrWhiteSpace(unit.id)).ToDictionary(unit => unit.id, unit => unit);
            _heroesById = Heroes.Where(hero => !string.IsNullOrWhiteSpace(hero.id)).ToDictionary(hero => hero.id, hero => hero);
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
