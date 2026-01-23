using System;
using System.Net;
using System.Net.Sockets;
using AndoW_Manager;

namespace TurtleTools
{
    public static class RethinkDbConfigurator
    {
        private const string LoopbackHost = "127.0.0.1";
        private static readonly string DefaultHost = GetLocalIpAddressOrDefault();
        private const int DefaultPort = 28015;
        private const string DefaultUser = "admin";
        private const string DefaultPassword = "turtle04!9";
        private const string DefaultDatabaseName = "NewHyOn";
        private static string _databaseName = DefaultDatabaseName;
        private static bool _configured;
        private static readonly object SyncRoot = new object();

        public static string GetSettingsDatabaseName()
        {
            EnsureConfigured();
            return _databaseName;
        }

        public static string GetDataDatabaseName()
        {
            EnsureConfigured();
            return _databaseName;
        }

        public static void EnsureConfigured()
        {
            if (_configured)
            {
                return;
            }

            lock (SyncRoot)
            {
                if (_configured)
                {
                    return;
                }

                var settings = LocalSettingsStore.GetConnectionSettings();
                _databaseName = string.IsNullOrWhiteSpace(settings?.RethinkDatabase)
                    ? DefaultDatabaseName
                    : settings.RethinkDatabase.Trim();

                int port = settings?.RethinkPort > 0 ? settings.RethinkPort : DefaultPort;
                string configuredHost = string.IsNullOrWhiteSpace(settings?.RethinkHost) ? DefaultHost : settings.RethinkHost.Trim();
                string configuredUser = string.IsNullOrWhiteSpace(settings?.RethinkUser) ? DefaultUser : settings.RethinkUser.Trim();
                string configuredPassword = string.IsNullOrWhiteSpace(settings?.RethinkPassword) ? DefaultPassword : settings.RethinkPassword;

                RethinkDbContext.Configure(configuredHost, port, configuredUser, configuredPassword);
                _configured = true;
            }
        }

        private static string GetLocalIpAddressOrDefault()
        {
            try
            {
                var hostEntry = Dns.GetHostEntry(Dns.GetHostName());
                foreach (var ipAddress in hostEntry.AddressList)
                {
                    if (ipAddress.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(ipAddress))
                    {
                        return ipAddress.ToString();
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.WriteErrorLog($"Local IP lookup failed: {ex.Message}", Logger.GetLogFileName());
            }

            return LoopbackHost;
        }
    }
}
