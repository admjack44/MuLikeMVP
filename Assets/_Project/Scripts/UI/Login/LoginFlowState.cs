namespace MuLike.UI.Login
{
    public enum LoginFlowState
    {
        Idle,
        Connecting,
        Authenticating,
        Authenticated,
        Refreshing,
        Failed,
        LoggedOut
    }
}
