namespace MuLike.Server.Game.Entities
{
    public sealed class PlayerEntity : Entity
    {
        public int AccountId { get; }
        public string Name { get; }
        public string CharacterClass { get; }
        public int Level { get; private set; }
        public int Experience { get; private set; }
        public int? TargetId { get; set; }

        public PlayerEntity(int id, int accountId, string name, float x, float y, float z, string characterClass = "Warrior")
            : base(id, x, y, z)
        {
            AccountId = accountId;
            Name = name;
            CharacterClass = string.IsNullOrWhiteSpace(characterClass) ? "Warrior" : characterClass;
            Level = 1;
            Experience = 0;
            HpMax = 100;
            HpCurrent = HpMax;
            UpdateStatsForLevel(1);
        }

        public void SetLevel(int level)
        {
            Level = level;
            UpdateStatsForLevel(level);
        }

        private void UpdateStatsForLevel(int level)
        {
            Attack = 10 + (level - 1) * 3;
            Defense = 5 + (level - 1) * 2;
            CriticalChance = 0.1f + (level - 1) * 0.01f; // +1% per level
            HitChance = 0.95f;
            AttackSpeed = 1.0f;
            HpMax = 100 + (level - 1) * 20;
            HpCurrent = HpMax;
        }

        public void AddExperience(int amount)
        {
            if (amount <= 0)
                return;

            Experience += amount;

            while (Experience >= GetRequiredExperienceForLevel(Level))
            {
                Experience -= GetRequiredExperienceForLevel(Level);
                SetLevel(Level + 1);
            }
        }

        private static int GetRequiredExperienceForLevel(int level)
        {
            return 100 + ((level - 1) * 50);
        }

        public void ApplyEquipmentBonuses(int attackBonus, int defenseBonus, int hpBonus)
        {
            Attack += attackBonus;
            Defense += defenseBonus;
            HpMax += hpBonus;
            if (HpCurrent > HpMax)
                HpCurrent = HpMax;
        }
    }
}
