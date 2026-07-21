using System;
using UnityEngine;
using UnityEngine.UI;

namespace ProphecyCentury.UI
{
    /// <summary>
    /// 通用确认对话框组件。
    /// 支持标题、内容文本、确认/取消回调，点击遮罩不关闭。
    /// </summary>
    public sealed class ConfirmDialog : MonoBehaviour
    {
        [SerializeField] private Text titleLabel;
        [SerializeField] private Text contentLabel;
        [SerializeField] private Button confirmButton;
        [SerializeField] private Button cancelButton;

        private Action _onConfirm;
        private Action _onCancel;

        public static ConfirmDialog Create(Transform parent)
        {
            var modal = new GameObject("ConfirmDialog", typeof(RectTransform), typeof(Image));
            modal.transform.SetParent(parent, false);
            var modalRect = modal.GetComponent<RectTransform>();
            modalRect.anchorMin = Vector2.zero;
            modalRect.anchorMax = Vector2.one;
            modalRect.offsetMin = Vector2.zero;
            modalRect.offsetMax = Vector2.zero;
            modal.GetComponent<Image>().color = new Color32(4, 3, 12, 210);
            modal.GetComponent<Image>().raycastTarget = true;

            var panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(modal.transform, false);
            var panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.anchoredPosition = Vector2.zero;
            panelRect.sizeDelta = new Vector2(600f, 340f);
            panel.GetComponent<Image>().color = new Color32(22, 28, 48, 252);

            var titleLabel = CreateText("TitleLabel", panel.transform, "提示", 32, TextAnchor.MiddleCenter,
                new Vector2(0.08f, 0.68f), new Vector2(0.92f, 0.88f), Vector2.zero, Vector2.zero);
            titleLabel.color = new Color32(239, 204, 126, 255);

            var contentLabel = CreateText("ContentLabel", panel.transform, string.Empty, 22, TextAnchor.MiddleCenter,
                new Vector2(0.08f, 0.28f), new Vector2(0.92f, 0.64f), Vector2.zero, Vector2.zero);
            contentLabel.color = new Color32(214, 220, 232, 255);

            var confirmButtonObj = new GameObject("ConfirmButton", typeof(RectTransform), typeof(Image), typeof(Button));
            confirmButtonObj.transform.SetParent(panel.transform, false);
            var confirmRect = confirmButtonObj.GetComponent<RectTransform>();
            confirmRect.anchorMin = new Vector2(0.26f, 0.08f);
            confirmRect.anchorMax = new Vector2(0.48f, 0.24f);
            confirmRect.offsetMin = Vector2.zero;
            confirmRect.offsetMax = Vector2.zero;
            confirmButtonObj.GetComponent<Image>().color = new Color32(187, 129, 42, 255);
            var confirmLabel = CreateText("Label", confirmButtonObj.transform, "确定", 22, TextAnchor.MiddleCenter,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            confirmLabel.color = new Color32(255, 243, 200, 255);
            var confirmButton = confirmButtonObj.GetComponent<Button>();
            confirmButton.targetGraphic = confirmButtonObj.GetComponent<Image>();

            var cancelButtonObj = new GameObject("CancelButton", typeof(RectTransform), typeof(Image), typeof(Button));
            cancelButtonObj.transform.SetParent(panel.transform, false);
            var cancelRect = cancelButtonObj.GetComponent<RectTransform>();
            cancelRect.anchorMin = new Vector2(0.52f, 0.08f);
            cancelRect.anchorMax = new Vector2(0.74f, 0.24f);
            cancelRect.offsetMin = Vector2.zero;
            cancelRect.offsetMax = Vector2.zero;
            cancelButtonObj.GetComponent<Image>().color = new Color32(27, 51, 67, 214);
            var cancelLabel = CreateText("Label", cancelButtonObj.transform, "取消", 22, TextAnchor.MiddleCenter,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            cancelLabel.color = new Color32(183, 220, 217, 205);
            var cancelButton = cancelButtonObj.GetComponent<Button>();
            cancelButton.targetGraphic = cancelButtonObj.GetComponent<Image>();

            var dialog = modal.AddComponent<ConfirmDialog>();
            dialog.titleLabel = titleLabel;
            dialog.contentLabel = contentLabel;
            dialog.confirmButton = confirmButton;
            dialog.cancelButton = cancelButton;

            dialog.confirmButton.onClick.AddListener(dialog.OnConfirmClicked);
            dialog.cancelButton.onClick.AddListener(dialog.OnCancelClicked);

            modal.SetActive(false);
            return dialog;
        }

        public void Show(string title, string content, Action onConfirm, Action onCancel = null)
        {
            if (titleLabel != null) titleLabel.text = title;
            if (contentLabel != null) contentLabel.text = content;
            _onConfirm = onConfirm;
            _onCancel = onCancel;
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        private void OnConfirmClicked()
        {
            RuntimeSfxPlayer.PlayClick();
            var callback = _onConfirm;
            Hide();
            callback?.Invoke();
        }

        private void OnCancelClicked()
        {
            RuntimeSfxPlayer.PlayClick();
            var callback = _onCancel;
            Hide();
            callback?.Invoke();
        }

        private static Text CreateText(string name, Transform parent, string value, int fontSize, TextAnchor alignment,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            var textObject = new GameObject(name, typeof(Text));
            textObject.transform.SetParent(parent, false);
            var rect = textObject.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            var text = textObject.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.color = Color.white;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.text = value;
            return text;
        }

        public static ConfirmDialog FindOrCreate(Transform parent)
        {
            var existing = parent.GetComponentInChildren<ConfirmDialog>(true);
            if (existing != null) return existing;
            return Create(parent);
        }
    }
}