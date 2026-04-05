using System;
using System.Collections.Generic;
using MuLike.Data.DTO;
using MuLike.Shared.Items;
using UnityEngine;

namespace MuLike.Data.Catalogs
{
    /// <summary>
    /// Runtime cache for item data loaded from the manifest and split catalogs.
    /// </summary>
    public sealed class ItemCatalogService
    {
        private readonly Dictionary<int, ItemDto> _byId = new Dictionary<int, ItemDto>();
        private readonly Dictionary<int, ItemDefinition> _definitionsById = new Dictionary<int, ItemDefinition>();
        private readonly Dictionary<string, List<ItemDto>> _byFamily = new Dictionary<string, List<ItemDto>>();
        private readonly Dictionary<ItemCategory, List<ItemDefinition>> _byCategory = new Dictionary<ItemCategory, List<ItemDefinition>>();

        public readonly struct LoadReport
        {
            public readonly int ItemCount;
            public readonly IReadOnlyList<ItemCatalogValidationIssue> Issues;

            public LoadReport(int itemCount, IReadOnlyList<ItemCatalogValidationIssue> issues)
            {
                ItemCount = itemCount;
                Issues = issues;
            }

            public bool HasErrors
            {
                get
                {
                    for (int i = 0; i < Issues.Count; i++)
                    {
                        if (Issues[i].Severity == ItemCatalogValidationSeverity.Error)
                            return true;
                    }

                    return false;
                }
            }
        }

        public int Count => _byId.Count;
        public IReadOnlyDictionary<int, ItemDefinition> DefinitionsById => _definitionsById;
        public IReadOnlyDictionary<ItemCategory, List<ItemDefinition>> DefinitionsByCategory => _byCategory;

        public LoadReport LoadOrReload()
        {
            _byId.Clear();
            _definitionsById.Clear();
            _byFamily.Clear();
            _byCategory.Clear();

            var loader = new ItemCatalogLoader();
            ItemCatalogLoadResult result = loader.LoadAll();
            var stagedDefinitions = new List<ItemDefinition>(result.Items.Count);
            var issues = new List<ItemCatalogValidationIssue>();

            for (int i = 0; i < result.Items.Count; i++)
            {
                ItemDto item = result.Items[i];
                _byId[item.id] = item;
                ItemDefinition definition = ItemDefinition.FromDto(item);
                stagedDefinitions.Add(definition);

                string family = string.IsNullOrEmpty(item.family) ? "unknown" : item.family;
                if (!_byFamily.ContainsKey(family))
                    _byFamily[family] = new List<ItemDto>();

                _byFamily[family].Add(item);
            }

            PopulateDefinitionCaches(stagedDefinitions, issues);

            LogIssues(issues);

            Debug.Log($"[ItemCatalogService] Loaded {Count} items.");
            return new LoadReport(Count, issues);
        }

        public LoadReport LoadFromDefinitions(IReadOnlyList<ItemDefinition> definitions)
        {
            _byId.Clear();
            _definitionsById.Clear();
            _byFamily.Clear();
            _byCategory.Clear();

            var normalized = new List<ItemDefinition>();
            var issues = new List<ItemCatalogValidationIssue>();

            if (definitions != null)
            {
                for (int i = 0; i < definitions.Count; i++)
                {
                    if (definitions[i] == null)
                        continue;

                    normalized.Add(definitions[i]);
                }
            }

            PopulateDefinitionCaches(normalized, issues);

            foreach (var pair in _definitionsById)
            {
                ItemDefinition definition = pair.Value;
                ItemDto dto = definition.ToDto();
                _byId[definition.ItemId] = dto;

                string family = string.IsNullOrWhiteSpace(definition.Family) ? "unknown" : definition.Family;
                if (!_byFamily.TryGetValue(family, out List<ItemDto> familyList))
                {
                    familyList = new List<ItemDto>();
                    _byFamily[family] = familyList;
                }

                familyList.Add(dto);
            }

            LogIssues(issues);

            Debug.Log($"[ItemCatalogService] Loaded {Count} items from ScriptableObjects.");
            return new LoadReport(Count, issues);
        }

        private void PopulateDefinitionCaches(List<ItemDefinition> definitions, List<ItemCatalogValidationIssue> issues)
        {
            for (int i = 0; i < definitions.Count; i++)
            {
                ItemDefinition item = definitions[i];

                if (!ValidateDefinition(item, issues))
                    continue;

                if (_definitionsById.ContainsKey(item.ItemId))
                {
                    issues.Add(new ItemCatalogValidationIssue(
                        ItemCatalogValidationSeverity.Error,
                        item.ItemId,
                        "Duplicate item id detected."));
                    continue;
                }

                NormalizeDefinition(item, issues);

                _definitionsById[item.ItemId] = item;

                if (!_byCategory.TryGetValue(item.Category, out List<ItemDefinition> bucket))
                {
                    bucket = new List<ItemDefinition>();
                    _byCategory[item.Category] = bucket;
                }

                bucket.Add(item);
            }
        }

