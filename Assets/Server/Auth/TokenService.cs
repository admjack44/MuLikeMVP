using System;
using System.Text;

namespace MuLike.Server.Auth
{
    public sealed class TokenService
    {
        private readonly string _issuer;

        public TokenService(string issuer)
        {
            _issuer = issuer;
        }

        public string CreateToken(int accountId, string accountName)
        {
            string safeName = EscapeJson(accountName);
            string safeIssuer = EscapeJson(_issuer);
            long iat = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            string json = "{"
                + "\"sub\":" + accountId + ","
                + "\"name\":\"" + safeName + "\","
                + "\"iss\":\"" + safeIssuer + "\","
                + "\"iat\":" + iat
                + "}";

            return Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
        }

        private static string EscapeJson(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;

            return value
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\n", "\\n")
                .Replace("\r", "\\r")
                .Replace("\t", "\\t");
        }
    }
}
