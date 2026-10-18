using System;
using System.Collections.Generic;

namespace Poketato.Extraction
{
    public sealed class TalentNode
    {
        public string id;
        public PokeType type;
        public int slot;
        public int tier;
        public int column;
        public string moveZh;
        public int maxRanks;
        public string prereq;
        public bool passive;
        public EffectShape shape;
        public float power;
        public float cooldown;
        public StatusKind status;
    }

    public static class SkillTrees
    {
        static readonly Dictionary<string, TalentNode> byId = new Dictionary<string, TalentNode>();
        static readonly Dictionary<PokeType, TalentNode[]> byType = new Dictionary<PokeType, TalentNode[]>();

        static readonly string[] names =
        {
            // fire
            "火花","喷射火焰","鬼火","热风","火焰旋涡","蓄能焰袭","大字爆炎","火焰踢","闪焰冲锋","爆炸烈焰","炼狱","燃尽","熔岩风暴","大晴天","过热","青焰",
            // water
            "水枪","泡沫光线","浊流","冲浪","潮旋","水流喷射","攀瀑","水之波动","水流环","热水","浸水","缩入壳中","水炮","求雨","生命水滴","加农水炮",
            // electric
            "电击","十万伏特","电磁波","放电","电网","充电","打雷","闪电牙","抛物面充电","疯狂伏特","电磁飘浮","磁场操控","电磁炮","电气场地","电球","伏特攻击",
            // grass
            "藤鞭","飞叶快刀","寄生种子","花瓣舞","麻痹粉","生长","叶刃","能量球","光合作用","强力鞭打","棉花孢子","扎根","日光束","青草场地","终极吸取","飞叶风暴",
            // ice
            "细雪","冰冻光束","冰冻之风","暴风雪","白雾","冰砾","冷冻干燥","冰锥","极光幕","绝对零度","冰雹","雪景","冰柱坠击","极光束","冰锤","冰冰霜冻",
            // fighting
            "空手劈","劈瓦","下盘踢","爆裂拳","双倍奉还","音速拳","冲天拳","真气拳","吸取拳","近身战","看穿","健美","波导弹","胜利之舞","快速防守","绝处逢生",
            // poison
            "毒针","污泥攻击","毒瓦斯","污泥炸弹","毒粉","毒击","十字毒刃","毒菱","吞下","污泥波","剧毒","溶化","毒液冲击","剧毒牙","垃圾射击","打嗝",
            // ground
            "掷泥","泥巴射击","玩沙","地震","流沙地狱","挖洞","骨棒","十万马力","重踏","大地之力","尖刺防守","耕地","直冲钻","沙暴","震级","断崖之剑",
            // flying
            "啄","翅膀攻击","羽毛舞","空气斩","吹飞","燕返","空气利刃","神鸟猛击","羽栖","勇鸟猛攻","顺风","鹦鹉学舌","自由落体","清除浓雾","起风","暴风",
            // psychic
            "念力","幻象光线","催眠术","精神强念","光墙","高速移动","精神利刃","预知未来","治愈波动","精神冲击","反射壁","冥想","精神场地","戏法空间","辅助力量","精神突进",
            // bug
            "虫咬","双针","吐丝","虫鸣","死缠烂打","连斩","十字剪","飞弹针","吸血","超级角击","黏黏网","虫之抵抗","猛扑","蝶舞","迎头一击","致命针刺",
            // rock
            "落石","岩石封锁","岩崩","滚动","沙暴","岩石打磨","原始之力","力量宝石","变圆","尖石攻击","击落","硬化","岩石爆击","隐形岩","重磅冲撞","双刃头锤",
            // ghost
            "舌舔","暗影拳","奇异之光","暗影球","祸不单行","影子偷袭","暗影爪","咒术","分担痛楚","潜灵奇袭","怨念","同命","奇异之风","黑色目光","黑夜魔影","灵骚",
            // dragon
            "龙息","龙爪","龙尾","龙之波动","龙卷风","鳞射","二连劈","龙之怒","龙之舞","逆鳞","鳞片噪音","魂舞烈音爆","巨龙威能","亚空裂斩","流星群","时光咆哮",
            // normal
            "撞击","电光一闪","叫声","泰山压顶","刺耳声","高速移动","舍身冲撞","剑舞","自我再生","破坏光线","影子分身","硬化","爆音波","神秘守护","硬撑","神速",
        };

