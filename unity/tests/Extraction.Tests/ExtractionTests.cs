using System;
using System.Collections.Generic;
using System.Linq;
using Poketato.Extraction;
using Xunit;

namespace Poketato.Extraction.Tests
{
    public class ExtractionTests
    {
        [Fact]
        public void ThirtyPlayableSpeciesCoverRolesAndTypes()
        {
            Assert.Equal(30, SpeciesCatalog.PlayableCount);
            var dex = new HashSet<int>();
            foreach (var species in SpeciesCatalog.Playable)
            {
                Assert.True(dex.Add(species.dex));
                Assert.False(string.IsNullOrEmpty(species.nameZh));
                Assert.True(species.hp > 0 && species.spe > 0);
                Assert.Equal(species.primary, SkillTrees.Tree(species.primary)[0].type);
            }
            var charizard = SpeciesCatalog.Get(6);
            Assert.Equal("喷火龙", charizard.nameZh);
            Assert.Equal(PokeType.Fire, charizard.primary);
            Assert.Equal(PokeType.Flying, charizard.secondary);
            Assert.Equal(Role.RangedDps, charizard.role);
            Assert.Equal(78, charizard.hp);
            Assert.Equal("皮卡丘", SpeciesCatalog.Get(25).nameZh);
            Assert.Equal(Role.Skirmisher, SpeciesCatalog.Get(25).role);
        }

        [Fact]
        public void EveryTypeHasASixteenNodeWowTree()
        {
            Assert.Equal(15, SkillTrees.TypeCount);
            var ultimates = new Dictionary<PokeType, string>
            {
                { PokeType.Fire, "青焰" }, { PokeType.Water, "加农水炮" }, { PokeType.Electric, "伏特攻击" },
                { PokeType.Grass, "飞叶风暴" }, { PokeType.Ice, "冰冰霜冻" }, { PokeType.Fighting, "绝处逢生" },
                { PokeType.Poison, "打嗝" }, { PokeType.Ground, "断崖之剑" }, { PokeType.Flying, "暴风" },
                { PokeType.Psychic, "精神突进" }, { PokeType.Bug, "致命针刺" }, { PokeType.Rock, "双刃头锤" },
                { PokeType.Ghost, "灵骚" }, { PokeType.Dragon, "时光咆哮" }, { PokeType.Normal, "神速" }
            };
            var basics = new Dictionary<PokeType, string>
            {
                { PokeType.Fire, "火花" }, { PokeType.Water, "水枪" }, { PokeType.Electric, "电击" },
                { PokeType.Grass, "藤鞭" }, { PokeType.Ice, "细雪" }, { PokeType.Fighting, "空手劈" },
                { PokeType.Poison, "毒针" }, { PokeType.Ground, "掷泥" }, { PokeType.Flying, "啄" },
                { PokeType.Psychic, "念力" }, { PokeType.Bug, "虫咬" }, { PokeType.Rock, "落石" },
                { PokeType.Ghost, "舌舔" }, { PokeType.Dragon, "龙息" }, { PokeType.Normal, "撞击" }
            };
            foreach (var type in SkillTrees.Types)
            {
                var tree = SkillTrees.Tree(type);
                Assert.Equal(16, tree.Length);
                var names = new HashSet<string>();
                for (int i = 0; i < tree.Length; i++)
                {
                    Assert.Equal(i, tree[i].slot);
                    Assert.Equal(i == 15 ? 5 : i / 3, tree[i].tier);
                    Assert.True(names.Add(tree[i].moveZh));
                    if (tree[i].prereq != null) Assert.True(SkillTrees.TryGet(tree[i].prereq, out _));
                }
                Assert.Equal(basics[type], tree[0].moveZh);
                Assert.Equal(ultimates[type], tree[15].moveZh);
                Assert.Equal(tree[12].id, tree[15].prereq);
            }
        }

