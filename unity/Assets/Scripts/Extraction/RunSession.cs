using System;
using System.Collections;
using System.Collections.Generic;

namespace Poketato.Extraction
{
    public sealed class PlayerJoin
    {
        public int speciesId = 25;
        public int level = 1;
        public PokeType? mewExtra;
        public TalentBuild build;
        public bool autopilot = true;
        public List<ItemInstance> gear;
    }

    public struct CombatFx
    {
        public float x, z, radius;
        public PokeType type;
        public bool zone;
    }

    public struct PlayerInput
    {
        public int playerId;
        public float moveX, moveZ;
        public float aimX, aimZ;
        public int skill;
        public bool interact;
        public bool hasAim;
    }

    public sealed class SimRules
    {
        public bool noCrit;
        public bool forceStatus;
        public bool enemiesIdle;
    }

    public sealed class Actor
    {
        public int id;
        public int playerId;
        public int team;
        public int level;
        public SpeciesDef species;
        public float x, z, facing = 1f;
        public float hp, maxHp, shield, radius = 0.45f;
        public bool alive = true;
        public bool isBoss;
        public bool looted;
        public string roomId;
        public RuntimeStats stats;
        public List<TalentNode> skills = new List<TalentNode>();
        public List<TalentNode> hotbar = new List<TalentNode>();
        public Dictionary<string, float> cd = new Dictionary<string, float>();
        public List<StatusInst> statuses = new List<StatusInst>();
    }

    public sealed class StatusInst
    {
        public StatusKind kind;
        public float duration;
        public float dps;
        public int sourceId;
        public string skillId;
        public string move;
    }

    public sealed class PlayerState
    {
        public int id;
        public SpeciesDef species;
        public int level;
        public PokeType? mewExtra;
        public TalentBuild build;
        public bool autopilot = true;
        public bool alive = true;
        public bool extracted;
        public string roomId;
        public int runGold;
        public int extractedGold;
        public Actor actor;
        public List<ItemInstance> bag = new List<ItemInstance>();
        public List<ItemInstance> extractedItems = new List<ItemInstance>();
        public Dictionary<string, ItemInstance> worn = new Dictionary<string, ItemInstance>();
    }

    public sealed class SkillTotal
    {
        public string skillId;
        public string move;
        public int casts;
        public int hits;
        public float damage;
        public float healing;
    }

    public sealed class ParticipantReport
    {
        public int actorId;
        public int playerId;
        public int speciesId;
        public string name;
        public float damage;
        public float healing;
        public float damageTaken;
        public List<SkillTotal> skills = new List<SkillTotal>();
    }

    public sealed class RoomRuntime
    {
        public RoomDef def;
        public bool spawned;
        public bool cleared;
        public bool chestOpened;
    }

    sealed class DoorRect
    {
        public float minX, maxX, minZ, maxZ;
    }

    sealed class Proj
    {
        public float x, z, vx, vz, radius, life, damage;
        public int sourceId, team;
        public string skillId, move;
        public PokeType type;
        public bool physical;
    }

    sealed class Zone
    {
        public float x, z, radius, life, tick, every, damage;
        public int sourceId, team;
        public string skillId, move;
        public PokeType type;
    }

    public sealed class RunSession
    {
        public readonly MissionDef mission;
        public readonly Rng rng;
        public readonly SimRules rules = new SimRules();
        public readonly List<PlayerState> players = new List<PlayerState>();
        public readonly List<Actor> actors = new List<Actor>();
        public readonly DamageLedger meter = new DamageLedger();
        public float time;
        public bool failed;
        public bool puzzleSolved;

        readonly List<RoomRuntime> rooms = new List<RoomRuntime>();
        readonly List<DoorRect> doors = new List<DoorRect>();
        readonly List<Proj> projectiles = new List<Proj>();
        readonly List<Zone> zones = new List<Zone>();
        readonly Dictionary<string, RoomRuntime> roomById = new Dictionary<string, RoomRuntime>();
        int nextActor = 1;
        int nextPlayer = 1;
        bool bossSpawned;

        public RunSession(MissionDef mission, int seed)
        {
            this.mission = mission;
            rng = new Rng(seed);
            for (int i = 0; i < mission.rooms.Length; i++)
            {
                var room = new RoomRuntime { def = mission.rooms[i] };
                room.cleared = room.def.kind != RoomKind.Combat && room.def.kind != RoomKind.Elite && room.def.kind != RoomKind.Boss;
                rooms.Add(room);
                roomById[room.def.id] = room;
            }
            BuildDoors();
        }

        public static RunSession Start(string missionId, int seed)
        {
            return new RunSession(MissionCatalog.Get(missionId), seed);
        }

        public bool TryAddPlayer(PlayerJoin join)
        {
            if (failed || players.Count >= mission.maxPlayers) return false;
            var species = SpeciesCatalog.Get(join.speciesId);
            var player = new PlayerState
            {
                id = nextPlayer++,
                species = species,
                level = Math.Max(1, join.level),
                mewExtra = species.dex == 151 ? join.mewExtra : (PokeType?)null,
                autopilot = join.autopilot,
                build = join.build ?? TalentBuild.TowardKeystone(species, Math.Max(1, join.level))
            };
            if (join.gear != null)
                for (int i = 0; i < join.gear.Count; i++) Wear(player, join.gear[i]);
            var start = StartRoom();
            player.roomId = start.def.id;
            player.actor = MakePlayerActor(player, start.def);
            players.Add(player);
            actors.Add(player.actor);
            return true;
        }

        public void PlaceInRoom(int playerId, string roomId)
        {
            var player = FindPlayer(playerId);
            var room = roomById[roomId];
            float x, z;
            DungeonLayout.Center(room.def, out x, out z);
            player.actor.x = x;
            player.actor.z = z;
            player.actor.roomId = roomId;
            player.roomId = roomId;
            TrySpawn(room);
        }

        public void Tick(float dt, PlayerInput[] inputs)
        {
            if (dt <= 0f || failed) return;
            if (dt > 0.1f) dt = 0.1f;
            time += dt;
            var inputById = new Dictionary<int, PlayerInput>();
            if (inputs != null)
                for (int i = 0; i < inputs.Length; i++) inputById[inputs[i].playerId] = inputs[i];

            for (int i = 0; i < actors.Count; i++)
            {
                if (!actors[i].alive) continue;
                TickCooldowns(actors[i], dt);
                actors[i].hp = Math.Min(actors[i].maxHp, actors[i].hp + actors[i].stats.regen * dt);
                TickStatuses(actors[i], dt);
            }

            for (int i = 0; i < players.Count; i++)
            {
                var player = players[i];
                if (!player.alive || player.extracted) continue;
                RefreshRoom(player);
                PlayerInput input;
                if (!inputById.TryGetValue(player.id, out input))
                    input = new PlayerInput { playerId = player.id, skill = -1 };
                if (player.autopilot) input = Autopilot(player, input);
                bool locked = HardLocked(player.actor);
                if (!locked) Move(player.actor, input.moveX, input.moveZ, dt);
                RefreshRoom(player);
                if (!locked && !Paralyzed(player.actor) && input.skill >= 0)
                    CastIndex(player.actor, input.skill, input.aimX, input.aimZ, input.hasAim);
                inputById[player.id] = input;
            }

            if (!rules.enemiesIdle) EnemyAi(dt);
            TickProjectiles(dt);
            TickZones(dt);
            ResolveDeaths();
            for (int i = 0; i < players.Count; i++)
            {
                var player = players[i];
                if (!player.alive || player.extracted) continue;
                PlayerInput input;
                if (inputById.TryGetValue(player.id, out input) && input.interact)
                    Interact(player);
            }
            if (players.Count > 0 && Wiped()) failed = true;
        }

