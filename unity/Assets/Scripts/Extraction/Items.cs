using System;
using System.Collections;
using System.Collections.Generic;

namespace Poketato.Extraction
{
    public sealed class ItemBase
    {
        public string id;
        public string nameZh;
        public Slot slot;
        public int reqLevel;
        public float weaponDamage;
        public float armor;
        public bool spell;
    }

    public sealed class AffixDef
    {
        public string id;
        public string nameZh;
        public bool prefix;
        public string group;
        public int minIlvl;
        public StatId stat;
        public float min;
        public float max;
    }

    public sealed class RolledAffix
    {
        public string id;
        public StatId stat;
        public float value;
    }

    public sealed class ItemInstance
    {
        public string baseId;
        public Rarity rarity;
        public int ilvl;
        public float durability = 100f;
        public float maxDurability = 100f;
        public bool foundThisRun;
        public string uniqueId;
        public string setId;
        public string nameZh;
        public List<RolledAffix> affixes = new List<RolledAffix>();
    }

    public sealed class UniqueDef
    {
        public string id;
        public string nameZh;
        public string baseId;
        public StatId statA;
        public float minA, maxA;
        public StatId statB;
        public float minB, maxB;
    }

    public sealed class SetPieceDef
    {
        public string id;
        public string setId;
        public string nameZh;
        public string baseId;
        public StatId stat;
        public float value;
    }

    public static class ItemCatalog
    {
        static readonly Dictionary<string, ItemBase> bases = new Dictionary<string, ItemBase>();
        static readonly List<ItemBase> baseList = new List<ItemBase>();
        static readonly List<AffixDef> affixes = new List<AffixDef>();
        static readonly Dictionary<string, AffixDef> affixById = new Dictionary<string, AffixDef>();
        static readonly List<UniqueDef> uniques = new List<UniqueDef>();
        static readonly List<SetPieceDef> setPieces = new List<SetPieceDef>();

