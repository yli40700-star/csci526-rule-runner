using System;
using RuleRunner.Core;
using UnityEngine;
using UnityEngine.UI;

namespace RuleRunner.UI
{
    /// <summary>
    /// End-of-run card (design doc section 5.2).
    ///   Level clear: a centered card over a dimmed screen, with the stats and the Energy note.
    ///   Run failed:  a small card docked over the run controls. The map is not dimmed or covered, so the red X where
    ///                the runner died and the one-line tip in the banner above the map stay in view.
    /// </summary>
    public class ResultPopup : MonoBehaviour
    {
        public event Action NextLevel, EditRules, BackToLevels;

        static readonly Vector2 ClearSize = new Vector2(540, 340);
        /// <summary>Height of the failed-run card (GameScreen moves the run log down by the same amount).</summary>
        public const float FailedHeight = 190f;
        /// <summary>The failed-run card: as wide as the run controls it sits on.</summary>
        static readonly Vector2 FailedSize = new Vector2(352, FailedHeight);

        Text title, cause, hint, qiValue, stepValue, cardValue;
        Image accent, overlay;
        RectTransform boxRect, statsRow;
        Button nextButton, editButton;

        public static ResultPopup Create(Transform canvas)
        {
            var root = UIKit.Fill(canvas, "ResultPopup");
            var popup = root.gameObject.AddComponent<ResultPopup>();
            popup.overlay = root.gameObject.AddComponent<Image>();
            popup.overlay.color = new Color(0.03f, 0.04f, 0.06f, 0.6f);

            var box = UIKit.Box(root, "Box", Theme.Surface, 20f, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            popup.boxRect = box.rectTransform;
            popup.boxRect.sizeDelta = ClearSize;
            UIKit.DropShadow(box, 10f, 0.55f);
            var b = box.transform;

            popup.accent = UIKit.Box(b, "Accent", Theme.Accent, 4f, new Vector2(0, 1), new Vector2(1, 1), new Vector2(24, -10), new Vector2(-24, -4));
            popup.title = UIKit.Label(UIKit.TopStrip(b, "Title", 24f, 48f, 24f, 24f), "Text", "", 38, TextAnchor.MiddleCenter, Theme.Text, FontStyle.Bold);
            popup.cause = UIKit.Label(UIKit.TopStrip(b, "Cause", 74f, 26f, 24f, 24f), "Text", "", 18, TextAnchor.MiddleCenter, Theme.Dim);

            var stats = UIKit.TopStrip(b, "Stats", 112f, 64f, 40f, 40f);
            popup.statsRow = stats;
            UIKit.HRow(stats, 12, null, true);
            popup.qiValue = Stat(stats, "ENERGY LEFT", Theme.Qi);
            popup.stepValue = Stat(stats, "STEPS", Theme.Text);
            popup.cardValue = Stat(stats, "CARDS", Theme.Gold);

            popup.hint = UIKit.Label(UIKit.TopStrip(b, "Hint", 190f, 56f, 32f, 32f), "Text", "", 18, TextAnchor.MiddleCenter, Theme.Gold, FontStyle.Italic);

            var buttons = UIKit.Rect(b, "Buttons", new Vector2(0, 0), new Vector2(1, 0), new Vector2(24, 20), new Vector2(-24, 70));
            UIKit.HRow(buttons, 12, null, true);
            var levels = UIKit.Button(buttons, "Levels", "Level list", 17, Theme.Raised, () => popup.Fire(popup.BackToLevels), 12f);
            UIKit.Layout(levels, -1, 50, 1f);
            popup.editButton = UIKit.Button(buttons, "Edit", "Edit cards", 17, Theme.Raised, () => popup.Fire(popup.EditRules), 12f);
            UIKit.Layout(popup.editButton, -1, 50, 1f);
            popup.nextButton = UIKit.Button(buttons, "Next", "Next level  >", 17, Theme.Accent, () => popup.Fire(popup.NextLevel), 12f);
            UIKit.Layout(popup.nextButton, -1, 50, 1f);

            root.gameObject.SetActive(false);
            return popup;
        }

        static Text Stat(RectTransform row, string label, Color valueColor)
        {
            var box = UIKit.Box(row, label, Theme.Surface2, 12f, Vector2.zero, Vector2.one);
            UIKit.Layout(box, -1, 64, 1f);
            UIKit.Label(UIKit.TopStrip(box.transform, "Label", 8f, 16f), "Text", label, 12, TextAnchor.MiddleCenter, Theme.Dim, FontStyle.Bold);
            return UIKit.Label(UIKit.TopStrip(box.transform, "Value", 24f, 34f), "Text", "", 28, TextAnchor.MiddleCenter, valueColor, FontStyle.Bold);
        }

        void Fire(Action a)
        {
            Hide();
            a?.Invoke();
        }

        public RectTransform EditButtonRect => (RectTransform)editButton.transform;

        /// <summary>
        /// A failed run's tip is not repeated here: it is in the banner above the map, next to the X it talks about.
        /// </summary>
        public void Show(RunResult result, LevelDef level, int cardCount, bool hasNextLevel)
        {
            bool won = result.Won;
            Dock(won, GameScreen.BottomFraction(level.AllowAnd) * 720f);
            title.text = won ? "LEVEL CLEAR" : "RUN FAILED";
            title.fontSize = won ? 38 : 30;
            title.color = won ? Theme.Hex(0x7de08c) : Theme.Hex(0xff8a8a);
            accent.color = won ? Theme.Accent : Theme.Danger;
            cause.text = won ? "The runner reached the flag." : Vocabulary.Describe(result.Outcome) + " at step " + result.Steps + ".";
            qiValue.text = result.QiLeft.ToString();
            qiValue.transform.parent.gameObject.SetActive(!level.HideQi);  // qi is not shown before its tutorial level
            stepValue.text = result.Steps.ToString();
            cardValue.text = cardCount.ToString();
            hint.text = !won || level.HideQi ? ""
                : result.QiLeft >= level.SolutionQiLeft ? "Perfect run - no Energy wasted."
                : "This level can be cleared with " + level.SolutionQiLeft + " Energy left.";
            nextButton.gameObject.SetActive(won && hasNextLevel);
            // After a failure the next step is to change the cards, so that button stands out.
            UIKit.ButtonText(editButton).text = won ? "Try again" : "Edit cards";
            ((Image)editButton.targetGraphic).color = won ? Theme.Raised : Theme.Gold;
            UIKit.ButtonText(editButton).color = won ? Theme.Text : Theme.Background;
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
        }

        public void Hide() => gameObject.SetActive(false);

        /// <summary>
        /// Level clear: centered over a dimmed screen. Run failed: no dimming, and the card sits on the run controls
        /// (bottom right, under the map), whose top edge is <paramref name="bottomPx"/> - 8 above the screen bottom.
        /// </summary>
        void Dock(bool won, float bottomPx)
        {
            // Without the dimmed image nothing blocks the pointer, so the header (Levels, ?, Hint) stays usable.
            overlay.enabled = won;
            statsRow.gameObject.SetActive(won);
            hint.gameObject.SetActive(won);
            if (won)
            {
                boxRect.anchorMin = boxRect.anchorMax = boxRect.pivot = new Vector2(0.5f, 0.5f);
                boxRect.anchoredPosition = Vector2.zero;
                boxRect.sizeDelta = ClearSize;
            }
            else
            {
                boxRect.anchorMin = boxRect.anchorMax = new Vector2(1f, 0f);
                boxRect.pivot = new Vector2(1f, 1f);
                boxRect.anchoredPosition = new Vector2(-12f, bottomPx - 8f);
                boxRect.sizeDelta = FailedSize;
            }
        }

        public static string HintFor(RunResult r)
        {
            if (r.RanOutOfQi) return "Did a card fire more often than needed?";
            switch (r.Outcome)
            {
                case Outcome.Pit: return "Which card should have fired before the pit?";
                case Outcome.Spike: return "Was the landing spot safe?";
                case Outcome.Trap: return "Was the trap up when you stepped on it?";
                case Outcome.Timeout: return "Is a card making the runner wait or bump into a wall forever?";
                default: return "";
            }
        }
    }
}
