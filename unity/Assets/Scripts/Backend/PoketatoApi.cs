using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Poketato.Backend
{
    public sealed class WalletRow
    {
        public long ExplorerCoin;
        public long GuildMedal;
    }

    public sealed class CharacterRow
    {
        public string Id;
        public int Dex;
        public int Level;
        public long Xp;
        public string MewShift;
    }

    public sealed class TalentRank
    {
        public string SkillId;
        public int Rank;
    }

    public sealed class ItemRow
    {
        public string Id;
        public string BaseId;
        public string Rarity;
        public int ItemLevel;
        public int Durability;
        public string UniqueId;
        public string SetId;
        public string EquippedSlot;
        public JsonValue Affixes;
    }

    public sealed class ShopSku
    {
        public string Id;
        public string Name;
        public long Price;
        public string Kind;
    }

    public sealed class GuildRow
    {
        public string Id;
        public string Name;
        public string EmblemColor;
        public string Role;
    }

    public sealed class AchievementRow
    {
        public string Id;
        public string Name;
        public string Description;
        public long Progress;
        public string UnlockedAt;
        public bool Unlocked => !string.IsNullOrEmpty(UnlockedAt);
    }

    public sealed class LeaderboardRow
    {
        public string Board;
        public string DisplayName;
        public string UserId;
        public long Score;
    }

    public sealed class SkillTotalRow
    {
        public string SkillId;
        public long Casts;
        public long Hits;
        public long Damage;
        public long Healing;
    }

    public sealed class ReportRow
    {
        public string Id;
        public string Kind;
        public string MissionId;
        public string StartedAt;
        public string EndedAt;
    }

    public sealed class LedgerRow
    {
        public string Currency;
        public long Amount;
        public long BalanceAfter;
        public string Reason;
        public string RefType;
        public string RefId;
        public string CreatedAt;
    }

    /// <summary>
    /// Typed calls for the extraction backend. Gear slots are weapon, helm, armor, gloves,
    /// boots, belt, amulet, ring_left and ring_right. Shop prices are explorer-coin.
    /// </summary>
    public sealed class PoketatoApi
    {
        public readonly SupabaseClient Client;
        public PoketatoApi(SupabaseClient client) { Client = client; }

        public string UserId => Client.Session != null ? Client.Session.UserId : null;

        public async Task<WalletRow> Wallet()
        {
            RequireUser();
            JsonValue rows = await Client.Get("wallets", "select=explorer_coin,guild_medal&user_id=eq." + UserId);
            return new WalletRow
            {
                ExplorerCoin = rows[0]["explorer_coin"].AsLong(),
                GuildMedal = rows[0]["guild_medal"].AsLong(),
            };
        }

        public async Task<List<LedgerRow>> Ledger()
        {
            RequireUser();
            JsonValue rows = await Client.Get("currency_ledger",
                "select=currency,amount,balance_after,reason,ref_type,ref_id,created_at&user_id=eq." + UserId + "&order=created_at.desc&limit=50");
            return Map(rows, row => new LedgerRow
            {
                Currency = row["currency"].AsString(),
                Amount = row["amount"].AsLong(),
                BalanceAfter = row["balance_after"].AsLong(),
                Reason = row["reason"].AsString(),
                RefType = row["ref_type"].AsString(),
                RefId = row["ref_id"].AsString(),
                CreatedAt = row["created_at"].AsString(),
            });
        }

        public async Task<List<CharacterRow>> Characters()
        {
            RequireUser();
            JsonValue rows = await Client.Get("characters",
                "select=id,dex,level,xp,mew_shift&user_id=eq." + UserId + "&order=created_at.asc");
            return Map(rows, row => new CharacterRow
            {
                Id = row["id"].AsString(),
                Dex = (int)row["dex"].AsLong(),
                Level = (int)row["level"].AsLong(),
                Xp = row["xp"].AsLong(),
                MewShift = row["mew_shift"].AsString(),
            });
        }

        public async Task<string> CreateCharacter(int dex, string mewShift)
        {
            RequireUser();
            var args = JsonValue.Obj().Set("p_dex", dex);
            if (!string.IsNullOrEmpty(mewShift)) args.Set("p_shift", mewShift);
            JsonValue id = await Client.Rpc("create_character", args);
            return id.AsString();
        }

        public async Task<List<TalentRank>> Talents(string characterId)
        {
            RequireUser();
            JsonValue rows = await Client.Get("talent_ranks",
                "select=skill_id,rank&character_id=eq." + Esc(characterId));
            return Map(rows, row => new TalentRank
            {
                SkillId = row["skill_id"].AsString(),
                Rank = (int)row["rank"].AsLong(),
            });
        }

        public Task SaveTalents(string characterId, JsonValue ranks)
        {
            RequireUser();
            return Client.Rpc("save_talents", JsonValue.Obj()
                .Set("p_character", characterId)
                .Set("p_ranks", ranks ?? JsonValue.Obj()));
        }

        public async Task<List<ItemRow>> Items(string characterId)
        {
            RequireUser();
            JsonValue rows = await Client.Get("items",
                "select=id,base_id,rarity,item_level,durability,affixes,unique_id,set_id,equipped_slot&character_id=eq." + Esc(characterId));
            return Map(rows, row => new ItemRow
            {
                Id = row["id"].AsString(),
                BaseId = row["base_id"].AsString(),
                Rarity = row["rarity"].AsString(),
                ItemLevel = (int)row["item_level"].AsLong(),
                Durability = (int)row["durability"].AsLong(),
                UniqueId = row["unique_id"].AsString(),
                SetId = row["set_id"].AsString(),
                EquippedSlot = row["equipped_slot"].AsString(),
                Affixes = row["affixes"],
            });
        }

        public Task Equip(string itemId, string slot)
        {
            RequireUser();
            var args = JsonValue.Obj().Set("p_item", itemId);
            if (!string.IsNullOrEmpty(slot)) args.Set("p_slot", slot);
            return Client.Rpc("equip_item", args);
        }

        public Task Unequip(string itemId)
        {
            RequireUser();
            return Client.Rpc("unequip_item", JsonValue.Obj().Set("p_item", itemId));
        }

        public async Task<long> Repair(string itemId)
        {
            RequireUser();
            JsonValue cost = await Client.Rpc("repair_item", JsonValue.Obj().Set("p_item", itemId));
            return cost.AsLong();
        }

        public Task Discard(string itemId)
        {
            RequireUser();
            return Client.Rpc("discard_item", JsonValue.Obj().Set("p_item", itemId));
        }

        public async Task<List<ShopSku>> Shop()
        {
            JsonValue rows = await Client.Get("shop_skus", "select=id,name,price,kind&order=price.asc");
            return Map(rows, row => new ShopSku
            {
                Id = row["id"].AsString(),
                Name = row["name"].AsString(),
                Price = row["price"].AsLong(),
                Kind = row["kind"].AsString(),
            });
        }

        public Task Purchase(string sku, string characterId)
        {
            RequireUser();
            var args = JsonValue.Obj().Set("p_sku", sku);
            if (!string.IsNullOrEmpty(characterId)) args.Set("p_character", characterId);
            return Client.Rpc("purchase", args);
        }

        public async Task<string> CreateGuild(string name, string emblemColor)
        {
            RequireUser();
            JsonValue id = await Client.Rpc("create_guild", JsonValue.Obj()
                .Set("p_name", name)
                .Set("p_color", string.IsNullOrEmpty(emblemColor) ? "#888888" : emblemColor));
            return id.AsString();
        }

        public Task JoinGuild(string guildId)
        {
            RequireUser();
            return Client.Rpc("join_guild", JsonValue.Obj().Set("p_guild", guildId));
        }

        public Task LeaveGuild()
        {
            RequireUser();
            return Client.Rpc("leave_guild", JsonValue.Obj());
        }

        public Task Kick(string userId)
        {
            RequireUser();
            return Client.Rpc("kick_member", JsonValue.Obj().Set("p_user", userId));
        }

        public Task SetMemberRole(string userId, string role)
        {
            RequireUser();
            return Client.Rpc("set_member_role", JsonValue.Obj().Set("p_user", userId).Set("p_role", role));
        }

        public Task DisbandGuild()
        {
            RequireUser();
            return Client.Rpc("disband_guild", JsonValue.Obj());
        }

        public async Task<List<GuildRow>> Guilds()
        {
            JsonValue rows = await Client.Get("guilds", "select=id,name,emblem_color&order=created_at.desc&limit=50");
            return Map(rows, row => new GuildRow
            {
                Id = row["id"].AsString(),
                Name = row["name"].AsString(),
                EmblemColor = row["emblem_color"].AsString(),
            });
        }

        public async Task<GuildRow> MyGuild()
        {
            RequireUser();
            JsonValue mine = await Client.Get("guild_members", "select=guild_id,role&user_id=eq." + UserId);
            if (mine.Type != JsonValue.Kind.Array || mine.Array.Count == 0) return null;
            string guildId = mine[0]["guild_id"].AsString();
            JsonValue rows = await Client.Get("guilds", "select=id,name,emblem_color&id=eq." + Esc(guildId));
            if (rows.Type != JsonValue.Kind.Array || rows.Array.Count == 0) return null;
            return new GuildRow
            {
                Id = rows[0]["id"].AsString(),
                Name = rows[0]["name"].AsString(),
                EmblemColor = rows[0]["emblem_color"].AsString(),
                Role = mine[0]["role"].AsString(),
            };
        }

        public async Task<List<AchievementRow>> Achievements()
        {
            JsonValue defs = await Client.Get("achievement_defs", "select=id,name,description&order=id.asc");
            var progress = new Dictionary<string, JsonValue>();
            if (Client.Session != null)
            {
                JsonValue rows = await Client.Get("achievement_progress",
                    "select=achievement_id,progress,unlocked_at&user_id=eq." + UserId);
                if (rows.Type == JsonValue.Kind.Array)
                    for (int i = 0; i < rows.Array.Count; i++) progress[rows[i]["achievement_id"].AsString()] = rows[i];
            }
            return Map(defs, row =>
            {
                JsonValue got;
                progress.TryGetValue(row["id"].AsString(), out got);
                if (got == null) got = JsonValue.Null;
                return new AchievementRow
                {
                    Id = row["id"].AsString(),
                    Name = row["name"].AsString(),
                    Description = row["description"].AsString(),
                    Progress = got["progress"].AsLong(),
                    UnlockedAt = got["unlocked_at"].AsString(),
                };
            });
        }

        public async Task<List<LeaderboardRow>> Leaderboard(string board, int limit)
        {
            if (limit < 1) limit = 1;
            if (limit > 100) limit = 100;
            string order = board == "fastest-foggy" ? "score.asc" : "score.desc";
            JsonValue rows = await Client.Get("leaderboard_entries",
                "select=board,display_name,user_id,score&board=eq." + Esc(board)
                + "&order=" + order + "&limit=" + limit);
            return Map(rows, row => new LeaderboardRow
            {
                Board = row["board"].AsString(),
                DisplayName = row["display_name"].AsString(),
                UserId = row["user_id"].AsString(),
                Score = row["score"].AsLong(),
            });
        }

        public async Task<List<SkillTotalRow>> SkillTotals(string characterId)
        {
            RequireUser();
            JsonValue rows = await Client.Get("character_skill_totals",
                "select=skill_id,casts,hits,damage,healing&character_id=eq." + Esc(characterId) + "&order=damage.desc");
            return Map(rows, row => new SkillTotalRow
            {
                SkillId = row["skill_id"].AsString(),
                Casts = row["casts"].AsLong(),
                Hits = row["hits"].AsLong(),
                Damage = row["damage"].AsLong(),
                Healing = row["healing"].AsLong(),
            });
        }

        public async Task<List<ReportRow>> Reports(int limit)
        {
            RequireUser();
            if (limit < 1) limit = 1;
            if (limit > 50) limit = 50;
            JsonValue rows = await Client.Get("encounter_reports",
                "select=id,kind,mission_id,started_at,ended_at&order=ended_at.desc&limit=" + limit);
            return Map(rows, row => new ReportRow
            {
                Id = row["id"].AsString(),
                Kind = row["kind"].AsString(),
                MissionId = row["mission_id"].AsString(),
                StartedAt = row["started_at"].AsString(),
                EndedAt = row["ended_at"].AsString(),
            });
        }

        public async Task<long> PreviewReward(string missionId, string outcome, long runGold)
        {
            JsonValue value = await Client.Rpc("mission_reward", JsonValue.Obj()
                .Set("p_mission", missionId)
                .Set("p_outcome", outcome)
                .Set("p_run_gold", runGold));
            return value.AsLong();
        }

        void RequireUser()
        {
            if (Client.Session == null || string.IsNullOrEmpty(Client.Session.UserId))
                throw new SupabaseException(401, "login required");
        }

        static string Esc(string value) => Uri.EscapeDataString(value ?? "");

        static List<T> Map<T>(JsonValue rows, Func<JsonValue, T> map)
        {
            var list = new List<T>();
            if (rows == null || rows.Type != JsonValue.Kind.Array || rows.Array == null) return list;
            for (int i = 0; i < rows.Array.Count; i++) list.Add(map(rows[i]));
            return list;
        }
    }
}
