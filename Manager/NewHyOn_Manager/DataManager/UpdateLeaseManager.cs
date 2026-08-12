using System;
using System.Collections.Generic;
using AndoW.Shared;
using TurtleTools;

namespace AndoW_Manager
{
    internal sealed class UpdateLeaseManager : RethinkDbManagerBase<UpdateLeaseEntry>
    {
        private const string LeaseTableName = "UpdateLease";
        private const string QueueTableName = "UpdateQueue";

        private UpdateLeaseManager()
            : base(RethinkDbConfigurator.GetDataDatabaseName(), LeaseTableName, "id")
        {
        }

        public static void CleanupInvalidLeases(int ttlSeconds)
        {
            new UpdateLeaseManager().Cleanup(ttlSeconds <= 0 ? 3600 : ttlSeconds);
        }

        private void Cleanup(int ttlSeconds)
        {
            foreach (var lease in LoadAllDocuments())
            {
                if (lease == null || string.IsNullOrWhiteSpace(lease.Id))
                {
                    continue;
                }

                DateTime expiresAt;
                bool hasExpiry = DateTime.TryParse(lease.LeaseExpiresAt, out expiresAt);
                DateTime lastRenewAt;
                bool hasLastRenew = DateTime.TryParse(lease.LastRenewAt, out lastRenewAt);
                bool expired = hasExpiry
                    ? expiresAt <= DateTime.Now
                    : !hasLastRenew || lastRenewAt.AddSeconds(ttlSeconds) <= DateTime.Now;
                bool acquisitionGrace = hasLastRenew
                    && lastRenewAt.AddSeconds(Math.Min(ttlSeconds, 60)) > DateTime.Now;
                if (expired || (!acquisitionGrace && !IsActiveDownloadQueue(lease.PlayerId, lease.QueueId)))
                {
                    DeleteIfUnchanged(lease);
                }
            }
        }

        private static void DeleteIfUnchanged(UpdateLeaseEntry lease)
        {
            string databaseName = RethinkDbConfigurator.GetDataDatabaseName();
            RethinkDbContext.Run(
                RethinkDbContext.Table(databaseName, LeaseTableName)
                    .Filter(row => row["id"].Eq(lease.Id)
                        .And(row["OwnerToken"].Default_(string.Empty).Eq(lease.OwnerToken ?? string.Empty))
                        .And(row["LastRenewAt"].Default_(string.Empty).Eq(lease.LastRenewAt ?? string.Empty)))
                    .Delete());
        }

        private static bool IsActiveDownloadQueue(string playerId, string queueId)
        {
            if (string.IsNullOrWhiteSpace(queueId))
            {
                return false;
            }

            string databaseName = RethinkDbConfigurator.GetDataDatabaseName();
            RethinkDbContext.EnsureTable(databaseName, QueueTableName, "id");
            string expectedKey = BuildQueueKey(playerId, queueId);
            foreach (var queue in RethinkDbContext.RunListOrThrow<Dictionary<string, object>>(
                RethinkDbContext.Table(databaseName, QueueTableName)))
            {
                object statusValue;
                if (queue == null
                    || (!queue.TryGetValue("status", out statusValue) && !queue.TryGetValue("Status", out statusValue))
                    || !string.Equals(Convert.ToString(statusValue), "DOWNLOADING", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                object queuePlayerId;
                queue.TryGetValue("playerId", out queuePlayerId);
                if (queuePlayerId == null)
                {
                    queue.TryGetValue("PlayerId", out queuePlayerId);
                }
                foreach (string field in new[] { "queueId", "QueueId", "id", "Id" })
                {
                    object candidate;
                    if (queue.TryGetValue(field, out candidate)
                        && string.Equals(BuildQueueKey(Convert.ToString(queuePlayerId), Convert.ToString(candidate)), expectedKey, StringComparison.Ordinal))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private static string BuildQueueKey(string playerId, string queueId)
        {
            string player = (playerId ?? string.Empty).Trim().ToLowerInvariant();
            string queue = (queueId ?? string.Empty).Trim().ToLowerInvariant();
            string prefix = player + ":";
            if (!string.IsNullOrWhiteSpace(player) && queue.StartsWith(prefix, StringComparison.Ordinal))
            {
                queue = queue.Substring(prefix.Length);
            }
            return player + "\n" + queue;
        }
    }
}