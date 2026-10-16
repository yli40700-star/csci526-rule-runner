using System;
using System.Collections.Generic;
using RuleRunner.Core;
using RuleRunner.View;
using UnityEngine;
using UnityEngine.UI;

namespace RuleRunner.UI
{
    /// <summary>
    /// Edit-and-run screen (1280x720 reference).
    ///   top:     header (back, level chip, title, hint)
    ///   middle:  the map, drawn by the camera, with a tile legend underneath
    ///   bottom:  LoadoutArea on the left (filled by the Cards module), run controls and log on the right
    /// The App layer drives the run.
    /// </summary>
    public class GameScreen : MonoBehaviour
    {
        public event Action Back, Run, Reset;
        public event Action<float> SpeedChanged;

        const float HeaderPx = 64f;
        const float BottomPx = 300f;
        public const float HeaderFraction = HeaderPx / 720f;
        public const float BottomFraction = BottomPx / 720f;
        const int MaxLogLines = 250;

        public LevelDef Level { get; private set; }
        public RectTransform CanvasRect { get; private set; }
        /// <summary>Empty area at the bottom-left where the loadout tray (Cards module) is built.</summary>
        public RectTransform LoadoutArea { get; private set; }
        public float Speed => speed;

        Text levelChipText, titleText, hintText, floatText;
        RectTransform logContent, floatBubble;
        ScrollRect logScroll;
        Button runButton, resetButton;
        readonly List<Button> speedButtons = new List<Button>();
        readonly Queue<GameObject> logLines = new Queue<GameObject>();
        float speed = 1f;
        float floatTimer;

        public static GameScreen Create(RectTransform canvas)
        {
            var root = UIKit.Fill(canvas, "GameScreen");
            var screen = root.gameObject.AddComponent<GameScreen>();
            screen.CanvasRect = canvas;
            screen.BuildHeader(root);
            screen.BuildLegend(root);
            screen.LoadoutArea = UIKit.Rect(root, "LoadoutArea", new Vector2(0, 0), new Vector2(1, 0),
                new Vector2(12, 12), new Vector2(-376, BottomPx - 8));
            screen.BuildControls(root);
            screen.BuildFloatingBubble(root);
            return screen;
        }

        // ---------- Layout ----------

        void BuildHeader(Transform root)
        {
            var header = UIKit.Panel(root, "Header", Theme.Surface, new Vector2(0, 1), Vector2.one, new Vector2(0, -HeaderPx), Vector2.zero);
            UIKit.Panel(header.transform, "Edge", Theme.Line, Vector2.zero, new Vector2(1, 0), Vector2.zero, new Vector2(0, 1));

            var back = UIKit.Button(header.transform, "Back", "<  Levels", 17, Theme.Raised, () => Back?.Invoke(), 10f);
            Place(back.transform, new Vector2(0, 0.5f), new Vector2(14, 0), new Vector2(112, 40), new Vector2(0, 0.5f));

            var chip = UIKit.Box(header.transform, "LevelChip", Theme.Gold, 12f, new Vector2(0, 0.5f), new Vector2(0, 0.5f));
            Place(chip.transform, new Vector2(0, 0.5f), new Vector2(142, 0), new Vector2(64, 40), new Vector2(0, 0.5f));
            levelChipText = UIKit.Label(chip.transform, "Text", "1-1", 20, TextAnchor.MiddleCenter, Theme.Background, FontStyle.Bold);

            titleText = UIKit.Label(UIKit.Rect(header.transform, "Title", new Vector2(0, 0.5f), new Vector2(1, 1), new Vector2(222, -2), new Vector2(-20, -6)),
                "Text", "", 24, TextAnchor.LowerLeft, Theme.Text, FontStyle.Bold);
            hintText = UIKit.Label(UIKit.Rect(header.transform, "Hint", new Vector2(0, 0), new Vector2(1, 0.5f), new Vector2(222, 2), new Vector2(-20, 0)),
                "Text", "", 15, TextAnchor.UpperLeft, Theme.Dim, FontStyle.Italic);
        }

        void BuildLegend(Transform root)
        {
            var legend = UIKit.Rect(root, "Legend", new Vector2(0, 0), new Vector2(1, 0), new Vector2(16, BottomPx + 2), new Vector2(-16, BottomPx + 28));
            UIKit.HRow(legend, 6, null, false, TextAnchor.MiddleCenter);
            var items = new (string, Color)[]
            {
                ("Ground", Palette.Ground), ("Pit", Palette.PitHole), ("Spike", Palette.Spike),
                ("Low wall (jump over)", Palette.LowWall), ("High wall / edge", Palette.HighWall),
                ("Trap (up 2, down 2)", Palette.TrapUp), ("Qi orb +2", Palette.Orb), ("Finish", Palette.Finish),
            };
            foreach (var (name, color) in items)
            {
                var swatch = UIKit.Rect(legend, "Swatch", Vector2.zero, Vector2.one);
                UIKit.Round(swatch.gameObject.AddComponent<Image>(), 3f).color = color;
                UIKit.Layout(swatch, 12, 12);
                var label = UIKit.Label(legend, "Label", name, 14, TextAnchor.MiddleLeft, Theme.Dim);
                label.horizontalOverflow = HorizontalWrapMode.Overflow;
                UIKit.Layout(label, label.preferredWidth + 14, 20);
            }
        }

