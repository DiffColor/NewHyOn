using System;
using System.Collections.Generic;
using System.Linq;
using AndoW.Shared;
using TurtleTools;

namespace AndoW_Manager
{
    public sealed class CommandQueueManager : RethinkDbManagerBase<CommandQueueEntry>
    {
        private const string TableName = "CommandQueue";

        public CommandQueueManager()
            : base(RethinkDbConfigurator.GetDataDatabaseName(), TableName, "id")
        {
        }

        public CommandQueueEntry EnqueueCommand(string playerId, string command, string payloadBase64, string source, string expiresAt = "")
        {
            string normalizedPlayerId = NormalizePlayerId(playerId);
            if (string.IsNullOrWhiteSpace(normalizedPlayerId) || string.IsNullOrWhiteSpace(command))
            {
                return null;
            }

            string now = NowString();
            var entry = new CommandQueueEntry
            {
                Id = Guid.NewGuid().ToString(),
                PlayerIds = new List<string> { normalizedPlayerId },
                Command = command.Trim(),
                PayloadBase64 = payloadBase64 ?? string.Empty,
                Status = BuildStatusMap(normalizedPlayerId, "pending"),
                CreatedAt = now,
                UpdatedAt = now,
                ExpiresAt = expiresAt ?? string.Empty,
                AttemptCount = 0,
                LastAttemptAt = string.Empty,
                Source = source ?? string.Empty,
                ReplacedBy = string.Empty
            };

            Upsert(entry);
            return entry;
        }

        public void SupersedePending(string playerId, string replacedById)
        {
            string normalizedPlayerId = NormalizePlayerId(playerId);
            if (string.IsNullOrWhiteSpace(normalizedPlayerId) || string.IsNullOrWhiteSpace(replacedById))
            {
                return;
            }

            var pending = Find(x => HasPlayer(x, normalizedPlayerId)
                                    && IsStatus(x, normalizedPlayerId, "pending"));
            if (pending.Count == 0)
            {
                return;
            }

            string now = NowString();
            foreach (var entry in pending)
            {
                if (entry == null || string.Equals(entry.Id, replacedById, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                SetStatus(entry, normalizedPlayerId, "superseded");
                entry.ReplacedBy = replacedById;
                entry.UpdatedAt = now;
                Upsert(entry);
            }
        }

        public List<CommandQueueEntry> LoadPending(string playerId)
        {
            string normalizedPlayerId = NormalizePlayerId(playerId);
            if (string.IsNullOrWhiteSpace(normalizedPlayerId))
            {
                return new List<CommandQueueEntry>();
            }

            return Find(x => HasPlayer(x, normalizedPlayerId)
                             && IsStatus(x, normalizedPlayerId, "pending"))
                .OrderBy(x => x.CreatedAt)
                .ToList();
        }

        public void MarkStatus(string commandId, string playerId, string status)
        {
            if (string.IsNullOrWhiteSpace(commandId) || string.IsNullOrWhiteSpace(playerId) || string.IsNullOrWhiteSpace(status))
            {
                return;
            }

            var entry = FindById(commandId);
            if (entry == null)
            {
                return;
            }

            SetStatus(entry, playerId, status);
            entry.UpdatedAt = NowString();
            Upsert(entry);
        }

        public void MarkAttempt(string commandId)
        {
            if (string.IsNullOrWhiteSpace(commandId))
            {
                return;
            }

            var entry = FindById(commandId);
            if (entry == null)
            {
                return;
            }

            entry.AttemptCount++;
            entry.LastAttemptAt = NowString();
            entry.UpdatedAt = entry.LastAttemptAt;
            Upsert(entry);
        }

        private static string NowString()
        {
            return DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
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

        private static void SetStatus(CommandQueueEntry entry, string playerId, string status)
        {
            if (entry == null || string.IsNullOrWhiteSpace(playerId))
            {
                return;
            }

            string normalizedPlayerId = NormalizePlayerId(playerId);
            if (string.IsNullOrWhiteSpace(normalizedPlayerId))
            {
                return;
            }

            if (entry.Status == null)
            {
                entry.Status = new Dictionary<string, string>();
            }

            entry.Status[normalizedPlayerId] = status?.Trim() ?? string.Empty;
        }

        private static Dictionary<string, string> BuildStatusMap(string normalizedPlayerId, string status)
        {
            var map = new Dictionary<string, string>();
            if (!string.IsNullOrWhiteSpace(normalizedPlayerId))
            {
                map[normalizedPlayerId] = status ?? string.Empty;
            }

            return map;
        }
    }
}
