using System;
using System.Collections.Generic;
using UnityEngine;

namespace Poketato
{
    [Serializable]
    public class AnimInfo
    {
        public string name;
        public int x, y, w, h, frames, rows;
        public int[] durations;
        public float ax, ay;
    }

    [Serializable]
    public class SpeciesInfo
    {
        public int dex;
        public string id, nameCn, texture;
        public string[] types;
        public int atlasW, atlasH, shadow;
        public bool hover;
        public AnimInfo[] anims;

        [NonSerialized] public Material material;
        [NonSerialized] public Texture2D atlas;
        [NonSerialized] Dictionary<string, AnimInfo> byName;

        public AnimInfo Anim(string n)
        {
            if (byName == null)
            {
                byName = new Dictionary<string, AnimInfo>();
                foreach (var a in anims) byName[a.name] = a;
            }
            if (byName.TryGetValue(n, out var r)) return r;
            return byName.TryGetValue("Idle", out r) ? r : anims[0];
        }

        public Rect FrameUV(AnimInfo a, int frame, int row)
        {
            const float inset = 0.02f;
            float x0 = a.x + frame * a.w + inset, y0 = a.y + row * a.h + inset;
            float w = a.w - inset * 2, h = a.h - inset * 2;
            return new Rect(x0 / atlasW, 1f - (y0 + h) / atlasH, w / atlasW, h / atlasH);
        }
    }

    [Serializable]
    class SpeciesFile { public SpeciesInfo[] species; }

    /// <summary>Loads the sprite atlas metadata written by tools/import_sprites.py and the per-species materials.</summary>
    public static class SpriteDb
    {
        static Dictionary<int, SpeciesInfo> byDex;
        static Mesh quad;

        public static IEnumerable<SpeciesInfo> All { get { Load(); return byDex.Values; } }

        public static SpeciesInfo Get(int dex)
        {
            Load();
            return byDex[dex];
        }

        static void Load()
        {
            if (byDex != null) return;
            byDex = new Dictionary<int, SpeciesInfo>();
            var json = Resources.Load<TextAsset>("pokemon_sprites");
            var file = JsonUtility.FromJson<SpeciesFile>(json.text);
            foreach (var s in file.species)
            {
                s.material = Resources.Load<Material>("PokemonMaterials/" + s.texture);
                s.atlas = s.material != null ? s.material.mainTexture as Texture2D : null;
                byDex[s.dex] = s;
            }
        }

        /// <summary>Unit quad with its pivot at the bottom-left corner, facing -Z.</summary>
        public static Mesh Quad
        {
            get
            {
                if (quad != null) return quad;
                quad = new Mesh { name = "SpriteQuad" };
                quad.vertices = new[] { new Vector3(0, 0, 0), new Vector3(1, 0, 0), new Vector3(0, 1, 0), new Vector3(1, 1, 0) };
                quad.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) };
                quad.normals = new[] { Vector3.back, Vector3.back, Vector3.back, Vector3.back };
                quad.triangles = new[] { 0, 2, 1, 2, 3, 1 };
                quad.bounds = new Bounds(new Vector3(0.5f, 0.5f, 0), new Vector3(1, 1, 0.2f));
                return quad;
            }
        }
    }
}
