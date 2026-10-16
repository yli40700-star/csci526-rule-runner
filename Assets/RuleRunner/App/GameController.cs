using System.Collections;
using System.Collections.Generic;
using RuleRunner.Analytics;
using RuleRunner.Cards;
using RuleRunner.Core;
using RuleRunner.Levels;
using RuleRunner.UI;
using RuleRunner.View;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RuleRunner.App
{
    /// <summary>
    /// Entry point. Put this on one GameObject in an empty scene: it builds the camera, map, runner and UI,
    /// then wires them together. Run flow: simulate the whole run in Core, then play the frames back.
    /// </summary>
    public class GameController : MonoBehaviour
    {
        const float BaseStepSeconds = 0.4f;
        const string ClearedKey = "rr_cleared_";

        CameraFitter cameraFitter;
        MapView map;
        RunnerView runner;
        GameScreen gameScreen;
        LoadoutPanel loadout;
        LevelSelectScreen levelSelect;
        ResultPopup resultPopup;
        AnalyticsTracker analytics;

        LevelDef level;
        readonly Dictionary<string, List<Rule>> rulesByLevel = new Dictionary<string, List<Rule>>();
        Coroutine playback;
        float speed = 1f;

        void Awake()
        {
            Application.targetFrameRate = 60;
            BuildWorld();
            BuildUI();
            analytics = gameObject.AddComponent<AnalyticsTracker>();
        }

        void Start() => ShowLevelSelect();

        void BuildWorld()
        {
            var cam = Camera.main;
            if (cam == null)
            {
                cam = new GameObject("Main Camera", typeof(Camera)).GetComponent<Camera>();
                cam.tag = "MainCamera";
            }
            cameraFitter = cam.GetComponent<CameraFitter>();
            if (cameraFitter == null) cameraFitter = cam.gameObject.AddComponent<CameraFitter>();
            cameraFitter.headerHeight = GameScreen.HeaderFraction;
            cameraFitter.viewportHeight = 1f - GameScreen.HeaderFraction - GameScreen.BottomFraction - 34f / 720f;

            // The main camera only draws the map strip; this one clears the rest of the screen every frame.
            var bg = new GameObject("Background Camera").AddComponent<Camera>();
            bg.clearFlags = CameraClearFlags.SolidColor;
            bg.backgroundColor = Theme.Background;
            bg.cullingMask = 0;
            bg.depth = cam.depth - 1;

            map = new GameObject("MapView").AddComponent<MapView>();
            runner = new GameObject("Runner").AddComponent<RunnerView>();
        }

        void BuildUI()
        {
            if (FindAnyObjectByType<EventSystem>() == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

            var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.matchWidthOrHeight = 0.5f;
            var canvasRect = (RectTransform)canvasGo.transform;

            var picker = OptionPicker.Create(canvasRect);
            gameScreen = GameScreen.Create(canvasRect);
            loadout = LoadoutPanel.Create(gameScreen.LoadoutArea, picker);
            levelSelect = LevelSelectScreen.Create(canvasRect, IsCleared);
            resultPopup = ResultPopup.Create(canvasRect);
            picker.transform.SetAsLastSibling();

            levelSelect.LevelChosen += LoadLevel;
            gameScreen.Back += ShowLevelSelect;
            gameScreen.Run += StartRun;
            gameScreen.Reset += ResetRun;
            gameScreen.SpeedChanged += s => speed = s;
            resultPopup.BackToLevels += ShowLevelSelect;
            resultPopup.EditRules += ResetRun;
            resultPopup.NextLevel += () =>
            {
                var next = LevelDatabase.Next(level);
                if (next != null) LoadLevel(next); else ShowLevelSelect();
            };
        }

        // ---------- Screens ----------

        void ShowLevelSelect()
        {
            StopPlayback();
            resultPopup.Hide();
            gameScreen.Hide();
            map.gameObject.SetActive(false);
            runner.gameObject.SetActive(false);
            levelSelect.Show();
        }

        void LoadLevel(LevelDef def)
        {
            StopPlayback();
            level = def;
            levelSelect.Hide();
            resultPopup.Hide();
            map.gameObject.SetActive(true);
            runner.gameObject.SetActive(true);
            map.Build(level);
            cameraFitter.Fit(level.Map.Length);

            if (!rulesByLevel.TryGetValue(level.Id, out var rules))
                rulesByLevel[level.Id] = rules = new List<Rule>();
            gameScreen.Show();
            gameScreen.Setup(level);
            loadout.Setup(level, rules);
            ResetRun();
            analytics.LevelStarted(level.Id);
        }

        // ---------- Run / Reset ----------

        void StartRun()
        {
            if (level == null || playback != null) return;
            ResetRun();
            gameScreen.SetRunning(true);
            loadout.SetEditable(false);
            var rules = new List<Rule>(loadout.Rules.ConvertAll(r => r.Clone()));
            var result = Simulator.Run(level, rules);
            analytics.RunFinished(level, rules, result);
            gameScreen.Log("--- Run: " + rules.Count + (rules.Count == 1 ? " card" : " cards") + ", qi " + level.Qi + " ---", Theme.Dim);
            playback = StartCoroutine(Play(rules, result));
        }

        void ResetRun()
        {
            StopPlayback();
            resultPopup.Hide();
            if (level == null) return;
            map.ResetState();
            runner.Place(level.Start, level.Dir);
            gameScreen.SetRunning(false);
            loadout.SetEditable(true);
            loadout.SetQi(level.Qi, level.Qi);
            loadout.HighlightRule(-1, -1);
            gameScreen.HideFloat();
        }

        void StopPlayback()
        {
            if (playback != null) StopCoroutine(playback);
            playback = null;
        }

        IEnumerator Play(List<Rule> rules, RunResult result)
        {
            var hits = new int[rules.Count];
            foreach (var f in result.Frames)
            {
                float step = BaseStepSeconds / speed;
                map.SetTime(f.T);
                loadout.HighlightRule(f.FiredRule, f.StarvedRule);

                if (f.FiredRule >= 0)
                {
                    hits[f.FiredRule]++;
                    loadout.SetHits(hits);
                    var rule = rules[f.FiredRule];
                    int cost = Vocabulary.Cost(rule.Action);
                    string label = "Card " + (f.FiredRule + 1) + ": " + Vocabulary.Display(rule.Action) + (cost > 0 ? " -" + cost : "");
                    gameScreen.ShowFloat(cameraFitter.WorldToCanvasPoint(runner.HeadPosition, gameScreen.CanvasRect), label, step * 1.2f);
                }
                else if (f.StarvedRule >= 0)
                {
                    gameScreen.ShowFloat(cameraFitter.WorldToCanvasPoint(runner.HeadPosition, gameScreen.CanvasRect),
                        "Card " + (f.StarvedRule + 1) + ": no qi!", step * 1.2f);
                }
                loadout.SetQi(f.Qi, level.Qi);
                gameScreen.Log(DescribeStep(f, rules), f.StarvedRule >= 0 ? new Color(1f, 0.6f, 0.6f) : (Color?)null);

                yield return runner.PlayStep(f, step);
                if (f.CollectedOrb) map.CollectOrb(f.X);
            }

            loadout.HighlightRule(-1, -1);
            gameScreen.Log("Result: " + Vocabulary.Describe(result.Outcome) + "  (qi left " + result.QiLeft + ")",
                result.Won ? new Color(0.5f, 0.9f, 0.55f) : new Color(1f, 0.55f, 0.55f));
            yield return runner.PlayOutcome(result.Outcome, 0.7f);

            if (result.Won) PlayerPrefs.SetInt(ClearedKey + level.Id, 1);
            playback = null;
            resultPopup.Show(result, level, rules.Count, LevelDatabase.Next(level) != null);
        }

        static string DescribeStep(StepFrame f, List<Rule> rules)
        {
            string s = "Step " + (f.T + 1) + " - ";
            if (f.FiredRule >= 0)
                s += "Card " + (f.FiredRule + 1) + " (" + rules[f.FiredRule].ToDisplayString() + ") fired, qi left " + f.Qi;
            else if (f.StarvedRule >= 0)
                s += "Card " + (f.StarvedRule + 1) + " was true but needs " + Vocabulary.Cost(rules[f.StarvedRule].Action) +
                     " qi (have " + f.QiBefore + ") - walked instead";
            else
                s += "no card fired, Walk";
            if (f.Blocked) s += " - blocked by a wall";
            if (f.CollectedOrb) s += " - picked up a qi orb (+" + Vocabulary.OrbQi + ")";
            return s;
        }

        static bool IsCleared(string id) => PlayerPrefs.GetInt(ClearedKey + id, 0) == 1;
    }
}
