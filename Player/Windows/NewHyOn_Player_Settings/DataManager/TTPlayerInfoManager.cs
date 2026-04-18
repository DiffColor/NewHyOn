using AndoW.LiteDb;
using LiteDB;
using Newtonsoft.Json;

namespace NewHyOn.Player.Settings.DataManager;

public sealed class TTPlayerInfoManager
{
    private readonly TTPlayerRepository repository = new();
    private static bool mapperConfigured;

    public TTPlayerInfoClass PlayerInfo { get; private set; } = new();

    public TTPlayerInfoManager()
    {
        ConfigureMapper();
        Load();
    }

    private static void ConfigureMapper()
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
        TTPlayerInfoClass? stored = repository.FindOne(_ => true);
        if (stored == null)
        {
            PlayerInfo = new TTPlayerInfoClass();
            SaveData();
            return;
        }

        PlayerInfo = stored;
    }

    public void SaveData()
    {
        PlayerInfo.Id = 0;
        repository.Upsert(PlayerInfo);
    }

    private sealed class TTPlayerRepository : LiteDbRepository<TTPlayerInfoClass>
    {
        public TTPlayerRepository()
            : base("TTPlayerInfoManager", "Id")
        {
        }
    }
}

public sealed class TTPlayerInfoClass
{
    [BsonId]
    [BsonField("id")]
    [JsonProperty("id")]
    public int Id { get; set; }

    public string TTInfo_Data1 { get; set; } = "NO";
    public string TTInfo_DAta2 { get; set; } = "NO";
    public string TTInfo_Data3 { get; set; } = "NO";
    public string TTInfo_DAta4 { get; set; } = "NO";
    public string TTInfo_Data5 { get; set; } = string.Empty;
    public string TTInfo_DAta6 { get; set; } = "0";
    public string TTInfo_Data7 { get; set; } = "0";
    public string TTInfo_DAta8 { get; set; } = "160";
    public string TTInfo_Data9 { get; set; } = "90";
}
