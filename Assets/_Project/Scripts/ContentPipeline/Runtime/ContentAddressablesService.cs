using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
#if MULIKE_USE_ADDRESSABLES
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
#endif

namespace MuLike.ContentPipeline.Runtime
{
    public static class ContentAddressablesLabels
    {
        public const string Ui = "ui";
        public const string Maps = "maps";
        public const string Characters = "characters";
        public const string Monsters = "monsters";
        public const string Skills = "skills";
        public const string Audio = "audio";
        public const string Vfx = "vfx";

        public static readonly string[] LoginPreload =
        {
            Ui,
            Audio
        };

        public static readonly string[] WorldHudPreload =
        {
            Ui,
            Skills,
            Audio,
            Vfx,
            Characters,
            Monsters,
            Maps
        };
    }

    public interface IContentAddressablesService
    {
        bool HasAddressables { get; }
        Task<T> LoadAssetAsync<T>(string key, CancellationToken ct = default) where T : UnityEngine.Object;
        Task<IReadOnlyList<T>> LoadAssetsByLabelAsync<T>(string label, CancellationToken ct = default) where T : UnityEngine.Object;
        void Release(object handleOrAsset);
        Task<bool> PreloadGroupAsync(string groupName, IEnumerable<string> labels, CancellationToken ct = default);
    }

    public sealed class ContentAddressablesService : IContentAddressablesService, IDisposable
    {
        private readonly HashSet<string> _preloadedGroups = new(StringComparer.OrdinalIgnoreCase);

#if MULIKE_USE_ADDRESSABLES
        private readonly Dictionary<object, AsyncOperationHandle> _trackedAddressablesHandles = new();
#endif

        public bool HasAddressables
        {
            get
            {
#if MULIKE_USE_ADDRESSABLES
                return true;
#else
                return false;
#endif
            }
        }

        public async Task<T> LoadAssetAsync<T>(string key, CancellationToken ct = default) where T : UnityEngine.Object
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                Debug.LogWarning("[ContentAddressablesService] LoadAssetAsync received an empty key.");
                return null;
            }

#if MULIKE_USE_ADDRESSABLES
            try
            {
                AsyncOperationHandle<T> handle = Addressables.LoadAssetAsync<T>(key);
                await handle.Task;

                if (ct.IsCancellationRequested)
                {
                    Addressables.Release(handle);
                    return null;
                }

                if (handle.Status == AsyncOperationStatus.Succeeded && handle.Result != null)
                {
                    _trackedAddressablesHandles[handle.Result] = handle;
                    return handle.Result;
                }

                Debug.LogWarning($"[ContentAddressablesService] Addressables load failed for key '{key}'. Falling back to Resources.");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ContentAddressablesService] Addressables exception while loading '{key}': {ex.Message}. Falling back to Resources.");
            }
#else
            Debug.Log($"[ContentAddressablesService] Addressables unavailable. Loading '{key}' from Resources.");
#endif

            T fallback = await LoadFromResourcesAsync<T>(key, ct);
            if (fallback == null)
                Debug.LogWarning($"[ContentAddressablesService] Resource fallback failed for key '{key}'. Asset not found.");

            return fallback;
        }

        public async Task<IReadOnlyList<T>> LoadAssetsByLabelAsync<T>(string label, CancellationToken ct = default) where T : UnityEngine.Object
        {
            var result = new List<T>();
            if (string.IsNullOrWhiteSpace(label))
            {
                Debug.LogWarning("[ContentAddressablesService] LoadAssetsByLabelAsync received an empty label.");
                return result;
            }

#if MULIKE_USE_ADDRESSABLES
            try
            {
                AsyncOperationHandle<IList<T>> handle = Addressables.LoadAssetsAsync<T>(label, null);
                await handle.Task;

                if (ct.IsCancellationRequested)
                {
                    Addressables.Release(handle);
                    return result;
                }

                if (handle.Status == AsyncOperationStatus.Succeeded && handle.Result != null)
                {
                    for (int i = 0; i < handle.Result.Count; i++)
                    {
                        T asset = handle.Result[i];
                        if (asset == null)
                            continue;

                        result.Add(asset);
                        _trackedAddressablesHandles[asset] = handle;
                    }

                    return result;
                }

                Debug.LogWarning($"[ContentAddressablesService] Addressables label load failed for '{label}'. Falling back to Resources folder load.");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ContentAddressablesService] Addressables exception while loading label '{label}': {ex.Message}. Falling back to Resources folder load.");
            }
