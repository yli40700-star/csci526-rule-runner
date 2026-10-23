using System;
using RuleRunner.Core;
using RuleRunner.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RuleRunner.Cards
{
    /// <summary>
    /// A rule as a "skill card" the runner equips:
    ///   header band (action color): priority number, hit counter, qi cost pips
    ///   IF   section: NOT chip + condition diagram + name   (click to change the condition)
    ///   THEN section: action diagram + name                  (click to change the action)
    ///   footer: move left / remove / move right
    /// In AND levels the card is taller: each condition has its own diagram, NOT button and name, with
    /// "+ AND" under the first one, or an "AND x" divider and the second condition on an AND card.
    /// The owner (GameScreen) holds the rule list; the card only raises events.
    /// </summary>
    public class RuleCardView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public static readonly Vector2 CardSize = new Vector2(156, 226);
        /// <summary>Card size in AND levels: room for two full conditions, close to the usual card's proportions.</summary>
        public static readonly Vector2 TallCardSize = new Vector2(184, 276);
        /// <summary>Diagrams on the tall card are drawn smaller so two conditions and the action fit.</summary>
        const float TallIconScale = 0.72f;
        public static Vector2 SizeFor(bool tall) => tall ? TallCardSize : CardSize;

        public event Action<RuleCardView> ToggleNot, PickCondition, PickAction, MoveLeft, MoveRight, Remove;
        /// <summary>AND cards: + AND / x, the second NOT button and the second condition.</summary>
        public event Action<RuleCardView> ToggleAnd, ToggleNot2, PickCondition2;
        /// <summary>Pointer entered / left the card (used for the "where will this fire" preview).</summary>
        public event Action<RuleCardView, bool> Hover;

        public void OnPointerEnter(PointerEventData e) => Hover?.Invoke(this, true);
        public void OnPointerExit(PointerEventData e) => Hover?.Invoke(this, false);

        public RectTransform IfArea => (RectTransform)(tall ? cond1.Area : condArea).transform;
        public RectTransform NotChip => (tall ? cond1.NotChip : notChip).rectTransform;
        public RectTransform AndChip => (RectTransform)addAndButton.transform;

        /// <summary>When true, an unpressed NOT chip gently blinks (until the player has tried NOT once).</summary>
        public bool PulseNot { get; set; }
        bool negated, notVisible;
        public RectTransform Body => body;

        RectTransform body, condIcon, actionIcon, pips;
        Image bodyImage, band;
        Outline glow;
        Text priorityText, hitsText, condText, actionText, notText;
        Image notChip;
        Button notButton, condArea, actionArea, leftButton, rightButton, removeButton;
        GameObject classic;

        // Tall layout (AND levels).
        bool tall, sized;
        GameObject tallRoot;
        TallCondition cond1, cond2;
        Button addAndButton, tallActionArea;
        RectTransform andDivider, tallActionIcon;
        Text tallActionText, costText;
        float lift, liftTarget;

        public int Index { get; private set; }

        public static RuleCardView Create(Transform parent)
        {
            var root = UIKit.Rect(parent, "SkillCard", Vector2.zero, Vector2.one);
            UIKit.Layout(root, CardSize.x, CardSize.y);
            var card = root.gameObject.AddComponent<RuleCardView>();

            // Body moves up when the card fires, so it lives in its own rect.
            card.bodyImage = UIKit.FillBox(root, "Body", Theme.Surface2, 14f);
            card.body = card.bodyImage.rectTransform;
            UIKit.DropShadow(card.bodyImage, 5f, 0.5f);
            card.glow = card.bodyImage.gameObject.AddComponent<Outline>();
            card.glow.effectDistance = new Vector2(3f, -3f);
            card.glow.enabled = false;

            // Header band.
            var bandRt = UIKit.TopStrip(card.body, "Band", 6f, 30f, 6f, 6f);
            card.band = UIKit.Round(bandRt.gameObject.AddComponent<Image>(), 9f);
            var badge = UIKit.Shape(bandRt, "Badge", UISprites.Circle, new Color(0, 0, 0, 0.35f), Vector2.zero, new Vector2(24, 24));
            var badgeRt = badge.rectTransform;
            badgeRt.anchorMin = badgeRt.anchorMax = new Vector2(0f, 0.5f);
            badgeRt.anchoredPosition = new Vector2(17f, 0f);
            card.priorityText = UIKit.Label(badgeRt, "N", "1", 15, TextAnchor.MiddleCenter, Theme.Text, FontStyle.Bold);
            card.hitsText = UIKit.Label(bandRt, "Hits", "", 15, TextAnchor.MiddleCenter, Theme.Text, FontStyle.Bold);
            card.pips = UIKit.Rect(bandRt, "Cost", new Vector2(1, 0), new Vector2(1, 1), new Vector2(-62, 0), new Vector2(-8, 0));
            UIKit.HRow(card.pips, 3, null, false, TextAnchor.MiddleRight);

            BuildClassic(card);
            BuildTall(card);

            // Footer: < remove >
            var footer = UIKit.Rect(card.body, "Footer", new Vector2(0, 0), new Vector2(1, 0), new Vector2(8, 6), new Vector2(-8, 26));
            card.leftButton = UIKit.Button(footer, "Left", "<", 14, Theme.Raised, () => card.MoveLeft?.Invoke(card), 8f);
            SetH(card.leftButton.transform, 0f, 28f);
            card.rightButton = UIKit.Button(footer, "Right", ">", 14, Theme.Raised, () => card.MoveRight?.Invoke(card), 8f);
            SetH(card.rightButton.transform, 1f, 28f);
            card.removeButton = UIKit.Button(footer, "Remove", "remove", 12, Theme.Raised, () => card.Remove?.Invoke(card), 8f, FontStyle.Normal);
            var removeRt = (RectTransform)card.removeButton.transform;
            removeRt.anchorMin = new Vector2(0.5f, 0f);
            removeRt.anchorMax = new Vector2(0.5f, 1f);
            removeRt.sizeDelta = new Vector2(64f, 0f);
            UIKit.ButtonText(card.removeButton).color = Theme.Dim;
            return card;
        }

        /// <summary>The one-condition card used outside AND levels: NOT chip in the IF row, diagram, name.</summary>
        static void BuildClassic(RuleCardView card)
        {
            var root = UIKit.Fill(card.body, "Classic");
            card.classic = root.gameObject;

            // IF section.
            var ifRt = UIKit.TopStrip(root, "If", 40f, 80f, 6f, 6f);
            card.condArea = UIKit.HitArea(ifRt, () => card.PickCondition?.Invoke(card), new Color(1, 1, 1, 0.07f));
            var ifLabel = UIKit.TopStrip(ifRt, "IfLabel", 4f, 18f, 8f, 8f);
            UIKit.Label(ifLabel, "Text", "IF", 14, TextAnchor.MiddleLeft, Theme.Dim, FontStyle.Bold);
            card.notChip = UIKit.Box(ifLabel, "Not", Theme.Raised, 9f, new Vector2(1, 0), new Vector2(1, 1), new Vector2(-46, 0), Vector2.zero);
            card.notButton = card.notChip.gameObject.AddComponent<Button>();
            card.notButton.targetGraphic = card.notChip;
            card.notButton.onClick.AddListener(() => card.ToggleNot?.Invoke(card));
            card.notText = UIKit.Label(card.notChip.transform, "Text", "NOT", 12, TextAnchor.MiddleCenter, Theme.Dim, FontStyle.Bold);
            NotTooltip.Attach(card.notChip.rectTransform);
            card.condIcon = UIKit.Centered(ifRt, "Icon", new Vector2(0, 2f), Icons.Size);
            var condLabel = UIKit.Rect(ifRt, "Name", new Vector2(0, 0), new Vector2(1, 0), new Vector2(4, 2), new Vector2(-4, 20));
            card.condText = UIKit.Label(condLabel, "Text", "", 14, TextAnchor.MiddleCenter);

            // Divider.
            UIKit.Panel(root, "Divider", Theme.Line, new Vector2(0, 1), new Vector2(1, 1), new Vector2(16, -124), new Vector2(-16, -123));

            // THEN section.
            var thenRt = UIKit.TopStrip(root, "Then", 126f, 70f, 6f, 6f);
            card.actionArea = UIKit.HitArea(thenRt, () => card.PickAction?.Invoke(card), new Color(1, 1, 1, 0.07f));
            UIKit.Label(UIKit.TopStrip(thenRt, "ThenLabel", 2f, 16f, 8f, 8f), "Text", "THEN", 14, TextAnchor.MiddleLeft, Theme.Dim, FontStyle.Bold);
            card.actionIcon = UIKit.Centered(thenRt, "Icon", new Vector2(0, 3f), Icons.Size);
            var actLabel = UIKit.TopStrip(root, "ActionName", 180f, 18f, 6f, 6f);
            card.actionText = UIKit.Label(actLabel, "Text", "", 15, TextAnchor.MiddleCenter, Theme.Text, FontStyle.Bold);
        }

        /// <summary>
        /// The AND-level card: first condition, then "+ AND" or (on an AND card) an "AND x" divider and the
        /// second condition, then the action with its cost spelled out ("3 qi + 1 for AND").
        /// </summary>
        static void BuildTall(RuleCardView card)
        {
            var root = UIKit.Fill(card.body, "Tall");
            card.tallRoot = root.gameObject;
            card.cond1 = TallCondition.Build(root, "If1", 38f, "IF", () => card.PickCondition?.Invoke(card), () => card.ToggleNot?.Invoke(card));

            var addRt = UIKit.TopStrip(root, "AddAnd", 97f, 20f, 56f, 56f);
            card.addAndButton = UIKit.Button(addRt, "Button", "+ AND", 12, Theme.Raised, () => card.ToggleAnd?.Invoke(card), 8f);
            UIKit.ButtonText(card.addAndButton).color = Theme.Dim;

            // Divider: line, AND, x (back to one condition), line.
            card.andDivider = UIKit.TopStrip(root, "AndDivider", 96f, 20f, 10f, 10f);
            UIKit.Panel(card.andDivider, "LineL", Theme.Line, new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -0.5f), new Vector2(-36, 0.5f));
            UIKit.Panel(card.andDivider, "LineR", Theme.Line, new Vector2(0.5f, 0.5f), new Vector2(1, 0.5f), new Vector2(38, -0.5f), new Vector2(0, 0.5f));
            UIKit.Label(UIKit.Centered(card.andDivider, "And", new Vector2(-12, 0), new Vector2(42, 20)), "Text", "AND", 14,
                TextAnchor.MiddleCenter, Theme.Gold, FontStyle.Bold);
            var x = UIKit.Button(UIKit.Centered(card.andDivider, "Remove", new Vector2(22, 0), new Vector2(22, 20)), "Button", "×", 15,
                Theme.Raised, () => card.ToggleAnd?.Invoke(card), 6f);
            UIKit.ButtonText(x).color = Theme.Dim;

            card.cond2 = TallCondition.Build(root, "If2", 117f, null, () => card.PickCondition2?.Invoke(card), () => card.ToggleNot2?.Invoke(card));

            // THEN sits on the bottom, so the actions line up across a row of cards.
            UIKit.Panel(root, "Divider", Theme.Line, new Vector2(0, 0), new Vector2(1, 0), new Vector2(16, 100), new Vector2(-16, 101));
            var thenRt = UIKit.Rect(root, "Then", new Vector2(0, 0), new Vector2(1, 0), new Vector2(6, 28), new Vector2(-6, 98));
            card.tallActionArea = UIKit.HitArea(thenRt, () => card.PickAction?.Invoke(card), new Color(1, 1, 1, 0.07f));
            UIKit.Label(UIKit.TopStrip(thenRt, "Label", 2f, 14f, 6f, 6f), "Text", "THEN", 12, TextAnchor.MiddleLeft, Theme.Dim, FontStyle.Bold);
            card.tallActionIcon = TopCentered(thenRt, "Icon", 2f, Icons.Size, TallIconScale);
            card.tallActionText = UIKit.Label(UIKit.TopStrip(thenRt, "Name", 35f, 17f), "Text", "", 14, TextAnchor.MiddleCenter, Theme.Text, FontStyle.Bold);
            card.costText = UIKit.Label(UIKit.TopStrip(thenRt, "Cost", 52f, 14f), "Text", "", 11, TextAnchor.MiddleCenter, Theme.Dim);
            card.costText.supportRichText = true;
            root.gameObject.SetActive(false);
        }

        static RectTransform TopCentered(RectTransform parent, string name, float top, Vector2 size, float scale = 1f)
        {
            var rt = UIKit.Rect(parent, name, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = size;
            rt.anchoredPosition = new Vector2(0f, -top);
            rt.localScale = new Vector3(scale, scale, 1f);
            return rt;
        }

        /// <summary>Anchors a footer button to the left (0) or right (1) edge with a fixed width.</summary>
        static void SetH(Transform t, float side, float width)
        {
            var rt = (RectTransform)t;
            rt.anchorMin = new Vector2(side, 0f);
            rt.anchorMax = new Vector2(side, 1f);
            rt.pivot = new Vector2(side, 0.5f);
            rt.sizeDelta = new Vector2(width, 0f);
            rt.anchoredPosition = Vector2.zero;
        }

        public void Bind(int index, Rule rule, bool isFirst, bool isLast, bool editable, bool allowNot = true, bool allowAnd = false)
        {
            SetTall(allowAnd);
            negated = rule.Negate;
            notVisible = allowNot;
            Index = index;
            priorityText.text = (index + 1).ToString();
            band.color = Theme.ActionColor(rule.Action);
            BuildPips(Vocabulary.Cost(rule.Action), rule.And ? Vocabulary.AndExtraQi : 0);
            if (tall) BindTall(rule, editable, allowNot);
            else BindClassic(rule, editable, allowNot);

            leftButton.interactable = editable && !isFirst;
            rightButton.interactable = editable && !isLast;
            removeButton.interactable = editable;
        }

        void SetTall(bool on)
        {
            if (sized && tall == on) return;
            sized = true;
            tall = on;
            classic.SetActive(!on);
            tallRoot.SetActive(on);
            var size = SizeFor(on);
            UIKit.Layout(this, size.x, size.y);
        }

        void BindClassic(Rule rule, bool editable, bool allowNot)
        {
            notChip.gameObject.SetActive(allowNot);
            // Reads like a toggle: "+NOT" (off, outlined) / "NOT" (on, red).
            notChip.color = rule.Negate ? Theme.Not : Theme.Raised;
            notText.text = rule.Negate ? "NOT" : "+NOT";
            notText.color = rule.Negate ? Theme.Text : Theme.Dim;
            condText.text = (rule.Negate ? "NOT " : "") + Vocabulary.Display(rule.Condition);
            condText.color = rule.Negate ? Theme.Hex(0xffb3a3) : Theme.Text;
            Icons.DrawCondition(condIcon, rule.Condition, rule.Negate);

            actionText.text = Vocabulary.Display(rule.Action);
            Icons.DrawAction(actionIcon, rule.Action);

            notButton.interactable = editable;
            condArea.interactable = editable;
            actionArea.interactable = editable;
        }

        void BindTall(Rule rule, bool editable, bool allowNot)
        {
            bool and = rule.And;
            cond1.Bind(rule.Condition, rule.Negate, allowNot, editable);
            addAndButton.gameObject.SetActive(!and);
            addAndButton.interactable = editable;
            andDivider.gameObject.SetActive(and);
            foreach (var b in andDivider.GetComponentsInChildren<Button>()) b.interactable = editable;
            cond2.Area.gameObject.SetActive(and);
            if (and) cond2.Bind(rule.Condition2, rule.Negate2, allowNot, editable);

            Icons.DrawAction(tallActionIcon, rule.Action);
            tallActionText.text = Vocabulary.Display(rule.Action);
            int cost = Vocabulary.Cost(rule.Action);
            costText.text = (cost == 0 ? "free" : cost + " qi") +
                (and ? "  <color=#F5C542><b>+ " + Vocabulary.AndExtraQi + " for AND</b></color>" : "");
            tallActionArea.interactable = editable;
        }

        /// <summary>One pip per qi a firing costs: the action's in qi color, AND's extra in gold.</summary>
        void BuildPips(int cost, int andExtra)
        {
            UIKit.DestroyChildren(pips);
            int total = cost + andExtra;
            if (total == 0)
            {
                var free = UIKit.Label(pips, "Free", "FREE", 12, TextAnchor.MiddleRight, Theme.Text, FontStyle.Bold);
                UIKit.Layout(free, 40f, 20f);
                return;
            }
            // Four pips (an AND card's Big Jump) shrink a little so they stay clear of the hit counter.
            float size = total > 3 ? 9f : 11f;
            for (int i = 0; i < total; i++)
            {
                var pip = UIKit.Rect(pips, "Pip", Vector2.zero, Vector2.one);
                var img = pip.gameObject.AddComponent<Image>();
                img.sprite = UISprites.Circle;
                img.color = i < cost ? Theme.Qi : Theme.Gold;
                img.raycastTarget = false;
                pip.gameObject.AddComponent<Outline>().effectColor = new Color(0, 0, 0, 0.35f);
                UIKit.Layout(pip, size, size);
            }
        }

        public void SetHits(int hits) => hitsText.text = hits > 0 ? "x" + hits : "";

        /// <summary>Firing cards lift and glow gold; a card that lacked qi glows red.</summary>
        public void SetState(bool firing, bool starved = false)
        {
            glow.enabled = firing || starved;
            glow.effectColor = starved ? Theme.Danger : Theme.Gold;
            liftTarget = firing ? 12f : starved ? 4f : 0f;
            bodyImage.color = starved ? Theme.Hex(0x3a2a30) : Theme.Surface2;
        }

        void Update()
        {
            var chip = tall ? cond1.NotChip : notChip;
            var text = tall ? cond1.NotText : notText;
            var button = tall ? cond1.Not : notButton;
            if (PulseNot && notVisible && !negated && button.interactable)
            {
                float k = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 5f);
                chip.color = Color.Lerp(Theme.Raised, Theme.Not, k * 0.8f);
                text.color = Color.Lerp(Theme.Dim, Theme.Text, k);
            }
            if (Mathf.Abs(lift - liftTarget) < 0.05f) return;
            lift = Mathf.Lerp(lift, liftTarget, 1f - Mathf.Exp(-Time.deltaTime * 18f));
            body.offsetMin = new Vector2(0f, lift);
            body.offsetMax = new Vector2(0f, lift);
        }

        public static string ActionLabel(ActionType a)
        {
            int cost = Vocabulary.Cost(a);
            return Vocabulary.Display(a) + (cost == 0 ? "  (free)" : "  (-" + cost + " qi)");
        }

        /// <summary>One condition on a tall card: diagram, then the NOT button and the name. Click it to change the condition.</summary>
        sealed class TallCondition
        {
            public Button Area, Not;
            public Image NotChip;
            public Text NotText, Name;
            RectTransform icon;

            public static TallCondition Build(RectTransform parent, string name, float top, string label, Action pick, Action toggleNot)
            {
                var c = new TallCondition();
                var rt = UIKit.TopStrip(parent, name, top, 56f, 6f, 6f);
                c.Area = UIKit.HitArea(rt, () => pick(), new Color(1, 1, 1, 0.07f));
                if (label != null)
                    UIKit.Label(UIKit.TopStrip(rt, "Label", 2f, 14f, 6f, 6f), "Text", label, 12, TextAnchor.MiddleLeft, Theme.Dim, FontStyle.Bold);
                c.icon = TopCentered(rt, "Icon", 2f, Icons.Size, TallIconScale);

                var row = UIKit.TopStrip(rt, "Row", 34f, 21f, 4f, 4f);
                c.NotChip = UIKit.Box(row, "Not", Theme.Raised, 7f, Vector2.zero, new Vector2(0, 1), Vector2.zero, new Vector2(40, 0));
                c.Not = c.NotChip.gameObject.AddComponent<Button>();
                c.Not.targetGraphic = c.NotChip;
                c.Not.onClick.AddListener(() => toggleNot());
                c.NotText = UIKit.Label(c.NotChip.transform, "Text", "+NOT", 11, TextAnchor.MiddleCenter, Theme.Dim, FontStyle.Bold);
                NotTooltip.Attach(c.NotChip.rectTransform);
                c.Name = UIKit.Label(UIKit.Rect(row, "Name", Vector2.zero, Vector2.one, new Vector2(46, 0), Vector2.zero), "Text", "", 13,
                    TextAnchor.MiddleLeft, Theme.Text, FontStyle.Bold);
                c.Name.horizontalOverflow = HorizontalWrapMode.Overflow;
                return c;
            }

            public void Bind(Condition condition, bool negate, bool allowNot, bool editable)
            {
                Icons.DrawCondition(icon, condition, negate);
                NotChip.gameObject.SetActive(allowNot);
                NotChip.color = negate ? Theme.Not : Theme.Raised;
                NotText.text = negate ? "NOT" : "+NOT";
                NotText.color = negate ? Theme.Text : Theme.Dim;
                // The red NOT button already says it, so the name only turns red (no "NOT " prefix).
                Name.text = Vocabulary.Display(condition);
                Name.color = negate ? Theme.Hex(0xffb3a3) : Theme.Text;
                Area.interactable = editable;
                Not.interactable = editable;
            }
        }
    }

    /// <summary>Hover tooltip on the NOT chip explaining what it does.</summary>
    public class NotTooltip : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        GameObject tip;

        public static void Attach(RectTransform chip)
        {
            var t = chip.gameObject.AddComponent<NotTooltip>();
            var box = UIKit.Box(chip, "Tooltip", Theme.Background.WithAlpha(0.96f), 8f, new Vector2(1, 1), new Vector2(1, 1));
            box.raycastTarget = false;
            var rt = box.rectTransform;
            rt.pivot = new Vector2(1, 0);
            rt.sizeDelta = new Vector2(206, 46);
            rt.anchoredPosition = new Vector2(8, 6);
            box.gameObject.AddComponent<Outline>().effectColor = Theme.Not.WithAlpha(0.7f);
            UIKit.Label(UIKit.Fill(rt, "Text", 6f), "Text", "NOT flips the condition:\nthe card fires when it is FALSE.", 12,
                TextAnchor.MiddleCenter, Theme.Text);
            t.tip = box.gameObject;
            t.tip.SetActive(false);
        }

        public void OnPointerEnter(PointerEventData e)
        {
            tip.SetActive(true);
            tip.transform.SetAsLastSibling();
        }

        public void OnPointerExit(PointerEventData e) => tip.SetActive(false);
        void OnDisable() { if (tip) tip.SetActive(false); }
    }

    /// <summary>An unused card slot: dashed-looking outline with "+ Equip card".</summary>
    public class EmptySlotView : MonoBehaviour
    {
        Button button;

        public static EmptySlotView Create(Transform parent, Action onClick)
        {
            var root = UIKit.Rect(parent, "EmptySlot", Vector2.zero, Vector2.one);
            UIKit.Layout(root, RuleCardView.CardSize.x, RuleCardView.CardSize.y);
            var view = root.gameObject.AddComponent<EmptySlotView>();
            var outline = UIKit.OutlineBox(root, "Outline", Theme.Line, 14f);
            view.button = root.gameObject.AddComponent<Button>();
            view.button.targetGraphic = outline;
            var colors = view.button.colors;
            colors.highlightedColor = new Color(1.6f, 1.6f, 1.6f, 1f);
            colors.disabledColor = new Color(1, 1, 1, 0.35f);
            view.button.colors = colors;
            view.button.onClick.AddListener(() => onClick());
            UIKit.Label(UIKit.Centered(root, "Plus", new Vector2(0, 16), new Vector2(60, 50)), "Text", "+", 40, TextAnchor.MiddleCenter, Theme.Dim);
            UIKit.Label(UIKit.Centered(root, "Hint", new Vector2(0, -24), new Vector2(140, 24)), "Text", "Equip card", 15, TextAnchor.MiddleCenter, Theme.Dim);
            return view;
        }

        public void SetInteractable(bool on) => button.interactable = on;

        /// <summary>Matches the card size of the level (taller in AND levels).</summary>
        public void SetTall(bool tall)
        {
            var size = RuleCardView.SizeFor(tall);
            UIKit.Layout(this, size.x, size.y);
        }
    }
}
