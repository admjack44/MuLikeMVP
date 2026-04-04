using System;
using System.Collections.Generic;
using MuLike.Data.DTO;
using UnityEngine;

namespace MuLike.Data.Catalogs
{
    public sealed class ItemCatalogLoadResult
    {
        public ItemManifestDto Manifest { get; }
        public IReadOnlyList<ItemDto> Items { get; }
        public IReadOnlyDictionary<string, List<ItemDto>> ItemsByGroup { get; }

        public ItemCatalogLoadResult(
            ItemManifestDto manifest,
            List<ItemDto> items,
            Dictionary<string, List<ItemDto>> itemsByGroup)
        {
            Manifest = manifest;
            Items = items;
            ItemsByGroup = itemsByGroup;
        }
    }

    /// <summary>
    /// Loads the item manifest and all referenced catalog files from Resources/Data/Items.
    /// </summary>
    public sealed class ItemCatalogLoader
    {
        private const string ManifestResourcePath = "Data/Items/items";

        public ItemCatalogLoadResult LoadAll()
        {
            ItemManifestDto manifest = LoadManifest();
            var allItems = new List<ItemDto>();
            var dedupe = new HashSet<int>();
            var itemsByGroup = new Dictionary<string, List<ItemDto>>(StringComparer.OrdinalIgnoreCase);

            if (manifest == null || manifest.sources == null)
            {
                return new ItemCatalogLoadResult(manifest, allItems, itemsByGroup);
            }

            for (int i = 0; i < manifest.sources.Length; i++)
            {
                ItemManifestSourceDto source = manifest.sources[i];
                if (source == null || string.IsNullOrWhiteSpace(source.file))
                    continue;

                string resourcePath = ToResourcePath(source.file);
                TextAsset asset = Resources.Load<TextAsset>(resourcePath);

                if (asset == null)
                {
                    Debug.LogWarning($"[ItemCatalogLoader] Catalog not found: {resourcePath}");
                    continue;
                }

                ItemCatalogFileDto fileDto = JsonUtility.FromJson<ItemCatalogFileDto>(asset.text);
                if (fileDto == null || fileDto.items == null)
                {
                    Debug.LogWarning($"[ItemCatalogLoader] Invalid catalog format: {source.file}");
                    continue;
                }

                if (!itemsByGroup.ContainsKey(fileDto.group))
                    itemsByGroup[fileDto.group] = new List<ItemDto>();

                for (int j = 0; j < fileDto.items.Length; j++)
                {
                    ItemDto item = fileDto.items[j];
                    if (item == null) continue;

                    if (!dedupe.Add(item.id))
                    {
                        Debug.LogWarning($"[ItemCatalogLoader] Duplicate item id skipped: {item.id}");
                        continue;
                    }

                    allItems.Add(item);
                    itemsByGroup[fileDto.group].Add(item);
                }
            }

            allItems.Sort((a, b) => a.id.CompareTo(b.id));
            return new ItemCatalogLoadResult(manifest, allItems, itemsByGroup);
        }

        public ItemManifestDto LoadManifest()
        {
            TextAsset asset = Resources.Load<TextAsset>(ManifestResourcePath);
            if (asset == null)
            {
                Debug.LogWarning($"[ItemCatalogLoader] Manifest not found: {ManifestResourcePath}");
                return null;
            }

            ItemManifestDto manifest = JsonUtility.FromJson<ItemManifestDto>(asset.text);
            if (manifest == null)
            {
                Debug.LogWarning("[ItemCatalogLoader] Failed to parse manifest JSON.");
                return null;
            }

            return manifest;
        }

        private static string ToResourcePath(string manifestFile)
        {
            string path = manifestFile.Replace("\\", "/").Trim();

            if (path.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                path = path.Substring(0, path.Length - 5);

            return "Data/Items/" + path;
        }
    }
}
