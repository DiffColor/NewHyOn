using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;

namespace TurtleTools
{
    public static class SecureJsonTools
    {
        private const int IvSize = 16;
        private const int HmacSize = 32;
        private const string DefaultPassphrase = "ninja04!9akftp!";
        private static readonly byte[] FastMagic = Encoding.ASCII.GetBytes("NHY2");

        public static void WriteEncryptedJson(string filePath, object data, string passphrase = null)
        {
            if (string.IsNullOrWhiteSpace(filePath) || data == null)
            {
                return;
            }

            string json = JsonConvert.SerializeObject(data, Formatting.None);
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
            return EncryptFast(plainText, passphrase);
        }

        private static byte[] EncryptFast(string plainText, string passphrase)
        {
            DeriveFastKeys(passphrase, out byte[] encKey, out byte[] macKey);

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

            int authLength = FastMagic.Length + IvSize + cipherBytes.Length;
            byte[] output = new byte[authLength + HmacSize];
            Buffer.BlockCopy(FastMagic, 0, output, 0, FastMagic.Length);
            Buffer.BlockCopy(iv, 0, output, FastMagic.Length, IvSize);
            Buffer.BlockCopy(cipherBytes, 0, output, FastMagic.Length + IvSize, cipherBytes.Length);

            byte[] mac = ComputeHmac(macKey, output, 0, authLength);
            Buffer.BlockCopy(mac, 0, output, authLength, mac.Length);

            return output;
        }

        private static string Decrypt(byte[] encryptedPayload, string passphrase)
        {
            if (IsFastPayload(encryptedPayload) == false)
            {
                return null;
            }

            return DecryptFast(encryptedPayload, passphrase);
        }

        private static string DecryptFast(byte[] encryptedPayload, string passphrase)
        {
            if (encryptedPayload == null || encryptedPayload.Length < FastMagic.Length + IvSize + HmacSize)
            {
                return null;
            }

            DeriveFastKeys(passphrase, out byte[] encKey, out byte[] macKey);

            int cipherOffset = FastMagic.Length + IvSize;
            int cipherLength = encryptedPayload.Length - cipherOffset - HmacSize;
            if (cipherLength <= 0)
            {
                return null;
            }

            byte[] providedMac = new byte[HmacSize];
            Buffer.BlockCopy(encryptedPayload, cipherOffset + cipherLength, providedMac, 0, HmacSize);
            byte[] expectedMac = ComputeHmac(macKey, encryptedPayload, 0, cipherOffset + cipherLength);
            if (!ConstantTimeEquals(providedMac, expectedMac))
            {
                return null;
            }

            byte[] iv = new byte[IvSize];
            Buffer.BlockCopy(encryptedPayload, FastMagic.Length, iv, 0, IvSize);
            byte[] cipherBytes = new byte[cipherLength];
            Buffer.BlockCopy(encryptedPayload, cipherOffset, cipherBytes, 0, cipherLength);

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

        private static bool IsFastPayload(byte[] payload)
        {
            if (payload == null || payload.Length < FastMagic.Length)
            {
                return false;
            }

            for (int i = 0; i < FastMagic.Length; i++)
            {
                if (payload[i] != FastMagic[i])
                {
                    return false;
                }
            }
            return true;
        }

        private static void DeriveFastKeys(string passphrase, out byte[] encKey, out byte[] macKey)
        {
            string secret = string.IsNullOrEmpty(passphrase) ? DefaultPassphrase : passphrase;
            encKey = ComputeSha256(secret + ":enc");
            macKey = ComputeSha256(secret + ":mac");
        }

        private static byte[] ComputeSha256(string value)
        {
            using (SHA256 sha = SHA256.Create())
            {
                return sha.ComputeHash(Encoding.UTF8.GetBytes(value ?? string.Empty));
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