        static ItemCatalog()
        {
            Base("mengya-claw", "树果短刃", Slot.Weapon, 1, 8, 0, false);
            Base("sharp-fang", "尖牙", Slot.Weapon, 10, 16, 0, false);
            Base("type-staff", "属性手杖", Slot.Weapon, 10, 14, 0, true);
            Base("shell-hammer", "重壳锤", Slot.Weapon, 20, 28, 0, false);
            Base("dragon-blade", "龙骨巨刃", Slot.Weapon, 30, 42, 0, false);
            Base("leaf-cap", "叶帽", Slot.Helm, 1, 0, 6, false);
            Base("rock-helm", "岩盔", Slot.Helm, 18, 0, 22, false);
            Base("cloth-vest", "布甲", Slot.Armor, 1, 0, 10, false);
            Base("hide-armor", "皮甲", Slot.Armor, 12, 0, 28, false);
            Base("scale-mail", "鳞甲", Slot.Armor, 24, 0, 48, false);
            Base("cloth-gloves", "布手套", Slot.Gloves, 1, 0, 4, false);
            Base("spike-gauntlets", "刺甲护手", Slot.Gloves, 16, 0, 14, false);
            Base("soft-boots", "软靴", Slot.Boots, 1, 0, 4, false);
            Base("swift-boots", "疾风靴", Slot.Boots, 14, 0, 12, false);
            Base("rope-belt", "绳带", Slot.Belt, 1, 0, 3, false);
            Base("titan-belt", "巨兽腰带", Slot.Belt, 20, 0, 16, false);
            Base("berry-charm", "树果坠", Slot.Amulet, 1, 0, 0, false);
            Base("type-orb", "属性宝珠", Slot.Amulet, 15, 0, 0, false);
            Base("copper-ring", "铜戒指", Slot.Ring, 1, 0, 0, false);
            Base("gold-ring", "金戒指", Slot.Ring, 18, 0, 0, false);

            Prefix("fierce", "猛攻", "phys", 1, StatId.Atk, 6, 18);
            Prefix("blazing", "烈焰", "fire", 4, StatId.Fire, 0.08f, 0.20f);
            Prefix("tidal", "潮汐", "water", 4, StatId.Water, 0.08f, 0.20f);
            Prefix("storm", "雷鸣", "electric", 4, StatId.Electric, 0.08f, 0.20f);
            Prefix("verdant", "森罗", "grass", 4, StatId.Grass, 0.08f, 0.20f);
            Prefix("bitter", "急冻", "ice", 8, StatId.Ice, 0.08f, 0.20f);
            Prefix("toxic", "猛毒", "poison", 8, StatId.Poison, 0.08f, 0.20f);
            Prefix("warded", "坚壁", "armor", 1, StatId.Armor, 8, 24);
            Prefix("swift", "迅捷", "speed", 6, StatId.Haste, 0.06f, 0.15f);
            Prefix("vampiric", "吸血", "leech", 12, StatId.Lifesteal, 0.03f, 0.08f);
            Prefix("scholar", "学者", "cdr", 10, StatId.Cdr, 0.04f, 0.10f);
            Prefix("hunter", "猎手", "crit", 8, StatId.Crit, 0.03f, 0.08f);
            Prefix("piercing", "穿透", "pierce", 14, StatId.Pierce, 0.06f, 0.18f);
            Prefix("giant", "巨兽", "hp", 1, StatId.Hp, 15, 45);

            Suffix("of-claw", "之爪", "atk", 1, StatId.Atk, 4, 12);
            Suffix("of-spell", "之法", "spa", 1, StatId.Spa, 4, 12);
            Suffix("of-plate", "之甲", "def", 1, StatId.Def, 4, 12);
            Suffix("of-whale", "之鲸", "hp2", 1, StatId.Hp, 20, 50);
            Suffix("of-eagle", "之鹰", "critd", 10, StatId.CritDamage, 0.10f, 0.25f);
            Suffix("of-snake", "之蛇", "pres", 6, StatId.PoisonRes, 0.10f, 0.25f);
            Suffix("of-phoenix", "之凰", "fres", 6, StatId.FireRes, 0.10f, 0.25f);
            Suffix("of-tide", "之潮", "wres", 6, StatId.WaterRes, 0.10f, 0.25f);
            Suffix("of-spark", "之雷", "eres", 6, StatId.ElectricRes, 0.10f, 0.25f);
            Suffix("of-leaf", "之叶", "gres", 6, StatId.GrassRes, 0.10f, 0.25f);
            Suffix("of-haste", "之速", "move", 4, StatId.MoveSpeed, 0.04f, 0.10f);
            Suffix("of-life", "之命", "regen", 8, StatId.Regen, 1f, 3f);
            Suffix("of-eye", "之眼", "mf", 12, StatId.MagicFind, 0.08f, 0.20f);
            Suffix("of-will", "之毅", "allres", 16, StatId.AllRes, 0.04f, 0.10f);

            Unique("char-fang", "喷火龙之牙", "sharp-fang", StatId.Atk, 20, 30, StatId.Fire, 0.25f, 0.40f);
            Unique("blastoise-cannon", "水箭龟之炮", "type-staff", StatId.Spa, 20, 30, StatId.Water, 0.25f, 0.40f);
            Unique("pika-pouch", "皮卡丘颊囊", "berry-charm", StatId.Electric, 0.20f, 0.35f, StatId.MoveSpeed, 0.08f, 0.12f);
            Unique("snorlax-belt", "卡比兽之胃", "titan-belt", StatId.Hp, 80, 120, StatId.Regen, 3, 5);
            Unique("mewtwo-band", "超梦头环", "rock-helm", StatId.Spa, 30, 45, StatId.Cdr, 0.08f, 0.12f);
            Unique("alakazam-glove", "胡地手套", "spike-gauntlets", StatId.Crit, 0.06f, 0.10f, StatId.Spa, 15, 25);
            Unique("aero-claw", "化石翼龙之爪", "mengya-claw", StatId.Haste, 0.12f, 0.20f, StatId.Crit, 0.05f, 0.08f);
            Unique("gengar-shroud", "耿鬼残影", "hide-armor", StatId.Ghost, 0.20f, 0.35f, StatId.Lifesteal, 0.04f, 0.07f);

            SetPiece("volcano-helm", "volcano", "火山角盔", "rock-helm", StatId.Armor, 8);
            SetPiece("volcano-armor", "volcano", "火山鳞甲", "scale-mail", StatId.Fire, 0.05f);
            SetPiece("volcano-fang", "volcano", "火山尖牙", "sharp-fang", StatId.Atk, 8);
            SetPiece("volcano-orb", "volcano", "火山宝珠", "type-orb", StatId.Fire, 0.05f);
            SetPiece("tide-helm", "tide", "潮汐盔", "rock-helm", StatId.Hp, 20);
            SetPiece("tide-armor", "tide", "潮汐鳞甲", "scale-mail", StatId.Water, 0.05f);
            SetPiece("tide-staff", "tide", "潮汐手杖", "type-staff", StatId.Spa, 8);
            SetPiece("tide-charm", "tide", "潮汐坠", "berry-charm", StatId.WaterRes, 0.05f);
        }

