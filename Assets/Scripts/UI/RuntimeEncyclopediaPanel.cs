using System.Collections.Generic;
using System.Linq;
using ProphecyCentury.Core;
using ProphecyCentury.Data;
using UnityEngine;
using UnityEngine.UI;

namespace ProphecyCentury.UI
{
    public sealed class RuntimeEncyclopediaPanel : MonoBehaviour
    {
        [SerializeField] private GameObject _root;
        [SerializeField] private Transform _gridRoot;
        [SerializeField] private Text _countLabel;
        [SerializeField] private Text _detailTitle;
        [SerializeField] private Text _detailMeta;
        [SerializeField] private Text _detailBody;
        [SerializeField] private GameObject _detailRoot;
        [SerializeField] private Transform _detailRelatedRoot;
        [SerializeField] private Image _detailPortrait;
        [SerializeField] private Button _detailCloseButton;
        [SerializeField] private Button _closeButton;
        [SerializeField] private Button _raceFilterButton;
        [SerializeField] private Button _faithFilterButton;
        [SerializeField] private Button _typeFilterButton;
        [SerializeField] private Button _starFilterButton;
        [SerializeField] private Button _resetFilterButton;
        [SerializeField] private UnitCardRaceStyleLibrary _unitCardRaceStyles;

        private string _raceFilter = "all";
        private string _faithFilter = "all";
        private string _typeFilter = "all";
        private string _starFilter = "all";

        private List<string> _raceOptions;
        private List<string> _faithOptions;
        private List<string> _typeOptions;
        private readonly List<string> _starOptions = new List<string> { "all", "1", "2", "3", "4", "5", "6" };
        private bool _buttonsWired;
        private bool _detailButtonsWired;

        private readonly struct SkillGroup
        {
            public SkillGroup(string label, SkillDefinition[] skills)
            {
                Label = label;
                Skills = skills ?? new SkillDefinition[0];
            }

            public string Label { get; }
            public IReadOnlyList<SkillDefinition> Skills { get; }
        }

        private readonly struct RelatedUnitEntry
        {
            public RelatedUnitEntry(UnitDefinition unit, string relation, string sourceLabel)
            {
                Unit = unit;
                Relation = relation;
                SourceLabel = sourceLabel;
            }

            public UnitDefinition Unit { get; }
            public string Relation { get; }
            public string SourceLabel { get; }
        }

        public void Open()
        {
            EnsureBuilt();
            if (_root == null)
            {
                return;
            }

            EnsureLayoutFits();
            EnsureDetailModal();
            _root.SetActive(true);
            if (_detailRoot != null)
            {
                _detailRoot.SetActive(false);
            }

            RefreshFilters();
            RefreshGrid();
        }

        public void Close()
        {
            if (_root != null)
            {
                _root.SetActive(false);
            }

            CloseDetail();
        }

        private void EnsureBuilt()
        {
            if (_root != null)
            {
                WireButtons();
                EnsureLayoutFits();
                EnsureDetailModal();
                return;
            }

            var canvas = GetComponentInParent<Canvas>() ?? FindObjectOfType<Canvas>();
            if (canvas == null)
            {
                return;
            }

            BuildGeneratedLayout(canvas.transform);
        }

        public void BuildGeneratedLayoutForPrefab(Transform parent)
        {
            if (_root != null || parent == null)
            {
                return;
            }

            BuildGeneratedLayout(parent);
        }

