using System.Collections.Generic;
using System.Linq;
using ProphecyCentury.Core;
using ProphecyCentury.Data;
using ProphecyCentury.Model;
using ProphecyCentury.Systems;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ProphecyCentury.UI
{
    public sealed class WorldMapView : MonoBehaviour, IDragHandler, IScrollHandler
    {
        private const float WorldMapUiScale = 3f;
        private const float NodeWidth = 128f * WorldMapUiScale;
        private const float NodeHeight = 72f * WorldMapUiScale;
        private const float LayerSpacing = 260f * WorldMapUiScale;
        private const float RowSpacing = 122f * WorldMapUiScale;

        private readonly WorldMapSystem _worldMapSystem = new WorldMapSystem();
        private readonly Dictionary<string, Vector2> _nodePositions = new Dictionary<string, Vector2>();
        private RunSceneController _controller;
        private RectTransform _viewport;
        private RectTransform _contentRoot;
        private RectTransform _lineRoot;
        private RectTransform _nodeRoot;
        private Text _titleLabel;
        private Text _detailLabel;
        private Button _nightButton;
        private Vector2 _contentSize = new Vector2(1700f, 900f);

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
            _nodePositions.Clear();

            var current = map?.nodes?.FirstOrDefault(node => node != null && node.id == run.currentNodeId);
            _titleLabel.text = $"第 {run.dayCount} 天  移动力 {run.remainingMovePoints}/{run.maxMovePoints}";
            _detailLabel.text = current == null
                ? "当前位置：未知"
                : $"当前位置：{current.name}  {FormatNodeType(current.type)}";

            if (map?.nodes == null || map.nodes.Length == 0)
            {
                return;
            }

            RecalculateContentLayout(map);
            CenterContentOnNode(current?.id);
            if (map.connections != null)
            {
                foreach (var connection in map.connections.Where(connection => connection != null))
                {
                    CreateConnectionLine(connection);
                }
            }

            var available = new HashSet<string>(_worldMapSystem.GetAvailableDestinations(run, map).Select(node => node.id));
            foreach (var node in map.nodes.Where(node => node != null).OrderBy(node => node.layer).ThenByDescending(node => node.y).ThenBy(node => node.id))
            {
                CreateNodeButton(run, node, available.Contains(node.id));
            }

            ClampContentPosition();
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (_contentRoot == null || eventData == null)
            {
                return;
            }

            _contentRoot.anchoredPosition += eventData.delta;
            ClampContentPosition();
        }

        public void OnScroll(PointerEventData eventData)
        {
            if (_contentRoot == null || eventData == null)
            {
                return;
            }

            _contentRoot.anchoredPosition += new Vector2(eventData.scrollDelta.x * 44f * WorldMapUiScale, eventData.scrollDelta.y * 44f * WorldMapUiScale);
            ClampContentPosition();
        }

        private void RecalculateContentLayout(WorldMapDefinition map)
        {
            var viewportSize = ResolveViewportSize();
            var nodes = map.nodes.Where(node => node != null).ToList();
            var layers = nodes
                .Select(node => node.layer)
                .Distinct()
                .OrderBy(layer => layer)
                .ToList();
            if (layers.Count == 0)
            {
                layers.Add(0);
            }

            var maxLayerCount = layers.Max(layer => nodes.Count(node => node.layer == layer));
            _contentSize = new Vector2(
                Mathf.Max(viewportSize.x, 220f * WorldMapUiScale + Mathf.Max(1, layers.Count - 1) * LayerSpacing + NodeWidth),
                Mathf.Max(viewportSize.y, 220f * WorldMapUiScale + Mathf.Max(1, maxLayerCount - 1) * RowSpacing + NodeHeight));

            _contentRoot.sizeDelta = _contentSize;
            _lineRoot.sizeDelta = _contentSize;
            _nodeRoot.sizeDelta = _contentSize;

            var layerIndexByValue = layers
                .Select((layer, index) => new { layer, index })
                .ToDictionary(item => item.layer, item => item.index);
            foreach (var layer in layers)
            {
                var layerNodes = nodes
                    .Where(node => node.layer == layer)
                    .OrderByDescending(node => node.y)
                    .ThenBy(node => node.id)
                    .ToList();
                var columnIndex = layerIndexByValue[layer];
                var x = -_contentSize.x * 0.5f + 110f * WorldMapUiScale + columnIndex * LayerSpacing;
                var firstY = (layerNodes.Count - 1) * RowSpacing * 0.5f;
                for (var i = 0; i < layerNodes.Count; i += 1)
                {
                    _nodePositions[layerNodes[i].id] = new Vector2(x, firstY - i * RowSpacing);
                }
            }
        }

        private void CreateConnectionLine(WorldMapConnectionDefinition connection)
        {
            if (!_nodePositions.TryGetValue(connection.fromNodeId, out var start)
                || !_nodePositions.TryGetValue(connection.toNodeId, out var end))
            {
                return;
            }

            var lineObject = new GameObject($"{connection.fromNodeId}_to_{connection.toNodeId}", typeof(Image));
            lineObject.transform.SetParent(_lineRoot, false);
            var rect = lineObject.GetComponent<RectTransform>();
            var delta = end - start;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = start;
            rect.sizeDelta = new Vector2(delta.magnitude, 4f * WorldMapUiScale);
            rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
            lineObject.GetComponent<Image>().color = new Color32(104, 118, 150, 180);
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
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = _nodePositions.TryGetValue(node.id, out var position) ? position : Vector2.zero;
            rect.sizeDelta = isCurrent ? new Vector2(NodeWidth + 18f * WorldMapUiScale, NodeHeight + 8f * WorldMapUiScale) : new Vector2(NodeWidth, NodeHeight);

            var image = nodeObject.GetComponent<Image>();
            image.color = ResolveNodeColor(visible, available, cleared, isCurrent, node.type);
            var button = nodeObject.GetComponent<Button>();
            button.interactable = available;
            button.onClick.AddListener(RuntimeSfxPlayer.PlayClick);
            button.onClick.AddListener(() => _controller.SelectWorldMapNode(node.id));

            var label = CreateText(nodeObject.transform, "Label", visible ? $"{FormatNodeType(node.type)}\n{node.name}" : "???", ScaleFont(16), TextAnchor.MiddleCenter);
            label.color = visible ? Color.white : new Color32(160, 164, 176, 255);
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = ScaleFont(10);
            label.resizeTextMaxSize = ScaleFont(16);
        }

        private static Color32 ResolveNodeColor(bool visible, bool available, bool cleared, bool current, string type)
        {
            if (!visible) return new Color32(48, 52, 62, 220);
            if (current) return new Color32(62, 138, 210, 250);
            if (cleared) return new Color32(58, 92, 76, 235);
            if (available) return WorldMapSystem.IsBossNodeType(type) ? new Color32(156, 64, 76, 250) : new Color32(192, 138, 54, 250);
            return new Color32(70, 76, 94, 230);
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
            image.raycastTarget = true;

            _titleLabel = CreateText(transform, "WorldMapTitle", string.Empty, ScaleFont(30), TextAnchor.MiddleLeft);
            SetAnchors(_titleLabel.rectTransform, new Vector2(0.04f, 0.9f), new Vector2(0.54f, 0.98f));
            _detailLabel = CreateText(transform, "WorldMapDetail", string.Empty, ScaleFont(20), TextAnchor.MiddleLeft);
            SetAnchors(_detailLabel.rectTransform, new Vector2(0.04f, 0.8f), new Vector2(0.96f, 0.9f));

            var nightObject = new GameObject("EnterNightButton", typeof(Image), typeof(Button));
            nightObject.transform.SetParent(transform, false);
            SetAnchors(nightObject.GetComponent<RectTransform>(), new Vector2(0.54f, 0.9f), new Vector2(0.96f, 0.98f));
            nightObject.GetComponent<Image>().color = new Color32(76, 66, 132, 245);
            _nightButton = nightObject.GetComponent<Button>();
            _nightButton.onClick.AddListener(RuntimeSfxPlayer.PlayClick);
            _nightButton.onClick.AddListener(() => _controller.EnterNightFromWorldMap());
            CreateText(nightObject.transform, "Label", "入夜经营", ScaleFont(20), TextAnchor.MiddleCenter);

            var viewportObject = new GameObject("WorldMapViewport", typeof(Image), typeof(RectMask2D));
            viewportObject.transform.SetParent(transform, false);
            _viewport = viewportObject.GetComponent<RectTransform>();
            SetAnchors(_viewport, new Vector2(0.04f, 0.04f), new Vector2(0.96f, 0.78f));
            var viewportImage = viewportObject.GetComponent<Image>();
            viewportImage.color = new Color32(8, 12, 22, 90);
            viewportImage.raycastTarget = true;

            var contentObject = new GameObject("WorldMapContent", typeof(RectTransform));
            contentObject.transform.SetParent(_viewport, false);
            _contentRoot = contentObject.GetComponent<RectTransform>();
            _contentRoot.anchorMin = _contentRoot.anchorMax = new Vector2(0.5f, 0.5f);
            _contentRoot.pivot = new Vector2(0.5f, 0.5f);
            _contentRoot.anchoredPosition = Vector2.zero;
            _contentRoot.sizeDelta = _contentSize;

            var lineRootObject = new GameObject("WorldMapLines", typeof(RectTransform));
            lineRootObject.transform.SetParent(_contentRoot, false);
            _lineRoot = lineRootObject.GetComponent<RectTransform>();
            CenterFill(_lineRoot);

            var nodeRootObject = new GameObject("WorldMapNodes", typeof(RectTransform));
            nodeRootObject.transform.SetParent(_contentRoot, false);
            _nodeRoot = nodeRootObject.GetComponent<RectTransform>();
            CenterFill(_nodeRoot);
            _nodeRoot.SetAsLastSibling();
        }

        private Vector2 ResolveViewportSize()
        {
            if (_viewport == null)
            {
                return new Vector2(1500f, 760f);
            }

            var size = _viewport.rect.size;
            return size.sqrMagnitude > 1f ? size : new Vector2(1500f, 760f);
        }

        private void ClampContentPosition()
        {
            if (_contentRoot == null || _viewport == null)
            {
                return;
            }

            var viewportSize = ResolveViewportSize();
            var maxX = Mathf.Max(0f, (_contentSize.x - viewportSize.x) * 0.5f);
            var maxY = Mathf.Max(0f, (_contentSize.y - viewportSize.y) * 0.5f);
            var position = _contentRoot.anchoredPosition;
            _contentRoot.anchoredPosition = new Vector2(
                Mathf.Clamp(position.x, -maxX, maxX),
                Mathf.Clamp(position.y, -maxY, maxY));
        }

        private static int ScaleFont(int fontSize)
        {
            return Mathf.RoundToInt(fontSize * WorldMapUiScale);
        }

        private void CenterContentOnNode(string nodeId)
        {
            if (_contentRoot == null || string.IsNullOrWhiteSpace(nodeId) || !_nodePositions.TryGetValue(nodeId, out var position))
            {
                return;
            }

            _contentRoot.anchoredPosition = -position;
            ClampContentPosition();
        }

        private static void CenterFill(RectTransform rect)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
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
                case "boss_guard": return "Boss 前哨";
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
