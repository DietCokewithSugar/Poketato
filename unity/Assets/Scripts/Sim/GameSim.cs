using System.Collections.Generic;
using UnityEngine;

namespace Poketato
{
    public enum SimPhase { Idle, Wave, Inter, Over, Won }

    /// <summary>
    /// Single-player port of poke-rouge's GameSim (waves, spawning, enemy AI, auto-cast skills, damage, candies,
    /// level-up cards and shop). Pure C#: no scene objects, so it can later run headless on a server.
    /// </summary>
    public class GameSim
    {
        public readonly Rng rng;
        public ISimListener fx;
        public SimPlayer player;
        public readonly List<Enemy> enemies = new();
        public readonly List<Proj> projs = new();
        public readonly List<Strike> strikes = new();
        public readonly List<Zone> zones = new();
        public readonly List<Pickup> pickups = new();
        public readonly List<PendingSpawn> pending = new();

        public SimPhase phase = SimPhase.Idle;
        public int wave;
        public float waveT, waveDur;
        public float elapsed;

        // Benchmark controls: keep a fixed number of enemies alive, never end the wave, never lose.
        public bool benchMode;
        public int benchTarget;
        public bool godMode;
        public bool showcaseOnly;

        float spawnAcc;
        int elitesDone;
        int nextId = 1;
        const float Cell = 32f;
        readonly Dictionary<int, List<Enemy>> grid = new();
        readonly List<Enemy> tmpEnemies = new();
        readonly List<Vector2> tmpPoints = new();

        public List<LevelCard> cards;
        public List<ShopOffer> shop = new();
        public int shopRerolls;

        public GameSim(int seed) { rng = new Rng(seed); }

        // ------------------------------------------------------------------ run / waves

        public void StartRun(string lineId)
        {
            var L = GameData.Lines[lineId];
            player = new SimPlayer { line = L, pos = new Vector2(GameData.ArenaW / 2, GameData.ArenaH / 2) };
            for (int i = 0; i < L.start.Length; i++)
                player.skills.Add(new SkillSlot { def = GameData.Skills[L.start[i]], cd = 0.5f + i * 0.2f });
            Recompute();
            player.hp = player.d.maxHp;
            wave = 0;
            phase = SimPhase.Inter;
        }

        public void StartWave(int w)
        {
            wave = w;
            var W = GameData.Waves[Mathf.Clamp(w - 1, 0, GameData.Waves.Count - 1)];
            waveDur = W.duration;
            waveT = 0;
            spawnAcc = 0;
            elitesDone = 0;
            phase = SimPhase.Wave;
            player.hp = player.d.maxHp;
            player.invuln = 1.5f;
            player.pos = new Vector2(GameData.ArenaW / 2, GameData.ArenaH / 2);
            cards = null;
            fx?.Message(benchMode ? "性能测试" : $"第 {w} 波");
        }

        void EndWave()
        {
            for (int i = enemies.Count - 1; i >= 0; i--) RemoveEnemy(i, false);
            for (int i = projs.Count - 1; i >= 0; i--) { fx?.ProjRemoved(projs[i]); }
            projs.Clear();
            foreach (var s in pending) fx?.SpawnWarnEnd(s);
            pending.Clear();
            foreach (var z in zones) fx?.ZoneEnd(z);
            zones.Clear();
            strikes.Clear();
            // Uncollected candies fly to the player at wave end, like the web version.
            for (int i = pickups.Count - 1; i >= 0; i--)
                if (pickups[i].kind == PickupKind.Candy) Collect(i);
            if (wave >= GameData.Waves.Count) { phase = SimPhase.Won; fx?.Message("通关！"); return; }
            phase = SimPhase.Inter;
            fx?.Message("波次完成！");
        }

        public void Step(float dt, Vector2 input)
        {
            if (phase != SimPhase.Wave) return;
            elapsed += dt;
            waveT += dt;
            UpdatePlayer(dt, input);
            UpdateSpawns(dt);
            UpdateSkills(dt);
            UpdateEnemies(dt);
            UpdateProjs(dt);
            UpdateStrikes(dt);
            UpdateZones(dt);
            UpdatePickups(dt);
            if (!benchMode && waveT >= waveDur) EndWave();
            if (!godMode && player.hp <= 0 && phase == SimPhase.Wave)
            {
                phase = SimPhase.Over;
                fx?.Message("倒下了……");
            }
        }

        // ------------------------------------------------------------------ player

