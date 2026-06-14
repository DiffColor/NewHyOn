using LicenseHub.DeviceAuth.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

#pragma warning disable CS8603

namespace NewHyOn.Shared.Auth;

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
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
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
            return Deserialize(File.ReadAllText(path));
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

    public static void Delete()
    {
        string path = LicenseHubAuthPolicy.LicenseFilePath;
        if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
        {
            File.Delete(path);
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
    private static readonly JsonSerializerOptions JsonOptions = new()
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

        string deviceId = string.IsNullOrWhiteSpace(license.DeviceId)
            ? license.DeviceFingerprint
            : license.DeviceId;
        return Build(license.ProductId, deviceId);
    }

    public static string Build(ValidationResult validation)
    {
        if (validation == null || !validation.IsValid || string.IsNullOrWhiteSpace(validation.PayloadJson))
        {
            return string.Empty;
        }

        try
        {
            LicenseTokenPayload? payload = JsonSerializer.Deserialize<LicenseTokenPayload>(
                validation.PayloadJson,
                JsonOptions);
            if (payload == null || payload.ProductId != LicenseHubAuthPolicy.ProductId)
            {
                return string.Empty;
            }

            string deviceId = string.IsNullOrWhiteSpace(payload.DeviceId)
                ? payload.DeviceFingerprint
                : payload.DeviceId;
            return Build(payload.ProductId, deviceId);
        }
        catch
        {
            return string.Empty;
        }
    }

    public static bool IsValidationMarker(string raw)
    {
        return TryParse(raw, out _);
    }

    public static bool TryParse(string raw, out LicenseHubValidationMarker marker)
    {
        marker = new LicenseHubValidationMarker();
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        LicenseHubValidationMarker? parsedMarker;
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
        return string.Equals(marker.AuthProvider, "LicenseHub", StringComparison.OrdinalIgnoreCase)
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
        return ValidateUsingCore(
            license,
            currentFingerprint,
            LicenseHubAuthPolicy.LicenseFilePath,
            discardLocalLicenseWhenServerInvalid: true,
            enableServerValidation: true);
    }

    public static ValidationResult ValidateForDeviceFingerprint(LicenseHubLocalLicenseFile license, string expectedFingerprint)
    {
        return ValidateUsingCore(
            license,
            expectedFingerprint,
            LicenseHubAuthPolicy.LicenseFilePath,
            discardLocalLicenseWhenServerInvalid: false,
            enableServerValidation: false);
    }

    public static ValidationResult ValidateLocalForDeviceFingerprintOnly(LicenseHubLocalLicenseFile license, string expectedFingerprint)
    {
        return ValidateForDeviceFingerprint(license, expectedFingerprint);
    }

    public static bool IsCurrentDeviceLicense(string raw)
    {
        LicenseHubLocalLicenseFile license = LicenseHubLocalLicenseStore.TryDeserialize(raw);
        return ValidateForCurrentDevice(license).IsValid;
    }

    private static ValidationResult ValidateUsingCore(
        LicenseHubLocalLicenseFile license,
        string expectedFingerprint,
        string licenseFilePath,
        bool discardLocalLicenseWhenServerInvalid,
        bool enableServerValidation)
    {
        string currentFingerprint = (expectedFingerprint ?? string.Empty).Trim();
        return DeviceAuthClient.ValidateAsync(
            new ValidateOptions
            {
                ApiBaseUrl = LicenseHubAuthPolicy.ApiBaseUrl,
                ProductId = LicenseHubAuthPolicy.ProductId,
                PublicKeyPem = LicenseHubAuthPolicy.PublicKeyPem,
                LicenseFilePath = licenseFilePath ?? string.Empty,
                LicenseToken = license?.LicenseToken ?? string.Empty,
                DeviceFingerprint = currentFingerprint,
                DeviceId = license?.DeviceId ?? string.Empty,
                EnableServerValidation = enableServerValidation,
                DiscardLocalLicenseWhenServerInvalid = discardLocalLicenseWhenServerInvalid,
                ServerValidationTimeoutMilliseconds = 3000
            }).GetAwaiter().GetResult();
    }
}

public sealed class LicenseHubDeviceFingerprintInfo
{
    public string Fingerprint { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
}

public static class LicenseHubDeviceFingerprint
{
    private static readonly HashSet<string> DummyValues = new(StringComparer.OrdinalIgnoreCase)
    {
        "", "0", "00", "000", "0000", "00000000", "unknown", "none", "default", "n/a",
        "android", "alps", "generic", "goldfish", "default string"
    };

    public static LicenseHubDeviceFingerprintInfo Generate()
    {
        List<string> raw = new()
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
        if (DummyValues.Contains(normalized) || normalized.All(ch => ch == '0'))
        {
            return string.Empty;
        }

        return normalized;
    }

    private static string QueryWmi(string className, string property)
    {
        try
        {
            using ManagementObjectSearcher searcher = new("SELECT " + property + " FROM " + className);
            foreach (ManagementBaseObject item in searcher.Get())
            {
                object? value = item[property];
                return value == null ? string.Empty : value.ToString() ?? string.Empty;
            }
        }
        catch
        {
        }

        return string.Empty;
    }

    private static byte[] Sha256(string value)
    {
        return SHA256.HashData(Encoding.UTF8.GetBytes(value ?? string.Empty));
    }

    private static string ToHexUpper(byte[] bytes)
    {
        if (bytes == null || bytes.Length == 0)
        {
            return string.Empty;
        }

        StringBuilder builder = new(bytes.Length * 2);
        foreach (byte value in bytes)
        {
            builder.Append(value.ToString("X2"));
        }

        return builder.ToString();
    }
}
