using System.Collections.Generic;
using MuLike.Data.Catalogs;
using ClientItemDefinition = MuLike.Data.Catalogs.ItemDefinition;
using ClientExcellentFlags = MuLike.Data.Catalogs.ExcellentOptionFlags;
using ClientStackRule = MuLike.Data.Catalogs.ItemStackRule;

namespace MuLike.Server.Game.Definitions
{
    /// <summary>
    /// Bridges the Unity client item catalog (loaded from Resources/Data/Items JSON) into
    /// the server's ItemDatabase. Call once from ServerApplication.CreateDefault() so that
    /// the server and client share one authoritative definition source.
    ///
    /// ID integrity is enforced by ItemCatalogSyncPolicy ranges; this bridge does NOT relax
    /// those constraints — items outside known ranges are still loaded but flagged by the policy.
    /// </summary>
    public static class ServerItemCatalogBridge
    {
        /// <summary>
        /// Loads all items from the client json catalog and populates the server ItemDatabase.
        /// Falls back to the database's built-in seed data on load failure.
        /// </summary>
        public static void PopulateFromClientCatalog(ItemDatabase database)
        {
            var service = new ItemCatalogService();
            ItemCatalogService.LoadReport report = service.LoadOrReload();

            if (report.HasErrors || report.ItemCount == 0)
            {
                UnityEngine.Debug.LogWarning(
                    $"[ServerItemCatalogBridge] Catalog had errors or was empty (count={report.ItemCount}). " +
                    "Server ItemDatabase will use built-in seed data.");
                return;
            }

            var converted = new List<ItemDefinition>(report.ItemCount);
            foreach (KeyValuePair<int, ClientItemDefinition> kvp in service.DefinitionsById)
                converted.Add(ToServerDefinition(kvp.Value));

            database.Populate(converted);

            UnityEngine.Debug.Log(
                $"[ServerItemCatalogBridge] Populated ItemDatabase with {converted.Count} items from client catalog.");
        }

        public static ItemDefinition ToServerDefinition(ClientItemDefinition src)
        {
            string primarySlot = src.AllowedEquipSlots != null && src.AllowedEquipSlots.Count > 0
                ? src.AllowedEquipSlots[0].ToString()
                : string.Empty;

            string[] allSlots = ToSlotStrings(src);

            return new ItemDefinition
            {
                ItemId = src.ItemId,
                Name = src.Name,
                Type = src.Type,
                Rarity = (int)src.Rarity + 1,   // enum 0-based → 1 = Common … 6 = Mythic
                RequiredLevel = src.RequiredLevel,
                IsTwoHanded = src.IsTwoHanded,
                ClassRestrictions = ToClassStrings(src),

                IsStackable = src.Stackable,
                MaxStack = src.MaxStack,
                StackRule = (ItemStackRule)(int)src.StackRule,  // same ordinal values

                EquipSlot = primarySlot,
                EquipSlots = allSlots,

                MinDamage = src.BasicStats.MinDamage,
                MaxDamage = src.BasicStats.MaxDamage,
                AttackSpeed = src.BasicStats.AttackSpeed,
                MagicPower = src.BasicStats.MagicPower,
                Defense = src.BasicStats.Defense,
                BlockRate = src.BasicStats.BlockRate,
                MoveBonus = src.BasicStats.MoveBonus,

                BonusHp = src.StatBonuses.Hp,
                BonusAttackRate = src.StatBonuses.AttackRate,
                BonusMana = src.StatBonuses.Mana,
                BonusSpellPower = src.StatBonuses.SpellPower,
                BonusMoveSpeed = src.StatBonuses.MoveSpeed,
                DamageAbsorb = src.StatBonuses.DamageAbsorb,
                DamageBoostPct = src.StatBonuses.DamageBoost,
                PetDamageBonus = src.StatBonuses.PetDamage,
                PetDefenseBonus = src.StatBonuses.PetDefense,
                AutoLoot = src.StatBonuses.AutoLoot,

                RequiredStrength = src.StatRequirements.Strength,
                RequiredAgility = src.StatRequirements.Agility,
                RequiredEnergy = src.StatRequirements.Energy,
                RequiredCommand = src.StatRequirements.Command,

                AllowedExcellentOptions = (ExcellentOptionFlags)(int)src.AllowedExcellentOptions,
                AllowSockets = src.AllowSockets,
                MaxSockets = src.MaxSockets,
                SellValue = src.SellValue
            };
        }

        private static string[] ToClassStrings(ClientItemDefinition src)
        {
            if (src.AllowedClasses == null || src.AllowedClasses.Count == 0)
                return new[] { "Any" };

            var result = new string[src.AllowedClasses.Count];
            for (int i = 0; i < src.AllowedClasses.Count; i++)
                result[i] = src.AllowedClasses[i].ToString();

            return result;
        }

        private static string[] ToSlotStrings(ClientItemDefinition src)
        {
            if (src.AllowedEquipSlots == null || src.AllowedEquipSlots.Count == 0)
                return System.Array.Empty<string>();

            var result = new string[src.AllowedEquipSlots.Count];
            for (int i = 0; i < src.AllowedEquipSlots.Count; i++)
                result[i] = src.AllowedEquipSlots[i].ToString();

            return result;
        }
    }
}
