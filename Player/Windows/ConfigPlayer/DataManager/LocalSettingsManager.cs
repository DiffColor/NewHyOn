using AndoW.LiteDb;
using AndoW.Shared;
using System.Collections.Generic;

namespace ConfigPlayer
{
    public class LocalSettingsManager
    {
        private readonly LocalPlayerSettingsRepository repository = new LocalPlayerSettingsRepository();
        public LocalPlayerSettings Settings { get; private set; } = new LocalPlayerSettings();

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

            if (Settings.SyncClientIps == null)
            {
                Settings.SyncClientIps = new List<string>();
            }
        }

        public void SaveData()
        {
            repository.Upsert(Settings);
        }

        private class LocalPlayerSettingsRepository : LiteDbRepository<LocalPlayerSettings>
        {
            public LocalPlayerSettingsRepository()
                : base("LocalPlayerSettings", "id")
            {
            }
        }
    }
}
