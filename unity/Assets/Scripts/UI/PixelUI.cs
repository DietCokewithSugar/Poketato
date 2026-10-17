using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Poketato
{
        /// <summary>
        /// GBA-style UI toolkit. One screen-space canvas is fixed at 480x320 (2x the GBA frame) and scaled by an
        /// integer, so windows, the pixel font and the game view share one sharp pixel grid. Layout uses top-left
        /// pixel coordinates like the GBA.
        /// </summary>
        public static class PixelUI
        {
            public const int Width = 480, Height = 320;
            public static Font Font;
            public static readonly Color Text = PixelArt.TextColor;
            public static readonly Color Shadow = PixelArt.TextShadow;
            public static readonly Color Light = new(0.97f, 0.97f, 0.97f);
            public static readonly Color DarkShadow = new(0.1f, 0.1f, 0.14f);

            public static RectTransform CreateCanvas(Camera cam, int sortingOrder)
            {
                var go = new GameObject("PixelCanvas", typeof(RectTransform));
                var canvas = go.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = sortingOrder;
                canvas.pixelPerfect = true;
                var scaler = go.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
                scaler.scaleFactor = 1;
                scaler.dynamicPixelsPerUnit = 1;
                scaler.referencePixelsPerUnit = PixelArt.UiPPU;
                go.AddComponent<GraphicRaycaster>();
                var rt = (RectTransform)go.transform;
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(Width, Height);
                rt.anchoredPosition = Vector2.zero;
                return rt;
            }

        public static void PointFilterFont()
        {
            if (Font && Font.material && Font.material.mainTexture) Font.material.mainTexture.filterMode = FilterMode.Point;
        }

        public static RectTransform Box(Transform parent, int x, int y, int w, int h, string name = "Box")
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(w, h);
            return rt;
        }

        public static RectTransform Fill(Transform parent, string name = "Fill")
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            return rt;
        }

        public static Image Window(Transform parent, int x, int y, int w, int h, string style = "win")
        {
            var rt = Box(parent, x, y, w, h, "Window");
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = PixelArt.Get(style);
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = 1f;
            img.raycastTarget = false;
            return img;
        }

        public static Image Solid(Transform parent, int x, int y, int w, int h, Color c)
        {
            var img = Box(parent, x, y, w, h, "Solid").gameObject.AddComponent<Image>();
            img.sprite = PixelArt.Get("white");
            img.color = c;
            img.raycastTarget = false;
            return img;
        }

        public static Image Picture(Transform parent, int x, int y, Sprite s)
        {
            var img = Box(parent, x, y, (int)s.rect.width, (int)s.rect.height, "Picture").gameObject.AddComponent<Image>();
            img.sprite = s;
            img.raycastTarget = false;
            return img;
        }

        public static Text Label(Transform parent, int x, int y, int w, int h, string text, Color? color = null, TextAnchor align = TextAnchor.UpperLeft, int size = 12, bool shadow = true, Color? shadowColor = null)
        {
            var rt = Box(parent, x, y, w, h, "Label");
            var t = rt.gameObject.AddComponent<Text>();
            t.font = Font;
            t.fontSize = size;
            t.text = text;
            t.color = color ?? Text;
            t.alignment = align;
            t.lineSpacing = 1.15f;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            t.supportRichText = true;
            if (shadow)
            {
                var sh = rt.gameObject.AddComponent<UnityEngine.UI.Shadow>();
                sh.effectColor = shadowColor ?? Shadow;
                sh.effectDistance = new Vector2(1, -1);
            }
            return t;
        }

        /// <summary>Emerald HP bar: orange "HP" tag + framed bar. Returns the fill image (Filled, horizontal).</summary>
        public static Image HpBar(Transform parent, int x, int y, int w, bool tag = true)
        {
            if (tag)
            {
                var tagImg = Box(parent, x, y, 15, 7, "HpTag").gameObject.AddComponent<Image>();
                tagImg.sprite = PixelArt.Get("hptag");
                tagImg.raycastTarget = false;
                x += 14;
            }
            var frame = Box(parent, x, y, w, 7, "HpFrame").gameObject.AddComponent<Image>();
            frame.sprite = PixelArt.Get("hpframe");
            frame.type = Image.Type.Sliced;
            frame.raycastTarget = false;
            return Bar(frame.transform, 2, 2, w - 4, 3, HpGreen);
        }

        public static Image Bar(Transform parent, int x, int y, int w, int h, Color c)
        {
            var img = Box(parent, x, y, w, h, "Bar").gameObject.AddComponent<Image>();
            img.sprite = PixelArt.Get("white");
            img.type = Image.Type.Filled;
            img.fillMethod = Image.FillMethod.Horizontal;
            img.color = c;
            img.raycastTarget = false;
            return img;
        }

        public static readonly Color HpGreen = new(0.44f, 0.97f, 0.66f);
        public static readonly Color HpYellow = new(0.97f, 0.88f, 0.22f);
        public static readonly Color HpRed = new(0.97f, 0.35f, 0.22f);
        public static readonly Color ExpBlue = new(0.25f, 0.78f, 0.97f);

        public static void SetHp(Image fill, float frac)
        {
            frac = Mathf.Clamp01(frac);
            // Fill in whole pixels so the bar steps like the GBA's.
            float w = fill.rectTransform.rect.width;
            fill.fillAmount = w > 0 ? Mathf.Ceil(frac * w) / w : frac;
            fill.color = frac > 0.5f ? HpGreen : frac > 0.2f ? HpYellow : HpRed;
        }
    }

    /// <summary>Vertical (or horizontal) list with the Emerald ▶ cursor; keyboard and mouse.</summary>
    public class PixelMenu
    {
        public class Entry
        {
            public RectTransform row;
            public Text label, right;
            public System.Action onSelect;
            public bool enabled = true;
        }

        public readonly List<Entry> entries = new();
        public int index;
        public bool horizontal;
        public System.Action<int> onHighlight;
        public System.Action onCancel;
        Image cursor;
        readonly RectTransform parent;
        readonly int x, y, w, rowH;

        public PixelMenu(RectTransform parent, int x, int y, int w, int rowH = 18)
        {
            this.parent = parent;
            this.x = x;
            this.y = y;
            this.w = w;
            this.rowH = rowH;
            cursor = PixelUI.Picture(parent, x, y, PixelArt.Get("cursor"));
        }

        public Entry Add(string label, System.Action onSelect, string right = null, bool enabled = true, Color? color = null)
        {
            int i = entries.Count;
            var row = PixelUI.Box(parent, x, y + i * rowH, w, rowH, "Entry" + i);
            var hit = row.gameObject.AddComponent<Image>();
            hit.color = new Color(0, 0, 0, 0);
            var btn = row.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.navigation = new Navigation { mode = Navigation.Mode.None };
            btn.onClick.AddListener(() => { Highlight(i); Confirm(); });
            row.gameObject.AddComponent<PixelHover>().onEnter = () => Highlight(i);
            var e = new Entry { row = row, onSelect = onSelect, enabled = enabled };
            var c = enabled ? color ?? PixelUI.Text : new Color(0.6f, 0.6f, 0.6f);
            e.label = PixelUI.Label(row, 10, 2, w - 10, rowH, label, c);
            if (right != null) e.right = PixelUI.Label(row, 10, 2, w - 14, rowH, right, c, TextAnchor.UpperRight);
            entries.Add(e);
            cursor.transform.SetAsLastSibling();
            Highlight(index);
            return e;
        }

        public void Highlight(int i)
        {
            if (entries.Count == 0) return;
            index = Mathf.Clamp(i, 0, entries.Count - 1);
            var r = entries[index].row;
            cursor.rectTransform.anchoredPosition = r.anchoredPosition + new Vector2(1, -4);
            onHighlight?.Invoke(index);
        }

        public void Move(int d)
        {
            if (entries.Count == 0) return;
            Highlight((index + d + entries.Count) % entries.Count);
        }

        public void Confirm()
        {
            if (entries.Count == 0) return;
            var e = entries[index];
            if (e.enabled) e.onSelect?.Invoke();
        }

        public void SetCursorVisible(bool on) => cursor.enabled = on;
    }

    public class PixelHover : MonoBehaviour, IPointerEnterHandler
    {
        public System.Action onEnter;
        public void OnPointerEnter(PointerEventData eventData) => onEnter?.Invoke();
    }
}
