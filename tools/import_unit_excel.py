import json
import os
import re
import sys
import zipfile
import xml.etree.ElementTree as ET


NS = {"a": "http://schemas.openxmlformats.org/spreadsheetml/2006/main"}


def col_number(cell_ref):
    match = re.match(r"([A-Z]+)", cell_ref)
    value = 0
    for char in match.group(1):
        value = value * 26 + ord(char) - 64
    return value


def read_sheet(path, sheet_index=1):
    with zipfile.ZipFile(path) as archive:
        shared = []
        if "xl/sharedStrings.xml" in archive.namelist():
            root = ET.fromstring(archive.read("xl/sharedStrings.xml"))
            for item in root.findall("a:si", NS):
                shared.append("".join(text.text or "" for text in item.findall(".//a:t", NS)))

        worksheet = ET.fromstring(archive.read(f"xl/worksheets/sheet{sheet_index}.xml"))
        rows = []
        for row in worksheet.findall("a:sheetData/a:row", NS):
            values = {}
            for cell in row.findall("a:c", NS):
                node = cell.find("a:v", NS)
                value = "" if node is None else node.text
                if cell.attrib.get("t") == "s" and value != "":
                    value = shared[int(value)]
                values[col_number(cell.attrib["r"])] = value
            rows.append(values)
        return rows


def as_int(value, fallback=0):
    if value is None or value == "":
        return fallback
    return int(float(value))


def as_float(value, fallback=0.0):
    if value is None or value == "":
        return fallback
    return float(value)


def split_tags(value):
    if not value or value == "-":
        return []
    return [part.strip() for part in re.split(r"[,/，、；;]", value) if part.strip() and part.strip() != "-"]


def first_non_empty(*values):
    for value in values:
        if value is not None and str(value).strip() and str(value).strip() != "-":
            return str(value).strip()
    return ""


def sanitize_skill_text(value):
    if not value:
        return value
    text = str(value)
    legacy_term = "\u8865\u5458"
    text = text.replace(legacy_term, "获得数量")
    text = text.replace("获得其当前数量50%的获得数量", "获得固定数量+1（TODO：新版固定值待确认）")
    text = text.replace("获得其当前数量50% 的获得数量", "获得固定数量+1（TODO：新版固定值待确认）")
    text = text.replace("巫兽师继承双方当前数量并受自身编制上限约束", "巫兽师继承双方当前数量之和（TODO：固定损耗待确认）")
    text = text.replace("开战后，当前数量最低的友方部队临时获得50%的额外数量。", "开战后，当前数量最低的友方部队临时获得固定数量+3。")
    text = text.replace("开战后，当前数量最低的友方部队临时获得100%的额外数量。", "开战后，当前数量最低的友方部队临时获得固定数量+6。")
    text = text.replace("开战后，在最近3个格子分别召唤1支临时火元素部队，每支数量与场上数量最多的火元素一致，至少为10。", "开战后，在最近3个格子分别召唤1支临时火元素部队，每支固定数量为22。")
    text = text.replace("开战后，在最近3个格子分别召唤1支临时火元素部队，每支数量比场上数量最多的火元素多20%，至少为10。", "开战后，在最近3个格子分别召唤1支临时火元素部队，每支固定数量为26。")
    text = text.replace("开战后，在最近的1个格子中召唤1支临时格尔兽部队（数量为本驯兽师数量的25%，至少为1）参加战斗。", "开战后，在最近的1个格子中召唤1支临时格尔兽部队（数量为格尔兽默认数量）参加战斗。")
    text = text.replace("开战后，在最近的2个格子中分别召唤1支临时格尔兽部队（每支数量为本驯兽师数量的30%，至少为1）参加战斗。", "开战后，在最近的2个格子中分别召唤1支临时格尔兽部队（每支数量为格尔兽默认数量）参加战斗。")
    text = text.replace("阵亡后，原地召唤1支临时格尔兽部队（数量为本兽骑兵数量的25%，至少为1）参加战斗。", "阵亡后，原地召唤1支临时格尔兽部队（数量为格尔兽默认数量）参加战斗。")
    text = text.replace("阵亡后，在原地和最近的1个格子中分别召唤1支临时格尔兽部队（每支数量为本兽骑兵数量的30%，至少为1）参加战斗。", "阵亡后，在原地和最近的1个格子中分别召唤1支临时格尔兽部队（每支数量为格尔兽默认数量）参加战斗。")
    return text


def extract_count_value(text):
    if not text:
        return 0
    patterns = [
        r"\+(\d+)\s*数量",
        r"数量\+(\d+)",
        r"获得\+?(\d+)\s*数量",
        r"固定数量\+(\d+)",
        r"固定数量为(\d+)",
    ]
    for pattern in patterns:
        match = re.search(pattern, text)
        if match:
            return int(match.group(1))
    return 0


def extract_default_count_ratio(text):
    if not text:
        return 0.0
    match = re.search(r"默认数量/(\d+)", text)
    if not match:
        return 0.0
    divisor = int(match.group(1))
    return 1.0 / divisor if divisor > 0 else 0.0


def normalize_skill_values(skills, text):
    amount = extract_count_value(text)
    ratio = extract_default_count_ratio(text)
    if (amount <= 0 and ratio <= 0) or not skills:
        return skills
    normalized = []
    for skill in skills:
        copied = dict(skill)
        if amount > 0:
            copied["value"] = amount
        if ratio > 0:
            copied["ratio"] = ratio
        normalized.append(copied)
    return normalized


