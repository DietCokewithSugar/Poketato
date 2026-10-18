using System;
using System.Collections.Generic;

namespace Poketato.Extraction
{
    public enum PokeType
    {
        Normal, Fire, Water, Electric, Grass, Ice, Fighting, Poison,
        Ground, Flying, Psychic, Bug, Rock, Ghost, Dragon
    }

    public enum Role
    {
        Tank, Bruiser, MeleeDps, RangedDps, Assassin, Controller, Support, Burst, Skirmisher
    }

    public enum StatusKind
    {
        None, Burn, Poison, Slow, Snare, Paralyze, Freeze, Sunder, Confuse, Weaken, Stun, Mark, Leech
    }

    public enum EffectShape
    {
        Melee, Projectile, Nova, Zone, Dash, Heal, Buff
    }

    public enum StatId
    {
        Atk, Spa, Def, Hp, Armor, Haste, Lifesteal, Cdr, Crit, CritDamage, Pierce,
        MoveSpeed, Regen, MagicFind, AllRes, HealBonus, BurnOnHit,
        Fire, Water, Electric, Grass, Ice, Poison, Ghost, Psychic,
        FireRes, WaterRes, ElectricRes, GrassRes, PoisonRes, IceRes
    }

    public enum Rarity { Normal, Magic, Rare, Set, Unique }

    public enum Slot { Weapon, Helm, Armor, Gloves, Boots, Belt, Amulet, Ring }

    public enum DropSource { Trash, Elite, Boss, Chest }

    public enum MissionKind { Standard, Boss, Guild, WorldWar }

    public enum RoomKind
    {
        Start, Combat, Elite, Treasure, Puzzle, Merchant, Secret,
        Boss, Extract, EarlyExtract
    }

    /// <summary>Deterministic xorshift. Same seed, same loot and crits.</summary>
    public sealed class Rng
    {
        uint state;

        public Rng(int seed)
        {
            state = (uint)seed;
            if (state == 0) state = 1;
        }

        public uint NextU()
        {
            uint x = state;
            x ^= x << 13;
            x ^= x >> 17;
            x ^= x << 5;
            state = x;
            return x;
        }

        public float NextFloat()
        {
            return (NextU() & 0xFFFFFF) / (float)0x1000000;
        }

        public int NextInt(int count)
        {
            if (count <= 1) return 0;
            return (int)(NextU() % (uint)count);
        }

        public float Range(float min, float max)
        {
            return min + (max - min) * NextFloat();
        }
    }

    public static class TypeChart
    {
        static readonly Dictionary<long, float> table = Build();

        public static string Key(PokeType type)
        {
            switch (type)
            {
                case PokeType.Normal: return "normal";
                case PokeType.Fire: return "fire";
                case PokeType.Water: return "water";
                case PokeType.Electric: return "electric";
                case PokeType.Grass: return "grass";
                case PokeType.Ice: return "ice";
                case PokeType.Fighting: return "fighting";
                case PokeType.Poison: return "poison";
                case PokeType.Ground: return "ground";
                case PokeType.Flying: return "flying";
                case PokeType.Psychic: return "psychic";
                case PokeType.Bug: return "bug";
                case PokeType.Rock: return "rock";
                case PokeType.Ghost: return "ghost";
                default: return "dragon";
            }
        }

        public static bool IsPhysical(PokeType type)
        {
            return type == PokeType.Normal || type == PokeType.Fighting || type == PokeType.Rock
                || type == PokeType.Ground || type == PokeType.Bug || type == PokeType.Flying;
        }

        public static StatusKind StatusOf(PokeType type)
        {
            switch (type)
            {
                case PokeType.Fire: return StatusKind.Burn;
                case PokeType.Water: return StatusKind.Slow;
                case PokeType.Electric: return StatusKind.Paralyze;
                case PokeType.Grass: return StatusKind.Leech;
                case PokeType.Ice: return StatusKind.Freeze;
                case PokeType.Fighting: return StatusKind.Sunder;
                case PokeType.Poison: return StatusKind.Poison;
                case PokeType.Ground: return StatusKind.Snare;
                case PokeType.Psychic: return StatusKind.Confuse;
                case PokeType.Bug: return StatusKind.Slow;
                case PokeType.Rock: return StatusKind.Stun;
                case PokeType.Ghost: return StatusKind.Weaken;
                case PokeType.Dragon: return StatusKind.Mark;
                default: return StatusKind.None;
            }
        }

