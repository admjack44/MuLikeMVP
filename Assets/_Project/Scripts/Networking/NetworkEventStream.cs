using System;
using System.Collections.Generic;
using MuLike.Shared.Protocol;
using UnityEngine;

namespace MuLike.Networking
{
    /// <summary>
    /// Centralized packet event stream based on PacketRouter.
    /// </summary>
    public sealed class NetworkEventStream
    {
        private readonly PacketRouter _router;

        public NetworkEventStream(PacketRouter router)
        {
            _router = router ?? throw new ArgumentNullException(nameof(router));
            RegisterRoutes();
        }

        public event Action<bool, string, string> LoginResponseReceived;
        public event Action<bool, PacketContracts.TokenBundle, string> LoginTokenBundleReceived;
        public event Action<bool, PacketContracts.TokenBundle, string> RefreshTokenResponseReceived;
        public event Action<long> HeartbeatResponseReceived;
        public event Action<List<CharacterSummary>> ListCharactersResponseReceived;
        public event Action<bool, int, string> CreateCharacterResponseReceived;
        public event Action<bool, string> DeleteCharacterResponseReceived;
        public event Action<bool, int, string> SelectCharacterResponseReceived;
        public event Action<bool, float, float, float, string> MoveResponseReceived;
        public event Action<int, float, float, float> MoveSnapshotReceived;
        public event Action<int, bool, int, bool> AttackResponseReceived; // targetId, hitSuccess, damage, isCritical
        public event Action<int> EntityDiedReceived; // entityId
        public event Action<int, float, float, float> EntityRespawnedReceived; // entityId, x, y, z
        public event Action<SnapshotData> FullSnapshotReceived;
        public event Action<SnapshotData> DeltaSnapshotReceived;
        public event Action<bool, int, int, string> SkillResponseReceived;
        public event Action<string> ErrorReceived;
        public event Action<ProtocolError, uint> TypedErrorReceived;

        public void ProcessPacket(byte[] packet)
        {
            _router.Route(packet);
        }

