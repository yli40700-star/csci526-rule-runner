using UnityEngine;

namespace RuleRunner.View
{
    /// <summary>Procedural grey-box sprites (no art assets allowed). Each sprite is 1 x 1 world unit, pivot at bottom-center.</summary>
    public static class Shapes
    {
        const int Res = 32;
        static Sprite square, triangle, circle;

        public static Sprite Square => square ? square : (square = Make((x, y) => true));
        public static Sprite Triangle => triangle ? triangle : (triangle = Make((x, y) =>
            Mathf.Abs(x - 0.5f) <= 0.5f * (1f - y)));
        public static Sprite Circle => circle ? circle : (circle = Make((x, y) =>
            (x - 0.5f) * (x - 0.5f) + (y - 0.5f) * (y - 0.5f) <= 0.25f));

        static Sprite Make(System.Func<float, float, bool> inside)
        {
            var tex = new Texture2D(Res, Res, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            var pixels = new Color32[Res * Res];
            for (int y = 0; y < Res; y++)
                for (int x = 0; x < Res; x++)
                    pixels[y * Res + x] = inside((x + 0.5f) / Res, (y + 0.5f) / Res)
                        ? new Color32(255, 255, 255, 255)
                        : new Color32(255, 255, 255, 0);
            tex.SetPixels32(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, Res, Res), new Vector2(0.5f, 0f), Res);
        }

        /// <summary>Creates a colored shape. position is the bottom-center; size is width x height in world units.</summary>
        public static SpriteRenderer Create(Transform parent, string name, Sprite sprite, Color color,
            Vector2 position, Vector2 size, int order = 0)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = color;
            sr.sortingOrder = order;
            return sr;
        }
    }
}
