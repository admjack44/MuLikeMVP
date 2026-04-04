using System;
using System.IO;
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
        public event Action<bool, float, float, float, string> MoveResponseReceived;
        public event Action<bool, int, int, string> SkillResponseReceived;
        public event Action<string> ErrorReceived;

        public void ProcessPacket(byte[] packet)
        {
            _router.Route(packet);
        }

        private void RegisterRoutes()
        {
            _router.Register(NetOpcodes.LoginResponse, payload =>
            {
                if (!ServerMessageParser.TryParseLoginResponse(payload, out bool success, out string token, out string message))
                {
                    ErrorReceived?.Invoke("Invalid LoginResponse payload.");
                    return;
                }

                LoginResponseReceived?.Invoke(success, token, message);
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
                try
                {
                    using var ms = new MemoryStream(payload);
                    using var reader = new BinaryReader(ms);
                    string message = PacketCodec.ReadString(reader);
                    ErrorReceived?.Invoke(message);
                }
                catch
                {
                    ErrorReceived?.Invoke("Malformed server error payload.");
                }
            });
        }
    }
}
