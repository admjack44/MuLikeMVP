using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace MuLike.Server.Gateway
{
    public sealed class SessionManager
    {
        private readonly ConcurrentDictionary<Guid, ClientConnection> _sessions = new();

        public int SessionCount => _sessions.Count;

        public bool TryAdd(ClientConnection connection)
        {
            if (connection == null) return false;
            return _sessions.TryAdd(connection.SessionId, connection);
        }

        public bool TryGet(Guid sessionId, out ClientConnection connection)
        {
            return _sessions.TryGetValue(sessionId, out connection);
        }

        public bool TryMarkHeartbeat(Guid sessionId)
        {
            if (!_sessions.TryGetValue(sessionId, out ClientConnection connection))
                return false;

            connection.MarkHeartbeat();
            return true;
        }

        public bool TryRemove(Guid sessionId, out ClientConnection removed)
        {
            return _sessions.TryRemove(sessionId, out removed);
        }

        public IReadOnlyCollection<ClientConnection> GetAll()
        {
            return _sessions.Values.ToArray();
        }

        public IReadOnlyList<Guid> GetExpiredSessionIds(TimeSpan timeout, DateTime nowUtc)
        {
            if (timeout <= TimeSpan.Zero)
                return Array.Empty<Guid>();

            return _sessions.Values
                .Where(c => nowUtc - c.LastHeartbeatUtc > timeout)
                .Select(c => c.SessionId)
                .ToArray();
        }
    }
}
