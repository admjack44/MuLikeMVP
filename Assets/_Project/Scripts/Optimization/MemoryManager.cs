using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using MuLike.Performance.Content;
using MuLike.World;
using UnityEngine;
using UnityEngine.Profiling;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.U2D;
using UnityEngine.U2D;
#endif

namespace MuLike.Optimization
{
    public enum TextureMemoryTier
    {
        Tier128Mb = 128,
        Tier256Mb = 256,
        Tier512Mb = 512
    }

    /// <summary>
    /// Runtime memory manager for map unload, texture budget control and scheduled GC.
    /// </summary>
    public sealed class MemoryManager : MonoBehaviour
    {
        [Header("Dependencies")]
        [SerializeField] private MapLoader _mapLoader;
        [SerializeField] private QualityManager _qualityManager;

        [Header("Texture budget")]
        [SerializeField] private bool _autoBudgetByDeviceMemory = true;
        [SerializeField] private TextureMemoryTier _textureMemoryBudget = TextureMemoryTier.Tier256Mb;
        [SerializeField, Min(2f)] private float _memorySampleIntervalSeconds = 5f;
        [SerializeField, Min(1f)] private float _textureBudgetSoftMarginMb = 16f;

        [Header("Garbage collection")]
        [SerializeField] private bool _collectOnMapTransition = true;
        [SerializeField, Min(10f)] private float _minSecondsBetweenGc = 25f;
        [SerializeField, Range(15f, 60f)] private float _minFpsForGc = 26f;

        [Header("Map asset unload")]
        [SerializeField] private bool _releaseNonActiveMapHandles = true;

        private readonly Dictionary<string, List<object>> _mapHandles = new Dictionary<string, List<object>>(StringComparer.OrdinalIgnoreCase);

        private float _nextMemorySampleAt;
        private float _nextGcAllowedAt;
        private float _fpsEma = 60f;
        private string _activeMapKey;

        public int TextureBudgetMb => (int)_textureMemoryBudget;

        private void Awake()
        {
            if (_mapLoader == null)
                _mapLoader = FindAnyObjectByType<MapLoader>();
            if (_qualityManager == null)
                _qualityManager = FindAnyObjectByType<QualityManager>();

            if (_autoBudgetByDeviceMemory)
                _textureMemoryBudget = ResolveBudgetByRam(SystemInfo.systemMemorySize);

            _nextMemorySampleAt = Time.unscaledTime + _memorySampleIntervalSeconds;
            _nextGcAllowedAt = Time.unscaledTime + _minSecondsBetweenGc;
        }

        private void OnEnable()
        {
            if (_mapLoader == null)
                return;

            _mapLoader.OnMapLoaded += HandleMapLoaded;
            _mapLoader.OnMapTransitionCompleted += HandleMapTransitionCompleted;
        }

        private void OnDisable()
        {
            if (_mapLoader == null)
                return;

            _mapLoader.OnMapLoaded -= HandleMapLoaded;
            _mapLoader.OnMapTransitionCompleted -= HandleMapTransitionCompleted;
        }

        private void Update()
        {
            float frameFps = 1f / Mathf.Max(0.0001f, Time.unscaledDeltaTime);
            _fpsEma = Mathf.Lerp(_fpsEma, frameFps, 0.06f);

            if (Time.unscaledTime < _nextMemorySampleAt)
                return;

            _nextMemorySampleAt = Time.unscaledTime + Mathf.Max(1f, _memorySampleIntervalSeconds);
            EnforceTextureBudget();
        }

        public void RegisterMapAddressableHandle(string mapKey, object handle)
        {
            if (string.IsNullOrWhiteSpace(mapKey) || handle == null)
                return;

            if (!_mapHandles.TryGetValue(mapKey, out List<object> handles))
            {
                handles = new List<object>(8);
                _mapHandles[mapKey] = handles;
            }

            handles.Add(handle);
        }

        public void UnloadMapAssets(string mapKey)
        {
            if (string.IsNullOrWhiteSpace(mapKey))
                return;

            if (_mapHandles.TryGetValue(mapKey, out List<object> handles))
            {
                for (int i = 0; i < handles.Count; i++)
                    AddressablesContentLoader.Release(handles[i]);

                handles.Clear();
                _mapHandles.Remove(mapKey);
            }

            _ = Resources.UnloadUnusedAssets();
        }

