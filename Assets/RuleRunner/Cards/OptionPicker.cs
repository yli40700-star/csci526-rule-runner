using System;
using System.Collections.Generic;
using RuleRunner.Core;
using RuleRunner.UI;
using UnityEngine;
using UnityEngine.UI;

namespace RuleRunner.Cards
{
    /// <summary>Modal grid of option tiles (each with its mini diagram) for picking a card's condition or action.</summary>
    public class OptionPicker : MonoBehaviour
    {
        RectTransform grid, box;
        Text title, subtitle;

        public static OptionPicker Create(Transform canvas)
        {
            var root = UIKit.Fill(canvas, "OptionPicker");
            var blocker = root.gameObject.AddComponent<Image>();
            blocker.color = new Color(0.03f, 0.04f, 0.06f, 0.7f);
            var picker = root.gameObject.AddComponent<OptionPicker>();
            var closeBtn = root.gameObject.AddComponent<Button>();
            closeBtn.transition = Selectable.Transition.None;
            closeBtn.onClick.AddListener(picker.Hide);

            var boxImg = UIKit.Box(root, "Box", Theme.Surface, 18f, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            UIKit.DropShadow(boxImg, 8f, 0.5f);
            picker.box = boxImg.rectTransform;

            picker.title = UIKit.Label(UIKit.TopStrip(picker.box, "Title", 16f, 30f, 24f, 24f), "Text", "", 24, TextAnchor.MiddleLeft, Theme.Gold, FontStyle.Bold);
            picker.subtitle = UIKit.Label(UIKit.TopStrip(picker.box, "Subtitle", 46f, 22f, 24f, 24f), "Text", "", 15, TextAnchor.MiddleLeft, Theme.Dim);

            picker.grid = UIKit.Rect(picker.box, "Grid", Vector2.zero, Vector2.one, new Vector2(20, 64), new Vector2(-20, -78));
            var g = picker.grid.gameObject.AddComponent<GridLayoutGroup>();
            g.cellSize = new Vector2(150, 112);
            g.spacing = new Vector2(12, 12);
            g.childAlignment = TextAnchor.UpperCenter;

            var cancel = UIKit.Button(picker.box, "Cancel", "Cancel", 16, Theme.Raised, picker.Hide, 10f);
            var cancelRt = (RectTransform)cancel.transform;
            cancelRt.anchorMin = cancelRt.anchorMax = new Vector2(0.5f, 0f);
            cancelRt.pivot = new Vector2(0.5f, 0f);
            cancelRt.sizeDelta = new Vector2(140, 38);
            cancelRt.anchoredPosition = new Vector2(0, 16);

            root.gameObject.SetActive(false);
            return picker;
        }

        public void ShowConditions(string heading, string sub, IList<Condition> options, bool negate, Condition selected, Action<Condition> onPick)
        {
            Open(heading, sub, options.Count);
            foreach (var c in options)
            {
                var cond = c;
                var tile = Tile(c == selected, () => onPick(cond));
                Icons.DrawCondition(UIKit.Centered(tile, "Icon", new Vector2(0, 14), Icons.Size), c, negate);
                UIKit.Label(UIKit.Rect(tile, "Name", Vector2.zero, new Vector2(1, 0), new Vector2(6, 10), new Vector2(-6, 36)),
                    "Text", (negate ? "NOT " : "") + Vocabulary.Display(c), 15, TextAnchor.MiddleCenter, Theme.Text, FontStyle.Bold);
            }
        }

        public void ShowActions(string heading, string sub, IList<ActionType> options, ActionType selected, Action<ActionType> onPick)
        {
            Open(heading, sub, options.Count);
            foreach (var a in options)
            {
                var act = a;
                var tile = Tile(a == selected, () => onPick(act));
                var band = UIKit.Box(tile, "Band", Theme.ActionColor(a), 6f, new Vector2(0, 1), new Vector2(1, 1), new Vector2(8, -12), new Vector2(-8, -6));
                band.raycastTarget = false;
                Icons.DrawAction(UIKit.Centered(tile, "Icon", new Vector2(0, 12), Icons.Size), a);
                int cost = Vocabulary.Cost(a);
                UIKit.Label(UIKit.Rect(tile, "Name", Vector2.zero, new Vector2(1, 0), new Vector2(6, 8), new Vector2(-6, 34)),
                    "Text", Vocabulary.Display(a) + (cost == 0 ? "   free" : "   -" + cost + " qi"), 15, TextAnchor.MiddleCenter, Theme.Text, FontStyle.Bold);
            }
        }

        void Open(string heading, string sub, int count)
        {
            title.text = heading;
            subtitle.text = sub;
            UIKit.DestroyChildren(grid);
            int columns = Mathf.Clamp(count, 1, 3);
            int rows = Mathf.CeilToInt(count / (float)columns);
            box.sizeDelta = new Vector2(Mathf.Max(360, columns * 150 + (columns - 1) * 12 + 40), 78 + rows * 112 + (rows - 1) * 12 + 70);
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
        }

        RectTransform Tile(bool selected, Action onClick)
        {
            var b = UIKit.Button(grid, "Tile", null, 0, selected ? Theme.RaisedHover : Theme.Surface2, () => { Hide(); onClick(); }, 12f);
            if (selected)
            {
                var o = b.gameObject.AddComponent<Outline>();
                o.effectColor = Theme.Gold;
                o.effectDistance = new Vector2(2, -2);
            }
            return (RectTransform)b.transform;
        }

        public void Hide() => gameObject.SetActive(false);
    }
}
