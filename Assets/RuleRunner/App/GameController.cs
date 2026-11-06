using System.Collections;
using System.Collections.Generic;
using System.Linq;
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
        const int FailsBeforeNudge = 3;
        const string NudgeShownKey = "rr_nudge_shown";
        const string NotTriedKey = "rr_not_tried";        // the player has pressed a NOT chip at least once
        const string NotCoachKey = "rr_not_coach";        // the one-step NOT spotlight in the NOT level was shown
        const string AndCoachKey = "rr_and_coach";        // the one-step +AND spotlight in the first AND level was shown
        bool notCoachActive;   // the arrow is a one-time lesson: "hints live here"
        bool andCoachActive;

        CameraFitter cameraFitter;
        MapView map;
        RunnerView runner;
        GameScreen gameScreen;
        LoadoutPanel loadout;
        LevelSelectScreen levelSelect;
        ResultPopup resultPopup;
        IntroCard introCard;
        CoachOverlay coach;
        TapHint tapHint;
        HeadCards headCards;
        Tutorial tutorial;
        TutorialScreen tutorialScreen;
        AnalyticsTracker analytics;

        LevelDef level;
        readonly Dictionary<string, List<Rule>> rulesByLevel = new Dictionary<string, List<Rule>>();
        Coroutine playback;
        float speed = 1f;
        /// <summary>The first tutorial level's cards are playing once by themselves (not a player run).</summary>
        bool demoRun;

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

        /// <summary>A new player goes straight into the first tutorial level; anyone who has cleared one sees the level list.</summary>
        void Start()
        {
            var first = LevelDatabase.All.FirstOrDefault(l => l.Tutorial && l.Playable);
            if (first != null && !LevelDatabase.All.Any(l => l.Tutorial && IsCleared(l.Id))) LoadLevel(first);
            else ShowLevelSelect();
        }

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
            cameraFitter.viewportHeight = MapViewportHeight(false);

            // The main camera only draws the map strip; this one clears the rest of the screen every frame.
            var bg = new GameObject("Background Camera").AddComponent<Camera>();
            bg.clearFlags = CameraClearFlags.SolidColor;
            bg.backgroundColor = Theme.Background;
            bg.cullingMask = 0;
            bg.depth = cam.depth - 1;

            map = new GameObject("MapView").AddComponent<MapView>();
            runner = new GameObject("Runner").AddComponent<RunnerView>();
        }

        /// <summary>
        /// Screen fraction left for the map: AND levels have a taller card area, so the map is shorter.
        /// The tutorial has no tile legend under the map, so the map takes that strip too.
        /// </summary>
        static float MapViewportHeight(bool andLevel, bool tutorial = false) =>
            1f - GameScreen.HeaderFraction - GameScreen.BottomFraction(andLevel) - (tutorial ? 6f : 34f) / 720f;

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
            headCards = HeadCards.Create(gameScreen.transform);
            levelSelect = LevelSelectScreen.Create(canvasRect, IsCleared);
            resultPopup = ResultPopup.Create(canvasRect);
            introCard = IntroCard.Create(canvasRect);
            coach = CoachOverlay.Create(canvasRect);
            tapHint = TapHint.Create(canvasRect);
            picker.transform.SetAsLastSibling();
            tutorial = new Tutorial(tapHint, gameScreen, loadout);
            // The tutorial levels (T1-T5) play in their own screen, a port of docs/proto/tutorial.html.
            tutorialScreen = TutorialScreen.Create(canvasRect);
            tutorialScreen.Back += ShowLevelSelect;
            // After T5 the player picks from the level list; after the AND guide (2-0) the chapter goes straight on.
            tutorialScreen.Finished += id =>
            {
                var def = LevelDatabase.Get(id);
                var next = def != null && def.Chapter > 0 ? LevelDatabase.Next(def) : null;
                if (next != null) LoadLevel(next); else ShowLevelSelect();
            };
            tutorialScreen.Cleared += id => PlayerPrefs.SetInt(ClearedKey + id, 1);
            tutorialScreen.StageStarted += id => analytics.LevelStarted(id);
            tutorialScreen.RunEnded += (id, ids, won, hints) =>
            {
                var def = LevelDatabase.Get(id);
                var rules = Rule.ParseList(ids);
                if (def != null) analytics.RunFinished(def, rules, Simulator.Run(def, rules), hints);
            };

            levelSelect.LevelChosen += LoadLevel;
            gameScreen.Back += ShowLevelSelect;
            gameScreen.Run += StartRun;
            gameScreen.Reset += ResetRun;
            gameScreen.SpeedChanged += s => speed = s;
            gameScreen.IntroRequested += () => { gameScreen.HideNudge(); ShowIntro(); };
            gameScreen.HintRequested += () => { gameScreen.HideNudge(); ShowNextHint(); };
            introCard.Closed += OnIntroClosed;
            loadout.CardHover += PreviewCard;
            loadout.Changed += BindHeadCards;
            loadout.NotToggled += OnNotToggled;
            loadout.Changed += MaybeCoachNot;
            loadout.AndToggled += EndAndCoach;
            loadout.Changed += MaybeCoachAnd;
            coach.Next += EndNotCoach;
            coach.Skip += EndNotCoach;
            coach.Next += EndAndCoach;
            coach.Skip += EndAndCoach;
            resultPopup.BackToLevels += ShowLevelSelect;
            resultPopup.EditRules += ResetRun;
            resultPopup.NextLevel += () =>
            {
                // The last tutorial level leads to the level list, not straight into chapter 1.
                var next = LevelDatabase.Next(level);
                if (next != null && (next.Tutorial || !level.Tutorial)) LoadLevel(next); else ShowLevelSelect();
            };
        }

        // ---------- Screens ----------

        void ShowLevelSelect()
        {
            StopPlayback();
            tutorial.Stop();
            tutorialScreen.Close();
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
            if (def.Tutorial)
            {
                level = null;
                tutorial.Stop();
                levelSelect.Hide();
                resultPopup.Hide();
                introCard.gameObject.SetActive(false);
                gameScreen.Hide();
                map.gameObject.SetActive(false);
                runner.gameObject.SetActive(false);
                tutorialScreen.Open(def.Id);
                return;
            }
            tutorialScreen.Close();
            level = def;
            deathX = -1;
            levelSelect.Hide();
            resultPopup.Hide();
            map.gameObject.SetActive(true);
            runner.gameObject.SetActive(true);
            map.Build(level);
            cameraFitter.viewportHeight = MapViewportHeight(level.AllowAnd, level.Tutorial);
            // The tutorial's cards ride above the runner, so the ground sits lower to leave them room.
            cameraFitter.groundFromBottom = level.Tutorial ? 0.45f : 1.3f;
            cameraFitter.Fit(level.Map.Length);

            if (!rulesByLevel.TryGetValue(level.Id, out var rules))
                rulesByLevel[level.Id] = rules = level.StartCards.ConvertAll(r => r.Clone());
            gameScreen.Show();
            gameScreen.Setup(level);
            loadout.Setup(level, rules);
            loadout.SetNotPulse(level.AllowNot && !level.Tutorial && PlayerPrefs.GetInt(NotTriedKey, 0) == 0);
            headCards.Show(level.Tutorial);
            BindHeadCards();
            ResetRun();
            gameScreen.HideBanner();
            gameScreen.HideNudge();
            UpdateHintButton();
            analytics.LevelStarted(level.Id);

            // The first tutorial level opens with its cards playing the level once by themselves.
            bool demo = level.DemoFirst && rules.Count > 0 && rules.SequenceEqual(level.StartCards);
            tutorial.LevelLoaded(level, demo);
            // A tutorial level shows an intro card only when it brings something new (Big Jump, card order, Energy);
            // chapter levels that teach something (all of chapter 1, and 2-2) show it until the level is cleared.
            bool showIntro = level.IntroUntilCleared ? !IsCleared(level.Id) : PlayerPrefs.GetInt(IntroKey(level), 0) == 0;
            if (level.HasIntro && showIntro) ShowIntro();
            if (demo) StartCoroutine(StartDemo());
        }

        IEnumerator StartDemo()
        {
            yield return new WaitForSeconds(0.8f);
            if (level != null && level.DemoFirst && playback == null) StartRun(true);
        }

        void BindHeadCards()
        {
            if (level == null || !level.Tutorial) return;
            headCards.Bind(loadout.Rules, level.AllowNot || level.NotOptions.Count > 0);
        }

        /// <summary>The tutorial's cards ride above the runner's head.</summary>
        void LateUpdate()
        {
            if (level == null || !level.Tutorial || !headCards.gameObject.activeInHierarchy) return;
            headCards.Follow(cameraFitter.WorldToCanvasPoint(runner.HeadPosition, gameScreen.CanvasRect), gameScreen.CanvasRect,
                GameScreen.HeaderFraction * 720f);
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

        void ShowIntro()
        {
            if (level == null || playback != null) return;
            introCard.Show(level);
            IntroItems.Build(introCard.DiagramArea, level.Guide, !level.HideQi);
        }

        void OnIntroClosed()
        {
            PlayerPrefs.SetInt(IntroKey(level), 1);
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

        // ---------- NOT guidance (playtest feedback: "NOT is not straightforward") ----------

        void OnNotToggled()
        {
            if (PlayerPrefs.GetInt(NotTriedKey, 0) == 0)
            {
                PlayerPrefs.SetInt(NotTriedKey, 1);
                loadout.SetNotPulse(false);
            }
            EndNotCoach();
        }

        /// <summary>In the level that introduces NOT, the first equipped card gets a one-time spotlight on its NOT chip.</summary>
        void MaybeCoachNot()
        {
            if (notCoachActive || level == null || level.Tutorial || level.Guide.Concept != "not") return;
            if (loadout.Rules.Count == 0 || PlayerPrefs.GetInt(NotCoachKey, 0) == 1) return;
            var card = loadout.CardAt(0);
            if (card == null) return;
            notCoachActive = true;
            coach.Show(card.NotChip, "Tap +NOT to flip the condition. IF NOT Ground ahead fires before ANY danger - pits and spikes alike.", true);
        }

        void EndNotCoach()
        {
            if (!notCoachActive) return;
            notCoachActive = false;
            coach.Hide();
            PlayerPrefs.SetInt(NotCoachKey, 1);
        }

        // ---------- AND guidance: same one-time spotlight, on the +AND chip of the first AND level ----------

        void MaybeCoachAnd()
        {
            if (andCoachActive || notCoachActive || level == null || level.Tutorial || level.Guide.Concept != "and") return;
            if (loadout.Rules.Count == 0 || PlayerPrefs.GetInt(AndCoachKey, 0) == 1) return;
            var card = loadout.CardAt(0);
            if (card == null) return;
            andCoachActive = true;
            coach.Show(card.AndChip, "Tap +AND to add a second condition. The card fires only when BOTH are true, for 1 extra Energy.", true);
        }

        void EndAndCoach()
        {
            if (!andCoachActive) return;
            andCoachActive = false;
            coach.Hide();
            PlayerPrefs.SetInt(AndCoachKey, 1);
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

        void StartRun() => StartRun(false);

        /// <param name="demo">The first tutorial level's cards play once by themselves: not counted, not cleared.</param>
        void StartRun(bool demo)
        {
            if (level == null || playback != null) return;
            demoRun = demo;
            deathX = -1;
            ResetRun();
            gameScreen.HideBanner();
            gameScreen.SetRunning(true);
            loadout.SetEditable(false);
            var rules = new List<Rule>(loadout.Rules.ConvertAll(r => r.Clone()));
            var result = Simulator.Run(level, rules);
            hintsShown.TryGetValue(level.Id, out int hintsUsed);
            if (!demo) analytics.RunFinished(level, rules, result, hintsUsed);
            tutorial.RunStarted();
            gameScreen.Log("--- Run: " + rules.Count + (rules.Count == 1 ? " card" : " cards") + ", Energy " + level.Qi + " ---", Theme.Dim);
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
            headCards.Clear();
            map.ClearHighlight();
        }

        void StopPlayback()
        {
            if (playback != null) StopCoroutine(playback);
            playback = null;
        }

        IEnumerator Play(List<Rule> rules, RunResult result)
        {
            var hits = new int[rules.Count];
            var cells = level.Map.ToCharArray();
            bool firstFiring = true;
            foreach (var f in result.Frames)
            {
                float step = BaseStepSeconds / speed;
                map.SetTime(f.T);
                loadout.HighlightRule(f.FiredRule >= 0 ? f.FiredRule : f.HoldingRule, f.StarvedRule);

                if (level.Tutorial && (f.FiredRule >= 0 || f.StarvedRule >= 0))
                {
                    // The cards above the runner are read out loud before it acts; the first time slowly.
                    yield return ReadCards(f, rules, cells, firstFiring);
                    firstFiring = false;
                }

                if (f.FiredRule >= 0)
                {
                    hits[f.FiredRule]++;
                    loadout.SetHits(hits);
                    var rule = rules[f.FiredRule];
                    int cost = rule.Cost;
                    string label = level.Tutorial
                        ? (level.HideQi || cost == 0 ? "" : "-" + cost + " Energy")
                        : "Card " + (f.FiredRule + 1) + ": " + Vocabulary.Display(rule.Action) + (cost > 0 ? " -" + cost : "");
                    if (label.Length > 0)
                        gameScreen.ShowFloat(cameraFitter.WorldToCanvasPoint(runner.HeadPosition, gameScreen.CanvasRect), label, step * 1.2f);
                    if (level.Tutorial && !level.HideQi && cost > 0)
                    {
                        // Spending Energy is shown: the Energy pool lights up and the runner waits a moment.
                        loadout.SetQi(f.Qi, level.Qi);
                        loadout.FlashEnergy(true, 0.9f / speed);
                        yield return new WaitForSeconds(0.9f / speed);
                    }
                }
                else if (f.HoldingRule >= 0 && !level.Tutorial)
                {
                    gameScreen.ShowFloat(cameraFitter.WorldToCanvasPoint(runner.HeadPosition, gameScreen.CanvasRect),
                        "Card " + (f.HoldingRule + 1) + ": waiting...", step * 1.2f);
                }
                else if (f.StarvedRule >= 0)
                {
                    gameScreen.ShowFloat(cameraFitter.WorldToCanvasPoint(runner.HeadPosition, gameScreen.CanvasRect),
                        level.Tutorial ? "not enough Energy!" : "Card " + (f.StarvedRule + 1) + ": not enough Energy!", step * 1.2f);
                }
                loadout.SetQi(f.Qi, level.Qi);
                gameScreen.Log(DescribeStep(f, rules), f.StarvedRule >= 0 ? new Color(1f, 0.6f, 0.6f) : (Color?)null);

                yield return runner.PlayStep(f, step);
                if (f.CollectedOrb) map.CollectOrb(f.X);
                if (level.Tutorial)
                {
                    headCards.Clear();
                    map.ClearHighlight();
                }
            }

            loadout.HighlightRule(-1, -1);
            if (!demoRun) gameScreen.Log("Result: " + Vocabulary.Describe(result.Outcome) + "  (Energy left " + result.QiLeft + ")",
                result.Won ? new Color(0.5f, 0.9f, 0.55f) : new Color(1f, 0.55f, 0.55f));
            yield return runner.PlayOutcome(result.Outcome, 0.7f);

            string line = "";
            if (demoRun)
            {
                // The cards have shown how it is done; back at the start they shake, fade and are taken away,
                // and the empty slot is the player's.
                demoRun = false;
                playback = null;               // ResetRun would stop this coroutine
                ResetRun();
                gameScreen.SetRunning(true);
                loadout.SetEditable(false);
                yield return new WaitForSeconds(0.9f);
                yield return headCards.Break();
                loadout.ClearRules();
                ResetRun();
                tutorial.DemoFinished();
                yield break;
            }
            if (result.Won) PlayerPrefs.SetInt(ClearedKey + level.Id, 1);
            else if (level.Tutorial)
            {
                // No popup and no words: rewind to the step before the fall and freeze there, with the X on
                // the spot, then the finger shows what to change.
                fails.TryGetValue(level.Id, out int f);
                fails[level.Id] = f + 1;
                if (result.Outcome != Outcome.Timeout && result.Frames.Count > 0) deathX = result.Frames[result.Frames.Count - 1].X;
                playback = null;
                ResetRun();
                if (result.Frames.Count > 0)
                {
                    var last = result.Frames[result.Frames.Count - 1];
                    runner.Place(last.XBefore, last.DirBefore);
                }
                tutorial.RunFinished(result);
                UpdateHintButton();
                hintsShown.TryGetValue(level.Id, out int seen);
                if (f + 1 == FailsBeforeHint && seen == 0 && level.Guide.Hints.Count > 0) gameScreen.ShowNudge(true);
                yield break;
            }
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
                if (failed >= FailsBeforeNudge && PlayerPrefs.GetInt(NudgeShownKey, 0) == 0)
                {
                    hintsShown.TryGetValue(level.Id, out int shown);
                    gameScreen.ShowNudge(shown < level.Guide.Hints.Count);
                    PlayerPrefs.SetInt(NudgeShownKey, 1);
                }
            }
            playback = null;
            resultPopup.Show(result, level, rules.Count, LevelDatabase.Next(level) != null, line);
            tutorial.RunFinished(result);
        }

        /// <summary>
        /// Tutorial: reads the cards above the runner before it acts. The tile ahead is framed; with several cards a pointer
        /// moves from the left, each card it reaches gets a cross on its IF (no match, then grey) until the first tick.
        /// The THEN of that card lights gold, or red when there is not enough Energy. Cards after it are never read and
        /// stay grey; when one of them would also have matched, the moment is held longer (the order decided it).
        /// </summary>
        IEnumerator ReadCards(StepFrame f, List<Rule> rules, char[] cells, bool slow)
        {
            int k = f.FiredRule >= 0 ? f.FiredRule : f.StarvedRule;
            float pace = 1f / speed;
            map.HighlightCell(f.XBefore + f.DirBefore);
            if (rules.Count > 1)
            {
                headCards.GreyAll();
                for (int i = 0; i < k; i++)
                {
                    headCards.Read(i);
                    yield return new WaitForSeconds(0.32f * pace);
                    headCards.Miss(i);
                    yield return new WaitForSeconds(0.9f * pace);
                    headCards.Unread(i);
                }
                headCards.Read(k);
                yield return new WaitForSeconds(0.32f * pace);
            }
            headCards.Hit(k);
            yield return new WaitForSeconds((slow ? 0.7f : 0.26f) * pace);

            bool starved = f.StarvedRule >= 0;
            headCards.Then(k, !starved);
            if (starved)
            {
                loadout.FlashEnergy(false, 1f * pace);
                yield return new WaitForSeconds(1f * pace);
                yield break;
            }
            bool shadowed = false;
            for (int j = k + 1; j < rules.Count; j++)
                if (Simulator.Holds(cells, rules[j], f.XBefore, f.DirBefore, f.T))
                {
                    shadowed = true;
                    headCards.Unread(j, 0.15f);
                }
            yield return new WaitForSeconds((shadowed ? 1.4f : slow ? 0.45f : 0.16f) * pace);
        }

        static string DescribeStep(StepFrame f, List<Rule> rules)
        {
            string s = "Step " + (f.T + 1) + " - ";
            if (f.FiredRule >= 0)
                s += "Card " + (f.FiredRule + 1) + " (" + rules[f.FiredRule].ToDisplayString() + ") fired, Energy left " + f.Qi;
            else if (f.HoldingRule >= 0)
                s += "Card " + (f.HoldingRule + 1) + " keeps waiting for the trap to go down (no extra Energy)";
            else if (f.StarvedRule >= 0)
                s += "Card " + (f.StarvedRule + 1) + " was true but needs " + rules[f.StarvedRule].Cost +
                     " Energy (have " + f.QiBefore + ") - walked instead";
            else
                s += "no card fired, Walk";
            if (f.Blocked) s += " - blocked by a wall";
            if (f.CollectedOrb) s += " - picked up an Energy orb (+" + Vocabulary.OrbQi + ")";
            return s;
        }

        static bool IsCleared(string id) => PlayerPrefs.GetInt(ClearedKey + id, 0) == 1;
    }
}
