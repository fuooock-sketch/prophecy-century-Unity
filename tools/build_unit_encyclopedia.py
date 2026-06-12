#!/usr/bin/env python3
"""Build the local static unit encyclopedia page."""

from __future__ import annotations

import json
import shutil
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
UNIT_DATA_PATH = ROOT / "Assets" / "Resources" / "Data" / "unit_data.json"
UNIT_ICON_SOURCE_DIR = ROOT / "Assets" / "Art" / "icon" / "unit"
OUTPUT_DIR = ROOT / "web" / "unit-encyclopedia"
ICON_OUTPUT_DIR = OUTPUT_DIR / "assets" / "unit-icons"
HTML_OUTPUT_PATH = OUTPUT_DIR / "index.html"


def normalize_text(value):
    return "" if value is None else str(value)


def load_units():
    with UNIT_DATA_PATH.open("r", encoding="utf-8") as handle:
        units = json.load(handle)
    return sorted(
        (unit for unit in units if unit and not unit.get("hidden", False)),
        key=lambda unit: (
            int(unit.get("star") or 0),
            normalize_text(unit.get("race")),
            normalize_text(unit.get("faith")),
            normalize_text(unit.get("name")),
        ),
    )


def copy_icon(unit):
    name = normalize_text(unit.get("name"))
    source = UNIT_ICON_SOURCE_DIR / f"{name}.png"
    if not name or not source.exists():
        return None

    target_name = f"{unit.get('id') or name}.png"
    target = ICON_OUTPUT_DIR / target_name
    try:
        shutil.copy2(source, target)
    except PermissionError:
        if not target.exists():
            return None
    return f"assets/unit-icons/{target_name}"


def compact_unit(unit, icon_path):
    return {
        "id": unit.get("id"),
        "name": unit.get("name"),
        "star": unit.get("star") or 1,
        "race": unit.get("race") or "无",
        "faith": unit.get("faith") or "无",
        "type": unit.get("type") or "",
        "typeLabel": unit.get("typeLabel") or unit.get("type") or "",
        "tags": unit.get("tags") or [],
        "icon": icon_path,
        "stats": {
            "hpPerUnit": unit.get("hpPerUnit") or unit.get("hp") or 0,
            "attack": unit.get("attack") or 0,
            "defense": unit.get("defense") or 0,
            "power": unit.get("power") or 0,
            "speed": unit.get("speed") or 0,
            "luck": unit.get("luck") or 0,
            "morale": unit.get("morale") or 0,
            "range": unit.get("attackRange") or unit.get("range") or 0,
            "damageMin": unit.get("damageMin") or 0,
            "damageMax": unit.get("damageMax") or 0,
            "startCount": unit.get("startCount") or unit.get("defaultCount") or unit.get("baseCount") or 0,
            "limit": unit.get("limit") or 0,
            "attackInterval": unit.get("attackInterval") or 0,
        },
        "texts": {
            "talent": unit.get("talentText") or "—",
            "goldTalent": unit.get("goldTalentText") or "—",
            "battle": unit.get("battleText") or "—",
            "goldBattle": unit.get("goldBattleText") or "—",
        },
    }


