# -*- coding: utf-8 -*-
"""检查 BattleStubSystem.cs 和 BattleRealtimeSystem.cs 的技能实现一致性"""
import re
import os
from collections import defaultdict

def extract_kinds_with_context(filepath):
    """从 C# 文件提取 kind 及其周围的上下文行"""
    if not os.path.exists(filepath):
        return {}
    with open(filepath, "r", encoding="utf-8") as f:
        lines = f.readlines()

    kinds = {}
    # 模式1: case "kindname":
    for i, line in enumerate(lines):
        m = re.search(r'case\s+"([^"]+)":', line)
        if m:
            kind = m.group(1)
            # 取接下来的5行作为上下文
            ctx = "".join(line.rstrip() for line in lines[i:i+8])
            kinds[kind] = ("case", i+1, ctx[:200])

    # 模式2: skill.kind == "kindname"
    for i, line in enumerate(lines):
        m = re.search(r'(?:skill|talent)\.kind\s*==\s*"([^"]+)"', line)
        if m:
            kind = m.group(1)
            if kind not in kinds:
                ctx = "".join(line.rstrip() for line in lines[max(0,i-1):i+6])
                kinds[kind] = ("inline", i+1, ctx[:200])

    return kinds

print("=" * 80)
print("  BattleStubSystem ⇔ BattleRealtimeSystem 技能一致性检查")
print("=" * 80)

stub_kinds = extract_kinds_with_context("Assets/Scripts/Systems/BattleStubSystem.cs")
realtime_kinds = extract_kinds_with_context("Assets/Scripts/Systems/BattleRealtimeSystem.cs")

print(f"\n  BattleStubSystem:     {len(stub_kinds)} kinds")
print(f"  BattleRealtimeSystem: {len(realtime_kinds)} kinds")

only_stub = set(stub_kinds.keys()) - set(realtime_kinds.keys())
only_realtime = set(realtime_kinds.keys()) - set(stub_kinds.keys())
shared = set(stub_kinds.keys()) & set(realtime_kinds.keys())

print(f"  共享 kind:            {len(shared)}")
print(f"  仅Stub有:            {len(only_stub)}")
print(f"  仅Realtime有:        {len(only_realtime)}")
print()

# ── 仅 Stub 有 ──
if only_stub:
    print("─" * 80)
    print(f"⚠ 仅 BattleStubSystem 中存在 ({len(only_stub)} 个) — Realtime缺少实现！")
    print("─" * 80)
    # 按重要性排序：排除明显的管理器kind
    manager_like = {"on_extra_attack_once_next_round_gold"}  # 有小商人的追击金币
    for k in sorted(only_stub):
        mode, ln, ctx = stub_kinds[k]
        marker = " ← 有技能使用!" if k in manager_like else ""
        print(f"  kind={k} (Stub Ln{ln}, {mode}){marker}")
        # 统计JSON中哪些单位用这个kind
    print()

# ── 仅 Realtime 有 ──
if only_realtime:
    print("─" * 80)
    print(f"⚠ 仅 BattleRealtimeSystem 中存在 ({len(only_realtime)} 个) — Stub缺少实现！")
    print("─" * 80)
    for k in sorted(only_realtime):
        mode, ln, ctx = realtime_kinds[k]
        print(f"  kind={k} (Realtime Ln{ln}, {mode})")
    print()

# ── 共享 kind 但实现细节不同 ──
print("─" * 80)
print(f"共享 kind 的粗略一致性抽查 ({len(shared)} 个)")
print("─" * 80)

# 选取一些关键 kind 做代码片段对比
sample_kinds = [
    "battle_start_pounce_nearest_damage",
    "battle_start_stealth",
    "battle_start_summon_units",
    "battle_start_team_shield",
    "battle_start_lowest_power_ally_gain_source_power",
    "battle_start_lock_highest_hp_targets",
    "passive_every_nth_attack_force_crit",
    "on_attack_chance_force_crit",
    "battle_aura_sync_unit_id_attack_to_highest",
    "battle_periodic_temp_power",
]

issues = []
for k in sample_kinds:
    if k not in stub_kinds or k not in realtime_kinds:
        continue
    stub_mode, stub_ln, stub_ctx = stub_kinds[k]
    rt_mode, rt_ln, rt_ctx = realtime_kinds[k]

    # 简单 check：两个文件中的实现模式是否一致（都是case或都是inline）
    if stub_mode != rt_mode:
        issues.append(f"  ⚠ {k}: 实现模式不同 Stub={stub_mode} vs Realtime={rt_mode}")
        print(f"  ⚠ {k}: 实现模式不同 Stub={stub_mode}(Ln{stub_ln}) vs Realtime={rt_mode}(Ln{rt_ln})")

if not issues:
    print("  PASS: 抽查的10个关键kind实现模式一致")

# ── 深入对比：检查同一个kind的两个实现是否有结构性差异 ──
print()
print("─" * 80)
print("深入对比：共享kind的核心参数使用差异")
print("─" * 80)

# 读取两个文件的完整内容做更细致的分析
with open("Assets/Scripts/Systems/BattleStubSystem.cs", "r", encoding="utf-8") as f:
    stub_code = f.read()
with open("Assets/Scripts/Systems/BattleRealtimeSystem.cs", "r", encoding="utf-8") as f:
    realtime_code = f.read()

# 比较关键数值常量的差异
param_checks = [
    ("LUCK_CRIT_CHANCE", r"LUCK_CRIT_CHANCE_PER_POINT\s*=\s*([0-9.]+)f"),
    ("LUCK_CRIT_DAMAGE", r"LUCK_CRIT_DAMAGE_MULTIPLIER\s*=\s*([0-9.]+)f"),
    ("MORALE_EXTRA", r"MORALE_EXTRA_ATTACK_CHANCE_PER_POINT\s*=\s*([0-9.]+)f"),
    ("MAX_BATTLE_SECONDS", r"MaxBattleSeconds\s*=\s*(\d+)"),
    ("MAX_BATTLE_ROUNDS", r"MaxBattleRounds\s*=\s*(\d+)"),
    ("TARGET_SEARCH_INTERVAL", r"TargetSearchInterval\s*=\s*([0-9.]+)f"),
]

print("  战斗参数常量对比:")
for name, pattern in param_checks:
    stub_match = re.search(pattern, stub_code)
    rt_match = re.search(pattern, realtime_code)
    sv = stub_match.group(1) if stub_match else "N/A"
    rv = rt_match.group(1) if rt_match else "N/A"
    status = "✓" if sv == rv else "✗"
    print(f"    {status} {name}: Stub={sv}  Realtime={rv}")

# ── 总结 ──
print()
print("=" * 80)
print("  一致性检查总结")
print("=" * 80)
print(f"  共享 kind 数:     {len(shared)}")
print(f"  仅Stub独有:       {len(only_stub)} — 可能在Realtime中遗漏")
print(f"  仅Realtime独有:   {len(only_realtime)} — 可能在Stub中遗漏")
print(f"  抽查差异数:        {len(issues)}")
print()
print("  建议：")
if only_stub:
    print(f"    → 检查仅Stub独有的{len(only_stub)}个kind是否也需要在Realtime中实现")
if only_realtime:
    print(f"    → 检查仅Realtime独有的{len(only_realtime)}个kind是否也需要在Stub中实现")
if not only_stub and not only_realtime and not issues:
    print("    ✓ 两个战斗系统实现一致，无需修复")
print()
print("=" * 80)