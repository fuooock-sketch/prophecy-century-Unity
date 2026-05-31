using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProphecyCentury.UI
{
    [CreateAssetMenu(menuName = "Prophecy Century/UI/Unit Card Race Style Library")]
    public sealed class UnitCardRaceStyleLibrary : ScriptableObject
    {
        private const string DefaultResourcePath = "Data/UnitCardRaceStyleLibrary";

        [SerializeField] private List<UnitCardRaceStyle> styles = new List<UnitCardRaceStyle>();
        [SerializeField] private UnitCardRaceStyle fallbackStyle = UnitCardRaceStyle.Fallback();

        private static UnitCardRaceStyleLibrary _cachedDefault;

        public static UnitCardRaceStyleLibrary LoadDefault()
        {
            if (_cachedDefault != null)
            {
                return _cachedDefault;
            }

            _cachedDefault = Resources.Load<UnitCardRaceStyleLibrary>(DefaultResourcePath);
            if (_cachedDefault == null)
            {
                _cachedDefault = CreateInstance<UnitCardRaceStyleLibrary>();
                _cachedDefault.ResetToDefaultRaceStyles();
            }

            return _cachedDefault;
        }

        public UnitCardRaceStyle GetStyle(string race)
        {
            if (!string.IsNullOrWhiteSpace(race))
            {
                for (var i = 0; i < styles.Count; i += 1)
                {
                    if (styles[i] != null && string.Equals(styles[i].race, race, StringComparison.Ordinal))
                    {
                        return styles[i];
                    }
                }
            }

            return fallbackStyle ?? UnitCardRaceStyle.Fallback();
        }

        public void ResetToDefaultRaceStyles()
        {
            styles = new List<UnitCardRaceStyle>
            {
                new UnitCardRaceStyle("甘地", new Color32(51, 78, 118, 245), new Color32(126, 174, 224, 255)),
                new UnitCardRaceStyle("甘德", new Color32(45, 88, 75, 245), new Color32(116, 196, 154, 255)),
                new UnitCardRaceStyle("甘席", new Color32(92, 67, 42, 245), new Color32(224, 170, 94, 255)),
                new UnitCardRaceStyle("甘格尔", new Color32(83, 48, 75, 245), new Color32(207, 116, 184, 255))
            };
            fallbackStyle = UnitCardRaceStyle.Fallback();
        }

        public void SetDefaultSprites(Sprite normalFrame, Sprite goldenFrame, Sprite gandhiBackground, Sprite gandeBackground, Sprite ganxiBackground, Sprite gangerBackground)
        {
            ResetToDefaultRaceStyles();
            for (var i = 0; i < styles.Count; i += 1)
            {
                styles[i].frame = normalFrame;
                styles[i].goldenFrame = goldenFrame;
                switch (styles[i].race)
                {
                    case "甘地":
                        styles[i].background = gandhiBackground;
                        break;
                    case "甘德":
                        styles[i].background = gandeBackground;
                        break;
                    case "甘席":
                        styles[i].background = ganxiBackground;
                        break;
                    case "甘格尔":
                        styles[i].background = gangerBackground;
                        break;
                }
            }

            fallbackStyle.frame = normalFrame;
            fallbackStyle.background = gandhiBackground;
            fallbackStyle.goldenFrame = goldenFrame;
        }
    }

    [Serializable]
    public sealed class UnitCardRaceStyle
    {
        public string race;
        public Sprite background;
        public Sprite frame;
        public Sprite goldenFrame;
        public Color backgroundColor = new Color32(42, 58, 74, 245);
        public Color frameColor = new Color32(96, 132, 164, 255);
        public Color titleColor = Color.white;
        public Color statsColor = Color.white;
        public Color tagsColor = new Color32(220, 228, 236, 255);

        public UnitCardRaceStyle()
        {
        }

        public UnitCardRaceStyle(string race, Color backgroundColor, Color frameColor)
        {
            this.race = race;
            this.backgroundColor = backgroundColor;
            this.frameColor = frameColor;
        }

        public static UnitCardRaceStyle Fallback()
        {
            return new UnitCardRaceStyle("默认", new Color32(42, 58, 74, 245), new Color32(96, 132, 164, 255));
        }
    }
}
