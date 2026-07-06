using ProphecyCentury.Data;
using ProphecyCentury.Model;
using ProphecyCentury.Systems;
using UnityEngine;
using UnityEngine.UI;

namespace ProphecyCentury.UI
{
    public enum UnitCardPresentationMode
    {
        Grid,
        List,
        Board,
        Encyclopedia
    }

    public sealed class UnitCardView : MonoBehaviour
    {
        public const string PrefabResourcePath = "Prefabs/UI/UnitCard";
        public const string PrefabAssetPath = "Assets/Resources/Prefabs/UI/UnitCard.prefab";
        public const string ShopPrefabResourcePath = "Prefabs/UI/UnitCardShop";
        public const string ShopPrefabAssetPath = "Assets/Resources/Prefabs/UI/UnitCardShop.prefab";
        public const string HandPrefabResourcePath = "Prefabs/UI/UnitCardHand";
        public const string HandPrefabAssetPath = "Assets/Resources/Prefabs/UI/UnitCardHand.prefab";
        public const string BoardPrefabResourcePath = "Prefabs/UI/UnitCardBoard";
        public const string BoardPrefabAssetPath = "Assets/Resources/Prefabs/UI/UnitCardBoard.prefab";
        public const string StyleLibraryAssetPath = "Assets/Resources/Data/UnitCardRaceStyleLibrary.asset";

        [Header("Visuals")]
        [SerializeField] private Image backgroundImage;
        [SerializeField] private Image raceBackgroundImage;
        [SerializeField] private Image frameImage;
        [SerializeField] private Image iconImage;

        [Header("Labels")]
        [SerializeField] private Text starsLabel;
        [SerializeField] private Text nameLabel;
        [SerializeField] private Text statsLabel;
        [SerializeField] private Text tagsLabel;
        [SerializeField] private Text gemLabel;

        [Header("Layout Control")]
        [SerializeField] private bool useScriptedLayout;

        [Header("Board Layout")]
        [SerializeField] private bool usePrefabBoardLayout = true;

        [Header("Scripted Board Layout Fallback")]
        [Min(0.1f)]
        [SerializeField] private float boardScaleMin = 0.75f;
        [Min(0.1f)]
        [SerializeField] private float boardScaleMax = 1.15f;
        [Min(0f)]
        [SerializeField] private float boardPadding = 5f;
        [Min(0f)]
        [SerializeField] private float boardIconTop = 67f;
        [Min(1f)]
        [SerializeField] private float boardIconSize = 80f;
        [Min(0f)]
        [SerializeField] private float boardNameTop = 2f;
        [Min(1f)]
        [SerializeField] private float boardNameHeight = 24f;
        [Min(1)]
        [SerializeField] private int boardNameFontMin = 11;
        [Min(1)]
        [SerializeField] private int boardNameFontMax = 16;
        [Min(0f)]
        [SerializeField] private float boardGemRight = 5f;
        [Min(0f)]
        [SerializeField] private float boardGemTop = 78f;
        [Min(1f)]
        [SerializeField] private float boardGemWidth = 45f;
        [Min(1f)]
        [SerializeField] private float boardGemHeight = 20f;
        [Min(1)]
        [SerializeField] private int boardGemFontMin = 10;
        [Min(1)]
        [SerializeField] private int boardGemFontMax = 13;
        [Min(0f)]
        [SerializeField] private float boardStatsBottom = 25f;
        [Min(1f)]
        [SerializeField] private float boardStatsHeight = 24f;
        [Min(1)]
        [SerializeField] private int boardStatsFontMin = 10;
        [Min(1)]
        [SerializeField] private int boardStatsFontMax = 14;

        private static GameObject _cachedPrefab;
        private static GameObject _cachedShopPrefab;
        private static GameObject _cachedHandPrefab;
        private static GameObject _cachedBoardPrefab;
        private static readonly Color32 GoldenNameColor = new Color32(255, 216, 107, 255);

