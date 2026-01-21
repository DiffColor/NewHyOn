using Newtonsoft.Json;
using TurtleTools;

namespace AndoWSettings
{
    public class ServerSettingsManager : RethinkDbManagerBase<ServerSettings>
    {
        public ServerSettings sData { get; private set; }

        public ServerSettingsManager()
            : base(RethinkDbConfigurator.GetSettingsDatabaseName(), nameof(ServerSettingsManager), "id")
        {
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
    }
}
