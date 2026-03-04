using System;
using System.Linq;
using Newtonsoft.Json;
using RethinkDb.Driver;
using RethinkDb.Driver.Net;
using TurtleTools;

namespace HyOnPlayer
{
    internal sealed class ServerSettingsClient : IDisposable
    {
        private const string DatabaseName = "NewHyOn";
        private const string TableName = "ServerSettings";
        private const int DefaultPort = 28015;
        private const string DefaultUser = "admin";
        private const string DefaultPassword = "turtle04!9";
        private static readonly RethinkDB R = RethinkDB.R;

        private readonly object syncRoot = new object();
        private string host;
        private Connection connection;
        private DateTime lastFetchAt = DateTime.MinValue;
        private ServerSettingsSnapshot cached;
        private readonly TimeSpan cacheDuration = TimeSpan.FromSeconds(10);

        public ServerSettingsClient(string rethinkHost)
        {
            UpdateHost(rethinkHost);
        }

        public void UpdateHost(string rethinkHost)
        {
            string next = string.IsNullOrWhiteSpace(rethinkHost) ? null : rethinkHost.Trim();
            lock (syncRoot)
            {
                if (string.Equals(host, next, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                host = next;
                cached = null;
                lastFetchAt = DateTime.MinValue;
                ResetConnection();
            }
        }

        public ServerSettingsSnapshot GetSettings(bool forceRefresh = false)
        {
            lock (syncRoot)
            {
                if (!forceRefresh && cached != null && DateTime.Now - lastFetchAt < cacheDuration)
                {
                    return cached;
                }
            }

            try
            {
                if (string.IsNullOrWhiteSpace(host))
                {
                    Logger.WriteErrorLog("ServerSettings fetch skipped: Rethink host is empty.", Logger.GetLogFileName());
                    return null;
                }

                var conn = GetConnection();
                if (conn == null)
                {
                    return null;
                }

                var table = R.Db(DatabaseName).Table(TableName);
                var settings = table.Get(0).RunAtom<ServerSettingsSnapshot>(conn);
                if (settings == null)
                {
                    settings = table.RunCursor<ServerSettingsSnapshot>(conn).FirstOrDefault();
                }

                if (settings != null)
                {
                    lock (syncRoot)
                    {
                        cached = settings;
                        lastFetchAt = DateTime.Now;
                    }

                    return settings;
                }
            }
            catch (Exception ex)
            {
                Logger.WriteErrorLog($"ServerSettings fetch failed: {ex}", Logger.GetLogFileName());
                ResetConnection();
            }

            return null;
        }

        private Connection GetConnection()
        {
            lock (syncRoot)
            {
                if (string.IsNullOrWhiteSpace(host))
                {
                    return null;
                }

                if (connection != null && connection.Open)
                {
                    return connection;
                }

                connection = R.Connection()
                    .Hostname(host)
                    .Port(DefaultPort)
                    .User(DefaultUser, DefaultPassword)
                    .Timeout(3000)
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

        public void Dispose()
        {
            ResetConnection();
        }
    }

    internal sealed class ServerSettingsSnapshot
    {
        [JsonProperty("id")]
        public int Id { get; set; } = 0;
        public int FTP_Port { get; set; }
        public int FTP_PasvMinPort { get; set; }
        public int FTP_PasvMaxPort { get; set; }
        public string FTP_RootPath { get; set; }
        public string DataServerIp { get; set; }
        public string MessageServerIp { get; set; }
    }
}
