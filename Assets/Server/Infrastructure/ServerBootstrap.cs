using System;
using System.Net;
using System.Threading.Tasks;

namespace MuLike.Server.Infrastructure
{
    /// <summary>
    /// Convenience bootstrap used by tools/tests to create a running server and connected session.
    /// </summary>
    public static class ServerBootstrap
    {
        public static async Task<(ServerApplication app, InMemoryGatewayBridge bridge, Guid sessionId)> StartDefaultAsync()
        {
            var app = ServerApplication.CreateDefault();
            await app.StartAsync();

            var bridge = new InMemoryGatewayBridge(app);
            Guid sessionId = app.ConnectClient(new IPEndPoint(IPAddress.Loopback, 7777));
            return (app, bridge, sessionId);
        }
    }
}
