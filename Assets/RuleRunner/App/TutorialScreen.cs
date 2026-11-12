using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using RuleRunner.UI;
using UnityEngine;
using UnityEngine.UI;

namespace RuleRunner.App
{
    /// <summary>
    /// The tutorial (T1-T5), a direct port of docs/proto/tutorial.html: same layout, stages, card reading,
    /// empty cards, picker, ghost loop, blinking, hints, intros and Next button. The AND guide (2-0) plays here too,
    /// a port of the guide in docs/proto/chapter2.html: AND cards get a second IF row (Ground 2 ahead) and cost 1 more. Coordinates are the page's CSS
    /// pixels (top-left origin, y down); the whole page is scaled to fit the screen. Only the Energy costs differ:
    /// Big Jump costs 3 here, as in the chapters, so T5 gives 4 Energy instead of 3.
    /// It is also the title screen (docs/proto title.html): in title mode the top bar and the loadout are hidden, the stage
    /// moves down under RULE RUNNER, and T1's demo (a title-only stage) loops at chapter speed. START slides the tutorial back
    /// in and goes on with T1, which starts with the card breaking; Levels opens the level list.
    /// </summary>
    public class TutorialScreen : MonoBehaviour
    {
        // ---------- data (STAGES in the prototype) ----------

        sealed class CardData
        {
            public string If, Then;
            public bool Not;
            /// <summary>AND card: a second condition (If2: "ground2"), negated when Not2.</summary>
            public bool And, Not2;
            public string If2;
            public CardData Copy() => new CardData { If = If, Not = Not, Then = Then, And = And, If2 = If2, Not2 = Not2 };
            public bool Complete => If != null && Then != null && (!And || If2 != null);
        }

        sealed class Stage
        {
            public string Id, Map, Hint, NewAction, PulseOpt;
            public CardData[] Cards, BreakFrom;
            public string[] Ifs, If2s, Thens, Blink, Hints;
            public bool Watch, NotIntro, NotChip, LockIf, Locked, Reorder, NewOrder, NewEnergy, NoBlink, NewAnd, AndChip;
            /// <summary>Plays only as the title screen's demo; the tutorial itself skips it (no dot, never opened by T1).</summary>
            public bool TitleOnly;
            public int? Energy;
        }

        static CardData Cd(string i, string t, bool n = false) => new CardData { If = i, Then = t, Not = n };
        static CardData CdAnd(string i, string i2, bool n2, string t) => new CardData { If = i, Then = t, And = true, If2 = i2, Not2 = n2 };
        static readonly string[] BaseIfs = { "ground", "pit", "spike" };

        /// <summary>Tutorial level id per stage: stage 0 is T1's watch part, stage 1 its fix part; the last two are 2-0's.</summary>
        static readonly string[] StageLevel = { "T1", "T1", "T2", "T3", "T4", "T5", "2-0", "2-0" };
        const int TitleStage = 0;   // the T1 demo the title screen loops

        /// <summary>Stages that play as one lesson (dots, Next): the tutorial T1-T5, and the AND guide on its own.</summary>
        static string Group(int i) => StageLevel[i].StartsWith("T") ? "T" : StageLevel[i];

        static readonly Stage[] Stages =
        {
            // T1's demo now lives on the title screen, which loops it; T1 itself starts with the card breaking.
            new Stage { Id = "T1", Map = "gggpgggpggf", Cards = new[] { Cd("pit", "jump") }, Watch = true, TitleOnly = true },
            new Stage { Id = "T1", Map = "gggpggpggf", Cards = new[] { Cd(null, null) }, BreakFrom = new[] { Cd("pit", "jump") },
                Ifs = new[] { "ground", "pit" }, Thens = new[] { "jump" } },
            new Stage { Id = "T2", Map = "ggpggsggpggf", Cards = new[] { Cd("pit", "jump") }, Ifs = BaseIfs.Concat(new[] { "notground" }).ToArray(),
                Thens = new[] { "jump" }, NotIntro = true, PulseOpt = "notground", Blink = new[] { "if" },
                Hint = "Sometimes there is more than one kind of danger. Maybe we need a different way of thinking." },
            new Stage { Id = "T3", Map = "ggssgggppggf", Cards = new[] { Cd(null, null) }, Ifs = BaseIfs, Thens = new[] { "jump", "bigjump" },
                NotChip = true, NewAction = "bigjump",
                Hints = new[] { "Spikes and pits are both \"not ground\".", "Both dangers are two tiles wide.", "Try: IF NOT Ground ahead, THEN Big Jump." } },
            new Stage { Id = "T4", Map = "ggsgsggppggf", Cards = new[] { Cd("ground", "jump", true), Cd("pit", "jump") }, LockIf = true, Reorder = true,
                NotChip = true, NewOrder = true, Thens = new[] { "jump", "bigjump" }, Hint = "Order matters.",
                Hints = new[] { "At the pit, watch the pointer: which card does it stop at?", "Move the Pit card to the front, so it is read first.",
                    "A normal Jump is too short for a two-tile pit: use Big Jump." } },
            new Stage { Id = "T5", Map = "ggsgggppggf", Energy = 4, NewEnergy = true, NotChip = true, Reorder = true, NoBlink = true, Ifs = BaseIfs,
                Thens = new[] { "jump", "bigjump" }, Cards = new[] { Cd("ground", "bigjump", true), Cd(null, null) },
                Hint = "One card for every danger costs too much Energy. Does every danger need a Big Jump?",
                Hints = new[] { "Watch the Energy bar: how much is left when you reach the pit?", "The spike only needs a normal Jump (1 Energy).",
                    "Use the second card: Spike -> Jump, and move it to the front." } },
            // 2-0, the AND guide: watch an AND card at work, then the AND is taken off and the player puts it back.
            new Stage { Id = "2-0", Map = "gggpgggppgggpgggf", Energy = 6, Watch = true, NewAnd = true, AndChip = true,
                Cards = new[] { CdAnd("pit", "ground2", true, "bigjump"), Cd("pit", "jump") },
                Hint = "Card 1 checks two things. It acts only when BOTH are true." },
            new Stage { Id = "2-0", Map = "gggpgggppgggpgggf", Energy = 6, AndChip = true,
                BreakFrom = new[] { CdAnd("pit", "ground2", true, "bigjump"), Cd("pit", "jump") },
                Cards = new[] { Cd("pit", "bigjump"), Cd("pit", "jump") },
                Ifs = BaseIfs, If2s = new[] { "ground2", "notground2" }, Thens = new[] { "jump", "bigjump" }, PulseOpt = "notground2",
                Blink = new[] { "and", "if2" }, Hint = "A Big Jump costs 3. Is every pit that wide?",
                Hints = new[] { "Watch the Energy: it is gone before the last pit.", "Tap + AND on card 1, then pick NOT Ground 2 ahead.",
                    "IF Pit ahead AND NOT Ground 2 ahead THEN Big Jump, then IF Pit ahead THEN Jump." } },
        };

        static readonly Dictionary<string, string> Name = new Dictionary<string, string>
        {
            { "ground", "Ground ahead" }, { "pit", "Pit ahead" }, { "spike", "Spike ahead" }, { "notground", "NOT Ground ahead" },
            { "ground2", "Ground 2 ahead" }, { "notground2", "NOT Ground 2 ahead" },
            { "jump", "Jump" }, { "bigjump", "Big Jump" },
        };
        static readonly Dictionary<string, int> Dist = new Dictionary<string, int> { { "walk", 1 }, { "jump", 2 }, { "bigjump", 3 } };
        static readonly Dictionary<string, int> Cost = new Dictionary<string, int> { { "jump", 1 }, { "bigjump", 3 } };
        static string CondName(string cond, bool neg) => cond == "notground" ? Name["notground"] : (neg ? "NOT " : "") + Name[cond];
        /// <summary>Energy a card spends when it acts: its action, plus 1 for an AND card.</summary>
        static int CostOf(CardData c) => Cost[c.Then] + (c.And ? 1 : 0);
        /// <summary>The picker key of a card's second condition ("ground2" or "notground2").</summary>
        static string If2Key(CardData c) => c.If2 == null ? null : c.Not2 ? "not" + c.If2 : c.If2;

        // ---------- colors (C and the CSS variables) ----------

        static Color H(int rgb, float a = 1f) => Theme.Hex(rgb, a);
        static readonly Color CGround = H(0x8a93a3), CGroundDark = H(0x5c6473), CPit = H(0x0d1016), CSpike = H(0xe04f4f), CWall = H(0x3d4555),
            CFinish = H(0x5cc96b), CRunner = H(0xf5d03b), CEye = H(0x1d232e), CGold = H(0xf5c542), CNot = H(0xff6b4a), CNotName = H(0xffb3a3), CLine = H(0x3a4459),
            CJump = H(0x4c8dff), CBigJump = H(0x9b6bff), CBg = H(0x141922), CSurface = H(0x1e2531), CSurface2 = H(0x283041), CRaised = H(0x343e53),
            CText = H(0xeef1f5), CDim = H(0x8d97aa), CFaint = H(0x5d677a), CAccent = H(0x43b86a), CQi = H(0x4fd6e8), CStage = H(0x1d232e),
            CRed = H(0xe04f4f);
        static Color ActColor(string a) => a == "jump" ? CJump : a == "bigjump" ? CBigJump : CLine;

        const float T = 52f, W = 760f, Hh = 440f, GY = 370f;
        const float AppW = 820f, StageW = 788f;
        static float S => StageW / W;

        // ---------- state (S and R in the prototype) ----------

        public event Action Back;
        /// <summary>The Next button after a lesson's last stage (the level id of that stage: "T5" or "2-0").</summary>
        public event Action<string> Finished;
        /// <summary>The player cleared a tutorial level (its id, e.g. "T2").</summary>
        public event Action<string> Cleared;
        /// <summary>A stage started (level id).</summary>
        public event Action<string> StageStarted;
        /// <summary>A player run ended: level id, the complete cards as rule ids, won.</summary>
        public event Action<string, List<string>, bool, int> RunEnded;
        /// <summary>The title screen's Levels button.</summary>
        public event Action LevelsRequested;

        int stage;
        string map = "";
        List<CardData> cards = new List<CardData>();
        bool ghost, slow, won;
        int? energy;
        int hintsShown;
        (int tile, bool lit)? beam;
        int? hl, hl2, mark;
        readonly HashSet<string> pulse = new HashSet<string>(), done = new HashSet<string>();
        float rx, ry, rfall, rrot, ralpha = 1f;
        int token;
        Stage St => Stages[stage];

        // ---------- UI ----------

        CanvasGroup bubbleGroup;
        RectTransform app, stageRt, canvasRt, panel, loadoutRow, bubble, reader, mapLayer, fx;
        Image runnerImg, runnerEye;
        RectTransform hlImg, hlGlow, hl2Img;
        RectTransform runnerRt;
        readonly List<Image> beamDashes = new List<Image>();
        Image markA, markB;
        Text lvlText, hintText, hintBtnText, bannerText, nudgeText, energyLabel;
        RectTransform hintBox, hintBtn, banner, nudge, energyBox, energyPips, dotsRt, nextBtn;
        Image nextIcon;
        Text energySpent;
        readonly List<CardView> headViews = new List<CardView>(), bigViews = new List<CardView>();
        RectTransform pickerRoot, pickerGrid, introRoot, notIntroRoot;
        Text pickTitle, introTitle, introText;
        RectTransform introLeft, introRight;
        Text introEq;
        RectTransform introArrow;
        RectTransform niOpt, niCardHolder, finger;
        bool panelOff;
        Coroutine niCycle;

        // Title mode: positions of the top bar, stage and loadout in the tutorial layout, and how far they move.
        const float TopY = 12f, StageY = 63f, TitleDrop = 116f;
        static float PanelY => StageY + Hh * S + 12f;
        RectTransform topBar, titleRoot;
        CanvasGroup appGroup, topGroup, panelGroup, titleGroup;
        bool title;
        // The title layout ends under the Levels button: stage moved down, then START (50) and Levels (42).
        static float TitleBottom => StageY + TitleDrop + Hh * S + 26f + 50f + 14f + 42f;

        /// <summary>
        /// The page height a stage needs: top bar, stage, and the loadout panel (150 minimum; a watch stage has no cards there).
        /// A stage whose cards can become AND cards counts them as AND cards, so toggling AND never grows the page.
        /// </summary>
        static float StagePageHeight(Stage s)
        {
            bool and = s.AndChip || s.Cards.Any(c => c.And);
            float panelH = s.Watch ? 150f : Mathf.Max(150f, 12 + 21 + CardHeight(true, and, s.Reorder) + 12);
            return StageY + Hh * S + 12 + panelH + 24;
        }