        public void Recompute()
        {
            var p = player;
            var m = new Mods();
            m.Add(p.levelMods);
            p.typeMods.Clear();
            foreach (var kv in p.items)
            {
                var it = GameData.Items[kv.Key];
                m.Add(it.mods, kv.Value);
                if (it.typeBoost.HasValue)
                {
                    p.typeMods.TryGetValue(it.typeBoost.Value, out var cur);
                    p.typeMods[it.typeBoost.Value] = cur + it.typeBoostPct * kv.Value;
                }
            }
            if (p.buffT > 0) { m.haste += p.buffHaste; m.spe += p.buffSpe; }
            var b = p.line.stats;
            float Pct(float v) => Mathf.Max(1f, v);
            var d = p.d;
            float oldMax = d.maxHp;
            d.atk = Pct(b.atk + m.atk); d.spa = Pct(b.spa + m.spa); d.def = Pct(b.def + m.def); d.spd = Pct(b.spd + m.spd); d.spe = Pct(b.spe + m.spe);
            d.maxHp = Mathf.Max(1, Mathf.Round(b.hp * 0.6f + 10 + m.maxHp));
            d.physMult = d.atk / 50f;
            d.specMult = d.spa / 50f;
            d.physTaken = 100f / (50f + d.def);
            d.specTaken = 100f / (50f + d.spd);
            d.moveSpeed = Mathf.Clamp(80f + d.spe * 0.5f, 50f, 220f);
            d.cdMult = m.haste >= 0 ? Mathf.Max(0.5f, 1f / (1f + m.haste / 100f)) : 1f - m.haste / 100f;
            d.crit = Mathf.Clamp01(0.05f + m.crit / 100f);
            d.rangeMult = Mathf.Clamp(1f + m.range / 100f, 0.5f, 3f);
            d.dodge = Mathf.Clamp(m.dodge / 100f, 0f, 0.6f);
            d.regenPerSec = Mathf.Max(0, m.regen) / 10f;
            d.lifesteal = Mathf.Clamp01(m.lifesteal / 100f);
            d.pickupRadius = 42f * Mathf.Clamp(1f + m.pickup / 100f, 0.5f, 5f);
            d.luck = m.luck;
            d.xpMult = Mathf.Max(0, 1f + m.xp / 100f);
            d.moneyMult = Mathf.Max(0, 1f + m.money / 100f);
            d.dmgMult = Mathf.Max(0.1f, 1f + m.dmg / 100f);
            d.minionMult = Mathf.Max(0.1f, 1f + m.dmgMinion / 100f);
            d.bossMult = Mathf.Max(0.1f, 1f + m.dmgBoss / 100f);
            if (oldMax > 0 && d.maxHp > oldMax) p.hp += d.maxHp - oldMax;
            p.hp = Mathf.Min(p.hp, d.maxHp);
        }

        void UpdatePlayer(float dt, Vector2 input)
        {
            var p = player;
            if (p.invuln > 0) p.invuln -= dt;
            if (p.shieldT > 0 && (p.shieldT -= dt) <= 0) p.shield = 0;
            if (p.buffT > 0 && (p.buffT -= dt) <= 0) Recompute();
            p.field = Fields.At(p.pos)?.name;
            var fieldDef = Fields.At(p.pos);
            float speed = p.d.moveSpeed;
            if (fieldDef != null && fieldDef.kind == FieldKind.Rain && System.Array.IndexOf(p.Types, PType.Flying) >= 0) speed *= 0.75f;
            if (input.sqrMagnitude > 1f) input.Normalize();
            p.moving = input.sqrMagnitude > 0.01f;
            if (p.moving) p.facing = input.normalized;
            p.pos += input * speed * dt;
            p.pos.x = Mathf.Clamp(p.pos.x, 12, GameData.ArenaW - 12);
            p.pos.y = Mathf.Clamp(p.pos.y, 12, GameData.ArenaH - 12);
            if (p.d.regenPerSec > 0 && p.hp < p.d.maxHp) p.regenAcc += p.d.regenPerSec * dt;
            if (p.regenAcc >= 1f) { float h = Mathf.Floor(p.regenAcc); p.regenAcc -= h; Heal(h); }
            // Lava burns non-fire/rock Pokemon: 3% max HP per second, ignores defence.
            bool lavaImmune = System.Array.IndexOf(p.Types, PType.Fire) >= 0 || System.Array.IndexOf(p.Types, PType.Rock) >= 0;
            if (fieldDef != null && fieldDef.kind == FieldKind.Lava && !lavaImmune) p.burnT = 2f;
            if (p.burnT > 0)
            {
                p.burnT -= dt;
                p.burnAcc += p.d.maxHp * 0.03f * dt;
                if (p.burnAcc >= 1f) { float h = Mathf.Floor(p.burnAcc); p.burnAcc -= h; HurtPlayer(h, SkillCat.Spec, true); }
            }
        }

        public void Heal(float amount)
        {
            var p = player;
            float before = p.hp;
            p.hp = Mathf.Min(p.d.maxHp, p.hp + amount);
            if (p.hp > before) { p.stats.healed += p.hp - before; fx?.PlayerHealed(p.hp - before); }
        }

        public void HurtPlayer(float raw, SkillCat cat, bool pure = false)
        {
            var p = player;
            if (raw <= 0 || (!pure && p.invuln > 0)) return;
            if (!pure && rng.Chance(p.d.dodge)) { p.invuln = 0.2f; fx?.PlayerHurt(0, true); return; }
            float dmg = pure ? raw : raw * (cat == SkillCat.Phys ? p.d.physTaken : p.d.specTaken);
            dmg = Mathf.Max(1, Mathf.Round(dmg));
            if (p.shield > 0)
            {
                float a = Mathf.Min(p.shield, dmg);
                p.shield -= a;
                dmg -= a;
            }
            if (!pure) p.invuln = 0.45f;
            if (dmg <= 0) { fx?.PlayerHurt(0, false); return; }
            if (godMode) dmg = 0;
            p.hp -= dmg;
            p.stats.taken += dmg;
            fx?.PlayerHurt(dmg, false);
        }

        void AddXp(float amount)
        {
            var p = player;
            p.xp += amount;
            while (p.xp >= GameData.XpToNext(p.level))
            {
                p.xp -= GameData.XpToNext(p.level);
                p.level++;
                p.levelQueue.Add(p.level);
                Recompute();
                p.hp = Mathf.Min(p.d.maxHp, p.hp + 2);
                fx?.LevelUp(p.level);
            }
        }

        // ------------------------------------------------------------------ spawning

