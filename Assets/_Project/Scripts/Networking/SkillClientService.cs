using System;
using System.Threading.Tasks;

namespace MuLike.Networking
{
    /// <summary>
    /// Handles skill cast commands and parsed skill responses.
    /// </summary>
    public sealed class SkillClientService
    {
        private readonly IGameConnection _connection;

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

            byte[] packet = ClientMessageFactory.CreateSkillCastRequest(skillId, targetId);
            return _connection.SendAsync(packet);
        }

        private void HandleSkillResponse(bool success, int targetId, int damage, string message)
        {
            SkillResultReceived?.Invoke(success, targetId, damage, message);
        }
    }
}
