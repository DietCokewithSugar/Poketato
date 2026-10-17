using System.Collections.Generic;
using UnityEngine;

namespace Poketato
{
    // Gameplay data ported from poke-rouge (src/shared/*.ts). Distances, speeds and ranges keep the original
    // pixel units (16 px = 1 tile = 1 Unity unit) so balance numbers can be compared 1:1 with the web build.

    public enum PType { Normal, Fire, Water, Grass, Electric, Ice, Fighting, Poison, Ground, Flying, Psychic, Bug, Rock, Ghost, Dragon, Dark, Steel, Fairy }

    public static class Types
    {
        public static readonly string[] NameCn = { "一般", "火", "水", "草", "电", "冰", "格斗", "毒", "地面", "飞行", "超能力", "虫", "岩石", "幽灵", "龙", "恶", "钢", "妖精" };

        static readonly string[] Hex =
        {
            "#a8a878", "#f08030", "#6890f0", "#78c850", "#f8d030", "#98d8d8", "#c03028", "#a040a0", "#e0c068",
            "#a890f0", "#f85888", "#a8b820", "#b8a038", "#705898", "#7038f8", "#705848", "#b8b8d0", "#ee99ac",
        };

        public static Color ColorOf(PType t)
        {
            ColorUtility.TryParseHtmlString(Hex[(int)t], out var c);
            return c;
        }

        static readonly Dictionary<PType, (PType[] x2, PType[] x05, PType[] x0)> Chart = new()
        {
            { PType.Normal, (new PType[0], new[] { PType.Rock, PType.Steel }, new[] { PType.Ghost }) },
            { PType.Fire, (new[] { PType.Grass, PType.Ice, PType.Bug, PType.Steel }, new[] { PType.Fire, PType.Water, PType.Rock, PType.Dragon }, new PType[0]) },
            { PType.Water, (new[] { PType.Fire, PType.Ground, PType.Rock }, new[] { PType.Water, PType.Grass, PType.Dragon }, new PType[0]) },
            { PType.Electric, (new[] { PType.Water, PType.Flying }, new[] { PType.Electric, PType.Grass, PType.Dragon }, new[] { PType.Ground }) },
            { PType.Grass, (new[] { PType.Water, PType.Ground, PType.Rock }, new[] { PType.Fire, PType.Grass, PType.Poison, PType.Flying, PType.Bug, PType.Dragon, PType.Steel }, new PType[0]) },
            { PType.Ice, (new[] { PType.Grass, PType.Ground, PType.Flying, PType.Dragon }, new[] { PType.Fire, PType.Water, PType.Ice, PType.Steel }, new PType[0]) },
            { PType.Fighting, (new[] { PType.Normal, PType.Ice, PType.Rock, PType.Dark, PType.Steel }, new[] { PType.Poison, PType.Flying, PType.Psychic, PType.Bug, PType.Fairy }, new[] { PType.Ghost }) },
            { PType.Poison, (new[] { PType.Grass, PType.Fairy }, new[] { PType.Poison, PType.Ground, PType.Rock, PType.Ghost }, new[] { PType.Steel }) },
            { PType.Ground, (new[] { PType.Fire, PType.Electric, PType.Poison, PType.Rock, PType.Steel }, new[] { PType.Grass, PType.Bug }, new[] { PType.Flying }) },
            { PType.Flying, (new[] { PType.Grass, PType.Fighting, PType.Bug }, new[] { PType.Electric, PType.Rock, PType.Steel }, new PType[0]) },
            { PType.Psychic, (new[] { PType.Fighting, PType.Poison }, new[] { PType.Psychic, PType.Steel }, new[] { PType.Dark }) },
            { PType.Bug, (new[] { PType.Grass, PType.Psychic, PType.Dark }, new[] { PType.Fire, PType.Fighting, PType.Poison, PType.Flying, PType.Ghost, PType.Steel, PType.Fairy }, new PType[0]) },
            { PType.Rock, (new[] { PType.Fire, PType.Ice, PType.Flying, PType.Bug }, new[] { PType.Fighting, PType.Ground, PType.Steel }, new PType[0]) },
            { PType.Ghost, (new[] { PType.Psychic, PType.Ghost }, new[] { PType.Dark }, new[] { PType.Normal }) },
            { PType.Dragon, (new[] { PType.Dragon }, new[] { PType.Steel }, new[] { PType.Fairy }) },
            { PType.Dark, (new[] { PType.Psychic, PType.Ghost }, new[] { PType.Fighting, PType.Dark, PType.Fairy }, new PType[0]) },
            { PType.Steel, (new[] { PType.Ice, PType.Rock, PType.Fairy }, new[] { PType.Fire, PType.Water, PType.Electric, PType.Steel }, new PType[0]) },
            { PType.Fairy, (new[] { PType.Fighting, PType.Dragon, PType.Dark }, new[] { PType.Fire, PType.Poison, PType.Steel }, new PType[0]) },
        };

