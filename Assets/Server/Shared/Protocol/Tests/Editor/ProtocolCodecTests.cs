using NUnit.Framework;

namespace MuLike.Shared.Protocol.Tests.Editor
{
    public sealed class ProtocolCodecTests
    {
        [Test]
        public void NetOpcodes_LegacyAliases_MatchDomainConstants()
        {
            Assert.AreEqual(NetOpcodes.Auth.LoginRequest, NetOpcodes.LoginRequest);
            Assert.AreEqual(NetOpcodes.Character.ListRequest, NetOpcodes.ListCharactersRequest);
            Assert.AreEqual(NetOpcodes.World.MoveResponse, NetOpcodes.MoveResponse);
            Assert.AreEqual(NetOpcodes.Combat.AttackRequest, NetOpcodes.AttackRequest);
            Assert.AreEqual(NetOpcodes.Skill.CastResponse, NetOpcodes.SkillCastResponse);
            Assert.AreEqual(NetOpcodes.System.ErrorResponse, NetOpcodes.ErrorResponse);
        }

        [Test]
        public void ProtocolCatalog_ShouldClassifyKnownOpcode()
        {
            ProtocolOpcodeInfo info = ProtocolCatalog.GetInfo(NetOpcodes.Skill.ListRequest);
            Assert.AreEqual(ProtocolDomain.Skill, info.Domain);
            Assert.AreEqual(ProtocolMessageKind.Request, info.Kind);
        }

        [Test]
        public void EnvelopeCodec_ShouldRoundTripCurrentEnvelope()
        {
            byte[] packet = PacketEnvelopeCodec.Encode(
                NetOpcodes.MoveRequest,
                requestId: 42,
                error: null,
                payloadWriter: writer =>
                {
                    writer.Write(1.5f);
                    writer.Write(2.5f);
                    writer.Write(3.5f);
                });

            Assert.IsTrue(PacketEnvelopeCodec.TryDecode(packet, out PacketEnvelope envelope));
            Assert.AreEqual(ProtocolVersion.Current, envelope.Version);
            Assert.AreEqual(ProtocolDomain.World, envelope.Domain);
            Assert.AreEqual(ProtocolMessageKind.Request, envelope.Kind);
            Assert.AreEqual((uint)42, envelope.RequestId);
            Assert.IsNull(envelope.Error);

            Assert.IsTrue(PacketContracts.TryReadMoveRequest(envelope.Payload, out float x, out float y, out float z));
            Assert.AreEqual(1.5f, x);
            Assert.AreEqual(2.5f, y);
            Assert.AreEqual(3.5f, z);
        }

        [Test]
        public void EnvelopeCodec_ShouldFallbackToLegacyPacket()
        {
            byte[] packet = PacketContracts.CreateMoveRequest(10f, 20f, 30f);

            Assert.IsTrue(PacketEnvelopeCodec.TryDecode(packet, out PacketEnvelope envelope));
            Assert.AreEqual(ProtocolVersion.Legacy, envelope.Version);
            Assert.AreEqual(ProtocolDomain.World, envelope.Domain);
            Assert.AreEqual(ProtocolMessageKind.Request, envelope.Kind);
            Assert.IsNull(envelope.Error);

            Assert.IsTrue(PacketContracts.TryReadMoveRequest(envelope.Payload, out float x, out float y, out float z));
            Assert.AreEqual(10f, x);
            Assert.AreEqual(20f, y);
            Assert.AreEqual(30f, z);
        }

        [Test]
        public void ErrorContract_ShouldRoundTripTypedError()
        {
            ProtocolError expected = ProtocolError.Create(
                ProtocolErrorCode.ValidationFailed,
                "Invalid move payload",
                "x/y/z fields missing");

            byte[] packet = PacketContracts.CreateErrorResponse(expected, requestId: 77);
            Assert.IsTrue(PacketCodec.TryDecode(packet, out ushort opcode, out byte[] payload));
            Assert.AreEqual(NetOpcodes.ErrorResponse, opcode);

            Assert.IsTrue(PacketContracts.TryReadErrorResponse(payload, out ProtocolError actual, out uint requestId));
            Assert.AreEqual((uint)77, requestId);
            Assert.AreEqual(expected.Code, actual.Code);
            Assert.AreEqual(expected.Message, actual.Message);
            Assert.AreEqual(expected.Details, actual.Details);
        }

        [Test]
        public void ErrorContract_ShouldReadLegacyStringError()
        {
            const string message = "Legacy error";
            byte[] legacyPacket = PacketCodec.Encode(NetOpcodes.ErrorResponse, writer =>
            {
                PacketCodec.WriteString(writer, message);
            });

            Assert.IsTrue(PacketCodec.TryDecode(legacyPacket, out _, out byte[] payload));
            Assert.IsTrue(PacketContracts.TryReadErrorResponse(payload, out ProtocolError parsed, out uint requestId));
            Assert.AreEqual((uint)0, requestId);
            Assert.AreEqual(ProtocolErrorCode.Unknown, parsed.Code);
            Assert.AreEqual(message, parsed.Message);
        }
    }
}
