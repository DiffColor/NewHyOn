using Newtonsoft.Json;
using TurtleTools;

namespace AndoW_Manager
{
    public class ServerSettingsManager : RethinkDbManagerBase<ServerSettings>
    {
        public ServerSettings sData { get; private set; }

        public ServerSettingsManager()
            : base(RethinkDbConfigurator.GetSettingsDatabaseName(), nameof(ServerSettingsManager), "id")
        {
            LoadData();
        }

        public ServerSettings LoadData()
        {
            sData = FindOne(_ => true);

            if (sData == null)
            {
                sData = new ServerSettings();
                Upsert(sData);
            }

            return sData;
        }

        public void SaveData(ServerSettings settings)
        {
            sData = settings;
            Save();
        }

        private void Save()
        {
            if (sData == null)
                return;

            Upsert(sData);
        }
    }

    public class ServerSettings
    {
        [JsonProperty("id")]
        public int Id { get; set; } = 0; // 한개의 데이터만 저장하기 위한 필드
        public int FTP_Port { get; set; } = NetworkTools.FTP_PORT;
        public int FTP_PasvMinPort { get; set; } = NetworkTools.FTP_PASV_MIN_PORT;
        public int FTP_PasvMaxPort { get; set; } = NetworkTools.FTP_PASV_MAX_PORT;
        public bool PreserveAspectRatio { get; set; } = false;
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