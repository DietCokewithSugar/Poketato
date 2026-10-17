using System.Collections.Generic;
using UnityEngine;

namespace Poketato
{
    /// <summary>Procedural low-poly meshes for the generated environment (flat shaded, no external assets).</summary>
    public static class MeshGen
    {
        public class Builder
        {
            readonly List<Vector3> v = new();
            readonly List<Vector3> n = new();
            readonly List<Vector2> uv = new();
            readonly List<Color> c = new();
            readonly List<List<int>> subs = new();

            public Builder(int submeshes = 1)
            {
                for (int i = 0; i < submeshes; i++) subs.Add(new List<int>());
            }

            public void Tri(int sub, Vector3 a, Vector3 b, Vector3 d, Color? col = null, Vector2? ua = null, Vector2? ub = null, Vector2? ud = null)
            {
                var nn = Vector3.Cross(b - a, d - a).normalized;
                int i = v.Count;
                v.Add(a); v.Add(b); v.Add(d);
                n.Add(nn); n.Add(nn); n.Add(nn);
                uv.Add(ua ?? new Vector2(a.x, a.z)); uv.Add(ub ?? new Vector2(b.x, b.z)); uv.Add(ud ?? new Vector2(d.x, d.z));
                var cc = col ?? Color.white;
                c.Add(cc); c.Add(cc); c.Add(cc);
                subs[sub].Add(i); subs[sub].Add(i + 1); subs[sub].Add(i + 2);
            }

            public void Quad(int sub, Vector3 a, Vector3 b, Vector3 d, Vector3 e, Color? col = null)
            {
                Tri(sub, a, b, d, col);
                Tri(sub, a, d, e, col);
            }

            /// <summary>Smooth vertex (shared normal) for terrain-like grids.</summary>
            public int Vert(Vector3 p, Vector3 normal, Vector2 tex, Color col)
            {
                v.Add(p); n.Add(normal); uv.Add(tex); c.Add(col);
                return v.Count - 1;
            }

            public void Index(int sub, int a, int b, int d)
            {
                subs[sub].Add(a); subs[sub].Add(b); subs[sub].Add(d);
            }

            public int Count => v.Count;

            public Mesh Build(string name)
            {
                var m = new Mesh { name = name, hideFlags = HideFlags.DontSave };
                if (v.Count > 65000) m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                m.SetVertices(v);
                m.SetNormals(n);
                m.SetUVs(0, uv);
                m.SetColors(c);
                m.subMeshCount = subs.Count;
                for (int i = 0; i < subs.Count; i++) m.SetTriangles(subs[i], i);
                m.RecalculateBounds();
                return m;
            }
        }

        public static void Cone(Builder b, int sub, Vector3 basePos, float radius, float height, int sides, float rot = 0f, Color? col = null, float jitter = 0f, System.Random rng = null)
        {
            var top = basePos + Vector3.up * height;
            var ring = new Vector3[sides];
            for (int i = 0; i < sides; i++)
            {
                float a = rot + Mathf.PI * 2 * i / sides;
                float r = radius * (1f + (rng != null ? ((float)rng.NextDouble() - 0.5f) * jitter : 0f));
                ring[i] = basePos + new Vector3(Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r);
            }
            for (int i = 0; i < sides; i++)
            {
                var p0 = ring[i];
                var p1 = ring[(i + 1) % sides];
                b.Tri(sub, p0, top, p1, col);
                b.Tri(sub, p1, basePos, p0, col);
            }
        }

        public static void Cylinder(Builder b, int sub, Vector3 basePos, float radius, float height, int sides, Color? col = null)
        {
            var up = Vector3.up * height;
            for (int i = 0; i < sides; i++)
            {
                float a0 = Mathf.PI * 2 * i / sides, a1 = Mathf.PI * 2 * (i + 1) / sides;
                var p0 = basePos + new Vector3(Mathf.Cos(a0), 0, Mathf.Sin(a0)) * radius;
                var p1 = basePos + new Vector3(Mathf.Cos(a1), 0, Mathf.Sin(a1)) * radius;
                b.Quad(sub, p0, p0 + up, p1 + up, p1, col);
                b.Tri(sub, basePos + up, p1 + up, p0 + up, col);
            }
        }

