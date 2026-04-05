using System;
using System.Collections.Generic;
using MuLike.Chat;
using MuLike.Core;
using MuLike.Data.Catalogs;
using MuLike.Economy;
using MuLike.Inventory;
using UnityEngine;

namespace MuLike.Crafting
{
    /// <summary>
    /// MU crafting runtime: chaos combinations, wing crafting, sockets, and elemental attributes.
    /// </summary>
    public sealed class ChaosMachine : MonoBehaviour
    {
        public enum CraftRecipeType
        {
            GenericCombination,
            WingLevel1,
            WingLevel2,
            WingLevel3,
            AddSocket,
            ApplyElement
        }

        public enum ElementalType
        {
            Fire,
            Ice,
            Lightning,
            Poison
        }

        [Serializable]
        public sealed class SocketInfo
        {
            public int maxSockets;
            public int filledSockets;
        }

        [Serializable]
        public sealed class CraftedMetadata
        {
            public string instanceId;
            public SocketInfo socketInfo = new();
            public ElementalType? elementalType;
            public ExcellentOptionFlags excellentFlags;
        }

        [Serializable]
        public struct CraftResult
        {
            public bool success;
            public string message;
            public InventoryManager.InventoryItem item;
        }

        [Header("Success Rates")]
        [SerializeField, Range(0f, 1f)] private float _genericCombinationChance = 0.75f;
        [SerializeField, Range(0f, 1f)] private float _wingLevel1Chance = 0.80f;
        [SerializeField, Range(0f, 1f)] private float _wingLevel2Chance = 0.55f;
        [SerializeField, Range(0f, 1f)] private float _wingLevel3Chance = 0.35f;

        [Header("Dependencies")]
        [SerializeField] private InventoryManager _inventoryManager;
        [SerializeField] private CurrencyManager _currencyManager;
        [SerializeField] private ChatSystem _chatSystem;

        private readonly Dictionary<string, CraftedMetadata> _metadataByInstanceId = new(StringComparer.OrdinalIgnoreCase);

        public IReadOnlyDictionary<string, CraftedMetadata> MetadataByInstanceId => _metadataByInstanceId;

        public event Action<CraftRecipeType, CraftResult> OnCraftCompleted;

        private void Awake()
        {
            if (_inventoryManager == null)
                _inventoryManager = FindAnyObjectByType<InventoryManager>();
            if (_currencyManager == null)
                _currencyManager = FindAnyObjectByType<CurrencyManager>();
            if (_chatSystem == null)
                _chatSystem = FindAnyObjectByType<ChatSystem>();

            GameContext.RegisterSystem(this);
        }

        public CraftResult CombineItems(string[] itemInstanceIds)
        {
            var consumed = RemoveItems(itemInstanceIds);
            if (consumed.Count == 0)
                return Fail(CraftRecipeType.GenericCombination, "No valid items provided.");

            if (_currencyManager == null || !_currencyManager.TrySpendCurrency(CurrencyManager.CurrencyType.JewelOfChaos, 1))
            {
                RestoreItems(consumed);
                return Fail(CraftRecipeType.GenericCombination, "Jewel of Chaos required.");
            }

            if (UnityEngine.Random.value > _genericCombinationChance)
                return FailureWithLoss(CraftRecipeType.GenericCombination, consumed, "Chaos combination failed.");

            InventoryManager.InventoryItem reward = BuildCombinedReward(consumed);
            if (!_inventoryManager.TryAddItem(reward))
            {
                RestoreItems(consumed);
                return Fail(CraftRecipeType.GenericCombination, "Inventory full.");
            }

            return Succeed(CraftRecipeType.GenericCombination, reward, "Combination succeeded.");
        }

        public CraftResult CreateWing(int wingLevel, string baseItemInstanceId)
        {
            if (!_inventoryManager.TryRemoveItem(baseItemInstanceId, out InventoryManager.InventoryItem source) || source == null)
                return Fail(ResolveWingRecipe(wingLevel), "Base item not found.");

            if (_currencyManager == null || !_currencyManager.TrySpendCurrency(CurrencyManager.CurrencyType.JewelOfChaos, wingLevel))
            {
                _inventoryManager.TryAddItem(source);
                return Fail(ResolveWingRecipe(wingLevel), "Not enough Jewel of Chaos.");
            }

            float chance = wingLevel switch
            {
                1 => _wingLevel1Chance,
                2 => _wingLevel2Chance,
                _ => _wingLevel3Chance
            };

            if (UnityEngine.Random.value > chance)
                return FailureWithLoss(ResolveWingRecipe(wingLevel), new List<InventoryManager.InventoryItem> { source }, $"Wing level {wingLevel} crafting failed.");

            InventoryManager.InventoryItem wing = new InventoryManager.InventoryItem
            {
                instanceId = Guid.NewGuid().ToString("N"),
                itemId = 10000 + wingLevel,
                displayName = $"Wing Lv{wingLevel}",
                category = ItemCategory.Wings,
                rarity = wingLevel >= 3 ? InventoryManager.InventoryRarity.Legendary : wingLevel == 2 ? InventoryManager.InventoryRarity.Epic : InventoryManager.InventoryRarity.Rare,
                size = InventoryManager.ItemSize.TwoByTwo,
                quantity = 1,
                enhancementLevel = source.enhancementLevel,
                stats = new InventoryManager.ItemStats
                {
                    defense = 20 * wingLevel,
                    damage = 12 * wingLevel,
                    agility = 8 * wingLevel
                }
            };

            _inventoryManager.TryAddItem(wing);
            return Succeed(ResolveWingRecipe(wingLevel), wing, $"Wing level {wingLevel} crafted.");
        }

