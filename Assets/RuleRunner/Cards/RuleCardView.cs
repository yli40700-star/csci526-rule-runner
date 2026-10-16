using System;
using RuleRunner.Core;
using RuleRunner.UI;
using UnityEngine;
using UnityEngine.UI;

namespace RuleRunner.Cards
{
    /// <summary>
    /// A rule as a "skill card" the runner equips:
    ///   header band (action color): priority number, hit counter, qi cost pips
    ///   IF   section: NOT chip + condition diagram + name   (click to change the condition)
    ///   THEN section: action diagram + name                  (click to change the action)
    ///   footer: move left / remove / move right
    /// The owner (GameScreen) holds the rule list; the card only raises events.
    /// </summary>
    public class RuleCardView : MonoBehaviour
    {
        public static readonly Vector2 CardSize = new Vector2(156, 226);

        public event Action<RuleCardView> ToggleNot, PickCondition, PickAction, MoveLeft, MoveRight, Remove;

        RectTransform body, condIcon, actionIcon, pips;
        Image bodyImage, band;
        Outline glow;
        Text priorityText, hitsText, condText, actionText, notText;
        Image notChip;
        Button notButton, condArea, actionArea, leftButton, rightButton, removeButton;
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
            card.condIcon = UIKit.Centered(ifRt, "Icon", new Vector2(0, 2f), Icons.Size);
            var condLabel = UIKit.Rect(ifRt, "Name", new Vector2(0, 0), new Vector2(1, 0), new Vector2(4, 2), new Vector2(-4, 20));
            card.condText = UIKit.Label(condLabel, "Text", "", 14, TextAnchor.MiddleCenter);

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

        public void Bind(int index, Rule rule, bool isFirst, bool isLast, bool editable)
        {
            Index = index;
            priorityText.text = (index + 1).ToString();
            band.color = Theme.ActionColor(rule.Action);

            notChip.color = rule.Negate ? Theme.Not : new Color(1, 1, 1, 0.06f);
            notText.color = rule.Negate ? Theme.Text : Theme.Faint;
            condText.text = (rule.Negate ? "NOT " : "") + Vocabulary.Display(rule.Condition);
            condText.color = rule.Negate ? Theme.Hex(0xffb3a3) : Theme.Text;
            Icons.DrawCondition(condIcon, rule.Condition, rule.Negate);

            actionText.text = Vocabulary.Display(rule.Action);
            Icons.DrawAction(actionIcon, rule.Action);
            BuildPips(Vocabulary.Cost(rule.Action));

            notButton.interactable = editable;
            condArea.interactable = editable;
            actionArea.interactable = editable;
            leftButton.interactable = editable && !isFirst;
            rightButton.interactable = editable && !isLast;
            removeButton.interactable = editable;
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
            for (int i = 0; i < cost; i++)
            {
                var pip = UIKit.Rect(pips, "Pip", Vector2.zero, Vector2.one);
                var img = pip.gameObject.AddComponent<Image>();
                img.sprite = UISprites.Circle;
                img.color = Theme.Qi;
                img.raycastTarget = false;
                pip.gameObject.AddComponent<Outline>().effectColor = new Color(0, 0, 0, 0.35f);
                UIKit.Layout(pip, 11f, 11f);
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
