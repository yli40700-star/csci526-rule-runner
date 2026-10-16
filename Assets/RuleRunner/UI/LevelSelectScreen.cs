using System;
using System.Linq;
using RuleRunner.Core;
using RuleRunner.Levels;
using RuleRunner.View;
using UnityEngine;
using UnityEngine.UI;

namespace RuleRunner.UI
{
    /// <summary>Title screen: one row of level tiles per chapter, each with a mini map preview.</summary>
    public class LevelSelectScreen : MonoBehaviour
    {
        public event Action<LevelDef> LevelChosen;

        RectTransform chapters;
        Func<string, bool> isCleared;

        static readonly Vector2 TileSize = new Vector2(196, 132);

        public static LevelSelectScreen Create(Transform canvas, Func<string, bool> isCleared)
        {
            var root = UIKit.Panel(canvas, "LevelSelect", Theme.Background, Vector2.zero, Vector2.one);
            var screen = root.gameObject.AddComponent<LevelSelectScreen>();
            screen.isCleared = isCleared;

            // Title with a little runner jumping over a spike, made of the same shapes as the game.
            var logo = UIKit.TopStrip(root.transform, "Logo", 40f, 120f);
            var title = UIKit.Label(logo, "Title", "RULE RUNNER", 76, TextAnchor.MiddleCenter, Theme.Gold, FontStyle.Bold);
            UIKit.DropShadow(title, 5f, 0.6f);
            var deco = UIKit.Centered(logo, "Deco", new Vector2(-330, 6), new Vector2(90, 70));
            UIKit.Shape(deco, "Ground", UISprites.Rounded, Palette.Ground, new Vector2(0, -26), new Vector2(84, 8));
            UIKit.Shape(deco, "Spike", UISprites.Triangle, Palette.Spike, new Vector2(4, -14), new Vector2(18, 16));
            UIKit.Shape(deco, "Runner", UISprites.Rounded, Palette.Runner, new Vector2(-6, 18), new Vector2(22, 22));
            for (int i = 0; i < 4; i++)
                UIKit.Shape(deco, "Dot", UISprites.Circle, new Color(1, 1, 1, 0.5f),
                    new Vector2(-38 + i * 9, -16 + 28 * Mathf.Sin(i / 4f * Mathf.PI * 0.8f)), new Vector2(4, 4));

            UIKit.Label(UIKit.TopStrip(root.transform, "Tagline", 158f, 60f, 160f, 160f), "Text",
                "You never press jump during a run. Equip your runner with IF-THEN skill cards.\n" +
                "Every card fires every time its condition comes true - and every firing spends qi.",
                19, TextAnchor.MiddleCenter, Theme.Dim);

            screen.chapters = UIKit.Rect(root.transform, "Chapters", new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(-540, -660), new Vector2(540, -240));
            UIKit.VColumn(screen.chapters, 14);

            UIKit.Label(UIKit.Rect(root.transform, "Footer", new Vector2(0, 0), new Vector2(1, 0), new Vector2(20, 10), new Vector2(-20, 36)),
                "Text", "CSCI 526  -  Beta build  -  grey-box prototype", 14, TextAnchor.MiddleCenter, Theme.Faint);
            return screen;
        }

        public void Show()
        {
            Refresh();
            gameObject.SetActive(true);
        }

        public void Hide() => gameObject.SetActive(false);

        void Refresh()
        {
            UIKit.DestroyChildren(chapters);
            foreach (var group in LevelDatabase.All.GroupBy(l => l.Chapter))
            {
                var head = UIKit.Rect(chapters, "Chapter " + group.Key, Vector2.zero, Vector2.one);
                UIKit.Layout(head, -1, 30);
                int cleared = group.Count(l => l.Playable && isCleared(l.Id));
                int playable = group.Count(l => l.Playable);
                UIKit.Label(head, "Name", "CHAPTER " + group.Key, 18, TextAnchor.MiddleLeft, Theme.Text, FontStyle.Bold);
                UIKit.Label(head, "Progress", cleared + " / " + playable + " cleared", 15, TextAnchor.MiddleRight, Theme.Dim);

                var row = UIKit.Rect(chapters, "Row " + group.Key, Vector2.zero, Vector2.one);
                UIKit.HRow(row, 15);
                UIKit.Layout(row, -1, TileSize.y + 8);
                foreach (var level in group) Tile(row, level);
            }
        }