        static readonly PokeType[] order =
        {
            PokeType.Fire, PokeType.Water, PokeType.Electric, PokeType.Grass, PokeType.Ice,
            PokeType.Fighting, PokeType.Poison, PokeType.Ground, PokeType.Flying, PokeType.Psychic,
            PokeType.Bug, PokeType.Rock, PokeType.Ghost, PokeType.Dragon, PokeType.Normal
        };

        static SkillTrees()
        {
            if (names.Length != order.Length * 16)
                throw new InvalidOperationException("Skill name table is not 15x16.");
            for (int t = 0; t < order.Length; t++)
            {
                var type = order[t];
                var nodes = new TalentNode[16];
                for (int slot = 0; slot < 16; slot++)
                {
                    int tier = slot == 15 ? 5 : slot / 3;
                    int column = slot == 15 ? 1 : slot % 3;
                    bool passive = slot == 2 || slot == 5 || slot == 7 || slot == 10 || slot == 11 || slot == 13 || slot == 14;
                    var node = new TalentNode
                    {
                        id = TypeChart.Key(type) + "-" + slot,
                        type = type,
                        slot = slot,
                        tier = tier,
                        column = column,
                        moveZh = names[t * 16 + slot],
                        maxRanks = MaxRank(slot),
                        prereq = Prereq(type, slot),
                        passive = passive,
                        shape = Shape(type, slot, passive),
                        power = CombatMath.PowerOf(slot),
                        cooldown = CombatMath.CooldownOf(slot),
                        status = passive ? StatusKind.None : TypeChart.StatusOf(type)
                    };
                    nodes[slot] = node;
                    byId[node.id] = node;
                }
                byType[type] = nodes;
            }
        }

        static int MaxRank(int slot)
        {
            if (slot == 0 || slot == 2 || slot == 5 || slot == 11) return 5;
            if (slot == 7 || slot == 10 || slot == 14) return 3;
            return 1;
        }

        static string Prereq(PokeType type, int slot)
        {
            int required = -1;
            if (slot == 6) required = 1;
            else if (slot == 9) required = 6;
            else if (slot == 10) required = 4;
            else if (slot == 12) required = 9;
            else if (slot == 15) required = 12;
            if (required < 0) return null;
            return TypeChart.Key(type) + "-" + required;
        }

        static EffectShape Shape(PokeType type, int slot, bool passive)
        {
            if (passive) return EffectShape.Buff;
            if (slot == 8) return Sustain(type);
            if (slot == 3 || slot == 9 || slot == 12 || slot == 15) return EffectShape.Nova;
            if (slot == 4) return EffectShape.Zone;
            return TypeChart.IsPhysical(type) ? EffectShape.Melee : EffectShape.Projectile;
        }

        static EffectShape Sustain(PokeType type)
        {
            if (type == PokeType.Ice || type == PokeType.Dragon || type == PokeType.Rock) return EffectShape.Buff;
            if (type == PokeType.Fire || type == PokeType.Fighting || type == PokeType.Ground) return EffectShape.Dash;
            return EffectShape.Heal;
        }

        public static TalentNode Get(string id)
        {
            return byId[id];
        }

        public static bool TryGet(string id, out TalentNode node)
        {
            return byId.TryGetValue(id, out node);
        }

        public static TalentNode[] Tree(PokeType type)
        {
            return byType[type];
        }

        public static IEnumerable<PokeType> Types { get { return order; } }

        public static int TypeCount { get { return order.Length; } }

        public static float CastRange(TalentNode node)
        {
            switch (node.shape)
            {
                case EffectShape.Melee: return 2.4f;
                case EffectShape.Projectile: return 9f;
                case EffectShape.Zone: return 8f;
                case EffectShape.Dash: return 6f;
                case EffectShape.Heal:
                case EffectShape.Buff: return 99f;
                default:
                    if (node.slot == 15) return 5.8f;
                    if (node.slot == 12) return 4.6f;
                    return 3.4f;
            }
        }
    }
}