        [Fact]
        public void TalentGatesMatchWowRowsAndKeystoneFitsInThirtyPoints()
        {
            var charizard = SpeciesCatalog.Get(6);
            var empty = new TalentBuild();
            var advanced = SkillTrees.Get("fire-6");
            Assert.False(empty.CanSpend(charizard, null, advanced));
            var built = TalentBuild.TowardKeystone(charizard, 30);
            Assert.Equal(1, built.Rank("fire-15"));
            Assert.InRange(built.PointsIn(PokeType.Fire), 26, 30);
            Assert.True(empty.CanSpend(charizard, null, SkillTrees.Get("flying-1")));
            var mew = SpeciesCatalog.Get(151);
            var mewBuild = new TalentBuild();
            Assert.True(mewBuild.CanSpend(mew, PokeType.Fire, SkillTrees.Get("fire-1")));
            Assert.False(mewBuild.CanSpend(mew, null, SkillTrees.Get("fire-1")));
        }

        [Fact]
        public void TypeChartUsesIntendedGen1Matchups()
        {
            Assert.Equal(2f, TypeChart.One(PokeType.Fire, PokeType.Grass));
            Assert.Equal(0.5f, TypeChart.One(PokeType.Fire, PokeType.Water));
            Assert.Equal(2f, TypeChart.One(PokeType.Water, PokeType.Fire));
            Assert.Equal(0f, TypeChart.One(PokeType.Electric, PokeType.Ground));
            Assert.Equal(0f, TypeChart.One(PokeType.Ground, PokeType.Flying));
            Assert.Equal(0f, TypeChart.One(PokeType.Ghost, PokeType.Normal));
            Assert.Equal(0f, TypeChart.One(PokeType.Normal, PokeType.Ghost));
            Assert.Equal(0f, TypeChart.One(PokeType.Fighting, PokeType.Ghost));
            Assert.Equal(2f, TypeChart.One(PokeType.Ghost, PokeType.Psychic));
            Assert.Equal(1f, TypeChart.One(PokeType.Fire, PokeType.Psychic));
            Assert.Equal(2f, TypeChart.Effectiveness(PokeType.Fire, PokeType.Grass, PokeType.Poison));
        }

        [Fact]
        public void PhysicalHitMatchesTheDamageFormula()
        {
            var session = RunSession.Start("practice", 3);
            session.rules.noCrit = true;
            session.rules.enemiesIdle = true;
            Assert.True(session.TryAddPlayer(new PlayerJoin
            {
                speciesId = 68, level = 1, autopilot = false
            }));
            var player = session.players[0];
            session.PlaceInRoom(player.id, "yard");
            Actor enemy = null;
            for (int i = 0; i < session.actors.Count; i++)
                if (session.actors[i].team == 1) enemy = session.actors[i];
            Assert.NotNull(enemy);
            enemy.hp = enemy.maxHp = 10000f;
            player.actor.x = enemy.x;
            player.actor.z = enemy.z + 1.2f;
            player.actor.facing = (float)Math.Atan2(enemy.x - player.actor.x, enemy.z - player.actor.z);
            float before = enemy.hp;
            session.Tick(0.05f, new[]
            {
                new PlayerInput { playerId = player.id, skill = 0, aimX = enemy.x, aimZ = enemy.z, hasAim = true }
            });
            float dealt = before - enemy.hp;
            var node = SkillTrees.Get("fighting-0");
            float expected = CombatMath.PhysicalRaw(player.actor.stats.weaponDamage, node.power, 1f, player.actor.stats.atk, 1f);
            expected *= 1f - CombatMath.ArmorMitigation(enemy.stats.armor, 1);
            expected *= TypeChart.One(PokeType.Fighting, PokeType.Normal);
            Assert.InRange(Math.Abs(dealt - expected), 0.0, 0.05);
            Assert.True(dealt > 10f);
        }

