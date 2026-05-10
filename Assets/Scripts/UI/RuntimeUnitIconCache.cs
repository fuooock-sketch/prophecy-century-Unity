using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

namespace ProphecyCentury.UI
{
    public static class RuntimeUnitIconCache
    {
        private static readonly Dictionary<string, Sprite> SpritesByUnitName = new Dictionary<string, Sprite>();

        public static Sprite GetUnitSprite(string unitName)
        {
            if (string.IsNullOrWhiteSpace(unitName))
            {
                return null;
            }

            if (SpritesByUnitName.TryGetValue(unitName, out var sprite))
            {
                return sprite;
            }

            var fullPath = Path.Combine(Application.dataPath, "Art", "icon", "unit", unitName + ".png");
            if (!File.Exists(fullPath))
            {
                SpritesByUnitName[unitName] = null;
                return null;
            }

            var bytes = File.ReadAllBytes(fullPath);
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!texture.LoadImage(bytes))
            {
                SpritesByUnitName[unitName] = null;
                return null;
            }

            texture.filterMode = FilterMode.Bilinear;
            texture.anisoLevel = 4;
            texture.wrapMode = TextureWrapMode.Clamp;
            sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f));
            SpritesByUnitName[unitName] = sprite;
            return sprite;
        }

        public static void ApplyTo(Image image, string unitName)
        {
            if (image == null)
            {
                return;
            }

            var sprite = GetUnitSprite(unitName);
            image.sprite = sprite;
            image.preserveAspect = true;
            image.enabled = sprite != null;
        }
    }
}
