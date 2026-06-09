using LicenseHub.DeviceAuth.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management;
using System.Net;
using System.Net.NetworkInformation;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

#pragma warning disable CS8603

namespace NewHyOn.Shared.Auth
{
    public static class LicenseHubAuthPolicy
    {
        public const int ProductId = 6;
        public const int AuthWindowWidth = 1060;
        public const int AuthWindowHeight = 760;
        public const string ApiBaseUrl = "https://licensehub.ilycode.app";
        public const string AuthWindowTitle = "디바이스 인증 필요";

        public static string LicenseFilePath
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "LicenseHub",
                    "licenses",
                    "product-" + ProductId + ".json");
            }
        }

        public static string PublicKeyPem
        {
            get { return DeviceAuthDefaults.DefaultPublicKeyPem; }
        }
    }

    public sealed class LicenseHubLocalLicenseFile
    {
        public int ProductId { get; set; }
        public string DeviceFingerprint { get; set; } = string.Empty;
        public string DeviceId { get; set; } = string.Empty;
        public string LicenseToken { get; set; } = string.Empty;
        public DateTimeOffset SavedAt { get; set; } = DateTimeOffset.UtcNow;
    }

    public static class LicenseHubLocalLicenseStore
    {
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = true
        };

        public static LicenseHubLocalLicenseFile Read()
        {
            string path = LicenseHubAuthPolicy.LicenseFilePath;
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                return null;
            }

            try
            {
                string raw = File.ReadAllText(path);
                return Deserialize(raw);
            }
            catch
            {
                return null;
            }
        }

        public static void Write(LicenseHubLocalLicenseFile license)
        {
            if (license == null)
            {
                return;
            }

            string path = LicenseHubAuthPolicy.LicenseFilePath;
            string directory = Path.GetDirectoryName(path) ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(path, Serialize(license));
        }

        public static string Serialize(LicenseHubLocalLicenseFile license)
        {
            return license == null ? string.Empty : JsonSerializer.Serialize(license, JsonOptions);
        }

        public static LicenseHubLocalLicenseFile Deserialize(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return null;
            }

            return JsonSerializer.Deserialize<LicenseHubLocalLicenseFile>(raw, JsonOptions);
        }

        public static LicenseHubLocalLicenseFile TryDeserialize(string raw)
        {
            try
            {
                return Deserialize(raw);
            }
            catch
            {
                return null;
            }
        }

    }

    public sealed class LicenseHubValidationMarker
    {
        public string AuthProvider { get; set; } = "LicenseHub";
        public string AuthSchema { get; set; } = "ValidationResult";
        public int AuthVersion { get; set; } = 2;
        public int ProductId { get; set; }
        public bool IsValid { get; set; }
        public string DeviceId { get; set; } = string.Empty;
    }

    public static class LicenseHubAuthMarker
    {
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        public static string Build(string deviceId)
        {
            return Build(LicenseHubAuthPolicy.ProductId, deviceId);
        }

        public static string Build(int productId, string deviceId)
        {
            string normalizedDeviceId = (deviceId ?? string.Empty).Trim();
            if (productId <= 0 || string.IsNullOrWhiteSpace(normalizedDeviceId))
            {
                return string.Empty;
            }

            return JsonSerializer.Serialize(
                new LicenseHubValidationMarker
                {
                    ProductId = productId,
                    IsValid = true,
                    DeviceId = normalizedDeviceId
                },
                JsonOptions);
        }

        public static string Build(LicenseHubLocalLicenseFile license)
        {
            if (license == null || license.ProductId != LicenseHubAuthPolicy.ProductId)
            {
                return string.Empty;
            }

            return Build(license.ProductId, license.DeviceId);
        }

        public static bool IsValidationMarker(string raw)
        {
            LicenseHubValidationMarker marker;
            return TryParse(raw, out marker);
        }

        public static bool TryParse(string raw, out LicenseHubValidationMarker marker)
        {
            marker = new LicenseHubValidationMarker();
            if (string.IsNullOrWhiteSpace(raw))
            {
                return false;
            }

            var parsedMarker = new LicenseHubValidationMarker();
            try
            {
                parsedMarker = JsonSerializer.Deserialize<LicenseHubValidationMarker>(raw, JsonOptions);
            }
            catch
            {
                return false;
            }

            if (parsedMarker == null)
            {
                return false;
            }

            marker = parsedMarker;
            return marker != null
                && string.Equals(marker.AuthProvider, "LicenseHub", StringComparison.OrdinalIgnoreCase)
                && string.Equals(marker.AuthSchema, "ValidationResult", StringComparison.OrdinalIgnoreCase)
                && marker.AuthVersion >= 2
                && marker.ProductId > 0
                && marker.IsValid
                && !string.IsNullOrWhiteSpace(marker.DeviceId);
        }
    }

    public static class LicenseHubLocalValidator
    {
        public static ValidationResult Validate()
        {
            return ValidateForCurrentDevice(LicenseHubLocalLicenseStore.Read());
        }

        public static ValidationResult ValidateForCurrentDevice(LicenseHubLocalLicenseFile license)
        {
            string currentFingerprint = LicenseHubDeviceFingerprint.Generate().Fingerprint;
            if (!IsLicenseHubApiReachable())
            {
                return ValidateLocalForDeviceFingerprint(license, currentFingerprint);
            }

            return ValidateUsingCore(
                license,
                currentFingerprint,
                LicenseHubAuthPolicy.LicenseFilePath,
                discardLocalLicenseWhenServerInvalid: true,
                allowServerBootstrap: true);
        }

        public static ValidationResult ValidateForDeviceFingerprint(LicenseHubLocalLicenseFile license, string expectedFingerprint)
        {
            return ValidateLocalForDeviceFingerprint(license, expectedFingerprint);
        }

        public static ValidationResult ValidateLocalForDeviceFingerprintOnly(LicenseHubLocalLicenseFile license, string expectedFingerprint)
        {
            return ValidateLocalForDeviceFingerprint(license, expectedFingerprint);
        }

        private static ValidationResult ValidateUsingCore(
            LicenseHubLocalLicenseFile license,
            string expectedFingerprint,
            string licenseFilePath,
            bool discardLocalLicenseWhenServerInvalid,
            bool allowServerBootstrap)
        {
            string currentFingerprint = (expectedFingerprint ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(currentFingerprint))
            {
                return Invalid("기기식별키가 없습니다.");
            }

            if (license == null && !allowServerBootstrap)
            {
                return Invalid("저장된 라이선스 파일이 없습니다.");
            }

            try
            {
                return LicenseHub.DeviceAuth.Core.DeviceAuthClient.ValidateAsync(
                    new LicenseHub.DeviceAuth.Core.ValidateOptions
                    {
                        ApiBaseUrl = LicenseHubAuthPolicy.ApiBaseUrl,
                        ProductId = LicenseHubAuthPolicy.ProductId,
                        PublicKeyPem = LicenseHubAuthPolicy.PublicKeyPem,
                        LicenseFilePath = licenseFilePath ?? string.Empty,
                        LicenseToken = license?.LicenseToken ?? string.Empty,
                        DeviceFingerprint = currentFingerprint,
                        DeviceId = license?.DeviceId ?? string.Empty,
                        EnableServerValidation = true,
                        DiscardLocalLicenseWhenServerInvalid = discardLocalLicenseWhenServerInvalid,
                        ServerValidationTimeoutMilliseconds = 3000
                    }).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                return Invalid("서버 검증 중 오류가 발생했습니다: " + ex.Message);
            }
        }

        private static ValidationResult ValidateLocalForDeviceFingerprint(LicenseHubLocalLicenseFile license, string expectedFingerprint)
        {
            if (license == null)
            {
                return Invalid("저장된 라이선스 파일이 없습니다.");
            }

            if (license.ProductId != LicenseHubAuthPolicy.ProductId)
            {
                return Invalid("라이선스 productId가 일치하지 않습니다.");
            }

            string token = (license.LicenseToken ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(token))
            {
                return Invalid("저장된 라이선스 토큰이 없습니다.");
            }

            string currentFingerprint = (expectedFingerprint ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(currentFingerprint))
            {
                return Invalid("기기식별키가 없습니다.");
            }

            string fingerprint = (license.DeviceFingerprint ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(fingerprint))
            {
                fingerprint = currentFingerprint;
            }

            if (!string.Equals(fingerprint, currentFingerprint, StringComparison.OrdinalIgnoreCase))
            {
                return Invalid("라이선스 기기식별키가 현재 장비와 일치하지 않습니다.");
            }

            return LicenseTokenValidator.Validate(
                token,
                LicenseHubAuthPolicy.PublicKeyPem,
                LicenseHubAuthPolicy.ProductId,
                fingerprint.ToUpperInvariant(),
                DateTimeOffset.UtcNow);
        }

        public static bool IsCurrentDeviceLicense(string raw)
        {
            LicenseHubLocalLicenseFile license = LicenseHubLocalLicenseStore.TryDeserialize(raw);
            return ValidateForCurrentDevice(license).IsValid;
        }

        private static ValidationResult Invalid(string reason)
        {
            return new ValidationResult
            {
                IsValid = false,
                Reason = reason
            };
        }

        private static bool IsLicenseHubApiReachable()
        {
            try
            {
                if (!HasUsableNetworkInterface())
                {
                    return false;
                }

                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(LicenseHubAuthPolicy.ApiBaseUrl);
                request.Method = "GET";
                request.Timeout = 1500;
                request.ReadWriteTimeout = 1500;
                request.AllowAutoRedirect = false;
                request.UserAgent = "NewHyOn-LicenseHub-Validator";

                using (request.GetResponse())
                {
                    return true;
                }
            }
            catch (WebException ex)
            {
                return ex.Response is HttpWebResponse;
            }
            catch
            {
                return false;
            }
        }

        private static bool HasUsableNetworkInterface()
        {
            try
            {
                NetworkInterface[] adapters = NetworkInterface.GetAllNetworkInterfaces();
                if (adapters == null || adapters.Length == 0)
                {
                    return false;
                }

                foreach (NetworkInterface adapter in adapters)
                {
                    if (adapter == null)
                    {
                        continue;
                    }

                    if (adapter.OperationalStatus != OperationalStatus.Up ||
                        adapter.NetworkInterfaceType == NetworkInterfaceType.Loopback ||
                        adapter.NetworkInterfaceType == NetworkInterfaceType.Tunnel)
                    {
                        continue;
                    }

                    return true;
                }
            }
            catch
            {
            }

            return false;
        }
    }

    public sealed class LicenseHubDeviceFingerprintInfo
    {
        public string Fingerprint { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
    }

    public static class LicenseHubDeviceFingerprint
    {
        private static readonly HashSet<string> DummyValues = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "", "0", "00", "000", "0000", "00000000", "unknown", "none", "default", "n/a",
            "android", "alps", "generic", "goldfish", "default string"
        };

        public static LicenseHubDeviceFingerprintInfo Generate()
        {
            List<string> raw = new List<string>
            {
                QueryWmi("Win32_Processor", "ProcessorId"),
                QueryWmi("Win32_BaseBoard", "SerialNumber"),
                QueryWmi("Win32_BIOS", "SerialNumber"),
                QueryWmi("Win32_DiskDrive", "SerialNumber"),
                QueryWmi("Win32_ComputerSystemProduct", "UUID")
            };

            string[] normalized = raw
                .Select(Normalize)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();

            if (normalized.Length == 0)
            {
                string fallback = Environment.MachineName + "|" + Environment.OSVersion.VersionString;
                return new LicenseHubDeviceFingerprintInfo
                {
                    Source = fallback,
                    Fingerprint = ToHexUpper(Sha256(fallback))
                };
            }

            string source = string.Join("|", normalized);
            return new LicenseHubDeviceFingerprintInfo
            {
                Source = source,
                Fingerprint = ToHexUpper(Sha256(source))
            };
        }

        private static string Normalize(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            string normalized = value.Trim().ToLowerInvariant();
            if (DummyValues.Contains(normalized))
            {
                return string.Empty;
            }

            if (normalized.All(ch => ch == '0'))
            {
                return string.Empty;
            }

            return normalized;
        }

        private static string QueryWmi(string className, string property)
        {
            try
            {
                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT " + property + " FROM " + className))
                {
                    foreach (ManagementBaseObject item in searcher.Get())
                    {
                        object value = item[property];
                        return value == null ? string.Empty : value.ToString();
                    }
                }
            }
            catch
            {
            }

            return string.Empty;
        }

        private static byte[] Sha256(string value)
        {
            using (SHA256 sha = SHA256.Create())
            {
                return sha.ComputeHash(Encoding.UTF8.GetBytes(value ?? string.Empty));
            }
        }

        private static string ToHexUpper(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0)
            {
                return string.Empty;
            }

            StringBuilder builder = new StringBuilder(bytes.Length * 2);
            foreach (byte value in bytes)
            {
                builder.Append(value.ToString("X2"));
            }

            return builder.ToString();
        }
    }

    public static class LegacyAuthKeyValidator
    {
        public static bool IsValidForCurrentDevice(string encodedKey)
        {
            if (string.IsNullOrWhiteSpace(encodedKey) || LicenseHubAuthMarker.IsValidationMarker(encodedKey))
            {
                return false;
            }

            string expected = DecodeAuthKey(encodedKey).Trim();
            if (string.IsNullOrWhiteSpace(expected))
            {
                return false;
            }

            foreach (string mac in GetAllMacAddresses())
            {
                if (string.Equals(expected, NormalizeDeviceKey(mac), StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            string uuid = GetUuid12FromWmi();
            return !string.IsNullOrWhiteSpace(uuid)
                && string.Equals(expected, NormalizeDeviceKey(uuid), StringComparison.OrdinalIgnoreCase);
        }

        public static string EncodeAuthKey(string sourceKey)
        {
            string mixed = MixMultipleString(NormalizeDeviceKey(sourceKey));
            if (string.IsNullOrEmpty(mixed))
            {
                return string.Empty;
            }

            byte[] asciiBytes = Encoding.ASCII.GetBytes(mixed);
            byte[] shiftedBytes = MoveDiff(asciiBytes);
            return Encoding.ASCII.GetString(shiftedBytes);
        }

        private static string DecodeAuthKey(string authKey)
        {
            if (string.IsNullOrWhiteSpace(authKey))
            {
                return string.Empty;
            }

            byte[] asciiBytes = Encoding.ASCII.GetBytes(authKey.Trim());
            byte[] restoredBytes = MoveDiff(asciiBytes);
            string restored = Encoding.ASCII.GetString(restoredBytes);
            return RestoreMixedString(restored);
        }

        private static string MixMultipleString(string sourceKey)
        {
            if (string.IsNullOrWhiteSpace(sourceKey) || sourceKey.Length < 12)
            {
                return string.Empty;
            }

            string left = string.Empty;
            string right = string.Empty;
            int index = 0;
            while (left.Length < sourceKey.Length / 2 && index + 3 < sourceKey.Length)
            {
                left += sourceKey.Substring(index, 2);
                index += 2;
                right += sourceKey.Substring(index, 2);
                index += 2;
            }

            return left + right;
        }

        private static string RestoreMixedString(string mixedKey)
        {
            if (string.IsNullOrWhiteSpace(mixedKey) || mixedKey.Length < 12)
            {
                return string.Empty;
            }

            string restored = string.Empty;
            int half = mixedKey.Length / 2;
            for (int index = 0; index + 1 < half; index += 2)
            {
                restored += mixedKey.Substring(index, 2);
                if (index + half + 1 < mixedKey.Length)
                {
                    restored += mixedKey.Substring(index + half, 2);
                }
            }

            return restored;
        }

        private static byte[] MoveDiff(byte[] bytes)
        {
            if (bytes == null)
            {
                return new byte[0];
            }

            List<byte> output = new List<byte>();
            foreach (byte item in bytes)
            {
                int dec = Convert.ToInt32(item);
                if (dec >= 48 && dec <= 57)
                {
                    output.Add(Convert.ToByte(48 + (57 - dec)));
                }
                else if (dec >= 65 && dec <= 90)
                {
                    output.Add(Convert.ToByte(65 + (90 - dec)));
                }
                else
                {
                    output.Add(Convert.ToByte(97 + (122 - dec)));
                }
            }

            return output.ToArray();
        }

        private static List<string> GetAllMacAddresses()
        {
            List<string> values = new List<string>();
            try
            {
                NetworkInterface[] adapters = NetworkInterface.GetAllNetworkInterfaces();
                if (adapters == null || adapters.Length == 0)
                {
                    return values;
                }

                foreach (NetworkInterface adapter in adapters)
                {
                    if (adapter == null)
                    {
                        continue;
                    }

                    string address = adapter.GetPhysicalAddress()?.ToString() ?? string.Empty;
                    if (!string.IsNullOrWhiteSpace(address))
                    {
                        values.Add(NormalizeDeviceKey(address));
                    }
                }
            }
            catch
            {
            }

            return values;
        }

        private static string GetUuid12FromWmi()
        {
            try
            {
                using (ManagementClass computerSystemProduct = new ManagementClass("Win32_ComputerSystemProduct"))
                using (ManagementObjectCollection instances = computerSystemProduct.GetInstances())
                {
                    foreach (ManagementObject instance in instances)
                    {
                        string rawUuid = instance.Properties["UUID"]?.Value?.ToString() ?? string.Empty;
                        if (string.IsNullOrWhiteSpace(rawUuid))
                        {
                            continue;
                        }

                        string[] parts = rawUuid.Split(new[] { '-' }, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length > 0)
                        {
                            return NormalizeDeviceKey(parts[parts.Length - 1]);
                        }
                    }
                }
            }
            catch
            {
            }

            return string.Empty;
        }

        private static string NormalizeDeviceKey(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : value.Replace(":", string.Empty).Replace("-", string.Empty).Trim().ToUpperInvariant();
        }
    }
}