        private void BuildGeneratedLayout(Transform parent)
        {
            _root = CreatePanel("RuntimeEncyclopedia", parent, new Color32(0, 0, 0, 170));
            Stretch(_root.GetComponent<RectTransform>());

            var panel = CreatePanel("Panel", _root.transform, new Color32(17, 10, 42, 250));
            SetAnchoredPanel(panel.GetComponent<RectTransform>(), 0.055f, 0.06f, 0.945f, 0.94f);

            CreateText("Title", panel.transform, "图鉴", 54, TextAnchor.MiddleLeft, 44f, 18f, 220f, 72f);
            _countLabel = CreateText("Count", panel.transform, string.Empty, 22, TextAnchor.MiddleLeft, 260f, 30f, 720f, 48f);
            _closeButton = CreateButton("CloseButton", panel.transform, "关闭", 2030f, 24f, 128f, 64f, Close);

            _raceFilterButton = CreateButton("RaceFilter", panel.transform, "种族：全部", 44f, 110f, 250f, 54f, () => CycleFilter(_raceOptions, ref _raceFilter));
            _faithFilterButton = CreateButton("FaithFilter", panel.transform, "信仰：全部", 310f, 110f, 250f, 54f, () => CycleFilter(_faithOptions, ref _faithFilter));
            _typeFilterButton = CreateButton("TypeFilter", panel.transform, "职业：全部", 576f, 110f, 250f, 54f, () => CycleFilter(_typeOptions, ref _typeFilter));
            _starFilterButton = CreateButton("StarFilter", panel.transform, "星级：全部", 842f, 110f, 250f, 54f, () => CycleFilter(_starOptions, ref _starFilter));
            _resetFilterButton = CreateButton("ResetFilter", panel.transform, "重置", 1108f, 110f, 140f, 54f, ResetFilters);

            var gridScroll = CreateScrollArea("GridScroll", panel.transform, 44f, 188f, 1290f, 850f);
            _gridRoot = gridScroll.content;
            var grid = _gridRoot.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(214f, 270f);
            grid.spacing = new Vector2(18f, 18f);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 5;

            var detailPanel = CreatePanel("DetailPanel", panel.transform, new Color32(24, 18, 58, 255));
            SetTopLeft(detailPanel.GetComponent<RectTransform>(), 1370f, 188f, 786f, 850f);
            _detailTitle = CreateText("DetailTitle", detailPanel.transform, "选择一张卡牌", 38, TextAnchor.UpperLeft, 28f, 24f, 730f, 62f);
            _detailMeta = CreateText("DetailMeta", detailPanel.transform, string.Empty, 22, TextAnchor.UpperLeft, 28f, 92f, 730f, 88f);
            _detailBody = CreateText("DetailBody", detailPanel.transform, string.Empty, 22, TextAnchor.UpperLeft, 28f, 190f, 730f, 620f);
            EnsureDetailModal();
            _root.SetActive(false);
            WireButtons();
        }

        private void WireButtons()
        {
            if (_buttonsWired)
            {
                return;
            }

            WireButton(_closeButton, Close);
            WireButton(_raceFilterButton, () => CycleFilter(_raceOptions, ref _raceFilter));
            WireButton(_faithFilterButton, () => CycleFilter(_faithOptions, ref _faithFilter));
            WireButton(_typeFilterButton, () => CycleFilter(_typeOptions, ref _typeFilter));
            WireButton(_starFilterButton, () => CycleFilter(_starOptions, ref _starFilter));
            WireButton(_resetFilterButton, ResetFilters);
            _buttonsWired = true;
        }

        private void WireDetailButtons()
        {
            if (_detailButtonsWired)
            {
                return;
            }

            WireButton(_detailCloseButton, CloseDetail);
            _detailButtonsWired = true;
        }

        private static void WireButton(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button == null)
            {
                return;
            }

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(RuntimeSfxPlayer.PlayClick);
            button.onClick.AddListener(action);
        }

        private void RefreshFilters()
        {
            var units = GetAllUnits();
            _raceOptions = BuildOptions(units.Select(unit => unit.race));
            _faithOptions = BuildOptions(units.Select(unit => unit.faith));
            _typeOptions = BuildOptions(units.Select(unit => unit.typeLabel));

            SetButtonLabel(_raceFilterButton, "种族", _raceFilter);
            SetButtonLabel(_faithFilterButton, "信仰", _faithFilter);
            SetButtonLabel(_typeFilterButton, "职业", _typeFilter);
            SetButtonLabel(_starFilterButton, "星级", _starFilter);
        }

