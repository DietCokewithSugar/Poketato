using System;
using System.Collections;
using System.Collections.Generic;

namespace Poketato.Extraction
{
    public sealed class SpeciesDef
    {
        public int dex;
        public string nameZh;
        public string nameEn;
        public PokeType primary;
        public PokeType? secondary;
        public Role role;
        public int hp, atk, def, spa, spd, spe;
        public bool playable;

        public SpeciesDef(int dex, string nameZh, string nameEn, PokeType primary, PokeType? secondary,
            Role role, int hp, int atk, int def, int spa, int spd, int spe, bool playable)
        {
            this.dex = dex;
            this.nameZh = nameZh;
            this.nameEn = nameEn;
            this.primary = primary;
            this.secondary = secondary;
            this.role = role;
            this.hp = hp;
            this.atk = atk;
            this.def = def;
            this.spa = spa;
            this.spd = spd;
            this.spe = spe;
            this.playable = playable;
        }

        public bool HasType(PokeType type, PokeType? mewExtra)
        {
            if (type == primary) return true;
            if (secondary.HasValue && type == secondary.Value) return true;
            if (dex == 151 && mewExtra.HasValue && type == mewExtra.Value) return true;
            return false;
        }
    }

    public static class SpeciesCatalog
    {
        static readonly Dictionary<int, SpeciesDef> byDex = new Dictionary<int, SpeciesDef>();
        static readonly List<SpeciesDef> playable = new List<SpeciesDef>();

