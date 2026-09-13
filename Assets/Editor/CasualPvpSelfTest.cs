using System;
using System.IO;
using ProphecyCentury.Core;
using ProphecyCentury.Model;
using ProphecyCentury.Systems;
using UnityEditor;
using UnityEngine;

public static class CasualPvpSelfTest
{
    [MenuItem("Tools/Prophecy Century/Run Casual PVP Self Test")]
    public static void Run()
    {
        var directory = Path.Combine(Path.GetTempPath(), "prophecy-century-casual-pvp-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(directory);
            var service = new CasualPvpSaveSlotService(directory, false);
            Assert(service.GetAllSlots().Count == 3, "casual mode must expose exactly three slots");
            Assert(!service.HasAnyValidSave(), "new casual save store must be empty");
            Assert(service.CreateNewGame(3).Success && service.CurrentSaveSlotIndex == 3, "empty slot must be reservable for hero selection");
            Assert(service.GetSlot(3).State == SaveSlotState.Empty, "slot reservation must not create a default-hero save");

            WriteValidSlot(service, 1, false, 8, 63);
            WriteValidSlot(service, 2, true, 18, 24);
            Assert(service.HasAnyValidSave(), "valid casual saves must be discovered");
            Assert(service.GetSlot(1).Metadata.summary.Contains("第 8 / 15 回合"), "pre-milestone summary must show round progress");
            Assert(service.GetSlot(2).Metadata.summary.Contains("无尽阶段"), "endless summary must show endless phase");
            Assert(service.GetSlot(2).State == SaveSlotState.Valid, "valid casual save must pass validation");
            Assert(service.DeleteSave(1).Success && service.GetSlot(1).State == SaveSlotState.Empty, "casual save must be deletable");
            CheckRecovery(service);
            CheckOpponentUi();
            Debug.Log("Casual PVP self test passed.");
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }

    public static void RunBatch()
    {
        try
        {
            Run();
            var args = Environment.GetCommandLineArgs();
            var index = Array.IndexOf(args, "-pvpTestEndpoint");
            if (index >= 0 && index + 1 < args.Length) CasualPvpNetworkSelfTest.StartBatch(args[index + 1]);
            else EditorApplication.Exit(0);
        }
        catch (Exception ex) { Debug.LogException(ex); EditorApplication.Exit(1); }
    }

    private static void CheckOpponentUi()
    {
        var root = new GameObject("PvpUiTest", typeof(RectTransform), typeof(Canvas));
        try
        {
            var ui = root.AddComponent<ProphecyCentury.UI.CasualPvpUiController>();
            var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            typeof(ProphecyCentury.UI.CasualPvpUiController).GetMethod("BuildOverlay", flags).Invoke(ui, null);
            var snapshot = new CasualPvpSnapshotState { round = 8, displayName = "测试对手", sourceType = "player" };
            snapshot.units.Add(new CasualPvpUnitState { name = "测试单位", slotId = "1-1", star = 2, count = 10, isGolden = true });
            var starts = 0;
            ui.ShowOpponentReveal(snapshot, false, () => starts++);
            var left = (UnityEngine.UI.Button)typeof(ProphecyCentury.UI.CasualPvpUiController).GetField("_overlayLeftButton", flags).GetValue(ui);
            var right = (UnityEngine.UI.Button)typeof(ProphecyCentury.UI.CasualPvpUiController).GetField("_overlayRightButton", flags).GetValue(ui);
            var content = (UnityEngine.UI.Text)typeof(ProphecyCentury.UI.CasualPvpUiController).GetField("_overlayContent", flags).GetValue(ui);
            Assert(starts == 0 && left.gameObject.activeSelf && !right.gameObject.activeSelf
                && left.GetComponentInChildren<UnityEngine.UI.Text>().text == "开始战斗"
                && content.text.Contains("金色"), "opponent screen must wait for explicit start and show golden state");
            left.onClick.Invoke();
            Assert(starts == 1, "start button must invoke the battle callback");
            ui.ShowOpponentReveal(snapshot, false, () => starts += 10);
            left.onClick.Invoke();
            Assert(starts == 11, "restored opponent screen must replace stale callbacks");
            Debug.Log("Casual PVP opponent UI controls passed.");
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    private static void CheckRecovery(CasualPvpSaveSlotService service)
    {
        var createdSession = ProphecyGameSession.Instance == null;
        var session = ProphecyGameSession.EnsureInstance();
        // Build the edit-mode fixture without runtime Awake/DontDestroyOnLoad.
        if (session.Data == null)
        {
            var data = new ProphecyCentury.Data.GameDataRepository();
            data.LoadAll();
            typeof(ProphecyGameSession).GetProperty("Instance").GetSetMethod(true).Invoke(null, new object[] { session });
            typeof(ProphecyGameSession).GetProperty("Data").GetSetMethod(true).Invoke(session, new object[] { data });
        }
        var previous = session.CurrentRun;
        try
        {
            var run = new RunState { round = 8, state = "manage", phase = GamePhase.NightManage, gold = 50 };
            CasualPvpSystem.InitializeRun(run);
            session.RestoreRun(run);
            var flow = new RunFlowController();
            flow.ResolveRoundEndBeforeBattle();
            Assert(CasualPvpSystem.IsLocked(run), "round end must lock the run before matching");
            var afterFirst = JsonUtility.ToJson(run);
            flow.ResolveRoundEndBeforeBattle();
            Assert(afterFirst == JsonUtility.ToJson(run), "round end retry must not grant growth again");
            Assert(!flow.RefreshShop() && !flow.UpgradeShop() && !flow.ToggleShopLock()
                && !flow.BuyUnit(0) && !flow.DeployUnit(0) && !flow.SellHandUnit(0)
                && !flow.SellBoardUnit("1-1") && !flow.MoveBoardUnit("1-1", "2-1")
                && !flow.UseForestGemCard(0, "1-1"), "locked run must reject manage commands");
            Assert(afterFirst == JsonUtility.ToJson(run), "blocked commands must not mutate the checkpoint");

            var opponent = new CasualPvpSnapshotState { snapshotId = "test_opponent", round = 8 };
            CasualPvpSystem.LockOpponent(run, opponent, "test_match");
            run.shopCards.Add(null);
            run.pendingBattleUnitPick = null;
            var restored = CasualPvpSystem.CreateBattleCopy(run);
            Assert(restored.pendingBattleUnitPick == null && restored.shopCards[run.shopCards.Count - 1] == null,
                "battle copy must preserve absent rewards and empty shop slots");
            CasualPvpSystem.Normalize(restored);
            Assert(service.SaveCurrentRun().Success && service.LoadGame(3).Success,
                "locked checkpoint must save and load through a real casual slot");
            Assert(CasualPvpSystem.IsLocked(session.CurrentRun)
                && session.CurrentRun.phase == GamePhase.Battle
                && session.CurrentRun.casualPvpMatchId == "test_match",
                "disk load and legacy normalization must preserve lock and opponent");
            session.RestoreRun(run);
            Assert(CasualPvpSystem.IsLocked(restored) && restored.casualPvpMatchId == "test_match"
                && restored.casualPvpOpponent.snapshotId == "test_opponent"
                && restored.casualPvpPlayerSnapshot.snapshotId == run.casualPvpPlayerSnapshot.snapshotId,
                "serialized checkpoint must preserve both snapshots and the match");
            restored.playerHp -= 20;
            restored.pendingBattleRewards.nextRoundGold += 5;
            Assert(run.playerHp == 100 && run.pendingBattleRewards.nextRoundGold == 0,
                "battle copy must isolate HP and nested rewards from save checkpoint");

            run.state = "manage";
            run.phase = GamePhase.NightManage;
            run.casualPvpRoundEndResolvedRound = 0;
            CasualPvpSystem.Normalize(run);
            Assert(CasualPvpSystem.IsLocked(run) && run.casualPvpRoundEndResolvedRound == 8,
                "legacy matched save must migrate without repeating round end");

            var unit = session.Data.Units[0];
            run.boardUnits.Add(new BoardUnitState { unitId = unit.id, boardSlotId = "1-1", baseCount = 1 });
            opponent.units.Add(new CasualPvpUnitState { unitId = unit.id, slotId = "1-1", count = 1, maxHp = 1, attack = 1 });
            var checkpoint = JsonUtility.ToJson(run);
            var simulated = CasualPvpSystem.CreateBattleCopy(run);
            var battle = new BattleStubSystem();
            var first = battle.Resolve(simulated);
            var repeated = battle.Resolve(CasualPvpSystem.CreateBattleCopy(run));
            Assert(checkpoint == JsonUtility.ToJson(run), "real combat must not mutate saved checkpoint");
            Assert(first.Victory == repeated.Victory && first.HpDelta == repeated.HpDelta,
                "replaying checkpoint must preserve combat outcome");

            run.round = 15;
            run.playerHp = 60;
            flow.ResolveBattleOutcome(new BattleStubResult { Victory = true });
            Assert(run.state == "casual_milestone" && run.casualPvpMilestoneOffered,
                "round 15 must enter milestone");
            var wins = run.campaignWins;
            flow.ResolveBattleOutcome(new BattleStubResult { Victory = true });
            Assert(run.campaignWins == wins, "duplicate settlement must not count another win");
            restored = CasualPvpSystem.CreateBattleCopy(run);
            CasualPvpSystem.Normalize(restored);
            session.RestoreRun(restored);
            Assert(restored.state == "casual_milestone", "milestone must survive serialization");
            Assert(flow.FinishCasualPvpAtMilestone() && !flow.FinishCasualPvpAtMilestone(),
                "finish choice must apply only once");
            session.RestoreRun(run);
            Assert(flow.ContinueCasualPvpAfterMilestone() && run.round == 16 && run.casualPvpEndless,
                "continue must advance into endless round 16");
            Assert(!flow.ContinueCasualPvpAfterMilestone() && run.round == 16,
                "continue retry must not advance another round");
            run.playerHp = 0;
            CasualPvpSystem.LockOpponent(run, new CasualPvpSnapshotState { snapshotId = "round16", round = 16 });
            flow.ResolveBattleOutcome(new BattleStubResult { Victory = false });
            Assert(run.state == "gameover" && run.playerHp == 0, "zero HP must end endless play");
            CheckReportQueue(run, service);
            CheckMirrorAttributes(session);
            CheckLineups(session);
            Debug.Log("Casual PVP recovery checks passed: lock, serialization, isolation, migration, milestone and defeat.");
        }
        finally
        {
            session.RestoreRun(previous);
            if (createdSession) UnityEngine.Object.DestroyImmediate(session.gameObject);
        }
    }

    private static void CheckLineups(ProphecyGameSession session)
    {
        var factory = typeof(CasualPvpSystem).GetMethod("CreateSystemFallback",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        for (var round = 1; round <= 200; round++)
        {
            var mirror = (CasualPvpSnapshotState)factory.Invoke(null, new object[] { round });
            Assert(mirror.round == round && CasualPvpSystem.HasValidLineup(mirror), "fallback must have a legal same-round lineup: " + round);
            Assert(mirror.powerScore == BattleStubSystem.EstimateCasualPvpSnapshotScore(mirror),
                "fallback score must use the shared battle estimator: " + round);
            foreach (var unit in mirror.units)
                Assert(unit.star <= Math.Min(6, 1 + (round - 1) / 3), "fallback star progression must match the round");
        }
        var late = (CasualPvpSnapshotState)factory.Invoke(null, new object[] { 100000 });
        Assert(late.round == 100000 && CasualPvpSystem.HasValidLineup(late), "maximum server round must have a legal fallback");
        Assert(late.powerScore == BattleStubSystem.EstimateCasualPvpSnapshotScore(late),
            "late-round fallback score must use the shared battle estimator");
        var large = session.Data.Units[0];
        var originalSize = large.size;
        try
        {
            large.size = 2; // Exercise the supported footprint rule even when all shipped units are size=1.
            var test = new CasualPvpSnapshotState();
            test.units.Add(new CasualPvpUnitState { unitId = large.id, slotId = "2-2" });
            Assert(CasualPvpSystem.HasValidLineup(test), "large unit must occupy two valid slots");
            test.units.Add(new CasualPvpUnitState { unitId = "small_merchant", slotId = "2-1" });
            Assert(!CasualPvpSystem.HasValidLineup(test), "distinct anchors must not conceal footprint overlap");
            test.units.RemoveAt(1);
            test.units[0].slotId = "1-1";
            Assert(!CasualPvpSystem.HasValidLineup(test), "large unit must not extend outside board");
            test.units[0].unitId = "missing_unit";
            Assert(!CasualPvpSystem.HasValidLineup(test), "unknown units must be rejected");
        }
        finally { large.size = originalSize; }
        Debug.Log("Casual PVP lineup checks passed: footprint overlap, edge bounds, rounds 1-200 and 100000.");
    }

    private static void CheckReportQueue(RunState run, CasualPvpSaveSlotService service)
    {
        var report = new CasualPvpBattleReport { matchId = "queue_match", snapshotId = "opponent", round = 16,
            requesterRunId = run.casualPvpRunId, victory = false, hpDelta = -5 };
        Assert(!CasualPvpSystem.QueueReport(run, report), "local fallback must not report to a server");
        run.casualPvpMatchEndpoint = "http://127.0.0.1:8765";
        Assert(CasualPvpSystem.QueueReport(run, report) && !CasualPvpSystem.QueueReport(run, report),
            "report must enter outbox only once");
        Assert(service.SaveCurrentRun().Success && service.LoadGame(3).Success, "outbox must survive disk load");
        run = ProphecyGameSession.Instance.CurrentRun;
        var pending = run.casualPvpPendingReports[0];
        Assert(pending.endpoint == "http://127.0.0.1:8765" && pending.report.matchId == "queue_match",
            "outbox must retain original endpoint and match ID");
        CasualPvpSystem.CompleteReportAttempt(run, pending, false, "timeout");
        Assert(service.SaveCurrentRun().Success && service.LoadGame(3).Success, "failed attempt must survive reload");
        run = ProphecyGameSession.Instance.CurrentRun;
        pending = run.casualPvpPendingReports[0];
        Assert(pending.attempts == 1 && pending.lastError == "timeout", "failed report must remain queued");
        Assert(!CasualPvpNetworkClient.IsReportAcknowledged("{}")
            && !CasualPvpNetworkClient.IsReportAcknowledged("{\"success\":false}")
            && !CasualPvpNetworkClient.IsReportAcknowledged("not json")
            && CasualPvpNetworkClient.IsReportAcknowledged("{\"success\":true,\"created\":false}"),
            "only explicit server success, including duplicate receipt, acknowledges a result");
        CasualPvpSystem.CompleteReportAttempt(run, pending, true, null);
        Assert(service.SaveCurrentRun().Success && service.LoadGame(3).Success
            && ProphecyGameSession.Instance.CurrentRun.casualPvpPendingReports.Count == 0,
            "acknowledged report must stay removed after reload");
        Debug.Log("Casual PVP outbox checks passed.");
    }

    private static void CheckMirrorAttributes(ProphecyGameSession session)
    {
        var definition = session.Data.Units[0];
        var run = new RunState { round = 8, state = "manage", phase = GamePhase.NightManage };
        CasualPvpSystem.InitializeRun(run);
        session.RestoreRun(run);
        var unit = new BoardUnitState { unitId = definition.id, boardSlotId = "1-1", isGolden = true,
            star = definition.star, baseCount = 7, roundTempCount = 3, shopBuffAttack = 5,
            roundTempAttack = 2, boardAuraAttack = 4, shopBuffDefense = 3, shopBuffPower = 4,
            roundTempPower = 2, shopBuffSpeed = 2, shopBuffLuck = 3, shopBuffMorale = 4,
            roundTempMorale = 1, forestGemsReceived = 11, forestGemsAttached = 9 };
        unit.battleProgressCounters.Add(new BattleProgressCounterState { key = "test", value = 3 });
        run.boardUnits.Add(unit);
        run.manageResources.forestGiftTotal = 23;
        var snapshot = CasualPvpSystem.CapturePlayerSnapshot(run);
        run.casualPvpOpponent = snapshot;
        Assert(snapshot.teamForestGiftTotal == 23 && snapshot.units[0].forestGemsReceived == 11
            && snapshot.units[0].forestGemsAttached == 9 && snapshot.units[0].battleProgressCounters[0].value == 3,
            "mirror must capture team and unit combat counters");
        run.manageResources.forestGiftTotal = 999;
        var buildEnemy = typeof(BattleStubSystem).GetMethod("BuildCasualPvpEnemyRuntimeUnits",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        var enemies = (System.Collections.IList)buildEnemy.Invoke(null, new object[] { snapshot });
        var runtime = enemies[0];
        var runtimeType = runtime.GetType();
        var source = (UnitCardState)runtimeType.GetField("SourceState").GetValue(runtime);
        Assert((int)runtimeType.GetField("TeamForestGiftTotal").GetValue(runtime) == 23
            && source.forestGemsReceived == 11 && source.forestGemsAttached == 9
            && source.battleProgressCounters[0].value == 3,
            "enemy counters must come from its mirror rather than the current player's run");
        var preview = new BattleStubSystem().CreatePreview(run);
        var own = preview.InitialPlayerUnits[0];
        var enemy = preview.InitialEnemyUnits[0];
        Assert(own.IsGolden == enemy.IsGolden && own.Star == enemy.Star && own.BaseCount == enemy.BaseCount
            && own.MaxHp == enemy.MaxHp && own.Attack == enemy.Attack && own.Defense == enemy.Defense
            && own.Power == enemy.Power && own.Speed == enemy.Speed && own.Luck == enemy.Luck
            && own.Morale == enemy.Morale && own.AttackInterval == enemy.AttackInterval,
            "mirrored initial combat stats must equal the actual player stats");
        Debug.Log("Casual PVP mirror attribute checks passed.");
    }

    private static void WriteValidSlot(CasualPvpSaveSlotService service, int slotIndex, bool endless, int round, int hp)
    {
        var now = DateTime.UtcNow.ToString("O");
        var phase = endless ? "无尽阶段" : $"第 {round} / 15 回合";
        var file = new SaveSlotFile
        {
            formatVersion = SaveSlotService.CurrentFormatVersion,
            metadata = new SaveSlotMetadata
            {
                slotIndex = slotIndex,
                saveId = Guid.NewGuid().ToString("N"),
                createdAtUtc = now,
                lastSavedAtUtc = now,
                gameVersion = "test",
                chapter = "休闲对战",
                mapOrLevel = phase,
                summary = $"{phase} · 生命 {hp}"
            },
            gameData = new RunState
            {
                saveVersion = 1,
                gameMode = GameModeIds.CasualPvp,
                campaignId = GameModeIds.CasualPvp,
                state = "manage",
                phase = GamePhase.NightManage,
                round = round,
                playerHp = hp,
                maxFateValue = 100,
                casualPvpRunId = Guid.NewGuid().ToString("N"),
                casualPvpEndless = endless
            }
        };
        File.WriteAllText(service.GetSlotPath(slotIndex), JsonUtility.ToJson(file, true));
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("Casual PVP self test failed: " + message);
    }
}