        [Fact]
        public void RareRollsStayInsideAffixRulesAndAreDeterministic()
        {
            for (int seed = 1; seed <= 40; seed++)
            {
                var item = LootRoller.RollWithRarity(new Rng(seed), 30, Rarity.Rare);
                Assert.InRange(item.affixes.Count, 3, 6);
                var groups = new HashSet<string>();
                for (int i = 0; i < item.affixes.Count; i++)
                {
                    var def = ItemCatalog.GetAffix(item.affixes[i].id);
                    Assert.True(groups.Add(def.group));
                }
            }
            var magic = LootRoller.RollWithRarity(new Rng(7), 30, Rarity.Magic);
            int prefixes = 0, suffixes = 0;
            for (int i = 0; i < magic.affixes.Count; i++)
            {
                if (ItemCatalog.GetAffix(magic.affixes[i].id).prefix) prefixes++;
                else suffixes++;
            }
            Assert.True(prefixes <= 1 && suffixes <= 1);
            Assert.True(prefixes + suffixes >= 1);
            var a = LootRoller.Roll(new Rng(99), 25, 0.5f, DropSource.Elite);
            var b = LootRoller.Roll(new Rng(99), 25, 0.5f, DropSource.Elite);
            Assert.Equal(a.rarity, b.rarity);
            Assert.Equal(a.baseId, b.baseId);
            Assert.Equal(a.affixes.Count, b.affixes.Count);
            var unique = LootRoller.RollWithRarity(new Rng(4), 30, Rarity.Unique);
            Assert.Equal(Rarity.Unique, unique.rarity);
            Assert.False(string.IsNullOrEmpty(unique.uniqueId));
            Assert.Equal(0f, LootRoller.RarityWeights(0f, DropSource.Boss)[0]);
            var plain = LootRoller.RarityWeights(0f, DropSource.Trash);
            var hunted = LootRoller.RarityWeights(1f, DropSource.Trash);
            Assert.True(hunted[2] > plain[2]);
        }

        [Fact]
        public void SetBonusGrowsAtTwoAndFourPieces()
        {
            var pieces = ItemCatalog.SetPieces.Where(p => p.setId == "volcano").ToList();
            Assert.Equal(4, pieces.Count);
            var stats = GearStats.Build(SpeciesCatalog.Get(6), 20, null, MakeWorn(pieces.Take(2)), null);
            Assert.True(stats.elem[(int)PokeType.Fire] >= 0.15f);
            Assert.False(stats.burnOnHit);
            stats = GearStats.Build(SpeciesCatalog.Get(6), 20, null, MakeWorn(pieces), null);
            Assert.True(stats.elem[(int)PokeType.Fire] >= 0.30f);
            Assert.True(stats.burnOnHit);
        }

        static IEnumerable<ItemInstance> MakeWorn(IEnumerable<SetPieceDef> pieces)
        {
            foreach (var piece in pieces)
            {
                yield return new ItemInstance
                {
                    baseId = piece.baseId,
                    rarity = Rarity.Set,
                    setId = piece.setId,
                    uniqueId = piece.id,
                    ilvl = 30,
                    durability = 100f,
                    maxDurability = 100f,
                    affixes = new List<RolledAffix> { new RolledAffix { id = piece.id, stat = piece.stat, value = piece.value } }
                };
            }
        }

        [Fact]
        public void DeathDeletesRunDropsAndExtractKeepsThem()
        {
            var session = RunSession.Start("practice", 5);
            var brought = LootRoller.Plain("mengya-claw");
            Assert.True(session.TryAddPlayer(new PlayerJoin
            {
                speciesId = 6, level = 8, autopilot = false,
                gear = new List<ItemInstance> { brought }
            }));
            var player = session.players[0];
            var found = LootRoller.Plain("leaf-cap");
            found.foundThisRun = true;
            player.bag.Add(found);
            player.runGold = 30;
            session.Kill(player.id);
            Assert.Empty(player.bag);
            Assert.Equal(0, player.runGold);
            Assert.True(player.worn.ContainsKey("Weapon"));
            Assert.Equal(85f, player.worn["Weapon"].durability);

            session = RunSession.Start("practice", 6);
            brought = LootRoller.Plain("mengya-claw");
            Assert.True(session.TryAddPlayer(new PlayerJoin
            {
                speciesId = 6, level = 8, autopilot = false,
                gear = new List<ItemInstance> { brought }
            }));
            player = session.players[0];
            found = LootRoller.Plain("berry-charm");
            found.foundThisRun = true;
            player.worn["Amulet"] = found;
            player.worn.Remove("Weapon");
            player.bag.Add(brought);
            session.Kill(player.id);
            Assert.False(player.worn.ContainsKey("Amulet"));
            Assert.Single(player.bag);
            Assert.Equal(85f, player.bag[0].durability);

            session = RunSession.Start("foggy-forest", 2);
            Assert.True(session.TryAddPlayer(new PlayerJoin { speciesId = 9, level = 10, autopilot = false }));
            player = session.players[0];
            found = LootRoller.Plain("soft-boots");
            found.foundThisRun = true;
            player.bag.Add(found);
            player.runGold = 12;
            Assert.False(session.TryExtract(player.id));
            session.PlaceInRoom(player.id, "early");
            Assert.True(session.TryExtract(player.id));
            Assert.Equal(40 + 12, player.extractedGold);
            Assert.Single(player.extractedItems);
            Assert.Empty(player.bag);
        }

