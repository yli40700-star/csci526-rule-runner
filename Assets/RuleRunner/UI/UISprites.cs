using UnityEngine;

namespace RuleRunner.UI
{
    /// <summary>
    /// Procedural UI sprites (anti-aliased): a 9-sliced rounded rectangle, its outline, a circle and a triangle.
    /// Corner size is set per Image with pixelsPerUnitMultiplier (see UIKit.Round).
    /// </summary>
    public static class UISprites
    {
        const int Size = 64;
        public const float CornerRadius = 20f;
        const float Border = 22f;

        static Sprite rounded, outline, circle, triangle;

        public static Sprite Rounded => rounded ? rounded : (rounded = Make(RoundedCoverage(0f), true));
        public static Sprite Outline => outline ? outline : (outline = Make(RoundedCoverage(2.5f), true));
        public static Sprite Circle => circle ? circle : (circle = Make((x, y) =>
        {
            float c = Size / 2f;
            float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) - (c - 1f);
            return Mathf.Clamp01(0.5f - d);
        }, false));

        public static Sprite Triangle => triangle ? triangle : (triangle = Make((x, y) =>
        {
            // 4x4 supersampling of an upward triangle.
            int hits = 0;
            for (int sy = 0; sy < 4; sy++)
                for (int sx = 0; sx < 4; sx++)
                {
                    float u = (x - 0.5f + (sx + 0.5f) / 4f) / Size;
                    float v = (y - 0.5f + (sy + 0.5f) / 4f) / Size;
                    if (Mathf.Abs(u - 0.5f) <= 0.5f * (1f - v)) hits++;
                }
            return hits / 16f;
        }, false));

        /// <summary>Coverage for a rounded rect; ring > 0 makes an outline of that thickness.</summary>
        static System.Func<float, float, float> RoundedCoverage(float ring) => (x, y) =>
        {
            float c = Size / 2f;
            float qx = Mathf.Abs(x - c) - (c - CornerRadius);
            float qy = Mathf.Abs(y - c) - (c - CornerRadius);
            float outside = Mathf.Sqrt(Mathf.Max(qx, 0) * Mathf.Max(qx, 0) + Mathf.Max(qy, 0) * Mathf.Max(qy, 0));
            float d = outside + Mathf.Min(Mathf.Max(qx, qy), 0f) - CornerRadius;
            float a = Mathf.Clamp01(0.5f - d);
            if (ring > 0f) a *= Mathf.Clamp01(d + ring + 0.5f);
            return a;
        };

        static Sprite Make(System.Func<float, float, float> coverage, bool sliced)
        {
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            var pixels = new Color32[Size * Size];
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                {
                    byte a = (byte)Mathf.RoundToInt(coverage(x + 0.5f, y + 0.5f) * 255f);
                    pixels[y * Size + x] = new Color32(255, 255, 255, a);
                }
            tex.SetPixels32(pixels);
            tex.Apply();
            var border = sliced ? new Vector4(Border, Border, Border, Border) : Vector4.zero;
            return Sprite.Create(tex, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect, border);
        }
    }
}