        private void RefreshGrid()
        {
            foreach (Transform child in _gridRoot)
            {
                Destroy(child.gameObject);
            }

            var units = GetFilteredUnits();
            _countLabel.text = $"显示 {units.Count} / {GetAllUnits().Count} 张卡牌，按星级排序";

            foreach (var unit in units)
            {
                CreateUnitCard(unit);
            }

            var rows = Mathf.CeilToInt(units.Count / 5f);
            var contentRect = _gridRoot.GetComponent<RectTransform>();
            contentRect.sizeDelta = new Vector2(contentRect.sizeDelta.x, Mathf.Max(850f, rows * 288f));

            if (units.Count > 0)
            {
                ShowInlineDetail(units[0]);
            }
            else
            {
                ShowInlineDetail(null);
            }
        }

        private List<UnitDefinition> GetFilteredUnits()
        {
            return GetAllUnits()
                .Where(unit => Matches(_raceFilter, unit.race))
                .Where(unit => Matches(_faithFilter, unit.faith))
                .Where(unit => Matches(_typeFilter, unit.typeLabel))
                .Where(unit => _starFilter == "all" || unit.star.ToString() == _starFilter)
                .ToList();
        }

        private static List<UnitDefinition> GetAllUnits()
        {
            return ProphecyGameSession.Instance?.Data?.Units
                .Where(unit => unit != null)
                .OrderBy(unit => unit.star)
                .ThenBy(unit => unit.race)
                .ThenBy(unit => unit.typeLabel)
                .ThenBy(unit => unit.name)
                .ThenBy(unit => unit.id)
                .ToList() ?? new List<UnitDefinition>();
        }

        private void CreateUnitCard(UnitDefinition unit)
        {
            var view = UnitCardView.Instantiate(_gridRoot, UnitCardPresentationMode.Grid);
            var card = view.gameObject;
            card.name = "EncyclopediaCard";
            var rect = card.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(214f, 270f);
            var button = card.GetComponent<Button>() ?? card.AddComponent<Button>();
            button.targetGraphic = view.BackgroundImage != null ? view.BackgroundImage : card.GetComponent<Image>();
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(RuntimeSfxPlayer.PlayClick);
            button.onClick.AddListener(() => OpenDetail(unit));

            view.Bind(unit, null, UnitCardPresentationMode.Encyclopedia, GetUnitCardRaceStyles());
        }

        private UnitCardRaceStyleLibrary GetUnitCardRaceStyles()
        {
            if (_unitCardRaceStyles == null)
            {
                _unitCardRaceStyles = UnitCardRaceStyleLibrary.LoadDefault();
            }

            return _unitCardRaceStyles;
        }

        private void ShowInlineDetail(UnitDefinition unit)
        {
            if (unit == null)
            {
                if (_detailTitle != null)
                {
                    _detailTitle.text = "没有符合条件的卡牌";
                }

                if (_detailMeta != null)
                {
                    _detailMeta.text = string.Empty;
                }

                if (_detailBody != null)
                {
                    _detailBody.text = "请调整筛选条件。";
                }

                return;
            }

            if (_detailTitle != null)
            {
                _detailTitle.text = unit.name;
            }

            if (_detailMeta != null)
            {
                _detailMeta.text = BuildMetaLine("图鉴卡牌", unit);
            }

            if (_detailBody != null)
            {
                _detailBody.text = BuildCompactDetailText(unit);
            }
        }

        private void OpenDetail(UnitDefinition unit)
        {
            if (unit == null)
            {
                return;
            }

            EnsureDetailModal();
            if (_detailRoot == null)
            {
                ShowInlineDetail(unit);
                return;
            }

            _detailRoot.SetActive(true);
            _detailRoot.transform.SetAsLastSibling();
            if (_detailTitle != null)
            {
                _detailTitle.text = unit.name;
            }

            if (_detailMeta != null)
            {
                _detailMeta.text = BuildMetaLine("图鉴卡牌", unit);
            }

            if (_detailPortrait != null)
            {
                RuntimeUnitIconCache.ApplyTo(_detailPortrait, unit.name);
            }

            if (_detailBody != null)
            {
                _detailBody.text = BuildFullDetailText(unit);
            }

            RebuildRelatedCards(unit);
        }

