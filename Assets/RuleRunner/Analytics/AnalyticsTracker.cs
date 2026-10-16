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

        Config config;
        readonly Dictionary<string, int> attempts = new Dictionary<string, int>();
        string currentLevel;
        float levelStartTime;

        void Awake()
        {
            SessionId = Guid.NewGuid().ToString("N").Substring(0, 12);
            var asset = Resources.Load<TextAsset>("analytics_config");
            config = asset != null ? JsonUtility.FromJson<Config>(asset.text) : new Config { enabled = false };
            Debug.Log("[Analytics] session " + SessionId + (string.IsNullOrEmpty(config.endpoint) ? " (no endpoint: log only)" : ""));
        }

        /// <summary>Call when the player opens a level (resets the level timer).</summary>
        public void LevelStarted(string levelId)
        {
            currentLevel = levelId;
            levelStartTime = Time.realtimeSinceStartup;
        }

        public RunRecord RunFinished(LevelDef level, IList<Rule> rules, RunResult result)
        {
            attempts.TryGetValue(level.Id, out int n);
            attempts[level.Id] = ++n;
            if (currentLevel != level.Id) LevelStarted(level.Id);

            var record = new RunRecord
            {
                sessionId = SessionId,
                build = config?.build ?? "",
                levelId = level.Id,
                attempt = n,
                result = Vocabulary.Id(result.Outcome),
                qiLeft = result.QiLeft,
                ruleCount = rules.Count,
                rules = Rule.JoinIds(rules),
                steps = result.Steps,
                ranOutOfQi = result.RanOutOfQi,
                secondsSinceLevelStart = Mathf.RoundToInt(Time.realtimeSinceStartup - levelStartTime),
                timestampUtc = DateTime.UtcNow.ToString("o"),
            };
            Send(record);
            return record;
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
