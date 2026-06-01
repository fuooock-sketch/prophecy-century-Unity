#!/usr/bin/env python3
"""Fix RuntimeUnitTooltip.cs: add layout rebuild and adjacent placement logic."""

import os

path = os.path.join(os.path.dirname(__file__), '..', 'Assets', 'Scripts', 'UI', 'RuntimeUnitTooltip.cs')
path = os.path.normpath(path)

with open(path, 'r', encoding='utf-8') as f:
    content = f.read()

# --- Change 1: Add LayoutRebuilder line before Move(eventData) in Show() ---
old_show = '            Canvas.ForceUpdateCanvases();\n            Move(eventData);'
new_show = '            Canvas.ForceUpdateCanvases();\n            LayoutRebuilder.ForceRebuildLayoutImmediate(_panelRect);\n            Move(eventData);'

if old_show in content:
    content = content.replace(old_show, new_show)
    print('[OK] Added LayoutRebuilder.ForceRebuildLayoutImmediate')
else:
    print('[WARN] Show() pattern NOT FOUND - trying partial match')
    # Try finding just the unique surrounding context
    if 'Canvas.ForceUpdateCanvases();' in content and 'Move(eventData);' in content:
        print('[INFO] Both lines exist individually')

# --- Change 2: Replace the entire Move method ---
old_move_marker = '        private static void Move(PointerEventData eventData)'
new_move_body = '''        private static void Move(PointerEventData eventData)
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
            var cs = new (float left, float top)[]
            {
                (srcR, srcT),
                (srcL, srcB),
                (srcL - panelW, srcT),
                (srcL, srcT + panelH),
            };

            foreach (var c in cs)
            {
                float pl = c.left;
                float pr = c.left + panelW;
                float pt = c.top;
                float pb = c.top - panelH;

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
        }'''

# Find the old Move method and the method after it (Hide)
if old_move_marker in content:
    hide_marker = '        private static void Hide()'
    start_idx = content.index(old_move_marker)
    end_idx = content.index(hide_marker, start_idx + 1)
    
    before = content[:start_idx]
    after = content[end_idx:]
    content = before + new_move_body + '\n' + after
    print('[OK] Replaced Move() method')
else:
    print('[ERROR] Move method marker NOT FOUND')

with open(path, 'w', encoding='utf-8', newline='') as f:
    f.write(content)

print('[DONE] File written successfully')