        public Image BackgroundImage => backgroundImage;
        public Image RaceBackgroundImage => raceBackgroundImage;
        public Image FrameImage => frameImage;
        public Image IconImage => iconImage;
        public Text StarsLabel => starsLabel;
        public Text NameLabel => nameLabel;
        public Text StatsLabel => statsLabel;
        public Text TagsLabel => tagsLabel;
        public Text GemLabel => gemLabel;

        public static UnitCardView Instantiate(Transform parent)
        {
            return Instantiate(parent, UnitCardPresentationMode.Grid);
        }

        public static UnitCardView Instantiate(Transform parent, UnitCardPresentationMode mode)
        {
            if (_cachedPrefab == null)
            {
                _cachedPrefab = Resources.Load<GameObject>(PrefabResourcePath);
            }

            var prefab = LoadPrefabForMode(mode) ?? _cachedPrefab;
            if (prefab != null)
            {
                var instance = Object.Instantiate(prefab, parent, false);
                return instance.GetComponent<UnitCardView>();
            }

            return CreateRuntimeInstance(parent);
        }

        private static GameObject LoadPrefabForMode(UnitCardPresentationMode mode)
        {
            switch (mode)
            {
                case UnitCardPresentationMode.Board:
                    return LoadCachedPrefab(ref _cachedBoardPrefab, BoardPrefabResourcePath);
                case UnitCardPresentationMode.List:
                    return LoadCachedPrefab(ref _cachedHandPrefab, HandPrefabResourcePath);
                case UnitCardPresentationMode.Grid:
                    return LoadCachedPrefab(ref _cachedShopPrefab, ShopPrefabResourcePath);
                default:
                    return null;
            }
        }

        private static GameObject LoadCachedPrefab(ref GameObject cachedPrefab, string resourcePath)
        {
            if (cachedPrefab == null)
            {
                cachedPrefab = Resources.Load<GameObject>(resourcePath);
            }

            return cachedPrefab;
        }

        public static UnitCardView CreateRuntimeInstance(Transform parent)
        {
            var root = new GameObject("UnitCard", typeof(Image), typeof(Button), typeof(LayoutElement), typeof(UnitCardView));
            root.transform.SetParent(parent, false);

            var background = root.GetComponent<Image>();
            background.color = new Color32(38, 38, 40, 255);

            var raceBackground = CreateImage("RaceBackgroundImage", root.transform);
            raceBackground.preserveAspect = true;
            raceBackground.raycastTarget = false;

            var frame = CreateImage("FrameImage", root.transform);
            Stretch(frame.rectTransform, Vector2.zero, Vector2.zero);
            frame.type = Image.Type.Sliced;
            frame.color = new Color32(96, 132, 164, 90);
            frame.raycastTarget = false;

            var icon = CreateImage("IconImage", root.transform);
            icon.preserveAspect = true;
            icon.raycastTarget = false;

            var stars = CreateText("StarsLabel", root.transform, 20, TextAnchor.MiddleCenter);
            var name = CreateText("NameLabel", root.transform, 22, TextAnchor.MiddleCenter);
            var stats = CreateText("StatsLabel", root.transform, 18, TextAnchor.MiddleCenter);
            var tags = CreateText("TagsLabel", root.transform, 16, TextAnchor.MiddleCenter);
            var gems = CreateText("GemLabel", root.transform, 16, TextAnchor.MiddleRight);

            var view = root.GetComponent<UnitCardView>();
            view.ConfigureReferences(background, raceBackground, frame, icon, stars, name, stats, tags, gems);
            view.useScriptedLayout = true;
            view.usePrefabBoardLayout = false;
            return view;
        }

        public void UsePrefabDrivenLayout()
        {
            useScriptedLayout = false;
            usePrefabBoardLayout = true;
        }

