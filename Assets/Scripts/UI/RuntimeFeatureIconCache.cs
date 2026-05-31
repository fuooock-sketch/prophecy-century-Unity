using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

namespace ProphecyCentury.UI
{
    public static class RuntimeFeatureIconCache
    {
        private static readonly Dictionary<string, Sprite> SpritesByIconName = new Dictionary<string, Sprite>();

        public static Sprite GetFeatureSprite(string iconName)
        {
            if (string.IsNullOrWhiteSpace(iconName))
            {
                return null;
            }

            if (SpritesByIconName.TryGetValue(iconName, out var sprite))
            {
                return sprite;
            }

            var fullPath = Path.Combine(Application.dataPath, "Art", "icon", "feature", iconName + ".png");
            if (!File.Exists(fullPath))
            {
                SpritesByIconName[iconName] = null;
                return null;
            }

            var bytes = File.ReadAllBytes(fullPath);
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!texture.LoadImage(bytes))
            {
                SpritesByIconName[iconName] = null;
                return null;
            }

            texture.filterMode = FilterMode.Bilinear;
            texture.anisoLevel = 4;
            texture.wrapMode = TextureWrapMode.Clamp;
            sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f));
            SpritesByIconName[iconName] = sprite;
            return sprite;
        }

        public static void ApplyTo(Image image, string iconName)
        {
            if (image == null)
            {
                return;
            }

            var sprite = GetFeatureSprite(iconName);
            image.sprite = sprite;
            image.preserveAspect = true;
            image.enabled = sprite != null;
        }
    }
}
