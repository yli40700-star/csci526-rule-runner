using System.Collections;
using System.Collections.Generic;
using RuleRunner.Core;
using UnityEngine;

namespace RuleRunner.View
{
    /// <summary>
    /// Draws a level's map with basic shapes. Cell i sits at world x = i; the ground top is y = 0.
    /// The map edges are drawn as high walls, matching the simulator, except next to a floor that already ends in one.
    /// Two-floor levels (chapter 3): the level's Map is the upper floor, a thin floor at y = FloorGap, and its pits
    /// are open holes; Lower is drawn at y = 0 like a one-row map. Floor 0 is always the Map, floor 1 the Lower.
    /// </summary>
    public class MapView : MonoBehaviour
    {
        public const float LowWallHeight = 0.55f;
        public const float HighWallHeight = 2.4f;
        /// <summary>Height of the upper floor above the lower one on two-floor levels.</summary>
        public const float FloorGap = 2.6f;
        /// <summary>Thickness of the upper floor's tiles (the lower floor and one-row maps use 1).</summary>
        const float UpperSlab = 0.35f;

        /// <summary>A cracked tile: its pieces, where they rest, their colors, and the pit drawn once it breaks.</summary>
        sealed class Crack
        {
            public Transform Root;
            public Vector3 Home;
            public SpriteRenderer[] Parts;
            public Color[] Colors;
            public SpriteRenderer Hole;
        }

        readonly Dictionary<int, SpriteRenderer> orbs = new Dictionary<int, SpriteRenderer>();
        readonly Dictionary<int, Crack> cracks = new Dictionary<int, Crack>();
        readonly List<(SpriteRenderer slab, SpriteRenderer spikes)> traps = new List<(SpriteRenderer, SpriteRenderer)>();
        Transform root;
        Transform marks;
        Transform highlight;
        bool twoFloors;

        public int Length { get; private set; }
        public bool TwoFloors => twoFloors;

        /// <summary>World y of a floor's ground line: the upper floor of a two-floor level sits FloorGap up.</summary>
        public float FloorY(int floor) => twoFloors && floor == 0 ? FloorGap : 0f;

        static int Key(int floor, int x) => floor * 1000 + x;

        public void Build(LevelDef level)
        {
            StopAllCoroutines();
            if (root) Destroy(root.gameObject);
            root = new GameObject("Map").transform;
            root.SetParent(transform, false);
            orbs.Clear();
            cracks.Clear();
            traps.Clear();
            marks = new GameObject("Marks").transform;
            marks.SetParent(root, false);
            Length = level.Map.Length;
            twoFloors = level.TwoFloors;

            // Map edges count as high walls, one per floor. A floor that already ends in a high wall gets no extra
            // edge wall, so the wall is not drawn two tiles thick.
            DrawEdgeWalls(level.Map, 0);
            if (twoFloors) DrawEdgeWalls(level.Lower, 1);

            for (int i = 0; i < Length; i++) DrawCell(i, level.Map[i], 0);
            if (twoFloors)
                for (int i = 0; i < Length; i++) DrawCell(i, level.Lower[i], 1);
            SetTime(0);
        }