        static bool Has(PType[] a, PType t) => System.Array.IndexOf(a, t) >= 0;

        public static float Raw(PType atk, PType[] def)
        {
            var row = Chart[atk];
            float m = 1f;
            foreach (var t in def)
            {
                if (Has(row.x0, t)) m *= 0f;
                else if (Has(row.x2, t)) m *= 2f;
                else if (Has(row.x05, t)) m *= 0.5f;
            }
            return m;
        }

        /// <summary>poke-rouge's softened chart: super effective is only +10% (+20% double), resisted -10%, immune -20%.</summary>
        public static float Game(PType atk, PType[] def)
        {
            float m = Raw(atk, def);
            if (m == 0f) return 0.8f;
            if (m >= 4f) return 1.2f;
            if (m >= 2f) return 1.1f;
            if (m <= 0.25f) return 0.8f;
            if (m <= 0.5f) return 0.9f;
            return 1f;
        }
    }

    public struct BaseStats
    {
        public float hp, atk, def, spa, spd, spe;
        public BaseStats(float hp, float atk, float def, float spa, float spd, float spe) { this.hp = hp; this.atk = atk; this.def = def; this.spa = spa; this.spd = spd; this.spe = spe; }
    }

    public enum SkillCat { Phys, Spec, Status }
    public enum Pattern { Proj, Melee, Beam, Nova, Chain, Strike, Zone, Shield, Buff }
    public enum StatusKind { None, Burn, Poison, Para, Slow, Stun, Leech }

    public class SkillDef
    {
        public string id, name, desc;
        public PType type;
        public SkillCat cat;
        public int tier;
        public Pattern pattern;
        public float power, cd, range, speed, spread, size, homing, radius, width, arc, delay, duration, tick, knock, crit, shield, heal;
        public int count = 1, pierce, chains, strikes = 1;
        public StatusKind status;
        public float statusChance, statusDur, statusAmount;
        public float buffHaste, buffSpe, buffDur;
    }

    public class LineDef
    {
        public string id, name, flavor;
        public int dex;
        public PType[] types;
        public BaseStats stats;
        public string[] start;
        public (int lv, string skill)[] learnset;
        public int slots = 4;
    }

    public enum EnemyAI { Chase, Swarm, Shooter, EliteBeedrill }
    public enum Rank { Minion, Elite, Boss }

    public class EnemyShot
    {
        public float power, speed, cd, range, spread, slow;
        public int count = 1;
    }

    public class EnemyDef
    {
        public string id, name;
        public int dex;
        public PType[] types;
        public float hp, dmg, speed, r, candy, knockResist;
        public EnemyAI ai;
        public Rank rank;
        public bool flying;
        public EnemyShot shot;
    }

    public class WaveDef
    {
        public float duration, rate;
        public (string id, float w)[] pool;
        public string[] elites = new string[0];
    }