        static void Base(string id, string name, Slot slot, int req, float weapon, float armor, bool spell)
        {
            var item = new ItemBase
            {
                id = id, nameZh = name, slot = slot, reqLevel = req,
                weaponDamage = weapon, armor = armor, spell = spell
            };
            bases[id] = item;
            baseList.Add(item);
        }

        static void Prefix(string id, string name, string group, int ilvl, StatId stat, float min, float max)
        {
            AddAffix(id, name, true, group, ilvl, stat, min, max);
        }

        static void Suffix(string id, string name, string group, int ilvl, StatId stat, float min, float max)
        {
            AddAffix(id, name, false, group, ilvl, stat, min, max);
        }

        static void AddAffix(string id, string name, bool prefix, string group, int ilvl, StatId stat, float min, float max)
        {
            var affix = new AffixDef
            {
                id = id, nameZh = name, prefix = prefix, group = group,
                minIlvl = ilvl, stat = stat, min = min, max = max
            };
            affixes.Add(affix);
            affixById[id] = affix;
        }

        static void Unique(string id, string name, string baseId, StatId a, float minA, float maxA, StatId b, float minB, float maxB)
        {
            uniques.Add(new UniqueDef
            {
                id = id, nameZh = name, baseId = baseId,
                statA = a, minA = minA, maxA = maxA, statB = b, minB = minB, maxB = maxB
            });
        }

        static void SetPiece(string id, string setId, string name, string baseId, StatId stat, float value)
        {
            setPieces.Add(new SetPieceDef
            {
                id = id, setId = setId, nameZh = name, baseId = baseId, stat = stat, value = value
            });
        }

        public static ItemBase GetBase(string id) { return bases[id]; }
        public static AffixDef GetAffix(string id) { return affixById[id]; }
        public static IList<ItemBase> Bases { get { return baseList; } }
        public static IList<AffixDef> Affixes { get { return affixes; } }
        public static IList<UniqueDef> Uniques { get { return uniques; } }
        public static IList<SetPieceDef> SetPieces { get { return setPieces; } }

        public static SetPieceDef FindSetPiece(string id)
        {
            for (int i = 0; i < setPieces.Count; i++)
                if (setPieces[i].id == id) return setPieces[i];
            return null;
        }
    }

    public static class LootRoller
    {
        public static float[] RarityWeights(float magicFind, DropSource source)
        {
            float normal = 100f, magic = 45f, rare = 10f, set = 1.5f, unique = 0.6f;
            magic *= 1f + magicFind;
            rare *= 1f + magicFind * 1.4f;
            set *= 1f + magicFind;
            unique *= 1f + magicFind * 0.8f;
            if (source == DropSource.Boss)
            {
                normal = 0f; magic = 10f; rare = 50f; set = 8f; unique = 6f;
            }
            if (source == DropSource.Elite)
            {
                rare *= 2f;
                unique *= 2f;
            }
            if (source == DropSource.Chest)
            {
                normal *= 0.2f;
                rare *= 3f;
            }
            return new[] { normal, magic, rare, set, unique };
        }

        public static Rarity RollRarity(Rng rng, float magicFind, DropSource source)
        {
            var weights = RarityWeights(magicFind, source);
            float total = weights[0] + weights[1] + weights[2] + weights[3] + weights[4];
            float roll = rng.NextFloat() * total;
            float cursor = 0f;
            for (int i = 0; i < weights.Length; i++)
            {
                cursor += weights[i];
                if (roll <= cursor) return (Rarity)i;
            }
            return Rarity.Normal;
        }

        public static ItemInstance Roll(Rng rng, int ilvl, float magicFind, DropSource source)
        {
            return RollWithRarity(rng, ilvl, RollRarity(rng, magicFind, source));
        }

