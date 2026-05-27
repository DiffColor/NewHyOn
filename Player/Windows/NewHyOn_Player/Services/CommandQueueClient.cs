using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json;
using RethinkDb.Driver;
using RethinkDb.Driver.Ast;
using RethinkDb.Driver.Net;
using TurtleTools;

namespace NewHyOnPlayer
{
    internal sealed class CommandQueueClient : IDisposable
    {
        private const string DatabaseName = "NewHyOn";
        private const string TableName = "CommandQueue";
        private const int SentRetryDelayMs = 15000;

        private static readonly RethinkDB R = RethinkDB.R;
        private readonly object syncRoot = new object();
        private readonly string host;
        private Connection connection;

        public CommandQueueClient(string managerHost)
        {
            host = string.IsNullOrWhiteSpace(managerHost) ? "127.0.0.1" : managerHost;
        }

        public void Dispose()
        {
            ResetConnection();
        }

        public List<CommandQueueEntry> FetchPendingCommands(string playerId)
        {
            if (string.IsNullOrWhiteSpace(playerId))
            {
                return new List<CommandQueueEntry>();
            }

            string normalizedPlayerId = NormalizePlayerId(playerId);
            if (string.IsNullOrWhiteSpace(normalizedPlayerId))
            {
                return new List<CommandQueueEntry>();
            }
            try
            {
                var conn = GetConnection();
                if (conn == null)
                {
                    return new List<CommandQueueEntry>();
                }

                ReqlExpr query = R.Db(DatabaseName)
                    .Table(TableName)
                    .Filter(row => row["PlayerIds"].Contains(normalizedPlayerId))
                    .OrderBy("CreatedAt");

                var entries = query
                    .CoerceTo("array")
                    .RunAtom<List<CommandQueueEntry>>(conn) ?? new List<CommandQueueEntry>();

                return entries
                    .Where(entry => HasPlayer(entry, normalizedPlayerId)
                        && IsActionableStatus(entry, normalizedPlayerId))
                    .ToList();
            }
            catch (Exception ex)
            {
                Logger.WriteErrorLog(ex.ToString(), Logger.GetLogFileName());
                ResetConnection();
                return new List<CommandQueueEntry>();
            }
        }

        public CommandQueueEntry FetchNextPending(string playerId)
        {
            return FetchPendingCommands(playerId).FirstOrDefault();
        }

        public void MarkAck(string commandId, string playerId)
        {
            UpdateStatus(commandId, playerId, "ack");
        }

        public void MarkFailed(string commandId, string playerId)
        {
            UpdateStatus(commandId, playerId, "failed");
        }

        public void MarkSent(string commandId, string playerId)
        {
            UpdateStatus(commandId, playerId, "sent");
        }

        public void MarkAttempt(string commandId)
        {
            if (string.IsNullOrWhiteSpace(commandId))
            {
                return;
            }

            string now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            try
            {
                var conn = GetConnection();
                if (conn == null)
                {
                    return;
                }

                R.Db(DatabaseName)
                    .Table(TableName)
                    .Get(commandId)
                    .Update(row => new
                    {
                        AttemptCount = row["AttemptCount"].Default_(0).Add(1),
                        LastAttemptAt = now,
                        UpdatedAt = now
                    })
                    .Run(conn);
            }
            catch (Exception ex)
            {
                Logger.WriteErrorLog(ex.ToString(), Logger.GetLogFileName());
                ResetConnection();
            }
        }