        private void RegisterRoutes()
        {
            _router.Register(NetOpcodes.LoginResponse, payload =>
            {
                if (!ServerMessageParser.TryParseLoginResponse(payload, out bool success, out PacketContracts.TokenBundle tokens, out string message))
                {
                    ErrorReceived?.Invoke("Invalid LoginResponse payload.");
                    return;
                }

                LoginResponseReceived?.Invoke(success, tokens?.AccessToken ?? string.Empty, message);
                LoginTokenBundleReceived?.Invoke(success, tokens, message);
            });

            _router.Register(NetOpcodes.RefreshTokenResponse, payload =>
            {
                if (!ServerMessageParser.TryParseRefreshTokenResponse(payload, out bool success, out PacketContracts.TokenBundle tokens, out string message))
                {
                    ErrorReceived?.Invoke("Invalid RefreshTokenResponse payload.");
                    return;
                }

                RefreshTokenResponseReceived?.Invoke(success, tokens, message);
            });

            _router.Register(NetOpcodes.HeartbeatResponse, payload =>
            {
                if (!ServerMessageParser.TryParseHeartbeatResponse(payload, out long serverUtcTicks))
                {
                    ErrorReceived?.Invoke("Invalid HeartbeatResponse payload.");
                    return;
                }

                HeartbeatResponseReceived?.Invoke(serverUtcTicks);
            });

            _router.Register(NetOpcodes.ListCharactersResponse, payload =>
            {
                if (!ServerMessageParser.TryParseListCharactersResponse(payload, out List<CharacterSummary> characters))
                {
                    ErrorReceived?.Invoke("Invalid ListCharactersResponse payload.");
                    return;
                }

                ListCharactersResponseReceived?.Invoke(characters);
            });

            _router.Register(NetOpcodes.CreateCharacterResponse, payload =>
            {
                if (!ServerMessageParser.TryParseCreateCharacterResponse(payload, out bool success, out int characterId, out string message))
                {
                    ErrorReceived?.Invoke("Invalid CreateCharacterResponse payload.");
                    return;
                }

                CreateCharacterResponseReceived?.Invoke(success, characterId, message);
            });

            _router.Register(NetOpcodes.DeleteCharacterResponse, payload =>
            {
                if (!ServerMessageParser.TryParseDeleteCharacterResponse(payload, out bool success, out string message))
                {
                    ErrorReceived?.Invoke("Invalid DeleteCharacterResponse payload.");
                    return;
                }

                DeleteCharacterResponseReceived?.Invoke(success, message);
            });

            _router.Register(NetOpcodes.SelectCharacterResponse, payload =>
            {
                if (!ServerMessageParser.TryParseSelectCharacterResponse(payload, out bool success, out int characterId, out string message))
                {
                    ErrorReceived?.Invoke("Invalid SelectCharacterResponse payload.");
                    return;
                }

                SelectCharacterResponseReceived?.Invoke(success, characterId, message);
            });

            _router.Register(NetOpcodes.MoveResponse, payload =>
            {
                if (!ServerMessageParser.TryParseMoveResponse(payload, out bool success, out float x, out float y, out float z, out string message))
                {
                    ErrorReceived?.Invoke("Invalid MoveResponse payload.");
                    return;
                }

                MoveResponseReceived?.Invoke(success, x, y, z, message);
            });

            _router.Register(NetOpcodes.MoveSnapshot, payload =>
            {
                if (!ServerMessageParser.TryParseMoveSnapshot(payload, out int entityId, out float x, out float y, out float z))
                {
                    ErrorReceived?.Invoke("Invalid MoveSnapshot payload.");
                    return;
                }

                MoveSnapshotReceived?.Invoke(entityId, x, y, z);
            });

            _router.Register(NetOpcodes.AttackResponse, payload =>
            {
                if (!ServerMessageParser.TryParseAttackResponse(payload, out int targetId, out bool hitSuccess, out int damage, out bool isCritical))
                {
                    ErrorReceived?.Invoke("Invalid AttackResponse payload.");
                    return;
                }

                AttackResponseReceived?.Invoke(targetId, hitSuccess, damage, isCritical);
            });

            _router.Register(NetOpcodes.DeathNotification, payload =>
            {
                if (!ServerMessageParser.TryParseDeathNotification(payload, out int entityId))
                {
                    ErrorReceived?.Invoke("Invalid DeathNotification payload.");
                    return;
                }

                EntityDiedReceived?.Invoke(entityId);
            });

            _router.Register(NetOpcodes.RespawnNotification, payload =>
            {
                if (!ServerMessageParser.TryParseRespawnNotification(payload, out int entityId, out float x, out float y, out float z))
                {
                    ErrorReceived?.Invoke("Invalid RespawnNotification payload.");
                    return;
                }

                EntityRespawnedReceived?.Invoke(entityId, x, y, z);
            });

            _router.Register(NetOpcodes.FullSnapshot, payload =>
            {
                if (!ServerMessageParser.TryParseSnapshotData(payload, out SnapshotData snapshot))
                {
                    ErrorReceived?.Invoke("Invalid FullSnapshot payload.");
                    return;
                }

                FullSnapshotReceived?.Invoke(snapshot);
            });

            _router.Register(NetOpcodes.DeltaSnapshot, payload =>
            {
                if (!ServerMessageParser.TryParseSnapshotData(payload, out SnapshotData snapshot))
                {
                    ErrorReceived?.Invoke("Invalid DeltaSnapshot payload.");
                    return;
                }

                DeltaSnapshotReceived?.Invoke(snapshot);
            });

            _router.Register(NetOpcodes.SkillCastResponse, payload =>
            {
                if (!ServerMessageParser.TryParseSkillCastResponse(payload, out bool success, out int targetId, out int damage, out string message))
                {
                    ErrorReceived?.Invoke("Invalid SkillCastResponse payload.");
                    return;
                }

                SkillResponseReceived?.Invoke(success, targetId, damage, message);
            });

            _router.Register(NetOpcodes.ErrorResponse, payload =>
            {
                if (ServerMessageParser.TryParseErrorResponse(payload, out ProtocolError error, out uint requestId))
                {
                    TypedErrorReceived?.Invoke(error, requestId);
                    ErrorReceived?.Invoke(error?.Message ?? "Unknown server error.");
                    return;
                }

                ErrorReceived?.Invoke("Malformed server error payload.");
            });
        }
    }
}
