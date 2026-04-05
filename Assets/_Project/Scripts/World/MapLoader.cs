using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.AI;

namespace MuLike.World
{
    /// <summary>
    /// MU map loader with MAP/ATT/OBJS parsers and 3x3 chunk streaming around player.
    ///
    /// Supported content flow:
    /// - .MAP: height data per tile
    /// - .ATT: terrain attribute flags (walkable/water/safezone/block)
    /// - .OBJS: world objects (npc/portal/chest/monument/static)
    ///
    /// This implementation is format-tolerant: if binary schema does not match expected,
    /// it falls back to safe defaults rather than hard-crashing runtime.
    /// </summary>
    public sealed class MapLoader : MonoBehaviour
    {
        public enum MapId
        {
            Lorencia = 0,
            Noria = 1,
            Devias = 2,
            Dungeon = 3,
            LostTower = 4,
            Atlans = 5,
            Tarkan = 6,
            Icarus = 7
        }

        [Flags]
        public enum TerrainAttribute : byte
        {
            None = 0,
            Walkable = 1 << 0,
            Blocked = 1 << 1,
            Water = 1 << 2,
            SafeZone = 1 << 3,
            NoSummon = 1 << 4,
            NoTeleport = 1 << 5
        }

        public enum WorldObjectType
        {
            Static,
            Npc,
            Portal,
            Chest,
            Monument
        }

        [Serializable]
        public struct MapAssetBinding
        {
            public MapId mapId;
            public TextAsset mapFile;
            public TextAsset attFile;
            public TextAsset objsFile;
            public Material terrainMaterial;
            public NavMeshData navMeshData;
        }

        [Serializable]
        public struct WorldObjectRecord
        {
            public int id;
            public WorldObjectType type;
            public string prefabKey;
            public Vector3 position;
            public Vector3 euler;
            public Vector3 scale;
            public int lodGroup;
            public bool interactive;
        }

        [Serializable]
        public sealed class ParsedMapData
        {
            public int width;
            public int height;
            public float tileSize;
            public float[,] heights;
            public TerrainAttribute[,] attributes;
            public List<WorldObjectRecord> objects = new();
        }

        [Header("Bindings")]
        [SerializeField] private MapAssetBinding[] _maps = Array.Empty<MapAssetBinding>();

        [Header("Streaming")]
        [SerializeField, Min(8)] private int _chunkSize = 32;
        [SerializeField] private int _streamRadius = 1; // 1 => 3x3
        [SerializeField, Min(0.1f)] private float _streamUpdateInterval = 0.35f;
        [SerializeField] private Transform _player;

        [Header("Transition")]
        [SerializeField] private CanvasGroup _fadeCanvas;
        [SerializeField, Min(0.05f)] private float _fadeDuration = 0.3f;

        private readonly Dictionary<MapId, ParsedMapData> _cache = new();
        private readonly Dictionary<Vector2Int, ChunkRuntime> _loadedChunks = new();

        private ParsedMapData _activeMap;
        private MapId _activeMapId;
        private float _nextStreamTick;
        private bool _isTransitioning;

        public MapId ActiveMapId => _activeMapId;
        public ParsedMapData ActiveMap => _activeMap;

        public event Action<MapId> OnMapLoaded;
        public event Action<MapId> OnMapTransitionStarted;
        public event Action<MapId> OnMapTransitionCompleted;
        public event Action<List<WorldObjectRecord>> OnChunkObjectsLoaded;

        private sealed class ChunkRuntime
        {
            public Vector2Int coord;
            public GameObject root;
        }

        private void Awake()
        {
            if (_player == null)
                _player = FindAnyObjectByType<MuLike.Gameplay.Controllers.CharacterMotor>()?.transform;

            if (_fadeCanvas != null)
            {
                _fadeCanvas.alpha = 0f;
                _fadeCanvas.blocksRaycasts = false;
            }
        }

        private void Update()
        {
            if (_activeMap == null || _player == null || _isTransitioning)
                return;

            if (Time.unscaledTime < _nextStreamTick)
                return;

            _nextStreamTick = Time.unscaledTime + _streamUpdateInterval;
            StreamChunksAroundPlayer();
        }

        public void LoadMapImmediate(MapId mapId)
        {
            if (!TryGetBinding(mapId, out MapAssetBinding binding))
                return;

            ParsedMapData parsed = ParseOrGet(mapId, binding);
            if (parsed == null)
                return;

            _activeMapId = mapId;
            _activeMap = parsed;
            ClearAllChunks();
            StreamChunksAroundPlayer(force: true);
            OnMapLoaded?.Invoke(mapId);
        }

        public void TransitionToMap(MapId mapId)
        {
            if (!isActiveAndEnabled)
            {
                LoadMapImmediate(mapId);
                return;
            }

            StartCoroutine(TransitionRoutine(mapId));
        }

