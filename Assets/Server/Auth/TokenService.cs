using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace MuLike.Server.Auth
{
    public sealed class AuthOptions
    {
        public string Issuer { get; set; } = string.Empty;
        public string Audience { get; set; } = string.Empty;
        public string AccessTokenSigningKey { get; set; } = string.Empty;
        public TimeSpan AccessTokenLifetime { get; set; } = TimeSpan.FromMinutes(15);
        public TimeSpan RefreshTokenLifetime { get; set; } = TimeSpan.FromDays(14);
        public bool RotateRefreshTokens { get; set; } = true;
        public int MaxActiveRefreshSessionsPerAccount { get; set; } = 5;

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(Issuer)) throw new InvalidOperationException("Issuer is required.");
            if (string.IsNullOrWhiteSpace(Audience)) throw new InvalidOperationException("Audience is required.");
            if (string.IsNullOrWhiteSpace(AccessTokenSigningKey)) throw new InvalidOperationException("AccessTokenSigningKey is required.");

            byte[] keyBytes = Encoding.UTF8.GetBytes(AccessTokenSigningKey);
            if (keyBytes.Length < 32)
                throw new InvalidOperationException("AccessTokenSigningKey must be at least 32 bytes.");

            if (AccessTokenLifetime <= TimeSpan.Zero)
                throw new InvalidOperationException("AccessTokenLifetime must be > 0.");

            if (RefreshTokenLifetime <= TimeSpan.Zero)
                throw new InvalidOperationException("RefreshTokenLifetime must be > 0.");

            if (MaxActiveRefreshSessionsPerAccount <= 0)
                throw new InvalidOperationException("MaxActiveRefreshSessionsPerAccount must be > 0.");
        }
    }

    public sealed class AccessTokenPrincipal
    {
        public int AccountId { get; set; }
        public string AccountName { get; set; }
        public string SessionId { get; set; }
        public string Issuer { get; set; }
        public string Audience { get; set; }
        public DateTime IssuedAtUtc { get; set; }
        public DateTime ExpiresAtUtc { get; set; }
        public string TokenId { get; set; }
    }

    public readonly struct AccessTokenValidationResult
    {
        public AccessTokenValidationResult(bool success, AccessTokenPrincipal principal, string error)
        {
            Success = success;
            Principal = principal;
            Error = error;
        }

        public bool Success { get; }
        public AccessTokenPrincipal Principal { get; }
        public string Error { get; }
    }

    public readonly struct RefreshTokenValue
    {
        public RefreshTokenValue(string raw, string hash)
        {
            Raw = raw;
            Hash = hash;
        }

        public string Raw { get; }
        public string Hash { get; }
    }

    public sealed class TokenService
    {
        private const string Hs256 = "HS256";
        private readonly AuthOptions _options;
        private readonly byte[] _signingKey;

        public TokenService(AuthOptions options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _options.Validate();
            _signingKey = Encoding.UTF8.GetBytes(_options.AccessTokenSigningKey);
        }

        public string CreateAccessToken(int accountId, string accountName, string sessionId)
        {
            DateTime now = DateTime.UtcNow;
            DateTime exp = now.Add(_options.AccessTokenLifetime);
            string jti = Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);

            string headerJson = "{" +
                "\"alg\":\"" + Hs256 + "\"," +
                "\"typ\":\"JWT\"" +
                "}";

            string payloadJson = "{" +
                "\"iss\":\"" + EscapeJson(_options.Issuer) + "\"," +
                "\"aud\":\"" + EscapeJson(_options.Audience) + "\"," +
                "\"sub\":" + accountId + "," +
                "\"name\":\"" + EscapeJson(accountName ?? string.Empty) + "\"," +
                "\"sid\":\"" + EscapeJson(sessionId ?? string.Empty) + "\"," +
                "\"jti\":\"" + jti + "\"," +
                "\"iat\":" + ToUnixSeconds(now) + "," +
                "\"exp\":" + ToUnixSeconds(exp) +
                "}";

            string encodedHeader = Base64UrlEncode(Encoding.UTF8.GetBytes(headerJson));
            string encodedPayload = Base64UrlEncode(Encoding.UTF8.GetBytes(payloadJson));
            string signingInput = encodedHeader + "." + encodedPayload;
            string signature = Base64UrlEncode(Sign(signingInput));
            return signingInput + "." + signature;
        }

        public AccessTokenValidationResult ValidateAccessToken(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
                return new AccessTokenValidationResult(false, null, "Token is empty.");

            string[] parts = token.Split('.');
            if (parts.Length != 3)
                return new AccessTokenValidationResult(false, null, "Token format invalid.");

            string signingInput = parts[0] + "." + parts[1];
            byte[] expectedSig = Sign(signingInput);
            byte[] providedSig;
            try
            {
                providedSig = Base64UrlDecode(parts[2]);
            }
            catch
            {
                return new AccessTokenValidationResult(false, null, "Token signature encoding invalid.");
            }

            if (!FixedTimeEquals(expectedSig, providedSig))
                return new AccessTokenValidationResult(false, null, "Token signature invalid.");

            Dictionary<string, string> payload;
            try
            {
                string payloadJson = Encoding.UTF8.GetString(Base64UrlDecode(parts[1]));
                payload = ParseFlatJsonObject(payloadJson);
            }
            catch
            {
                return new AccessTokenValidationResult(false, null, "Token payload invalid.");
            }

            if (!payload.TryGetValue("iss", out string iss) || !string.Equals(iss, _options.Issuer, StringComparison.Ordinal))
                return new AccessTokenValidationResult(false, null, "Issuer validation failed.");

            if (!payload.TryGetValue("aud", out string aud) || !string.Equals(aud, _options.Audience, StringComparison.Ordinal))
                return new AccessTokenValidationResult(false, null, "Audience validation failed.");

            if (!payload.TryGetValue("exp", out string expRaw) || !long.TryParse(expRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out long expUnix))
                return new AccessTokenValidationResult(false, null, "Missing exp claim.");

            if (DateTimeOffset.UtcNow.ToUnixTimeSeconds() >= expUnix)
                return new AccessTokenValidationResult(false, null, "Token expired.");

            if (!payload.TryGetValue("iat", out string iatRaw) || !long.TryParse(iatRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out long iatUnix))
                return new AccessTokenValidationResult(false, null, "Missing iat claim.");

            if (!payload.TryGetValue("sub", out string subRaw) || !int.TryParse(subRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out int sub))
                return new AccessTokenValidationResult(false, null, "Missing sub claim.");

            payload.TryGetValue("name", out string accountNameValue);
            payload.TryGetValue("sid", out string sessionIdValue);
            payload.TryGetValue("jti", out string jtiValue);

            var principal = new AccessTokenPrincipal
            {
                AccountId = sub,
                AccountName = accountNameValue ?? string.Empty,
                SessionId = sessionIdValue ?? string.Empty,
                Issuer = iss,
                Audience = aud,
                IssuedAtUtc = DateTimeOffset.FromUnixTimeSeconds(iatUnix).UtcDateTime,
                ExpiresAtUtc = DateTimeOffset.FromUnixTimeSeconds(expUnix).UtcDateTime,
                TokenId = jtiValue ?? string.Empty
            };

            return new AccessTokenValidationResult(true, principal, string.Empty);
        }

        public RefreshTokenValue CreateRefreshToken()
        {
            byte[] rawBytes = new byte[48];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(rawBytes);
            }

            string raw = Base64UrlEncode(rawBytes);
            string hash = HashRefreshToken(raw);
            return new RefreshTokenValue(raw, hash);
        }

        public string HashRefreshToken(string rawRefreshToken)
        {
            if (string.IsNullOrWhiteSpace(rawRefreshToken))
                throw new ArgumentException("Refresh token is required.", nameof(rawRefreshToken));

            byte[] bytes = Encoding.UTF8.GetBytes(rawRefreshToken);
            using var sha = SHA256.Create();
            byte[] hash = sha.ComputeHash(bytes);
            return Convert.ToBase64String(hash);
        }

        private byte[] Sign(string value)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(value);
            using var hmac = new HMACSHA256(_signingKey);
            return hmac.ComputeHash(bytes);
        }

        private static Dictionary<string, string> ParseFlatJsonObject(string json)
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            if (string.IsNullOrWhiteSpace(json))
                return result;

            string trimmed = json.Trim();
            if (trimmed.Length < 2 || trimmed[0] != '{' || trimmed[trimmed.Length - 1] != '}')
                throw new FormatException("JSON object expected.");

            int index = 1;
            while (index < trimmed.Length - 1)
            {
                SkipSpaces(trimmed, ref index);
                if (index >= trimmed.Length - 1)
                    break;

                string key = ReadJsonString(trimmed, ref index);
                SkipSpaces(trimmed, ref index);
                if (index >= trimmed.Length - 1 || trimmed[index] != ':')
                    throw new FormatException("Invalid JSON key/value separator.");

                index++;
                SkipSpaces(trimmed, ref index);

                string value;
                if (index < trimmed.Length && trimmed[index] == '"')
                {
                    value = ReadJsonString(trimmed, ref index);
                }
                else
                {
                    int start = index;
                    while (index < trimmed.Length && trimmed[index] != ',' && trimmed[index] != '}')
                        index++;

                    value = trimmed.Substring(start, index - start).Trim();
                }

                result[key] = value;

                SkipSpaces(trimmed, ref index);
                if (index < trimmed.Length && trimmed[index] == ',')
                    index++;
            }

            return result;
        }

        private static void SkipSpaces(string text, ref int index)
        {
            while (index < text.Length && char.IsWhiteSpace(text[index]))
                index++;
        }

        private static string ReadJsonString(string text, ref int index)
        {
            if (index >= text.Length || text[index] != '"')
                throw new FormatException("Invalid JSON string.");

            index++;
            var sb = new StringBuilder();
            while (index < text.Length)
            {
                char c = text[index++];
                if (c == '"')
                    return sb.ToString();

                if (c == '\\' && index < text.Length)
                {
                    char escaped = text[index++];
                    switch (escaped)
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case '/': sb.Append('/'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        default: sb.Append(escaped); break;
                    }
                }
                else
                {
                    sb.Append(c);
                }
            }

            throw new FormatException("Unterminated JSON string.");
        }

        private static string EscapeJson(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            return value
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\n", "\\n")
                .Replace("\r", "\\r")
                .Replace("\t", "\\t");
        }

        private static long ToUnixSeconds(DateTime utc)
        {
            return new DateTimeOffset(utc).ToUnixTimeSeconds();
        }

        private static string Base64UrlEncode(byte[] bytes)
        {
            return Convert.ToBase64String(bytes)
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
        }

        private static byte[] Base64UrlDecode(string value)
        {
            string padded = value
                .Replace('-', '+')
                .Replace('_', '/');

            switch (padded.Length % 4)
            {
                case 2: padded += "=="; break;
                case 3: padded += "="; break;
            }

            return Convert.FromBase64String(padded);
        }

        private static bool FixedTimeEquals(byte[] left, byte[] right)
        {
            if (left == null || right == null || left.Length != right.Length)
                return false;

            int diff = 0;
            for (int i = 0; i < left.Length; i++)
                diff |= left[i] ^ right[i];

            return diff == 0;
        }
    }
}
