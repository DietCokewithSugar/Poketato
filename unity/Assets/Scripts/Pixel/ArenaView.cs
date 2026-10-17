using System.Collections.Generic;
using UnityEngine;

namespace Poketato
{
    /// <summary>2D presentation of the simulation on the pixel arena (sprites, pixel effects, digits, weather).</summary>
    public class ArenaView : MonoBehaviour, ISimListener
    {
        public const int GroundOrder = -3000, DecorOrder = -2900, ShadowOrder = -2000, ActorOrder = 0, FxOrder = 2000, NumberOrder = 3000;

        public GameSim sim;
        public CameraFollow2D cam;
        public System.Action<string> onMessage;
        public System.Action<int> onLevelUp;

        public PixelActor PlayerActor { get; private set; }
        public int FxCount => fx.Count;

        Transform actorRoot, fxRoot;
        readonly Dictionary<int, Stack<PixelActor>> actorPool = new();
        readonly List<(PixelActor a, float t, float life)> dying = new();

        class Fx
        {
            public GameObject go;
            public SpriteRenderer sr;
            public Vector3 pos, vel;
            public float t, life, s0 = 1, s1 = 1, fps, rot;
            public Sprite[] frames;
            public bool blinkEnd = true, fade;
            public Color color = Color.white;
            public float gravity;
            public object owner;
            public bool hold;
        }

        class ProjView
        {
            public Fx fx;
            public float trailT;
            public Color tint;
        }

        class Num
        {
            public GameObject go;
            public readonly List<SpriteRenderer> digits = new();
            public Vector3 pos;
            public float t;
            public bool live;
        }

        class ZoneView
        {
            public Zone zone;
            public Fx disc;
            public float spawnT;
            public Color color;
            public string particle;
        }

        readonly List<Fx> fx = new();
        readonly Stack<Fx> fxPool = new();
        readonly List<Pickup> livePickups = new();
        readonly List<Proj> liveProj = new();
        readonly List<ZoneView> zones = new();
        readonly List<Num> nums = new();
        int nextNum;

        Rect rainRect;
        float rainAcc, lavaAcc;

        public static Vector3 W(Vector2 px) => new((Mathf.Round(px.x) - GameData.ArenaW * 0.5f) / PixelArt.WorldPPU, (GameData.ArenaH * 0.5f - Mathf.Round(px.y)) / PixelArt.WorldPPU, 0);

        public void Init()
        {
            PixelArt.Build();
            actorRoot = new GameObject("Actors").transform;
            actorRoot.SetParent(transform, false);
            fxRoot = new GameObject("Fx").transform;
            fxRoot.SetParent(transform, false);
            for (int i = 0; i < 60; i++)
            {
                var n = new Num { go = new GameObject("Num") };
                n.go.transform.SetParent(fxRoot, false);
                for (int d = 0; d < 5; d++)
                {
                    var c = new GameObject("d" + d);
                    c.transform.SetParent(n.go.transform, false);
                    c.transform.localPosition = new Vector3(d * 8f / PixelArt.WorldPPU, 0, 0);
                    var sr = c.AddComponent<SpriteRenderer>();
                    sr.sortingOrder = NumberOrder;
                    n.digits.Add(sr);
                }
                n.go.SetActive(false);
                nums.Add(n);
            }
            rainRect = ArenaMap.FieldRectPx(Fields.All[0]);
        }

        // ------------------------------------------------------------------ actors

        public PixelActor GetActor(int dex)
        {
            if (actorPool.TryGetValue(dex, out var st) && st.Count > 0)
            {
                var a = st.Pop();
                a.gameObject.SetActive(true);
                a.ResetState();
                return a;
            }
            return PixelActor.Create(SpriteDb.Get(dex), actorRoot);
        }

        public void ReleaseActor(PixelActor a)
        {
            a.gameObject.SetActive(false);
            if (!actorPool.TryGetValue(a.Species.dex, out var st)) actorPool[a.Species.dex] = st = new Stack<PixelActor>();
            st.Push(a);
        }

        public void SpawnPlayer()
        {
            if (PlayerActor) ReleaseActor(PlayerActor);
            PlayerActor = GetActor(sim.player.line.dex);
            sim.player.view = PlayerActor;
            PlayerActor.Place(sim.player.pos);
            if (cam) { cam.target = PlayerActor.transform; cam.Snap(); }
        }

