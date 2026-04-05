using System;
using System.Collections.Generic;
using UnityEngine;

namespace MuLike.Server.Game.Skills
{
    public sealed class SkillDatabase
    {
        private readonly Dictionary<int, SkillDefinition> _skills = new();

        public SkillDatabase()
        {
            InitializeDemoSkills();
        }

        private void InitializeDemoSkills()
        {
            // Skill 1: Slash - Single target melee
            var slash = new SkillDefinition(
                id: 1,
                name: "Slash",
                description: "A basic melee slash attack",
                manaCost: 10,
                cooldownSeconds: 1.5f,
                castRange: 2f,
                castType: SkillCastType.SingleTarget,
                minLevel: 1);
            slash.SetDamageFormula((attack, defense) => Math.Max(1, attack - defense / 2));
            _skills[slash.Id] = slash;

            // Skill 2: Fireball - Single target ranged with mana cost
            var fireball = new SkillDefinition(
                id: 2,
                name: "Fireball",
                description: "Hurl a fireball at the target",
                manaCost: 30,
                cooldownSeconds: 3f,
                castRange: 15f,
                castType: SkillCastType.SingleTarget,
                minLevel: 5);
            fireball.SetDamageFormula((attack, defense) => 
            {
                // Fireball scales better with attack, ignores some defense
                int baseDamage = attack + 10;
                int mitigated = Mathf.Max(0, defense / 3);
                return Math.Max(5, baseDamage - mitigated);
            });
            _skills[fireball.Id] = fireball;

            // Skill 3: Whirlwind - Area attack, all nearby enemies
            var whirlwind = new SkillDefinition(
                id: 3,
                name: "Whirlwind",
                description: "Spin rapidly, hitting all nearby enemies",
                manaCost: 50,
                cooldownSeconds: 4f,
                castRange: 1f, // Self-cast origin
                castType: SkillCastType.Area,
                minLevel: 10,
                areaRadius: 8f);
            whirlwind.SetDamageFormula((attack, defense) =>
            {
                // Whirlwind does consistent damage regardless of target defense
                int baseDamage = attack / 2 + 15;
                return Math.Max(3, baseDamage);
            });
            _skills[whirlwind.Id] = whirlwind;
        }

        public bool TryGetSkill(int skillId, out SkillDefinition skill)
        {
            return _skills.TryGetValue(skillId, out skill);
        }

        public void Populate(IReadOnlyList<SkillDefinition> skills)
        {
            if (skills == null || skills.Count == 0)
                return;

            _skills.Clear();
            for (int i = 0; i < skills.Count; i++)
            {
                SkillDefinition skill = skills[i];
                if (skill == null)
                    continue;

                _skills[skill.Id] = skill;
            }
        }

        public void Register(SkillDefinition skill)
        {
            if (skill == null)
                return;

            _skills[skill.Id] = skill;
        }

        public IReadOnlyCollection<SkillDefinition> GetAllSkills()
        {
            return _skills.Values;
        }

        public IReadOnlyList<SkillDefinition> GetSkillsByLevel(int playerLevel)
        {
            var learnable = new List<SkillDefinition>();
            foreach (var skill in _skills.Values)
            {
                if (skill.MinLevel <= playerLevel)
                    learnable.Add(skill);
            }
            return learnable;
        }
    }
}