        public bool TryExtract(int playerId)
        {
            var player = FindPlayer(playerId);
            if (player == null || !player.alive || player.extracted) return false;
            RoomRuntime room;
            if (!roomById.TryGetValue(player.roomId, out room)) return false;
            if (room.def.kind != RoomKind.Extract && room.def.kind != RoomKind.EarlyExtract) return false;
            if (room.def.kind == RoomKind.Extract && HasBoss() && !BossDown()) return false;
            player.extracted = true;
            var kept = new List<ItemInstance>(player.bag.Count + 4);
            for (int i = 0; i < player.bag.Count; i++)
            {
                var item = player.bag[i];
                item.foundThisRun = false;
                kept.Add(item);
            }
            player.bag.Clear();
            var wornKeys = new List<string>(player.worn.Keys);
            for (int i = 0; i < wornKeys.Count; i++)
            {
                var item = player.worn[wornKeys[i]];
                if (!item.foundThisRun) continue;
                item.foundThisRun = false;
                kept.Add(item);
                player.worn.Remove(wornKeys[i]);
            }
            player.extractedItems = kept;
            int bonus = mission.clearBonus;
            if (room.def.kind == RoomKind.EarlyExtract) bonus /= 2;
            player.extractedGold = player.runGold + bonus;
            player.runGold = 0;
            player.actor.alive = false;
            player.alive = false;
            return true;
        }

        public bool Cast(int playerId, string skillId, float aimX, float aimZ)
        {
            var player = FindPlayer(playerId);
            if (player == null || !player.alive) return false;
            TalentNode node;
            if (!SkillTrees.TryGet(skillId, out node)) return false;
            if (node.slot != 0 && player.build.Rank(node.id) < 1) return false;
            CastNode(player.actor, node, aimX, aimZ);
            return true;
        }

        public void Kill(int playerId)
        {
            var player = FindPlayer(playerId);
            if (player == null || !player.alive) return;
            DownPlayer(player);
        }

        public string StatusLine()
        {
            if (players.Count == 0) return "no players";
            var player = players[0];
            int aliveEnemies = 0;
            for (int i = 0; i < actors.Count; i++)
                if (actors[i].team == 1 && actors[i].alive) aliveEnemies++;
            return "t=" + time.ToString("0.0")
                + " room=" + player.roomId
                + " pos=" + player.actor.x.ToString("0.0") + "," + player.actor.z.ToString("0.0")
                + " hp=" + player.actor.hp.ToString("0")
                + " gold=" + player.runGold
                + " bag=" + player.bag.Count
                + " ext=" + player.extracted
                + " enemies=" + aliveEnemies
                + " failed=" + failed;
        }

        public PlayerState FindPlayer(int id)
        {
            for (int i = 0; i < players.Count; i++)
                if (players[i].id == id) return players[i];
            return null;
        }

        public bool TryWear(int playerId, int bagIndex)
        {
            var player = FindPlayer(playerId);
            if (player == null || !player.alive || player.extracted) return false;
            if (bagIndex < 0 || bagIndex >= player.bag.Count) return false;
            var item = player.bag[bagIndex];
            if (item.durability <= 0f) return false;
            var basis = ItemCatalog.GetBase(item.baseId);
            if (player.level < basis.reqLevel) return false;
            ItemInstance displaced;
            string key = SlotFor(player, basis, out displaced);
            player.bag.RemoveAt(bagIndex);
            if (player.worn.ContainsKey(key)) player.bag.Add(player.worn[key]);
            player.worn[key] = item;
            Recompute(player);
            return true;
        }

        public bool TryWearBest(int playerId)
        {
            var player = FindPlayer(playerId);
            if (player == null || !player.alive || player.extracted) return false;
            bool any = false;
            int guard = 0;
            while (guard++ < 16)
            {
                int bestIndex = -1;
                float bestGain = 0.05f;
                for (int i = 0; i < player.bag.Count; i++)
                {
                    float gain = WearGain(player, player.bag[i]);
                    if (gain > bestGain) { bestGain = gain; bestIndex = i; }
                }
                if (bestIndex < 0 || !TryWear(playerId, bestIndex)) break;
                any = true;
            }
            return any;
        }

        public bool ChestOpened(string roomId)
        {
            RoomRuntime room;
            return roomById.TryGetValue(roomId, out room) && room.chestOpened;
        }

        public void CopyCombatFx(List<CombatFx> into)
        {
            into.Clear();
            for (int i = 0; i < projectiles.Count; i++)
            {
                var proj = projectiles[i];
                into.Add(new CombatFx { x = proj.x, z = proj.z, radius = 0.35f, type = proj.type, zone = false });
            }
            for (int i = 0; i < zones.Count; i++)
            {
                var zone = zones[i];
                into.Add(new CombatFx { x = zone.x, z = zone.z, radius = zone.radius, type = zone.type, zone = true });
            }
        }

        static float ItemScore(ItemInstance item)
        {
            if (item == null || item.durability <= 0f) return -1f;
            var basis = ItemCatalog.GetBase(item.baseId);
            float score = basis.weaponDamage + basis.armor * 0.5f + (int)item.rarity;
            if (basis.spell) score += 6f;
            for (int i = 0; i < item.affixes.Count; i++) score += Math.Abs(item.affixes[i].value);
            return score;
        }

        float WearGain(PlayerState player, ItemInstance item)
        {
            if (item == null || item.durability <= 0f) return -1f;
            var basis = ItemCatalog.GetBase(item.baseId);
            if (player.level < basis.reqLevel) return -1f;
            ItemInstance displaced;
            string key = SlotFor(player, basis, out displaced);
            if (!player.worn.ContainsKey(key)) return ItemScore(item) + 1f;
            return ItemScore(item) - ItemScore(player.worn[key]);
        }

        static string SlotFor(PlayerState player, ItemBase basis, out ItemInstance displaced)
        {
            displaced = null;
            if (basis.slot != Slot.Ring)
            {
                string key = basis.slot.ToString();
                if (player.worn.ContainsKey(key)) displaced = player.worn[key];
                return key;
            }
            ItemInstance left, right;
            bool hasLeft = player.worn.TryGetValue("RingL", out left);
            bool hasRight = player.worn.TryGetValue("RingR", out right);
            if (!hasLeft) return "RingL";
            if (!hasRight) return "RingR";
            if (ItemScore(left) <= ItemScore(right)) { displaced = left; return "RingL"; }
            displaced = right;
            return "RingR";
        }

