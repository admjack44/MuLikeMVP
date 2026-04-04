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
    public sealed class NetworkCharacterSelectService : ICharacterSelectService
    {
        private readonly NetworkGameClient _networkClient;
        private readonly ICharacterSelectService _fallback;

        public NetworkCharacterSelectService(NetworkGameClient networkClient, ICharacterSelectService fallback)
        {
            _networkClient = networkClient;
            _fallback = fallback;
        }

        public async Task<IReadOnlyList<CharacterSummaryDto>> GetCharactersAsync()
        {
            if (CanUseNetworkContract())
            {
                Debug.Log("[NetworkCharacterSelectService] Character list contract is pending; using local fallback.");
            }

            return await _fallback.GetCharactersAsync();
        }

        public async Task<CharacterSelectOperationResultDto> CreateCharacterAsync(CreateCharacterRequestDto request)
        {
            if (CanUseNetworkContract())
            {
                Debug.Log("[NetworkCharacterSelectService] Create character contract is pending; using local fallback.");
            }

            return await _fallback.CreateCharacterAsync(request);
        }

        public async Task<CharacterSelectOperationResultDto> DeleteCharacterAsync(int characterId)
        {
            if (CanUseNetworkContract())
            {
                Debug.Log("[NetworkCharacterSelectService] Delete character contract is pending; using local fallback.");
            }

            return await _fallback.DeleteCharacterAsync(characterId);
        }

        public async Task<EnterWorldResultDto> EnterWorldAsync(int characterId)
        {
            if (CanUseNetworkContract())
            {
                Debug.Log("[NetworkCharacterSelectService] Enter world contract is pending; using local fallback.");
            }

            return await _fallback.EnterWorldAsync(characterId);
        }

        private bool CanUseNetworkContract()
        {
            return _networkClient != null && _networkClient.IsConnected && _networkClient.IsAuthenticated;
        }
    }
}
