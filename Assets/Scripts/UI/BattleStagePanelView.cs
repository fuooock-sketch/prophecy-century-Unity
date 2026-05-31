using UnityEngine;
using UnityEngine.UI;

namespace ProphecyCentury.UI
{
    public sealed class BattleStagePanelView : MonoBehaviour
    {
        [SerializeField] private Transform playerBattleRoot;
        [SerializeField] private Transform enemyBattleRoot;
        [SerializeField] private Text statusLabel;
        [SerializeField] private Text logLabel;
        [SerializeField] private Image progressFill;

        public Transform PlayerBattleRoot
        {
            get
            {
                EnsureReferences();
                return playerBattleRoot;
            }
        }

        public Transform EnemyBattleRoot
        {
            get
            {
                EnsureReferences();
                return enemyBattleRoot;
            }
        }

        public Text StatusLabel
        {
            get
            {
                EnsureReferences();
                return statusLabel;
            }
        }

        public Text LogLabel
        {
            get
            {
                EnsureReferences();
                return logLabel;
            }
        }

        public Image ProgressFill
        {
            get
            {
                EnsureReferences();
                return progressFill;
            }
        }

        public void Bind(RunSceneController controller)
        {
            if (controller == null)
            {
                return;
            }

            EnsureReferences();
            controller.BindBattleStagePanel(gameObject, playerBattleRoot, enemyBattleRoot, statusLabel, logLabel, progressFill);
        }

        private void EnsureReferences()
        {
            if (playerBattleRoot == null)
            {
                playerBattleRoot = FindDeepChild(transform, "PlayerBattleRoot");
            }

            if (enemyBattleRoot == null)
            {
                enemyBattleRoot = FindDeepChild(transform, "EnemyBattleRoot");
            }

            if (statusLabel == null)
            {
                var status = FindDeepChild(transform, "BattleStageStatus");
                statusLabel = status != null ? status.GetComponent<Text>() : null;
            }

            if (logLabel == null)
            {
                var log = FindDeepChild(transform, "BattleStageLog");
                logLabel = log != null ? log.GetComponent<Text>() : null;
            }

            if (progressFill == null)
            {
                var fill = FindDeepChild(transform, "BattleProgressFill");
                progressFill = fill != null ? fill.GetComponent<Image>() : null;
            }
        }

        private static Transform FindDeepChild(Transform root, string childName)
        {
            if (root == null)
            {
                return null;
            }

            if (root.name == childName)
            {
                return root;
            }

            foreach (Transform child in root)
            {
                var match = FindDeepChild(child, childName);
                if (match != null)
                {
                    return match;
                }
            }

            return null;
        }
    }
}