        /// <summary>
        /// One page height for a whole group (T1 to T5 and the title screen; the 2-0 guide): the tallest stage in it.
        /// The page is scaled to the screen by this height, so it no longer zooms in and out from one stage to the next.
        /// </summary>
        static float GroupPageHeight(int i)
        {
            float h = 0f;
            for (int j = 0; j < Stages.Length; j++)
                if (Group(j) == Group(i)) h = Mathf.Max(h, StagePageHeight(Stages[j]));
            if (Group(i) == "T") h = Mathf.Max(h, TitleBottom + 24f);
            return h;
        }

        float pageHeight;
        Action introOk;

        sealed class Particle { public RectTransform Rt; public Image Img; public float X, Y, Vx, Vy, Life; }
        sealed class Float { public Text Txt; public float X, Y, Life; }
        readonly List<Particle> parts = new List<Particle>();
        readonly List<Float> floats = new List<Float>();

        // ---------- building ----------

        public static TutorialScreen Create(RectTransform canvas)
        {
            var root = UIKit.Fill(canvas, "Tutorial");
            root.gameObject.AddComponent<Image>().color = CBg;
            var t = root.gameObject.AddComponent<TutorialScreen>();
            t.Build(root);
            root.gameObject.SetActive(false);
            return t;
        }

        static RectTransform N(Transform p, string name, float x, float y, float w, float h)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(p, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(w, h);
            return rt;
        }

        static void Place(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(w, h);
        }

        static Image Img(RectTransform rt, Color c, float radius = 0f, Sprite sprite = null)
        {
            var img = rt.gameObject.AddComponent<Image>();
            img.color = c;
            img.raycastTarget = false;
            if (sprite != null) img.sprite = sprite;
            else if (radius > 0f) UIKit.Round(img, radius);
            return img;
        }

        static Image Box(Transform p, string name, float x, float y, float w, float h, Color c, float radius = 0f) =>
            Img(N(p, name, x, y, w, h), c, radius);

        /// <summary>A rounded border of the given thickness (the outline sprite's ring is 2.5 units at multiplier 1).</summary>
        static Image Ring(Transform p, string name, float x, float y, float w, float h, Color c, float thick)
        {
            var img = Img(N(p, name, x, y, w, h), c);
            img.sprite = UISprites.Outline;
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = 2.5f / Mathf.Max(0.5f, thick);
            return img;
        }

        /// <summary>A square-cornered frame of four bars (canvas strokeRect).</summary>
        static RectTransform Frame(Transform p, string name, float thick, Color c)
        {
            var rt = N(p, name, 0, 0, 10, 10);
            (Vector2 min, Vector2 max, Vector2 omin, Vector2 omax)[] sides =
            {
                (new Vector2(0, 1), new Vector2(1, 1), new Vector2(-thick / 2, -thick / 2), new Vector2(thick / 2, thick / 2)),
                (new Vector2(0, 0), new Vector2(1, 0), new Vector2(-thick / 2, -thick / 2), new Vector2(thick / 2, thick / 2)),
                (new Vector2(0, 0), new Vector2(0, 1), new Vector2(-thick / 2, thick / 2), new Vector2(thick / 2, -thick / 2)),
                (new Vector2(1, 0), new Vector2(1, 1), new Vector2(-thick / 2, thick / 2), new Vector2(thick / 2, -thick / 2)),
            };
            foreach (var (min, max, omin, omax) in sides)
                Img(UIKit.Rect(rt, "Side", min, max, omin, omax), c);
            return rt;
        }

        /// <summary>A border that fills its parent.</summary>
        static Image FillRing(Transform p, Color c, float thick)
        {
            var img = Ring(p, "Border", 0, 0, 0, 0, c, thick);
            var rt = img.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            return img;
        }

        static Text Txt(Transform p, string name, float x, float y, float w, float h, string s, int size, Color c,
            TextAnchor a = TextAnchor.MiddleLeft, bool bold = false)
        {
            var rt = N(p, name, x, y, w, h);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = UIKit.Font;
            t.text = s;
            t.fontSize = size;
            t.color = c;
            t.alignment = a;
            t.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            t.supportRichText = true;
            return t;
        }

        static Button Btn(RectTransform rt, Action onClick)
        {
            var g = rt.GetComponent<Graphic>();
            if (g == null) { g = rt.gameObject.AddComponent<Image>(); g.color = new Color(1, 1, 1, 0f); }
            g.raycastTarget = true;
            var b = rt.gameObject.AddComponent<Button>();
            b.targetGraphic = g;
            var colors = b.colors;
            colors.highlightedColor = new Color(1.12f, 1.12f, 1.12f, 1f);
            colors.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
            colors.selectedColor = Color.white;
            b.colors = colors;
            b.onClick.AddListener(() => onClick());
            return b;
        }

        /// <summary>A dashed rounded-ish rectangle border made of short bars (CSS "2px dashed").</summary>
        static RectTransform Dashed(Transform p, string name, float x, float y, float w, float h, Color c, float thick = 2f)
        {
            var rt = N(p, name, x, y, w, h);
            const float dash = 5f, gap = 4f;
            for (float d = 6f; d < w - 6f; d += dash + gap)
            {
                float len = Mathf.Min(dash, w - 6f - d);
                Box(rt, "D", d, 0, len, thick, c);
                Box(rt, "D", d, h - thick, len, thick, c);
            }
            for (float d = 6f; d < h - 6f; d += dash + gap)
            {
                float len = Mathf.Min(dash, h - 6f - d);
                Box(rt, "D", 0, d, thick, len, c);
                Box(rt, "D", w - thick, d, thick, len, c);
            }
            return rt;
        }

        void Build(RectTransform root)
        {
            // #app: 820 wide, centered, scaled to fit the screen.
            app = N(root, "App", 0, 0, AppW, 860);
            app.anchorMin = app.anchorMax = new Vector2(0.5f, 1f);
            app.pivot = new Vector2(0.5f, 1f);
            appGroup = app.gameObject.AddComponent<CanvasGroup>();

            // #top
            var top = topBar = N(app, "Top", 16, TopY, AppW - 32, 51);
            topGroup = top.gameObject.AddComponent<CanvasGroup>();
            Img(top, CSurface, 12f);
            Box(top, "Edge", 0, 50, AppW - 32, 1, CLine);
            var levels = N(top, "Levels", 12, 10, 74, 31);
            Img(levels, CRaised, 8f);
            Txt(levels, "T", 0, 0, 74, 31, "< Levels", 13, CDim, TextAnchor.MiddleCenter, true);
            Btn(levels, () => { StopAll(); Back?.Invoke(); });
            var chip = N(top, "Chip", 96, 12, 38, 27);
            Img(chip, CGold, 8f);
            lvlText = Txt(chip, "T", 0, 0, 38, 27, "T1", 14, CBg, TextAnchor.MiddleCenter, true);
            hintBox = N(top, "Hint", 150, 8, 0, 35);
            Img(hintBox, CSurface2, 10f);
            FillRing(hintBox, CGold, 1f);
            hintText = Txt(hintBox, "T", 12, 0, 0, 35, "", 13, CText, TextAnchor.MiddleCenter);
            hintBtn = N(top, "HintBtn", 0, 10, 82, 31);
            Img(hintBtn, CGold, 8f);
            hintBtnText = Txt(hintBtn, "T", 0, 0, 82, 31, "Hint 1/3", 13, CBg, TextAnchor.MiddleCenter, true);
            Btn(hintBtn, OnHint);
            dotsRt = N(top, "Dots", AppW - 32 - 12 - 6 * 11 - 5 * 7, 20, 6 * 11 + 5 * 7, 11);

            // #stage
            stageRt = N(app, "Stage", 16, StageY, StageW, Hh * S);
            Img(stageRt, CSurface);
            stageRt.gameObject.AddComponent<RectMask2D>();
            canvasRt = N(stageRt, "Canvas", 0, 0, W, Hh);
            canvasRt.localScale = Vector3.one * S;
            Img(canvasRt, CStage);
            mapLayer = N(canvasRt, "Map", 0, 0, W, Hh);
            hlGlow = Frame(canvasRt, "HlGlow", 10f, CGold.WithAlpha(0.22f));
            hlImg = Frame(canvasRt, "Hl", 4f, CGold);
            hl2Img = Frame(canvasRt, "Hl2", 4f, CQi);
            for (int i = 0; i < 60; i++)
            {
                var d = Box(canvasRt, "Beam", 0, 0, 5, 2, Color.white);
                d.rectTransform.pivot = new Vector2(0f, 0.5f);
                beamDashes.Add(d);
            }
            markA = Box(canvasRt, "MarkA", 0, 0, 6, 40, CSpike, 3f);
            markB = Box(canvasRt, "MarkB", 0, 0, 6, 40, CSpike, 3f);
            markA.rectTransform.pivot = markB.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            runnerRt = N(canvasRt, "Runner", 0, 0, 34, 34);
            runnerRt.pivot = new Vector2(0.5f, 0f);
            runnerImg = Img(runnerRt, CRunner);
            runnerEye = Box(runnerRt, "Eye", 22, 6, 6, 8, CEye);
            fx = N(canvasRt, "Fx", 0, 0, W, Hh);

            bubble = N(stageRt, "Bubble", 0, 0, 10, 10);
            bubble.pivot = new Vector2(0.5f, 0f);
            bubbleGroup = bubble.gameObject.AddComponent<CanvasGroup>();
            bubbleGroup.blocksRaycasts = false;
            reader = N(bubble, "Reader", 0, 0, 18, 9);
            reader.pivot = new Vector2(0.5f, 1f);
            Img(reader, Color.white, 0, UISprites.Triangle);

            banner = N(stageRt, "Banner", 0, 12, 300, 36);
            banner.anchorMin = banner.anchorMax = new Vector2(0.5f, 1f);
            banner.pivot = new Vector2(0.5f, 1f);
            Img(banner, CBg.WithAlpha(0.9f), 10f);
            FillRing(banner, CGold, 1f);
            bannerText = Txt(banner, "T", 14, 0, 0, 36, "", 14, CGold, TextAnchor.MiddleCenter);
            nudge = N(stageRt, "Nudge", StageW - 84 - 160, 6, 160, 18);
            nudgeText = Txt(nudge, "T", 0, 0, 146, 18, "Stuck? Try a hint", 13, CGold, TextAnchor.MiddleRight, true);
            Arrow(nudge, "Up", 148, 2, 13, CGold, 90);

            energyBox = N(stageRt, "Energy", 0, 12, 10, 28);
            energyBox.anchorMin = energyBox.anchorMax = new Vector2(1f, 1f);
            energyBox.pivot = new Vector2(1f, 1f);
            Img(energyBox, CSurface2, 10f);
            energyLabel = Txt(energyBox, "L", 10, 0, 60, 28, "ENERGY", 12, CQi, TextAnchor.MiddleLeft, true);
            energyPips = N(energyBox, "Pips", 72, 7, 10, 14);
            energySpent = Txt(energyBox, "Spent", 0, 0, 60, 20, "", 16, CQi, TextAnchor.MiddleRight, true);
            var est = energySpent.rectTransform;
            est.anchorMin = est.anchorMax = est.pivot = new Vector2(1f, 1f);
            est.anchoredPosition = new Vector2(-4, -32);

            // #panel and #next
            panel = N(app, "Panel", 16, PanelY, AppW - 32, 270);
            panelGroup = panel.gameObject.AddComponent<CanvasGroup>();
            Img(panel, CSurface, 12f);
            Txt(panel, "Title", 14, 12, 200, 15, "LOADOUT", 13, CGold, TextAnchor.MiddleLeft, true);
            loadoutRow = N(panel, "Loadout", 14, 33, AppW - 60, 240);
            nextBtn = N(panel, "Next", AppW - 32 - 18 - 58, 0, 58, 58);
            Img(nextBtn, CAccent, 0, UISprites.Circle);
            nextIcon = Img(N(nextBtn, "Icon", 15, 15, 22, 17), CBg, 0, UISprites.Triangle);
            nextIcon.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            nextIcon.rectTransform.anchoredPosition = new Vector2(31, -29);
            nextIcon.rectTransform.localRotation = Quaternion.Euler(0, 0, -90);
            Btn(nextBtn, () => PlayStage(stage + 1 < Stages.Length && Group(stage + 1) == Group(stage) ? stage + 1 : stage, true));

            BuildTitle();
            TitleLayout(0f);

            BuildPicker(root);
            BuildIntro(root);
            BuildNotIntro(root);
        }

