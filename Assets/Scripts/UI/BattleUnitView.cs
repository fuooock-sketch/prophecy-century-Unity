using UnityEngine;
using UnityEngine.UI;

namespace ProphecyCentury.UI
{
    public sealed class BattleUnitView : MonoBehaviour
    {
        [SerializeField] private Image backingImage;
        [SerializeField] private Image unitIconImage;
        [SerializeField] private Text labelText;
        [SerializeField] private Image healthFillImage;

        private static readonly Color32 HealthBarBackColor = new Color32(24, 28, 28, 230);
        private static readonly Color32 HealthBarFillColor = new Color32(86, 218, 156, 255);
        private static readonly Color32 CountBadgePlayerColor = new Color32(24, 140, 168, 245);
        private static readonly Color32 CountBadgeEnemyColor = new Color32(168, 86, 34, 245);
        private static readonly Color32 CountBadgeBorderColor = new Color32(24, 16, 12, 230);

        private bool _playerSide;
        private const int PlayerLabelFontSize = 32;
        private const int EnemyLabelFontSize = 28;

        public Image BackingImage
        {
            get
            {
                if (!EnsureReferences())
                {
                    return null;
                }

                return backingImage;
            }
        }

        public Image UnitIconImage
        {
            get
            {
                if (!EnsureReferences())
                {
                    return null;
                }

                return unitIconImage;
            }
        }

        public Text LabelText
        {
            get
            {
                if (!EnsureReferences())
                {
                    return null;
                }

                return labelText;
            }
        }

        public Image HealthFillImage
        {
            get
            {
                if (!EnsureReferences())
                {
                    return null;
                }

                return healthFillImage;
            }
        }

        public void Bind(string iconName, string label, int star, bool playerSide)
        {
            if (!EnsureReferences())
            {
                return;
            }

            _playerSide = playerSide;

            if (backingImage != null)
            {
                backingImage.raycastTarget = false;
            }

            if (unitIconImage != null)
            {
                RuntimeUnitIconCache.ApplyTo(unitIconImage, iconName);
                unitIconImage.rectTransform.localScale = playerSide ? Vector3.one : new Vector3(-1f, 1f, 1f);
            }

            if (labelText != null)
            {
                var fontSize = playerSide ? PlayerLabelFontSize : EnemyLabelFontSize;
                labelText.fontSize = fontSize;
                labelText.text = string.Empty;
                labelText.color = playerSide ? Color.white : new Color32(255, 180, 180, 255);
                labelText.alignment = TextAnchor.UpperCenter;
                labelText.raycastTarget = false;
            }

            ConfigureStatusUi();
        }

        public void SetHealth(int hp, int maxHp)
        {
            SetHealth(hp, maxHp, null);
        }

        public void SetHealth(int hp, int maxHp, string healthText)
        {
            if (!EnsureReferences())
            {
                return;
            }

            var amount = Mathf.Clamp01(Mathf.Max(0, hp) / (float)Mathf.Max(1, maxHp));
            SetBarFill(amount);

            var healthLabel = GetOrCreateBarLabel(12);
            if (healthLabel != null)
            {
                healthLabel.text = string.IsNullOrWhiteSpace(healthText) ? string.Empty : healthText;
            }
        }

        public void SetCount(int count, int maxCount)
        {
            if (!EnsureReferences())
            {
                return;
            }

            ConfigureStatusUi();

            var countLabel = GetOrCreateCountBadgeLabel();
            if (countLabel != null)
            {
                countLabel.text = Mathf.Max(0, count).ToString();
                countLabel.color = Color.white;
            }
        }