        [Fact]
        public void MissionsAreConnectedDistinctAndCapTheParty()
        {
            var tiles = new HashSet<string>();
            foreach (var mission in MissionCatalog.All)
            {
                Assert.True(tiles.Add(mission.tileset));
                Assert.NotEmpty(mission.tilesetRefs);
                var main = mission.rooms.Where(r => !r.optional).ToDictionary(r => r.id);
                Assert.Contains(main.Values, r => r.kind == RoomKind.Start);
                Assert.Contains(main.Values, r => r.kind == RoomKind.Extract);
                var seen = new HashSet<string>();
                var queue = new Queue<string>();
                var start = main.Values.First(r => r.kind == RoomKind.Start).id;
                queue.Enqueue(start);
                while (queue.Count > 0)
                {
                    var id = queue.Dequeue();
                    if (!main.ContainsKey(id) || !seen.Add(id)) continue;
                    foreach (var link in main[id].links)
                        if (main.ContainsKey(link)) queue.Enqueue(link);
                }
                Assert.Contains(main.Values, r => r.kind == RoomKind.Extract && seen.Contains(r.id));
                if (main.Values.Any(r => r.kind == RoomKind.Boss))
                    Assert.Contains(main.Values, r => r.kind == RoomKind.Boss && seen.Contains(r.id));
                foreach (var room in mission.rooms)
                {
                    foreach (var link in room.links)
                    {
                        var other = mission.rooms.First(r => r.id == link);
                        Assert.Equal(1, DungeonLayout.Manhattan(room, other));
                    }
                }
            }
            Assert.Equal(4, MissionCatalog.Get("foggy-forest").maxPlayers);
            Assert.Equal(8, MissionCatalog.Get("temporal-tower").maxPlayers);
            Assert.Equal(8, MissionCatalog.Get("guild-basement").maxPlayers);
            Assert.Equal(20, MissionCatalog.Get("northern-desert").maxPlayers);
            Assert.True(MissionCatalog.Get("beach-cave").reflectiveWater);

            var foggy = RunSession.Start("foggy-forest", 1);
            for (int i = 0; i < 4; i++)
                Assert.True(foggy.TryAddPlayer(new PlayerJoin { speciesId = 25, level = 5, autopilot = false }));
            Assert.False(foggy.TryAddPlayer(new PlayerJoin { speciesId = 25, level = 5 }));
            var tower = RunSession.Start("temporal-tower", 1);
            for (int i = 0; i < 8; i++)
                Assert.True(tower.TryAddPlayer(new PlayerJoin { speciesId = 150, level = 30, autopilot = false }));
            Assert.False(tower.TryAddPlayer(new PlayerJoin { speciesId = 150, level = 30 }));
            var war = RunSession.Start("northern-desert", 1);
            for (int i = 0; i < 20; i++)
                Assert.True(war.TryAddPlayer(new PlayerJoin { speciesId = 25, level = 20, autopilot = false }));
            Assert.False(war.TryAddPlayer(new PlayerJoin { speciesId = 25, level = 20 }));
        }

        [Fact]
        public void BossMeterSplitsDamageByActorAndSkill()
        {
            var session = RunSession.Start("temporal-tower", 11);
            session.rules.noCrit = true;
            session.rules.enemiesIdle = true;
            Assert.True(session.TryAddPlayer(new PlayerJoin { speciesId = 6, level = 30, autopilot = false }));
            Assert.True(session.TryAddPlayer(new PlayerJoin { speciesId = 9, level = 30, autopilot = false }));
            session.PlaceInRoom(session.players[0].id, "boss");
            session.PlaceInRoom(session.players[1].id, "boss");
            Actor boss = null;
            for (int i = 0; i < session.actors.Count; i++)
                if (session.actors[i].isBoss) boss = session.actors[i];
            Assert.NotNull(boss);
            boss.hp = boss.maxHp = 1000000f;
            for (int n = 0; n < 8; n++)
            {
                session.Tick(0.05f, new[]
                {
                    new PlayerInput { playerId = session.players[0].id, skill = 0, aimX = boss.x, aimZ = boss.z, hasAim = true },
                    new PlayerInput { playerId = session.players[1].id, skill = 1, aimX = boss.x, aimZ = boss.z, hasAim = true }
                });
                session.Tick(0.6f, new PlayerInput[0]);
            }
            var reports = session.meter.Participants().Where(p => p.playerId > 0).ToList();
            Assert.Equal(2, reports.Count);
            foreach (var report in reports)
            {
                float sum = 0f;
                foreach (var skill in report.skills) sum += skill.damage;
                Assert.InRange(Math.Abs(sum - report.damage), 0.0, 0.05);
                Assert.True(report.damage > 0f);
                Assert.Contains(report.skills, s => s.hits > 0 && !string.IsNullOrEmpty(s.move));
            }
            Assert.NotEqual(reports[0].skills[0].skillId, reports[1].skills.Single(s => s.hits > 0).skillId);
        }