        void UpdateSpawns(float dt)
        {
            for (int i = pending.Count - 1; i >= 0; i--)
            {
                var s = pending[i];
                s.t -= dt;
                if (s.t > 0) continue;
                pending.RemoveAt(i);
                fx?.SpawnWarnEnd(s);
                SpawnEnemy(s.id, s.pos);
            }
            if (benchMode)
            {
                if (showcaseOnly) return;
                int missing = benchTarget - enemies.Count - pending.Count;
                for (int i = 0; i < missing; i++)
                {
                    string[] ids = { "caterpie", "rattata", "pidgey", "zubat", "oddish", "beedrill" };
                    string id = rng.Chance(0.03f) ? "beedrill" : ids[rng.Int(0, 4)];
                    QueueSpawn(id, SpawnPoint(110), 0.6f);
                }
                return;
            }
            var W = GameData.Waves[wave - 1];
            if (elitesDone < W.elites.Length)
            {
                float at = waveDur * (0.2f + 0.6f * elitesDone / Mathf.Max(1, W.elites.Length));
                if (waveT >= at)
                {
                    var id = W.elites[elitesDone++];
                    fx?.Message($"{GameData.Enemies[id].name}出现了！");
                    QueueSpawn(id, SpawnPoint(140), 1.2f);
                }
            }
            if (waveT > waveDur - 1.5f) return;
            float progress = waveT / waveDur;
            float rate = W.rate * (0.7f + 0.6f * progress);
            spawnAcc += rate * dt;
            const int maxAlive = 100;
            while (spawnAcc >= 1f)
            {
                if (enemies.Count + pending.Count >= maxAlive) { spawnAcc = 0; break; }
                var id = rng.Weighted(W.pool);
                var def = GameData.Enemies[id];
                bool big = def.hp >= 40 || def.ai == EnemyAI.Shooter;
                int g = Mathf.Max(1, Mathf.Min(Mathf.FloorToInt(spawnAcc), big ? rng.Int(1, 2) : rng.Int(1, 2 + wave / 4)));
                var c = SpawnPoint(100);
                for (int i = 0; i < g; i++)
                {
                    float a = rng.Range(0, Mathf.PI * 2), r = i == 0 ? 0 : rng.Range(10, 22);
                    QueueSpawn(id, c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r, 1f);
                }
                spawnAcc -= g;
            }
        }

        Vector2 SpawnPoint(float minDist)
        {
            for (int k = 0; k < 20; k++)
            {
                var p = new Vector2(rng.Range(24, GameData.ArenaW - 24), rng.Range(24, GameData.ArenaH - 24));
                if ((p - player.pos).sqrMagnitude > minDist * minDist) return p;
            }
            return new Vector2(rng.Range(24, GameData.ArenaW - 24), 24);
        }

        public void QueueSpawn(string id, Vector2 pos, float delay)
        {
            var s = new PendingSpawn { id = id, pos = pos, t = delay };
            pending.Add(s);
            fx?.SpawnWarn(s);
        }

        public Enemy SpawnEnemy(string id, Vector2 pos, bool showcase = false)
        {
            var def = GameData.Enemies[id];
            int w = Mathf.Max(1, wave);
            var e = new Enemy
            {
                id = nextId++, def = def, pos = pos, r = def.r, showcase = showcase,
                hp = def.hp * GameData.EnemyHpMult(w), dmg = def.dmg * GameData.EnemyDmgMult(w),
                speed = def.speed * (1f + Mathf.Min(0.25f, w * 0.012f)), candy = def.candy,
                cd = rng.Range(0.5f, 1.5f), cd2 = 2f,
            };
            e.maxHp = e.hp;
            enemies.Add(e);
            fx?.EnemySpawned(e);
            return e;
        }

        // ------------------------------------------------------------------ enemies

        static float AngDiff(float a, float b)
        {
            float d = Mathf.Repeat(a - b + Mathf.PI, Mathf.PI * 2) - Mathf.PI;
            return Mathf.Abs(d);
        }

        int GridKey(Vector2 p) => Mathf.FloorToInt(p.x / Cell) + Mathf.FloorToInt(p.y / Cell) * 1000;

