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

        [NonSerialized] public Texture2D atlas;
        [NonSerialized] Dictionary<string, AnimInfo> byName;
        [NonSerialized] readonly Dictionary<int, Sprite> sprites = new();

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

        /// <summary>
        /// Sprite for one frame/direction. Pivot is the PMD foot anchor so sprites stand on their sim position;
        /// 16 px per unit like the tiles, drawn 1:1 on the 480x320 pixel grid.
        /// </summary>
        public Sprite Frame(AnimInfo a, int frame, int row)
        {
            int key = (Array.IndexOf(anims, a) * 64 + frame) * 8 + row;
            if (sprites.TryGetValue(key, out var s)) return s;
            int x0 = a.x + frame * a.w, y0 = a.y + row * a.h;
            var rect = new Rect(x0, atlasH - y0 - a.h, a.w, a.h);
            var pivot = new Vector2((a.ax + 0.5f) / a.w, 1f - (a.ay + 0.5f) / a.h);
            s = Sprite.Create(atlas, rect, pivot, PixelArt.WorldPPU, 0, SpriteMeshType.FullRect);
            s.name = $"{id}_{a.name}_{frame}_{row}";
            sprites[key] = s;
            return s;
        }
    }

    [Serializable]
    class SpeciesFile { public SpeciesInfo[] species; }

    /// <summary>Loads the PMD sprite atlases written by tools/import_sprites.py (Resources/Pokemon).</summary>
    public static class SpriteDb
    {
        static Dictionary<int, SpeciesInfo> byDex;

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
                s.atlas = Resources.Load<Texture2D>("Pokemon/" + s.texture);
                if (s.atlas) s.atlas.filterMode = FilterMode.Point;
                byDex[s.dex] = s;
            }
        }
    }
}
