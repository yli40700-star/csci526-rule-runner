using UnityEngine;
using UnityEngine.UI;

namespace RuleRunner.UI
{
    /// <summary>
    /// Wordless pointer for the tutorial levels: a fingertip that keeps tapping one UI element, with a ripple
    /// and a pulsing frame around it. It never blocks clicks, so the player taps the element itself.
    /// </summary>
    public class TapHint : MonoBehaviour
    {
        const float Period = 1.3f;

        RectTransform canvas, target, frame, finger, ripple;
        Image frameImage, rippleImage;
        float time;

        public static TapHint Create(RectTransform canvas)
        {
            var root = UIKit.Fill(canvas, "TapHint");
            var h = root.gameObject.AddComponent<TapHint>();
            h.canvas = canvas;

            h.frameImage = UIKit.Panel(root, "Frame", Theme.Gold, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            h.frameImage.sprite = UISprites.Outline;
            h.frameImage.type = Image.Type.Sliced;
            h.frameImage.pixelsPerUnitMultiplier = UISprites.CornerRadius / 12f;
            h.frameImage.raycastTarget = false;
            h.frame = h.frameImage.rectTransform;

            h.rippleImage = UIKit.Shape(root, "Ripple", UISprites.Circle, Theme.Gold, Vector2.zero, new Vector2(20, 20));
            h.rippleImage.raycastTarget = false;
            h.ripple = h.rippleImage.rectTransform;

            // Fingertip: a white disc with a dark rim, and a short "finger" behind it pointing up-left.
            var f = UIKit.Rect(root, "Finger", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            f.sizeDelta = new Vector2(40, 40);
            h.finger = f;
            var shaft = UIKit.Shape(f, "Shaft", UISprites.Rounded, Color.white, new Vector2(14, -16), new Vector2(22, 46));
            shaft.rectTransform.localRotation = Quaternion.Euler(0, 0, 35f);
            shaft.gameObject.AddComponent<Outline>().effectColor = new Color(0, 0, 0, 0.7f);
            var tip = UIKit.Shape(f, "Tip", UISprites.Circle, Color.white, Vector2.zero, new Vector2(26, 26));
            tip.gameObject.AddComponent<Outline>().effectColor = new Color(0, 0, 0, 0.7f);
            foreach (var g in f.GetComponentsInChildren<Graphic>()) g.raycastTarget = false;

            root.gameObject.SetActive(false);
            return h;
        }

        public bool Showing => gameObject.activeSelf;

        /// <summary>Starts tapping on the target (follows it if the layout moves it).</summary>
        public void Show(RectTransform t)
        {
            if (t == null) { Hide(); return; }
            target = t;
            time = 0f;
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            Update();
        }

        public void Hide()
        {
            target = null;
            gameObject.SetActive(false);
        }

        void Update()
        {
            if (target == null || !target.gameObject.activeInHierarchy)
            {
                // The target is hidden for now (e.g. during a run): keep waiting, invisibly.
                SetVisible(false);
                return;
            }
            SetVisible(true);
            time += Time.unscaledDeltaTime;
            var r = CanvasRectOf(target);
            var c = r.center;

            float pulse = 0.5f + 0.5f * Mathf.Sin(time * Mathf.PI * 2f / Period);
            frame.anchoredPosition = c;
            frame.sizeDelta = r.size + Vector2.one * (14f + 6f * pulse);
            frameImage.color = Theme.Gold.WithAlpha(0.55f + 0.45f * pulse);

            // The finger comes in from the lower right, presses, and lifts again; the ripple starts at the press.
            float p = (time % Period) / Period;
            float approach = p < 0.45f ? Mathf.SmoothStep(0f, 1f, p / 0.45f) : p < 0.6f ? 1f : 1f - Mathf.SmoothStep(0f, 1f, (p - 0.6f) / 0.4f);
            finger.anchoredPosition = c + Vector2.Lerp(new Vector2(46, -58), new Vector2(6, -6), approach);
            finger.localScale = Vector3.one * (p >= 0.45f && p < 0.6f ? 0.88f : 1f);

            float rp = Mathf.Clamp01((p - 0.45f) / 0.5f);
            ripple.anchoredPosition = c;
            ripple.sizeDelta = Vector2.one * Mathf.Lerp(16f, 70f, rp);
            rippleImage.color = Theme.Gold.WithAlpha(p < 0.45f ? 0f : 0.5f * (1f - rp));
        }

        void SetVisible(bool on)
        {
            frame.gameObject.SetActive(on);
            finger.gameObject.SetActive(on);
            ripple.gameObject.SetActive(on);
        }

        Rect CanvasRectOf(RectTransform t)
        {
            var corners = new Vector3[4];
            t.GetWorldCorners(corners);
            Vector2 min = new Vector2(float.MaxValue, float.MaxValue), max = new Vector2(float.MinValue, float.MinValue);
            foreach (var corner in corners)
            {
                RectTransformUtility.ScreenPointToLocalPointInRectangle(canvas, RectTransformUtility.WorldToScreenPoint(null, corner), null, out var p);
                min = Vector2.Min(min, p);
                max = Vector2.Max(max, p);
            }
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }
    }
}
