using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using RethinkDb.Driver;
using TurtleTools;

namespace AndoW_Manager
{
    public sealed class ContentDetailsManager : RethinkDbManagerBase<ContentDetails>
    {
        private const string TableName = "ContentDetails";
        private const string PrimaryKeyName = "id";
        private const string IndexPartialHash = "idx_partial_hash";
        private const string IndexEntireHash = "idx_entire_hash";
        private static readonly object IndexSync = new object();
        private static bool IndexesEnsured;
        private readonly string _databaseName;

        public ContentDetailsManager()
            : base(RethinkDbConfigurator.GetDataDatabaseName(), TableName, PrimaryKeyName)
        {
            _databaseName = RethinkDbConfigurator.GetDataDatabaseName();
            EnsureIndexes();
        }

        public List<ContentDetails> LoadAll()
        {
            return LoadAllDocuments();
        }

        public List<ContentDetails> FindByPartialHash(string partialHash)
        {
            if (string.IsNullOrWhiteSpace(partialHash))
            {
                return new List<ContentDetails>();
            }

            EnsureIndexes();
            var table = RethinkDbContext.Table(_databaseName, TableName);
            var query = table.GetAll(partialHash).OptArg("index", IndexPartialHash);
            return RethinkDbContext.RunList<ContentDetails>(query);
        }

        public List<ContentDetails> FindByEntireHash(string entireHash)
        {
            if (string.IsNullOrWhiteSpace(entireHash))
            {
                return new List<ContentDetails>();
            }

            EnsureIndexes();
            var table = RethinkDbContext.Table(_databaseName, TableName);
            var query = table.GetAll(entireHash).OptArg("index", IndexEntireHash);
            return RethinkDbContext.RunList<ContentDetails>(query);
        }

        public List<ContentDetails> FindByFileName(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return new List<ContentDetails>();
            }

            EnsureIndexes();
            var table = RethinkDbContext.Table(_databaseName, TableName);
            var query = table.Filter(row => row["filename"].Eq(fileName));
            return RethinkDbContext.RunList<ContentDetails>(query);
        }

        public ContentDetails FindById(string id)
        {
            return FindById((object)id);
        }

        public void Save(ContentDetails details)
        {
            Upsert(details);
        }

        private void EnsureIndexes()
        {
            if (IndexesEnsured)
            {
                return;
            }

            lock (IndexSync)
            {
                if (IndexesEnsured)
                {
                    return;
                }

                try
                {
                    var table = RethinkDbContext.Table(_databaseName, TableName);
                    var existing = RethinkDbContext.RunSingleOrDefault<List<string>>(table.IndexList()) ?? new List<string>();
                    var created = new List<string>();

                    if (!existing.Contains(IndexPartialHash, StringComparer.OrdinalIgnoreCase))
                    {
                        if (TryCreateIndex(table, IndexPartialHash, row => row["partial_hash"]))
                        {
                            created.Add(IndexPartialHash);
                        }
                    }

                    if (!existing.Contains(IndexEntireHash, StringComparer.OrdinalIgnoreCase))
                    {
                        if (TryCreateIndex(table, IndexEntireHash, row => row["entire_hash"]))
                        {
                            created.Add(IndexEntireHash);
                        }
                    }

                    if (created.Count > 0)
                    {
                        RethinkDbContext.Run(table.IndexWait(created));
                    }

                    IndexesEnsured = true;
                }
                catch (Exception ex)
                {
                    Logger.WriteErrorLog(ex.ToString(), Logger.GetLogFileName());
                }
            }
        }

        private static bool TryCreateIndex(RethinkDb.Driver.Ast.Table table,
                                           string indexName,
                                           RethinkDb.Driver.Ast.ReqlFunction1 indexFunction)
        {
            try
            {
                table.IndexCreate(indexName, indexFunction).Run(RethinkDbContext.GetRawConnection());
                return true;
            }
            catch (Exception ex)
            {
                if (ex.Message != null
                    && ex.Message.IndexOf("already exists", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return false;
                }

                throw;
            }
        }
    }

    public sealed class ContentDetails
    {
        [JsonProperty("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString("N");

        [JsonProperty("partial_hash")]
        public string PartialHash { get; set; } = string.Empty;

        [JsonProperty("entire_hash")]
        public string EntireHash { get; set; } = string.Empty;

        [JsonProperty("type")]
        public string Type { get; set; } = string.Empty;

        [JsonProperty("filename")]
        public string FileName { get; set; } = string.Empty;

        [JsonProperty("filesize")]
        public long FileSize { get; set; }

        [JsonProperty("vLength")]
        public double VideoLength { get; set; }
    }
}
