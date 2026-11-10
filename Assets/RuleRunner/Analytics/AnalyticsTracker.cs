using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using RuleRunner.Core;
using UnityEngine;
using UnityEngine.Networking;

namespace RuleRunner.Analytics
{
    /// <summary>
    /// Builds a RunRecord after every run and POSTs it as JSON to a Google Apps Script web app.
    /// The endpoint lives in Resources/analytics_config.json; empty endpoint = log only.
    /// Content-Type is text/plain so the browser sends a "simple" request without a CORS preflight.
    /// </summary>
    public class AnalyticsTracker : MonoBehaviour
    {
        [Serializable]
        class Config
        {
            public bool enabled = true;
            public string endpoint = "";
            public string build = "dev";
        }

        public string SessionId { get; private set; }

        /// <summary>Stays the same across reloads in one browser (PlayerPrefs, which WebGL keeps in IndexedDB).</summary>
        public string PlayerId { get; private set; }

        const string PlayerIdKey = "analytics.playerId";

        Config config;
        readonly Dictionary<string, int> attempts = new Dictionary<string, int>();
        string currentLevel;
        float levelStartTime;

        void Awake()
        {
            SessionId = Guid.NewGuid().ToString("N").Substring(0, 12);
            PlayerId = PlayerPrefs.GetString(PlayerIdKey, "");
            if (string.IsNullOrEmpty(PlayerId))
            {
                PlayerId = Guid.NewGuid().ToString("N").Substring(0, 12);
                PlayerPrefs.SetString(PlayerIdKey, PlayerId);
                PlayerPrefs.Save();
            }
            var asset = Resources.Load<TextAsset>("analytics_config");
            config = asset != null ? JsonUtility.FromJson<Config>(asset.text) : new Config { enabled = false };
            BuildTag = (config.build ?? "") + (IsInternal(Application.isEditor, Application.absoluteURL) ? "-internal" : "");
            Debug.Log("[Analytics] session " + SessionId + (string.IsNullOrEmpty(config.endpoint) ? " (no endpoint: log only)" : ""));
        }

        /// <summary>
        /// The build tag written on every row. Runs from the Editor or a local server get "-internal" appended,
        /// and so does any page opened with ?internal=1, so team testing can be filtered out of playtest data.
        /// </summary>
        public string BuildTag { get; private set; }

        /// <summary>Call when the player opens a level: resets the level timer and sends a level_start row.</summary>
        public void LevelStarted(string levelId)
        {
            currentLevel = levelId;
            levelStartTime = Time.realtimeSinceStartup;
            attempts.TryGetValue(levelId, out int runsSoFar);
            Send(new RunRecord
            {
                sessionId = SessionId,
                playerId = PlayerId,
                build = BuildTag,
                eventType = RunRecord.LevelStart,
                levelId = levelId,
                attempt = runsSoFar,
                timestampUtc = DateTime.UtcNow.ToString("o"),
            });
        }

        public RunRecord RunFinished(LevelDef level, IList<Rule> rules, RunResult result, int hintsUsed = 0)
        {
            attempts.TryGetValue(level.Id, out int n);
            attempts[level.Id] = ++n;
            if (currentLevel != level.Id) LevelStarted(level.Id);

            EndPosition(level, result, out int endX, out int endFloor);
            var record = new RunRecord
            {
                sessionId = SessionId,
                playerId = PlayerId,
                build = BuildTag,
                eventType = RunRecord.RunEnd,
                levelId = level.Id,
                attempt = n,
                result = Vocabulary.Id(result.Outcome),
                qiLeft = result.QiLeft,
                ruleCount = rules.Count,
                rules = Rule.JoinIds(rules),
                steps = result.Steps,
                ranOutOfQi = result.RanOutOfQi,
                secondsSinceLevelStart = Mathf.RoundToInt(Time.realtimeSinceStartup - levelStartTime),
                hintsUsed = hintsUsed,
                endX = endX,
                endFloor = endFloor,
                timestampUtc = DateTime.UtcNow.ToString("o"),
            };
            Send(record);
            return record;
        }

        /// <summary>Where the run ended: the last step's tile and floor, or the start tile if no step was taken.</summary>
        public static void EndPosition(LevelDef level, RunResult result, out int x, out int floor)
        {
            var frames = result.Frames;
            if (frames != null && frames.Count > 0)
            {
                var last = frames[frames.Count - 1];
                x = last.X;
                floor = last.Floor;
            }
            else
            {
                x = level.Start;
                floor = 0;
            }
        }

        /// <summary>True for team testing: the Editor, a local server, or a page URL containing internal=1.</summary>
        public static bool IsInternal(bool isEditor, string url)
        {
            if (isEditor) return true;
            if (string.IsNullOrEmpty(url)) return false;
            string u = url.ToLowerInvariant();
            return u.Contains("://localhost") || u.Contains("://127.0.0.1") || u.Contains("internal=1");
        }

        void Send(RunRecord record)
        {
            string json = JsonUtility.ToJson(record);
            if (config == null || !config.enabled) return;
            if (string.IsNullOrEmpty(config.endpoint))
            {
                Debug.Log("[Analytics] " + json);
                return;
            }
            StartCoroutine(Post(config.endpoint, json));
        }

        static IEnumerator Post(string url, string json)
        {
            using (var req = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST))
            {
                req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "text/plain;charset=utf-8");
                req.timeout = 15;
                yield return req.SendWebRequest();
                if (req.result != UnityWebRequest.Result.Success)
                    Debug.LogWarning("[Analytics] upload failed: " + req.error);
            }
        }
    }
}
