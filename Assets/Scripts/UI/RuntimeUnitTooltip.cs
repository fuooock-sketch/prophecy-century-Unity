using System.Text;
using ProphecyCentury.Core;
using ProphecyCentury.Data;
using ProphecyCentury.Model;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ProphecyCentury.UI
{
    public sealed class RuntimeUnitTooltip : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerMoveHandler
    {
        private const float PanelWidth = 668f;
        private static readonly Color32 NormalPanelColor = new Color32(31, 43, 74, 248);
        private static readonly Color32 NormalFrameColor = new Color32(230, 238, 232, 255);
        private static readonly Color32 GoldenFrameColor = new Color32(255, 210, 0, 255);
        private static readonly Color32 TextColor = new Color32(218, 224, 232, 255);
        private static readonly Color32 BuffTextColor = new Color32(0, 255, 115, 255);
        private static readonly Color32 StarTextColor = new Color32(255, 217, 64, 255);

        private static GameObject _panel;
        private static RectTransform _panelRect;
        private static Image _panelImage;
        private static Outline _panelOutline;
        private static Text _titleLabel;
        private static Text _starsLabel;
        private static Text _statsLeftLabel;
        private static Text _statsRightLabel;
        private static Text _talentLabel;
        private static Text _battleLabel;
        private static Text _tagsLabel;
        private static bool _suppressed;

        public UnitCardState Unit { get; set; }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (_suppressed)
            {
                Hide();
                return;
            }

            Show(Unit, eventData);
        }

        public void OnPointerMove(PointerEventData eventData)
        {
            if (_suppressed)
            {
                Hide();
                return;
            }

            Move(eventData);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            Hide();
        }

        private void OnDisable()
        {
            Hide();
        }

        private void OnDestroy()
        {
            Hide();
        }

        public static void HideCurrent()
        {
            Hide();
        }

        public static void SetSuppressed(bool suppressed)
        {
            _suppressed = suppressed;
            if (suppressed)
            {
                Hide();
            }
        }

        private static void Show(UnitCardState unit, PointerEventData eventData)
        {
            if (_suppressed || unit == null)
            {
                return;
            }

            EnsurePanel();
            if (_panel == null)
            {
                return;
            }

            Bind(unit);
            _panel.SetActive(true);
            LayoutRebuilder.ForceRebuildLayoutImmediate(_panelRect);
            Move(eventData);
        }

        private static void Move(PointerEventData eventData)
        {
            if (_panelRect == null || !_panel.activeSelf || eventData == null)
            {
                return;
            }

            var position = eventData.position + new Vector2(24f, -24f);
            var width = _panelRect.rect.width;
            var height = _panelRect.rect.height;
            position.x = Mathf.Min(position.x, Screen.width - width - 12f);
            position.y = Mathf.Max(position.y, height + 12f);
            _panelRect.position = position;
        }

        private static void Hide()
        {
            if (_panel != null)
            {
                _panel.SetActive(false);
            }
        }

        private static void EnsurePanel()
        {
            if (_panel != null)
            {
                return;
            }

            var canvas = FindObjectOfType<Canvas>();
            if (canvas == null)
            {
                return;
            }

            _panel = new GameObject("RuntimeUnitTooltipPanel", typeof(Image), typeof(Outline), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter), typeof(LayoutElement));
            _panel.transform.SetParent(canvas.transform, false);
            _panelRect = _panel.GetComponent<RectTransform>();
            _panelRect.pivot = new Vector2(0f, 1f);
            _panelRect.sizeDelta = new Vector2(PanelWidth, 0f);

            _panelImage = _panel.GetComponent<Image>();
            _panelImage.color = NormalPanelColor;
            _panelImage.raycastTarget = false;

            _panelOutline = _panel.GetComponent<Outline>();
            _panelOutline.effectColor = NormalFrameColor;
            _panelOutline.effectDistance = new Vector2(3f, -3f);

            var panelLayoutElement = _panel.GetComponent<LayoutElement>();
            panelLayoutElement.preferredWidth = PanelWidth;

            var layout = _panel.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(36, 36, 28, 28);
            layout.spacing = 18f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var fitter = _panel.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var header = CreateRow("Header", _panel.transform, 0f);
            _titleLabel = CreateText("Title", header.transform, 52, TextAnchor.MiddleLeft, TextColor);
            _starsLabel = CreateText("Stars", header.transform, 42, TextAnchor.MiddleRight, StarTextColor);
            SetFlexible(_titleLabel.gameObject, 1f, 320f);
            SetFlexible(_starsLabel.gameObject, 0f, 264f);

            var statsRow = CreateRow("Stats", _panel.transform, 24f);
            _statsLeftLabel = CreateText("StatsLeft", statsRow.transform, 34, TextAnchor.UpperLeft, TextColor);
            _statsRightLabel = CreateText("StatsRight", statsRow.transform, 34, TextAnchor.UpperLeft, TextColor);
            SetFlexible(_statsLeftLabel.gameObject, 1f, 276f);
            SetFlexible(_statsRightLabel.gameObject, 1f, 276f);

            _talentLabel = CreateText("Talent", _panel.transform, 34, TextAnchor.UpperLeft, TextColor);
            _battleLabel = CreateText("Battle", _panel.transform, 34, TextAnchor.UpperLeft, TextColor);
            _tagsLabel = CreateText("Tags", _panel.transform, 50, TextAnchor.MiddleLeft, TextColor);
            _tagsLabel.horizontalOverflow = HorizontalWrapMode.Overflow;

            _panel.SetActive(false);
        }

        private static void Bind(UnitCardState unit)
        {
            var data = ProphecyGameSession.Instance?.Data?.FindUnit(unit.unitId);
            if (data == null)
            {
                SetPanelStyle(unit.isGolden);
                SetText(_titleLabel, Escape(unit.name));
                SetText(_starsLabel, BuildStars(unit.star));
                SetText(_statsLeftLabel, string.Empty);
                SetText(_statsRightLabel, string.Empty);
                SetSkillText(_talentLabel, null, null);
                SetSkillText(_battleLabel, null, null);
                SetText(_tagsLabel, unit.isGolden ? "金色" : string.Empty);
                return;
            }

            SetPanelStyle(unit.isGolden);
            SetText(_titleLabel, $"{(unit.isGolden ? "<color=#FFD200>" : string.Empty)}{Escape(data.name)}{(unit.isGolden ? "</color>" : string.Empty)}");
            SetText(_starsLabel, BuildStars(data.star));
            SetText(_statsLeftLabel, BuildStatsLeft(data, unit));
            SetText(_statsRightLabel, BuildStatsRight(data, unit));

            var talent = unit.isGolden && !string.IsNullOrWhiteSpace(data.goldTalentText)
                ? data.goldTalentText
                : data.talentText;
            var battle = unit.isGolden && !string.IsNullOrWhiteSpace(data.goldBattleText)
                ? data.goldBattleText
                : data.battleText;

            SetSkillText(_talentLabel, "经营技能：", talent);
            SetSkillText(_battleLabel, "战斗技能：", battle);
            SetText(_tagsLabel, BuildTags(data));
        }

        private static string BuildStatsLeft(UnitDefinition data, UnitCardState unit)
        {
            var builder = new StringBuilder();
            builder.AppendLine(StatLine("攻击", data.attack, unit.shopBuffAttack + unit.roundTempAttack));
            builder.AppendLine(StatLine("防御", data.defense, unit.shopBuffDefense));
            builder.AppendLine(StatLine("力量", data.power, unit.shopBuffPower + unit.roundTempPower));
            builder.Append(StatLine("生命", data.hp, unit.shopBuffHp));
            return builder.ToString();
        }

        private static string BuildStatsRight(UnitDefinition data, UnitCardState unit)
        {
            var builder = new StringBuilder();
            builder.AppendLine(StatLine("射程", FormatRange(data.range), null));
            builder.AppendLine(StatLine("速度", data.speed, unit.shopBuffSpeed));
            builder.AppendLine(StatLine("士气", data.morale, unit.shopBuffMorale + unit.roundTempMorale));
            builder.Append(StatLine("幸运", data.luck, unit.shopBuffLuck));
            return builder.ToString();
        }

        private static string StatLine(string label, int baseValue, int bonus)
        {
            return StatLine(label, (baseValue + bonus).ToString(), bonus == 0 ? null : $"({FormatSigned(bonus)})");
        }

        private static string StatLine(string label, string value, string bonus)
        {
            if (string.IsNullOrWhiteSpace(bonus))
            {
                return $"{label}：{value}";
            }

            return $"{label}：<color=#{ColorUtility.ToHtmlStringRGB(BuffTextColor)}>{value} {bonus}</color>";
        }

        private static string BuildTags(UnitDefinition data)
        {
            var race = Escape(data.race);
            var faith = Escape(data.faith);
            var type = Escape(string.IsNullOrWhiteSpace(data.typeLabel) ? data.type : data.typeLabel);
            return $"{race}    {type}    {faith}";
        }

        private static string BuildStars(int star)
        {
            return new string('★', Mathf.Clamp(star, 0, 8));
        }

        private static string FormatRange(float range)
        {
            if (Mathf.Approximately(range, Mathf.Round(range)))
            {
                return Mathf.RoundToInt(range).ToString();
            }

            return range.ToString("0.#");
        }

        private static string FormatSigned(int value)
        {
            return value > 0 ? $"+{value}" : value.ToString();
        }

        private static void SetSkillText(Text label, string heading, string body)
        {
            var visible = !string.IsNullOrWhiteSpace(body);
            if (label != null)
            {
                label.gameObject.SetActive(visible);
            }

            if (!visible)
            {
                SetText(label, string.Empty);
                return;
            }

            SetText(label, $"<color=#DCE4EE>{heading}</color>\n<color=#C8D0DD>{Escape(body)}</color>");
        }

        private static void SetPanelStyle(bool golden)
        {
            if (_panelImage != null)
            {
                _panelImage.color = NormalPanelColor;
            }

            if (_panelOutline != null)
            {
                _panelOutline.effectColor = golden ? GoldenFrameColor : NormalFrameColor;
            }

            if (_titleLabel != null)
            {
                _titleLabel.color = golden ? GoldenFrameColor : TextColor;
            }
        }

        private static GameObject CreateRow(string name, Transform parent, float spacing)
        {
            var row = new GameObject(name, typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            row.transform.SetParent(parent, false);
            var layout = row.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = spacing;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            return row;
        }

        private static Text CreateText(string name, Transform parent, int fontSize, TextAnchor alignment, Color color)
        {
            var obj = new GameObject(name, typeof(Text), typeof(ContentSizeFitter));
            obj.transform.SetParent(parent, false);
            var text = obj.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            text.supportRichText = true;
            text.lineSpacing = 1.05f;

            var fitter = obj.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return text;
        }

        private static void SetFlexible(GameObject obj, float flexibleWidth, float preferredWidth)
        {
            var element = obj.GetComponent<LayoutElement>() ?? obj.AddComponent<LayoutElement>();
            element.flexibleWidth = flexibleWidth;
            element.preferredWidth = preferredWidth;
        }

        private static void SetText(Text label, string text)
        {
            if (label != null)
            {
                label.text = text ?? string.Empty;
            }
        }

        private static string Escape(string value)
        {
            return string.IsNullOrEmpty(value)
                ? string.Empty
                : value.Replace("<", "＜").Replace(">", "＞");
        }
    }
}