    public class ItemDef
    {
        public string id, name, desc;
        public int tier, price;
        public Mods mods = new Mods();
        public PType? typeBoost;
        public float typeBoostPct;
    }

    /// <summary>Additive stat modifiers (poke-rouge Mods; percentages are in points, 10 = 10%).</summary>
    public class Mods
    {
        public float maxHp, atk, def, spa, spd, spe, haste, crit, range, dodge, regen, lifesteal, pickup, luck, harvest, xp, money, dmg, dmgMinion, dmgBoss;

        public void Add(Mods o, float times = 1f)
        {
            maxHp += o.maxHp * times; atk += o.atk * times; def += o.def * times; spa += o.spa * times; spd += o.spd * times; spe += o.spe * times;
            haste += o.haste * times; crit += o.crit * times; range += o.range * times; dodge += o.dodge * times; regen += o.regen * times;
            lifesteal += o.lifesteal * times; pickup += o.pickup * times; luck += o.luck * times; harvest += o.harvest * times;
            xp += o.xp * times; money += o.money * times; dmg += o.dmg * times; dmgMinion += o.dmgMinion * times; dmgBoss += o.dmgBoss * times;
        }
    }

    public class StatCard
    {
        public string stat, name, unit;
        public float per;
        public System.Action<Mods, float> apply;
    }

    public static class GameData
    {
        public const float ArenaW = 768f, ArenaH = 576f;
        public const float PlayerR = 7f;
        public const float PxPerUnit = 16f;
        public const int SkillMaxLv = 3;
        public static readonly int[] SkillPrice = { 0, 22, 48, 88, 145 };

        static float Deg(float d) => d * Mathf.Deg2Rad;

        public static readonly Dictionary<string, SkillDef> Skills = new();
        public static readonly Dictionary<string, LineDef> Lines = new();
        public static readonly Dictionary<string, EnemyDef> Enemies = new();
        public static readonly Dictionary<string, ItemDef> Items = new();
        public static readonly List<string> LineOrder = new() { "charmander", "squirtle", "bulbasaur", "pikachu" };
        public static readonly List<WaveDef> Waves = new();
        public static readonly List<StatCard> StatCards = new();
        public static readonly List<string> ShopSkills = new() { "swift", "ember", "water_gun", "razor_leaf", "thunder_shock", "bubble_beam", "dragon_breath", "seed_bomb", "fire_fang", "spark", "electro_ball", "water_pulse" };

        static GameData()
        {
            AddSkills();
            AddLines();
            AddEnemies();
            AddWaves();
            AddItems();
            AddCards();
        }

        static void S(SkillDef s) => Skills[s.id] = s;

