using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Poketato
{
    /// <summary>Presentation of the simulation: sprites, projectiles, lights, particles and ground decals.</summary>
    public class GameView : MonoBehaviour, ISimListener
    {
        public GameSim sim;
        public LightPool lights;
        public CameraRig rig;
        public DamageNumbers numbers;
        public System.Action<string> onMessage;
        public System.Action<int> onLevelUp;

        public SpriteActor PlayerActor { get; private set; }
        public int ProjectileCount => liveProj.Count;
        public int TransientCount => transients.Count;

        Transform actorRoot, fxRoot;
        readonly Dictionary<int, Stack<SpriteActor>> actorPool = new();
        readonly List<(SpriteActor a, float t, float life)> dying = new();

        ParticleSystem sparks;
        Material orbBase, additiveBase, candyMat, berryMat;
        readonly Dictionary<PType, Material> orbMats = new();
        readonly Dictionary<PType, Material> trailMats = new();
        Mesh sphere, ring, disc, flatQuad;
        readonly Dictionary<int, Mesh> arcMeshes = new();
        MaterialPropertyBlock mpb;
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        class ProjView
        {
            public GameObject go;
            public TrailRenderer trail;
            public object light;
        }

        readonly Stack<ProjView> projPool = new();
        readonly List<Proj> liveProj = new();

        enum TKind { Arc, Ring, Disc, Quad, Line }

        class Transient
        {
            public TKind kind;
            public GameObject go;
            public MeshRenderer mr;
            public LineRenderer line;
            public float t, life, scale0, scale1, pulse;
            public Color color;
            public object owner;
            public bool hold;
        }

        readonly List<Transient> transients = new();
        readonly Dictionary<TKind, Stack<Transient>> tPool = new();
        readonly Stack<GameObject> candyPool = new(), berryPool = new();
        readonly List<Pickup> livePickups = new();

        public static Vector3 W(Vector2 px, float y = 0f) => Fields.ToWorld(px, y);

        public void Init()
        {
            actorRoot = new GameObject("Actors").transform;
            actorRoot.SetParent(transform, false);
            fxRoot = new GameObject("Fx").transform;
            fxRoot.SetParent(transform, false);
            mpb = new MaterialPropertyBlock();
            orbBase = Resources.Load<Material>("Fx/FxOrb");
            additiveBase = Resources.Load<Material>("Fx/FxAdditive");
            candyMat = Resources.Load<Material>("Fx/Candy");
            berryMat = Resources.Load<Material>("Fx/Berry");
            sphere = Resources.Load<Mesh>("Fx/Sphere");
            ring = BuildRing(48, 0.82f);
            disc = BuildDisc(40);
            flatQuad = BuildFlatQuad();
            sparks = BuildSparks();
            foreach (PType t in System.Enum.GetValues(typeof(PType)))
            {
                var c = Types.ColorOf(t);
                var m = new Material(orbBase) { name = "Orb_" + t };
                m.SetColor(BaseColorId, c * 3.5f + Color.white * 0.6f);
                orbMats[t] = m;
                var tm = new Material(additiveBase) { name = "Trail_" + t };
                tm.SetColor(BaseColorId, c * 2.2f);
                trailMats[t] = tm;
            }
        }

        // ------------------------------------------------------------------ actors

        public SpriteActor GetActor(int dex)
        {
            if (actorPool.TryGetValue(dex, out var st) && st.Count > 0)
            {
                var a = st.Pop();
                a.gameObject.SetActive(true);
                a.ResetState();
                return a;
            }
            return SpriteActor.Create(SpriteDb.Get(dex), actorRoot);
        }

        public void ReleaseActor(SpriteActor a)
        {
            a.gameObject.SetActive(false);
            if (!actorPool.TryGetValue(a.Species.dex, out var st)) actorPool[a.Species.dex] = st = new Stack<SpriteActor>();
            st.Push(a);
        }

        public void SpawnPlayer()
        {
            if (PlayerActor) ReleaseActor(PlayerActor);
            PlayerActor = GetActor(sim.player.line.dex);
            sim.player.view = PlayerActor;
            PlayerActor.transform.position = W(sim.player.pos);
            if (rig) { rig.target = PlayerActor.transform; rig.Snap(); }
        }

        public void Sync(float dt)
        {
            var p = sim.player;
            if (PlayerActor && p != null)
            {
                PlayerActor.transform.position = W(p.pos);
                PlayerActor.SetFacing(p.facing);
                PlayerActor.SetBase(p.moving ? "Walk" : "Idle");
                PlayerActor.SetAlpha(p.invuln > 0 && sim.phase == SimPhase.Wave ? (Mathf.Repeat(Time.time * 12f, 1f) > 0.5f ? 0.55f : 1f) : 1f);
            }
            foreach (var e in sim.enemies)
            {
                if (e.view is not SpriteActor a) continue;
                a.transform.position = W(e.pos);
                Vector2 face = e.move.sqrMagnitude > 0.01f ? e.move : (p.pos - e.pos);
                a.SetFacing(face);
                a.SetBase(e.move.sqrMagnitude > 0.01f ? "Walk" : "Idle", e.paraT > 0 || e.slowT > 0 ? 0.5f : 1f);
                a.SetTint(e.burnT > 0 || e.fieldBurnT > 0 ? new Color(1f, 0.75f, 0.6f) : e.paraT > 0 ? new Color(1f, 1f, 0.55f) : e.poisonT > 0 ? new Color(0.85f, 0.65f, 1f) : Color.white);
            }
            foreach (var pr in liveProj)
            {
                if (pr.view is ProjView v) v.go.transform.position = W(pr.pos, pr.enemy ? 0.55f : 0.75f);
            }
            float time = Time.time;
            foreach (var pk in livePickups)
            {
                if (pk.view is GameObject go)
                {
                    go.transform.position = W(pk.pos, 0.22f + Mathf.Sin(time * 4f + pk.id) * 0.06f);
                    go.transform.rotation = Quaternion.Euler(0, time * 120f + pk.id * 37f, 20f);
                }
            }
            for (int i = dying.Count - 1; i >= 0; i--)
            {
                var (a, t, life) = dying[i];
                t += dt;
                a.SetAlpha(Mathf.Clamp01(1f - (t - life * 0.5f) / (life * 0.5f)));
                if (t >= life) { dying.RemoveAt(i); ReleaseActor(a); }
                else dying[i] = (a, t, life);
            }
            UpdateTransients(dt);
        }

        // ------------------------------------------------------------------ ISimListener

        public void EnemySpawned(Enemy e)
        {
            var a = GetActor(e.def.dex);
            a.transform.position = W(e.pos);
            if (e.def.rank != Rank.Minion) a.SetEmission(0.05f);
            e.view = a;
        }

        public void EnemyRemoved(Enemy e, bool killed)
        {
            if (e.view is not SpriteActor a) return;
            e.view = null;
            if (killed)
            {
                a.Play("Faint", true);
                dying.Add((a, 0f, 0.6f));
                Burst(W(e.pos, 0.5f), Types.ColorOf(e.def.types[0]), 8, 3f);
            }
            else dying.Add((a, 0.15f, 0.3f));
        }

        public void EnemyHit(Enemy e, float dmg, bool crit, float eff, PType type)
        {
            if (e.view is SpriteActor a)
            {
                a.Flash(crit ? 1f : 0.75f);
                if (!a.Busy && e.def.rank == Rank.Minion) a.Play("Hurt");
            }
            Burst(W(e.pos, 0.6f), Types.ColorOf(type), crit ? 7 : 4, crit ? 4f : 2.5f);
            numbers?.Show(W(e.pos, 1.6f), dmg, crit ? DamageNumbers.Kind.Crit : eff > 1f ? DamageNumbers.Kind.Super : DamageNumbers.Kind.Normal);
        }

        public void PlayerHurt(float dmg, bool dodged)
        {
            if (PlayerActor)
            {
                PlayerActor.Flash(1f);
                if (dmg > 0) PlayerActor.Play("Hurt");
            }
            numbers?.Show(W(sim.player.pos, 1.8f), dmg, dodged ? DamageNumbers.Kind.Dodge : DamageNumbers.Kind.Player);
            if (dmg > 0) rig?.Shake(0.12f, 0.15f);
        }

        public void PlayerHealed(float amount) => numbers?.Show(W(sim.player.pos, 1.8f), amount, DamageNumbers.Kind.Heal);

        public void ProjSpawned(Proj p)
        {
            var v = projPool.Count > 0 ? projPool.Pop() : NewProjView();
            v.go.SetActive(true);
            var mr = v.go.GetComponent<MeshRenderer>();
            var c = Types.ColorOf(p.type);
            mr.sharedMaterial = orbMats[p.type];
            float s = (p.enemy ? 0.22f : 0.18f + p.size * 0.035f);
            v.go.transform.localScale = Vector3.one * s;
            v.go.transform.position = W(p.pos, p.enemy ? 0.55f : 0.75f);
            v.trail.sharedMaterial = trailMats[p.type];
            v.trail.widthMultiplier = s * 0.9f;
            v.trail.time = p.enemy ? 0.12f : 0.18f;
            v.trail.Clear();
            v.trail.emitting = true;
            if (!p.enemy) v.light = lights.Attach(v.go.transform, c, 2.2f, 3.2f);
            p.view = v;
            liveProj.Add(p);
            if (!p.enemy && PlayerActor && !PlayerActor.Busy) PlayerActor.Play("Shoot");
        }

        public void ProjRemoved(Proj p)
        {
            liveProj.Remove(p);
            if (p.view is not ProjView v) return;
            p.view = null;
            if (v.light != null) { lights.Detach(v.light); v.light = null; }
            v.trail.emitting = false;
            v.go.SetActive(false);
            projPool.Push(v);
            if (!p.enemy) Burst(W(p.pos, 0.75f), Types.ColorOf(p.type), 3, 2f);
        }

        public void Melee(Vector2 pos, float angle, float arc, float radius, PType type)
        {
            var t = Spawn(TKind.Arc, W(pos, 0.12f), Quaternion.Euler(0, angle * Mathf.Rad2Deg, 0), Types.ColorOf(type) * 2.2f, 0.18f, radius / GameData.PxPerUnit * 0.6f, radius / GameData.PxPerUnit);
            t.mr.GetComponent<MeshFilter>().sharedMesh = ArcMesh(arc);
            if (PlayerActor && !PlayerActor.Busy) PlayerActor.Play("Attack");
        }

        public void Nova(Vector2 pos, float radius, PType type)
        {
            float r = radius / GameData.PxPerUnit;
            Spawn(TKind.Ring, W(pos, 0.1f), Quaternion.identity, Types.ColorOf(type) * 2.5f, 0.3f, r * 0.2f, r);
            lights.Flash(W(pos, 1f), Types.ColorOf(type), 5f, r * 1.6f, 0.3f);
            if (PlayerActor && !PlayerActor.Busy) PlayerActor.Play("Attack");
        }

        public void Beam(Vector2 pos, float angle, float len, float width, PType type)
        {
            var t = Spawn(TKind.Quad, W(pos, 0.7f), Quaternion.Euler(0, angle * Mathf.Rad2Deg, 0), Types.ColorOf(type) * 3f, 0.2f, 1f, 1f);
            t.go.transform.localScale = new Vector3(len / GameData.PxPerUnit, 1f, width / GameData.PxPerUnit);
            Vector2 mid = pos + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * len * 0.5f;
            lights.Flash(W(mid, 0.8f), Types.ColorOf(type), 4f, 4f, 0.25f);
            if (PlayerActor && !PlayerActor.Busy) PlayerActor.Play("Shoot");
        }

        public void Chain(List<Vector2> points, PType type)
        {
            var t = Spawn(TKind.Line, Vector3.zero, Quaternion.identity, Types.ColorOf(type) * 3f, 0.16f, 1f, 1f);
            var pts = new List<Vector3>();
            for (int i = 0; i < points.Count; i++)
            {
                var a = W(points[i], 0.8f);
                if (i > 0)
                {
                    var b = W(points[i - 1], 0.8f);
                    for (int k = 1; k < 4; k++)
                        pts.Add(Vector3.Lerp(b, a, k / 4f) + Random.insideUnitSphere * 0.25f);
                    lights.Flash(a, Types.ColorOf(type), 3f, 3f, 0.2f);
                }
                pts.Add(a);
            }
            t.line.positionCount = pts.Count;
            t.line.SetPositions(pts.ToArray());
        }

        public void StrikeWarn(Strike s)
        {
            float r = s.radius / GameData.PxPerUnit;
            var t = Spawn(TKind.Ring, W(s.pos, 0.06f), Quaternion.identity, Types.ColorOf(s.type) * 1.6f, s.delay, r, r);
            t.pulse = 10f;
            s.view = t;
        }

        public void StrikeBoom(Strike s)
        {
            if (s.view is Transient t) t.t = t.life;
            float r = s.radius / GameData.PxPerUnit;
            var c = Types.ColorOf(s.type);
            Spawn(TKind.Disc, W(s.pos, 0.08f), Quaternion.identity, c * 3f, 0.25f, r * 0.4f, r * 1.1f);
            Burst(W(s.pos, 0.4f), c, 16, 5f);
            lights.Flash(W(s.pos, 1.2f), c, 7f, r * 3f, 0.35f);
            rig?.Shake(0.08f, 0.12f);
        }

        public void ZoneStart(Zone z)
        {
            float r = z.radius / GameData.PxPerUnit;
            var c = Types.ColorOf(z.type);
            var t = Spawn(TKind.Disc, W(z.pos, 0.07f), Quaternion.identity, c * 1.8f, z.t + 0.3f, r, r);
            t.pulse = 6f;
            t.owner = lights.Attach(t.go.transform, c, 3f, r * 2.5f);
            z.view = t;
        }

        public void ZoneEnd(Zone z)
        {
            if (z.view is not Transient t) return;
            if (t.owner != null) { lights.Detach(t.owner); t.owner = null; }
            t.life = Mathf.Min(t.life, t.t + 0.3f);
        }

        public void ShieldUp(float amount)
        {
            var pos = sim.player.pos;
            Spawn(TKind.Ring, W(pos, 0.1f), Quaternion.identity, new Color(0.5f, 0.8f, 2.5f), 0.5f, 0.6f, 1.6f);
            lights.Flash(W(pos, 1f), new Color(0.5f, 0.75f, 1f), 3f, 4f, 0.5f);
        }

        public void Buff()
        {
            var pos = sim.player.pos;
            Spawn(TKind.Ring, W(pos, 0.1f), Quaternion.identity, new Color(2.5f, 2.2f, 0.6f), 0.5f, 0.4f, 1.8f);
        }

        public void PickupSpawned(Pickup p)
        {
            var pool = p.kind == PickupKind.Candy ? candyPool : berryPool;
            GameObject go;
            if (pool.Count > 0) { go = pool.Pop(); go.SetActive(true); }
            else
            {
                go = new GameObject(p.kind.ToString());
                go.transform.SetParent(fxRoot, false);
                go.AddComponent<MeshFilter>().sharedMesh = sphere;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = p.kind == PickupKind.Candy ? candyMat : berryMat;
                mr.shadowCastingMode = ShadowCastingMode.On;
                go.transform.localScale = p.kind == PickupKind.Candy ? new Vector3(0.26f, 0.18f, 0.18f) : Vector3.one * 0.3f;
            }
            go.transform.position = W(p.pos, 0.22f);
            p.view = go;
            livePickups.Add(p);
        }

        public void PickupCollected(Pickup p)
        {
            livePickups.Remove(p);
            if (p.view is not GameObject go) return;
            p.view = null;
            go.SetActive(false);
            (p.kind == PickupKind.Candy ? candyPool : berryPool).Push(go);
        }

        public void SpawnWarn(PendingSpawn s)
        {
            var t = Spawn(TKind.Ring, W(s.pos, 0.05f), Quaternion.identity, new Color(1.2f, 0.25f, 0.2f), s.t + 0.05f, 0.35f, 0.55f);
            t.pulse = 8f;
            s.view = t;
        }

        public void SpawnWarnEnd(PendingSpawn s)
        {
            if (s.view is Transient t) t.t = t.life;
        }

        public void DashWarn(Enemy e, Vector2 dir, float len, float time)
        {
            float ang = Mathf.Atan2(dir.y, dir.x);
            var t = Spawn(TKind.Quad, W(e.pos, 0.06f), Quaternion.Euler(0, ang * Mathf.Rad2Deg, 0), new Color(1.6f, 0.2f, 0.15f), time, 1f, 1f);
            t.go.transform.localScale = new Vector3(len / GameData.PxPerUnit, 1f, 1.1f);
            t.pulse = 12f;
        }

        public void LevelUp(int level)
        {
            var pos = sim.player.pos;
            Spawn(TKind.Ring, W(pos, 0.1f), Quaternion.identity, new Color(3f, 2.4f, 0.8f), 0.7f, 0.3f, 2.4f);
            lights.Flash(W(pos, 1.2f), new Color(1f, 0.85f, 0.4f), 6f, 6f, 0.7f);
            onLevelUp?.Invoke(level);
        }

        public void Message(string text) => onMessage?.Invoke(text);

        // ------------------------------------------------------------------ fx helpers

        void Burst(Vector3 at, Color c, int count, float speed)
        {
            var ep = new ParticleSystem.EmitParams { position = at, startColor = c, applyShapeToPosition = false };
            for (int i = 0; i < count; i++)
            {
                ep.velocity = Random.insideUnitSphere * speed + Vector3.up * speed * 0.4f;
                ep.startSize = Random.Range(0.08f, 0.18f);
                ep.startLifetime = Random.Range(0.25f, 0.45f);
                sparks.Emit(ep, 1);
            }
        }

        Transient Spawn(TKind kind, Vector3 pos, Quaternion rot, Color color, float life, float s0, float s1)
        {
            if (!tPool.TryGetValue(kind, out var pool)) tPool[kind] = pool = new Stack<Transient>();
            var t = pool.Count > 0 ? pool.Pop() : NewTransient(kind);
            t.go.SetActive(true);
            t.go.transform.SetPositionAndRotation(pos, rot);
            t.t = 0;
            t.life = Mathf.Max(0.01f, life);
            t.scale0 = s0;
            t.scale1 = s1;
            t.color = color;
            t.pulse = 0;
            t.owner = null;
            if (kind != TKind.Quad && kind != TKind.Line) t.go.transform.localScale = Vector3.one * s0;
            transients.Add(t);
            ApplyColor(t, 1f);
            return t;
        }

        Transient NewTransient(TKind kind)
        {
            var t = new Transient { kind = kind, go = new GameObject("Fx" + kind) };
            t.go.transform.SetParent(fxRoot, false);
            if (kind == TKind.Line)
            {
                t.line = t.go.AddComponent<LineRenderer>();
                t.line.useWorldSpace = true;
                t.line.widthMultiplier = 0.14f;
                t.line.numCapVertices = 2;
                t.line.shadowCastingMode = ShadowCastingMode.Off;
                t.line.sharedMaterial = additiveBase;
                return t;
            }
            var mf = t.go.AddComponent<MeshFilter>();
            mf.sharedMesh = kind switch { TKind.Ring => ring, TKind.Disc => disc, TKind.Quad => flatQuad, _ => ArcMesh(Mathf.PI) };
            t.mr = t.go.AddComponent<MeshRenderer>();
            t.mr.sharedMaterial = additiveBase;
            t.mr.shadowCastingMode = ShadowCastingMode.Off;
            t.mr.receiveShadows = false;
            return t;
        }

        void ApplyColor(Transient t, float k)
        {
            var c = t.color * k;
            c.a = 1f;
            if (t.line != null)
            {
                t.line.startColor = c;
                t.line.endColor = c;
                return;
            }
            mpb.SetColor(BaseColorId, c);
            t.mr.SetPropertyBlock(mpb);
        }

        void UpdateTransients(float dt)
        {
            for (int i = transients.Count - 1; i >= 0; i--)
            {
                var t = transients[i];
                t.t += dt;
                float u = Mathf.Clamp01(t.t / t.life);
                if (u >= 1f)
                {
                    if (t.owner != null) { lights.Detach(t.owner); t.owner = null; }
                    t.go.SetActive(false);
                    transients.RemoveAt(i);
                    tPool[t.kind].Push(t);
                    continue;
                }
                if (t.kind != TKind.Quad && t.kind != TKind.Line)
                    t.go.transform.localScale = Vector3.one * Mathf.Lerp(t.scale0, t.scale1, 1f - (1f - u) * (1f - u));
                float fade = t.pulse > 0 ? 0.55f + 0.45f * Mathf.Sin(t.t * t.pulse) : 1f - u * u;
                ApplyColor(t, Mathf.Max(0, fade));
            }
        }

        ProjView NewProjView()
        {
            var go = new GameObject("Proj");
            go.transform.SetParent(fxRoot, false);
            go.AddComponent<MeshFilter>().sharedMesh = sphere;
            var mr = go.AddComponent<MeshRenderer>();
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            var trail = go.AddComponent<TrailRenderer>();
            trail.minVertexDistance = 0.1f;
            trail.shadowCastingMode = ShadowCastingMode.Off;
            trail.widthCurve = new AnimationCurve(new Keyframe(0, 1), new Keyframe(1, 0));
            trail.colorGradient = new Gradient
            {
                colorKeys = new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                alphaKeys = new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(0, 1) },
            };
            return new ProjView { go = go, trail = trail };
        }

        ParticleSystem BuildSparks()
        {
            var go = new GameObject("Sparks");
            go.transform.SetParent(fxRoot, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = false;
            main.playOnAwake = false;
            main.maxParticles = 1500;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = 0.8f;
            main.startSpeed = 0;
            var emission = ps.emission;
            emission.enabled = false;
            var shape = ps.shape;
            shape.enabled = false;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(new Gradient
            {
                colorKeys = new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                alphaKeys = new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(0, 1) },
            });
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0, 1), new Keyframe(1, 0.1f)));
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = Resources.Load<Material>("Fx/FxSpark");
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.shadowCastingMode = ShadowCastingMode.Off;
            ps.Play();
            return ps;
        }

        Mesh ArcMesh(float arc)
        {
            int key = Mathf.RoundToInt(arc * Mathf.Rad2Deg);
            if (arcMeshes.TryGetValue(key, out var m)) return m;
            int seg = Mathf.Max(6, key / 8);
            var verts = new List<Vector3>();
            var cols = new List<Color>();
            var tris = new List<int>();
            for (int i = 0; i <= seg; i++)
            {
                float a = -arc / 2 + arc * i / seg;
                var dir = new Vector3(Mathf.Cos(a), 0, -Mathf.Sin(a));
                verts.Add(dir * 0.35f);
                verts.Add(dir);
                float edge = Mathf.Sin(Mathf.PI * i / seg);
                cols.Add(new Color(1, 1, 1, 0));
                cols.Add(new Color(edge, edge, edge, 1));
                if (i < seg)
                {
                    int b = i * 2;
                    tris.AddRange(new[] { b, b + 1, b + 2, b + 1, b + 3, b + 2 });
                }
            }
            m = new Mesh { name = "Arc" + key };
            m.SetVertices(verts);
            m.SetColors(cols);
            m.SetTriangles(tris, 0);
            m.RecalculateBounds();
            arcMeshes[key] = m;
            return m;
        }

        static Mesh BuildRing(int seg, float inner)
        {
            var verts = new List<Vector3>();
            var cols = new List<Color>();
            var tris = new List<int>();
            for (int i = 0; i <= seg; i++)
            {
                float a = Mathf.PI * 2 * i / seg;
                var d = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                verts.Add(d * inner);
                verts.Add(d);
                cols.Add(new Color(1, 1, 1, 1));
                cols.Add(new Color(0.4f, 0.4f, 0.4f, 1));
                if (i < seg)
                {
                    int b = i * 2;
                    tris.AddRange(new[] { b, b + 2, b + 1, b + 1, b + 2, b + 3 });
                }
            }
            var m = new Mesh { name = "Ring" };
            m.SetVertices(verts);
            m.SetColors(cols);
            m.SetTriangles(tris, 0);
            m.RecalculateBounds();
            return m;
        }

        static Mesh BuildDisc(int seg)
        {
            var verts = new List<Vector3> { Vector3.zero };
            var cols = new List<Color> { new Color(0.25f, 0.25f, 0.25f, 1) };
            var tris = new List<int>();
            for (int i = 0; i <= seg; i++)
            {
                float a = Mathf.PI * 2 * i / seg;
                verts.Add(new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)));
                cols.Add(new Color(0.8f, 0.8f, 0.8f, 1));
                if (i < seg) tris.AddRange(new[] { 0, i + 2, i + 1 });
            }
            var m = new Mesh { name = "Disc" };
            m.SetVertices(verts);
            m.SetColors(cols);
            m.SetTriangles(tris, 0);
            m.RecalculateBounds();
            return m;
        }

        static Mesh BuildFlatQuad()
        {
            var m = new Mesh { name = "FlatQuad" };
            m.vertices = new[] { new Vector3(0, 0, -0.5f), new Vector3(1, 0, -0.5f), new Vector3(0, 0, 0.5f), new Vector3(1, 0, 0.5f), new Vector3(0, 0, 0), new Vector3(1, 0, 0) };
            m.colors = new[] { new Color(0, 0, 0, 1), new Color(0, 0, 0, 1), new Color(0, 0, 0, 1), new Color(0, 0, 0, 1), Color.white, Color.white };
            m.triangles = new[] { 0, 4, 1, 4, 5, 1, 4, 2, 5, 2, 3, 5 };
            m.RecalculateBounds();
            return m;
        }
    }
}
