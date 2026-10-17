using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Poketato
{
    /// <summary>
    /// Builds the 48x36-tile arena (poke-rouge's 768x576 px field) as Unity Tilemaps at load time: route grass,
    /// paths, a paved plaza, tall-grass patches, the gameplay fields (rain puddle = water, lava pool = lava) and a
    /// forest border. Field tiles come straight from Fields.cs, so visuals always match the simulation.
    /// </summary>
    public class ArenaMap : MonoBehaviour
    {
        public const int Cols = 48, Rows = 36, Border = 10;

        enum Mat { Grass, Path, Stone, Water, Lava, Tall, Tree, Ledge }

        Mat[,] mat;
        Tilemap ground, decor;
        readonly Dictionary<string, PixelTile> tiles = new();
        public Bounds ArenaBounds => new(Vector3.zero, new Vector3(Cols, Rows, 1));

        static readonly Vector3[] TallPatches = { new(5, 5, 2.6f), new(42, 31, 3f), new(19, 31, 2.2f), new(30, 4, 2.4f), new(44, 22, 2f), new(3, 32, 2f) };

        public static Vector3Int Cell(int tx, int ty) => new(tx - Cols / 2, Rows / 2 - 1 - ty, 0);

        PixelTile T(string name, params string[] frames)
        {
            if (tiles.TryGetValue(name, out var t)) return t;
            var sprites = new Sprite[frames.Length == 0 ? 1 : frames.Length];
            if (frames.Length == 0) sprites[0] = PixelArt.Get(name);
            else for (int i = 0; i < frames.Length; i++) sprites[i] = PixelArt.Get(frames[i]);
            t = PixelTile.Make(sprites);
            tiles[name] = t;
            return t;
        }

        Tilemap NewTilemap(Transform grid, string name, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(grid, false);
            var tm = go.AddComponent<Tilemap>();
            var r = go.AddComponent<TilemapRenderer>();
            r.sortingOrder = order;
            r.mode = TilemapRenderer.Mode.Chunk;
            return tm;
        }

        static bool InCircles(FieldDef f, float px, float py) => f.Contains(new Vector2(px, py));

        public void Build()
        {
            PixelArt.Build();
            var grid = new GameObject("Grid").AddComponent<Grid>();
            grid.transform.SetParent(transform, false);
            ground = NewTilemap(grid.transform, "Ground", ArenaView.GroundOrder);
            decor = NewTilemap(grid.transform, "Decor", ArenaView.DecorOrder);

            int w = Cols + Border * 2, h = Rows + Border * 2;
            mat = new Mat[w, h];
            for (int ty = -Border; ty < Rows + Border; ty++)
            for (int tx = -Border; tx < Cols + Border; tx++)
                mat[tx + Border, ty + Border] = Classify(tx, ty);

            for (int ty = -Border; ty < Rows + Border; ty++)
            for (int tx = -Border; tx < Cols + Border; tx++)
                Paint(tx, ty);

            AddProp("crystal", new Vector2(92, 96));
            AddProp("sign", new Vector2(424, 336));
        }

        Mat At(int tx, int ty)
        {
            tx = Mathf.Clamp(tx, -Border, Cols + Border - 1);
            ty = Mathf.Clamp(ty, -Border, Rows + Border - 1);
            return mat[tx + Border, ty + Border];
        }

        static bool OnPath(int tx, int ty) => tx == 23 || tx == 24 || ty == 17 || ty == 18;

        Mat Classify(int tx, int ty)
        {
            bool inside = tx >= 0 && tx < Cols && ty >= 0 && ty < Rows;
            if (!inside)
            {
                bool road = tx == 23 || tx == 24;
                if (ty == Rows && tx >= 0 && tx < Cols && !road) return Mat.Ledge;
                if (road && (ty < 0 || ty >= Rows)) return Mat.Path;
                if (ty == Rows + 1 && tx >= 0 && tx < Cols) return Mat.Grass;
                return Mat.Tree;
            }
            float px = (tx + 0.5f) * 16f, py = (ty + 0.5f) * 16f;
            if (InCircles(Fields.All[0], px, py)) return Mat.Water;
            if (InCircles(Fields.All[1], px, py)) return Mat.Lava;
            float dx = tx + 0.5f - Cols / 2f, dy = ty + 0.5f - Rows / 2f;
            if (dx * dx + dy * dy < 4.6f * 4.6f) return Mat.Stone;
            if (OnPath(tx, ty)) return Mat.Path;
            foreach (var p in TallPatches)
            {
                float ex = tx + 0.5f - p.x, ey = ty + 0.5f - p.y;
                if (ex * ex + ey * ey < p.z * p.z) return Mat.Tall;
            }
            return Mat.Grass;
        }

        int Mask(int tx, int ty, System.Func<Mat, bool> same)
        {
            int m = 0;
            if (same(At(tx, ty - 1))) m |= PixelArt.N;
            if (same(At(tx + 1, ty))) m |= PixelArt.E;
            if (same(At(tx, ty + 1))) m |= PixelArt.S;
            if (same(At(tx - 1, ty))) m |= PixelArt.Wb;
            return m;
        }

        void Paint(int tx, int ty)
        {
            var cell = Cell(tx, ty);
            uint hsh = (uint)(tx * 73856093 ^ ty * 19349663);
            var grass = T($"grass_{hsh % 4}");
            switch (At(tx, ty))
            {
                case Mat.Grass:
                    ground.SetTile(cell, (hsh >> 4) % 23 == 0 ? FlowerTile(hsh) : grass);
                    break;
                case Mat.Tall:
                    ground.SetTile(cell, T("tall", "tall_0", "tall_1"));
                    break;
                case Mat.Path:
                {
                    int m = Mask(tx, ty, k => k == Mat.Path || k == Mat.Stone);
                    ground.SetTile(cell, T($"path_{m}"));
                    break;
                }
                case Mat.Stone:
                {
                    int m = Mask(tx, ty, k => k == Mat.Stone);
                    // Where the plaza meets a path the path tile carries the edge.
                    if (At(tx, ty - 1) == Mat.Path) m |= PixelArt.N;
                    if (At(tx + 1, ty) == Mat.Path) m |= PixelArt.E;
                    if (At(tx, ty + 1) == Mat.Path) m |= PixelArt.S;
                    if (At(tx - 1, ty) == Mat.Path) m |= PixelArt.Wb;
                    ground.SetTile(cell, T($"stone_{m}"));
                    break;
                }
                case Mat.Water:
                {
                    int m = Mask(tx, ty, k => k == Mat.Water);
                    var t = T($"water_{m}", $"water_{m}_0", $"water_{m}_1");
                    t.fps = 1.5f;
                    ground.SetTile(cell, t);
                    break;
                }
                case Mat.Lava:
                {
                    int m = Mask(tx, ty, k => k == Mat.Lava);
                    var t = T($"lava_{m}", $"lava_{m}_0", $"lava_{m}_1");
                    t.fps = 2.5f;
                    ground.SetTile(cell, t);
                    break;
                }
                case Mat.Ledge:
                    ground.SetTile(cell, T("ledge"));
                    break;
                case Mat.Tree:
                {
                    ground.SetTile(cell, grass);
                    int bx = ((tx + Border) & ~1) - Border, by = ((ty + Border) & ~1) - Border;
                    bool whole = At(bx, by) == Mat.Tree && At(bx + 1, by) == Mat.Tree && At(bx, by + 1) == Mat.Tree && At(bx + 1, by + 1) == Mat.Tree;
                    if (whole) decor.SetTile(cell, T($"tree_{(tx - bx) + (ty - by) * 2}"));
                    else if ((hsh >> 6) % 5 == 0) decor.SetTile(cell, T("rock"));
                    break;
                }
            }
            bool inside = tx >= 0 && tx < Cols && ty >= 0 && ty < Rows;
            if (inside && At(tx, ty) == Mat.Grass && (tx < 2 || tx >= Cols - 2 || ty < 2) && (hsh >> 9) % 9 == 0)
                decor.SetTile(cell, T("rock"));
        }

        PixelTile FlowerTile(uint hsh)
        {
            string[] kinds = { "flowerR", "flowerY", "flowerW" };
            string k = kinds[(hsh >> 8) % 3];
            var t = T(k, $"{k}_0", $"{k}_1");
            t.fps = 1.5f;
            return t;
        }

        void AddProp(string sprite, Vector2 simPx)
        {
            var go = new GameObject("Prop_" + sprite);
            go.transform.SetParent(transform, false);
            go.transform.position = new Vector3((simPx.x - GameData.ArenaW / 2) / 16f, (GameData.ArenaH / 2 - simPx.y) / 16f, 0);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = PixelArt.Get(sprite);
            sr.sortingOrder = ArenaView.ActorOrder + (int)simPx.y;
        }

        /// <summary>World-space rectangle of the rain field (for the rain effect).</summary>
        public static Rect FieldRectPx(FieldDef f)
        {
            float x0 = float.MaxValue, y0 = float.MaxValue, x1 = float.MinValue, y1 = float.MinValue;
            foreach (var b in f.blobs)
            {
                x0 = Mathf.Min(x0, b.x - b.z); y0 = Mathf.Min(y0, b.y - b.z);
                x1 = Mathf.Max(x1, b.x + b.z); y1 = Mathf.Max(y1, b.y + b.z);
            }
            return Rect.MinMaxRect(x0, y0, x1, y1);
        }
    }
}