        void Wear(PlayerState player, ItemInstance item)
        {
            var slot = ItemCatalog.GetBase(item.baseId).slot;
            string key = slot.ToString();
            if (slot == Slot.Ring) key = player.worn.ContainsKey("RingL") ? "RingR" : "RingL";
            player.worn[key] = item;
        }

        Actor MakePlayerActor(PlayerState player, RoomDef room)
        {
            float x, z;
            DungeonLayout.Center(room, out x, out z);
            var stats = GearStats.Build(player.species, player.level, player.build, player.worn.Values, player.mewExtra);
            var actor = new Actor
            {
                id = nextActor++,
                playerId = player.id,
                team = 0,
                level = player.level,
                species = player.species,
                x = x,
                z = z,
                roomId = room.id,
                stats = stats,
                maxHp = stats.maxHp,
                hp = stats.maxHp,
                radius = 0.45f
            };
            BindPlayerSkills(actor, player);
            return actor;
        }

        void BindPlayerSkills(Actor actor, PlayerState player)
        {
            actor.skills.Clear();
            actor.hotbar.Clear();
            var basic = SkillTrees.Tree(player.species.primary)[0];
            actor.skills.Add(basic);
            var bar = player.build.Hotbar(player.species, player.mewExtra);
            for (int i = 0; i < bar.Count; i++)
            {
                actor.hotbar.Add(bar[i]);
                actor.skills.Add(bar[i]);
            }
        }

        void Recompute(PlayerState player)
        {
            var stats = GearStats.Build(player.species, player.level, player.build, player.worn.Values, player.mewExtra);
            float ratio = player.actor.maxHp <= 0f ? 1f : player.actor.hp / player.actor.maxHp;
            player.actor.stats = stats;
            player.actor.maxHp = stats.maxHp;
            player.actor.hp = Math.Min(stats.maxHp, stats.maxHp * ratio);
        }

        RoomRuntime StartRoom()
        {
            for (int i = 0; i < rooms.Count; i++)
                if (rooms[i].def.kind == RoomKind.Start) return rooms[i];
            return rooms[0];
        }

        void BuildDoors()
        {
            for (int i = 0; i < rooms.Count; i++)
            {
                var a = rooms[i].def;
                for (int l = 0; l < a.links.Length; l++)
                {
                    RoomRuntime other;
                    if (!roomById.TryGetValue(a.links[l], out other)) continue;
                    var b = other.def;
                    if (a.id.CompareTo(b.id) > 0) continue;
                    if (DungeonLayout.Manhattan(a, b) != 1) continue;
                    float aMinX, aMaxX, aMinZ, aMaxZ, bMinX, bMaxX, bMinZ, bMaxZ;
                    DungeonLayout.Interior(a, out aMinX, out aMaxX, out aMinZ, out aMaxZ);
                    DungeonLayout.Interior(b, out bMinX, out bMaxX, out bMinZ, out bMaxZ);
                    var door = new DoorRect();
                    if (a.gx != b.gx)
                    {
                        door.minX = Math.Min(aMaxX, bMaxX) - 0.3f;
                        door.maxX = Math.Max(aMinX, bMinX) + 0.3f;
                        if (door.minX > door.maxX) { float s = door.minX; door.minX = door.maxX; door.maxX = s; }
                        float z = (Math.Max(aMinZ, bMinZ) + Math.Min(aMaxZ, bMaxZ)) * 0.5f;
                        door.minZ = z - 2.2f;
                        door.maxZ = z + 2.2f;
                    }
                    else
                    {
                        door.minZ = Math.Min(aMaxZ, bMaxZ) - 0.3f;
                        door.maxZ = Math.Max(aMinZ, bMinZ) + 0.3f;
                        if (door.minZ > door.maxZ) { float s = door.minZ; door.minZ = door.maxZ; door.maxZ = s; }
                        float x = (Math.Max(aMinX, bMinX) + Math.Min(aMaxX, bMaxX)) * 0.5f;
                        door.minX = x - 2.2f;
                        door.maxX = x + 2.2f;
                    }
                    doors.Add(door);
                }
            }
        }

        bool Walkable(float x, float z)
        {
            for (int i = 0; i < rooms.Count; i++)
            {
                float minX, maxX, minZ, maxZ;
                DungeonLayout.Interior(rooms[i].def, out minX, out maxX, out minZ, out maxZ);
                if (x >= minX && x <= maxX && z >= minZ && z <= maxZ) return true;
            }
            for (int i = 0; i < doors.Count; i++)
            {
                var door = doors[i];
                if (x >= door.minX && x <= door.maxX && z >= door.minZ && z <= door.maxZ) return true;
            }
            return false;
        }

        RoomRuntime RoomAt(float x, float z)
        {
            for (int i = 0; i < rooms.Count; i++)
            {
                float minX, maxX, minZ, maxZ;
                DungeonLayout.Interior(rooms[i].def, out minX, out maxX, out minZ, out maxZ);
                if (x >= minX && x <= maxX && z >= minZ && z <= maxZ) return rooms[i];
            }
            return null;
        }

        void RefreshRoom(PlayerState player)
        {
            var room = RoomAt(player.actor.x, player.actor.z);
            if (room == null) return;
            if (!CanBeHere(player.roomId, room)) return;
            player.roomId = room.def.id;
            player.actor.roomId = room.def.id;
            TrySpawn(room);
        }

        bool CanBeHere(string fromId, RoomRuntime to)
        {
            if (fromId == to.def.id) return true;
            RoomRuntime from;
            if (!roomById.TryGetValue(fromId, out from)) return true;
            return CanEnter(from, to);
        }

        bool CanEnter(RoomRuntime from, RoomRuntime to)
        {
            if (!Linked(from.def, to.def.id)) return false;
            if (to.def.requiresPuzzle && !puzzleSolved) return false;
            if (to.def.kind == RoomKind.Boss && !RequiredCleared()) return false;
            if (!from.cleared && (from.def.kind == RoomKind.Combat || from.def.kind == RoomKind.Elite || from.def.kind == RoomKind.Boss))
                return false;
            return true;
        }

        bool Linked(RoomDef room, string id)
        {
            for (int i = 0; i < room.links.Length; i++)
                if (room.links[i] == id) return true;
            return false;
        }

        bool RequiredCleared()
        {
            for (int i = 0; i < rooms.Count; i++)
            {
                var kind = rooms[i].def.kind;
                if (rooms[i].def.optional) continue;
                if ((kind == RoomKind.Combat || kind == RoomKind.Elite) && !rooms[i].cleared) return false;
            }
            return true;
        }

        bool HasBoss()
        {
            for (int i = 0; i < rooms.Count; i++)
                if (rooms[i].def.kind == RoomKind.Boss) return true;
            return false;
        }

        bool BossDown()
        {
            for (int i = 0; i < rooms.Count; i++)
                if (rooms[i].def.kind == RoomKind.Boss && !rooms[i].cleared) return false;
            return true;
        }

