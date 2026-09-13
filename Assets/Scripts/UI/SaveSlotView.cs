using System;
using System.Globalization;
using ProphecyCentury.Systems;
using UnityEngine;
using UnityEngine.UI;

namespace ProphecyCentury.UI
{
    public sealed class SaveSlotView : MonoBehaviour
    {
        private Button _button;
        private Image _background;
        private Text _title;
        private Text _body;
        private Text _badge;
        private Button _deleteButton;

        public static SaveSlotView Create(Transform parent)
        {
            var root = new GameObject("SaveSlot", typeof(Image), typeof(Button), typeof(LayoutElement), typeof(SaveSlotView));
            root.transform.SetParent(parent, false);
            var layout = root.GetComponent<LayoutElement>();
            layout.preferredWidth = 500f;
            layout.preferredHeight = 190f;

            var view = root.GetComponent<SaveSlotView>();
            view._background = root.GetComponent<Image>();
            view._button = root.GetComponent<Button>();
            view._button.targetGraphic = view._background;
            view._title = CreateText("Title", root.transform, 27, TextAnchor.UpperLeft, new Vector2(22f, -16f), new Vector2(-150f, -56f));
            view._body = CreateText("Body", root.transform, 18, TextAnchor.UpperLeft, new Vector2(22f, -62f), new Vector2(-22f, -166f));
            view._badge = CreateText("Badge", root.transform, 18, TextAnchor.MiddleRight, new Vector2(-190f, -17f), new Vector2(-22f, -55f));
            view._deleteButton = CreateButton("DeleteButton", root.transform, "删除", new Vector2(-132f, 18f), new Vector2(-18f, 62f));
            return view;
        }

        public void Bind(SaveSlotInfo info, bool managementMode, Action onSelect, Action onDelete, bool allowLoadValid = false)
        {
            _button.onClick.RemoveAllListeners();
            _deleteButton.onClick.RemoveAllListeners();
            _title.text = $"存档 {info.SlotIndex}";
            _deleteButton.gameObject.SetActive(managementMode && info.State != SaveSlotState.Empty);
            if (onDelete != null) _deleteButton.onClick.AddListener(() => onDelete());

            switch (info.State)
            {
                case SaveSlotState.Empty:
                    _body.text = managementMode ? "空白存档" : "空白存档\n开始新游戏";
                    _badge.text = "空白";
                    _background.color = new Color32(16, 39, 55, 245);
                    _button.interactable = !managementMode;
                    break;
                case SaveSlotState.Valid:
                    var metadata = info.Metadata;
                    _body.text = $"{metadata?.summary ?? "游戏进度"}\n最后保存：{FormatTime(metadata?.lastSavedAtUtc)}\n版本：{metadata?.gameVersion ?? "未知"}";
                    _badge.text = managementMode ? "查看详情" : allowLoadValid ? "继续" : "已有存档";
                    _background.color = managementMode ? new Color32(23, 48, 63, 250) : new Color32(26, 33, 45, 210);
                    _button.interactable = managementMode || allowLoadValid;
                    break;
                default:
                    _body.text = "存档异常\n" + (info.Error ?? "文件无法读取");
                    _badge.text = "异常";
                    _background.color = new Color32(76, 30, 35, 235);
                    _button.interactable = false;
                    break;
            }

            if (_button.interactable && onSelect != null) _button.onClick.AddListener(() => onSelect());
            var colors = _button.colors;
            colors.disabledColor = new Color32(90, 90, 96, 150);
            _button.colors = colors;
        }

        private static string FormatTime(string value)
        {
            return DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var time)
                ? time.ToLocalTime().ToString("yyyy-MM-dd HH:mm")
                : "未知";
        }

        private static Text CreateText(string name, Transform parent, int size, TextAnchor alignment, Vector2 offsetMin, Vector2 offsetMax)
        {
            var obj = new GameObject(name, typeof(Text));
            obj.transform.SetParent(parent, false);
            var rect = obj.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(offsetMin.x, -offsetMax.y);
            rect.offsetMax = new Vector2(offsetMax.x, -offsetMin.y);
            var text = obj.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size;
            text.alignment = alignment;
            text.color = new Color32(225, 230, 235, 255);
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 14;
            text.resizeTextMaxSize = size;
            return text;
        }

        private static Button CreateButton(string name, Transform parent, string label, Vector2 min, Vector2 max)
        {
            var obj = new GameObject(name, typeof(Image), typeof(Button));
            obj.transform.SetParent(parent, false);
            var rect = obj.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.offsetMin = min;
            rect.offsetMax = max;
            obj.GetComponent<Image>().color = new Color32(118, 48, 44, 255);
            var text = CreateText("Label", obj.transform, 18, TextAnchor.MiddleCenter, Vector2.zero, Vector2.zero);
            text.rectTransform.offsetMin = Vector2.zero;
            text.rectTransform.offsetMax = Vector2.zero;
            text.text = label;
            return obj.GetComponent<Button>();
        }
    }
}