        static void AddSkills()
        {
            S(new SkillDef { id = "tackle", name = "撞击", type = PType.Normal, cat = SkillCat.Phys, tier = 1, pattern = Pattern.Melee, power = 11, cd = 0.8f, range = 34, radius = 34, arc = Deg(120), knock = 50, desc = "用身体撞向前方的敌人。" });
            S(new SkillDef { id = "scratch", name = "抓", type = PType.Normal, cat = SkillCat.Phys, tier = 1, pattern = Pattern.Melee, power = 10, cd = 0.7f, range = 34, radius = 34, arc = Deg(110), desc = "用利爪抓挠前方。" });
            S(new SkillDef { id = "quick_attack", name = "电光一闪", type = PType.Normal, cat = SkillCat.Phys, tier = 1, pattern = Pattern.Melee, power = 8, cd = 0.45f, range = 36, radius = 36, arc = Deg(90), desc = "迅雷不及掩耳的快速突击，冷却极短。" });
            S(new SkillDef { id = "swift", name = "高速星星", type = PType.Normal, cat = SkillCat.Spec, tier = 2, pattern = Pattern.Proj, power = 9, cd = 1.1f, range = 170, speed = 210, count = 3, spread = Deg(40), homing = 6, size = 4, desc = "发射 3 颗会追踪敌人的星星。" });
            S(new SkillDef { id = "ember", name = "火花", type = PType.Fire, cat = SkillCat.Spec, tier = 1, pattern = Pattern.Proj, power = 9, cd = 0.9f, range = 155, speed = 220, size = 3, status = StatusKind.Burn, statusChance = 0.15f, statusDur = 3, desc = "发射小火苗，有几率灼伤。" });
            S(new SkillDef { id = "fire_fang", name = "火焰牙", type = PType.Fire, cat = SkillCat.Phys, tier = 2, pattern = Pattern.Melee, power = 22, cd = 1.0f, range = 36, radius = 36, arc = Deg(100), status = StatusKind.Burn, statusChance = 0.2f, statusDur = 3, desc = "用燃烧的牙齿撕咬，可能灼伤。" });
            S(new SkillDef { id = "fire_spin", name = "火焰旋涡", type = PType.Fire, cat = SkillCat.Spec, tier = 2, pattern = Pattern.Zone, power = 5, cd = 3.0f, range = 165, radius = 30, duration = 3, tick = 0.5f, desc = "在敌人脚下卷起持续 3 秒的火焰旋涡。" });
            S(new SkillDef { id = "flamethrower", name = "喷射火焰", type = PType.Fire, cat = SkillCat.Spec, tier = 3, pattern = Pattern.Beam, power = 32, cd = 1.3f, range = 135, width = 16, status = StatusKind.Burn, statusChance = 0.25f, statusDur = 3, desc = "喷出一道火焰，贯穿直线上的所有敌人。" });
            S(new SkillDef { id = "dragon_breath", name = "龙息", type = PType.Dragon, cat = SkillCat.Spec, tier = 1, pattern = Pattern.Melee, power = 11, cd = 1.1f, range = 52, radius = 52, arc = Deg(70), status = StatusKind.Para, statusChance = 0.2f, statusDur = 2, desc = "喷出龙之气息，可能麻痹。" });
            S(new SkillDef { id = "water_gun", name = "水枪", type = PType.Water, cat = SkillCat.Spec, tier = 1, pattern = Pattern.Proj, power = 8, cd = 0.8f, range = 150, speed = 240, size = 3, knock = 60, desc = "喷射水流，能击退敌人。" });
            S(new SkillDef { id = "withdraw", name = "缩入壳中", type = PType.Water, cat = SkillCat.Status, tier = 1, pattern = Pattern.Shield, power = 0, cd = 8, range = 130, shield = 0.15f, duration = 4, desc = "缩进壳里，获得相当于 15% 最大HP 的护盾。" });
            S(new SkillDef { id = "bubble_beam", name = "泡沫光线", type = PType.Water, cat = SkillCat.Spec, tier = 2, pattern = Pattern.Proj, power = 9, cd = 1.1f, range = 150, speed = 190, count = 3, spread = Deg(30), size = 4, status = StatusKind.Slow, statusChance = 0.35f, statusDur = 1.5f, statusAmount = 0.35f, desc = "吐出 3 串泡泡，可能减速敌人。" });
            S(new SkillDef { id = "water_pulse", name = "水之波动", type = PType.Water, cat = SkillCat.Spec, tier = 2, pattern = Pattern.Proj, power = 20, cd = 1.2f, range = 160, speed = 200, size = 6, pierce = 2, status = StatusKind.Stun, statusChance = 0.2f, statusDur = 0.6f, desc = "超声波般的水球，能穿透并使敌人混乱。" });
            S(new SkillDef { id = "aqua_tail", name = "水流尾", type = PType.Water, cat = SkillCat.Phys, tier = 3, pattern = Pattern.Melee, power = 40, cd = 1.3f, range = 46, radius = 46, arc = Deg(360), knock = 80, desc = "挥动水流缠绕的尾巴，横扫一圈。" });
            S(new SkillDef { id = "vine_whip", name = "藤鞭", type = PType.Grass, cat = SkillCat.Phys, tier = 1, pattern = Pattern.Melee, power = 11, cd = 0.9f, range = 58, radius = 58, arc = Deg(50), desc = "甩出细长的藤蔓抽打远处的敌人。" });
            S(new SkillDef { id = "leech_seed", name = "寄生种子", type = PType.Grass, cat = SkillCat.Spec, tier = 1, pattern = Pattern.Proj, power = 4, cd = 2.0f, range = 150, speed = 170, size = 3, status = StatusKind.Leech, statusChance = 1, statusDur = 5, heal = 0.5f, desc = "种下种子持续吸取 HP，并为自己回复。" });
            S(new SkillDef { id = "razor_leaf", name = "飞叶快刀", type = PType.Grass, cat = SkillCat.Phys, tier = 2, pattern = Pattern.Proj, power = 12, cd = 0.9f, range = 160, speed = 240, count = 2, spread = Deg(16), pierce = 1, size = 4, crit = 15, desc = "飞出锋利的叶片，容易击中要害。" });
            S(new SkillDef { id = "poison_powder", name = "毒粉", type = PType.Poison, cat = SkillCat.Spec, tier = 2, pattern = Pattern.Zone, power = 3, cd = 3.0f, range = 160, radius = 36, duration = 3.5f, tick = 0.5f, status = StatusKind.Poison, statusChance = 0.6f, statusDur = 4, desc = "撒下毒粉云，持续伤害并使敌人中毒。" });
            S(new SkillDef { id = "seed_bomb", name = "种子炸弹", type = PType.Grass, cat = SkillCat.Phys, tier = 2, pattern = Pattern.Strike, power = 26, cd = 1.4f, range = 160, radius = 30, delay = 0.4f, desc = "投掷坚硬的种子，在落点爆炸。" });
            S(new SkillDef { id = "thunder_shock", name = "电击", type = PType.Electric, cat = SkillCat.Spec, tier = 1, pattern = Pattern.Proj, power = 7, cd = 0.6f, range = 150, speed = 300, size = 3, status = StatusKind.Para, statusChance = 0.1f, statusDur = 2, desc = "放出电流，冷却很短，可能麻痹。" });
            S(new SkillDef { id = "electro_ball", name = "电球", type = PType.Electric, cat = SkillCat.Spec, tier = 2, pattern = Pattern.Proj, power = 12, cd = 1.1f, range = 165, speed = 220, size = 6, desc = "速度越快，电球的威力越大。" });
            S(new SkillDef { id = "spark", name = "电光", type = PType.Electric, cat = SkillCat.Phys, tier = 2, pattern = Pattern.Melee, power = 20, cd = 1.0f, range = 40, radius = 40, arc = Deg(120), status = StatusKind.Para, statusChance = 0.3f, statusDur = 2, desc = "带电撞击，较高几率麻痹。" });
            S(new SkillDef { id = "agility", name = "高速移动", type = PType.Psychic, cat = SkillCat.Status, tier = 2, pattern = Pattern.Buff, power = 0, cd = 10, range = 130, buffHaste = 30, buffSpe = 30, buffDur = 4, desc = "4 秒内速度与技能急速大幅提升。" });
            S(new SkillDef { id = "thunderbolt", name = "十万伏特", type = PType.Electric, cat = SkillCat.Spec, tier = 3, pattern = Pattern.Chain, power = 30, cd = 1.3f, range = 165, chains = 4, status = StatusKind.Para, statusChance = 0.2f, statusDur = 2, desc = "强力电流在 4 个敌人之间连锁。" });
            S(new SkillDef { id = "discharge", name = "放电", type = PType.Electric, cat = SkillCat.Spec, tier = 3, pattern = Pattern.Nova, power = 30, cd = 1.6f, range = 76, radius = 78, status = StatusKind.Para, statusChance = 0.3f, statusDur = 2, desc = "向四周释放电流。" });
        }

