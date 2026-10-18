using System;
using System.Collections.Generic;

namespace Poketato.Extraction
{
    public sealed class RoomDef
    {
        public string id;
        public string nameZh;
        public RoomKind kind;
        public int gx, gz;
        public int levelBonus;
        public int count;
        public int[] enemies = new int[0];
        public int bossSpecies;
        public int guaranteedDrops;
        public bool optional;
        public bool requiresPuzzle;
        public bool puzzle;
        public string tileset;
        public string[] links = new string[0];
    }

    public sealed class MissionDef
    {
        public string id;
        public string nameZh;
        public MissionKind kind;
        public int minLevel = 1;
        public int maxLevel = 40;
        public int maxPlayers = 4;
        public int clearBonus;
        public string tileset;
        public string[] tilesetRefs = new string[0];
        public bool reflectiveWater;
        public float cr = 0.45f, cg = 0.55f, cb = 0.4f;
        public RoomDef[] rooms = new RoomDef[0];
    }

    public static class DungeonLayout
    {
        public const float RoomW = 16f;
        public const float RoomD = 12f;
        public const float Gap = 4f;
        public const float Inset = 0.8f;

        public static void Interior(RoomDef room, out float minX, out float maxX, out float minZ, out float maxZ)
        {
            float ox = room.gx * (RoomW + Gap);
            float oz = room.gz * (RoomD + Gap);
            minX = ox + Inset;
            maxX = ox + RoomW - Inset;
            minZ = oz + Inset;
            maxZ = oz + RoomD - Inset;
        }

        public static void Center(RoomDef room, out float x, out float z)
        {
            float minX, maxX, minZ, maxZ;
            Interior(room, out minX, out maxX, out minZ, out maxZ);
            x = (minX + maxX) * 0.5f;
            z = (minZ + maxZ) * 0.5f;
        }

        public static int Manhattan(RoomDef a, RoomDef b)
        {
            return Math.Abs(a.gx - b.gx) + Math.Abs(a.gz - b.gz);
        }
    }

    public static class MissionCatalog
    {
        static readonly Dictionary<string, MissionDef> byId = new Dictionary<string, MissionDef>();
        static readonly List<MissionDef> all = new List<MissionDef>();

        static MissionCatalog()
        {
            Add(Practice());
            Add(Foggy());
            Add(Beach());
            Add(Steam());
            Add(Amp());
            Add(Quicksand());
            Add(Dark());
            Add(Temporal());
            Add(Hidden());
            Add(Guild());
            Add(Desert());
        }

        public static MissionDef Get(string id) { return byId[id]; }

        public static IList<MissionDef> All { get { return all; } }

        static void Add(MissionDef mission)
        {
            byId[mission.id] = mission;
            all.Add(mission);
        }

        static RoomDef R(string id, string name, RoomKind kind, int x, int z, int bonus, int count, int[] foes, string[] links)
        {
            return new RoomDef
            {
                id = id, nameZh = name, kind = kind, gx = x, gz = z,
                levelBonus = bonus, count = count,
                enemies = foes ?? new int[0], links = links ?? new string[0]
            };
        }

        static MissionDef Practice()
        {
            var yard = R("yard", "空地", RoomKind.Combat, 1, 0, 0, 1, new[] { 19 }, new[] { "start", "exit" });
            yard.guaranteedDrops = 1;
            return new MissionDef
            {
                id = "practice", nameZh = "训练空地", kind = MissionKind.Standard,
                minLevel = 1, maxLevel = 5, maxPlayers = 4, clearBonus = 10,
                tileset = "占位草地", tilesetRefs = new[] { "placeholder" },
                cr = 0.45f, cg = 0.62f, cb = 0.32f,
                rooms = new[]
                {
                    R("start", "起点", RoomKind.Start, 0, 0, 0, 0, null, new[] { "yard" }),
                    yard,
                    R("exit", "撤离", RoomKind.Extract, 2, 0, 0, 0, null, new[] { "yard" })
                }
            };
        }