        public void Sync(float dt)
        {
            var p = sim.player;
            if (PlayerActor && p != null)
            {
                PlayerActor.Place(p.pos);
                PlayerActor.SetFacing(p.facing);
                PlayerActor.SetBase(p.moving ? "Walk" : "Idle");
                PlayerActor.SetAlpha(p.invuln > 0 && sim.phase == SimPhase.Wave && Mathf.Repeat(Time.time * 16f, 1f) > 0.5f ? 0.35f : 1f);
            }
            foreach (var e in sim.enemies)
            {
                if (e.view is not PixelActor a) continue;
                a.Place(e.pos);
                a.SetFacing(e.move.sqrMagnitude > 0.01f ? e.move : p.pos - e.pos);
                a.SetBase(e.move.sqrMagnitude > 0.01f ? "Walk" : "Idle", e.paraT > 0 || e.slowT > 0 ? 0.5f : 1f);
                a.SetTint(e.burnT > 0 || e.fieldBurnT > 0 ? new Color(1f, 0.7f, 0.55f) : e.paraT > 0 ? new Color(1f, 1f, 0.5f) : e.poisonT > 0 ? new Color(0.85f, 0.6f, 1f) : Color.white);
            }
            foreach (var pr in liveProj)
            {
                if (pr.view is not ProjView v) continue;
                v.fx.pos = W(pr.pos) + new Vector3(0, (pr.enemy ? 6f : 8f) / PixelArt.WorldPPU, 0);
                v.trailT -= dt;
                if (v.trailT <= 0 && !pr.enemy)
                {
                    v.trailT = 0.035f;
                    var d = Spawn(v.fx.pos, "dot2", 0.16f, v.tint);
                    d.fade = true;
                }
            }
            float time = Time.time;
            foreach (var pk in livePickups)
            {
                if (pk.view is Fx f) f.pos = W(pk.pos) + new Vector3(0, (Mathf.Repeat(time * 2f + pk.id * 0.37f, 1f) > 0.5f ? 1f : 0f) / PixelArt.WorldPPU, 0);
            }
            for (int i = dying.Count - 1; i >= 0; i--)
            {
                var (a, t, life) = dying[i];
                t += dt;
                if (t > life * 0.45f) a.Blink(0.1f);
                if (t >= life) { dying.RemoveAt(i); ReleaseActor(a); }
                else dying[i] = (a, t, life);
            }
            UpdateZones(dt);
            UpdateWeather(dt);
            UpdateFx(dt);
            UpdateNumbers(dt);
        }

        // ------------------------------------------------------------------ ISimListener

        public void EnemySpawned(Enemy e)
        {
            var a = GetActor(e.def.dex);
            a.Place(e.pos);
            e.view = a;
        }

        public void EnemyRemoved(Enemy e, bool killed)
        {
            if (e.view is not PixelActor a) return;
            e.view = null;
            if (killed)
            {
                a.Play("Faint", true);
                dying.Add((a, 0f, 0.7f));
                Sparks(W(e.pos) + Up(8), Types.ColorOf(e.def.types[0]), 6, 50f);
            }
            else dying.Add((a, 0.3f, 0.6f));
        }

        public void EnemyHit(Enemy e, float dmg, bool crit, float eff, PType type)
        {
            if (e.view is PixelActor a)
            {
                a.Blink(0.2f);
                if (!a.Busy && e.def.rank == Rank.Minion) a.Play("Hurt");
            }
            Sparks(W(e.pos) + Up(10), Types.ColorOf(type), crit ? 5 : 3, crit ? 70f : 45f);
            ShowNumber(W(e.pos) + Up(18), dmg, crit ? new Color(1f, 0.9f, 0.25f) : eff > 1f ? new Color(1f, 0.62f, 0.35f) : Color.white);
        }

        public void PlayerHurt(float dmg, bool dodged)
        {
            if (!PlayerActor) return;
            PlayerActor.Blink(0.35f);
            if (dmg > 0)
            {
                PlayerActor.Play("Hurt");
                ShowNumber(W(sim.player.pos) + Up(22), dmg, new Color(1f, 0.38f, 0.32f));
                cam?.Shake(1f, 0.12f);
            }
        }

