using System;
using AndoW.Shared;
using TurtleTools;

namespace AndoW_Manager
{
    public sealed class UpdateThrottleSettingsManager : RethinkDbManagerBase<UpdateThrottleSettings>
    {
        private const string TableName = "UpdateThrottleSettings";
        private const string DefaultId = "global";

        public UpdateThrottleSettingsManager()
            : base(RethinkDbConfigurator.GetDataDatabaseName(), TableName, "id")
        {
        }

        public UpdateThrottleSettings LoadSettings()
        {
            var settings = FindById(DefaultId) ?? CreateDefault();
            EnsureDefaults(settings);
            return settings;
        }

        public void SaveSettings(UpdateThrottleSettings settings)
        {
            if (settings == null)
            {
                return;
            }

            settings.Id = DefaultId;
            EnsureDefaults(settings);
            settings.UpdatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            Upsert(settings);
        }

        private UpdateThrottleSettings CreateDefault()
        {
            var settings = new UpdateThrottleSettings
            {
                Id = DefaultId
            };
            EnsureDefaults(settings);
            settings.UpdatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            Upsert(settings);
            return settings;
        }

        private void EnsureDefaults(UpdateThrottleSettings settings)
        {
            if (settings == null)
            {
                return;
            }

            if (settings.MaxConcurrentDownloads <= 0)
            {
                settings.MaxConcurrentDownloads = 8;
            }

            if (settings.RetryIntervalSeconds <= 0)
            {
                settings.RetryIntervalSeconds = 60;
            }

            if (settings.LeaseTtlSeconds <= 0)
            {
                settings.LeaseTtlSeconds = 3600;
            }

            if (settings.LeaseRenewIntervalSeconds <= 0)
            {
                settings.LeaseRenewIntervalSeconds = 30;
            }

            if (settings.SettingsRefreshSeconds <= 0)
            {
                settings.SettingsRefreshSeconds = 1800;
            }
        }
    }
}