        // ---------- title screen ----------

        void BuildTitle()
        {
            titleRoot = N(app, "Title", 0, 0, AppW, 860);
            titleGroup = titleRoot.gameObject.AddComponent<CanvasGroup>();

            // RULE RUNNER with the little runner jumping over a spike, as on the level list.
            var heading = N(titleRoot, "Heading", 0, 60, AppW, 92);
            var headingText = UIKit.Label(heading, "Text", "RULE RUNNER", 60, TextAnchor.MiddleCenter, CGold, FontStyle.Bold);
            UIKit.DropShadow(headingText, 4f, 0.6f);
            var deco = UIKit.Centered(heading, "Deco", new Vector2(-276, 4), new Vector2(90, 70));
            UIKit.Shape(deco, "Ground", UISprites.Rounded, CGround, new Vector2(0, -26), new Vector2(84, 8));
            UIKit.Shape(deco, "Spike", UISprites.Triangle, CSpike, new Vector2(4, -14), new Vector2(18, 16));
            UIKit.Shape(deco, "Runner", UISprites.Rounded, CRunner, new Vector2(-6, 18), new Vector2(22, 22));
            for (int i = 0; i < 4; i++)
                UIKit.Shape(deco, "Dot", UISprites.Circle, new Color(1, 1, 1, 0.5f),
                    new Vector2(-38 + i * 9, -16 + 28 * Mathf.Sin(i / 4f * Mathf.PI * 0.8f)), new Vector2(4, 4));

            // START is the big one; Levels sits under it, smaller.
            float below = StageY + TitleDrop + Hh * S;
            var start = N(titleRoot, "Start", (AppW - 190) / 2, below + 26, 190, 50);   // TitleBottom follows these sizes
            var startBg = Img(start, CAccent, 13f);
            var startShadow = startBg.gameObject.AddComponent<Shadow>();
            startShadow.effectColor = new Color(0, 0, 0, 0.45f);
            startShadow.effectDistance = new Vector2(0, -5);
            var play = Img(N(start, "Icon", 0, 0, 14, 16), CBg, 0, UISprites.Triangle);
            play.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            play.rectTransform.anchoredPosition = new Vector2(40, -25);
            play.rectTransform.localRotation = Quaternion.Euler(0, 0, -90);
            Txt(start, "T", 16, 0, 190 - 16, 50, "START", 19, CBg, TextAnchor.MiddleCenter, true);
            Btn(start, StartFromTitle);

            var levels = N(titleRoot, "Levels", (AppW - 160) / 2, below + 26 + 50 + 14, 160, 42);
            Img(levels, CSurface2, 11f);
            FillRing(levels, CLine, 1f);
            Txt(levels, "T", 0, 0, 160, 42, "Levels", 16, CText, TextAnchor.MiddleCenter);
            Btn(levels, () => { StopAll(); title = false; LevelsRequested?.Invoke(); });
        }

        /// <summary>t = 1: title screen (top bar and loadout hidden, stage lower, title shown); t = 0: the tutorial.</summary>
        void TitleLayout(float t)
        {
            topBar.anchoredPosition = new Vector2(16, -(TopY - 36f * t));
            stageRt.anchoredPosition = new Vector2(16, -(StageY + TitleDrop * t));
            panel.anchoredPosition = new Vector2(16, -(PanelY + 60f * t));
            topGroup.alpha = panelGroup.alpha = 1f - t;
            topGroup.interactable = topGroup.blocksRaycasts = panelGroup.interactable = panelGroup.blocksRaycasts = t < 0.5f;
            titleGroup.alpha = t;
            titleGroup.interactable = titleGroup.blocksRaycasts = t > 0.5f;
            titleRoot.gameObject.SetActive(t > 0.001f);
        }

        /// <summary>Opens the title screen: T1's demo loops under RULE RUNNER until START or Levels.</summary>
        public void ShowTitle()
        {
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            HideUntilFitted();
            StopAll();
            StartCoroutine(TitleRoutine());
            FitPage();
        }

        IEnumerator TitleRoutine()
        {
            title = true;
            TitleLayout(1f);
            LoadStage(TitleStage);
            RenderCards();
            slow = false;   // the title demo runs at chapter speed; T1 itself keeps the slow watch speed
            while (true)
            {
                yield return RunOnce();
                if (runResult == "win")
                {
                    foreach (var v in headViews) CardState(v, "win");
                    yield return Wait(900);
                    foreach (var v in headViews) CardState(v, "");
                }
                else yield return Wait(700);
            }
        }

        void StartFromTitle()
        {
            if (!title) return;
            StopRun();
            title = false;
            ResetRunner();
            ClearLit();
            StartCoroutine(LeaveTitle());
        }

        IEnumerator LeaveTitle()
        {
            yield return Tween(600, k => TitleLayout(Mathf.Pow(1f - k, 3f)));
            TitleLayout(0f);
            // The demo just played on the title, so T1 goes straight to the card breaking.
            PlayStage(FirstStageOf("T1"), false);
        }

        // ---------- card diagrams (condIcon / actIcon), drawn in an 89 x 44 box like the SVGs ----------

        static float X(int i) => i * 23 + 10;

        static void DiaCell(RectTransform d, int i, char kind, bool dim)
        {
            float a = kind == '?' ? 0.18f : dim ? 0.35f : 1f;
            Box(d, "Cell", X(i) - 10, 28, 20, 8, (kind == 'p' ? CPit : CGround).WithAlpha(a), 2f);
            if (kind == 's')
                for (int k = 0; k < 2; k++)
                {
                    float cx = X(i) - 4.5f + 9 * k;
                    Img(N(d, "Spike", cx - 4.5f, 18, 9, 10), CSpike.WithAlpha(a), 0, UISprites.Triangle);
                }
            if (kind == 'p') Box(d, "Rim", X(i) - 10, 22, 20, 2, CLine.WithAlpha(a), 1f);
        }

        static void DiaRunner(RectTransform d)
        {
            Box(d, "Runner", X(0) - 6.5f, 14.5f, 13, 13, CRunner, 2f);
            Box(d, "Eye", X(0) + 1.5f, 17, 3, 4, CEye, 1f);
        }

        /// <summary>Builds a diagram box of the given size at (x, y); kind "if" or "then".</summary>
        static RectTransform Diagram(Transform p, float x, float y, float w, float h, string kind, string key, bool neg)
        {
            var holder = N(p, "Dia", x, y, w, h);
            if (key == null) return holder;
            var d = N(holder, "Svg", 0, 0, 89, 44);
            d.gameObject.AddComponent<RectMask2D>();   // an inline SVG clips what is outside its viewBox
            float scale = Mathf.Min(w / 89f, h / 44f);
            d.localScale = Vector3.one * scale;
            d.anchoredPosition = new Vector2((w - 89 * scale) / 2f, -(h - 44 * scale) / 2f);
            if (kind == "if")
            {
                string cond = key;
                if (cond == "notground") { cond = "ground"; neg = true; }
                if (cond == "notground2") { cond = "ground2"; neg = true; }
                // Ground 2 ahead looks at the second tile; everything else at the first.
                int ti = cond == "ground2" ? 2 : 1;
                char k = cond == "ground" || cond == "ground2" ? 'g' : cond == "pit" ? 'p' : 's';
                var col = neg ? CNot : CGold;
                DiaCell(d, 0, 'g', false);
                DiaCell(d, 1, ti == 1 ? k : '?', ti != 1);
                DiaCell(d, 2, ti == 2 ? k : '?', ti != 2);
                DiaCell(d, 3, '?', true);
                DiaRunner(d);
                Box(d, "Under", X(ti) - 10, 39, 20, 4, col, 2f);
                var tri = Img(N(d, "Marker", X(ti) - 4, 2.5f, 8, 7), col, 0, UISprites.Triangle);
                tri.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                tri.rectTransform.anchoredPosition = new Vector2(X(ti), -6f);
                tri.rectTransform.localRotation = Quaternion.Euler(0, 0, 180);
                if (neg)
                {
                    var slash = Box(d, "Not", 0, 0, 3.5f, 26, CNot, 1.5f).rectTransform;
                    slash.pivot = new Vector2(0.5f, 0.5f);
                    slash.anchoredPosition = new Vector2(X(ti), -22f);
                    slash.localRotation = Quaternion.Euler(0, 0, -40);
                }
            }
            else
            {
                DiaCell(d, 0, 'g', false); DiaCell(d, 1, 'g', true); DiaCell(d, 2, 'g', true); DiaCell(d, 3, 'g', true);
                DiaRunner(d);
                int to = key == "jump" ? 2 : 3;
                float hgt = key == "jump" ? 21 : 27;
                int n = key == "jump" ? 6 : 8;
                for (int k = 0; k < n; k++)
                {
                    float t = k / (float)(n - 1);
                    float cx = X(0) + 6 + (X(to) - X(0) - 6) * t, cy = 19 - hgt * 4 * t * (1 - t);
                    Img(N(d, "Dot", cx - 2.25f, cy - 2.25f, 4.5f, 4.5f), new Color(1, 1, 1, 0.996f), 0, UISprites.Circle);
                }
                Box(d, "Land", X(to) - 10, 39, 20, 4, CGold, 2f);
            }
            return holder;
        }

        // ---------- one card (cardHTML) ----------

        sealed class CardView
        {
            public RectTransform Root, IfSec, If2Sec, ThenSec, IfDiaArea, If2DiaArea, ThenDiaArea, NotChip, AndChip, Move, BandCost;
            public CanvasGroup Group;
            public Image Border, Glow, IfBg, If2Bg, ThenBg, IfFrame, If2Frame, ThenFrame, Badge;
            public RectTransform Tick, Cross;
            public RectTransform NeverBorder;
            public float Height;
        }

        const float AndRowHeight = 16f;
        static float SectionHeight(bool big) => 2 + 16 + (big ? 46 : 40) + (big ? 16 : 14) + 4;
        static float CardHeight(bool big, bool and, bool foot)
        {
            float secH = SectionHeight(big);
            return 7 + 22 + 4 + secH + (and ? AndRowHeight + secH : 0) + 3 + 1 + 4 + secH + (foot ? 5 + 23 : 0) + 7;
        }

        /// <summary>Builds a card. big = the loadout card (156 wide), otherwise the one over the runner (120 wide).</summary>
        CardView BuildCard(Transform parent, CardData card, int n, bool big, bool badge, bool notChip, bool cost, bool foot, int count,
            bool andChip = false)
        {
            var v = new CardView();
            float w = big ? 156 : 120, cw = w - 14, dia = big ? 46 : 40, nameH = big ? 16 : 14;
            float secH = SectionHeight(big);
            const float andRow = AndRowHeight;   // the "AND" line between the two IF rows
            float h = CardHeight(big, card.And, foot);
            v.Height = h;
            v.Root = N(parent, "Card" + n, 0, 0, w, h);
            v.Group = v.Root.gameObject.AddComponent<CanvasGroup>();
            v.Glow = Img(N(v.Root, "Glow", -6, -6, w + 12, h + 12), new Color(1, 1, 1, 0f), 16f);
            var body = Img(N(v.Root, "Body", 0, 0, w, h), CSurface2, 12f);
            var shadow = body.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0, 0, 0, 0.53f);
            shadow.effectDistance = new Vector2(0, -5);
            v.Border = Ring(v.Root, "Border", 0, 0, w, h, new Color(1, 1, 1, 0f), 2f);
            v.NeverBorder = Dashed(v.Root, "Never", 0, 0, w, h, CFaint);
            v.NeverBorder.gameObject.SetActive(false);

            // band
            var band = Box(v.Root, "Band", 7, 7, cw, 22, card.Then != null ? ActColor(card.Then) : CLine, 7f);
            var num = Img(N(band.rectTransform, "N", 4, 2.5f, 17, 17), new Color(0, 0, 0, 0.35f), 0, UISprites.Circle);
            Txt(num.rectTransform, "T", 0, 0, 17, 17, n.ToString(), 11, CText, TextAnchor.MiddleCenter, true);
            v.BandCost = N(band.rectTransform, "Cost", 0, 6.5f, cw - 4, 9);
            if (cost && card.Then != null) Pips(v.BandCost, CostOf(card), 9, true);