        private static bool ValidateDefinition(ItemDefinition item, List<ItemCatalogValidationIssue> issues)
        {
            if (item.ItemId <= 0)
            {
                issues.Add(new ItemCatalogValidationIssue(
                    ItemCatalogValidationSeverity.Error,
                    item.ItemId,
                    "ItemId must be greater than zero."));
                return false;
            }

            if (!ItemCatalogSyncPolicy.IsInKnownRange(item.ItemId))
            {
                issues.Add(new ItemCatalogValidationIssue(
                    ItemCatalogValidationSeverity.Warning,
                    item.ItemId,
                    "ItemId is outside declared sync ranges."));
            }

            if (string.IsNullOrWhiteSpace(item.Name))
            {
                issues.Add(new ItemCatalogValidationIssue(
                    ItemCatalogValidationSeverity.Error,
                    item.ItemId,
                    "Name is required."));
                return false;
            }

            if (item.MaxStack < 0)
            {
                issues.Add(new ItemCatalogValidationIssue(
                    ItemCatalogValidationSeverity.Error,
                    item.ItemId,
                    "MaxStack cannot be negative."));
                return false;
            }

            if (item.RequiredLevel < 0)
            {
                issues.Add(new ItemCatalogValidationIssue(
                    ItemCatalogValidationSeverity.Error,
                    item.ItemId,
                    "RequiredLevel cannot be negative."));
                return false;
            }

            if (item.SellValue < 0)
            {
                issues.Add(new ItemCatalogValidationIssue(
                    ItemCatalogValidationSeverity.Error,
                    item.ItemId,
                    "SellValue cannot be negative."));
                return false;
            }

            if (item.BasicStats.MinDamage > item.BasicStats.MaxDamage)
            {
                issues.Add(new ItemCatalogValidationIssue(
                    ItemCatalogValidationSeverity.Error,
                    item.ItemId,
                    "MinDamage cannot exceed MaxDamage."));
                return false;
            }

            return true;
        }

        private static void NormalizeDefinition(ItemDefinition item, List<ItemCatalogValidationIssue> issues)
        {
            if (!item.Stackable && item.MaxStack != 1)
            {
                issues.Add(new ItemCatalogValidationIssue(
                    ItemCatalogValidationSeverity.Warning,
                    item.ItemId,
                    "Non-stackable item had MaxStack != 1. Normalized to 1."));
                item.MaxStack = 1;
            }

            if (item.Stackable && item.MaxStack < 2)
            {
                issues.Add(new ItemCatalogValidationIssue(
                    ItemCatalogValidationSeverity.Warning,
                    item.ItemId,
                    "Stackable item had MaxStack < 2. Normalized to 2."));
                item.MaxStack = 2;
            }

            if (!item.Stackable)
            {
                item.StackRule = ItemStackRule.None;
            }
            else if (item.StackRule == ItemStackRule.None)
            {
                item.StackRule = ItemStackRule.ByItemId;
            }

            if (!item.AllowSockets)
            {
                item.MaxSockets = 0;
            }
            else if (item.MaxSockets < 1)
            {
                item.MaxSockets = 1;
            }

            if (item.MaxSockets > 5)
                item.MaxSockets = 5;

            if (item.AllowedEquipSlots == null)
                item.AllowedEquipSlots = new List<ItemEquipSlot>();

            if (item.AllowedClasses == null || item.AllowedClasses.Count == 0)
                item.AllowedClasses = new List<CharacterClassRestriction> { CharacterClassRestriction.Any };

            if (item.Category == ItemCategory.Weapon
                || item.Category == ItemCategory.Shield
                || item.Category == ItemCategory.Armor
                || item.Category == ItemCategory.Accessory
                || item.Category == ItemCategory.Wings
                || item.Category == ItemCategory.Pet
                || item.Category == ItemCategory.Costume)
            {
                if (item.AllowedEquipSlots.Count == 0)
                {
                    issues.Add(new ItemCatalogValidationIssue(
                        ItemCatalogValidationSeverity.Warning,
                        item.ItemId,
                        "Equippable category item has no allowed equip slots."));
                }
            }
        }

        private static void LogIssues(IReadOnlyList<ItemCatalogValidationIssue> issues)
        {
            for (int i = 0; i < issues.Count; i++)
            {
                ItemCatalogValidationIssue issue = issues[i];
                if (issue.Severity == ItemCatalogValidationSeverity.Error)
                    Debug.LogError($"[ItemCatalogService] {issue}");
                else
                    Debug.LogWarning($"[ItemCatalogService] {issue}");
            }
        }

        public bool TryGetDefinition(int itemId, out ItemDefinition definition)
        {
            return _definitionsById.TryGetValue(itemId, out definition);
        }

        public IReadOnlyList<ItemDefinition> GetByCategory(ItemCategory category)
        {
            if (_byCategory.TryGetValue(category, out List<ItemDefinition> list))
                return list;

            return Array.Empty<ItemDefinition>();
        }

        public bool TryGetById(int itemId, out ItemDto item)
        {
            return _byId.TryGetValue(itemId, out item);
        }

        public IReadOnlyList<ItemDto> GetByFamily(string family)
        {
            if (string.IsNullOrEmpty(family)) return System.Array.Empty<ItemDto>();
            if (_byFamily.TryGetValue(family, out var list)) return list;
            return System.Array.Empty<ItemDto>();
        }
    }
}