        public void BakePrefabDefaultLayout(UnitCardPresentationMode mode, Vector2 size)
        {
            var rect = transform as RectTransform;
            if (rect != null)
            {
                rect.sizeDelta = size;
            }

            useScriptedLayout = true;
            usePrefabBoardLayout = false;
            EnsureReferences();
            ApplyLayout(mode);

            if (mode == UnitCardPresentationMode.Board)
            {
                if (frameImage != null)
                {
                    frameImage.enabled = false;
                }
            }
            else
            {
                if (raceBackgroundImage != null)
                {
                    raceBackgroundImage.transform.SetSiblingIndex(0);
                }

                if (frameImage != null)
                {
                    frameImage.enabled = true;
                    frameImage.transform.SetAsLastSibling();
                }
            }

            UsePrefabDrivenLayout();
        }

        public void ConfigureReferences(Image background, Image raceBackground, Image frame, Image icon, Text stars, Text name, Text stats, Text tags, Text gems = null)
        {
            backgroundImage = background;
            raceBackgroundImage = raceBackground;
            frameImage = frame;
            iconImage = icon;
            starsLabel = stars;
            nameLabel = name;
            statsLabel = stats;
            tagsLabel = tags;
            gemLabel = gems;
        }

        public void Bind(
            UnitDefinition definition,
            UnitCardState card,
            UnitCardPresentationMode mode,
            UnitCardRaceStyleLibrary styleLibrary,
            string prefix = null,
            bool selected = false)
        {
            EnsureReferences();
            if (useScriptedLayout)
            {
                ApplyLayout(mode);
            }

            var hasUnit = definition != null || card != null;
            var style = styleLibrary != null ? styleLibrary.GetStyle(definition?.race) : null;
            if (ManageEventResolver.IsForestGemCard(card))
            {
                ApplyStyle(style, false, selected, true, mode);
                SetText(starsLabel, string.Empty);
                SetText(nameLabel, string.IsNullOrWhiteSpace(prefix) ? ManageEventResolver.ForestGemCardName : $"{prefix}  {ManageEventResolver.ForestGemCardName}");
                SetText(statsLabel, $"\u4f7f\u7528\uff1a\u83b7\u5f97\u6570\u91cf +{ManageEventResolver.ForestGemReinforceCount}");
                SetText(tagsLabel, "\u5bc6\u6797  \u6d88\u8017\u54c1");
                SetText(gemLabel, string.Empty);
                SetIcon(iconImage, null);
                return;
            }

            var golden = card != null && card.isGolden;
            ApplyStyle(style, golden, selected, hasUnit, mode);

            if (!hasUnit)
            {
                SetText(starsLabel, string.Empty);
                SetText(nameLabel, string.Empty);
                SetText(statsLabel, string.Empty);
                SetText(tagsLabel, string.Empty);
                SetText(gemLabel, string.Empty);
                SetIcon(iconImage, null);
                return;
            }

            var displayName = card?.name ?? definition?.name ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(prefix))
            {
                displayName = $"{prefix}  {displayName}";
            }

            var star = Mathf.Clamp(definition?.star ?? card?.star ?? 0, 0, 6);
            var count = ResolveBaseCount(definition, card);
            var damageMin = Mathf.Max(1, definition?.damageMin ?? 1);
            var damageMax = Mathf.Max(damageMin, definition?.damageMax ?? damageMin);

            SetText(starsLabel, new string('\u2605', star));
            if (mode == UnitCardPresentationMode.Board)
            {
                SetText(nameLabel, Mathf.Max(0, count).ToString());
                SetText(statsLabel, FormatBoardIdentity(definition));
                SetText(tagsLabel, string.Empty);
            }
            else
            {
                SetText(nameLabel, displayName);
                SetText(statsLabel, $"\u6570 {count}  \u4f24 {damageMin}-{damageMax}");
                SetText(tagsLabel, definition == null ? string.Empty : $"{definition.race}  {definition.typeLabel}  {definition.faith}");
            }
            SetText(gemLabel, FormatBoardGemText(definition, card, mode));
            SetIcon(iconImage, card?.name ?? definition?.name);
        }

