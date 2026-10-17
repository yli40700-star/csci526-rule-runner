using System.Collections.Generic;
using RuleRunner.Core;
using UnityEngine;

namespace RuleRunner.View
{
    /// <summary>
    /// Draws a level's one-row map with basic shapes. Cell i sits at world x = i; the ground top is y = 0.
    /// The map edges are drawn as high walls, matching the simulator.
    /// </summary>
    public class MapView : MonoBehaviour
    {
        public const float LowWallHeight = 0.55f;
        public const float HighWallHeight = 2.4f;

        readonly Dictionary<int, SpriteRenderer> orbs = new Dictionary<int, SpriteRenderer>();
        readonly List<(SpriteRenderer slab, SpriteRenderer spikes)> traps = new List<(SpriteRenderer, SpriteRenderer)>();
        Transform root;
        Transform marks;

        public int Length { get; private set; }

        public void Build(LevelDef level)
        {
            if (root) Destroy(root.gameObject);
            root = new GameObject("Map").transform;
            root.SetParent(transform, false);
            orbs.Clear();
            traps.Clear();
            marks = new GameObject("Marks").transform;
            marks.SetParent(root, false);
            Length = level.Map.Length;

            // Map edges count as high walls.
            DrawHighWall(-1);
            DrawHighWall(Length);

            for (int i = 0; i < Length; i++) DrawCell(i, level.Map[i]);
            SetTime(0);
        }

        void DrawCell(int i, char c)
        {
            var sq = Shapes.Square;
            if (c != Vocabulary.Pit)
            {
                Shapes.Create(root, "Ground " + i, sq, i % 2 == 0 ? Palette.Ground : Palette.GroundDark,
                    new Vector2(i, -1f), new Vector2(0.98f, 1f));
            }
            else
            {
                Shapes.Create(root, "Pit " + i, sq, Palette.PitHole, new Vector2(i, -1f), new Vector2(0.98f, 1f));
            }

            switch (c)
            {
                case Vocabulary.Spike:
                    for (int k = 0; k < 3; k++)
                        Shapes.Create(root, "Spike " + i, Shapes.Triangle, Palette.Spike,
                            new Vector2(i - 0.3f + 0.3f * k, 0f), new Vector2(0.3f, 0.55f), 1);
                    break;
                case Vocabulary.LowWall:
                    Shapes.Create(root, "LowWall " + i, sq, Palette.LowWall, new Vector2(i, 0f), new Vector2(0.9f, LowWallHeight), 1);
                    break;
                case Vocabulary.HighWall:
                    DrawHighWall(i);
                    break;
                case Vocabulary.Trap:
                    var slab = Shapes.Create(root, "Trap " + i, sq, Palette.TrapUp, new Vector2(i, 0f), new Vector2(0.9f, 0.12f), 1);
                    var spikes = Shapes.Create(root, "TrapSpikes " + i, Shapes.Triangle, Palette.TrapUp,
                        new Vector2(i, 0.1f), new Vector2(0.7f, 0.45f), 1);
                    traps.Add((slab, spikes));
                    break;
                case Vocabulary.Orb:
                    orbs[i] = Shapes.Create(root, "Orb " + i, Shapes.Circle, Palette.Orb, new Vector2(i, 0.25f), new Vector2(0.4f, 0.4f), 2);
                    break;
                case Vocabulary.Finish:
                    Shapes.Create(root, "FlagPole " + i, sq, Color.white, new Vector2(i + 0.25f, 0f), new Vector2(0.06f, 1.3f), 1);
                    Shapes.Create(root, "Flag " + i, sq, Palette.Finish, new Vector2(i + 0.02f, 0.85f), new Vector2(0.42f, 0.42f), 1);
                    break;
            }
        }

        void DrawHighWall(int i) =>
            Shapes.Create(root, "HighWall " + i, Shapes.Square, Palette.HighWall, new Vector2(i, -1f), new Vector2(0.98f, HighWallHeight + 1f), 1);

        /// <summary>Shows traps raised or lowered for step time t.</summary>
        public void SetTime(int t)
        {
            bool up = Vocabulary.TrapUp(t);
            foreach (var (slab, spikes) in traps)
            {
                slab.color = up ? Palette.TrapUp : Palette.TrapDown;
                spikes.enabled = up;
            }
        }

        public void CollectOrb(int x)
        {
            if (orbs.TryGetValue(x, out var orb)) orb.enabled = false;
        }

        /// <summary>Back to the state at the start of a run.</summary>
        public void ResetState()
        {
            foreach (var orb in orbs.Values) orb.enabled = true;
            SetTime(0);
            ClearMarks();
        }

        /// <summary>A red X over the cell where the run ended badly.</summary>
        public void MarkDeath(int x)
        {
            if (!marks) return;
            const float h = 0.8f;
            var center = new Vector2(x, 1.2f);
            foreach (float angle in new[] { 40f, -40f })
            {
                // Sprites pivot at the bottom, so offset the bar to rotate it around its middle.
                float r = angle * Mathf.Deg2Rad;
                var pos = center + new Vector2(Mathf.Sin(r) * h * 0.5f, -Mathf.Cos(r) * h * 0.5f);
                var bar = Shapes.Create(marks, "DeathMark", Shapes.Square, Palette.Dead, pos, new Vector2(0.14f, h), 8);
                bar.transform.localRotation = Quaternion.Euler(0, 0, angle);
            }
        }

        /// <summary>Small markers in front of the cells where a card would fire (hover preview).</summary>
        public void ShowPreview(IEnumerable<(int x, int dir)> spots, Color color)
        {
            ClearMarks();
            if (!marks) return;
            foreach (var (x, dir) in spots)
            {
                Shapes.Create(marks, "Preview", Shapes.Circle, color, new Vector2(x + 0.5f * dir, 1.1f), new Vector2(0.28f, 0.28f), 8);
                Shapes.Create(marks, "PreviewLine", Shapes.Square, new Color(color.r, color.g, color.b, 0.35f),
                    new Vector2(x + 0.5f * dir, 0.05f), new Vector2(0.05f, 1.05f), 7);
            }
        }

        public void ClearMarks()
        {
            if (!marks) return;
            for (int i = marks.childCount - 1; i >= 0; i--) Destroy(marks.GetChild(i).gameObject);
        }
    }
}
