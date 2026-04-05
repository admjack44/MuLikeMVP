using System.Collections.Generic;
using System.Threading.Tasks;
using MuLike.Data.DTO;
using MuLike.Networking;
using UnityEngine;

namespace MuLike.UI.CharacterSelect
{
    /// <summary>
    /// Network-ready adapter for character select operations.
    /// Currently delegates to a local fallback until dedicated character opcodes are implemented.
    /// </summary>
    public sealed class NetworkCharacterSelectService : ICharacterSelectRuntimeService, ICharacterSelectService
    {
        private readonly NetworkGameClient _networkClient;
        private readonly ICharacterSelectService _fallback;
        private readonly int _connectTimeoutMs;

        public event System.Action<CharacterSelectServiceState, string> StateChanged;

        public NetworkCharacterSelectService(NetworkGameClient networkClient, ICharacterSelectService fallback, int connectTimeoutMs = 10000)
        {
            _networkClient = networkClient;
            _fallback = fallback;
            _connectTimeoutMs = Mathf.Max(1000, connectTimeoutMs);
        }

        public async Task<IReadOnlyList<CharacterSummaryDto>> GetCharactersAsync()
        {
            EmitState(CharacterSelectServiceState.Loading, "Loading characters...");

            if (await TryReconnectAsync())
            {
                Debug.Log("[NetworkCharacterSelectService] Character list contract is pending; using local fallback.");
            }

            IReadOnlyList<CharacterSummaryDto> characters = await _fallback.GetCharactersAsync();
            if (characters == null)
                return characters;

            for (int i = 0; i < characters.Count; i++)
            {
                CharacterSummaryDto c = characters[i];
                if (c == null)
                    continue;

                if (c.powerScore <= 0)
                    c.powerScore = Mathf.Max(100, c.level * 280);

                if (string.IsNullOrWhiteSpace(c.mapName))
                    c.mapName = c.mapId == 1 ? "Lorencia" : "World";
            }

            EmitState(CharacterSelectServiceState.Ready, "Characters loaded.");
            return characters;
        }

        public async Task<CharacterSelectOperationResultDto> CreateCharacterAsync(CreateCharacterRequestDto request)
        {
            EmitState(CharacterSelectServiceState.Loading, "Creating character...");

            if (await TryReconnectAsync())
            {
                Debug.Log("[NetworkCharacterSelectService] Create character contract is pending; using local fallback.");
            }

            CharacterSelectOperationResultDto result = await _fallback.CreateCharacterAsync(request);
            EmitState(result.success ? CharacterSelectServiceState.Ready : CharacterSelectServiceState.Failed, result.message);
            return result;
        }

        public async Task<CharacterSelectOperationResultDto> DeleteCharacterAsync(int characterId)
        {
            EmitState(CharacterSelectServiceState.Loading, "Deleting character...");

            if (await TryReconnectAsync())
            {
                Debug.Log("[NetworkCharacterSelectService] Delete character contract is pending; using local fallback.");
            }

            CharacterSelectOperationResultDto result = await _fallback.DeleteCharacterAsync(characterId);
            EmitState(result.success ? CharacterSelectServiceState.Ready : CharacterSelectServiceState.Failed, result.message);
            return result;
        }

        public async Task<EnterWorldResultDto> EnterWorldAsync(int characterId)
        {
            EmitState(CharacterSelectServiceState.Loading, "Preparing world entry...");

            if (await TryReconnectAsync())
            {
                Debug.Log("[NetworkCharacterSelectService] Enter world contract is pending; using local fallback.");
            }

            EnterWorldResultDto result = await _fallback.EnterWorldAsync(characterId);
            EmitState(result.success ? CharacterSelectServiceState.Ready : CharacterSelectServiceState.Failed, result.message);
            return result;
        }

        private bool CanUseNetworkContract()
        {
            return _networkClient != null && _networkClient.IsConnected && _networkClient.IsAuthenticated;
        }

        private async Task<bool> TryReconnectAsync()
        {
            if (_networkClient == null)
                return false;

            if (_networkClient.IsConnected)
                return CanUseNetworkContract();

            EmitState(CharacterSelectServiceState.Reconnecting, "Reconnecting...");
            bool connected = await _networkClient.EnsureConnectedAsync(_connectTimeoutMs);
            if (!connected)
            {
                EmitState(CharacterSelectServiceState.Failed, "Could not reconnect. Using offline fallback.");
                return false;
            }

            EmitState(CharacterSelectServiceState.Ready, "Connection restored.");
            return CanUseNetworkContract();
        }

        private void EmitState(CharacterSelectServiceState state, string message)
        {
            StateChanged?.Invoke(state, message ?? string.Empty);
        }
    }
}
