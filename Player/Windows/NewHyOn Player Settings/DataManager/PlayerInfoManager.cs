using AndoW.LiteDb;
using AndoW.Shared;
using System;
using System.Collections.Generic;
using System.Linq;

namespace NewHyOn.Player.Settings.DataManager;

public sealed class PlayerInfoManager
{
    private readonly PlayerInfoRepository repository = new();

    public PlayerInfoClass PlayerInfo { get; private set; } = new();

    public PlayerInfoManager()
    {
        LoadData();
    }

    public void LoadData()
    {
        List<PlayerInfoClass> storedList = repository.LoadAll() ?? new List<PlayerInfoClass>();
        if (storedList.Count == 0)
        {
            PlayerInfo = new PlayerInfoClass();
            SaveData();
            return;
        }

        PlayerInfo = SelectPreferredRecord(storedList);
        if (storedList.Count > 1)
        {
            PlayerInfo.Id = 0;
            repository.ReplaceAll(new[] { PlayerInfo });
        }
    }

    public void SaveData()
    {
        if (string.IsNullOrWhiteSpace(PlayerInfo.PIF_GUID))
        {
            PlayerInfo.PIF_GUID = Guid.NewGuid().ToString();
        }

        if (string.IsNullOrWhiteSpace(PlayerInfo.PIF_AuthKey))
        {
            PlayerInfo.PIF_AuthKey = Guid.NewGuid().ToString("N");
        }

        PlayerInfo.Id = 0;
        repository.ReplaceAll(new[] { PlayerInfo });
    }

    private static PlayerInfoClass SelectPreferredRecord(List<PlayerInfoClass> records)
    {
        return records
            .OrderByDescending(x => x.Id == 0)
            .ThenByDescending(x => !string.IsNullOrWhiteSpace(x.PIF_GUID))
            .ThenByDescending(x => !string.IsNullOrWhiteSpace(x.PIF_PlayerName))
            .First();
    }

    private sealed class PlayerInfoRepository : LiteDbRepository<PlayerInfoClass>
    {
        public PlayerInfoRepository()
            : base("PlayerInfoManager", "Id")
        {
        }
    }
}
