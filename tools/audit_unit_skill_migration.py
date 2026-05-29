import json
import os
import re
import sys

import import_unit_excel


EXCEL_PATH = r"C:\projectZhongxu\excel\unit_202605250127_数量修订版02_攻防血压缩版.xlsx"
UNIT_JSON_PATH = r"Assets\Resources\Data\unit_data.json"
OUTPUT_PATH = os.path.join("docs", "UNIT_SKILL_MIGRATION_HEATMAP.md")

STATUS_NOT_STARTED = "⬜"
STATUS_TEXT = "🟨"
STATUS_PARTIAL = "🟧"
STATUS_DONE = "🟩"
STATUS_BLOCKED = "🟥"
STATUS_NA = "-"


MECHANISMS = [
    ("入场触发", r"入场"),
    ("回合开始", r"回合开始"),
    ("回合结束", r"回合结束"),
    ("获得数量", r"数量|获得.*默认数量|当前数量"),
    ("密林宝钻", r"密林宝钻"),
    ("林地标记", r"林地标记"),
    ("商店/手牌/发现", r"商店|卡牌|发现|金币"),
    ("吞噬/复制", r"吞噬|复制"),
    ("进阶/变身", r"进阶|变成|变为"),
    ("开战触发", r"开战"),
    ("攻击触发", r"攻击"),
    ("受伤触发", r"受伤|受到伤害"),
    ("死亡触发", r"死亡|阵亡"),
    ("召唤", r"召唤"),
    ("护盾", r"护盾"),
    ("控制/位移", r"眩晕|锁住|无法移动|瞬移"),
    ("范围伤害", r"范围|半径|火雨"),
    ("士气/运气/暴击", r"士气|运气|暴击|追击"),
]


MECHANISM_LOGIC_STATUS = {
    "获得数量": STATUS_PARTIAL,
    "密林宝钻": STATUS_PARTIAL,
    "入场触发": STATUS_PARTIAL,
    "回合开始": STATUS_PARTIAL,
    "回合结束": STATUS_PARTIAL,
    "商店/手牌/发现": STATUS_PARTIAL,
    "吞噬/复制": STATUS_PARTIAL,
    "士气/运气/暴击": STATUS_DONE,
    "开战触发": STATUS_PARTIAL,
    "攻击触发": STATUS_PARTIAL,
    "护盾": STATUS_PARTIAL,
    "范围伤害": STATUS_PARTIAL,
}


def read_text(path):
    with open(path, encoding="utf-8") as handle:
        return handle.read()


def clean_text(value):
    if value is None:
        return ""
    text = str(value).strip()
    return "" if text in {"", "-", "—"} else text


def classify(text):
    matches = []
    for name, pattern in MECHANISMS:
        if re.search(pattern, text):
            matches.append(name)
    return matches


def load_excel_units(path):
    rows = import_unit_excel.read_sheet(path, 1)
    units = []
    for row in rows[2:]:
        name = clean_text(row.get(1))
        if not name:
            continue
        talent = import_unit_excel.sanitize_skill_text(clean_text(row.get(21)))
        gold_talent = import_unit_excel.sanitize_skill_text(clean_text(row.get(22)))
        battle = import_unit_excel.sanitize_skill_text(clean_text(row.get(23)))
        gold_battle = import_unit_excel.sanitize_skill_text(clean_text(row.get(24)))
        texts = [talent, gold_talent, battle, gold_battle]
        joined = "\n".join(text for text in texts if text)
        units.append(
            {
                "name": name,
                "talent": talent,
                "goldTalent": gold_talent,
                "battle": battle,
                "goldBattle": gold_battle,
                "mechanisms": classify(joined),
                "hasText": bool(joined),
            }
        )
    return units


def load_current_units(path):
    with open(path, encoding="utf-8-sig") as handle:
        data = json.load(handle)
    return {unit.get("name"): unit for unit in data if unit.get("name")}


def has_new_fields(unit):
    required = [
        "startCount",
        "hpPerUnit",
        "attack",
        "defense",
        "damageMin",
        "damageMax",
        "initiative",
        "speed",
        "morale",
        "luck",
        "attackRange",
        "size",
        "skillText",
    ]
    return unit is not None and all(field in unit for field in required)


