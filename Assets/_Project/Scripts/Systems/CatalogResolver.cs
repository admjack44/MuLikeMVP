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
        private readonly ItemCatalogService _itemCatalogService = new();
        private readonly Dictionary<int, object> _items = new();
        private readonly Dictionary<int, object> _monsters = new();
        private readonly Dictionary<int, object> _skills = new();

        public void LoadItemCatalog()
        {
            _itemCatalogService.LoadOrReload();
        }

        public bool TryGetItemDto(int id, out ItemDto item)
        {
            return _itemCatalogService.TryGetById(id, out item);
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
        }
    }
}
