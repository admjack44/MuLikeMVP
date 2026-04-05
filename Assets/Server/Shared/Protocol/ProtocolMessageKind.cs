namespace MuLike.Shared.Protocol
{
    public enum ProtocolMessageKind : byte
    {
        Unknown = 0,
        Request = 1,
        Response = 2,
        Event = 3
    }
}