def has_executable_skills(unit):
    if unit is None:
        return False
    return any(unit.get(field) for field in ["talents", "goldTalents", "battleSkills", "goldBattleSkills"])


def unit_logic_status(unit):
    if unit is None:
        return STATUS_BLOCKED
    if not has_executable_skills(unit):
        return STATUS_BLOCKED
    return STATUS_PARTIAL


def mechanism_rows(excel_units):
    acceptance_done = fixed_count_acceptance_passed()
    rows = []
    for mechanism, _ in MECHANISMS:
        count = sum(1 for unit in excel_units if mechanism in unit["mechanisms"])
        if count == 0:
            continue
        logic = MECHANISM_LOGIC_STATUS.get(mechanism, STATUS_NOT_STARTED)
        test = STATUS_DONE if mechanism == "获得数量" and acceptance_done else STATUS_NOT_STARTED
        rows.append((mechanism, count, STATUS_TEXT, logic, test))
    return rows


def skill_values(unit, field):
    return unit.get(field) or []


def has_skill_value(unit, field, kind, **expected):
    for skill in skill_values(unit, field):
        if skill.get("kind") != kind:
            continue
        if all(skill.get(key) == value for key, value in expected.items()):
            return True
    return False


def fixed_count_acceptance_checks():
    with open(UNIT_JSON_PATH, encoding="utf-8-sig") as handle:
        units = json.load(handle)
    by_id = {unit.get("id"): unit for unit in units}
    visible = [unit for unit in units if not unit.get("hidden")]
    data_text = json.dumps(units, ensure_ascii=False)
    manage_text = read_text(r"Assets\Scripts\Systems\ManageEventResolver.cs")
    shop_text = read_text(r"Assets\Scripts\Systems\ShopSystem.cs")
    board_text = read_text(r"Assets\Scripts\Systems\BoardSystem.cs")
    flow_text = read_text(r"Assets\Scripts\Systems\RunFlowController.cs")

    checks = []

    def add(name, passed, detail):
        checks.append((name, STATUS_DONE if passed else STATUS_BLOCKED, detail))

    add("No visible count cap", all(unit.get("maxCount", 0) in (0, None) for unit in visible), "`maxCount` must stay zero/unused for visible runtime units.")
    add("No percentage quantity text", not re.search(r"数量\d+%|当前数量\d+%|数量[^，。；\n]*\d+%", data_text), "Visible unit data must not contain percentage-based quantity gain.")
    add("GainCount writes permanent count", "target.baseCount = current + amount" in manage_text and "target.maxCount = 0" in manage_text, "`GainCount`/`ReinforceUnit` must increase `baseCount` and clear compatibility cap.")
    add("GainCount dispatches count event", 'Dispatch(runState, "on_gain_count"' in manage_text, "Dependent effects must listen to `on_gain_count`.")
    add("Shop cards start with default count", "baseCount = ResolveStartCount(unit)" in shop_text, "Shop-generated cards must receive default quantity.")
    add("Board deploy fills missing count", "card.baseCount > 0 ? card.baseCount : startCount" in board_text, "Legacy hand cards must not enter battle with zero quantity.")
    add("Reward/discovery cards start with default count", "baseCount = ResolveStartCount(definition)" in flow_text and "baseCount = ResolveStartCount(unit)" in flow_text, "Reward and discovery cards must receive default quantity.")
    add("Forest gem count rule", "ForestGemReinforceCount = 1" in manage_text and "target.forestGemCount += amount" in manage_text and "ReinforceUnit(target, amount * ForestGemReinforceCount)" in manage_text, "Forest gem must count +1 and permanent quantity +1 per gem.")
    add("Shop quantity feedback uses count", "count = gain" in manage_text and "ReinforceUnit(card, gain)" in manage_text, "Shop-card quantity growth must use count feedback and card quantity.")

    bright = by_id.get("bright_warrior", {})
    elf = by_id.get("elf", {})
    fire = by_id.get("fire_elemental", {})
    earth = by_id.get("earth_elemental", {})
    ger = by_id.get("ger_beast", {})
    dragon = by_id.get("magic_dragon", {})

    add("Bright Warrior fixed values", has_skill_value(bright, "talents", "round_end_if_adjacent_faith_self_gain_attack", value=10) and has_skill_value(bright, "goldTalents", "round_end_if_adjacent_faith_self_gain_attack", value=20), "Round-end adjacent faith gain should be fixed +10/+20 quantity.")
    add("Elf fixed values", has_skill_value(elf, "talents", "while_on_board_on_entry_race_self_gain_attack", value=10) and has_skill_value(elf, "goldTalents", "while_on_board_on_entry_race_self_gain_attack", value=20), "Entry-trigger self gain should be fixed +10/+20 quantity.")
    add("Fire Elemental count chain", has_skill_value(fire, "talents", "on_gain_power_self_gain_attack", value=4) and has_skill_value(fire, "goldTalents", "on_gain_power_self_gain_attack", value=8), "Legacy skill kind should now route through `on_gain_count` with fixed +4/+8.")
    add("Earth Elemental fixed temporary count", has_skill_value(earth, "battleSkills", "battle_start_lowest_power_ally_gain_source_power", value=3) and has_skill_value(earth, "goldBattleSkills", "battle_start_lowest_power_ally_gain_source_power", value=6), "Temporary battle count gain should be fixed +3/+6.")
    add("Ger Beast default divisor formula", has_skill_value(ger, "talents", "on_entry_devour_random_shop_gain_stats", ratio=0.1) and has_skill_value(ger, "goldTalents", "on_entry_devour_random_shop_gain_stats", ratio=0.2), "Default-count divisor formula should import to ratio 0.1/0.2.")
    add("Magic Dragon fixed summon count", has_skill_value(dragon, "battleSkills", "battle_start_summon_and_buff_type", value=22) and has_skill_value(dragon, "goldBattleSkills", "battle_start_summon_and_buff_type", value=26), "Summoned Fire Elemental count should be fixed 22/26.")

    return checks