        static void AddLines()
        {
            Lines["charmander"] = new LineDef
            {
                id = "charmander", name = "小火龙", dex = 4, types = new[] { PType.Fire }, stats = new BaseStats(39, 52, 43, 60, 50, 65),
                start = new[] { "ember", "scratch" },
                learnset = new[] { (4, "dragon_breath"), (8, "fire_fang"), (10, "fire_spin"), (12, "flamethrower") },
                flavor = "尾巴上的火焰代表生命力。偏特攻的高爆发输出手。",
            };
            Lines["squirtle"] = new LineDef
            {
                id = "squirtle", name = "杰尼龟", dex = 7, types = new[] { PType.Water }, stats = new BaseStats(44, 48, 65, 50, 64, 43),
                start = new[] { "water_gun", "tackle" },
                learnset = new[] { (4, "withdraw"), (8, "bubble_beam"), (10, "water_pulse"), (12, "aqua_tail") },
                flavor = "坚硬的甲壳能弹开攻击。高防御、带击退的稳健坦克。",
            };
            Lines["bulbasaur"] = new LineDef
            {
                id = "bulbasaur", name = "妙蛙种子", dex = 1, types = new[] { PType.Grass, PType.Poison }, stats = new BaseStats(45, 49, 49, 65, 65, 45),
                start = new[] { "vine_whip", "tackle" },
                learnset = new[] { (4, "leech_seed"), (8, "razor_leaf"), (10, "poison_powder"), (12, "seed_bomb") },
                flavor = "背上的种子吸收阳光成长。续航与范围控制兼备。",
            };
            Lines["pikachu"] = new LineDef
            {
                id = "pikachu", name = "皮卡丘", dex = 25, types = new[] { PType.Electric }, stats = new BaseStats(35, 55, 40, 50, 50, 90),
                start = new[] { "thunder_shock", "quick_attack" },
                learnset = new[] { (4, "electro_ball"), (8, "spark"), (10, "agility"), (12, "thunderbolt") },
                flavor = "速度极快但身板脆弱。技能栏多达 5 个，靠数量与麻痹取胜。", slots = 5,
            };
        }

