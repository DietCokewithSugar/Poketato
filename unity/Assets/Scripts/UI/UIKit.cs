using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Poketato
{
    /// <summary>Helpers that build uGUI hierarchies from code (PMD-style navy panels with a double white border).</summary>
    public static class UIKit
    {
        public static Font Font;
        public static Sprite Fill, Border;
        public static readonly Color PanelColor = new(0.07f, 0.11f, 0.27f, 0.94f);
        public static readonly Color Accent = new(1f, 0.82f, 0.25f);

        public static RectTransform Rect(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
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

        /// <summary>Fixed-size rect anchored at a normalized point.</summary>
        public static RectTransform Box(Transform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            var rt = Rect(parent, name, anchor, anchor, Vector2.zero, Vector2.zero);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;
            return rt;
        }

        public static RectTransform Stretch(Transform parent, string name, float inset = 0) =>
            Rect(parent, name, Vector2.zero, Vector2.one, new Vector2(inset, inset), new Vector2(-inset, -inset));

        public static Image PanelImage(RectTransform rt, Color? color = null)
        {
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = Fill;
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = 0.5f;
            img.color = color ?? PanelColor;
            var border = Stretch(rt, "Border");
            border.SetAsFirstSibling();
            var b = border.gameObject.AddComponent<Image>();
            b.sprite = Border;
            b.type = Image.Type.Sliced;
            b.pixelsPerUnitMultiplier = 0.5f;
            b.raycastTarget = false;
            return img;
        }

        public static Image Solid(RectTransform rt, Color color)
        {
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        public static Text Label(Transform parent, string text, int size, Color color, TextAnchor align = TextAnchor.MiddleLeft, string name = "Label")
        {
            var rt = Stretch(parent, name);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = Font;
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.lineSpacing = 1.1f;
            var sh = rt.gameObject.AddComponent<Shadow>();
            sh.effectColor = new Color(0, 0, 0, 0.6f);
            sh.effectDistance = new Vector2(2, -2);
            return t;
        }

        public static Text LabelAt(Transform parent, string text, int size, Color color, Vector2 anchorMin, Vector2 anchorMax, Vector2 offMin, Vector2 offMax, TextAnchor align = TextAnchor.MiddleLeft)
        {
            var t = Label(parent, text, size, color, align);
            var rt = t.rectTransform;
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = offMin;
            rt.offsetMax = offMax;
            return t;
        }

        public static Button Button(Transform parent, string label, Vector2 anchor, Vector2 pos, Vector2 size, UnityAction onClick, int fontSize = 24)
        {
            var rt = Box(parent, "Btn_" + label, anchor, pos, size);
            var img = PanelImage(rt, new Color(0.13f, 0.22f, 0.5f, 0.97f));
            var b = rt.gameObject.AddComponent<Button>();
            b.targetGraphic = img;
            var cb = b.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = new Color(1.25f, 1.2f, 0.8f);
            cb.pressedColor = new Color(0.8f, 0.8f, 0.8f);
            cb.selectedColor = new Color(1.15f, 1.1f, 0.85f);
            cb.colorMultiplier = 1.3f;
            b.colors = cb;
            b.onClick.AddListener(onClick);
            var t = Label(rt, label, fontSize, Color.white, TextAnchor.MiddleCenter);
            t.rectTransform.offsetMin = new Vector2(8, 4);
            t.rectTransform.offsetMax = new Vector2(-8, -4);
            return b;
        }

        public static Image Bar(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 offMin, Vector2 offMax, Color fill, out RectTransform root)
        {
            root = Rect(parent, name, anchorMin, anchorMax, offMin, offMax);
            Solid(root, new Color(0, 0, 0, 0.6f));
            var f = Stretch(root, "Fill", 2);
            var img = Solid(f, fill);
            img.type = UnityEngine.UI.Image.Type.Filled;
            img.fillMethod = UnityEngine.UI.Image.FillMethod.Horizontal;
            img.sprite = Fill;
            return img;
        }

        public static string Stars(int tier) => new string('★', tier);
        public static readonly Color[] TierColors = { Color.white, new(0.85f, 0.9f, 1f), new(0.5f, 0.85f, 1f), new(0.85f, 0.55f, 1f), new(1f, 0.75f, 0.25f) };
    }
}
