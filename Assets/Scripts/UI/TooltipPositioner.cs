using System;
using UnityEngine;

namespace ProphecyCentury.UI
{
    internal static class TooltipPositioner
    {
        internal const float PointerOffsetX = 24f;
        internal const float PointerOffsetY = -24f;
        internal const float ScreenMargin = 12f;

        internal static void Calculate(
            float pointerX,
            float pointerY,
            float panelWidth,
            float panelHeight,
            float screenWidth,
            float screenHeight,
            out float x,
            out float y,
            out float visualWidth,
            out float visualHeight,
            out float scale)
        {
            Calculate(pointerX, pointerY, panelWidth, panelHeight, screenWidth, screenHeight, null, out x, out y, out visualWidth, out visualHeight, out scale);
        }

        /// <summary>
        /// Calculate tooltip position, avoiding overlap with the source element when provided.
        /// Tooltip pivot is (0, 1) — top-left.
        /// Candidates tried in priority: right → below → left → above.
        /// Falls back to pointer-relative positioning if no adjacency works.
        /// </summary>
        internal static void Calculate(
            float pointerX,
            float pointerY,
            float panelWidth,
            float panelHeight,
            float screenWidth,
            float screenHeight,
            RectTransform sourceRectTransform,
            out float x,
            out float y,
            out float visualWidth,
            out float visualHeight,
            out float scale)
        {
            var availableHeight = Math.Max(1f, screenHeight - ScreenMargin * 2f);
            var availableWidth = Math.Max(1f, screenWidth - ScreenMargin * 2f);
            var naturalHeight = Math.Max(1f, panelHeight);
            var naturalWidth = Math.Max(1f, panelWidth);
            var scaleByHeight = availableHeight / naturalHeight;
            var scaleByWidth = availableWidth / naturalWidth;
            scale = Math.Min(1f, Math.Min(scaleByHeight, scaleByWidth));
            visualWidth = naturalWidth * scale;
            visualHeight = naturalHeight * scale;

            // If we know the source element, try to place tooltip adjacent to it without overlap
            if (sourceRectTransform != null)
            {
                if (TryPlaceAdjacentToSource(sourceRectTransform, visualWidth, visualHeight,
                        screenWidth, screenHeight, out x, out y))
                {
                    return;
                }
            }

            // Fallback: pointer-relative positioning with screen clamping
            x = pointerX + PointerOffsetX;
            y = pointerY + PointerOffsetY;

            x = Math.Min(x, screenWidth - visualWidth - ScreenMargin);
            x = Math.Max(x, ScreenMargin);

            y = Math.Min(y, screenHeight - ScreenMargin);
            y = Math.Max(y, visualHeight + ScreenMargin);
        }

        private static bool TryPlaceAdjacentToSource(
            RectTransform sourceRect,
            float panelW, float panelH,
            float screenW, float screenH,
            out float outX, out float outY)
        {
            outX = 0f;
            outY = 0f;

            // Get source element screen-space bounds
            Vector3[] corners = new Vector3[4];
            sourceRect.GetWorldCorners(corners);
            // corners: [0]=bottom-left, [1]=top-left, [2]=top-right, [3]=bottom-right
            float srcLeft = corners[0].x;
            float srcBottom = corners[0].y;
            float srcRight = corners[2].x;
            float srcTop = corners[1].y;
            Rect srcRect = new Rect(srcLeft, srcBottom, srcRight - srcLeft, srcTop - srcBottom);

            // Define candidate positions: (tooltipLeft, tooltipTop)
            // Tooltip pivot (0,1) maps tooltip's top-left to this point.
            // We try: right-edge, below, left-edge, above (in preference order)
            var candidates = new (float left, float top)[]
            {
                // Right of source: tooltip top-left at (srcRight, srcTop)
                (srcRight, srcTop),
                // Below source: tooltip top-left at (srcLeft, srcBottom - panelH)
                (srcLeft, srcBottom),
                // Left of source: tooltip top-right at (srcLeft - panelW, srcTop)
                (srcLeft - panelW, srcTop),
                // Above source: tooltip bottom-left at (srcLeft, srcTop + panelH)
                (srcLeft, srcTop + panelH),
            };

            foreach (var c in candidates)
            {
                float panelLeft = c.left;
                float panelRight = c.left + panelW;
                float panelTop = c.top;
                float panelBottom = c.top - panelH;

                // Check screen fit
                if (panelLeft < ScreenMargin || panelRight > screenW - ScreenMargin ||
                    panelBottom < ScreenMargin || panelTop > screenH - ScreenMargin)
                {
                    continue;
                }

                // Check no overlap with source
                Rect panelRect = new Rect(panelLeft, panelBottom, panelW, panelH);
                if (!srcRect.Overlaps(panelRect))
                {
                    outX = panelLeft;
                    outY = panelTop;
                    return true;
                }
            }

            return false;
        }
    }
}
