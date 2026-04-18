using LiteDB;
using Newtonsoft.Json;
using NewHyOn.Settings.Wpf.Models;
using RethinkDb.Driver;
using RethinkDb.Driver.Net;
using System.IO;
using System.Linq;

namespace NewHyOn.Settings.Wpf.Services;

public sealed class SettingsRepository
{
    private const string DbPassword = "turtle04!9";
    private const string SingletonId = "singleton";
    private const string ConnectionCollection = "local_connection";
    private const string FtpCollection = "local_ftp";
    private const string UiCollection = "local_ui";
    private const string ServerSettingsCollection = "ServerSettings";
    private const string DefaultRootPath = "/NewHyOn";
    private static readonly object SyncRoot = new();
    private static readonly RethinkDB R = RethinkDB.R;
    private static bool _initialized;
    private static string _databasePath = string.Empty;
    private static string _encryptedConnectionString = string.Empty;
    private static string _plainConnectionString = string.Empty;

    public SettingsLoadResult LoadWithRemotePriority()
    {
        using var database = OpenDatabase();
        EnsureSeeded(database);

        var connectionCollection = database.GetCollection<LocalConnectionSettings>(ConnectionCollection);
        var ftpCollection = database.GetCollection<LocalFtpSettings>(FtpCollection);
        var uiCollection = database.GetCollection<LocalUiSettings>(UiCollection);
        var serverCollection = database.GetCollection<ServerSettings>(ServerSettingsCollection);

        var connection = connectionCollection.FindById(SingletonId) ?? BuildConnectionSeed(null);
        var ftp = ftpCollection.FindById(SingletonId) ?? BuildFtpSeed(null, connection);
        var ui = uiCollection.FindById(SingletonId) ?? BuildUiSeed(null);
        var localData = BuildFormData(connection, ftp, ui);

        if (TryLoadRemoteServerSettings(connection, out var remoteSettings, out var remoteErrorMessage) == false || remoteSettings is null)
        {
            return new SettingsLoadResult
            {
                Data = localData,
                LoadedFromRemote = false,
                UsedLocalFallback = true,
                RemoteErrorMessage = remoteErrorMessage
            };
        }

        SyncLocalDatabase(database, connectionCollection, ftpCollection, uiCollection, serverCollection, remoteSettings, connection, ftp, ui);

        return new SettingsLoadResult
        {
            Data = BuildFormData(remoteSettings),
            LoadedFromRemote = true,
            UsedLocalFallback = false
        };
    }

    public SettingsSaveResult SaveWithRemoteSync(SettingsFormData formData)
    {
        ArgumentNullException.ThrowIfNull(formData);

        using var database = OpenDatabase();
        EnsureSeeded(database);

        var connectionCollection = database.GetCollection<LocalConnectionSettings>(ConnectionCollection);
        var ftpCollection = database.GetCollection<LocalFtpSettings>(FtpCollection);
        var uiCollection = database.GetCollection<LocalUiSettings>(UiCollection);
        var serverCollection = database.GetCollection<ServerSettings>(ServerSettingsCollection);

        var connection = connectionCollection.FindById(SingletonId) ?? BuildConnectionSeed(null);
        var ftp = ftpCollection.FindById(SingletonId) ?? BuildFtpSeed(null, connection);
        var ui = uiCollection.FindById(SingletonId) ?? BuildUiSeed(null);
        var server = serverCollection.FindById(0) ?? serverCollection.FindOne(Query.All()) ?? new ServerSettings();

        ApplyFormData(formData, connection, ftp, ui, server);

        connectionCollection.Upsert(connection);
        ftpCollection.Upsert(ftp);
        uiCollection.Upsert(ui);
        serverCollection.Upsert(server);

        if (TryUpsertRethink(server, connection, out var remoteErrorMessage))
        {
            return new SettingsSaveResult
            {
                SavedToLocal = true,
                SavedToRemote = true,
                Message = "설정을 데이터서버와 로컬에 저장했습니다."
            };
        }

        return new SettingsSaveResult
        {
            SavedToLocal = true,
            SavedToRemote = false,
            Message = $"데이터서버 저장에 실패했습니다.{Environment.NewLine}설정은 로컬에만 저장했습니다.",
            RemoteErrorMessage = remoteErrorMessage
        };
    }

    private static void EnsureInitialized()
    {
        if (_initialized)
        {
            return;
        }

        lock (SyncRoot)
        {
            if (_initialized)
            {
                return;
            }

            var dataDirectory = Path.Combine(AppContext.BaseDirectory, "Data");
            Directory.CreateDirectory(dataDirectory);

            _databasePath = Path.Combine(dataDirectory, "local.db");
            _encryptedConnectionString = $"Filename={_databasePath};Connection=shared;Password={DbPassword}";
            _plainConnectionString = $"Filename={_databasePath};Connection=shared";
            _initialized = true;
        }
    }

