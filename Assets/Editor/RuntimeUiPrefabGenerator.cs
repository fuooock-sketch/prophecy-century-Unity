using System.IO;
using ProphecyCentury.UI;
using UnityEditor;
using UnityEngine;

namespace ProphecyCentury.Editor
{
    public static class RuntimeUiPrefabGenerator
    {
        [MenuItem("Prophecy Century/Generate Runtime UI Prefab")]
        public static void GenerateRuntimeUiPrefab()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("Runtime UI prefab generation is only supported outside Play Mode.");
                return;
            }

            UnitCardPrefabGenerator.GenerateUnitCardPrefab(false);
            Directory.CreateDirectory(Path.GetDirectoryName(RuntimeUiBootstrap.RuntimeUiPrefabAssetPath));

            var runtimeUi = RuntimeUiBootstrap.CreateGeneratedUi(false);
            runtimeUi.name = "RuntimeCanvas";
            var encyclopedia = runtimeUi.GetComponent<RuntimeEncyclopediaPanel>();
            if (encyclopedia != null)
            {
                encyclopedia.BuildGeneratedLayoutForPrefab(runtimeUi.transform);
            }

            try
            {
                PrefabUtility.SaveAsPrefabAsset(runtimeUi, RuntimeUiBootstrap.RuntimeUiPrefabAssetPath, out var success);
                if (!success)
                {
                    Debug.LogError($"Failed to save runtime UI prefab at {RuntimeUiBootstrap.RuntimeUiPrefabAssetPath}.");
                    return;
                }

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(RuntimeUiBootstrap.RuntimeUiPrefabAssetPath);
                Debug.Log($"Runtime UI prefab generated: {RuntimeUiBootstrap.RuntimeUiPrefabAssetPath}");
            }
            finally
            {
                Object.DestroyImmediate(runtimeUi);
            }
        }
    }
}