        public static ItemInstance RollWithRarity(Rng rng, int ilvl, Rarity rarity)
        {
            if (rarity == Rarity.Unique)
            {
                var pool = EligibleUniques(ilvl);
                if (pool.Count == 0) rarity = Rarity.Rare;
                else return MakeUnique(rng, ilvl, pool[rng.NextInt(pool.Count)]);
            }
            if (rarity == Rarity.Set)
            {
                var pool = EligibleSets(ilvl);
                if (pool.Count == 0) rarity = Rarity.Rare;
                else return MakeSet(ilvl, pool[rng.NextInt(pool.Count)]);
            }
            var item = new ItemInstance
            {
                baseId = PickBase(rng, ilvl).id,
                rarity = rarity,
                ilvl = ilvl,
                durability = 100f,
                maxDurability = 100f
            };
            if (rarity == Rarity.Magic) RollMagic(rng, ilvl, item);
            else if (rarity == Rarity.Rare) RollRare(rng, ilvl, item);
            item.nameZh = NameOf(item);
            return item;
        }

        public static ItemInstance Plain(string baseId)
        {
            var item = new ItemInstance
            {
                baseId = baseId,
                rarity = Rarity.Normal,
                ilvl = ItemCatalog.GetBase(baseId).reqLevel,
                durability = 100f,
                maxDurability = 100f,
                nameZh = ItemCatalog.GetBase(baseId).nameZh
            };
            return item;
        }

        static List<UniqueDef> EligibleUniques(int ilvl)
        {
            var list = new List<UniqueDef>();
            var all = ItemCatalog.Uniques;
            for (int i = 0; i < all.Count; i++)
                if (ItemCatalog.GetBase(all[i].baseId).reqLevel <= ilvl) list.Add(all[i]);
            return list;
        }

        static List<SetPieceDef> EligibleSets(int ilvl)
        {
            var list = new List<SetPieceDef>();
            var all = ItemCatalog.SetPieces;
            for (int i = 0; i < all.Count; i++)
                if (ItemCatalog.GetBase(all[i].baseId).reqLevel <= ilvl) list.Add(all[i]);
            return list;
        }

        static ItemInstance MakeUnique(Rng rng, int ilvl, UniqueDef unique)
        {
            var item = new ItemInstance
            {
                baseId = unique.baseId,
                rarity = Rarity.Unique,
                ilvl = ilvl,
                uniqueId = unique.id,
                nameZh = unique.nameZh,
                durability = 100f,
                maxDurability = 100f
            };
            item.affixes.Add(new RolledAffix { id = unique.id + "-a", stat = unique.statA, value = rng.Range(unique.minA, unique.maxA) });
            item.affixes.Add(new RolledAffix { id = unique.id + "-b", stat = unique.statB, value = rng.Range(unique.minB, unique.maxB) });
            return item;
        }

        static ItemInstance MakeSet(int ilvl, SetPieceDef piece)
        {
            return new ItemInstance
            {
                baseId = piece.baseId,
                rarity = Rarity.Set,
                ilvl = ilvl,
                setId = piece.setId,
                uniqueId = piece.id,
                nameZh = piece.nameZh,
                durability = 100f,
                maxDurability = 100f,
                affixes = new List<RolledAffix>
                {
                    new RolledAffix { id = piece.id, stat = piece.stat, value = piece.value }
                }
            };
        }

        static ItemBase PickBase(Rng rng, int ilvl)
        {
            var pool = new List<ItemBase>();
            var all = ItemCatalog.Bases;
            int best = 0;
            for (int i = 0; i < all.Count; i++)
                if (all[i].reqLevel <= ilvl && all[i].reqLevel >= best) best = all[i].reqLevel;
            int floor = Math.Max(1, best - 12);
            for (int i = 0; i < all.Count; i++)
                if (all[i].reqLevel <= ilvl && all[i].reqLevel >= floor) pool.Add(all[i]);
            if (pool.Count == 0) return all[0];
            return pool[rng.NextInt(pool.Count)];
        }

        static void RollMagic(Rng rng, int ilvl, ItemInstance item)
        {
            var prefix = PickAffix(rng, ilvl, true, null);
            var suffix = PickAffix(rng, ilvl, false, null);
            if (prefix != null) item.affixes.Add(RollAffix(rng, prefix));
            if (suffix != null) item.affixes.Add(RollAffix(rng, suffix));
        }

        static void RollRare(Rng rng, int ilvl, ItemInstance item)
        {
            int target = 3 + rng.NextInt(4);
            var used = new HashSet<string>();
            bool wantPrefix = true;
            int guard = 0;
            while (item.affixes.Count < target && guard++ < 40)
            {
                var pick = PickAffix(rng, ilvl, wantPrefix, used);
                if (pick == null)
                {
                    wantPrefix = !wantPrefix;
                    pick = PickAffix(rng, ilvl, wantPrefix, used);
                }
                if (pick == null) break;
                used.Add(pick.group);
                item.affixes.Add(RollAffix(rng, pick));
                wantPrefix = !wantPrefix;
            }
        }

