using System;

namespace MuLike.Server.Game.Skills
{
    public enum SkillCastType
    {
        SingleTarget,
        Self,
        Area
    }

    public sealed class SkillDefinition
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public int ManaCost { get; set; }
        public float CooldownSeconds { get; set; }
        public float CastRange { get; set; }
        public float AreaRadius { get; set; } // For area skills
        public SkillCastType CastType { get; set; }
        public int MinLevel { get; set; }
        public Func<int, int, int> DamageFormula { get; set; } // (attacker.Attack, target.Defense) -> damage

        public SkillDefinition(
            int id,
            string name,
            string description,
            int manaCost,
            float cooldownSeconds,
            float castRange,
            SkillCastType castType,
            int minLevel = 1,
            float areaRadius = 0f)
        {
            Id = id;
            Name = name;
            Description = description;
            ManaCost = manaCost;
            CooldownSeconds = cooldownSeconds;
            CastRange = castRange;
            CastType = castType;
            MinLevel = minLevel;
            AreaRadius = areaRadius;
            DamageFormula = DefaultDamageFormula;
        }

        // Default damage formula: Attack - Defense/2
        private int DefaultDamageFormula(int attack, int defense)
        {
            return Math.Max(1, attack - defense / 2);
        }

        public void SetDamageFormula(Func<int, int, int> formula)
        {
            DamageFormula = formula ?? DefaultDamageFormula;
        }
    }
}
