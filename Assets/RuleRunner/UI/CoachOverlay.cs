using System;
using UnityEngine;
using UnityEngine.UI;

namespace RuleRunner.UI
{
    /// <summary>
    /// Tutorial spotlight: dims the screen except a hole around one target, frames the hole,
    /// and shows a one-line tip with optional Next and an always-available Skip.
    /// Clicks only reach the target inside the hole.
    /// </summary>
    public class CoachOverlay : MonoBehaviour
    {
        public event Action Next, Skip;

        RectTransform canvas;
        RectTransform top, bottom, left, right, frame, tip;
        Text tipText;
        Button nextButton;

        public static CoachOverlay Create(RectTransform canvas)
        {
            var root = UIKit.Fill(canvas, "CoachOverlay");
            var o = root.gameObject.AddComponent<CoachOverlay>();
            o.canvas = canvas;
            var dim = new Color(0.02f, 0.03f, 0.05f, 0.72f);
            o.top = Blocker(root, "Top", dim);
            o.bottom = Blocker(root, "Bottom", dim);
            o.left = Blocker(root, "Left", dim);
            o.right = Blocker(root, "Right", dim);

            var f = UIKit.Panel(root, "Frame", Theme.Gold, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            f.sprite = UISprites.Outline;
            f.type = Image.Type.Sliced;
            f.pixelsPerUnitMultiplier = UISprites.CornerRadius / 12f;
            f.raycastTarget = false;
            o.frame = f.rectTransform;

            var tipBox = UIKit.Box(root, "Tip", Theme.Surface, 14f, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            UIKit.DropShadow(tipBox, 6f, 0.5f);
            tipBox.gameObject.AddComponent<Outline>().effectColor = Theme.Gold.WithAlpha(0.6f);
            o.tip = tipBox.rectTransform;
            o.tip.sizeDelta = new Vector2(440, 104);
            o.tipText = UIKit.Label(UIKit.Rect(o.tip, "Text", Vector2.zero, Vector2.one, new Vector2(18, 42), new Vector2(-18, -10)),
                "Text", "", 18, TextAnchor.MiddleLeft, Theme.Text);

            var skip = UIKit.Button(o.tip, "Skip", "Skip tutorial", 13, Theme.Raised, () => o.Skip?.Invoke(), 8f, FontStyle.Normal);
            var skipRt = (RectTransform)skip.transform;
            skipRt.anchorMin = skipRt.anchorMax = new Vector2(0, 0);
            skipRt.pivot = new Vector2(0, 0);
            skipRt.sizeDelta = new Vector2(110, 28);
            skipRt.anchoredPosition = new Vector2(14, 10);
            UIKit.ButtonText(skip).color = Theme.Dim;

            o.nextButton = UIKit.Button(o.tip, "Next", "Next", 15, Theme.Accent, () => o.Next?.Invoke(), 8f);
            var nextRt = (RectTransform)o.nextButton.transform;
            nextRt.anchorMin = nextRt.anchorMax = new Vector2(1, 0);
            nextRt.pivot = new Vector2(1, 0);
            nextRt.sizeDelta = new Vector2(90, 28);
            nextRt.anchoredPosition = new Vector2(-14, 10);

            root.gameObject.SetActive(false);
            return o;
        }

        static RectTransform Blocker(Transform parent, string name, Color color)
        {
            var img = UIKit.Panel(parent, name, color, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            img.raycastTarget = true;
            return img.rectTransform;
        }

        /// <summary>Spotlights a UI element.</summary>
        public void Show(RectTransform target, string text, bool showNext)
        {
            Canvas.ForceUpdateCanvases(); // the target may have just been created or moved by a layout group
            Show(CanvasRectOf(target, 8f), text, showNext);
        }

        /// <summary>Spotlights a rectangle given in canvas-local coordinates (origin at the canvas center).</summary>
        public void Show(Rect hole, string text, bool showNext)
        {
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            var size = canvas.rect.size;
            float l = -size.x / 2f, r = size.x / 2f, b = -size.y / 2f, t = size.y / 2f;

            Place(top, new Rect(l, hole.yMax, size.x, t - hole.yMax));
            Place(bottom, new Rect(l, b, size.x, hole.yMin - b));
            Place(left, new Rect(l, hole.yMin, hole.xMin - l, hole.height));
            Place(right, new Rect(hole.xMax, hole.yMin, r - hole.xMax, hole.height));
            Place(frame, hole);

            tipText.text = text;
            nextButton.gameObject.SetActive(showNext);
            // Grow the tip to fit its text (wrapping at the tip's width).
            float textHeight = tipText.cachedTextGeneratorForLayout.GetPreferredHeight(text,
                tipText.GetGenerationSettings(new Vector2(tip.sizeDelta.x - 36f, 0f))) / tipText.pixelsPerUnit;
            tip.sizeDelta = new Vector2(tip.sizeDelta.x, Mathf.Clamp(textHeight + 58f, 104f, 220f));
            // Put the tip above the hole if there is room, else below; keep it on screen.
            float y = hole.yMax + 14f + tip.sizeDelta.y / 2f;
            if (y + tip.sizeDelta.y / 2f > t - 10f) y = hole.yMin - 14f - tip.sizeDelta.y / 2f;
            float x = Mathf.Clamp(hole.center.x, l + tip.sizeDelta.x / 2f + 10f, r - tip.sizeDelta.x / 2f - 10f);
            tip.anchoredPosition = new Vector2(x, y);
        }

        public void Hide() => gameObject.SetActive(false);

        static void Place(RectTransform rt, Rect r)
        {
            rt.pivot = new Vector2(0, 0);
            rt.anchoredPosition = new Vector2(r.x, r.y);
            rt.sizeDelta = new Vector2(Mathf.Max(0, r.width), Mathf.Max(0, r.height));
        }

        /// <summary>A UI element's rectangle in this canvas' local coordinates, padded.</summary>
        public Rect CanvasRectOf(RectTransform target, float pad)
        {
            var corners = new Vector3[4];
            target.GetWorldCorners(corners);
            Vector2 min = new Vector2(float.MaxValue, float.MaxValue), max = new Vector2(float.MinValue, float.MinValue);
            foreach (var c in corners)
            {
                RectTransformUtility.ScreenPointToLocalPointInRectangle(canvas, RectTransformUtility.WorldToScreenPoint(null, c), null, out var p);
                min = Vector2.Min(min, p);
                max = Vector2.Max(max, p);
            }
            return Rect.MinMaxRect(min.x - pad, min.y - pad, max.x + pad, max.y + pad);
        }
    }
}