        void TrySpawn(RoomRuntime room)
        {
            if (room.spawned) return;
            if (room.def.kind != RoomKind.Combat && room.def.kind != RoomKind.Elite && room.def.kind != RoomKind.Boss) return;
            room.spawned = true;
            room.cleared = false;
            float cx, cz;
            DungeonLayout.Center(room.def, out cx, out cz);
            if (room.def.kind == RoomKind.Boss)
            {
                int dex = room.def.bossSpecies != 0 ? room.def.bossSpecies : room.def.enemies[0];
                SpawnMonster(dex, LevelOf(room), room.def.id, cx, cz, true);
                bossSpawned = true;
                return;
            }
            int count = Math.Max(1, room.def.count);
            for (int i = 0; i < count; i++)
            {
                int dex = room.def.enemies[rng.NextInt(room.def.enemies.Length)];
                float ang = (float)(i * Math.PI * 2.0 / count);
                SpawnMonster(dex, LevelOf(room), room.def.id, cx + (float)Math.Cos(ang) * 2.2f, cz + (float)Math.Sin(ang) * 1.6f, false);
            }
        }

        int LevelOf(RoomRuntime room)
        {
            int level = mission.minLevel + room.def.levelBonus;
            if (level < 1) level = 1;
            if (level > mission.maxLevel) level = mission.maxLevel;
            return level;
        }

        void SpawnMonster(int dex, int level, string roomId, float x, float z, bool boss)
        {
            var species = SpeciesCatalog.Get(dex);
            var stats = GearStats.Build(species, level, null, null, null);
            stats.maxHp *= 1f + (level - 1) * 0.08f;
            if (boss)
            {
                stats.maxHp *= 8f;
                stats.atk *= 1.35f;
                stats.spa *= 1.35f;
                stats.weaponDamage *= 1.35f;
            }
            var actor = new Actor
            {
                id = nextActor++,
                team = 1,
                level = level,
                species = species,
                x = x,
                z = z,
                roomId = roomId,
                stats = stats,
                maxHp = stats.maxHp,
                hp = stats.maxHp,
                radius = boss ? 0.9f : 0.45f,
                isBoss = boss
            };
            var tree = SkillTrees.Tree(species.primary);
            actor.skills.Add(tree[0]);
            if (level >= 5) actor.skills.Add(tree[1]);
            if (level >= 10) actor.skills.Add(tree[3]);
            actors.Add(actor);
        }

        void Move(Actor actor, float x, float z, float dt)
        {
            float len = (float)Math.Sqrt(x * x + z * z);
            if (len < 0.01f) return;
            x /= len;
            z /= len;
            if (Has(actor, StatusKind.Confuse))
            {
                float ang = rng.NextFloat() * 6.28318f;
                x = (float)Math.Cos(ang);
                z = (float)Math.Sin(ang);
            }
            float speed = actor.stats.moveSpeed;
            if (Has(actor, StatusKind.Slow) || Has(actor, StatusKind.Snare)) speed *= 0.65f;
            actor.facing = (float)Math.Atan2(x, z);
            float nx = actor.x + x * speed * dt;
            float nz = actor.z + z * speed * dt;
            if (CanStep(actor, nx, nz)) { actor.x = nx; actor.z = nz; return; }
            if (CanStep(actor, nx, actor.z)) { actor.x = nx; return; }
            if (CanStep(actor, actor.x, nz)) actor.z = nz;
        }

        bool CanStep(Actor actor, float x, float z)
        {
            if (actor.team == 1) return InsideRoom(actor.roomId, x, z);
            if (!Walkable(x, z)) return false;
            var room = RoomAt(x, z);
            if (room == null || actor.playerId <= 0) return true;
            var player = FindPlayer(actor.playerId);
            return player == null || CanBeHere(player.roomId, room);
        }

        bool InsideRoom(string roomId, float x, float z)
        {
            RoomRuntime room;
            if (roomId == null || !roomById.TryGetValue(roomId, out room)) return false;
            float minX, maxX, minZ, maxZ;
            DungeonLayout.Interior(room.def, out minX, out maxX, out minZ, out maxZ);
            return x >= minX && x <= maxX && z >= minZ && z <= maxZ;
        }

        PlayerInput Autopilot(PlayerState player, PlayerInput existing)
        {
            var input = existing;
            input.playerId = player.id;
            input.skill = -1;
            TryWearBest(player.id);
            RoomRuntime hereRoom;
            if (roomById.TryGetValue(player.roomId, out hereRoom))
            {
                bool closedChest = (hereRoom.def.kind == RoomKind.Treasure || hereRoom.def.kind == RoomKind.Secret)
                    && !hereRoom.chestOpened;
                bool needsClick = closedChest || (hereRoom.def.puzzle && !puzzleSolved);
                if (needsClick)
                {
                    float cx, cz;
                    DungeonLayout.Center(hereRoom.def, out cx, out cz);
                    input.moveX = cx - player.actor.x;
                    input.moveZ = cz - player.actor.z;
                    input.aimX = cx;
                    input.aimZ = cz;
                    input.hasAim = true;
                    input.interact = true;
                    return input;
                }
            }
            var enemy = NearestEnemy(player.roomId, player.actor.x, player.actor.z);
            if (enemy != null)
            {
                float dist = Dist(player.actor, enemy);
                input.aimX = enemy.x;
                input.aimZ = enemy.z;
                input.hasAim = true;
                var skill = BestReady(player.actor, dist);
                if (skill != null)
                {
                    input.skill = SkillIndex(player.actor, skill);
                    input.moveX = 0f;
                    input.moveZ = 0f;
                    return input;
                }
                input.moveX = enemy.x - player.actor.x;
                input.moveZ = enemy.z - player.actor.z;
                return input;
            }
            string goal = AdjacentSearch(player.roomId);
            if (goal == null) goal = NextGoal(player.roomId);
            if (goal == null || goal == player.roomId)
            {
                RoomRuntime here;
                if (roomById.TryGetValue(player.roomId, out here)
                    && (here.def.kind == RoomKind.Extract || here.def.kind == RoomKind.EarlyExtract))
                    input.interact = true;
                return input;
            }
            string step = NextStep(player.roomId, goal);
            if (step == null) return input;
            float tx, tz;
            DungeonLayout.Center(roomById[step].def, out tx, out tz);
            input.moveX = tx - player.actor.x;
            input.moveZ = tz - player.actor.z;
            input.aimX = tx;
            input.aimZ = tz;
            input.hasAim = true;
            return input;
        }

        void EnemyAi(float dt)
        {
            for (int i = 0; i < actors.Count; i++)
            {
                var actor = actors[i];
                if (!actor.alive || actor.team != 1) continue;
                if (HardLocked(actor)) continue;
                var target = NearestPlayer(actor.roomId, actor.x, actor.z);
                if (target == null) continue;
                float dist = Dist(actor, target);
                if (dist > 1.7f) Move(actor, target.x - actor.x, target.z - actor.z, dt);
                if (Paralyzed(actor)) continue;
                var skill = BestReady(actor, dist);
                if (skill != null)
                {
                    actor.facing = (float)Math.Atan2(target.x - actor.x, target.z - actor.z);
                    CastNode(actor, skill, target.x, target.z);
                }
            }
        }

