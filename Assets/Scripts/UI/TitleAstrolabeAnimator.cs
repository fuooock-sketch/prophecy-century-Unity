using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ProphecyCentury.UI
{
    /// <summary>
    /// 标题界面星盘动画控制器。
    /// 在 TitlePanel 上挂载此组件后自动驱动星盘元素的旋转、脉冲、闪烁和摆荡。
    /// </summary>
    public sealed class TitleAstrolabeAnimator : MonoBehaviour
    {
        [Header("Animation Toggles")]
        [SerializeField] private bool animateRing = true;
        [SerializeField] private bool animateGlow = true;
        [SerializeField] private bool animateStars = true;
        [SerializeField] private bool animateFateLines = true;
        [SerializeField] private bool animateTitleText = true;

        [Header("Ring (outer)")]
        [SerializeField] private float ringRotationSpeed = 6f;

        [Header("Glow (inner oracle)")]
        [SerializeField] private float glowPulseMin = 0.95f;
        [SerializeField] private float glowPulseMax = 1.05f;
        [SerializeField] private float glowPulsePeriod = 3f;

        [Header("Stars")]
        [SerializeField] private float starBlinkMinInterval = 2f;
        [SerializeField] private float starBlinkMaxInterval = 5f;
        [SerializeField] private float starBlinkMinAlpha = 0.2f;
        [SerializeField] private float starBlinkMaxAlpha = 1f;
        [SerializeField] private float starBlinkTransitionDuration = 0.4f;

        [Header("Fate Lines")]
        [SerializeField] private float fateLineSwingAngle = 3f;
        [SerializeField] private float fateLineSwingPeriod = 8f;

        [Header("Title Text")]
        [SerializeField] private float titleBreatheMin = 0.97f;
        [SerializeField] private float titleBreatheMax = 1.03f;
        [SerializeField] private float titleBreathePeriod = 4f;

        private RectTransform _ringOuter;
        private RectTransform _glowTransform;
        private Image _glowImage;
        private readonly List<StarState> _stars = new List<StarState>();
        private readonly List<FateLineState> _fateLines = new List<FateLineState>();
        private RectTransform _titleTextRect;
        private RectTransform _titleShadowRect;

        private struct StarState
        {
            public Image Image;
            public float NextBlinkTime;
            public float BlinkPhase;
            public float TargetAlpha;
            public float CurrentAlpha;
        }

        private struct FateLineState
        {
            public RectTransform Rect;
            public float BaseRotation;
        }

        private void Awake()
        {
            CacheElements();
        }

        private void CacheElements()
        {
            var ringObj = FindDeepChildByName(transform, "RingOuter");
            if (ringObj != null)
            {
                _ringOuter = ringObj.GetComponent<RectTransform>();
            }

            var glowObj = FindDeepChildByName(transform, "InnerOracleGlow");
            if (glowObj != null)
            {
                _glowTransform = glowObj.GetComponent<RectTransform>();
                _glowImage = glowObj.GetComponent<Image>();
            }

            foreach (var star in FindAllDeepChildrenByNamePrefix(transform, "StarPoint"))
            {
                var img = star.GetComponent<Image>();
                if (img != null)
                {
                    _stars.Add(new StarState
                    {
                        Image = img,
                        NextBlinkTime = Random.Range(0f, starBlinkMaxInterval),
                        BlinkPhase = Random.Range(0f, 1f),
                        TargetAlpha = 1f,
                        CurrentAlpha = img.color.a
                    });
                }
            }

            foreach (var line in FindAllDeepChildrenByNamePrefix(transform, "FateLine"))
            {
                var rect = line.GetComponent<RectTransform>();
                if (rect != null)
                {
                    _fateLines.Add(new FateLineState
                    {
                        Rect = rect,
                        BaseRotation = rect.localRotation.eulerAngles.z
                    });
                }
            }

            var titleObj = FindDeepChildByName(transform, "TitleText");
            if (titleObj != null)
            {
                _titleTextRect = titleObj.GetComponent<RectTransform>();
            }

            var shadowObj = FindDeepChildByName(transform, "TitleTextGlow");
            if (shadowObj != null)
            {
                _titleShadowRect = shadowObj.GetComponent<RectTransform>();
            }
        }

        private void Update()
        {
            AnimateRing();
            AnimateGlow();
            AnimateStars();
            AnimateFateLines();
            AnimateTitleText();
        }

        private void AnimateRing()
        {
            if (!animateRing || _ringOuter == null) return;
            _ringOuter.localRotation = Quaternion.Euler(0f, 0f, Time.time * ringRotationSpeed);
        }

        private void AnimateGlow()
        {
            if (!animateGlow || _glowTransform == null) return;
            var t = (Mathf.Sin(Time.time * 2f * Mathf.PI / glowPulsePeriod) + 1f) * 0.5f;
            var scale = Mathf.Lerp(glowPulseMin, glowPulseMax, t);
            _glowTransform.localScale = new Vector3(scale, scale, 1f);

            if (_glowImage != null)
            {
                var alpha = Mathf.Lerp(0.5f, 1f, t);
                var color = _glowImage.color;
                color.a = alpha;
                _glowImage.color = color;
            }
        }

        private void AnimateStars()
        {
            if (!animateStars) return;
            for (var i = 0; i < _stars.Count; i++)
            {
                var star = _stars[i];
                if (star.Image == null) continue;

                if (Time.time >= star.NextBlinkTime)
                {
                    star.TargetAlpha = star.TargetAlpha > 0.8f ? starBlinkMinAlpha : starBlinkMaxAlpha;
                    star.NextBlinkTime = Time.time + Random.Range(starBlinkMinInterval, starBlinkMaxInterval);
                }

                star.CurrentAlpha = Mathf.MoveTowards(star.CurrentAlpha, star.TargetAlpha,
                    Time.deltaTime / starBlinkTransitionDuration);
                var color = star.Image.color;
                color.a = star.CurrentAlpha;
                star.Image.color = color;
                _stars[i] = star;
            }
        }

        private void AnimateFateLines()
        {
            if (!animateFateLines) return;
            var angle = Mathf.Sin(Time.time * 2f * Mathf.PI / fateLineSwingPeriod) * fateLineSwingAngle;
            for (var i = 0; i < _fateLines.Count; i += 1)
            {
                var line = _fateLines[i];
                if (line.Rect == null) continue;
                line.Rect.localRotation = Quaternion.Euler(0f, 0f, line.BaseRotation + angle * (i % 2 == 0 ? 1f : -1f));
            }
        }

        private void AnimateTitleText()
        {
            if (!animateTitleText) return;
            var t = (Mathf.Sin(Time.time * 2f * Mathf.PI / titleBreathePeriod) + 1f) * 0.5f;
            var scale = Mathf.Lerp(titleBreatheMin, titleBreatheMax, t);

            if (_titleTextRect != null)
            {
                _titleTextRect.localScale = new Vector3(scale, scale, 1f);
            }

            if (_titleShadowRect != null)
            {
                _titleShadowRect.localScale = new Vector3(scale * 1.02f, scale * 1.02f, 1f);
            }
        }

        private static Transform FindDeepChildByName(Transform parent, string name)
        {
            for (var i = 0; i < parent.childCount; i += 1)
            {
                var child = parent.GetChild(i);
                if (child.name == name) return child;
                var found = FindDeepChildByName(child, name);
                if (found != null) return found;
            }
            return null;
        }

        private static List<Transform> FindAllDeepChildrenByNamePrefix(Transform parent, string prefix)
        {
            var results = new List<Transform>();
            CollectByNamePrefix(parent, prefix, results);
            return results;
        }

        private static void CollectByNamePrefix(Transform parent, string prefix, List<Transform> results)
        {
            for (var i = 0; i < parent.childCount; i += 1)
            {
                var child = parent.GetChild(i);
                if (child.name.StartsWith(prefix, System.StringComparison.Ordinal))
                {
                    results.Add(child);
                }
                CollectByNamePrefix(child, prefix, results);
            }
        }
    }
}