        static MissionDef Foggy()
        {
            var apple = R("apple", "苹果林", RoomKind.Treasure, 1, 1, 2, 0, null, new[] { "yard" });
            apple.optional = true;
            apple.tileset = "Apple Woods";
            var early = R("early", "林边小径", RoomKind.EarlyExtract, 2, -1, 2, 0, null, new[] { "canopy" });
            early.optional = true;
            return new MissionDef
            {
                id = "foggy-forest", nameZh = "雾林小径", kind = MissionKind.Standard,
                minLevel = 1, maxLevel = 8, maxPlayers = 4, clearBonus = 80,
                tileset = "Foggy Forest Entrance",
                tilesetRefs = new[] { "Foggy Forest Entrance", "Mystifying Forest", "Treeshroud Forest", "Apple Woods" },
                cr = 0.35f, cg = 0.48f, cb = 0.32f,
                rooms = new[]
                {
                    R("start", "入口", RoomKind.Start, 0, 0, 0, 0, null, new[] { "yard" }),
                    R("yard", "林间", RoomKind.Combat, 1, 0, 0, 4, new[] { 19, 16, 10 }, new[] { "start", "canopy", "apple" }),
                    apple,
                    R("canopy", "树冠", RoomKind.Combat, 2, 0, 2, 4, new[] { 43, 16, 21 }, new[] { "yard", "elite", "early" }),
                    early,
                    R("elite", "迷雾深处", RoomKind.Elite, 3, 0, 4, 2, new[] { 15 }, new[] { "canopy", "boss" }),
                    Boss("boss", "雾心", 4, 0, 7, 3, new[] { "elite", "exit" }),
                    R("exit", "林外", RoomKind.Extract, 5, 0, 0, 0, null, new[] { "boss" })
                }
            };
        }

        static MissionDef Beach()
        {
            var wreck = R("wreck", "沉船", RoomKind.Treasure, 1, 1, 2, 0, null, new[] { "tide" });
            wreck.optional = true;
            var early = R("early", "潮池", RoomKind.EarlyExtract, 2, -1, 2, 0, null, new[] { "falls" });
            early.optional = true;
            return new MissionDef
            {
                id = "beach-cave", nameZh = "海滩洞窟", kind = MissionKind.Standard,
                minLevel = 6, maxLevel = 14, maxPlayers = 4, clearBonus = 120,
                tileset = "Beach Cave", reflectiveWater = true,
                tilesetRefs = new[] { "Beach Cave", "waterfall cave", "Drenched Bluff" },
                cr = 0.35f, cg = 0.5f, cb = 0.55f,
                rooms = new[]
                {
                    R("start", "沙滩", RoomKind.Start, 0, 0, 0, 0, null, new[] { "tide" }),
                    R("tide", "潮间带", RoomKind.Combat, 1, 0, 1, 4, new[] { 60, 74, 129 }, new[] { "start", "falls", "wreck" }),
                    wreck,
                    R("falls", "瀑布洞", RoomKind.Combat, 2, 0, 3, 4, new[] { 60, 41, 74 }, new[] { "tide", "elite", "early" }),
                    early,
                    R("elite", "岩架", RoomKind.Elite, 3, 0, 5, 2, new[] { 95 }, new[] { "falls", "boss" }),
                    Boss("boss", "洞窟尽头", 4, 0, 8, 9, new[] { "elite", "exit" }),
                    R("exit", "退潮口", RoomKind.Extract, 5, 0, 0, 0, null, new[] { "boss" })
                }
            };
        }

        static MissionDef Steam()
        {
            var cart = R("cart", "矿车", RoomKind.Treasure, 1, 1, 3, 0, null, new[] { "vent" });
            cart.optional = true;
            return new MissionDef
            {
                id = "steam-cave", nameZh = "蒸汽洞窟", kind = MissionKind.Standard,
                minLevel = 12, maxLevel = 20, maxPlayers = 4, clearBonus = 160,
                tileset = "Steam Cave Entrance",
                tilesetRefs = new[] { "Steam Cave Entrance" },
                cr = 0.55f, cg = 0.32f, cb = 0.22f,
                rooms = new[]
                {
                    R("start", "洞口", RoomKind.Start, 0, 0, 0, 0, null, new[] { "vent" }),
                    R("vent", "热气走廊", RoomKind.Combat, 1, 0, 2, 4, new[] { 77, 109 }, new[] { "start", "lava", "cart" }),
                    cart,
                    R("lava", "熔岩平台", RoomKind.Combat, 2, 0, 4, 4, new[] { 77, 95, 109 }, new[] { "vent", "elite" }),
                    R("elite", "炉心", RoomKind.Elite, 3, 0, 6, 2, new[] { 95 }, new[] { "lava", "boss" }),
                    Boss("boss", "龙巢", 4, 0, 8, 6, new[] { "elite", "exit" }),
                    R("exit", "通风井", RoomKind.Extract, 5, 0, 0, 0, null, new[] { "boss" })
                }
            };
        }

