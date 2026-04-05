using MuLike.Networking;
using MuLike.Performance.Pooling;
using MuLike.Performance.Rendering;
using UnityEngine;

namespace MuLike.Gameplay.Entities
{
    /// <summary>
    /// IEntityViewFactory implementation using MobilePoolManager.
    /// </summary>
    public sealed class PooledEntityViewFactory : MonoBehaviour, IEntityViewFactory
    {
        [SerializeField] private MobilePoolManager _poolManager;
        [SerializeField] private string _remotePlayerKey = "entity.remote-player";
        [SerializeField] private string _monsterKey = "entity.monster";
        [SerializeField] private string _petKey = "entity.pet";
        [SerializeField] private string _dropKey = "entity.drop";
        [SerializeField] private string _fallbackKey = "entity.fallback";

        public EntityView CreateView(SnapshotApplier.EntitySnapshot snapshot)
        {
            MobilePoolManager manager = _poolManager != null ? _poolManager : MobilePoolManager.Instance;
            if (manager == null)
            {
                Debug.LogWarning("[PooledEntityViewFactory] Missing MobilePoolManager.");
                return null;
            }

            string key = ResolvePoolKey(snapshot.Type);
            if (!manager.TrySpawn(key, snapshot.Position, Quaternion.Euler(0f, snapshot.RotationY, 0f), out GameObject spawned))
            {
                Debug.LogWarning($"[PooledEntityViewFactory] Could not spawn pool key: {key}");
                return null;
            }

            EntityView view = spawned.GetComponent<EntityView>();
            if (view == null)
            {
                Debug.LogWarning($"[PooledEntityViewFactory] Spawned object has no EntityView: {key}");
                manager.Release(spawned);
                return null;
            }

            if (spawned.GetComponent<EntityRecycler>() == null)
                spawned.AddComponent<EntityRecycler>();

            EnsurePerformanceComponents(spawned, snapshot.Type);

            view.Initialize(snapshot.EntityId);
            return view;
        }

        public void DestroyView(EntityView view)
        {
            if (view == null)
                return;

            MobilePoolManager manager = _poolManager != null ? _poolManager : MobilePoolManager.Instance;
            if (manager != null)
            {
                manager.Release(view.gameObject);
                return;
            }

            Destroy(view.gameObject);
        }

        private string ResolvePoolKey(SnapshotApplier.EntityType entityType)
        {
            return entityType switch
            {
                SnapshotApplier.EntityType.RemotePlayer => _remotePlayerKey,
                SnapshotApplier.EntityType.Monster => _monsterKey,
                SnapshotApplier.EntityType.Pet => _petKey,
                SnapshotApplier.EntityType.Drop => _dropKey,
                _ => _fallbackKey
            };
        }

        private static void EnsurePerformanceComponents(GameObject entityObject, SnapshotApplier.EntityType entityType)
        {
            if (entityObject == null)
                return;

            DistanceCullingController culling = entityObject.GetComponent<DistanceCullingController>();
            if (culling == null)
                culling = entityObject.AddComponent<DistanceCullingController>();

            if (entityType == SnapshotApplier.EntityType.Monster || entityType == SnapshotApplier.EntityType.RemotePlayer)
            {
                if (entityObject.GetComponent<DistanceLodController>() == null)
                    entityObject.AddComponent<DistanceLodController>();
            }
        }
    }
}
