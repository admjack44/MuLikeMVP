using System;
using System.Collections.Generic;
using UnityEngine;

namespace MuLike.ContentPipeline.Authoring
{
    [Serializable]
    public struct MapDefinitionRecord
    {
        public int mapId;
        public string mapName;
        public string sceneName;
        public string biome;
        public int recommendedLevel;
    }

    [CreateAssetMenu(fileName = "MapDefinition", menuName = "MuLike/Content Pipeline/Map Definition")]
    public sealed class MapDefinitionAsset : ScriptableObject
    {
        [SerializeField] private MapDefinitionRecord definition;

        public MapDefinitionRecord Definition => definition;
    }

    [CreateAssetMenu(fileName = "MapCatalogDatabase", menuName = "MuLike/Content Pipeline/Map Catalog Database")]
    public sealed class MapCatalogDatabase : ScriptableObject
    {
        public List<MapDefinitionAsset> mapAssets = new();
        public List<MapDefinitionRecord> inlineMaps = new();

        public List<MapDefinitionRecord> BuildDefinitions()
        {
            var maps = new List<MapDefinitionRecord>();

            for (int i = 0; i < mapAssets.Count; i++)
            {
                if (mapAssets[i] == null)
                    continue;

                maps.Add(mapAssets[i].Definition);
            }

            maps.AddRange(inlineMaps);
            return maps;
        }
    }
}
