using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.OpenSsl;

namespace NewHyOn.Shared.Auth
{
    public static class LicenseHubAuthPolicy
    {
        public const int ProductId = 6;
        public const int AuthWindowWidth = 1060;
        public const int AuthWindowHeight = 760;
        public const string ApiBaseUrl = "https://licensehub.ilycode.app";
        public const string AuthWindowTitle = "디바이스 인증 필요";
        private const string DefaultPublicKeyPem = "-----BEGIN PUBLIC KEY-----\n" +
                                                   "MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAEAhMxB0eP/q5vyjFOwWENS68uq/d1\n" +
                                                   "hmq6Uv1tHpjMExWVgY3jhbDZ9dM9EyWJ3XXCI8IMgSyF6pKEm6K3LplFHQ==\n" +
                                                   "-----END PUBLIC KEY-----";

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
            get { return DefaultPublicKeyPem; }
        }
    }

    public sealed class LicenseHubLocalLicenseFile
    {
        public int ProductId { get; set; }
        public string DeviceFingerprint { get; set; }
        public string DeviceId { get; set; }
        public string LicenseToken { get; set; }
        public DateTimeOffset SavedAt { get; set; }

        public LicenseHubLocalLicenseFile()
        {
            DeviceFingerprint = string.Empty;
            DeviceId = string.Empty;
            LicenseToken = string.Empty;
            SavedAt = DateTimeOffset.UtcNow;
        }
    }

    public sealed class ValidationResult
    {
        public bool IsValid { get; set; }
        public string Reason { get; set; }
        public string PayloadJson { get; set; }
        public DateTimeOffset? ExpiresAt { get; set; }
        public bool ServerChecked { get; set; }
        public bool UsedOfflineFallback { get; set; }
        public bool LocalLicenseDiscarded { get; set; }
        public string ServerStatus { get; set; }

        public ValidationResult()
        {
            Reason = string.Empty;
            PayloadJson = string.Empty;
            ServerStatus = string.Empty;
        }
    }

    public sealed class LicenseTokenPayload
    {
        public int Version { get; set; }
        public string LicenseId { get; set; }
        public string DeviceId { get; set; }
        public string DeviceFingerprint { get; set; }
        public string SoftwareId { get; set; }
        public int ProductId { get; set; }
        public string Status { get; set; }
        public long IssuedAt { get; set; }
        public long ExpiresAt { get; set; }

        public LicenseTokenPayload()
        {
            LicenseId = string.Empty;
            DeviceId = string.Empty;
            DeviceFingerprint = string.Empty;
            SoftwareId = string.Empty;
            Status = string.Empty;
        }
    }

    internal sealed class DeviceLicenseValidateRequest
    {
        public int ProductId { get; set; }
        public string DeviceFingerprint { get; set; }
        public string LicenseToken { get; set; }
        public string DeviceId { get; set; }

        public DeviceLicenseValidateRequest()
        {
            DeviceFingerprint = string.Empty;
            LicenseToken = string.Empty;
            DeviceId = string.Empty;
        }
    }

    internal sealed class DeviceLicenseValidateResponse
    {
        public bool IsValid { get; set; }
        public bool ShouldDiscardLocal { get; set; }
        public string Status { get; set; }
        public string Reason { get; set; }
        public string LicenseId { get; set; }
        public string DeviceId { get; set; }
        public int ProductId { get; set; }
        public DateTimeOffset? ExpiresAt { get; set; }
        public DateTimeOffset CheckedAt { get; set; }

        public DeviceLicenseValidateResponse()
        {
            Status = string.Empty;
            Reason = string.Empty;
            LicenseId = string.Empty;
            DeviceId = string.Empty;
        }
    }

    internal sealed class DeviceLicenseBootstrapRequest
    {
        public int ProductId { get; set; }
        public string DeviceFingerprint { get; set; }

        public DeviceLicenseBootstrapRequest()
        {
            DeviceFingerprint = string.Empty;
        }
    }

    internal sealed class DeviceLicenseBootstrapResponse
    {
        public bool IsIssued { get; set; }
        public string Status { get; set; }
        public string Reason { get; set; }
        public string LicenseToken { get; set; }
        public string LicenseId { get; set; }
        public string DeviceId { get; set; }
        public int ProductId { get; set; }
        public DateTimeOffset? ExpiresAt { get; set; }
        public DateTimeOffset CheckedAt { get; set; }

        public DeviceLicenseBootstrapResponse()
        {
            Status = string.Empty;
            Reason = string.Empty;
            LicenseToken = string.Empty;
            LicenseId = string.Empty;
            DeviceId = string.Empty;
        }
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
                return Deserialize(File.ReadAllText(path));
            }
            catch
            {
                return null;
            }
        }

        public static void Write(LicenseHubLocalLicenseFile license)
        {
            Write(LicenseHubAuthPolicy.LicenseFilePath, license);
        }

        public static void Write(string path, LicenseHubLocalLicenseFile license)
        {
            if (license == null)
            {
                return;
            }

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
            Delete(LicenseHubAuthPolicy.LicenseFilePath);
        }

        public static void Delete(string path)
        {
            if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    public sealed class LicenseHubValidationMarker
    {
        public string AuthProvider { get; set; }
        public string AuthSchema { get; set; }
        public int AuthVersion { get; set; }
        public int ProductId { get; set; }
        public bool IsValid { get; set; }
        public string DeviceId { get; set; }

        public LicenseHubValidationMarker()
        {
            AuthProvider = "LicenseHub";
            AuthSchema = "ValidationResult";
            AuthVersion = 2;
            DeviceId = string.Empty;
        }
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
                LicenseTokenPayload payload = JsonSerializer.Deserialize<LicenseTokenPayload>(
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

            LicenseHubValidationMarker parsedMarker;
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
            return LicenseHubStandaloneValidator.Validate(
                license,
                currentFingerprint,
                LicenseHubAuthPolicy.LicenseFilePath,
                discardLocalLicenseWhenServerInvalid: true,
                enableServerValidation: true);
        }

        public static ValidationResult ValidateForDeviceFingerprint(LicenseHubLocalLicenseFile license, string expectedFingerprint)
        {
            return LicenseHubStandaloneValidator.Validate(
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
    }

    internal static class LicenseHubStandaloneValidator
    {
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = true
        };

        public static ValidationResult Validate(
            LicenseHubLocalLicenseFile license,
            string expectedFingerprint,
            string licenseFilePath,
            bool discardLocalLicenseWhenServerInvalid,
            bool enableServerValidation)
        {
            string token;
            string fingerprint;
            string resolvedDeviceId;
            string licensePath = string.IsNullOrWhiteSpace(licenseFilePath)
                ? LicenseHubAuthPolicy.LicenseFilePath
                : licenseFilePath.Trim();

            LicenseHubLocalLicenseFile localLicense = license;
            if (localLicense == null && !string.IsNullOrWhiteSpace(licensePath) && File.Exists(licensePath))
            {
                localLicense = LicenseHubLocalLicenseStore.Read();
            }

            token = (localLicense == null ? string.Empty : localLicense.LicenseToken ?? string.Empty).Trim();
            fingerprint = (expectedFingerprint ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(fingerprint))
            {
                fingerprint = (localLicense == null ? string.Empty : localLicense.DeviceFingerprint ?? string.Empty).Trim();
            }
            fingerprint = (fingerprint ?? string.Empty).Trim().ToUpperInvariant();

            resolvedDeviceId = (localLicense == null ? string.Empty : localLicense.DeviceId ?? string.Empty).Trim();
            string apiBase = NormalizeApiBase(LicenseHubAuthPolicy.ApiBaseUrl);

            if (string.IsNullOrWhiteSpace(fingerprint))
            {
                return new ValidationResult
                {
                    IsValid = false,
                    Reason = "기기식별키가 없습니다.",
                    ServerChecked = false,
                    UsedOfflineFallback = false
                };
            }

            if (string.IsNullOrWhiteSpace(token))
            {
                if (!enableServerValidation)
                {
                    return new ValidationResult
                    {
                        IsValid = false,
                        Reason = "검증할 라이선스 토큰이 없습니다."
                    };
                }

                BootstrapValidationResult bootstrap = TryBootstrap(
                    LicenseHubAuthPolicy.ProductId,
                    fingerprint,
                    apiBase,
                    licensePath);
                if (!bootstrap.Result.IsValid || string.IsNullOrWhiteSpace(bootstrap.Token))
                {
                    return bootstrap.Result;
                }

                token = bootstrap.Token;
                licensePath = bootstrap.LicensePath;
                if (string.IsNullOrWhiteSpace(resolvedDeviceId))
                {
                    resolvedDeviceId = bootstrap.DeviceId;
                }
            }

            ValidationResult localResult = LicenseHubStandaloneTokenValidator.Validate(
                token,
                LicenseHubAuthPolicy.PublicKeyPem,
                LicenseHubAuthPolicy.ProductId,
                fingerprint,
                DateTimeOffset.UtcNow);
            if (!localResult.IsValid)
            {
                localResult.ServerChecked = false;
                localResult.UsedOfflineFallback = false;
                return localResult;
            }

            LicenseTokenPayload tokenPayloadFromLocal;
            if (string.IsNullOrWhiteSpace(resolvedDeviceId) &&
                TryDeserializeTokenPayload(localResult.PayloadJson, out tokenPayloadFromLocal))
            {
                resolvedDeviceId = (tokenPayloadFromLocal.DeviceId ?? string.Empty).Trim();
            }

            if (!enableServerValidation)
            {
                localResult.ServerChecked = false;
                localResult.UsedOfflineFallback = true;
                return localResult;
            }

            DeviceLicenseValidateRequest request = new DeviceLicenseValidateRequest
            {
                ProductId = LicenseHubAuthPolicy.ProductId,
                DeviceFingerprint = fingerprint,
                LicenseToken = token,
                DeviceId = resolvedDeviceId
            };

            try
            {
                using (CancellationTokenSource timeoutCts = new CancellationTokenSource(3000))
                using (HttpClient httpClient = new HttpClient())
                using (HttpRequestMessage httpRequest = new HttpRequestMessage(HttpMethod.Post, apiBase + "/api/device/license/validate"))
                {
                    httpRequest.Content = new StringContent(JsonSerializer.Serialize(request, JsonOptions), Encoding.UTF8, "application/json");
                    using (HttpResponseMessage httpResponse = httpClient.SendAsync(httpRequest, timeoutCts.Token).GetAwaiter().GetResult())
                    {
                        string raw = httpResponse.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                        if (!httpResponse.IsSuccessStatusCode)
                        {
                            return new ValidationResult
                            {
                                IsValid = false,
                                Reason = ReadApiError(raw, "서버 라이선스 검증 요청이 실패했습니다."),
                                PayloadJson = localResult.PayloadJson,
                                ExpiresAt = localResult.ExpiresAt,
                                ServerChecked = true,
                                UsedOfflineFallback = false,
                                ServerStatus = "http_" + (int)httpResponse.StatusCode
                            };
                        }

                        DeviceLicenseValidateResponse server = JsonSerializer.Deserialize<DeviceLicenseValidateResponse>(raw, JsonOptions);
                        if (server == null)
                        {
                            return new ValidationResult
                            {
                                IsValid = false,
                                Reason = "서버 응답 형식이 올바르지 않습니다.",
                                PayloadJson = localResult.PayloadJson,
                                ExpiresAt = localResult.ExpiresAt,
                                ServerChecked = true,
                                UsedOfflineFallback = false,
                                ServerStatus = "invalid_response"
                            };
                        }

                        localResult.ServerChecked = true;
                        localResult.ServerStatus = server.Status ?? string.Empty;
                        localResult.UsedOfflineFallback = false;

                        if (server.IsValid)
                        {
                            if (server.ExpiresAt.HasValue)
                            {
                                localResult.ExpiresAt = server.ExpiresAt;
                            }

                            string serverDeviceId = string.IsNullOrWhiteSpace(server.DeviceId)
                                ? resolvedDeviceId
                                : server.DeviceId.Trim();
                            if (!string.IsNullOrWhiteSpace(licensePath))
                            {
                                LicenseHubLocalLicenseStore.Write(licensePath, new LicenseHubLocalLicenseFile
                                {
                                    ProductId = LicenseHubAuthPolicy.ProductId,
                                    DeviceFingerprint = fingerprint,
                                    DeviceId = serverDeviceId,
                                    LicenseToken = token,
                                    SavedAt = DateTimeOffset.UtcNow
                                });
                            }

                            return localResult;
                        }

                        bool discarded = false;
                        if (discardLocalLicenseWhenServerInvalid &&
                            server.ShouldDiscardLocal &&
                            !string.IsNullOrWhiteSpace(licensePath))
                        {
                            LicenseHubLocalLicenseStore.Delete(licensePath);
                            discarded = true;
                        }

                        return new ValidationResult
                        {
                            IsValid = false,
                            Reason = string.IsNullOrWhiteSpace(server.Reason) ? "서버 라이선스 검증 실패" : server.Reason,
                            PayloadJson = localResult.PayloadJson,
                            ExpiresAt = server.ExpiresAt,
                            ServerChecked = true,
                            UsedOfflineFallback = false,
                            LocalLicenseDiscarded = discarded,
                            ServerStatus = server.Status ?? string.Empty
                        };
                    }
                }
            }
            catch (OperationCanceledException)
            {
                localResult.ServerChecked = false;
                localResult.UsedOfflineFallback = true;
                return localResult;
            }
            catch (HttpRequestException)
            {
                localResult.ServerChecked = false;
                localResult.UsedOfflineFallback = true;
                return localResult;
            }
        }

        private static BootstrapValidationResult TryBootstrap(
            int productId,
            string fingerprint,
            string apiBase,
            string licensePath)
        {
            try
            {
                using (CancellationTokenSource timeoutCts = new CancellationTokenSource(3000))
                using (HttpClient httpClient = new HttpClient())
                using (HttpRequestMessage bootstrapRequest = new HttpRequestMessage(HttpMethod.Post, apiBase + "/api/device/license/bootstrap"))
                {
                    bootstrapRequest.Content = new StringContent(
                        JsonSerializer.Serialize(new DeviceLicenseBootstrapRequest
                        {
                            ProductId = productId,
                            DeviceFingerprint = fingerprint
                        }, JsonOptions),
                        Encoding.UTF8,
                        "application/json");

                    using (HttpResponseMessage bootstrapResponse = httpClient.SendAsync(bootstrapRequest, timeoutCts.Token).GetAwaiter().GetResult())
                    {
                        string bootstrapRaw = bootstrapResponse.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                        if (!bootstrapResponse.IsSuccessStatusCode)
                        {
                            return BootstrapValidationResult.Failed(new ValidationResult
                            {
                                IsValid = false,
                                Reason = ReadApiError(bootstrapRaw, "활성 라이선스 토큰 조회에 실패했습니다."),
                                ServerChecked = true,
                                UsedOfflineFallback = false,
                                ServerStatus = "http_" + (int)bootstrapResponse.StatusCode
                            });
                        }

                        DeviceLicenseBootstrapResponse bootstrap = JsonSerializer.Deserialize<DeviceLicenseBootstrapResponse>(bootstrapRaw, JsonOptions);
                        if (bootstrap == null || !bootstrap.IsIssued || string.IsNullOrWhiteSpace(bootstrap.LicenseToken))
                        {
                            return BootstrapValidationResult.Failed(new ValidationResult
                            {
                                IsValid = false,
                                Reason = bootstrap == null || string.IsNullOrWhiteSpace(bootstrap.Reason)
                                    ? "활성 라이선스 토큰을 발급받지 못했습니다."
                                    : bootstrap.Reason,
                                ServerChecked = true,
                                UsedOfflineFallback = false,
                                ServerStatus = bootstrap == null ? "bootstrap_failed" : bootstrap.Status ?? "bootstrap_failed"
                            });
                        }

                        string token = bootstrap.LicenseToken.Trim();
                        string resolvedDeviceId = (bootstrap.DeviceId ?? string.Empty).Trim();
                        string savePath = string.IsNullOrWhiteSpace(licensePath)
                            ? LicenseHubAuthPolicy.LicenseFilePath
                            : licensePath;
                        LicenseHubLocalLicenseStore.Write(savePath, new LicenseHubLocalLicenseFile
                        {
                            ProductId = productId,
                            DeviceFingerprint = fingerprint,
                            DeviceId = resolvedDeviceId,
                            LicenseToken = token,
                            SavedAt = DateTimeOffset.UtcNow
                        });
                        return BootstrapValidationResult.Success(token, savePath, resolvedDeviceId);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                return BootstrapValidationResult.Failed(new ValidationResult
                {
                    IsValid = false,
                    Reason = "서버 토큰 조회 시간이 초과되었습니다.",
                    ServerChecked = false,
                    UsedOfflineFallback = true
                });
            }
            catch (HttpRequestException)
            {
                return BootstrapValidationResult.Failed(new ValidationResult
                {
                    IsValid = false,
                    Reason = "서버 연결이 불가능하여 라이선스 토큰을 복구하지 못했습니다.",
                    ServerChecked = false,
                    UsedOfflineFallback = true
                });
            }
        }

        private static bool TryDeserializeTokenPayload(string payloadJson, out LicenseTokenPayload payload)
        {
            payload = null;
            if (string.IsNullOrWhiteSpace(payloadJson))
            {
                return false;
            }

            try
            {
                payload = JsonSerializer.Deserialize<LicenseTokenPayload>(payloadJson, JsonOptions);
                return payload != null;
            }
            catch
            {
                payload = null;
                return false;
            }
        }

        private static string NormalizeApiBase(string apiBaseUrl)
        {
            string text = (apiBaseUrl ?? string.Empty).Trim();
            return string.IsNullOrWhiteSpace(text)
                ? LicenseHubAuthPolicy.ApiBaseUrl
                : text.TrimEnd('/');
        }

        private static string ReadApiError(string raw, string fallback)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return fallback;
            }

            try
            {
                using (JsonDocument doc = JsonDocument.Parse(raw))
                {
                    JsonElement root = doc.RootElement;
                    if (root.ValueKind == JsonValueKind.Object)
                    {
                        JsonElement messageProp;
                        if (root.TryGetProperty("message", out messageProp) &&
                            messageProp.ValueKind == JsonValueKind.String)
                        {
                            string message = (messageProp.GetString() ?? string.Empty).Trim();
                            if (!string.IsNullOrWhiteSpace(message))
                            {
                                return message;
                            }
                        }

                        JsonElement errorProp;
                        if (root.TryGetProperty("error", out errorProp) &&
                            errorProp.ValueKind == JsonValueKind.String)
                        {
                            string error = (errorProp.GetString() ?? string.Empty).Trim();
                            if (!string.IsNullOrWhiteSpace(error))
                            {
                                return error;
                            }
                        }
                    }
                }
            }
            catch
            {
            }

            string text = raw.Trim();
            return string.IsNullOrWhiteSpace(text) ? fallback : text;
        }

        private sealed class BootstrapValidationResult
        {
            public ValidationResult Result { get; private set; }
            public string Token { get; private set; }
            public string LicensePath { get; private set; }
            public string DeviceId { get; private set; }

            public BootstrapValidationResult()
            {
                Result = new ValidationResult();
                Token = string.Empty;
                LicensePath = string.Empty;
                DeviceId = string.Empty;
            }

            public static BootstrapValidationResult Success(string token, string licensePath, string deviceId)
            {
                return new BootstrapValidationResult
                {
                    Result = new ValidationResult { IsValid = true },
                    Token = token ?? string.Empty,
                    LicensePath = licensePath ?? string.Empty,
                    DeviceId = deviceId ?? string.Empty
                };
            }

            public static BootstrapValidationResult Failed(ValidationResult result)
            {
                return new BootstrapValidationResult
                {
                    Result = result ?? new ValidationResult { IsValid = false },
                    Token = string.Empty,
                    LicensePath = string.Empty,
                    DeviceId = string.Empty
                };
            }
        }
    }

    internal static class LicenseHubStandaloneTokenValidator
    {
        public static ValidationResult Validate(
            string token,
            string publicKeyPem,
            int expectedProductId,
            string expectedFingerprint,
            DateTimeOffset now)
        {
            ValidationResult result = new ValidationResult
            {
                IsValid = false,
                Reason = "알 수 없는 오류"
            };

            if (string.IsNullOrWhiteSpace(token))
            {
                result.Reason = "토큰이 비어 있습니다.";
                return result;
            }

            string[] parts = token.Split(new[] { '.' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 2)
            {
                result.Reason = "토큰 형식 불일치";
                return result;
            }

            byte[] payloadBytes;
            byte[] signatureBytes;
            try
            {
                payloadBytes = Base64UrlDecode(parts[0]);
                signatureBytes = Base64UrlDecode(parts[1]);
                result.PayloadJson = Encoding.UTF8.GetString(payloadBytes);
            }
            catch
            {
                result.Reason = "토큰 디코딩 실패";
                return result;
            }

            if (!VerifyEcdsaSignature(payloadBytes, signatureBytes, publicKeyPem))
            {
                result.Reason = "서명 검증 실패";
                return result;
            }

            LicenseTokenPayload payload;
            try
            {
                payload = JsonSerializer.Deserialize<LicenseTokenPayload>(
                    Encoding.UTF8.GetString(payloadBytes),
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch
            {
                payload = null;
            }

            if (payload == null)
            {
                result.Reason = "토큰 payload 파싱 실패";
                return result;
            }

            if (!string.Equals(payload.Status, "active", StringComparison.OrdinalIgnoreCase))
            {
                result.Reason = "비활성 라이선스";
                return result;
            }

            if (payload.ProductId != expectedProductId)
            {
                result.Reason = "productId 불일치";
                return result;
            }

            if (!string.Equals(payload.DeviceFingerprint, expectedFingerprint, StringComparison.OrdinalIgnoreCase))
            {
                result.Reason = "디바이스 지문 불일치";
                return result;
            }

            if (payload.ExpiresAt <= now.ToUnixTimeSeconds())
            {
                result.Reason = "라이선스 만료";
                return result;
            }

            result.IsValid = true;
            result.Reason = string.Empty;
            result.ExpiresAt = DateTimeOffset.FromUnixTimeSeconds(payload.ExpiresAt);
            return result;
        }

        private static byte[] Base64UrlDecode(string value)
        {
            string normalized = (value ?? string.Empty).Trim().Replace('-', '+').Replace('_', '/');
            if (normalized.Length % 4 == 2)
            {
                normalized += "==";
            }
            else if (normalized.Length % 4 == 3)
            {
                normalized += "=";
            }
            else if (normalized.Length % 4 != 0)
            {
                throw new FormatException("잘못된 base64url 형식입니다.");
            }

            return Convert.FromBase64String(normalized);
        }

        private static bool VerifyEcdsaSignature(byte[] payloadBytes, byte[] signatureDerBytes, string publicKeyPem)
        {
            if (payloadBytes == null || payloadBytes.Length == 0 || signatureDerBytes == null || signatureDerBytes.Length == 0)
            {
                return false;
            }

            try
            {
                ECPublicKeyParameters publicKey = ReadPublicKey(publicKeyPem);
                if (publicKey == null)
                {
                    return false;
                }

                Asn1Sequence sequence = Asn1Object.FromByteArray(signatureDerBytes) as Asn1Sequence;
                if (sequence == null || sequence.Count != 2)
                {
                    return false;
                }

                DerInteger rInt = sequence[0] as DerInteger;
                DerInteger sInt = sequence[1] as DerInteger;
                if (rInt == null || sInt == null)
                {
                    return false;
                }

                BigInteger r = rInt.PositiveValue;
                BigInteger s = sInt.PositiveValue;

                byte[] digest;
                using (SHA256 sha = SHA256.Create())
                {
                    digest = sha.ComputeHash(payloadBytes);
                }

                ECDsaSigner signer = new ECDsaSigner();
                signer.Init(false, publicKey);
                return signer.VerifySignature(digest, r, s);
            }
            catch
            {
                return false;
            }
        }

        private static ECPublicKeyParameters ReadPublicKey(string pem)
        {
            if (string.IsNullOrWhiteSpace(pem))
            {
                return null;
            }

            using (StringReader reader = new StringReader(pem))
            {
                PemReader pemReader = new PemReader(reader);
                object keyObject = pemReader.ReadObject();

                ECPublicKeyParameters ecPublic = keyObject as ECPublicKeyParameters;
                if (ecPublic != null)
                {
                    return ecPublic;
                }

                Org.BouncyCastle.Crypto.AsymmetricKeyParameter asymmetric =
                    keyObject as Org.BouncyCastle.Crypto.AsymmetricKeyParameter;
                return asymmetric as ECPublicKeyParameters;
            }
        }
    }

    public sealed class LicenseHubDeviceFingerprintInfo
    {
        public string Fingerprint { get; set; }
        public string Source { get; set; }

        public LicenseHubDeviceFingerprintInfo()
        {
            Fingerprint = string.Empty;
            Source = string.Empty;
        }
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
}
