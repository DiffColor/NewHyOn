using System;
using System.IO;
using System.Threading.Tasks;
using FluentFTP;

namespace TurtleTools
{
    internal static class FtpTransferTools
    {
        private const int DefaultPort = 21;
        private const int DefaultTimeoutMs = 5000;
        private const string UpgradeApkRelativePath = "UpgradeAPK/AndoW_Player.apk";

        public static bool TryTestConnection(out string error)
        {
            error = null;

            if (!TryGetValidatedSettings(out var settings, out error))
            {
                return false;
            }

            try
            {
                using (var client = new FtpClient(settings.Host, settings.User, settings.Password, settings.Port))
                {
                    ApplyTimeouts(client.Config);
                    client.Connect();
                    client.Disconnect();
                }

                return true;
            }
            catch (Exception ex)
            {
                error = $"FTP 연결 실패: {ex.Message}";
                return false;
            }
        }

        public static Task<string> UploadUpgradeApkAsync(string localPath)
        {
            return UploadFileAsync(localPath, UpgradeApkRelativePath);
        }

        public static async Task<string> UploadFileAsync(string localPath, string remoteRelativePath)
        {
            if (string.IsNullOrWhiteSpace(localPath) || !File.Exists(localPath))
            {
                return "업로드할 파일이 존재하지 않습니다.";
            }

            if (!TryGetValidatedSettings(out var settings, out string error))
            {
                return error;
            }

            string remotePath = CombineRemotePath(settings.RootPath, remoteRelativePath);

            try
            {
                using (var client = new AsyncFtpClient(settings.Host, settings.User, settings.Password, settings.Port))
                {
                    ApplyTimeouts(client.Config);
                    client.Config.RetryAttempts = 2;
                    await client.Connect();
                    await client.UploadFile(localPath, remotePath, FtpRemoteExists.Overwrite, true, FtpVerify.Retry);
                    await client.Disconnect();
                }

                return null;
            }
            catch (Exception ex)
            {
                return $"FTP 업로드 실패: {ex.Message}";
            }
        }

        private static bool TryGetValidatedSettings(out LocalFtpSettings settings, out string error)
        {
            settings = LocalSettingsStore.GetFtpSettings();
            error = null;

            if (settings == null)
            {
                error = "FTP 설정을 불러올 수 없습니다.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(settings.Host))
            {
                error = "FTP 호스트를 설정해 주세요.";
                return false;
            }

            int port = settings.Port > 0 ? settings.Port : DefaultPort;
            if (port <= 0 || port > 65535)
            {
                error = "FTP 포트 설정이 올바르지 않습니다.";
                return false;
            }

            settings.Port = port;

            if (!string.IsNullOrWhiteSpace(settings.User) == false)
            {
                error = "FTP 계정을 설정해 주세요.";
                return false;
            }

            if (settings.PasvMinPort > 0 && settings.PasvMaxPort > 0 && settings.PasvMinPort > settings.PasvMaxPort)
            {
                error = "FTP 패시브 포트 범위가 올바르지 않습니다.";
                return false;
            }

            return true;
        }

        private static void ApplyTimeouts(FtpConfig config)
        {
            if (config == null)
            {
                return;
            }

            config.ConnectTimeout = DefaultTimeoutMs;
            config.ReadTimeout = DefaultTimeoutMs;
            config.DataConnectionConnectTimeout = DefaultTimeoutMs;
            config.DataConnectionReadTimeout = DefaultTimeoutMs;
        }

        private static string CombineRemotePath(string root, string relative)
        {
            string normalizedRoot = NormalizeRemotePath(root);
            if (!normalizedRoot.EndsWith("/"))
            {
                normalizedRoot += "/";
            }

            string normalizedRelative = NormalizeRemotePath(relative).TrimStart('/');
            if (string.IsNullOrWhiteSpace(normalizedRelative))
            {
                return normalizedRoot;
            }

            return normalizedRoot + normalizedRelative;
        }

        private static string NormalizeRemotePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return "/";
            }

            string normalized = path.Replace("\\", "/").Trim();
            if (!normalized.StartsWith("/"))
            {
                normalized = "/" + normalized;
            }

            return normalized;
        }
    }
}