        private void CloseDetail()
        {
            if (_detailRoot != null)
            {
                _detailRoot.SetActive(false);
            }
        }

        private void EnsureLayoutFits()
        {
            if (_root == null)
            {
                return;
            }

            Stretch(_root.GetComponent<RectTransform>());
            var panel = _root.transform.Find("Panel") as RectTransform;
            if (panel != null)
            {
                SetAnchoredPanel(panel, 0.055f, 0.06f, 0.945f, 0.94f);
            }
        }

        private void EnsureDetailModal()
        {
            if (_root == null)
            {
                return;
            }

            if (_detailRoot == null)
            {
                var existing = _root.transform.Find("EncyclopediaDetailModal");
                _detailRoot = existing != null ? existing.gameObject : null;
            }

            if (_detailRoot != null)
            {
                BindExistingDetailReferences();
                Stretch(_detailRoot.GetComponent<RectTransform>());
                WireDetailButtons();
                return;
            }

            _detailRoot = CreatePanel("EncyclopediaDetailModal", _root.transform, new Color32(4, 6, 14, 172));
            Stretch(_detailRoot.GetComponent<RectTransform>());

            var panel = CreatePanel("DetailPanelModal", _detailRoot.transform, new Color32(22, 27, 48, 252));
            var panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(980f, 1060f);
            panelRect.anchoredPosition = Vector2.zero;

            _detailTitle = CreateText("DetailTitle", panel.transform, "卡牌详情", 42, TextAnchor.MiddleLeft, 34f, 24f, 600f, 62f);
            _detailMeta = CreateText("DetailMeta", panel.transform, string.Empty, 21, TextAnchor.UpperLeft, 34f, 88f, 740f, 70f);
            _detailCloseButton = CreateButton("DetailCloseButton", panel.transform, "关闭", 820f, 28f, 120f, 54f, CloseDetail);

            _detailPortrait = CreatePanel("DetailPortrait", panel.transform, new Color32(42, 50, 78, 220)).GetComponent<Image>();
            SetTopLeft(_detailPortrait.rectTransform, 36f, 160f, 172f, 172f);
            _detailPortrait.preserveAspect = true;

            var heroPanel = CreatePanel("HeroSummary", panel.transform, new Color32(32, 38, 62, 238));
            SetTopLeft(heroPanel.GetComponent<RectTransform>(), 230f, 160f, 710f, 172f);
            CreateText("HeroHint", heroPanel.transform, "点击关联卡牌可继续查看详情", 22, TextAnchor.MiddleLeft, 22f, 18f, 660f, 38f);
            CreateText("HeroTags", heroPanel.transform, "基础信息 / 属性 / 技能 / 关联卡牌", 20, TextAnchor.MiddleLeft, 22f, 74f, 660f, 42f);

            var scroll = CreateScrollArea("DetailScroll", panel.transform, 34f, 354f, 906f, 522f);
            _detailBody = CreateText("DetailBody", scroll.content, string.Empty, 20, TextAnchor.UpperLeft, 0f, 0f, 882f, 900f);
            _detailBody.verticalOverflow = VerticalWrapMode.Overflow;
            scroll.content.sizeDelta = new Vector2(906f, 900f);

            var relatedLabel = CreateText("RelatedTitle", panel.transform, "关联卡牌", 24, TextAnchor.MiddleLeft, 34f, 894f, 160f, 34f);
            relatedLabel.color = new Color32(255, 216, 107, 255);
            _detailRelatedRoot = CreateRelatedRoot(panel.transform);
            _detailRoot.SetActive(false);
            _detailButtonsWired = false;
            WireDetailButtons();
        }

