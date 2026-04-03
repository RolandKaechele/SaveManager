using System;
using System.Security.Cryptography;
using System.Text;

namespace SaveManager.Runtime
{
    /// <summary>
    /// AES-256-CBC encryption + HMAC-SHA256 integrity check for save file content.
    /// 
    /// Blob layout (Base64-encoded on disk):
    ///   [IV  – 16 bytes]
    ///   [MAC – 32 bytes]  HMAC-SHA256 over (IV + ciphertext)
    ///   [Ciphertext – variable]
    /// 
    /// The key is derived from the passphrase via SHA-256 so any length works.
    /// Note: this protects against casual cheating; it does not prevent reverse-
    /// engineering of a shipped binary that contains the passphrase.
    /// </summary>
    internal static class SaveCrypto
    {
        private const int IvSize  = 16;
        private const int MacSize = 32;

        // -------------------------------------------------------------------------
        // Key derivation
        // -------------------------------------------------------------------------

        private static byte[] DeriveKey(string passphrase)
        {
            using var sha = SHA256.Create();
            return sha.ComputeHash(Encoding.UTF8.GetBytes(passphrase));
        }

        // -------------------------------------------------------------------------
        // Encrypt
        // -------------------------------------------------------------------------

        /// <summary>
        /// Encrypt <paramref name="plaintext"/> and return a Base64 blob.
        /// </summary>
        internal static string Encrypt(string plaintext, string passphrase)
        {
            byte[] key  = DeriveKey(passphrase);
            byte[] data = Encoding.UTF8.GetBytes(plaintext);

            using var aes = Aes.Create();
            aes.KeySize = 256;
            aes.Mode    = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            aes.Key     = key;
            aes.GenerateIV();
            byte[] iv = aes.IV;

            byte[] cipher;
            using (var enc = aes.CreateEncryptor())
                cipher = enc.TransformFinalBlock(data, 0, data.Length);

            // MAC over IV + ciphertext (encrypt-then-MAC)
            byte[] payload = new byte[IvSize + cipher.Length];
            Buffer.BlockCopy(iv,     0, payload, 0,      IvSize);
            Buffer.BlockCopy(cipher, 0, payload, IvSize, cipher.Length);

            using var hmac = new HMACSHA256(key);
            byte[] mac = hmac.ComputeHash(payload);

            // Final blob: IV | MAC | ciphertext
            byte[] blob = new byte[IvSize + MacSize + cipher.Length];
            Buffer.BlockCopy(iv,     0, blob, 0,              IvSize);
            Buffer.BlockCopy(mac,    0, blob, IvSize,         MacSize);
            Buffer.BlockCopy(cipher, 0, blob, IvSize + MacSize, cipher.Length);

            return Convert.ToBase64String(blob);
        }

        // -------------------------------------------------------------------------
        // Decrypt
        // -------------------------------------------------------------------------

        /// <summary>
        /// Decrypt a Base64 blob produced by <see cref="Encrypt"/>.
        /// Returns null if the data has been tampered with, the key is wrong,
        /// or the input is not a valid blob.
        /// </summary>
        internal static string Decrypt(string base64, string passphrase)
        {
            byte[] blob;
            try { blob = Convert.FromBase64String(base64); }
            catch { return null; }

            if (blob.Length < IvSize + MacSize + 1) return null;

            byte[] iv     = new byte[IvSize];
            byte[] mac    = new byte[MacSize];
            byte[] cipher = new byte[blob.Length - IvSize - MacSize];

            Buffer.BlockCopy(blob, 0,              iv,     0, IvSize);
            Buffer.BlockCopy(blob, IvSize,         mac,    0, MacSize);
            Buffer.BlockCopy(blob, IvSize + MacSize, cipher, 0, cipher.Length);

            byte[] key = DeriveKey(passphrase);

            // Verify MAC before decrypting (prevents padding-oracle attacks)
            byte[] payload = new byte[IvSize + cipher.Length];
            Buffer.BlockCopy(iv,     0, payload, 0,      IvSize);
            Buffer.BlockCopy(cipher, 0, payload, IvSize, cipher.Length);

            using var hmac = new HMACSHA256(key);
            byte[] expected = hmac.ComputeHash(payload);
            if (!ConstantTimeEquals(mac, expected)) return null;

            using var aes = Aes.Create();
            aes.KeySize = 256;
            aes.Mode    = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            aes.Key     = key;
            aes.IV      = iv;

            try
            {
                using var dec = aes.CreateDecryptor();
                byte[] plain = dec.TransformFinalBlock(cipher, 0, cipher.Length);
                return Encoding.UTF8.GetString(plain);
            }
            catch { return null; }
        }

