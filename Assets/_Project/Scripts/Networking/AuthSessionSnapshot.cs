using System;

namespace MuLike.Networking
{
    [Serializable]
    public struct AuthSessionSnapshot
    {
        public string AccessToken;
        public long AccessTokenExpiresAtUtcTicks;
        public string RefreshToken;
        public long RefreshTokenExpiresAtUtcTicks;

        public bool HasRefreshToken => !string.IsNullOrWhiteSpace(RefreshToken) && RefreshTokenExpiresAtUtcTicks > DateTime.UtcNow.Ticks;
    }
}