            float y = 7 + 22 + 4;
            v.IfSec = Section(v, card, "if", y, cw, dia, nameH, secH, big, notChip, badge);
            y += secH;
            if (card.And)
            {
                Box(v.Root, "AndL", 7 + 10, y + andRow / 2, cw / 2 - 30, 1, CQi.WithAlpha(0.6f));
                Box(v.Root, "AndR", 7 + cw / 2 + 20, y + andRow / 2, cw / 2 - 30, 1, CQi.WithAlpha(0.6f));
                Txt(v.Root, "And", 7, y, cw, andRow, "AND", 11, CQi, TextAnchor.MiddleCenter, true);
                y += andRow;
                v.If2Sec = Section(v, card, "if2", y, cw, dia, nameH, secH, big, false, false);
                y += secH;
            }
            // "+AND" on a loadout card turns it into an AND card (and back).
            if (andChip && big)
            {
                var chip = N(v.IfSec, "AndChip", cw - 4 - 44 - (notChip ? 44 : 0), 3, 44, 14);
                Img(chip, card.And ? CQi : CRaised, 7f);
                Txt(chip, "T", 0, 0, 44, 14, card.And ? "AND x" : "+AND", 10, card.And ? CBg : CDim, TextAnchor.MiddleCenter, true);
                v.AndChip = chip;
            }
            Box(v.Root, "Div", 7 + 10, y + 3, cw - 20, 1, CLine);
            y += 4 + 4;
            v.ThenSec = Section(v, card, "then", y, cw, dia, nameH, secH, big, false, false);
            y += secH;

            if (foot)
            {
                y += 5;
                var left = N(v.Root, "Left", 7, y, 30, 23);
                Img(left, CRaised, 8f);
                Txt(left, "T", 0, 0, 30, 23, "<", 13, CText, TextAnchor.MiddleCenter, true);
                left.gameObject.SetActive(n != 1);
                var right = N(v.Root, "Right", 7 + cw - 30, y, 30, 23);
                Img(right, CRaised, 8f);
                Txt(right, "T", 0, 0, 30, 23, ">", 13, CText, TextAnchor.MiddleCenter, true);
                right.gameObject.SetActive(n != count);
                int i = n - 1;
                Btn(left, () => MoveCard(i, -1));
                Btn(right, () => MoveCard(i, +1));
                v.Move = right;
            }
            return v;
        }

        RectTransform Section(CardView v, CardData card, string kind, float y, float cw, float dia, float nameH, float secH,
            bool big, bool notChip, bool badge)
        {
            bool isIf2 = kind == "if2", isIf = kind == "if" || isIf2;
            string key = isIf2 ? If2Key(card) : isIf ? card.If : card.Then;
            var sec = N(v.Root, isIf2 ? "If2" : isIf ? "If" : "Then", 7, y, cw, secH);
            var bg = Img(sec, new Color(1, 1, 1, 0f), 8f);
            var frame = Ring(sec, "Frame", 0, 0, cw, secH, new Color(1, 1, 1, 0f), 2f);
            if (isIf2) { v.If2Bg = bg; v.If2Frame = frame; }
            else if (isIf) { v.IfBg = bg; v.IfFrame = frame; } else { v.ThenBg = bg; v.ThenFrame = frame; }
            Txt(sec, "Lab", 4, 2, 40, 16, isIf2 ? "AND" : isIf ? "IF" : "THEN", 11, isIf2 ? CQi : CDim, TextAnchor.MiddleLeft, true);
            if (isIf && notChip)
            {
                // A switch, not a label: off is an outlined pill with NOT struck through, on is a filled red pill.
                // The outline and the fill are the same rounded shape (UIKit.RoundOutline).
                const float chipW = 40, chipH = 16, radius = chipH / 2;
                var chip = N(sec, "NotChip", cw - 4 - chipW, 2, chipW, chipH);
                Img(chip, card.Not ? CNot : new Color(1, 1, 1, 0f), radius);
                if (!card.Not) UIKit.RoundOutline(Img(N(chip, "Ring", 0, 0, chipW, chipH), CDim), radius, 1.5f);
                Txt(chip, "T", 0, 0, chipW, chipH, "NOT", 10, card.Not ? Color.white : CDim, TextAnchor.MiddleCenter, true);
                if (!card.Not)
                {
                    var strike = Img(N(chip, "Strike", 0, 0, 28, 1.5f), CDim).rectTransform;
                    strike.pivot = new Vector2(0.5f, 0.5f);
                    strike.anchoredPosition = new Vector2(chipW / 2, -chipH / 2);
                    strike.localRotation = Quaternion.Euler(0, 0, 18);
                }
                v.NotChip = chip;
            }
            float svgW = big ? 96 : 84, svgH = big ? 44 : 40;
            RectTransform diaRt;
            if (key == null)
            {
                Dashed(sec, "Empty", 4 + 6, 18, cw - 8 - 12, dia, CFaint);
                diaRt = N(sec, "Dia", (cw - svgW) / 2, 18 + (dia - svgH) / 2, svgW, svgH);
            }
            else diaRt = Diagram(sec, (cw - svgW) / 2, 18 + (dia - svgH) / 2, svgW, svgH, isIf ? "if" : "then", key, !isIf2 && card.Not);
            if (isIf2) v.If2DiaArea = diaRt; else if (isIf) v.IfDiaArea = diaRt; else v.ThenDiaArea = diaRt;
            string name = key == null ? "" : isIf2 ? Name[key] : isIf ? CondName(key, card.Not) : Name[key];
            int size = big ? 13 : isIf ? 11 : 12;
            // A negated condition reads in red, like the NOT switch that made it.
            Txt(sec, "Name", 0, 18 + dia, cw, nameH, name, size, isIf && !isIf2 && card.Not ? CNotName : CText, TextAnchor.MiddleCenter, !isIf);
            if (badge)
            {
                v.Badge = Img(N(sec, "Badge", -13, secH / 2 - 11, 22, 22), CAccent, 0, UISprites.Circle);
                v.Badge.gameObject.AddComponent<Shadow>().effectColor = new Color(0, 0, 0, 0.5f);
                v.Tick = Mark(v.Badge.rectTransform, new[] { (new Vector2(-3.5f, -1f), new Vector2(3f, 8f), 45f), (new Vector2(2.5f, 1f), new Vector2(3f, 13f), -38f) });
                v.Cross = Mark(v.Badge.rectTransform, new[] { (Vector2.zero, new Vector2(3f, 13f), 45f), (Vector2.zero, new Vector2(3f, 13f), -45f) });
                v.Badge.gameObject.SetActive(false);
            }
            return sec;
        }

        /// <summary>White bars around the center of a 22 px badge: a tick or a cross (the font has no such glyphs).</summary>
        static RectTransform Mark(RectTransform badge, (Vector2 pos, Vector2 size, float angle)[] bars)
        {
            var rt = UIKit.Fill(badge, "Mark");
            foreach (var (pos, size, angle) in bars)
            {
                var bar = UIKit.Shape(rt, "Bar", null, Color.white, pos, size);
                UIKit.Round(bar, 1.5f);
                bar.rectTransform.localRotation = Quaternion.Euler(0, 0, angle);
            }
            return rt;
        }

        /// <summary>An arrow drawn with a bar and a triangle, pointing right (angle 0) or up (90); the font has no arrow glyphs.</summary>
        static RectTransform Arrow(Transform p, string name, float x, float y, float len, Color c, float angle)
        {
            var rt = N(p, name, x, y, len, len);
            var holder = UIKit.Centered(rt, "A", Vector2.zero, new Vector2(len, len));
            holder.localRotation = Quaternion.Euler(0, 0, angle);
            UIKit.Shape(holder, "Bar", null, c, new Vector2(-len * 0.12f, 0), new Vector2(len * 0.62f, len * 0.16f));
            var head = UIKit.Shape(holder, "Head", UISprites.Triangle, c, new Vector2(len * 0.27f, 0), new Vector2(len * 0.5f, len * 0.42f));
            head.rectTransform.localRotation = Quaternion.Euler(0, 0, -90);
            return rt;
        }

        static void Pips(RectTransform holder, int count, float size, bool rightAligned)
        {
            UIKit.DestroyChildren(holder);
            float total = count * size + (count - 1) * 3;
            float x0 = rightAligned ? holder.sizeDelta.x - total : 0;
            for (int i = 0; i < count; i++)
            {
                var pip = Img(N(holder, "Pip", x0 + i * (size + 3) + size / 2, size / 2, size, size), CQi, 0, UISprites.Circle).rectTransform;
                pip.pivot = new Vector2(0.5f, 0.5f);
            }
        }

        static void SecState(CardView v, string kind, string state)
        {
            var bg = kind == "if" ? v.IfBg : kind == "if2" ? v.If2Bg : v.ThenBg;
            var frame = kind == "if" ? v.IfFrame : kind == "if2" ? v.If2Frame : v.ThenFrame;
            if (bg == null) return;
            switch (state)
            {
                case "lit": bg.color = CGold.WithAlpha(0.2f); frame.color = CGold; break;
                case "miss": bg.color = CRed.WithAlpha(0.15f); frame.color = CRed; break;
                case "starve": bg.color = CRed.WithAlpha(0.2f); frame.color = CRed; break;
                default: bg.color = new Color(1, 1, 1, 0f); frame.color = new Color(1, 1, 1, 0f); break;
            }
        }

        /// <summary>Card classes: "" / unread / never / reading / win.</summary>
        static void CardState(CardView v, string state)
        {
            v.Group.alpha = state == "unread" ? 0.3f : state == "never" ? 0.22f : 1f;
            v.NeverBorder.gameObject.SetActive(state == "never");
            v.Border.color = state == "reading" ? Color.white : state == "win" ? CGold : new Color(1, 1, 1, 0f);
            v.Glow.color = state == "reading" ? new Color(1, 1, 1, 0.35f) : state == "win" ? CGold.WithAlpha(0.45f) : new Color(1, 1, 1, 0f);
        }

        static void SetBadge(CardView v, bool? yes)
        {
            if (v.Badge == null) return;
            v.Badge.gameObject.SetActive(yes.HasValue);
            if (!yes.HasValue) return;
            v.Badge.color = yes.Value ? CAccent : CRed;
            v.Tick.gameObject.SetActive(yes.Value);
            v.Cross.gameObject.SetActive(!yes.Value);
        }

        // ---------- rendering the cards (renderCards) ----------

        void RenderCards() => RenderCards(cards, cards);

        void RenderCards(List<CardData> head, List<CardData> loadout)
        {
            var s = St;
            UIKit.DestroyChildren(bubble);
            headViews.Clear();
            float x = 0, maxH = 0;
            for (int i = 0; i < head.Count; i++)
            {
                var v = BuildCard(bubble, head[i], i + 1, false, true, s.NotChip, s.Energy.HasValue, false, head.Count);
                v.Root.anchoredPosition = new Vector2(x, 0);
                x += 120 + 6;
                maxH = Mathf.Max(maxH, v.Height);
                headViews.Add(v);
            }
            float bw = Mathf.Max(10, x - 6);
            bubble.sizeDelta = new Vector2(bw, maxH);
            foreach (var v in headViews) v.Root.anchoredPosition = new Vector2(v.Root.anchoredPosition.x, -(maxH - v.Height));
            // tail
            var tail = Img(N(bubble, "Tail", bw / 2 - 7, maxH, 14, 7), CSurface2, 0, UISprites.Triangle);
            tail.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            tail.rectTransform.anchoredPosition = new Vector2(bw / 2, -maxH - 3.5f);
            tail.rectTransform.localRotation = Quaternion.Euler(0, 0, 180);
            reader = N(bubble, "Reader", 0, maxH + 16 - 9, 18, 9);
            reader.pivot = new Vector2(0.5f, 1f);
            reader.anchoredPosition = new Vector2(0, -(maxH + 7));
            Img(reader, Color.white, 0, UISprites.Triangle);
            reader.gameObject.SetActive(false);

            UIKit.DestroyChildren(loadoutRow);
            bigViews.Clear();
            if (St.Watch) return;
            bool ifLocked = s.Locked || s.LockIf;
            float bx = 0;
            for (int i = 0; i < loadout.Count; i++)
            {
                var v = BuildCard(loadoutRow, loadout[i], i + 1, true, false, s.NotChip, s.Energy.HasValue, s.Reorder, loadout.Count, s.AndChip);
                v.Root.anchoredPosition = new Vector2(bx, 0);
                bx += 156 + 12;
                int idx = i;
                if (!s.Locked)
                {
                    if (!ifLocked) Btn(v.IfSec, () => OpenPicker(idx, "if"));
                    if (v.If2Sec != null) Btn(v.If2Sec, () => OpenPicker(idx, "if2"));
                    Btn(v.ThenSec, () => OpenPicker(idx, "then"));
                }
                if (v.AndChip != null)
                {
                    Btn(v.AndChip, () => ToggleAnd(idx));
                    v.AndChip.SetAsLastSibling();
                }
                if (v.NotChip != null && !ifLocked)
                {
                    Btn(v.NotChip, () => ToggleNot(idx));
                    v.NotChip.SetAsLastSibling();
                }
                bigViews.Add(v);
            }
            ApplyPulse();
        }