        public static void Box(Builder b, int sub, Vector3 center, Vector3 size, Quaternion rot, Color? col = null)
        {
            Vector3 h = size * 0.5f;
            Vector3 P(float x, float y, float z) => center + rot * new Vector3(x * h.x, y * h.y, z * h.z);
            b.Quad(sub, P(-1, -1, -1), P(-1, 1, -1), P(1, 1, -1), P(1, -1, -1), col);
            b.Quad(sub, P(1, -1, 1), P(1, 1, 1), P(-1, 1, 1), P(-1, -1, 1), col);
            b.Quad(sub, P(-1, -1, 1), P(-1, 1, 1), P(-1, 1, -1), P(-1, -1, -1), col);
            b.Quad(sub, P(1, -1, -1), P(1, 1, -1), P(1, 1, 1), P(1, -1, 1), col);
            b.Quad(sub, P(-1, 1, -1), P(-1, 1, 1), P(1, 1, 1), P(1, 1, -1), col);
            b.Quad(sub, P(-1, -1, 1), P(-1, -1, -1), P(1, -1, -1), P(1, -1, 1), col);
        }

        static readonly int[] IcoTris =
        {
            0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11, 1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
            3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9, 4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1,
        };

        public static List<Vector3[]> IcosphereTris(int subdiv)
        {
            float t = (1f + Mathf.Sqrt(5f)) / 2f;
            var pts = new[]
            {
                new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
                new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
                new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1),
            };
            var tris = new List<Vector3[]>();
            for (int i = 0; i < IcoTris.Length; i += 3)
                tris.Add(new[] { pts[IcoTris[i]].normalized, pts[IcoTris[i + 1]].normalized, pts[IcoTris[i + 2]].normalized });
            for (int s = 0; s < subdiv; s++)
            {
                var next = new List<Vector3[]>();
                foreach (var tri in tris)
                {
                    var a = ((tri[0] + tri[1]) * 0.5f).normalized;
                    var bb = ((tri[1] + tri[2]) * 0.5f).normalized;
                    var c = ((tri[2] + tri[0]) * 0.5f).normalized;
                    next.Add(new[] { tri[0], a, c });
                    next.Add(new[] { tri[1], bb, a });
                    next.Add(new[] { tri[2], c, bb });
                    next.Add(new[] { a, bb, c });
                }
                tris = next;
            }
            return tris;
        }

        /// <summary>Noise-displaced icosphere (rocks, bushes, canopies). Displacement is position-hashed so shared vertices stay welded.</summary>
        public static void Blob(Builder b, int sub, Vector3 center, Vector3 scale, int subdiv, float noise, int seed, Color? col = null)
        {
            Vector3 D(Vector3 p)
            {
                float h = Mathf.Sin(Vector3.Dot(p, new Vector3(12.9898f, 78.233f, 37.719f)) + seed * 1.37f) * 43758.5453f;
                h -= Mathf.Floor(h);
                return center + Vector3.Scale(p * (1f + (h - 0.5f) * noise), scale);
            }
            foreach (var tri in IcosphereTris(subdiv))
                b.Tri(sub, D(tri[0]), D(tri[2]), D(tri[1]), col);
        }

        public static Mesh UVSphere(int lon, int lat)
        {
            var b = new Builder();
            var idx = new int[lon + 1, lat + 1];
            for (int j = 0; j <= lat; j++)
            for (int i = 0; i <= lon; i++)
            {
                float th = Mathf.PI * j / lat, ph = Mathf.PI * 2 * i / lon;
                var p = new Vector3(Mathf.Sin(th) * Mathf.Cos(ph), Mathf.Cos(th), Mathf.Sin(th) * Mathf.Sin(ph)) * 0.5f;
                idx[i, j] = b.Vert(p, p.normalized, new Vector2((float)i / lon, 1f - (float)j / lat), Color.white);
            }
            for (int j = 0; j < lat; j++)
            for (int i = 0; i < lon; i++)
            {
                b.Index(0, idx[i, j], idx[i + 1, j], idx[i, j + 1]);
                b.Index(0, idx[i + 1, j], idx[i + 1, j + 1], idx[i, j + 1]);
            }
            return b.Build("Sphere");
        }

