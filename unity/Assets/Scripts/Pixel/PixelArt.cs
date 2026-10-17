using System.Collections.Generic;
using UnityEngine;

namespace Poketato
{
    /// <summary>
    /// All non-Pokémon art (tiles, props, effects, digits, UI frames) is original pixel art drawn here at startup in a
    /// GBA / Pokémon Emerald inspired palette and packed into one point-filtered atlas. No Nintendo assets are used.
    /// </summary>
    public static class PixelArt
    {
        public const int WorldPPU = 16;
        public const int UiPPU = 100;

        public class Img
        {
            public readonly int w, h;
            public readonly Color32[] px;
            public Vector2 pivot = new(0.5f, 0.5f);
            public int ppu = WorldPPU;
            public Vector4 border;

            public Img(int w, int h) { this.w = w; this.h = h; px = new Color32[w * h]; }

            public void Set(int x, int y, Color32 c)
            {
                if (x < 0 || y < 0 || x >= w || y >= h) return;
                px[y * w + x] = c;
            }

            public Color32 Get(int x, int y) => x < 0 || y < 0 || x >= w || y >= h ? default : px[y * w + x];
            public bool Solid(int x, int y) => Get(x, y).a > 0;

            public void Fill(Color32 c) { for (int i = 0; i < px.Length; i++) px[i] = c; }

            public void Rect(int x, int y, int rw, int rh, Color32 c)
            {
                for (int j = y; j < y + rh; j++)
                for (int i = x; i < x + rw; i++) Set(i, j, c);
            }

            public void Disc(float cx, float cy, float r, Color32 c)
            {
                for (int y = Mathf.FloorToInt(cy - r); y <= Mathf.CeilToInt(cy + r); y++)
                for (int x = Mathf.FloorToInt(cx - r); x <= Mathf.CeilToInt(cx + r); x++)
                    if ((x + 0.5f - cx) * (x + 0.5f - cx) + (y + 0.5f - cy) * (y + 0.5f - cy) <= r * r) Set(x, y, c);
            }

            public void Ellipse(float cx, float cy, float rx, float ry, Color32 c)
            {
                for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float dx = (x + 0.5f - cx) / rx, dy = (y + 0.5f - cy) / ry;
                    if (dx * dx + dy * dy <= 1f) Set(x, y, c);
                }
            }

            public void Ring(float cx, float cy, float r, float thick, Color32 c)
            {
                for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float d = Mathf.Sqrt((x + 0.5f - cx) * (x + 0.5f - cx) + (y + 0.5f - cy) * (y + 0.5f - cy));
                    if (d <= r && d > r - thick) Set(x, y, c);
                }
            }

