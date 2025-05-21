using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace GenHub.Core.Helpers
{
    /// <summary>
    /// Simple encryption utility for token storage
    /// </summary>
    public static class SimpleEncryption
    {
        // Fixed encryption key and IV for simple token encryption
        private static readonly byte[] Key = new byte[] 
        { 0x23, 0x95, 0xAF, 0x19, 0x22, 0x78, 0x54, 0x87, 0xA3, 0xCD, 0xE2, 0x12, 0x45, 0xB5, 0x67, 0x98 };
        
        private static readonly byte[] IV = new byte[] 
        { 0xC2, 0x43, 0x65, 0x19, 0xB2, 0x8F, 0x31, 0xA1, 0xF6, 0x3C, 0xE5, 0xA2, 0x3D, 0x45, 0xD1, 0x5B };

        /// <summary>
        /// Encrypts a string value
        /// </summary>
        public static string Encrypt(string plainText)
        {
            if (string.IsNullOrEmpty(plainText))
                return string.Empty;

            try
            {
                using var aes = Aes.Create();
                aes.Key = Key;
                aes.IV = IV;

                using var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
                using var msEncrypt = new MemoryStream();
                using (var csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
                using (var swEncrypt = new StreamWriter(csEncrypt))
                {
                    swEncrypt.Write(plainText);
                }

                return Convert.ToBase64String(msEncrypt.ToArray());
            }
            catch (Exception)
            {
                // Don't expose encryption errors with specific details
                return string.Empty;
            }
        }

        /// <summary>
        /// Decrypts a string value
        /// </summary>
        public static string Decrypt(string cipherText)
        {
            if (string.IsNullOrEmpty(cipherText))
                return string.Empty;

            try
            {
                byte[] buffer = Convert.FromBase64String(cipherText);

                using var aes = Aes.Create();
                aes.Key = Key;
                aes.IV = IV;

                using var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
                using var msDecrypt = new MemoryStream(buffer);
                using var csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read);
                using var srDecrypt = new StreamReader(csDecrypt);
                
                return srDecrypt.ReadToEnd();
            }
            catch (Exception)
            {
                // Don't expose decryption errors with specific details
                return string.Empty;
            }
        }
    }
}