        static void E(EnemyDef e) => Enemies[e.id] = e;

        static void AddEnemies()
        {
            E(new EnemyDef { id = "caterpie", dex = 10, name = "绿毛虫", types = new[] { PType.Bug }, hp = 8, dmg = 3, speed = 38, r = 7, ai = EnemyAI.Chase, rank = Rank.Minion, candy = 1 });
            E(new EnemyDef { id = "rattata", dex = 19, name = "小拉达", types = new[] { PType.Normal }, hp = 10, dmg = 4, speed = 62, r = 7, ai = EnemyAI.Swarm, rank = Rank.Minion, candy = 1 });
            E(new EnemyDef { id = "pidgey", dex = 16, name = "波波", types = new[] { PType.Normal, PType.Flying }, hp = 12, dmg = 4, speed = 50, r = 7, ai = EnemyAI.Chase, rank = Rank.Minion, candy = 1, flying = true });
            E(new EnemyDef { id = "zubat", dex = 41, name = "超音蝠", types = new[] { PType.Poison, PType.Flying }, hp = 8, dmg = 3, speed = 76, r = 6, ai = EnemyAI.Swarm, rank = Rank.Minion, candy = 1, flying = true });
            E(new EnemyDef
            {
                id = "oddish", dex = 43, name = "走路草", types = new[] { PType.Grass, PType.Poison }, hp = 14, dmg = 3, speed = 30, r = 7, ai = EnemyAI.Shooter, rank = Rank.Minion, candy = 1,
                shot = new EnemyShot { power = 4, speed = 95, cd = 2.6f, range = 150 },
            });
            E(new EnemyDef
            {
                id = "beedrill", dex = 15, name = "大针蜂", types = new[] { PType.Bug, PType.Poison }, hp = 210, dmg = 10, speed = 56, r = 10, ai = EnemyAI.EliteBeedrill, rank = Rank.Elite, candy = 16, flying = true, knockResist = 0.7f,
                shot = new EnemyShot { power = 7, speed = 150, cd = 2.0f, range = 170, count = 2, spread = 0.25f },
            });
            // The playable lines also exist as harmless wanderers for the benchmark's "10 species on screen" scenario.
            foreach (var l in Lines.Values)
                E(new EnemyDef { id = l.id, dex = l.dex, name = l.name, types = l.types, hp = 40, dmg = 0, speed = 52, r = 8, ai = EnemyAI.Chase, rank = Rank.Minion, candy = 0 });
        }

