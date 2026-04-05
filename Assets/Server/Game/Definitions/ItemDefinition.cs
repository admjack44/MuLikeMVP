namespace MuLike.Server.Game.Definitions
{
    [System.Flags]
    public enum ExcellentOptionFlags
    {
        None = 0,
        BonusDamage = 1 << 0,
        BonusDefense = 1 << 1,
        BonusHp = 1 << 2,
        BonusCritChance = 1 << 3
    }

    public enum ItemStackRule
    {
        None,
        ByItemId,
        ByItemAndEnhancement
    }

    public sealed class ItemDefinition
    {
        // --- Identity ---
        public int ItemId { get; set; }
        public string Name { get; set; }
        public string Type { get; set; }

        // --- Restrictions ---
        public int Rarity { get; set; }                      // 1 = Common … 6 = Mythic
        public int RequiredLevel { get; set; }
        public bool IsTwoHanded { get; set; }
        public string[] ClassRestrictions { get; set; } = System.Array.Empty<string>();

        // --- Stack rules ---
        public bool IsStackable { get; set; }
        public int MaxStack { get; set; } = 1;
        public ItemStackRule StackRule { get; set; } = ItemStackRule.None;

        // --- Equipment slots ---
        // Primary slot string kept for EquipmentSystem backward compat.
        public string EquipSlot { get; set; }
        // Full list of slots the item is valid in (e.g. ring items accept RingLeft and RingRight).
        public string[] EquipSlots { get; set; } = System.Array.Empty<string>();

        // --- Base weapon/armor stats (intrinsic to the item level/type) ---
        public int MinDamage { get; set; }
        public int MaxDamage { get; set; }
        public int AttackSpeed { get; set; }
        public int MagicPower { get; set; }
        public int Defense { get; set; }
        public int BlockRate { get; set; }
        public int MoveBonus { get; set; }

        // --- Equip stat bonuses (passive bonuses while the item is worn) ---
        public int BonusAttack { get; set; }
        public int BonusDefense { get; set; }
        public int BonusHp { get; set; }
        public int BonusAttackRate { get; set; }
        public int BonusMana { get; set; }
        public int BonusSpellPower { get; set; }
        public int BonusMoveSpeed { get; set; }
        public int DamageAbsorb { get; set; }
        public int DamageBoostPct { get; set; }
        public int PetDamageBonus { get; set; }
        public int PetDefenseBonus { get; set; }
        public bool AutoLoot { get; set; }

        // --- Attribute requirements to equip ---
        public int RequiredStrength { get; set; }
        public int RequiredAgility { get; set; }
        public int RequiredEnergy { get; set; }
        public int RequiredCommand { get; set; }

        // --- Excellent options ---
        public ExcellentOptionFlags AllowedExcellentOptions { get; set; }

        // --- Socket system ---
        public bool AllowSockets { get; set; }
        public int MaxSockets { get; set; }

        // --- Economy ---
        public int SellValue { get; set; }
    }
}