            /// <summary>Adds a 1px outline around opaque pixels (4-neighbourhood).</summary>
            public void Outline(Color32 c)
            {
                var copy = (Color32[])px.Clone();
                for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    if (copy[y * w + x].a > 0) continue;
                    bool n = (x > 0 && copy[y * w + x - 1].a > 0) || (x < w - 1 && copy[y * w + x + 1].a > 0) ||
                             (y > 0 && copy[(y - 1) * w + x].a > 0) || (y < h - 1 && copy[(y + 1) * w + x].a > 0);
                    if (n) px[y * w + x] = c;
                }
            }

            public void Pattern(int ox, int oy, string[] rows, Color32 c)
            {
                for (int j = 0; j < rows.Length; j++)
                for (int i = 0; i < rows[j].Length; i++)
                    if (rows[j][i] != ' ' && rows[j][i] != '.') Set(ox + i, oy + j, c);
            }

            public void Blit(Img src, int ox, int oy)
            {
                for (int y = 0; y < src.h; y++)
                for (int x = 0; x < src.w; x++)
                {
                    var c = src.px[y * src.w + x];
                    if (c.a > 0) Set(ox + x, oy + y, c);
                }
            }

            public Img Clone()
            {
                var r = new Img(w, h) { pivot = pivot, ppu = ppu, border = border };
                System.Array.Copy(px, r.px, px.Length);
                return r;
            }
        }

        // ------------------------------------------------------------------ palette

        static Color32 H(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out var c);
            return c;
        }

        static readonly Color32 Clear = new(0, 0, 0, 0);
        static readonly Color32 GLight = H("#b8e890"), GBase = H("#98d870"), GShade = H("#78c058"), GBlade = H("#58a048"), GDark = H("#387830");
        static readonly Color32 TgBase = H("#60b050"), TgDark = H("#307828"), TgTip = H("#a8e078"), TgOutline = H("#205818");
        static readonly Color32 P0 = H("#f8e8c0"), P1 = H("#e8d098"), P2 = H("#d8b878"), P3 = H("#b89858");
        static readonly Color32 W0 = H("#f8f8f8"), W1 = H("#a0d0f8"), W2 = H("#70a0f0"), W3 = H("#5080d8"), Sand = H("#f0e0a8"), SandDark = H("#d8c080");
        static readonly Color32 L0 = H("#f8e878"), L1 = H("#f8a830"), L2 = H("#e86020"), L3 = H("#b03818"), Rock0 = H("#806058"), Rock1 = H("#584038"), Rock2 = H("#302018");
        static readonly Color32 S0 = H("#e8e8e0"), S1 = H("#c8c8c0"), S2 = H("#a0a098"), S3 = H("#787870");
        static readonly Color32 T0 = H("#a8d878"), T1 = H("#70b050"), T2 = H("#489038"), T3 = H("#306828"), T4 = H("#184018"), Trunk = H("#a06838"), TrunkDark = H("#684020");
        static readonly Color32 R0 = H("#e8e0d8"), R1 = H("#b8b0a8"), R2 = H("#908880"), R3 = H("#484040");
        static readonly Color32 Wood = H("#c08850"), WoodDark = H("#805028"), WoodLight = H("#e0b078");
        public static readonly Color32 TextColor = H("#404040"), TextShadow = H("#d0d0c8");
        public static readonly Color32 WinOut = H("#384058"), WinMid = H("#6888c8"), WinIn = H("#c0d0f0"), WinFill = H("#f8f8f8");

        // ------------------------------------------------------------------ registry

        static readonly Dictionary<string, Img> imgs = new();
        static readonly Dictionary<string, Sprite> sprites = new();
        public static Texture2D Atlas { get; private set; }
        public static bool Built => Atlas != null;

        public static Sprite Get(string name) => sprites.TryGetValue(name, out var s) ? s : sprites["white"];

        public static Sprite[] Frames(string name, int n)
        {
            var r = new Sprite[n];
            for (int i = 0; i < n; i++) r[i] = Get($"{name}_{i}");
            return r;
        }

        static Img Add(string name, Img img)
        {
            imgs[name] = img;
            return img;
        }

        static uint Hash(int x, int y, int s)
        {
            uint h = (uint)(x * 374761393 + y * 668265263 + s * 1442695041);
            h = (h ^ (h >> 13)) * 1274126177u;
            return h ^ (h >> 16);
        }

        static float Rand(int x, int y, int s) => (Hash(x, y, s) & 0xffff) / 65535f;

        public static void Build()
        {
            if (Built) return;
            DrawTiles();
            DrawProps();
            DrawFx();
            DrawDigits();
            DrawUi();
            Pack();
        }

        // ------------------------------------------------------------------ tiles

        /// <summary>Neighbour mask bits for edge tiles: set when the neighbour is the same material.</summary>
        public const int N = 1, E = 2, S = 4, Wb = 8;

        static void DrawTiles()
        {
            for (int v = 0; v < 4; v++)
            {
                var g = new Img(16, 16);
                GrassBase(g, v);
                Add($"grass_{v}", g);
            }
            for (int f = 0; f < 2; f++)
            {
                var t = new Img(16, 16);
                TallGrass(t, f);
                Add($"tall_{f}", t);
                foreach (var (name, col) in new[] { ("flowerR", H("#f85858")), ("flowerY", H("#f8d838")), ("flowerW", H("#f8f8f8")) })
                {
                    var fl = new Img(16, 16);
                    GrassBase(fl, 1);
                    Flower(fl, 4, 4, col, f);
                    Flower(fl, 11, 10, col, 1 - f);
                    Add($"{name}_{f}", fl);
                }
            }
            for (int m = 0; m < 16; m++)
            {
                Add($"path_{m}", PathTile(m));
                Add($"stone_{m}", StoneTile(m));
                for (int f = 0; f < 2; f++)
                {
                    Add($"water_{m}_{f}", WaterTile(m, f));
                    Add($"lava_{m}_{f}", LavaTile(m, f));
                }
            }
            var tree = Tree();
            for (int i = 0; i < 4; i++)
            {
                var part = new Img(16, 16);
                int ox = (i % 2) * 16, oy = (i / 2) * 16;
                for (int y = 0; y < 16; y++)
                for (int x = 0; x < 16; x++) part.Set(x, y, tree.Get(ox + x, oy + y));
                Add($"tree_{i}", part);
            }
            var ledge = new Img(16, 16);
            GrassBase(ledge, 2);
            for (int x = 0; x < 16; x++)
            {
                int y0 = 10 + (x % 8 == 3 ? 1 : 0);
                ledge.Set(x, y0, GDark);
                ledge.Set(x, y0 + 1, GBlade);
                ledge.Rect(x, y0 + 2, 1, 16 - y0 - 2, GShade);
            }
            Add("ledge", ledge);
        }

        static void GrassBase(Img g, int v)
        {
            g.Fill(GBase);
            for (int y = 0; y < 16; y++)
            for (int x = 0; x < 16; x++)
            {
                float r = Rand(x, y, 11 + v);
                if (r > 0.93f) g.Set(x, y, GLight);
                else if (r < 0.05f) g.Set(x, y, GShade);
            }
            int blades = v == 3 ? 0 : 2 + v % 2;
            for (int i = 0; i < blades; i++)
            {
                int bx = 1 + (int)(Rand(i, v, 3) * 12), by = 2 + (int)(Rand(v, i, 5) * 11);
                g.Set(bx, by, GBlade);
                g.Set(bx + 2, by, GBlade);
                g.Set(bx + 1, by + 1, GBlade);
                g.Set(bx + 1, by - 1, GLight);
            }
        }

        static void TallGrass(Img t, int f)
        {
            t.Fill(TgBase);
            for (int row = 0; row < 2; row++)
            for (int col = 0; col < 2; col++)
            {
                int ox = col * 8 + (row == 1 ? 4 : 0) + (f == 1 ? 1 : 0), oy = row * 8 + 1;
                string[] tuft = { "   #   ", "  ###  ", " ## ## ", "##   ##", "#  #  #" };
                for (int dx = -8; dx <= 8; dx += 16)
                {
                    t.Pattern(ox + dx - 3 + 4, oy, tuft, TgOutline);
                    t.Set(ox + dx + 4, oy + 1, TgTip);
                    t.Set(ox + dx + 3, oy + 2, TgTip);
                    t.Set(ox + dx + 5, oy + 2, TgTip);
                }
            }
            for (int x = 0; x < 16; x++) t.Set(x, 15, TgDark);
        }

        static void Flower(Img g, int cx, int cy, Color32 petal, int f)
        {
            var center = H("#f8a800");
            if (f == 0)
            {
                g.Set(cx, cy - 1, petal); g.Set(cx, cy + 1, petal); g.Set(cx - 1, cy, petal); g.Set(cx + 1, cy, petal);
            }
            else
            {
                g.Set(cx - 1, cy - 1, petal); g.Set(cx + 1, cy - 1, petal); g.Set(cx - 1, cy + 1, petal); g.Set(cx + 1, cy + 1, petal);
            }
            g.Set(cx, cy, center);
            g.Set(cx, cy + 2, GBlade);
        }

        delegate void EdgePainter(Img img, int x, int y, int depth);

        /// <summary>Paints a border band where the neighbour is a different material; rounded outer corners.</summary>
        static void Edges(Img img, int mask, int band, EdgePainter paint)
        {
            for (int y = 0; y < 16; y++)
            for (int x = 0; x < 16; x++)
            {
                int d = 99;
                if ((mask & N) == 0) d = Mathf.Min(d, y);
                if ((mask & S) == 0) d = Mathf.Min(d, 15 - y);
                if ((mask & Wb) == 0) d = Mathf.Min(d, x);
                if ((mask & E) == 0) d = Mathf.Min(d, 15 - x);
                // Rounded corners where two edges meet.
                if ((mask & N) == 0 && (mask & Wb) == 0 && x + y < band + 2) d = Mathf.Min(d, (x + y) / 2);
                if ((mask & N) == 0 && (mask & E) == 0 && (15 - x) + y < band + 2) d = Mathf.Min(d, ((15 - x) + y) / 2);
                if ((mask & S) == 0 && (mask & Wb) == 0 && x + (15 - y) < band + 2) d = Mathf.Min(d, (x + (15 - y)) / 2);
                if ((mask & S) == 0 && (mask & E) == 0 && (15 - x) + (15 - y) < band + 2) d = Mathf.Min(d, ((15 - x) + (15 - y)) / 2);
                if (d < band) paint(img, x, y, d);
            }
        }

        static Img PathTile(int mask)
        {
            var t = new Img(16, 16);
            t.Fill(P1);
            for (int y = 0; y < 16; y++)
            for (int x = 0; x < 16; x++)
            {
                float r = Rand(x, y, 31);
                if (r > 0.9f) t.Set(x, y, P2);
                else if (r < 0.04f) t.Set(x, y, P0);
            }
            Edges(t, mask, 3, (img, x, y, d) => img.Set(x, y, d == 0 ? GBase : d == 1 ? GShade : P3));
            return t;
        }

        static Img StoneTile(int mask)
        {
            var t = new Img(16, 16);
            for (int y = 0; y < 16; y++)
            for (int x = 0; x < 16; x++)
            {
                int ox = (y / 8) % 2 * 4;
                bool grout = y % 8 == 7 || (x + ox) % 8 == 7;
                bool lit = y % 8 == 0 || (x + ox) % 8 == 0;
                t.Set(x, y, grout ? S3 : lit ? S0 : S1);
            }
            Edges(t, mask, 3, (img, x, y, d) => img.Set(x, y, d == 0 ? GBase : d == 1 ? S3 : S2));
            return t;
        }

        static Img WaterTile(int mask, int f)
        {
            var t = new Img(16, 16);
            t.Fill(W2);
            for (int i = 0; i < 3; i++)
            {
                int wy = (i * 5 + 2 + f * 2) % 16, wx = (i * 7 + f * 3) % 16;
                for (int k = 0; k < 4; k++) t.Set((wx + k) % 16, wy, W1);
                t.Set((wx + 1) % 16, (wy + 15) % 16, W0);
                t.Set((wx + 2) % 16, (wy + 15) % 16, W0);
            }
            for (int y = 0; y < 16; y++)
            for (int x = 0; x < 16; x++)
                if (Rand(x, y, 41 + f) > 0.97f) t.Set(x, y, W3);
            Edges(t, mask, 4, (img, x, y, d) => img.Set(x, y, d <= 1 ? Sand : d == 2 ? SandDark : (x + y + f) % 3 == 0 ? W0 : W1));
            return t;
        }

        static Img LavaTile(int mask, int f)
        {
            var t = new Img(16, 16);
            t.Fill(L2);
            for (int y = 0; y < 16; y++)
            for (int x = 0; x < 16; x++)
            {
                float n = Mathf.Sin((x + f * 3) * 0.9f) + Mathf.Sin((y - f * 2) * 0.7f + x * 0.3f);
                if (n > 1.3f) t.Set(x, y, L0);
                else if (n > 0.6f) t.Set(x, y, L1);
                else if (n < -1.4f) t.Set(x, y, L3);
            }
            Edges(t, mask, 4, (img, x, y, d) => img.Set(x, y, d == 0 ? Rock2 : d == 1 ? Rock1 : d == 2 ? Rock0 : L3));
            return t;
        }

        static Img Tree()
        {
            var t = new Img(32, 32);
            t.Rect(13, 22, 6, 9, Trunk);
            t.Rect(13, 22, 1, 9, TrunkDark);
            t.Rect(18, 22, 1, 9, TrunkDark);
            var canopy = new Img(32, 32);
            canopy.Disc(16, 12, 12.5f, T2);
            canopy.Disc(7, 17, 6.5f, T2);
            canopy.Disc(25, 17, 6.5f, T2);
            canopy.Disc(16, 19, 8f, T2);
            for (int y = 0; y < 32; y++)
            for (int x = 0; x < 32; x++)
            {
                if (!canopy.Solid(x, y)) continue;
                if (y > 18 || (!canopy.Solid(x, y + 3) && y > 10)) canopy.Set(x, y, T3);
            }
            canopy.Disc(11, 7, 4.5f, T1);
            canopy.Disc(21, 10, 3f, T1);
            canopy.Disc(6, 15, 2.5f, T1);
            canopy.Set(10, 5, T0); canopy.Set(11, 5, T0); canopy.Set(9, 6, T0); canopy.Set(20, 9, T0); canopy.Set(5, 14, T0);
            t.Blit(canopy, 0, 0);
            t.Outline(T4);
            return t;
        }

        // ------------------------------------------------------------------ props

        static void DrawProps()
        {
            var rock = new Img(16, 16);
            rock.Ellipse(8, 9.5f, 6.5f, 5.5f, R1);
            rock.Ellipse(7, 8, 4f, 3f, R0);
            for (int y = 0; y < 16; y++)
            for (int x = 0; x < 16; x++)
                if (rock.Solid(x, y) && y > 11) rock.Set(x, y, R2);
            rock.Outline(R3);
            Add("rock", rock);

            var fence = new Img(16, 16);
            fence.Rect(0, 6, 16, 2, Wood);
            fence.Rect(0, 10, 16, 2, Wood);
            fence.Rect(2, 3, 3, 12, WoodLight);
            fence.Rect(11, 3, 3, 12, WoodLight);
            fence.Outline(WoodDark);
            Add("fence", fence);

            var sign = new Img(16, 16);
            sign.Rect(2, 2, 12, 8, WoodLight);
            sign.Rect(3, 4, 10, 1, WoodDark);
            sign.Rect(3, 6, 8, 1, WoodDark);
            sign.Rect(7, 10, 2, 5, Wood);
            sign.Outline(WoodDark);
            sign.pivot = new Vector2(0.5f, 0.05f);
            Add("sign", sign);

            var crystal = new Img(18, 32);
            var pink = H("#f8a0d8");
            var pinkDark = H("#c050a0");
            var pinkLight = H("#fff0fa");
            void Shard(int x0, int top, int width, int bottom)
            {
                for (int y = top; y <= bottom; y++)
                {
                    float k = Mathf.Clamp01((y - top) / 4f);
                    int half = Mathf.RoundToInt(width / 2f * k);
                    for (int x = x0 - half; x <= x0 + half; x++) crystal.Set(x, y, x < x0 ? pink : pinkDark);
                }
                crystal.Set(x0 - 1, top + 3, pinkLight);
                crystal.Set(x0 - 1, top + 4, pinkLight);
            }
            Shard(9, 1, 7, 29);
            Shard(4, 12, 5, 30);
            Shard(14, 9, 5, 30);
            crystal.Outline(H("#702060"));
            crystal.pivot = new Vector2(0.5f, 0.03f);
            Add("crystal", crystal);
        }

        // ------------------------------------------------------------------ effects

        static void DrawFx()
        {
            Add("white", Solid(4, 4, Color.white));
            var shadowCol = new Color32(16, 40, 16, 110);
            int[][] sizes = { new[] { 12, 4 }, new[] { 16, 5 }, new[] { 22, 7 } };
            for (int i = 0; i < 3; i++)
            {
                var s = new Img(sizes[i][0], sizes[i][1]);
                s.Ellipse(s.w / 2f, s.h / 2f, s.w / 2f, s.h / 2f, shadowCol);
                Add($"shadow_{i}", s);
            }

            var candy = new Img(11, 7);
            candy.Disc(5.5f, 3.5f, 3f, H("#f85898"));
            candy.Pattern(0, 1, new[] { "##", "# ", "##" }, H("#f8c0e0"));
            candy.Pattern(9, 1, new[] { "##", " #", "##" }, H("#f8c0e0"));
            candy.Set(4, 2, W0);
            candy.Outline(H("#902858"));
            Add("candy", candy);

            var berry = new Img(9, 10);
            berry.Disc(4.5f, 5.5f, 3.6f, H("#4878f8"));
            berry.Set(3, 4, H("#c0d8ff"));
            berry.Rect(4, 0, 2, 2, H("#48a838"));
            berry.Outline(H("#203878"));
            Add("berry", berry);

            Add("dot2", Solid(2, 2, Color.white));
            var dot3 = Solid(3, 3, Color.white);
            dot3.Set(0, 0, Clear); dot3.Set(2, 0, Clear); dot3.Set(0, 2, Clear); dot3.Set(2, 2, Clear);
            Add("dot3", dot3);
            Add("orb", Orb(7, Color.white, H("#c0c0c0")));
            Add("orbBig", Orb(11, Color.white, H("#c0c0c0")));

            for (int f = 0; f < 2; f++)
            {
                var ember = new Img(9, 10);
                ember.Pattern(0, 0, f == 0
                    ? new[] { "    #    ", "   ##    ", "   ###   ", "  #####  ", " ####### ", " ####### ", "#########", " ####### ", "  #####  ", "   ###   " }
                    : new[] { "     #   ", "    ##   ", "   ###   ", "  #####  ", " ####### ", " ####### ", "#########", " ####### ", "  #####  ", "   ###   " }, L2);
                ember.Pattern(0, 0, new[] { "", "", "", "    #    ", "   ###   ", "  #####  ", "  #####  ", "  #####  ", "   ###   " }, L1);
                ember.Pattern(0, 0, new[] { "", "", "", "", "", "   ###   ", "   ###   ", "    #    " }, L0);
                ember.Outline(L3);
                Add($"ember_{f}", ember);
            }

            var drop = new Img(7, 9);
            drop.Pattern(0, 0, new[] { "   #   ", "  ###  ", "  ###  ", " ##### ", "#######", "#######", "#######", " ##### ", "  ###  " }, W2);
            drop.Set(2, 4, W0); drop.Set(2, 5, W1);
            drop.Outline(H("#284890"));
            Add("drop", drop);

            var bubble = new Img(9, 9);
            bubble.Ring(4.5f, 4.5f, 4.5f, 1.2f, H("#a0d8f8"));
            bubble.Set(3, 2, W0); bubble.Set(2, 3, W0);
            Add("bubble", bubble);

            var leaf = new Img(9, 7);
            leaf.Pattern(0, 0, new[] { "     ### ", "   ######", " ####### ", "#######  ", " #####   ", "  ##     ", " #       " }, H("#58c038"));
            leaf.Pattern(0, 0, new[] { "", "      ## ", "    ##   ", "  ##     " }, H("#a8f078"));
            leaf.Outline(H("#205818"));
            Add("leaf", leaf);

            var star = new Img(9, 9);
            star.Pattern(0, 0, new[] { "    #    ", "    #    ", "   ###   ", "#########", " ####### ", "  #####  ", "  ## ##  ", " ##   ## ", " #     # " }, H("#f8e048"));
            star.Set(4, 3, W0); star.Set(4, 4, W0);
            star.Outline(H("#a07010"));
            Add("star", star);

            var spark = new Img(9, 11);
            spark.Pattern(0, 0, new[] { "    ###  ", "   ###   ", "  ###    ", " ######  ", "   ####  ", "   ###   ", "  ###    ", "  ##     ", " ##      ", " #       ", "#        " }, H("#f8e040"));
            spark.Pattern(0, 0, new[] { "", "    #    ", "   #     ", "  ###    ", "    #    " }, W0);
            spark.Outline(H("#a08010"));
            Add("spark", spark);

            var seed = new Img(6, 6);
            seed.Disc(3, 3, 2.6f, H("#a88838"));
            seed.Set(2, 2, H("#e8d080"));
            seed.Outline(H("#584018"));
            Add("seed", seed);

            var acid = new Img(8, 8);
            acid.Disc(4, 4.5f, 3.4f, H("#b058d0"));
            acid.Set(3, 3, H("#f0c0ff"));
            acid.Outline(H("#582070"));
            Add("acid", acid);

            var needle = new Img(9, 3);
            needle.Rect(0, 1, 9, 1, H("#e8e8e8"));
            needle.Rect(6, 0, 2, 3, H("#c0c0c0"));
            needle.Set(8, 1, W0);
            Add("needle", needle);

            var slash = new Img(24, 24);
            slash.Ring(12, 12, 11.5f, 3f, H("#ffffff"));
            for (int y = 0; y < 24; y++)
            for (int x = 0; x < 24; x++)
                if (x < 12) slash.Set(x, y, Clear);
            slash.Ring(12, 12, 9f, 1f, H("#c8c8c8"));
            for (int y = 0; y < 24; y++)
            for (int x = 0; x < 12; x++) slash.Set(x, y, Clear);
            Add("slash", slash);

            foreach (int d in new[] { 16, 48, 96 })
            {
                var ring = new Img(d, d);
                ring.Ring(d / 2f, d / 2f, d / 2f, d >= 48 ? 2f : 1.5f, H("#ffffff"));
                ring.Ring(d / 2f, d / 2f, d / 2f - 2f, 1f, H("#a0a0a0"));
                Add($"ring{d}", ring);
                var disc = new Img(d, d);
                disc.Disc(d / 2f, d / 2f, d / 2f, new Color32(255, 255, 255, 90));
                disc.Ring(d / 2f, d / 2f, d / 2f, 1.5f, H("#ffffff"));
                Add($"disc{d}", disc);
            }

            for (int f = 0; f < 4; f++)
            {
                var b = new Img(32, 32);
                float r = 4 + f * 4;
                if (f < 3)
                {
                    b.Disc(16, 16, r, f == 0 ? H("#ffffff") : H("#e0e0e0"));
                    for (int k = 0; k < 8; k++)
                    {
                        float a = k * Mathf.PI / 4 + f * 0.3f;
                        b.Disc(16 + Mathf.Cos(a) * (r + 2), 16 + Mathf.Sin(a) * (r + 2), 2.2f - f * 0.4f, H("#ffffff"));
                    }
                    if (f > 0) b.Disc(16, 16, r - 4, H("#ffffff"));
                }
                else
                {
                    for (int k = 0; k < 10; k++)
                    {
                        float a = k * Mathf.PI / 5;
                        b.Disc(16 + Mathf.Cos(a) * 13, 16 + Mathf.Sin(a) * 13, 1.6f, H("#c0c0c0"));
                    }
                }
                Add($"burst_{f}", b);
            }

            for (int f = 0; f < 3; f++)
            {
                var fl = new Img(8, 10);
                int sway = f == 1 ? 1 : f == 2 ? -1 : 0;
                fl.Pattern(sway, 0, new[] { "   #    ", "   ##   ", "  ###   ", "  ####  ", " ###### ", " ###### ", "########", "########", " ###### ", "  ####  " }, L2);
                fl.Pattern(sway, 0, new[] { "", "", "", "   #    ", "  ###   ", "  ####  ", " #####  ", " ###### ", "  ####  " }, L1);
                fl.Pattern(0, 0, new[] { "", "", "", "", "", "   #    ", "  ###   ", "  ###   ", "   ##   " }, L0);
                Add($"flame_{f}", fl);
                var puff = new Img(10, 9);
                puff.Disc(4f + sway, 4.5f, 3.5f, H("#ffffff"));
                puff.Disc(6.5f, 5f, 2.8f, H("#e0e0e0"));
                puff.Disc(3f, 6f, 2.5f, H("#e8e8e8"));
                Add($"puff_{f}", puff);
            }

            var beam = new Img(6, 7);
            beam.Rect(0, 0, 6, 7, H("#a0a0a0"));
            beam.Rect(0, 1, 6, 5, H("#e0e0e0"));
            beam.Rect(0, 2, 6, 3, H("#ffffff"));
            Add("beam", beam);

            var warn = new Img(9, 9);
            warn.Pattern(0, 0, new[] { "##     ##", "###   ###", " ### ### ", "  #####  ", "   ###   ", "  #####  ", " ### ### ", "###   ###", "##     ##" }, H("#f84830"));
            Add("warn", warn);
            var exclaim = new Img(7, 12);
            exclaim.Rect(0, 0, 7, 12, W0);
            exclaim.Rect(3, 2, 1, 5, H("#e83820"));
            exclaim.Rect(2, 2, 1, 4, H("#e83820"));
            exclaim.Rect(4, 2, 1, 4, H("#e83820"));
            exclaim.Rect(2, 8, 3, 2, H("#e83820"));
            exclaim.Outline(H("#404040"));
            exclaim.pivot = new Vector2(0.5f, 0f);
            Add("exclaim", exclaim);

            Add("rain", Solid(1, 5, H("#d0e8ff")));
            for (int f = 0; f < 2; f++)
            {
                var sp = new Img(7, 3);
                if (f == 0) sp.Pattern(0, 0, new[] { "  # #  ", " #   # " }, H("#e0f0ff"));
                else sp.Pattern(0, 0, new[] { "#     #", "       " }, H("#e0f0ff"));
                Add($"splash_{f}", sp);
                var lb = new Img(7, 7);
                if (f == 0) lb.Disc(3.5f, 4, 2.2f, L1);
                else lb.Ring(3.5f, 3.5f, 3.5f, 1f, L0);
                Add($"lavapop_{f}", lb);
            }
        }

        static Img Solid(int w, int h, Color32 c)
        {
            var i = new Img(w, h);
            i.Fill(c);
            return i;
        }

        static Img Orb(int d, Color32 core, Color32 rim)
        {
            var o = new Img(d, d);
            o.Disc(d / 2f, d / 2f, d / 2f, rim);
            o.Disc(d / 2f, d / 2f, d / 2f - 1.2f, core);
            return o;
        }

        // ------------------------------------------------------------------ digits

        static readonly string[][] Glyphs =
        {
            new[] { " ### ", "#   #", "#  ##", "# # #", "##  #", "#   #", " ### " },
            new[] { "  #  ", " ##  ", "  #  ", "  #  ", "  #  ", "  #  ", " ### " },
            new[] { " ### ", "#   #", "    #", "   # ", "  #  ", " #   ", "#####" },
            new[] { "#####", "   # ", "  #  ", "   # ", "    #", "#   #", " ### " },
            new[] { "   # ", "  ## ", " # # ", "#  # ", "#####", "   # ", "   # " },
            new[] { "#####", "#    ", "#### ", "    #", "    #", "#   #", " ### " },
            new[] { "  ## ", " #   ", "#    ", "#### ", "#   #", "#   #", " ### " },
            new[] { "#####", "    #", "   # ", "  #  ", " #   ", " #   ", " #   " },
            new[] { " ### ", "#   #", "#   #", " ### ", "#   #", "#   #", " ### " },
            new[] { " ### ", "#   #", "#   #", " ####", "    #", "   # ", " ##  " },
            new[] { "     ", "  #  ", "  #  ", "#####", "  #  ", "  #  ", "     " },
        };

        static void DrawDigits()
        {
            for (int i = 0; i < Glyphs.Length; i++)
            {
                var g = new Img(7, 9);
                g.Pattern(1, 1, Glyphs[i], Color.white);
                g.Outline(H("#202020"));
                g.pivot = new Vector2(0f, 0f);
                Add(i < 10 ? $"digit_{i}" : "digit_plus", g);
            }
        }

        // ------------------------------------------------------------------ UI

        static void DrawUi()
        {
            // Emerald-style message window: rounded blue-grey frame, white body. 9-slice, 1 px = 1 canvas unit.
            var win = new Img(16, 16);
            for (int y = 0; y < 16; y++)
            for (int x = 0; x < 16; x++)
            {
                int e = Mathf.Min(Mathf.Min(x, y), Mathf.Min(15 - x, 15 - y));
                bool corner = (x < 2 || x > 13) && (y < 2 || y > 13) && (Mathf.Min(x, 15 - x) + Mathf.Min(y, 15 - y) < 2);
                if (corner) continue;
                win.Set(x, y, e == 0 ? WinOut : e <= 2 ? WinMid : e == 3 ? WinIn : WinFill);
            }
            UiImg("win", win, 5);

            var dark = new Img(16, 16);
            for (int y = 0; y < 16; y++)
            for (int x = 0; x < 16; x++)
            {
                int e = Mathf.Min(Mathf.Min(x, y), Mathf.Min(15 - x, 15 - y));
                if ((x == 0 || x == 15) && (y == 0 || y == 15)) continue;
                dark.Set(x, y, e == 0 ? H("#101828") : e == 1 ? H("#a8b8e0") : new Color32(32, 40, 64, 225));
            }
            UiImg("winDark", dark, 3);

            // Battle-info style box (cream body, dark green outline) used for the HUD status panel.
            var info = new Img(16, 16);
            for (int y = 0; y < 16; y++)
            for (int x = 0; x < 16; x++)
            {
                int e = Mathf.Min(Mathf.Min(x, y), Mathf.Min(15 - x, 15 - y));
                if ((x == 0 || x == 15) && (y == 0 || y == 15)) continue;
                info.Set(x, y, e <= 1 ? H("#304830") : e == 2 ? H("#90b078") : H("#f8f8d8"));
            }
            UiImg("info", info, 4);

            var badge = new Img(8, 8);
            for (int y = 0; y < 8; y++)
            for (int x = 0; x < 8; x++)
            {
                if ((x == 0 || x == 7) && (y == 0 || y == 7)) continue;
                int e = Mathf.Min(Mathf.Min(x, y), Mathf.Min(7 - x, 7 - y));
                badge.Set(x, y, e == 0 ? H("#a0a0a0") : Color.white);
            }
            UiImg("badge", badge, 3);

            var hpFrame = new Img(8, 7);
            hpFrame.Fill(H("#404040"));
            hpFrame.Rect(1, 1, 6, 5, H("#506050"));
            UiImg("hpframe", hpFrame, 2);

            var tag = new Img(15, 7);
            tag.Fill(H("#f8a800"));
            tag.Pattern(2, 1, new[] { "# #", "# #", "###", "# #", "# #" }, H("#f8f8f8"));
            tag.Pattern(7, 1, new[] { "## ", "# #", "## ", "#  ", "#  " }, H("#f8f8f8"));
            tag.Set(0, 0, Clear); tag.Set(0, 6, Clear);
            UiImg("hptag", tag, 0);

            var cursor = new Img(6, 10);
            cursor.Pattern(1, 1, new[] { "#    ", "##   ", "###  ", "#### ", "###  ", "##   ", "#    " }, TextShadow);
            cursor.Pattern(0, 0, new[] { "#    ", "##   ", "###  ", "#### ", "###  ", "##   ", "#    " }, TextColor);
            UiImg("cursor", cursor, 0);

            var down = new Img(8, 5);
            down.Pattern(0, 0, new[] { "#######", " ##### ", "  ###  ", "   #   " }, H("#e05038"));
            UiImg("down", down, 0);

            var bag = new Img(16, 16);
            bag.Ellipse(8, 10, 6, 5, H("#e0a050"));
            bag.Rect(5, 3, 6, 3, H("#c08038"));
            bag.Rect(6, 9, 4, 2, H("#f8e0a0"));
            bag.Outline(H("#603818"));
            UiImg("icon_item", bag, 0);
            var disc = new Img(16, 16);
            disc.Disc(8, 8, 7, H("#e0e0e0"));
            disc.Disc(8, 8, 2, H("#606060"));
            disc.Set(5, 4, W0); disc.Set(4, 5, W0);
            disc.Outline(H("#404040"));
            UiImg("icon_tm", disc, 0);
        }

        static void UiImg(string name, Img img, int border)
        {
            img.ppu = UiPPU;
            img.border = new Vector4(border, border, border, border);
            Add(name, img);
        }

        // ------------------------------------------------------------------ packing

        static void Pack()
        {
            const int pad = 2, width = 1024;
            var list = new List<KeyValuePair<string, Img>>(imgs);
            list.Sort((a, b) => b.Value.h != a.Value.h ? b.Value.h.CompareTo(a.Value.h) : string.CompareOrdinal(a.Key, b.Key));
            var pos = new Dictionary<string, Vector2Int>();
            int x = pad, y = pad, shelf = 0;
            foreach (var kv in list)
            {
                var img = kv.Value;
                if (x + img.w + pad > width) { x = pad; y += shelf + pad * 2; shelf = 0; }
                pos[kv.Key] = new Vector2Int(x, y);
                x += img.w + pad * 2;
                shelf = Mathf.Max(shelf, img.h);
            }
            int height = Mathf.NextPowerOfTwo(y + shelf + pad);
            var data = new Color32[width * height];
            foreach (var kv in list)
            {
                var img = kv.Value;
                var p = pos[kv.Key];
                // Extrude edge pixels into the padding so tiles never sample transparent neighbours.
                for (int j = -pad; j < img.h + pad; j++)
                for (int i = -pad; i < img.w + pad; i++)
                {
                    int sx = Mathf.Clamp(i, 0, img.w - 1), sy = Mathf.Clamp(j, 0, img.h - 1);
                    bool inside = i == sx && j == sy;
                    bool tile = img.w == 16 && img.h == 16 && img.ppu == WorldPPU;
                    if (!inside && !tile) continue;
                    int tx = p.x + i, ty = height - 1 - (p.y + j);
                    data[ty * width + tx] = img.px[sy * img.w + sx];
                }
            }
            Atlas = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = "PixelArtAtlas", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp,
            };
            Atlas.SetPixels32(data);
            Atlas.Apply(false, true);
            foreach (var kv in list)
            {
                var img = kv.Value;
                var p = pos[kv.Key];
                var rect = new Rect(p.x, height - p.y - img.h, img.w, img.h);
                var s = Sprite.Create(Atlas, rect, img.pivot, img.ppu, 0, SpriteMeshType.FullRect, img.border);
                s.name = kv.Key;
                sprites[kv.Key] = s;
            }
            imgs.Clear();
        }
    }
}
