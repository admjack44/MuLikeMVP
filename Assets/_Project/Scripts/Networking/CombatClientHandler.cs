using System;
using UnityEngine;

namespace MuLike.Networking
{
    /// <summary>
    /// Handles combat events from server: attacks, deaths, respawns.
    /// Client applies combat effects locally based on server updates.
    /// </summary>
    public sealed class CombatClientHandler
    {
        private readonly IGameConnection _connection;
        private readonly NetworkEventStream _eventStream;

        public event Action<int, bool, int, bool> AttackPerformed; // targetId, hitSuccess, damage, isCritical
        public event Action<int> EntityDied; // entityId
        public event Action<int, Vector3> EntityRespawned; // entityId, position

        public CombatClientHandler(IGameConnection connection, NetworkEventStream eventStream)
        {
            _connection = connection ?? throw new ArgumentNullException(nameof(connection));
            _eventStream = eventStream ?? throw new ArgumentNullException(nameof(eventStream));

            _eventStream.AttackResponseReceived += HandleAttackResponse;
            _eventStream.EntityDiedReceived += HandleEntityDied;
            _eventStream.EntityRespawnedReceived += HandleEntityRespawned;
        }

        public async System.Threading.Tasks.Task AttackAsync(int targetId)
        {
            if (!_connection.IsConnected)
                return;

            byte[] packet = ClientMessageFactory.CreateAttackRequest(targetId);
            await _connection.SendAsync(packet);
        }

        private void HandleAttackResponse(int targetId, bool hitSuccess, int damage, bool isCritical)
        {
            AttackPerformed?.Invoke(targetId, hitSuccess, damage, isCritical);
        }

        private void HandleEntityDied(int entityId)
        {
            EntityDied?.Invoke(entityId);
        }

        private void HandleEntityRespawned(int entityId, float x, float y, float z)
        {
            EntityRespawned?.Invoke(entityId, new Vector3(x, y, z));
        }
    }
}
