using System;
using System.Threading;
using System.Threading.Tasks;

namespace MuLike.Networking
{
    /// <summary>
    /// Handles skill cast commands and parsed skill responses.
    /// </summary>
    public sealed class SkillClientService
    {
        private readonly IGameConnection _connection;
        private bool _isAwaitingSkillResponse;
        private int _lastSkillId;
        private int _lastTargetId;
        private CancellationTokenSource _skillTimeoutCts;

        public int RequestTimeoutMs { get; set; } = 8_000;

        public SkillClientService(IGameConnection connection, NetworkEventStream eventStream)
        {
            _connection = connection ?? throw new ArgumentNullException(nameof(connection));

            if (eventStream == null) throw new ArgumentNullException(nameof(eventStream));
            eventStream.SkillResponseReceived += HandleSkillResponse;
        }

        public event Action<bool, int, int, string> SkillResultReceived;

        public Task CastAsync(int skillId, int targetId)
        {
            if (!_connection.IsConnected)
                return Task.CompletedTask;

            if (_isAwaitingSkillResponse)
                return Task.CompletedTask;

            byte[] packet = ClientMessageFactory.CreateSkillCastRequest(skillId, targetId);
            _lastSkillId = skillId;
            _lastTargetId = targetId;
            _isAwaitingSkillResponse = true;
            ArmSkillTimeout();
            return _connection.SendAsync(packet);
        }

        private void HandleSkillResponse(bool success, int targetId, int damage, string message)
        {
            _isAwaitingSkillResponse = false;
            CancelSkillTimeout();
            SkillResultReceived?.Invoke(success, targetId, damage, message);
        }

        private void ArmSkillTimeout()
        {
            CancelSkillTimeout();
            _skillTimeoutCts = new CancellationTokenSource();
            _ = MonitorSkillTimeoutAsync(_skillTimeoutCts.Token);
        }

        private async Task MonitorSkillTimeoutAsync(CancellationToken token)
        {
            try
            {
                await Task.Delay(Math.Max(500, RequestTimeoutMs), token);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            if (!_isAwaitingSkillResponse)
                return;

            _isAwaitingSkillResponse = false;
            SkillResultReceived?.Invoke(false, _lastTargetId, 0, $"Skill {_lastSkillId} request timeout.");
        }

        private void CancelSkillTimeout()
        {
            if (_skillTimeoutCts == null)
                return;

            _skillTimeoutCts.Cancel();
            _skillTimeoutCts.Dispose();
            _skillTimeoutCts = null;
        }
    }
}
