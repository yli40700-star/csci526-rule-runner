using System;
using RuleRunner.Core;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace RuleRunner.UI
{
    /// <summary>UI color palette. Grey-box, but consistent.</summary>
    public static class Theme
    {
        public static readonly Color Background = Hex(0x141922);
        public static readonly Color Surface = Hex(0x1e2531);
        public static readonly Color Surface2 = Hex(0x283041);
        public static readonly Color Raised = Hex(0x343e53);
        public static readonly Color RaisedHover = Hex(0x3f4a62);
        public static readonly Color Line = Hex(0x3a4459);
        public static readonly Color Text = Hex(0xeef1f5);
        public static readonly Color Dim = Hex(0x8d97aa);
        public static readonly Color Faint = Hex(0x5d677a);
        public static readonly Color Accent = Hex(0x43b86a);
        public static readonly Color Gold = Hex(0xf5c542);
        public static readonly Color Danger = Hex(0xe05555);
        public static readonly Color Qi = Hex(0x4fd6e8);
        public static readonly Color Not = Hex(0xff6b4a);
        public static readonly Color Shadow = new Color(0f, 0f, 0f, 0.4f);

        /// <summary>Each action gets its own card color, like a skill type.</summary>
        public static Color ActionColor(ActionType a)
        {
            switch (a)
            {
                case ActionType.Walk: return Hex(0x7d8798);
                case ActionType.Jump: return Hex(0x4c8dff);
                case ActionType.BigJump: return Hex(0x9b6bff);
                case ActionType.Turn: return Hex(0x2fbfa5);
                case ActionType.Wait: return Hex(0xf0a030);
                default: return Hex(0x7d8798);
            }
        }

        public static Color Hex(int rgb, float alpha = 1f) =>
            new Color(((rgb >> 16) & 0xff) / 255f, ((rgb >> 8) & 0xff) / 255f, (rgb & 0xff) / 255f, alpha);

        public static Color WithAlpha(this Color c, float a) => new Color(c.r, c.g, c.b, a);
    }

    /// <summary>
    /// Tiny factory for building uGUI from code (no prefabs, no art).
    /// Only ASCII text is used: WebGL has no system font fallback.
    /// </summary>
    public static class UIKit
    {
        static Font font;
        public static Font Font => font ? font : (font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));

        public static RectTransform Rect(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 offsetMin = default, Vector2 offsetMax = default)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
            return rt;
        }

        /// <summary>A rect anchored at its parent's center with a fixed size.</summary>
        public static RectTransform Centered(Transform parent, string name, Vector2 position, Vector2 size)
        {
            var rt = Rect(parent, name, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            rt.sizeDelta = size;
            rt.anchoredPosition = position;
            return rt;
        }

        /// <summary>A strip along the top of the parent: y measured downward from the top edge.</summary>
        public static RectTransform TopStrip(Transform parent, string name, float top, float height, float left = 0f, float right = 0f) =>
            Rect(parent, name, new Vector2(0, 1), new Vector2(1, 1), new Vector2(left, -top - height), new Vector2(-right, -top));

        public static RectTransform Fill(Transform parent, string name, float inset = 0f) =>
            Rect(parent, name, Vector2.zero, Vector2.one, new Vector2(inset, inset), new Vector2(-inset, -inset));

        public static Image Panel(Transform parent, string name, Color color, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 offsetMin = default, Vector2 offsetMax = default)
        {
            var rt = Rect(parent, name, anchorMin, anchorMax, offsetMin, offsetMax);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            return img;
        }

        /// <summary>Makes an Image a rounded rectangle with the given corner radius (UI units).</summary>
        public static Image Round(Image img, float radius)
        {
            img.sprite = UISprites.Rounded;
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = UISprites.CornerRadius / Mathf.Max(1f, radius);
            return img;
        }

        public static Image Box(Transform parent, string name, Color color, float radius, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 offsetMin = default, Vector2 offsetMax = default) =>
            Round(Panel(parent, name, color, anchorMin, anchorMax, offsetMin, offsetMax), radius);

        public static Image FillBox(Transform parent, string name, Color color, float radius, float inset = 0f) =>
            Box(parent, name, color, radius, Vector2.zero, Vector2.one, new Vector2(inset, inset), new Vector2(-inset, -inset));

        /// <summary>A rounded outline (used for empty card slots).</summary>
        public static Image OutlineBox(Transform parent, string name, Color color, float radius)
        {
            var img = Panel(parent, name, color, Vector2.zero, Vector2.one);
            img.sprite = UISprites.Outline;
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = UISprites.CornerRadius / Mathf.Max(1f, radius);
            return img;
        }

        public static Image Shape(Transform parent, string name, Sprite sprite, Color color, Vector2 position, Vector2 size)
        {
            var rt = Centered(parent, name, position, size);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        public static void DropShadow(Graphic g, float distance = 4f, float alpha = 0.45f)
        {
            var s = g.gameObject.AddComponent<Shadow>();
            s.effectColor = new Color(0, 0, 0, alpha);
            s.effectDistance = new Vector2(0, -distance);
        }

        public static Text Label(Transform parent, string name, string text, int size,
            TextAnchor align = TextAnchor.MiddleLeft, Color? color = null, FontStyle style = FontStyle.Normal)
        {
            var rt = Fill(parent, name);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = Font;
            t.text = text;
            t.fontSize = size;
            t.fontStyle = style;
            t.alignment = align;
            t.color = color ?? Theme.Text;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        public static Button Button(Transform parent, string name, string text, int size, Color color, UnityAction onClick,
            float radius = 10f, FontStyle style = FontStyle.Bold)
        {
            var img = Box(parent, name, color, radius, Vector2.zero, Vector2.one);
            var b = img.gameObject.AddComponent<Button>();
            b.targetGraphic = img;
            var colors = b.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.12f, 1.12f, 1.12f, 1f);
            colors.selectedColor = Color.white;
            colors.pressedColor = new Color(0.82f, 0.82f, 0.82f, 1f);
            colors.disabledColor = new Color(0.6f, 0.6f, 0.6f, 0.45f);
            colors.fadeDuration = 0.08f;
            b.colors = colors;
            if (onClick != null) b.onClick.AddListener(onClick);
            if (!string.IsNullOrEmpty(text))
            {
                var label = Label(img.transform, "Text", text, size, TextAnchor.MiddleCenter, Theme.Text, style);
                label.horizontalOverflow = HorizontalWrapMode.Overflow;
            }
            return b;
        }

        /// <summary>An invisible clickable area (for clicking a whole card section).</summary>
        public static Button HitArea(RectTransform rt, UnityAction onClick, Color hover)
        {
            var img = rt.gameObject.AddComponent<Image>();
            Round(img, 10f);
            img.color = Color.white;
            var b = rt.gameObject.AddComponent<Button>();
            b.targetGraphic = img;
            var colors = b.colors;
            colors.normalColor = new Color(1, 1, 1, 0f);
            colors.highlightedColor = hover;
            colors.selectedColor = new Color(1, 1, 1, 0f);
            colors.pressedColor = hover * 1.4f;
            colors.disabledColor = new Color(1, 1, 1, 0f);
            colors.fadeDuration = 0.08f;
            b.colors = colors;
            b.onClick.AddListener(onClick);
            return b;
        }

        public static Text ButtonText(Button b) => b.GetComponentInChildren<Text>();

        /// <summary>Adds a LayoutElement with a preferred / flexible size.</summary>
        public static LayoutElement Layout(Component c, float preferredWidth = -1, float preferredHeight = -1,
            float flexibleWidth = -1, float flexibleHeight = -1)
        {
            var le = c.gameObject.GetComponent<LayoutElement>();
            if (le == null) le = c.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = preferredWidth;
            le.preferredHeight = preferredHeight;
            le.flexibleWidth = flexibleWidth;
            le.flexibleHeight = flexibleHeight;
            if (preferredWidth >= 0) le.minWidth = preferredWidth;
            if (preferredHeight >= 0) le.minHeight = preferredHeight;
            // A fixed size means "don't stretch" unless flexibility was asked for explicitly
            // (nested layout groups otherwise report flexible size 1 and fill the parent).
            if (preferredWidth >= 0 && flexibleWidth < 0) le.flexibleWidth = 0;
            if (preferredHeight >= 0 && flexibleHeight < 0) le.flexibleHeight = 0;
            return le;
        }

        public static HorizontalLayoutGroup HRow(RectTransform rt, float spacing, RectOffset padding = null,
            bool expandWidth = false, TextAnchor align = TextAnchor.MiddleLeft)
        {
            var g = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
            g.spacing = spacing;
            g.padding = padding ?? new RectOffset(0, 0, 0, 0);
            g.childAlignment = align;
            g.childControlWidth = true;
            g.childControlHeight = true;
            g.childForceExpandWidth = expandWidth;
            g.childForceExpandHeight = false;
            return g;
        }

        public static VerticalLayoutGroup VColumn(RectTransform rt, float spacing, RectOffset padding = null)
        {
            var g = rt.gameObject.AddComponent<VerticalLayoutGroup>();
            g.spacing = spacing;
            g.padding = padding ?? new RectOffset(0, 0, 0, 0);
            g.childAlignment = TextAnchor.UpperLeft;
            g.childControlWidth = true;
            g.childControlHeight = true;
            g.childForceExpandWidth = true;
            g.childForceExpandHeight = false;
            return g;
        }

        /// <summary>A vertical scroll view; returns the content rect (grows with its children).</summary>
        public static RectTransform ScrollView(Transform parent, string name, out ScrollRect scroll)
        {
            var view = Fill(parent, name);
            var bg = view.gameObject.AddComponent<Image>();
            bg.color = new Color(0, 0, 0, 0.001f);
            view.gameObject.AddComponent<RectMask2D>();
            scroll = view.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 25f;

            var content = Rect(view, "Content", new Vector2(0, 1), new Vector2(1, 1));
            content.pivot = new Vector2(0.5f, 1f);
            content.sizeDelta = Vector2.zero;
            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = content;
            scroll.viewport = view;
            return content;
        }

        public static void DestroyChildren(Transform t)
        {
            for (int i = t.childCount - 1; i >= 0; i--)
            {
                var child = t.GetChild(i).gameObject;
                child.SetActive(false);
                UnityEngine.Object.Destroy(child);
            }
        }

        public static void SafeInvoke(Action a)
        {
            try { a?.Invoke(); }
            catch (Exception e) { Debug.LogException(e); }
        }
    }
}
