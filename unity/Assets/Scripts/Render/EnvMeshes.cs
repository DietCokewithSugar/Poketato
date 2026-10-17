using System.Collections.Generic;
using UnityEngine;

namespace Poketato
{
    public enum EnvKind { Ground, Path, Plaza, Curb, RainWater, Lava, LavaRim, Crystals, Lanterns, Trees, Hills, Rocks, Tufts, Flowers, Fence }

    public static class EnvMeshes
    {
        public static Vector3[] FieldCirclesWorld(FieldDef f)
        {
            var r = new Vector3[f.blobs.Length];
            for (int i = 0; i < f.blobs.Length; i++)
            {
                var w = Fields.ToWorld(new Vector2(f.blobs[i].x, f.blobs[i].y));
                r[i] = new Vector3(w.x, w.z, f.blobs[i].z / GameData.PxPerUnit);
            }
            return r;
        }

        public static bool InsideAnyField(Vector2 worldXZ, float margin)
        {
            foreach (var f in Fields.All)
                foreach (var c in FieldCirclesWorld(f))
                    if (Vector2.Distance(worldXZ, new Vector2(c.x, c.y)) < c.z + margin) return true;
            return false;
        }

        public static Vector2 Centroid(Vector3[] circles)
        {
            Vector2 s = Vector2.zero;
            foreach (var c in circles) s += new Vector2(c.x, c.y);
            return s / circles.Length;
        }

        public static Vector2 Extent(Vector3[] circles)
        {
            var c0 = Centroid(circles);
            Vector2 e = Vector2.zero;
            foreach (var c in circles)
            {
                e.x = Mathf.Max(e.x, Mathf.Abs(c.x - c0.x) + c.z);
                e.y = Mathf.Max(e.y, Mathf.Abs(c.y - c0.y) + c.z);
            }
            return e;
        }

        public static float PathX(float z) => Mathf.Sin(z * 0.08f) * 2f;
        public static Vector3 CrystalPos => Fields.ToWorld(new Vector2(92, 96));

        public static Vector3 LanternPos(int i)
        {
            float a = Mathf.PI / 4 + i * Mathf.PI / 2;
            return new Vector3(Mathf.Cos(a) * 5.6f, 0, Mathf.Sin(a) * 5.6f);
        }

