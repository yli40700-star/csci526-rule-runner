using RuleRunner.Core;
using RuleRunner.View;
using RuleRunner.UI;
using UnityEngine;
using UnityEngine.UI;

namespace RuleRunner.Cards
{
    /// <summary>
    /// Tiny diagrams drawn with basic shapes: the runner plus the next three cells.
    /// Conditions highlight the cell they look at; actions draw the path the runner takes.
    /// </summary>
    public static class Icons
    {
        const float Cell = 20f;
        const float Gap = 3f;
        public static readonly Vector2 Size = new Vector2(4 * Cell + 3 * Gap, 44f);
        const float GroundTop = -6f;   // y of the ground line inside the icon (center origin)

        static float X(int i) => -Size.x / 2f + i * (Cell + Gap) + Cell / 2f;

        /// <summary>Builds (or rebuilds) a condition diagram inside the container.</summary>
        public static void DrawCondition(RectTransform container, Condition c, bool negate)
        {
            UIKit.DestroyChildren(container);
            char c1 = '?', c2 = '?';
            int tested = 1;
            switch (c)
            {
                case Condition.Ground: c1 = 'g'; break;
                case Condition.Pit: c1 = 'p'; break;
                case Condition.Spike: c1 = 's'; break;
                case Condition.Wall: c1 = 'w'; break;
                case Condition.Ground2: c2 = 'g'; tested = 2; break;
                case Condition.Trap: c1 = 'T'; break;
            }
            DrawCell(container, 0, 'g', false);
            DrawCell(container, 1, c1, tested != 1);
            DrawCell(container, 2, c2, tested != 2);
            DrawCell(container, 3, '?', true);
            Runner(container, 0);

            // Marker under the cell the condition looks at; NOT turns it into a red cross bar.
            var color = negate ? Theme.Not : Theme.Gold;
            UIKit.Shape(container, "Marker", UISprites.Rounded, color, new Vector2(X(tested), GroundTop - 13f), new Vector2(Cell, 4f));
            UIKit.Shape(container, "Arrow", UISprites.Triangle, color, new Vector2(X(tested), GroundTop + 22f), new Vector2(8f, 7f))
                .rectTransform.localRotation = Quaternion.Euler(0, 0, 180f);
            if (negate)
            {
                var slash = UIKit.Shape(container, "Not", UISprites.Rounded, Theme.Not, new Vector2(X(tested), GroundTop + 6f), new Vector2(3.5f, Cell * 1.3f));
                slash.rectTransform.localRotation = Quaternion.Euler(0, 0, -40f);
            }
        }

        /// <summary>Builds (or rebuilds) an action diagram inside the container.</summary>
        public static void DrawAction(RectTransform container, ActionType a)
        {
            UIKit.DestroyChildren(container);
            for (int i = 0; i < 4; i++) DrawCell(container, i, 'g', i > 0);
            Runner(container, 0);
            var dot = new Color(1f, 1f, 1f, 0.9f);
            float y0 = GroundTop + Cell * 0.45f;

            switch (a)
            {
                case ActionType.Walk:
                    Dots(container, t => new Vector2(Mathf.Lerp(X(0) + 9f, X(1) - 2f, t), y0), 3, dot);
                    Arrow(container, new Vector2(X(1) + 4f, y0), -90f, dot);
                    break;
                case ActionType.Jump:
                    Arc(container, X(0) + 6f, X(2), y0, Cell * 1.05f, 6, dot);
                    Landing(container, 2);
                    break;
                case ActionType.BigJump:
                    Arc(container, X(0) + 6f, X(3), y0, Cell * 1.35f, 8, dot);
                    Landing(container, 3);
                    break;
                case ActionType.Turn:
                {
                    var c = new Vector2(X(0) + 12f, y0 + 4f);
                    Dots(container, t =>
                    {
                        float ang = Mathf.Lerp(100f, -100f, t) * Mathf.Deg2Rad;
                        return c + new Vector2(Mathf.Cos(ang) * 14f, Mathf.Sin(ang) * 11f);
                    }, 6, dot);
                    Arrow(container, c + new Vector2(-6f, -11f), 90f, dot);
                    break;
                }
                case ActionType.Wait:
                    UIKit.Shape(container, "Pause1", UISprites.Rounded, dot, new Vector2(X(1) - 3f, y0 + 2f), new Vector2(4.5f, 15f));
                    UIKit.Shape(container, "Pause2", UISprites.Rounded, dot, new Vector2(X(1) + 5f, y0 + 2f), new Vector2(4.5f, 15f));
                    break;
            }
        }

