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
    ///                 in AND levels a +AND chip; an AND card shows its two conditions as two short lines
    ///   THEN section: action diagram + name                  (click to change the action)
    ///   footer: move left / remove / move right
    /// The owner (GameScreen) holds the rule list; the card only raises events.
    /// </summary>
    public class RuleCardView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public static readonly Vector2 CardSize = new Vector2(156, 226);

        public event Action<RuleCardView> ToggleNot, PickCondition, PickAction, MoveLeft, MoveRight, Remove;
        /// <summary>AND cards: the +AND chip, the second NOT chip and the second condition line.</summary>
        public event Action<RuleCardView> ToggleAnd, ToggleNot2, PickCondition2;
        /// <summary>Pointer entered / left the card (used for the "where will this fire" preview).</summary>
        public event Action<RuleCardView, bool> Hover;

        public void OnPointerEnter(PointerEventData e) => Hover?.Invoke(this, true);
        public void OnPointerExit(PointerEventData e) => Hover?.Invoke(this, false);

        public RectTransform IfArea => (RectTransform)condArea.transform;
        public RectTransform NotChip => notChip.rectTransform;
        public RectTransform AndChip => andChip.rectTransform;

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

        // AND: the chip in the IF row, and the two-line layout that replaces the diagram on an AND card.
        Image andChip, notChip2;
        Text andText, notText2, cond1Line, cond2Line;
        Button andButton, notButton2, cond2Area;
        RectTransform andRows;
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

            // IF section.
            var ifRt = UIKit.TopStrip(card.body, "If", 40f, 80f, 6f, 6f);
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
            BuildAnd(card, ifRt, ifLabel);

            // Divider.
            UIKit.Panel(card.body, "Divider", Theme.Line, new Vector2(0, 1), new Vector2(1, 1), new Vector2(16, -124), new Vector2(-16, -123));

            // THEN section.
            var thenRt = UIKit.TopStrip(card.body, "Then", 126f, 70f, 6f, 6f);
            card.actionArea = UIKit.HitArea(thenRt, () => card.PickAction?.Invoke(card), new Color(1, 1, 1, 0.07f));
            UIKit.Label(UIKit.TopStrip(thenRt, "ThenLabel", 2f, 16f, 8f, 8f), "Text", "THEN", 14, TextAnchor.MiddleLeft, Theme.Dim, FontStyle.Bold);
            card.actionIcon = UIKit.Centered(thenRt, "Icon", new Vector2(0, 3f), Icons.Size);
            var actLabel = UIKit.TopStrip(card.body, "ActionName", 180f, 18f, 6f, 6f);
            card.actionText = UIKit.Label(actLabel, "Text", "", 15, TextAnchor.MiddleCenter, Theme.Text, FontStyle.Bold);

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

        /// <summary>
        /// The +AND chip sits left of the NOT chip. On an AND card the diagram gives way to two short lines:
        /// the first condition (click the IF area to change it) and "AND [NOT] second condition" (click to change it).
        /// </summary>
        static void BuildAnd(RuleCardView card, RectTransform ifRt, RectTransform ifLabel)
        {
            card.andChip = UIKit.Box(ifLabel, "And", Theme.Raised, 9f, new Vector2(1, 0), new Vector2(1, 1), new Vector2(-96, 0), new Vector2(-50, 0));
            card.andButton = card.andChip.gameObject.AddComponent<Button>();
            card.andButton.targetGraphic = card.andChip;
            card.andButton.onClick.AddListener(() => card.ToggleAnd?.Invoke(card));
            card.andText = UIKit.Label(card.andChip.transform, "Text", "+AND", 12, TextAnchor.MiddleCenter, Theme.Dim, FontStyle.Bold);

            card.andRows = UIKit.Rect(ifRt, "AndRows", Vector2.zero, Vector2.one);
            card.cond1Line = UIKit.Label(UIKit.TopStrip(card.andRows, "Cond1", 24f, 18f, 4f, 4f), "Text", "", 13, TextAnchor.MiddleCenter);

            var second = UIKit.TopStrip(card.andRows, "Second", 44f, 36f);
            card.cond2Area = UIKit.HitArea(second, () => card.PickCondition2?.Invoke(card), new Color(1, 1, 1, 0.07f));
            var andLabel = UIKit.TopStrip(second, "AndLabel", 2f, 16f, 8f, 8f);
            UIKit.Label(andLabel, "Text", "AND", 13, TextAnchor.MiddleLeft, Theme.Qi, FontStyle.Bold);
            card.notChip2 = UIKit.Box(andLabel, "Not2", Theme.Raised, 9f, new Vector2(1, 0), new Vector2(1, 1), new Vector2(-46, 0), Vector2.zero);
            card.notButton2 = card.notChip2.gameObject.AddComponent<Button>();
            card.notButton2.targetGraphic = card.notChip2;
            card.notButton2.onClick.AddListener(() => card.ToggleNot2?.Invoke(card));
            card.notText2 = UIKit.Label(card.notChip2.transform, "Text", "+NOT", 12, TextAnchor.MiddleCenter, Theme.Dim, FontStyle.Bold);
            NotTooltip.Attach(card.notChip2.rectTransform);
            card.cond2Line = UIKit.Label(UIKit.TopStrip(second, "Cond2", 18f, 18f, 4f, 4f), "Text", "", 13, TextAnchor.MiddleCenter);
            card.andRows.gameObject.SetActive(false);
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
            negated = rule.Negate;
            notVisible = allowNot;
            notChip.gameObject.SetActive(allowNot);
            Index = index;
            priorityText.text = (index + 1).ToString();
            band.color = Theme.ActionColor(rule.Action);

            // Reads like a toggle: "+NOT" (off, outlined) / "NOT" (on, red).
            notChip.color = rule.Negate ? Theme.Not : Theme.Raised;
            notText.text = rule.Negate ? "NOT" : "+NOT";
            notText.color = rule.Negate ? Theme.Text : Theme.Dim;
            condText.text = (rule.Negate ? "NOT " : "") + Vocabulary.Display(rule.Condition);
            condText.color = rule.Negate ? Theme.Hex(0xffb3a3) : Theme.Text;
            Icons.DrawCondition(condIcon, rule.Condition, rule.Negate);

            actionText.text = Vocabulary.Display(rule.Action);
            Icons.DrawAction(actionIcon, rule.Action);
            BuildPips(rule.Cost);
            BindAnd(rule, editable, allowNot, allowAnd);

            notButton.interactable = editable;
            condArea.interactable = editable;
            actionArea.interactable = editable;
            leftButton.interactable = editable && !isFirst;
            rightButton.interactable = editable && !isLast;
            removeButton.interactable = editable;
        }

        void BindAnd(Rule rule, bool editable, bool allowNot, bool allowAnd)
        {
            bool and = rule.And;
            andChip.gameObject.SetActive(allowAnd || and);
            andChip.color = and ? Theme.Qi : Theme.Raised;
            andText.text = and ? "AND" : "+AND";
            andText.color = and ? Theme.Background : Theme.Dim;
            andButton.interactable = editable;

            condIcon.gameObject.SetActive(!and);
            condText.gameObject.SetActive(!and);
            andRows.gameObject.SetActive(and);
            if (!and) return;

            cond1Line.text = (rule.Negate ? "NOT " : "") + Vocabulary.Display(rule.Condition);
            cond1Line.color = rule.Negate ? Theme.Hex(0xffb3a3) : Theme.Text;
            cond2Line.text = (rule.Negate2 ? "NOT " : "") + Vocabulary.Display(rule.Condition2);
            cond2Line.color = rule.Negate2 ? Theme.Hex(0xffb3a3) : Theme.Text;
            notChip2.gameObject.SetActive(allowNot);
            notChip2.color = rule.Negate2 ? Theme.Not : Theme.Raised;
            notText2.text = rule.Negate2 ? "NOT" : "+NOT";
            notText2.color = rule.Negate2 ? Theme.Text : Theme.Dim;
            notButton2.interactable = editable;
            cond2Area.interactable = editable;
        }

        void BuildPips(int cost)
        {
            UIKit.DestroyChildren(pips);
            if (cost == 0)
            {
                var free = UIKit.Label(pips, "Free", "FREE", 12, TextAnchor.MiddleRight, Theme.Text, FontStyle.Bold);
                UIKit.Layout(free, 40f, 20f);
                return;
            }
            // Four pips (an AND card's Big Jump) shrink a little so they stay clear of the hit counter.
            float size = cost > 3 ? 9f : 11f;
            for (int i = 0; i < cost; i++)
            {
                var pip = UIKit.Rect(pips, "Pip", Vector2.zero, Vector2.one);
                var img = pip.gameObject.AddComponent<Image>();
                img.sprite = UISprites.Circle;
                img.color = Theme.Qi;
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
            if (PulseNot && notVisible && !negated && notButton.interactable)
            {
                float k = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 5f);
                notChip.color = Color.Lerp(Theme.Raised, Theme.Not, k * 0.8f);
                notText.color = Color.Lerp(Theme.Dim, Theme.Text, k);
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
    }
}
