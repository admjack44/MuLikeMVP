using System;
using System.Collections.Generic;
using MuLike.Server.Game.Entities;

namespace MuLike.Server.Game.Systems
{
    public sealed class AutoAttackSystem
    {
        private class AttackState
        {
            public Entity Attacker { get; set; }
            public Entity Target { get; set; }
            public float AttackTimer { get; set; }
        }

        private readonly Dictionary<int, AttackState> _activeAttacks = new();

        public event Action<int, int, int, bool> AttackPerformed; // attackerId, targetId, damage, isCritical

        public void StartAutoAttack(Entity attacker, Entity target)
        {
            if (attacker == null || target == null)
                return;

            _activeAttacks[attacker.Id] = new AttackState
            {
                Attacker = attacker,
                Target = target,
                AttackTimer = 0f
            };
        }

        public void StopAutoAttack(Entity attacker)
        {
            if (attacker != null)
                _activeAttacks.Remove(attacker.Id);
        }

        public void Update(float deltaTime, CombatSystem combatSystem, TargetingSystem targetingSystem)
        {
            var attackersToRemove = new List<int>();

            foreach (var kvp in _activeAttacks)
            {
                int attackerId = kvp.Key;
                var state = kvp.Value;

                if (state.Attacker == null || state.Attacker.IsDead() || 
                    state.Target == null || state.Target.IsDead())
                {
                    attackersToRemove.Add(attackerId);
                    continue;
                }

                // Check if target is still in range
                if (!targetingSystem.IsInRangeOfTarget(state.Attacker, state.Target))
                {
                    attackersToRemove.Add(attackerId);
                    continue;
                }

                // Update timer
                float attackInterval = 1f / state.Attacker.AttackSpeed;
                state.AttackTimer += deltaTime;

                // If enough time passed, perform attack
                if (state.AttackTimer >= attackInterval)
                {
                    var result = combatSystem.CalculateAttack(state.Attacker, state.Target);
                    
                    if (result.HitSuccess)
                    {
                        AttackPerformed?.Invoke(
                            state.Attacker.Id,
                            state.Target.Id,
                            result.DamageDeal,
                            result.IsCritical);
                    }

                    state.AttackTimer = 0f;
                }
            }

            // Remove dead/out-of-range attackers
            foreach (int id in attackersToRemove)
                _activeAttacks.Remove(id);
        }

        public bool IsAutoAttacking(Entity entity)
        {
            return entity != null && _activeAttacks.ContainsKey(entity.Id);
        }
    }
}
