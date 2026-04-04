using System;

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
    }
}
