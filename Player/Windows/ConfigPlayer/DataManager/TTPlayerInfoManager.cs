using AndoW.LiteDb;
using LiteDB;
using Newtonsoft.Json;

namespace ConfigPlayer
{
    public class TTPlayerInfoManager
    {
        private readonly TTPlayerRepository repository = new TTPlayerRepository();
        private static bool mapperConfigured = false;
        public TTPlayerInfoClass g_PlayerInfo = new TTPlayerInfoClass();

        public TTPlayerInfoManager()
        {
            ConfigureMapper();
            Load();
        }

        private void ConfigureMapper()
        {
            if (mapperConfigured)
            {
                return;
            }

            BsonMapper.Global.Entity<TTPlayerInfoClass>().Id(x => x.Id, false);
            mapperConfigured = true;
        }

        public void Load()
        {
            TTPlayerInfoClass stored = repository.FindOne(_ => true);
            if (stored == null)
            {
                NewPlayerInfo();
                return;
            }

            g_PlayerInfo = stored;
        }

        public void NewPlayerInfo()
        {
            g_PlayerInfo = new TTPlayerInfoClass();
            SaveData();
        }

        public void SaveData()
        {
            g_PlayerInfo.Id = 0;
            repository.Upsert(g_PlayerInfo);
        }

        private class TTPlayerRepository : LiteDbRepository<TTPlayerInfoClass>
        {
            public TTPlayerRepository()
                : base("TTPlayerInfoManager", "Id")
            {
            }
        }
    }

    public class TTPlayerInfoClass
    {
        [BsonId]
        [BsonField("id")]
        [JsonProperty("id")]
        public int Id { get; set; } = 0; // 한개의 데이터만 저장하기 위한 필드

        public string TTInfo_Data1 { get; set; } = "NO";
        public string TTInfo_DAta2 { get; set; } = "YES";
        public string TTInfo_Data3 { get; set; } = "NO";
        public string TTInfo_DAta4 { get; set; } = "NO";
        public string TTInfo_Data5 { get; set; } = string.Empty;
        public string TTInfo_DAta6 { get; set; } = "0";
        public string TTInfo_Data7 { get; set; } = "0";
        public string TTInfo_DAta8 { get; set; } = "160";
        public string TTInfo_Data9 { get; set; } = "90";

        public void Clone(TTPlayerInfoClass paramCls)
        {
            TTInfo_Data1 = paramCls.TTInfo_Data1;
            TTInfo_DAta2 = paramCls.TTInfo_DAta2;
            TTInfo_Data3 = paramCls.TTInfo_Data3;
            TTInfo_DAta4 = paramCls.TTInfo_DAta4;
            TTInfo_Data5 = paramCls.TTInfo_Data5;
            TTInfo_DAta6 = paramCls.TTInfo_DAta6;
            TTInfo_Data7 = paramCls.TTInfo_Data7;
            TTInfo_DAta8 = paramCls.TTInfo_DAta8;
            TTInfo_Data9 = paramCls.TTInfo_Data9;
        }

        public void Clear()
        {
            TTInfo_Data1 = "NO";
            TTInfo_DAta2 = "YES";
            TTInfo_Data3 = "NO";
            TTInfo_DAta4 = "NO";
            TTInfo_Data5 = string.Empty;
            TTInfo_DAta6 = "0";
            TTInfo_Data7 = "0";
            TTInfo_DAta8 = "160";
            TTInfo_Data9 = "90";
        }
    }
}
