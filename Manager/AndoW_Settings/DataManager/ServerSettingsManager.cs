using LiteDB;
using Newtonsoft.Json;
using RethinkDb.Driver;
using System;
using System.Collections.Generic;
using TurtleTools;

namespace AndoWSettings
{
    public class ServerSettingsManager
    {
        public ServerSettings sData { get; private set; }
        private const string CollectionName = "ServerSettings";
        private static readonly RethinkDB R = RethinkDB.R;
        private const string RethinkTableName = "ServerSettings";

        public ServerSettings LoadData()
        {
            using (var db = LocalDbContext.OpenDatabase())
            {
                var collection = db.GetCollection<ServerSettings>(CollectionName);
                sData = collection.FindById(0);

                if (sData == null)
                {
                    sData = collection.FindOne(Query.All());
                    if (sData != null)
                    {
                        sData.Id = 0;
                    }
                    else
                    {
                        sData = new ServerSettings();
                    }
                }

                if (string.IsNullOrWhiteSpace(sData.DataServerIp))
                {
                    sData.DataServerIp = "127.0.0.1";
                }

                if (string.IsNullOrWhiteSpace(sData.MessageServerIp))
                {
                    sData.MessageServerIp = "127.0.0.1";
                }

                collection.Upsert(sData);
            }

            return sData;
        }

        public void SaveData(ServerSettings settings)
        {
            if (settings == null)
            {
                return;
            }

            settings.Id = 0;

            using (var db = LocalDbContext.OpenDatabase())
            {
                var collection = db.GetCollection<ServerSettings>(CollectionName);
                collection.Upsert(settings);
            }

            TryUpsertRethink(settings);
            sData = settings;
        }

        private static void TryUpsertRethink(ServerSettings settings)
        {
            try
            {
                LocalSettingsStore.EnsureSeeded();
                var connSettings = LocalSettingsStore.GetConnectionSettings();
                if (connSettings == null || string.IsNullOrWhiteSpace(connSettings.RethinkHost))
                {
                    Logger.WriteErrorLog("RethinkDB upsert skipped: Rethink host is empty.", Logger.GetLogFileName());
                    return;
                }

                string host = connSettings.RethinkHost.Trim();
                int port = connSettings.RethinkPort > 0 ? connSettings.RethinkPort : 28015;
                string user = string.IsNullOrWhiteSpace(connSettings.RethinkUser) ? "admin" : connSettings.RethinkUser.Trim();
                string password = string.IsNullOrWhiteSpace(connSettings.RethinkPassword) ? "turtle04!9" : connSettings.RethinkPassword;
                string database = string.IsNullOrWhiteSpace(connSettings.RethinkDatabase) ? "NewHyOn" : connSettings.RethinkDatabase.Trim();

                using (var conn = R.Connection()
                    .Hostname(host)
                    .Port(port)
                    .User(user, password)
                    .Timeout(5000)
                    .Connect())
                {
                    var databases = R.DbList().RunAtom<List<string>>(conn) ?? new List<string>();
                    if (!databases.Contains(database))
                    {
                        R.DbCreate(database).Run(conn);
                    }

                    var tables = R.Db(database).TableList().RunAtom<List<string>>(conn) ?? new List<string>();
                    if (!tables.Contains(RethinkTableName))
                    {
                        R.Db(database).TableCreate(RethinkTableName).Run(conn);
                    }

                    settings.Id = 0;
                    R.Db(database)
                        .Table(RethinkTableName)
                        .Insert(settings)
                        .OptArg("conflict", "replace")
                        .Run(conn);
                }
            }
            catch (Exception ex)
            {
                Logger.WriteErrorLog($"RethinkDB upsert failed: {ex}", Logger.GetLogFileName());
            }
        }
    }

    public class ServerSettings
    {
        [JsonProperty("id")]
        [BsonId(false)]
        public int Id { get; set; } = 0; // ?쒓컻???곗씠?곕쭔 ??ν븯湲??꾪븳 ?꾨뱶
        public int FTP_Port { get; set; } = NetworkTools.FTP_PORT;
        public int FTP_PasvMinPort { get; set; } = NetworkTools.FTP_PASV_MIN_PORT;
        public int FTP_PasvMaxPort { get; set; } = NetworkTools.FTP_PASV_MAX_PORT;
        public bool PreserveAspectRatio { get; set; } = false;
        public string DataServerIp { get; set; } = "127.0.0.1";
        public string MessageServerIp { get; set; } = "127.0.0.1";
    }
}
