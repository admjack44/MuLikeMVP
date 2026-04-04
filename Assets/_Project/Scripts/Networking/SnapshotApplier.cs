using System.Collections.Generic;
using UnityEngine;

namespace MuLike.Networking
{
    /// <summary>
    /// Receives world-state snapshots from the server and applies them to the local entity views.
    /// </summary>
    public class SnapshotApplier
    {
        public struct EntitySnapshot
        {
            public int EntityId;
            public Vector3 Position;
            public float RotationY;
            public int HpCurrent;
            public int HpMax;
        }

        private readonly Dictionary<int, EntitySnapshot> _lastSnapshots = new();

        public void Apply(IReadOnlyList<EntitySnapshot> snapshots)
        {
            foreach (var snap in snapshots)
            {
                _lastSnapshots[snap.EntityId] = snap;
                ApplyToView(snap);
            }
        }

        public bool TryGetLast(int entityId, out EntitySnapshot snapshot)
        {
            return _lastSnapshots.TryGetValue(entityId, out snapshot);
        }

        private void ApplyToView(EntitySnapshot snap)
        {
            // TODO: resolve EntityView by ID from the entity registry and apply position/state
            Debug.Log($"[SnapshotApplier] Apply entity {snap.EntityId} pos={snap.Position}");
        }
    }
}
