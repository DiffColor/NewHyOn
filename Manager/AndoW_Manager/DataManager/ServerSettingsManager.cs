using LiteDB;
using Newtonsoft.Json;
using TurtleTools;

namespace AndoW_Manager
{
    public class ServerSettingsManager
    {
        public ServerSettings sData { get; private set; }

        public ServerSettingsManager()
        {
            LoadData();
        }

        public ServerSettings LoadData()
        {
            LocalSettingsStore.EnsureSeeded();
            var connection = LocalSettingsStore.GetConnectionSettings();
            var ftp = LocalSettingsStore.GetFtpSettings();
            var ui = LocalSettingsStore.GetUiSettings();

            sData = new ServerSettings();

            if (connection != null)
            {
                sData.DataServerIp = connection.RethinkHost;
                sData.MessageServerIp = connection.SignalRHost;
            }

            if (ftp != null)
            {
                sData.FTP_Port = ftp.Port > 0 ? ftp.Port : NetworkTools.FTP_PORT;
                sData.FTP_PasvMinPort = ftp.PasvMinPort > 0 ? ftp.PasvMinPort : NetworkTools.FTP_PASV_MIN_PORT;
                sData.FTP_PasvMaxPort = ftp.PasvMaxPort > 0 ? ftp.PasvMaxPort : NetworkTools.FTP_PASV_MAX_PORT;
                sData.FTP_RootPath = NormalizeFtpRootPath(ftp.RootPath);
            }

            if (ui != null)
            {
                sData.PreserveAspectRatio = ui.PreserveAspectRatio;
                sData.DefaultWelcomeFontFamily = ui.DefaultWelcomeFontFamily ?? sData.DefaultWelcomeFontFamily;
                sData.DefaultWelcomeFontSize = ui.DefaultWelcomeFontSize > 0 ? ui.DefaultWelcomeFontSize : sData.DefaultWelcomeFontSize;
                sData.DefaultWelcomeFontColor = ui.DefaultWelcomeFontColor ?? sData.DefaultWelcomeFontColor;
                sData.DefaultWelcomeBackgroundColor = ui.DefaultWelcomeBackgroundColor ?? sData.DefaultWelcomeBackgroundColor;
                sData.DefaultWelcomeFontColorIndex = ui.DefaultWelcomeFontColorIndex;
                sData.DefaultWelcomeBackgroundColorIndex = ui.DefaultWelcomeBackgroundColorIndex;
                sData.DefaultResolutionOrientation = ui.DefaultResolutionOrientation;
                sData.DefaultResolutionRows = ui.DefaultResolutionRows;
                sData.DefaultResolutionColumns = ui.DefaultResolutionColumns;
                sData.DefaultResolutionWidthPixels = ui.DefaultResolutionWidthPixels;
                sData.DefaultResolutionHeightPixels = ui.DefaultResolutionHeightPixels;
            }

            if (string.IsNullOrWhiteSpace(sData.DataServerIp))
            {
                sData.DataServerIp = "127.0.0.1";
            }

            if (string.IsNullOrWhiteSpace(sData.MessageServerIp))
            {
                sData.MessageServerIp = "127.0.0.1";
            }

            sData.FTP_RootPath = NormalizeFtpRootPath(sData.FTP_RootPath);

            return sData;
        }

