using System;
using MuLike.Server.Game.Entities;

namespace MuLike.Server.Game.Systems
{
    public sealed class DeathSystem
    {
        public event Action<int> EntityDied;

        public bool IsDead(Entity entity)
        {
            return entity.HpCurrent <= 0;
        }

        public void HandleDeath(Entity entity)
        {
            if (entity == null || !IsDead(entity))
                return;

            EntityDied?.Invoke(entity.Id);
        }

        public void RespawnPlayer(PlayerEntity player, float respawnX, float respawnY, float respawnZ)
        {
            if (player == null)
                return;

            player.SetPosition(respawnX, respawnY, respawnZ);
            player.Heal(player.HpMax);
            player.TargetId = null;
        }

        public void RespawnMonster(MonsterEntity monster, float originalX, float originalY, float originalZ)
        {
            if (monster == null)
                return;

            monster.SetPosition(originalX, originalY, originalZ);
            monster.Heal(monster.HpMax);
            monster.TargetId = null;
        }
    }
}
