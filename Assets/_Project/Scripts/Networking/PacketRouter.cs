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
        public delegate void PacketMiddleware(ushort opcode, byte[] payload, Action next);

        private readonly Dictionary<ushort, List<Action<byte[]>>> _routes = new();
        private readonly List<PacketMiddleware> _middlewares = new();

        public bool EnableVerboseLogs { get; set; } = true;

        public void Register(ushort opcode, Action<byte[]> handler)
        {
            ValidateHandler(handler, nameof(handler));

            if (_routes.TryGetValue(opcode, out var existing) && existing.Count > 0)
            {
                Debug.LogWarning($"[PacketRouter] Opcode {opcode} already registered. Replacing existing handlers.");
            }

            _routes[opcode] = new List<Action<byte[]>> { handler };

            if (EnableVerboseLogs)
                Debug.Log($"[PacketRouter] Register opcode={opcode} handlers=1");
        }

        public void Subscribe(ushort opcode, Action<byte[]> handler)
        {
            ValidateHandler(handler, nameof(handler));

            if (!_routes.TryGetValue(opcode, out var handlers))
            {
                handlers = new List<Action<byte[]>>();
                _routes[opcode] = handlers;
            }

            if (handlers.Contains(handler))
            {
                Debug.LogWarning($"[PacketRouter] Duplicate handler ignored for opcode={opcode}.");
                return;
            }

            handlers.Add(handler);

            if (EnableVerboseLogs)
                Debug.Log($"[PacketRouter] Subscribe opcode={opcode} handlers={handlers.Count}");
        }

        public void Unsubscribe(ushort opcode, Action<byte[]> handler)
        {
            if (handler == null) return;
            if (!_routes.TryGetValue(opcode, out var handlers)) return;

            handlers.Remove(handler);
            if (handlers.Count == 0)
                _routes.Remove(opcode);

            if (EnableVerboseLogs)
                Debug.Log($"[PacketRouter] Unsubscribe opcode={opcode} handlers={(handlers.Count > 0 ? handlers.Count : 0)}");
        }

        public void Use(PacketMiddleware middleware)
        {
            if (middleware == null)
                throw new ArgumentNullException(nameof(middleware));

            _middlewares.Add(middleware);

            if (EnableVerboseLogs)
                Debug.Log($"[PacketRouter] Middleware added. count={_middlewares.Count}");
        }

        public void Unregister(ushort opcode)
        {
            _routes.Remove(opcode);

            if (EnableVerboseLogs)
                Debug.Log($"[PacketRouter] Unregister opcode={opcode}");
        }

        public void Route(byte[] packet)
        {
            if (packet == null || packet.Length == 0)
            {
                Debug.LogWarning("[PacketRouter] Received null or empty packet.");
                return;
            }

            if (!PacketCodec.TryDecode(packet, out ushort opcode, out byte[] payload))
            {
                Debug.LogWarning("[PacketRouter] Received malformed packet.");
                return;
            }

            if (!_routes.TryGetValue(opcode, out var handlers) || handlers == null || handlers.Count == 0)
            {
                ProtocolOpcodeInfo info = ProtocolCatalog.GetInfo(opcode);
                Debug.LogWarning($"[PacketRouter] No handler for opcode={opcode} domain={info.Domain} kind={info.Kind}");
                return;
            }

            Action finalDispatch = () => DispatchHandlers(opcode, payload, handlers);
            Action pipeline = BuildMiddlewarePipeline(opcode, payload, finalDispatch);

            if (EnableVerboseLogs)
            {
                ProtocolOpcodeInfo info = ProtocolCatalog.GetInfo(opcode);
                Debug.Log($"[PacketRouter] Route opcode={opcode} domain={info.Domain} kind={info.Kind} payloadBytes={(payload != null ? payload.Length : 0)} handlers={handlers.Count}");
            }

            pipeline();
        }

        private Action BuildMiddlewarePipeline(ushort opcode, byte[] payload, Action terminal)
        {
            if (_middlewares.Count == 0)
                return terminal;

            Action current = terminal;

            for (int i = _middlewares.Count - 1; i >= 0; i--)
            {
                PacketMiddleware middleware = _middlewares[i];
                Action next = current;

                current = () =>
                {
                    try
                    {
                        middleware(opcode, payload, next);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[PacketRouter] Middleware exception for opcode={opcode}: {ex.Message}");
                    }
                };
            }

            return current;
        }

        private void DispatchHandlers(ushort opcode, byte[] payload, List<Action<byte[]>> handlers)
        {
            Action<byte[]>[] snapshot = handlers.ToArray();

            for (int i = 0; i < snapshot.Length; i++)
            {
                Action<byte[]> handler = snapshot[i];
                if (handler == null)
                {
                    Debug.LogWarning($"[PacketRouter] Null handler skipped for opcode={opcode}, index={i}.");
                    continue;
                }

                try
                {
                    handler(payload);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[PacketRouter] Handler exception for opcode={opcode}, index={i}: {ex.Message}");
                }
            }
        }

        private static void ValidateHandler(Action<byte[]> handler, string argumentName)
        {
            if (handler == null)
                throw new ArgumentNullException(argumentName, "Packet handler cannot be null.");
        }
    }
}