        public void SaveData(ServerSettings settings)
        {
            if (settings == null)
            {
                return;
            }

            LocalSettingsStore.EnsureSeeded();
            var connection = LocalSettingsStore.GetConnectionSettings();
            var ftp = LocalSettingsStore.GetFtpSettings();
            var ui = LocalSettingsStore.GetUiSettings();

            if (connection != null)
            {
                if (!string.IsNullOrWhiteSpace(settings.DataServerIp))
                {
                    connection.RethinkHost = settings.DataServerIp.Trim();
                }

                if (!string.IsNullOrWhiteSpace(settings.MessageServerIp))
                {
                    connection.SignalRHost = settings.MessageServerIp.Trim();
                }

                LocalSettingsStore.SaveConnectionSettings(connection);
            }

            if (ftp != null)
            {
                ftp.Port = settings.FTP_Port;
                ftp.PasvMinPort = settings.FTP_PasvMinPort;
                ftp.PasvMaxPort = settings.FTP_PasvMaxPort;
                ftp.RootPath = NormalizeFtpRootPath(settings.FTP_RootPath);
                if (!string.IsNullOrWhiteSpace(settings.DataServerIp))
                {
                    ftp.Host = settings.DataServerIp.Trim();
                }

                LocalSettingsStore.SaveFtpSettings(ftp);
            }

            if (ui != null)
            {
                ui.PreserveAspectRatio = settings.PreserveAspectRatio;
                ui.DefaultWelcomeFontFamily = settings.DefaultWelcomeFontFamily;
                ui.DefaultWelcomeFontSize = settings.DefaultWelcomeFontSize;
                ui.DefaultWelcomeFontColor = settings.DefaultWelcomeFontColor;
                ui.DefaultWelcomeBackgroundColor = settings.DefaultWelcomeBackgroundColor;
                ui.DefaultWelcomeFontColorIndex = settings.DefaultWelcomeFontColorIndex;
                ui.DefaultWelcomeBackgroundColorIndex = settings.DefaultWelcomeBackgroundColorIndex;
                ui.DefaultResolutionOrientation = settings.DefaultResolutionOrientation;
                ui.DefaultResolutionRows = settings.DefaultResolutionRows;
                ui.DefaultResolutionColumns = settings.DefaultResolutionColumns;
                ui.DefaultResolutionWidthPixels = settings.DefaultResolutionWidthPixels;
                ui.DefaultResolutionHeightPixels = settings.DefaultResolutionHeightPixels;

                LocalSettingsStore.SaveUiSettings(ui);
            }

            sData = settings;
            sData.FTP_RootPath = NormalizeFtpRootPath(sData.FTP_RootPath);
        }

        private static string NormalizeFtpRootPath(string rootPath)
        {
            if (string.IsNullOrWhiteSpace(rootPath))
            {
                return "/NewHyOnEnt";
            }

            string normalized = rootPath.Replace("\\", "/").Trim();
            if (!normalized.StartsWith("/"))
            {
                normalized = "/" + normalized;
            }

            normalized = normalized.TrimEnd('/');
            return string.IsNullOrWhiteSpace(normalized) ? "/" : normalized;
        }
    }

    public class ServerSettings
    {
        [JsonProperty("id")]
        [BsonId(false)]
        public int Id { get; set; } = 0; // ?쒓컻???곗씠?곕쭔 ??ν븯湲??꾪븳 ?꾨뱶
        public int FTP_Port { get; set; } = NetworkTools.FTP_PORT;
        public int FTP_PasvMinPort { get; set; } = NetworkTools.FTP_PASV_MIN_PORT;
        public int FTP_PasvMaxPort { get; set; } = NetworkTools.FTP_PASV_MAX_PORT;
        public string FTP_RootPath { get; set; } = "/NewHyOnEnt";
        public bool PreserveAspectRatio { get; set; } = false;
        public string DataServerIp { get; set; } = "127.0.0.1";
        public string MessageServerIp { get; set; } = "127.0.0.1";
        public string DefaultWelcomeFontFamily { get; set; } = "Malgun Gothic";
        public double DefaultWelcomeFontSize { get; set; } = 76;
        public string DefaultWelcomeFontColor { get; set; } = "#FFCBCBCB";
        public string DefaultWelcomeBackgroundColor { get; set; } = "#FF000000";
        public int DefaultWelcomeFontColorIndex { get; set; } = 0;
        public int DefaultWelcomeBackgroundColorIndex { get; set; } = 7;
        public DeviceOrientation DefaultResolutionOrientation { get; set; } = DeviceOrientation.Landscape;
        public int DefaultResolutionRows { get; set; } = 1;
        public int DefaultResolutionColumns { get; set; } = 1;
        public double DefaultResolutionWidthPixels { get; set; } = 1920;
        public double DefaultResolutionHeightPixels { get; set; } = 1080;
    }
}
