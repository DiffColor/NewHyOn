using LicenseHub.DeviceAuth.Core;
using NewHyOn.Shared.Auth;

namespace NewHyOnPlayer.Services
{
    internal static class LicenseHubAuthService
    {
        public static ValidationResult Validate()
        {
            PlayerInfoManager manager = new PlayerInfoManager();
            if (ClearLegacyAuthData(manager))
            {
                return new ValidationResult
                {
                    IsValid = false,
                    Reason = "구버전 인증 데이터가 삭제되었습니다. LicenseHub 인증이 필요합니다."
                };
            }

            LicenseHubLocalLicenseFile license = LicenseHubLocalLicenseStore.Read();
            string storedFingerprint = manager.g_PlayerInfo?.PIF_MacAddress?.Trim() ?? string.Empty;
            ValidationResult validation = LicenseHubLocalValidator.ValidateForStoredFingerprint(license, storedFingerprint);
            if (validation.IsValid)
            {
                PersistLicenseHubAuth(manager, validation);
                return validation;
            }

            ValidationResult offlineValidation;
            if (TryValidateStoredOfflineMarker(manager, validation, out offlineValidation))
            {
                return offlineValidation;
            }

            ClearStoredAuthKey(manager);

            return validation;
        }

        private static bool ClearLegacyAuthData(PlayerInfoManager manager)
        {
            if (manager?.g_PlayerInfo == null)
            {
                return false;
            }

            string authKey = manager.g_PlayerInfo.PIF_AuthKey?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(authKey) || LicenseHubAuthMarker.IsValidationMarker(authKey))
            {
                return false;
            }

            manager.g_PlayerInfo.PIF_AuthKey = string.Empty;
            manager.SaveData();
            return true;
        }

        private static void ClearStoredAuthKey(PlayerInfoManager manager)
        {
            if (manager?.g_PlayerInfo == null || string.IsNullOrWhiteSpace(manager.g_PlayerInfo.PIF_AuthKey))
            {
                return;
            }

            manager.g_PlayerInfo.PIF_AuthKey = string.Empty;
            manager.SaveData();
        }

        private static bool TryValidateStoredOfflineMarker(
            PlayerInfoManager manager,
            ValidationResult coreValidation,
            out ValidationResult validation)
        {
            validation = null;
            if (manager?.g_PlayerInfo == null)
            {
                return false;
            }

            string authKey = manager.g_PlayerInfo.PIF_AuthKey?.Trim() ?? string.Empty;
            string storedFingerprint = manager.g_PlayerInfo.PIF_MacAddress?.Trim() ?? string.Empty;
            return LicenseHubOfflineMarkerValidator.TryValidateForCurrentDevice(
                coreValidation,
                authKey,
                storedFingerprint,
                out validation);
        }

        private static void PersistLicenseHubAuth(
            PlayerInfoManager manager,
            ValidationResult validation)
        {
            if (manager?.g_PlayerInfo == null || validation == null || !validation.IsValid)
            {
                return;
            }

            bool changed = false;
            LicenseHubLocalLicenseFile license = LicenseHubLocalLicenseStore.Read();
            string authMarker = LicenseHubAuthMarker.Build(license);
            if (string.IsNullOrWhiteSpace(authMarker))
            {
                authMarker = LicenseHubAuthMarker.Build(validation);
            }

            if (!string.IsNullOrWhiteSpace(authMarker) &&
                !string.Equals(manager.g_PlayerInfo.PIF_AuthKey ?? string.Empty, authMarker, System.StringComparison.Ordinal))
            {
                manager.g_PlayerInfo.PIF_AuthKey = authMarker;
                changed = true;
            }

            string fingerprint = LicenseHubAuthMarker.ResolveDeviceFingerprint(license, validation);
            if (!string.IsNullOrWhiteSpace(fingerprint) &&
                !string.Equals(manager.g_PlayerInfo.PIF_MacAddress ?? string.Empty, fingerprint, System.StringComparison.OrdinalIgnoreCase))
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
