using System.Collections.Generic;
using System.Linq;
using ProphecyCentury.Core;
using ProphecyCentury.Data;
using ProphecyCentury.Model;
using ProphecyCentury.Systems;
using UnityEngine;
using UnityEngine.UI;

namespace ProphecyCentury.UI
{
    public sealed class WorldMapView : MonoBehaviour
    {
        private readonly WorldMapSystem _worldMapSystem = new WorldMapSystem();
        private RunSceneController _controller;
        private RectTransform _lineRoot;
        private RectTransform _nodeRoot;
        private Text _titleLabel;
        private Text _detailLabel;
        private Button _nightButton;

        public void Bind(RunSceneController controller)
        {
            _controller = controller;
            EnsureLayout();
        }

        public void Refresh(RunState run, WorldMapDefinition map)
        {
            EnsureLayout();
            gameObject.SetActive(run != null && run.phase == GamePhase.DayExplore);
            if (!gameObject.activeSelf)
            {
                return;
            }

            ClearChildren(_lineRoot);
            ClearChildren(_nodeRoot);
            var current = map?.nodes?.FirstOrDefault(node => node != null && node.id == run.currentNodeId);
            _titleLabel.text = $"第 {run.dayCount} 天  移动力 {run.remainingMovePoints}/{run.maxMovePoints}";
            _detailLabel.text = current == null
                ? "当前位置：未知"
                : $"当前位置：{current.name}  {FormatNodeType(current.type)}";

            if (map?.nodes == null)
            {
                return;
            }

            if (map.connections != null)
            {
                foreach (var connection in map.connections.Where(connection => connection != null))
                {
                    var from = map.nodes.FirstOrDefault(node => node != null && node.id == connection.fromNodeId);
                    var to = map.nodes.FirstOrDefault(node => node != null && node.id == connection.toNodeId);
                    if (from != null && to != null)
                    {
                        CreateConnectionLine(from, to);
                    }
                }
            }

            var available = new HashSet<string>(_worldMapSystem.GetAvailableDestinations(run, map).Select(node => node.id));
            foreach (var node in map.nodes.Where(node => node != null))
            {
                CreateNodeButton(run, node, available.Contains(node.id));
            }
        }

        private void CreateConnectionLine(WorldMapNodeDefinition from, WorldMapNodeDefinition to)
        {
            var lineObject = new GameObject($"{from.id}_to_{to.id}", typeof(Image));
            lineObject.transform.SetParent(_lineRoot, false);
            var rect = lineObject.GetComponent<RectTransform>();
            var size = _lineRoot.rect.size;
            if (size.x <= 1f || size.y <= 1f)
            {
                size = new Vector2(1700f, 720f);
            }

            var start = new Vector2((Mathf.Clamp01(from.x) - 0.5f) * size.x, (Mathf.Clamp01(from.y) - 0.5f) * size.y);
            var end = new Vector2((Mathf.Clamp01(to.x) - 0.5f) * size.x, (Mathf.Clamp01(to.y) - 0.5f) * size.y);
            var delta = end - start;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = start;
            rect.sizeDelta = new Vector2(delta.magnitude, 4f);
            rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
            lineObject.GetComponent<Image>().color = new Color32(88, 98, 126, 180);
        }

        private void CreateNodeButton(RunState run, WorldMapNodeDefinition node, bool available)
        {
            var state = run.worldMapNodes.FirstOrDefault(item => item != null && item.nodeId == node.id);
            var isCurrent = run.currentNodeId == node.id;
            var visible = state != null && state.isVisible;
            var cleared = state != null && state.isCleared;

            var nodeObject = new GameObject(node.id, typeof(Image), typeof(Button));
            nodeObject.transform.SetParent(_nodeRoot, false);
            var rect = nodeObject.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(Mathf.Clamp01(node.x), Mathf.Clamp01(node.y));
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = isCurrent ? new Vector2(132f, 76f) : new Vector2(118f, 68f);

            var image = nodeObject.GetComponent<Image>();
            image.color = ResolveNodeColor(visible, available, cleared, isCurrent, node.type);
            var button = nodeObject.GetComponent<Button>();
            button.interactable = available;
            button.onClick.AddListener(RuntimeSfxPlayer.PlayClick);
            button.onClick.AddListener(() => _controller.SelectWorldMapNode(node.id));

            var label = CreateText(nodeObject.transform, "Label", visible ? $"{FormatNodeType(node.type)}\n{node.name}" : "???", 16, TextAnchor.MiddleCenter);
            label.color = visible ? Color.white : new Color32(160, 164, 176, 255);
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = 10;
            label.resizeTextMaxSize = 16;
        }