        void Tile(RectTransform row, LevelDef level)
        {
            bool playable = level.Playable;
            bool cleared = playable && isCleared(level.Id);
            var lvl = level;
            var b = UIKit.Button(row, "Level " + level.Id, null, 0, playable ? Theme.Surface2 : Theme.Surface, () => LevelChosen?.Invoke(lvl), 14f);
            b.interactable = playable;
            UIKit.Layout(b, TileSize.x, TileSize.y);
            var img = (Image)b.targetGraphic;
            if (playable) UIKit.DropShadow(img, 4f, 0.45f);
            var t = b.transform;

            var chip = UIKit.Box(t, "Id", playable ? Theme.Gold : Theme.Raised, 8f, new Vector2(0, 1), new Vector2(0, 1), new Vector2(12, -38), new Vector2(64, -12));
            UIKit.Label(chip.transform, "Text", level.Id, 16, TextAnchor.MiddleCenter, playable ? Theme.Background : Theme.Dim, FontStyle.Bold);
            if (cleared)
            {
                var badge = UIKit.Box(t, "Clear", Theme.Accent, 8f, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-72, -38), new Vector2(-12, -12));
                UIKit.Label(badge.transform, "Text", "CLEAR", 13, TextAnchor.MiddleCenter, Theme.Text, FontStyle.Bold);
            }

            UIKit.Label(UIKit.TopStrip(t, "Title", 44f, 40f, 12f, 12f), "Text", playable ? level.Title : "Coming soon",
                16, TextAnchor.UpperLeft, playable ? Theme.Text : Theme.Faint, FontStyle.Bold);

            if (playable)
            {
                UIKit.Label(UIKit.Rect(t, "Meta", new Vector2(0, 0), new Vector2(1, 0), new Vector2(12, 26), new Vector2(-12, 44)),
                    "Text", level.Qi + " qi   -   " + level.Slots + (level.Slots == 1 ? " card" : " cards"), 13, TextAnchor.MiddleLeft, Theme.Dim);
                MiniMap(UIKit.Rect(t, "Map", new Vector2(0, 0), new Vector2(1, 0), new Vector2(12, 10), new Vector2(-12, 22)), level.Map);
            }
        }

        /// <summary>The level's map as a strip of colored blocks.</summary>
        static void MiniMap(RectTransform rt, string map)
        {
            float width = TileSize.x - 24f;
            float w = Mathf.Min(10f, width / map.Length);
            for (int i = 0; i < map.Length; i++)
            {
                char c = map[i];
                Color color;
                float h = 4f;
                switch (c)
                {
                    case Vocabulary.Pit: color = Palette.PitHole; break;
                    case Vocabulary.Spike: color = Palette.Spike; h = 8f; break;
                    case Vocabulary.LowWall: color = Palette.LowWall; h = 10f; break;
                    case Vocabulary.HighWall: color = Palette.HighWall; h = 12f; break;
                    case Vocabulary.Trap: color = Palette.TrapUp; h = 7f; break;
                    case Vocabulary.Orb: color = Palette.Orb; h = 7f; break;
                    case Vocabulary.Finish: color = Palette.Finish; h = 12f; break;
                    default: color = Palette.Ground; break;
                }
                var cell = UIKit.Panel(rt, "C", color, new Vector2(0, 0), new Vector2(0, 0));
                cell.raycastTarget = false;
                var crt = cell.rectTransform;
                crt.pivot = new Vector2(0, 0);
                crt.sizeDelta = new Vector2(Mathf.Max(1f, w - 1f), h);
                crt.anchoredPosition = new Vector2(i * w, 0);
            }
        }
    }
}
