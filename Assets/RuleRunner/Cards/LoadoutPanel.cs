using System;
using System.Collections.Generic;
using RuleRunner.Core;
using RuleRunner.UI;
using RuleRunner.View;
using UnityEngine;
using UnityEngine.UI;

namespace RuleRunner.Cards
{
    /// <summary>
    /// The runner's loadout tray: runner portrait with the qi pool, then the equipped skill cards
    /// (checked left to right) and empty slots up to the level's limit. Owns the rule list being edited.
    /// </summary>
    public class LoadoutPanel : MonoBehaviour
    {
        const int MaxPips = 24;

        public List<Rule> Rules { get; private set; } = new List<Rule>();
        public LevelDef Level { get; private set; }

        Text loadoutText, qiText;
        RectTransform cardRow, pipGrid;
        OptionPicker picker;
        readonly List<RuleCardView> cards = new List<RuleCardView>();
        readonly List<EmptySlotView> emptySlots = new List<EmptySlotView>();
        readonly List<Image> pipImages = new List<Image>();
        bool editable = true;

        /// <summary>Builds the tray inside the given area (GameScreen.LoadoutArea).</summary>
        public static LoadoutPanel Create(RectTransform area, OptionPicker picker)
        {
            var tray = UIKit.FillBox(area, "Loadout", Theme.Surface, 16f);
            UIKit.DropShadow(tray, 4f, 0.35f);
            var panel = tray.gameObject.AddComponent<LoadoutPanel>();
            panel.picker = picker;

            var head = UIKit.TopStrip(tray.transform, "Head", 8f, 26f, 18f, 18f);
            UIKit.Label(head, "Title", "LOADOUT", 15, TextAnchor.MiddleLeft, Theme.Gold, FontStyle.Bold);
            panel.loadoutText = UIKit.Label(UIKit.Rect(head, "Info", Vector2.zero, Vector2.one, new Vector2(84, 0), Vector2.zero),
                "Text", "", 14, TextAnchor.MiddleLeft, Theme.Dim);
            UIKit.Label(head, "Default", "No card fires -> Walk (free)", 14, TextAnchor.MiddleRight, Theme.Faint);

            panel.cardRow = UIKit.Rect(tray.transform, "Row", Vector2.zero, Vector2.one, new Vector2(14, 12), new Vector2(-14, -40));
            UIKit.HRow(panel.cardRow, 12, null, false, TextAnchor.MiddleLeft);
            panel.BuildRunnerPanel(panel.cardRow);
            return panel;
        }

        void BuildRunnerPanel(RectTransform row)
        {
            var panel = UIKit.Box(row, "Runner", Theme.Surface2, 14f, Vector2.zero, Vector2.one);
            UIKit.Layout(panel, 138, RuleCardView.CardSize.y);
            var p = panel.transform;

            UIKit.Label(UIKit.TopStrip(p, "Label", 10f, 18f), "Text", "RUNNER", 13, TextAnchor.MiddleCenter, Theme.Dim, FontStyle.Bold);
            // Portrait: the same yellow block as in the map, on a little pedestal.
            var portraitBg = UIKit.Shape(p, "Glow", UISprites.Circle, Palette.Runner.WithAlpha(0.12f), Vector2.zero, new Vector2(84, 84));
            Top(portraitBg.rectTransform, 66f);
            var body = UIKit.Shape(p, "Body", UISprites.Rounded, Palette.Runner, Vector2.zero, new Vector2(46, 46));
            UIKit.Round(body, 8f);
            Top(body.rectTransform, 66f);
            var eye = UIKit.Shape(p, "Eye", UISprites.Rounded, Palette.RunnerEye, Vector2.zero, new Vector2(8, 12));
            Top(eye.rectTransform, 62f, 10f);

            UIKit.Panel(p, "Divider", Theme.Line, new Vector2(0, 1), new Vector2(1, 1), new Vector2(14, -122), new Vector2(-14, -121));
            UIKit.Label(UIKit.TopStrip(p, "QiLabel", 128f, 16f, 12f, 12f), "Text", "QI POOL", 12, TextAnchor.MiddleCenter, Theme.Dim, FontStyle.Bold);
            qiText = UIKit.Label(UIKit.TopStrip(p, "QiValue", 145f, 24f, 12f, 12f), "Text", "0", 22, TextAnchor.MiddleCenter, Theme.Qi, FontStyle.Bold);

            pipGrid = UIKit.TopStrip(p, "Pips", 176f, 46f, 12f, 12f);
            var grid = pipGrid.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(11, 11);
            grid.spacing = new Vector2(3, 3);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 8;
            grid.childAlignment = TextAnchor.UpperCenter;
        }

