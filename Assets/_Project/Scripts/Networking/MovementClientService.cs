using System;
using System.Threading;
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
        private bool _isAwaitingMoveResponse;
        private Vector3 _lastRequestedPosition;
        private CancellationTokenSource _moveTimeoutCts;

        public int RequestTimeoutMs { get; set; } = 8_000;

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

            if (_isAwaitingMoveResponse)
                return Task.CompletedTask;

            byte[] packet = ClientMessageFactory.CreateMoveRequest(x, y, z);
            _lastRequestedPosition = new Vector3(x, y, z);
            _isAwaitingMoveResponse = true;
            ArmMoveTimeout();
            return _connection.SendAsync(packet);
        }

        private void HandleMoveResponse(bool success, float x, float y, float z, string message)
        {
            _isAwaitingMoveResponse = false;
            CancelMoveTimeout();
            MoveResultReceived?.Invoke(success, new Vector3(x, y, z), message);
        }

        private void ArmMoveTimeout()
        {
            CancelMoveTimeout();
            _moveTimeoutCts = new CancellationTokenSource();
            _ = MonitorMoveTimeoutAsync(_moveTimeoutCts.Token);
        }

        private async Task MonitorMoveTimeoutAsync(CancellationToken token)
        {
            try
            {
                await Task.Delay(Math.Max(500, RequestTimeoutMs), token);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            if (!_isAwaitingMoveResponse)
                return;

            _isAwaitingMoveResponse = false;
            MoveResultReceived?.Invoke(false, _lastRequestedPosition, "Move request timeout.");
        }

        private void CancelMoveTimeout()
        {
            if (_moveTimeoutCts == null)
                return;

            _moveTimeoutCts.Cancel();
            _moveTimeoutCts.Dispose();
            _moveTimeoutCts = null;
        }
    }
}
