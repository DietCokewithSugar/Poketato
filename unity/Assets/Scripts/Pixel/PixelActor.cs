using System.Collections.Generic;
using UnityEngine;

namespace Poketato
{
    /// <summary>
    /// A PMD-style Pokémon sprite (8 directions x N frames) on the 2D arena with a pixel blob shadow.
    /// Damage feedback blinks the sprite like the GBA games instead of tinting it.
    /// </summary>
    public class PixelActor : MonoBehaviour
    {
        public static readonly List<PixelActor> Active = new();

        public SpeciesInfo Species { get; private set; }
        public bool hover;

        SpriteRenderer body, shadow;
        Transform bodyT;
        string baseAnim = "Idle";
        AnimInfo cur;
        int frame, dir;
        float t, blink, animSpeed = 1f, hoverPhase;
        bool once, hold, dirty = true;

        public static PixelActor Create(SpeciesInfo sp, Transform parent)
        {
            var go = new GameObject(sp.id);
            go.transform.SetParent(parent, false);
            var a = go.AddComponent<PixelActor>();
            a.Init(sp);
            return a;
        }

        void Init(SpeciesInfo sp)
        {
            Species = sp;
            var sh = new GameObject("Shadow");
            sh.transform.SetParent(transform, false);
            shadow = sh.AddComponent<SpriteRenderer>();
            shadow.sprite = PixelArt.Get($"shadow_{Mathf.Clamp(sp.shadow, 0, 2)}");
            var b = new GameObject("Body");
            bodyT = b.transform;
            bodyT.SetParent(transform, false);
            body = b.AddComponent<SpriteRenderer>();
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
            blink = 0;
            animSpeed = 1f;
            body.color = Color.white;
            body.enabled = true;
            shadow.enabled = true;
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

        /// <summary>Facing in sim space (x right, y down). PMD rows: 0 down, 1 down-right, 2 right ... 7 down-left.</summary>
        public void SetFacing(Vector2 d)
        {
            if (d.sqrMagnitude < 0.0001f) return;
            float ang = Mathf.Atan2(d.y, d.x);
            int row = ((Mathf.RoundToInt((Mathf.PI / 2 - ang) / (Mathf.PI / 4)) % 8) + 8) % 8;
            if (row != dir) { dir = row; dirty = true; }
        }

        public void Blink(float seconds = 0.25f) => blink = Mathf.Max(blink, seconds);
        public void SetTint(Color c) => body.color = new Color(c.r, c.g, c.b, body.color.a);
        public void SetAlpha(float a) { var c = body.color; c.a = a; body.color = c; shadow.enabled = a > 0.5f; }

        /// <summary>Places the actor at a sim position (pixels), snapped to the pixel grid and depth-sorted by y.</summary>
        public void Place(Vector2 simPx)
        {
            float x = Mathf.Round(simPx.x), y = Mathf.Round(simPx.y);
            transform.position = new Vector3((x - GameData.ArenaW * 0.5f) / PixelArt.WorldPPU, (GameData.ArenaH * 0.5f - y) / PixelArt.WorldPPU, 0);
            int order = ArenaView.ActorOrder + (int)y;
            body.sortingOrder = order;
            shadow.sortingOrder = ArenaView.ShadowOrder;
        }

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
            if (blink > 0)
            {
                blink -= dt;
                body.enabled = blink <= 0 || Mathf.Repeat(blink * 30f, 2f) < 1f;
            }
            if (dirty)
            {
                dirty = false;
                body.sprite = Species.Frame(cur, Mathf.Min(frame, cur.frames - 1), Mathf.Min(dir, cur.rows - 1));
            }
            float lift = hover ? 6f + Mathf.Round(Mathf.Sin(Time.time * 3f + hoverPhase)) : 0f;
            bodyT.localPosition = new Vector3(0, lift / PixelArt.WorldPPU, 0);
        }
    }
}