        private static string FormatBoardIdentity(UnitDefinition definition)
        {
            if (definition == null)
            {
                return string.Empty;
            }

            var type = string.IsNullOrWhiteSpace(definition.typeLabel) ? definition.type : definition.typeLabel;
            if (string.IsNullOrWhiteSpace(type))
            {
                return definition.faith ?? string.Empty;
            }

            if (string.IsNullOrWhiteSpace(definition.faith))
            {
                return type;
            }

            return $"{type} \u00b7 {definition.faith}";
        }

        private void ApplyStyle(UnitCardRaceStyle style, bool golden, bool selected, bool hasUnit, UnitCardPresentationMode mode)
        {
            if (!useScriptedLayout)
            {
                ApplyPrefabImages(style, golden, hasUnit);
                ApplyLabelColors(style, golden);
                return;
            }

            if (backgroundImage != null)
            {
                backgroundImage.sprite = null;
                backgroundImage.color = !hasUnit
                    ? new Color32(20, 20, 22, 170)
                    : selected
                        ? new Color32(56, 56, 60, 255)
                        : new Color32(38, 38, 40, 255);
            }

            if (raceBackgroundImage != null)
            {
                raceBackgroundImage.sprite = hasUnit ? style?.background : null;
                raceBackgroundImage.preserveAspect = true;
                raceBackgroundImage.color = !hasUnit
                    ? new Color32(22, 28, 36, 160)
                    : raceBackgroundImage.sprite != null
                        ? (selected ? new Color(1f, 1f, 1f, 0.96f) : Color.white)
                        : selected
                            ? Color.Lerp(style?.backgroundColor ?? new Color32(42, 58, 74, 245), Color.white, 0.16f)
                            : style?.backgroundColor ?? new Color32(42, 58, 74, 245);
            }

            if (frameImage != null)
            {
                if (useScriptedLayout && mode == UnitCardPresentationMode.Board)
                {
                    frameImage.enabled = false;
                    frameImage.sprite = null;
                    frameImage.raycastTarget = false;
                }
                else
                {
                    frameImage.enabled = true;
                    frameImage.sprite = golden && style?.goldenFrame != null ? style.goldenFrame : style?.frame;
                    frameImage.type = frameImage.sprite != null && HasSpriteBorder(frameImage.sprite)
                        ? Image.Type.Sliced
                        : Image.Type.Simple;
                    frameImage.raycastTarget = false;
                    if (useScriptedLayout)
                    {
                        frameImage.transform.SetAsLastSibling();
                    }

                    var frameColor = style?.frameColor ?? new Color32(96, 132, 164, 255);
                    if (frameImage.sprite == null)
                    {
                        frameColor.a = 0.36f;
                    }

                    frameImage.color = !hasUnit
                        ? new Color32(58, 70, 84, 120)
                        : golden
                            ? (frameImage.sprite == null ? new Color32(242, 198, 82, 110) : Color.white)
                            : selected
                                ? (frameImage.sprite == null ? new Color(1f, 1f, 1f, 0.42f) : Color.white)
                                : (frameImage.sprite == null ? frameColor : Color.white);
                }
            }

            ApplyLabelColors(style, golden);
        }

        private void ApplyLabelColors(UnitCardRaceStyle style, bool golden)
        {
            SetColor(nameLabel, golden ? GoldenNameColor : style?.titleColor ?? Color.white);
            SetColor(statsLabel, style?.statsColor ?? Color.white);
            SetColor(tagsLabel, style?.tagsColor ?? new Color32(220, 228, 236, 255));
            SetColor(starsLabel, new Color32(255, 220, 96, 255));
            SetColor(gemLabel, new Color32(235, 248, 255, 255));
        }