        [Fact]
        public void PracticeRunKillsLootsAndExtracts()
        {
            var session = RunSession.Start("practice", 1);
            session.rules.noCrit = true;
            Assert.True(session.TryAddPlayer(new PlayerJoin { speciesId = 6, level = 30, autopilot = true }));
            bool extracted = false;
            for (int i = 0; i < 1600; i++)
            {
                session.Tick(0.05f, null);
                if (session.players[0].extracted) { extracted = true; break; }
                if (session.failed) break;
            }
            Assert.True(extracted, session.StatusLine());
            Assert.True(session.players[0].extractedGold > 0);
            Assert.NotEmpty(session.players[0].extractedItems);
            Assert.Empty(session.players[0].bag);
            Assert.False(session.failed);
        }

        [Fact]
        public void EveryPlayableSpeciesCastsItsTypeSkills()
        {
            var types = new HashSet<PokeType>();
            foreach (var species in SpeciesCatalog.Playable)
            {
                types.Add(species.primary);
                if (species.secondary.HasValue) types.Add(species.secondary.Value);
                var session = RunSession.Start("practice", 3);
                session.rules.noCrit = true;
                session.rules.enemiesIdle = true;
                Assert.True(session.TryAddPlayer(new PlayerJoin
                {
                    speciesId = species.dex, level = 30, autopilot = false
                }));
                var player = session.players[0];
                var basic = SkillTrees.Tree(species.primary)[0];
                Assert.Equal(basic.id, player.actor.skills[0].id);
                Assert.Equal(basic.moveZh, player.actor.skills[0].moveZh);
                Assert.NotEmpty(player.actor.hotbar);
                foreach (var node in player.actor.hotbar)
                {
                    Assert.True(species.HasType(node.type, null));
                    Assert.False(node.passive);
                    Assert.True(player.build.Rank(node.id) >= 1);
                }
                session.PlaceInRoom(player.id, "yard");
                Actor enemy = null;
                for (int i = 0; i < session.actors.Count; i++)
                    if (session.actors[i].team == 1) enemy = session.actors[i];
                Assert.NotNull(enemy);
                Assert.Equal(SkillTrees.Tree(enemy.species.primary)[0].id, enemy.skills[0].id);
                enemy.hp = enemy.maxHp = 100000f;
                player.actor.x = enemy.x;
                player.actor.z = enemy.z - 1.2f;
                session.Tick(0.05f, new[]
                {
                    new PlayerInput { playerId = player.id, skill = 0, aimX = enemy.x, aimZ = enemy.z, hasAim = true }
                });
                Assert.True(player.actor.cd.ContainsKey(basic.id));
                var hot = player.actor.hotbar[0];
                session.Tick(0.05f, new[]
                {
                    new PlayerInput { playerId = player.id, skill = 1, aimX = enemy.x, aimZ = enemy.z, hasAim = true }
                });
                Assert.True(player.actor.cd.ContainsKey(hot.id));
            }
            Assert.Equal(15, types.Count);
        }