        public static float One(PokeType attack, PokeType defense)
        {
            float value;
            if (table.TryGetValue(KeyOf(attack, defense), out value)) return value;
            return 1f;
        }

        public static float Effectiveness(PokeType attack, PokeType primary, PokeType? secondary)
        {
            float value = One(attack, primary);
            if (secondary.HasValue) value *= One(attack, secondary.Value);
            return value;
        }

        static long KeyOf(PokeType attack, PokeType defense)
        {
            return ((long)attack << 8) | (long)defense;
        }

        static Dictionary<long, float> Build()
        {
            var map = new Dictionary<long, float>();
            Set(map, PokeType.Normal, PokeType.Rock, 0.5f);
            Set(map, PokeType.Normal, PokeType.Ghost, 0f);
            Set(map, PokeType.Fire, PokeType.Fire, 0.5f);
            Set(map, PokeType.Fire, PokeType.Water, 0.5f);
            Set(map, PokeType.Fire, PokeType.Grass, 2f);
            Set(map, PokeType.Fire, PokeType.Ice, 2f);
            Set(map, PokeType.Fire, PokeType.Bug, 2f);
            Set(map, PokeType.Fire, PokeType.Rock, 0.5f);
            Set(map, PokeType.Fire, PokeType.Dragon, 0.5f);
            Set(map, PokeType.Water, PokeType.Fire, 2f);
            Set(map, PokeType.Water, PokeType.Water, 0.5f);
            Set(map, PokeType.Water, PokeType.Grass, 0.5f);
            Set(map, PokeType.Water, PokeType.Ground, 2f);
            Set(map, PokeType.Water, PokeType.Rock, 2f);
            Set(map, PokeType.Water, PokeType.Dragon, 0.5f);
            Set(map, PokeType.Electric, PokeType.Water, 2f);
            Set(map, PokeType.Electric, PokeType.Electric, 0.5f);
            Set(map, PokeType.Electric, PokeType.Grass, 0.5f);
            Set(map, PokeType.Electric, PokeType.Ground, 0f);
            Set(map, PokeType.Electric, PokeType.Flying, 2f);
            Set(map, PokeType.Electric, PokeType.Dragon, 0.5f);
            Set(map, PokeType.Grass, PokeType.Fire, 0.5f);
            Set(map, PokeType.Grass, PokeType.Water, 2f);
            Set(map, PokeType.Grass, PokeType.Grass, 0.5f);
            Set(map, PokeType.Grass, PokeType.Poison, 0.5f);
            Set(map, PokeType.Grass, PokeType.Ground, 2f);
            Set(map, PokeType.Grass, PokeType.Flying, 0.5f);
            Set(map, PokeType.Grass, PokeType.Bug, 0.5f);
            Set(map, PokeType.Grass, PokeType.Rock, 2f);
            Set(map, PokeType.Grass, PokeType.Dragon, 0.5f);
            Set(map, PokeType.Ice, PokeType.Fire, 0.5f);
            Set(map, PokeType.Ice, PokeType.Water, 0.5f);
            Set(map, PokeType.Ice, PokeType.Grass, 2f);
            Set(map, PokeType.Ice, PokeType.Ice, 0.5f);
            Set(map, PokeType.Ice, PokeType.Ground, 2f);
            Set(map, PokeType.Ice, PokeType.Flying, 2f);
            Set(map, PokeType.Ice, PokeType.Dragon, 2f);
            Set(map, PokeType.Fighting, PokeType.Normal, 2f);
            Set(map, PokeType.Fighting, PokeType.Ice, 2f);
            Set(map, PokeType.Fighting, PokeType.Poison, 0.5f);
            Set(map, PokeType.Fighting, PokeType.Flying, 0.5f);
            Set(map, PokeType.Fighting, PokeType.Psychic, 0.5f);
            Set(map, PokeType.Fighting, PokeType.Bug, 0.5f);
            Set(map, PokeType.Fighting, PokeType.Rock, 2f);
            Set(map, PokeType.Fighting, PokeType.Ghost, 0f);
            Set(map, PokeType.Poison, PokeType.Grass, 2f);
            Set(map, PokeType.Poison, PokeType.Poison, 0.5f);
            Set(map, PokeType.Poison, PokeType.Ground, 0.5f);
            Set(map, PokeType.Poison, PokeType.Rock, 0.5f);
            Set(map, PokeType.Poison, PokeType.Ghost, 0.5f);
            Set(map, PokeType.Poison, PokeType.Bug, 2f);
            Set(map, PokeType.Ground, PokeType.Fire, 2f);
            Set(map, PokeType.Ground, PokeType.Electric, 2f);
            Set(map, PokeType.Ground, PokeType.Grass, 0.5f);
            Set(map, PokeType.Ground, PokeType.Poison, 2f);
            Set(map, PokeType.Ground, PokeType.Flying, 0f);
            Set(map, PokeType.Ground, PokeType.Bug, 0.5f);
            Set(map, PokeType.Ground, PokeType.Rock, 2f);
            Set(map, PokeType.Flying, PokeType.Electric, 0.5f);
            Set(map, PokeType.Flying, PokeType.Grass, 2f);
            Set(map, PokeType.Flying, PokeType.Fighting, 2f);
            Set(map, PokeType.Flying, PokeType.Bug, 2f);
            Set(map, PokeType.Flying, PokeType.Rock, 0.5f);
            Set(map, PokeType.Psychic, PokeType.Fighting, 2f);
            Set(map, PokeType.Psychic, PokeType.Poison, 2f);
            Set(map, PokeType.Psychic, PokeType.Psychic, 0.5f);
            Set(map, PokeType.Bug, PokeType.Fire, 0.5f);
            Set(map, PokeType.Bug, PokeType.Grass, 2f);
            Set(map, PokeType.Bug, PokeType.Fighting, 0.5f);
            Set(map, PokeType.Bug, PokeType.Poison, 0.5f);
            Set(map, PokeType.Bug, PokeType.Flying, 0.5f);
            Set(map, PokeType.Bug, PokeType.Psychic, 2f);
            Set(map, PokeType.Bug, PokeType.Ghost, 0.5f);
            Set(map, PokeType.Rock, PokeType.Fire, 2f);
            Set(map, PokeType.Rock, PokeType.Ice, 2f);
            Set(map, PokeType.Rock, PokeType.Fighting, 0.5f);
            Set(map, PokeType.Rock, PokeType.Ground, 0.5f);
            Set(map, PokeType.Rock, PokeType.Flying, 2f);
            Set(map, PokeType.Rock, PokeType.Bug, 2f);
            Set(map, PokeType.Ghost, PokeType.Normal, 0f);
            Set(map, PokeType.Ghost, PokeType.Psychic, 2f);
            Set(map, PokeType.Ghost, PokeType.Ghost, 2f);
            Set(map, PokeType.Dragon, PokeType.Dragon, 2f);
            return map;
        }

