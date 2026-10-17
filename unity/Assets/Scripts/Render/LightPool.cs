using System.Collections.Generic;
using UnityEngine;

namespace Poketato
{
    /// <summary>
    /// Fixed pool of dynamic point lights for projectiles and explosions. The cap is the main knob that keeps
    /// per-pixel lighting cost bounded on WebGL (URP Forward evaluates every light per object per pixel).
    /// </summary>
    public class LightPool : MonoBehaviour
    {
        class Slot
        {
            public Light light;
            public Transform follow;
            public Vector3 offset;
            public float t, life, intensity;
            public bool busy;
        }

        readonly List<Slot> slots = new();
        public int Capacity { get; private set; }
        public int ActiveCount { get; private set; }

        public void SetCapacity(int n)
        {
            while (slots.Count < n)
            {
                var go = new GameObject("FxLight" + slots.Count);
                go.transform.SetParent(transform, false);
                var l = go.AddComponent<Light>();
                l.type = LightType.Point;
                l.shadows = LightShadows.None;
                l.renderMode = LightRenderMode.ForcePixel;
                go.SetActive(false);
                slots.Add(new Slot { light = l });
            }
            for (int i = n; i < slots.Count; i++) Release(slots[i]);
            Capacity = n;
        }

        public void Flash(Vector3 pos, Color c, float intensity, float range, float life) =>
            Request(pos, null, c, intensity, range, life);

        public object Attach(Transform follow, Color c, float intensity, float range) =>
            Request(Vector3.zero, follow, c, intensity, range, -1f);

        object Request(Vector3 pos, Transform follow, Color c, float intensity, float range, float life)
        {
            for (int i = 0; i < Capacity; i++)
            {
                var s = slots[i];
                if (s.busy) continue;
                s.busy = true;
                s.follow = follow;
                s.t = 0;
                s.life = life;
                s.intensity = intensity;
                s.light.color = c;
                s.light.intensity = intensity;
                s.light.range = range;
                s.light.transform.position = follow ? follow.position : pos;
                s.light.gameObject.SetActive(true);
                return s;
            }
            return null;
        }

        public void Detach(object handle)
        {
            if (handle is Slot s) Release(s);
        }

        void Release(Slot s)
        {
            s.busy = false;
            s.follow = null;
            if (s.light) s.light.gameObject.SetActive(false);
        }

        void LateUpdate()
        {
            int active = 0;
            float dt = Time.deltaTime;
            for (int i = 0; i < slots.Count; i++)
            {
                var s = slots[i];
                if (!s.busy) continue;
                if (s.follow) s.light.transform.position = s.follow.position;
                if (s.life > 0)
                {
                    s.t += dt;
                    float k = 1f - s.t / s.life;
                    if (k <= 0) { Release(s); continue; }
                    s.light.intensity = s.intensity * k * k;
                }
                active++;
            }
            ActiveCount = active;
        }
    }
}