        static void AddWaves()
        {
            // Compressed version of poke-rouge waves 1-5 using only the MVP roster.
            Waves.Add(new WaveDef { duration = 30, rate = 0.95f, pool = new[] { ("caterpie", 3f), ("rattata", 1f) } });
            Waves.Add(new WaveDef { duration = 35, rate = 1.25f, pool = new[] { ("caterpie", 1f), ("rattata", 2f), ("pidgey", 2f), ("zubat", 1f) } });
            Waves.Add(new WaveDef { duration = 40, rate = 1.5f, pool = new[] { ("pidgey", 2f), ("zubat", 2f), ("oddish", 2f), ("rattata", 1f) }, elites = new[] { "beedrill" } });
        }

        static void I(ItemDef d) => Items[d.id] = d;

        static void AddItems()
        {
            I(new ItemDef { id = "muscle_band", name = "力量头带", tier = 1, price = 20, mods = new Mods { dmgMinion = 8 }, desc = "对小怪伤害 +8%" });
            I(new ItemDef { id = "big_charm", name = "大护符", tier = 1, price = 18, mods = new Mods { maxHp = 5 }, desc = "最大HP +5" });
            I(new ItemDef { id = "float_stone", name = "浮石", tier = 1, price = 18, mods = new Mods { spe = 8 }, desc = "速度 +8" });
            I(new ItemDef { id = "air_balloon", name = "气球", tier = 1, price = 20, mods = new Mods { dodge = 4 }, desc = "闪避 +4%" });
            I(new ItemDef { id = "exp_share", name = "学习装置", tier = 1, price = 24, mods = new Mods { xp = 15, pickup = 20 }, desc = "经验 +15%，拾取范围 +20%" });
            I(new ItemDef { id = "charcoal", name = "木炭", tier = 1, price = 20, typeBoost = PType.Fire, typeBoostPct = 12, desc = "火系技能 +12%" });
            I(new ItemDef { id = "mystic_water", name = "神秘水滴", tier = 1, price = 20, typeBoost = PType.Water, typeBoostPct = 12, desc = "水系技能 +12%" });
            I(new ItemDef { id = "miracle_seed", name = "奇迹种子", tier = 1, price = 20, typeBoost = PType.Grass, typeBoostPct = 12, desc = "草系技能 +12%" });
            I(new ItemDef { id = "magnet", name = "磁铁", tier = 1, price = 20, typeBoost = PType.Electric, typeBoostPct = 12, desc = "电系技能 +12%" });
            I(new ItemDef { id = "silk_scarf", name = "丝绸围巾", tier = 1, price = 18, typeBoost = PType.Normal, typeBoostPct = 12, desc = "一般系技能 +12%" });
            I(new ItemDef { id = "hp_up", name = "HP增强剂", tier = 1, price = 20, mods = new Mods { maxHp = 6 }, desc = "最大HP +6" });
            I(new ItemDef { id = "protein", name = "攻击增强剂", tier = 1, price = 22, mods = new Mods { atk = 6 }, desc = "物攻 +6" });
            I(new ItemDef { id = "calcium", name = "特攻增强剂", tier = 1, price = 22, mods = new Mods { spa = 6 }, desc = "特攻 +6" });
            I(new ItemDef { id = "iron", name = "防御增强剂", tier = 1, price = 20, mods = new Mods { def = 6 }, desc = "物防 +6" });
            I(new ItemDef { id = "amulet_coin", name = "护符金币", tier = 1, price = 24, mods = new Mods { money = 10 }, desc = "金钱获取 +10%" });
            I(new ItemDef { id = "quick_claw", name = "先制之爪", tier = 2, price = 45, mods = new Mods { haste = 10 }, desc = "技能急速 +10%" });
            I(new ItemDef { id = "scope_lens", name = "焦点镜", tier = 2, price = 45, mods = new Mods { crit = 6 }, desc = "暴击率 +6%" });
            I(new ItemDef { id = "leftovers", name = "吃剩的东西", tier = 2, price = 48, mods = new Mods { regen = 5, maxHp = 3 }, desc = "生命回复 +5，最大HP +3" });
            I(new ItemDef { id = "wide_lens", name = "广角镜", tier = 2, price = 42, mods = new Mods { range = 12 }, desc = "技能范围 +12%" });
            I(new ItemDef { id = "bright_powder", name = "光粉", tier = 2, price = 46, mods = new Mods { dodge = 7 }, desc = "闪避 +7%" });
        }

