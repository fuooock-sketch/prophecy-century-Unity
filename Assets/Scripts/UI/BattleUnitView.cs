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

        public Image BackingImage
        {
            get
            {
                EnsureReferences();
                return backingImage;
            }
        }

        public Image UnitIconImage
        {
            get
            {
                EnsureReferences();
                return unitIconImage;
            }
        }

        public Text LabelText
        {
            get
            {
                EnsureReferences();
                return labelText;
            }
        }

        public Image HealthFillImage
        {
            get
            {
                EnsureReferences();
                return healthFillImage;
            }
        }

        public void Bind(string iconName, string label, int star, bool playerSide)
        {
            EnsureReferences();
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
                labelText.text = $"{new string('*', Mathf.Clamp(star, 0, 6))}\n{label}";
                labelText.color = Color.white;
            }
        }

        public void SetHealth(int hp, int maxHp)
        {
            EnsureReferences();
            var amount = Mathf.Clamp01(Mathf.Max(0, hp) / (float)Mathf.Max(1, maxHp));
            if (healthFillImage != null)
            {
                healthFillImage.fillAmount = amount;
                var rect = healthFillImage.rectTransform;
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = new Vector2(amount, 1f);
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
            }
        }

        private void EnsureReferences()
        {
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
        }
    }
}
