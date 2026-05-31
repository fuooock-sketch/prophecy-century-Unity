import os
path = r'd:\Prophecy-Century\GameProject\prophecy_century\prophecy-century-Unity\Assets\Scripts\UI\RuntimeUnitTooltip.cs'
with open(path, 'r', encoding='utf-8') as f:
    content = f.read()

# Check current state
print('Has LayoutRebuilder:', 'LayoutRebuilder' in content)
print('Has TryPlacePanelAdjacent:', 'TryPlacePanelAdjacent' in content)
print('Has _sourceRect:', '_sourceRect' in content)

# 1. Replace Show method body to add LayoutRebuilder
old_show_anchor = '            Canvas.ForceUpdateCanvases();\n            Move(eventData);'
new_show_anchor = '            Canvas.ForceUpdateCanvases();\n            LayoutRebuilder.ForceRebuildLayoutImmediate(_panelRect);\n            Move(eventData);'
if old_show_anchor in content:
    content = content.replace(old_show_anchor, new_show_anchor)
    print('OK: LayoutRebuilder added')
else:
    print('FAIL: could not find Show anchor')

# 2. Replace Move method with new implementation + TryPlacePanelAdjacent
old_move_start = '\n        private static void Move(PointerEventData eventData)\n        {\n'
end_marker = '        private static void Hide()'

if old_move_start in content and end_marker in content:
    idx_start = content.index(old_move_start)
    idx_end = content.index(end_marker, idx_start + 1)

    new_methods = old_move_start + '''            if (_panelRect == null || !_panel.activeSelf || eventData == null)
            {
                return;
            }

            float pw = _panelRect.rect.width > 10f ? _panelRect.rect.width : PanelWidth;
            float rawH = _panelRect.rect.height;
            float ph = rawH > 10f ? rawH : 500f;

            float availH = Mathf.Max(1f, Screen.height - TooltipPositioner.ScreenMargin * 2f);
            float s = Mathf.Min(1f, availH / Mathf.Max(1f, ph));
            _panelRect.localScale = new Vector3(s, s, 1f);
            float visualW = pw * s;
            float visualH = ph * s;

            if (_sourceRect != null && TryPlacePanelAdjacent(_sourceRect, visualW, visualH, out float ox, out float oy))
            {
                _panelRect.position = new Vector2(ox, oy);
                return;
            }

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

'''

    before = content[:idx_start]
    after = content[idx_end:]
    content = before + new_methods + after
    print('OK: Move() replaced')
else:
    print('FAIL: could not find Move() markers')

with open(path, 'w', encoding='utf-8', newline='\n') as f:
    f.write(content)
print('DONE')