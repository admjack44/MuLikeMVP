using MuLike.Data.Catalogs;

namespace MuLike.Systems
{
    public static class ItemPowerScoreCalculator
    {
        public static int Calculate(ItemDefinition item)
        {
            if (item == null)
                return 0;

            int score = 0;
            score += (item.RequiredLevel * 8);
            score += item.BasicStats.MinDamage * 2;
            score += item.BasicStats.MaxDamage * 2;
            score += item.BasicStats.Defense * 3;
            score += item.BasicStats.MagicPower * 2;
            score += item.StatBonuses.DamageBoost * 4;
            score += item.StatBonuses.Hp;
            score += item.StatBonuses.Mana;
            score += item.StatBonuses.SpellPower * 3;
            score += item.StatBonuses.AttackRate * 2;

            if (item.Rarity >= ItemRarity.Epic)
                score += 120;

            if (item.IsTwoHanded)
                score += 40;

            return score;
        }

        public static int CalculateCharacterPower(
            InventoryClientSystem inventory,
            EquipmentClientSystem equipment,
            CatalogResolver catalog)
        {
            int score = 0;

            if (equipment != null)
            {
                foreach (var pair in equipment.Equipped)
                {
                    if (pair.Value.IsEmpty)
                        continue;

                    if (catalog != null && catalog.TryGetItemDefinition(pair.Value.Item.ItemId, out ItemDefinition def))
                        score += Calculate(def);
                    else
                        score += 20;
                }
            }

            if (inventory != null)
            {
                for (int i = 0; i < inventory.Slots.Count; i++)
                {
                    InventoryClientSystem.InventorySlot slot = inventory.Slots[i];
                    if (slot.IsEmpty)
                        continue;

                    if (catalog != null && catalog.TryGetItemDefinition(slot.ItemId, out ItemDefinition def))
                        score += Calculate(def) / 5;
                    else
                        score += 3;
                }
            }

            return score;
        }
    }
}
