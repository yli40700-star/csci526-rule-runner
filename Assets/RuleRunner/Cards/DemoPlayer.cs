using System.Collections;
using System.Collections.Generic;
using RuleRunner.Core;
using RuleRunner.UI;
using RuleRunner.View;
using UnityEngine;
using UnityEngine.UI;

namespace RuleRunner.Cards
{
    /// <summary>
    /// Tiny looping animation for the intro card: runs a DemoSpec through the real Simulator and plays
    /// the frames with UI shapes (walk, jump arcs, turn, wait, traps, orbs), then restarts.
    /// </summary>
    public class DemoPlayer : MonoBehaviour
    {
        const float StepSeconds = 0.5f;

        DemoSpec spec;
        RunResult result;
        RectTransform board;
        RectTransform runner, eye;
        Text qiText, actionText;
        readonly List<Image> trapSpikes = new List<Image>();
        readonly Dictionary<int, Image> orbs = new Dictionary<int, Image>();
        float cell, baseY, x0;
        bool built;

        public static DemoPlayer Create(RectTransform area, DemoSpec spec)
        {
            var player = area.gameObject.AddComponent<DemoPlayer>();
            player.spec = spec;
            player.result = Simulator.Run(spec.Map, spec.Start, spec.Dir, spec.Qi, spec.Rules, true, spec.Steps);
            player.Build(area);
            player.built = true;
            player.StartCoroutine(player.Loop());
            return player;
        }

        float X(int i) => x0 + i * cell;

        void Build(RectTransform area)
        {
            board = UIKit.Fill(area, "Board");
            var size = area.rect.size;
            if (size.x <= 0) size = new Vector2(300, 74);
            int n = spec.Map.Length;
            cell = Mathf.Min(30f, (size.x - 20f) / (n + 2));
            x0 = -(n - 1) * cell / 2f;
            baseY = -size.y / 2f + 20f;

            Block(-1, Palette.HighWall, cell * 1.6f);
            Block(n, Palette.HighWall, cell * 1.6f);
            for (int i = 0; i < n; i++) DrawCell(i, spec.Map[i]);

            runner = UIKit.Shape(board, "Runner", UISprites.Rounded, Palette.Runner, Vector2.zero, new Vector2(cell * 0.62f, cell * 0.62f)).rectTransform;
            UIKit.Round(runner.GetComponent<Image>(), 4f);
            eye = UIKit.Shape(runner, "Eye", UISprites.Rounded, Palette.RunnerEye, Vector2.zero, new Vector2(cell * 0.12f, cell * 0.18f)).rectTransform;

            qiText = UIKit.Label(UIKit.Rect(board, "Qi", new Vector2(1, 1), new Vector2(1, 1), new Vector2(-70, -18), new Vector2(-4, 0)),
                "Text", "", 12, TextAnchor.MiddleRight, Theme.Qi, FontStyle.Bold);
            actionText = UIKit.Label(UIKit.Centered(board, "Action", Vector2.zero, new Vector2(120, 18)),
                "Text", "", 12, TextAnchor.MiddleCenter, Theme.Gold, FontStyle.Bold);
        }

        void Block(int i, Color color, float height) =>
            UIKit.Shape(board, "Wall", UISprites.Rounded, color, new Vector2(X(i), baseY + height / 2f - cell * 0.25f), new Vector2(cell * 0.92f, height));

