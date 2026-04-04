using System;
using System.Security.Cryptography;

namespace MuLike.Server.Auth
{
    public sealed class PasswordHasher
    {
        private const int SaltSize = 16;
        private const int KeySize = 32;
        private const int Iterations = 120_000;

        public string Hash(string password)
        {
            byte[] salt = new byte[SaltSize];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(salt);
            }

            byte[] key;
            using (var deriveBytes = new Rfc2898DeriveBytes(password, salt, Iterations))
            {
                key = deriveBytes.GetBytes(KeySize);
            }

            return $"{Iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(key)}";
        }

        public bool Verify(string password, string hash)
        {
            if (string.IsNullOrWhiteSpace(hash)) return false;
            string[] parts = hash.Split('.');
            if (parts.Length != 3) return false;

            if (!int.TryParse(parts[0], out int iterations)) return false;
            byte[] salt = Convert.FromBase64String(parts[1]);
            byte[] expected = Convert.FromBase64String(parts[2]);

            byte[] actual;
            using (var deriveBytes = new Rfc2898DeriveBytes(password, salt, iterations))
            {
                actual = deriveBytes.GetBytes(expected.Length);
            }

            return FixedTimeEquals(actual, expected);
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
