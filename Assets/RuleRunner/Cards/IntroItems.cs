using RuleRunner.Core;
using RuleRunner.UI;
using UnityEngine;
using UnityEngine.UI;

namespace RuleRunner.Cards
{
    /// <summary>Fills the intro card: one panel per new card / tile / rule with a looping demo, or a static diagram.</summary>
    public static class IntroItems
    {
        public static void Build(RectTransform area, LevelGuide guide)
        {
            UIKit.DestroyChildren(area);
            var items = guide.NewItems;
            if (items.Count == 0)
            {
                var bg = UIKit.FillBox(area, "Bg", Theme.Surface2, 14f);
                var inner = UIKit.Fill(bg.transform, "Diagram");
                ConceptDiagram.Build(inner, guide.Concept);
                return;
            }

            int k = Mathf.Min(items.Count, 2);
            const float gap = 12f;
            float w = (area.rect.width > 0 ? area.rect.width : 672f);
            float panelW = (w - gap * (k - 1)) / k;
            for (int i = 0; i < k; i++)
            {
                var item = items[i];
                float left = i * (panelW + gap);
                var panel = UIKit.Box(area, "Item " + i, Theme.Surface2, 14f, new Vector2(0, 0), new Vector2(0, 1),
                    new Vector2(left, 0), new Vector2(left + panelW, 0));
                var p = panel.transform;
                UIKit.Label(UIKit.TopStrip(p, "Title", 8f, 20f, 12f, 12f), "Text", item.Title, 14, TextAnchor.MiddleCenter, Theme.Gold, FontStyle.Bold);

                var demoArea = UIKit.TopStrip(p, "Demo", 28f, 86f, 10f, 10f);
                demoArea.gameObject.AddComponent<RectMask2D>();
                DemoPlayer.Create(demoArea, item);

                // One card per line, numbered when there are several (order matters).
                var lines = item.Rules.ConvertAll(r => r.ToDisplayString());
                if (lines.Count > 1) for (int j = 0; j < lines.Count; j++) lines[j] = (j + 1) + "  " + lines[j];
                var cardsText = lines.Count == 0 ? "(no cards)" : string.Join("\n", lines);
                UIKit.Label(UIKit.TopStrip(p, "Cards", 114f, 32f, 10f, 10f), "Text", cardsText, 12, TextAnchor.MiddleCenter, Theme.Text);
                var line = UIKit.Label(UIKit.Rect(p, "Line", Vector2.zero, new Vector2(1, 0), new Vector2(14, 6), new Vector2(-14, 46)),
                    "Text", item.Line, 13, TextAnchor.MiddleCenter, Theme.Dim);
                line.verticalOverflow = VerticalWrapMode.Truncate;
            }
        }
    }
}
