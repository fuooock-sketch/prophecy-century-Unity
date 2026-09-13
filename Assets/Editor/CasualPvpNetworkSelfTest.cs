using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using ProphecyCentury.Core;
using ProphecyCentury.Model;
using ProphecyCentury.Systems;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

// Runs real UnityWebRequest coroutines on editor updates, without blocking native networking.
public static class CasualPvpNetworkSelfTest
{
    private static readonly Stack<IEnumerator> Routines = new Stack<IEnumerator>();
    private static AsyncOperation _waiting;
    private static double _deadline;
    private static string _endpoint;

    public static void StartBatch(string endpoint)
    {
        _endpoint = endpoint;
        _deadline = EditorApplication.timeSinceStartup + 90;
        Routines.Push(CheckNetwork());
        EditorApplication.update += Tick;
    }

    private static void Tick()
    {
        try
        {
            if (EditorApplication.timeSinceStartup > _deadline) throw new Exception("Network test timed out");
            if (_waiting != null && !_waiting.isDone) return;
            _waiting = null;
            for (var step = 0; step < 32 && Routines.Count > 0; step++)
            {
                var routine = Routines.Peek();
                if (!routine.MoveNext()) { (routine as IDisposable)?.Dispose(); Routines.Pop(); continue; }
                if (routine.Current is IEnumerator nested) { Routines.Push(nested); continue; }
                _waiting = routine.Current as AsyncOperation;
                return;
            }
            if (Routines.Count == 0) Finish(null);
        }
        catch (Exception ex) { Finish(ex); }
    }

    private static void Finish(Exception error)
    {
        EditorApplication.update -= Tick;
        while (Routines.Count > 0) (Routines.Pop() as IDisposable)?.Dispose();
        if (error != null) Debug.LogException(error);
        else Debug.Log("Casual PVP network integration passed.");
        EditorApplication.Exit(error == null ? 0 : 1);
    }