#endif

            T[] fallbackAssets = Resources.LoadAll<T>(label);
            if (fallbackAssets != null && fallbackAssets.Length > 0)
            {
                result.AddRange(fallbackAssets);
                return result;
            }

            Debug.LogWarning($"[ContentAddressablesService] No assets found for label/path '{label}' in Addressables or Resources.");
            await Task.CompletedTask;
            return result;
        }

        public void Release(object handleOrAsset)
        {
            if (handleOrAsset == null)
                return;

#if MULIKE_USE_ADDRESSABLES
            try
            {
                if (handleOrAsset is AsyncOperationHandle directHandle)
                {
                    if (directHandle.IsValid())
                        Addressables.Release(directHandle);

                    return;
                }

                if (_trackedAddressablesHandles.TryGetValue(handleOrAsset, out AsyncOperationHandle tracked))
                {
                    if (tracked.IsValid())
                        Addressables.Release(tracked);

                    _trackedAddressablesHandles.Remove(handleOrAsset);
                    return;
                }

                Addressables.Release(handleOrAsset);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ContentAddressablesService] Release failed for '{handleOrAsset}': {ex.Message}");
            }
#else
            Debug.Log($"[ContentAddressablesService] Release ignored for '{handleOrAsset}'. Addressables unavailable and Resources assets are managed by Unity.");
#endif
        }

        public async Task<bool> PreloadGroupAsync(string groupName, IEnumerable<string> labels, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(groupName))
            {
                Debug.LogWarning("[ContentAddressablesService] PreloadGroupAsync received an empty group name.");
                return false;
            }

            if (_preloadedGroups.Contains(groupName))
            {
                Debug.Log($"[ContentAddressablesService] Group '{groupName}' already preloaded. Skipping.");
                return true;
            }

            if (labels == null)
            {
                Debug.LogWarning($"[ContentAddressablesService] Group '{groupName}' has no labels.");
                return false;
            }

            int labelsProcessed = 0;
            foreach (string label in labels)
            {
                if (ct.IsCancellationRequested)
                    return false;

                IReadOnlyList<UnityEngine.Object> assets = await LoadAssetsByLabelAsync<UnityEngine.Object>(label, ct);
                labelsProcessed++;
                Debug.Log($"[ContentAddressablesService] Preload group '{groupName}' label '{label}' loaded {assets.Count} assets.");
            }

            _preloadedGroups.Add(groupName);
            Debug.Log($"[ContentAddressablesService] Preload group '{groupName}' completed. Labels processed: {labelsProcessed}.");
            return true;
        }

        public void Dispose()
        {
#if MULIKE_USE_ADDRESSABLES
            foreach (KeyValuePair<object, AsyncOperationHandle> pair in _trackedAddressablesHandles)
            {
                AsyncOperationHandle handle = pair.Value;
                if (handle.IsValid())
                    Addressables.Release(handle);
            }

            _trackedAddressablesHandles.Clear();
#endif
            _preloadedGroups.Clear();
        }

        private static async Task<T> LoadFromResourcesAsync<T>(string key, CancellationToken ct) where T : UnityEngine.Object
        {
            ResourceRequest request = Resources.LoadAsync<T>(key);
            if (request == null)
                return null;

            while (!request.isDone)
            {
                if (ct.IsCancellationRequested)
                    return null;

                await Task.Yield();
            }

            return request.asset as T;
        }
    }
}