        private System.Collections.IEnumerator TransitionRoutine(MapId mapId)
        {
            if (_isTransitioning)
                yield break;

            _isTransitioning = true;
            OnMapTransitionStarted?.Invoke(mapId);

            yield return Fade(0f, 1f);
            LoadMapImmediate(mapId);
            yield return Fade(1f, 0f);

            _isTransitioning = false;
            OnMapTransitionCompleted?.Invoke(mapId);
        }

        private System.Collections.IEnumerator Fade(float from, float to)
        {
            if (_fadeCanvas == null)
                yield break;

            _fadeCanvas.blocksRaycasts = true;
            float t = 0f;
            while (t < 1f)
            {
                t += Time.unscaledDeltaTime / Mathf.Max(0.05f, _fadeDuration);
                _fadeCanvas.alpha = Mathf.Lerp(from, to, t);
                yield return null;
            }

            _fadeCanvas.alpha = to;
            _fadeCanvas.blocksRaycasts = to > 0.01f;
        }

        private void StreamChunksAroundPlayer(bool force = false)
        {
            if (_activeMap == null || _player == null)
                return;

            Vector2Int center = WorldToChunk(_player.position);
            var required = new HashSet<Vector2Int>();
            for (int y = -_streamRadius; y <= _streamRadius; y++)
            {
                for (int x = -_streamRadius; x <= _streamRadius; x++)
                    required.Add(new Vector2Int(center.x + x, center.y + y));
            }

            // Unload out-of-range chunks
            var toRemove = new List<Vector2Int>();
            foreach (var kv in _loadedChunks)
            {
                if (!required.Contains(kv.Key))
                    toRemove.Add(kv.Key);
            }

            for (int i = 0; i < toRemove.Count; i++)
                UnloadChunk(toRemove[i]);

            // Load new chunks
            foreach (Vector2Int coord in required)
            {
                if (_loadedChunks.ContainsKey(coord) && !force)
                    continue;

                if (_loadedChunks.ContainsKey(coord))
                    UnloadChunk(coord);

                LoadChunk(coord);
            }
        }

        private void LoadChunk(Vector2Int coord)
        {
            if (_activeMap == null)
                return;

            int startX = coord.x * _chunkSize;
            int startY = coord.y * _chunkSize;
            if (startX >= _activeMap.width || startY >= _activeMap.height)
                return;

            GameObject root = new($"Chunk_{coord.x}_{coord.y}");
            root.transform.SetParent(transform, false);

            List<WorldObjectRecord> objectsInChunk = new();
            for (int i = 0; i < _activeMap.objects.Count; i++)
            {
                WorldObjectRecord obj = _activeMap.objects[i];
                if (!BelongsToChunk(obj.position, coord))
                    continue;

                objectsInChunk.Add(obj);
            }

            _loadedChunks[coord] = new ChunkRuntime { coord = coord, root = root };
            OnChunkObjectsLoaded?.Invoke(objectsInChunk);
        }

        private void UnloadChunk(Vector2Int coord)
        {
            if (!_loadedChunks.TryGetValue(coord, out ChunkRuntime chunk))
                return;

            if (chunk.root != null)
                Destroy(chunk.root);

            _loadedChunks.Remove(coord);
        }

        private void ClearAllChunks()
        {
            foreach (var kv in _loadedChunks)
            {
                if (kv.Value.root != null)
                    Destroy(kv.Value.root);
            }

            _loadedChunks.Clear();
        }

        private Vector2Int WorldToChunk(Vector3 world)
        {
            int cx = Mathf.FloorToInt(world.x / Mathf.Max(1, _chunkSize));
            int cy = Mathf.FloorToInt(world.z / Mathf.Max(1, _chunkSize));
            return new Vector2Int(cx, cy);
        }

        private bool BelongsToChunk(Vector3 worldPos, Vector2Int chunk)
        {
            Vector2Int c = WorldToChunk(worldPos);
            return c == chunk;
        }

        private bool TryGetBinding(MapId mapId, out MapAssetBinding binding)
        {
            for (int i = 0; i < _maps.Length; i++)
            {
                if (_maps[i].mapId != mapId)
                    continue;

                binding = _maps[i];
                return true;
            }

            binding = default;
            return false;
        }

        private ParsedMapData ParseOrGet(MapId mapId, MapAssetBinding binding)
        {
            if (_cache.TryGetValue(mapId, out ParsedMapData cached) && cached != null)
                return cached;

            ParsedMapData data = new ParsedMapData();
            ParseMapFile(binding.mapFile, data);
            ParseAttFile(binding.attFile, data);
            ParseObjsFile(binding.objsFile, data);
            _cache[mapId] = data;
            return data;
        }