        private void BindExistingDetailReferences()
        {
            if (_detailRoot == null)
            {
                return;
            }

            _detailTitle = _detailTitle != null ? _detailTitle : FindDeepChild(_detailRoot.transform, "DetailTitle")?.GetComponent<Text>();
            _detailMeta = _detailMeta != null ? _detailMeta : FindDeepChild(_detailRoot.transform, "DetailMeta")?.GetComponent<Text>();
            _detailBody = _detailBody != null ? _detailBody : FindDeepChild(_detailRoot.transform, "DetailBody")?.GetComponent<Text>();
            _detailPortrait = _detailPortrait != null ? _detailPortrait : FindDeepChild(_detailRoot.transform, "DetailPortrait")?.GetComponent<Image>();
            _detailCloseButton = _detailCloseButton != null ? _detailCloseButton : FindDeepChild(_detailRoot.transform, "DetailCloseButton")?.GetComponent<Button>();
            _detailRelatedRoot = _detailRelatedRoot != null ? _detailRelatedRoot : FindDeepChild(_detailRoot.transform, "RelatedRoot");
            if (_detailCloseButton != null)
            {
                _detailButtonsWired = false;
            }
        }

        private void RebuildRelatedCards(UnitDefinition unit)
        {
            if (_detailRelatedRoot == null)
            {
                return;
            }

            foreach (Transform child in _detailRelatedRoot)
            {
                Destroy(child.gameObject);
            }

            var entries = CollectRelatedUnits(unit);
            if (entries.Count == 0)
            {
                var empty = CreateText("RelatedEmpty", _detailRelatedRoot, "无关联卡牌", 18, TextAnchor.MiddleCenter, 0f, 0f, 420f, 74f);
                empty.color = new Color32(207, 215, 255, 255);
                return;
            }

            foreach (var entry in entries.Take(6))
            {
                CreateRelatedCard(entry);
            }
        }

        private void CreateRelatedCard(RelatedUnitEntry entry)
        {
            var obj = CreatePanel("RelatedCard", _detailRelatedRoot, new Color32(42, 50, 78, 210));
            var layout = obj.AddComponent<LayoutElement>();
            layout.preferredWidth = 284f;
            layout.preferredHeight = 76f;
            var button = obj.AddComponent<Button>();
            button.targetGraphic = obj.GetComponent<Image>();
            button.onClick.AddListener(RuntimeSfxPlayer.PlayClick);
            button.onClick.AddListener(() => OpenDetail(entry.Unit));

            var icon = CreatePanel("Icon", obj.transform, new Color32(255, 255, 255, 255)).GetComponent<Image>();
            SetTopLeft(icon.rectTransform, 8f, 8f, 58f, 58f);
            icon.preserveAspect = true;
            RuntimeUnitIconCache.ApplyTo(icon, entry.Unit.name);

            CreateText("Name", obj.transform, entry.Unit.name, 17, TextAnchor.UpperLeft, 74f, 8f, 196f, 24f);
            var meta = CreateText("Meta", obj.transform, $"{entry.Relation} / {entry.SourceLabel}", 13, TextAnchor.UpperLeft, 74f, 34f, 196f, 18f);
            meta.color = new Color32(255, 216, 107, 255);
            var tags = CreateText("Tags", obj.transform, $"{entry.Unit.race} / {entry.Unit.faith} / {entry.Unit.typeLabel}", 12, TextAnchor.UpperLeft, 74f, 52f, 196f, 18f);
            tags.color = new Color32(159, 183, 216, 255);
        }

        private string BuildCompactDetailText(UnitDefinition unit)
        {
            return string.Join("\n", new[]
            {
                $"生命 {unit.hp}    攻击 {unit.attack}    防御 {unit.defense}",
                $"力量 {unit.power}    速度 {unit.speed}    幸运 {unit.luck}    士气 {unit.morale}",
                "点击卡牌打开完整详情。"
            });
        }