        static AffixDef PickAffix(Rng rng, int ilvl, bool prefix, HashSet<string> used)
        {
            var pool = new List<AffixDef>();
            var all = ItemCatalog.Affixes;
            for (int i = 0; i < all.Count; i++)
            {
                var affix = all[i];
                if (affix.prefix != prefix || affix.minIlvl > ilvl) continue;
                if (used != null && used.Contains(affix.group)) continue;
                pool.Add(affix);
            }
            if (pool.Count == 0) return null;
            return pool[rng.NextInt(pool.Count)];
        }

        static RolledAffix RollAffix(Rng rng, AffixDef affix)
        {
            return new RolledAffix
            {
                id = affix.id,
                stat = affix.stat,
                value = rng.Range(affix.min, affix.max)
            };
        }

        static string NameOf(ItemInstance item)
        {
            var baseName = ItemCatalog.GetBase(item.baseId).nameZh;
            if (item.rarity == Rarity.Normal) return baseName;
            string prefix = null, suffix = null;
            for (int i = 0; i < item.affixes.Count; i++)
            {
                AffixDef def;
                try { def = ItemCatalog.GetAffix(item.affixes[i].id); }
                catch (KeyNotFoundException) { continue; }
                if (def.prefix && prefix == null) prefix = def.nameZh;
                if (!def.prefix && suffix == null) suffix = def.nameZh;
            }
            if (prefix == null) prefix = "稀有";
            if (suffix == null) suffix = "";
            return prefix + "的" + baseName + suffix;
        }
    }

    public sealed class RuntimeStats
    {
        public float maxHp = 1f;
        public float atk, def, spa, spd, spe;
        public float armor, weaponDamage, spellWeapon;
        public float crit = 0.05f;
        public float critMult = 1.5f;
        public float haste, cdr, lifesteal, pierce, magicFind, regen, healBonus, dr;
        public float statusChance, statusDuration, basicCoeff = 1f;
        public float moveSpeed = 4.2f;
        public float moveBonus;
        public bool burnOnHit;
        public PokeType auraType;
        public float aura;
        public float[] elem = new float[15];
        public float[] resist = new float[15];
    }

    public static class GearStats
    {
        public static RuntimeStats Build(SpeciesDef species, int level, TalentBuild build, IEnumerable<ItemInstance> worn, PokeType? mewExtra)
        {
            var stats = new RuntimeStats();
            float post = CombatMath.Post30(level);
            stats.maxHp = (species.hp * 1.4f + 30f) * post;
            stats.atk = species.atk;
            stats.def = species.def;
            stats.spa = species.spa;
            stats.spd = species.spd;
            stats.spe = species.spe;
            stats.armor = species.def * 0.8f;
            stats.weaponDamage = 6f + species.atk * 0.15f;
            ApplyPassives(species, mewExtra, build, stats);
            if (worn != null)
            {
                var sets = new Dictionary<string, int>();
                foreach (var item in worn)
                {
                    if (item == null || item.durability <= 0f) continue;
                    ApplyItem(stats, item);
                    if (!string.IsNullOrEmpty(item.setId))
                    {
                        int count;
                        sets.TryGetValue(item.setId, out count);
                        sets[item.setId] = count + 1;
                    }
                }
                foreach (var pair in sets) ApplySet(stats, pair.Key, pair.Value);
            }
            if (stats.cdr < 0f) stats.cdr = 0f;
            if (stats.cdr > 0.4f) stats.cdr = 0.4f;
            if (stats.dr > 0.6f) stats.dr = 0.6f;
            if (build != null)
                stats.basicCoeff = 1f + 0.15f * build.Rank(SkillTrees.Tree(species.primary)[0].id);
            stats.moveSpeed = (4.2f + stats.spe * 0.025f) * (1f + stats.moveBonus);
            return stats;
        }

        static void ApplyPassives(SpeciesDef species, PokeType? mewExtra, TalentBuild build, RuntimeStats stats)
        {
            if (build == null) return;
            ApplyTree(species.primary, build, stats);
            if (species.secondary.HasValue) ApplyTree(species.secondary.Value, build, stats);
            if (species.dex == 151 && mewExtra.HasValue) ApplyTree(mewExtra.Value, build, stats);
        }