        static MissionDef Amp()
        {
            var chest = R("cache", "避雷针", RoomKind.Treasure, 1, 1, 3, 0, null, new[] { "hill" });
            chest.optional = true;
            var early = R("early", "山脊", RoomKind.EarlyExtract, 2, -1, 3, 0, null, new[] { "tower" });
            early.optional = true;
            return new MissionDef
            {
                id = "amp-plains", nameZh = "雷鸣平原", kind = MissionKind.Standard,
                minLevel = 14, maxLevel = 22, maxPlayers = 4, clearBonus = 180,
                tileset = "Amp Plains Entrance",
                tilesetRefs = new[] { "Amp Plains Entrance", "Far Amp Plains Boss Arena" },
                cr = 0.55f, cg = 0.55f, cb = 0.28f,
                rooms = new[]
                {
                    R("start", "平原入口", RoomKind.Start, 0, 0, 0, 0, null, new[] { "hill" }),
                    R("hill", "草丘", RoomKind.Combat, 1, 0, 2, 4, new[] { 81, 100, 21 }, new[] { "start", "tower", "cache" }),
                    chest,
                    R("tower", "铁塔", RoomKind.Combat, 2, 0, 4, 4, new[] { 81, 100 }, new[] { "hill", "elite", "early" }),
                    early,
                    R("elite", "雷云", RoomKind.Elite, 3, 0, 6, 2, new[] { 100 }, new[] { "tower", "boss" }),
                    Boss("boss", "平原尽头", 4, 0, 8, 145, new[] { "elite", "exit" }),
                    R("exit", "避风处", RoomKind.Extract, 5, 0, 0, 0, null, new[] { "boss" })
                }
            };
        }

        static MissionDef Quicksand()
        {
            var well = R("well", "干涸井", RoomKind.Treasure, 1, 1, 4, 0, null, new[] { "slope" });
            well.optional = true;
            return new MissionDef
            {
                id = "quicksand-cave", nameZh = "流沙洞窟", kind = MissionKind.Standard,
                minLevel = 18, maxLevel = 26, maxPlayers = 4, clearBonus = 200,
                tileset = "Quicksand Cave Entrance",
                tilesetRefs = new[] { "Quicksand Cave Entrance", "Quicksand Cave Exterior" },
                cr = 0.62f, cg = 0.5f, cb = 0.28f,
                rooms = new[]
                {
                    R("start", "沙口", RoomKind.Start, 0, 0, 0, 0, null, new[] { "slope" }),
                    R("slope", "沙坡", RoomKind.Combat, 1, 0, 2, 4, new[] { 104, 74 }, new[] { "start", "fall", "well" }),
                    well,
                    R("fall", "塌方", RoomKind.Combat, 2, 0, 4, 4, new[] { 104, 95, 74 }, new[] { "slope", "elite" }),
                    R("elite", "沙心", RoomKind.Elite, 3, 0, 6, 1, new[] { 51 }, new[] { "fall", "boss" }),
                    Boss("boss", "巢穴", 4, 0, 8, 34, new[] { "elite", "exit" }),
                    R("exit", "上风口", RoomKind.Extract, 5, 0, 0, 0, null, new[] { "boss" })
                }
            };
        }

        static MissionDef Dark()
        {
            var seal = R("seal", "封印门", RoomKind.Puzzle, 2, 0, 3, 0, null, new[] { "tomb", "elite", "cache" });
            seal.puzzle = true;
            var cache = R("cache", "隐藏宝箱", RoomKind.Secret, 2, 1, 4, 0, null, new[] { "seal" });
            cache.optional = true;
            cache.requiresPuzzle = true;
            return new MissionDef
            {
                id = "dark-hill", nameZh = "暗夜山丘", kind = MissionKind.Standard,
                minLevel = 22, maxLevel = 30, maxPlayers = 4, clearBonus = 240,
                tileset = "Dark Hill Entrance",
                tilesetRefs = new[] { "Dark Hill Entrance", "Sealed Ruin", "Dark Crater" },
                cr = 0.22f, cg = 0.2f, cb = 0.28f,
                rooms = new[]
                {
                    R("start", "山脚", RoomKind.Start, 0, 0, 0, 0, null, new[] { "tomb" }),
                    R("tomb", "墓道", RoomKind.Combat, 1, 0, 2, 4, new[] { 92, 88, 109 }, new[] { "start", "seal" }),
                    seal,
                    cache,
                    R("elite", "夜色", RoomKind.Elite, 3, 0, 5, 2, new[] { 92 }, new[] { "seal", "boss" }),
                    Boss("boss", "山顶", 4, 0, 8, 94, new[] { "elite", "exit" }),
                    R("exit", "山门", RoomKind.Extract, 5, 0, 0, 0, null, new[] { "boss" })
                }
            };
        }