def fixed_count_acceptance_passed():
    return all(status == STATUS_DONE for _, status, _ in fixed_count_acceptance_checks())


def risk_rows(excel_units, current_units):
    all_text = "\n".join(
        "\n".join([unit["talent"], unit["goldTalent"], unit["battle"], unit["goldBattle"]])
        for unit in excel_units
    )
    risks = [
        ("旧数量成长术语", STATUS_BLOCKED if "\u8865\u5458" in all_text else STATUS_DONE, "必须统一改成“获得数量”。"),
        ("百分比获得数量文本", STATUS_BLOCKED if re.search(r"数量\d+%|当前数量\d+%|数量[^，。；\n]*\d+%", all_text) else STATUS_DONE, "发现后需转成固定公式或设计确认。"),
        ("默认数量除法公式", STATUS_PARTIAL if re.search(r"默认数量/\d+", all_text) else STATUS_NA, "属于固定公式，已由导入脚本同步 ratio 参数，仍需专项验收。"),
        ("旧 power 技能数组", STATUS_BLOCKED if any("power" in json.dumps(unit, ensure_ascii=False) for unit in current_units.values()) else STATUS_DONE, "power 可兼容保留，但不能驱动新版伤害。"),
        ("技能数组丢失", STATUS_DONE if all(has_executable_skills(unit) for unit in current_units.values() if not unit.get("hidden")) else STATUS_BLOCKED, "经营效果依赖可执行技能数组。"),
        ("Unity batchmode 验证", STATUS_BLOCKED, "当前本机 batchmode 启动曾卡住，需要手动确认 Unity 无弹窗。"),
    ]
    return risks


