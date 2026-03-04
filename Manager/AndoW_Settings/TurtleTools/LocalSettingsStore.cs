using AndoWSettings;
using AndoWSettings.Properties;
using LiteDB;
using System;
using System.Configuration;

namespace TurtleTools
{
    internal static class LocalSettingsStore
    {
        private const string DefaultFtpRootPath = "/NewHyOnEnt";
        private const string ConnectionId = "singleton";
        private const string CollectionConnection = "local_connection";
        private const string CollectionFtp = "local_ftp";
        private const string CollectionUi = "local_ui";
        private static readonly object SyncRoot = new object();
        private static bool _seeded;

        public static void EnsureSeeded()
        {
            if (_seeded)
            {
                return;
            }

            lock (SyncRoot)
            {
                if (_seeded)
                {
                    return;
                }

                using (var db = LocalDbContext.OpenDatabase())
                {
                    var connCol = db.GetCollection<LocalConnectionSettings>(CollectionConnection);
                    var ftpCol = db.GetCollection<LocalFtpSettings>(CollectionFtp);
                    var uiCol = db.GetCollection<LocalUiSettings>(CollectionUi);

                    if (connCol.FindById(ConnectionId) == null)
                    {
                        connCol.Upsert(BuildConnectionSeed());
                    }

                    if (ftpCol.FindById(ConnectionId) == null)
                    {
                        ftpCol.Upsert(BuildFtpSeed());
                    }

                    if (uiCol.FindById(ConnectionId) == null)
                    {
                        uiCol.Upsert(BuildUiSeed());
                    }
                }

                _seeded = true;
            }
        }

        public static LocalConnectionSettings GetConnectionSettings()
        {
            EnsureSeeded();
            using (var db = LocalDbContext.OpenDatabase())
            {
                var collection = db.GetCollection<LocalConnectionSettings>(CollectionConnection);
                var settings = collection.FindById(ConnectionId) ?? BuildConnectionSeed();
                settings.RethinkDatabase = string.IsNullOrWhiteSpace(settings.RethinkDatabase) ? "NewHyOn" : settings.RethinkDatabase;
                collection.Upsert(settings);
                return settings;
            }
        }

        public static void SaveConnectionSettings(LocalConnectionSettings settings)
        {
            if (settings == null)
            {
                return;
            }

            settings.Id = ConnectionId;
            using (var db = LocalDbContext.OpenDatabase())
            {
                db.GetCollection<LocalConnectionSettings>(CollectionConnection).Upsert(settings);
            }
        }

        public static LocalFtpSettings GetFtpSettings()
        {
            EnsureSeeded();
            using (var db = LocalDbContext.OpenDatabase())
            {
                var collection = db.GetCollection<LocalFtpSettings>(CollectionFtp);
                var settings = collection.FindById(ConnectionId) ?? BuildFtpSeed();
                settings.RootPath = NormalizeRootPath(settings.RootPath);
                collection.Upsert(settings);
                return settings;
            }
        }

        public static void SaveFtpSettings(LocalFtpSettings settings)
        {
            if (settings == null)
            {
                return;
            }

            settings.Id = ConnectionId;
            settings.RootPath = NormalizeRootPath(settings.RootPath);
            using (var db = LocalDbContext.OpenDatabase())
            {
                db.GetCollection<LocalFtpSettings>(CollectionFtp).Upsert(settings);
            }
        }

        public static LocalUiSettings GetUiSettings()
        {
            EnsureSeeded();
            using (var db = LocalDbContext.OpenDatabase())
            {
                var collection = db.GetCollection<LocalUiSettings>(CollectionUi);
                var settings = collection.FindById(ConnectionId) ?? BuildUiSeed();
                collection.Upsert(settings);
                return settings;
            }
        }

        public static void SaveUiSettings(LocalUiSettings settings)
        {
            if (settings == null)
            {
                return;
            }

            settings.Id = ConnectionId;
            using (var db = LocalDbContext.OpenDatabase())
            {
                db.GetCollection<LocalUiSettings>(CollectionUi).Upsert(settings);
            }
        }

        private static LocalConnectionSettings BuildConnectionSeed()
        {
            string rethinkHost = ConfigurationManager.AppSettings["RethinkDbHost"];
            string rethinkPortRaw = ConfigurationManager.AppSettings["RethinkDbPort"];
            string rethinkDb = ConfigurationManager.AppSettings["RethinkDbDatabase"];
            string rethinkUser = ConfigurationManager.AppSettings["RethinkDbUser"];
            string rethinkPassword = ConfigurationManager.AppSettings["RethinkDbPassword"];
            string signalrHost = ConfigurationManager.AppSettings["SignalRHost"];
            string signalrPortRaw = ConfigurationManager.AppSettings["SignalRPort"];
            string signalrHubPath = ConfigurationManager.AppSettings["SignalRHubPath"];

            var serverSettings = TryLoadServerSettings();
            if (string.IsNullOrWhiteSpace(rethinkHost))
            {
                rethinkHost = serverSettings?.DataServerIp;
            }

            if (string.IsNullOrWhiteSpace(signalrHost))
            {
                signalrHost = serverSettings?.MessageServerIp;
            }

            return new LocalConnectionSettings
            {
                Id = ConnectionId,
                RethinkHost = string.IsNullOrWhiteSpace(rethinkHost) ? "127.0.0.1" : rethinkHost.Trim(),
                RethinkPort = ParseInt(rethinkPortRaw, 28015),
                RethinkDatabase = string.IsNullOrWhiteSpace(rethinkDb) ? "NewHyOn" : rethinkDb,
                RethinkUser = string.IsNullOrWhiteSpace(rethinkUser) ? "admin" : rethinkUser.Trim(),
                RethinkPassword = string.IsNullOrWhiteSpace(rethinkPassword) ? "turtle04!9" : rethinkPassword,
                SignalRHost = string.IsNullOrWhiteSpace(signalrHost) ? "127.0.0.1" : signalrHost.Trim(),
                SignalRPort = ParseInt(signalrPortRaw, 5000),
                SignalRHubPath = string.IsNullOrWhiteSpace(signalrHubPath) ? "/Data" : signalrHubPath.Trim()
            };
        }

