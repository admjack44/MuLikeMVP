using System;
using UnityEngine;

namespace MuLike.Networking
{
    /// <summary>
    /// Handles movement updates from server snapshots.
    /// Updates local entity transforms based on authoritative server positions.
    /// </summary>
    public sealed class MovementHandler
    {
        private readonly NetworkEventStream _eventStream;

        public event Action<int, Vector3> EntityMoved;

        public MovementHandler(NetworkEventStream eventStream)
        {
            _eventStream = eventStream ?? throw new ArgumentNullException(nameof(eventStream));
            _eventStream.MoveSnapshotReceived += HandleMoveSnapshot;
        }

        private void HandleMoveSnapshot(int entityId, float x, float y, float z)
        {
            EntityMoved?.Invoke(entityId, new Vector3(x, y, z));
        }
    }
}
