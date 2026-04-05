using System;
using System.Collections.Generic;
using MuLike.World;
using UnityEngine;
using UnityEngine.AI;

namespace MuLike.World
{
    /// <summary>
    /// Terrain runtime for MU maps.
    /// - Builds TerrainData from parsed heightmap
    /// - Applies 4-way splat blend textures to custom terrain material
    /// - Applies lightmap mode/baked data hooks
    /// - Builds collision from terrain + object colliders
    /// - Loads baked NavMeshData by map
    /// - Instantiates OBJS entries with LOD (3 levels) and distance culling
    /// </summary>
    public sealed class TerrainManager : MonoBehaviour
    {
        [Serializable]
        public struct SplatSet
        {
            public Texture2D tex0;
            public Texture2D tex1;
            public Texture2D tex2;
            public Texture2D tex3;
        }

        [Serializable]
        public struct LODPrefabSet
        {
            public string key;
            public GameObject lod0;
            public GameObject lod1;
            public GameObject lod2;
        }

        [Serializable]
        public struct MapBakeBinding
        {
            public MapLoader.MapId mapId;
            public Material terrainShaderMaterial;
            public LightmapData[] bakedLightmaps;
            public NavMeshData bakedNavMesh;
        }

        [Header("Dependencies")]
        [SerializeField] private MapLoader _mapLoader;
        [SerializeField] private Transform _player;

        [Header("Terrain")]
        [SerializeField] private SplatSet _defaultSplat;
        [SerializeField] private float _terrainHeightScale = 30f;

        [Header("Objects")]
        [SerializeField] private LODPrefabSet[] _lodPrefabs = Array.Empty<LODPrefabSet>();
        [SerializeField] private LayerMask _smallObjectCullingLayer;
        [SerializeField] private float _smallObjectCullDistance = 30f;

        [Header("Bake bindings")]
        [SerializeField] private MapBakeBinding[] _bakeBindings = Array.Empty<MapBakeBinding>();

        private Terrain _runtimeTerrain;
        private TerrainCollider _runtimeCollider;
        private NavMeshDataInstance _navMeshInstance;
        private readonly List<GameObject> _spawnedObjects = new();

        private void Awake()
        {
            if (_mapLoader == null)
                _mapLoader = FindAnyObjectByType<MapLoader>();
            if (_player == null)
                _player = FindAnyObjectByType<MuLike.Gameplay.Controllers.CharacterMotor>()?.transform;

            if (_mapLoader != null)
            {
                _mapLoader.OnMapLoaded += HandleMapLoaded;
                _mapLoader.OnChunkObjectsLoaded += HandleChunkObjects;
            }
        }

        private void OnDestroy()
        {
            if (_mapLoader != null)
            {
                _mapLoader.OnMapLoaded -= HandleMapLoaded;
                _mapLoader.OnChunkObjectsLoaded -= HandleChunkObjects;
            }

            if (_navMeshInstance.valid)
                _navMeshInstance.Remove();
        }

        private void Update()
        {
            if (_player == null)
                return;

            ApplySmallObjectCulling();
        }

        private void HandleMapLoaded(MapLoader.MapId mapId)
        {
            if (_mapLoader == null || _mapLoader.ActiveMap == null)
                return;

            BuildTerrain(_mapLoader.ActiveMap, mapId);
            ApplyMapBake(mapId);
        }

        private void BuildTerrain(MapLoader.ParsedMapData map, MapLoader.MapId mapId)
        {
            if (map == null)
                return;

            if (_runtimeTerrain != null)
                Destroy(_runtimeTerrain.gameObject);

            TerrainData td = new TerrainData
            {
                heightmapResolution = Mathf.NextPowerOfTwo(Mathf.Max(map.width, map.height)) + 1,
                size = new Vector3(map.width * map.tileSize, Mathf.Max(1f, _terrainHeightScale), map.height * map.tileSize)
            };

            float[,] normalized = NormalizeHeights(map.heights, map.width, map.height);
            td.SetHeights(0, 0, ResizeHeightsForTerrain(td.heightmapResolution, normalized, map.width, map.height));

            var alphas = BuildDefaultAlphaMap(td.alphamapWidth, td.alphamapHeight);
            td.terrainLayers = BuildTerrainLayers();
            td.SetAlphamaps(0, 0, alphas);

            GameObject terrainGo = Terrain.CreateTerrainGameObject(td);
            terrainGo.name = $"Terrain_{mapId}";
            terrainGo.transform.SetParent(transform, false);

            _runtimeTerrain = terrainGo.GetComponent<Terrain>();
            _runtimeCollider = terrainGo.GetComponent<TerrainCollider>();
            _runtimeTerrain.drawInstanced = true;
            _runtimeTerrain.materialTemplate = ResolveTerrainMaterial(mapId);

            EnsureTerrainCollision(_runtimeCollider, td);
        }