        private string BuildFullDetailText(UnitDefinition unit)
        {
            var tags = unit.tags != null && unit.tags.Length > 0 ? string.Join(" / ", unit.tags) : "无";
            var lines = new List<string>
            {
                "基础信息",
                $"ID：{unit.id}",
                $"种族：{ValueOrNone(unit.race)}    信仰：{ValueOrNone(unit.faith)}    职业：{ValueOrNone(unit.typeLabel)}",
                $"类型：{FormatRawType(unit.type)}    隐藏/衍生：{(unit.hidden ? "是" : "否")}    标签：{tags}",
                string.Empty,
                "基础属性",
                $"生命 {unit.hp}    攻击 {unit.attack}    防御 {unit.defense}    力量 {unit.power}",
                $"速度 {unit.speed}    幸运 {unit.luck}    士气 {unit.morale}    攻速 {unit.attackInterval:0.##}s    射程 {unit.range:0.##}    体型 {unit.size}",
                string.Empty,
                "普通经营技能",
                ValueOrNone(unit.talentText),
                string.Empty,
                "普通战斗技能",
                ValueOrNone(unit.battleText),
                string.Empty,
                "金色经营技能",
                ValueOrNone(unit.goldTalentText),
                string.Empty,
                "金色战斗技能",
                ValueOrNone(unit.goldBattleText)
            };

            var related = CollectRelatedUnits(unit);
            if (related.Count > 0)
            {
                lines.Add(string.Empty);
                lines.Add("关联卡牌");
                lines.AddRange(related.Take(6).Select(entry => $"{entry.Relation}：{entry.Unit.name}（{entry.SourceLabel}）"));
            }

            return string.Join("\n", lines);
        }

        private static string BuildMetaLine(string sourceLabel, UnitDefinition unit)
        {
            return $"{sourceLabel}  |  {new string('*', Mathf.Clamp(unit.star, 0, 6))}  |  {ValueOrNone(unit.race)} / {ValueOrNone(unit.faith)} / {ValueOrNone(unit.typeLabel)}{(unit.hidden ? "  |  隐藏/衍生" : string.Empty)}";
        }

        private static string ValueOrNone(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "无" : value;
        }

        private static string FormatRawType(string type)
        {
            if (type == "range")
            {
                return "远程";
            }

            if (type == "melee")
            {
                return "近战";
            }

            return ValueOrNone(type);
        }

        private List<RelatedUnitEntry> CollectRelatedUnits(UnitDefinition unit)
        {
            var entries = new List<RelatedUnitEntry>();
            var seen = new HashSet<string>();
            if (unit == null)
            {
                return entries;
            }

            foreach (var group in GetSkillGroups(unit))
            {
                foreach (var skill in group.Skills)
                {
                    AddOutgoingRelation(entries, seen, unit, skill.targetUnitId, GetRelationLabel(skill, "targetUnitId", false), group.Label);
                    AddOutgoingRelation(entries, seen, unit, skill.summonUnitId, GetRelationLabel(skill, "summonUnitId", false), group.Label);
                    AddOutgoingRelation(entries, seen, unit, skill.transformUnitId, GetRelationLabel(skill, "transformUnitId", false), group.Label);
                    AddOutgoingRelation(entries, seen, unit, skill.unitId, GetRelationLabel(skill, "unitId", false), group.Label);
                }
            }

            foreach (var other in GetAllUnits())
            {
                if (other == null || other.id == unit.id)
                {
                    continue;
                }

                foreach (var group in GetSkillGroups(other))
                {
                    foreach (var skill in group.Skills)
                    {
                        AddIncomingRelation(entries, seen, unit, other, skill, skill.targetUnitId, "targetUnitId", group.Label);
                        AddIncomingRelation(entries, seen, unit, other, skill, skill.summonUnitId, "summonUnitId", group.Label);
                        AddIncomingRelation(entries, seen, unit, other, skill, skill.transformUnitId, "transformUnitId", group.Label);
                        AddIncomingRelation(entries, seen, unit, other, skill, skill.unitId, "unitId", group.Label);
                    }
                }
            }

            return entries
                .OrderBy(entry => entry.Unit.star)
                .ThenBy(entry => entry.Unit.name)
                .ToList();
        }

