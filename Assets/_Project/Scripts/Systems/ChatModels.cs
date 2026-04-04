using System;

namespace MuLike.Systems
{
    public enum ChatChannel
    {
        General = 0,
        System = 1,
        Private = 2
    }

    [Serializable]
    public struct ChatMessage
    {
        public string MessageId;
        public ChatChannel Channel;
        public string Sender;
        public string Target;
        public string Text;
        public long TimestampUnixMs;
        public bool IsLocalEcho;

        public string ToDisplayString()
        {
            DateTimeOffset ts = TimestampUnixMs > 0
                ? DateTimeOffset.FromUnixTimeMilliseconds(TimestampUnixMs)
                : DateTimeOffset.UtcNow;

            string hhmm = ts.ToLocalTime().ToString("HH:mm");
            string sender = string.IsNullOrWhiteSpace(Sender) ? "?" : Sender;

            return Channel switch
            {
                ChatChannel.System => $"[{hhmm}] [System] {Text}",
                ChatChannel.Private => $"[{hhmm}] [PM] {sender} -> {Target}: {Text}",
                _ => $"[{hhmm}] [{sender}] {Text}"
            };
        }
    }

    [Serializable]
    public struct ChatSendRequest
    {
        public ChatChannel Channel;
        public string Text;
        public string Target;
    }

    [Serializable]
    public struct ChatWireMessage
    {
        public int version;
        public string messageId;
        public string channel;
        public string from;
        public string to;
        public string text;
        public long ts;
    }
}