        static void AddCards()
        {
            void C(string stat, string name, float per, string unit, System.Action<Mods, float> apply) =>
                StatCards.Add(new StatCard { stat = stat, name = name, per = per, unit = unit, apply = apply });
            C("maxHp", "HP增强剂", 4, "最大HP", (m, v) => m.maxHp += v);
            C("atk", "攻击增强剂", 5, "物攻", (m, v) => m.atk += v);
            C("spa", "特攻增强剂", 5, "特攻", (m, v) => m.spa += v);
            C("def", "防御增强剂", 5, "物防", (m, v) => m.def += v);
            C("spd", "特防增强剂", 5, "特防", (m, v) => m.spd += v);
            C("spe", "速度增强剂", 5, "速度", (m, v) => m.spe += v);
            C("crit", "聚气", 3, "暴击率 %", (m, v) => m.crit += v);
            C("haste", "PP提升剂", 5, "技能急速 %", (m, v) => m.haste += v);
            C("dodge", "影子分身", 2, "闪避 %", (m, v) => m.dodge += v);
            C("regen", "树果汁", 2, "生命回复", (m, v) => m.regen += v);
            C("range", "锐利目光", 8, "技能范围 %", (m, v) => m.range += v);
            C("luck", "幸运草", 5, "幸运", (m, v) => m.luck += v);
        }

        public static float XpToNext(int level) => (level + 3) * (level + 3);
        public static float SkillLevelDmg(int lv) => 1f + 0.35f * (lv - 1);
        public static float SkillLevelCd(int lv) => 1f - 0.1f * (lv - 1);

        public static float EnemyHpMult(int wave)
        {
            int w = wave - 1;
            return (1f + 0.3f * w + 0.012f * w * w) * (1f + 0.1f * Mathf.Max(0, wave - 10));
        }

        public static float EnemyDmgMult(int wave)
        {
            int w = wave - 1;
            return 1f + 0.1f * w + 0.004f * w * w;
        }

        public static float PriceMult(int wave)
        {
            int w = wave - 1;
            return 1f + 0.09f * w + 0.003f * w * w;
        }

        public static float CandyMult(int wave)
        {
            float legacy = 60f + 30f * (wave - 1) / 19f;
            return legacy / Waves[Mathf.Clamp(wave - 1, 0, Waves.Count - 1)].duration / 1.1f;
        }
    }
}
