using System;
using MuLike.Server.Game.Entities;
using UnityEngine;

namespace MuLike.Server.Game.Systems
{
    public sealed class CombatSystem
    {
        public struct AttackResult
        {
            public bool HitSuccess { get; set; }
            public bool IsCritical { get; set; }
            public int DamageDeal { get; set; }
            public string Message { get; set; }
        }

        public AttackResult CalculateAttack(Entity attacker, Entity target)
        {
            var result = new AttackResult { HitSuccess = false };

            if (attacker == null || target == null || target.IsDead())
                return result;

            // Roll for hit chance
            if (UnityEngine.Random.value > attacker.HitChance)
            {
                result.Message = "Miss!";
                return result;
            }

            // Roll for critical
            bool isCritical = UnityEngine.Random.value < attacker.CriticalChance;

            // Base damage from attack stat, modified by attacker's attack vs target's defense
            int baseDamage = attacker.Attack;
            int defenseMitigation = Mathf.Max(0, target.Defense / 2); // Simple def mitigation
            int finalDamage = Mathf.Max(1, baseDamage - defenseMitigation);

            if (isCritical)
            {
                finalDamage = Mathf.RoundToInt(finalDamage * 1.5f);
            }

            int appliedDamage = target.ApplyDamage(finalDamage);

            if (attacker is PlayerEntity player && target is MonsterEntity monster)
            {
                monster.RegisterDamageFromPlayer(player.Id);
            }

            result.HitSuccess = true;
            result.IsCritical = isCritical;
            result.DamageDeal = appliedDamage;
            result.Message = isCritical ? "Critical Hit!" : "Hit!";

            return result;
        }

        public int ApplyDamage(Entity source, Entity target, int damage)
        {
            if (source == null || target == null) return 0;
            return target.ApplyDamage(damage);
        }
    }
}