        [Fact]
        public void WornRunDropsLeaveWithYouOrVanishOnDeath()
        {
            var session = RunSession.Start("practice", 8);
            Assert.True(session.TryAddPlayer(new PlayerJoin { speciesId = 6, level = 12, autopilot = false }));
            var player = session.players[0];
            var fang = LootRoller.Plain("sharp-fang");
            fang.foundThisRun = true;
            player.bag.Add(fang);
            var tooSoon = LootRoller.Plain("dragon-blade");
            tooSoon.foundThisRun = true;
            player.bag.Add(tooSoon);
            float before = player.actor.stats.weaponDamage;
            Assert.False(session.TryWear(player.id, 1));
            Assert.True(session.TryWear(player.id, 0));
            Assert.True(player.actor.stats.weaponDamage > before + 10f);
            Assert.Equal("尖牙", player.worn["Weapon"].nameZh);
            Assert.True(player.worn["Weapon"].foundThisRun);
            session.PlaceInRoom(player.id, "exit");
            Assert.True(session.TryExtract(player.id));
            Assert.Contains(player.extractedItems, item => item.nameZh == "尖牙" && !item.foundThisRun);
            Assert.False(player.worn.ContainsKey("Weapon"));
            Assert.Empty(player.bag);

            session = RunSession.Start("practice", 9);
            Assert.True(session.TryAddPlayer(new PlayerJoin { speciesId = 25, level = 8, autopilot = false }));
            player = session.players[0];
            var cap = LootRoller.Plain("leaf-cap");
            cap.foundThisRun = true;
            player.bag.Add(cap);
            Assert.True(session.TryWear(player.id, 0));
            Assert.Equal("叶帽", player.worn["Helm"].nameZh);
            session.Kill(player.id);
            Assert.False(player.worn.ContainsKey("Helm"));
            Assert.Empty(player.bag);
            Assert.Empty(player.extractedItems);
        }

        [Fact]
        public void FoggyForestRunLootsWearsAndExtracts()
        {
            var session = RunSession.Start("foggy-forest", 1);
            session.rules.noCrit = true;
            Assert.True(session.TryAddPlayer(new PlayerJoin { speciesId = 6, level = 20, autopilot = true }));
            bool extracted = false;
            for (int i = 0; i < 12000; i++)
            {
                session.Tick(0.05f, null);
                if (session.players[0].extracted) { extracted = true; break; }
                if (session.failed) break;
            }
            Assert.True(extracted, session.StatusLine());
            Assert.True(session.ChestOpened("apple"));
            Assert.NotEmpty(session.players[0].extractedItems);
            foreach (var item in session.players[0].extractedItems)
                Assert.False(item.foundThisRun);
            Assert.Empty(session.players[0].bag);
            Assert.True(session.players[0].extractedGold >= 80);
            Assert.DoesNotContain(session.players[0].worn.Values, item => item.foundThisRun);
        }

        [Fact]
        public void HiddenChestOpensAfterThePuzzle()
        {
            var session = RunSession.Start("dark-hill", 4);
            session.rules.enemiesIdle = true;
            Assert.True(session.TryAddPlayer(new PlayerJoin { speciesId = 94, level = 30, autopilot = false }));
            var player = session.players[0];
            session.PlaceInRoom(player.id, "seal");
            session.Tick(0.05f, new[] { new PlayerInput { playerId = player.id, skill = -1, interact = true } });
            Assert.True(session.puzzleSolved);
            session.PlaceInRoom(player.id, "cache");
            int before = player.bag.Count;
            session.Tick(0.05f, new[] { new PlayerInput { playerId = player.id, skill = -1, interact = true } });
            Assert.True(session.ChestOpened("cache"));
            Assert.Equal(before + 2, player.bag.Count);
            Assert.All(player.bag, item => Assert.True(item.foundThisRun));
        }

        [Fact]
        public void MetaIdsMatchTheDesignContract()
        {
            Assert.True(MetaCatalog.UniqueIds(MetaCatalog.Shop.Select(s => s.id)));
            Assert.Contains(MetaCatalog.Shop, s => s.id == "potion" && s.price == 50 && s.currency == MetaCatalog.ExplorerCoin);
            Assert.DoesNotContain(MetaCatalog.Shop, s => s.price <= 0);
            Assert.True(MetaCatalog.UniqueIds(MetaCatalog.Achievements.Select(a => a.id)));
            Assert.Contains(MetaCatalog.Achievements, a => a.id == "first-extract");
            Assert.Equal(16, MetaCatalog.Achievements.Length);
            Assert.Contains("boss-damage", MetaCatalog.Leaderboards);
            Assert.Contains("world-damage", MetaCatalog.Leaderboards);
        }
    }
}
