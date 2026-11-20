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
    /// the frames with UI shapes (walk, jump arcs, turn, wait, traps, orbs, cracked ground, drops to a
    /// lower floor), then restarts.
    /// </summary>
    public class DemoPlayer : MonoBehaviour
    {
        const float StepSeconds = 0.5f;

        DemoSpec spec;
        RunResult result;
        RectTransform board;
        RectTransform runner, eye;
        Text qiText, actionText;
        bool showEnergy = true;
        readonly List<Image> trapSpikes = new List<Image>();
        readonly Dictionary<int, Image> orbs = new Dictionary<int, Image>();
        readonly Dictionary<int, Image> cracks = new Dictionary<int, Image>();
        float cell, baseY, upperY, x0;
        bool twoFloors, built;

        /// <param name="showEnergy">False in the tutorial levels before Energy is taught: no Energy counter, no costs.</param>
        public static DemoPlayer Create(RectTransform area, DemoSpec spec, bool showEnergy = true)
        {
            var player = area.gameObject.AddComponent<DemoPlayer>();
            player.spec = spec;
            player.showEnergy = showEnergy;
            player.result = Simulator.Run(spec.Map, spec.Start, spec.Dir, spec.Qi, spec.Rules, true, spec.Steps, spec.Lower);
            player.Build(area);
            player.qiText.gameObject.SetActive(showEnergy);
            player.built = true;
            player.StartCoroutine(player.Loop());
            return player;
        }

        float X(int i) => x0 + i * cell;

        /// <summary>Ground line of a floor: floor 0 is the spec's Map (raised on a two-floor demo), floor 1 the Lower.</summary>
        float FloorY(int floor) => twoFloors && floor == 0 ? upperY : baseY;

        static int Key(int floor, int i) => floor * 1000 + i;

        void Build(RectTransform area)
        {
            board = UIKit.Fill(area, "Board");
            var size = area.rect.size;
            if (size.x <= 0) size = new Vector2(300, 74);
            int n = spec.Map.Length;
            twoFloors = !string.IsNullOrEmpty(spec.Lower);
            // Two floors need the height: smaller cells, the lower floor near the bottom, the upper one above it.
            cell = Mathf.Min(twoFloors ? 22f : 30f, (size.x - 20f) / (n + 2));
            x0 = -(n - 1) * cell / 2f;
            baseY = -size.y / 2f + (twoFloors ? 12f : 20f);
            upperY = baseY + cell * 1.9f;

            float wallHeight = (FloorY(0) - baseY) + cell * 1.6f;
            Block(-1, Palette.HighWall, wallHeight);
            Block(n, Palette.HighWall, wallHeight);
            for (int i = 0; i < n; i++) DrawCell(i, spec.Map[i], 0);
            if (twoFloors)
                for (int i = 0; i < n; i++) DrawCell(i, spec.Lower[i], 1);

            runner = UIKit.Shape(board, "Runner", UISprites.Rounded, Palette.Runner, Vector2.zero, new Vector2(cell * 0.62f, cell * 0.62f)).rectTransform;
            UIKit.Round(runner.GetComponent<Image>(), 4f);
            eye = UIKit.Shape(runner, "Eye", UISprites.Rounded, Palette.RunnerEye, Vector2.zero, new Vector2(cell * 0.12f, cell * 0.18f)).rectTransform;

            qiText = UIKit.Label(UIKit.Rect(board, "Qi", new Vector2(1, 1), new Vector2(1, 1), new Vector2(-70, -18), new Vector2(-4, 0)),
                "Text", "", 12, TextAnchor.MiddleRight, Theme.Qi, FontStyle.Bold);
            actionText = UIKit.Label(UIKit.Centered(board, "Action", Vector2.zero, new Vector2(120, 18)),
                "Text", "", 12, TextAnchor.MiddleCenter, Theme.Gold, FontStyle.Bold);
        }

        void Block(int i, Color color, float height, float bottom) =>
            UIKit.Shape(board, "Wall", UISprites.Rounded, color, new Vector2(X(i), bottom + height / 2f - cell * 0.25f), new Vector2(cell * 0.92f, height));

        void Block(int i, Color color, float height) => Block(i, color, height, baseY);

        void DrawCell(int i, char c, int floor)
        {
            float top = FloorY(floor);
            bool upper = twoFloors && floor == 0;
            // The upper floor is a thin floor, and its pits are open holes (nothing drawn).
            if (!(upper && c == Vocabulary.Pit))
            {
                var groundColor = c == Vocabulary.Pit ? Palette.PitHole
                    : c == Vocabulary.Crumble ? Palette.Crumble
                    : (i % 2 == 0 ? Palette.Ground : Palette.GroundDark);
                float h = upper ? cell * 0.3f : cell * 0.5f;
                var ground = UIKit.Shape(board, "Cell", UISprites.Rounded, groundColor, new Vector2(X(i), top - h / 2f), new Vector2(cell * 0.94f, h));
                if (c == Vocabulary.Crumble) cracks[Key(floor, i)] = ground;
            }
            switch (c)
            {
                case Vocabulary.Spike:
                    UIKit.Shape(board, "Spike", UISprites.Triangle, Palette.Spike, new Vector2(X(i), top + cell * 0.25f), new Vector2(cell * 0.7f, cell * 0.5f));
                    break;
                case Vocabulary.LowWall:
                    UIKit.Shape(board, "LowWall", UISprites.Rounded, Palette.LowWall, new Vector2(X(i), top + cell * 0.3f), new Vector2(cell * 0.8f, cell * 0.6f));
                    break;
                case Vocabulary.HighWall:
                    Block(i, Palette.HighWall, cell * 1.6f, top);
                    break;
                case Vocabulary.Trap:
                    trapSpikes.Add(UIKit.Shape(board, "Trap", UISprites.Triangle, Palette.TrapUp, new Vector2(X(i), top + cell * 0.22f), new Vector2(cell * 0.7f, cell * 0.44f)));
                    break;
                case Vocabulary.Orb:
                    orbs[Key(floor, i)] = UIKit.Shape(board, "Orb", UISprites.Circle, Palette.Orb, new Vector2(X(i), top + cell * 0.3f), new Vector2(cell * 0.4f, cell * 0.4f));
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

        void Place(float x, float lift, int dir, float ground)
        {
            runner.anchoredPosition = new Vector2(x, ground + cell * 0.31f + lift);
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
                foreach (var c in cracks.Values) { c.enabled = true; c.color = Palette.Crumble; }
                runner.GetComponent<Image>().color = Palette.Runner;
                Place(X(spec.Start), 0f, spec.Dir, FloorY(0));
                SetTraps(0);
                qiText.text = "Energy " + spec.Qi;
                actionText.text = "";
                yield return Wait(0.6f);

                foreach (var f in result.Frames)
                {
                    SetTraps(f.T);
                    if (f.FiredRule >= 0)
                    {
                        var rule = spec.Rules[f.FiredRule];
                        int cost = rule.Cost;
                        actionText.text = Vocabulary.Display(rule.Action) + (cost > 0 && showEnergy ? " -" + cost : "");
                    }
                    else if (f.StarvedRule >= 0) actionText.text = "not enough Energy!";
                    else actionText.text = f.HoldingRule >= 0 ? "waiting..." : "";
                    actionText.color = f.StarvedRule >= 0 ? Theme.Danger : Theme.Gold;
                    float from = FloorY(f.FloorBefore), to = FloorY(f.Floor);
                    actionText.rectTransform.anchoredPosition = new Vector2(X(f.XBefore), from + cell * (twoFloors ? 1.25f : 1.5f));

                    float height = f.Action == ActionType.Jump ? cell * 1.0f : f.Action == ActionType.BigJump ? cell * 1.4f : 0f;
                    for (float t = 0; t < 1f; t += Time.unscaledDeltaTime / StepSeconds)
                    {
                        float k = Mathf.Clamp01(t);
                        float x = f.Blocked ? X(f.XBefore) : Mathf.Lerp(X(f.XBefore), X(f.X), k);
                        int dir = f.Action == ActionType.Turn ? (k < 0.5f ? f.DirBefore : f.Dir) : f.Dir;
                        Place(x, height * 4f * k * (1f - k), dir, from);
                        yield return null;
                    }
                    // A cracked tile left behind breaks; on the upper floor it leaves an open hole.
                    if (f.Crumbled >= 0 && cracks.TryGetValue(Key(f.FloorBefore, f.Crumbled), out var crack))
                    {
                        if (twoFloors && f.FloorBefore == 0) crack.enabled = false;
                        else crack.color = Palette.PitHole;
                    }
                    if (f.Fell)
                    {
                        for (float t = 0; t < 1f; t += Time.unscaledDeltaTime / (StepSeconds * 0.6f))
                        {
                            float k = Mathf.Clamp01(t);
                            Place(X(f.X), 0f, f.Dir, Mathf.Lerp(from, to, k * k));
                            yield return null;
                        }
                    }
                    Place(X(f.X), 0f, f.Dir, to);
                    qiText.text = "Energy " + f.Qi;
                    if (f.CollectedOrb && orbs.TryGetValue(Key(f.Floor, f.X), out var orb))
                    {
                        orb.enabled = false;
                        actionText.text = "+2 Energy";
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
