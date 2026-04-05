using System;
using System.Security.Cryptography;

namespace MuLike.Server.Auth
{
    public readonly struct PasswordVerificationResult
    {
        public PasswordVerificationResult(bool success, bool requiresRehash)
        {
            Success = success;
            RequiresRehash = requiresRehash;
        }

        public bool Success { get; }
        public bool RequiresRehash { get; }
    }

    public sealed class PasswordHasher
    {
        private const string FormatV2Prefix = "pbkdf2-sha256";
        private const int SaltSize = 16;
        private const int KeySize = 32;
        private const int Iterations = 210_000;

        public string Hash(string password)
        {
            if (password == null) throw new ArgumentNullException(nameof(password));

            byte[] salt = new byte[SaltSize];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(salt);
            }

            byte[] key;
            using (var deriveBytes = new Rfc2898DeriveBytes(password, salt, Iterations, HashAlgorithmName.SHA256))
            {
                key = deriveBytes.GetBytes(KeySize);
            }

            return $"{FormatV2Prefix}${Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(key)}";
        }

        public bool Verify(string password, string hash)
        {
            return VerifyDetailed(password, hash).Success;
        }

        public PasswordVerificationResult VerifyDetailed(string password, string hash)
        {
            if (password == null) throw new ArgumentNullException(nameof(password));
            if (string.IsNullOrWhiteSpace(hash)) return new PasswordVerificationResult(false, false);

            if (TryVerifyV2(password, hash, out bool verifiedV2, out bool requiresRehashV2))
                return new PasswordVerificationResult(verifiedV2, verifiedV2 && requiresRehashV2);

            if (TryVerifyLegacyV1(password, hash, out bool verifiedLegacy))
                return new PasswordVerificationResult(verifiedLegacy, verifiedLegacy);

            return new PasswordVerificationResult(false, false);
        }

        private static bool TryVerifyV2(string password, string hash, out bool verified, out bool requiresRehash)
        {
            verified = false;
            requiresRehash = false;

            string[] parts = hash.Split('$');
            if (parts.Length != 4) return false;
            if (!string.Equals(parts[0], FormatV2Prefix, StringComparison.Ordinal)) return false;
            if (!int.TryParse(parts[1], out int iterations) || iterations <= 0) return false;

            byte[] salt;
            byte[] expected;
            try
            {
                salt = Convert.FromBase64String(parts[2]);
                expected = Convert.FromBase64String(parts[3]);
            }
            catch
            {
                return false;
            }

            byte[] actual;
            using (var deriveBytes = new Rfc2898DeriveBytes(password, salt, iterations, HashAlgorithmName.SHA256))
            {
                actual = deriveBytes.GetBytes(expected.Length);
            }

            verified = FixedTimeEquals(actual, expected);
            requiresRehash = iterations < Iterations || expected.Length < KeySize;
            return true;
        }

        // Legacy compatibility: "iterations.base64Salt.base64Hash" produced by PBKDF2-SHA1 default ctor.
        private static bool TryVerifyLegacyV1(string password, string hash, out bool verified)
        {
            verified = false;

            string[] parts = hash.Split('.');
            if (parts.Length != 3) return false;
            if (!int.TryParse(parts[0], out int iterations) || iterations <= 0) return false;

            byte[] salt;
            byte[] expected;
            try
            {
                salt = Convert.FromBase64String(parts[1]);
                expected = Convert.FromBase64String(parts[2]);
            }
            catch
            {
                return false;
            }

            byte[] actual;
            using (var deriveBytes = new Rfc2898DeriveBytes(password, salt, iterations, HashAlgorithmName.SHA1))
            {
                actual = deriveBytes.GetBytes(expected.Length);
            }

            verified = FixedTimeEquals(actual, expected);
            return true;
        }

        private static bool FixedTimeEquals(byte[] left, byte[] right)
        {
            if (left == null || right == null || left.Length != right.Length) return false;

            int diff = 0;
            for (int i = 0; i < left.Length; i++)
            {
                diff |= left[i] ^ right[i];
            }

            return diff == 0;
        }
    }
}
