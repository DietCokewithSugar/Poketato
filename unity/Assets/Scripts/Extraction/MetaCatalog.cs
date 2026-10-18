using System.Collections.Generic;

namespace Poketato.Extraction
{
    public sealed class ShopItem
    {
        public string id;
        public string nameZh;
        public string currency;
        public int price;
    }

    public sealed class AchievementDef
    {
        public string id;
        public string nameZh;
    }

    public static class MetaCatalog
    {
        public static readonly string ExplorerCoin = "explorer-coin";
        public static readonly string GuildMedal = "guild-medal";

        public static readonly ShopItem[] Shop =
        {
            new ShopItem { id = "potion", nameZh = "伤药", currency = "explorer-coin", price = 50 },
            new ShopItem { id = "antidote", nameZh = "解毒药", currency = "explorer-coin", price = 40 },
            new ShopItem { id = "repair-hammer", nameZh = "修理锤", currency = "explorer-coin", price = 80 },
            new ShopItem { id = "respec", nameZh = "天赋重置", currency = "explorer-coin", price = 200 },
            new ShopItem { id = "stash-page", nameZh = "仓库扩页", currency = "explorer-coin", price = 500 },
            new ShopItem { id = "scarf-placeholder", nameZh = "占位围巾", currency = "explorer-coin", price = 300 }
        };

        public static readonly AchievementDef[] Achievements =
        {
            new AchievementDef { id = "first-extract", nameZh = "活着出来" },
            new AchievementDef { id = "extract-10", nameZh = "老手" },
            new AchievementDef { id = "die-rich", nameZh = "带不走" },
            new AchievementDef { id = "boss-venusaur", nameZh = "雾心" },
            new AchievementDef { id = "boss-mewtwo", nameZh = "塔顶" },
            new AchievementDef { id = "guild-clear", nameZh = "演武" },
            new AchievementDef { id = "world-join", nameZh = "上沙场" },
            new AchievementDef { id = "meter-top", nameZh = "扛输出" },
            new AchievementDef { id = "unique-drop", nameZh = "暗金" },
            new AchievementDef { id = "set-volcano", nameZh = "火山之心" },
            new AchievementDef { id = "no-death-hidden", nameZh = "隐藏之地" },
            new AchievementDef { id = "keystone", nameZh = "终极技" },
            new AchievementDef { id = "mew-shift", nameZh = "变身" },
            new AchievementDef { id = "early-out", nameZh = "见好就收" },
            new AchievementDef { id = "repair-broke", nameZh = "碎掉了" },
            new AchievementDef { id = "party-four", nameZh = "满编" }
        };

        public static readonly string[] Leaderboards =
        {
            "extract-count", "fastest-foggy", "boss-damage", "world-damage", "richest"
        };

        public static bool UniqueIds(IEnumerable<string> ids)
        {
            var seen = new HashSet<string>();
            foreach (var id in ids)
            {
                if (string.IsNullOrEmpty(id) || !seen.Add(id)) return false;
            }
            return true;
        }
    }
}
