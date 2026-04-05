using System;
using System.Collections.Generic;
using UnityEngine;

namespace MuLike.Optimization
{
    /// <summary>
    /// Generic object pool for monsters and particle effects.
    /// </summary>
    public sealed class ObjectPool : MonoBehaviour
    {
        [Serializable]
        public sealed class PoolDefinition
        {
            public string key;
            public GameObject prefab;
            [Min(0)] public int prewarmCount = 12;
            [Min(1)] public int maxSize = 128;
            [Tooltip("Auto release particles when they stop emitting.")]
            public bool autoReleaseParticleSystems = true;
        }

        private sealed class RuntimePool
        {
            public readonly Queue<GameObject> Inactive = new Queue<GameObject>();
            public readonly HashSet<GameObject> Active = new HashSet<GameObject>();
            public GameObject Prefab;
            public int MaxSize;
            public bool AutoReleaseParticles;
        }

        [SerializeField] private PoolDefinition[] _definitions = Array.Empty<PoolDefinition>();
        [SerializeField] private bool _dontDestroyOnLoad = true;

        private readonly Dictionary<string, RuntimePool> _pools = new Dictionary<string, RuntimePool>(StringComparer.OrdinalIgnoreCase);

        public static ObjectPool Instance { get; private set; }

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

            BuildPools();
        }

        private void Update()
        {
            foreach (KeyValuePair<string, RuntimePool> pair in _pools)
                TickAutoRelease(pair.Key, pair.Value);
        }

        public bool TrySpawn(string key, Vector3 position, Quaternion rotation, out GameObject instance)
        {
            instance = null;
            RuntimePool pool;
            if (!_pools.TryGetValue(key, out pool) || pool.Prefab == null)
                return false;

            instance = pool.Inactive.Count > 0
                ? pool.Inactive.Dequeue()
                : CreateInstance(key, pool);

            if (instance == null)
                return false;

            pool.Active.Add(instance);
            instance.transform.SetParent(null, true);
            instance.transform.SetPositionAndRotation(position, rotation);
            instance.SetActive(true);

            PooledToken token = EnsureToken(instance, key);
            token.MarkSpawned();

            return true;
        }

        public void Release(GameObject instance)
        {
            if (instance == null)
                return;

            PooledToken token = instance.GetComponent<PooledToken>();
            if (token == null || string.IsNullOrWhiteSpace(token.PoolKey))
            {
                Destroy(instance);
                return;
            }

            RuntimePool pool;
            if (!_pools.TryGetValue(token.PoolKey, out pool))
            {
                Destroy(instance);
                return;
            }

            if (!pool.Active.Remove(instance))
                return;

            if (pool.Inactive.Count >= pool.MaxSize)
            {
                Destroy(instance);
                return;
            }

            token.MarkRecycled();
            instance.SetActive(false);
            instance.transform.SetParent(transform, false);
            pool.Inactive.Enqueue(instance);
        }

        public int GetActiveCount(string key)
        {
            RuntimePool pool;
            return _pools.TryGetValue(key, out pool) ? pool.Active.Count : 0;
        }

        public int GetInactiveCount(string key)
        {
            RuntimePool pool;
            return _pools.TryGetValue(key, out pool) ? pool.Inactive.Count : 0;
        }

        private void BuildPools()
        {
            _pools.Clear();

            for (int i = 0; i < _definitions.Length; i++)
            {
                PoolDefinition definition = _definitions[i];
                if (definition == null || definition.prefab == null || string.IsNullOrWhiteSpace(definition.key))
                    continue;

                RuntimePool pool = new RuntimePool
                {
                    Prefab = definition.prefab,
                    MaxSize = Mathf.Max(1, definition.maxSize),
                    AutoReleaseParticles = definition.autoReleaseParticleSystems
                };

                _pools[definition.key] = pool;

                int prewarm = Mathf.Clamp(definition.prewarmCount, 0, pool.MaxSize);
                for (int j = 0; j < prewarm; j++)
                {
                    GameObject instance = CreateInstance(definition.key, pool);
                    if (instance == null)
                        continue;

                    instance.SetActive(false);
                    pool.Inactive.Enqueue(instance);
                }
            }
        }

        private GameObject CreateInstance(string key, RuntimePool pool)
        {
            if (pool.Prefab == null)
                return null;

            GameObject instance = Instantiate(pool.Prefab, transform);
            EnsureToken(instance, key);
            return instance;
        }

        private static PooledToken EnsureToken(GameObject instance, string key)
        {
            PooledToken token = instance.GetComponent<PooledToken>();
            if (token == null)
                token = instance.AddComponent<PooledToken>();

            token.PoolKey = key;
            return token;
        }

        private void TickAutoRelease(string key, RuntimePool pool)
        {
            if (!pool.AutoReleaseParticles || pool.Active.Count == 0)
                return;

            var toRelease = ListPool<GameObject>.Get();
            try
            {
                foreach (GameObject go in pool.Active)
                {
                    if (go == null)
                    {
                        toRelease.Add(go);
                        continue;
                    }

                    ParticleSystem[] particles = go.GetComponentsInChildren<ParticleSystem>(true);
                    if (particles == null || particles.Length == 0)
                        continue;

                    bool alive = false;
                    for (int i = 0; i < particles.Length; i++)
                    {
                        if (particles[i] != null && particles[i].IsAlive(true))
                        {
                            alive = true;
                            break;
                        }
                    }

                    if (!alive)
                        toRelease.Add(go);
                }

                for (int i = 0; i < toRelease.Count; i++)
                    Release(toRelease[i]);
            }
            finally
            {
                ListPool<GameObject>.Release(toRelease);
            }
        }

        private static class ListPool<T>
        {
            private static readonly Stack<List<T>> s_pool = new Stack<List<T>>();

            public static List<T> Get()
            {
                return s_pool.Count > 0 ? s_pool.Pop() : new List<T>(64);
            }

            public static void Release(List<T> list)
            {
                if (list == null)
                    return;

                list.Clear();
                s_pool.Push(list);
            }
        }
    }

    public sealed class PooledToken : MonoBehaviour
    {
        public string PoolKey { get; set; }
        public float LastSpawnedAt { get; private set; }

        public void MarkSpawned()
        {
            LastSpawnedAt = Time.unscaledTime;
        }

        public void MarkRecycled()
        {
            // Left intentionally simple: hook for reset logic per prefab.
        }
    }
}
