using AndoW.LiteDb;
using AndoW.Shared;
using System.Collections.Generic;

namespace NewHyOn.Player.Settings.DataManager;

public sealed class LocalSettingsManager
{
    private readonly LocalPlayerSettingsRepository repository = new();

    public LocalPlayerSettings Settings { get; private set; } = new();

    public LocalSettingsManager()
    {
        LoadData();
    }

    public void LoadData()
    {
        var stored = repository.FindOne(_ => true);
        Settings = stored ?? new LocalPlayerSettings();
        if (stored == null)
        {
            repository.Upsert(Settings);
        }

        Settings.SyncClientIps ??= new List<string>();
    }

    public void SaveData()
    {
        repository.Upsert(Settings);
    }

    private sealed class LocalPlayerSettingsRepository : LiteDbRepository<LocalPlayerSettings>
    {
        public LocalPlayerSettingsRepository()
            : base("LocalPlayerSettings", "id")
        {
        }
    }
}