        public void PlayerHealed(float amount) => ShowNumber(W(sim.player.pos) + Up(22), amount, new Color(0.45f, 1f, 0.5f), true);

        static Vector3 Up(float px) => new(0, px / PixelArt.WorldPPU, 0);

        static Color Tint(PType t) => Color.Lerp(Color.white, Types.ColorOf(t), 0.65f);

        public void ProjSpawned(Proj p)
        {
            string sprite;
            bool rotate = false, anim = false;
            Color tint = Color.white;
            if (p.enemy)
            {
                sprite = p.type == PType.Bug ? "needle" : "acid";
                rotate = sprite == "needle";
            }
            else switch (p.skill?.id)
            {
                case "ember": sprite = "ember"; anim = true; break;
                case "water_gun": sprite = "drop"; rotate = true; break;
                case "bubble_beam": sprite = "bubble"; break;
                case "water_pulse": sprite = "orbBig"; tint = new Color(0.55f, 0.75f, 1f); break;
                case "leech_seed": sprite = "seed"; break;
                case "razor_leaf": sprite = "leaf"; rotate = true; break;
                case "thunder_shock": sprite = "spark"; break;
                case "electro_ball": sprite = "orbBig"; tint = new Color(1f, 0.95f, 0.45f); break;
                case "swift": sprite = "star"; break;
                default: sprite = "orb"; tint = Tint(p.type); break;
            }
            var f = anim ? SpawnAnim(W(p.pos), sprite, 2, 10f, 999f, Color.white) : Spawn(W(p.pos), sprite, 999f, tint);
            f.blinkEnd = false;
            f.hold = true;
            f.sr.sortingOrder = FxOrder + 1;
            if (rotate) f.rot = Mathf.Round(Mathf.Atan2(-p.vel.y, p.vel.x) * Mathf.Rad2Deg / 45f) * 45f;
            p.view = new ProjView { fx = f, tint = p.enemy ? new Color(0.8f, 0.5f, 1f) : Tint(p.type) };
            liveProj.Add(p);
            if (!p.enemy && PlayerActor && !PlayerActor.Busy) PlayerActor.Play("Shoot");
        }

        public void ProjRemoved(Proj p)
        {
            liveProj.Remove(p);
            if (p.view is not ProjView v) return;
            p.view = null;
            v.fx.life = 0;
            v.fx.hold = false;
            if (!p.enemy) Sparks(W(p.pos), v.tint, 2, 35f);
        }

        public void Melee(Vector2 pos, float angle, float arc, float radius, PType type)
        {
            var c = Tint(type);
            if (arc >= Mathf.PI * 1.9f)
            {
                var r = Spawn(W(pos) + Up(6), "ring48", 0.2f, c);
                r.s0 = radius / 24f * 0.6f;
                r.s1 = radius / 24f;
            }
            else
            {
                var s = Spawn(W(pos) + Up(8), "slash", 0.16f, c);
                s.s0 = s.s1 = Mathf.Max(1f, Mathf.Round(radius / 12f * 2f) / 2f);
                s.rot = Mathf.Round(-angle * Mathf.Rad2Deg / 45f) * 45f;
            }
            if (PlayerActor && !PlayerActor.Busy) PlayerActor.Play("Attack");
        }

        public void Nova(Vector2 pos, float radius, PType type)
        {
            var r = Spawn(W(pos) + Up(4), "ring96", 0.3f, Tint(type));
            r.s0 = 0.15f;
            r.s1 = radius / 48f;
            if (PlayerActor && !PlayerActor.Busy) PlayerActor.Play("Attack");
        }

        public void Beam(Vector2 pos, float angle, float len, float width, PType type)
        {
            var dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            bool fire = type == PType.Fire;
            for (float d = 6; d < len; d += fire ? 7 : 5)
            {
                var p = W(pos + dir * d) + Up(8);
                var f = fire ? SpawnAnim(p, "flame", 3, 14f, 0.24f, Color.white) : Spawn(p, "beam", 0.2f, Tint(type));
                if (!fire) f.rot = -angle * Mathf.Rad2Deg;
                f.vel = Up(Random.Range(4f, 14f));
            }
            if (PlayerActor && !PlayerActor.Busy) PlayerActor.Play("Shoot");
        }