        // -------------------------------------------------------------------------
        // Helpers
        // -------------------------------------------------------------------------

        /// <summary>Constant-time byte-array comparison (prevents timing attacks).</summary>
        private static bool ConstantTimeEquals(byte[] a, byte[] b)
        {
            if (a.Length != b.Length) return false;
            int diff = 0;
            for (int i = 0; i < a.Length; i++)
                diff |= a[i] ^ b[i];
            return diff == 0;
        }

        // -------------------------------------------------------------------------
        // Binary overloads (used for screenshot files)
        // -------------------------------------------------------------------------

        /// <summary>
        /// Encrypt raw bytes and return the blob as a byte array (written directly to disk).
        /// Blob layout: [IV – 16 bytes][HMAC-SHA256 – 32 bytes][ciphertext]
        /// </summary>
        internal static byte[] EncryptBytes(byte[] data, string passphrase)
        {
            byte[] key = DeriveKey(passphrase);

            using var aes = Aes.Create();
            aes.KeySize = 256;
            aes.Mode    = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            aes.Key     = key;
            aes.GenerateIV();
            byte[] iv = aes.IV;

            byte[] cipher;
            using (var enc = aes.CreateEncryptor())
                cipher = enc.TransformFinalBlock(data, 0, data.Length);

            byte[] payload = new byte[IvSize + cipher.Length];
            Buffer.BlockCopy(iv,     0, payload, 0,      IvSize);
            Buffer.BlockCopy(cipher, 0, payload, IvSize, cipher.Length);

            using var hmac = new HMACSHA256(key);
            byte[] mac = hmac.ComputeHash(payload);

            byte[] blob = new byte[IvSize + MacSize + cipher.Length];
            Buffer.BlockCopy(iv,     0, blob, 0,               IvSize);
            Buffer.BlockCopy(mac,    0, blob, IvSize,          MacSize);
            Buffer.BlockCopy(cipher, 0, blob, IvSize + MacSize, cipher.Length);
            return blob;
        }

        /// <summary>
        /// Decrypt a blob produced by <see cref="EncryptBytes"/>.
        /// Returns null if the data has been tampered with or the key is wrong.
        /// </summary>
        internal static byte[] DecryptBytes(byte[] blob, string passphrase)
        {
            if (blob == null || blob.Length < IvSize + MacSize + 1) return null;

            byte[] iv     = new byte[IvSize];
            byte[] mac    = new byte[MacSize];
            byte[] cipher = new byte[blob.Length - IvSize - MacSize];

            Buffer.BlockCopy(blob, 0,               iv,     0, IvSize);
            Buffer.BlockCopy(blob, IvSize,           mac,    0, MacSize);
            Buffer.BlockCopy(blob, IvSize + MacSize, cipher, 0, cipher.Length);

            byte[] key = DeriveKey(passphrase);

            byte[] payload = new byte[IvSize + cipher.Length];
            Buffer.BlockCopy(iv,     0, payload, 0,      IvSize);
            Buffer.BlockCopy(cipher, 0, payload, IvSize, cipher.Length);

            using var hmac = new HMACSHA256(key);
            byte[] expected = hmac.ComputeHash(payload);
            if (!ConstantTimeEquals(mac, expected)) return null;

            using var aes = Aes.Create();
            aes.KeySize = 256;
            aes.Mode    = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            aes.Key     = key;
            aes.IV      = iv;

            try
            {
                using var dec = aes.CreateDecryptor();
                return dec.TransformFinalBlock(cipher, 0, cipher.Length);
            }
            catch { return null; }
        }
    }
}
