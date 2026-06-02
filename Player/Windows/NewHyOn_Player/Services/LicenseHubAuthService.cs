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

            ClearStoredAuthIfNotLicenseHub(manager, authKey);
            return validation;
        }

        private static void ClearStoredAuthIfNotLicenseHub(PlayerInfoManager manager, string authKey)
        {
            if (manager?.g_PlayerInfo == null)
            {
                return;
            }

            string fingerprint = LicenseHubDeviceFingerprint.Generate().Fingerprint;
            bool changed = false;
            if (!string.IsNullOrWhiteSpace(authKey) && LicenseHubLocalLicenseStore.TryDeserialize(authKey) == null)
            {
                manager.g_PlayerInfo.PIF_AuthKey = string.Empty;
                changed = true;
            }

            if (!string.Equals(manager.g_PlayerInfo.PIF_MacAddress ?? string.Empty, fingerprint, System.StringComparison.OrdinalIgnoreCase))
            {
                manager.g_PlayerInfo.PIF_MacAddress = fingerprint;
                changed = true;
            }

            if (changed)
            {
                manager.SaveData();
            }
        }
    }
}
