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
        const string IntroSeenKey = "rr_intro_";
        const int FailsBeforeHint = 2;
        const int FailsBeforeNudge = 3;   // then again every 2 more failures

        CameraFitter cameraFitter;
        MapView map;
        RunnerView runner;
        GameScreen gameScreen;
        LoadoutPanel loadout;
        LevelSelectScreen levelSelect;
        ResultPopup resultPopup;
        IntroCard introCard;
        CoachOverlay coach;
        Tutorial tutorial;
        AnalyticsTracker analytics;

        LevelDef level;
        readonly Dictionary<string, List<Rule>> rulesByLevel = new Dictionary<string, List<Rule>>();
        Coroutine playback;
        float speed = 1f;

        // Guidance state per level (this session): failed runs, hints opened, where the last run died.
        readonly Dictionary<string, int> fails = new Dictionary<string, int>();
        readonly Dictionary<string, int> hintsShown = new Dictionary<string, int>();
        int deathX = -1;

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
            introCard = IntroCard.Create(canvasRect);
            coach = CoachOverlay.Create(canvasRect);
            picker.transform.SetAsLastSibling();
            tutorial = new Tutorial(coach, gameScreen, loadout, resultPopup, runner, cameraFitter);

            levelSelect.LevelChosen += LoadLevel;
            gameScreen.Back += ShowLevelSelect;
            gameScreen.Run += StartRun;
            gameScreen.Reset += ResetRun;
            gameScreen.SpeedChanged += s => speed = s;
            gameScreen.IntroRequested += () => { gameScreen.HideNudge(); ShowIntro(); };
            gameScreen.HintRequested += () => { gameScreen.HideNudge(); ShowNextHint(); };
            introCard.Closed += OnIntroClosed;
            loadout.CardHover += PreviewCard;
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
            tutorial.Abort();
            introCard.gameObject.SetActive(false);
            resultPopup.Hide();
            gameScreen.Hide();
            map.gameObject.SetActive(false);
            runner.gameObject.SetActive(false);
            levelSelect.Show();
        }

        void LoadLevel(LevelDef def)
        {
            StopPlayback();
            tutorial.Abort();
            level = def;
            deathX = -1;
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
            gameScreen.HideBanner();
            gameScreen.HideNudge();
            UpdateHintButton();
            analytics.LevelStarted(level.Id);

            if (PlayerPrefs.GetInt(IntroKey(level), 0) == 0 || NeedsTutorial()) ShowIntro();
        }

        // ---------- Intro card, tutorial, hints ----------

        /// <summary>
        /// "Intro seen" is stored per level and per intro content, so editing a level's intro
        /// (text or new-card demos) shows it again to players who saw the old one.
        /// </summary>
        static string IntroKey(LevelDef l)
        {
            var text = l.Hint + "|" + l.Guide.Concept;
            foreach (var item in l.Guide.NewItems) text += "|" + item.Title + "|" + item.Line + "|" + item.Map;
            uint h = 2166136261;                       // FNV-1a: stable across runs, unlike string.GetHashCode
            foreach (char c in text) { h ^= c; h *= 16777619; }
            return IntroSeenKey + l.Id + "_" + h.ToString("x8");
        }

        bool NeedsTutorial() => level != null && level.Id == "1-1" && !Tutorial.Done;

        void ShowIntro()
        {
            if (level == null || playback != null) return;
            introCard.Show(level);
            IntroItems.Build(introCard.DiagramArea, level.Guide);
        }

        void OnIntroClosed()
        {
            PlayerPrefs.SetInt(IntroKey(level), 1);
            if (NeedsTutorial() && !tutorial.Active) tutorial.Begin();
        }

        void UpdateHintButton()
        {
            int total = level.Guide.Hints.Count;
            fails.TryGetValue(level.Id, out int f);
            hintsShown.TryGetValue(level.Id, out int shown);
            gameScreen.SetHintButton(f >= FailsBeforeHint && shown < total, shown + 1, total);
        }

        void ShowNextHint()
        {
            hintsShown.TryGetValue(level.Id, out int shown);
            var hints = level.Guide.Hints;
            if (shown >= hints.Count) return;
            gameScreen.ShowBanner(hints[shown]);
            gameScreen.Log("Hint " + (shown + 1) + ": " + hints[shown], Theme.Gold);
            hintsShown[level.Id] = shown + 1;
            UpdateHintButton();
        }

        /// <summary>Hovering a card marks every spot where it would fire, by simulating the current cards.</summary>
        void PreviewCard(int index)
        {
            if (level == null || playback != null) return;
            if (index < 0)
            {
                map.ClearMarks();
                if (deathX >= 0) map.MarkDeath(deathX);
                return;
            }
            var result = Simulator.Run(level, loadout.Rules);
            var spots = new List<(int, int)>();
            foreach (var f in result.Frames)
                if (f.FiredRule == index) spots.Add((f.XBefore, f.DirBefore));
            map.ShowPreview(spots, Theme.ActionColor(loadout.Rules[index].Action));
        }

        // ---------- Run / Reset ----------

        void StartRun()
        {
            if (level == null || playback != null) return;
            deathX = -1;
            ResetRun();
            gameScreen.HideBanner();
            gameScreen.SetRunning(true);
            loadout.SetEditable(false);
            var rules = new List<Rule>(loadout.Rules.ConvertAll(r => r.Clone()));
            var result = Simulator.Run(level, rules);
            hintsShown.TryGetValue(level.Id, out int hintsUsed);
            analytics.RunFinished(level, rules, result, hintsUsed);
            gameScreen.Log("--- Run: " + rules.Count + (rules.Count == 1 ? " card" : " cards") + ", qi " + level.Qi + " ---", Theme.Dim);
            playback = StartCoroutine(Play(rules, result));
        }

        void ResetRun()
        {
            StopPlayback();
            resultPopup.Hide();
            if (level == null) return;
            map.ResetState();
            if (deathX >= 0) map.MarkDeath(deathX); // keep showing where the last run died while editing
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
                loadout.HighlightRule(f.FiredRule >= 0 ? f.FiredRule : f.HoldingRule, f.StarvedRule);

                if (f.FiredRule >= 0)
                {
                    hits[f.FiredRule]++;
                    loadout.SetHits(hits);
                    var rule = rules[f.FiredRule];
                    int cost = Vocabulary.Cost(rule.Action);
                    string label = "Card " + (f.FiredRule + 1) + ": " + Vocabulary.Display(rule.Action) + (cost > 0 ? " -" + cost : "");
                    gameScreen.ShowFloat(cameraFitter.WorldToCanvasPoint(runner.HeadPosition, gameScreen.CanvasRect), label, step * 1.2f);
                }
                else if (f.HoldingRule >= 0)
                {
                    gameScreen.ShowFloat(cameraFitter.WorldToCanvasPoint(runner.HeadPosition, gameScreen.CanvasRect),
                        "Card " + (f.HoldingRule + 1) + ": waiting...", step * 1.2f);
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

            string line = "";
            if (result.Won) PlayerPrefs.SetInt(ClearedKey + level.Id, 1);
            else
            {
                fails.TryGetValue(level.Id, out int f);
                line = GuideText.DeathLine(level, rules, result, f);
                fails[level.Id] = f + 1;
                if (result.Outcome != Outcome.Timeout && result.Frames.Count > 0)
                {
                    deathX = result.Frames[result.Frames.Count - 1].X;
                    map.MarkDeath(deathX);
                }
                gameScreen.Log(line, Theme.Gold);
                gameScreen.ShowBanner(line);
                UpdateHintButton();
                int failed = f + 1;
                if (failed >= FailsBeforeNudge && (failed - FailsBeforeNudge) % 2 == 0)
                {
                    hintsShown.TryGetValue(level.Id, out int shown);
                    gameScreen.ShowNudge(shown < level.Guide.Hints.Count);
                }
            }
            playback = null;
            resultPopup.Show(result, level, rules.Count, LevelDatabase.Next(level) != null, line);
            tutorial.OnRunFinished(result);
        }

        static string DescribeStep(StepFrame f, List<Rule> rules)
        {
            string s = "Step " + (f.T + 1) + " - ";
            if (f.FiredRule >= 0)
                s += "Card " + (f.FiredRule + 1) + " (" + rules[f.FiredRule].ToDisplayString() + ") fired, qi left " + f.Qi;
            else if (f.HoldingRule >= 0)
                s += "Card " + (f.HoldingRule + 1) + " keeps waiting for the trap to go down (no extra qi)";
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