        // MAP parser: expected binary layout [int w][int h][float tileSize][w*h float heights]
        private static void ParseMapFile(TextAsset map, ParsedMapData data)
        {
            if (map == null || data == null)
            {
                ApplyDefault(data);
                return;
            }

            try
            {
                using MemoryStream ms = new(map.bytes, false);
                using BinaryReader br = new(ms);

                int w = br.ReadInt32();
                int h = br.ReadInt32();
                float tile = br.ReadSingle();
                if (w <= 0 || h <= 0 || w > 2048 || h > 2048)
                {
                    ApplyDefault(data);
                    return;
                }

                data.width = w;
                data.height = h;
                data.tileSize = Mathf.Max(0.1f, tile);
                data.heights = new float[w, h];

                for (int y = 0; y < h; y++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        if (ms.Position + sizeof(float) > ms.Length)
                            break;

                        data.heights[x, y] = br.ReadSingle();
                    }
                }
            }
            catch
            {
                ApplyDefault(data);
            }
        }

        // ATT parser: expected [int w][int h][w*h byte flags]
        private static void ParseAttFile(TextAsset att, ParsedMapData data)
        {
            EnsureDimensions(data);
            data.attributes = new TerrainAttribute[data.width, data.height];

            if (att == null)
            {
                FillDefaultWalkable(data.attributes, data.width, data.height);
                return;
            }

            try
            {
                using MemoryStream ms = new(att.bytes, false);
                using BinaryReader br = new(ms);

                int w = br.ReadInt32();
                int h = br.ReadInt32();
                if (w != data.width || h != data.height)
                {
                    FillDefaultWalkable(data.attributes, data.width, data.height);
                    return;
                }

                for (int y = 0; y < h; y++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        if (ms.Position >= ms.Length)
                        {
                            data.attributes[x, y] = TerrainAttribute.Walkable;
                            continue;
                        }

                        data.attributes[x, y] = (TerrainAttribute)br.ReadByte();
                    }
                }
            }
            catch
            {
                FillDefaultWalkable(data.attributes, data.width, data.height);
            }
        }

        // OBJS parser: expected [int count]{int id,byte type,float px,py,pz,float rx,ry,rz,float sx,sy,sz,int lod,byte interactive,byte nameLen,nameBytes}
        private static void ParseObjsFile(TextAsset objs, ParsedMapData data)
        {
            data.objects ??= new List<WorldObjectRecord>();
            data.objects.Clear();

            if (objs == null)
                return;

            try
            {
                using MemoryStream ms = new(objs.bytes, false);
                using BinaryReader br = new(ms);

                int count = br.ReadInt32();
                count = Mathf.Clamp(count, 0, 50000);

                for (int i = 0; i < count; i++)
                {
                    if (ms.Position + 4 + 1 + 12 + 12 + 12 + 4 + 1 + 1 > ms.Length)
                        break;

                    int id = br.ReadInt32();
                    WorldObjectType type = (WorldObjectType)Mathf.Clamp(br.ReadByte(), 0, 4);
                    Vector3 pos = new(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
                    Vector3 rot = new(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
                    Vector3 scl = new(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
                    int lod = br.ReadInt32();
                    bool interactive = br.ReadByte() != 0;

                    byte nameLen = br.ReadByte();
                    string prefabKey = nameLen > 0 && ms.Position + nameLen <= ms.Length
                        ? System.Text.Encoding.UTF8.GetString(br.ReadBytes(nameLen))
                        : string.Empty;

                    data.objects.Add(new WorldObjectRecord
                    {
                        id = id,
                        type = type,
                        prefabKey = prefabKey,
                        position = pos,
                        euler = rot,
                        scale = scl,
                        lodGroup = Mathf.Max(0, lod),
                        interactive = interactive
                    });
                }
            }
            catch
            {
                data.objects.Clear();
            }
        }

        private static void ApplyDefault(ParsedMapData data)
        {
            if (data == null)
                return;

            data.width = 256;
            data.height = 256;
            data.tileSize = 1f;
            data.heights = new float[data.width, data.height];
        }

        private static void EnsureDimensions(ParsedMapData data)
        {
            if (data == null)
                return;

            if (data.width <= 0 || data.height <= 0)
            {
                data.width = 256;
                data.height = 256;
                data.tileSize = 1f;
            }

            if (data.heights == null || data.heights.GetLength(0) != data.width || data.heights.GetLength(1) != data.height)
                data.heights = new float[data.width, data.height];
        }

        private static void FillDefaultWalkable(TerrainAttribute[,] att, int width, int height)
        {
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                    att[x, y] = TerrainAttribute.Walkable;
            }
        }
    }
}
