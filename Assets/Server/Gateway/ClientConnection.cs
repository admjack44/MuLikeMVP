using System;
using System.Collections.Generic;
using System.Net;
using MuLike.Shared.Protocol;

namespace MuLike.Server.Gateway
{
    public sealed class ClientConnection
    {
        public Guid SessionId { get; }
        public EndPoint RemoteEndPoint { get; }
        public DateTime ConnectedAtUtc { get; }
        public DateTime LastHeartbeatUtc { get; private set; }
        public bool IsAuthenticated { get; private set; }
        public int? CharacterId { get; private set; }

        public uint LastFullSnapshotSequence { get; set; } = 0;
        public Dictionary<int, SnapshotEntityData> LastSnapshotEntities { get; } = new();
        public DateTime LastSnapshotSentUtc { get; set; } = DateTime.UtcNow;

        public ClientConnection(Guid sessionId, EndPoint remoteEndPoint)
        {
            SessionId = sessionId;
            RemoteEndPoint = remoteEndPoint;
            ConnectedAtUtc = DateTime.UtcNow;
            LastHeartbeatUtc = ConnectedAtUtc;
        }

        public void MarkHeartbeat()
        {
            LastHeartbeatUtc = DateTime.UtcNow;
        }

        public void MarkAuthenticated(int characterId)
        {
            IsAuthenticated = true;
            CharacterId = characterId;
        }

        public void MarkDisconnected()
        {
            IsAuthenticated = false;
            CharacterId = null;
        }
    }
}