        private static void AddOutgoingRelation(List<RelatedUnitEntry> entries, HashSet<string> seen, UnitDefinition source, string unitId, string relation, string sourceLabel)
        {
            if (string.IsNullOrWhiteSpace(unitId) || unitId == source.id)
            {
                return;
            }

            var target = ProphecyGameSession.Instance?.Data?.FindUnit(unitId);
            if (target == null)
            {
                return;
            }

            AddRelation(entries, seen, target, relation, sourceLabel, false);
        }

        private static void AddIncomingRelation(List<RelatedUnitEntry> entries, HashSet<string> seen, UnitDefinition unit, UnitDefinition source, SkillDefinition skill, string targetId, string field, string sourceLabel)
        {
            if (string.IsNullOrWhiteSpace(targetId) || targetId != unit.id)
            {
                return;
            }

            AddRelation(entries, seen, source, GetRelationLabel(skill, field, true), sourceLabel, true);
        }

        private static void AddRelation(List<RelatedUnitEntry> entries, HashSet<string> seen, UnitDefinition unit, string relation, string sourceLabel, bool incoming)
        {
            var key = $"{(incoming ? "in" : "out")}|{unit.id}|{relation}|{sourceLabel}";
            if (!seen.Add(key))
            {
                return;
            }

            entries.Add(new RelatedUnitEntry(unit, relation, sourceLabel));
        }

        private static IEnumerable<SkillGroup> GetSkillGroups(UnitDefinition unit)
        {
            yield return new SkillGroup("普通经营技能", unit.talents);
            yield return new SkillGroup("金色经营技能", unit.goldTalents);
            yield return new SkillGroup("普通战斗技能", unit.battleSkills);
            yield return new SkillGroup("金色战斗技能", unit.goldBattleSkills);
        }

        private static string GetRelationLabel(SkillDefinition skill, string field, bool incoming)
        {
            var kind = skill?.kind ?? string.Empty;
            if (field == "summonUnitId")
            {
                return incoming ? "召唤来源" : "召唤单位";
            }

            if (field == "transformUnitId")
            {
                return incoming ? "变身来源" : "变身结果";
            }

            if (field == "unitId")
            {
                return incoming ? "加入来源" : "加入手牌";
            }

            if (kind.Contains("evolve"))
            {
                return incoming ? "进阶来源" : "进阶目标";
            }

            if (kind.Contains("mount"))
            {
                return incoming ? "骑乘来源" : "骑乘目标";
            }

            if (kind.Contains("sync"))
            {
                return incoming ? "同步来源" : "同步目标";
            }

            return incoming ? "关联来源" : "关联目标";
        }

        private void CycleFilter(IReadOnlyList<string> options, ref string filter)
        {
            if (options == null || options.Count == 0)
            {
                filter = "all";
            }
            else
            {
                var index = 0;
                for (var i = 0; i < options.Count; i += 1)
                {
                    if (options[i] == filter)
                    {
                        index = i;
                        break;
                    }
                }

                filter = options[(index + 1 + options.Count) % options.Count];
            }

            RefreshFilters();
            RefreshGrid();
        }

        private void ResetFilters()
        {
            _raceFilter = "all";
            _faithFilter = "all";
            _typeFilter = "all";
            _starFilter = "all";
            RefreshFilters();
            RefreshGrid();
        }

        private static bool Matches(string filter, string value)
        {
            return filter == "all" || string.Equals(filter, value ?? string.Empty);
        }

        private static List<string> BuildOptions(IEnumerable<string> values)
        {
            return new[] { "all" }
                .Concat(values.Where(value => !string.IsNullOrWhiteSpace(value)).Distinct().OrderBy(value => value))
                .ToList();
        }