        private void UpdateStatus(string commandId, string playerId, string status)
        {
            if (string.IsNullOrWhiteSpace(commandId) || string.IsNullOrWhiteSpace(playerId) || string.IsNullOrWhiteSpace(status))
            {
                return;
            }

            string normalizedPlayerId = NormalizePlayerId(playerId);
            if (string.IsNullOrWhiteSpace(normalizedPlayerId))
            {
                return;
            }

            string now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            try
            {
                var conn = GetConnection();
                if (conn == null)
                {
                    return;
                }

                var entry = R.Db(DatabaseName)
                    .Table(TableName)
                    .Get(commandId)
                    .RunAtom<CommandQueueEntry>(conn);
                if (entry == null)
                {
                    return;
                }

                if (entry.Status == null)
                {
                    entry.Status = new Dictionary<string, string>();
                }

                entry.Status[normalizedPlayerId] = status.Trim();

                R.Db(DatabaseName)
                    .Table(TableName)
                    .Get(commandId)
                    .Update(new
                    {
                        Status = entry.Status,
                        UpdatedAt = now
                    })
                    .Run(conn);
            }
            catch (Exception ex)
            {
                Logger.WriteErrorLog(ex.ToString(), Logger.GetLogFileName());
                ResetConnection();
            }
        }

        private static string NormalizePlayerId(string playerId)
        {
            return string.IsNullOrWhiteSpace(playerId) ? string.Empty : playerId.Trim().ToLowerInvariant();
        }

        private static bool HasPlayer(CommandQueueEntry entry, string normalizedPlayerId)
        {
            if (entry == null || entry.PlayerIds == null || entry.PlayerIds.Count == 0 || string.IsNullOrWhiteSpace(normalizedPlayerId))
            {
                return false;
            }

            return entry.PlayerIds.Any(id => string.Equals(id, normalizedPlayerId, StringComparison.OrdinalIgnoreCase));
        }

        private static string GetStatus(CommandQueueEntry entry, string normalizedPlayerId)
        {
            if (entry == null || entry.Status == null || string.IsNullOrWhiteSpace(normalizedPlayerId))
            {
                return string.Empty;
            }

            if (entry.Status.TryGetValue(normalizedPlayerId, out string status))
            {
                return status ?? string.Empty;
            }

            return string.Empty;
        }

        private static bool IsStatus(CommandQueueEntry entry, string normalizedPlayerId, string expected)
        {
            if (string.IsNullOrWhiteSpace(expected))
            {
                return false;
            }

            string current = GetStatus(entry, normalizedPlayerId);
            return string.Equals(current, expected, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsActionableStatus(CommandQueueEntry entry, string normalizedPlayerId)
        {
            if (IsStatus(entry, normalizedPlayerId, "pending"))
            {
                return true;
            }

            if (!IsStatus(entry, normalizedPlayerId, "sent"))
            {
                return false;
            }

            return IsSentDeliveryStale(entry);
        }

        private static bool IsSentDeliveryStale(CommandQueueEntry entry)
        {
            if (entry == null)
            {
                return false;
            }

            DateTime sentAt = ParseTimestamp(entry.UpdatedAt);
            if (sentAt == DateTime.MinValue)
            {
                sentAt = ParseTimestamp(entry.CreatedAt);
            }
            if (sentAt == DateTime.MinValue)
            {
                return true;
            }

            return DateTime.Now - sentAt >= TimeSpan.FromMilliseconds(SentRetryDelayMs);
        }

        private static DateTime ParseTimestamp(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return DateTime.MinValue;
            }

            if (DateTime.TryParseExact(
                    value.Trim(),
                    "yyyy-MM-dd HH:mm:ss",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeLocal,
                    out DateTime exact))
            {
                return exact;
            }

            if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out DateTime parsed))
            {
                return parsed;
            }

            return DateTime.MinValue;
        }

        private Connection GetConnection()
        {
            lock (syncRoot)
            {
                if (connection != null && connection.Open)
                {
                    return connection;
                }

                connection = R.Connection()
                    .Hostname(host)
                    .Port(28015)
                    .User("admin", "turtle04!9")
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
    }

    internal sealed class CommandQueueEntry
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        public List<string> PlayerIds { get; set; }
        public string Command { get; set; }

        [JsonProperty("payloadJson")]
        public string PayloadBase64 { get; set; }

        public Dictionary<string, string> Status { get; set; }
        public string CreatedAt { get; set; }
        public string UpdatedAt { get; set; }
        public string ExpiresAt { get; set; }
        public int AttemptCount { get; set; }
        public string LastAttemptAt { get; set; }
        public string Source { get; set; }
        public string ReplacedBy { get; set; }
    }
}