        void UpdateEnemies(float dt)
        {
            var p = player;
            foreach (var l in grid.Values) l.Clear();
            for (int i = enemies.Count - 1; i >= 0; i--)
            {
                var e = enemies[i];
                e.age += dt;
                if (e.hp <= 0) { RemoveEnemy(i, true); continue; }
                Vector2 to = p.pos - e.pos;
                float d = to.magnitude;
                Vector2 u = d > 0.001f ? to / d : Vector2.zero;
                Vector2 mv = Vector2.zero;
                float speedMul = 1f;
                if (e.stunT > 0) { e.stunT -= dt; speedMul = 0; }
                if (e.paraT > 0) { e.paraT -= dt; speedMul *= 0.5f; }
                if (e.slowT > 0) { e.slowT -= dt; speedMul *= 1f - e.slowAmt; }
                if (e.cd > 0) e.cd -= dt;
                if (e.cd2 > 0) e.cd2 -= dt;

                if (e.showcase)
                {
                    // Bench "roster" scenario: wander around the player without hurting it.
                    float a = e.age * 0.6f + e.id * 1.7f;
                    Vector2 goal = p.pos + new Vector2(Mathf.Cos(a), Mathf.Sin(a) * 0.6f) * (70 + (e.id % 3) * 26);
                    Vector2 g = goal - e.pos;
                    mv = g.magnitude > 4 ? g.normalized : Vector2.zero;
                }
                else switch (e.def.ai)
                {
                    case EnemyAI.Chase:
                        mv = u;
                        break;
                    case EnemyAI.Swarm:
                    {
                        float wob = Mathf.Sin(e.age * 4 + e.id) * 0.7f;
                        float a = Mathf.Atan2(u.y, u.x) + wob;
                        mv = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                        break;
                    }
                    case EnemyAI.Shooter:
                    {
                        var shot = e.def.shot;
                        float want = shot.range * 0.75f;
                        float side = e.id % 2 == 1 ? 1 : -1;
                        if (d > want + 15) mv = u;
                        else if (d < want - 35) mv = -u;
                        else mv = new Vector2(-u.y, u.x) * 0.35f * side;
                        if (e.cd <= 0 && d < shot.range)
                        {
                            e.cd = shot.cd * rng.Range(0.85f, 1.2f);
                            EnemyShoot(e, Mathf.Atan2(to.y, to.x));
                        }
                        break;
                    }
                    case EnemyAI.EliteBeedrill:
                        mv = Beedrill(e, u, d, dt);
                        break;
                }
                e.move = mv;
                e.pos += mv * e.speed * speedMul * dt + e.vel * dt;
                e.vel *= Mathf.Exp(-8f * dt);
                e.pos.x = Mathf.Clamp(e.pos.x, 8, GameData.ArenaW - 8);
                e.pos.y = Mathf.Clamp(e.pos.y, 8, GameData.ArenaH - 8);
                UpdateEnemyStatus(e, dt);
                if (e.hp <= 0) { RemoveEnemy(i, true); continue; }

                if (!e.showcase && e.dmg > 0 && p.invuln <= 0)
                {
                    float rr = e.r + GameData.PlayerR - 1;
                    if ((p.pos - e.pos).sqrMagnitude < rr * rr) HurtPlayer(e.dmg, SkillCat.Phys);
                }
                int key = GridKey(e.pos);
                if (!grid.TryGetValue(key, out var cell)) grid[key] = cell = new List<Enemy>();
                cell.Add(e);
            }
            Separate(dt);
        }

        Vector2 Beedrill(Enemy e, Vector2 u, float d, float dt)
        {
            if (e.state == 0)
            {
                if (e.cd <= 0) { e.cd = 2.0f; EnemyShoot(e, Mathf.Atan2(u.y, u.x)); }
                if (e.cd2 <= 0 && d < 170)
                {
                    e.state = 1; e.t = 0.55f; e.aim = u;
                    fx?.DashWarn(e, u, 170, 0.55f);
                }
                return u;
            }
            e.t -= dt;
            if (e.state == 1)
            {
                if (e.t <= 0) { e.state = 2; e.t = 0.4f; }
                return Vector2.zero;
            }
            if (e.t <= 0) { e.state = 0; e.cd2 = 3.2f; }
            return e.aim * 5.5f;
        }

        void UpdateEnemyStatus(Enemy e, float dt)
        {
            var f = Fields.At(e.pos);
            bool lavaImmune = System.Array.IndexOf(e.def.types, PType.Fire) >= 0 || System.Array.IndexOf(e.def.types, PType.Rock) >= 0;
            if (f != null && f.kind == FieldKind.Lava && !lavaImmune) e.fieldBurnT = 2f;
            float dps = 0;
            if (e.burnT > 0) { e.burnT -= dt; dps += e.burnDps; }
            if (e.poisonT > 0) { e.poisonT -= dt; dps += e.poisonDps; }
            if (e.leechT > 0)
            {
                e.leechT -= dt;
                dps += e.leechDps;
                if (e.leechHeal > 0) player.regenAcc += e.leechDps * e.leechHeal * dt;
            }
            if (e.fieldBurnT > 0)
            {
                e.fieldBurnT -= dt;
                dps += e.maxHp * 0.03f * (e.def.rank == Rank.Minion ? 1f : 1f / 3f);
            }
            if (dps <= 0) return;
            e.dotAcc += dps * dt;
            if (e.dotAcc >= 1f)
            {
                float dmg = Mathf.Floor(e.dotAcc);
                e.dotAcc -= dmg;
                if (!e.showcase) e.hp -= dmg;
                player.stats.damage += dmg;
                fx?.EnemyHit(e, dmg, false, 1f, PType.Fire);
            }
        }

        void Separate(float dt)
        {
            foreach (var e in enemies)
            {
                int cx = Mathf.FloorToInt(e.pos.x / Cell), cy = Mathf.FloorToInt(e.pos.y / Cell);
                Vector2 push = Vector2.zero;
                for (int oy = -1; oy <= 1; oy++)
                for (int ox = -1; ox <= 1; ox++)
                {
                    if (!grid.TryGetValue(cx + ox + (cy + oy) * 1000, out var cell)) continue;
                    foreach (var o in cell)
                    {
                        if (o == e) continue;
                        Vector2 dv = e.pos - o.pos;
                        float rr = e.r + o.r;
                        float d2 = dv.sqrMagnitude;
                        if (d2 >= rr * rr || d2 < 0.0001f) continue;
                        float dd = Mathf.Sqrt(d2);
                        push += dv / dd * (rr - dd);
                    }
                }
                e.pos += push * Mathf.Min(1f, 6f * dt);
            }
        }