        private static LocalFtpSettings BuildFtpSeed()
        {
            var serverSettings = TryLoadServerSettings();
            var connection = BuildConnectionSeed();
            return new LocalFtpSettings
            {
                Id = ConnectionId,
                Host = string.IsNullOrWhiteSpace(serverSettings?.DataServerIp) ? connection.RethinkHost : serverSettings.DataServerIp.Trim(),
                Port = serverSettings?.FTP_Port > 0 ? serverSettings.FTP_Port : NetworkTools.FTP_PORT,
                PasvMinPort = serverSettings?.FTP_PasvMinPort > 0 ? serverSettings.FTP_PasvMinPort : NetworkTools.FTP_PASV_MIN_PORT,
                PasvMaxPort = serverSettings?.FTP_PasvMaxPort > 0 ? serverSettings.FTP_PasvMaxPort : NetworkTools.FTP_PASV_MAX_PORT,
                User = "asdf",
                Password = "Emfndhk!",
                RootPath = NormalizeRootPath(serverSettings?.FTP_RootPath)
            };
        }

        private static LocalUiSettings BuildUiSeed()
        {
            var serverSettings = TryLoadServerSettings();
            if (serverSettings == null)
            {
                return new LocalUiSettings { Id = ConnectionId };
            }

            return new LocalUiSettings
            {
                Id = ConnectionId,
                PreserveAspectRatio = serverSettings.PreserveAspectRatio,
                DefaultResolutionOrientation = "Landscape",
                DefaultResolutionRows = 1,
                DefaultResolutionColumns = 1,
                DefaultResolutionWidthPixels = 1920,
                DefaultResolutionHeightPixels = 1080
            };
        }

        private static ServerSettings TryLoadServerSettings()
        {
            try
            {
                using (var db = LocalDbContext.OpenDatabase())
                {
                    var collection = db.GetCollection<ServerSettings>("ServerSettings");
                    var settings = collection.FindById(0) ?? collection.FindOne(Query.All());
                    return settings;
                }
            }
            catch (Exception ex)
            {
                Logger.WriteErrorLog($"local.db server settings read failed: {ex}", Logger.GetLogFileName());
                return null;
            }
        }

        private static int ParseInt(string raw, int fallback)
        {
            if (!string.IsNullOrWhiteSpace(raw) && int.TryParse(raw, out var value) && value > 0)
            {
                return value;
            }

            return fallback;
        }

        private static string NormalizeRootPath(string rootPath)
        {
            if (string.IsNullOrWhiteSpace(rootPath))
            {
                return DefaultFtpRootPath;
            }

            string normalized = rootPath.Replace("\\", "/").Trim();
            if (!normalized.StartsWith("/"))
            {
                normalized = "/" + normalized;
            }

            normalized = normalized.TrimEnd('/');
            return string.IsNullOrWhiteSpace(normalized) ? "/" : normalized;
        }
    }

    internal sealed class LocalConnectionSettings
    {
        [BsonId]
        public string Id { get; set; }
        public string RethinkHost { get; set; }
        public int RethinkPort { get; set; }
        public string RethinkDatabase { get; set; }
        public string RethinkUser { get; set; }
        public string RethinkPassword { get; set; }
        public string SignalRHost { get; set; }
        public int SignalRPort { get; set; }
        public string SignalRHubPath { get; set; }
    }

    internal sealed class LocalFtpSettings
    {
        [BsonId]
        public string Id { get; set; }
        public string Host { get; set; }
        public int Port { get; set; }
        public int PasvMinPort { get; set; }
        public int PasvMaxPort { get; set; }
        public string User { get; set; }
        public string Password { get; set; }
        public string RootPath { get; set; }
    }

    internal sealed class LocalUiSettings
    {
        [BsonId]
        public string Id { get; set; }
        public bool PreserveAspectRatio { get; set; }
        public string DefaultResolutionOrientation { get; set; }
        public int DefaultResolutionRows { get; set; }
        public int DefaultResolutionColumns { get; set; }
        public double DefaultResolutionWidthPixels { get; set; }
        public double DefaultResolutionHeightPixels { get; set; }
    }
}
