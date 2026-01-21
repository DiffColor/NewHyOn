using AndoW.Shared;

using System.Collections.Generic;

namespace HyOnPlayer
{
    public class LocalSettingsManager
    {
        private readonly LocalPlayerSettingsRepository repository = new LocalPlayerSettingsRepository();
        public LocalPlayerSettings Settings { get; private set; } = new LocalPlayerSettings();

        public LocalSettingsManager()
        {
            Load();
        }

        public void Load()
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

        public void Save()
        {
            repository.Upsert(Settings);
        }
    }
}