        void ClearLit()
        {
            foreach (var v in headViews)
            {
                SecState(v, "if", "");
                SecState(v, "if2", "");
                SecState(v, "then", "");
                CardState(v, "");
                SetBadge(v, null);
            }
            if (reader) reader.gameObject.SetActive(false);
        }

        void ResetRunner()
        {
            rx = Px(0); ry = 0; rfall = 0; rrot = 0; ralpha = ghost ? 0.55f : 1f;
            beam = null; hl = null; hl2 = null; mark = null;
            ClearLit();
        }

        // ---------- Energy ----------

        void RenderEnergy()
        {
            int? max = St.Energy;
            energyBox.gameObject.SetActive(max.HasValue);
            if (!max.HasValue) return;
            UIKit.DestroyChildren(energyPips);
            for (int i = 0; i < max.Value; i++)
                Img(N(energyPips, "Pip", i * (14 + 3), 0, 14, 14), i < (energy ?? 0) ? CQi : CRaised, 0, UISprites.Circle);
            float w = max.Value * 17 - 3;
            energyPips.sizeDelta = new Vector2(w, 14);
            energyBox.sizeDelta = new Vector2(10 + 56 + 6 + w + 10, 28);
            // The AND guide's cards are taller and would cover a top-right Energy bar near the flag: it sits top-left there.
            bool left = Group(stage) != "T";
            energyBox.anchorMin = energyBox.anchorMax = energyBox.pivot = new Vector2(left ? 0f : 1f, 1f);
            energyBox.anchoredPosition = new Vector2(left ? 14 : -14, -12);
        }

        IEnumerator FlashEnergy()
        {
            var bg = energyBox.GetComponent<Image>();
            for (int k = 0; k < 3; k++)
            {
                for (float t = 0; t < 0.3f; t += Time.deltaTime)
                {
                    float p = Mathf.Sin(t / 0.3f * Mathf.PI);
                    bg.color = Color.Lerp(CSurface2, CRed, p);
                    energyLabel.color = Color.Lerp(CQi, Color.white, p);
                    yield return null;
                }
            }
            bg.color = CSurface2;
            energyLabel.color = CQi;
        }

        // ---------- the run (runOnce / readInOrder / loop) ----------

        float Px(int i) => OffX + i * T + T / 2;
        float OffX => (W - map.Length * T) / 2;
        static bool Standable(char t) => t == 'g' || t == 'f';

        /// <summary>One condition against the tile ahead (t) and the one after it (t2).</summary>
        static bool Test(string cond, bool neg, char t, char t2)
        {
            if (cond == "notground") return !Standable(t);
            bool b = cond == "ground" ? Standable(t) : cond == "pit" ? t == 'p' : cond == "spike" ? t == 's' : cond == "ground2" && Standable(t2);
            return neg ? !b : b;
        }

        /// <summary>A card's IF holds; an AND card needs both of its conditions.</summary>
        static bool Matches(CardData c, char t, char t2) =>
            Test(c.If, c.Not, t, t2) && (!c.And || (c.If2 != null && Test(c.If2, c.Not2, t, t2)));

        static IEnumerator Wait(float ms) { yield return new WaitForSeconds(ms / 1000f); }

        IEnumerator Tween(float ms, Action<float> f)
        {
            for (float t = 0; t < ms / 1000f; t += Time.deltaTime)
            {
                f(Mathf.Clamp01(t / (ms / 1000f)));
                yield return null;
            }
            f(1f);
        }

        string runResult;

        IEnumerator RunOnce()
        {
            ResetRunner();
            energy = St.Energy;
            RenderEnergy();
            int x = 0;
            yield return Wait(title ? 250 : 500);
            while (true)
            {
                if (map[x] == 'f') { runResult = "win"; yield break; }
                char ahead = x + 1 < map.Length ? map[x + 1] : 'g';
                char ahead2 = x + 2 < map.Length ? map[x + 2] : 'H';
                int fire = cards.FindIndex(c => c.Complete && Matches(c, ahead, ahead2));
                bool starved = fire >= 0 && energy.HasValue && energy.Value < CostOf(cards[fire]);
                beam = (x + 1, fire >= 0);
                if (cards.Count > 1 && fire >= 0)
                {
                    yield return ReadInOrder(fire, ahead, ahead2, starved);
                }
                else if (fire >= 0)
                {
                    hl = x + 1;
                    if (cards[fire].And) { hl2 = x + 2; SecState(headViews[fire], "if2", "lit"); }
                    SecState(headViews[fire], "if", "lit");
                    yield return Wait(slow ? 700 : 260);
                    if (starved) { SecState(headViews[fire], "then", "starve"); StartCoroutine(FlashEnergy()); }
                    else SecState(headViews[fire], "then", "lit");
                    yield return Wait(starved ? 1000 : slow ? 450 : 160);
                }
                else yield return Wait(slow ? 120 : 40);

                string act = fire >= 0 && !starved ? cards[fire].Then : "walk";
                if (energy.HasValue && act != "walk")
                {
                    int spent = CostOf(cards[fire]);
                    energy -= spent;
                    RenderEnergy();
                    AddFloat(rx, GY - 60, "-" + spent);
                    // The Energy bar lights up, shows what was spent, and the runner waits a moment.
                    energySpent.text = "-" + spent;
                    energyBox.localScale = Vector3.one * 1.35f;
                    var cardCost = fire >= 0 && fire < headViews.Count ? headViews[fire].BandCost : null;
                    if (cardCost) foreach (Transform pip in cardCost) pip.localScale = Vector3.one * 1.5f;
                    yield return Wait(900);
                    energyBox.localScale = Vector3.one;
                    energySpent.text = "";
                    if (cardCost) foreach (Transform pip in cardCost) pip.localScale = Vector3.one;
                }
                int to = x + Dist[act];
                float from = rx, dest = Px(to);
                float hgt = act == "jump" ? 78 : act == "bigjump" ? 92 : 0;
                float ms = act == "walk" ? (title ? 190 : 280) : slow ? 800 : act == "jump" ? 520 : 620;
                yield return Tween(ms, k =>
                {
                    rx = from + (dest - from) * k;
                    ry = hgt > 0 ? -Mathf.Sin(Mathf.PI * k) * hgt : -Mathf.Abs(Mathf.Sin(Mathf.PI * k * 2)) * 3;
                });
                beam = null; hl = null; hl2 = null;
                ClearLit();
                x = to;
                char t = x < map.Length ? map[x] : 'p';
                if (t == 'p')
                {
                    mark = x;
                    float a0 = ghost ? 0.55f : 1f;
                    yield return Tween(500, k => { rfall = k * k * 120; ralpha = a0 * (1 - k); });
                    runResult = "dead";
                    yield break;
                }
                if (t == 's')
                {
                    mark = x;
                    Burst(rx, GY - 20, CSpike, 14);
                    float a0 = ghost ? 0.55f : 1f;
                    yield return Tween(450, k => { rrot = k * Mathf.PI / 2; ralpha = a0 * (1 - k * 0.7f); });
                    runResult = "dead";
                    yield break;
                }
            }
        }

        // Several cards: every check starts with all cards grey. A pointer moves from the left card to the right one;
        // each card it reaches gets a red cross (no match) or a gold tick (match). At the first tick it stops.
        IEnumerator ReadInOrder(int fire, char ahead, char ahead2, bool starved)
        {
            foreach (var v in headViews) { CardState(v, "unread"); SetBadge(v, null); }
            reader.gameObject.SetActive(true);
            bool shadowed = fire >= 0 && cards.Where((c, i) => i > fire && c.Complete && Matches(c, ahead, ahead2)).Any();
            int last = fire >= 0 ? fire : headViews.Count - 1;
            for (int i = 0; i <= last; i++)
            {
                reader.anchoredPosition = new Vector2(i * 126 + 60, reader.anchoredPosition.y);
                CardState(headViews[i], "reading");
                yield return Wait(fire >= 0 ? 320 : 150);
                CardState(headViews[i], "");
                if (i == fire)
                {
                    SetBadge(headViews[i], true);
                    hl = beam?.tile;
                    if (cards[i].And) { hl2 = beam?.tile + 1; SecState(headViews[i], "if2", "lit"); }
                    SecState(headViews[i], "if", "lit");
                    yield return Wait(260);
                    if (starved)
                    {
                        SecState(headViews[i], "then", "starve");
                        StartCoroutine(FlashEnergy());
                        yield return Wait(1000);
                        yield break;
                    }
                    SecState(headViews[i], "then", "lit");
                    if (shadowed) for (int j = i + 1; j < headViews.Count; j++) CardState(headViews[j], "never");
                    yield return Wait(shadowed ? 1400 : 220);
                    yield break;
                }
                SetBadge(headViews[i], false);
                // Hold the cross a moment so the miss is seen before the pointer moves on.
                yield return Wait(900);
                CardState(headViews[i], "unread");
            }
        }

        IEnumerator Loop()
        {
            int deaths = 0;
            while (true)
            {
                yield return RunOnce();
                ReportRun(runResult == "win");
                if (runResult == "win") { Celebrate(); yield break; }
                deaths++;
                HintPanel();
                if (deaths >= 2) OfferHint();
                yield return Wait(700);
            }
        }

        string lastReported;

        void ReportRun(bool win)
        {
            if (St.Watch) return;
            var ids = cards.Where(c => c.Complete)
                .Select(c => "IF " + (c.If == "notground" ? "NOT ground" : (c.Not ? "NOT " : "") + c.If) +
                             (c.And ? " AND " + (c.Not2 ? "NOT " : "") + c.If2 : "") + " THEN " + c.Then).ToList();
            string key = stage + "|" + string.Join(";", ids) + "|" + win;
            // The ghost runs again and again; each card setup counts once.
            if (key == lastReported) return;
            lastReported = key;
            RunEnded?.Invoke(StageLevel[stage], ids, win, hintsShown);
        }

        // ---------- hints ----------

        void OfferHint()
        {
            var s = St;
            if (s.Hints == null || hintsShown >= s.Hints.Length) return;
            hintBtn.gameObject.SetActive(true);
            hintBtnText.text = "Hint " + (hintsShown + 1) + "/" + s.Hints.Length;
            if (hintsShown == 0) nudge.gameObject.SetActive(true);
            LayoutTop();
        }

        void OnHint()
        {
            var s = St;
            nudge.gameObject.SetActive(false);
            bannerText.text = s.Hints[hintsShown++];
            bannerText.horizontalOverflow = HorizontalWrapMode.Overflow;
            float bw = Mathf.Min(bannerText.preferredWidth + 28, StageW * 0.7f);
            bannerText.horizontalOverflow = HorizontalWrapMode.Wrap;
            bannerText.rectTransform.sizeDelta = new Vector2(bw - 28, 36);
            float bh = Mathf.Max(36, bannerText.preferredHeight + 16);
            bannerText.rectTransform.sizeDelta = new Vector2(bw - 28, bh);
            banner.sizeDelta = new Vector2(bw, bh);
            banner.gameObject.SetActive(true);
            if (hintsShown >= s.Hints.Length) hintBtn.gameObject.SetActive(false);
            else hintBtnText.text = "Hint " + (hintsShown + 1) + "/" + s.Hints.Length;
            LayoutTop();
        }

