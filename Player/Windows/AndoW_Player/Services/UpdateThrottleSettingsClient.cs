using System;
using AndoW.Shared;
using RethinkDb.Driver;
using RethinkDb.Driver.Net;
using TurtleTools;

namespace HyOnPlayer
{
    internal sealed class UpdateThrottleSettingsClient : IDisposable
    {
        private const string DatabaseName = "AndoW";
        private const string TableName = "UpdateThrottleSettings";
        private const string DefaultId = "global";

        private static readonly RethinkDB R = RethinkDB.R;
        private readonly object syncRoot = new object();
        private readonly string host;
        private Connection connection;
        private UpdateThrottleSettings cached;
        private DateTime nextRefresh;

        public UpdateThrottleSettingsClient(string managerHost)
        {
            host = string.IsNullOrWhiteSpace(managerHost) ? "127.0.0.1" : managerHost;
        }

        public void Dispose()
        {
            ResetConnection();
        }

        public UpdateThrottleSettings GetSettings()
        {
            if (cached == null || DateTime.Now >= nextRefresh)
            {
                RefreshSettings();
            }

            return cached ?? BuildDefault();
        }

        public void RefreshSettings()
        {
            try
            {
                var conn = GetConnection();
                if (conn == null)
                {
                    cached = BuildDefault();
                }
                else
                {
                    cached = R.Db(DatabaseName)
                        .Table(TableName)
                        .Get(DefaultId)
                        .RunAtom<UpdateThrottleSettings>(conn) ?? BuildDefault();
                }
            }
            catch (Exception ex)
            {
                Logger.WriteErrorLog(ex.ToString(), Logger.GetLogFileName());
                ResetConnection();
                cached = BuildDefault();
            }

            int refreshSeconds = cached?.SettingsRefreshSeconds ?? 1800;
            if (refreshSeconds <= 0)
            {
                refreshSeconds = 1800;
            }
            nextRefresh = DateTime.Now.AddSeconds(refreshSeconds);
        }

        private UpdateThrottleSettings BuildDefault()
        {
            return new UpdateThrottleSettings
            {
                Id = DefaultId,
                MaxConcurrentDownloads = 8,
                RetryIntervalSeconds = 60,
                LeaseTtlSeconds = 3600,
                LeaseRenewIntervalSeconds = 30,
                SettingsRefreshSeconds = 1800,
                UpdatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
            };
        }

        private Connection GetConnection()
        {
            lock (syncRoot)
            {
                if (connection != null && connection.Open)
                {
                    return connection;
                }

                connection = R.Connection()
                    .Hostname(host)
                    .Port(28015)
                    .User("admin", "turtle04!9")
                    .Connect();

                return connection;
            }
        }

        private void ResetConnection()
        {
            lock (syncRoot)
            {
                if (connection != null)
                {
                    try
                    {
                        connection.Close(false);
                        connection.Dispose();
                    }
                    catch
                    {
                    }
                    connection = null;
                }
            }
        }
    }
}
