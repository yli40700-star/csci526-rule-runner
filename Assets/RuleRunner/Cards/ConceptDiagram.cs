using RuleRunner.Core;
using RuleRunner.UI;
using UnityEngine;

namespace RuleRunner.Cards
{
    /// <summary>Draws the "new in this level" diagram on the intro card, using the same mini diagrams as the cards.</summary>
    public static class ConceptDiagram
    {
        public static void Build(RectTransform area, string concept)
        {
            UIKit.DestroyChildren(area);
            switch (concept)
            {
                case "basics": Pair(area, 0, Condition.Pit, false, ActionType.Jump, "IF Pit ahead", "THEN Jump"); break;
                case "not": Pair(area, 0, Condition.Ground, true, ActionType.Jump, "IF NOT Ground ahead", "THEN Jump"); break;
                case "lowwall": Pair(area, 0, Condition.Wall, false, ActionType.Jump, "A low wall", "can be jumped"); break;
                case "order":
                case "combo":
                    Pair(area, -105, Condition.Pit, false, ActionType.BigJump, "1  IF Pit", "THEN Big Jump");
                    Pair(area, 105, Condition.Ground, true, ActionType.Jump, "2  IF NOT Ground", "THEN Jump");
                    break;
                case "trap": Pair(area, 0, Condition.Trap, false, ActionType.Wait, "IF Trap up ahead", "THEN Wait (until it drops)"); break;
                case "turn": Pair(area, 0, Condition.Wall, false, ActionType.Turn, "IF Wall ahead", "THEN Turn"); break;
                case "walkblock": Pair(area, 0, Condition.Ground, false, ActionType.Walk, "IF Ground ahead", "THEN Walk (free, stops the rest)"); break;
                case "ground2": Pair(area, 0, Condition.Ground2, true, ActionType.BigJump, "IF NOT Ground 2 ahead", "THEN Big Jump"); break;
                case "and": And(area); break;
            }
        }

        /// <summary>Two condition diagrams joined by AND, then the action: the card fires only when both hold.</summary>
        static void And(RectTransform area)
        {
            float[] xs = { -215f, -70f, 125f };
            var c1 = UIKit.Centered(area, "Cond1", new Vector2(xs[0], 14), Icons.Size);
            Icons.DrawCondition(c1, Condition.Ground, true);
            var c2 = UIKit.Centered(area, "Cond2", new Vector2(xs[1], 14), Icons.Size);
            Icons.DrawCondition(c2, Condition.Ground2, false);
            var act = UIKit.Centered(area, "Act", new Vector2(xs[2], 14), Icons.Size);
            Icons.DrawAction(act, ActionType.Jump);

            UIKit.Label(UIKit.Centered(area, "And", new Vector2((xs[0] + xs[1]) / 2f, 16), new Vector2(50, 20)), "Text", "AND", 14,
                TextAnchor.MiddleCenter, Theme.Qi, FontStyle.Bold);
            UIKit.Shape(area, "Arrow", UISprites.Triangle, Theme.Dim, new Vector2((xs[1] + xs[2]) / 2f, 16), new Vector2(10, 9))
                .rectTransform.localRotation = Quaternion.Euler(0, 0, -90f);

            UIKit.Label(UIKit.Centered(area, "Label1", new Vector2(xs[0], -24), new Vector2(150, 20)), "Text", "IF NOT Ground ahead", 13,
                TextAnchor.MiddleCenter, Theme.Not, FontStyle.Bold);
            UIKit.Label(UIKit.Centered(area, "Label2", new Vector2(xs[1], -24), new Vector2(150, 20)), "Text", "AND Ground 2 ahead", 13,
                TextAnchor.MiddleCenter, Theme.Text, FontStyle.Bold);
            UIKit.Label(UIKit.Centered(area, "Label3", new Vector2(xs[2], -24), new Vector2(150, 20)), "Text", "THEN Jump", 13,
                TextAnchor.MiddleCenter, Theme.ActionColor(ActionType.Jump), FontStyle.Bold);
            UIKit.Label(UIKit.Centered(area, "Note", new Vector2(0, -56), new Vector2(640, 20)), "Text",
                "Fires only when BOTH are true: something is in the way, and the landing is safe. Costs 1 extra qi.", 13,
                TextAnchor.MiddleCenter, Theme.Dim);
        }

        /// <summary>Condition diagram, arrow, action diagram, with a caption under each.</summary>
        static void Pair(RectTransform area, float x, Condition c, bool negate, ActionType a, string condLabel, string actLabel)
        {
            bool wide = Mathf.Approximately(x, 0f);
            float gap = wide ? 80f : 52f;
            var cond = UIKit.Centered(area, "Cond", new Vector2(x - gap, 8), Icons.Size);
            Icons.DrawCondition(cond, c, negate);
            var act = UIKit.Centered(area, "Act", new Vector2(x + gap, 8), Icons.Size);
            Icons.DrawAction(act, a);
            UIKit.Shape(area, "Arrow", UISprites.Triangle, Theme.Dim, new Vector2(x, 10), new Vector2(10, 9))
                .rectTransform.localRotation = Quaternion.Euler(0, 0, -90f);
            float w = wide ? 180f : 120f;
            UIKit.Label(UIKit.Centered(area, "CondLabel", new Vector2(x - gap, -30), new Vector2(w, 20)), "Text", condLabel, 13,
                TextAnchor.MiddleCenter, negate ? Theme.Not : Theme.Text, FontStyle.Bold);
            UIKit.Label(UIKit.Centered(area, "ActLabel", new Vector2(x + gap, -30), new Vector2(w, 20)), "Text", actLabel, 13,
                TextAnchor.MiddleCenter, Theme.ActionColor(a), FontStyle.Bold);
        }
    }
}