    private static LiteDatabase OpenDatabase()
    {
        EnsureInitialized();

        try
        {
            return new LiteDatabase(_encryptedConnectionString);
        }
        catch (LiteException)
        {
            if (TryUpgradeToEncrypted() == false)
            {
                throw;
            }
        }

        return new LiteDatabase(_encryptedConnectionString);
    }

    private static bool TryUpgradeToEncrypted()
    {
        if (string.IsNullOrWhiteSpace(_databasePath) || File.Exists(_databasePath) == false)
        {
            return false;
        }

        try
        {
            using var database = new LiteDatabase(_plainConnectionString);
            database.Rebuild(new LiteDB.Engine.RebuildOptions { Password = DbPassword });
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static void EnsureSeeded(LiteDatabase database)
    {
        var connectionCollection = database.GetCollection<LocalConnectionSettings>(ConnectionCollection);
        var ftpCollection = database.GetCollection<LocalFtpSettings>(FtpCollection);
        var uiCollection = database.GetCollection<LocalUiSettings>(UiCollection);
        var serverCollection = database.GetCollection<ServerSettings>(ServerSettingsCollection);
        var server = serverCollection.FindById(0) ?? serverCollection.FindOne(Query.All());

        if (connectionCollection.FindById(SingletonId) is null)
        {
            connectionCollection.Upsert(BuildConnectionSeed(server));
        }

        if (ftpCollection.FindById(SingletonId) is null)
        {
            ftpCollection.Upsert(BuildFtpSeed(server, BuildConnectionSeed(server)));
        }

        if (uiCollection.FindById(SingletonId) is null)
        {
            uiCollection.Upsert(BuildUiSeed(server));
        }

        if (server is not null && server.Id != 0)
        {
            server.Id = 0;
            serverCollection.Upsert(server);
        }
    }

    private static SettingsFormData BuildFormData(LocalConnectionSettings connection, LocalFtpSettings ftp, LocalUiSettings ui)
    {
        return new SettingsFormData
        {
            DataServerIp = string.IsNullOrWhiteSpace(connection.RethinkHost) ? "127.0.0.1" : connection.RethinkHost.Trim(),
            MessageServerIp = string.IsNullOrWhiteSpace(connection.SignalRHost) ? "127.0.0.1" : connection.SignalRHost.Trim(),
            FtpPort = ftp.Port > 0 ? ftp.Port : NetworkDefaults.FtpPort,
            PasvMinPort = ftp.PasvMinPort > 0 ? ftp.PasvMinPort : NetworkDefaults.FtpPasvMinPort,
            PasvMaxPort = ftp.PasvMaxPort > 0 ? ftp.PasvMaxPort : NetworkDefaults.FtpPasvMaxPort,
            FtpRootPath = NormalizeRootPath(ftp.RootPath),
            PreserveAspectRatio = ui.PreserveAspectRatio
        };
    }

    private static SettingsFormData BuildFormData(ServerSettings server)
    {
        return new SettingsFormData
        {
            DataServerIp = string.IsNullOrWhiteSpace(server.DataServerIp) ? "127.0.0.1" : server.DataServerIp.Trim(),
            MessageServerIp = string.IsNullOrWhiteSpace(server.MessageServerIp) ? "127.0.0.1" : server.MessageServerIp.Trim(),
            FtpPort = server.FTP_Port > 0 ? server.FTP_Port : NetworkDefaults.FtpPort,
            PasvMinPort = server.FTP_PasvMinPort > 0 ? server.FTP_PasvMinPort : NetworkDefaults.FtpPasvMinPort,
            PasvMaxPort = server.FTP_PasvMaxPort > 0 ? server.FTP_PasvMaxPort : NetworkDefaults.FtpPasvMaxPort,
            FtpRootPath = NormalizeRootPath(server.FTP_RootPath),
            PreserveAspectRatio = server.PreserveAspectRatio
        };
    }

    private static void ApplyFormData(SettingsFormData formData, LocalConnectionSettings connection, LocalFtpSettings ftp, LocalUiSettings ui, ServerSettings server)
    {
        connection.Id = SingletonId;
        connection.RethinkHost = formData.DataServerIp.Trim();
        connection.SignalRHost = formData.MessageServerIp.Trim();

        ftp.Id = SingletonId;
        ftp.Host = formData.DataServerIp.Trim();
        ftp.Port = formData.FtpPort;
        ftp.PasvMinPort = formData.PasvMinPort;
        ftp.PasvMaxPort = formData.PasvMaxPort;
        ftp.RootPath = NormalizeRootPath(formData.FtpRootPath);

        ui.Id = SingletonId;
        ui.PreserveAspectRatio = formData.PreserveAspectRatio;
        ui.DefaultResolutionOrientation ??= "Landscape";
        ui.DefaultResolutionRows = ui.DefaultResolutionRows <= 0 ? 1 : ui.DefaultResolutionRows;
        ui.DefaultResolutionColumns = ui.DefaultResolutionColumns <= 0 ? 1 : ui.DefaultResolutionColumns;
        ui.DefaultResolutionWidthPixels = ui.DefaultResolutionWidthPixels <= 0 ? 1920 : ui.DefaultResolutionWidthPixels;
        ui.DefaultResolutionHeightPixels = ui.DefaultResolutionHeightPixels <= 0 ? 1080 : ui.DefaultResolutionHeightPixels;

        server.Id = 0;
        server.DataServerIp = connection.RethinkHost;
        server.MessageServerIp = connection.SignalRHost;
        server.FTP_Port = ftp.Port;
        server.FTP_PasvMinPort = ftp.PasvMinPort;
        server.FTP_PasvMaxPort = ftp.PasvMaxPort;
        server.FTP_RootPath = ftp.RootPath;
        server.PreserveAspectRatio = ui.PreserveAspectRatio;
    }

    private static void SyncLocalDatabase(
        LiteDatabase database,
        ILiteCollection<LocalConnectionSettings> connectionCollection,
        ILiteCollection<LocalFtpSettings> ftpCollection,
        ILiteCollection<LocalUiSettings> uiCollection,
        ILiteCollection<ServerSettings> serverCollection,
        ServerSettings remoteSettings,
        LocalConnectionSettings connection,
        LocalFtpSettings ftp,
        LocalUiSettings ui)
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(remoteSettings);

        remoteSettings.Id = 0;
        remoteSettings.DataServerIp = string.IsNullOrWhiteSpace(remoteSettings.DataServerIp) ? "127.0.0.1" : remoteSettings.DataServerIp.Trim();
        remoteSettings.MessageServerIp = string.IsNullOrWhiteSpace(remoteSettings.MessageServerIp) ? "127.0.0.1" : remoteSettings.MessageServerIp.Trim();
        remoteSettings.FTP_RootPath = NormalizeRootPath(remoteSettings.FTP_RootPath);

        connection.Id = SingletonId;
        connection.RethinkHost = remoteSettings.DataServerIp;
        connection.SignalRHost = remoteSettings.MessageServerIp;

        ftp.Id = SingletonId;
        ftp.Host = remoteSettings.DataServerIp;
        ftp.Port = remoteSettings.FTP_Port > 0 ? remoteSettings.FTP_Port : NetworkDefaults.FtpPort;
        ftp.PasvMinPort = remoteSettings.FTP_PasvMinPort > 0 ? remoteSettings.FTP_PasvMinPort : NetworkDefaults.FtpPasvMinPort;
        ftp.PasvMaxPort = remoteSettings.FTP_PasvMaxPort > 0 ? remoteSettings.FTP_PasvMaxPort : NetworkDefaults.FtpPasvMaxPort;
        ftp.RootPath = remoteSettings.FTP_RootPath;

        ui.Id = SingletonId;
        ui.PreserveAspectRatio = remoteSettings.PreserveAspectRatio;
        ui.DefaultResolutionOrientation ??= "Landscape";
        ui.DefaultResolutionRows = ui.DefaultResolutionRows <= 0 ? 1 : ui.DefaultResolutionRows;
        ui.DefaultResolutionColumns = ui.DefaultResolutionColumns <= 0 ? 1 : ui.DefaultResolutionColumns;
        ui.DefaultResolutionWidthPixels = ui.DefaultResolutionWidthPixels <= 0 ? 1920 : ui.DefaultResolutionWidthPixels;
        ui.DefaultResolutionHeightPixels = ui.DefaultResolutionHeightPixels <= 0 ? 1080 : ui.DefaultResolutionHeightPixels;

        connectionCollection.Upsert(connection);
        ftpCollection.Upsert(ftp);
        uiCollection.Upsert(ui);
        serverCollection.Upsert(remoteSettings);
    }

    private static bool TryLoadRemoteServerSettings(LocalConnectionSettings connectionSettings, out ServerSettings? serverSettings, out string? errorMessage)
    {
        serverSettings = null;
        errorMessage = null;

        if (string.IsNullOrWhiteSpace(connectionSettings.RethinkHost))
        {
            errorMessage = "저장된 데이터서버 정보가 없습니다.";
            return false;
        }

        try
        {
            using var connection = OpenRethinkConnection(connectionSettings);
            var databaseName = string.IsNullOrWhiteSpace(connectionSettings.RethinkDatabase) ? "NewHyOn" : connectionSettings.RethinkDatabase.Trim();

            var databases = R.DbList().RunAtom<List<string>>(connection) ?? [];
            if (databases.Contains(databaseName) == false)
            {
                errorMessage = "데이터서버에 설정 데이터베이스가 없습니다.";
                return false;
            }

            var tables = R.Db(databaseName).TableList().RunAtom<List<string>>(connection) ?? [];
            if (tables.Contains(ServerSettingsCollection) == false)
            {
                errorMessage = "데이터서버에 설정 테이블이 없습니다.";
                return false;
            }

            serverSettings = R.Db(databaseName).Table(ServerSettingsCollection).Get(0).RunAtom<ServerSettings>(connection);
            if (serverSettings is null)
            {
                using var cursor = R.Db(databaseName).Table(ServerSettingsCollection).Limit(1).RunCursor<ServerSettings>(connection);
                serverSettings = cursor.FirstOrDefault();
            }

            if (serverSettings is null)
            {
                errorMessage = "데이터서버에 저장된 설정이 없습니다.";
                return false;
            }

            serverSettings.Id = 0;
            serverSettings.DataServerIp = string.IsNullOrWhiteSpace(serverSettings.DataServerIp) ? "127.0.0.1" : serverSettings.DataServerIp.Trim();
            serverSettings.MessageServerIp = string.IsNullOrWhiteSpace(serverSettings.MessageServerIp) ? "127.0.0.1" : serverSettings.MessageServerIp.Trim();
            serverSettings.FTP_RootPath = NormalizeRootPath(serverSettings.FTP_RootPath);
            return true;
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            return false;
        }
    }

    private static bool TryUpsertRethink(ServerSettings serverSettings, LocalConnectionSettings connectionSettings, out string? errorMessage)
    {
        errorMessage = null;

        try
        {
            using var connection = OpenRethinkConnection(connectionSettings);
            var databaseName = string.IsNullOrWhiteSpace(connectionSettings.RethinkDatabase) ? "NewHyOn" : connectionSettings.RethinkDatabase.Trim();

            var databases = R.DbList().RunAtom<List<string>>(connection) ?? [];
            if (databases.Contains(databaseName) == false)
            {
                R.DbCreate(databaseName).Run(connection);
            }

            var tables = R.Db(databaseName).TableList().RunAtom<List<string>>(connection) ?? [];
            if (tables.Contains(ServerSettingsCollection) == false)
            {
                R.Db(databaseName).TableCreate(ServerSettingsCollection).Run(connection);
            }

            serverSettings.Id = 0;
            serverSettings.FTP_RootPath = NormalizeRootPath(serverSettings.FTP_RootPath);

            R.Db(databaseName)
                .Table(ServerSettingsCollection)
                .Insert(serverSettings)
                .OptArg("conflict", "replace")
                .Run(connection);

            return true;
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            return false;
        }
    }

    private static Connection OpenRethinkConnection(LocalConnectionSettings connectionSettings)
    {
        return R.Connection()
            .Hostname(connectionSettings.RethinkHost.Trim())
            .Port(connectionSettings.RethinkPort > 0 ? connectionSettings.RethinkPort : 28015)
            .User(
                string.IsNullOrWhiteSpace(connectionSettings.RethinkUser) ? "admin" : connectionSettings.RethinkUser.Trim(),
                string.IsNullOrWhiteSpace(connectionSettings.RethinkPassword) ? "turtle04!9" : connectionSettings.RethinkPassword)
            .Timeout(5)
            .Connect();
    }

    private static string NormalizeRootPath(string? rootPath)
    {
        if (string.IsNullOrWhiteSpace(rootPath))
        {
            return DefaultRootPath;
        }

        var normalized = rootPath.Replace("\\", "/").Trim();
        if (normalized.StartsWith("/") == false)
        {
            normalized = "/" + normalized;
        }

        normalized = normalized.TrimEnd('/');
        return string.IsNullOrWhiteSpace(normalized) ? "/" : normalized;
    }

    private static LocalConnectionSettings BuildConnectionSeed(ServerSettings? server)
    {
        return new LocalConnectionSettings
        {
            Id = SingletonId,
            RethinkHost = string.IsNullOrWhiteSpace(server?.DataServerIp) ? "127.0.0.1" : server.DataServerIp.Trim(),
            RethinkPort = 28015,
            RethinkDatabase = "NewHyOn",
            RethinkUser = "admin",
            RethinkPassword = "turtle04!9",
            SignalRHost = string.IsNullOrWhiteSpace(server?.MessageServerIp) ? "127.0.0.1" : server.MessageServerIp.Trim(),
            SignalRPort = 5000,
            SignalRHubPath = "/Data"
        };
    }

    private static LocalFtpSettings BuildFtpSeed(ServerSettings? server, LocalConnectionSettings connection)
    {
        return new LocalFtpSettings
        {
            Id = SingletonId,
            Host = string.IsNullOrWhiteSpace(server?.DataServerIp) ? connection.RethinkHost : server.DataServerIp.Trim(),
            Port = server?.FTP_Port > 0 ? server.FTP_Port : NetworkDefaults.FtpPort,
            PasvMinPort = server?.FTP_PasvMinPort > 0 ? server.FTP_PasvMinPort : NetworkDefaults.FtpPasvMinPort,
            PasvMaxPort = server?.FTP_PasvMaxPort > 0 ? server.FTP_PasvMaxPort : NetworkDefaults.FtpPasvMaxPort,
            User = "asdf",
            Password = "Emfndhk!",
            RootPath = NormalizeRootPath(server?.FTP_RootPath)
        };
    }

    private static LocalUiSettings BuildUiSeed(ServerSettings? server)
    {
        return new LocalUiSettings
        {
            Id = SingletonId,
            PreserveAspectRatio = server?.PreserveAspectRatio ?? false,
            DefaultResolutionOrientation = "Landscape",
            DefaultResolutionRows = 1,
            DefaultResolutionColumns = 1,
            DefaultResolutionWidthPixels = 1920,
            DefaultResolutionHeightPixels = 1080
        };
    }

    private sealed class LocalConnectionSettings
    {
        [BsonId]
        public string Id { get; set; } = SingletonId;

        public string RethinkHost { get; set; } = "127.0.0.1";

        public int RethinkPort { get; set; } = 28015;

        public string RethinkDatabase { get; set; } = "NewHyOn";

        public string RethinkUser { get; set; } = "admin";

        public string RethinkPassword { get; set; } = "turtle04!9";

        public string SignalRHost { get; set; } = "127.0.0.1";

        public int SignalRPort { get; set; } = 5000;

        public string SignalRHubPath { get; set; } = "/Data";
    }

    private sealed class LocalFtpSettings
    {
        [BsonId]
        public string Id { get; set; } = SingletonId;

        public string Host { get; set; } = "127.0.0.1";

        public int Port { get; set; } = NetworkDefaults.FtpPort;

        public int PasvMinPort { get; set; } = NetworkDefaults.FtpPasvMinPort;

        public int PasvMaxPort { get; set; } = NetworkDefaults.FtpPasvMaxPort;

        public string User { get; set; } = "asdf";

        public string Password { get; set; } = "Emfndhk!";

        public string RootPath { get; set; } = DefaultRootPath;
    }

    private sealed class LocalUiSettings
    {
        [BsonId]
        public string Id { get; set; } = SingletonId;

        public bool PreserveAspectRatio { get; set; }

        public string DefaultResolutionOrientation { get; set; } = "Landscape";

        public int DefaultResolutionRows { get; set; } = 1;

        public int DefaultResolutionColumns { get; set; } = 1;

        public double DefaultResolutionWidthPixels { get; set; } = 1920;

        public double DefaultResolutionHeightPixels { get; set; } = 1080;
    }

    private sealed class ServerSettings
    {
        [JsonProperty("id")]
        [BsonId(false)]
        public int Id { get; set; }

        public int FTP_Port { get; set; } = NetworkDefaults.FtpPort;

        public int FTP_PasvMinPort { get; set; } = NetworkDefaults.FtpPasvMinPort;

        public int FTP_PasvMaxPort { get; set; } = NetworkDefaults.FtpPasvMaxPort;

        public string FTP_RootPath { get; set; } = DefaultRootPath;

        public bool PreserveAspectRatio { get; set; }

        public string DataServerIp { get; set; } = "127.0.0.1";

        public string MessageServerIp { get; set; } = "127.0.0.1";
    }

    private static class NetworkDefaults
    {
        public const int FtpPort = 10021;
        public const int FtpPasvMinPort = 24000;
        public const int FtpPasvMaxPort = 24240;
    }
}