        public CraftResult TryAddSocket(string itemInstanceId)
        {
            if (!_inventoryManager.TryRemoveItem(itemInstanceId, out InventoryManager.InventoryItem item) || item == null)
                return Fail(CraftRecipeType.AddSocket, "Item not found.");

            if (GameContext.CatalogResolver != null && GameContext.CatalogResolver.TryGetItemDefinition(item.itemId, out ItemDefinition def) && def != null)
            {
                if (!def.AllowSockets)
                {
                    _inventoryManager.TryAddItem(item);
                    return Fail(CraftRecipeType.AddSocket, "Item does not allow sockets.");
                }

                CraftedMetadata metadata = GetOrCreateMetadata(item);
                if (metadata.socketInfo.filledSockets >= Mathf.Max(1, def.MaxSockets))
                {
                    _inventoryManager.TryAddItem(item);
                    return Fail(CraftRecipeType.AddSocket, "Maximum sockets reached.");
                }

                if (_currencyManager == null || !_currencyManager.TrySpendCurrency(CurrencyManager.CurrencyType.JewelOfChaos, 1))
                {
                    _inventoryManager.TryAddItem(item);
                    return Fail(CraftRecipeType.AddSocket, "Jewel of Chaos required.");
                }

                metadata.socketInfo.maxSockets = Mathf.Max(1, def.MaxSockets);
                metadata.socketInfo.filledSockets++;
                _inventoryManager.TryAddItem(item);
                return Succeed(CraftRecipeType.AddSocket, item, "Socket added successfully.");
            }

            _inventoryManager.TryAddItem(item);
            return Fail(CraftRecipeType.AddSocket, "Catalog definition not available.");
        }

        public CraftResult TryApplyElementalAttribute(string itemInstanceId, ElementalType element)
        {
            if (!_inventoryManager.TryRemoveItem(itemInstanceId, out InventoryManager.InventoryItem item) || item == null)
                return Fail(CraftRecipeType.ApplyElement, "Item not found.");

            if (_currencyManager == null || !_currencyManager.TrySpendCurrency(CurrencyManager.CurrencyType.JewelOfLife, 1))
            {
                _inventoryManager.TryAddItem(item);
                return Fail(CraftRecipeType.ApplyElement, "Jewel of Life required.");
            }

            CraftedMetadata metadata = GetOrCreateMetadata(item);
            metadata.elementalType = element;

            switch (element)
            {
                case ElementalType.Fire:
                    item.stats.damage += 12;
                    break;
                case ElementalType.Ice:
                    item.stats.defense += 12;
                    break;
                case ElementalType.Lightning:
                    item.stats.agility += 10;
                    break;
                case ElementalType.Poison:
                    item.stats.hp += 40;
                    break;
            }

            _inventoryManager.TryAddItem(item);
            return Succeed(CraftRecipeType.ApplyElement, item, $"Applied {element} element.");
        }

        public bool TryApplyExcellentOption(string itemInstanceId, ExcellentOptionFlags option)
        {
            if (!_inventoryManager.TryRemoveItem(itemInstanceId, out InventoryManager.InventoryItem item) || item == null)
                return false;

            if (_currencyManager == null || !_currencyManager.TrySpendCurrency(CurrencyManager.CurrencyType.JewelOfLife, 1))
            {
                _inventoryManager.TryAddItem(item);
                return false;
            }

            CraftedMetadata metadata = GetOrCreateMetadata(item);
            metadata.excellentFlags |= option;
            _inventoryManager.TryAddItem(item);
            return true;
        }

        private CraftedMetadata GetOrCreateMetadata(InventoryManager.InventoryItem item)
        {
            if (!_metadataByInstanceId.TryGetValue(item.instanceId, out CraftedMetadata metadata))
            {
                metadata = new CraftedMetadata
                {
                    instanceId = item.instanceId,
                    socketInfo = new SocketInfo()
                };
                _metadataByInstanceId[item.instanceId] = metadata;
            }

            return metadata;
        }

