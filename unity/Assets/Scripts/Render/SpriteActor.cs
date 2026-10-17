using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Poketato
{
    /// <summary>
    /// One PMD-style sprite (8 directions x N frames per animation) drawn as an upright quad in the 3D scene.
    /// Every instance of a species shares one material, the frame is selected through an instanced _UVRect so
    /// hordes are drawn with GPU instancing.
    /// </summary>
    public class SpriteActor : MonoBehaviour
    {
        public static readonly List<SpriteActor> Active = new();
        public static Quaternion Billboard = Quaternion.identity;
        public const float Scale = 1.3f;

        static readonly int UVRectId = Shader.PropertyToID("_UVRect");
        static readonly int TintId = Shader.PropertyToID("_Tint");
        static readonly int FlashId = Shader.PropertyToID("_Flash");

        public SpeciesInfo Species { get; private set; }
        public bool hover;
        public float hoverHeight = 0.55f;

        MeshRenderer mr;
        Transform quadT;
        MaterialPropertyBlock mpb;
        string baseAnim = "Idle";
        AnimInfo cur;
        int frame, dir;
        float t, flash, alpha = 1f, emission, hoverPhase, animSpeed = 1f;
        bool once, hold;
        Color tint = Color.white;
        bool dirty = true;

        public static SpriteActor Create(SpeciesInfo sp, Transform parent)
        {
            var go = new GameObject(sp.id);
            go.transform.SetParent(parent, false);
            var a = go.AddComponent<SpriteActor>();
            a.Init(sp);
            return a;
        }

        void Init(SpeciesInfo sp)
        {
            Species = sp;
            var q = new GameObject("Quad");
            quadT = q.transform;
            quadT.SetParent(transform, false);
            q.AddComponent<MeshFilter>().sharedMesh = SpriteDb.Quad;
            mr = q.AddComponent<MeshRenderer>();
            mr.sharedMaterial = sp.material;
            mr.shadowCastingMode = ShadowCastingMode.On;
            mr.receiveShadows = true;
            mr.lightProbeUsage = LightProbeUsage.Off;
            mr.reflectionProbeUsage = ReflectionProbeUsage.Off;
            mpb = new MaterialPropertyBlock();
            hover = sp.hover;
            hoverPhase = Random.value * 10f;
            ResetState();
        }

        void OnEnable() => Active.Add(this);
        void OnDisable() => Active.Remove(this);

        public void ResetState()
        {
            baseAnim = "Idle";
            once = hold = false;
            flash = 0;
            alpha = 1;
            emission = 0;
            tint = Color.white;
            animSpeed = 1f;
            StartAnim("Idle");
        }

        public bool Busy => once && !hold;

        public void SetBase(string anim, float speed = 1f)
        {
            animSpeed = speed;
            if (baseAnim == anim) return;
            baseAnim = anim;
            if (!once) StartAnim(anim);
        }

        public void Play(string anim, bool holdLast = false)
        {
            once = true;
            hold = holdLast;
            StartAnim(anim);
        }

        void StartAnim(string anim)
        {
            cur = Species.Anim(anim);
            frame = 0;
            t = 0;
            dirty = true;
        }

        /// <summary>Facing in sim/screen space (x right, y down).</summary>
        public void SetFacing(Vector2 d)
        {
            if (d.sqrMagnitude < 0.0001f) return;
            float ang = Mathf.Atan2(d.y, d.x);
            int row = ((Mathf.RoundToInt((Mathf.PI / 2 - ang) / (Mathf.PI / 4)) % 8) + 8) % 8;
            if (row != dir) { dir = row; dirty = true; }
        }

        public void Flash(float amount = 1f) { flash = Mathf.Max(flash, amount); dirty = true; }
        public void SetAlpha(float a) { if (!Mathf.Approximately(a, alpha)) { alpha = a; dirty = true; } }
        public void SetTint(Color c) { tint = c; dirty = true; }
        public void SetEmission(float e) { if (!Mathf.Approximately(e, emission)) { emission = e; dirty = true; } }

        public static void TickAll(float dt)
        {
            for (int i = Active.Count - 1; i >= 0; i--) Active[i].Tick(dt);
        }

        void Tick(float dt)
        {
            if (cur.durations.Length > 1 || once)
            {
                t += dt * 60f * animSpeed;
                float d = cur.durations[Mathf.Min(frame, cur.durations.Length - 1)];
                while (t >= d)
                {
                    t -= d;
                    if (frame + 1 >= cur.frames)
                    {
                        if (once)
                        {
                            if (hold) { t = 0; break; }
                            once = false;
                            StartAnim(baseAnim);
                            break;
                        }
                        frame = 0;
                    }
                    else frame++;
                    dirty = true;
                    d = cur.durations[Mathf.Min(frame, cur.durations.Length - 1)];
                }
            }
            if (flash > 0) { flash = Mathf.Max(0, flash - dt * 8f); dirty = true; }
            if (hover || dirty) Apply();
        }

        void Apply()
        {
            dirty = false;
            var a = cur;
            var uv = Species.FrameUV(a, Mathf.Min(frame, a.frames - 1), Mathf.Min(dir, a.rows - 1));
            mpb.SetVector(UVRectId, new Vector4(uv.x, uv.y, uv.width, uv.height));
            mpb.SetVector(TintId, new Vector4(tint.r, tint.g, tint.b, alpha));
            mpb.SetVector(FlashId, new Vector4(flash, emission, 0, 0));
            mr.SetPropertyBlock(mpb);

            float k = Scale / GameData.PxPerUnit;
            float w = a.w * k, h = a.h * k;
            float axU = (a.ax + 0.5f) * k, ayU = (a.h - (a.ay + 0.5f)) * k;
            float lift = hover ? hoverHeight + Mathf.Sin(Time.time * 2.6f + hoverPhase) * 0.08f : 0f;
            quadT.localRotation = Billboard;
            quadT.localScale = new Vector3(w, h, 1f);
            quadT.localPosition = Billboard * new Vector3(-axU, -ayU, 0) + Vector3.up * lift;
        }

        public void SetShadows(bool on) => mr.shadowCastingMode = on ? ShadowCastingMode.On : ShadowCastingMode.Off;
    }
}
