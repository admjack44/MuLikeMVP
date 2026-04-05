using System;
using System.Collections.Generic;
using UnityEngine;

namespace MuLike.Performance.Pooling
{
    /// <summary>
    /// Runtime pool manager for entities, FX and drops.
    /// </summary>
    public sealed class MobilePoolManager : MonoBehaviour
    {
        [Serializable]
        public sealed class PoolSetup
        {
            public string key;
            public GameObject prefab;
            public int warmupCount = 8;
            public int maxSize = 128;
            public Transform parent;
        }

        private sealed class PoolRuntime
        {
            public readonly Queue<GameObject> Inactive = new();
            public readonly HashSet<GameObject> Active = new();
            public GameObject Prefab;
            public Transform Parent;
            public int MaxSize;
        }

        [SerializeField] private PoolSetup[] _pools = Array.Empty<PoolSetup>();
        [SerializeField] private bool _dontDestroyOnLoad = true;

        private readonly Dictionary<string, PoolRuntime> _runtimes = new(StringComparer.OrdinalIgnoreCase);

        public static MobilePoolManager Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            if (_dontDestroyOnLoad)
                DontDestroyOnLoad(gameObject);

            InitializePools();
        }

        public bool TrySpawn(string key, Vector3 position, Quaternion rotation, out GameObject instance)
        {
            instance = null;
            if (!_runtimes.TryGetValue(key, out PoolRuntime runtime) || runtime.Prefab == null)
                return false;

            instance = runtime.Inactive.Count > 0
                ? runtime.Inactive.Dequeue()
                : CreateInstance(runtime, key);

            if (instance == null)
                return false;

            instance.transform.SetPositionAndRotation(position, rotation);
            instance.SetActive(true);
            runtime.Active.Add(instance);

            if (instance.TryGetComponent(out PoolableObject poolable))
                poolable.OnSpawnedFromPool();

            return true;
        }

        public void Release(GameObject instance)
        {
            if (instance == null)
                return;

            PoolableObject poolable = instance.GetComponent<PoolableObject>();
            if (poolable == null || string.IsNullOrWhiteSpace(poolable.PoolKey))
            {
                Destroy(instance);
                return;
            }

            if (!_runtimes.TryGetValue(poolable.PoolKey, out PoolRuntime runtime))
            {
                Destroy(instance);
                return;
            }

            if (!runtime.Active.Remove(instance))
                return;

            if (runtime.Inactive.Count >= runtime.MaxSize)
            {
                Destroy(instance);
                return;
            }

            poolable.OnRecycledToPool();
            instance.SetActive(false);
            instance.transform.SetParent(runtime.Parent, false);
            runtime.Inactive.Enqueue(instance);
        }

        private void InitializePools()
        {
            _runtimes.Clear();
            for (int i = 0; i < _pools.Length; i++)
            {
                PoolSetup setup = _pools[i];
                if (setup == null || string.IsNullOrWhiteSpace(setup.key) || setup.prefab == null)
                    continue;

                var runtime = new PoolRuntime
                {
                    Prefab = setup.prefab,
                    Parent = setup.parent != null ? setup.parent : transform,
                    MaxSize = Mathf.Max(8, setup.maxSize)
                };

                _runtimes[setup.key] = runtime;

                int warmup = Mathf.Clamp(setup.warmupCount, 0, runtime.MaxSize);
                for (int j = 0; j < warmup; j++)
                {
                    GameObject instance = CreateInstance(runtime, setup.key);
                    if (instance != null)
                    {
                        instance.SetActive(false);
                        runtime.Inactive.Enqueue(instance);
                    }
                }
            }
        }

        private static GameObject CreateInstance(PoolRuntime runtime, string key)
        {
            if (runtime.Prefab == null)
                return null;

            GameObject instance = Instantiate(runtime.Prefab, runtime.Parent);
            PoolableObject poolable = instance.GetComponent<PoolableObject>();
            if (poolable == null)
                poolable = instance.AddComponent<PoolableObject>();

            poolable.ConfigurePoolKey(key);
            return instance;
        }
    }
}