        void BuildControls(Transform root)
        {
            var controls = UIKit.Box(root, "Controls", Theme.Surface, 16f, new Vector2(1, 0), new Vector2(1, 0),
                new Vector2(-364, BottomPx - 8 - 112), new Vector2(-12, BottomPx - 8));
            UIKit.DropShadow(controls, 4f, 0.35f);

            runButton = UIKit.Button(controls.transform, "Run", "RUN", 24, Theme.Accent, () => Run?.Invoke(), 12f);
            var runRt = (RectTransform)runButton.transform;
            runRt.anchorMin = new Vector2(0, 1);
            runRt.anchorMax = new Vector2(1, 1);
            runRt.offsetMin = new Vector2(12, -64);
            runRt.offsetMax = new Vector2(-12, -12);
            UIKit.DropShadow((Image)runButton.targetGraphic, 3f, 0.4f);

            var row = UIKit.Rect(controls.transform, "Row", new Vector2(0, 0), new Vector2(1, 0), new Vector2(12, 10), new Vector2(-12, 44));
            UIKit.HRow(row, 6, null, true, TextAnchor.MiddleLeft);
            resetButton = UIKit.Button(row, "Reset", "Reset", 16, Theme.Raised, () => Reset?.Invoke(), 9f);
            UIKit.Layout(resetButton, -1, 34, 1.4f);
            var gap = UIKit.Rect(row, "Gap", Vector2.zero, Vector2.one);
            UIKit.Layout(gap, 8, 34);
            foreach (var s in new[] { 1f, 2f, 4f })
            {
                float value = s;
                var b = UIKit.Button(row, "Speed" + s, s + "x", 15, Theme.Raised, () => SetSpeed(value), 9f);
                UIKit.Layout(b, -1, 34, 1f);
                speedButtons.Add(b);
            }
            SetSpeed(1f);

            var log = UIKit.Box(root, "Log", Theme.Surface, 16f, new Vector2(1, 0), new Vector2(1, 0),
                new Vector2(-364, 12), new Vector2(-12, BottomPx - 8 - 112 - 10));
            UIKit.DropShadow(log, 4f, 0.35f);
            UIKit.Label(UIKit.TopStrip(log.transform, "Title", 8f, 20f, 14f, 14f), "Text", "RUN LOG", 13, TextAnchor.MiddleLeft, Theme.Dim, FontStyle.Bold);
            var logArea = UIKit.Rect(log.transform, "Area", Vector2.zero, Vector2.one, new Vector2(8, 8), new Vector2(-8, -30));
            logContent = UIKit.ScrollView(logArea, "Scroll", out logScroll);
            UIKit.VColumn(logContent, 3, new RectOffset(6, 6, 2, 4));
        }

        void BuildFloatingBubble(Transform root)
        {
            var bubble = UIKit.Box(root, "FloatingBubble", Theme.Background.WithAlpha(0.92f), 12f, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            floatBubble = bubble.rectTransform;
            floatBubble.sizeDelta = new Vector2(170, 32);
            bubble.raycastTarget = false;
            bubble.gameObject.AddComponent<Outline>().effectColor = Theme.Gold.WithAlpha(0.8f);
            floatText = UIKit.Label(floatBubble, "Text", "", 16, TextAnchor.MiddleCenter, Theme.Gold, FontStyle.Bold);
            floatText.horizontalOverflow = HorizontalWrapMode.Overflow;
            floatBubble.gameObject.SetActive(false);
        }

        static void Place(Transform t, Vector2 anchor, Vector2 pos, Vector2 size, Vector2 pivot)
        {
            var rt = (RectTransform)t;
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;
        }

        // ---------- Level setup ----------

        public void Setup(LevelDef level)
        {
            Level = level;
            levelChipText.text = level.Id;
            titleText.text = level.Title;
            hintText.text = level.Hint;
            SetRunning(false);
            ClearLog();
            Log("Equip cards, then press RUN.", Theme.Dim);
            HideFloat();
        }

        /// <summary>RUN is disabled while a run is playing.</summary>
        public void SetRunning(bool running) => runButton.interactable = !running;

        void SetSpeed(float value)
        {
            speed = value;
            float[] values = { 1f, 2f, 4f };
            for (int i = 0; i < speedButtons.Count; i++)
                ((Image)speedButtons[i].targetGraphic).color = Mathf.Approximately(values[i], value) ? Theme.Qi.WithAlpha(0.85f) : Theme.Raised;
            SpeedChanged?.Invoke(speed);
        }

        // ---------- Run feedback ----------

        public void Log(string line, Color? color = null)
        {
            var text = UIKit.Label(logContent, "Line", line, 14, TextAnchor.UpperLeft, color ?? Theme.Text);
            var fitter = text.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            logLines.Enqueue(text.gameObject);
            while (logLines.Count > MaxLogLines) Destroy(logLines.Dequeue());
            Canvas.ForceUpdateCanvases();
            logScroll.verticalNormalizedPosition = 0f;
        }

        public void ClearLog()
        {
            while (logLines.Count > 0) Destroy(logLines.Dequeue());
        }

        /// <summary>Shows a short bubble (e.g. "Rule 2: Jump -1") above the runner for a moment.</summary>
        public void ShowFloat(Vector2 canvasPos, string text, float seconds)
        {
            floatBubble.anchoredPosition = canvasPos + new Vector2(0f, 18f);
            floatText.text = text;
            floatBubble.sizeDelta = new Vector2(floatText.preferredWidth + 28f, 32f);
            floatBubble.gameObject.SetActive(true);
            floatTimer = seconds;
        }

        public void HideFloat()
        {
            floatBubble.gameObject.SetActive(false);
            floatTimer = 0f;
        }

        void Update()
        {
            if (floatTimer > 0f)
            {
                floatTimer -= Time.deltaTime;
                if (floatTimer <= 0f) HideFloat();
            }
        }

        public void Show() => gameObject.SetActive(true);
        public void Hide() => gameObject.SetActive(false);
    }
}
