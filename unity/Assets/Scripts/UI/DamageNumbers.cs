using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Poketato
{
    /// <summary>Pooled floating numbers drawn on a screen-space canvas and positioned from world space.</summary>
    public class DamageNumbers : MonoBehaviour
    {
        public enum Kind { Normal, Crit, Super, Player, Heal, Dodge }

        class Item
        {
            public Text text;
            public RectTransform rt;
            public Vector3 world;
            public float t;
            public bool live;
        }

        public Camera cam;
        public int capacity = 70;
        readonly List<Item> items = new();
        int next;
        RectTransform root;

        public void Init(RectTransform parent, Font font)
        {
            root = parent;
            for (int i = 0; i < capacity; i++)
            {
                var go = new GameObject("Num", typeof(RectTransform));
                go.transform.SetParent(root, false);
                var t = go.AddComponent<Text>();
                t.font = font;
                t.fontSize = 24;
                t.alignment = TextAnchor.MiddleCenter;
                t.horizontalOverflow = HorizontalWrapMode.Overflow;
                t.verticalOverflow = VerticalWrapMode.Overflow;
                t.raycastTarget = false;
                var o = go.AddComponent<Outline>();
                o.effectColor = new Color(0, 0, 0, 0.85f);
                o.effectDistance = new Vector2(2, -2);
                go.SetActive(false);
                items.Add(new Item { text = t, rt = (RectTransform)go.transform });
            }
        }

        public void Show(Vector3 world, float amount, Kind kind)
        {
            if (items.Count == 0) return;
            var it = items[next];
            next = (next + 1) % items.Count;
            it.world = world + new Vector3(Random.Range(-0.3f, 0.3f), 0, 0);
            it.t = 0;
            it.live = true;
            int n = Mathf.Max(1, Mathf.RoundToInt(amount));
            switch (kind)
            {
                case Kind.Crit: it.text.text = n + "!"; it.text.color = new Color(1f, 0.85f, 0.2f); it.text.fontSize = 30; break;
                case Kind.Super: it.text.text = n.ToString(); it.text.color = new Color(1f, 0.6f, 0.35f); it.text.fontSize = 24; break;
                case Kind.Player: it.text.text = "-" + n; it.text.color = new Color(1f, 0.3f, 0.3f); it.text.fontSize = 28; break;
                case Kind.Heal: it.text.text = "+" + n; it.text.color = new Color(0.45f, 1f, 0.5f); it.text.fontSize = 24; break;
                case Kind.Dodge: it.text.text = "闪避"; it.text.color = new Color(0.7f, 0.85f, 1f); it.text.fontSize = 22; break;
                default: it.text.text = n.ToString(); it.text.color = Color.white; it.text.fontSize = 22; break;
            }
            it.text.gameObject.SetActive(true);
        }

        public void ClearAll()
        {
            foreach (var it in items) { it.live = false; it.text.gameObject.SetActive(false); }
        }

        void LateUpdate()
        {
            if (!cam) return;
            float dt = Time.deltaTime;
            float scale = root.lossyScale.x > 0 ? 1f / root.lossyScale.x : 1f;
            foreach (var it in items)
            {
                if (!it.live) continue;
                it.t += dt;
                if (it.t > 0.7f) { it.live = false; it.text.gameObject.SetActive(false); continue; }
                Vector3 sp = cam.WorldToScreenPoint(it.world + Vector3.up * it.t * 1.2f);
                if (sp.z < 0) { it.text.enabled = false; continue; }
                it.text.enabled = true;
                it.rt.anchoredPosition = new Vector2(sp.x, sp.y) * scale;
                var c = it.text.color;
                c.a = Mathf.Clamp01(1.4f - it.t * 2f);
                it.text.color = c;
            }
        }
    }
}