def build_html(units):
    data_json = json.dumps(units, ensure_ascii=False, separators=(",", ":"))
    return f"""<!doctype html>
<html lang="zh-CN">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <title>预言世纪 单位图鉴</title>
  <style>
    :root {{
      color-scheme: dark;
      --bg: #10131a;
      --panel: #171c25;
      --panel-2: #202733;
      --line: #323b4b;
      --text: #edf2f7;
      --muted: #aeb8c7;
      --accent: #e5b85a;
      --good: #70d6a1;
      --danger: #ee6b6e;
      --blue: #79b8ff;
    }}
    * {{ box-sizing: border-box; }}
    body {{
      margin: 0;
      min-height: 100vh;
      font-family: "Microsoft YaHei", "PingFang SC", "Noto Sans CJK SC", Arial, sans-serif;
      background: var(--bg);
      color: var(--text);
    }}
    button, input, select {{ font: inherit; }}
    .app {{
      display: grid;
      grid-template-columns: minmax(0, 1fr) 420px;
      min-height: 100vh;
    }}
    .main {{
      padding: 22px;
      min-width: 0;
    }}
    .topbar {{
      display: grid;
      grid-template-columns: minmax(260px, 1fr) repeat(4, minmax(120px, 160px));
      gap: 10px;
      align-items: center;
      margin-bottom: 16px;
    }}
    .search, .select {{
      width: 100%;
      height: 42px;
      color: var(--text);
      background: var(--panel);
      border: 1px solid var(--line);
      border-radius: 6px;
      padding: 0 12px;
      outline: none;
    }}
    .search:focus, .select:focus {{ border-color: var(--accent); }}
    .summary {{
      display: flex;
      gap: 10px;
      align-items: center;
      margin-bottom: 14px;
      color: var(--muted);
      font-size: 14px;
    }}
    .pill {{
      display: inline-flex;
      align-items: center;
      height: 26px;
      padding: 0 10px;
      border: 1px solid var(--line);
      border-radius: 999px;
      color: var(--muted);
      background: rgba(255,255,255,0.03);
    }}
    .grid {{
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(210px, 1fr));
      gap: 12px;
    }}
    .card {{
      position: relative;
      display: grid;
      grid-template-columns: 72px minmax(0, 1fr);
      gap: 10px;
      min-height: 118px;
      padding: 12px;
      border: 1px solid var(--line);
      border-radius: 8px;
      background: var(--panel);
      cursor: pointer;
      text-align: left;
      color: inherit;
    }}
    .card:hover, .card.active {{
      border-color: var(--accent);
      background: #202638;
    }}
    .portrait {{
      width: 72px;
      height: 72px;
      border-radius: 8px;
      background: #0c0f15;
      border: 1px solid var(--line);
      object-fit: contain;
    }}
    .placeholder {{
      display: grid;
      place-items: center;
      font-size: 24px;
      color: var(--muted);
    }}
    .name-row {{
      display: flex;
      gap: 8px;
      align-items: baseline;
      min-width: 0;
    }}
    .name {{
      overflow: hidden;
      text-overflow: ellipsis;
      white-space: nowrap;
      font-size: 18px;
      font-weight: 700;
    }}
    .stars {{
      color: var(--accent);
      font-size: 13px;
      white-space: nowrap;
    }}
    .meta {{
      margin-top: 4px;
      color: var(--muted);
      font-size: 13px;
      white-space: nowrap;
      overflow: hidden;
      text-overflow: ellipsis;
    }}
    .mini-stats {{
      display: grid;
      grid-template-columns: repeat(3, 1fr);
      gap: 5px;
      margin-top: 10px;
      font-size: 12px;
      color: var(--muted);
    }}
    .mini-stats b {{ color: var(--text); font-weight: 600; }}
    .detail {{
      position: sticky;
      top: 0;
      height: 100vh;
      overflow: auto;
      border-left: 1px solid var(--line);
      background: #111720;
      padding: 22px;
    }}
    .detail-head {{
      display: grid;
      grid-template-columns: 112px minmax(0, 1fr);
      gap: 16px;
      align-items: center;
      margin-bottom: 18px;
    }}
    .detail .portrait {{
      width: 112px;
      height: 112px;
    }}
    .detail h1 {{
      margin: 0 0 8px;
      font-size: 28px;
      line-height: 1.15;
    }}
    .segmented {{
      display: inline-grid;
      grid-template-columns: 1fr 1fr;
      border: 1px solid var(--line);
      border-radius: 7px;
      overflow: hidden;
      margin: 4px 0 18px;
    }}
    .segmented button {{
      height: 34px;
      min-width: 82px;
      border: 0;
      color: var(--muted);
      background: var(--panel);
      cursor: pointer;
    }}
    .segmented button.active {{
      color: #181203;
      background: var(--accent);
      font-weight: 700;
    }}
    .stat-grid {{
      display: grid;
      grid-template-columns: repeat(3, 1fr);
      gap: 8px;
      margin-bottom: 18px;
    }}
    .stat {{
      padding: 9px 10px;
      border: 1px solid var(--line);
      border-radius: 7px;
      background: var(--panel);
    }}
    .stat span {{
      display: block;
      color: var(--muted);
      font-size: 12px;
      margin-bottom: 4px;
    }}
    .stat b {{
      font-size: 18px;
      font-weight: 700;
    }}
    .section {{
      margin-top: 12px;
      padding: 13px;
      border: 1px solid var(--line);
      border-radius: 8px;
      background: var(--panel);
    }}
    .section h2 {{
      margin: 0 0 8px;
      font-size: 15px;
      color: var(--accent);
    }}
    .section p {{
      margin: 0;
      color: var(--text);
      line-height: 1.55;
      white-space: pre-wrap;
    }}
    .tags {{
      display: flex;
      flex-wrap: wrap;
      gap: 6px;
      margin-top: 10px;
    }}
    .tag {{
      padding: 3px 8px;
      border-radius: 999px;
      background: var(--panel-2);
      color: var(--muted);
      font-size: 12px;
    }}
    .empty {{
      padding: 48px 16px;
      text-align: center;
      color: var(--muted);
      border: 1px dashed var(--line);
      border-radius: 8px;
    }}
    @media (max-width: 1050px) {{
      .app {{ grid-template-columns: 1fr; }}
      .detail {{
        position: static;
        height: auto;
        border-left: 0;
        border-top: 1px solid var(--line);
      }}
      .topbar {{ grid-template-columns: 1fr 1fr; }}
      .search {{ grid-column: 1 / -1; }}
    }}
  </style>
</head>
<body>
  <div class="app">
    <main class="main">
      <div class="topbar">
        <input id="search" class="search" placeholder="搜索名称、种族、信仰、技能..." autocomplete="off">
        <select id="starFilter" class="select"></select>
        <select id="raceFilter" class="select"></select>
        <select id="faithFilter" class="select"></select>
        <select id="typeFilter" class="select"></select>
      </div>
      <div class="summary">
        <span class="pill" id="countPill"></span>
        <span class="pill">点击卡片查看详情</span>
      </div>
      <section id="grid" class="grid"></section>
    </main>
    <aside id="detail" class="detail"></aside>
  </div>
  <script>
    const UNITS = {data_json};
    let selectedId = UNITS[0]?.id || null;
    let mode = "normal";

    const search = document.getElementById("search");
    const filters = {{
      star: document.getElementById("starFilter"),
      race: document.getElementById("raceFilter"),
      faith: document.getElementById("faithFilter"),
      type: document.getElementById("typeFilter"),
    }};
    const grid = document.getElementById("grid");
    const detail = document.getElementById("detail");
    const countPill = document.getElementById("countPill");

    function unique(key) {{
      return [...new Set(UNITS.map(unit => unit[key]).filter(Boolean))].sort((a, b) => String(a).localeCompare(String(b), "zh-Hans-CN"));
    }}

    function setupFilter(select, label, values) {{
      select.innerHTML = `<option value="">${{label}}</option>` + values.map(value => `<option value="${{escapeAttr(value)}}">${{escapeHtml(value)}}</option>`).join("");
      select.addEventListener("change", render);
    }}

    function init() {{
      setupFilter(filters.star, "全部星级", unique("star").map(value => `${{value}} 星`));
      setupFilter(filters.race, "全部种族", unique("race"));
      setupFilter(filters.faith, "全部信仰", unique("faith"));
      setupFilter(filters.type, "全部类型", unique("type").map(formatType));
      search.addEventListener("input", render);
      render();
    }}

    function filteredUnits() {{
      const q = search.value.trim().toLowerCase();
      return UNITS.filter(unit => {{
        if (filters.star.value && `${{unit.star}} 星` !== filters.star.value) return false;
        if (filters.race.value && unit.race !== filters.race.value) return false;
        if (filters.faith.value && unit.faith !== filters.faith.value) return false;
        if (filters.type.value && formatType(unit.type) !== filters.type.value) return false;
        if (!q) return true;
        const haystack = [
          unit.name, unit.id, unit.race, unit.faith, unit.typeLabel, unit.type,
          unit.texts.talent, unit.texts.goldTalent, unit.texts.battle, unit.texts.goldBattle,
          ...(unit.tags || [])
        ].join(" ").toLowerCase();
        return haystack.includes(q);
      }});
    }}

    function render() {{
      const units = filteredUnits();
      if (!units.some(unit => unit.id === selectedId)) selectedId = units[0]?.id || UNITS[0]?.id || null;
      countPill.textContent = `显示 ${{units.length}} / ${{UNITS.length}} 个单位`;
      grid.innerHTML = units.length ? units.map(renderCard).join("") : `<div class="empty">没有匹配的单位</div>`;
      grid.querySelectorAll(".card").forEach(card => {{
        card.addEventListener("click", () => {{
          selectedId = card.dataset.id;
          render();
        }});
      }});
      renderDetail(UNITS.find(unit => unit.id === selectedId) || units[0] || UNITS[0]);
    }}

    function renderCard(unit) {{
      const stats = unit.stats;
      const active = unit.id === selectedId ? " active" : "";
      return `<button class="card${{active}}" data-id="${{escapeAttr(unit.id)}}">
        ${{renderPortrait(unit, "portrait")}}
        <div>
          <div class="name-row">
            <span class="name">${{escapeHtml(unit.name)}}</span>
            <span class="stars">${{"★".repeat(Number(unit.star) || 1)}}</span>
          </div>
          <div class="meta">${{escapeHtml(unit.race)}} / ${{escapeHtml(unit.faith)}} / ${{escapeHtml(unit.typeLabel || formatType(unit.type))}}</div>
          <div class="mini-stats">
            <span>数量 <b>${{stats.startCount}}</b></span>
            <span>血量 <b>${{stats.hpPerUnit}}</b></span>
            <span>攻击 <b>${{stats.attack}}</b></span>
            <span>防御 <b>${{stats.defense}}</b></span>
            <span>速度 <b>${{stats.speed}}</b></span>
            <span>射程 <b>${{stats.range}}</b></span>
          </div>
        </div>
      </button>`;
    }}

    function renderDetail(unit) {{
      if (!unit) {{
        detail.innerHTML = `<div class="empty">没有单位数据</div>`;
        return;
      }}
      const stats = unit.stats;
      const talent = mode === "gold" ? unit.texts.goldTalent : unit.texts.talent;
      const battle = mode === "gold" ? unit.texts.goldBattle : unit.texts.battle;
      detail.innerHTML = `
        <div class="detail-head">
          ${{renderPortrait(unit, "portrait")}}
          <div>
            <h1>${{escapeHtml(unit.name)}}</h1>
            <div class="stars">${{"★".repeat(Number(unit.star) || 1)}}</div>
            <div class="meta">${{escapeHtml(unit.race)}} / ${{escapeHtml(unit.faith)}} / ${{escapeHtml(unit.typeLabel || formatType(unit.type))}}</div>
            ${{renderTags(unit)}}
          </div>
        </div>
        <div class="segmented">
          <button class="${{mode === "normal" ? "active" : ""}}" data-mode="normal">普通</button>
          <button class="${{mode === "gold" ? "active" : ""}}" data-mode="gold">金色</button>
        </div>
        <div class="stat-grid">
          ${{stat("初始数量", stats.startCount)}}
          ${{stat("单体血量", stats.hpPerUnit)}}
          ${{stat("攻击", stats.attack)}}
          ${{stat("防御", stats.defense)}}
          ${{stat("力量", stats.power)}}
          ${{stat("速度", stats.speed)}}
          ${{stat("幸运", stats.luck)}}
          ${{stat("士气", stats.morale)}}
          ${{stat("射程", stats.range)}}
          ${{stat("伤害", `${{stats.damageMin}}-${{stats.damageMax}}`)}}
          ${{stat("攻速", stats.attackInterval)}}
          ${{stat("上限", stats.limit || "—")}}
        </div>
        <div class="section">
          <h2>${{mode === "gold" ? "金色经营技能" : "经营技能"}}</h2>
          <p>${{escapeHtml(cleanText(talent))}}</p>
        </div>
        <div class="section">
          <h2>${{mode === "gold" ? "金色战斗技能" : "战斗技能"}}</h2>
          <p>${{escapeHtml(cleanText(battle))}}</p>
        </div>`;
      detail.querySelectorAll("[data-mode]").forEach(button => {{
        button.addEventListener("click", () => {{
          mode = button.dataset.mode;
          renderDetail(unit);
        }});
      }});
    }}

    function renderPortrait(unit, className) {{
      if (unit.icon) return `<img class="${{className}}" src="${{escapeAttr(unit.icon)}}" alt="${{escapeAttr(unit.name)}}">`;
      return `<div class="${{className}} placeholder">${{escapeHtml((unit.name || "?").slice(0, 1))}}</div>`;
    }}

    function renderTags(unit) {{
      const tags = unit.tags || [];
      if (!tags.length) return "";
      return `<div class="tags">${{tags.map(tag => `<span class="tag">${{escapeHtml(tag)}}</span>`).join("")}}</div>`;
    }}

    function stat(label, value) {{
      return `<div class="stat"><span>${{escapeHtml(label)}}</span><b>${{escapeHtml(value)}}</b></div>`;
    }}

    function cleanText(text) {{
      const value = String(text || "—").trim();
      return value && value !== "—" ? value : "—";
    }}

    function formatType(type) {{
      if (type === "melee") return "近战";
      if (type === "range") return "远程";
      return type || "无";
    }}

    function escapeHtml(value) {{
      return String(value ?? "").replace(/[&<>"']/g, char => ({{"&":"&amp;","<":"&lt;",">":"&gt;",'"':"&quot;","'":"&#39;"}}[char]));
    }}

    function escapeAttr(value) {{
      return escapeHtml(value);
    }}

    init();
  </script>
</body>
</html>
"""


def main():
    units = load_units()
    OUTPUT_DIR.mkdir(parents=True, exist_ok=True)
    ICON_OUTPUT_DIR.mkdir(parents=True, exist_ok=True)
    for old_icon in ICON_OUTPUT_DIR.glob("*.png"):
        try:
            old_icon.unlink()
        except PermissionError:
            pass

    compact_units = []
    missing_icons = 0
    for unit in units:
        icon_path = copy_icon(unit)
        missing_icons += 0 if icon_path else 1
        compact_units.append(compact_unit(unit, icon_path))

    HTML_OUTPUT_PATH.write_text(build_html(compact_units), encoding="utf-8")
    print(f"Built {HTML_OUTPUT_PATH.relative_to(ROOT)}")
    print(f"Units: {len(compact_units)}, missing icons: {missing_icons}")


if __name__ == "__main__":
    main()
