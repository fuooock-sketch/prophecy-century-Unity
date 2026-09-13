using System;
using System.IO;
using ProphecyCentury.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace ProphecyCentury.Editor
{
    public static class TitlePanelPrefabGenerator
    {
        [MenuItem("Prophecy Century/Open Title Panel Prefab")]
        public static void OpenTitlePanelPrefab()
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(RuntimeUiBootstrap.TitlePanelPrefabAssetPath);
            if (asset == null) asset = Generate();
            Selection.activeObject = asset;
            AssetDatabase.OpenAsset(asset);
        }

        public static GameObject Generate()
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(RuntimeUiBootstrap.TitlePanelPrefabAssetPath);
            if (existing != null) return existing;
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Create the title prefab outside Play Mode.");

            Directory.CreateDirectory(Path.GetDirectoryName(RuntimeUiBootstrap.TitlePanelPrefabAssetPath));
            AssetDatabase.Refresh();
            var canvas = new GameObject("TitlePrefabPreview", typeof(RectTransform), typeof(Canvas));
            canvas.GetComponent<RectTransform>().sizeDelta = new Vector2(2560f, 1280f);
            try
            {
                var title = RuntimeUiBootstrap.CreateDefaultTitlePanel(canvas.transform);
                foreach (var logger in title.GetComponentsInChildren<RuntimeButtonClickLogger>(true))
                    UnityEngine.Object.DestroyImmediate(logger);
                foreach (var button in title.GetComponentsInChildren<Button>(true)) button.onClick.RemoveAllListeners();
                var asset = PrefabUtility.SaveAsPrefabAsset(title, RuntimeUiBootstrap.TitlePanelPrefabAssetPath, out var success);
                if (!success) throw new InvalidOperationException("Failed to save title prefab.");
                AssetDatabase.SaveAssets();
                return asset;
            }
            finally { UnityEngine.Object.DestroyImmediate(canvas); }
        }

        // Also verifies that loading and binding preserve edits to the authored layout.
        public static void GenerateAndValidateBatch()
        {
            try
            {
                var prefab = Generate();
                if (prefab.transform.Find("TitleLogo")?.GetComponent<RawImage>().texture == null)
                    throw new InvalidOperationException("Title logo reference was not saved.");
                if (prefab.transform.Find("TitleMenuShade") != null)
                    throw new InvalidOperationException("Removed shade must not be generated.");
                var root = new GameObject("TitleBindingTest", typeof(RectTransform));
                try
                {
                    var panel = RuntimeUiBootstrap.CreateTitlePanel(root.transform);
                    var controller = root.AddComponent<RunSceneController>();
                    var button = panel.transform.Find("QuitGameButton").GetComponent<RectTransform>();
                    button.anchoredPosition = new Vector2(321f, -123f);
                    button.sizeDelta = new Vector2(444f, 66f);
                    var background = panel.GetComponent<Image>();
                    background.color = Color.clear;
                    RuntimeUiBootstrap.BindTitlePanel(root.transform, controller);
                    if (button.anchoredPosition != new Vector2(321f, -123f) || button.sizeDelta != new Vector2(444f, 66f)
                        || background.color != Color.clear || panel.transform.Find("TitleLogo").GetComponent<RawImage>().texture == null)
                        throw new InvalidOperationException("Title binding overwrote authored layout.");
                }
                finally { UnityEngine.Object.DestroyImmediate(root); }
                Debug.Log("Title panel prefab generation and layout validation passed.");
                EditorApplication.Exit(0);
            }
            catch (Exception exception) { Debug.LogException(exception); EditorApplication.Exit(1); }
        }
    }
}