        public void Chain(List<Vector2> points, PType type)
        {
            var c = Tint(type);
            for (int i = 1; i < points.Count; i++)
            {
                Vector2 a = points[i - 1], b = points[i];
                Vector2 mid = (a + b) * 0.5f + new Vector2(Random.Range(-8f, 8f), Random.Range(-8f, 8f));
                foreach (var (from, to) in new[] { (a, mid), (mid, b) })
                {
                    float len = Vector2.Distance(from, to);
                    for (float d = 0; d < len; d += 3)
                        Spawn(W(Vector2.Lerp(from, to, d / len)) + Up(10), "dot2", 0.15f, c);
                }
                Sparks(W(b) + Up(10), c, 3, 40f);
            }
        }

        public void StrikeWarn(Strike s)
        {
            var r = Spawn(W(s.pos), "ring48", s.delay, new Color(1f, 0.45f, 0.35f));
            r.s0 = r.s1 = s.radius / 24f;
            r.blinkEnd = false;
            s.view = r;
            r.fps = -1;
        }

        public void StrikeBoom(Strike s)
        {
            if (s.view is Fx r) r.life = 0;
            var b = SpawnAnim(W(s.pos) + Up(4), "burst", 4, 16f, 0.26f, Tint(s.type));
            b.s0 = b.s1 = Mathf.Max(1f, Mathf.Round(s.radius / 16f));
            Sparks(W(s.pos) + Up(4), Tint(s.type), 8, 80f);
            cam?.Shake(1f, 0.1f);
        }

        public void ZoneStart(Zone z)
        {
            var c = Tint(z.type);
            var d = Spawn(W(z.pos), "disc48", z.t + 0.2f, new Color(c.r, c.g, c.b, 0.85f));
            d.s0 = d.s1 = z.radius / 24f;
            d.blinkEnd = true;
            d.sr.sortingOrder = ShadowOrder + 10;
            zones.Add(new ZoneView { zone = z, disc = d, color = c, particle = z.type == PType.Fire ? "flame" : "puff" });
        }

        public void ZoneEnd(Zone z)
        {
            for (int i = zones.Count - 1; i >= 0; i--)
                if (zones[i].zone == z)
                {
                    zones[i].disc.life = Mathf.Min(zones[i].disc.life, zones[i].disc.t + 0.15f);
                    zones.RemoveAt(i);
                }
        }

        void UpdateZones(float dt)
        {
            foreach (var zv in zones)
            {
                zv.spawnT -= dt;
                if (zv.spawnT > 0) continue;
                zv.spawnT = 0.09f;
                Vector2 o = Random.insideUnitCircle * zv.zone.radius;
                var f = SpawnAnim(W(zv.zone.pos + o), zv.particle, 3, 10f, 0.35f, zv.particle == "flame" ? Color.white : zv.color);
                f.vel = Up(18f);
            }
        }

        public void ShieldUp(float amount)
        {
            var r = Spawn(W(sim.player.pos) + Up(10), "ring48", 0.5f, new Color(0.6f, 0.85f, 1f));
            r.s0 = 0.4f;
            r.s1 = 0.9f;
        }

        public void Buff()
        {
            for (int i = 0; i < 8; i++)
            {
                var f = Spawn(W(sim.player.pos) + new Vector3(Random.Range(-0.8f, 0.8f), Random.Range(0f, 1.2f), 0), "dot3", 0.5f, new Color(1f, 0.95f, 0.5f));
                f.vel = Up(30f);
            }
        }

        public void PickupSpawned(Pickup p)
        {
            var f = Spawn(W(p.pos), p.kind == PickupKind.Candy ? "candy" : "berry", 9999f, Color.white);
            f.hold = true;
            f.blinkEnd = false;
            f.sr.sortingOrder = ShadowOrder + 20;
            p.view = f;
            livePickups.Add(p);
        }

        public void PickupCollected(Pickup p)
        {
            livePickups.Remove(p);
            if (p.view is Fx f) { f.hold = false; f.life = 0; }
            p.view = null;
        }

        public void SpawnWarn(PendingSpawn s)
        {
            var f = Spawn(W(s.pos), "warn", s.t + 0.05f, Color.white);
            f.fps = -1;
            f.sr.sortingOrder = ShadowOrder + 30;
            s.view = f;
        }

