using System;
using System.Collections.Generic;
using MuLike.Shared.Protocol;
using UnityEngine;

namespace MuLike.Networking
{
    /// <summary>
    /// Dispatches incoming raw packets to the correct handler based on packet opcode/type.
    /// </summary>
    public class PacketRouter
    {
        private readonly Dictionary<ushort, Action<byte[]>> _routes = new();

        public void Register(ushort opcode, Action<byte[]> handler)
        {
            if (_routes.ContainsKey(opcode))
            {
                Debug.LogWarning($"[PacketRouter] Opcode {opcode} already registered. Overwriting.");
            }
            _routes[opcode] = handler;
        }

        public void Unregister(ushort opcode)
        {
            _routes.Remove(opcode);
        }

        public void Route(byte[] packet)
        {
            if (!PacketCodec.TryDecode(packet, out ushort opcode, out byte[] payload))
            {
                Debug.LogWarning("[PacketRouter] Received malformed packet.");
                return;
            }

            if (_routes.TryGetValue(opcode, out var handler))
                handler?.Invoke(payload);
            else
                Debug.LogWarning($"[PacketRouter] No handler for opcode: {opcode}");
        }
    }
}