        public void ForceCollectNow()
        {
            StartCoroutine(CollectRoutine(force: true));
        }

        private void HandleMapLoaded(MapLoader.MapId mapId)
        {
            _activeMapKey = mapId.ToString();

            if (!_releaseNonActiveMapHandles)
                return;

            var keys = new List<string>(_mapHandles.Keys);
            for (int i = 0; i < keys.Count; i++)
            {
                if (string.Equals(keys[i], _activeMapKey, StringComparison.OrdinalIgnoreCase))
                    continue;

                UnloadMapAssets(keys[i]);
            }
        }

        private void HandleMapTransitionCompleted(MapLoader.MapId _)
        {
            if (!_collectOnMapTransition)
                return;

            StartCoroutine(CollectRoutine(force: false));
        }

        private IEnumerator CollectRoutine(bool force)
        {
            if (!force && Time.unscaledTime < _nextGcAllowedAt)
                yield break;

            // Wait a couple of frames after map switch so loads/spawns settle.
            yield return null;
            yield return null;

            if (!force && _fpsEma < _minFpsForGc)
                yield break;

            yield return Resources.UnloadUnusedAssets();
            GC.Collect();
            _nextGcAllowedAt = Time.unscaledTime + Mathf.Max(5f, _minSecondsBetweenGc);

            Debug.Log($"[MemoryManager] GC completed. Texture budget={TextureBudgetMb}MB, ActiveMap={_activeMapKey}");
        }

        private void EnforceTextureBudget()
        {
            float budgetMb = TextureBudgetMb;
            float currentMb = EstimateTextureMemoryMb();
            float threshold = budgetMb + Mathf.Max(0f, _textureBudgetSoftMarginMb);

            if (currentMb <= threshold)
                return;

            QualitySettings.globalTextureMipmapLimit = Mathf.Clamp(QualitySettings.globalTextureMipmapLimit + 1, 0, 3);
            _ = Resources.UnloadUnusedAssets();

            if (_qualityManager != null)
                _qualityManager.ApplyQuality(MobileQualityLevel.Low);

            Debug.LogWarning($"[MemoryManager] Texture memory high: {currentMb:F1}MB > {threshold:F1}MB. Lowering texture detail.");
        }

        private static float EstimateTextureMemoryMb()
        {
            Texture[] textures = Resources.FindObjectsOfTypeAll<Texture>();
            long bytes = 0;
            for (int i = 0; i < textures.Length; i++)
            {
                Texture tex = textures[i];
                if (tex == null)
                    continue;

                bytes += Profiler.GetRuntimeMemorySizeLong(tex);
            }

            return bytes / (1024f * 1024f);
        }

        private static TextureMemoryTier ResolveBudgetByRam(int ramMb)
        {
            int clamped = Mathf.Max(1024, ramMb);
            if (clamped <= 3072)
                return TextureMemoryTier.Tier128Mb;
            if (clamped <= 6144)
                return TextureMemoryTier.Tier256Mb;
            return TextureMemoryTier.Tier512Mb;
        }

        public void ApplyQaProfile(MobileQaDeviceTier tier)
        {
            _autoBudgetByDeviceMemory = false;

            switch (tier)
            {
                case MobileQaDeviceTier.Low:
                    _textureMemoryBudget = TextureMemoryTier.Tier128Mb;
                    _memorySampleIntervalSeconds = 4f;
                    _textureBudgetSoftMarginMb = 12f;
                    _minSecondsBetweenGc = 20f;
                    _minFpsForGc = 24f;
                    break;
                case MobileQaDeviceTier.High:
                    _textureMemoryBudget = TextureMemoryTier.Tier512Mb;
                    _memorySampleIntervalSeconds = 7f;
                    _textureBudgetSoftMarginMb = 24f;
                    _minSecondsBetweenGc = 40f;
                    _minFpsForGc = 32f;
                    break;
                default:
                    _textureMemoryBudget = TextureMemoryTier.Tier256Mb;
                    _memorySampleIntervalSeconds = 5f;
                    _textureBudgetSoftMarginMb = 16f;
                    _minSecondsBetweenGc = 30f;
                    _minFpsForGc = 28f;
                    break;
            }

            _nextMemorySampleAt = Time.unscaledTime + _memorySampleIntervalSeconds;
        }
    }
}