        // After a fall, every part of a card the player has not touched yet blinks: IF, THEN and the NOT button
        // (or the ">" that moves a card, in the order level). A part stops blinking once it is used.
        void HintPanel()
        {
            var s = St;
            if (s.NoBlink) return;
            IEnumerable<string> partsList;
            if (s.Locked) partsList = new[] { "0:move" };
            else if (s.LockIf) partsList = done.Contains("0:move") ? new[] { "0:then" } : new[] { "0:move" };
            else
            {
                int empty = cards.FindIndex(c => !c.Complete);
                int i = empty >= 0 ? empty : 0;
                var list = s.Blink ?? new[] { "if", "then" }.Concat(s.NotChip ? new[] { "not" } : new string[0]).ToArray();
                partsList = list.Select(p => i + ":" + p);
            }
            foreach (var p in partsList) if (!done.Contains(p)) pulse.Add(p);
            ApplyPulse();
        }

        readonly List<(RectTransform rt, Image frame)> pulsing = new List<(RectTransform, Image)>();

        void ApplyPulse()
        {
            foreach (var p in pulsing) if (p.frame) Destroy(p.frame.gameObject);
            pulsing.Clear();
            for (int i = 0; i < bigViews.Count; i++)
            {
                var v = bigViews[i];
                var el = new Dictionary<string, RectTransform>
                {
                    { "if", v.IfSec }, { "if2", v.If2Sec }, { "then", v.ThenSec }, { "not", v.NotChip }, { "and", v.AndChip }, { "move", v.Move },
                };
                foreach (var kv in el)
                    if (kv.Value != null && pulse.Contains(i + ":" + kv.Key)) AddPulse(kv.Value, kv.Key == "not" || kv.Key == "and" ? 7f : 8f);
            }
        }

        void AddPulse(RectTransform target, float radius)
        {
            var frame = FillRing(target, CGold, 2f);
            frame.name = "Pulse";
            pulsing.Add((target, frame));
        }

        void Used(string part)
        {
            done.Add(part);
            pulse.Remove(part);
        }

        // ---------- win / next ----------

        void Celebrate()
        {
            won = true;
            ghost = false;
            ralpha = 1f;
            foreach (var v in headViews) CardState(v, "win");
            panelOff = true;
            for (int i = 0; i < 4; i++) StartCoroutine(DelayedBurst(i));
            RenderDots(true);
            if (!St.Watch) Cleared?.Invoke(StageLevel[stage]);
            if (St.NotIntro) StartCoroutine(After(1300, ShowNotIntro));
            else ShowNext();
        }

        IEnumerator DelayedBurst(int i)
        {
            yield return Wait(i * 150);
            Burst(Px(map.Length - 1), GY - 70, new[] { CGold, CFinish, CJump, H(0xf472b6) }[i], 18);
        }

        IEnumerator After(float ms, Action a)
        {
            yield return Wait(ms);
            a();
        }

        void ShowNext()
        {
            nextBtn.gameObject.SetActive(true);
        }

        // ---------- particles and floating text ----------

        void Burst(float x, float y, Color c, int n)
        {
            for (int i = 0; i < n; i++)
            {
                float a = UnityEngine.Random.value * Mathf.PI * 2, v = 2 + UnityEngine.Random.value * 4;
                var img = Box(fx, "P", 0, 0, 6, 6, c);
                parts.Add(new Particle { Rt = img.rectTransform, Img = img, X = x, Y = y, Vx = Mathf.Cos(a) * v, Vy = Mathf.Sin(a) * v - 3, Life = 1 });
            }
        }

        void AddFloat(float x, float y, string text)
        {
            var t = Txt(fx, "F", 0, 0, 80, 26, text, 22, CQi, TextAnchor.MiddleCenter, true);
            floats.Add(new Float { Txt = t, X = x, Y = y, Life = 1 });
        }

        // ---------- dots and top bar ----------

        void RenderDots(bool doneCurrent)
        {
            UIKit.DestroyChildren(dotsRt);
            int n = 0;
            for (int i = 0; i < Stages.Length; i++)
            {
                if (Group(i) != Group(stage) || Stages[i].TitleOnly) continue;
                var c = i < stage || (i == stage && doneCurrent) ? CAccent : i == stage ? CGold : CRaised;
                Img(N(dotsRt, "Dot", n++ * 18, 0, 11, 11), c, 0, UISprites.Circle);
            }
        }

        void LayoutTop()
        {
            float right = AppW - 32 - 12 - (6 * 11 + 5 * 7);
            if (hintBtn.gameObject.activeSelf)
            {
                hintBtn.anchoredPosition = new Vector2(right - 10 - 82, -10);
                right -= 10 + 82;
            }
            float left = 144;
            hintBox.anchoredPosition = new Vector2(left, -8);
            hintBox.sizeDelta = new Vector2(right - 6 - 10 - left, 35);
            hintText.rectTransform.sizeDelta = new Vector2(hintBox.sizeDelta.x - 24, 35);
            hintText.horizontalOverflow = HorizontalWrapMode.Wrap;
        }

        // ---------- picking (IF / THEN) and moving cards ----------

        void BuildPicker(RectTransform root)
        {
            pickerRoot = UIKit.Fill(root, "Picker");
            var dim = pickerRoot.gameObject.AddComponent<Image>();
            dim.color = new Color(0, 0, 0, 0.53f);
            Btn(pickerRoot, ClosePicker).transition = Selectable.Transition.None;
            var box = N(pickerRoot, "Box", 0, 0, 400, 200);
            box.anchorMin = box.anchorMax = box.pivot = new Vector2(0.5f, 0.5f);
            Img(box, CSurface, 16f).raycastTarget = true;
            box.gameObject.AddComponent<Button>().transition = Selectable.Transition.None;   // clicks on the box do not close it
            pickTitle = Txt(box, "Title", 18, 16, 300, 20, "", 16, CGold);
            pickerGrid = N(box, "Grid", 18, 48, 300, 84);
            var cancel = N(box, "Cancel", 0, 0, 72, 31);
            Img(cancel, CRaised, 8f);
            Txt(cancel, "T", 0, 0, 72, 31, "Cancel", 13, CText, TextAnchor.MiddleCenter, true);
            Btn(cancel, ClosePicker);
            pickerRoot.gameObject.SetActive(false);
        }

        void OpenPicker(int i, string kind)
        {
            if (won || panelOff) return;
            var s = St;
            var card = cards[i];
            pickTitle.text = kind == "if" ? "IF ..." : kind == "if2" ? "AND ..." : "THEN ...";
            UIKit.DestroyChildren(pickerGrid);
            string cur = kind == "then" ? card.Then : kind == "if2" ? If2Key(card) : card.Not && !s.NotChip ? "not" + card.If : card.If;
            var keys = kind == "if" ? s.Ifs : kind == "if2" ? s.If2s : s.Thens;
            for (int k = 0; k < keys.Length; k++)
            {
                string key = keys[k];
                var opt = N(pickerGrid, "Opt", k * 120, 0, 110, 84);
                Img(opt, cur == key ? H(0x3f4a62) : CSurface2, 12f).raycastTarget = true;
                if (cur == key) Ring(opt, "Sel", 0, 0, 110, 84, CGold, 2f);
                bool hasCost = kind == "then" && s.Energy.HasValue;
                Diagram(opt, (110 - 84) / 2f, hasCost ? 6 : 12, 84, 40, kind == "then" ? "then" : "if", key, false);
                Txt(opt, "Name", 0, hasCost ? 46 : 54, 110, 16, Name[key], 12, CText, TextAnchor.MiddleCenter, true);
                if (hasCost)
                {
                    int c = Cost[key] + (card.And ? 1 : 0);
                    var pr = N(opt, "Cost", (110 - (c * 12 - 3)) / 2f, 66, c * 12 - 3, 9);
                    Pips(pr, c, 9, false);
                }
                if (s.PulseOpt == key && cur != key) AddPulse(opt, 12f);
                Btn(opt, () => { ClosePicker(); Choose(i, kind, key); });
            }
            float gw = keys.Length * 120 - 10;
            float bw = Mathf.Max(gw + 36, 200);
            var box = (RectTransform)pickTitle.transform.parent;
            box.sizeDelta = new Vector2(bw, 48 + 84 + 12 + 31 + 14);
            pickerGrid.anchoredPosition = new Vector2((bw - gw) / 2f, -48);
            var cancel = box.Find("Cancel") as RectTransform;
            cancel.anchoredPosition = new Vector2((bw - 72) / 2f, -(48 + 84 + 12));
            pickerRoot.gameObject.SetActive(true);
            pickerRoot.SetAsLastSibling();
        }

        void ClosePicker() => pickerRoot.gameObject.SetActive(false);

        void Choose(int i, string kind, string key)
        {
            if (won) return;
            Used(i + ":" + kind);
            StopRun();
            ResetRunner();
            var prev = cards.Select(c => c.Copy()).ToList();
            var c2 = cards[i].Copy();
            if (kind == "then") c2.Then = key;
            else if (kind == "if2") { c2.Not2 = key.StartsWith("not"); c2.If2 = c2.Not2 ? key.Substring(3) : key; }
            else if (key == "notground") { c2.If = "ground"; c2.Not = true; }
            // With a NOT button on the card, picking a new condition keeps the NOT setting.
            else { c2.If = key; c2.Not = St.NotChip && cards[i].Not; }
            cards[i] = c2;
            // The loadout card changes first; the card over the runner catches up when the copy lands.
            RenderCards(prev, cards);
            var from = kind == "if" ? bigViews[i].IfDiaArea : kind == "if2" ? bigViews[i].If2DiaArea : bigViews[i].ThenDiaArea;
            var target = kind == "if" ? headViews[i].IfDiaArea : kind == "if2" ? headViews[i].If2DiaArea : headViews[i].ThenDiaArea;
            int my = token;
            StartCoroutine(Fly(from, kind == "then" ? "then" : "if", kind == "if" ? c2.If : key, kind == "if" && c2.Not, target, () =>
            {
                if (my != token) return;
                RenderCards();
                StartCoroutine(Loop());
            }));
        }

        IEnumerator Fly(RectTransform from, string kind, string key, bool neg, RectTransform to, Action doneCb)
        {
            var root = (RectTransform)transform;
            var fly = N(root, "Fly", 0, 0, from.rect.width, from.rect.height);
            flying.Add(fly.gameObject);
            fly.pivot = new Vector2(0f, 1f);
            Img(fly, CSurface2, 12f);
            Diagram(fly, 0, 0, from.rect.width, from.rect.height, kind, key, neg);
            Vector3 a = from.TransformPoint(new Vector3(from.rect.xMin, from.rect.yMax));
            Vector3 b = to.TransformPoint(new Vector3(to.rect.xMin, to.rect.yMax));
            float sa = from.lossyScale.x, sb = to.lossyScale.x * to.rect.height / Mathf.Max(1f, from.rect.height);
            fly.position = a;
            fly.localScale = Vector3.one * (sa / root.lossyScale.x);
            yield return null;
            for (float t = 0; t < 0.35f; t += Time.deltaTime)
            {
                float k = t / 0.35f;
                k = k * k;
                fly.position = Vector3.Lerp(a, b, k);
                fly.localScale = Vector3.one * Mathf.Lerp(sa, sb, k) / root.lossyScale.x;
                yield return null;
            }
            yield return Wait(30);
            flying.Remove(fly.gameObject);
            Destroy(fly.gameObject);
            doneCb();
        }

        void ToggleNot(int i)
        {
            if (won || panelOff) return;
            Used(i + ":not");
            StopRun();
            cards[i] = cards[i].Copy();
            cards[i].Not = !cards[i].Not;
            ResetRunner();
            RenderCards();
            SecState(headViews[i], "if", "lit");
            int my = token;
            StartCoroutine(After(350, () => { if (my == token && i < headViews.Count) SecState(headViews[i], "if", ""); }));
            StartCoroutine(Loop());
        }

        /// <summary>"+AND" turns card i into an AND card with an empty second row, or back into a one-condition card.</summary>
        void ToggleAnd(int i)
        {
            if (won || panelOff) return;
            Used(i + ":and");
            StopRun();
            var c = cards[i].Copy();
            c.And = !c.And;
            c.If2 = null;
            c.Not2 = false;
            cards[i] = c;
            ResetRunner();
            RenderCards();
            StartCoroutine(Loop());
        }

        void MoveCard(int i, int d)
        {
            int j = i + d;
            if (won || panelOff || j < 0 || j >= cards.Count) return;
            Used("0:move");
            StopRun();
            (cards[i], cards[j]) = (cards[j], cards[i]);
            ResetRunner();
            RenderCards();
            StartCoroutine(Shake(bubble));
            StartCoroutine(Loop());
        }