def build_markdown(excel_units, current_units):
    visible_units = [unit for unit in current_units.values() if not unit.get("hidden")]
    mechanism_heatmap = mechanism_rows(excel_units)
    lines = [
        "# Unit Skill Migration Heatmap",
        "",
        "Source: `unit_202605250127_数量修订版02_攻防血压缩版.xlsx`",
        "",
        "术语约定：玩家可见文本统一使用“获得数量”。",
        "",
        "## Legend",
        "",
        "| 状态 | 含义 |",
        "| --- | --- |",
        f"| {STATUS_NOT_STARTED} | 未开始 |",
        f"| {STATUS_TEXT} | 文本/数据已导入 |",
        f"| {STATUS_PARTIAL} | 逻辑部分接入，未全量验收 |",
        f"| {STATUS_DONE} | 已验证 |",
        f"| {STATUS_BLOCKED} | 阻塞或需要设计确认 |",
        "",
        "## Snapshot",
        "",
        f"- Excel units: {len(excel_units)}",
        f"- Visible runtime units: {len(visible_units)}",
        f"- Units with executable skill arrays: {sum(1 for unit in visible_units if has_executable_skills(unit))}",
        f"- Mechanism families detected: {len(mechanism_heatmap)}",
        "",
        "## Mechanism Heatmap",
        "",
        "| 机制 | 涉及单位数 | 文本 | 逻辑 | 测试 |",
        "| --- | ---: | --- | --- | --- |",
    ]
    for mechanism, count, text, logic, test in mechanism_heatmap:
        lines.append(f"| {mechanism} | {count} | {text} | {logic} | {test} |")

    lines.extend(
        [
            "",
            "## Unit Heatmap",
            "",
            "| 单位 | 机制标签 | 新字段 | 可执行技能 | 逻辑 | 测试 |",
            "| --- | --- | --- | --- | --- | --- |",
        ]
    )
    for unit in excel_units:
        current = current_units.get(unit["name"])
        mechanism_text = "、".join(unit["mechanisms"]) if unit["mechanisms"] else "待分类"
        lines.append(
            "| {name} | {mechanisms} | {fields} | {skills} | {logic} | {test} |".format(
                name=unit["name"],
                mechanisms=mechanism_text,
                fields=STATUS_DONE if has_new_fields(current) else STATUS_BLOCKED,
                skills=STATUS_DONE if has_executable_skills(current) else STATUS_BLOCKED,
                logic=unit_logic_status(current),
                test=STATUS_NOT_STARTED,
            )
        )

    lines.extend(
        [
            "",
            "## Risk Heatmap",
            "",
            "| 风险项 | 状态 | 处理方式 |",
            "| --- | --- | --- |",
        ]
    )
    for name, status, action in risk_rows(excel_units, current_units):
        lines.append(f"| {name} | {status} | {action} |")

    lines.extend(
        [
            "",
            "## Fixed Count Acceptance",
            "",
            "| Check | Status | Detail |",
            "| --- | --- | --- |",
        ]
    )
    for name, status, detail in fixed_count_acceptance_checks():
        lines.append(f"| {name} | {status} | {detail} |")

    lines.extend(
        [
            "",
            "## Next Mechanism Sprint",
            "",
            "目标机制：固定获得数量。",
            "",
            "验收点：",
            "",
            "- 经营阶段单位获得数量时，永久数量 `baseCount` 增加固定值。",
            "- 战斗阶段临时获得数量时，`currentCount` 与 `currentTotalHp` 同步增加。",
            "- 密林宝钻赐予时，密林宝钻计数 +1，永久获得数量 +1。",
            "- 玩家可见文本统一使用“获得数量”。",
            "- 不出现按百分比获得数量。",
            "",
        ]
    )
    return "\n".join(lines)


def main():
    excel_path = sys.argv[1] if len(sys.argv) > 1 else EXCEL_PATH
    output_path = sys.argv[2] if len(sys.argv) > 2 else OUTPUT_PATH
    if not os.path.exists(excel_path):
        raise SystemExit(f"Excel not found: {excel_path}")
    excel_units = load_excel_units(excel_path)
    current_units = load_current_units(UNIT_JSON_PATH)
    markdown = build_markdown(excel_units, current_units)
    with open(output_path, "w", encoding="utf-8", newline="\n") as handle:
        handle.write(markdown)


if __name__ == "__main__":
    main()
