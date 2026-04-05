using System;
using UnityEngine;
#if MULIKE_USE_ADDRESSABLES
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
#endif

namespace MuLike.Performance.Content
{
    /// <summary>
    /// Addressables-first content loader with reflection fallback when package is missing.
    /// </summary>
    public static class AddressablesContentLoader
    {
        public static bool HasAddressables
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

        public static void LoadGameObjectAsync(string address, Action<GameObject> onLoaded)
        {
            if (string.IsNullOrWhiteSpace(address))
            {
                onLoaded?.Invoke(null);
                return;
            }

#if MULIKE_USE_ADDRESSABLES
            AsyncOperationHandle<GameObject> handle = Addressables.LoadAssetAsync<GameObject>(address);
            handle.Completed += op => onLoaded?.Invoke(op.Result);
#else
            ResourceRequest fallback = Resources.LoadAsync<GameObject>(address);
            fallback.completed += _ => onLoaded?.Invoke(fallback.asset as GameObject);
#endif
        }

        public static void Release(object handleOrInstance)
        {
            if (!HasAddressables || handleOrInstance == null)
                return;

#if MULIKE_USE_ADDRESSABLES
            Addressables.Release(handleOrInstance);
#endif
        }
    }
}