def import_units(excel_path, json_path):
    rows = read_sheet(excel_path, 1)
    old_units = json.load(open(json_path, encoding="utf-8-sig"))
    data_rows = [row for row in rows[2:] if row.get(1)]
    old_by_name = {unit.get("name"): unit for unit in old_units if unit.get("name")}
    matched_ids = set()

    imported = []
    for row in data_rows:
        old = old_by_name.get(str(row.get(1)).strip())
        if old is None:
            raise RuntimeError(f"Excel unit '{row.get(1)}' does not match an existing unit id.")

        matched_ids.add(old.get("id"))
        talent_text = sanitize_skill_text(first_non_empty(row.get(21)))
        gold_talent_text = sanitize_skill_text(first_non_empty(row.get(22)))
        battle_text = sanitize_skill_text(first_non_empty(row.get(23)))
        gold_battle_text = sanitize_skill_text(first_non_empty(row.get(24)))
        skill_text = "\n".join(text for text in [talent_text, gold_talent_text, battle_text, gold_battle_text] if text)

        start_count = as_int(row.get(7), 1)
        hp_per_unit = max(1, as_int(row.get(8), 1))
        damage_min = max(1, as_int(row.get(11), 1))
        damage_max = max(damage_min, as_int(row.get(12), damage_min))
        initiative = as_int(row.get(13), old.get("initiative", 0))
        speed = max(0, as_int(row.get(14), old.get("speed", 0)))
        attack_range = max(1.0, as_float(row.get(17), old.get("range", 1.0)))
        design_only_initial_base_damage = as_int(row.get(20), 0)

        unit = dict(old)
        talents = normalize_skill_values(old.get("talents", []), talent_text)
        gold_talents = normalize_skill_values(old.get("goldTalents", []), gold_talent_text)
        battle_skills = normalize_skill_values(old.get("battleSkills", []), battle_text)
        gold_battle_skills = normalize_skill_values(old.get("goldBattleSkills", []), gold_battle_text)

        unit.update(
            {
                "name": str(row.get(1)).strip(),
                "star": as_int(row.get(2), old.get("star", 1)),
                "race": first_non_empty(row.get(3)),
                "typeLabel": first_non_empty(row.get(4)),
                "faith": first_non_empty(row.get(5)),
                "tags": split_tags(row.get(6)),
                "startCount": start_count,
                "defaultCount": start_count,
                "baseCount": start_count,
                "maxCount": 0,
                "hpPerUnit": hp_per_unit,
                "damageMin": damage_min,
                "damageMax": damage_max,
                "initiative": initiative,
                "speed": speed,
                "morale": max(0, as_int(row.get(15), old.get("morale", 0))),
                "luck": max(0, as_int(row.get(16), old.get("luck", 0))),
                "range": attack_range,
                "attackRange": attack_range,
                "size": max(1, as_int(row.get(18), old.get("size", 1))),
                "firstPurchaseHp": as_int(row.get(19), start_count * hp_per_unit),
                "firstPurchaseAverageDamage": design_only_initial_base_damage,
                "designOnlyInitialBaseDamage": design_only_initial_base_damage,
                "talentText": talent_text,
                "goldTalentText": gold_talent_text,
                "battleText": battle_text,
                "goldBattleText": gold_battle_text,
                "skillText": skill_text,
                "talents": talents,
                "goldTalents": gold_talents,
                "battleSkills": battle_skills,
                "goldBattleSkills": gold_battle_skills,
                "attack": max(0, as_int(row.get(9), old.get("attack", 0))),
                "defense": max(0, as_int(row.get(10), old.get("defense", 0))),
                "hp": hp_per_unit,
                "power": old.get("power", 0),
                "attackInterval": max(0.2, 1.2 - min(0.8, initiative * 0.05)),
            }
        )
        imported.append(unit)

    for old in old_units:
        if old.get("id") in matched_ids:
            continue
        old["hidden"] = True
        old.setdefault("startCount", 1)
        old.setdefault("defaultCount", old.get("startCount", 1))
        old.setdefault("baseCount", old.get("startCount", 1))
        old["maxCount"] = 0
        old.setdefault("hpPerUnit", max(1, old.get("hp", 1)))
        old.setdefault("damageMin", 1)
        old.setdefault("damageMax", max(1, old.get("attack", 1)))
        old.setdefault("initiative", old.get("speed", 0))
        old.setdefault("attackRange", old.get("range", 1.0))
        old.setdefault("designOnlyInitialBaseDamage", old.get("firstPurchaseAverageDamage", 0))
        old["talentText"] = sanitize_skill_text(old.get("talentText"))
        old["goldTalentText"] = sanitize_skill_text(old.get("goldTalentText"))
        old["battleText"] = sanitize_skill_text(old.get("battleText"))
        old["goldBattleText"] = sanitize_skill_text(old.get("goldBattleText"))
        old["skillText"] = sanitize_skill_text("\n".join(text for text in [old.get("talentText"), old.get("goldTalentText"), old.get("battleText"), old.get("goldBattleText")] if text))
        imported.append(old)

    with open(json_path, "w", encoding="utf-8", newline="\n") as output:
        json.dump(imported, output, ensure_ascii=False, indent=4)
        output.write("\n")


if __name__ == "__main__":
    if len(sys.argv) != 3:
        raise SystemExit("usage: import_unit_excel.py <excel.xlsx> <unit_data.json>")
    import_units(os.path.abspath(sys.argv[1]), os.path.abspath(sys.argv[2]))
