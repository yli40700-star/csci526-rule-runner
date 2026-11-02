using System.Collections.Generic;
using RuleRunner.Core;
using RuleRunner.UI;
using UnityEngine;
using UnityEngine.UI;

namespace RuleRunner.Cards
{
    /// <summary>
    /// Tutorial: small copies of the equipped cards that ride above the runner's head, so the eye never has to leave
    /// the runner. During a run they are read out loud, card by card from the left: a pointer moves under the card
    /// being read, its IF gets a red cross (no match) or a green tick (match), and reading stops at the first tick.
    /// Cards to the right of it stay grey: they are never read.
    /// </summary>
    public class HeadCards : MonoBehaviour
    {
        static readonly Vector2 MiniSize = new Vector2(112, 146);
        const float Scale = 0.82f;

        RectTransform row, pointer;
        readonly List<Mini> minis = new List<Mini>();

        public static HeadCards Create(Transform parent)
        {
            var root = UIKit.Rect(parent, "HeadCards", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            root.pivot = new Vector2(0.5f, 0f);
            var view = root.gameObject.AddComponent<HeadCards>();
            view.row = root;
            UIKit.HRow(root, 6, null, false, TextAnchor.LowerCenter);
            var fitter = root.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            root.localScale = Vector3.one * Scale;

            view.pointer = UIKit.Shape(root, "Pointer", UISprites.Triangle, Color.white, Vector2.zero, new Vector2(18, 13)).rectTransform;
            view.pointer.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            view.pointer.anchorMin = view.pointer.anchorMax = new Vector2(0f, 0f);
            view.pointer.gameObject.AddComponent<Outline>().effectColor = new Color(0, 0, 0, 0.5f);
            view.pointer.gameObject.SetActive(false);
            root.gameObject.SetActive(false);
            return view;
        }

        public void Show(bool on) => gameObject.SetActive(on);

        /// <summary>Places the row so its bottom edge sits just above the given canvas point, kept inside the canvas.</summary>
        public void Follow(Vector2 canvasPoint, RectTransform canvas)
        {
            var rt = (RectTransform)transform;
            float half = rt.rect.width * Scale / 2f + 8f;
            float limit = canvas.rect.width / 2f - half;
            rt.anchoredPosition = new Vector2(Mathf.Clamp(canvasPoint.x, -limit, limit), canvasPoint.y + 14f);
        }

        public void Bind(IList<Rule> rules, bool showNot)
        {
            while (minis.Count < rules.Count) minis.Add(Mini.Build(row));
            for (int i = 0; i < minis.Count; i++)
            {
                bool on = i < rules.Count;
                minis[i].Root.gameObject.SetActive(on);
                if (on) minis[i].Bind(i, rules[i], showNot);
            }
            pointer.SetAsLastSibling();
            Clear();
        }

        /// <summary>Back to plain cards: no ticks, no grey, no pointer.</summary>
        public void Clear()
        {
            foreach (var m in minis) m.Reset();
            pointer.gameObject.SetActive(false);
        }

        /// <summary>A new reading starts: every card is grey until the pointer reaches it.</summary>
        public void GreyAll()
        {
            foreach (var m in minis) m.SetAlpha(0.3f);
        }

        public void Read(int i)
        {
            if (!Valid(i)) return;
            var m = minis[i];
            m.SetAlpha(1f);
            m.SetReading(true);
            Canvas.ForceUpdateCanvases();
            var card = m.Root;
            pointer.gameObject.SetActive(true);
            pointer.anchoredPosition = new Vector2(card.anchoredPosition.x, -9f);
            pointer.localRotation = Quaternion.identity;
        }

        public void Miss(int i)
        {
            if (!Valid(i)) return;
            minis[i].SetReading(false);
            minis[i].MarkIf(false);
        }

        public void Unread(int i, float alpha = 0.3f)
        {
            if (Valid(i)) minis[i].SetAlpha(alpha);
        }

        public void Hit(int i)
        {
            if (!Valid(i)) return;
            minis[i].SetAlpha(1f);
            minis[i].SetReading(false);
            minis[i].MarkIf(true);
        }

        /// <summary>The THEN part lights gold (it acts) or red (not enough Energy to act).</summary>
        public void Then(int i, bool canPay)
        {
            if (Valid(i)) minis[i].MarkThen(canPay);
        }

        bool Valid(int i) => i >= 0 && i < minis.Count && minis[i].Root.gameObject.activeSelf;

        /// <summary>One small card: band with its number, IF part (diagram + name), THEN part (diagram + name).</summary>
        sealed class Mini
        {
            public RectTransform Root;
            CanvasGroup group;
            Outline reading;
            Image band, ifBg, thenBg, badge;
            Outline ifEdge, thenEdge;
            Text number, ifName, thenName, notTag;
            RectTransform ifIcon, thenIcon, tick, cross;

            public static Mini Build(RectTransform row)
            {
                var m = new Mini();
                m.Root = UIKit.Rect(row, "MiniCard", Vector2.zero, Vector2.one);
                UIKit.Layout(m.Root, MiniSize.x, MiniSize.y);
                m.group = m.Root.gameObject.AddComponent<CanvasGroup>();
                m.group.blocksRaycasts = false;

                var body = UIKit.FillBox(m.Root, "Body", Theme.Surface2, 12f);
                UIKit.DropShadow(body, 4f, 0.5f);
                m.reading = body.gameObject.AddComponent<Outline>();
                m.reading.effectColor = Color.white;
                m.reading.effectDistance = new Vector2(2.5f, -2.5f);
                m.reading.enabled = false;

                m.band = UIKit.Box(body.transform, "Band", Theme.Line, 7f, new Vector2(0, 1), new Vector2(1, 1), new Vector2(5, -21), new Vector2(-5, -5));
                m.number = UIKit.Label(m.band.transform, "N", "1", 12, TextAnchor.MiddleLeft, Theme.Text, FontStyle.Bold);
                ((RectTransform)m.number.transform).offsetMin = new Vector2(7, 0);

                m.ifBg = Part(body.transform, "If", 24f, "IF", out m.ifEdge, out m.ifIcon, out m.ifName);
                m.notTag = UIKit.Label(UIKit.Rect(m.ifBg.transform, "Not", new Vector2(1, 1), new Vector2(1, 1), new Vector2(-34, -15), new Vector2(-4, -2)),
                    "Text", "NOT", 10, TextAnchor.MiddleCenter, Theme.Not, FontStyle.Bold);
                m.thenBg = Part(body.transform, "Then", 84f, "THEN", out m.thenEdge, out m.thenIcon, out m.thenName);

                // Tick / cross badge on the left edge of the IF part: it is the condition that was checked, not the whole card.
                m.badge = UIKit.Shape(m.ifBg.transform, "Badge", UISprites.Circle, Theme.Accent, Vector2.zero, new Vector2(22, 22));
                var brt = m.badge.rectTransform;
                brt.anchorMin = brt.anchorMax = new Vector2(0f, 0.5f);
                brt.anchoredPosition = new Vector2(-2f, 0f);
                m.badge.gameObject.AddComponent<Outline>().effectColor = new Color(0, 0, 0, 0.5f);
                m.tick = Mark(brt, new[] { (new Vector2(-3.5f, -1f), new Vector2(3f, 8f), 45f), (new Vector2(2.5f, 1f), new Vector2(3f, 13f), -38f) });
                m.cross = Mark(brt, new[] { (Vector2.zero, new Vector2(3f, 13f), 45f), (Vector2.zero, new Vector2(3f, 13f), -45f) });
                return m;
            }

            static Image Part(Transform body, string name, float top, string label, out Outline edge, out RectTransform icon, out Text text)
            {
                var bg = UIKit.Box(body, name, new Color(1, 1, 1, 0f), 8f, new Vector2(0, 1), new Vector2(1, 1),
                    new Vector2(4, -top - 58f), new Vector2(-4, -top));
                bg.raycastTarget = false;
                edge = bg.gameObject.AddComponent<Outline>();
                edge.effectDistance = new Vector2(2f, -2f);
                edge.enabled = false;
                UIKit.Label(UIKit.TopStrip(bg.transform, "Label", 1f, 13f, 5f, 5f), "Text", label, 10, TextAnchor.MiddleLeft, Theme.Dim, FontStyle.Bold);
                icon = UIKit.Centered(bg.transform, "Icon", new Vector2(0, 2f), Icons.Size);
                icon.localScale = Vector3.one * 0.8f;
                text = UIKit.Label(UIKit.Rect(bg.transform, "Name", Vector2.zero, new Vector2(1, 0), new Vector2(2, 1), new Vector2(-2, 14)),
                    "Text", "", 11, TextAnchor.MiddleCenter, Theme.Text, FontStyle.Bold);
                text.horizontalOverflow = HorizontalWrapMode.Overflow;
                return bg;
            }

            static RectTransform Mark(RectTransform parent, (Vector2 pos, Vector2 size, float angle)[] bars)
            {
                var rt = UIKit.Fill(parent, "Mark");
                foreach (var (pos, size, angle) in bars)
                    UIKit.Shape(rt, "Bar", UISprites.Rounded, Color.white, pos, size).rectTransform.localRotation = Quaternion.Euler(0, 0, angle);
                return rt;
            }

            public void Bind(int index, Rule rule, bool showNot)
            {
                number.text = (index + 1).ToString();
                band.color = Theme.ActionColor(rule.Action);
                Icons.DrawCondition(ifIcon, rule.Condition, rule.Negate);
                ifName.text = Vocabulary.Display(rule.Condition);
                ifName.color = rule.Negate ? Theme.Hex(0xffb3a3) : Theme.Text;
                notTag.gameObject.SetActive(rule.Negate && showNot);
                Icons.DrawAction(thenIcon, rule.Action);
                thenName.text = Vocabulary.Display(rule.Action);
            }

            public void Reset()
            {
                SetAlpha(1f);
                SetReading(false);
                badge.gameObject.SetActive(false);
                Light(ifBg, ifEdge, null);
                Light(thenBg, thenEdge, null);
            }

            public void SetAlpha(float a) => group.alpha = a;
            public void SetReading(bool on) => reading.enabled = on;

            public void MarkIf(bool match)
            {
                badge.gameObject.SetActive(true);
                badge.color = match ? Theme.Accent : Theme.Danger;
                tick.gameObject.SetActive(match);
                cross.gameObject.SetActive(!match);
                Light(ifBg, ifEdge, match ? Theme.Gold : Theme.Danger);
            }

            public void MarkThen(bool canPay) => Light(thenBg, thenEdge, canPay ? Theme.Gold : Theme.Danger);

            static void Light(Image bg, Outline edge, Color? color)
            {
                edge.enabled = color.HasValue;
                if (!color.HasValue)
                {
                    bg.color = new Color(1, 1, 1, 0f);
                    return;
                }
                bg.color = color.Value.WithAlpha(0.22f);
                edge.effectColor = color.Value;
            }
        }
    }
}
