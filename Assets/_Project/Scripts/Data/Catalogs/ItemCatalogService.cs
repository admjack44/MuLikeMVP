using System.Collections.Generic;
using MuLike.Data.DTO;
using UnityEngine;

namespace MuLike.Data.Catalogs
{
    /// <summary>
    /// Runtime cache for item data loaded from the manifest and split catalogs.
    /// </summary>
    public sealed class ItemCatalogService
    {
        private readonly Dictionary<int, ItemDto> _byId = new Dictionary<int, ItemDto>();
        private readonly Dictionary<string, List<ItemDto>> _byFamily = new Dictionary<string, List<ItemDto>>();

        public int Count => _byId.Count;

        public void LoadOrReload()
        {
            _byId.Clear();
            _byFamily.Clear();

            var loader = new ItemCatalogLoader();
            ItemCatalogLoadResult result = loader.LoadAll();

            for (int i = 0; i < result.Items.Count; i++)
            {
                ItemDto item = result.Items[i];
                _byId[item.id] = item;

                string family = string.IsNullOrEmpty(item.family) ? "unknown" : item.family;
                if (!_byFamily.ContainsKey(family))
                    _byFamily[family] = new List<ItemDto>();

                _byFamily[family].Add(item);
            }

            Debug.Log($"[ItemCatalogService] Loaded {Count} items.");
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