        public static Mesh Build(EnvKind kind, int seed)
        {
            var rng = new System.Random(seed + (int)kind * 7919);
            float R(float a, float b) => a + (b - a) * (float)rng.NextDouble();
            switch (kind)
            {
                case EnvKind.Ground:
                    return MeshGen.GroundPlane("Ground", 150, 120, new Vector3(0, 0, 12), 0.25f, 30, 24);

                case EnvKind.Path:
                {
                    var b = new MeshGen.Builder();
                    for (int i = 0; i < 40; i++)
                    {
                        float z0 = -45 + i * 2.5f, z1 = z0 + 2.5f;
                        float w0 = 1.6f + Mathf.Sin(z0 * 0.7f) * 0.25f, w1 = 1.6f + Mathf.Sin(z1 * 0.7f) * 0.25f;
                        float x0 = PathX(z0), x1 = PathX(z1);
                        b.Quad(0, new Vector3(x0 - w0, 0.012f, z0), new Vector3(x1 - w1, 0.012f, z1), new Vector3(x1 + w1, 0.012f, z1), new Vector3(x0 + w0, 0.012f, z0));
                    }
                    return ScaleUV(b.Build("Path"), 0.25f);
                }

                case EnvKind.Plaza:
                    return ScaleUV(MeshGen.FieldMesh("Plaza", new[] { new Vector3(0, 0, 4.4f) }, 0.022f, 0.4f, 0.01f), 0.5f);

                case EnvKind.Curb:
                {
                    var b = new MeshGen.Builder();
                    for (int i = 0; i < 28; i++)
                    {
                        float a = Mathf.PI * 2 * i / 28;
                        MeshGen.Box(b, 0, new Vector3(Mathf.Cos(a) * 4.55f, 0.07f, Mathf.Sin(a) * 4.55f), new Vector3(0.45f, 0.14f, 1.02f), Quaternion.Euler(0, -a * Mathf.Rad2Deg, 0));
                    }
                    return b.Build("Curb");
                }

                case EnvKind.RainWater:
                    return MeshGen.FieldMesh("RainWater", FieldCirclesWorld(Fields.All[0]), 0.04f, 0.25f, 0.6f);

                case EnvKind.Lava:
                    return MeshGen.FieldMesh("Lava", FieldCirclesWorld(Fields.All[1]), 0.03f, 0.25f, 0.7f);

                case EnvKind.LavaRim:
                {
                    var b = new MeshGen.Builder();
                    var circles = FieldCirclesWorld(Fields.All[1]);
                    int k = 0;
                    foreach (var c in circles)
                    {
                        int n = Mathf.RoundToInt(c.z * 4);
                        for (int i = 0; i < n; i++)
                        {
                            float a = Mathf.PI * 2 * i / n + R(-0.1f, 0.1f);
                            var p = new Vector2(c.x + Mathf.Cos(a) * (c.z + 0.1f), c.y + Mathf.Sin(a) * (c.z + 0.1f));
                            bool inside = false;
                            foreach (var o in circles) if (o != c && Vector2.Distance(p, new Vector2(o.x, o.y)) < o.z - 0.1f) inside = true;
                            if (inside) continue;
                            float s = R(0.25f, 0.55f);
                            MeshGen.Blob(b, 0, new Vector3(p.x, s * 0.25f, p.y), new Vector3(s, s * 0.6f, s), 1, 0.45f, k++);
                        }
                    }
                    return b.Build("LavaRim");
                }

                case EnvKind.Crystals:
                {
                    var b = new MeshGen.Builder();
                    var cp = CrystalPos;
                    for (int i = 0; i < 6; i++)
                    {
                        float a = i * 1.1f;
                        float h = i == 0 ? 3.2f : R(1.0f, 2.2f);
                        var bp = cp + (i == 0 ? Vector3.zero : new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * R(0.6f, 1.1f));
                        var tilt = Quaternion.Euler(i == 0 ? 0 : R(-20, 20), R(0, 360), i == 0 ? 0 : R(-20, 20));
                        Crystal(b, bp, h, h * 0.22f, tilt);
                    }
                    return b.Build("Crystals");
                }

                case EnvKind.Lanterns:
                {
                    var b = new MeshGen.Builder(3);
                    for (int i = 0; i < 4; i++)
                    {
                        var bp = LanternPos(i);
                        MeshGen.Cylinder(b, 0, bp, 0.09f, 2.1f, 6);
                        MeshGen.Box(b, 1, bp + Vector3.up * 2.12f, new Vector3(0.42f, 0.07f, 0.42f), Quaternion.Euler(0, 45, 0));
                        MeshGen.Box(b, 2, bp + Vector3.up * 1.9f, new Vector3(0.26f, 0.36f, 0.26f), Quaternion.Euler(0, 45, 0));
                    }
                    return b.Build("Lanterns");
                }

                case EnvKind.Trees:
                {
                    var b = new MeshGen.Builder(4);
                    var pts = new List<Vector2>();
                    for (int tries = 0; tries < 4000 && pts.Count < 230; tries++)
                    {
                        var p = new Vector2(R(-62, 62), R(-24, 62));
                        bool inArena = Mathf.Abs(p.x) < 26.5f && p.y > -21f && p.y < 20.5f;
                        if (inArena || Mathf.Abs(p.x - PathX(p.y)) < 3.2f) continue;
                        if (p.y < -19f && Mathf.Abs(p.x) < 30f) continue;
                        bool close = false;
                        foreach (var q in pts) if ((q - p).sqrMagnitude < 4.2f) { close = true; break; }
                        if (close) continue;
                        pts.Add(p);
                        float s = R(0.85f, 1.45f) * (p.y > 30 ? 1.25f : 1f);
                        var bp = new Vector3(p.x, 0, p.y);
                        if (rng.NextDouble() < 0.7)
                        {
                            MeshGen.Cylinder(b, 0, bp, 0.22f * s, 1.1f * s, 6);
                            int leaf = rng.NextDouble() < 0.5 ? 1 : 2;
                            MeshGen.Cone(b, leaf, bp + Vector3.up * 0.9f * s, 1.5f * s, 2.3f * s, 7, R(0, 6), null, 0.15f, rng);
                            MeshGen.Cone(b, leaf, bp + Vector3.up * 2.0f * s, 1.15f * s, 2.0f * s, 7, R(0, 6), null, 0.15f, rng);
                            MeshGen.Cone(b, leaf, bp + Vector3.up * 3.0f * s, 0.75f * s, 1.7f * s, 7, R(0, 6), null, 0.15f, rng);
                        }
                        else
                        {
                            MeshGen.Cylinder(b, 0, bp, 0.25f * s, 1.6f * s, 6);
                            MeshGen.Blob(b, 3, bp + Vector3.up * 2.4f * s, new Vector3(1.5f, 1.25f, 1.5f) * s, 1, 0.35f, pts.Count);
                            MeshGen.Blob(b, 1, bp + new Vector3(0.6f, 2.0f, 0.3f) * s, new Vector3(0.9f, 0.8f, 0.9f) * s, 1, 0.35f, pts.Count + 99);
                        }
                    }
                    return b.Build("Trees");
                }

                case EnvKind.Hills:
                {
                    var b = new MeshGen.Builder();
                    for (int i = 0; i < 9; i++)
                    {
                        float x = -80 + i * 20 + R(-5, 5);
                        MeshGen.Blob(b, 0, new Vector3(x, -2f, R(70, 85)), new Vector3(R(16, 24), R(8, 13), R(10, 14)), 2, 0.12f, i);
                    }
                    return b.Build("Hills");
                }

                case EnvKind.Rocks:
                {
                    var b = new MeshGen.Builder();
                    for (int i = 0; i < 46; i++)
                    {
                        Vector2 p = i < 10 ? new Vector2(R(-23, 23), R(-17, 17)) : new Vector2(R(-40, 40), R(-22, 40));
                        if (i < 10 && (p.magnitude < 7f || InsideAnyField(p, 1.5f))) continue;
                        if (i >= 10 && Mathf.Abs(p.x) < 25 && p.y < 19 && p.y > -19) continue;
                        float s = i < 10 ? R(0.25f, 0.5f) : R(0.5f, 1.4f);
                        MeshGen.Blob(b, 0, new Vector3(p.x, s * 0.3f, p.y), new Vector3(s * 1.2f, s * 0.8f, s), 1, 0.4f, i * 7);
                    }
                    return b.Build("Rocks");
                }

                case EnvKind.Tufts:
                case EnvKind.Flowers:
                {
                    var tufts = new MeshGen.Builder();
                    var flowers = new MeshGen.Builder(3);
                    for (int i = 0; i < 700; i++)
                    {
                        var p = new Vector2(R(-30, 30), R(-21, 24));
                        if (p.magnitude < 5f || InsideAnyField(p, 0.6f) || Mathf.Abs(p.x - PathX(p.y)) < 2.2f) continue;
                        var bp = new Vector3(p.x, 0, p.y);
                        if (i % 5 == 0) MeshGen.Cylinder(flowers, i % 3, bp, 0.06f, 0.18f, 5);
                        else
                        {
                            for (int k = 0; k < 3; k++)
                            {
                                float a = R(0, Mathf.PI * 2);
                                var d = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * 0.09f;
                                var tip = bp + new Vector3(R(-0.06f, 0.06f), R(0.18f, 0.32f), R(-0.06f, 0.06f));
                                tufts.Tri(0, bp - d, tip, bp + d);
                                tufts.Tri(0, bp + d, tip, bp - d);
                            }
                        }
                    }
                    return kind == EnvKind.Tufts ? tufts.Build("Tufts") : flowers.Build("Flowers");
                }

                case EnvKind.Fence:
                {
                    var b = new MeshGen.Builder();
                    for (float x = -27f; x <= 27f; x += 2f)
                    {
                        if (Mathf.Abs(x) < 3f) continue;
                        MeshGen.Box(b, 0, new Vector3(x, 0.45f, -20.2f), new Vector3(0.16f, 0.9f, 0.16f), Quaternion.identity);
                        if (x + 2f <= 27f && Mathf.Abs(x + 1f) >= 3f)
                        {
                            MeshGen.Box(b, 0, new Vector3(x + 1f, 0.65f, -20.2f), new Vector3(2f, 0.09f, 0.07f), Quaternion.identity);
                            MeshGen.Box(b, 0, new Vector3(x + 1f, 0.35f, -20.2f), new Vector3(2f, 0.09f, 0.07f), Quaternion.identity);
                        }
                    }
                    return b.Build("Fence");
                }
            }
            return null;
        }

        static Mesh ScaleUV(Mesh m, float k)
        {
            var uv = new List<Vector2>();
            m.GetUVs(0, uv);
            for (int i = 0; i < uv.Count; i++) uv[i] *= k;
            m.SetUVs(0, uv);
            return m;
        }

        static void Crystal(MeshGen.Builder b, Vector3 basePos, float height, float radius, Quaternion rot)
        {
            const int sides = 6;
            var ring0 = new Vector3[sides];
            var ring1 = new Vector3[sides];
            for (int i = 0; i < sides; i++)
            {
                float a = Mathf.PI * 2 * i / sides;
                var d = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * radius;
                ring0[i] = basePos + rot * d;
                ring1[i] = basePos + rot * (d * 0.9f + Vector3.up * height * 0.72f);
            }
            var tip = basePos + rot * (Vector3.up * height);
            for (int i = 0; i < sides; i++)
            {
                int j = (i + 1) % sides;
                b.Quad(0, ring0[i], ring1[i], ring1[j], ring0[j]);
                b.Tri(0, ring1[i], tip, ring1[j]);
            }
        }
    }
}
