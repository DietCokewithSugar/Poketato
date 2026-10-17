using System.Collections.Generic;
using UnityEngine;

namespace Poketato
{
    public class Rng
    {
        System.Random r;
        public Rng(int seed) { r = new System.Random(seed); }
        public float Next() => (float)r.NextDouble();
        public float Range(float a, float b) => a + (b - a) * Next();
        public bool Chance(float p) => Next() < p;
        public int Int(int a, int bInclusive) => r.Next(a, bInclusive + 1);
        public T Pick<T>(IList<T> list) => list[r.Next(list.Count)];

        public string Weighted((string id, float w)[] pool)
        {
            float sum = 0;
            foreach (var p in pool) sum += p.w;
            float x = Next() * sum;
            foreach (var p in pool)
            {
                x -= p.w;
                if (x <= 0) return p.id;
            }
            return pool[pool.Length - 1].id;
        }

        public void Shuffle<T>(IList<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = r.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }

    public class SkillSlot
    {
        public SkillDef def;
        public int lv = 1;
        public float cd;
    }

    public class Derived
    {
        public float maxHp, atk, def, spa, spd, spe, physMult, specMult, physTaken, specTaken, moveSpeed, cdMult, crit, rangeMult, dodge,
            regenPerSec, lifesteal, pickupRadius, luck, xpMult, moneyMult, dmgMult, minionMult, bossMult;
    }

    public class RunStats
    {
        public int kills, crits, eliteKills;
        public float damage, taken, moneyEarned, healed;
        public readonly Dictionary<string, float> skillDmg = new();
    }

    public class SimPlayer
    {
        public LineDef line;
        public int level = 1;
        public float xp, money = 30;
        public Vector2 pos, facing = new(0, 1);
        public bool moving;
        public float hp;
        public Derived d = new();
        public Mods levelMods = new();
        public readonly Dictionary<PType, float> typeMods = new();
        public readonly Dictionary<string, int> items = new();
        public readonly List<SkillSlot> skills = new();
        public readonly List<int> levelQueue = new();
        public float invuln, shield, shieldT, buffT, buffHaste, buffSpe, regenAcc, burnT, burnAcc;
        public string field;
        public readonly RunStats stats = new();
        public object view;

        public PType[] Types => line.types;
    }

    public class Enemy
    {
        public int id;
        public EnemyDef def;
        public Vector2 pos, vel, move;
        public float hp, maxHp, dmg, speed, r, age, hover;
        public bool dead, showcase;
        public int state;
        public float t, cd, cd2;
        public Vector2 aim;
        public float burnT, burnDps, poisonT, poisonDps, paraT, slowT, slowAmt, stunT, leechT, leechDps, leechHeal, fieldBurnT, dotAcc;
        public float candy;
        public object view;
    }

    public class Proj
    {
        public int id;
        public Vector2 pos, vel;
        public float life, dmg, size, homing, knock, crit, slow, heal;
        public int pierce;
        public PType type;
        public SkillDef skill;
        public bool enemy, dead;
        public readonly HashSet<int> hit = new();
        public object view;
    }

    public class Strike
    {
        public Vector2 pos;
        public float t, delay, radius, dmg;
        public PType type;
        public SkillDef skill;
        public object view;
    }

    public class Zone
    {
        public Vector2 pos;
        public float t, radius, dmg, tick, tickT;
        public PType type;
        public SkillDef skill;
        public object view;
    }

    public enum PickupKind { Candy, Berry }

    public class Pickup
    {
        public int id;
        public Vector2 pos;
        public float value, age;
        public PickupKind kind;
        public bool magnet;
        public object view;
    }

    public class PendingSpawn
    {
        public float t;
        public string id;
        public Vector2 pos;
        public object view;
    }

    public class LevelCard
    {
        public StatCard stat;
        public float amount;
        public int tier;
        public SkillDef skill;
        public bool upgrade;
    }

    public class ShopOffer
    {
        public ItemDef item;
        public SkillDef skill;
        public int price;
        public bool sold;
    }

    /// <summary>Everything the presentation layer needs to react to; mirrors poke-rouge's sim.emit([...]) events.</summary>
    public interface ISimListener
    {
        void EnemySpawned(Enemy e);
        void EnemyRemoved(Enemy e, bool killed);
        void EnemyHit(Enemy e, float dmg, bool crit, float eff, PType type);
        void PlayerHurt(float dmg, bool dodged);
        void PlayerHealed(float amount);
        void ProjSpawned(Proj p);
        void ProjRemoved(Proj p);
        void Melee(Vector2 pos, float angle, float arc, float radius, PType type);
        void Nova(Vector2 pos, float radius, PType type);
        void Beam(Vector2 pos, float angle, float len, float width, PType type);
        void Chain(List<Vector2> points, PType type);
        void StrikeWarn(Strike s);
        void StrikeBoom(Strike s);
        void ZoneStart(Zone z);
        void ZoneEnd(Zone z);
        void ShieldUp(float amount);
        void Buff();
        void PickupSpawned(Pickup p);
        void PickupCollected(Pickup p);
        void SpawnWarn(PendingSpawn s);
        void SpawnWarnEnd(PendingSpawn s);
        void DashWarn(Enemy e, Vector2 dir, float len, float time);
        void LevelUp(int level);
        void Message(string text);
    }
}