        static MissionDef Temporal()
        {
            return new MissionDef
            {
                id = "temporal-tower", nameZh = "时空之塔", kind = MissionKind.Boss,
                minLevel = 28, maxLevel = 35, maxPlayers = 8, clearBonus = 400,
                tileset = "Temporal Tower Entrance",
                tilesetRefs = new[] { "Temporal Tower Entrance", "Temporal Spire" },
                cr = 0.45f, cg = 0.4f, cb = 0.62f,
                rooms = new[]
                {
                    R("start", "大厅", RoomKind.Start, 0, 0, 0, 0, null, new[] { "west" }),
                    R("west", "左翼", RoomKind.Combat, 1, 0, 2, 4, new[] { 147, 63 }, new[] { "start", "east" }),
                    R("east", "右翼", RoomKind.Combat, 2, 0, 3, 4, new[] { 63, 147 }, new[] { "west", "boss" }),
                    Boss("boss", "塔顶", 3, 0, 7, 150, new[] { "east", "exit" }),
                    R("exit", "裂隙", RoomKind.Extract, 4, 0, 0, 0, null, new[] { "boss" })
                }
            };
        }

        static MissionDef Hidden()
        {
            var relic = R("relic", "遗迹", RoomKind.Treasure, 1, 1, 4, 0, null, new[] { "crystal" });
            relic.optional = true;
            return new MissionDef
            {
                id = "hidden-land", nameZh = "隐藏之地", kind = MissionKind.Standard,
                minLevel = 30, maxLevel = 40, maxPlayers = 4, clearBonus = 360,
                tileset = "Hidden Land Entrance",
                tilesetRefs = new[] { "Hidden Land Entrance" },
                cr = 0.55f, cg = 0.48f, cb = 0.7f,
                rooms = new[]
                {
                    R("start", "边界", RoomKind.Start, 0, 0, 0, 0, null, new[] { "crystal" }),
                    R("crystal", "水晶廊", RoomKind.Combat, 1, 0, 2, 4, new[] { 63, 147 }, new[] { "start", "aero", "relic" }),
                    relic,
                    R("aero", "化石翼龙", RoomKind.Elite, 2, 0, 4, 1, new[] { 142 }, new[] { "crystal", "dragon" }),
                    R("dragon", "快龙", RoomKind.Elite, 3, 0, 6, 1, new[] { 149 }, new[] { "aero", "boss" }),
                    Boss("boss", "核心", 4, 0, 8, 151, new[] { "dragon", "exit" }),
                    R("exit", "归途", RoomKind.Extract, 5, 0, 0, 0, null, new[] { "boss" })
                }
            };
        }

        static MissionDef Guild()
        {
            return new MissionDef
            {
                id = "guild-basement", nameZh = "公会地下", kind = MissionKind.Guild,
                minLevel = 20, maxLevel = 30, maxPlayers = 8, clearBonus = 300,
                tileset = "Guild Basement",
                tilesetRefs = new[] { "Guild Basement", "Guild Boss Arena" },
                cr = 0.42f, cg = 0.36f, cb = 0.28f,
                rooms = new[]
                {
                    R("start", "楼梯", RoomKind.Start, 0, 0, 0, 0, null, new[] { "store" }),
                    R("store", "仓库", RoomKind.Combat, 1, 0, 2, 4, new[] { 66, 19 }, new[] { "start", "dojo" }),
                    R("dojo", "演武场", RoomKind.Elite, 2, 0, 4, 3, new[] { 66 }, new[] { "store", "boss" }),
                    Boss("boss", "擂台", 3, 0, 8, 68, new[] { "dojo", "exit" }),
                    R("exit", "升降机", RoomKind.Extract, 4, 0, 0, 0, null, new[] { "boss" })
                }
            };
        }

        static MissionDef Desert()
        {
            return new MissionDef
            {
                id = "northern-desert", nameZh = "北方争霸", kind = MissionKind.WorldWar,
                minLevel = 15, maxLevel = 40, maxPlayers = 20, clearBonus = 260,
                tileset = "Northern Desert Entrance",
                tilesetRefs = new[] { "Northern Desert Entrance" },
                cr = 0.72f, cg = 0.58f, cb = 0.32f,
                rooms = new[]
                {
                    R("start", "集结", RoomKind.Start, 0, 0, 0, 0, null, new[] { "a" }),
                    R("a", "争夺点甲", RoomKind.Combat, 1, 0, 3, 5, new[] { 104, 95, 21 }, new[] { "start", "b" }),
                    R("b", "争夺点乙", RoomKind.Combat, 2, 0, 5, 5, new[] { 77, 95 }, new[] { "a", "c" }),
                    R("c", "争夺点丙", RoomKind.Elite, 3, 0, 7, 3, new[] { 130 }, new[] { "b", "boss" }),
                    Boss("boss", "沙暴中心", 4, 0, 10, 130, new[] { "c", "exit" }),
                    R("exit", "营地", RoomKind.Extract, 5, 0, 0, 0, null, new[] { "boss" })
                }
            };
        }

        static RoomDef Boss(string id, string name, int x, int z, int bonus, int species, string[] links)
        {
            var room = R(id, name, RoomKind.Boss, x, z, bonus, 1, new[] { species }, links);
            room.bossSpecies = species;
            return room;
        }
    }
}
