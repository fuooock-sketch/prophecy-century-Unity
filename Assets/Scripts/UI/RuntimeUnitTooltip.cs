using ProphecyCentury.Core;
using ProphecyCentury.Data;
using ProphecyCentury.Model;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ProphecyCentury.UI
{
    public sealed class RuntimeUnitTooltip : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerMoveHandler
    {
        // Panel dimensions (Figma: 560px compact design)
        private const float PanelWidth = 560f;

        // Font sizes
        private const int FontSizeTitle = 42;
        private const int FontSizeStars = 28;
        private const int FontSizeStat = 28;
        private const int FontSizeTag = 28;
        private const int FontSizeSkillHeading = 28;
        private const int FontSizeSkillBody = 28;
        private const int FontSizeIconGlyph = 28;

        // Normal (frost) palette
        private static readonly Color32 NormalPanelColor = new Color32(20, 30, 52, 250);
        private static readonly Color32 NormalFrameColor = new Color32(147, 182, 216, 133);
        private static readonly Color32 NormalAccentColor = new Color32(88, 216, 236, 255);
        private static readonly Color32 NormalTextPrimary = new Color32(228, 235, 246, 255);
        private static readonly Color32 NormalTextBody = new Color32(218, 229, 240, 255);
        private static readonly Color32 NormalStatLabelColor = new Color32(135, 157, 184, 255);
        private static readonly Color32 BonusTextColor = new Color32(84, 238, 143, 255);
        private static readonly Color32 StarTextColor = new Color32(255, 217, 64, 255);
        private static readonly Color32 TagFillColor = new Color32(12, 21, 38, 184);
        private static readonly Color32 TagBorderColor = new Color32(88, 216, 236, 140);
        private static readonly Color32 TagTextColor = new Color32(204, 218, 232, 255);
        private static readonly Color32 DividerColor = new Color32(92, 121, 158, 102);
        private static readonly Color32 DividerLightColor = new Color32(92, 121, 158, 92);
        private static readonly Color32 StatCardFill = new Color32(18, 30, 52, 199);
        private static readonly Color32 StatCardBorder = new Color32(72, 95, 130, 128);
        private static readonly Color32 StatIconBackplate = new Color32(13, 34, 48, 235);
        private static readonly Color32 StatIconBorder = new Color32(88, 216, 236, 122);

        // Skill block palette
        private static readonly Color32 MgmtBlockFill = new Color32(16, 47, 50, 173);
        private static readonly Color32 MgmtBlockBorder = new Color32(69, 221, 177, 97);
        private static readonly Color32 MgmtHeadingColor = new Color32(84, 238, 202, 255);
        private static readonly Color32 MgmtBodyColor = new Color32(205, 226, 222, 255);
        private static readonly Color32 BattleBlockFill = new Color32(55, 34, 28, 173);
        private static readonly Color32 BattleBlockBorder = new Color32(255, 173, 91, 107);
        private static readonly Color32 BattleHeadingColor = new Color32(255, 187, 103, 255);
        private static readonly Color32 BattleBodyColor = new Color32(235, 219, 204, 255);

        // Golden palette
        private static readonly Color32 GoldenFrameColor = new Color32(255, 203, 64, 230);
        private static readonly Color32 GoldenAccentColor = new Color32(255, 203, 64, 255);
        private static readonly Color32 GoldenTextPrimary = new Color32(255, 217, 91, 255);
        private static readonly Color32 GoldenTagBorder = new Color32(255, 203, 64, 140);
        private static readonly Color32 GoldenStatIconBackplate = new Color32(55, 42, 24, 235);
        private static readonly Color32 GoldenStatIconBorder = new Color32(255, 203, 64, 158);

        // Static panel references
        private static GameObject _panel;
        private static RectTransform _panelRect;
        private static CanvasGroup _panelGroup;
        private static Image _panelImage;
        private static Outline _panelOutline;
        private static Image _topRule;
        private static Text _titleLabel;
        private static Text _starsLabel;
        private static Transform _tagRow;
        private static GameObject _dividerA;
        private static Transform _statGrid;
        private static GameObject _dividerB;
        private static GameObject _mgmtBlock;
        private static Text _mgmtHeading;
        private static Text _mgmtBody;
        private static GameObject _battleBlock;
        private static Text _battleHeading;
        private static Text _battleBody;
        private static bool _suppressed;

        // Animation state
        private const float HoverShowDelay = 0.18f;
        private const float FadeInDuration = 0.15f;
        private const float FadeOutDuration = 0.10f;
        private static float _hoverStartTime;
        private static float _fadeInStartTime;
        private static float _hideStartTime;
        private static bool _hoverPending;
        private static bool _fadingIn;
        private static bool _fadingOut;
        private static RuntimeUnitTooltip _runner;
        private static RectTransform _sourceRect;

        public UnitCardState Unit { get; set; }

        private void Awake()
        {
            if (_runner == null)
            {
                _runner = this;
            }
        }

        private void Update()
        {
            if (_panel == null)
            {
                return;
            }

            // Hover delay → start fade-in
            if (_hoverPending && Time.unscaledTime - _hoverStartTime >= HoverShowDelay)
            {
                _hoverPending = false;
                _fadingIn = true;
                _fadingOut = false;
                _fadeInStartTime = Time.unscaledTime;
            }

            // Fade-in animation
            if (_fadingIn)
            {
                var elapsed = Time.unscaledTime - _fadeInStartTime;
                var t = Mathf.Clamp01(elapsed / FadeInDuration);
                _panelGroup.alpha = Mathf.Lerp(0f, 1f, t);
                _panelGroup.blocksRaycasts = t > 0.6f;
                _panelRect.localScale = Vector3.one * Mathf.Lerp(0.92f, 1f, Mathf.SmoothStep(0f, 1f, t));
                if (t >= 1f)
                {
                    _fadingIn = false;
                }
            }

            // Fade-out animation
            if (_fadingOut)
            {
                var elapsed = Time.unscaledTime - _hideStartTime;
                var t = Mathf.Clamp01(elapsed / FadeOutDuration);
                _panelGroup.alpha = Mathf.Lerp(1f, 0f, t);
                _panelGroup.blocksRaycasts = false;
                if (t >= 1f)
                {
                    _panel.SetActive(false);
                    _fadingOut = false;
                }
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (_suppressed)
            {
                Hide();
                return;
            }

            _sourceRect = (RectTransform)transform;
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
            _panelGroup.alpha = 0f;
            _panelGroup.blocksRaycasts = false;
            _panelRect.localScale = Vector3.one * 0.92f;
            _hoverStartTime = Time.unscaledTime;
            _hoverPending = true;
            _fadingIn = false;
            _fadingOut = false;
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(_panelRect);
            Move(eventData);
        }

        private static void Move(PointerEventData eventData)
        {
            if (_panelRect == null || !_panel.activeSelf || eventData == null)
            {
                return;
            }

            // Use actual panel dimensions; if layout hasn't resolved yet, use safe estimates.
            float pw = _panelRect.rect.width > 10f ? _panelRect.rect.width : PanelWidth;
            float rawH = _panelRect.rect.height;
            float ph = rawH > 10f ? rawH : 500f;

            float availH = Mathf.Max(1f, Screen.height - TooltipPositioner.ScreenMargin * 2f);
            float s = Mathf.Min(1f, availH / Mathf.Max(1f, ph));
            _panelRect.localScale = new Vector3(s, s, 1f);
            float visualW = pw * s;
            float visualH = ph * s;

            // Try adjacent placement first
            if (_sourceRect != null && TryPlacePanelAdjacent(_sourceRect, visualW, visualH, out float ox, out float oy))
            {
                _panelRect.position = new Vector2(ox, oy);
                return;
            }

            // Fallback: pointer-relative with screen clamp
            float fx = Mathf.Clamp(eventData.position.x + TooltipPositioner.PointerOffsetX,
                TooltipPositioner.ScreenMargin, Screen.width - visualW - TooltipPositioner.ScreenMargin);
            float fy = Mathf.Clamp(eventData.position.y + TooltipPositioner.PointerOffsetY,
                visualH + TooltipPositioner.ScreenMargin, Screen.height - TooltipPositioner.ScreenMargin);
            _panelRect.position = new Vector2(fx, fy);
        }

        private static bool TryPlacePanelAdjacent(RectTransform srcRT, float panelW, float panelH, out float outX, out float outY)
        {
            outX = 0f;
            outY = 0f;

            Vector3[] sc = new Vector3[4];
            srcRT.GetWorldCorners(sc);
            float srcL = sc[0].x;
            float srcB = sc[0].y;
            float srcR = sc[2].x;
            float srcT = sc[1].y;
            Rect srcRect = new Rect(srcL, srcB, srcR - srcL, srcT - srcB);

            float m = TooltipPositioner.ScreenMargin;
            float sw = Screen.width;
            float sh = Screen.height;

            // Candidates: right, below, left, above (pivot is top-left (0,1))
            (float left, float top)[] cs = new (float, float)[]
            {
                (srcR, srcT),
                (srcL, srcB),
                (srcL - panelW, srcT),
                (srcL, srcT + panelH),
            };

            foreach (var ct in cs)
            {
                float pl = ct.left;
                float pr = ct.left + panelW;
                float pt = ct.top;
                float pb = ct.top - panelH;

                if (pl < m || pr > sw - m || pb < m || pt > sh - m)
                    continue;

                Rect prRect = new Rect(pl, pb, panelW, panelH);
                if (!srcRect.Overlaps(prRect))
                {
                    outX = pl;
                    outY = pt;
                    return true;
                }
            }

            return false;
        }

        private static void Hide()
        {
            if (_panel != null && _panel.activeSelf && !_fadingOut && _panelGroup.alpha > 0.1f)
            {
                _fadingOut = true;
                _hideStartTime = Time.unscaledTime;
                _hoverPending = false;
                _fadingIn = false;
            }
            else if (_panel != null && !_fadingOut)
            {
                _panel.SetActive(false);
                _hoverPending = false;
                _fadingIn = false;
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

            // --- Panel root ---
            _panel = new GameObject("RuntimeUnitTooltipPanel", typeof(Image), typeof(Outline), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter), typeof(LayoutElement), typeof(CanvasGroup));
            _panel.transform.SetParent(canvas.transform, false);
            _panelRect = _panel.GetComponent<RectTransform>();
            _panelGroup = _panel.GetComponent<CanvasGroup>();
            _panelGroup.blocksRaycasts = false;
            _panelGroup.interactable = false;
            _panelGroup.ignoreParentGroups = true;
            _panelRect.pivot = new Vector2(0f, 1f);
            _panelRect.sizeDelta = new Vector2(PanelWidth, 0f);

            _panelImage = _panel.GetComponent<Image>();
            _panelImage.color = NormalPanelColor;
            _panelImage.raycastTarget = false;

            _panelOutline = _panel.GetComponent<Outline>();
            _panelOutline.effectColor = NormalFrameColor;
            _panelOutline.effectDistance = new Vector2(2f, -2f);

            var panelLayoutElement = _panel.GetComponent<LayoutElement>();
            panelLayoutElement.preferredWidth = PanelWidth;

            var layout = _panel.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(24, 24, 24, 24);
            layout.spacing = 14f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var fitter = _panel.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // --- Top rule (accent bar, outside layout) ---
            _topRule = CreateImage("TopRule", _panel.transform, NormalAccentColor);
            var topRuleRect = _topRule.rectTransform;
            topRuleRect.pivot = new Vector2(0f, 1f);
            topRuleRect.anchorMin = new Vector2(0f, 1f);
            topRuleRect.anchorMax = new Vector2(1f, 1f);
            topRuleRect.sizeDelta = new Vector2(0f, 4f);
            topRuleRect.anchoredPosition = Vector2.zero;
            var topRuleLayout = _topRule.gameObject.AddComponent<LayoutElement>();
            topRuleLayout.ignoreLayout = true;

            // --- Header: title + stars ---
            var header = CreateRow("Header", _panel.transform, 0f);

            _titleLabel = CreateText("Title", header.transform, FontSizeTitle, TextAnchor.MiddleLeft);
            _titleLabel.fontStyle = FontStyle.Bold;
            _titleLabel.raycastTarget = false;
            _titleLabel.supportRichText = true;
            _titleLabel.lineSpacing = 1f;

            _starsLabel = CreateText("Stars", header.transform, FontSizeStars, TextAnchor.MiddleRight);
            _starsLabel.fontStyle = FontStyle.Bold;
            _starsLabel.raycastTarget = false;
            _starsLabel.supportRichText = true;

            SetFlexible(_titleLabel.gameObject, 1f, 330f);
            SetFlexible(_starsLabel.gameObject, 0f, 136f);

            // --- Tag chips row ---
            _tagRow = CreateRow("TagRow", _panel.transform, 12f).transform;

            // --- Divider A ---
            _dividerA = CreateDivider(_panel.transform);

            // --- Stat grid (4x2) ---
            _statGrid = CreateStatGrid(_panel.transform);

            // --- Divider B ---
            _dividerB = CreateDivider(_panel.transform);

            // --- Management skill block ---
            _mgmtBlock = CreateSkillBlock("MgmtSkill", _panel.transform,
                MgmtBlockFill, MgmtBlockBorder, out _mgmtHeading, out _mgmtBody);
            _mgmtHeading.color = MgmtHeadingColor;
            _mgmtHeading.text = "经营技能";
            _mgmtBody.color = MgmtBodyColor;

            // --- Battle skill block ---
            _battleBlock = CreateSkillBlock("BattleSkill", _panel.transform,
                BattleBlockFill, BattleBlockBorder, out _battleHeading, out _battleBody);
            _battleHeading.color = BattleHeadingColor;
            _battleHeading.text = "战斗技能";
            _battleBody.color = BattleBodyColor;

            _panel.SetActive(false);
        }

        private static void Bind(UnitCardState unit)
        {
            var data = ProphecyGameSession.Instance?.Data?.FindUnit(unit.unitId);
            var golden = unit.isGolden;

            SetPanelStyle(golden);

            if (data == null)
            {
                SetText(_titleLabel, $"{(golden ? "<color=#FFD95B>金色  " : "")}{Escape(unit.name)}");
                SetText(_starsLabel, BuildStars(unit.star));
                ClearTagChips();
                SetDividerActive(_dividerA, false);
                ClearStatGrid();
                SetDividerActive(_dividerB, false);
                _mgmtBlock.SetActive(false);
                _battleBlock.SetActive(false);
                return;
            }

            // Title
            var titleText = golden
                ? $"<color=#FFD95B>金色  {Escape(data.name)}</color>"
                : Escape(data.name);
            SetText(_titleLabel, titleText);

            // Stars
            SetText(_starsLabel, BuildStars(data.star));

            // Tags
            BuildTagChips(data, golden);

            // Divider A
            SetDividerActive(_dividerA, true);

            // Stats
            BuildStatGrid(data, unit, golden);

            // Divider B
            SetDividerActive(_dividerB, true);

            // Skills
            var talent = golden && !string.IsNullOrWhiteSpace(data.goldTalentText)
                ? data.goldTalentText
                : data.talentText;
            var battle = golden && !string.IsNullOrWhiteSpace(data.goldBattleText)
                ? data.goldBattleText
                : data.battleText;

            BindSkillBlock(_mgmtBlock, _mgmtBody, talent);
            BindSkillBlock(_battleBlock, _battleBody, battle);
        }

        // ---------------------------------------------------------------
        //  Panel Style
        // ---------------------------------------------------------------

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

            if (_topRule != null)
            {
                _topRule.color = golden ? GoldenAccentColor : NormalAccentColor;
            }

            if (_titleLabel != null)
            {
                _titleLabel.color = golden ? GoldenTextPrimary : NormalTextPrimary;
            }
        }

        // ---------------------------------------------------------------
        //  Tag Chips
        // ---------------------------------------------------------------

        private static void ClearTagChips()
        {
            if (_tagRow == null)
            {
                return;
            }

            for (int i = _tagRow.childCount - 1; i >= 0; i--)
            {
                Object.Destroy(_tagRow.GetChild(i).gameObject);
            }
        }

        private static void BuildTagChips(UnitDefinition data, bool golden)
        {
            ClearTagChips();

            var borderColor = golden ? GoldenTagBorder : TagBorderColor;

            AddTagChip(data.race, borderColor);
            AddTagChip(string.IsNullOrWhiteSpace(data.typeLabel) ? data.type : data.typeLabel, borderColor);
            AddTagChip(data.faith, borderColor);
        }

        private static void AddTagChip(string text, Color32 borderColor)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            var chip = new GameObject("TagChip", typeof(Image), typeof(Outline), typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter), typeof(LayoutElement));
            chip.transform.SetParent(_tagRow, false);

            var chipImg = chip.GetComponent<Image>();
            chipImg.color = TagFillColor;
            chipImg.raycastTarget = false;

            var chipOutline = chip.GetComponent<Outline>();
            chipOutline.effectColor = borderColor;
            chipOutline.effectDistance = new Vector2(1f, -1f);

            var chipLayout = chip.GetComponent<HorizontalLayoutGroup>();
            chipLayout.padding = new RectOffset(10, 10, 5, 5);
            chipLayout.spacing = 0f;
            chipLayout.childControlWidth = true;
            chipLayout.childControlHeight = true;
            chipLayout.childForceExpandWidth = false;
            chipLayout.childForceExpandHeight = false;

            var chipFitter = chip.GetComponent<ContentSizeFitter>();
            chipFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            chipFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var chipElement = chip.GetComponent<LayoutElement>();

            var label = CreateText("Label", chip.transform, FontSizeTag, TextAnchor.MiddleCenter);
            label.color = TagTextColor;
            label.fontStyle = FontStyle.Normal;
            label.alignment = TextAnchor.MiddleCenter;
            label.raycastTarget = false;

            var labelFitter = label.GetComponent<ContentSizeFitter>();
            labelFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            labelFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            SetText(label, Escape(text));
        }

        // ---------------------------------------------------------------
        //  Stat Grid (4 columns x 2 rows, 8 stats from Figma)
        // ---------------------------------------------------------------

        private static Transform CreateStatGrid(Transform parent)
        {
            var gridRoot = new GameObject("StatGrid", typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            gridRoot.transform.SetParent(parent, false);
            var vLayout = gridRoot.GetComponent<VerticalLayoutGroup>();
            vLayout.spacing = 10f;
            vLayout.childControlWidth = true;
            vLayout.childControlHeight = true;
            vLayout.childForceExpandWidth = true;
            vLayout.childForceExpandHeight = false;

            var gridFitter = gridRoot.GetComponent<ContentSizeFitter>();
            gridFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            gridFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // Pre-create two rows
            CreateStatRow("StatRow0", gridRoot.transform, 10f);
            CreateStatRow("StatRow1", gridRoot.transform, 10f);

            return gridRoot.transform;
        }

        private static GameObject CreateStatRow(string name, Transform parent, float spacing)
        {
            var row = new GameObject(name, typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter));
            row.transform.SetParent(parent, false);
            var layout = row.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = spacing;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var rowFitter = row.GetComponent<ContentSizeFitter>();
            rowFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            rowFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            return row;
        }

        private enum StatIconType
        {
            Count,    // three dots
            Attack,   // sword
            Defense,  // shield
            Damage,   // diamond
            Speed,    // chevrons
            Morale,   // flag on pole
            Luck,     // clover glyph
            Range     // target rings
        }

        private static void ClearStatGrid()
        {
            if (_statGrid == null)
            {
                return;
            }

            for (int i = 0; i < _statGrid.childCount; i++)
            {
                var row = _statGrid.GetChild(i);
                for (int j = row.childCount - 1; j >= 0; j--)
                {
                    Object.Destroy(row.GetChild(j).gameObject);
                }
            }
        }

        private static void BuildStatGrid(UnitDefinition data, UnitCardState unit, bool golden)
        {
            ClearStatGrid();

            var baseCount = ResolveBaseCount(data, unit);
            var morale = data.morale + unit.shopBuffMorale + unit.roundTempMorale;
            var luck = data.luck + unit.shopBuffLuck;

            // Row 0: 数量, 攻击, 防御, 伤害
            var row0 = _statGrid.GetChild(0);
            AddStatCard(row0, "数量", baseCount.ToString(), ResolveCountBonus(data, unit), StatIconType.Count, golden);
            AddStatCard(row0, "攻击", (data.attack + unit.shopBuffAttack + unit.roundTempAttack + unit.boardAuraAttack).ToString(),
                FormatBonus(unit.shopBuffAttack + unit.roundTempAttack + unit.boardAuraAttack), StatIconType.Attack, golden);
            AddStatCard(row0, "防御", (data.defense + unit.shopBuffDefense).ToString(),
                FormatBonus(unit.shopBuffDefense), StatIconType.Defense, golden);
            AddStatCardWide(row0, "伤害", $"{Mathf.Max(1, data.damageMin)}-{Mathf.Max(Mathf.Max(1, data.damageMin), data.damageMax)}",
                null, StatIconType.Damage, golden);

            // Row 1: 速度, 士气, 幸运, 射程
            var row1 = _statGrid.GetChild(1);
            AddStatCard(row1, "速度", (data.speed + unit.shopBuffSpeed).ToString(),
                FormatBonus(unit.shopBuffSpeed), StatIconType.Speed, golden, true);
            AddStatCard(row1, "士气", morale.ToString(), null, StatIconType.Morale, golden);
            AddStatCard(row1, "幸运", luck.ToString(), null, StatIconType.Luck, golden);
            AddStatCardWide(row1, "射程", FormatRange(data.attackRange > 0f ? data.attackRange : data.range),
                null, StatIconType.Range, golden);
        }

        private static string FormatBonus(int bonus)
        {
            if (bonus == 0)
            {
                return null;
            }

            return bonus > 0 ? $"+{bonus}" : bonus.ToString();
        }

        private static string ResolveCountBonus(UnitDefinition definition, UnitCardState card)
        {
            var bonus = (card?.roundTempCount ?? 0);
            if (bonus == 0)
            {
                return null;
            }

            return bonus > 0 ? $"+{bonus}" : bonus.ToString();
        }

        /// <summary>
        /// Standard stat card with auto-sizing: label+value stack vertically, card grows to fit.
        /// </summary>
        private static void AddStatCard(Transform row, string label, string value, string bonus, StatIconType iconType, bool golden, bool emphasizeSpeed = false)
        {
            var width = emphasizeSpeed ? 118f : 112f;
            var card = CreateStatCardBase(row, width, label, value, bonus, iconType, golden);

            if (emphasizeSpeed)
            {
                var cardImg = card.GetComponent<Image>();
                cardImg.color = new Color32(24, 44, 66, 225);

                var cardOutline = card.GetComponent<Outline>();
                cardOutline.effectColor = new Color32(110, 220, 255, 160);
            }

            if (!string.IsNullOrEmpty(bonus))
            {
                // Bonus overlay anchored top-right inside card
                var bonusLabel = CreateText("Bonus", card.transform, FontSizeStat, TextAnchor.MiddleRight);
                bonusLabel.fontStyle = FontStyle.Bold;
                bonusLabel.color = BonusTextColor;
                bonusLabel.alignment = TextAnchor.MiddleRight;
                bonusLabel.raycastTarget = false;
                bonusLabel.supportRichText = true;
                var bonusFitter = bonusLabel.GetComponent<ContentSizeFitter>();
                bonusFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
                bonusFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

                var bonusRect = bonusLabel.rectTransform;
                bonusRect.pivot = new Vector2(1f, 0.5f);
                bonusRect.anchorMin = new Vector2(1f, 0.5f);
                bonusRect.anchorMax = new Vector2(1f, 0.5f);
                bonusRect.anchoredPosition = new Vector2(-12f, 1f);

                SetText(bonusLabel, bonus);
            }

            BuildStatIcon(card.transform, iconType, golden);
        }

        private static void AddStatCardWide(Transform row, string label, string value, string bonus, StatIconType iconType, bool golden)
        {
            var card = CreateStatCardBase(row, 122f, label, value, bonus, iconType, golden);
            BuildStatIcon(card.transform, iconType, golden);
        }

        /// <summary>
        /// Creates a stat card with VerticalLayoutGroup + ContentSizeFitter so height auto-grows with text.
        /// </summary>
        private static GameObject CreateStatCardBase(Transform row, float width, string label, string value, string bonus, StatIconType iconType, bool golden)
        {
            var card = new GameObject("StatCard", typeof(Image), typeof(Outline), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter), typeof(LayoutElement));
            card.transform.SetParent(row, false);

            var cardImg = card.GetComponent<Image>();
            cardImg.color = StatCardFill;
            cardImg.raycastTarget = false;

            var cardOutline = card.GetComponent<Outline>();
            cardOutline.effectColor = StatCardBorder;
            cardOutline.effectDistance = new Vector2(1f, -1f);

            var cardVlg = card.GetComponent<VerticalLayoutGroup>();
            cardVlg.padding = new RectOffset(12, 12, 8, 6);
            cardVlg.spacing = 2f;
            cardVlg.childControlWidth = true;
            cardVlg.childControlHeight = true;
            cardVlg.childForceExpandWidth = true;
            cardVlg.childForceExpandHeight = false;

            var cardFitter = card.GetComponent<ContentSizeFitter>();
            cardFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            cardFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var cardLayout = card.GetComponent<LayoutElement>();
            cardLayout.preferredWidth = width;
            cardLayout.flexibleWidth = 0f;

            // Label (top, auto-sizes height)
            var lblText = CreateText("Label", card.transform, FontSizeStat, TextAnchor.UpperLeft);
            lblText.fontStyle = FontStyle.Normal;
            lblText.color = NormalStatLabelColor;
            lblText.raycastTarget = false;
            lblText.alignment = TextAnchor.UpperLeft;
            SetText(lblText, label);

            // Value (bottom, auto-sizes height)
            var valText = CreateText("Value", card.transform, FontSizeStat, TextAnchor.UpperLeft);
            valText.fontStyle = FontStyle.Bold;
            valText.color = golden ? GoldenTextPrimary : NormalTextBody;
            valText.raycastTarget = false;
            valText.alignment = TextAnchor.UpperLeft;
            SetText(valText, value);

            return card;
        }

        private static void BuildStatIcon(Transform parent, StatIconType iconType, bool golden)
        {
            var backplateColor = golden ? GoldenStatIconBackplate : StatIconBackplate;
            var fgColor = golden ? GoldenTextPrimary : NormalAccentColor;
            var strokeColor = golden ? GoldenStatIconBorder : StatIconBorder;

            // Icon circle (anchored top-right, outside layout)
            var iconRoot = new GameObject("Icon", typeof(Image), typeof(Outline), typeof(LayoutElement));
            iconRoot.transform.SetParent(parent, false);
            var rootImg = iconRoot.GetComponent<Image>();
            rootImg.color = backplateColor;
            rootImg.raycastTarget = false;
            var rootOutline = iconRoot.GetComponent<Outline>();
            rootOutline.effectColor = strokeColor;
            rootOutline.effectDistance = new Vector2(1f, -1f);

            var iconLayoutEl = iconRoot.GetComponent<LayoutElement>();
            iconLayoutEl.ignoreLayout = true;

            var rootRect = iconRoot.GetComponent<RectTransform>();
            rootRect.pivot = new Vector2(1f, 1f);
            rootRect.anchorMin = new Vector2(1f, 1f);
            rootRect.anchorMax = new Vector2(1f, 1f);
            rootRect.sizeDelta = new Vector2(28f, 28f);
            rootRect.anchoredPosition = new Vector2(-14f, -8f);

            switch (iconType)
            {
                case StatIconType.Count:
                    BuildCountDots(iconRoot.transform, fgColor);
                    break;
                case StatIconType.Attack:
                    BuildSwordIcon(iconRoot.transform, fgColor);
                    break;
                case StatIconType.Defense:
                    BuildShieldIcon(iconRoot.transform, fgColor);
                    break;
                case StatIconType.Damage:
                    BuildDiamondIcon(iconRoot.transform, fgColor, backplateColor);
                    break;
                case StatIconType.Speed:
                    BuildSpeedChevrons(iconRoot.transform, fgColor);
                    break;
                case StatIconType.Morale:
                    BuildFlagIcon(iconRoot.transform, fgColor);
                    break;
                case StatIconType.Luck:
                    BuildCloverIcon(iconRoot.transform, fgColor);
                    break;
                case StatIconType.Range:
                    BuildRangeIcon(iconRoot.transform, fgColor);
                    break;
            }
        }

        // --- Icon builders (simplified geometric primitives) ---

        private static void BuildCountDots(Transform parent, Color32 color)
        {
            var positions = new Vector2[] {
                new Vector2(3f, 4f),
                new Vector2(7f, 1f),
                new Vector2(10f, 6f)
            };
            foreach (var pos in positions)
            {
                var dot = CreateImage("Dot", parent, color);
                dot.rectTransform.sizeDelta = new Vector2(3f, 3f);
                dot.rectTransform.anchoredPosition = pos;
            }
        }

        private static void BuildSwordIcon(Transform parent, Color32 color)
        {
            var blade = CreateImage("Blade", parent, color);
            blade.rectTransform.sizeDelta = new Vector2(3f, 8f);
            blade.rectTransform.anchoredPosition = new Vector2(5f, 3f);

            var guard = CreateImage("Guard", parent, color);
            guard.rectTransform.sizeDelta = new Vector2(7f, 2f);
            guard.rectTransform.anchoredPosition = new Vector2(5f, -2f);
        }

        private static void BuildShieldIcon(Transform parent, Color32 color)
        {
            var shield = CreateImage("Shield", parent, color);
            shield.rectTransform.sizeDelta = new Vector2(10f, 11f);
            shield.rectTransform.anchoredPosition = new Vector2(5f, 0f);
        }

        private static void BuildDiamondIcon(Transform parent, Color32 fgColor, Color32 bgColor)
        {
            var outer = CreateImage("Diamond", parent, fgColor);
            outer.rectTransform.sizeDelta = new Vector2(10f, 10f);
            outer.rectTransform.anchoredPosition = new Vector2(5f, 0f);
            outer.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);

            var inner = CreateImage("Core", parent, bgColor);
            inner.rectTransform.sizeDelta = new Vector2(5f, 5f);
            inner.rectTransform.anchoredPosition = new Vector2(5f, 0f);
            inner.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
        }

        private static void BuildSpeedChevrons(Transform parent, Color32 color)
        {
            for (int i = 0; i < 2; i++)
            {
                var chevron = CreateImage("Chevron", parent, color);
                chevron.rectTransform.sizeDelta = new Vector2(10f, 2f);
                chevron.rectTransform.anchoredPosition = new Vector2(4f, 1f + i * 4f);
                chevron.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 25f);
            }
        }

        private static void BuildFlagIcon(Transform parent, Color32 color)
        {
            var pole = CreateImage("Pole", parent, color);
            pole.rectTransform.sizeDelta = new Vector2(1.5f, 10f);
            pole.rectTransform.anchoredPosition = new Vector2(2f, 0f);

            var flag = CreateImage("Flag", parent, color);
            flag.rectTransform.sizeDelta = new Vector2(6f, 4f);
            flag.rectTransform.anchoredPosition = new Vector2(5f, 1f);
        }

        private static void BuildCloverIcon(Transform parent, Color32 color)
        {
            var label = CreateText("Glyph", parent, FontSizeIconGlyph, TextAnchor.MiddleCenter);
            label.fontStyle = FontStyle.Bold;
            label.color = color;
            label.alignment = TextAnchor.MiddleCenter;
            label.raycastTarget = false;
            var gf = label.gameObject.AddComponent<ContentSizeFitter>();
            gf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            gf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            label.rectTransform.anchoredPosition = new Vector2(5f, 0f);
            SetText(label, "✻");
        }

        private static void BuildRangeIcon(Transform parent, Color32 color)
        {
            var outer = CreateImage("RangeOuter", parent, new Color32(0, 0, 0, 0));
            outer.rectTransform.sizeDelta = new Vector2(10f, 10f);
            outer.rectTransform.anchoredPosition = new Vector2(5f, 0f);
            var outerOutline = outer.gameObject.AddComponent<Outline>();
            outerOutline.effectColor = color;
            outerOutline.effectDistance = new Vector2(1f, -1f);

            var inner = CreateImage("RangeInner", parent, color);
            inner.rectTransform.sizeDelta = new Vector2(3f, 3f);
            inner.rectTransform.anchoredPosition = new Vector2(5f, 2f);
        }

        // ---------------------------------------------------------------
        //  Skill Blocks
        // ---------------------------------------------------------------

        private static GameObject CreateSkillBlock(string name, Transform parent,
            Color32 fillColor, Color32 borderColor, out Text heading, out Text body)
        {
            var block = new GameObject(name, typeof(Image), typeof(Outline), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter), typeof(LayoutElement));
            block.transform.SetParent(parent, false);

            var blockImg = block.GetComponent<Image>();
            blockImg.color = fillColor;
            blockImg.raycastTarget = false;

            var blockOutline = block.GetComponent<Outline>();
            blockOutline.effectColor = borderColor;
            blockOutline.effectDistance = new Vector2(1f, -1f);

            var blockVlg = block.GetComponent<VerticalLayoutGroup>();
            blockVlg.padding = new RectOffset(14, 14, 11, 11);
            blockVlg.spacing = 4f;
            blockVlg.childControlWidth = true;
            blockVlg.childControlHeight = true;
            blockVlg.childForceExpandWidth = true;
            blockVlg.childForceExpandHeight = false;

            var blockFitter = block.GetComponent<ContentSizeFitter>();
            blockFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            blockFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var blockElement = block.GetComponent<LayoutElement>();
            blockElement.preferredWidth = PanelWidth - 48f;
            blockElement.flexibleWidth = 1f;

            // Heading (auto-sizes height)
            heading = CreateText("Heading", block.transform, FontSizeSkillHeading, TextAnchor.UpperLeft);
            heading.fontStyle = FontStyle.Bold;
            heading.raycastTarget = false;
            heading.supportRichText = true;
            heading.lineSpacing = 1f;

            // Body (auto-sizes height, wraps text)
            body = CreateText("Body", block.transform, FontSizeSkillBody, TextAnchor.UpperLeft);
            body.fontStyle = FontStyle.Normal;
            body.raycastTarget = false;
            body.supportRichText = true;
            body.lineSpacing = 1.05f;

            return block;
        }

        private static void BindSkillBlock(GameObject block, Text body, string text)
        {
            var visible = !string.IsNullOrWhiteSpace(text);
            if (block != null)
            {
                block.SetActive(visible);
            }

            if (visible && body != null)
            {
                SetText(body, HighlightKeywords(Escape(text)));
            }
        }

        private static readonly string[] _gameplayKeywords = {
            "数量", "攻击", "防御", "伤害", "速度", "士气", "幸运", "射程",
            "密林宝钻", "入场", "离场", "金币", "手牌", "商店",
            "甘德", "甘格尔", "甘地", "莱特", "圣临者", "征服者",
            "进阶", "升级", "刷新", "锁定", "吞噬", "祝福",
            "眩晕", "阵亡", "格挡", "免疫", "暴击",
            "回合", "开始", "结束", "出售", "战场",
            "士气高涨", "幸运", "追击", "召唤", "光环",
            "临时", "永久", "全体", "相邻", "同排", "周围",
            "种族", "信仰", "类型", "星级", "卡牌"
        };

        private static string HighlightKeywords(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return text;
            }

            // Sort by length descending so longer phrases match first
            var sorted = _gameplayKeywords.OrderByDescending(k => k.Length).ToArray();

            foreach (var keyword in sorted)
            {
                if (!text.Contains(keyword))
                {
                    continue;
                }

                var color = GetKeywordColor(keyword);
                var replacement = $"<color=#{color}>{keyword}</color>";
                text = text.Replace(keyword, replacement);
            }

            return text;
        }

        private static string GetKeywordColor(string keyword)
        {
            switch (keyword)
            {
                case "数量":
                case "密林宝钻":
                    return "74EE9A"; // bright green
                case "攻击":
                case "伤害":
                case "暴击":
                    return "FF6E6E"; // damage red
                case "防御":
                case "格挡":
                case "免疫":
                    return "60C8FF"; // shield blue
                case "速度":
                    return "6CE0FF"; // speed cyan
                case "士气":
                case "士气高涨":
                case "幸运":
                case "追击":
                    return "FFD960"; // morale gold
                case "入场":
                case "离场":
                case "召唤":
                case "阵亡":
                    return "FFA64D"; // event orange
                case "金币":
                    return "FFDC6B"; // gold coin
                case "手牌":
                case "商店":
                    return "C8A2FF"; // shop purple
                case "甘德":
                case "甘格尔":
                case "甘地":
                case "莱特":
                case "圣临者":
                case "征服者":
                    return "FFB8E0"; // race pink
                case "进阶":
                case "升级":
                case "吞噬":
                case "祝福":
                    return "FFD95B"; // upgrade gold
                case "眩晕":
                case "锁定":
                    return "96D8FF"; // control blue
                case "光环":
                case "全体":
                case "相邻":
                case "同排":
                case "周围":
                    return "A0E8D0"; // aura teal
                case "临时":
                case "永久":
                    return "E8E0A0"; // duration cream
                default:
                    return "FFB870"; // fallback warm
            }
        }

        // ---------------------------------------------------------------
        //  Helpers
        // ---------------------------------------------------------------

        private static GameObject CreateDivider(Transform parent)
        {
            var div = new GameObject("Divider", typeof(Image), typeof(LayoutElement));
            div.transform.SetParent(parent, false);
            var divImg = div.GetComponent<Image>();
            divImg.color = DividerColor;
            divImg.raycastTarget = false;
            var divLayout = div.GetComponent<LayoutElement>();
            divLayout.preferredHeight = 1f;
            divLayout.flexibleWidth = 1f;
            return div;
        }

        private static void SetDividerActive(GameObject divider, bool active)
        {
            if (divider != null)
            {
                divider.SetActive(active);
            }
        }

        private static Image CreateImage(string name, Transform parent, Color32 color)
        {
            var obj = new GameObject(name, typeof(Image));
            obj.transform.SetParent(parent, false);
            var img = obj.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            var rt = obj.GetComponent<RectTransform>();
            rt.sizeDelta = Vector2.zero;
            rt.anchoredPosition = Vector2.zero;
            return img;
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

        private static int ResolveBaseCount(UnitDefinition definition, UnitCardState card)
        {
            var startCount = ResolveStartCount(definition);
            return Mathf.Max(1, (card != null && card.baseCount > 0 ? card.baseCount : startCount) + (card?.roundTempCount ?? 0));
        }

        private static int ResolveStartCount(UnitDefinition definition)
        {
            if (definition == null)
            {
                return 1;
            }

            return Mathf.Max(1, definition.defaultCount > 0 ? definition.defaultCount : definition.startCount > 0 ? definition.startCount : definition.baseCount > 0 ? definition.baseCount : 1);
        }

        // ---------------------------------------------------------------
        //  UI Primitives
        // ---------------------------------------------------------------

        private static GameObject CreateRow(string name, Transform parent, float spacing)
        {
            var row = new GameObject(name, typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter));
            row.transform.SetParent(parent, false);
            var layout = row.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = spacing;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var rowFitter = row.GetComponent<ContentSizeFitter>();
            rowFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            rowFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            return row;
        }

        private static Text CreateText(string name, Transform parent, int fontSize, TextAnchor alignment)
        {
            var obj = new GameObject(name, typeof(Text), typeof(ContentSizeFitter));
            obj.transform.SetParent(parent, false);
            var text = obj.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            text.supportRichText = true;
            text.lineSpacing = 1.05f;

            // Horizontal: wrap within parent width (Unconstrained means parent VLG controls it)
            // Vertical: PreferredSize so ContentSizeFitter can measure actual text height
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