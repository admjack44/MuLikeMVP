using System;
using System.Text;
using UnityEngine;

namespace MuLike.UI.Login
{
    /// <summary>
    /// Minimal local secure-ish storage for tokens and last username.
    /// TODO: replace with platform secure keystore/keychain when native bridge is available.
    /// </summary>
    public sealed class PlayerPrefsLoginSessionStore : ILoginSessionStore
    {
        private const string SessionKey = "mulike.login.session.v1";
        private const string ObfuscationSalt = "MuLikeMobile_v1";

        [Serializable]
        private sealed class SessionPayload
        {
            public string lastUsername;
            public string accessToken;
            public long accessTokenExpiresAtUtcTicks;
            public string refreshToken;
            public long refreshTokenExpiresAtUtcTicks;
        }

        public bool TryLoad(out LoginSessionData session)
        {
            session = null;

            string encrypted = PlayerPrefs.GetString(SessionKey, string.Empty);
            if (string.IsNullOrWhiteSpace(encrypted))
                return false;

            try
            {
                string json = Deobfuscate(encrypted);
                SessionPayload payload = JsonUtility.FromJson<SessionPayload>(json);
                if (payload == null)
                    return false;

                session = new LoginSessionData
                {
                    LastUsername = payload.lastUsername ?? string.Empty,
                    AccessToken = payload.accessToken ?? string.Empty,
                    AccessTokenExpiresAtUtcTicks = payload.accessTokenExpiresAtUtcTicks,
                    RefreshToken = payload.refreshToken ?? string.Empty,
                    RefreshTokenExpiresAtUtcTicks = payload.refreshTokenExpiresAtUtcTicks
                };

                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[LoginFlow] Session load failed: {ex.Message}");
                return false;
            }
        }

        public void Save(LoginSessionData session)
        {
            if (session == null)
                return;

            var payload = new SessionPayload
            {
                lastUsername = session.LastUsername ?? string.Empty,
                accessToken = session.AccessToken ?? string.Empty,
                accessTokenExpiresAtUtcTicks = session.AccessTokenExpiresAtUtcTicks,
                refreshToken = session.RefreshToken ?? string.Empty,
                refreshTokenExpiresAtUtcTicks = session.RefreshTokenExpiresAtUtcTicks
            };

            string json = JsonUtility.ToJson(payload);
            string encrypted = Obfuscate(json);
            PlayerPrefs.SetString(SessionKey, encrypted);
            PlayerPrefs.Save();
        }

        public void Clear()
        {
            PlayerPrefs.DeleteKey(SessionKey);
            PlayerPrefs.Save();
        }

        private static string Obfuscate(string plain)
        {
            byte[] input = Encoding.UTF8.GetBytes(plain ?? string.Empty);
            byte[] key = Encoding.UTF8.GetBytes(ObfuscationSalt);
            byte[] output = new byte[input.Length];

            for (int i = 0; i < input.Length; i++)
                output[i] = (byte)(input[i] ^ key[i % key.Length]);

            return Convert.ToBase64String(output);
        }

        private static string Deobfuscate(string value)
        {
            byte[] input = Convert.FromBase64String(value);
            byte[] key = Encoding.UTF8.GetBytes(ObfuscationSalt);
            byte[] output = new byte[input.Length];

            for (int i = 0; i < input.Length; i++)
                output[i] = (byte)(input[i] ^ key[i % key.Length]);

            return Encoding.UTF8.GetString(output);
        }
    }
}
