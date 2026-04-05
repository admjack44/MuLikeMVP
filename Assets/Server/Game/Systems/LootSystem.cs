using System;
using System.Collections.Generic;
using MuLike.Server.Game.Definitions;

namespace MuLike.Server.Game.Systems
{
    public sealed class LootSystem
    {
        private readonly Random _rng = new();

        public bool RollDrop(int chancePercent)
        {
            if (chancePercent <= 0) return false;
            if (chancePercent >= 100) return true;
            return _rng.Next(0, 100) < chancePercent;
        }

        public IReadOnlyList<(int itemId, int quantity)> RollDrops(MonsterDropDefinition[] drops)
        {
            var results = new List<(int itemId, int quantity)>();
            if (drops == null || drops.Length == 0)
                return results;

            for (int i = 0; i < drops.Length; i++)
            {
                var drop = drops[i];
                if (drop == null)
                    continue;

                if (!RollDrop(drop.ChancePercent))
                    continue;

                int minQuantity = Math.Max(1, drop.MinQuantity);
                int maxQuantity = Math.Max(minQuantity, drop.MaxQuantity);
                int quantity = _rng.Next(minQuantity, maxQuantity + 1);
                results.Add((drop.ItemId, quantity));
            }

            return results;
        }
    }
}
