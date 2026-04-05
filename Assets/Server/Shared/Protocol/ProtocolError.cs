namespace MuLike.Shared.Protocol
{
    public enum ProtocolErrorCode
    {
        Unknown = 0,
        MalformedPacket = 1,
        SessionExpired = 2,
        UnsupportedOpcode = 3,
        ValidationFailed = 4,
        Unauthorized = 5,
        NotFound = 6,
        Conflict = 7,
        Internal = 8
    }

    public sealed class ProtocolError
    {
        public ProtocolErrorCode Code { get; set; } = ProtocolErrorCode.Unknown;
        public string Message { get; set; } = string.Empty;
        public string Details { get; set; } = string.Empty;

        public static ProtocolError Create(ProtocolErrorCode code, string message, string details = "")
        {
            return new ProtocolError
            {
                Code = code,
                Message = message ?? string.Empty,
                Details = details ?? string.Empty
            };
        }
    }
}