        static void ApplyTree(PokeType type, TalentBuild build, RuntimeStats stats)
        {
            var tree = SkillTrees.Tree(type);
            stats.statusChance += 0.04f * build.Rank(tree[2].id);
            stats.haste += 0.03f * build.Rank(tree[5].id);
            stats.crit += 0.03f * build.Rank(tree[7].id);
            stats.statusDuration += 0.12f * build.Rank(tree[10].id);
            stats.armor += 8f * build.Rank(tree[11].id);
            if (build.Rank(tree[13].id) > 0)
            {
                stats.elem[(int)type] += 0.05f;
                stats.auraType = type;
                stats.aura = 0.05f;
            }
            stats.dr += 0.03f * build.Rank(tree[14].id);
        }

        static void ApplyItem(RuntimeStats stats, ItemInstance item)
        {
            var basis = ItemCatalog.GetBase(item.baseId);
            stats.armor += basis.armor;
            if (basis.spell) stats.spellWeapon += basis.weaponDamage;
            else stats.weaponDamage += basis.weaponDamage;
            for (int i = 0; i < item.affixes.Count; i++) AddStat(stats, item.affixes[i].stat, item.affixes[i].value);
        }

        static void ApplySet(RuntimeStats stats, string setId, int count)
        {
            if (setId == "volcano")
            {
                if (count >= 2) stats.elem[(int)PokeType.Fire] += 0.15f;
                if (count >= 4)
                {
                    stats.elem[(int)PokeType.Fire] += 0.15f;
                    stats.burnOnHit = true;
                }
            }
            else if (setId == "tide")
            {
                if (count >= 2)
                {
                    stats.elem[(int)PokeType.Water] += 0.15f;
                    stats.maxHp += 40f;
                }
                if (count >= 4)
                {
                    stats.resist[(int)PokeType.Water] += 0.20f;
                    stats.healBonus += 0.25f;
                }
            }
        }

        public static void AddStat(RuntimeStats stats, StatId stat, float value)
        {
            switch (stat)
            {
                case StatId.Atk: stats.atk += value; break;
                case StatId.Spa: stats.spa += value; break;
                case StatId.Def: stats.def += value; stats.armor += value * 0.8f; break;
                case StatId.Hp: stats.maxHp += value; break;
                case StatId.Armor: stats.armor += value; break;
                case StatId.Haste: stats.haste += value; break;
                case StatId.Lifesteal: stats.lifesteal += value; break;
                case StatId.Cdr: stats.cdr += value; break;
                case StatId.Crit: stats.crit += value; break;
                case StatId.CritDamage: stats.critMult += value; break;
                case StatId.Pierce: stats.pierce += value; break;
                case StatId.MoveSpeed: stats.moveBonus += value; break;
                case StatId.Regen: stats.regen += value; break;
                case StatId.MagicFind: stats.magicFind += value; break;
                case StatId.HealBonus: stats.healBonus += value; break;
                case StatId.BurnOnHit: stats.burnOnHit = value > 0f; break;
                case StatId.AllRes:
                    for (int i = 0; i < stats.resist.Length; i++) stats.resist[i] += value;
                    break;
                case StatId.Fire: stats.elem[(int)PokeType.Fire] += value; break;
                case StatId.Water: stats.elem[(int)PokeType.Water] += value; break;
                case StatId.Electric: stats.elem[(int)PokeType.Electric] += value; break;
                case StatId.Grass: stats.elem[(int)PokeType.Grass] += value; break;
                case StatId.Ice: stats.elem[(int)PokeType.Ice] += value; break;
                case StatId.Poison: stats.elem[(int)PokeType.Poison] += value; break;
                case StatId.Ghost: stats.elem[(int)PokeType.Ghost] += value; break;
                case StatId.Psychic: stats.elem[(int)PokeType.Psychic] += value; break;
                case StatId.FireRes: stats.resist[(int)PokeType.Fire] += value; break;
                case StatId.WaterRes: stats.resist[(int)PokeType.Water] += value; break;
                case StatId.ElectricRes: stats.resist[(int)PokeType.Electric] += value; break;
                case StatId.GrassRes: stats.resist[(int)PokeType.Grass] += value; break;
                case StatId.PoisonRes: stats.resist[(int)PokeType.Poison] += value; break;
                case StatId.IceRes: stats.resist[(int)PokeType.Ice] += value; break;
            }
        }

    }
}