        IEnumerator Shake(RectTransform rt)
        {
            float[] keys = { 0, -7, 7, -5, 5, 0 };
            for (float t = 0; t < 0.45f; t += Time.deltaTime)
            {
                float p = t / 0.45f * 5f;
                int k = Mathf.Min(4, (int)p);
                shakeX = Mathf.Lerp(keys[k], keys[k + 1], p - k);
                yield return null;
            }
            shakeX = 0;
        }

        float shakeX;
        readonly List<GameObject> flying = new List<GameObject>();

        /// <summary>Stops the run and every pending step (token++ in the prototype).</summary>
        void StopRun()
        {
            token++;
            StopAllCoroutines();
            // A copy still in the air would stay where it was stopped.
            foreach (var f in flying) if (f) Destroy(f);
            flying.Clear();
            shakeX = 0;
            energyBox.localScale = Vector3.one;
        }

        void StopAll()
        {
            StopRun();
            ClosePicker();
            introRoot.gameObject.SetActive(false);
            notIntroRoot.gameObject.SetActive(false);
        }

        // ---------- stages (playStage) ----------

        /// <summary>The first stage of a tutorial level that the tutorial plays (title-only stages are skipped).</summary>
        static int FirstStageOf(string levelId)
        {
            for (int j = 0; j < Stages.Length; j++)
                if (StageLevel[j] == levelId && !Stages[j].TitleOnly) return j;
            return Array.FindIndex(Stages, st => !st.TitleOnly);
        }

        /// <summary>Opens the tutorial at the first stage of a tutorial level ("T1" starts with the card breaking).</summary>
        public void Open(string levelId)
        {
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            title = false;
            TitleLayout(0f);
            HideUntilFitted();
            int i = FirstStageOf(levelId);
            pageHeight = GroupPageHeight(i);
            FitPage();
            PlayStage(i, false);
        }

        public void Close()
        {
            StopAll();
            gameObject.SetActive(false);
        }

        void PlayStage(int i, bool fromNext)
        {
            // After the last stage, the Next button leads to the level list.
            if (fromNext && i == stage && won) { StopAll(); Finished?.Invoke(StageLevel[stage]); return; }
            StopRun();
            StartCoroutine(PlayStageRoutine(i));
        }

        /// <summary>Sets up a stage's map, cards, runner and top bar (no run, no intro).</summary>
        void LoadStage(int i)
        {
            var s = Stages[i];
            stage = i;
            pageHeight = GroupPageHeight(i);
            foreach (var p in parts) Destroy(p.Rt.gameObject);
            parts.Clear();
            map = s.Map;
            cards = s.Cards.Select(c => c.Copy()).ToList();
            ghost = !s.Watch;
            slow = s.Watch;
            won = false;
            panelOff = false;
            pulse.Clear();
            done.Clear();
            hintsShown = 0;
            lastReported = null;
            hintBtn.gameObject.SetActive(false);
            banner.gameObject.SetActive(false);
            nudge.gameObject.SetActive(false);
            lvlText.text = s.Id;
            hintText.text = s.Hint ?? "";
            hintBox.gameObject.SetActive(s.Hint != null);
            LayoutTop();
            nextBtn.gameObject.SetActive(false);
            RenderDots(false);
            BuildMap();
            ResetRunner();
        }

        IEnumerator PlayStageRoutine(int i)
        {
            var s = Stages[i];
            LoadStage(i);
            if (!s.Watch) StageStarted?.Invoke(StageLevel[i]);
            if (s.BreakFrom != null)
            {
                // The card from the last stage shows up, then cracks and goes blank.
                cards = s.BreakFrom.Select(c => c.Copy()).ToList();
                RenderCards();
                yield return Wait(900);
                StartCoroutine(Shake(bubble));
                Burst(rx, GY - 140, H(0xcbd5e1), 12);
                yield return Wait(450);
                cards = s.Cards.Select(c => c.Copy()).ToList();
            }
            RenderCards();
            energy = s.Energy;
            RenderEnergy();
            if (s.NewAction != null || s.NewEnergy || s.NewOrder || s.NewAnd)
            {
                bool closed = false;
                if (s.NewAnd) ShowAndIntro(() => closed = true);
                else if (s.NewAction != null) ShowNewAction(() => closed = true);
                else if (s.NewEnergy) ShowEnergyIntro(() => closed = true);
                else ShowOrderIntro(() => closed = true);
                while (!closed) yield return null;
            }
            yield return Wait(400);
            if (s.Watch)
            {
                yield return RunOnce();
                if (runResult == "win") Celebrate();
            }
            else yield return Loop();
        }

        void BuildMap()
        {
            UIKit.DestroyChildren(mapLayer);
            float ox = OffX;
            Box(mapLayer, "WallL", ox - T, GY - 120, T, Hh - GY + 120, CWall);
            Box(mapLayer, "WallR", ox + map.Length * T, GY - 120, T, Hh - GY + 120, CWall);
            for (int i = 0; i < map.Length; i++)
            {
                char t = map[i];
                float x0 = ox + i * T;
                if (t == 'p') { Box(mapLayer, "Pit", x0, GY, T, Hh - GY, CPit); continue; }
                Box(mapLayer, "Ground", x0 + 1, GY, T - 2, T, i % 2 == 1 ? CGroundDark : CGround);
                if (t == 's')
                    for (int k = 0; k < 3; k++)
                        Img(N(mapLayer, "Spike", x0 + 6 + k * 14, GY - 26, 14, 26), CSpike, 0, UISprites.Triangle);
                if (t == 'f')
                {
                    Box(mapLayer, "Pole", x0 + T / 2 + 6, GY - 66, 3, 66, H(0xeef1f5));
                    Box(mapLayer, "Flag", x0 + T / 2 - 15, GY - 66, 21, 20, CFinish);
                }
            }
        }

        // ---------- intro cards (showIntro / showNotIntro) ----------

        void BuildIntro(RectTransform root)
        {
            introRoot = UIKit.Fill(root, "Intro");
            Img(introRoot, new Color(0, 0, 0, 0.6f)).raycastTarget = true;
            var box = N(introRoot, "Box", 0, 0, 440, 280);
            box.anchorMin = box.anchorMax = box.pivot = new Vector2(0.5f, 0.5f);
            Img(box, CSurface, 16f);
            introTitle = Txt(box, "Title", 20, 18, 400, 20, "", 16, CGold, TextAnchor.MiddleLeft, true);
            introLeft = N(box, "Left", 0, 50, 110, 100);
            introEq = Txt(box, "Eq", 0, 50, 40, 100, "", 26, CDim, TextAnchor.MiddleCenter, true);
            introArrow = Arrow(box, "Arrow", 0, 0, 26, CDim, 0);
            introRight = N(box, "Right", 0, 50, 110, 100);
            introText = Txt(box, "Text", 20, 164, 400, 50, "", 13, CDim, TextAnchor.UpperCenter);
            introText.horizontalOverflow = HorizontalWrapMode.Wrap;
            var ok = N(box, "Ok", (440 - 72) / 2f, 228, 72, 31);
            Img(ok, CRaised, 8f);
            Txt(ok, "T", 0, 0, 72, 31, "Got it", 13, CText, TextAnchor.MiddleCenter, true);
            Btn(ok, () => { introRoot.gameObject.SetActive(false); introOk?.Invoke(); });
            introRoot.gameObject.SetActive(false);
        }

        /// <summary>One option tile (.opt) with a diagram, a name and optional cost pips.</summary>
        static void Opt(RectTransform holder, string kind, string key, string label, int pipCount, bool sel)
        {
            UIKit.DestroyChildren(holder);
            float h = pipCount > 0 ? 100 : 84;
            holder.sizeDelta = new Vector2(110, h);
            Img(N(holder, "Bg", 0, 0, 110, h), sel ? H(0x3f4a62) : CSurface2, 12f);
            if (sel) Ring(holder, "Sel", 0, 0, 110, h, CGold, 2f);
            Diagram(holder, 13, 8, 84, 40, kind, key, false);
            var t = Txt(holder, "Name", 4, 50, 102, 30, label, 12, CText, TextAnchor.UpperCenter, true);
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            if (pipCount > 0)
            {
                var pr = N(holder, "Cost", (110 - (pipCount * 12 - 3)) / 2f, h - 18, pipCount * 12 - 3, 9);
                Pips(pr, pipCount, 9, false);
            }
        }

        void ShowIntro(string title, Action<RectTransform> left, Action<RectTransform> right, string eq, string text, Action onOk)
        {
            introTitle.text = title;
            left(introLeft);
            right(introRight);
            introEq.text = eq == "->" ? "" : eq;
            introArrow.gameObject.SetActive(eq == "->");
            float total = introLeft.sizeDelta.x + 16 + 40 + 16 + introRight.sizeDelta.x;
            float x0 = (440 - total) / 2f;
            introLeft.anchoredPosition = new Vector2(x0, -50);
            introEq.rectTransform.anchoredPosition = new Vector2(x0 + introLeft.sizeDelta.x + 16, -50);
            introRight.anchoredPosition = new Vector2(x0 + introLeft.sizeDelta.x + 72, -50);
            introText.text = text;
            float top = 50 + Mathf.Max(introLeft.sizeDelta.y, introRight.sizeDelta.y);
            introEq.rectTransform.sizeDelta = new Vector2(40, top - 50);
            introArrow.anchoredPosition = new Vector2(x0 + introLeft.sizeDelta.x + 16 + 7, -(50 + (top - 50) / 2f - 13));
            introText.rectTransform.anchoredPosition = new Vector2(20, -(top + 14));
            float th = introText.preferredHeight;
            var box = (RectTransform)introText.transform.parent;
            var ok = (RectTransform)box.Find("Ok");
            ok.anchoredPosition = new Vector2((440 - 72) / 2f, -(top + 14 + th + 12));
            box.sizeDelta = new Vector2(440, top + 14 + th + 12 + 31 + 16);
            introOk = onOk;
            introRoot.gameObject.SetActive(true);
            introRoot.SetAsLastSibling();
        }

        void ShowNewAction(Action onOk) => ShowIntro("NEW: BIG JUMP",
            h => Opt(h, "then", "jump", Name["jump"], 0, false), h => Opt(h, "then", "bigjump", Name["bigjump"], 0, true), "->",
            "Jump clears one tile. <b><color=#b89bff>Big Jump</color></b> clears two tiles in a row.", onOk);

        void ShowOrderIntro(Action onOk) => ShowIntro("NEW: MORE THAN ONE CARD",
            h => MiniCard(h, 1, CJump, "spike", "jump", false), h => MiniCard(h, 2, CBigJump, "pit", "bigjump", true), "->",
            "Cards are checked <b><color=#eef1f5>from left to right</color></b>.\nThe first card whose IF matches is the one that acts; the cards after it are not checked.", onOk);

        void ShowEnergyIntro(Action onOk) => ShowIntro("NEW: ENERGY",
            h => Opt(h, "then", "jump", Name["jump"], Cost["jump"], false), h => Opt(h, "then", "bigjump", Name["bigjump"], Cost["bigjump"], true), "->",
            "Every jump costs <b><color=#4fd6e8>Energy</color></b>: Jump 1, Big Jump 3. You get 4 per run.\nA card without enough Energy does nothing.", onOk);

        void ShowAndIntro(Action onOk) => ShowIntro("NEW: AND",
            h => IntroCardOf(h, Cd("pit", "bigjump")), h => IntroCardOf(h, CdAnd("pit", "ground2", true, "bigjump")), "->",
            "<b><color=#4fd6e8>AND</color></b> adds a second condition. The card acts only when <b><color=#eef1f5>BOTH</color></b> are true.\n" +
            "An AND card costs 1 more Energy.", onOk);

        /// <summary>A loadout card shrunk to 110 wide, for the intro.</summary>
        void IntroCardOf(RectTransform holder, CardData card)
        {
            UIKit.DestroyChildren(holder);
            var v = BuildCard(holder, card, 1, true, false, false, true, false, 1);
            const float scale = 110f / 156f;
            v.Root.localScale = Vector3.one * scale;
            holder.sizeDelta = new Vector2(110, v.Height * scale);
        }

