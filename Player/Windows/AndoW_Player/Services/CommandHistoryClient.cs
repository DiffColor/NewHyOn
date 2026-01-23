using System;
using RethinkDb.Driver;
using RethinkDb.Driver.Net;
using HyOnPlayer.DataManager;
using System.Collections.Generic;

namespace HyOnPlayer
{
    internal sealed class CommandHistoryClient : IDisposable
    {
        private const string Database = "NewHyOn";
        private const string Table = "CommandHistory";
        private const string User = "admin";
        private const string Password = "turtle04!9";
        private const int Port = 28015;

        private static readonly RethinkDB R = RethinkDB.R;
        private Connection connection;
        private string host = "127.0.0.1";
        private bool ensuredTable;

        public CommandHistoryClient(string host = "127.0.0.1")
        {
            if (!string.IsNullOrWhiteSpace(host))
            {
                this.host = host;
            }
        }

        public string CreateQueued(string playerId, string playerName, string command, string metadataJson = "")
        {
            try
            {
                string now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                var doc = new
                {
                    id = Guid.NewGuid().ToString(),
                    playerId = playerId ?? string.Empty,
                    playerName = playerName ?? string.Empty,
                    command = command ?? string.Empty,
                    refQueueId = string.Empty,
                    status = CommandHistoryStatus.Queued,
                    errorCode = string.Empty,
                    errorMessage = string.Empty,
                    createdAt = now,
                    startedAt = string.Empty,
                    endedAt = string.Empty,
                    metadata = string.IsNullOrWhiteSpace(metadataJson) ? string.Empty : metadataJson
                };

                R.Db(Database).Table(Table).Insert(doc).Run(GetConnection());
                return doc.id;
            }
            catch
            {
                return string.Empty;
            }
        }

        public void UpsertQueued(string id, string playerId, string playerName, string command, string metadataJson = "")
        {
            if (string.IsNullOrWhiteSpace(id)) return;
            try
            {
                string now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                var doc = new
                {
                    id = id,
                    playerId = playerId ?? string.Empty,
                    playerName = playerName ?? string.Empty,
                    command = command ?? string.Empty,
                    refQueueId = id,
                    status = CommandHistoryStatus.Queued,
                    errorCode = string.Empty,
                    errorMessage = string.Empty,
                    createdAt = now,
                    startedAt = string.Empty,
                    endedAt = string.Empty,
                    metadata = string.IsNullOrWhiteSpace(metadataJson) ? string.Empty : metadataJson
                };
                R.Db(Database).Table(Table)
                    .Insert(doc)
                    .OptArg("conflict", "replace")
                    .Run(GetConnection());
            }
            catch
            {
            }
        }

        public void MarkInProgress(string historyId, string refQueueId = null, string metadataJson = null)
        {
            if (string.IsNullOrWhiteSpace(historyId)) return;
            try
            {
                string now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                var update = new
                {
                    status = CommandHistoryStatus.InProgress,
                    startedAt = now,
                    refQueueId = refQueueId ?? string.Empty,
                    metadata = metadataJson ?? string.Empty
                };
                R.Db(Database).Table(Table).Get(historyId).Update(update).Run(GetConnection());
            }
            catch
            {
            }
        }

        public void MarkDone(string historyId, string status, string errorCode, string errorMessage)
        {
            if (string.IsNullOrWhiteSpace(historyId)) return;
            try
            {
                string now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                var update = new
                {
                    status = status ?? CommandHistoryStatus.Done,
                    errorCode = errorCode ?? string.Empty,
                    errorMessage = errorMessage ?? string.Empty,
                    endedAt = now
                };
                R.Db(Database).Table(Table).Get(historyId).Update(update).Run(GetConnection());
            }
            catch
            {
            }
        }

        public void Dispose()
        {
            ResetConnection();
        }

        public void PurgeOlderThanDays(int days)
        {
            if (days <= 0) return;
            try
            {
                string threshold = DateTime.Now.AddDays(-days).ToString("yyyy-MM-dd HH:mm:ss");
                R.Db(Database)
                    .Table(Table)
                    .Filter(r => r["endedAt"].Ne(string.Empty).And(r["endedAt"].Lt(threshold)))
                    .Delete()
                    .Run(GetConnection());
            }
            catch
            {
            }
        }

        private Connection GetConnection()
        {
            if (connection != null && connection.Open) return connection;
            connection = R.Connection()
                .Hostname(string.IsNullOrWhiteSpace(host) ? "127.0.0.1" : host)
                .Port(Port)
                .User(User, Password)
                .Connect();
            EnsureTable();
            return connection;
        }

        private void ResetConnection()
        {
            try
            {
                if (connection != null)
                {
                    connection.Close(false);
                    connection.Dispose();
                }
            }
            catch
            {
            }
            connection = null;
        }

        private void EnsureTable()
        {
            if (ensuredTable) return;
            try
            {
                var conn = connection;
                if (conn == null || !conn.Open) return;

                var tables = R.Db(Database).TableList().Run<List<string>>(conn);
                if (tables == null || !tables.Contains(Table))
                {
                    R.Db(Database).TableCreate(Table).Run(conn);
                }
                ensuredTable = true;
            }
            catch
            {
                ensuredTable = false;
            }
        }
    }
}
