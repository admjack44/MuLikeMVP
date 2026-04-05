using System;
using MuLike.Server.Gateway;

namespace MuLike.Server.Infrastructure
{
    /// <summary>
    /// Test/development bridge that mimics transport: accepts client packet bytes and returns server response bytes.
    /// </summary>
    public sealed class InMemoryGatewayBridge
    {
        private readonly ServerPacketRouter _router;
        private readonly ServerApplication _app;

        public InMemoryGatewayBridge(ServerApplication app)
        {
            _app = app;
            _router = new ServerPacketRouter(app);
        }

        public byte[] Send(Guid sessionId, byte[] packet)
        {
            return _router.HandlePacket(sessionId, packet);
        }

        public bool TryPullSnapshotPacket(Guid sessionId, out byte[] packet)
        {
            return _app.TryCreateSnapshotPacket(sessionId, out packet);
        }
    }
}
