using System;

namespace MuLike.Shared.Content
{
    [Serializable]
    public sealed class GameContentBundleDto
    {
        public string version;
        public string exportedAtUtc;
        public ContentItemDto[] items;
        public ContentMonsterDto[] monsters;
        public ContentSkillDto[] skills;
        public ContentMapDto[] maps;
        public ContentSpawnTableDto[] spawnTables;
        public ContentDropTableDto[] dropTables;
        public ContentBalanceDto balance;
    }

    [Serializable]
    public sealed class ContentItemDto
    {
        public int itemId;
        public string name;
        public string type;
        public int rarity;
        public int requiredLevel;
        public bool isTwoHanded;
        public string[] classRestrictions;
        public bool isStackable;
        public int maxStack;
        public int stackRule;
        public string equipSlot;
        public string[] equipSlots;
        public int minDamage;
        public int maxDamage;
        public int attackSpeed;
        public int magicPower;
        public int defense;
        public int blockRate;
        public int moveBonus;
        public int bonusAttack;
        public int bonusDefense;
        public int bonusHp;
        public int bonusAttackRate;
        public int bonusMana;
        public int bonusSpellPower;
        public int bonusMoveSpeed;
        public int damageAbsorb;
        public int damageBoostPct;
        public int petDamageBonus;
        public int petDefenseBonus;
        public bool autoLoot;
        public int requiredStrength;
        public int requiredAgility;
        public int requiredEnergy;
        public int requiredCommand;
        public int allowedExcellentOptions;
        public bool allowSockets;
        public int maxSockets;
        public int sellValue;
    }

    [Serializable]
    public sealed class ContentMonsterDto
    {
        public int monsterId;
        public string name;
        public int level;
        public int hpMax;
        public int attack;
        public int defense;
        public float aggroRadius;
        public float chaseRadius;
        public float leashRadius;
        public float attackRange;
        public float moveSpeed;
        public float respawnSeconds;
        public int expReward;
        public string dropTableId;
    }

    [Serializable]
    public sealed class ContentSkillDto
    {
        public int skillId;
        public string name;
        public string description;
        public int manaCost;
        public float cooldownSeconds;
        public float castRange;
        public float areaRadius;
        public int castType;
        public int minLevel;
        public int baseDamage;
        public float attackScale;
        public float defenseScale;
    }

    [Serializable]
    public sealed class ContentMapDto
    {
        public int mapId;
        public string mapName;
        public string sceneName;
        public string biome;
        public int recommendedLevel;
    }

    [Serializable]
    public sealed class ContentSpawnTableDto
    {
        public string tableId;
        public int mapId;
        public ContentSpawnEntryDto[] entries;
    }

    [Serializable]
    public sealed class ContentSpawnEntryDto
    {
        public int monsterId;
        public float x;
        public float y;
        public float z;
        public int count;
    }

    [Serializable]
    public sealed class ContentDropTableDto
    {
        public string tableId;
        public ContentDropEntryDto[] entries;
    }

    [Serializable]
    public sealed class ContentDropEntryDto
    {
        public int itemId;
        public int chancePercent;
        public int minQuantity;
        public int maxQuantity;
    }

    [Serializable]
    public sealed class ContentBalanceDto
    {
        public float damageMultiplier;
        public float defenseMultiplier;
        public float skillDamageMultiplier;
        public float expMultiplier;
        public float dropRateMultiplier;
        public float zenMultiplier;
        public float respawnSpeedMultiplier;
        public float eliteSpawnChance;
    }
}