        private void ApplyPrefabImages(UnitCardRaceStyle style, bool golden, bool hasUnit)
        {
            if (raceBackgroundImage != null)
            {
                raceBackgroundImage.sprite = hasUnit ? style?.background : null;
                raceBackgroundImage.preserveAspect = true;
                raceBackgroundImage.raycastTarget = false;
            }

            if (frameImage != null)
            {
                frameImage.sprite = hasUnit
                    ? (golden && style?.goldenFrame != null ? style.goldenFrame : style?.frame)
                    : null;
                frameImage.type = frameImage.sprite != null && HasSpriteBorder(frameImage.sprite)
                    ? Image.Type.Sliced
                    : Image.Type.Simple;
                frameImage.raycastTarget = false;
            }

            if (iconImage != null)
            {
                iconImage.preserveAspect = true;
                iconImage.raycastTarget = false;
            }
        }

        private void ApplyLayout(UnitCardPresentationMode mode)
        {
            if (mode == UnitCardPresentationMode.Board)
            {
                ApplyBoardLayout();
                return;
            }

            var compact = mode == UnitCardPresentationMode.List;
            var cardRect = transform as RectTransform;
            var cardWidth = cardRect != null && cardRect.sizeDelta.x > 10f ? cardRect.sizeDelta.x : 221f;
            var cardHeight = cardRect != null && cardRect.sizeDelta.y > 10f ? cardRect.sizeDelta.y : 286f;
            var layoutScale = Mathf.Clamp(cardHeight / 286f, 0.78f, 1.0f);
            var raceSize = Mathf.Min(cardWidth, cardHeight * 0.77f);
            var iconSize = Mathf.Min(cardWidth * 0.68f, cardHeight * 0.53f);

            if (raceBackgroundImage != null)
            {
                var rect = raceBackgroundImage.rectTransform;
                rect.anchorMin = compact ? new Vector2(0f, 0.5f) : new Vector2(0.5f, 1f);
                rect.anchorMax = compact ? new Vector2(0f, 0.5f) : new Vector2(0.5f, 1f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = compact ? new Vector2(38f, 10f) : new Vector2(0f, -raceSize * 0.5f);
                rect.sizeDelta = compact ? new Vector2(70f, 70f) : new Vector2(raceSize, raceSize);
            }

            if (iconImage != null)
            {
                var rect = iconImage.rectTransform;
                rect.anchorMin = compact ? new Vector2(0f, 0.5f) : new Vector2(0.5f, 1f);
                rect.anchorMax = compact ? new Vector2(0f, 0.5f) : new Vector2(0.5f, 1f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = compact ? new Vector2(38f, 10f) : new Vector2(0f, -cardHeight * 0.36f);
                rect.sizeDelta = compact ? new Vector2(54f, 54f) : new Vector2(iconSize, iconSize);
            }

            if (compact)
            {
                Place(starsLabel, new Vector2(78f, -4f), new Vector2(-8f, -26f), 13, TextAnchor.UpperLeft);
                Place(nameLabel, new Vector2(78f, 28f), new Vector2(-8f, -56f), 14, TextAnchor.MiddleLeft);
                Place(statsLabel, new Vector2(78f, 54f), new Vector2(-8f, -82f), 12, TextAnchor.MiddleLeft);
                Place(tagsLabel, new Vector2(78f, 78f), new Vector2(-8f, -104f), 11, TextAnchor.MiddleLeft);
            }
            else
            {
                PlaceTop(starsLabel, 4f, 0f, cardWidth - 8f, 31f * layoutScale, Mathf.RoundToInt(23f * layoutScale), TextAnchor.MiddleCenter);
                PlaceTop(nameLabel, 8f, cardHeight * 0.625f, cardWidth - 16f, 38f * layoutScale, Mathf.RoundToInt(20f * layoutScale), TextAnchor.MiddleCenter);
                PlaceTop(statsLabel, 10f, cardHeight * 0.775f, cardWidth - 20f, 29f * layoutScale, Mathf.RoundToInt(17f * layoutScale), TextAnchor.MiddleCenter);
                PlaceTop(tagsLabel, 10f, cardHeight * 0.882f, cardWidth - 20f, 27f * layoutScale, Mathf.RoundToInt(16f * layoutScale), TextAnchor.MiddleCenter);
                ConfigureBestFit(nameLabel, 12, Mathf.RoundToInt(20f * layoutScale));
                ConfigureBestFit(statsLabel, 11, Mathf.RoundToInt(17f * layoutScale));
                ConfigureBestFit(tagsLabel, 10, Mathf.RoundToInt(16f * layoutScale));
            }
        }

        private void ApplyBoardLayout()
        {
            if (backgroundImage != null)
            {
                backgroundImage.color = new Color(1f, 1f, 1f, 0f);
            }

            if (raceBackgroundImage != null)
            {
                raceBackgroundImage.enabled = false;
            }

            if (starsLabel != null)
            {
                starsLabel.gameObject.SetActive(false);
            }

            var cardRect = transform as RectTransform;
            var parentRect = transform.parent as RectTransform;
            var cardWidth = ResolveBoardSize(cardRect, parentRect, true);
            var cardHeight = ResolveBoardSize(cardRect, parentRect, false);
            if (cardRect != null)
            {
                cardRect.anchorMin = Vector2.zero;
                cardRect.anchorMax = Vector2.one;
                cardRect.offsetMin = Vector2.zero;
                cardRect.offsetMax = Vector2.zero;
            }

            if (iconImage != null)
            {
                iconImage.enabled = true;
                iconImage.preserveAspect = true;
            }

            if (tagsLabel != null)
            {
                tagsLabel.gameObject.SetActive(false);
            }

            if (usePrefabBoardLayout)
            {
                ConfigureBestFit(nameLabel, 16, 24);
                ConfigureBestFit(gemLabel, 8, 11);
                ConfigureBestFit(statsLabel, 8, 12);
                return;
            }

            var scale = Mathf.Clamp(Mathf.Min(cardWidth / 146f, cardHeight / 146f), boardScaleMin, boardScaleMax);
            var padding = boardPadding * scale;

            if (iconImage != null)
            {
                var rect = iconImage.rectTransform;
                rect.anchorMin = new Vector2(0.5f, 1f);
                rect.anchorMax = new Vector2(0.5f, 1f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = new Vector2(0f, -boardIconTop * scale);
                rect.sizeDelta = new Vector2(boardIconSize * scale, boardIconSize * scale);
                iconImage.enabled = true;
                iconImage.preserveAspect = true;
            }

            PlaceTop(nameLabel, padding, boardNameTop * scale, cardWidth - padding * 2f, boardNameHeight * scale, Mathf.RoundToInt(boardNameFontMax * scale), TextAnchor.MiddleCenter);
            ConfigureBestFit(nameLabel, boardNameFontMin, Mathf.RoundToInt(boardNameFontMax * scale));
            PlaceTop(gemLabel, cardWidth - (boardGemRight + boardGemWidth) * scale, boardGemTop * scale, boardGemWidth * scale, boardGemHeight * scale, Mathf.RoundToInt(boardGemFontMax * scale), TextAnchor.MiddleRight);
            ConfigureBestFit(gemLabel, boardGemFontMin, Mathf.RoundToInt(boardGemFontMax * scale));
            PlaceTop(statsLabel, padding, cardHeight - boardStatsBottom * scale, cardWidth - padding * 2f, boardStatsHeight * scale, Mathf.RoundToInt(boardStatsFontMax * scale), TextAnchor.MiddleCenter);
            ConfigureBestFit(statsLabel, boardStatsFontMin, Mathf.RoundToInt(boardStatsFontMax * scale));

            if (frameImage != null)
            {
                Stretch(frameImage.rectTransform, Vector2.zero, Vector2.zero);
                frameImage.transform.SetAsLastSibling();
            }
        }

        private void EnsureReferences()
        {
            if (backgroundImage == null)
            {
                backgroundImage = GetComponent<Image>();
            }

            if (raceBackgroundImage == null)
            {
                raceBackgroundImage = useScriptedLayout
                    ? FindOrCreateImage("RaceBackgroundImage", transform, false)
                    : FindExistingImage("RaceBackgroundImage", transform);
                if (raceBackgroundImage != null)
                {
                    raceBackgroundImage.preserveAspect = true;
                    raceBackgroundImage.raycastTarget = false;
                    if (useScriptedLayout)
                    {
                        raceBackgroundImage.transform.SetSiblingIndex(0);
                    }
                }
            }
            else
            {
                if (useScriptedLayout)
                {
                    raceBackgroundImage.enabled = true;
                }
            }

            if (frameImage == null)
            {
                frameImage = useScriptedLayout
                    ? FindOrCreateImage("FrameImage", transform, true)
                    : FindExistingImage("FrameImage", transform);
            }

            if (frameImage != null)
            {
                frameImage.raycastTarget = false;
                if (useScriptedLayout)
                {
                    frameImage.enabled = true;
                    frameImage.transform.SetAsLastSibling();
                }
            }

            if (iconImage == null)
            {
                iconImage = useScriptedLayout
                    ? FindOrCreateImage("IconImage", transform, false)
                    : FindExistingImage("IconImage", transform);
                if (iconImage != null)
                {
                    iconImage.preserveAspect = true;
                    iconImage.raycastTarget = false;
                }
            }

            if (gemLabel == null)
            {
                gemLabel = FindExistingText("GemLabel", transform);
                if (gemLabel == null && useScriptedLayout && !usePrefabBoardLayout)
                {
                    gemLabel = FindOrCreateText("GemLabel", transform, 16, TextAnchor.MiddleRight);
                }
            }

            if (useScriptedLayout && starsLabel != null)
            {
                starsLabel.gameObject.SetActive(true);
            }

            if (useScriptedLayout && tagsLabel != null)
            {
                tagsLabel.gameObject.SetActive(true);
            }

        }

        private static Text CreateText(string name, Transform parent, int fontSize, TextAnchor alignment)
        {
            var obj = new GameObject(name, typeof(Text));
            obj.transform.SetParent(parent, false);
            var text = obj.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.color = Color.white;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;
            var outline = obj.AddComponent<Outline>();
            outline.effectColor = new Color32(0, 0, 0, 230);
            outline.effectDistance = new Vector2(2f, -2f);
            return text;
        }

        private static Image CreateImage(string name, Transform parent)
        {
            var obj = new GameObject(name, typeof(Image));
            obj.transform.SetParent(parent, false);
            return obj.GetComponent<Image>();
        }

        private static Image FindOrCreateImage(string name, Transform parent, bool stretch)
        {
            var child = parent.Find(name);
            if (child == null)
            {
                var obj = new GameObject(name, typeof(Image));
                obj.transform.SetParent(parent, false);
                child = obj.transform;
            }

            var image = child.GetComponent<Image>() ?? child.gameObject.AddComponent<Image>();
            if (stretch)
            {
                Stretch(image.rectTransform, Vector2.zero, Vector2.zero);
            }

            return image;
        }

        private static Image FindExistingImage(string name, Transform parent)
        {
            var child = parent.Find(name);
            return child == null ? null : child.GetComponent<Image>();
        }

        private static Text FindOrCreateText(string name, Transform parent, int fontSize, TextAnchor alignment)
        {
            var child = parent.Find(name);
            if (child == null)
            {
                return CreateText(name, parent, fontSize, alignment);
            }

            var text = child.GetComponent<Text>() ?? child.gameObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = Color.white;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;
            if (child.GetComponent<Outline>() == null)
            {
                var outline = child.gameObject.AddComponent<Outline>();
                outline.effectColor = new Color32(0, 0, 0, 230);
                outline.effectDistance = new Vector2(2f, -2f);
            }

            return text;
        }

        private static Text FindExistingText(string name, Transform parent)
        {
            var child = parent.Find(name);
            return child == null ? null : child.GetComponent<Text>();
        }

        private static string FormatBoardGemText(UnitDefinition definition, UnitCardState card, UnitCardPresentationMode mode)
        {
            if (mode != UnitCardPresentationMode.Board || card == null)
            {
                return string.Empty;
            }

            var threshold = GetEvolveGemThreshold(definition, card);
            return threshold > 0
                ? $"\u25c6 {Mathf.Max(0, card.forestGemsAttached)}/{threshold}"
                : string.Empty;
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

        private static int GetEvolveGemThreshold(UnitDefinition definition, UnitCardState card)
        {
            var threshold = FindEvolveGemThreshold(card != null && card.isGolden ? definition?.goldTalents : definition?.talents);
            if (threshold > 0)
            {
                return threshold;
            }

            threshold = FindEvolveGemThreshold(definition?.talents);
            if (threshold > 0)
            {
                return threshold;
            }

            return 0;
        }

        private static int FindEvolveGemThreshold(SkillDefinition[] skills)
        {
            if (skills == null)
            {
                return 0;
            }

            for (var i = 0; i < skills.Length; i += 1)
            {
                var skill = skills[i];
                if (skill == null || string.IsNullOrWhiteSpace(skill.kind))
                {
                    continue;
                }

                if (skill.kind.Contains("receive_gift") && skill.kind.Contains("evolve") && skill.threshold > 0)
                {
                    return skill.threshold;
                }
            }

            return 0;
        }

        private static void Place(Text text, Vector2 offsetMin, Vector2 offsetMax, int fontSize, TextAnchor alignment)
        {
            if (text == null)
            {
                return;
            }

            var rect = text.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.resizeTextForBestFit = false;
        }

        private static void PlaceTop(Text text, float left, float top, float width, float height, int fontSize, TextAnchor alignment)
        {
            if (text == null)
            {
                return;
            }

            var rect = text.rectTransform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(left, -top);
            rect.sizeDelta = new Vector2(width, height);
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.resizeTextForBestFit = false;
        }

        private static void ConfigureBestFit(Text text, int minSize, int maxSize)
        {
            if (text == null)
            {
                return;
            }

            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = minSize;
            text.resizeTextMaxSize = Mathf.Max(minSize, maxSize);
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
        }

        private static float ResolveBoardSize(RectTransform cardRect, RectTransform parentRect, bool width)
        {
            if (parentRect != null)
            {
                var parentSize = width ? parentRect.rect.width : parentRect.rect.height;
                if (parentSize > 10f)
                {
                    return parentSize;
                }

                parentSize = width ? parentRect.sizeDelta.x : parentRect.sizeDelta.y;
                if (parentSize > 10f)
                {
                    return parentSize;
                }
            }

            if (cardRect != null)
            {
                var cardSize = width ? cardRect.rect.width : cardRect.rect.height;
                if (cardSize > 10f)
                {
                    return cardSize;
                }

                cardSize = width ? cardRect.sizeDelta.x : cardRect.sizeDelta.y;
                if (cardSize > 10f)
                {
                    return cardSize;
                }
            }

            return 166f;
        }

        private static void Stretch(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        private static void SetText(Text text, string value)
        {
            if (text != null)
            {
                text.text = value;
            }
        }

        private static void SetColor(Text text, Color color)
        {
            if (text != null)
            {
                text.color = color;
            }
        }

        private static void SetIcon(Image image, string unitName)
        {
            if (image == null)
            {
                return;
            }

            image.enabled = !string.IsNullOrWhiteSpace(unitName);
            RuntimeUnitIconCache.ApplyTo(image, unitName);
        }

        private static bool HasSpriteBorder(Sprite sprite)
        {
            if (sprite == null)
            {
                return false;
            }

            var border = sprite.border;
            return border.x > 0f || border.y > 0f || border.z > 0f || border.w > 0f;
        }
    }
}
