using System;
using System.Configuration;

namespace TurtleTools
{
    public static class RethinkDbConfigurator
    {
        private const string DefaultHost = "127.0.0.1";
        private const int DefaultPort = 28015;
        private const string DefaultUser = "admin";
        private const string DefaultPassword = "turtle04!9";
        private const string DefaultDatabaseName = "AndoW";
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

                string host = ConfigurationManager.AppSettings["RethinkDbHost"];
                string portValue = ConfigurationManager.AppSettings["RethinkDbPort"];
                string databaseName = ConfigurationManager.AppSettings["RethinkDbDatabase"];
                string user = ConfigurationManager.AppSettings["RethinkDbUser"];
                string password = ConfigurationManager.AppSettings["RethinkDbPassword"];

                _databaseName = string.IsNullOrWhiteSpace(databaseName) ? DefaultDatabaseName : databaseName.Trim();

                int port = DefaultPort;
                if (!string.IsNullOrWhiteSpace(portValue) && int.TryParse(portValue, out var parsedPort) && parsedPort > 0)
                {
                    port = parsedPort;
                }

                string configuredHost = string.IsNullOrWhiteSpace(host) ? DefaultHost : host;
                string configuredUser = string.IsNullOrWhiteSpace(user) ? DefaultUser : user;
                string configuredPassword = string.IsNullOrWhiteSpace(password) ? DefaultPassword : password;
                RethinkDbContext.Configure(configuredHost, port, configuredUser, configuredPassword);

                _configured = true;
            }
        }
    }
}