        void EnemyShoot(Enemy e, float angle)
        {
            var s = e.def.shot;
            int n = s.count;
            for (int i = 0; i < n; i++)
            {
                float a = n == 1 ? angle : angle - s.spread / 2 + s.spread * i / (n - 1);
                var pr = new Proj
                {
                    id = nextId++, enemy = true, pos = e.pos, vel = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * s.speed,
                    life = s.range / s.speed * 1.4f, dmg = s.power * GameData.EnemyDmgMult(Mathf.Max(1, wave)), size = 4, type = e.def.types[0],
                    slow = s.slow,
                };
                projs.Add(pr);
                fx?.ProjSpawned(pr);
            }
        }

        void RemoveEnemy(int index, bool killed)
        {
            var e = enemies[index];
            enemies.RemoveAt(index);
            e.dead = true;
            if (killed && !e.showcase)
            {
                player.stats.kills++;
                if (e.def.rank != Rank.Minion) player.stats.eliteKills++;
                DropCandy(e.pos, e.candy * GameData.CandyMult(Mathf.Max(1, wave)));
                if (rng.Chance(0.012f * (1 + Mathf.Max(0, player.d.luck) / 100f)))
                    AddPickup(PickupKind.Berry, e.pos + new Vector2(rng.Range(-6, 6), rng.Range(-6, 6)), 1);
            }
            fx?.EnemyRemoved(e, killed);
        }

        // ------------------------------------------------------------------ skills

        Enemy Nearest(Vector2 from, float range)
        {
            Enemy best = null;
            float bd = range * range;
            foreach (var e in enemies)
            {
                if (e.hp <= 0) continue;
                float d = (e.pos - from).sqrMagnitude;
                if (d < bd) { bd = d; best = e; }
            }
            return best;
        }

        float BaseDamage(SkillSlot slot)
        {
            var s = slot.def;
            var p = player;
            float stat = s.cat == SkillCat.Phys ? p.d.physMult : p.d.specMult;
            float stab = System.Array.IndexOf(p.Types, s.type) >= 0 ? 1.5f : 1f;
            p.typeMods.TryGetValue(s.type, out var tb);
            float dmg = s.power * GameData.SkillLevelDmg(slot.lv) * stat * stab * (1f + tb / 100f) * p.d.dmgMult;
            if (s.id == "electro_ball") dmg *= 1f + Mathf.Max(0, p.d.spe - 50) / 60f;
            // 熔岩池: fire / rock moves +25% when cast from inside.
            var f = Fields.At(p.pos);
            if (f != null && f.kind == FieldKind.Lava && (s.type == PType.Fire || s.type == PType.Rock)) dmg *= 1.25f;
            return dmg;
        }

        void UpdateSkills(float dt)
        {
            foreach (var slot in player.skills)
            {
                if (slot.cd > 0) { slot.cd -= dt; continue; }
                if (Cast(slot))
                {
                    float cd = slot.def.cd * GameData.SkillLevelCd(slot.lv) * player.d.cdMult;
                    var f = Fields.At(player.pos);
                    // 局部阵雨: water / electric cooldowns -20%.
                    if (f != null && f.kind == FieldKind.Rain && (slot.def.type == PType.Water || slot.def.type == PType.Electric)) cd *= 0.8f;
                    slot.cd = Mathf.Max(0.1f, cd);
                }
            }
        }

