using System;

namespace MuLike.Data.DTO
{
    [Serializable]
    public class ItemManifestDto
    {
        public int version;
        public string catalog;
        public string layout;
        public string note;
        public ItemManifestSourceDto[] sources;
    }

    [Serializable]
    public class ItemManifestSourceDto
    {
        public string file;
        public string[] families;
    }

    [Serializable]
    public class ItemCatalogFileDto
    {
        public string group;
        public ItemDto[] items;
    }

    [Serializable]
    public class ItemDto
    {
        public int id;
        public string name;
        public string type;
        public string subtype;
        public string family;
        public string rarity;
        public int level;

        public int minDamage;
        public int maxDamage;
        public int attackSpeed;
        public int magicPower;
        public int defense;
        public int blockRate;
        public int moveBonus;

        public bool stackable;
        public int maxStack;
        public int sellPrice;

        public string model;
        public string icon;
        public string fxProfile;

        public ItemRequirementsDto requirements;
        public ItemBonusesDto bonuses;
        public ItemRestoreDto restore;
        public ItemUpgradeDto upgrade;
    }

    [Serializable]
    public class ItemRequirementsDto
    {
        public int strength;
        public int agility;
        public int energy;
        public int command;
    }

    [Serializable]
    public class ItemBonusesDto
    {
        public int attackRate;
        public int hp;
        public int mana;
        public int spellPower;
        public int moveSpeed;
        public int damageAbsorb;
        public int damageBoost;
        public int command;
        public int petDamage;
        public int petDefense;

        public bool autoLoot;
    }

    [Serializable]
    public class ItemRestoreDto
    {
        public int hp;
        public int mana;
    }

    [Serializable]
    public class ItemUpgradeDto
    {
        public string target;
        public string effect;
    }
}
