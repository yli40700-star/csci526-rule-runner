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
            }
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