        bool Cast(SkillSlot slot)
        {
            var s = slot.def;
            var p = player;
            float k = p.d.rangeMult;
            switch (s.pattern)
            {
                case Pattern.Shield:
                {
                    if (Nearest(p.pos, s.range) == null) return false;
                    p.shield = p.d.maxHp * s.shield;
                    p.shieldT = s.duration;
                    fx?.ShieldUp(p.shield);
                    return true;
                }
                case Pattern.Buff:
                {
                    if (Nearest(p.pos, s.range) == null) return false;
                    p.buffT = s.buffDur; p.buffHaste = s.buffHaste; p.buffSpe = s.buffSpe;
                    Recompute();
                    fx?.Buff();
                    return true;
                }
                case Pattern.Nova:
                {
                    float rad = s.radius * k;
                    if (Nearest(p.pos, rad) == null) return false;
                    float dmg = BaseDamage(slot);
                    tmpEnemies.Clear();
                    foreach (var e in enemies) if ((e.pos - p.pos).sqrMagnitude < (rad + e.r) * (rad + e.r)) tmpEnemies.Add(e);
                    foreach (var e in tmpEnemies) HitEnemy(e, dmg, s, e.pos - p.pos);
                    fx?.Nova(p.pos, rad, s.type);
                    return true;
                }
            }

            float range = (s.pattern == Pattern.Melee ? s.radius : s.range) * k;
            var t = Nearest(p.pos, range + 10);
            if (t == null) return false;
            Vector2 dir = (t.pos - p.pos);
            float ang = Mathf.Atan2(dir.y, dir.x);
            float baseDmg = BaseDamage(slot);
            p.facing = dir.normalized;

            switch (s.pattern)
            {
                case Pattern.Proj:
                {
                    int n = s.count;
                    for (int i = 0; i < n; i++)
                    {
                        float a = n == 1 ? ang : ang - s.spread / 2 + s.spread * i / (n - 1);
                        var pr = new Proj
                        {
                            id = nextId++, pos = p.pos, vel = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * s.speed, life = s.range * k / s.speed,
                            dmg = baseDmg, size = s.size, homing = s.homing, knock = s.knock, pierce = s.pierce, type = s.type, skill = s, crit = s.crit,
                            heal = s.heal,
                        };
                        projs.Add(pr);
                        fx?.ProjSpawned(pr);
                    }
                    return true;
                }
                case Pattern.Melee:
                {
                    float rad = s.radius * k;
                    tmpEnemies.Clear();
                    foreach (var e in enemies)
                    {
                        Vector2 dv = e.pos - p.pos;
                        if (dv.sqrMagnitude > (rad + e.r) * (rad + e.r)) continue;
                        if (s.arc < Mathf.PI * 2 - 0.01f && AngDiff(Mathf.Atan2(dv.y, dv.x), ang) > s.arc / 2 + 0.25f) continue;
                        tmpEnemies.Add(e);
                    }
                    foreach (var e in tmpEnemies) HitEnemy(e, baseDmg, s, e.pos - p.pos);
                    fx?.Melee(p.pos, ang, s.arc, rad, s.type);
                    return true;
                }
                case Pattern.Beam:
                {
                    float len = s.range * k;
                    Vector2 u = new(Mathf.Cos(ang), Mathf.Sin(ang));
                    tmpEnemies.Clear();
                    foreach (var e in enemies)
                    {
                        Vector2 dv = e.pos - p.pos;
                        float along = Vector2.Dot(dv, u);
                        if (along < -e.r || along > len + e.r) continue;
                        float off = Mathf.Abs(dv.x * u.y - dv.y * u.x);
                        if (off <= s.width / 2 + e.r) tmpEnemies.Add(e);
                    }
                    foreach (var e in tmpEnemies) HitEnemy(e, baseDmg, s, u);
                    fx?.Beam(p.pos, ang, len, s.width, s.type);
                    return true;
                }
                case Pattern.Chain:
                {
                    tmpPoints.Clear();
                    tmpPoints.Add(p.pos);
                    var hit = new HashSet<int>();
                    var cur = t;
                    float dmg = baseDmg;
                    for (int i = 0; i < s.chains && cur != null; i++)
                    {
                        hit.Add(cur.id);
                        tmpPoints.Add(cur.pos);
                        var from = cur;
                        HitEnemy(from, dmg, s, from.pos - p.pos);
                        dmg *= 0.88f;
                        Enemy next = null;
                        float bd = (95 * k) * (95 * k);
                        foreach (var e in enemies)
                        {
                            if (e.hp <= 0 || hit.Contains(e.id)) continue;
                            float d = (e.pos - from.pos).sqrMagnitude;
                            if (d < bd) { bd = d; next = e; }
                        }
                        cur = next;
                    }
                    fx?.Chain(new List<Vector2>(tmpPoints), s.type);
                    return true;
                }
                case Pattern.Strike:
                {
                    var st = new Strike { pos = t.pos, delay = s.delay, t = s.delay, radius = s.radius * k, dmg = baseDmg, type = s.type, skill = s };
                    strikes.Add(st);
                    fx?.StrikeWarn(st);
                    return true;
                }
                case Pattern.Zone:
                {
                    var z = new Zone { pos = t.pos, t = s.duration, radius = s.radius * k, dmg = baseDmg, tick = s.tick, tickT = 0, type = s.type, skill = s };
                    zones.Add(z);
                    fx?.ZoneStart(z);
                    return true;
                }
            }
            return false;
        }

        public float HitEnemy(Enemy e, float raw, SkillDef s, Vector2 knockDir, bool dot = false)
        {
            if (e.hp <= 0) return 0;
            var p = player;
            float eff = Types.Game(s.type, e.def.types);
            float dmg = raw * eff;
            dmg *= e.def.rank == Rank.Minion ? p.d.minionMult : p.d.bossMult;
            bool crit = false;
            if (!dot && rng.Chance(p.d.crit + s.crit / 100f)) { dmg *= 1.5f; crit = true; p.stats.crits++; }
            dmg = Mathf.Max(1, Mathf.Round(dmg));
            if (!e.showcase) e.hp -= dmg;
            p.stats.damage += dmg;
            p.stats.skillDmg.TryGetValue(s.name, out var acc);
            p.stats.skillDmg[s.name] = acc + dmg;
            if (!dot && p.d.lifesteal > 0 && rng.Chance(p.d.lifesteal)) Heal(1);
            fx?.EnemyHit(e, dmg, crit, eff, s.type);
            if (!dot && s.status != StatusKind.None && rng.Chance(s.statusChance)) ApplyStatus(e, s, dmg);
            if (!dot && s.knock > 0 && e.def.knockResist < 1f && knockDir.sqrMagnitude > 0.0001f)
                e.vel += knockDir.normalized * s.knock * (1f - e.def.knockResist);
            return dmg;
        }

        void ApplyStatus(Enemy e, SkillDef s, float dmg)
        {
            bool boss = e.def.rank != Rank.Minion;
            switch (s.status)
            {
                case StatusKind.Burn: e.burnT = s.statusDur; e.burnDps = Mathf.Max(e.burnDps * 0.6f, dmg * 0.25f); break;
                case StatusKind.Poison: e.poisonT = s.statusDur; e.poisonDps = Mathf.Max(e.poisonDps * 0.7f, dmg * 0.22f); break;
                case StatusKind.Leech: e.leechT = s.statusDur; e.leechDps = Mathf.Max(e.leechDps, dmg); e.leechHeal = s.heal; break;
                case StatusKind.Para: e.paraT = Mathf.Max(e.paraT, boss ? s.statusDur * 0.4f : s.statusDur); break;
                case StatusKind.Slow: e.slowT = Mathf.Max(e.slowT, s.statusDur); e.slowAmt = Mathf.Max(e.slowAmt, s.statusAmount); break;
                case StatusKind.Stun: e.stunT = Mathf.Max(e.stunT, boss ? s.statusDur * 0.4f : s.statusDur); break;
            }
        }

