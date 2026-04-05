using System;
using System.Collections.Generic;
using MuLike.Classes;
using UnityEngine;

namespace MuLike.Inventory
{
    /// <summary>
    /// MU-style equipment runtime layer.
    ///
    /// Features:
    /// - Requested equipment slots and assignment.
    /// - Stat comparison arrows (green/red) for touch preview.
    /// - Auto-equip best items by class and weighted score.
    /// - Character 3D preview rotate via swipe input.
    /// </summary>
    public sealed class EquipmentSystem : MonoBehaviour
    {
        public enum EquipmentSlot
        {
            Helm,
            Armor,
            Pants,
            Gloves,
            Boots,
            WeaponLeft,
            WeaponRight,
            Wings,
            Necklace,
            RingLeft,
            RingRight
        }

        public enum ComparisonArrow
        {
            Neutral,
            Up,
            Down
        }

        [Serializable]
        public struct ComparisonLine
        {
            public string statName;
            public int currentValue;
            public int candidateValue;
            public ComparisonArrow arrow;
            public int delta;
        }

        [Header("Dependencies")]
        [SerializeField] private InventoryManager _inventory;
        [SerializeField] private Transform _characterPreviewRoot;

        [Header("Preview")]
        [SerializeField] private float _previewYawSpeed = 0.22f;

        [Header("Class Weights")]
        [SerializeField] private MuClassId _currentClass = MuClassId.DarkKnight;

        private readonly Dictionary<EquipmentSlot, InventoryManager.InventoryItem> _equipped = new();

        public IReadOnlyDictionary<EquipmentSlot, InventoryManager.InventoryItem> Equipped => _equipped;

        public event Action<EquipmentSlot, InventoryManager.InventoryItem> OnEquippedChanged;
        public event Action<IReadOnlyList<ComparisonLine>> OnComparisonReady;
        public event Action<float> OnPreviewRotated;

        public bool TryEquip(EquipmentSlot slot, InventoryManager.InventoryItem item)
        {
            if (item == null)
                return false;

            _equipped[slot] = item;
            OnEquippedChanged?.Invoke(slot, item);
            return true;
        }

        public bool TryUnequip(EquipmentSlot slot)
        {
            if (!_equipped.ContainsKey(slot))
                return false;

            _equipped.Remove(slot);
            OnEquippedChanged?.Invoke(slot, null);
            return true;
        }

        public IReadOnlyList<ComparisonLine> CompareWithEquipped(EquipmentSlot slot, InventoryManager.InventoryItem candidate)
        {
            InventoryManager.ItemStats current = _equipped.TryGetValue(slot, out InventoryManager.InventoryItem equipped) && equipped != null
                ? equipped.stats
                : default;

            InventoryManager.ItemStats next = candidate != null ? candidate.stats : default;
            var lines = new List<ComparisonLine>(6)
            {
                BuildLine("Damage", current.damage, next.damage),
                BuildLine("Defense", current.defense, next.defense),
                BuildLine("HP", current.hp, next.hp),
                BuildLine("Mana", current.mana, next.mana),
                BuildLine("Strength", current.strength, next.strength),
                BuildLine("Agility", current.agility, next.agility)
            };

            OnComparisonReady?.Invoke(lines);
            return lines;
        }

        public void AutoEquipBestItems()
        {
            if (_inventory == null)
                return;

            IReadOnlyList<InventoryManager.GridEntry> entries = _inventory.InventoryEntries;
            var bestBySlot = new Dictionary<EquipmentSlot, (InventoryManager.InventoryItem item, float score)>();

            for (int i = 0; i < entries.Count; i++)
            {
                InventoryManager.InventoryItem item = entries[i]?.item;
                if (item == null)
                    continue;

                if (!TryMapItemToSlot(item, out EquipmentSlot slot))
                    continue;

                float score = ScoreItemForClass(item, _currentClass);
                if (!bestBySlot.TryGetValue(slot, out (InventoryManager.InventoryItem item, float score) existing) || score > existing.score)
                {
                    bestBySlot[slot] = (item, score);
                }
            }

            foreach (KeyValuePair<EquipmentSlot, (InventoryManager.InventoryItem item, float score)> kv in bestBySlot)
            {
                _equipped[kv.Key] = kv.Value.item;
                OnEquippedChanged?.Invoke(kv.Key, kv.Value.item);
            }
        }