        void DrawCell(int i, char c, int floor)
        {
            var sq = Shapes.Square;
            float y = FloorY(floor);
            bool upper = twoFloors && floor == 0;
            float slab = upper ? UpperSlab : 1f;

            if (c == Vocabulary.Crumble)
            {
                DrawCrack(i, floor, y, slab, upper);
                return;
            }
            if (c != Vocabulary.Pit)
            {
                Shapes.Create(root, "Ground " + i, sq, i % 2 == 0 ? Palette.Ground : Palette.GroundDark,
                    new Vector2(i, y - slab), new Vector2(0.98f, slab));
            }
            else if (!upper)
            {
                Shapes.Create(root, "Pit " + i, sq, Palette.PitHole, new Vector2(i, y - 1f), new Vector2(0.98f, 1f));
            }
            // A pit in the upper floor is an open hole: nothing is drawn, so the floor below shows through.

            switch (c)
            {
                case Vocabulary.Spike:
                    for (int k = 0; k < 3; k++)
                        Shapes.Create(root, "Spike " + i, Shapes.Triangle, Palette.Spike,
                            new Vector2(i - 0.3f + 0.3f * k, y), new Vector2(0.3f, 0.55f), 1);
                    break;
                case Vocabulary.LowWall:
                    Shapes.Create(root, "LowWall " + i, sq, Palette.LowWall, new Vector2(i, y), new Vector2(0.9f, LowWallHeight), 1);
                    break;
                case Vocabulary.HighWall:
                    DrawHighWall(i, y - slab, Mathf.Min(y + HighWallHeight, WallBand(floor).top));
                    break;
                case Vocabulary.Trap:
                    var trapSlab = Shapes.Create(root, "Trap " + i, sq, Palette.TrapUp, new Vector2(i, y), new Vector2(0.9f, 0.12f), 1);
                    var spikes = Shapes.Create(root, "TrapSpikes " + i, Shapes.Triangle, Palette.TrapUp,
                        new Vector2(i, y + 0.1f), new Vector2(0.7f, 0.45f), 1);
                    traps.Add((trapSlab, spikes));
                    break;
                case Vocabulary.Orb:
                    orbs[Key(floor, i)] = Shapes.Create(root, "Orb " + i, Shapes.Circle, Palette.Orb, new Vector2(i, y + 0.25f), new Vector2(0.4f, 0.4f), 2);
                    break;
                case Vocabulary.Finish:
                    Shapes.Create(root, "FlagPole " + i, sq, Color.white, new Vector2(i + 0.25f, y), new Vector2(0.06f, 1.3f), 1);
                    Shapes.Create(root, "Flag " + i, sq, Palette.Finish, new Vector2(i + 0.02f, y + 0.85f), new Vector2(0.42f, 0.42f), 1);
                    break;
            }
        }

        /// <summary>
        /// The vertical band a floor's walls fill. One-row maps: from under the ground to HighWallHeight.
        /// Two-floor levels: the lower floor's walls stop under the upper floor's slab, and the upper floor's start there.
        /// </summary>
        (float bottom, float top) WallBand(int floor)
        {
            if (!twoFloors) return (-1f, HighWallHeight);
            float ceiling = FloorY(0) - UpperSlab;
            return floor == 1 ? (-1f, ceiling) : (ceiling, FloorY(0) + HighWallHeight);
        }

        void DrawEdgeWalls(string row, int floor)
        {
            var (bottom, top) = WallBand(floor);
            if (row[0] != Vocabulary.HighWall) DrawHighWall(-1, bottom, top);
            if (row[row.Length - 1] != Vocabulary.HighWall) DrawHighWall(Length, bottom, top);
        }

        void DrawHighWall(int i, float bottom, float top) =>
            Shapes.Create(root, "HighWall " + i, Shapes.Square, Palette.HighWall, new Vector2(i, bottom), new Vector2(0.98f, top - bottom), 1);

        /// <summary>Cracked ground: a sandy slab with dark cracks. On a one-row floor a pit waits underneath, hidden until it breaks.</summary>
        void DrawCrack(int i, int floor, float y, float slab, bool upper)
        {
            var tile = new GameObject("Crack " + i).transform;
            tile.SetParent(root, false);
            tile.localPosition = new Vector3(i, y, 0f);
            Shapes.Create(tile, "Slab", Shapes.Square, Palette.Crumble, new Vector2(0f, -slab), new Vector2(0.98f, slab));
            float h = Mathf.Min(slab, 0.6f);
            var a = Shapes.Create(tile, "CrackA", Shapes.Square, Palette.CrumbleCrack, new Vector2(-0.2f, -h), new Vector2(0.05f, h * 0.95f), 1);
            a.transform.localRotation = Quaternion.Euler(0f, 0f, 18f);
            var b = Shapes.Create(tile, "CrackB", Shapes.Square, Palette.CrumbleCrack, new Vector2(0.18f, -h), new Vector2(0.05f, h * 0.95f), 1);
            b.transform.localRotation = Quaternion.Euler(0f, 0f, -24f);
            var c = Shapes.Create(tile, "CrackC", Shapes.Square, Palette.CrumbleCrack, new Vector2(0f, -h * 0.55f), new Vector2(0.04f, h * 0.45f), 1);
            c.transform.localRotation = Quaternion.Euler(0f, 0f, 70f);

            SpriteRenderer hole = null;
            if (!upper)
            {
                hole = Shapes.Create(root, "CrackHole " + i, Shapes.Square, Palette.PitHole, new Vector2(i, y - 1f), new Vector2(0.98f, 1f), -1);
                hole.enabled = false;
            }
            var parts = tile.GetComponentsInChildren<SpriteRenderer>(true);
            var colors = new Color[parts.Length];
            for (int k = 0; k < parts.Length; k++) colors[k] = parts[k].color;
            cracks[Key(floor, i)] = new Crack { Root = tile, Home = tile.localPosition, Parts = parts, Colors = colors, Hole = hole };
        }

