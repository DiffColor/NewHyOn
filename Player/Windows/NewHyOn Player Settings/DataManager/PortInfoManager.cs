using AndoW.LiteDb;
using LiteDB;
using Newtonsoft.Json;
using NewHyOn.Player.Settings.Services;
using System.Collections.Generic;

namespace NewHyOn.Player.Settings.DataManager;

public sealed class PortInfoManager
{
    private readonly PortInfoRepository repository = new();

    public List<PortInfoClass> DataList { get; } = new();

    public PortInfoManager()
    {
        LoadData();
    }

    public void LoadData()
    {
        DataList.Clear();
        List<PortInfoClass> stored = repository.LoadAll();
        if (stored.Count > 0)
        {
            DataList.AddRange(stored);
        }

        if (DataList.Count == 0)
        {
            DataList.Add(new PortInfoClass());
            SaveData();
            return;
        }

        bool needsSave = false;
        foreach (PortInfoClass item in DataList)
        {
            if (item.AIF_SYNC <= 0)
            {
                item.AIF_SYNC = LegacyNetworkService.SYNC_PORT;
                needsSave = true;
            }
        }

        if (needsSave)
        {
            SaveData();
        }
    }

    public void SaveData()
    {
        foreach (PortInfoClass item in DataList)
        {
            item.Id = 0;
        }

        repository.ReplaceAll(DataList);
    }

    private sealed class PortInfoRepository : LiteDbRepository<PortInfoClass>
    {
        public PortInfoRepository()
            : base("PortInfoManager", "Id")
        {
        }
    }
}

public sealed class PortInfoClass
{
    [BsonId]
    [BsonField("id")]
    [JsonProperty("id")]
    public int Id { get; set; }

    public int AIF_FTP { get; set; } = LegacyNetworkService.FTP_PORT;
    public int AIF_FTP_PasvMinPort { get; set; } = LegacyNetworkService.FTP_PASV_MIN_PORT;
    public int AIF_FTP_PasvMaxPort { get; set; } = LegacyNetworkService.FTP_PASV_MAX_PORT;
    public int AIF_SYNC { get; set; } = LegacyNetworkService.SYNC_PORT;
}
