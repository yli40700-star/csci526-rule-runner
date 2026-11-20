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
    /// Every level uses this one card, the tutorial's too (TutorialScreen draws the same layout). In AND levels a "+AND"
    /// chip sits at the right end of the divider, clear of the condition's name and the THEN label; an AND card shows an
    /// AND line (with "AND x" to undo it) and the second condition (its own NOT chip, diagram and name) under the first,
    /// so it grows downward by AndExtra and the THEN part moves down with it.
    /// The owner (GameScreen) holds the rule list; the card only raises events.
    /// </summary>
    public class RuleCardView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public static readonly Vector2 CardSize = new Vector2(156, 226);
        /// <summary>The AND line and the second condition under the first.</summary>
        public const float AndExtra = 96f;
        /// <summary>An AND card: the same card, AndExtra taller.</summary>
        public static readonly Vector2 AndCardSize = new Vector2(156, 226 + AndExtra);
        /// <summary>Card size: one size in every level; an AND card is taller.</summary>
        public static Vector2 SizeFor(bool tall, bool and = false) => and ? AndCardSize : CardSize;

        public event Action<RuleCardView> ToggleNot, PickCondition, PickAction, MoveLeft, MoveRight, Remove;
        /// <summary>AND cards: +AND / AND x, the second NOT button and the second condition.</summary>
        public event Action<RuleCardView> ToggleAnd, ToggleNot2, PickCondition2;
        /// <summary>Pointer entered / left the card (used for the "where will this fire" preview).</summary>
        public event Action<RuleCardView, bool> Hover;

        public void OnPointerEnter(PointerEventData e) => Hover?.Invoke(this, true);
        public void OnPointerExit(PointerEventData e) => Hover?.Invoke(this, false);

        public RectTransform IfArea => (RectTransform)condArea.transform;
        public RectTransform NotChip => notChip.rectTransform;
        public RectTransform AndChip => (RectTransform)addAndButton.transform;
        public RectTransform ThenArea => (RectTransform)actionArea.transform;
        public RectTransform MoveRightButton => (RectTransform)rightButton.transform;

        /// <summary>When true, an unpressed NOT chip gently blinks (until the player has tried NOT once).</summary>
        public bool PulseNot { get; set; }
        bool negated, notVisible;
        public RectTransform Body => body;

        RectTransform body, condIcon, actionIcon, pips;
        Image bodyImage, band;
        Outline glow;
        Text priorityText, hitsText, condText, actionText, notText;
        Image notChip, notRing, notStrike;
        Button notButton, condArea, actionArea, leftButton, rightButton, removeButton;

        // AND: the chip in the IF row, the AND line, and the second condition.
        Button addAndButton, cond2Area, not2Button;
        Image andChipImage, not2Chip, not2Ring, not2Strike;
        Text andChipText, not2Text, cond2Text;
        RectTransform andRow, cond2Icon, divider, thenRt, actionNameRt;
        bool sized, andSized;
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

            BuildSections(card);

            // Footer: < remove >. The row is big enough to hit easily, and the arrows say what they do on hover.
            var footer = UIKit.Rect(card.body, "Footer", new Vector2(0, 0), new Vector2(1, 0), new Vector2(8, 5), new Vector2(-8, 29));
            card.leftButton = UIKit.Button(footer, "Left", "<", 17, Theme.Raised, () => card.MoveLeft?.Invoke(card), 8f);
            SetH(card.leftButton.transform, 0f, 34f);
            HoverTip.Attach((RectTransform)card.leftButton.transform, "Check this card earlier", 0f);
            card.rightButton = UIKit.Button(footer, "Right", ">", 17, Theme.Raised, () => card.MoveRight?.Invoke(card), 8f);
            SetH(card.rightButton.transform, 1f, 34f);
            HoverTip.Attach((RectTransform)card.rightButton.transform, "Check this card later", 1f);
            card.removeButton = UIKit.Button(footer, "Remove", "remove", 13, Theme.Raised, () => card.Remove?.Invoke(card), 8f, FontStyle.Normal);
            var removeRt = (RectTransform)card.removeButton.transform;
            removeRt.anchorMin = new Vector2(0.5f, 0f);
            removeRt.anchorMax = new Vector2(0.5f, 1f);
            removeRt.sizeDelta = new Vector2(64f, 0f);
            UIKit.ButtonText(card.removeButton).color = Theme.Dim;
            return card;
        }

        /// <summary>IF (with the NOT chip, and "+AND" in AND levels), the AND line and second condition, the divider, THEN.</summary>
        static void BuildSections(RuleCardView card)
        {
            var root = UIKit.Fill(card.body, "Classic");

            // IF section.
            var ifRt = UIKit.TopStrip(root, "If", 40f, 80f, 6f, 6f);
            card.condArea = UIKit.HitArea(ifRt, () => card.PickCondition?.Invoke(card), new Color(1, 1, 1, 0.07f));
            var ifLabel = UIKit.TopStrip(ifRt, "IfLabel", 2f, NotChipHeight, 8f, 8f);
            UIKit.Label(ifLabel, "Text", "IF", 14, TextAnchor.MiddleLeft, Theme.Dim, FontStyle.Bold);
            card.notChip = BuildNotChip(ifLabel, () => card.ToggleNot?.Invoke(card), out card.notButton, out card.notRing, out card.notText, out card.notStrike);
            card.condIcon = UIKit.Centered(ifRt, "Icon", new Vector2(0, 2f), Icons.Size);
            // The name sits a little above the section's bottom edge, so "+AND" on the divider never touches it.
            var condLabel = UIKit.Rect(ifRt, "Name", new Vector2(0, 0), new Vector2(1, 0), new Vector2(4, 5), new Vector2(-4, 23));
            card.condText = UIKit.Label(condLabel, "Text", "", 14, TextAnchor.MiddleCenter);

            // AND line, then the second condition (shown on an AND card).
            card.andRow = UIKit.TopStrip(root, "AndRow", 120f, 16f, 6f, 6f);
            UIKit.Panel(card.andRow, "LineL", Theme.Qi.WithAlpha(0.6f), new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(10, -0.5f), new Vector2(-32, 0.5f));
            UIKit.Panel(card.andRow, "LineR", Theme.Qi.WithAlpha(0.6f), new Vector2(0.5f, 0.5f), new Vector2(1, 0.5f), new Vector2(32, -0.5f), new Vector2(-10, 0.5f));
            var if2Rt = UIKit.TopStrip(root, "If2", 136f, 80f, 6f, 6f);
            card.cond2Area = UIKit.HitArea(if2Rt, () => card.PickCondition2?.Invoke(card), new Color(1, 1, 1, 0.07f));
            var if2Label = UIKit.TopStrip(if2Rt, "If2Label", 2f, NotChipHeight, 8f, 8f);
            UIKit.Label(if2Label, "Text", "AND", 14, TextAnchor.MiddleLeft, Theme.Qi, FontStyle.Bold);
            card.not2Chip = BuildNotChip(if2Label, () => card.ToggleNot2?.Invoke(card), out card.not2Button, out card.not2Ring, out card.not2Text, out card.not2Strike);
            card.cond2Icon = UIKit.Centered(if2Rt, "Icon", new Vector2(0, 2f), Icons.Size);
            card.cond2Text = UIKit.Label(UIKit.Rect(if2Rt, "Name", new Vector2(0, 0), new Vector2(1, 0), new Vector2(4, 2), new Vector2(-4, 20)),
                "Text", "", 14, TextAnchor.MiddleCenter);

            // Divider.
            card.divider = UIKit.Panel(root, "Divider", Theme.Line, new Vector2(0, 1), new Vector2(1, 1), new Vector2(16, -124), new Vector2(-16, -123)).rectTransform;

            // THEN section.
            card.thenRt = UIKit.TopStrip(root, "Then", 126f, 70f, 6f, 6f);
            card.actionArea = UIKit.HitArea(card.thenRt, () => card.PickAction?.Invoke(card), new Color(1, 1, 1, 0.07f));
            UIKit.Label(UIKit.TopStrip(card.thenRt, "ThenLabel", 2f, 16f, 8f, 8f), "Text", "THEN", 14, TextAnchor.MiddleLeft, Theme.Dim, FontStyle.Bold);
            card.actionIcon = UIKit.Centered(card.thenRt, "Icon", new Vector2(0, 3f), Icons.Size);
            card.actionNameRt = UIKit.TopStrip(root, "ActionName", 180f, 18f, 6f, 6f);
            card.actionText = UIKit.Label(card.actionNameRt, "Text", "", 15, TextAnchor.MiddleCenter, Theme.Text, FontStyle.Bold);

            // AND levels: "+AND" sits at the right end of the divider, where a second condition would go; on an AND card
            // it is the "AND x" in the AND line and turns the card back. Built last so it is on top of the IF and THEN
            // click areas.
            card.andChipImage = UIKit.Box(root, "AndChip", Theme.Raised, 10f, new Vector2(0.5f, 1), new Vector2(0.5f, 1));
            card.andChipImage.rectTransform.sizeDelta = AndChipSize;
            card.addAndButton = card.andChipImage.gameObject.AddComponent<Button>();
            card.addAndButton.targetGraphic = card.andChipImage;
            card.addAndButton.onClick.AddListener(() => card.ToggleAnd?.Invoke(card));
            card.andChipText = UIKit.Label(card.andChipImage.transform, "Text", "+AND", 13, TextAnchor.MiddleCenter, Theme.Text, FontStyle.Bold);
        }

        /// <summary>Height of the NOT switch and of the label row it sits in.</summary>
        const float NotChipHeight = 22f;
        /// <summary>"+AND" on the divider (the "AND x" in the AND line is a little lower, to fit that line).</summary>
        static readonly Vector2 AndChipSize = new Vector2(54, 20);

        /// <summary>A NOT switch at the right end of a section's label row (48 x NotChipHeight).</summary>
        static Image BuildNotChip(RectTransform labelRow, Action toggle, out Button button, out Image ring, out Text text, out Image strike)
        {
            var chip = UIKit.Box(labelRow, "Not", Theme.Raised, NotChipHeight / 2f, new Vector2(1, 0), new Vector2(1, 1), new Vector2(-48, 0), Vector2.zero);
            button = chip.gameObject.AddComponent<Button>();
            button.targetGraphic = chip;
            button.onClick.AddListener(() => toggle());
            ring = NotSwitchRing(chip, NotChipHeight / 2f);
            text = UIKit.Label(chip.transform, "Text", "NOT", 13, TextAnchor.MiddleCenter, Theme.Dim, FontStyle.Bold);
            strike = NotSwitchStrike(chip, 34f);
            NotTooltip.Attach(chip.rectTransform);
            return chip;
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

        /// <summary>Moves a top-anchored strip so its top edge is at the given distance from the card's top.</summary>
        static void SetTop(RectTransform rt, float top)
        {
            float h = rt.offsetMax.y - rt.offsetMin.y;
            rt.offsetMax = new Vector2(rt.offsetMax.x, -top);
            rt.offsetMin = new Vector2(rt.offsetMin.x, -top - h);
        }

        /// <param name="lockConditions">IF and NOT are fixed (the card-order lesson): only THEN and the order can change.</param>
        public void Bind(int index, Rule rule, bool isFirst, bool isLast, bool editable, bool allowNot = true, bool allowAnd = false,
            bool lockConditions = false)
        {
            SetAnd(rule.And);
            negated = rule.Negate;
            notVisible = allowNot;
            Index = index;
            priorityText.text = (index + 1).ToString();
            band.color = Theme.ActionColor(rule.Action);
            BuildPips(Vocabulary.Cost(rule.Action), rule.And ? Vocabulary.AndExtraQi : 0);
            BindSections(rule, editable, allowNot, allowAnd, lockConditions);

            leftButton.interactable = editable && !isFirst;
            rightButton.interactable = editable && !isLast;
            removeButton.interactable = editable && !lockConditions;
        }

        void SetAnd(bool and)
        {
            if (sized && andSized == and) return;
            sized = true;
            andSized = and;
            var size = SizeFor(false, and);
            UIKit.Layout(this, size.x, size.y);
        }

        void BindSections(Rule rule, bool editable, bool allowNot, bool allowAnd, bool lockConditions)
        {
            notChip.gameObject.SetActive(allowNot);
            PaintNotSwitch(notChip, notRing, notStrike, notText, rule.Negate);
            condText.text = (rule.Negate ? "NOT " : "") + Vocabulary.Display(rule.Condition);
            condText.color = rule.Negate ? Theme.Hex(0xffb3a3) : Theme.Text;
            Icons.DrawCondition(condIcon, rule.Condition, rule.Negate);
            notButton.interactable = editable && !lockConditions;
            condArea.interactable = editable && !lockConditions;

            // AND levels: the chip in the IF row; an AND card shows the AND line and its second condition.
            bool and = allowAnd && rule.And;
            addAndButton.gameObject.SetActive(allowAnd);
            addAndButton.interactable = editable && !lockConditions;
            andChipImage.color = and ? Theme.Qi : Theme.Raised;
            andChipText.text = and ? "AND x" : "+AND";
            andChipText.color = and ? Theme.Background : Theme.Text;
            // "+AND": right end of the divider (y 114 to 134), under the NOT switch and beside the THEN label.
            // "AND x": centered in the AND line.
            var chipRt = andChipImage.rectTransform;
            chipRt.sizeDelta = and ? new Vector2(AndChipSize.x, 18f) : AndChipSize;
            chipRt.anchoredPosition = and ? new Vector2(0f, -128f) : new Vector2(CardSize.x / 2f - 8f - AndChipSize.x / 2f, -124f);
            andRow.gameObject.SetActive(and);
            cond2Area.gameObject.SetActive(and);
            if (and)
            {
                not2Chip.gameObject.SetActive(allowNot);
                PaintNotSwitch(not2Chip, not2Ring, not2Strike, not2Text, rule.Negate2);
                cond2Text.text = (rule.Negate2 ? "NOT " : "") + Vocabulary.Display(rule.Condition2);
                cond2Text.color = rule.Negate2 ? Theme.Hex(0xffb3a3) : Theme.Text;
                Icons.DrawCondition(cond2Icon, rule.Condition2, rule.Negate2);
                cond2Area.interactable = editable;
                not2Button.interactable = editable;
            }
            float d = and ? AndExtra : 0f;
            SetTop(divider, 123f + d);
            SetTop(thenRt, 126f + d);
            SetTop(actionNameRt, 180f + d);

            actionText.text = Vocabulary.Display(rule.Action);
            Icons.DrawAction(actionIcon, rule.Action);
            actionArea.interactable = editable;
        }

        /// <summary>One pip per Energy a firing costs: the action's in Energy color, AND's extra in gold.</summary>
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

        public void SetHits(int hits) => hitsText.text = hits > 0 && !plain ? "x" + hits : "";

        bool plain;

        /// <summary>Tutorial cards: no hit counter and no remove button; the move arrows only when there is more than one card.</summary>
        public void SetPlain(bool on, bool canMove)
        {
            plain = on;
            removeButton.gameObject.SetActive(!on);
            leftButton.gameObject.SetActive(!on || canMove);
            rightButton.gameObject.SetActive(!on || canMove);
            if (on) hitsText.text = "";
        }

        /// <summary>Firing cards lift and glow gold; a card that lacked Energy glows red.</summary>
        public void SetState(bool firing, bool starved = false)
        {
            glow.enabled = firing || starved;
            glow.effectColor = starved ? Theme.Danger : Theme.Gold;
            liftTarget = firing ? 12f : starved ? 4f : 0f;
            bodyImage.color = starved ? Theme.Hex(0x3a2a30) : Theme.Surface2;
        }

        void Update()
        {
            if (PulseNot && notVisible && !negated && notButton.interactable)
            {
                // The off switch fills with red and back, hinting that it can be turned on.
                float k = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 5f);
                notChip.color = Theme.Not.WithAlpha(k * 0.8f);
                notText.color = Color.Lerp(Theme.Dim, Theme.Text, k);
            }
            if (Mathf.Abs(lift - liftTarget) < 0.05f) return;
            lift = Mathf.Lerp(lift, liftTarget, 1f - Mathf.Exp(-Time.deltaTime * 18f));
            body.offsetMin = new Vector2(0f, lift);
            body.offsetMax = new Vector2(0f, lift);
        }

        /// <summary>The outline of a NOT switch: the same rounded shape as the chip's fill (UIKit.RoundOutline), 1.5 wide.</summary>
        static Image NotSwitchRing(Image chip, float radius)
        {
            var ring = UIKit.RoundOutline(UIKit.Panel(chip.transform, "Ring", Theme.Dim, Vector2.zero, Vector2.one), radius, 1.5f);
            ring.raycastTarget = false;
            return ring;
        }

        /// <summary>The slash across "NOT" while the switch is off. Created after the label so it draws on top of it.</summary>
        static Image NotSwitchStrike(Image chip, float width)
        {
            var strike = UIKit.Shape(chip.transform, "Strike", null, Theme.Dim, Vector2.zero, new Vector2(width, 1.5f));
            strike.raycastTarget = false;
            strike.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 18f);
            return strike;
        }

        /// <summary>
        /// The NOT switch reads like a switch, not a label: off is an outlined pill with "NOT" struck through in grey;
        /// on is a filled red pill with a white "NOT".
        /// </summary>
        static void PaintNotSwitch(Image chip, Image ring, Image strike, Text text, bool on)
        {
            chip.color = on ? Theme.Not : Theme.Not.WithAlpha(0f);
            ring.enabled = !on;
            strike.enabled = !on;
            text.text = "NOT";
            text.color = on ? Color.white : Theme.Dim;
        }

        public static string ActionLabel(ActionType a)
        {
            int cost = Vocabulary.Cost(a);
            return Vocabulary.Display(a) + (cost == 0 ? "  (free)" : "  (-" + cost + " Energy)");
        }
    }

    /// <summary>
    /// A short label that appears above a card control while the pointer is on it (the move arrows). It stays inside
    /// the card's width: <c>side</c> 0 grows to the right from the control's left edge, 1 to the left from its right edge.
    /// </summary>
    public class HoverTip : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        GameObject tip;
        Button button;

        public static void Attach(RectTransform target, string text, float side)
        {
            var t = target.gameObject.AddComponent<HoverTip>();
            t.button = target.GetComponent<Button>();
            var box = UIKit.Box(target, "Tip", Theme.Background.WithAlpha(0.96f), 8f, new Vector2(side, 1), new Vector2(side, 1));
            box.raycastTarget = false;
            box.gameObject.AddComponent<Outline>().effectColor = Theme.Gold.WithAlpha(0.6f);
            var label = UIKit.Label(UIKit.Fill(box.rectTransform, "Text", 4f), "Text", text, 12, TextAnchor.MiddleCenter, Theme.Text);
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            var rt = box.rectTransform;
            rt.pivot = new Vector2(side, 0);
            rt.sizeDelta = new Vector2(label.preferredWidth + 18f, 24f);
            rt.anchoredPosition = new Vector2(0, 5);
            t.tip = box.gameObject;
            t.tip.SetActive(false);
        }

        public void OnPointerEnter(PointerEventData e)
        {
            // An arrow that cannot be used (the first card's "<", any arrow during a run) has nothing to explain.
            if (button != null && !button.interactable) return;
            tip.SetActive(true);
        }

        public void OnPointerExit(PointerEventData e) => tip.SetActive(false);
        void OnDisable() { if (tip) tip.SetActive(false); }
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

        /// <summary>The card size (the same in every level; only an AND card is taller).</summary>
        public void SetTall(bool tall)
        {
            var size = RuleCardView.SizeFor(tall);
            UIKit.Layout(this, size.x, size.y);
        }
    }
}