        /// <summary>The cracked tile at x on that floor gives way: a short shake, then it drops and fades, leaving a pit.</summary>
        public void Crumble(int x, int floor = 0)
        {
            if (cracks.TryGetValue(Key(floor, x), out var crack) && crack.Root.gameObject.activeSelf)
                StartCoroutine(Collapse(crack));
        }

        IEnumerator Collapse(Crack crack)
        {
            const float shake = 0.12f, fall = 0.35f;
            for (float t = 0f; t < shake; t += Time.deltaTime)
            {
                crack.Root.localPosition = crack.Home + new Vector3(Mathf.Sin(t * 120f) * 0.04f, 0f, 0f);
                yield return null;
            }
            if (crack.Hole) crack.Hole.enabled = true;
            for (float t = 0f; t < fall; t += Time.deltaTime)
            {
                float k = Mathf.Clamp01(t / fall);
                crack.Root.localPosition = crack.Home + Vector3.down * (1.4f * k * k);
                for (int i = 0; i < crack.Parts.Length; i++)
                {
                    var col = crack.Colors[i];
                    col.a *= 1f - k;
                    crack.Parts[i].color = col;
                }
                yield return null;
            }
            crack.Root.gameObject.SetActive(false);
        }

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

        public void CollectOrb(int x, int floor = 0)
        {
            if (orbs.TryGetValue(Key(floor, x), out var orb)) orb.enabled = false;
        }

        /// <summary>Back to the state at the start of a run.</summary>
        public void ResetState()
        {
            StopAllCoroutines();
            foreach (var orb in orbs.Values) orb.enabled = true;
            foreach (var crack in cracks.Values)
            {
                crack.Root.gameObject.SetActive(true);
                crack.Root.localPosition = crack.Home;
                for (int i = 0; i < crack.Parts.Length; i++) crack.Parts[i].color = crack.Colors[i];
                if (crack.Hole) crack.Hole.enabled = false;
            }
            SetTime(0);
            ClearMarks();
        }

        /// <summary>A red X over the cell where the run ended badly.</summary>
        public void MarkDeath(int x, int floor = 0)
        {
            if (!marks) return;
            const float h = 0.8f;
            var center = new Vector2(x, FloorY(floor) + 1.2f);
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
        public void ShowPreview(IEnumerable<(int x, int dir, int floor)> spots, Color color, bool clear = true)
        {
            if (clear) ClearMarks();
            if (!marks) return;
            foreach (var (x, dir, floor) in spots)
            {
                float y = FloorY(floor);
                Shapes.Create(marks, "Preview", Shapes.Circle, color, new Vector2(x + 0.5f * dir, y + 1.1f), new Vector2(0.28f, 0.28f), 8);
                Shapes.Create(marks, "PreviewLine", Shapes.Square, new Color(color.r, color.g, color.b, 0.35f),
                    new Vector2(x + 0.5f * dir, y + 0.05f), new Vector2(0.05f, 1.05f), 7);
            }
        }

        /// <summary>A gold frame around one cell: the tile a card's IF is looking at (tutorial).</summary>
        public void HighlightCell(int x, int floor = 0)
        {
            ClearHighlight();
            if (!root) return;
            highlight = new GameObject("Highlight").transform;
            highlight.SetParent(root, false);
            highlight.localPosition = new Vector3(0f, FloorY(floor), 0f);
            var gold = new Color(0.96f, 0.77f, 0.26f);
            const float w = 0.07f, top = 1.25f, bottom = -0.35f;
            Shapes.Create(highlight, "L", Shapes.Square, gold, new Vector2(x - 0.47f, bottom), new Vector2(w, top - bottom), 9);
            Shapes.Create(highlight, "R", Shapes.Square, gold, new Vector2(x + 0.47f, bottom), new Vector2(w, top - bottom), 9);
            Shapes.Create(highlight, "T", Shapes.Square, gold, new Vector2(x, top - w), new Vector2(0.94f + w, w), 9);
            Shapes.Create(highlight, "B", Shapes.Square, gold, new Vector2(x, bottom), new Vector2(0.94f + w, w), 9);
        }

        public void ClearHighlight()
        {
            if (highlight) Destroy(highlight.gameObject);
            highlight = null;
        }

        public void ClearMarks()
        {
            if (!marks) return;
            for (int i = marks.childCount - 1; i >= 0; i--) Destroy(marks.GetChild(i).gameObject);
        }
    }
}