        TalentNode BestReady(Actor actor, float dist)
        {
            TalentNode best = null;
            for (int i = 0; i < actor.skills.Count; i++)
            {
                var node = actor.skills[i];
                if (Cooldown(actor, node.id) > 0f) continue;
                if (dist > SkillTrees.CastRange(node)) continue;
                if (node.shape == EffectShape.Dash && Has(actor, StatusKind.Snare)) continue;
                if (best == null || node.tier > best.tier || (node.tier == best.tier && node.power > best.power))
                    best = node;
            }
            return best;
        }

        int SkillIndex(Actor actor, TalentNode node)
        {
            if (node.slot == 0) return 0;
            for (int i = 0; i < actor.hotbar.Count; i++)
                if (actor.hotbar[i].id == node.id) return i + 1;
            return -1;
        }

        void CastIndex(Actor actor, int index, float aimX, float aimZ, bool hasAim)
        {
            TalentNode node = null;
            if (index == 0) node = actor.skills[0];
            else if (index > 0 && index <= actor.hotbar.Count) node = actor.hotbar[index - 1];
            if (node == null) return;
            if (!hasAim)
            {
                var enemy = NearestEnemy(actor.roomId, actor.x, actor.z);
                if (enemy != null) { aimX = enemy.x; aimZ = enemy.z; }
                else { aimX = actor.x + (float)Math.Sin(actor.facing); aimZ = actor.z + (float)Math.Cos(actor.facing); }
            }
            CastNode(actor, node, aimX, aimZ);
        }

        void CastNode(Actor actor, TalentNode node, float aimX, float aimZ)
        {
            if (Cooldown(actor, node.id) > 0f) return;
            if (node.shape == EffectShape.Dash && Has(actor, StatusKind.Snare)) return;
            float dx = aimX - actor.x;
            float dz = aimZ - actor.z;
            float dist = (float)Math.Sqrt(dx * dx + dz * dz);
            if (node.shape == EffectShape.Nova || node.shape == EffectShape.Melee)
            {
                var nearest = NearestEnemy(actor.roomId, actor.x, actor.z);
                float gate = SkillTrees.CastRange(node);
                if (nearest == null || Dist(actor, nearest) > gate + 0.2f) return;
            }
            else if (node.shape != EffectShape.Heal && node.shape != EffectShape.Buff && dist > SkillTrees.CastRange(node) + 0.2f)
            {
                return;
            }
            if (dist > 0.05f) actor.facing = (float)Math.Atan2(dx, dz);
            float cd = node.cooldown * (1f - actor.stats.cdr) / (1f + actor.stats.haste);
            if (cd < 0.05f) cd = 0.05f;
            actor.cd[node.id] = cd;
            NoteCast(actor, node);
            if (ParalyzeBlocks(actor)) return;
            switch (node.shape)
            {
                case EffectShape.Melee: Melee(actor, node); break;
                case EffectShape.Projectile: Shoot(actor, node, aimX, aimZ); break;
                case EffectShape.Nova: Nova(actor, node); break;
                case EffectShape.Zone: AddZone(actor, node, aimX, aimZ, dist); break;
                case EffectShape.Dash: Dash(actor, node, aimX, aimZ); break;
                case EffectShape.Heal: HealSkill(actor, node); break;
                case EffectShape.Buff: actor.shield += 10f + actor.stats.def * 0.15f; break;
            }
        }

        void Melee(Actor actor, TalentNode node)
        {
            float reach = SkillTrees.CastRange(node);
            float fx = (float)Math.Sin(actor.facing);
            float fz = (float)Math.Cos(actor.facing);
            for (int i = 0; i < actors.Count; i++)
            {
                var other = actors[i];
                if (!other.alive || other.team == actor.team) continue;
                float dx = other.x - actor.x;
                float dz = other.z - actor.z;
                float dist = (float)Math.Sqrt(dx * dx + dz * dz);
                if (dist > reach + other.radius) continue;
                if (dist > 0.05f && (dx * fx + dz * fz) / dist < 0.5f) continue;
                Hit(actor, other, node, false);
            }
        }

        void Nova(Actor actor, TalentNode node)
        {
            float radius = SkillTrees.CastRange(node);
            for (int i = 0; i < actors.Count; i++)
            {
                var other = actors[i];
                if (!other.alive || other.team == actor.team) continue;
                if (Dist(actor, other) <= radius + other.radius) Hit(actor, other, node, false);
            }
        }

        void Shoot(Actor actor, TalentNode node, float aimX, float aimZ)
        {
            float dx = aimX - actor.x;
            float dz = aimZ - actor.z;
            float dist = (float)Math.Sqrt(dx * dx + dz * dz);
            if (dist < 0.05f) { dx = (float)Math.Sin(actor.facing); dz = (float)Math.Cos(actor.facing); dist = 1f; }
            float speed = 14f;
            projectiles.Add(new Proj
            {
                x = actor.x, z = actor.z,
                vx = dx / dist * speed, vz = dz / dist * speed,
                radius = 0.45f, life = 1.3f,
                damage = RollAmount(actor, node, false),
                sourceId = actor.id, team = actor.team,
                skillId = node.id, move = node.moveZh, type = node.type,
                physical = UsesPhysical(node)
            });
        }

        void AddZone(Actor actor, TalentNode node, float aimX, float aimZ, float dist)
        {
            if (dist > SkillTrees.CastRange(node)) { aimX = actor.x; aimZ = actor.z; }
            zones.Add(new Zone
            {
                x = aimX, z = aimZ, radius = 2.8f, life = 3f, every = 0.5f,
                damage = RollAmount(actor, node, false) * 0.35f,
                sourceId = actor.id, team = actor.team,
                skillId = node.id, move = node.moveZh, type = node.type
            });
        }

        void Dash(Actor actor, TalentNode node, float aimX, float aimZ)
        {
            float dx = aimX - actor.x;
            float dz = aimZ - actor.z;
            float dist = (float)Math.Sqrt(dx * dx + dz * dz);
            if (dist < 0.05f) return;
            dx /= dist; dz /= dist;
            float step = Math.Min(4f, dist);
            for (int i = 1; i <= 4; i++)
            {
                float nx = actor.x + dx * step * (i / 4f);
                float nz = actor.z + dz * step * (i / 4f);
                if (!CanStep(actor, nx, nz)) break;
                actor.x = nx;
                actor.z = nz;
                for (int a = 0; a < actors.Count; a++)
                {
                    var other = actors[a];
                    if (!other.alive || other.team == actor.team) continue;
                    float ox = other.x - actor.x;
                    float oz = other.z - actor.z;
                    if (ox * ox + oz * oz <= 1.6f) Hit(actor, other, node, true);
                }
            }
        }