        private static void SetButtonLabel(Button button, string label, string value)
        {
            var text = button?.GetComponentInChildren<Text>();
            if (text != null)
            {
                text.text = $"{label}：{(value == "all" ? "全部" : value)}";
            }
        }

        private static GameObject CreatePanel(string name, Transform parent, Color32 color)
        {
            var obj = new GameObject(name, typeof(Image));
            obj.transform.SetParent(parent, false);
            obj.GetComponent<Image>().color = color;
            return obj;
        }

        private static Text CreateText(string name, Transform parent, string value, int fontSize, TextAnchor alignment, float left, float top, float width, float height)
        {
            var obj = new GameObject(name, typeof(Text));
            obj.transform.SetParent(parent, false);
            SetTopLeft(obj.GetComponent<RectTransform>(), left, top, width, height);
            var text = obj.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.fontSize = fontSize;
            text.color = Color.white;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.text = value;
            return text;
        }

        private static Button CreateButton(string name, Transform parent, string label, float left, float top, float width, float height, UnityEngine.Events.UnityAction action)
        {
            var obj = CreatePanel(name, parent, new Color32(70, 104, 180, 255));
            SetTopLeft(obj.GetComponent<RectTransform>(), left, top, width, height);
            var button = obj.AddComponent<Button>();
            button.targetGraphic = obj.GetComponent<Image>();
            button.onClick.AddListener(action);
            CreateText("Label", obj.transform, label, 28, TextAnchor.MiddleCenter, 0f, 0f, width, height);
            return button;
        }

        private static ScrollRect CreateScrollArea(string name, Transform parent, float left, float top, float width, float height)
        {
            var scrollObj = new GameObject(name, typeof(ScrollRect));
            scrollObj.transform.SetParent(parent, false);
            SetTopLeft(scrollObj.GetComponent<RectTransform>(), left, top, width, height);

            var viewport = CreatePanel("Viewport", scrollObj.transform, new Color32(0, 0, 0, 0));
            Stretch(viewport.GetComponent<RectTransform>());
            viewport.AddComponent<RectMask2D>();

            var content = new GameObject("Content", typeof(RectTransform));
            content.transform.SetParent(viewport.transform, false);
            var contentRect = content.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(0f, 1f);
            contentRect.pivot = new Vector2(0f, 1f);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = new Vector2(width, height);

            var scroll = scrollObj.GetComponent<ScrollRect>();
            scroll.viewport = viewport.GetComponent<RectTransform>();
            scroll.content = contentRect;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            return scroll;
        }

        private static Transform CreateRelatedRoot(Transform parent)
        {
            var root = new GameObject("RelatedRoot", typeof(RectTransform), typeof(GridLayoutGroup));
            root.transform.SetParent(parent, false);
            SetTopLeft(root.GetComponent<RectTransform>(), 34f, 936f, 906f, 92f);
            var grid = root.GetComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(284f, 76f);
            grid.spacing = new Vector2(12f, 10f);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 3;
            return root.transform;
        }

        private static Transform FindDeepChild(Transform root, string name)
        {
            if (root == null)
            {
                return null;
            }

            if (root.name == name)
            {
                return root;
            }

            foreach (Transform child in root)
            {
                var match = FindDeepChild(child, name);
                if (match != null)
                {
                    return match;
                }
            }

            return null;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void SetTopLeft(RectTransform rect, float left, float top, float width, float height)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(left, -top);
            rect.sizeDelta = new Vector2(width, height);
        }

        private static void SetAnchoredPanel(RectTransform rect, float minX, float minY, float maxX, float maxY)
        {
            if (rect == null)
            {
                return;
            }

            rect.anchorMin = new Vector2(minX, minY);
            rect.anchorMax = new Vector2(maxX, maxY);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.anchoredPosition = Vector2.zero;
        }
    }
}
