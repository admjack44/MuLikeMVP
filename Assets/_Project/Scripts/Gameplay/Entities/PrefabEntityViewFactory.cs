using MuLike.Networking;
using MuLike.Performance.Rendering;
using UnityEngine;

namespace MuLike.Gameplay.Entities
{
    /// <summary>
    /// Prefab-based implementation for spawning visual entity views from snapshot type.
    /// </summary>
    public class PrefabEntityViewFactory : MonoBehaviour, IEntityViewFactory
    {
        [Header("Prefabs")]
        [SerializeField] private EntityView _remotePlayerPrefab;
        [SerializeField] private EntityView _monsterPrefab;
        [SerializeField] private EntityView _petPrefab;
        [SerializeField] private EntityView _dropPrefab;
        [SerializeField] private EntityView _fallbackPrefab;

        [Header("Parent")]
        [SerializeField] private Transform _spawnRoot;

        public EntityView CreateView(SnapshotApplier.EntitySnapshot snapshot)
        {
            EntityView prefab = SelectPrefab(snapshot.Type);
            if (prefab == null)
            {
                Debug.LogWarning($"[PrefabEntityViewFactory] Missing prefab for entity type {snapshot.Type}.");
                return null;
            }

            Transform parent = _spawnRoot != null ? _spawnRoot : transform;
            EntityView instance = Instantiate(prefab, snapshot.Position, Quaternion.Euler(0f, snapshot.RotationY, 0f), parent);
            EnsurePerformanceComponents(instance.gameObject, snapshot.Type);
            instance.Initialize(snapshot.EntityId);
            return instance;
        }

        public void DestroyView(EntityView view)
        {
            if (view == null) return;
            Destroy(view.gameObject);
        }

        private EntityView SelectPrefab(SnapshotApplier.EntityType entityType)
        {
            return entityType switch
            {
                SnapshotApplier.EntityType.RemotePlayer => _remotePlayerPrefab != null ? _remotePlayerPrefab : _fallbackPrefab,
                SnapshotApplier.EntityType.Monster => _monsterPrefab != null ? _monsterPrefab : _fallbackPrefab,
                SnapshotApplier.EntityType.Pet => _petPrefab != null ? _petPrefab : _fallbackPrefab,
                SnapshotApplier.EntityType.Drop => _dropPrefab != null ? _dropPrefab : _fallbackPrefab,
                _ => _fallbackPrefab
            };
        }

        private static void EnsurePerformanceComponents(GameObject entityObject, SnapshotApplier.EntityType entityType)
        {
            if (entityObject == null)
                return;

            if (entityObject.GetComponent<DistanceCullingController>() == null)
                entityObject.AddComponent<DistanceCullingController>();

            if (entityType == SnapshotApplier.EntityType.Monster || entityType == SnapshotApplier.EntityType.RemotePlayer)
            {
                if (entityObject.GetComponent<DistanceLodController>() == null)
                    entityObject.AddComponent<DistanceLodController>();
            }
        }
    }
}
