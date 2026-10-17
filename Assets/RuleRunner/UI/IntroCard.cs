using System;
using RuleRunner.Core;
using UnityEngine;
using UnityEngine.UI;

namespace RuleRunner.UI
{
    /// <summary>
    /// Shown when a level opens for the first time (and from the "?" button): the level's one-line idea
    /// and a small diagram of what is new. The diagram is drawn by the Cards module into DiagramArea.
    /// </summary>
    public class IntroCard : MonoBehaviour
    {
        public event Action Closed;

        Text chip, title, line, newLabel;
        public RectTransform DiagramArea { get; private set; }

        public static IntroCard Create(Transform canvas)
        {
            var root = UIKit.Fill(canvas, "IntroCard");
            root.gameObject.AddComponent<Image>().color = new Color(0.03f, 0.04f, 0.06f, 0.6f);
            var card = root.gameObject.AddComponent<IntroCard>();

            var box = UIKit.Box(root, "Box", Theme.Surface, 20f, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            box.rectTransform.sizeDelta = new Vector2(600, 340);
            UIKit.DropShadow(box, 10f, 0.55f);
            var b = box.transform;

            UIKit.Box(b, "Accent", Theme.Gold, 4f, new Vector2(0, 1), new Vector2(1, 1), new Vector2(24, -10), new Vector2(-24, -4));
            var chipBox = UIKit.Box(b, "Chip", Theme.Gold, 10f, new Vector2(0, 1), new Vector2(0, 1), new Vector2(24, -62), new Vector2(84, -26));
            card.chip = UIKit.Label(chipBox.transform, "Text", "", 18, TextAnchor.MiddleCenter, Theme.Background, FontStyle.Bold);
            card.title = UIKit.Label(UIKit.TopStrip(b, "Title", 26f, 36f, 98f, 24f), "Text", "", 26, TextAnchor.MiddleLeft, Theme.Text, FontStyle.Bold);

            card.line = UIKit.Label(UIKit.TopStrip(b, "Line", 76f, 60f, 32f, 32f), "Text", "", 20, TextAnchor.MiddleCenter, Theme.Gold, FontStyle.Italic);

            card.newLabel = UIKit.Label(UIKit.TopStrip(b, "NewLabel", 142f, 18f, 32f, 32f), "Text", "NEW IN THIS LEVEL", 12, TextAnchor.MiddleCenter, Theme.Dim, FontStyle.Bold);
            var diagramBg = UIKit.Box(b, "DiagramBg", Theme.Surface2, 14f, new Vector2(0.5f, 1), new Vector2(0.5f, 1));
            var dbg = diagramBg.rectTransform;
            dbg.pivot = new Vector2(0.5f, 1);
            dbg.sizeDelta = new Vector2(420, 96);
            dbg.anchoredPosition = new Vector2(0, -164);
            card.DiagramArea = UIKit.Fill(dbg, "Diagram");

            var ok = UIKit.Button(b, "GotIt", "Got it", 18, Theme.Accent, card.Close, 12f);
            var okRt = (RectTransform)ok.transform;
            okRt.anchorMin = okRt.anchorMax = new Vector2(0.5f, 0);
            okRt.pivot = new Vector2(0.5f, 0);
            okRt.sizeDelta = new Vector2(180, 46);
            okRt.anchoredPosition = new Vector2(0, 20);

            root.gameObject.SetActive(false);
            return card;
        }

        public void Show(LevelDef level)
        {
            chip.text = level.Id;
            title.text = level.Title;
            line.text = level.Hint;
            UIKit.DestroyChildren(DiagramArea);
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
        }

        public bool IsOpen => gameObject.activeSelf;

        void Close()
        {
            gameObject.SetActive(false);
            Closed?.Invoke();
        }
    }
}