        static void DrawCell(RectTransform parent, int i, char kind, bool dim)
        {
            float alpha = dim ? 0.35f : 1f;
            var groundColor = (kind == 'p' ? Palette.PitHole : Palette.Ground).WithAlpha(kind == '?' ? 0.18f : alpha);
            UIKit.Shape(parent, "Cell" + i, UISprites.Rounded, groundColor, new Vector2(X(i), GroundTop - 4f), new Vector2(Cell, 8f));
            switch (kind)
            {
                case 's':
                    for (int k = 0; k < 2; k++)
                        UIKit.Shape(parent, "Spike", UISprites.Triangle, Palette.Spike.WithAlpha(alpha),
                            new Vector2(X(i) - 4.5f + 9f * k, GroundTop + 5f), new Vector2(9f, 10f));
                    break;
                case 'w':
                    UIKit.Shape(parent, "Wall", UISprites.Rounded, Palette.LowWall.WithAlpha(alpha), new Vector2(X(i), GroundTop + 6f), new Vector2(Cell * 0.8f, 12f));
                    break;
                case 'T':
                    UIKit.Shape(parent, "Trap", UISprites.Triangle, Palette.TrapUp.WithAlpha(alpha), new Vector2(X(i), GroundTop + 5f), new Vector2(Cell * 0.7f, 10f));
                    break;
                case 'p':
                    UIKit.Shape(parent, "PitEdge", UISprites.Rounded, Theme.Line.WithAlpha(alpha), new Vector2(X(i), GroundTop - 1f), new Vector2(Cell, 2f));
                    break;
            }
        }

        static void Runner(RectTransform parent, int i)
        {
            UIKit.Shape(parent, "Runner", UISprites.Rounded, Palette.Runner, new Vector2(X(i), GroundTop + 7f), new Vector2(13f, 13f));
            UIKit.Shape(parent, "Eye", UISprites.Rounded, Palette.RunnerEye, new Vector2(X(i) + 3f, GroundTop + 9f), new Vector2(3f, 4f));
        }

        static void Landing(RectTransform parent, int i) =>
            UIKit.Shape(parent, "Landing", UISprites.Rounded, Theme.Gold, new Vector2(X(i), GroundTop - 13f), new Vector2(Cell, 4f));

        static void Arc(RectTransform parent, float x0, float x1, float y, float height, int count, Color color) =>
            Dots(parent, t => new Vector2(Mathf.Lerp(x0, x1, t), y + height * 4f * t * (1f - t)), count, color);

        static void Dots(RectTransform parent, System.Func<float, Vector2> path, int count, Color color)
        {
            for (int k = 0; k < count; k++)
            {
                float t = count == 1 ? 0.5f : (float)k / (count - 1);
                UIKit.Shape(parent, "Dot", UISprites.Circle, color, path(t), new Vector2(4.5f, 4.5f));
            }
        }

        static void Arrow(RectTransform parent, Vector2 pos, float rotation, Color color) =>
            UIKit.Shape(parent, "Arrow", UISprites.Triangle, color, pos, new Vector2(9f, 8f))
                .rectTransform.localRotation = Quaternion.Euler(0, 0, rotation);
    }
}
