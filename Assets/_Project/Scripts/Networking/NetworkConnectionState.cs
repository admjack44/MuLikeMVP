namespace MuLike.Networking
{
    public enum NetworkConnectionState
    {
        Disconnected,
        Connecting,
        Connected,
        Authenticating,
        Authenticated,
        Reconnecting,
        Error
    }
}
