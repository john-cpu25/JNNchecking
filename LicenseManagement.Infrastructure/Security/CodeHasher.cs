using System;
using System.Security.Cryptography;
using System.Text;

namespace LicenseManagement.Infrastructure.Security
{
    public interface ICodeHasher
    {
        string HashCode(string activationCode);
        bool VerifyCode(string activationCode, string storedHash);
    }

    /// <summary>
    /// Hashes activation codes using SHA-256 with a configured server pepper.
    /// Activation codes are never stored or logged in plaintext.
    /// </summary>
    public class CodeHasher : ICodeHasher
    {
        private readonly string _pepper;

        public CodeHasher(string pepper)
        {
            _pepper = string.IsNullOrWhiteSpace(pepper) ? "JNNTool_Default_License_Pepper_2026_Secure!" : pepper;
        }

        public string HashCode(string activationCode)
        {
            if (string.IsNullOrWhiteSpace(activationCode))
                throw new ArgumentException("Activation code cannot be empty.", nameof(activationCode));

            // Normalize GUID string (lowercase without braces/spaces)
            string normalized = activationCode.Trim().ToLowerInvariant();
            byte[] inputBytes = Encoding.UTF8.GetBytes($"{normalized}:{_pepper}");

            byte[] hashBytes = SHA256.HashData(inputBytes);
            return Convert.ToHexString(hashBytes).ToLowerInvariant();
        }

        public bool VerifyCode(string activationCode, string storedHash)
        {
            if (string.IsNullOrWhiteSpace(activationCode) || string.IsNullOrWhiteSpace(storedHash))
                return false;

            string computedHash = HashCode(activationCode);
            byte[] a = Encoding.UTF8.GetBytes(computedHash);
            byte[] b = Encoding.UTF8.GetBytes(storedHash.ToLowerInvariant());

            return CryptographicOperations.FixedTimeEquals(a, b);
        }
    }
}