        public void SpawnWarnEnd(PendingSpawn s)
        {
            if (s.view is Fx f) f.life = 0;
        }

        public void DashWarn(Enemy e, Vector2 dir, float len, float time)
        {
            for (float d = 8; d < len; d += 8)
            {
                var f = Spawn(W(e.pos + dir * d), "dot3", time, new Color(1f, 0.3f, 0.25f));
                f.fps = -1;
                f.sr.sortingOrder = ShadowOrder + 30;
            }
        }

        public void LevelUp(int level)
        {
            var r = Spawn(W(sim.player.pos) + Up(8), "ring96", 0.6f, new Color(1f, 0.9f, 0.4f));
            r.s0 = 0.2f;
            r.s1 = 0.8f;
            Buff();
            onLevelUp?.Invoke(level);
        }

        public void Message(string text) => onMessage?.Invoke(text);

        // ------------------------------------------------------------------ fx core

        Fx Take()
        {
            if (fxPool.Count > 0)
            {
                var f = fxPool.Pop();
                f.go.SetActive(true);
                return f;
            }
            var go = new GameObject("Fx");
            go.transform.SetParent(fxRoot, false);
            return new Fx { go = go, sr = go.AddComponent<SpriteRenderer>() };
        }

        Fx Spawn(Vector3 pos, string sprite, float life, Color color)
        {
            var f = Take();
            f.pos = pos;
            f.vel = Vector3.zero;
            f.t = 0;
            f.life = life;
            f.s0 = f.s1 = 1;
            f.fps = 0;
            f.rot = 0;
            f.frames = null;
            f.blinkEnd = true;
            f.fade = false;
            f.hold = false;
            f.gravity = 0;
            f.color = color;
            f.sr.sprite = PixelArt.Get(sprite);
            f.sr.color = color;
            f.sr.enabled = true;
            f.sr.sortingOrder = FxOrder;
            fx.Add(f);
            Apply(f);
            return f;
        }

        Fx SpawnAnim(Vector3 pos, string name, int frames, float fps, float life, Color color)
        {
            var f = Spawn(pos, $"{name}_0", life, color);
            f.frames = PixelArt.Frames(name, frames);
            f.fps = fps;
            return f;
        }

        void Sparks(Vector3 at, Color c, int n, float speed)
        {
            for (int i = 0; i < n; i++)
            {
                var f = Spawn(at, "dot2", Random.Range(0.18f, 0.32f), c);
                Vector2 d = Random.insideUnitCircle.normalized * Random.Range(0.5f, 1f) * speed / PixelArt.WorldPPU;
                f.vel = new Vector3(d.x, Mathf.Abs(d.y) + 1.5f, 0);
                f.gravity = -9f;
            }
        }

        static void Apply(Fx f)
        {
            float k = PixelArt.WorldPPU;
            f.go.transform.position = new Vector3(Mathf.Round(f.pos.x * k) / k, Mathf.Round(f.pos.y * k) / k, 0);
            float u = f.life > 0 ? Mathf.Clamp01(f.t / f.life) : 1;
            float s = Mathf.Lerp(f.s0, f.s1, 1f - (1f - u) * (1f - u));
            f.go.transform.localScale = new Vector3(s, s, 1);
            f.go.transform.rotation = Quaternion.Euler(0, 0, f.rot);
        }

        void UpdateFx(float dt)
        {
            for (int i = fx.Count - 1; i >= 0; i--)
            {
                var f = fx[i];
                f.t += dt;
                if (!f.hold && f.t >= f.life)
                {
                    f.go.SetActive(false);
                    fx.RemoveAt(i);
                    fxPool.Push(f);
                    continue;
                }
                f.vel.y += f.gravity * dt;
                f.pos += f.vel * dt;
                if (f.frames != null && f.fps > 0) f.sr.sprite = f.frames[Mathf.Max(0, (int)(f.t * f.fps)) % f.frames.Length];
                if (f.fps < 0) f.sr.enabled = Mathf.Repeat(f.t * 8f, 1f) < 0.6f;
                else if (f.blinkEnd && !f.hold) f.sr.enabled = f.life - f.t > 0.08f || Mathf.Repeat(f.t * 30f, 2f) < 1f;
                if (f.fade)
                {
                    var c = f.color;
                    c.a *= 1f - Mathf.Clamp01(f.t / f.life);
                    f.sr.color = c;
                }
                Apply(f);
            }
        }

