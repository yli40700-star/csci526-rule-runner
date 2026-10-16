using System;
using RuleRunner.Core;
using UnityEngine;
using UnityEngine.UI;

namespace RuleRunner.UI
{
    /// <summary>End-of-run card with stats and a cause-specific hint (design doc section 5.2).</summary>
    public class ResultPopup : MonoBehaviour
    {
        public event Action NextLevel, EditRules, BackToLevels;

        Text title, cause, hint, qiValue, stepValue, cardValue;
        Image accent;
        Button nextButton, editButton;

        public static ResultPopup Create(Transform canvas)
        {
            var root = UIKit.Fill(canvas, "ResultPopup");
            root.gameObject.AddComponent<Image>().color = new Color(0.03f, 0.04f, 0.06f, 0.6f);
            var popup = root.gameObject.AddComponent<ResultPopup>();

            var box = UIKit.Box(root, "Box", Theme.Surface, 20f, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            box.rectTransform.sizeDelta = new Vector2(540, 340);
            UIKit.DropShadow(box, 10f, 0.55f);
            var b = box.transform;

            popup.accent = UIKit.Box(b, "Accent", Theme.Accent, 4f, new Vector2(0, 1), new Vector2(1, 1), new Vector2(24, -10), new Vector2(-24, -4));
            popup.title = UIKit.Label(UIKit.TopStrip(b, "Title", 24f, 48f, 24f, 24f), "Text", "", 38, TextAnchor.MiddleCenter, Theme.Text, FontStyle.Bold);
            popup.cause = UIKit.Label(UIKit.TopStrip(b, "Cause", 74f, 26f, 24f, 24f), "Text", "", 18, TextAnchor.MiddleCenter, Theme.Dim);

            var stats = UIKit.TopStrip(b, "Stats", 112f, 64f, 40f, 40f);
            UIKit.HRow(stats, 12, null, true);
            popup.qiValue = Stat(stats, "QI LEFT", Theme.Qi);
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

        public void Show(RunResult result, LevelDef level, int cardCount, bool hasNextLevel)
        {
            bool won = result.Won;
            title.text = won ? "LEVEL CLEAR" : "RUN FAILED";
            title.color = won ? Theme.Hex(0x7de08c) : Theme.Hex(0xff8a8a);
            accent.color = won ? Theme.Accent : Theme.Danger;
            cause.text = won ? "The runner reached the flag." : Vocabulary.Describe(result.Outcome) + " at step " + result.Steps + ".";
            qiValue.text = result.QiLeft.ToString();
            stepValue.text = result.Steps.ToString();
            cardValue.text = cardCount.ToString();
            hint.text = won
                ? (result.QiLeft >= level.SolutionQiLeft ? "Perfect run - no qi wasted." : "This level can be cleared with " + level.SolutionQiLeft + " qi left.")
                : HintFor(result);
            nextButton.gameObject.SetActive(won && hasNextLevel);
            UIKit.ButtonText(editButton).text = won ? "Try again" : "Edit cards";
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
        }

        public void Hide() => gameObject.SetActive(false);

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
