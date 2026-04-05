using System;
using System.Collections.Generic;
using MuLike.Server.Game.Entities;
using MuLike.Server.Game.Skills;
using UnityEngine;

namespace MuLike.Server.Game.Systems
{
    public sealed class SkillExecutionResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public int DamageDealt { get; set; }
        public List<int> AffectedTargets { get; set; } = new();
    }

    public sealed class SkillSystem
    {
        private readonly SkillDatabase _skillDatabase;
        private readonly SkillCooldownManager _cooldownManager;
        private readonly CombatSystem _combatSystem;

        public event Action<int, int, int> SkillExecuted; // playerId, skillId, damage

        public SkillSystem(SkillDatabase skillDatabase, CombatSystem combatSystem)
        {
            _skillDatabase = skillDatabase ?? throw new ArgumentNullException(nameof(skillDatabase));
            _combatSystem = combatSystem ?? throw new ArgumentNullException(nameof(combatSystem));
            _cooldownManager = new SkillCooldownManager();
        }

        public bool CanCast(int manaCurrent, int manaCost)
        {
            return manaCurrent >= manaCost;
        }

        public SkillExecutionResult ValidateAndExecuteSkill(
            Entity caster,
            int skillId,
            Entity target,
            IReadOnlyCollection<Entity> allEntitiesInMap)
        {
            var result = new SkillExecutionResult();

            if (caster == null || caster.IsDead())
            {
                result.Message = "Caster is dead";
                return result;
            }

            if (!_skillDatabase.TryGetSkill(skillId, out var skillDef))
            {
                result.Message = "Skill not found";
                return result;
            }

            // Check level requirement
            if (caster is PlayerEntity player && player.Level < skillDef.MinLevel)
            {
                result.Message = $"Requires level {skillDef.MinLevel}";
                return result;
            }

            // Check mana
            if (!CanCast(caster.HpCurrent, skillDef.ManaCost)) // TODO: Add mana pool to Entity
            {
                result.Message = "Not enough mana";
                return result;
            }

            // Check cooldown
            short casterId = (short)caster.Id;
            if (_cooldownManager.IsOnCooldown(casterId, skillId))
            {
                float remaining = _cooldownManager.GetRemainingCooldown(casterId, skillId);
                result.Message = $"Skill on cooldown ({remaining:F1}s)";
                return result;
            }

            // Execute skill based on cast type
            switch (skillDef.CastType)
            {
                case SkillCastType.SingleTarget:
                    result = ExecuteSingleTargetSkill(caster, skillDef, target);
                    break;
                case SkillCastType.Self:
                    result = ExecuteSelfSkill(caster, skillDef);
                    break;
                case SkillCastType.Area:
                    result = ExecuteAreaSkill(caster, skillDef, allEntitiesInMap);
                    break;
            }

            if (result.Success)
            {
                // Apply cooldown
                _cooldownManager.StartCooldown(casterId, skillId, skillDef.CooldownSeconds);
                SkillExecuted?.Invoke(caster.Id, skillId, result.DamageDealt);
            }

            return result;
        }

        private SkillExecutionResult ExecuteSingleTargetSkill(Entity caster, SkillDefinition skillDef, Entity target)
        {
            var result = new SkillExecutionResult();

            if (target == null || target.IsDead())
            {
                result.Message = "Target not found or dead";
                return result;
            }

            // Validate range
            float distance = Vector3.Distance(
                new Vector3(caster.X, caster.Y, caster.Z),
                new Vector3(target.X, target.Y, target.Z));

            if (distance > skillDef.CastRange)
            {
                result.Message = "Target out of range";
                return result;
            }

            // Calculate damage using skill's formula
            int damage = skillDef.DamageFormula(caster.Attack, target.Defense);
            int appliedDamage = target.ApplyDamage(damage);

            if (caster is PlayerEntity player && target is MonsterEntity monster)
            {
                monster.RegisterDamageFromPlayer(player.Id);
            }

            result.Success = true;
            result.DamageDealt = appliedDamage;
            result.Message = "Skill cast successfully";
            result.AffectedTargets.Add(target.Id);

            return result;
        }

        private SkillExecutionResult ExecuteSelfSkill(Entity caster, SkillDefinition skillDef)
        {
            var result = new SkillExecutionResult();

            // Apply benefit to self (for now, heal as placeholder)
            int healAmount = skillDef.DamageFormula(caster.Attack, 0);
            int actualHeal = caster.Heal(healAmount);

            result.Success = true;
            result.DamageDealt = actualHeal;
            result.Message = "Self skill cast successfully";
            result.AffectedTargets.Add(caster.Id);

            return result;
        }

        private SkillExecutionResult ExecuteAreaSkill(Entity caster, SkillDefinition skillDef, IReadOnlyCollection<Entity> allEntities)
        {
            var result = new SkillExecutionResult();

            if (allEntities == null)
            {
                result.Message = "No entities in area";
                return result;
            }

            int totalDamage = 0;
            foreach (var entity in allEntities)
            {
                if (entity.Id == caster.Id || entity.IsDead())
                    continue;

                // Check if in area radius
                float distance = Vector3.Distance(
                    new Vector3(caster.X, caster.Y, caster.Z),
                    new Vector3(entity.X, entity.Y, entity.Z));

                if (distance <= skillDef.AreaRadius)
                {
                    int damage = skillDef.DamageFormula(caster.Attack, entity.Defense);
                    int appliedDamage = entity.ApplyDamage(damage);

                    if (caster is PlayerEntity player && entity is MonsterEntity monster)
                    {
                        monster.RegisterDamageFromPlayer(player.Id);
                    }

                    totalDamage += appliedDamage;
                    result.AffectedTargets.Add(entity.Id);
                }
            }

            result.Success = result.AffectedTargets.Count > 0;
            result.DamageDealt = totalDamage;
            result.Message = result.Success 
                ? $"Area skill hit {result.AffectedTargets.Count} targets"
                : "No targets in area";

            return result;
        }

        public void Update(float deltaTime)
        {
            _cooldownManager.Update(deltaTime);
        }

        public SkillCooldownManager GetCooldownManager()
        {
            return _cooldownManager;
        }

        public IReadOnlyList<SkillDefinition> GetAvailableSkills(int playerLevel)
        {
            return _skillDatabase.GetSkillsByLevel(playerLevel);
        }
    }
}