        void UpdateProjs(float dt)
        {
            for (int i = projs.Count - 1; i >= 0; i--)
            {
                var pr = projs[i];
                pr.life -= dt;
                if (pr.homing > 0 && !pr.enemy)
                {
                    var t = Nearest(pr.pos, 120);
                    if (t != null)
                    {
                        Vector2 want = (t.pos - pr.pos).normalized * pr.vel.magnitude;
                        pr.vel = Vector2.Lerp(pr.vel, want, Mathf.Min(1f, pr.homing * dt));
                    }
                }
                pr.pos += pr.vel * dt;
                if (pr.enemy)
                {
                    float rr = pr.size + GameData.PlayerR;
                    if ((pr.pos - player.pos).sqrMagnitude < rr * rr && player.invuln <= 0)
                    {
                        HurtPlayer(pr.dmg, SkillCat.Spec);
                        pr.dead = true;
                    }
                }
                else
                {
                    foreach (var e in enemies)
                    {
                        if (e.hp <= 0 || pr.hit.Contains(e.id)) continue;
                        float rr = pr.size + e.r;
                        if ((pr.pos - e.pos).sqrMagnitude >= rr * rr) continue;
                        pr.hit.Add(e.id);
                        HitEnemy(e, pr.dmg, pr.skill, pr.vel);
                        if (pr.pierce-- <= 0) { pr.dead = true; break; }
                        pr.dmg *= 0.9f;
                    }
                }
                if (pr.life <= 0 || pr.pos.x < -20 || pr.pos.y < -20 || pr.pos.x > GameData.ArenaW + 20 || pr.pos.y > GameData.ArenaH + 20) pr.dead = true;
                if (pr.dead)
                {
                    projs.RemoveAt(i);
                    fx?.ProjRemoved(pr);
                }
            }
        }

        void UpdateStrikes(float dt)
        {
            for (int i = strikes.Count - 1; i >= 0; i--)
            {
                var s = strikes[i];
                s.t -= dt;
                if (s.t > 0) continue;
                strikes.RemoveAt(i);
                tmpEnemies.Clear();
                foreach (var e in enemies) if ((e.pos - s.pos).sqrMagnitude < (s.radius + e.r) * (s.radius + e.r)) tmpEnemies.Add(e);
                foreach (var e in tmpEnemies) HitEnemy(e, s.dmg, s.skill, e.pos - s.pos);
                fx?.StrikeBoom(s);
            }
        }

        void UpdateZones(float dt)
        {
            for (int i = zones.Count - 1; i >= 0; i--)
            {
                var z = zones[i];
                z.t -= dt;
                z.tickT -= dt;
                if (z.tickT <= 0)
                {
                    z.tickT = z.tick;
                    tmpEnemies.Clear();
                    foreach (var e in enemies) if ((e.pos - z.pos).sqrMagnitude < (z.radius + e.r) * (z.radius + e.r)) tmpEnemies.Add(e);
                    foreach (var e in tmpEnemies) HitEnemy(e, z.dmg, z.skill, Vector2.zero);
                }
                if (z.t <= 0)
                {
                    zones.RemoveAt(i);
                    fx?.ZoneEnd(z);
                }
            }
        }

        // ------------------------------------------------------------------ pickups

        void DropCandy(Vector2 at, float value)
        {
            int pieces = Mathf.Max(1, Mathf.Min(Mathf.FloorToInt(value), value > 20 ? 10 : 5));
            float each = value / pieces;
            for (int i = 0; i < pieces; i++)
            {
                float a = rng.Range(0, Mathf.PI * 2), r = pieces == 1 ? 0 : rng.Range(4, 12);
                AddPickup(PickupKind.Candy, at + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r, each);
            }
        }

        void AddPickup(PickupKind kind, Vector2 pos, float value)
        {
            if (benchMode && pickups.Count > 120) return;
            var pk = new Pickup { id = nextId++, kind = kind, pos = pos, value = value };
            pickups.Add(pk);
            fx?.PickupSpawned(pk);
        }

        void UpdatePickups(float dt)
        {
            var p = player;
            for (int i = pickups.Count - 1; i >= 0; i--)
            {
                var pk = pickups[i];
                pk.age += dt;
                Vector2 dv = p.pos - pk.pos;
                float d = dv.magnitude;
                float radius = pk.kind == PickupKind.Candy ? p.d.pickupRadius : 18;
                if (pk.kind == PickupKind.Berry && p.hp >= p.d.maxHp) continue;
                if (d < radius) pk.magnet = true;
                if (pk.magnet && d > 0.01f) pk.pos += dv / d * Mathf.Min(d, (180 + pk.age * 60) * dt);
                if (d < 10) Collect(i);
            }
        }

        void Collect(int index)
        {
            var pk = pickups[index];
            pickups.RemoveAt(index);
            var p = player;
            if (pk.kind == PickupKind.Candy)
            {
                float money = pk.value * p.d.moneyMult;
                p.money += money;
                p.stats.moneyEarned += money;
                AddXp(pk.value * p.d.xpMult);
            }
            else Heal(5 + p.d.maxHp * 0.08f);
            fx?.PickupCollected(pk);
        }

        // ------------------------------------------------------------------ level-up cards & shop