        static void Top(RectTransform rt, float centerFromTop, float x = 0f)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(x, -centerFromTop);
        }

        // ---------- Editing ----------

        public void Setup(LevelDef level, List<Rule> rules)
        {
            Level = level;
            Rules = rules;
            SetEditable(true);
        }

        public void SetEditable(bool canEdit)
        {
            editable = canEdit;
            if (!canEdit) picker.Hide();
            RebuildCards();
        }

        void RebuildCards()
        {
            int slots = Level != null ? Level.Slots : 0;
            while (cards.Count < Rules.Count)
            {
                var card = RuleCardView.Create(cardRow);
                card.ToggleNot += c => Edit(c.Index, r => r.Negate = !r.Negate);
                card.PickCondition += OnPickCondition;
                card.PickAction += OnPickAction;
                card.MoveLeft += c => Move(c.Index, -1);
                card.MoveRight += c => Move(c.Index, +1);
                card.Remove += c => { if (editable) { Rules.RemoveAt(c.Index); RebuildCards(); } };
                cards.Add(card);
            }
            while (emptySlots.Count < slots - Rules.Count)
                emptySlots.Add(EmptySlotView.Create(cardRow, AddRule));

            // Order in the row: runner panel, cards (priority order), empty slots.
            for (int i = 0; i < cards.Count; i++)
            {
                bool show = i < Rules.Count;
                cards[i].gameObject.SetActive(show);
                if (!show) continue;
                cards[i].transform.SetSiblingIndex(1 + i);
                cards[i].Bind(i, Rules[i], i == 0, i == Rules.Count - 1, editable);
                cards[i].SetState(false);
                cards[i].SetHits(0);
            }
            for (int i = 0; i < emptySlots.Count; i++)
            {
                bool show = i < slots - Rules.Count;
                emptySlots[i].gameObject.SetActive(show);
                emptySlots[i].transform.SetAsLastSibling();
                emptySlots[i].SetInteractable(editable);
            }

            loadoutText.text = Rules.Count + " / " + slots + " cards   -   checked left to right, the first true card fires";
        }

        void AddRule()
        {
            if (!editable || Level == null || Rules.Count >= Level.Slots) return;
            Rules.Add(new Rule(Level.Conditions[0], false, Level.Actions[0]));
            RebuildCards();
        }

        void Edit(int index, Action<Rule> change)
        {
            if (!editable) return;
            change(Rules[index]);
            RebuildCards();
        }

        void Move(int index, int delta)
        {
            int j = index + delta;
            if (!editable || j < 0 || j >= Rules.Count) return;
            (Rules[index], Rules[j]) = (Rules[j], Rules[index]);
            RebuildCards();
        }

        void OnPickCondition(RuleCardView card)
        {
            if (!editable) return;
            var rule = Rules[card.Index];
            picker.ShowConditions("IF ...", "What should card " + (card.Index + 1) + " look for?", Level.Conditions, rule.Negate,
                rule.Condition, c => Edit(card.Index, r => r.Condition = c));
        }

        void OnPickAction(RuleCardView card)
        {
            if (!editable) return;
            picker.ShowActions("THEN ...", "What should card " + (card.Index + 1) + " do?", Level.Actions,
                Rules[card.Index].Action, a => Edit(card.Index, r => r.Action = a));
        }

        // ---------- Run feedback ----------

        public void SetQi(int qi, int max)
        {
            qiText.text = qi + " / " + max;
            int total = Mathf.Min(Mathf.Max(max, qi), MaxPips);
            while (pipImages.Count < total)
            {
                var pip = UIKit.Rect(pipGrid, "Pip", Vector2.zero, Vector2.one);
                var img = pip.gameObject.AddComponent<Image>();
                img.sprite = UISprites.Circle;
                img.raycastTarget = false;
                pipImages.Add(img);
            }
            for (int i = 0; i < pipImages.Count; i++)
            {
                pipImages[i].gameObject.SetActive(i < total);
                pipImages[i].color = i < qi ? Theme.Qi : Theme.Raised;
            }
        }

        /// <summary>Lifts the card that fired this step; a card that lacked qi glows red.</summary>
        public void HighlightRule(int fired, int starved)
        {
            for (int i = 0; i < Rules.Count && i < cards.Count; i++)
                cards[i].SetState(i == fired, i == starved);
        }

        public void SetHits(int[] hits)
        {
            for (int i = 0; i < Rules.Count && i < cards.Count; i++)
                cards[i].SetHits(hits != null && i < hits.Length ? hits[i] : 0);
        }
    }
}