#if UNITY_EDITOR
namespace MuLike.Optimization.Editor
{
    /// <summary>
    /// One-click asset import optimizations for mobile builds.
    /// </summary>
    public static class MobileAssetOptimizationTools
    {
        private const string AtlasesFolder = "Assets/_Project/Generated/Atlases";

        [MenuItem("MuLike/Optimization/Apply Texture Compression (ASTC/PVRTC)")]
        public static void ApplyTextureCompressionProfiles()
        {
            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/_Project" });
            int updated = 0;

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null)
                    continue;

                bool changed = false;

                TextureImporterPlatformSettings android = importer.GetPlatformTextureSettings("Android");
                android.overridden = true;
                android.format = TextureImporterFormat.ASTC_6x6;
                android.compressionQuality = 50;
                importer.SetPlatformTextureSettings(android);
                changed = true;

                TextureImporterPlatformSettings ios = importer.GetPlatformTextureSettings("iPhone");
                ios.overridden = true;
                ios.format = TextureImporterFormat.PVRTC_RGBA4;
                ios.compressionQuality = 50;
                importer.SetPlatformTextureSettings(ios);

                if (changed)
                {
                    importer.SaveAndReimport();
                    updated++;
                }
            }

            Debug.Log($"[MobileAssetOptimizationTools] Texture compression updated for {updated} textures.");
        }

        [MenuItem("MuLike/Optimization/Create/Update Sprite Atlases")]
        public static void BuildSpriteAtlases()
        {
            EnsureFolder("Assets/_Project/Generated");
            EnsureFolder(AtlasesFolder);

            string[] textureGuids = AssetDatabase.FindAssets("t:Sprite", new[] { "Assets/_Project/Art", "Assets/_Project/UI" });
            var sprites = new List<UnityEngine.Object>(textureGuids.Length);
            for (int i = 0; i < textureGuids.Length; i++)
            {
                string spritePath = AssetDatabase.GUIDToAssetPath(textureGuids[i]);
                UnityEngine.Object sprite = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(spritePath);
                if (sprite != null)
                    sprites.Add(sprite);
            }

            const string atlasPath = AtlasesFolder + "/MainMobile.spriteatlas";
            SpriteAtlas atlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(atlasPath);
            if (atlas == null)
            {
                atlas = new SpriteAtlas();
                AssetDatabase.CreateAsset(atlas, atlasPath);
            }

            atlas.Remove(atlas.GetPackables());
            atlas.Add(sprites.ToArray());

            SpriteAtlasPackingSettings pack = atlas.GetPackingSettings();
            pack.enableRotation = false;
            pack.enableTightPacking = false;
            pack.padding = 2;
            atlas.SetPackingSettings(pack);

            SpriteAtlasTextureSettings tex = atlas.GetTextureSettings();
            tex.generateMipMaps = false;
            tex.sRGB = true;
            tex.readable = false;
            atlas.SetTextureSettings(tex);

            EditorUtility.SetDirty(atlas);
            AssetDatabase.SaveAssets();
            SpriteAtlasUtility.PackAtlases(new[] { atlas }, EditorUserBuildSettings.activeBuildTarget, false);

            Debug.Log($"[MobileAssetOptimizationTools] Atlas packed with {sprites.Count} sprites.");
        }

        [MenuItem("MuLike/Optimization/Optimize Audio Streaming")]
        public static void OptimizeAudioStreaming()
        {
            string[] guids = AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets/_Project/Audio" });
            int updated = 0;

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                AudioImporter importer = AssetImporter.GetAtPath(path) as AudioImporter;
                if (importer == null)
                    continue;

                AudioImporterSampleSettings settings = importer.defaultSampleSettings;
                settings.loadType = AudioClipLoadType.Streaming;
                settings.compressionFormat = AudioCompressionFormat.Vorbis;
                settings.quality = 0.45f;
                importer.defaultSampleSettings = settings;
                importer.loadInBackground = true;
                importer.forceToMono = false;

                importer.SaveAndReimport();
                updated++;
            }

            Debug.Log($"[MobileAssetOptimizationTools] Updated {updated} audio clips for streaming/compression.");
        }

        [MenuItem("MuLike/Optimization/Report Build Input Size")]
        public static void ReportInputSize()
        {
            long bytes = 0;
            bytes += SumFolder("Assets/_Project/Resources");
            bytes += SumFolder("Assets/StreamingAssets");
            bytes += SumFolder("Assets/_Project/Audio");

            float mb = bytes / (1024f * 1024f);
            string message = mb < 200f
                ? $"[MobileAssetOptimizationTools] Input size estimate: {mb:F1}MB (target < 200MB)."
                : $"[MobileAssetOptimizationTools] Input size estimate: {mb:F1}MB (over 200MB target).";

            if (mb < 200f)
                Debug.Log(message);
            else
                Debug.LogWarning(message);
        }

        [MenuItem("MuLike/Optimization/Build Map Asset Bundles")]
        public static void BuildMapAssetBundles()
        {
            const string outputFolder = "Assets/../AssetBundles/Maps";
            string mapsFolder = "Assets/_Project/Art/Maps";

            if (!AssetDatabase.IsValidFolder(mapsFolder))
            {
                mapsFolder = "Assets/_Project/World";
                if (!AssetDatabase.IsValidFolder(mapsFolder))
                {
                    Debug.LogWarning("[MobileAssetOptimizationTools] No map content folder found (expected Assets/_Project/Art/Maps or Assets/_Project/World).");
                    return;
                }
            }

            Directory.CreateDirectory(Path.GetFullPath(outputFolder));

            string[] guids = AssetDatabase.FindAssets(string.Empty, new[] { mapsFolder });
            var buildMap = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (AssetDatabase.IsValidFolder(path))
                    continue;

                string mapName = ExtractMapBundleName(path);
                if (!buildMap.TryGetValue(mapName, out List<string> assets))
                {
                    assets = new List<string>(64);
                    buildMap[mapName] = assets;
                }

                assets.Add(path);
            }

            var builds = new List<AssetBundleBuild>(buildMap.Count);
            foreach (KeyValuePair<string, List<string>> kvp in buildMap)
            {
                if (kvp.Value.Count == 0)
                    continue;

                builds.Add(new AssetBundleBuild
                {
                    assetBundleName = kvp.Key,
                    assetNames = kvp.Value.ToArray()
                });
            }

            if (builds.Count == 0)
            {
                Debug.LogWarning("[MobileAssetOptimizationTools] No map assets collected for AssetBundles.");
                return;
            }

            BuildPipeline.BuildAssetBundles(Path.GetFullPath(outputFolder), builds.ToArray(), BuildAssetBundleOptions.ChunkBasedCompression, EditorUserBuildSettings.activeBuildTarget);
            Debug.Log($"[MobileAssetOptimizationTools] Built {builds.Count} map bundles to {Path.GetFullPath(outputFolder)}");
        }

        private static long SumFolder(string folder)
        {
            if (!AssetDatabase.IsValidFolder(folder))
                return 0;

            string full = Path.GetFullPath(folder);
            if (!Directory.Exists(full))
                return 0;

            long bytes = 0;
            string[] files = Directory.GetFiles(full, "*.*", SearchOption.AllDirectories);
            for (int i = 0; i < files.Length; i++)
            {
                FileInfo info = new FileInfo(files[i]);
                bytes += info.Length;
            }

            return bytes;
        }

        private static string ExtractMapBundleName(string assetPath)
        {
            string normalized = assetPath.Replace('\\', '/');
            string[] parts = normalized.Split('/');
            for (int i = 0; i < parts.Length; i++)
            {
                string part = parts[i];
                if (part.StartsWith("map", StringComparison.OrdinalIgnoreCase))
                    return part.ToLowerInvariant() + ".bundle";
            }

            string file = Path.GetFileNameWithoutExtension(assetPath);
            return "map_misc_" + file.ToLowerInvariant() + ".bundle";
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder))
                return;

            string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
            string name = Path.GetFileName(folder);
            if (!AssetDatabase.IsValidFolder(parent))
                EnsureFolder(parent);

            AssetDatabase.CreateFolder(parent, name);
        }
    }
}
#endif