        void HealSkill(Actor actor, TalentNode node)
        {
            float amount = (12f + actor.stats.spa * 0.2f) * (1f + actor.stats.healBonus);
            Actor target = actor;
            for (int i = 0; i < actors.Count; i++)
            {
                var other = actors[i];
                if (!other.alive || other.team != actor.team) continue;
                if (Dist(actor, other) > 8f) continue;
                if (other.hp / other.maxHp < target.hp / target.maxHp) target = other;
            }
            float before = target.hp;
            target.hp = Math.Min(target.maxHp, target.hp + amount);
            float gained = target.hp - before;
            if (gained > 0f) meter.NoteHeal(actor, node, gained, RecordPair(actor, target));
        }

        void Hit(Actor src, Actor dst, TalentNode node, bool dash)
        {
            float amount = RollAmount(src, node, true);
            amount = FinishAmount(src, dst, node, amount);
            ApplyDamage(src, dst, node, amount, true, dash);
        }

        float RollAmount(Actor src, TalentNode node, bool includeCrit)
        {
            float coeff = node.slot == 0 ? src.stats.basicCoeff : 1f;
            float scale = CombatMath.LevelScale(src.level) * CombatMath.Post30(src.level);
            float raw;
            if (UsesPhysical(node))
                raw = CombatMath.PhysicalRaw(src.stats.weaponDamage, node.power, coeff, src.stats.atk, scale);
            else
                raw = CombatMath.SpecialRaw(node.power + src.stats.spellWeapon, coeff, src.stats.spa, src.stats.elem[(int)node.type], scale);
            if (includeCrit && !rules.noCrit && rng.NextFloat() < src.stats.crit)
                raw *= src.stats.critMult;
            return raw;
        }

        float FinishAmount(Actor src, Actor dst, TalentNode node, float raw)
        {
            if (UsesPhysical(node))
            {
                float armor = dst.stats.armor;
                if (Has(dst, StatusKind.Sunder)) armor *= 0.75f;
                raw *= 1f - CombatMath.ArmorMitigation(armor, src.level);
            }
            else raw *= CombatMath.ResistFactor(dst.stats.resist[(int)node.type], src.stats.pierce);
            raw *= TypeChart.Effectiveness(node.type, dst.species.primary, dst.species.secondary);
            if (Has(dst, StatusKind.Mark))
            {
                raw *= 1.25f;
                Consume(dst, StatusKind.Mark);
            }
            if (Has(src, StatusKind.Weaken)) raw *= 0.85f;
            raw *= 1f - dst.stats.dr;
            if (raw < 0f) raw = 0f;
            return raw;
        }

        static bool UsesPhysical(TalentNode node)
        {
            return node.shape == EffectShape.Melee || node.shape == EffectShape.Dash || TypeChart.IsPhysical(node.type);
        }

        void ApplyDamage(Actor src, Actor dst, TalentNode node, float amount, bool onHit, bool dash)
        {
            if (amount <= 0f || dst == null || !dst.alive) return;
            if (dst.shield > 0f)
            {
                float used = Math.Min(dst.shield, amount);
                dst.shield -= used;
                amount -= used;
            }
            dst.hp -= amount;
            bool record = RecordPair(src, dst);
            if (src != null) meter.NoteDamage(src, node, amount + 0f, record);
            if (dst.playerId > 0) meter.NoteTaken(dst, amount, record);
            if (onHit && src != null)
            {
                float leech = src.stats.lifesteal + (dash ? 0.2f : 0f);
                if (leech > 0f) src.hp = Math.Min(src.maxHp, src.hp + amount * leech);
                TryStatus(src, dst, node, amount);
                if (src.stats.burnOnHit) ApplyStatus(dst, StatusKind.Burn, src, node, 4f, 2f + node.power * 0.05f);
            }
            if (dst.hp <= 0f)
            {
                dst.hp = 0f;
                dst.alive = false;
            }
        }

        void TryStatus(Actor src, Actor dst, TalentNode node, float dealt)
        {
            if (node.status == StatusKind.None && !rules.forceStatus) return;
            var kind = node.status;
            if (kind == StatusKind.None) return;
            float chance = (node.slot == 0 || node.slot == 4 || node.slot == 9) ? 0.35f : 0.2f;
            chance += src.stats.statusChance;
            if (rules.forceStatus) chance = 1f;
            if (rng.NextFloat() > chance) return;
            if (kind == StatusKind.Leech)
            {
                src.hp = Math.Min(src.maxHp, src.hp + dealt * 0.4f);
                return;
            }
            float duration = BaseDuration(kind) * (1f + src.stats.statusDuration);
            float dps = 0f;
            if (kind == StatusKind.Burn) dps = (2f + node.power * 0.05f) * TypeChart.Effectiveness(PokeType.Fire, dst.species.primary, dst.species.secondary);
            if (kind == StatusKind.Poison) dps = (1.5f + node.power * 0.04f) * TypeChart.Effectiveness(PokeType.Poison, dst.species.primary, dst.species.secondary);
            ApplyStatus(dst, kind, src, node, duration, dps);
        }

        static float BaseDuration(StatusKind kind)
        {
            switch (kind)
            {
                case StatusKind.Burn: return 4f;
                case StatusKind.Poison: return 6f;
                case StatusKind.Freeze: return 1f;
                case StatusKind.Stun: return 0.6f;
                case StatusKind.Paralyze: return 3f;
                case StatusKind.Slow:
                case StatusKind.Snare: return 3f;
                case StatusKind.Confuse: return 2f;
                case StatusKind.Weaken: return 4f;
                case StatusKind.Sunder: return 5f;
                case StatusKind.Mark: return 6f;
                default: return 2f;
            }
        }

        void ApplyStatus(Actor dst, StatusKind kind, Actor src, TalentNode node, float duration, float dps)
        {
            for (int i = 0; i < dst.statuses.Count; i++)
            {
                if (dst.statuses[i].kind != kind) continue;
                if (duration > dst.statuses[i].duration) dst.statuses[i].duration = duration;
                dst.statuses[i].dps = Math.Max(dst.statuses[i].dps, dps);
                return;
            }
            dst.statuses.Add(new StatusInst
            {
                kind = kind, duration = duration, dps = dps,
                sourceId = src == null ? 0 : src.id,
                skillId = node == null ? "" : node.id,
                move = node == null ? "" : node.moveZh
            });
        }

        void TickStatuses(Actor actor, float dt)
        {
            for (int i = actor.statuses.Count - 1; i >= 0; i--)
            {
                var status = actor.statuses[i];
                status.duration -= dt;
                if ((status.kind == StatusKind.Burn || status.kind == StatusKind.Poison) && status.dps > 0f && actor.alive)
                {
                    var src = FindActor(status.sourceId);
                    TalentNode node = null;
                    if (!string.IsNullOrEmpty(status.skillId)) SkillTrees.TryGet(status.skillId, out node);
                    float amount = status.dps * dt * (1f - actor.stats.dr);
                    ApplyDamage(src, actor, node, amount, false, false);
                }
                if (status.duration <= 0f) actor.statuses.RemoveAt(i);
            }
        }

