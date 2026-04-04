using System;
using System.Threading.Tasks;
using UnityEngine;

namespace MuLike.Networking
{
    /// <summary>
    /// Handles movement commands and parsed movement responses.
    /// </summary>
    public sealed class MovementClientService
    {
        private readonly IGameConnection _connection;

        public MovementClientService(IGameConnection connection, NetworkEventStream eventStream)
        {
            _connection = connection ?? throw new ArgumentNullException(nameof(connection));

            if (eventStream == null) throw new ArgumentNullException(nameof(eventStream));
            eventStream.MoveResponseReceived += HandleMoveResponse;
        }

        public event Action<bool, Vector3, string> MoveResultReceived;

        public Task MoveAsync(float x, float y, float z)
        {
            if (!_connection.IsConnected)
                return Task.CompletedTask;

            byte[] packet = ClientMessageFactory.CreateMoveRequest(x, y, z);
            return _connection.SendAsync(packet);
        }

        private void HandleMoveResponse(bool success, float x, float y, float z, string message)
        {
            MoveResultReceived?.Invoke(success, new Vector3(x, y, z), message);
        }
    }
}