        // ------------------------------------------------------------------ numbers

        void ShowNumber(Vector3 at, float value, Color color, bool plus = false)
        {
            var n = nums[nextNum];
            nextNum = (nextNum + 1) % nums.Count;
            string s = (plus ? "+" : "") + Mathf.Max(1, Mathf.RoundToInt(value));
            for (int i = 0; i < n.digits.Count; i++)
            {
                var d = n.digits[i];
                d.enabled = i < s.Length;
                if (!d.enabled) continue;
                d.sprite = PixelArt.Get(s[i] == '+' ? "digit_plus" : $"digit_{s[i]}");
                d.color = color;
            }
            n.pos = at - new Vector3(s.Length * 4f / PixelArt.WorldPPU, 0, 0);
            n.t = 0;
            n.live = true;
            n.go.SetActive(true);
        }

        void UpdateNumbers(float dt)
        {
            float k = PixelArt.WorldPPU;
            foreach (var n in nums)
            {
                if (!n.live) continue;
                n.t += dt;
                if (n.t > 0.6f) { n.live = false; n.go.SetActive(false); continue; }
                float rise = Mathf.Min(10f, n.t * 40f);
                var p = n.pos + Up(rise);
                n.go.transform.position = new Vector3(Mathf.Round(p.x * k) / k, Mathf.Round(p.y * k) / k, 0);
                bool on = n.t < 0.45f || Mathf.Repeat(n.t * 30f, 2f) < 1f;
                foreach (var d in n.digits) if (d.sprite) d.gameObject.SetActive(on);
            }
        }

        public void ClearNumbers()
        {
            foreach (var n in nums) { n.live = false; n.go.SetActive(false); }
        }

        // ------------------------------------------------------------------ weather

        void UpdateWeather(float dt)
        {
            if (sim.player == null) return;
            var rain = Fields.All[0];
            // Rain only near the camera: the field is small and drops are cheap, but there is no point drawing them off-screen.
            var camPos = cam ? (Vector2)cam.transform.position : Vector2.zero;
            var center = W(rainRect.center);
            if (Mathf.Abs(center.x - camPos.x) < 30 && Mathf.Abs(center.y - camPos.y) < 22)
            {
                rainAcc += dt * 70f;
                while (rainAcc >= 1f)
                {
                    rainAcc -= 1f;
                    var land = new Vector2(Random.Range(rainRect.xMin, rainRect.xMax), Random.Range(rainRect.yMin, rainRect.yMax));
                    if (!rain.Contains(land)) continue;
                    var start = W(land) + new Vector3(0.6f, 3.2f, 0);
                    var f = Spawn(start, "rain", 0.4f, new Color(1, 1, 1, 0.85f));
                    f.vel = (W(land) - start) / 0.4f;
                    f.blinkEnd = false;
                    f.owner = land;
                    f.sr.sortingOrder = FxOrder - 10;
                    var sp = SpawnAnim(W(land), "splash", 2, 8f, 0.25f, Color.white);
                    sp.t = -0.4f;
                    sp.life = 0.25f;
                    sp.sr.enabled = false;
                    sp.blinkEnd = false;
                    sp.sr.sortingOrder = ShadowOrder + 5;
                }
            }
            var lava = Fields.All[1];
            var lr = ArenaMap.FieldRectPx(lava);
            lavaAcc += dt * 3f;
            while (lavaAcc >= 1f)
            {
                lavaAcc -= 1f;
                var p = new Vector2(Random.Range(lr.xMin, lr.xMax), Random.Range(lr.yMin, lr.yMax));
                if (!lava.Contains(p)) continue;
                var f = SpawnAnim(W(p), "lavapop", 2, 5f, 0.4f, Color.white);
                f.blinkEnd = false;
                f.sr.sortingOrder = ShadowOrder + 5;
            }
            // Splashes spawned with negative time become visible once their drop lands.
            foreach (var f in fx)
                if (f.t >= 0 && !f.sr.enabled && f.frames != null && f.sr.sprite != null && f.sr.sprite.name.StartsWith("splash")) f.sr.enabled = true;
        }
    }
}
