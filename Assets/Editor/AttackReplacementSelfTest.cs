using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ProphecyCentury.Core;
using ProphecyCentury.Data;
using ProphecyCentury.Systems;
using UnityEditor;
using UnityEngine;

public static class AttackReplacementSelfTest
{
    [MenuItem("Tools/Prophecy Century/Run Attack Replacement Self Test")]
    public static void Run()
    {
        var session = ProphecyGameSession.EnsureInstance();
        if (ProphecyGameSession.Instance == null)
        {
            // Edit-mode tests load data without invoking play-mode lifecycle methods.
            typeof(ProphecyGameSession).GetProperty("Instance").SetValue(null, session);
            var data = new GameDataRepository();
            data.LoadAll();
            typeof(ProphecyGameSession).GetProperty("Data").SetValue(session, data);
        }
        foreach (var realtime in new[] { false, true })
        foreach (var golden in new[] { false, true })
        {
            CheckPounce(realtime, golden, false);
            CheckPounce(realtime, golden, true);
            CheckFireRain(realtime, golden, false);
            CheckFireRain(realtime, golden, true);
            CheckFireRainCounter(realtime, golden);
            CheckWindAttackRange(realtime, golden, false);
            CheckWindAttackRange(realtime, golden, true);
        }
        Debug.Log("Attack replacement self test passed: 28 scenarios across both battle systems.");
    }

    public static void RunBatch()
    {
        try { Run(); EditorApplication.Exit(0); }
        catch (Exception exception) { Debug.LogException(exception); EditorApplication.Exit(1); }
    }

    private static void CheckPounce(bool realtime, bool golden, bool shield)
    {
        var fixture = new Fixture(realtime, "snow_lion", golden);
        var near = fixture.Enemy("near", 6, 2);
        var far = fixture.Enemy("far", 10, 2);
        var events = new List<BattleEvent>();
        fixture.Start(events);
        Check(events.Count == 0 && fixture.Hp(near) == 100000, "pounce must not run at battle start");
        Set(fixture.Source, realtime ? "StunRemaining" : "StunTurns", realtime ? (object)1f : 1);
        fixture.Act(events);
        Check(!events.Any(IsPounce), "stunned units must not pounce");
        Set(fixture.Source, realtime ? "StunRemaining" : "StunTurns", realtime ? (object)0f : 0);
        if (shield) Set(near, "ShieldLayers", 1);
        events.Clear();
        var baseDamage = fixture.BaseDamage(near);
        fixture.Act(events);
        Check(events.Count(IsPounce) == 1 && events.Single(IsPounce).TargetName == "near", "first attack must pounce at nearest enemy");
        Check(!events.Any(e => e.Kind == "attack"), "pounce must replace the ordinary hit");
        Check(fixture.Hp(near) == 100000 - (shield ? 0 : baseDamage * (golden ? 6 : 3)), "pounce multiplier or hit count is wrong");
        Check(fixture.Hp(far) == 100000, "pounce must only hit nearest target");
        Check((int)Get(fixture.Source, "AttackCount") == 1, "pounce must consume an attack even if shielded");
        events.Clear();
        fixture.Act(events);
        Check(!events.Any(IsPounce) && events.Any(e => e.Kind == "attack"), "second attack must be ordinary");
    }

    private static void CheckFireRain(bool realtime, bool golden, bool shield)
    {
        var fixture = new Fixture(realtime, "exorcist_mount", golden);
        var center = fixture.Enemy("center", 3, 2);
        var inside = fixture.Enemy("inside", 4, 2);
        var outer = fixture.Enemy("outer", 6, 2);
        var outside = fixture.Enemy("outside", 8, 2);
        if (shield) Set(center, "ShieldLayers", 1);
        var damage = fixture.BaseDamage(center);
        for (var attack = 1; attack <= 2; attack++)
        {
            var events = new List<BattleEvent>();
            fixture.Act(events);
            Check(!events.Any(e => e.Kind == "attack"), "fire rain must not include a separate ordinary attack");
            Check(events.Count(e => e.Message.Contains("召唤火雨")) == 1, "each attack must cast exactly one fire rain");
            Check(fixture.Hp(center) == 100000 - damage * (attack - (shield ? 1 : 0)), "center must take exactly one hit per attack");
            Check(fixture.Hp(inside) == 100000 - damage * attack, "nearby enemy must be hit even when center is shielded");
            Check(fixture.Hp(outer) == 100000 - (golden ? damage * attack : 0), "normal/golden radius mismatch");
            Check(fixture.Hp(outside) == 100000, "enemy outside radius must not be hit");
        }
    }

    private static void CheckFireRainCounter(bool realtime, bool golden)
    {
        var fixture = new Fixture(realtime, "exorcist_mount", golden);
        var target = fixture.Enemy("counter_target", 3, 2);
        Set(target, "Definition", new UnitDefinition {
            id = "counter_target", attackRange = 1,
            battleSkills = new[] { new SkillDefinition { kind = "first_hits_counterattack", count = 4, repeat = 1 } }
        });
        fixture.Counter(target, new List<BattleEvent>());
        Check(fixture.Hp(target) == 100000 - fixture.BaseDamage(target), "counterattack must also use fire rain");
        Check(fixture.Hp(fixture.Source) == 100000 && (int)Get(target, "ForcedCounterattackTriggers") == 0,
            "fire rain counterattack must not start another counterattack chain");
    }

