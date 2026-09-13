using System;
using System.Collections;
using System.Text;
using System.Linq;
using ProphecyCentury.Core;
using ProphecyCentury.Model;
using UnityEngine;
using UnityEngine.Networking;

namespace ProphecyCentury.Systems
{
    public sealed class CasualPvpNetworkClient
    {
        private const string EndpointPreferenceKey = "ProphecyCentury.CasualPvp.Endpoint";
        private const string LocalEndpoint = "http://127.0.0.1:8765";
        private const string EndpointConfigResourcePath = "Config/casual_pvp_network";
        private bool _flushing;
        private int _flushGeneration;
        private readonly string _endpointOverride;
        private readonly Func<bool> _saveCurrentRun;

        public CasualPvpNetworkClient(string endpointOverride = null, Func<bool> saveCurrentRun = null)
        {
            _endpointOverride = endpointOverride;
            _saveCurrentRun = saveCurrentRun ?? (() => new SaveGameSystem().SaveCurrentRun());
        }

        public string Endpoint => (_endpointOverride ?? PlayerPrefs.GetString(EndpointPreferenceKey, PackagedEndpoint)).TrimEnd('/');
        public void CancelReportFlush() { _flushGeneration++; _flushing = false; }

        public IEnumerator PrepareMatch(RunState run, CasualPvpSnapshotState playerSnapshot, Action<CasualPvpMatchResponse> completed)
        {
            if (run == null || playerSnapshot == null)
            {
                completed?.Invoke(new CasualPvpMatchResponse { success = false, error = "镜像请求内容为空。" });
                yield break;
            }

            var endpoint = Endpoint;
            if (playerSnapshot.isCheatRun)
            {
                Debug.Log("[CasualPvp] Debug-assisted run will not upload or consume online mirror matches.");
                completed?.Invoke(CasualPvpSystem.MatchLocally(run, playerSnapshot));
                yield break;
            }
            if (ProphecyGameSession.Instance?.CurrentRun != run) yield break;
            var uploadSucceeded = false;
            using (var upload = CreatePost(endpoint + "/api/v1/snapshots", JsonUtility.ToJson(playerSnapshot)))
            {
                yield return upload.SendWebRequest();
                if (ProphecyGameSession.Instance?.CurrentRun != run) yield break;
                uploadSucceeded = IsSuccess(upload);
            }

            if (uploadSucceeded)
            {
                var requestBody = new CasualPvpMatchRequest
                {
                    round = run.round,
                    requesterRunId = run.casualPvpRunId,
                    contentVersion = Application.version,
                    combatVersion = CasualPvpSystem.CombatVersion,
                    excludeSnapshotIds = run.casualPvpRecentOpponentIds
                };
                using (var matchRequest = CreatePost(endpoint + "/api/v1/matches", JsonUtility.ToJson(requestBody)))
                {
                    yield return matchRequest.SendWebRequest();
                    if (ProphecyGameSession.Instance?.CurrentRun != run) yield break;
                    if (IsSuccess(matchRequest))
                    {
                        CasualPvpMatchResponse response = null;
                        try
                        {
                            response = JsonUtility.FromJson<CasualPvpMatchResponse>(matchRequest.downloadHandler.text);
                        }
                        catch (Exception ex)
                        {
                            Debug.LogWarning($"[CasualPvp] Match response parse failed: {ex.Message}");
                        }
                        if (response != null && response.success && !string.IsNullOrWhiteSpace(response.matchId)
                            && response.snapshot?.sourceRunId != run.casualPvpRunId && IsUsable(response.snapshot, run.round))
                        {
                            CasualPvpSystem.LockOpponent(run, response.snapshot, response.matchId, endpoint);
                            completed?.Invoke(response);
                            yield break;
                        }
                    }
                }
            }

            var fallback = CasualPvpSystem.MatchLocally(run, playerSnapshot);
            fallback.fallback = string.Equals(fallback.snapshot?.sourceType, "system_fallback", StringComparison.Ordinal);
            completed?.Invoke(fallback);
        }

        public IEnumerator FlushReports(RunState run)
        {
            if (_flushing || run?.casualPvpPendingReports == null || run.casualPvpPendingReports.Count == 0) yield break;
            _flushing = true;
            var generation = _flushGeneration;
            try
            {
                foreach (var pending in run.casualPvpPendingReports.OrderBy(item => item.attempts).Take(5).ToArray())
                {
                    if (generation != _flushGeneration || ProphecyGameSession.Instance?.CurrentRun != run) yield break;
                    using (var request = CreatePost(pending.endpoint.TrimEnd('/') + $"/api/v1/matches/{Uri.EscapeDataString(pending.report.matchId)}/result", JsonUtility.ToJson(pending.report)))
                    {
                        yield return request.SendWebRequest();
                        if (generation != _flushGeneration || ProphecyGameSession.Instance?.CurrentRun != run) yield break;
                        var acknowledged = IsSuccess(request) && IsReportAcknowledged(request.downloadHandler.text);
                        CasualPvpSystem.CompleteReportAttempt(run, pending, acknowledged,
                            acknowledged ? null : $"HTTP {request.responseCode}: {request.error ?? "未收到成功确认"}");
                        _saveCurrentRun();
                        if (!acknowledged) yield break;
                    }
                }
            }
            finally { if (generation == _flushGeneration) _flushing = false; }
        }

        public static bool IsReportAcknowledged(string body)
        {
            try { return JsonUtility.FromJson<ReportReceipt>(body)?.success == true; }
            catch { return false; }
        }

        [Serializable]
        private sealed class ReportReceipt { public bool success; }

        [Serializable]
        private sealed class EndpointConfig { public string defaultEndpoint; }

        private static string PackagedEndpoint
        {
            get
            {
                var config = Resources.Load<TextAsset>(EndpointConfigResourcePath);
                if (config == null || string.IsNullOrWhiteSpace(config.text)) return LocalEndpoint;
                try
                {
                    var endpoint = JsonUtility.FromJson<EndpointConfig>(config.text)?.defaultEndpoint?.Trim();
                    return Uri.TryCreate(endpoint, UriKind.Absolute, out var uri)
                        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
                        ? endpoint.TrimEnd('/')
                        : LocalEndpoint;
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[CasualPvp] Endpoint config parse failed: {ex.Message}");
                    return LocalEndpoint;
                }
            }
        }

        private static UnityWebRequest CreatePost(string url, string json)
        {
            var request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST)
            {
                uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json ?? "{}")),
                downloadHandler = new DownloadHandlerBuffer(),
                timeout = 2
            };
            request.SetRequestHeader("Content-Type", "application/json");
            return request;
        }

        private static bool IsSuccess(UnityWebRequest request)
        {
#if UNITY_2020_2_OR_NEWER
            return request.result == UnityWebRequest.Result.Success;
#else
            return !request.isNetworkError && !request.isHttpError;
#endif
        }

        private static bool IsUsable(CasualPvpSnapshotState snapshot, int round)
        {
            return snapshot != null
                && snapshot.round == round
                && string.Equals(snapshot.contentVersion, Application.version, StringComparison.Ordinal)
                && string.Equals(snapshot.combatVersion, CasualPvpSystem.CombatVersion, StringComparison.Ordinal)
                && CasualPvpSystem.HasValidLineup(snapshot);
        }
    }
}
