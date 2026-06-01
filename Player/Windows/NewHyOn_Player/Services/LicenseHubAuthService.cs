using LicenseHub.DeviceAuth.Core;
using NewHyOn.Shared.Auth;

namespace NewHyOnPlayer.Services
{
    internal static class LicenseHubAuthService
    {
        public static ValidationResult Validate()
        {
            ValidationResult validation = LicenseHubLocalValidator.Validate();
            if (validation.IsValid)
            {
                return validation;
            }

            PlayerInfoManager manager = new PlayerInfoManager();
            string authKey = manager.g_PlayerInfo?.PIF_AuthKey ?? string.Empty;
            LicenseHubLocalLicenseFile storedLicense = LicenseHubLocalLicenseStore.TryDeserialize(authKey);
            validation = LicenseHubLocalValidator.ValidateForCurrentDevice(storedLicense);
            if (validation.IsValid)
            {
                LicenseHubLocalLicenseStore.Write(storedLicense);
                return validation;
            }

            if (LegacyAuthKeyValidator.IsValidForCurrentDevice(authKey))
            {
                return new ValidationResult
                {
                    IsValid = true,
                    Reason = "구버전 인증키가 현재 장비와 일치합니다."
                };
            }

            return validation;
        }
    }
}