        static SpeciesCatalog()
        {
            Add(3, "妙蛙花", "Venusaur", PokeType.Grass, PokeType.Poison, Role.Controller, 80, 82, 83, 100, 100, 80, true);
            Add(6, "喷火龙", "Charizard", PokeType.Fire, PokeType.Flying, Role.RangedDps, 78, 84, 78, 109, 85, 100, true);
            Add(9, "水箭龟", "Blastoise", PokeType.Water, null, Role.Tank, 79, 83, 100, 85, 105, 78, true);
            Add(25, "皮卡丘", "Pikachu", PokeType.Electric, null, Role.Skirmisher, 35, 55, 40, 50, 50, 90, true);
            Add(15, "大针蜂", "Beedrill", PokeType.Bug, PokeType.Poison, Role.Assassin, 65, 90, 40, 45, 80, 75, true);
            Add(18, "比雕", "Pidgeot", PokeType.Normal, PokeType.Flying, Role.Skirmisher, 83, 80, 75, 70, 70, 101, true);
            Add(36, "胖可丁", "Clefable", PokeType.Normal, null, Role.Support, 95, 70, 73, 95, 90, 60, true);
            Add(59, "风速狗", "Arcanine", PokeType.Fire, null, Role.Bruiser, 90, 110, 80, 100, 80, 95, true);
            Add(80, "呆壳兽", "Slowbro", PokeType.Water, PokeType.Psychic, Role.Tank, 95, 75, 110, 100, 80, 30, true);
            Add(94, "耿鬼", "Gengar", PokeType.Ghost, PokeType.Poison, Role.Assassin, 60, 65, 60, 130, 75, 110, true);
            Add(68, "怪力", "Machamp", PokeType.Fighting, null, Role.MeleeDps, 90, 130, 80, 65, 85, 55, true);
            Add(65, "胡地", "Alakazam", PokeType.Psychic, null, Role.Burst, 55, 50, 45, 135, 95, 120, true);
            Add(123, "飞天螳螂", "Scyther", PokeType.Bug, PokeType.Flying, Role.MeleeDps, 70, 110, 80, 55, 80, 105, true);
            Add(134, "水伊布", "Vaporeon", PokeType.Water, null, Role.Support, 130, 65, 60, 110, 95, 65, true);
            Add(135, "雷伊布", "Jolteon", PokeType.Electric, null, Role.Burst, 65, 65, 60, 110, 95, 130, true);
            Add(136, "火伊布", "Flareon", PokeType.Fire, null, Role.MeleeDps, 65, 130, 60, 95, 110, 65, true);
            Add(142, "化石翼龙", "Aerodactyl", PokeType.Rock, PokeType.Flying, Role.Assassin, 80, 105, 65, 60, 75, 130, true);
            Add(143, "卡比兽", "Snorlax", PokeType.Normal, null, Role.Tank, 160, 110, 65, 65, 110, 30, true);
            Add(144, "急冻鸟", "Articuno", PokeType.Ice, PokeType.Flying, Role.Controller, 90, 85, 100, 95, 125, 85, true);
            Add(145, "闪电鸟", "Zapdos", PokeType.Electric, PokeType.Flying, Role.RangedDps, 90, 90, 85, 125, 90, 100, true);
            Add(146, "火焰鸟", "Moltres", PokeType.Fire, PokeType.Flying, Role.RangedDps, 90, 100, 90, 125, 85, 90, true);
            Add(149, "快龙", "Dragonite", PokeType.Dragon, PokeType.Flying, Role.Bruiser, 91, 134, 95, 100, 100, 80, true);
            Add(150, "超梦", "Mewtwo", PokeType.Psychic, null, Role.Burst, 106, 110, 90, 154, 90, 130, true);
            Add(151, "梦幻", "Mew", PokeType.Psychic, null, Role.Support, 100, 100, 100, 100, 100, 100, true);
            Add(130, "暴鲤龙", "Gyarados", PokeType.Water, PokeType.Flying, Role.Bruiser, 95, 125, 79, 60, 100, 81, true);
            Add(131, "拉普拉斯", "Lapras", PokeType.Water, PokeType.Ice, Role.Support, 130, 85, 80, 85, 95, 60, true);
            Add(76, "隆隆岩", "Golem", PokeType.Rock, PokeType.Ground, Role.Tank, 80, 120, 130, 55, 65, 45, true);
            Add(51, "三地鼠", "Dugtrio", PokeType.Ground, null, Role.Assassin, 35, 100, 50, 50, 70, 120, true);
            Add(34, "尼多王", "Nidoking", PokeType.Poison, PokeType.Ground, Role.Bruiser, 81, 102, 77, 85, 75, 85, true);
            Add(115, "袋兽", "Kangaskhan", PokeType.Normal, null, Role.Bruiser, 105, 95, 80, 40, 80, 90, true);

            Add(19, "小拉达", "Rattata", PokeType.Normal, null, Role.MeleeDps, 30, 56, 35, 25, 35, 72, false);
            Add(16, "波波", "Pidgey", PokeType.Normal, PokeType.Flying, Role.MeleeDps, 40, 45, 40, 35, 35, 56, false);
            Add(21, "烈雀", "Spearow", PokeType.Normal, PokeType.Flying, Role.MeleeDps, 40, 60, 30, 31, 31, 70, false);
            Add(10, "绿毛虫", "Caterpie", PokeType.Bug, null, Role.MeleeDps, 45, 30, 35, 20, 20, 45, false);
            Add(43, "走路草", "Oddish", PokeType.Grass, PokeType.Poison, Role.MeleeDps, 45, 50, 55, 75, 65, 30, false);
            Add(41, "超音蝠", "Zubat", PokeType.Poison, PokeType.Flying, Role.MeleeDps, 40, 45, 35, 30, 40, 55, false);
            Add(74, "小拳石", "Geodude", PokeType.Rock, PokeType.Ground, Role.MeleeDps, 40, 80, 100, 30, 30, 20, false);
            Add(95, "大岩蛇", "Onix", PokeType.Rock, PokeType.Ground, Role.Tank, 35, 45, 160, 30, 45, 70, false);
            Add(129, "鲤鱼王", "Magikarp", PokeType.Water, null, Role.MeleeDps, 20, 10, 55, 15, 20, 80, false);
            Add(92, "鬼斯", "Gastly", PokeType.Ghost, PokeType.Poison, Role.Burst, 30, 35, 30, 100, 35, 80, false);
            Add(77, "小火马", "Ponyta", PokeType.Fire, null, Role.Skirmisher, 50, 85, 55, 65, 65, 90, false);
            Add(66, "腕力", "Machop", PokeType.Fighting, null, Role.MeleeDps, 70, 80, 50, 35, 35, 35, false);
            Add(60, "蚊香蝌蚪", "Poliwag", PokeType.Water, null, Role.Skirmisher, 40, 50, 40, 40, 40, 90, false);
            Add(81, "小磁怪", "Magnemite", PokeType.Electric, null, Role.RangedDps, 25, 35, 70, 95, 55, 45, false);
            Add(100, "霹雳电球", "Voltorb", PokeType.Electric, null, Role.Burst, 40, 30, 50, 55, 55, 100, false);
            Add(109, "瓦斯弹", "Koffing", PokeType.Poison, null, Role.Controller, 40, 65, 95, 60, 45, 35, false);
            Add(104, "卡拉卡拉", "Cubone", PokeType.Ground, null, Role.MeleeDps, 50, 50, 95, 40, 50, 35, false);
            Add(88, "臭泥", "Grimer", PokeType.Poison, null, Role.Bruiser, 80, 80, 50, 40, 50, 25, false);
            Add(63, "凯西", "Abra", PokeType.Psychic, null, Role.Burst, 25, 20, 15, 105, 55, 90, false);
            Add(147, "迷你龙", "Dratini", PokeType.Dragon, null, Role.Skirmisher, 41, 64, 45, 50, 50, 50, false);
        }

        static void Add(int dex, string zh, string en, PokeType primary, PokeType? secondary, Role role,
            int hp, int atk, int def, int spa, int spd, int spe, bool isPlayable)
        {
            var species = new SpeciesDef(dex, zh, en, primary, secondary, role, hp, atk, def, spa, spd, spe, isPlayable);
            byDex[dex] = species;
            if (isPlayable) playable.Add(species);
        }

        public static SpeciesDef Get(int dex)
        {
            SpeciesDef species;
            if (!byDex.TryGetValue(dex, out species))
                throw new ArgumentException("Unknown species " + dex);
            return species;
        }

        public static bool TryGet(int dex, out SpeciesDef species)
        {
            return byDex.TryGetValue(dex, out species);
        }

        public static IList<SpeciesDef> Playable { get { return playable; } }

        public static int PlayableCount { get { return playable.Count; } }
    }
}
