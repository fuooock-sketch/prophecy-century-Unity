$ErrorActionPreference = 'Stop'
# Execute the production count and damage methods outside Unity, with unrelated
# combat callbacks stubbed. This checks arithmetic/events, not the rendered UI.
$source = Get-Content (Join-Path $PSScriptRoot '../Assets/Scripts/Systems/BattleStubSystem.cs') -Raw
function Extract-Method([string]$name) {
    $match = [regex]::Match($source, '(?m)^        private static [^\r\n]+\b' + $name + '\(')
    if (-not $match.Success) { throw "Method not found: $name" }
    $start = $source.IndexOf('{', $match.Index)
    $depth = 1
    $end = $start + 1
    while ($depth -gt 0) {
        if ($source[$end] -eq '{') { $depth++ }
        if ($source[$end] -eq '}') { $depth-- }
        $end++
    }
    return $source.Substring($match.Index, $end - $match.Index)
}
$methods = @('ApplySelfCountLoss', 'AddTemporaryCount', 'ResolveAllyDeathTaggedPower', 'DealDamage') | ForEach-Object { Extract-Method $_ }
$harness = @'
using System;
using System.Linq;
using System.Collections.Generic;
public static class CountRegression {
    class SkillDefinition { public string kind; public string deadTag; public string targetTag; public int value; public int power = 0; }
    class BattleRuntimeUnit {
        public string Name; public string Tag; public int CurrentCount, CurrentHp, CurrentTotalHp, BaseCount, MaxHp;
        public int HpPerUnit = 10, SkillTriggers, ShieldLayers, DamageDone, DamagedCount, KillCount;
        public float InvincibleRemaining; public bool HasTakenDamage;
        public bool IsAlive { get { return CurrentCount > 0 && CurrentHp > 0; } }
        public List<SkillDefinition> Skills = new List<SkillDefinition>();
    }
    class BattleEvent { public string Kind; public BattleRuntimeUnit Target; public int Amount, TargetHp; }
    static IEnumerable<SkillDefinition> GetBattleSkills(BattleRuntimeUnit unit) { return unit.Skills; }
    static bool HasTag(BattleRuntimeUnit unit, string tag) { return unit.Tag == tag; }
    static void AddEvent(List<BattleEvent> events, float time, string kind, BattleRuntimeUnit source, BattleRuntimeUnit target, int amount, string message) {
        if (events != null) events.Add(new BattleEvent { Kind = kind, Target = target, Amount = amount, TargetHp = target.CurrentHp });
    }
    static void ResolveDamaged(BattleRuntimeUnit unit) { }
    static void ResolveKill(BattleRuntimeUnit unit) { }
    static void ResolveFirstHitsCounterattack(BattleRuntimeUnit a, BattleRuntimeUnit b, List<BattleRuntimeUnit> c, List<BattleRuntimeUnit> d, Random r, List<BattleEvent> e, float t) { }
    static void ResolveDeath(BattleRuntimeUnit a, BattleRuntimeUnit b, List<BattleRuntimeUnit> c, List<BattleRuntimeUnit> d, Random r, List<BattleEvent> e, float t) { }
    static BattleRuntimeUnit Unit(string name, string tag, int count) {
        return new BattleRuntimeUnit { Name = name, Tag = tag, CurrentCount = count, BaseCount = count, CurrentHp = count * 10, CurrentTotalHp = count * 10, MaxHp = count * 10, ShieldLayers = 0, InvincibleRemaining = 0 };
    }
    static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
    public static void Run() {
        foreach (int gain in new[] {10, 20}) {
            var gardener = Unit("gardener", "human", 2);
            gardener.Skills.Add(new SkillDefinition { kind = "on_ally_death_tagged_units_temp_power", deadTag = "element", targetTag = "element", value = gain });
            var dead = Unit("dead", "element", 0);
            var element = Unit("element", "element", 5);
            var other = Unit("other", "human", 5);
            var units = new List<BattleRuntimeUnit> { gardener, dead, element, other };
            var events = new List<BattleEvent>();
            ResolveAllyDeathTaggedPower(dead, units, events);
            Check(element.CurrentCount == 5 + gain && other.CurrentCount == 5 && dead.CurrentCount == 0, "Gardener targets/count incorrect");
            Check(events.Count == 1 && events[0].Kind == "count_gain" && events[0].Target == element && events[0].Amount == gain && events[0].TargetHp == (5 + gain) * 10, "Gardener refresh event incorrect");
            events.Clear();
            ResolveAllyDeathTaggedPower(Unit("non-element", "human", 0), units, events);
            Check(events.Count == 0 && element.CurrentCount == 5 + gain, "Non-element death triggered gardener");
        }
        foreach (int hp in new[] { 100, 95 }) {
            var smith = Unit("smith", "human", 10);
            smith.CurrentHp = smith.CurrentTotalHp = hp;
            Check(ApplySelfCountLoss(smith, 3) == hp - 70, "Self-loss damage incorrect");
            Check(smith.CurrentCount == 7 && smith.CurrentHp == 70 && smith.CurrentTotalHp == 70, "Self-loss HP not synchronized");
            var attacker = Unit("attacker", "human", 1);
            int damage = DealDamage(attacker, smith, 15, new List<BattleRuntimeUnit> { attacker }, new List<BattleRuntimeUnit> { smith }, new Random(1));
            Check(damage == 15 && smith.CurrentCount == 6 && smith.CurrentHp == 55 && smith.CurrentTotalHp == 55, "Damage after self-loss restored troops");
        }
    }
'@
Add-Type -TypeDefinition ($harness + [Environment]::NewLine + ($methods -join [Environment]::NewLine) + "`n}")
[CountRegression]::Run()
Write-Output 'PASS: gardener normal/gold refresh and target filtering; blacksmith full/wounded self-loss followed by damage.'
