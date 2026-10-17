using UnityEngine;

namespace Poketato
{
    public enum FieldKind { Rain, Lava }

    /// <summary>
    /// Local type fields from poke-rouge (局部属性场地). In the web game they grow randomly each wave; the MVP keeps
    /// two fixed ones so the scene generator can build matching 3D geometry (reflective puddles, emissive lava).
    /// Shapes are unions of circles in arena pixels.
    /// </summary>
    public class FieldDef
    {
        public FieldKind kind;
        public string name;
        public Vector3[] blobs; // x, y, radius (px)

        public bool Contains(Vector2 p)
        {
            foreach (var b in blobs)
            {
                float dx = p.x - b.x, dy = p.y - b.y;
                if (dx * dx + dy * dy < b.z * b.z) return true;
            }
            return false;
        }
    }

    public static class Fields
    {
        public static readonly FieldDef[] All =
        {
            new FieldDef
            {
                kind = FieldKind.Rain, name = "局部阵雨",
                blobs = new[] { new Vector3(196, 336, 66), new Vector3(256, 302, 54), new Vector3(150, 392, 50), new Vector3(244, 392, 52), new Vector3(300, 352, 40) },
            },
            new FieldDef
            {
                kind = FieldKind.Lava, name = "熔岩池",
                blobs = new[] { new Vector3(590, 196, 52), new Vector3(636, 238, 42), new Vector3(556, 246, 38), new Vector3(612, 150, 34) },
            },
        };

        public static FieldDef At(Vector2 p)
        {
            foreach (var f in All) if (f.Contains(p)) return f;
            return null;
        }

        public static Vector3 ToWorld(Vector2 px, float y = 0f) =>
            new((px.x - GameData.ArenaW * 0.5f) / GameData.PxPerUnit, y, (GameData.ArenaH * 0.5f - px.y) / GameData.PxPerUnit);
    }
}
