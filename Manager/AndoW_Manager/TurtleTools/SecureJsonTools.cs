using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;

namespace TurtleTools
{
    public static class SecureJsonTools
    {
        private const int SaltSize = 16;
        private const int IvSize = 16;
        private const int HmacSize = 32;
        private const int Iterations = 100000;
        private const string DefaultPassphrase = "ninja04!9akftp!";

        public static void WriteEncryptedJson(string filePath, object data, string passphrase = null)
        {
            if (string.IsNullOrWhiteSpace(filePath) || data == null)
            {
                return;
            }

            string json = JsonConvert.SerializeObject(data, Formatting.Indented);
            byte[] encrypted = Encrypt(json, passphrase);

            string directory = Path.GetDirectoryName(filePath);
            if (string.IsNullOrEmpty(directory) == false)
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(filePath, Convert.ToBase64String(encrypted), Encoding.UTF8);
        }

        public static T ReadEncryptedJson<T>(string filePath, string passphrase = null)
        {
            if (string.IsNullOrWhiteSpace(filePath) || File.Exists(filePath) == false)
            {
                return default(T);
            }

            string payloadBase64 = File.ReadAllText(filePath, Encoding.UTF8);
            byte[] encrypted = Convert.FromBase64String(payloadBase64);
            string json = Decrypt(encrypted, passphrase);

            if (json == null)
            {
                return default(T);
            }

            return JsonConvert.DeserializeObject<T>(json);
        }

        private static byte[] Encrypt(string plainText, string passphrase)
        {
            byte[] salt = GenerateRandomBytes(SaltSize);
            DeriveKeys(passphrase, salt, out byte[] encKey, out byte[] macKey);

            byte[] iv = GenerateRandomBytes(IvSize);
            byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);

            byte[] cipherBytes;
            using (Aes aes = Aes.Create())
            {
                aes.KeySize = 256;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;
                aes.Key = encKey;
                aes.IV = iv;

                using (ICryptoTransform encryptor = aes.CreateEncryptor())
                {
                    cipherBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);
                }
            }

            int totalLength = SaltSize + IvSize + cipherBytes.Length + HmacSize;
            byte[] output = new byte[totalLength];

            Buffer.BlockCopy(salt, 0, output, 0, SaltSize);
            Buffer.BlockCopy(iv, 0, output, SaltSize, IvSize);
            Buffer.BlockCopy(cipherBytes, 0, output, SaltSize + IvSize, cipherBytes.Length);

            byte[] mac = ComputeHmac(macKey, output, 0, SaltSize + IvSize + cipherBytes.Length);
            Buffer.BlockCopy(mac, 0, output, SaltSize + IvSize + cipherBytes.Length, mac.Length);

            return output;
        }

        private static string Decrypt(byte[] encryptedPayload, string passphrase)
        {
            if (encryptedPayload == null || encryptedPayload.Length < SaltSize + IvSize + HmacSize)
            {
                return null;
            }

            byte[] salt = new byte[SaltSize];
            byte[] iv = new byte[IvSize];
            Buffer.BlockCopy(encryptedPayload, 0, salt, 0, SaltSize);
            Buffer.BlockCopy(encryptedPayload, SaltSize, iv, 0, IvSize);

            int cipherLength = encryptedPayload.Length - SaltSize - IvSize - HmacSize;
            if (cipherLength <= 0)
            {
                return null;
            }

            byte[] cipherBytes = new byte[cipherLength];
            Buffer.BlockCopy(encryptedPayload, SaltSize + IvSize, cipherBytes, 0, cipherLength);

            byte[] providedMac = new byte[HmacSize];
            Buffer.BlockCopy(encryptedPayload, SaltSize + IvSize + cipherLength, providedMac, 0, HmacSize);

            DeriveKeys(passphrase, salt, out byte[] encKey, out byte[] macKey);

            byte[] expectedMac = ComputeHmac(macKey, encryptedPayload, 0, SaltSize + IvSize + cipherLength);
            if (!ConstantTimeEquals(providedMac, expectedMac))
            {
                return null;
            }

            using (Aes aes = Aes.Create())
            {
                aes.KeySize = 256;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;
                aes.Key = encKey;
                aes.IV = iv;

                using (ICryptoTransform decryptor = aes.CreateDecryptor())
                {
                    byte[] plainBytes = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);
                    return Encoding.UTF8.GetString(plainBytes);
                }
            }
        }

        private static void DeriveKeys(string passphrase, byte[] salt, out byte[] encKey, out byte[] macKey)
        {
            using (var kdf = new Rfc2898DeriveBytes(DefaultPassphrase, salt, Iterations, HashAlgorithmName.SHA256))
            {
                byte[] keyMaterial = kdf.GetBytes(64);
                encKey = new byte[32];
                macKey = new byte[32];

                Buffer.BlockCopy(keyMaterial, 0, encKey, 0, 32);
                Buffer.BlockCopy(keyMaterial, 32, macKey, 0, 32);
            }
        }

        private static byte[] GenerateRandomBytes(int length)
        {
            byte[] buffer = new byte[length];
            using (RandomNumberGenerator rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(buffer);
            }

            return buffer;
        }

        private static byte[] ComputeHmac(byte[] key, byte[] data, int offset, int count)
        {
            using (HMACSHA256 hmac = new HMACSHA256(key))
            {
                return hmac.ComputeHash(data, offset, count);
            }
        }

        private static bool ConstantTimeEquals(byte[] left, byte[] right)
        {
            if (left == null || right == null || left.Length != right.Length)
            {
                return false;
            }

            int diff = 0;
            for (int i = 0; i < left.Length; i++)
            {
                diff |= left[i] ^ right[i];
            }

            return diff == 0;
        }
    }
}