        private void HandleChunkObjects(List<MapLoader.WorldObjectRecord> records)
        {
            if (records == null)
                return;

            for (int i = 0; i < records.Count; i++)
            {
                GameObject go = SpawnObject(records[i]);
                if (go != null)
                    _spawnedObjects.Add(go);
            }
        }

        private GameObject SpawnObject(MapLoader.WorldObjectRecord record)
        {
            GameObject prefab = ResolvePrefab(record.prefabKey, record.lodGroup, 0);
            GameObject go;

            if (prefab != null)
                go = Instantiate(prefab, record.position, Quaternion.Euler(record.euler), transform);
            else
            {
                // Safe fallback primitive so parser remains testable without content packs.
                go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.transform.SetParent(transform, false);
                go.transform.position = record.position;
                go.transform.rotation = Quaternion.Euler(record.euler);
                go.transform.localScale = Vector3.one * 1.2f;
            }

            go.transform.localScale = record.scale == Vector3.zero ? Vector3.one : record.scale;
            go.name = string.IsNullOrWhiteSpace(record.prefabKey) ? $"Obj_{record.id}_{record.type}" : record.prefabKey;

            if (record.interactive)
            {
                var trigger = go.GetComponent<Collider>();
                if (trigger == null)
                    trigger = go.AddComponent<BoxCollider>();
                trigger.isTrigger = true;
            }

            EnsureLodGroup(go, record.prefabKey, record.lodGroup);
            return go;
        }

        private void EnsureLodGroup(GameObject root, string key, int lodGroup)
        {
            LODGroup group = root.GetComponent<LODGroup>();
            if (group != null)
                return;

            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers == null || renderers.Length == 0)
                return;

            group = root.AddComponent<LODGroup>();
            LOD[] lods = new LOD[3]
            {
                new LOD(0.60f, renderers),
                new LOD(0.30f, renderers),
                new LOD(0.08f, renderers)
            };
            group.SetLODs(lods);
            group.RecalculateBounds();
        }

        private void ApplySmallObjectCulling()
        {
            if (_player == null)
                return;

            float maxDist = Mathf.Max(5f, _smallObjectCullDistance);
            float sqr = maxDist * maxDist;

            for (int i = 0; i < _spawnedObjects.Count; i++)
            {
                GameObject go = _spawnedObjects[i];
                if (go == null)
                    continue;

                bool isSmall = go.transform.localScale.sqrMagnitude <= 4f;
                if (!isSmall)
                    continue;

                float d = (go.transform.position - _player.position).sqrMagnitude;
                bool visible = d <= sqr;
                if (go.activeSelf != visible)
                    go.SetActive(visible);
            }
        }

        private void ApplyMapBake(MapLoader.MapId mapId)
        {
            if (_navMeshInstance.valid)
                _navMeshInstance.Remove();

            for (int i = 0; i < _bakeBindings.Length; i++)
            {
                if (_bakeBindings[i].mapId != mapId)
                    continue;

                if (_bakeBindings[i].bakedNavMesh != null)
                    _navMeshInstance = NavMesh.AddNavMeshData(_bakeBindings[i].bakedNavMesh);

                if (_bakeBindings[i].bakedLightmaps != null && _bakeBindings[i].bakedLightmaps.Length > 0)
                    LightmapSettings.lightmaps = _bakeBindings[i].bakedLightmaps;

                break;
            }
        }