        private static Color32 ResolveNodeColor(bool visible, bool available, bool cleared, bool current, string type)
        {
            if (!visible) return new Color32(48, 52, 62, 210);
            if (current) return new Color32(62, 138, 210, 245);
            if (cleared) return new Color32(58, 92, 76, 230);
            if (available) return type == "boss" ? new Color32(156, 64, 76, 245) : new Color32(192, 138, 54, 245);
            return new Color32(70, 76, 94, 225);
        }

        private void EnsureLayout()
        {
            if (_nodeRoot != null)
            {
                return;
            }

            var rect = gameObject.GetComponent<RectTransform>() ?? gameObject.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var image = gameObject.GetComponent<Image>() ?? gameObject.AddComponent<Image>();
            image.color = new Color32(12, 17, 28, 252);

            _titleLabel = CreateText(transform, "WorldMapTitle", string.Empty, 30, TextAnchor.MiddleLeft);
            SetAnchors(_titleLabel.rectTransform, new Vector2(0.04f, 0.9f), new Vector2(0.7f, 0.98f));
            _detailLabel = CreateText(transform, "WorldMapDetail", string.Empty, 20, TextAnchor.MiddleLeft);
            SetAnchors(_detailLabel.rectTransform, new Vector2(0.04f, 0.83f), new Vector2(0.7f, 0.9f));

            var nightObject = new GameObject("EnterNightButton", typeof(Image), typeof(Button));
            nightObject.transform.SetParent(transform, false);
            SetAnchors(nightObject.GetComponent<RectTransform>(), new Vector2(0.82f, 0.9f), new Vector2(0.96f, 0.97f));
            nightObject.GetComponent<Image>().color = new Color32(76, 66, 132, 245);
            _nightButton = nightObject.GetComponent<Button>();
            _nightButton.onClick.AddListener(RuntimeSfxPlayer.PlayClick);
            _nightButton.onClick.AddListener(() => _controller.EnterNightFromWorldMap());
            CreateText(nightObject.transform, "Label", "入夜经营", 20, TextAnchor.MiddleCenter);

            var lineRootObject = new GameObject("WorldMapLines", typeof(RectTransform));
            lineRootObject.transform.SetParent(transform, false);
            _lineRoot = lineRootObject.GetComponent<RectTransform>();
            SetAnchors(_lineRoot, new Vector2(0.08f, 0.1f), new Vector2(0.92f, 0.8f));

            var nodeRootObject = new GameObject("WorldMapNodes", typeof(RectTransform));
            nodeRootObject.transform.SetParent(transform, false);
            _nodeRoot = nodeRootObject.GetComponent<RectTransform>();
            SetAnchors(_nodeRoot, new Vector2(0.08f, 0.1f), new Vector2(0.92f, 0.8f));
            _nodeRoot.SetAsLastSibling();
        }

        private static Text CreateText(Transform parent, string name, string value, int fontSize, TextAnchor alignment)
        {
            var textObject = new GameObject(name, typeof(Text));
            textObject.transform.SetParent(parent, false);
            var rect = textObject.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var text = textObject.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = value;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = Color.white;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            return text;
        }

        private static void SetAnchors(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void ClearChildren(Transform root)
        {
            if (root == null)
            {
                return;
            }

            for (var i = root.childCount - 1; i >= 0; i -= 1)
            {
                Destroy(root.GetChild(i).gameObject);
            }
        }

        private static string FormatNodeType(string type)
        {
            switch (type)
            {
                case "start": return "起点";
                case "battle":
                case "normal_battle": return "普通战";
                case "pressure_battle": return "压力战";
                case "hard_battle": return "高压战";
                case "elite_battle": return "精英";
                case "guard_battle": return "守卫";
                case "boss_guard": return "Boss 前守卫";
                case "boss": return "Boss";
                case "resource": return "资源";
                case "treasure": return "宝物";
                case "event": return "事件";
                case "rest": return "整备";
                default: return "空";
            }
        }
    }
}