        int RollTier(float x)
        {
            float luck = 1f + Mathf.Max(-50, player.d.luck) / 100f;
            float t4 = Mathf.Min(0.1f, Mathf.Max(0, (x - 7) * 0.012f)) * luck;
            float t3 = Mathf.Min(0.28f, Mathf.Max(0, (x - 3) * 0.025f)) * luck;
            float t2 = Mathf.Min(0.6f, Mathf.Max(0, (x - 1) * 0.06f)) * luck;
            float r = rng.Next();
            if (r < t4) return 4;
            if (r < t4 + t3) return 3;
            if (r < t4 + t3 + t2) return 2;
            return 1;
        }

        public bool NextCards()
        {
            var p = player;
            if (p.levelQueue.Count == 0) { cards = null; return false; }
            int level = p.levelQueue[0];
            cards = new List<LevelCard>();
            foreach (var (lv, skill) in p.line.learnset)
            {
                if (lv != level) continue;
                var owned = p.skills.Find(s => s.def.id == skill);
                if (owned == null || owned.lv < GameData.SkillMaxLv)
                    cards.Add(new LevelCard { skill = GameData.Skills[skill], upgrade = owned != null, tier = GameData.Skills[skill].tier });
            }
            var pool = new List<StatCard>(GameData.StatCards);
            rng.Shuffle(pool);
            foreach (var sc in pool)
            {
                if (cards.Count >= 4) break;
                int tier = RollTier(p.level / 1.2f);
                cards.Add(new LevelCard { stat = sc, amount = sc.per * tier, tier = tier });
            }
            return true;
        }

        public void PickCard(int i)
        {
            var c = cards[i];
            if (c.stat != null) c.stat.apply(player.levelMods, c.amount);
            else LearnSkill(c.skill);
            player.levelQueue.RemoveAt(0);
            Recompute();
            NextCards();
        }

        public string LearnSkill(SkillDef s)
        {
            var p = player;
            var owned = p.skills.Find(x => x.def.id == s.id);
            if (owned != null)
            {
                owned.lv = Mathf.Min(GameData.SkillMaxLv, owned.lv + 1);
                return $"{s.name} 强化到 Lv{owned.lv}";
            }
            if (p.skills.Count < p.line.slots)
            {
                p.skills.Add(new SkillSlot { def = s, cd = 0.3f });
                return $"学会了 {s.name}！";
            }
            // The web game asks which move to forget; the MVP replaces the lowest-tier one.
            int worst = 0;
            for (int i = 1; i < p.skills.Count; i++)
                if (p.skills[i].def.tier * 10 + p.skills[i].lv < p.skills[worst].def.tier * 10 + p.skills[worst].lv) worst = i;
            string old = p.skills[worst].def.name;
            p.skills[worst] = new SkillSlot { def = s, cd = 0.3f };
            return $"忘记了 {old}，学会了 {s.name}！";
        }

        public int RerollCost => Mathf.RoundToInt((2 + wave * 0.7f) * (1 + shopRerolls * 0.5f));

        public void RollShop()
        {
            shop.Clear();
            var itemIds = new List<string>(GameData.Items.Keys);
            var used = new HashSet<string>();
            for (int i = 0; i < 4; i++)
            {
                for (int tries = 0; tries < 30; tries++)
                {
                    if (rng.Chance(0.35f))
                    {
                        var s = GameData.Skills[rng.Pick(GameData.ShopSkills)];
                        var owned = player.skills.Find(x => x.def == s);
                        if (used.Contains(s.id) || (owned != null && owned.lv >= GameData.SkillMaxLv)) continue;
                        used.Add(s.id);
                        shop.Add(new ShopOffer { skill = s, price = Mathf.RoundToInt(GameData.SkillPrice[s.tier] * GameData.PriceMult(wave + 1)) });
                    }
                    else
                    {
                        var it = GameData.Items[rng.Pick(itemIds)];
                        if (used.Contains(it.id) || (it.tier > 1 && wave < 2 && rng.Chance(0.6f))) continue;
                        used.Add(it.id);
                        shop.Add(new ShopOffer { item = it, price = Mathf.RoundToInt(it.price * GameData.PriceMult(wave + 1)) });
                    }
                    break;
                }
            }
        }

        public string Buy(int i)
        {
            var o = shop[i];
            if (o.sold) return "已售出";
            if (player.money < o.price) return "金钱不足";
            player.money -= o.price;
            o.sold = true;
            if (o.item != null)
            {
                player.items.TryGetValue(o.item.id, out var n);
                player.items[o.item.id] = n + 1;
                Recompute();
                return $"得到了 {o.item.name}";
            }
            return LearnSkill(o.skill);
        }

        public string Reroll()
        {
            int cost = RerollCost;
            if (player.money < cost) return "金钱不足";
            player.money -= cost;
            shopRerolls++;
            RollShop();
            return null;
        }

        /// <summary>Remove everything spawned so far (used by the benchmark between scenarios).</summary>
        public void ClearField()
        {
            for (int i = enemies.Count - 1; i >= 0; i--) RemoveEnemy(i, false);
            foreach (var pr in projs) fx?.ProjRemoved(pr);
            projs.Clear();
            foreach (var s in pending) fx?.SpawnWarnEnd(s);
            pending.Clear();
            foreach (var z in zones) fx?.ZoneEnd(z);
            zones.Clear();
            strikes.Clear();
            for (int i = pickups.Count - 1; i >= 0; i--) { var pk = pickups[i]; pickups.RemoveAt(i); fx?.PickupCollected(pk); }
        }
    }
}