        private Material ResolveTerrainMaterial(MapLoader.MapId mapId)
        {
            for (int i = 0; i < _bakeBindings.Length; i++)
            {
                if (_bakeBindings[i].mapId == mapId && _bakeBindings[i].terrainShaderMaterial != null)
                    return _bakeBindings[i].terrainShaderMaterial;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Terrain/Lit");
            Material m = new Material(shader != null ? shader : Shader.Find("Standard"));
            m.SetTexture("_MainTex", _defaultSplat.tex0);
            return m;
        }

        private TerrainLayer[] BuildTerrainLayers()
        {
            Texture2D[] textures =
            {
                _defaultSplat.tex0,
                _defaultSplat.tex1,
                _defaultSplat.tex2,
                _defaultSplat.tex3
            };

            TerrainLayer[] layers = new TerrainLayer[4];
            for (int i = 0; i < 4; i++)
            {
                var layer = new TerrainLayer
                {
                    diffuseTexture = textures[i],
                    tileSize = new Vector2(12f, 12f)
                };
                layers[i] = layer;
            }

            return layers;
        }

        private static float[,] NormalizeHeights(float[,] src, int width, int height)
        {
            float[,] dst = new float[width, height];
            float min = float.MaxValue;
            float max = float.MinValue;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float h = src[x, y];
                    if (h < min) min = h;
                    if (h > max) max = h;
                }
            }

            float range = Mathf.Max(0.0001f, max - min);
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                    dst[x, y] = Mathf.Clamp01((src[x, y] - min) / range);
            }

            return dst;
        }

        private static float[,] ResizeHeightsForTerrain(int terrainRes, float[,] src, int srcW, int srcH)
        {
            float[,] dst = new float[terrainRes, terrainRes];
            for (int y = 0; y < terrainRes; y++)
            {
                float v = y / (float)(terrainRes - 1);
                int sy = Mathf.Clamp(Mathf.RoundToInt(v * (srcH - 1)), 0, srcH - 1);
                for (int x = 0; x < terrainRes; x++)
                {
                    float u = x / (float)(terrainRes - 1);
                    int sx = Mathf.Clamp(Mathf.RoundToInt(u * (srcW - 1)), 0, srcW - 1);
                    dst[y, x] = src[sx, sy];
                }
            }

            return dst;
        }

        private static float[,,] BuildDefaultAlphaMap(int w, int h)
        {
            float[,,] alpha = new float[h, w, 4];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    // Basic 4-way blend basis; replace with authored splat maps as needed.
                    float nx = x / (float)Mathf.Max(1, w - 1);
                    float ny = y / (float)Mathf.Max(1, h - 1);
                    alpha[y, x, 0] = Mathf.Clamp01(1f - nx);
                    alpha[y, x, 1] = Mathf.Clamp01(nx);
                    alpha[y, x, 2] = Mathf.Clamp01(ny * 0.5f);
                    alpha[y, x, 3] = Mathf.Clamp01((1f - ny) * 0.5f);

                    float sum = alpha[y, x, 0] + alpha[y, x, 1] + alpha[y, x, 2] + alpha[y, x, 3];
                    if (sum <= 0.0001f)
                    {
                        alpha[y, x, 0] = 1f;
                        alpha[y, x, 1] = alpha[y, x, 2] = alpha[y, x, 3] = 0f;
                    }
                    else
                    {
                        alpha[y, x, 0] /= sum;
                        alpha[y, x, 1] /= sum;
                        alpha[y, x, 2] /= sum;
                        alpha[y, x, 3] /= sum;
                    }
                }
            }

            return alpha;
        }

        private GameObject ResolvePrefab(string key, int lodGroup, int lodIndex)
        {
            if (string.IsNullOrWhiteSpace(key))
                return null;

            for (int i = 0; i < _lodPrefabs.Length; i++)
            {
                if (!string.Equals(_lodPrefabs[i].key, key, StringComparison.OrdinalIgnoreCase))
                    continue;

                return lodIndex switch
                {
                    2 => _lodPrefabs[i].lod2 != null ? _lodPrefabs[i].lod2 : _lodPrefabs[i].lod1,
                    1 => _lodPrefabs[i].lod1 != null ? _lodPrefabs[i].lod1 : _lodPrefabs[i].lod0,
                    _ => _lodPrefabs[i].lod0
                };
            }

            return null;
        }

        private static void EnsureTerrainCollision(TerrainCollider collider, TerrainData td)
        {
            if (collider == null)
                return;

            collider.terrainData = td;
        }
    }
}
