using System;
using HyOnPlayer.DataManager;
using RethinkDb.Driver;
using RethinkDb.Driver.Net;

namespace HyOnPlayer
{
    internal sealed class UpdateQueueRethinkClient : IDisposable
    {
        private const string Database = "AndoW";
        private const string Table = "UpdateQueue";
        private const string User = "admin";
        private const string Password = "turtle04!9";
        private const int Port = 28015;

        private static readonly RethinkDB R = RethinkDB.R;

        private Connection connection;
        private string host = "127.0.0.1";
        private bool ensuredTable;

        public UpdateQueueRethinkClient(string host = "127.0.0.1")
        {
            if (!string.IsNullOrWhiteSpace(host))
            {
                this.host = host;
            }
        }

        public void UpsertQueue(UpdateQueue queue)
        {
            if (queue == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(queue.PlayerId))
            {
                return;
            }

            try
            {
                EnsureTable();

                double progress = Math.Min(1.0, Math.Max(0.0, (queue.DownloadProgress + queue.ValidateProgress) / 2.0));
                string documentId = BuildDocumentId(queue.PlayerId, queue.Id);

                var payload = new
                {
                    id = documentId,
                    queueId = queue.Id ?? string.Empty,
                    playerId = queue.PlayerId ?? string.Empty,
                    playerName = queue.PlayerName ?? string.Empty,
                    playlistId = queue.PlaylistId ?? string.Empty,
                    status = queue.Status ?? string.Empty,
                    downloadProgress = queue.DownloadProgress,
                    validateProgress = queue.ValidateProgress,
                    progress = progress,
                    retryCount = queue.RetryCount,
                    nextAttemptTicks = queue.NextAttemptTicks,
                    createdTicks = queue.CreatedTicks,
                    lastError = queue.LastError ?? string.Empty,
                    downloadJson = queue.DownloadJson ?? string.Empty,
                    payloadJson = queue.PayloadJson ?? string.Empty,
                    updatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                };

                R.Db(Database)
                    .Table(Table)
                    .Insert(payload)
                    .OptArg("conflict", "replace")
                    .RunNoReply(GetConnection());
            }
            catch
            {
                // Rethink 반영 실패는 무시하여 재생/업데이트 흐름에 영향이 없도록 한다.
            }
        }

        public void DeleteQueueRecord(string queueId, string playerId)
        {
            if (string.IsNullOrWhiteSpace(queueId) || string.IsNullOrWhiteSpace(playerId))
            {
                return;
            }

            try
            {
                EnsureTable();
                string documentId = BuildDocumentId(playerId, queueId);
                R.Db(Database)
                    .Table(Table)
                    .Get(documentId)
                    .Delete()
                    .RunNoReply(GetConnection());
            }
            catch
            {
            }
        }

        public void Dispose()
        {
            try
            {
                connection?.Close(false);
                connection?.Dispose();
            }
            catch
            {
            }
            connection = null;
        }

        private Connection GetConnection()
        {
            if (connection != null && connection.Open)
            {
                return connection;
            }

            connection = R.Connection()
                .Hostname(string.IsNullOrWhiteSpace(host) ? "127.0.0.1" : host)
                .Port(Port)
                .User(User, Password)
                .Connect();

            return connection;
        }

        private void EnsureTable()
        {
            if (ensuredTable)
            {
                return;
            }

            try
            {
                var conn = GetConnection();
                if (conn == null || !conn.Open)
                {
                    return;
                }

                var tables = R.Db(Database).TableList().Run<System.Collections.Generic.List<string>>(conn);
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

        private static string BuildDocumentId(string playerId, string queueId)
        {
            string pid = playerId ?? string.Empty;
            string qid = queueId ?? string.Empty;

            if (!string.IsNullOrWhiteSpace(pid))
            {
                string prefix = $"{pid}:";
                if (qid.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    return qid;
                }
                if (!string.IsNullOrWhiteSpace(qid))
                {
                    return $"{pid}:{qid}";
                }
                return $"{pid}:{DateTime.Now.Ticks}";
            }

            if (!string.IsNullOrWhiteSpace(qid))
            {
                return qid;
            }

            return Guid.NewGuid().ToString();
        }
    }
}