    private static void CheckWindAttackRange(bool realtime, bool golden, bool hasTargetInRange)
    {
        var fixture = new Fixture(realtime, "wind_elemental", golden);
        if (realtime) Set(fixture.Source, "Range", 5f);
        var first = fixture.Enemy("first", 3, 2);
        Set(first, realtime ? "Hp" : "CurrentHp", 1);
        Set(first, "CurrentTotalHp", 1);
        var far = fixture.Enemy("out_of_range", 12, 2);
        var next = hasTargetInRange ? fixture.Enemy("in_range", 6, 2) : null;
        var events = new List<BattleEvent>();
        fixture.Start(events);
        Check((int)Get(fixture.Source, "ConsecutiveAttacks") == (golden ? 3 : 2), "wind aura must grant the configured attack count");
        fixture.Act(events);
        Check(fixture.Hp(first) == 0, "first attack should kill its target");
        Check(fixture.Hp(far) == 100000, "wind repeat must not damage an enemy outside attack range");
        var expectedAttacks = hasTargetInRange ? (golden ? 3 : 2) : 1;
        Check(events.Count(e => e.Kind == "attack") == expectedAttacks, "wind repeats must stop when no target remains in range");
        if (next != null) Check(fixture.Hp(next) < 100000, "wind repeats should retarget a living enemy in range");
    }

    private static bool IsPounce(BattleEvent e) => e.Message != null && e.Message.Contains("pounces");
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static object Get(object unit, string name) => unit.GetType().GetField(name).GetValue(unit);
    private static void Set(object unit, string name, object value) => unit.GetType().GetField(name).SetValue(unit, value);

    private sealed class Fixture
    {
        private readonly bool realtime;
        private readonly Type system;
        private readonly Type unitType;
        private readonly IList allies;
        private readonly IList enemies;
        public readonly object Source;

        public Fixture(bool realtime, string id, bool golden)
        {
            this.realtime = realtime;
            system = realtime ? typeof(BattleRealtimeSystem) : typeof(BattleStubSystem);
            unitType = realtime ? system.GetNestedType("RealtimeBattleUnit", BindingFlags.NonPublic)
                : system.Assembly.GetType("ProphecyCentury.Systems.BattleRuntimeUnit");
            allies = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(unitType));
            enemies = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(unitType));
            Source = Unit(id, id, true, 2, 2);
            Set(Source, "IsGolden", golden);
            allies.Add(Source);
        }

        public object Enemy(string name, int col, int row)
        {
            var unit = Unit("ger_beast", name, false, col, row);
            Set(unit, "Definition", new UnitDefinition { id = "test_target", attackRange = 1 });
            enemies.Add(unit);
            return unit;
        }

        private object Unit(string id, string name, bool player, int col, int row)
        {
            var unit = realtime ? Activator.CreateInstance(unitType, new object[] {
                new BattleUnitSnapshot { UnitId = id, Name = name, SlotId = "2-2", CurrentCount = 1, BaseCount = 1, HpPerUnit = 100000, CurrentTotalHp = 100000 }, player })
                : Activator.CreateInstance(unitType, true);
            Set(unit, "Definition", ProphecyGameSession.Instance.Data.FindUnit(id));
            Set(unit, "UnitId", id); Set(unit, "Name", name); Set(unit, "PlayerSide", player);
            Set(unit, "SlotId", "hex:" + col + ":" + row);
            Set(unit, "BaseCount", 1); Set(unit, "CurrentCount", 1); Set(unit, "MaxCount", 1);
            Set(unit, "HpPerUnit", 100000); Set(unit, "MaxHp", 100000); Set(unit, "CurrentTotalHp", 100000);
            Set(unit, realtime ? "Hp" : "CurrentHp", 100000);
            Set(unit, "Attack", 10); Set(unit, "Defense", 10); Set(unit, "Power", 1);
            Set(unit, "DamageMin", 100); Set(unit, "DamageMax", 100);
            Set(unit, "Luck", 0); Set(unit, "Morale", 0); Set(unit, "AttackInterval", 1f);
            Set(unit, "Row", row); Set(unit, "Col", col);
            if (realtime) { Set(unit, "X", col * 80f); Set(unit, "Y", row * 80f); Set(unit, "Range", 1f); }
            else { Set(unit, "HexColumn", col); Set(unit, "HexRow", row); }
            return unit;
        }

        public int Hp(object unit) => (int)Get(unit, realtime ? "Hp" : "CurrentHp");
        private object Call(string name, params object[] args) => system.GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, args);
        public int BaseDamage(object target) => (int)Call("CalculateDamage", Source, target, new System.Random(11));
        public void Start(List<BattleEvent> events) => Call("ResolveBattleStart", allies, enemies, new System.Random(11), events, 0f);
        public void Counter(object target, List<BattleEvent> events)
        {
            if (realtime) Call("ResolveAttackDamage", Source, target, allies, enemies, new System.Random(11), events, 1f, "counter", false);
            else Call("ApplyAttack", Source, target, allies, enemies, new System.Random(11), null, true, false, false, events, 1f);
        }
        public void Act(List<BattleEvent> events)
        {
            if (realtime)
            {
                Set(Source, "AttackTimer", 0f);
                var effects = Activator.CreateInstance(typeof(List<>).MakeGenericType(system.GetNestedType("RealtimeAreaEffect", BindingFlags.NonPublic)));
                Call("TickSide", allies, enemies, new System.Random(11), effects, 1f, events);
            }
            else Call("TakeHexTurn", Source, allies, enemies, new System.Random(11), 0, events, 1f);
        }
    }
}