    private static IEnumerator CheckNetwork()
    {
        var directory = Path.Combine(Path.GetTempPath(), "pvp-network-test-" + Guid.NewGuid().ToString("N"));
        var session = ProphecyGameSession.EnsureInstance();
        var data = new ProphecyCentury.Data.GameDataRepository();
        data.LoadAll();
        typeof(ProphecyGameSession).GetProperty("Instance").GetSetMethod(true).Invoke(null, new object[] { session });
        typeof(ProphecyGameSession).GetProperty("Data").GetSetMethod(true).Invoke(session, new object[] { data });
        try
        {
            var slots = new CasualPvpSaveSlotService(directory, false);
            Assert(slots.CreateNewGame(1).Success, "reserve first slot");
            var client = new CasualPvpNetworkClient(_endpoint, () => slots.SaveCurrentRun().Success);
            var run = NewRun(session, 8);
            yield return MatchAndQueue(client, run);
            Assert(slots.SaveCurrentRun().Success, "persist result before send");

            yield return Control("unavailable");
            yield return client.FlushReports(run);
            Assert(run.casualPvpPendingReports.Count == 1 && run.casualPvpPendingReports[0].attempts == 1,
                "HTTP failure must retain result");
            Assert(slots.LoadGame(1).Success, "reload offline result");
            run = session.CurrentRun;
            Assert(run.casualPvpPendingReports.Count == 1, "failed result must be durable");
            yield return Control("normal");
            yield return client.FlushReports(run);
            Assert(slots.LoadGame(1).Success && session.CurrentRun.casualPvpPendingReports.Count == 0,
                "recovery must persist acknowledgment");
            yield return AssertResultCount(1);

            run = NewRun(session, 9);
            yield return MatchAndQueue(client, run);
            Assert(slots.SaveCurrentRun().Success, "persist second result");
            yield return Control("drop_ack");
            yield return client.FlushReports(run);
            // Transport may internally replay a closed connection; either way the DB must stay idempotent.
            yield return client.FlushReports(run);
            Assert(run.casualPvpPendingReports.Count == 0, "lost receipt must recover on retry");
            yield return AssertResultCount(2);

            run = NewRun(session, 10);
            yield return MatchAndQueue(client, run);
            Assert(slots.SaveCurrentRun().Success, "persist delayed result in slot one");
            yield return Control("delay_result");
            using (var flight = (IDisposable)client.FlushReports(run))
            {
                var routine = (IEnumerator)flight;
                Assert(routine.MoveNext(), "start delayed report request");
                var operation = routine.Current;
                var other = NewRun(session, 3);
                client.CancelReportFlush();
                Assert(slots.CreateNewGame(2).Success && slots.SaveCurrentRun().Success, "switch to second save slot");
                yield return operation;
                while (routine.MoveNext()) yield return routine.Current;
                Assert(other.casualPvpPendingReports.Count == 0 && run.casualPvpPendingReports.Count == 1,
                    "old response must not mutate either newly selected run or old outbox");
            }
            yield return Control("normal");
            Assert(slots.LoadGame(1).Success, "return to first slot");
            var changedEndpoint = new CasualPvpNetworkClient("http://127.0.0.1:1", () => slots.SaveCurrentRun().Success);
            yield return changedEndpoint.FlushReports(session.CurrentRun);
            Assert(session.CurrentRun.casualPvpPendingReports.Count == 0, "retry must use original server endpoint");
            yield return AssertResultCount(3);

            run = NewRun(session, 11);
            yield return Control("delay_upload");
            var callback = false;
            using (var flight = (IDisposable)client.PrepareMatch(run, CasualPvpSystem.CapturePlayerSnapshot(run), _ => callback = true))
            {
                var routine = (IEnumerator)flight;
                Assert(routine.MoveNext(), "start delayed match upload");
                var operation = routine.Current;
                var other = NewRun(session, 4);
                yield return operation;
                while (routine.MoveNext()) yield return routine.Current;
                Assert(!callback && run.casualPvpOpponent == null && other.casualPvpOpponent == null,
                    "switching run during upload must cancel old matchmaking callback");
            }

            var closedPort = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
            closedPort.Start();
            var offlineEndpoint = "http://127.0.0.1:" + ((System.Net.IPEndPoint)closedPort.LocalEndpoint).Port;
            closedPort.Stop();
            run = NewRun(session, 12);
            var offline = new CasualPvpNetworkClient(offlineEndpoint);
            CasualPvpMatchResponse local = null;
            yield return offline.PrepareMatch(run, CasualPvpSystem.CapturePlayerSnapshot(run), response => local = response);
            Assert(local?.success == true && local.snapshot.round == 12 && local.snapshot.units.Count > 0
                && string.IsNullOrEmpty(run.casualPvpMatchEndpoint), "unreachable server must fall back to a same-round local opponent");
            Assert(!CasualPvpSystem.QueueReport(run, new CasualPvpBattleReport { matchId = local.matchId }),
                "offline opponent must not create a remote report");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(session.gameObject);
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }

    private static RunState NewRun(ProphecyGameSession session, int round)
    {
        var run = new RunState { saveVersion = 1, round = round, state = "manage", phase = GamePhase.NightManage };
        CasualPvpSystem.InitializeRun(run);
        run.boardUnits.Add(new BoardUnitState { unitId = session.Data.Units[0].id, boardSlotId = "1-1", baseCount = 2 });
        session.RestoreRun(run);
        return run;
    }

    private static IEnumerator MatchAndQueue(CasualPvpNetworkClient client, RunState run)
    {
        CasualPvpMatchResponse response = null;
        yield return client.PrepareMatch(run, CasualPvpSystem.CapturePlayerSnapshot(run), value => response = value);
        Assert(response?.success == true && !response.fallback && run.casualPvpMatchEndpoint == _endpoint,
            "real upload and same-round matching must succeed");
        Assert(response.snapshot.round == run.round && response.snapshot.sourceRunId != run.casualPvpRunId,
            "opponent must be another run in the same round");
        Assert(CasualPvpSystem.QueueReport(run, new CasualPvpBattleReport
        {
            matchId = response.matchId, snapshotId = response.snapshot.snapshotId, requesterRunId = run.casualPvpRunId,
            round = run.round, victory = true, hpDelta = 0, playerScore = 10, enemyScore = 9
        }), "queue matched result");
    }

    private static IEnumerator Control(string mode)
    {
        using (var request = new UnityWebRequest(_endpoint + "/test/control", "POST"))
        {
            request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes("{\"mode\":\"" + mode + "\"}"));
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            yield return request.SendWebRequest();
            Assert(request.responseCode == 200, "fault fixture control");
        }
    }

    private static IEnumerator AssertResultCount(int expected)
    {
        using (var request = UnityWebRequest.Get(_endpoint + "/test/state"))
        {
            yield return request.SendWebRequest();
            Assert(JsonUtility.FromJson<CountResponse>(request.downloadHandler.text).count == expected,
                "server must store exactly one result per match");
        }
    }

    [Serializable] private sealed class CountResponse { public int count; }
    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("PVP network integration: " + message);
    }
}