        void TickProjectiles(float dt)
        {
            for (int i = projectiles.Count - 1; i >= 0; i--)
            {
                var proj = projectiles[i];
                proj.life -= dt;
                proj.x += proj.vx * dt;
                proj.z += proj.vz * dt;
                bool hit = false;
                if (proj.life > 0f)
                {
                    for (int a = 0; a < actors.Count; a++)
                    {
                        var other = actors[a];
                        if (!other.alive || other.team == proj.team) continue;
                        float dx = other.x - proj.x;
                        float dz = other.z - proj.z;
                        float reach = proj.radius + other.radius;
                        if (dx * dx + dz * dz > reach * reach) continue;
                        var src = FindActor(proj.sourceId);
                        TalentNode node;
                        if (src != null && SkillTrees.TryGet(proj.skillId, out node))
                        {
                            float amount = FinishAmount(src, other, node, proj.damage);
                            ApplyDamage(src, other, node, amount, true, false);
                        }
                        hit = true;
                        break;
                    }
                }
                if (hit || proj.life <= 0f) projectiles.RemoveAt(i);
            }
        }

        void TickZones(float dt)
        {
            for (int i = zones.Count - 1; i >= 0; i--)
            {
                var zone = zones[i];
                zone.life -= dt;
                zone.tick -= dt;
                if (zone.tick <= 0f)
                {
                    zone.tick += zone.every;
                    var src = FindActor(zone.sourceId);
                    TalentNode node;
                    SkillTrees.TryGet(zone.skillId, out node);
                    for (int a = 0; a < actors.Count; a++)
                    {
                        var other = actors[a];
                        if (!other.alive || other.team == zone.team) continue;
                        float dx = other.x - zone.x;
                        float dz = other.z - zone.z;
                        if (dx * dx + dz * dz > zone.radius * zone.radius) continue;
                        if (src != null && node != null)
                        {
                            float amount = FinishAmount(src, other, node, zone.damage);
                            ApplyDamage(src, other, node, amount, true, false);
                        }
                    }
                }
                if (zone.life <= 0f) zones.RemoveAt(i);
            }
        }

        void ResolveDeaths()
        {
            for (int i = 0; i < actors.Count; i++)
            {
                var actor = actors[i];
                if (actor.alive || actor.team != 1 || actor.looted) continue;
                actor.looted = true;
                string roomId = actor.roomId;
                DropLoot(actor, roomId);
                actor.roomId = null;
                RefreshClear(roomId);
            }
            for (int i = 0; i < players.Count; i++)
            {
                if (players[i].alive && players[i].actor.hp <= 0f) DownPlayer(players[i]);
            }
        }

        void RefreshClear(string roomId)
        {
            RoomRuntime room;
            if (!roomById.TryGetValue(roomId, out room)) return;
            for (int i = 0; i < actors.Count; i++)
            {
                var actor = actors[i];
                if (actor.team == 1 && actor.alive && actor.roomId == roomId) return;
            }
            room.cleared = true;
        }

        void DropLoot(Actor monster, string roomId)
        {
            RoomRuntime room;
            if (roomId == null || !roomById.TryGetValue(roomId, out room)) return;
            int drops = room.def.guaranteedDrops;
            if (monster.isBoss) drops = Math.Max(2, drops);
            var source = monster.isBoss ? DropSource.Boss : room.def.kind == RoomKind.Elite ? DropSource.Elite : DropSource.Trash;
            for (int p = 0; p < players.Count; p++)
            {
                var player = players[p];
                if (!player.alive || player.extracted) continue;
                player.runGold += 4 + monster.level;
                int give = drops;
                if (give == 0 && rng.NextFloat() < DropChance(source, player.actor.stats.magicFind)) give = 1;
                for (int n = 0; n < give; n++)
                {
                    var item = LootRoller.Roll(rng, monster.level, player.actor.stats.magicFind, source);
                    if (monster.isBoss && (item.rarity == Rarity.Normal || item.rarity == Rarity.Magic))
                        item = LootRoller.RollWithRarity(rng, monster.level, Rarity.Rare);
                    item.foundThisRun = true;
                    player.bag.Add(item);
                }
            }
        }

        static float DropChance(DropSource source, float magicFind)
        {
            float chance = source == DropSource.Elite ? 0.55f : 0.18f;
            chance *= 1f + magicFind;
            if (chance > 0.95f) chance = 0.95f;
            return chance;
        }

        void DownPlayer(PlayerState player)
        {
            player.alive = false;
            player.actor.alive = false;
            player.actor.hp = 0f;
            player.runGold = 0;
            for (int i = player.bag.Count - 1; i >= 0; i--)
            {
                if (player.bag[i].foundThisRun) player.bag.RemoveAt(i);
                else player.bag[i].durability = Math.Max(0f, player.bag[i].durability - 15f);
            }
            var keys = new List<string>(player.worn.Keys);
            for (int i = 0; i < keys.Count; i++)
            {
                var item = player.worn[keys[i]];
                if (item.foundThisRun) player.worn.Remove(keys[i]);
                else item.durability = Math.Max(0f, item.durability - 15f);
            }
            Recompute(player);
        }

        void Interact(PlayerState player)
        {
            RoomRuntime room;
            if (!roomById.TryGetValue(player.roomId, out room)) return;
            if (room.def.kind == RoomKind.Extract || room.def.kind == RoomKind.EarlyExtract)
            {
                TryExtract(player.id);
                return;
            }
            if (room.def.puzzle) puzzleSolved = true;
            if ((room.def.kind == RoomKind.Treasure || room.def.kind == RoomKind.Secret) && !room.chestOpened)
            {
                room.chestOpened = true;
                for (int n = 0; n < 2; n++)
                {
                    var item = LootRoller.Roll(rng, LevelOf(room), player.actor.stats.magicFind, DropSource.Chest);
                    item.foundThisRun = true;
                    player.bag.Add(item);
                }
            }
            if (room.def.kind == RoomKind.Merchant && player.runGold >= 40)
            {
                player.runGold -= 40;
                player.actor.hp = Math.Min(player.actor.maxHp, player.actor.hp + player.actor.maxHp * 0.4f);
            }
        }

        bool Wiped()
        {
            bool anyExtracted = false;
            for (int i = 0; i < players.Count; i++)
            {
                if (players[i].extracted) anyExtracted = true;
                if (players[i].alive && !players[i].extracted) return false;
            }
            return !anyExtracted;
        }

        string AdjacentSearch(string from)
        {
            RoomRuntime here;
            if (!roomById.TryGetValue(from, out here) || !here.cleared) return null;
            for (int i = 0; i < here.def.links.Length; i++)
            {
                RoomRuntime other;
                if (!roomById.TryGetValue(here.def.links[i], out other) || !other.def.optional) continue;
                bool chest = other.def.kind == RoomKind.Treasure || other.def.kind == RoomKind.Secret;
                if (chest && !other.chestOpened && (!other.def.requiresPuzzle || puzzleSolved)) return other.def.id;
                if (other.def.puzzle && !puzzleSolved) return other.def.id;
            }
            return null;
        }