        void DrawCell(int i, char c)
        {
            var groundColor = c == Vocabulary.Pit ? Palette.PitHole : (i % 2 == 0 ? Palette.Ground : Palette.GroundDark);
            UIKit.Shape(board, "Cell", UISprites.Rounded, groundColor, new Vector2(X(i), baseY - cell * 0.25f), new Vector2(cell * 0.94f, cell * 0.5f));
            float top = baseY;
            switch (c)
            {
                case Vocabulary.Spike:
                    UIKit.Shape(board, "Spike", UISprites.Triangle, Palette.Spike, new Vector2(X(i), top + cell * 0.25f), new Vector2(cell * 0.7f, cell * 0.5f));
                    break;
                case Vocabulary.LowWall:
                    UIKit.Shape(board, "LowWall", UISprites.Rounded, Palette.LowWall, new Vector2(X(i), top + cell * 0.3f), new Vector2(cell * 0.8f, cell * 0.6f));
                    break;
                case Vocabulary.HighWall:
                    Block(i, Palette.HighWall, cell * 1.6f);
                    break;
                case Vocabulary.Trap:
                    trapSpikes.Add(UIKit.Shape(board, "Trap", UISprites.Triangle, Palette.TrapUp, new Vector2(X(i), top + cell * 0.22f), new Vector2(cell * 0.7f, cell * 0.44f)));
                    break;
                case Vocabulary.Orb:
                    orbs[i] = UIKit.Shape(board, "Orb", UISprites.Circle, Palette.Orb, new Vector2(X(i), top + cell * 0.3f), new Vector2(cell * 0.4f, cell * 0.4f));
                    break;
                case Vocabulary.Finish:
                    UIKit.Shape(board, "Flag", UISprites.Rounded, Palette.Finish, new Vector2(X(i), top + cell * 0.55f), new Vector2(cell * 0.4f, cell * 0.4f));
                    break;
            }
        }

        void OnEnable()
        {
            if (built) StartCoroutine(Loop());
        }

        void Place(float x, float lift, int dir)
        {
            runner.anchoredPosition = new Vector2(x, baseY + cell * 0.31f + lift);
            eye.anchoredPosition = new Vector2(cell * 0.14f * dir, cell * 0.06f);
        }

        void SetTraps(int t)
        {
            bool up = Vocabulary.TrapUp(t);
            foreach (var s in trapSpikes) s.color = up ? Palette.TrapUp : Palette.TrapDown.WithAlpha(0.6f);
        }

        IEnumerator Loop()
        {
            while (true)
            {
                foreach (var o in orbs.Values) o.enabled = true;
                runner.GetComponent<Image>().color = Palette.Runner;
                Place(X(spec.Start), 0f, spec.Dir);
                SetTraps(0);
                qiText.text = "qi " + spec.Qi;
                actionText.text = "";
                yield return Wait(0.6f);

                foreach (var f in result.Frames)
                {
                    SetTraps(f.T);
                    if (f.FiredRule >= 0)
                    {
                        var a = spec.Rules[f.FiredRule].Action;
                        int cost = Vocabulary.Cost(a);
                        actionText.text = Vocabulary.Display(a) + (cost > 0 ? " -" + cost : "");
                    }
                    else actionText.text = f.HoldingRule >= 0 ? "waiting..." : "";
                    actionText.rectTransform.anchoredPosition = new Vector2(X(f.XBefore), baseY + cell * 1.5f);

                    float height = f.Action == ActionType.Jump ? cell * 1.0f : f.Action == ActionType.BigJump ? cell * 1.4f : 0f;
                    for (float t = 0; t < 1f; t += Time.unscaledDeltaTime / StepSeconds)
                    {
                        float k = Mathf.Clamp01(t);
                        float x = f.Blocked ? X(f.XBefore) : Mathf.Lerp(X(f.XBefore), X(f.X), k);
                        int dir = f.Action == ActionType.Turn ? (k < 0.5f ? f.DirBefore : f.Dir) : f.Dir;
                        Place(x, height * 4f * k * (1f - k), dir);
                        yield return null;
                    }
                    Place(X(f.X), 0f, f.Dir);
                    qiText.text = "qi " + f.Qi;
                    if (f.CollectedOrb && orbs.TryGetValue(f.X, out var orb))
                    {
                        orb.enabled = false;
                        actionText.text = "+2 qi";
                    }
                    if (f.Outcome != Outcome.Running && f.Outcome != Outcome.Win)
                        runner.GetComponent<Image>().color = Palette.Dead;
                }
                yield return Wait(1.1f);
            }
        }

        static IEnumerator Wait(float seconds)
        {
            for (float t = 0; t < seconds; t += Time.unscaledDeltaTime) yield return null;
        }
    }
}
