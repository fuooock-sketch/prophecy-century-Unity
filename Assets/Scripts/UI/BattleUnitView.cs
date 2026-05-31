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

        private static readonly Color32 CountBarBackColor = new Color32(16, 44, 45, 230);
        private static readonly Color32 CountBarFillColor = new Color32(78, 214, 157, 255);
        private static readonly Color32 CountBarBackEnemyColor = new Color32(50, 18, 24, 230);
        private static readonly Color32 CountBarFillEnemyColor = new Color32(212, 38, 48, 255);

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
                labelText.text = $"{new string('*', Mathf.Clamp(star, 0, 6))}\n{label}";
                labelText.color = playerSide ? Color.white : new Color32(255, 180, 180, 255);
                labelText.alignment = TextAnchor.UpperCenter;
            }
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

            var healthLabel = GetOrCreateBarLabel(32);
            if (healthLabel != null)
            {
                healthLabel.text = healthText ?? $"{Mathf.Max(0, hp)}/{Mathf.Max(1, maxHp)}";
            }
        }

        public void SetCount(int count, int maxCount)
        {
            if (!EnsureReferences())
            {
                return;
            }

            var state = BattleUnitBarPresenter.CalculateCount(count, maxCount);
            var healthBar = transform.Find("HealthBar")?.GetComponent<Image>();
            if (healthBar != null)
            {
                healthBar.color = _playerSide ? CountBarBackColor : CountBarBackEnemyColor;
            }

            if (healthFillImage != null)
            {
                healthFillImage.color = _playerSide ? CountBarFillColor : CountBarFillEnemyColor;
            }

            SetBarFill(state.Amount);

            var healthLabel = GetOrCreateBarLabel(16);
            if (healthLabel != null)
            {
                healthLabel.text = state.Text;
                healthLabel.color = _playerSide ? Color.white : new Color32(255, 210, 210, 255);
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
            healthLabel.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
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
