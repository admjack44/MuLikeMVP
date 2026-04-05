using MuLike.Server.Game.Entities;
using UnityEngine;

namespace MuLike.Server.Game.Systems
{
    public sealed class TargetingSystem
    {
        private const float DefaultCombatRange = 10f;

        public bool SetTarget(Entity entity, Entity target)
        {
            if (entity == null || target == null || target.IsDead())
                return false;

            float distance = Vector3.Distance(
                new Vector3(entity.X, entity.Y, entity.Z),
                new Vector3(target.X, target.Y, target.Z));

            if (distance > DefaultCombatRange)
                return false;

            if (entity is PlayerEntity player)
                player.TargetId = target.Id;
            else if (entity is MonsterEntity monster)
                monster.TargetId = target.Id;

            return true;
        }

        public Entity GetTarget(Entity entity, System.Collections.Generic.IReadOnlyCollection<Entity> availableEntities)
        {
            int? targetId = null;

            if (entity is PlayerEntity player)
                targetId = player.TargetId;
            else if (entity is MonsterEntity monster)
                targetId = monster.TargetId;

            if (!targetId.HasValue)
                return null;

            foreach (var e in availableEntities)
            {
                if (e.Id == targetId.Value && !e.IsDead())
                    return e;
            }

            return null;
        }

        public bool IsInRangeOfTarget(Entity entity, Entity target, float range = DefaultCombatRange)
        {
            if (entity == null || target == null)
                return false;

            float distance = Vector3.Distance(
                new Vector3(entity.X, entity.Y, entity.Z),
                new Vector3(target.X, target.Y, target.Z));

            return distance <= range;
        }

        // Simple aggro: if player within aggro range, monster targets player
        public void SimpleAggro(MonsterEntity monster, PlayerEntity player)
        {
            if (monster == null || player == null || player.IsDead())
                return;

            float distance = Vector3.Distance(
                new Vector3(monster.X, monster.Y, monster.Z),
                new Vector3(player.X, player.Y, player.Z));

            if (distance <= monster.AggroRadius)
            {
                monster.TargetId = player.Id;
            }
        }

        public void ClearTarget(Entity entity)
        {
            if (entity is PlayerEntity player)
                player.TargetId = null;
            else if (entity is MonsterEntity monster)
                monster.TargetId = null;
        }
    }
}
