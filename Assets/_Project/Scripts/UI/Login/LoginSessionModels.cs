using System;

namespace MuLike.UI.Login
{
    [Serializable]
    public sealed class LoginSessionData
    {
        public string LastUsername;
        public string AccessToken;
        public long AccessTokenExpiresAtUtcTicks;
        public string RefreshToken;
        public long RefreshTokenExpiresAtUtcTicks;

        public bool HasRefreshToken
        {
            get
            {
                return !string.IsNullOrWhiteSpace(RefreshToken)
                    && RefreshTokenExpiresAtUtcTicks > DateTime.UtcNow.Ticks;
            }
        }
    }

    public readonly struct LoginAttemptResult
    {
        public readonly bool Success;
        public readonly string Message;

        public LoginAttemptResult(bool success, string message)
        {
            Success = success;
            Message = message ?? string.Empty;
        }
    }
}
