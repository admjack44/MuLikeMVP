using System;
using System.Collections.Generic;
using MuLike.Data.Catalogs;
using MuLike.Data.DTO;
using UnityEngine;

namespace MuLike.Systems
{
    /// <summary>
    /// Resolves catalog entries (items, monsters, skills, maps) by ID from loaded ScriptableObject catalogs.
    /// </summary>
    public class CatalogResolver
    {
        public enum ItemCatalogLoadMode
        {
            Auto,
            JsonResources,
            ScriptableObjectResources
        }

        public readonly struct ItemCatalogLoadSummary
        {
            public readonly ItemCatalogLoadMode Mode;
            public readonly int LoadedCount;
            public readonly IReadOnlyList<ItemCatalogValidationIssue> Issues;

            public ItemCatalogLoadSummary(ItemCatalogLoadMode mode, int loadedCount, IReadOnlyList<ItemCatalogValidationIssue> issues)
            {
                Mode = mode;
                LoadedCount = loadedCount;
                Issues = issues;
            }
        }

        private readonly ItemCatalogService _itemCatalogService = new();
        private readonly Dictionary<int, object> _items = new();
        private readonly Dictionary<int, object> _monsters = new();
        private readonly Dictionary<int, object> _skills = new();
        private const string DefaultScriptableCatalogResource = "Data/Items/ItemCatalogDatabase";

        public ItemCatalogLoadSummary LastItemCatalogLoad { get; private set; }

        public ItemCatalogLoadSummary LoadItemCatalog(
            ItemCatalogLoadMode mode = ItemCatalogLoadMode.Auto,
            string scriptableCatalogResourcePath = DefaultScriptableCatalogResource)
        {
            ItemCatalogService.LoadReport report;
            ItemCatalogLoadMode resolvedMode = mode;

            if (mode == ItemCatalogLoadMode.Auto)
            {
                bool loadedFromSo = TryLoadFromScriptableObject(scriptableCatalogResourcePath, out report);
                resolvedMode = loadedFromSo ? ItemCatalogLoadMode.ScriptableObjectResources : ItemCatalogLoadMode.JsonResources;

                if (!loadedFromSo)
                    report = _itemCatalogService.LoadOrReload();
            }
            else if (mode == ItemCatalogLoadMode.ScriptableObjectResources)
            {
                if (!TryLoadFromScriptableObject(scriptableCatalogResourcePath, out report))
                {
                    Debug.LogWarning($"[CatalogResolver] ScriptableObject catalog not found at '{scriptableCatalogResourcePath}'. Falling back to JSON resources.");
                    report = _itemCatalogService.LoadOrReload();
                    resolvedMode = ItemCatalogLoadMode.JsonResources;
                }
            }
            else
            {
                report = _itemCatalogService.LoadOrReload();
            }

            LastItemCatalogLoad = new ItemCatalogLoadSummary(resolvedMode, report.ItemCount, report.Issues);
            return LastItemCatalogLoad;
        }

        private bool TryLoadFromScriptableObject(string resourcePath, out ItemCatalogService.LoadReport report)
        {
            report = default;

            string path = string.IsNullOrWhiteSpace(resourcePath)
                ? DefaultScriptableCatalogResource
                : resourcePath;

            ItemCatalogDatabase database = Resources.Load<ItemCatalogDatabase>(path);
            if (database == null)
                return false;

            List<ItemDefinition> items = database.BuildDefinitions();
            report = _itemCatalogService.LoadFromDefinitions(items);
            return true;
        }

        public bool TryGetItemDto(int id, out ItemDto item)
        {
            return _itemCatalogService.TryGetById(id, out item);
        }

        public bool TryGetItemDefinition(int id, out ItemDefinition definition)
        {
            return _itemCatalogService.TryGetDefinition(id, out definition);
        }

        public IReadOnlyList<ItemDefinition> GetItemsByCategory(ItemCategory category)
        {
            return _itemCatalogService.GetByCategory(category);
        }

        public IReadOnlyList<ItemCatalogValidationIssue> GetValidationIssues()
        {
            return LastItemCatalogLoad.Issues ?? System.Array.Empty<ItemCatalogValidationIssue>();
        }

        public IReadOnlyList<ItemDto> GetItemsByFamily(string family)
        {
            return _itemCatalogService.GetByFamily(family);
        }

        public void RegisterItem(int id, object data) => _items[id] = data;
        public void RegisterMonster(int id, object data) => _monsters[id] = data;
        public void RegisterSkill(int id, object data) => _skills[id] = data;

        public T GetItem<T>(int id) where T : class
        {
            if (typeof(T) == typeof(ItemDto) && _itemCatalogService.TryGetById(id, out var dto))
                return dto as T;

            if (typeof(T) == typeof(ItemDefinition) && _itemCatalogService.TryGetDefinition(id, out var definition))
                return definition as T;

            if (_items.TryGetValue(id, out var data) && data is T typed) return typed;
            Debug.LogWarning($"[CatalogResolver] Item {id} not found or wrong type.");
            return null;
        }

        public T GetMonster<T>(int id) where T : class
        {
            if (_monsters.TryGetValue(id, out var data) && data is T typed) return typed;
            Debug.LogWarning($"[CatalogResolver] Monster {id} not found or wrong type.");
            return null;
        }

        public T GetSkill<T>(int id) where T : class
        {
            if (_skills.TryGetValue(id, out var data) && data is T typed) return typed;
            Debug.LogWarning($"[CatalogResolver] Skill {id} not found or wrong type.");
            return null;
        }

        public void Clear()
        {
            _items.Clear();
            _monsters.Clear();
            _skills.Clear();
            LastItemCatalogLoad = default;
        }
    }
}