        public void RotatePreviewBySwipe(float deltaX)
        {
            if (_characterPreviewRoot == null)
                return;

            float yawDelta = deltaX * _previewYawSpeed;
            _characterPreviewRoot.Rotate(0f, -yawDelta, 0f, Space.World);
            OnPreviewRotated?.Invoke(yawDelta);
        }

        private static ComparisonLine BuildLine(string name, int current, int candidate)
        {
            int delta = candidate - current;
            ComparisonArrow arrow = delta > 0 ? ComparisonArrow.Up : (delta < 0 ? ComparisonArrow.Down : ComparisonArrow.Neutral);

            return new ComparisonLine
            {
                statName = name,
                currentValue = current,
                candidateValue = candidate,
                delta = delta,
                arrow = arrow
            };
        }

        private static bool TryMapItemToSlot(InventoryManager.InventoryItem item, out EquipmentSlot slot)
        {
            slot = item.category switch
            {
                Data.Catalogs.ItemCategory.Weapon => EquipmentSlot.WeaponRight,
                Data.Catalogs.ItemCategory.Shield => EquipmentSlot.WeaponLeft,
                Data.Catalogs.ItemCategory.Wings => EquipmentSlot.Wings,
                Data.Catalogs.ItemCategory.Accessory => EquipmentSlot.Necklace,
                Data.Catalogs.ItemCategory.Armor => GuessArmorSlot(item),
                _ => EquipmentSlot.Helm
            };

            return item.category is Data.Catalogs.ItemCategory.Weapon
                or Data.Catalogs.ItemCategory.Shield
                or Data.Catalogs.ItemCategory.Wings
                or Data.Catalogs.ItemCategory.Accessory
                or Data.Catalogs.ItemCategory.Armor;
        }

        private static EquipmentSlot GuessArmorSlot(InventoryManager.InventoryItem item)
        {
            string name = item.displayName != null ? item.displayName.ToLowerInvariant() : string.Empty;
            if (name.Contains("helm") || name.Contains("casco")) return EquipmentSlot.Helm;
            if (name.Contains("pant") || name.Contains("pantal")) return EquipmentSlot.Pants;
            if (name.Contains("glove") || name.Contains("guante")) return EquipmentSlot.Gloves;
            if (name.Contains("boot") || name.Contains("bota")) return EquipmentSlot.Boots;
            return EquipmentSlot.Armor;
        }

        private static float ScoreItemForClass(InventoryManager.InventoryItem item, MuClassId classId)
        {
            InventoryManager.ItemStats s = item.stats;

            float offense = s.damage * 1.3f + s.agility * 0.35f;
            float defense = s.defense * 1.2f + s.hp * 0.2f;
            float utility = s.mana * 0.18f + s.strength * 0.15f;

            float classBias = classId switch
            {
                MuClassId.DarkWizard => offense * 0.95f + utility * 1.5f + defense * 0.6f,
                MuClassId.DarkKnight => offense * 1.15f + defense * 1.1f + utility * 0.6f,
                MuClassId.Elf => offense * 0.9f + defense * 0.8f + utility * 1.2f,
                MuClassId.Slayer => offense * 1.25f + defense * 0.75f + utility * 0.7f,
                MuClassId.RageFighter => offense * 1.2f + defense * 0.85f + utility * 0.65f,
                MuClassId.IllusionKnight => offense * 1.0f + defense * 0.8f + utility * 1.3f,
                _ => offense + defense + utility
            };

            float rarityBonus = item.rarity switch
            {
                InventoryManager.InventoryRarity.Legendary => 18f,
                InventoryManager.InventoryRarity.Epic => 12f,
                InventoryManager.InventoryRarity.Rare => 7f,
                InventoryManager.InventoryRarity.Magic => 3f,
                _ => 0f
            };

            float enhanceBonus = item.enhancementLevel * 2f;
            return classBias + rarityBonus + enhanceBonus;
        }
    }
}
