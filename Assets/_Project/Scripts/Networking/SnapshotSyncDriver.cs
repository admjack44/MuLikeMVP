using System.Collections.Generic;
using MuLike.Gameplay.Entities;
using UnityEngine;

namespace MuLike.Networking
{
    /// <summary>
    /// Runtime glue example for SnapshotApplier. Feed snapshots here when network world updates arrive.
    /// </summary>
    public class SnapshotSyncDriver : MonoBehaviour
    {
        [SerializeField] private PrefabEntityViewFactory _viewFactory;
        [SerializeField] private bool _demoFeedOnStart = false;

        private SnapshotApplier _snapshotApplier;

        private void Awake()
        {
            if (_viewFactory == null)
                _viewFactory = FindObjectOfType<PrefabEntityViewFactory>();

            _snapshotApplier = new SnapshotApplier(_viewFactory);
        }

        private void Update()
        {
            _snapshotApplier?.TickVisuals(Time.deltaTime);
        }

        /// <summary>
        /// Call this method from network message handlers once world snapshots are decoded.
        /// </summary>
        public void ApplyWorldSnapshot(IReadOnlyList<SnapshotApplier.EntitySnapshot> snapshots, bool isFullSnapshot = true)
        {
            _snapshotApplier.Apply(snapshots, isFullSnapshot);
        }

        private void Start()
        {
            if (!_demoFeedOnStart) return;

            var demo = new List<SnapshotApplier.EntitySnapshot>
            {
                new SnapshotApplier.EntitySnapshot
                {
                    EntityId = 1001,
                    Type = SnapshotApplier.EntityType.RemotePlayer,
                    Position = new Vector3(2f, 0f, 3f),
                    RotationY = 180f,
                    HpCurrent = 120,
                    HpMax = 120,
                    IsAlive = true,
                    DisplayName = "RemoteKnight",
                    OwnerEntityId = 0
                },
                new SnapshotApplier.EntitySnapshot
                {
                    EntityId = 2001,
                    Type = SnapshotApplier.EntityType.Monster,
                    Position = new Vector3(-4f, 0f, 6f),
                    RotationY = 40f,
                    HpCurrent = 85,
                    HpMax = 100,
                    IsAlive = true,
                    DisplayName = "Goblin",
                    OwnerEntityId = 0
                }
            };

            ApplyWorldSnapshot(demo, true);
        }
    }
}
