using System;
using System.Collections.Generic;

namespace Poketato.Extraction
{
    public sealed class TalentBuild
    {
        readonly Dictionary<string, int> ranks = new Dictionary<string, int>();

        public int Rank(string nodeId)
        {
            int value;
            return ranks.TryGetValue(nodeId, out value) ? value : 0;
        }

        public int PointsIn(PokeType type)
        {
            int total = 0;
            var tree = SkillTrees.Tree(type);
            for (int i = 0; i < tree.Length; i++) total += Rank(tree[i].id);
            return total;
        }

        public int TotalPoints()
        {
            int total = 0;
            foreach (var pair in ranks) total += pair.Value;
            return total;
        }

        public bool CanSpend(SpeciesDef species, PokeType? mewExtra, TalentNode node)
        {
            if (!species.HasType(node.type, mewExtra)) return false;
            if (Rank(node.id) >= node.maxRanks) return false;
            if (PointsIn(node.type) < node.tier * 5) return false;
            if (node.prereq != null && Rank(node.prereq) < 1) return false;
            return true;
        }

        public void Spend(TalentNode node)
        {
            ranks[node.id] = Rank(node.id) + 1;
        }

        public static TalentBuild TowardKeystone(SpeciesDef species, int level)
        {
            var build = new TalentBuild();
            int budget = Math.Min(Math.Max(level, 0), 30);
            int guard = 0;
            while (build.PointsIn(species.primary) < budget && guard++ < 80)
            {
                TalentNode pick = null;
                var tree = SkillTrees.Tree(species.primary);
                int[] chain = { 1, 6, 9, 12, 15, 3, 4, 8 };
                for (int i = 0; i < chain.Length; i++)
                {
                    var node = tree[chain[i]];
                    if (build.CanSpend(species, null, node)) { pick = node; break; }
                }
                if (pick == null)
                {
                    int[] fill = { 0, 2, 5, 7, 10, 11, 13, 14 };
                    for (int i = 0; i < fill.Length; i++)
                    {
                        var node = tree[fill[i]];
                        if (build.CanSpend(species, null, node)) { pick = node; break; }
                    }
                }
                if (pick == null) break;
                build.Spend(pick);
            }
            return build;
        }

        public List<TalentNode> Hotbar(SpeciesDef species, PokeType? mewExtra)
        {
            var known = new List<TalentNode>();
            Collect(species.primary, known);
            if (species.secondary.HasValue) Collect(species.secondary.Value, known);
            if (species.dex == 151 && mewExtra.HasValue && mewExtra.Value != species.primary)
                Collect(mewExtra.Value, known);
            known.Sort(delegate (TalentNode a, TalentNode b)
            {
                int tier = b.tier.CompareTo(a.tier);
                if (tier != 0) return tier;
                return a.slot.CompareTo(b.slot);
            });
            if (known.Count > 4) known.RemoveRange(4, known.Count - 4);
            return known;
        }

        void Collect(PokeType type, List<TalentNode> into)
        {
            var tree = SkillTrees.Tree(type);
            for (int i = 0; i < tree.Length; i++)
            {
                var node = tree[i];
                if (node.passive || node.slot == 0) continue;
                if (Rank(node.id) > 0) into.Add(node);
            }
        }
    }
}