        /// <summary>miniCard inside an option tile: number band, condition diagram, action diagram (gap 3, padding 8).</summary>
        static void MiniCard(RectTransform holder, int n, Color color, string cond, string act, bool sel)
        {
            UIKit.DestroyChildren(holder);
            const float h = 8 + 16 + 3 + 40 + 3 + 40 + 8;
            holder.sizeDelta = new Vector2(110, h);
            Img(N(holder, "Bg", 0, 0, 110, h), sel ? H(0x3f4a62) : CSurface2, 12f);
            if (sel) Ring(holder, "Sel", 0, 0, 110, h, CGold, 2f);
            var band = Box(holder, "N", 55 - 17, 8, 34, 16, color, 6f);
            Txt(band.rectTransform, "T", 0, 0, 34, 16, n.ToString(), 11, CText, TextAnchor.MiddleCenter);
            Diagram(holder, 13, 27, 84, 40, "if", cond, false);
            Diagram(holder, 13, 70, 84, 40, "then", act, false);
        }

        void BuildNotIntro(RectTransform root)
        {
            notIntroRoot = UIKit.Fill(root, "NotIntro");
            Img(notIntroRoot, new Color(0, 0, 0, 0.6f)).raycastTarget = true;
            var box = N(notIntroRoot, "Box", 0, 0, 440, 350);
            box.anchorMin = box.anchorMax = box.pivot = new Vector2(0.5f, 0.5f);
            Img(box, CSurface, 16f);
            Txt(box, "Title", 20, 18, 400, 20, "NOT", 16, CGold, TextAnchor.MiddleLeft, true);
            niOpt = N(box, "Opt", 60, 100, 110, 84);
            Txt(box, "Eq", 186, 50, 40, 200, "=", 26, CDim, TextAnchor.MiddleCenter, true);
            niCardHolder = N(box, "Card", 242, 50, 132, 210);
            var p = Txt(box, "Text", 20, 270, 400, 20, "From now on, tap <b><color=#ff6b4a>NOT</color></b> on a card to flip what it looks for.", 13, CDim,
                TextAnchor.MiddleCenter);
            p.horizontalOverflow = HorizontalWrapMode.Wrap;
            var ok = N(box, "Ok", (440 - 72) / 2f, 302, 72, 31);
            Img(ok, CRaised, 8f);
            Txt(ok, "T", 0, 0, 72, 31, "Got it", 13, CText, TextAnchor.MiddleCenter, true);
            Btn(ok, () =>
            {
                if (niCycle != null) StopCoroutine(niCycle);
                notIntroRoot.gameObject.SetActive(false);
                ShowNext();
            });
            notIntroRoot.gameObject.SetActive(false);
        }

        void ShowNotIntro()
        {
            Opt(niOpt, "if", "notground", Name["notground"], 0, true);
            notIntroRoot.gameObject.SetActive(true);
            notIntroRoot.SetAsLastSibling();
            niCycle = StartCoroutine(NotIntroCycle());
        }

        IEnumerator NotIntroCycle()
        {
            while (true)
            {
                UIKit.DestroyChildren(niCardHolder);
                var v = BuildCard(niCardHolder, Cd("ground", "jump"), 1, true, false, true, false, false, 1);
                v.Root.localScale = Vector3.one * (132f / 156f);
                var chip = v.NotChip;
                var f = N(niCardHolder, "Finger", 0, 0, 26, 26);
                f.pivot = new Vector2(0.5f, 0.5f);
                var fImg = Img(f, Color.white, 0, UISprites.Circle);
                var fRing = Img(N(f, "Ring", -2, -2, 30, 30), new Color(0, 0, 0, 0.67f), 0, UISprites.Circle);
                fRing.transform.SetAsFirstSibling();
                Vector3 chipPos = chip.TransformPoint(chip.rect.center);
                Vector3 local = niCardHolder.InverseTransformPoint(chipPos);
                Vector2 home = new Vector2(local.x, local.y);
                var group = f.gameObject.AddComponent<CanvasGroup>();
                group.alpha = 0;
                f.anchoredPosition = home + new Vector2(40, -50);
                yield return Wait(600);
                yield return Tween(350, k => { group.alpha = Mathf.Min(1, k * 1.75f); f.anchoredPosition = Vector2.Lerp(home + new Vector2(40, -50), home, k); });
                yield return Wait(150);
                yield return Tween(150, k => f.localScale = Vector3.one * Mathf.Lerp(1f, 0.8f, k));
                // tap: the chip turns on and the IF part shows NOT Ground ahead
                UIKit.DestroyChildren(niCardHolder);
                v = BuildCard(niCardHolder, Cd("ground", "jump", true), 1, true, false, true, false, false, 1);
                v.Root.localScale = Vector3.one * (132f / 156f);
                SecState(v, "if", "lit");
                f = N(niCardHolder, "Finger", 0, 0, 26, 26);
                f.pivot = new Vector2(0.5f, 0.5f);
                Img(f, Color.white, 0, UISprites.Circle);
                group = f.gameObject.AddComponent<CanvasGroup>();
                f.anchoredPosition = home;
                f.localScale = Vector3.one * 0.8f;
                yield return Tween(350, k => { group.alpha = 1 - k; f.anchoredPosition = Vector2.Lerp(home, home + new Vector2(30, -40), k); });
                yield return Wait(3200 - 600 - 350 - 150 - 150 - 350);
            }
        }

        // ---------- drawing (draw) ----------

        void Update()
        {
            if (map.Length == 0) return;
            float ox = OffX;

            if (hl.HasValue)
            {
                hlImg.gameObject.SetActive(true);
                hlGlow.gameObject.SetActive(true);
                Place(hlImg, ox + hl.Value * T + 3, GY - 32, T - 6, T + 28);
                Place(hlGlow, ox + hl.Value * T + 3, GY - 32, T - 6, T + 28);
            }
            else { hlImg.gameObject.SetActive(false); hlGlow.gameObject.SetActive(false); }
            // An AND card also looks two tiles ahead (Ground 2 ahead): that tile gets its own frame.
            hl2Img.gameObject.SetActive(hl2.HasValue);
            if (hl2.HasValue) Place(hl2Img, ox + hl2.Value * T + 3, GY - 32, T - 6, T + 28);

            // What the runner looks at: a dashed line to the next tile, gold when a card's IF matches it.
            int used = 0;
            if (beam.HasValue)
            {
                var a = new Vector2(rx + 8, GY + ry - 24);
                var b = new Vector2(ox + beam.Value.tile * T + T / 2, GY + 2);
                float len = Vector2.Distance(a, b);
                var dir = (b - a) / Mathf.Max(0.01f, len);
                float angle = Mathf.Atan2(-dir.y, dir.x) * Mathf.Rad2Deg;
                var col = beam.Value.lit ? CGold : new Color(0.933f, 0.945f, 0.961f, 0.45f);
                float th = beam.Value.lit ? 3 : 2;
                for (float d = 0; d < len && used < beamDashes.Count; d += 10)
                {
                    var dash = beamDashes[used++];
                    dash.gameObject.SetActive(true);
                    dash.color = col;
                    var p = a + dir * d;
                    dash.rectTransform.anchoredPosition = new Vector2(p.x, -p.y);
                    dash.rectTransform.sizeDelta = new Vector2(Mathf.Min(5, len - d), th);
                    dash.rectTransform.localRotation = Quaternion.Euler(0, 0, angle);
                }
            }
            for (int i = used; i < beamDashes.Count; i++) beamDashes[i].gameObject.SetActive(false);

            markA.gameObject.SetActive(mark.HasValue);
            markB.gameObject.SetActive(mark.HasValue);
            if (mark.HasValue)
            {
                float cx = ox + mark.Value * T + T / 2, cy = GY - 40;
                foreach (var (m, ang) in new[] { (markA, 45f), (markB, -45f) })
                {
                    m.rectTransform.anchoredPosition = new Vector2(cx, -cy);
                    m.rectTransform.sizeDelta = new Vector2(6, 40);
                    m.rectTransform.localRotation = Quaternion.Euler(0, 0, ang);
                }
            }

            runnerRt.anchoredPosition = new Vector2(rx, -(GY + ry + rfall));
            runnerRt.pivot = new Vector2(0.5f, 0f);
            runnerRt.localRotation = Quaternion.Euler(0, 0, -rrot * Mathf.Rad2Deg);
            runnerImg.color = CRunner.WithAlpha(ralpha);
            runnerEye.color = CEye.WithAlpha(ralpha);
            runnerEye.rectTransform.anchoredPosition = new Vector2(22, -6);

            for (int i = parts.Count - 1; i >= 0; i--)
            {
                var p = parts[i];
                p.X += p.Vx; p.Y += p.Vy; p.Vy += 0.25f; p.Life -= 0.02f;
                if (p.Life <= 0) { Destroy(p.Rt.gameObject); parts.RemoveAt(i); continue; }
                p.Rt.anchoredPosition = new Vector2(p.X - 3, -(p.Y - 3));
                p.Img.color = p.Img.color.WithAlpha(p.Life);
            }
            for (int i = floats.Count - 1; i >= 0; i--)
            {
                var f = floats[i];
                f.Y -= 0.8f; f.Life -= 0.015f;
                if (f.Life <= 0) { Destroy(f.Txt.gameObject); floats.RemoveAt(i); continue; }
                f.Txt.rectTransform.anchoredPosition = new Vector2(f.X - 40, -(f.Y - 20));
                f.Txt.color = CQi.WithAlpha(f.Life);
            }

            // The cards ride above the runner's head.
            float s = S, bw = bubble.sizeDelta.x;
            float left = Mathf.Min(Mathf.Max(rx * s, bw / 2 + 4), StageW - bw / 2 - 4);
            float topY = Mathf.Max(bubble.sizeDelta.y + 14, (GY - 126) * s);
            bubble.anchoredPosition = new Vector2(left + shakeX, -topY);
            bubbleGroup.alpha = rfall > 0 ? Mathf.Max(0.3f, ralpha) : 1f;

            // Pulse: a gold glow that grows and fades (1.1 s), on blinking card parts, a pulsing option and the Next button.
            float k2 = 0.5f - 0.5f * Mathf.Cos(Time.unscaledTime * Mathf.PI * 2f / 1.1f);
            foreach (var (rt, frame) in pulsing)
            {
                if (!frame) continue;
                frame.color = CGold.WithAlpha(0.6f * k2 + 0.15f);
                float g = 6f * k2;
                frame.rectTransform.offsetMin = new Vector2(-g, -g);
                frame.rectTransform.offsetMax = new Vector2(g, g);
            }
            if (nextBtn.gameObject.activeSelf) nextBtn.localScale = Vector3.one * (1f + 0.06f * k2);
            if (nudge.gameObject.activeSelf)
                nudge.anchoredPosition = new Vector2(StageW - 84 - 160, -6 + 5f * Mathf.Sin(Time.unscaledTime * Mathf.PI * 2f) * 0.5f - 2.5f);

        }

        void LateUpdate()
        {
            // The loadout panel grows with its cards (min 150 high).
            float h = 12 + 21 + (bigViews.Count > 0 ? bigViews.Max(v => v.Height) : 0) + 12;
            panel.sizeDelta = new Vector2(AppW - 32, Mathf.Max(150, h));
            panel.Find("Title").gameObject.SetActive(!St.Watch);
            nextBtn.anchoredPosition = new Vector2(AppW - 32 - 18 - 58, -(panel.sizeDelta.y / 2f - 29));

            // Fit after the Updates, when the canvas has its size for this frame. On the first frames after Play the
            // canvas is still settling, so the page stays hidden until its size holds for two frames in a row: one or
            // two blank frames instead of a page drawn at the wrong scale that then jumps.
            FitPage();
            var size = ((RectTransform)transform).rect.size;
            if (appGroup.alpha < 1f && size.x > 0f && size.y > 0f && size == lastRootSize) appGroup.alpha = 1f;
            lastRootSize = size;
        }

        Vector2 lastRootSize;

        /// <summary>
        /// Fits the page to the screen with one fixed height per group (see GroupPageHeight), so it keeps its size from the
        /// title through T5, whatever the loadout holds.
        /// </summary>
        void FitPage()
        {
            var root = (RectTransform)transform;
            float pageH = pageHeight;
            app.sizeDelta = new Vector2(AppW, pageH);
            float scale = Mathf.Min(root.rect.width / AppW, root.rect.height / pageH);
            if (scale <= 0f || float.IsNaN(scale) || float.IsInfinity(scale)) return;
            app.localScale = Vector3.one * scale;
            app.anchoredPosition = new Vector2(0, -(root.rect.height - pageH * scale) / 2f);
        }

        /// <summary>Hides the page until LateUpdate has fitted it to a settled canvas (see LateUpdate).</summary>
        void HideUntilFitted() => appGroup.alpha = 0f;
    }
}