        static void Set(Dictionary<long, float> map, PokeType attack, PokeType defense, float value)
        {
            map[KeyOf(attack, defense)] = value;
        }
    }

    public static class CombatMath
    {
        public static float LevelScale(int level)
        {
            return 1f + (Math.Max(1, level) - 1) * 0.04f;
        }

        public static float Post30(int level)
        {
            if (level <= 30) return 1f;
            return 1f + 0.02f * (level - 30);
        }

        public static float PhysicalRaw(float weaponDamage, float power, float coeff, float attack, float levelScale)
        {
            return (weaponDamage + power) * coeff * (1f + attack / 100f) * levelScale;
        }

        public static float SpecialRaw(float power, float coeff, float spa, float elementBonus, float levelScale)
        {
            return (power + spa * 0.25f) * coeff * (1f + elementBonus) * levelScale;
        }

        public static float ArmorMitigation(float armor, int attackerLevel)
        {
            float denom = armor + 12f * Math.Max(1, attackerLevel);
            if (denom <= 0.01f) return 0f;
            return armor / denom;
        }

        public static float ResistFactor(float resist, float pierce)
        {
            float value = resist - pierce;
            if (value < -0.20f) value = -0.20f;
            if (value > 0.75f) value = 0.75f;
            return 1f - value;
        }

        public static float PowerOf(int slot)
        {
            switch (slot)
            {
                case 0: return 8f;
                case 1: return 18f;
                case 3: return 14f;
                case 4: return 10f;
                case 6: return 26f;
                case 8: return 16f;
                case 9: return 34f;
                case 12: return 48f;
                case 15: return 70f;
                default: return 0f;
            }
        }

        public static float CooldownOf(int slot)
        {
            switch (slot)
            {
                case 0: return 0.55f;
                case 1: return 3.5f;
                case 3: return 7f;
                case 4: return 9f;
                case 6: return 6f;
                case 8: return 11f;
                case 9: return 10f;
                case 12: return 16f;
                case 15: return 28f;
                default: return 0f;
            }
        }
    }
}
