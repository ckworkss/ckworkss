using System;
using System.Security.Cryptography;
using System.Text;

namespace RemotePunch.Web.Core
{
    /// <summary>
    /// PBKDF2-HMAC-SHA256 password hashing. Salt and iteration count live next
    /// to the hash in the Employees table, so the work factor can be raised
    /// later without invalidating existing passwords.
    /// </summary>
    public static class PasswordHasher
    {
        public const int DefaultIterations = 120000;
        private const int SaltBytes = 32;
        private const int HashBytes = 32;

        public static void CreateHash(string password, out byte[] hash, out byte[] salt, out int iterations)
        {
            if (string.IsNullOrEmpty(password)) throw new ArgumentException("Password is required.", "password");

            salt = new byte[SaltBytes];
            using (RandomNumberGenerator rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(salt);
            }
            iterations = DefaultIterations;
            hash = Derive(password, salt, iterations);
        }

        public static bool Verify(string password, byte[] expectedHash, byte[] salt, int iterations)
        {
            if (string.IsNullOrEmpty(password) || expectedHash == null || salt == null) return false;
            if (iterations < 1000) iterations = DefaultIterations;

            byte[] actual = Derive(password, salt, iterations, expectedHash.Length);
            return FixedTimeEquals(actual, expectedHash);
        }

        private static byte[] Derive(string password, byte[] salt, int iterations)
        {
            return Derive(password, salt, iterations, HashBytes);
        }

        private static byte[] Derive(string password, byte[] salt, int iterations, int outputBytes)
        {
            using (Rfc2898DeriveBytes pbkdf2 = new Rfc2898DeriveBytes(password, salt, iterations, HashAlgorithmName.SHA256))
            {
                return pbkdf2.GetBytes(outputBytes);
            }
        }

        /// <summary>Comparison whose running time does not depend on where the bytes differ.</summary>
        public static bool FixedTimeEquals(byte[] a, byte[] b)
        {
            if (a == null || b == null || a.Length != b.Length) return false;
            int diff = 0;
            for (int i = 0; i < a.Length; i++) diff |= a[i] ^ b[i];
            return diff == 0;
        }

        /// <summary>Basic strength gate applied on every password change.</summary>
        public static bool IsStrongEnough(string password, out string problem)
        {
            problem = null;
            if (string.IsNullOrWhiteSpace(password) || password.Length < 10)
            {
                problem = "Password must be at least 10 characters long.";
                return false;
            }
            bool upper = false, lower = false, digit = false, other = false;
            foreach (char c in password)
            {
                if (char.IsUpper(c)) upper = true;
                else if (char.IsLower(c)) lower = true;
                else if (char.IsDigit(c)) digit = true;
                else other = true;
            }
            int classes = (upper ? 1 : 0) + (lower ? 1 : 0) + (digit ? 1 : 0) + (other ? 1 : 0);
            if (classes < 3)
            {
                problem = "Use at least three of: uppercase, lowercase, digits, symbols.";
                return false;
            }
            return true;
        }

        public static string Sha256Hex(string input)
        {
            using (SHA256 sha = SHA256.Create())
            {
                byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(input ?? string.Empty));
                StringBuilder sb = new StringBuilder(bytes.Length * 2);
                for (int i = 0; i < bytes.Length; i++) sb.Append(bytes[i].ToString("x2"));
                return sb.ToString();
            }
        }
    }
}
