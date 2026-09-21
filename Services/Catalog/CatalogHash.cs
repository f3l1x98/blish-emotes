using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace felix.BlishEmotes.Services.Catalog
{
    /// <summary>SHA-256 helpers matching the manifest's lowercase-hex <c>files</c> hashes.</summary>
    public static class CatalogHash
    {
        /// <summary>Lowercase hex SHA-256 of the given bytes; null/empty input hashes to the empty-input digest.</summary>
        public static string Sha256Hex(byte[] bytes)
        {
            using (var sha = SHA256.Create())
            {
                return ToHex(sha.ComputeHash(bytes ?? new byte[0]));
            }
        }

        /// <summary>Case-insensitive comparison against the manifest's expected hash.</summary>
        public static bool Verify(byte[] bytes, string expectedHash)
        {
            if (string.IsNullOrEmpty(expectedHash))
            {
                return false;
            }
            return string.Equals(Sha256Hex(bytes), expectedHash, StringComparison.OrdinalIgnoreCase);
        }

        private static string ToHex(byte[] hash)
        {
            var builder = new StringBuilder(hash.Length * 2);
            foreach (byte b in hash)
            {
                builder.Append(b.ToString("x2"));
            }
            return builder.ToString();
        }
    }
}
