using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace RuleRunner.UI
{
    /// <summary>
    /// A gold frame that pulses around a UI element ("tap me next"). It is a child of the element, so it moves with it,
    /// and it never takes clicks. Used by the tutorial on card parts after a failed run.
    /// </summary>
    public class Blink : MonoBehaviour
    {
        const float Period = 1.1f;
        static readonly List<Blink> active = new List<Blink>();
        Image frame;

        public static void On(RectTransform target)
        {
            if (target == null) return;
            var b = target.GetComponentInChildren<Blink>(true);
            if (b == null || b.transform.parent != target)
            {
                var rt = UIKit.Rect(target, "Blink", Vector2.zero, Vector2.one, new Vector2(-6, -6), new Vector2(6, 6));
                rt.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
                b = rt.gameObject.AddComponent<Blink>();
                b.frame = rt.gameObject.AddComponent<Image>();
                b.frame.sprite = UISprites.Outline;
                b.frame.type = Image.Type.Sliced;
                b.frame.pixelsPerUnitMultiplier = UISprites.CornerRadius / 12f;
                b.frame.raycastTarget = false;
            }
            b.gameObject.SetActive(true);
            b.transform.SetAsLastSibling();
            if (!active.Contains(b)) active.Add(b);
        }

        public static void Off(RectTransform target)
        {
            if (target == null) return;
            var b = target.GetComponentInChildren<Blink>(true);
            if (b == null) return;
            b.gameObject.SetActive(false);
            active.Remove(b);
        }

        public static void AllOff()
        {
            foreach (var b in active)
                if (b) b.gameObject.SetActive(false);
            active.Clear();
        }

        void Update()
        {
            float k = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * Mathf.PI * 2f / Period);
            frame.color = Theme.Gold.WithAlpha(0.35f + 0.65f * k);
            float grow = 2f + 4f * k;
            var rt = (RectTransform)transform;
            rt.offsetMin = new Vector2(-grow, -grow);
            rt.offsetMax = new Vector2(grow, grow);
        }
    }
}