        /// <summary>
        /// Soft-edged flat mesh covering a union of circles (world units). Vertex alpha = 1 inside, fading to 0 over
        /// `fade` units at the border, so shaders can blend the shore.
        /// </summary>
        public static Mesh FieldMesh(string name, Vector3[] circlesWorld, float y, float cell, float fade, float grow = 0f)
        {
            float Sdf(Vector2 p)
            {
                float d = float.MaxValue;
                foreach (var c in circlesWorld) d = Mathf.Min(d, Vector2.Distance(p, new Vector2(c.x, c.y)) - c.z - grow);
                return d;
            }
            float minX = float.MaxValue, minZ = float.MaxValue, maxX = float.MinValue, maxZ = float.MinValue;
            foreach (var c in circlesWorld)
            {
                minX = Mathf.Min(minX, c.x - c.z - grow - cell); maxX = Mathf.Max(maxX, c.x + c.z + grow + cell);
                minZ = Mathf.Min(minZ, c.y - c.z - grow - cell); maxZ = Mathf.Max(maxZ, c.y + c.z + grow + cell);
            }
            int nx = Mathf.CeilToInt((maxX - minX) / cell), nz = Mathf.CeilToInt((maxZ - minZ) / cell);
            var b = new Builder();
            var ids = new int[nx + 1, nz + 1];
            var sdf = new float[nx + 1, nz + 1];
            for (int i = 0; i <= nx; i++)
            for (int j = 0; j <= nz; j++)
            {
                var p = new Vector2(minX + i * cell, minZ + j * cell);
                sdf[i, j] = Sdf(p);
                ids[i, j] = -1;
            }
            int Id(int i, int j)
            {
                if (ids[i, j] >= 0) return ids[i, j];
                var p = new Vector3(minX + i * cell, y, minZ + j * cell);
                if (sdf[i, j] > 0)
                {
                    // Snap outside vertices onto the nearest circle so the outline is smooth instead of grid-stepped.
                    Vector3 best = Vector3.zero;
                    float bd = float.MaxValue;
                    foreach (var c in circlesWorld)
                    {
                        float d = Vector2.Distance(new Vector2(p.x, p.z), new Vector2(c.x, c.y)) - c.z - grow;
                        if (d < bd) { bd = d; best = c; }
                    }
                    var dir = new Vector2(p.x - best.x, p.z - best.y).normalized * (best.z + grow);
                    p = new Vector3(best.x + dir.x, y, best.y + dir.y);
                }
                float a = Mathf.Clamp01(-sdf[i, j] / fade);
                return ids[i, j] = b.Vert(p, Vector3.up, new Vector2(p.x, p.z), new Color(1, 1, 1, a));
            }
            for (int i = 0; i < nx; i++)
            for (int j = 0; j < nz; j++)
            {
                if (Mathf.Min(Mathf.Min(sdf[i, j], sdf[i + 1, j]), Mathf.Min(sdf[i, j + 1], sdf[i + 1, j + 1])) > 0) continue;
                int a = Id(i, j), bb = Id(i + 1, j), c = Id(i + 1, j + 1), d = Id(i, j + 1);
                b.Index(0, a, d, c);
                b.Index(0, a, c, bb);
            }
            return b.Build(name);
        }

        public static Mesh GroundPlane(string name, float sizeX, float sizeZ, Vector3 center, float uvScale, int segX, int segZ)
        {
            var b = new Builder();
            var ids = new int[segX + 1, segZ + 1];
            for (int i = 0; i <= segX; i++)
            for (int j = 0; j <= segZ; j++)
            {
                var p = center + new Vector3(-sizeX / 2 + sizeX * i / segX, 0, -sizeZ / 2 + sizeZ * j / segZ);
                ids[i, j] = b.Vert(p, Vector3.up, new Vector2(p.x, p.z) * uvScale, Color.white);
            }
            for (int i = 0; i < segX; i++)
            for (int j = 0; j < segZ; j++)
            {
                b.Index(0, ids[i, j], ids[i, j + 1], ids[i + 1, j + 1]);
                b.Index(0, ids[i, j], ids[i + 1, j + 1], ids[i + 1, j]);
            }
            return b.Build(name);
        }
    }
}
