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
    /// The runner's loadout tray: runner portrait with the Energy pool, then the equipped skill cards
    /// (checked left to right) and empty slots up to the level's limit. Owns the rule list being edited.
    /// </summary>
    public class LoadoutPanel : MonoBehaviour
    {
        const int MaxPips = 24;

        public List<Rule> Rules { get; private set; } = new List<Rule>();
        /// <summary>The runner portrait and Energy pool panel.</summary>
        public RectTransform RunnerPanel { get; private set; }
        public LevelDef Level { get; private set; }

        /// <summary>Raised whenever the player changes the cards (add, edit, move, remove).</summary>
        public event Action Changed;
        /// <summary>Card index under the pointer, or -1 when the pointer leaves the cards.</summary>
        public event Action<int> CardHover;
        /// <summary>The player pressed a NOT chip.</summary>
        public event Action NotToggled;
        /// <summary>The player pressed a +AND / AND chip.</summary>
        public event Action AndToggled;
        /// <summary>Which part of a card the player just used: "slot" (equipped a card), "if", "not", "then" or "move".</summary>
        public event Action<string> PartUsed;
        bool pulseNot;

        Text loadoutText, qiText, defaultText;
        RectTransform cardRow, pipGrid, qiBlock;
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
            panel.defaultText = UIKit.Label(head, "Default", "No card fires -> Walk (free)", 14, TextAnchor.MiddleRight, Theme.Faint);

            panel.cardRow = UIKit.Rect(tray.transform, "Row", Vector2.zero, Vector2.one, new Vector2(14, 12), new Vector2(-14, -40));
            // Top-aligned, so an AND card grows downward instead of pushing its row up.
            UIKit.HRow(panel.cardRow, 12, null, false, TextAnchor.UpperLeft);
            panel.BuildRunnerPanel(panel.cardRow);
            return panel;
        }

        void BuildRunnerPanel(RectTransform row)
        {
            var panel = UIKit.Box(row, "Runner", Theme.Surface2, 14f, Vector2.zero, Vector2.one);
            RunnerPanel = panel.rectTransform;
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
            // The Energy block (label, value, pips) has its own rect so it can be lit up when Energy is spent.
            qiBlock = UIKit.TopStrip(p, "Energy", 124f, 102f, 6f, 6f);
            qiBlock.pivot = new Vector2(0.5f, 0.75f);
            var glow = UIKit.FillBox(qiBlock, "Glow", Theme.Qi.WithAlpha(0f), 10f);
            glow.raycastTarget = false;
            UIKit.Label(UIKit.TopStrip(qiBlock, "QiLabel", 4f, 16f, 6f, 6f), "Text", "ENERGY", 12, TextAnchor.MiddleCenter, Theme.Dim, FontStyle.Bold);
            qiText = UIKit.Label(UIKit.TopStrip(qiBlock, "QiValue", 21f, 24f, 6f, 6f), "Text", "0", 22, TextAnchor.MiddleCenter, Theme.Qi, FontStyle.Bold);

            pipGrid = UIKit.TopStrip(qiBlock, "Pips", 52f, 46f, 6f, 6f);
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
            // The Energy pool only shows once Energy has been taught (the last tutorial level).
            RunnerPanel.gameObject.SetActive(!level.HideQi);
            SetEditable(true);
        }

        /// <summary>Cards can be changed (not just moved) in this level.</summary>
        bool CanEditCards => editable && Level != null;
        /// <summary>IF and NOT can be changed, and cards added or removed (not in the card-order lesson).</summary>
        bool CanEditConditions => CanEditCards && !Level.LockConditions;

        /// <summary>Takes every card away (the first tutorial level, after its cards have played once by themselves).</summary>
        public void ClearRules()
        {
            Rules.Clear();
            RebuildCards();
            Changed?.Invoke();
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
            bool tall = Level != null && Level.AllowAnd;
            UIKit.Layout(RunnerPanel, 138, RuleCardView.SizeFor(tall).y);
            while (cards.Count < Rules.Count)
            {
                var card = RuleCardView.Create(cardRow);
                card.ToggleNot += c => { if (!CanEditConditions) return; NotToggled?.Invoke(); PartUsed?.Invoke("not"); Edit(c.Index, r => r.Negate = !r.Negate); };
                card.ToggleAnd += c => { AndToggled?.Invoke(); Edit(c.Index, ToggleAnd); };
                card.ToggleNot2 += c => { NotToggled?.Invoke(); Edit(c.Index, r => r.Negate2 = !r.Negate2); };
                card.PickCondition2 += OnPickCondition2;
                card.PickCondition += OnPickCondition;
                card.PickAction += OnPickAction;
                card.MoveLeft += c => Move(c.Index, -1);
                card.MoveRight += c => Move(c.Index, +1);
                card.Remove += c => { if (CanEditConditions) { Rules.RemoveAt(c.Index); RebuildCards(); Changed?.Invoke(); } };
                card.Hover += (c, inside) => { if (editable) CardHover?.Invoke(inside ? c.Index : -1); };
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
                cards[i].Bind(i, Rules[i], i == 0, i == Rules.Count - 1, editable, Level == null || Level.AllowNot, tall,
                    Level != null && Level.LockConditions);
                cards[i].PulseNot = pulseNot;
                cards[i].SetState(false);
                cards[i].SetHits(0);
            }
            for (int i = 0; i < emptySlots.Count; i++)
            {
                bool show = i < slots - Rules.Count;
                emptySlots[i].gameObject.SetActive(show);
                emptySlots[i].transform.SetAsLastSibling();
                emptySlots[i].SetInteractable(editable && (Level == null || !Level.LockConditions));
                emptySlots[i].SetTall(tall);
            }

            loadoutText.text = Rules.Count + " / " + slots + " cards   -   checked left to right, the first true card acts";
        }

        void AddRule()
        {
            if (!CanEditConditions || Rules.Count >= Level.Slots) return;
            Rules.Add(new Rule(Level.Conditions[0], false, Level.Actions[0]));
            PartUsed?.Invoke("slot");
            RebuildCards();
            Changed?.Invoke();
        }

        void Edit(int index, Action<Rule> change)
        {
            if (!CanEditCards) return;
            change(Rules[index]);
            RebuildCards();
            Changed?.Invoke();
        }

        /// <summary>Turns a card into an AND card (second condition: the first other one the level offers) or back.</summary>
        void ToggleAnd(Rule r)
        {
            if (r.And)
            {
                r.And = false;
                return;
            }
            r.And = true;
            r.Negate2 = false;
            r.Condition2 = r.Condition;
            foreach (var c in Level.Conditions)
                if (c != r.Condition) { r.Condition2 = c; break; }
        }

        void Move(int index, int delta)
        {
            int j = index + delta;
            if (!editable || j < 0 || j >= Rules.Count) return;
            (Rules[index], Rules[j]) = (Rules[j], Rules[index]);
            PartUsed?.Invoke("move");
            RebuildCards();
            Changed?.Invoke();
        }

        // ---------- Targets for the tutorial ----------

        /// <summary>The first visible empty slot, or null.</summary>
        public RectTransform FirstEmptySlot
        {
            get
            {
                foreach (var e in emptySlots)
                    if (e.gameObject.activeSelf) return (RectTransform)e.transform;
                return null;
            }
        }

        /// <summary>Blink unpressed NOT chips (used until the player has tried NOT once).</summary>
        public void SetNotPulse(bool on)
        {
            pulseNot = on;
            foreach (var c in cards) c.PulseNot = on;
            if (!on) RebuildCards();
        }

        public RuleCardView CardAt(int index) => index >= 0 && index < Rules.Count && index < cards.Count ? cards[index] : null;

        void OnPickCondition(RuleCardView card)
        {
            if (!CanEditConditions) return;
            var rule = Rules[card.Index];
            if (!Level.AllowNot && Level.NotOptions.Count > 0)
            {
                // Before the NOT button: "NOT Ground ahead" is an option of its own, next to the plain conditions.
                var options = Level.Conditions.ConvertAll(c => (c, false));
                foreach (var c in Level.NotOptions) options.Add((c, true));
                picker.ShowConditionChoices("IF ...", "What should card " + (card.Index + 1) + " look for?", options, (rule.Condition, rule.Negate),
                    (c, negate) => { PartUsed?.Invoke("if"); Edit(card.Index, r => { r.Condition = c; r.Negate = negate; }); });
                return;
            }
            // On an AND card, picking what the AND line already checks swaps the two, so a card never checks one thing twice.
            picker.ShowConditions("IF ...", "What should card " + (card.Index + 1) + " look for?", Level.Conditions, rule.Negate,
                rule.Condition, c => Edit(card.Index, r =>
                {
                    PartUsed?.Invoke("if");
                    if (r.And && r.Condition2 == c) r.Condition2 = r.Condition;
                    r.Condition = c;
                }));
        }

        void OnPickCondition2(RuleCardView card)
        {
            if (!CanEditConditions) return;
            var rule = Rules[card.Index];
            // "What else": the condition the card already checks is not offered again.
            var others = Level.Conditions.FindAll(c => c != rule.Condition);
            picker.ShowConditions("AND ...", "What else must be true for card " + (card.Index + 1) + " to fire?", others,
                rule.Negate2, rule.Condition2, c => Edit(card.Index, r => r.Condition2 = c));
        }

        void OnPickAction(RuleCardView card)
        {
            if (!CanEditCards) return;
            var rule = Rules[card.Index];
            int extra = rule.And ? Vocabulary.AndExtraQi : 0;
            picker.ShowActions("THEN ...", "What should card " + (card.Index + 1) + " do?" + (rule.And ? "  (AND: 1 extra Energy)" : ""),
                Level.Actions, rule.Action, a => { PartUsed?.Invoke("then"); Edit(card.Index, r => r.Action = a); }, extra, !Level.HideQi);
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

        /// <summary>
        /// Lights up the Energy block for a moment: cyan and a little bigger when Energy is spent,
        /// red when a card needed more than was left.
        /// </summary>
        public void FlashEnergy(bool spent, float seconds)
        {
            if (!RunnerPanel.gameObject.activeInHierarchy) return;
            StopAllCoroutines();
            StartCoroutine(Flash(spent ? Theme.Qi : Theme.Danger, seconds));
        }

        System.Collections.IEnumerator Flash(Color color, float seconds)
        {
            var glow = qiBlock.GetChild(0).GetComponent<Image>();
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                float k = Mathf.Sin(Mathf.Clamp01(t / seconds) * Mathf.PI);
                qiBlock.localScale = Vector3.one * (1f + 0.18f * k);
                glow.color = color.WithAlpha(0.35f * k);
                yield return null;
            }
            qiBlock.localScale = Vector3.one;
            glow.color = color.WithAlpha(0f);
        }

        /// <summary>Lifts the card that fired this step; a card that lacked Energy glows red.</summary>
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
