using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MuLike.Data.DTO;
using MuLike.Networking;
using UnityEngine;

namespace MuLike.UI.CharacterSelect
{
    public enum CharacterSelectServiceState
    {
        Idle,
        Loading,
        Reconnecting,
        Ready,
        Failed
    }

    public interface ICharacterSelectRuntimeService : ICharacterSelectService
    {
        event Action<CharacterSelectServiceState, string> StateChanged;
    }

    public interface ICharacterSelectBackendApi
    {
        // Suggested contract for server API.
        // GET /api/characters
        // POST /api/characters/create
        // POST /api/characters/delete
        // POST /api/characters/enter-world
        Task<IReadOnlyList<CharacterSummaryDto>> GetCharactersAsync(CancellationToken ct);
        Task<CharacterSelectOperationResultDto> CreateCharacterAsync(CreateCharacterRequestDto request, CancellationToken ct);
        Task<CharacterSelectOperationResultDto> DeleteCharacterAsync(int characterId, CancellationToken ct);
        Task<EnterWorldResultDto> EnterWorldAsync(int characterId, CancellationToken ct);
    }

    /// <summary>
    /// Professional character select service with loading/reconnect states and backend fallback.
    /// </summary>
    public sealed class CharacterSelectService : ICharacterSelectRuntimeService
    {
        private readonly NetworkGameClient _networkClient;
        private readonly ICharacterSelectService _fallback;
        private readonly ICharacterSelectBackendApi _backendApi;
        private readonly int _connectTimeoutMs;

        public event Action<CharacterSelectServiceState, string> StateChanged;

        public CharacterSelectService(
            NetworkGameClient networkClient,
            ICharacterSelectService fallback,
            ICharacterSelectBackendApi backendApi = null,
            int connectTimeoutMs = 10_000)
        {
            _networkClient = networkClient;
            _fallback = fallback ?? throw new ArgumentNullException(nameof(fallback));
            _backendApi = backendApi;
            _connectTimeoutMs = Mathf.Max(1000, connectTimeoutMs);
        }

        public async Task<IReadOnlyList<CharacterSummaryDto>> GetCharactersAsync()
        {
            EmitState(CharacterSelectServiceState.Loading, "Loading characters...");

            if (await TryEnsureNetworkReadyAsync())
            {
                if (_backendApi != null)
                {
                    try
                    {
                        var remote = await _backendApi.GetCharactersAsync(CancellationToken.None);
                        if (remote != null)
                        {
                            EmitState(CharacterSelectServiceState.Ready, "Characters loaded from server.");
                            return remote;
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[CharacterSelectService] Server list failed: {ex.Message}");
                    }
                }
            }

            IReadOnlyList<CharacterSummaryDto> local = await _fallback.GetCharactersAsync();
            EmitState(CharacterSelectServiceState.Ready, "Using local character cache.");
            return local;
        }

        public async Task<CharacterSelectOperationResultDto> CreateCharacterAsync(CreateCharacterRequestDto request)
        {
            EmitState(CharacterSelectServiceState.Loading, "Creating character...");

            if (await TryEnsureNetworkReadyAsync())
            {
                if (_backendApi != null)
                {
                    try
                    {
                        CharacterSelectOperationResultDto result = await _backendApi.CreateCharacterAsync(request, CancellationToken.None);
                        EmitState(result.success ? CharacterSelectServiceState.Ready : CharacterSelectServiceState.Failed, result.message);
                        return result;
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[CharacterSelectService] Server create failed: {ex.Message}");
                    }
                }
            }

            CharacterSelectOperationResultDto fallbackResult = await _fallback.CreateCharacterAsync(request);
            EmitState(fallbackResult.success ? CharacterSelectServiceState.Ready : CharacterSelectServiceState.Failed, fallbackResult.message);
            return fallbackResult;
        }

        public async Task<CharacterSelectOperationResultDto> DeleteCharacterAsync(int characterId)
        {
            EmitState(CharacterSelectServiceState.Loading, "Deleting character...");

            if (await TryEnsureNetworkReadyAsync())
            {
                if (_backendApi != null)
                {
                    try
                    {
                        CharacterSelectOperationResultDto result = await _backendApi.DeleteCharacterAsync(characterId, CancellationToken.None);
                        EmitState(result.success ? CharacterSelectServiceState.Ready : CharacterSelectServiceState.Failed, result.message);
                        return result;
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[CharacterSelectService] Server delete failed: {ex.Message}");
                    }
                }
            }

            CharacterSelectOperationResultDto fallbackResult = await _fallback.DeleteCharacterAsync(characterId);
            EmitState(fallbackResult.success ? CharacterSelectServiceState.Ready : CharacterSelectServiceState.Failed, fallbackResult.message);
            return fallbackResult;
        }

        public async Task<EnterWorldResultDto> EnterWorldAsync(int characterId)
        {
            EmitState(CharacterSelectServiceState.Loading, "Entering world...");

            if (await TryEnsureNetworkReadyAsync())
            {
                if (_backendApi != null)
                {
                    try
                    {
                        EnterWorldResultDto result = await _backendApi.EnterWorldAsync(characterId, CancellationToken.None);
                        EmitState(result.success ? CharacterSelectServiceState.Ready : CharacterSelectServiceState.Failed, result.message);
                        return result;
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[CharacterSelectService] Server enter world failed: {ex.Message}");
                    }
                }
            }

            EnterWorldResultDto fallbackResult = await _fallback.EnterWorldAsync(characterId);
            EmitState(fallbackResult.success ? CharacterSelectServiceState.Ready : CharacterSelectServiceState.Failed, fallbackResult.message);
            return fallbackResult;
        }

        private async Task<bool> TryEnsureNetworkReadyAsync()
        {
            if (_networkClient == null)
                return false;

            if (_networkClient.IsConnected)
                return true;

            EmitState(CharacterSelectServiceState.Reconnecting, "Reconnecting...");
            bool connected = await _networkClient.EnsureConnectedAsync(_connectTimeoutMs, CancellationToken.None);
            if (!connected)
            {
                EmitState(CharacterSelectServiceState.Failed, "Could not reconnect. Using fallback.");
                return false;
            }

            EmitState(CharacterSelectServiceState.Ready, "Connection restored.");
            return true;
        }

        private void EmitState(CharacterSelectServiceState state, string message)
        {
            StateChanged?.Invoke(state, message ?? string.Empty);
        }
    }
}
