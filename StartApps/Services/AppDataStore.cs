using LiteDB;
using StartApps.Models;

namespace StartApps.Services;

public class AppDataStore : IDisposable
{
    private const string DatabaseFileName = "startapps.db";
    private const string CollectionName = "apps";
    private const string MigrationCollectionName = "migrations";
    private const string ManagerNtpMigrationId = "manager-ntp-v1";

    private readonly LiteDatabase _database;
    private readonly ILiteCollection<AppDefinition> _collection;

    public AppDataStore(AppProfile profile)
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var appFolder = Path.Combine(appData, profile.StorageFolderName);
        Directory.CreateDirectory(appFolder);

        var databasePath = Path.Combine(appFolder, DatabaseFileName);
        var mapper = BsonMapper.Global;
        mapper.EmptyStringToNull = false;

        _database = new LiteDatabase(databasePath);
        _collection = _database.GetCollection<AppDefinition>(CollectionName);
        _collection.EnsureIndex(x => x.Id, true);
        _collection.EnsureIndex(x => x.Zone);
    }

    public Task<IList<AppDefinition>> LoadAsync()
    {
        IList<AppDefinition> result = _collection.FindAll().ToList();
        return Task.FromResult(result);
    }

    public Task SaveAsync(IEnumerable<AppDefinition> apps)
    {
        var snapshot = apps.Select(CloneDefinition).ToList();
        return Task.Run(() =>
        {
            _database.BeginTrans();
            try
            {
                _collection.DeleteAll();
                if (snapshot.Count > 0)
                {
                    _collection.InsertBulk(snapshot);
                }
                _database.Commit();
            }
            catch
            {
                _database.Rollback();
                throw;
            }
        });
    }

    public Task<bool> HasManagerNtpMigrationAsync()
    {
        var migrations = _database.GetCollection<BsonDocument>(MigrationCollectionName);
        return Task.FromResult(migrations.Exists(Query.EQ("_id", ManagerNtpMigrationId)));
    }

    public Task MarkManagerNtpMigrationAsync()
    {
        var migrations = _database.GetCollection<BsonDocument>(MigrationCollectionName);
        migrations.Upsert(new BsonDocument
        {
            ["_id"] = ManagerNtpMigrationId,
            ["completedAt"] = DateTime.UtcNow
        });
        return Task.CompletedTask;
    }

    private static AppDefinition CloneDefinition(AppDefinition source)
    {
        return new AppDefinition
        {
            Id = source.Id,
            Name = source.Name,
            Type = source.Type,
            Zone = source.Zone,
            IsEnabled = source.IsEnabled,
            ExecutablePath = source.ExecutablePath,
            Arguments = source.Arguments,
            ShowWindow = source.ShowWindow,
            WindowStyle = source.WindowStyle,
            RunAsAdministrator = source.RunAsAdministrator,
            Port = source.Port,
            MsgHubPath = source.MsgHubPath,
            PassivePortRange = source.PassivePortRange,
            WorkingDirectory = source.WorkingDirectory,
            WaitForExitBeforeNext = source.WaitForExitBeforeNext,
            DisplayOrder = source.DisplayOrder,
            LastStartedAt = source.LastStartedAt,
            DelayMinutes = source.DelayMinutes,
            DelaySeconds = source.DelaySeconds,
            RequireNetworkAvailable = source.RequireNetworkAvailable,
            ToggleShortcutModifiers = source.ToggleShortcutModifiers,
            ToggleShortcutKey = source.ToggleShortcutKey,
            FtpUsername = source.FtpUsername,
            FtpPassword = source.FtpPassword,
            FtpHomeDirectory = source.FtpHomeDirectory,
            FtpAllowRead = source.FtpAllowRead,
            FtpAllowWrite = source.FtpAllowWrite
        };
    }

    public void Dispose()
    {
        _database?.Dispose();
    }
}
