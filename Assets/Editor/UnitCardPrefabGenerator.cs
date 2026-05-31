using System.IO;
using ProphecyCentury.UI;
using UnityEditor;
using UnityEngine;

namespace ProphecyCentury.Editor
{
    public static class UnitCardPrefabGenerator
    {
        [MenuItem("Prophecy Century/Generate Unit Card Prefab")]
        public static void GenerateUnitCardPrefab()
        {
            GenerateUnitCardPrefab(true);
        }

        public static void GenerateUnitCardPrefab(bool refreshAndSelect)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("Unit card prefab generation is only supported outside Play Mode.");
                return;
            }

            EnsureRaceStyleLibrary();
            Directory.CreateDirectory(Path.GetDirectoryName(UnitCardView.PrefabAssetPath));
            SaveUnitCardPrefab(UnitCardView.PrefabAssetPath, "UnitCard", UnitCardPresentationMode.Grid, new Vector2(221f, 286f), refreshAndSelect);
            SaveUnitCardPrefab(UnitCardView.ShopPrefabAssetPath, "UnitCardShop", UnitCardPresentationMode.Grid, new Vector2(221f, 286f), refreshAndSelect);
            SaveUnitCardPrefab(UnitCardView.HandPrefabAssetPath, "UnitCardHand", UnitCardPresentationMode.Grid, new Vector2(221f, 286f), refreshAndSelect);
            SaveUnitCardPrefab(UnitCardView.BoardPrefabAssetPath, "UnitCardBoard", UnitCardPresentationMode.Board, new Vector2(146f, 146f), refreshAndSelect);

            AssetDatabase.SaveAssets();
            if (refreshAndSelect)
            {
                AssetDatabase.Refresh();
                Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(UnitCardView.PrefabAssetPath);
            }

            Debug.Log("Unit card prefabs generated.");
        }

        private static void SaveUnitCardPrefab(string assetPath, string prefabName, UnitCardPresentationMode mode, Vector2 size, bool overwrite)
        {
            if (!overwrite && AssetDatabase.LoadAssetAtPath<GameObject>(assetPath) != null)
            {
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(assetPath));
            var view = UnitCardView.CreateRuntimeInstance(null);
            view.gameObject.name = prefabName;
            view.BakePrefabDefaultLayout(mode, size);

            try
            {
                PrefabUtility.SaveAsPrefabAsset(view.gameObject, assetPath, out var success);
                if (!success)
                {
                    Debug.LogError($"Failed to save unit card prefab at {assetPath}.");
                }
            }
            finally
            {
                Object.DestroyImmediate(view.gameObject);
            }
        }

        private static void EnsureRaceStyleLibrary()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(UnitCardView.StyleLibraryAssetPath));
            var library = AssetDatabase.LoadAssetAtPath<UnitCardRaceStyleLibrary>(UnitCardView.StyleLibraryAssetPath);
            if (library == null)
            {
                library = ScriptableObject.CreateInstance<UnitCardRaceStyleLibrary>();
                AssetDatabase.CreateAsset(library, UnitCardView.StyleLibraryAssetPath);
            }

            library.SetDefaultSprites(
                LoadFrameSprite("frame_normal.png", true),
                LoadFrameSprite("frame_golden.png", true),
                LoadFrameSprite("图标底_甘地.png"),
                LoadFrameSprite("图标底_甘德.png"),
                LoadFrameSprite("图标底_甘席.png"),
                LoadFrameSprite("图标底_甘格尔.png"));
            EditorUtility.SetDirty(library);
        }

        private static Sprite LoadFrameSprite(string fileName, bool slicedFrame = false)
        {
            var path = $"Assets/Art/icon/frame/{fileName}";
            if (slicedFrame)
            {
                EnsureSlicedSpriteImport(path);
            }

            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null)
            {
                Debug.LogWarning($"Unit card frame sprite not found or not imported as Sprite: {path}");
            }

            return sprite;
        }

        private static void EnsureSlicedSpriteImport(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                return;
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.spriteBorder = new Vector4(18f, 18f, 18f, 18f);
            EditorUtility.SetDirty(importer);
            importer.SaveAndReimport();
        }
    }
}