        string NextGoal(string from)
        {
            string best = null;
            int bestDist = 999;
            for (int i = 0; i < rooms.Count; i++)
            {
                var room = rooms[i];
                if (room.def.optional || room.cleared) continue;
                var kind = room.def.kind;
                if (kind != RoomKind.Combat && kind != RoomKind.Elite && kind != RoomKind.Boss) continue;
                int dist = Distance(from, room.def.id);
                if (dist < 0 || dist >= bestDist) continue;
                bestDist = dist;
                best = room.def.id;
            }
            if (best != null) return best;
            for (int i = 0; i < rooms.Count; i++)
                if (rooms[i].def.kind == RoomKind.Extract) return rooms[i].def.id;
            return null;
        }

        int Distance(string from, string to)
        {
            var step = NextStep(from, to);
            if (from == to) return 0;
            if (step == null) return -1;
            var seen = new HashSet<string>();
            var queue = new Queue<string>();
            var dist = new Dictionary<string, int>();
            queue.Enqueue(from);
            dist[from] = 0;
            while (queue.Count > 0)
            {
                var id = queue.Dequeue();
                if (!seen.Add(id)) continue;
                if (id == to) return dist[id];
                var room = roomById[id].def;
                for (int i = 0; i < room.links.Length; i++)
                {
                    if (dist.ContainsKey(room.links[i])) continue;
                    dist[room.links[i]] = dist[id] + 1;
                    queue.Enqueue(room.links[i]);
                }
            }
            return -1;
        }

        string NextStep(string from, string goal)
        {
            if (from == goal) return null;
            var prev = new Dictionary<string, string>();
            var queue = new Queue<string>();
            queue.Enqueue(goal);
            prev[goal] = null;
            while (queue.Count > 0)
            {
                var id = queue.Dequeue();
                var room = roomById[id].def;
                for (int i = 0; i < room.links.Length; i++)
                {
                    var next = room.links[i];
                    if (prev.ContainsKey(next)) continue;
                    prev[next] = id;
                    if (next == from) return id;
                    queue.Enqueue(next);
                }
            }
            return null;
        }

        Actor NearestEnemy(string roomId, float x, float z)
        {
            Actor best = null;
            float bestDist = 9999f;
            for (int i = 0; i < actors.Count; i++)
            {
                var actor = actors[i];
                if (!actor.alive || actor.team != 1 || actor.roomId != roomId) continue;
                float dx = actor.x - x;
                float dz = actor.z - z;
                float d = dx * dx + dz * dz;
                if (d < bestDist) { bestDist = d; best = actor; }
            }
            return best;
        }

        Actor NearestPlayer(string roomId, float x, float z)
        {
            Actor best = null;
            float bestDist = 9999f;
            for (int i = 0; i < actors.Count; i++)
            {
                var actor = actors[i];
                if (!actor.alive || actor.team != 0 || actor.roomId != roomId) continue;
                float dx = actor.x - x;
                float dz = actor.z - z;
                float d = dx * dx + dz * dz;
                if (d < bestDist) { bestDist = d; best = actor; }
            }
            return best;
        }

        static float Dist(Actor a, Actor b)
        {
            float dx = a.x - b.x;
            float dz = a.z - b.z;
            return (float)Math.Sqrt(dx * dx + dz * dz);
        }

        Actor FindActor(int id)
        {
            for (int i = 0; i < actors.Count; i++)
                if (actors[i].id == id) return actors[i];
            return null;
        }

        static void TickCooldowns(Actor actor, float dt)
        {
            if (actor.cd.Count == 0) return;
            var keys = new List<string>(actor.cd.Keys);
            for (int i = 0; i < keys.Count; i++)
            {
                float left = actor.cd[keys[i]] - dt;
                if (left <= 0f) actor.cd.Remove(keys[i]);
                else actor.cd[keys[i]] = left;
            }
        }

        static float Cooldown(Actor actor, string id)
        {
            float value;
            return actor.cd.TryGetValue(id, out value) ? value : 0f;
        }

        static bool Has(Actor actor, StatusKind kind)
        {
            for (int i = 0; i < actor.statuses.Count; i++)
                if (actor.statuses[i].kind == kind) return true;
            return false;
        }

        static void Consume(Actor actor, StatusKind kind)
        {
            for (int i = 0; i < actor.statuses.Count; i++)
                if (actor.statuses[i].kind == kind) { actor.statuses.RemoveAt(i); return; }
        }

        static bool HardLocked(Actor actor)
        {
            return Has(actor, StatusKind.Freeze) || Has(actor, StatusKind.Stun);
        }

        bool Paralyzed(Actor actor)
        {
            return Has(actor, StatusKind.Paralyze) && rng.NextFloat() < 0.3f;
        }

        bool ParalyzeBlocks(Actor actor)
        {
            return Has(actor, StatusKind.Paralyze) && rng.NextFloat() < 0.3f;
        }

        bool RecordPair(Actor src, Actor dst)
        {
            if (mission.kind != MissionKind.Standard) return true;
            return (src != null && src.isBoss) || (dst != null && dst.isBoss);
        }

        void NoteCast(Actor actor, TalentNode node)
        {
            if (mission.kind == MissionKind.Standard && !bossSpawned) return;
            meter.NoteCast(actor, node, mission.kind != MissionKind.Standard || actor.isBoss || bossSpawned);
        }
    }

    public sealed class DamageLedger
    {
        readonly Dictionary<int, ParticipantReport> people = new Dictionary<int, ParticipantReport>();
        readonly Dictionary<string, SkillTotal> cells = new Dictionary<string, SkillTotal>();

        public IList<ParticipantReport> Participants()
        {
            return new List<ParticipantReport>(people.Values);
        }

        public void NoteCast(Actor actor, TalentNode node, bool record)
        {
            if (!record || actor == null || node == null) return;
            Cell(actor, node).casts++;
        }

        public void NoteDamage(Actor actor, TalentNode node, float amount, bool record)
        {
            if (!record || actor == null || node == null || amount <= 0f) return;
            var person = Person(actor);
            var cell = Cell(actor, node);
            cell.damage += amount;
            cell.hits++;
            person.damage += amount;
        }

        public void NoteHeal(Actor actor, TalentNode node, float amount, bool record)
        {
            if (!record || actor == null || node == null || amount <= 0f) return;
            var person = Person(actor);
            var cell = Cell(actor, node);
            cell.healing += amount;
            person.healing += amount;
        }

        public void NoteTaken(Actor actor, float amount, bool record)
        {
            if (!record || actor == null || amount <= 0f) return;
            Person(actor).damageTaken += amount;
        }

        ParticipantReport Person(Actor actor)
        {
            ParticipantReport report;
            if (!people.TryGetValue(actor.id, out report))
            {
                report = new ParticipantReport
                {
                    actorId = actor.id,
                    playerId = actor.playerId,
                    speciesId = actor.species.dex,
                    name = actor.species.nameZh
                };
                people[actor.id] = report;
            }
            return report;
        }

        SkillTotal Cell(Actor actor, TalentNode node)
        {
            string key = actor.id + "|" + node.id;
            SkillTotal cell;
            if (!cells.TryGetValue(key, out cell))
            {
                cell = new SkillTotal { skillId = node.id, move = node.moveZh };
                cells[key] = cell;
                Person(actor).skills.Add(cell);
            }
            return cell;
        }
    }
}