        private List<InventoryManager.InventoryItem> RemoveItems(string[] itemInstanceIds)
        {
            var removed = new List<InventoryManager.InventoryItem>();
            if (_inventoryManager == null || itemInstanceIds == null)
                return removed;

            for (int i = 0; i < itemInstanceIds.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(itemInstanceIds[i]))
                    continue;

                if (_inventoryManager.TryRemoveItem(itemInstanceIds[i], out InventoryManager.InventoryItem item) && item != null)
                    removed.Add(item);
            }

            return removed;
        }

        private void RestoreItems(List<InventoryManager.InventoryItem> items)
        {
            if (_inventoryManager == null || items == null)
                return;

            for (int i = 0; i < items.Count; i++)
                _inventoryManager.TryAddItem(items[i]);
        }

        private InventoryManager.InventoryItem BuildCombinedReward(List<InventoryManager.InventoryItem> consumed)
        {
            int combinedId = 9000 + consumed.Count;
            int enhancement = 0;
            InventoryManager.InventoryRarity rarity = InventoryManager.InventoryRarity.Magic;
            InventoryManager.ItemStats stats = new InventoryManager.ItemStats();
            for (int i = 0; i < consumed.Count; i++)
            {
                InventoryManager.InventoryItem item = consumed[i];
                enhancement += item.enhancementLevel;
                if (item.rarity > rarity)
                    rarity = item.rarity;
                stats.damage += item.stats.damage / 2;
                stats.defense += item.stats.defense / 2;
                stats.hp += item.stats.hp / 2;
            }

            return new InventoryManager.InventoryItem
            {
                instanceId = Guid.NewGuid().ToString("N"),
                itemId = combinedId,
                displayName = "Chaos Combination Result",
                category = ResolveCombinedCategory(consumed),
                rarity = rarity,
                size = InventoryManager.ItemSize.OneByTwo,
                quantity = 1,
                enhancementLevel = Mathf.Clamp(enhancement / Mathf.Max(1, consumed.Count), 0, 15),
                stats = stats
            };
        }

        private static ItemCategory ResolveCombinedCategory(List<InventoryManager.InventoryItem> consumed)
        {
            if (consumed == null || consumed.Count == 0)
                return ItemCategory.Material;

            for (int i = 0; i < consumed.Count; i++)
            {
                InventoryManager.InventoryItem item = consumed[i];
                if (item == null)
                    continue;

                if (item.category == ItemCategory.Weapon || item.category == ItemCategory.Armor)
                    return item.category;
            }

            return consumed[0] != null ? consumed[0].category : ItemCategory.Material;
        }

        private CraftResult Succeed(CraftRecipeType recipeType, InventoryManager.InventoryItem item, string message)
        {
            var result = new CraftResult
            {
                success = true,
                message = message,
                item = item
            };
            _chatSystem?.ReceiveSystemMessage(message);
            OnCraftCompleted?.Invoke(recipeType, result);
            return result;
        }

        private CraftResult Fail(CraftRecipeType recipeType, string message)
        {
            var result = new CraftResult
            {
                success = false,
                message = message,
                item = null
            };
            OnCraftCompleted?.Invoke(recipeType, result);
            return result;
        }

        private CraftResult FailureWithLoss(CraftRecipeType recipeType, List<InventoryManager.InventoryItem> consumed, string message)
        {
            consumed?.Clear();
            return Fail(recipeType, message);
        }

        private static CraftRecipeType ResolveWingRecipe(int wingLevel)
        {
            return wingLevel switch
            {
                1 => CraftRecipeType.WingLevel1,
                2 => CraftRecipeType.WingLevel2,
                _ => CraftRecipeType.WingLevel3
            };
        }
    }

    internal static class ChaosMachineItemCategoryResolver
    {
        public static ItemCategory SpecializeCategory(List<InventoryManager.InventoryItem> items)
        {
            if (items == null || items.Count == 0)
                return ItemCategory.Material;

            int wings = 0;
            int weapons = 0;
            int armors = 0;
            for (int i = 0; i < items.Count; i++)
            {
                switch (items[i].category)
                {
                    case ItemCategory.Wings:
                        wings++;
                        break;
                    case ItemCategory.Weapon:
                        weapons++;
                        break;
                    case ItemCategory.Armor:
                        armors++;
                        break;
                }
            }

            if (wings > 0)
                return ItemCategory.Wings;
            if (weapons >= armors)
                return ItemCategory.Weapon;
            return ItemCategory.Armor;
        }
    }
}