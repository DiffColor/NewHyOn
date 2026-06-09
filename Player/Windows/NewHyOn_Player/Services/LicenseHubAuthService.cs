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
            ValidationResult validation = LicenseHubLocalValidator.ValidateForCurrentDevice(license);
            if (validation.IsValid)
            {
                PersistLicenseHubAuth(manager, license, validation);
                return validation;
            }

            if (IsStoredAuthMarkerForCurrentDevice(manager))
            {
                return new ValidationResult
                {
                    IsValid = true,
                    Reason = "저장된 오프라인 인증 정보가 유효합니다."
                };
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

        private static bool IsStoredAuthMarkerForCurrentDevice(PlayerInfoManager manager)
        {
            if (manager?.g_PlayerInfo == null)
            {
                return false;
            }

            if (!LicenseHubAuthMarker.TryParse(manager.g_PlayerInfo.PIF_AuthKey ?? string.Empty, out LicenseHubValidationMarker marker))
            {
                return false;
            }

            string fingerprint = LicenseHubDeviceFingerprint.Generate().Fingerprint;
            return marker.ProductId == LicenseHubAuthPolicy.ProductId &&
                string.Equals(manager.g_PlayerInfo.PIF_MacAddress ?? string.Empty, fingerprint, System.StringComparison.OrdinalIgnoreCase);
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

        private static void PersistLicenseHubAuth(
            PlayerInfoManager manager,
            LicenseHubLocalLicenseFile license,
            ValidationResult validation)
        {
            if (manager?.g_PlayerInfo == null || validation == null || !validation.IsValid)
            {
                return;
            }

            bool changed = false;
            string authMarker = LicenseHubAuthMarker.Build(license);
            if (string.IsNullOrWhiteSpace(authMarker))
            {
                ClearStoredAuthKey(manager);
                return;
            }

            if (!string.Equals(manager.g_PlayerInfo.PIF_AuthKey ?? string.Empty, authMarker, System.StringComparison.Ordinal))
            {
                manager.g_PlayerInfo.PIF_AuthKey = authMarker;
                changed = true;
            }

            string fingerprint = LicenseHubDeviceFingerprint.Generate().Fingerprint;
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
