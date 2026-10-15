using UnityEngine;

namespace RuleRunner.View
{
    /// <summary>
    /// Keeps the whole map (plus the edge walls) inside the top part of the screen.
    /// The UI covers the bottom part, so the camera renders into a viewport rect.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class CameraFitter : MonoBehaviour
    {
        /// <summary>Fraction of the screen height (from the top) used for the map.</summary>
        public float viewportHeight = 0.42f;
        /// <summary>Fraction of the screen height reserved at the very top for the level header.</summary>
        public float headerHeight = 0.13f;

        Camera cam;
        int mapLength = 10;
        int lastW, lastH;

        void Awake()
        {
            cam = GetComponent<Camera>();
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Palette.Background;
        }

        public void Fit(int length)
        {
            mapLength = length;
            Apply();
        }

        void Update()
        {
            if (Screen.width != lastW || Screen.height != lastH) Apply();
        }

        void Apply()
        {
            lastW = Screen.width;
            lastH = Screen.height;
            cam.rect = new Rect(0f, 1f - headerHeight - viewportHeight, 1f, viewportHeight);

            float aspect = cam.pixelHeight > 0 ? (float)cam.pixelWidth / cam.pixelHeight : 2.5f;
            float worldWidth = mapLength + 2.6f;        // cells + the two edge walls + margin
            float worldHeight = MapView.HighWallHeight + 1.6f;
            float sizeForWidth = worldWidth / aspect * 0.5f;
            cam.orthographicSize = Mathf.Max(sizeForWidth, worldHeight * 0.5f);

            // Center horizontally on the map; keep the ground line a little below the middle.
            float centerY = cam.orthographicSize - 1.3f;
            transform.position = new Vector3((mapLength - 1) * 0.5f, centerY, -10f);
        }

        public Vector2 WorldToCanvasPoint(Vector3 world, RectTransform canvas)
        {
            Vector3 screen = cam.WorldToScreenPoint(world);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvas, screen, null, out var local);
            return local;
        }
    }
}