        private void SetBarFill(float amount)
        {
            if (healthFillImage == null)
            {
                return;
            }

            var safeAmount = Mathf.Clamp01(amount);
            healthFillImage.fillAmount = safeAmount;
            var rect = healthFillImage.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = new Vector2(safeAmount, 1f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private Text GetOrCreateBarLabel(int fontSize)
        {
            var healthLabel = transform.Find("HealthBar/Label")?.GetComponent<Text>();
            if (healthLabel != null)
            {
                healthLabel.fontSize = fontSize;
                healthLabel.transform.SetAsLastSibling();
                return healthLabel;
            }

            var healthBar = transform.Find("HealthBar");
            if (healthBar == null)
            {
                return null;
            }

            var labelObject = new GameObject("Label", typeof(Text));
            labelObject.transform.SetParent(healthBar, false);
            var labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
            healthLabel = labelObject.GetComponent<Text>();
            healthLabel.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            healthLabel.fontSize = fontSize;
            healthLabel.alignment = TextAnchor.MiddleCenter;
            healthLabel.color = Color.white;
            healthLabel.raycastTarget = false;

            if (labelObject.GetComponent<Outline>() == null)
            {
                var outline = labelObject.AddComponent<Outline>();
                outline.effectColor = new Color32(0, 0, 0, 220);
                outline.effectDistance = new Vector2(1.5f, -1.5f);
                outline.useGraphicAlpha = true;
            }

            labelObject.transform.SetAsLastSibling();
            return healthLabel;
        }

        private Text GetOrCreateCountBadgeLabel()
        {
            var badge = transform.Find("CountBadge");
            if (badge == null)
            {
                var badgeObject = new GameObject("CountBadge", typeof(Image));
                badgeObject.transform.SetParent(transform, false);
                badge = badgeObject.transform;
            }

            var badgeRect = badge.GetComponent<RectTransform>();
            badgeRect.anchorMin = new Vector2(0.5f, 0f);
            badgeRect.anchorMax = new Vector2(0.5f, 0f);
            badgeRect.pivot = new Vector2(0.5f, 0.5f);
            badgeRect.anchoredPosition = new Vector2(0f, 54f);
            badgeRect.sizeDelta = new Vector2(58f, 28f);

            var badgeImage = badge.GetComponent<Image>() ?? badge.gameObject.AddComponent<Image>();
            badgeImage.color = CountBadgeBorderColor;
            badgeImage.raycastTarget = false;

            var fill = badge.Find("Fill");
            if (fill == null)
            {
                var fillObject = new GameObject("Fill", typeof(Image));
                fillObject.transform.SetParent(badge, false);
                fill = fillObject.transform;
            }

            var fillRect = fill.GetComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = new Vector2(4f, 3f);
            fillRect.offsetMax = new Vector2(-4f, -3f);
            var fillImage = fill.GetComponent<Image>() ?? fill.gameObject.AddComponent<Image>();
            fillImage.color = _playerSide ? CountBadgePlayerColor : CountBadgeEnemyColor;
            fillImage.raycastTarget = false;

            var label = badge.Find("Label")?.GetComponent<Text>();
            if (label == null)
            {
                var labelObject = new GameObject("Label", typeof(Text));
                labelObject.transform.SetParent(badge, false);
                var labelRect = labelObject.GetComponent<RectTransform>();
                labelRect.anchorMin = Vector2.zero;
                labelRect.anchorMax = Vector2.one;
                labelRect.offsetMin = Vector2.zero;
                labelRect.offsetMax = Vector2.zero;
                label = labelObject.GetComponent<Text>();
                label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                label.alignment = TextAnchor.MiddleCenter;
                label.raycastTarget = false;

                var outline = labelObject.AddComponent<Outline>();
                outline.effectColor = new Color32(0, 0, 0, 220);
                outline.effectDistance = new Vector2(1.2f, -1.2f);
                outline.useGraphicAlpha = true;
            }

            label.fontSize = 18;
            label.fontStyle = FontStyle.Bold;
            badge.SetAsLastSibling();
            return label;
        }

        private void ConfigureStatusUi()
        {
            var healthBar = transform.Find("HealthBar")?.GetComponent<Image>();
            if (healthBar != null)
            {
                var rect = healthBar.rectTransform;
                rect.anchorMin = new Vector2(0.5f, 0f);
                rect.anchorMax = new Vector2(0.5f, 0f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = new Vector2(0f, 74f);
                rect.sizeDelta = new Vector2(108f, 10f);
                healthBar.color = HealthBarBackColor;
                healthBar.raycastTarget = false;
            }

            if (healthFillImage != null)
            {
                healthFillImage.color = HealthBarFillColor;
                healthFillImage.raycastTarget = false;
            }
        }

        private bool EnsureReferences()
        {
            if (this == null)
            {
                return false;
            }

            if (backingImage == null)
            {
                backingImage = GetComponent<Image>();
            }

            if (unitIconImage == null)
            {
                var icon = transform.Find("Icon");
                if (icon != null)
                {
                    unitIconImage = icon.GetComponent<Image>();
                }
            }

            if (labelText == null)
            {
                var label = transform.Find("Label");
                if (label != null)
                {
                    labelText = label.GetComponent<Text>();
                }
            }

            if (healthFillImage == null)
            {
                var fill = transform.Find("HealthBar/Fill");
                if (fill != null)
                {
                    healthFillImage = fill.GetComponent<Image>();
                }
            }

            return true;
        }
    }
}
