using System;
using LiteDB;
using Newtonsoft.Json;

namespace HyOnPlayer.DataManager
{
    public static class CommandHistoryStatus
    {
        public const string Queued = "queued";
        public const string InProgress = "in_progress";
        public const string Done = "done";
        public const string Failed = "failed";
        public const string Cancelled = "cancelled";
    }

    public class CommandHistory
    {
        [BsonId]
        [BsonField("id")]
        [JsonProperty("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        public string PlayerId { get; set; } = string.Empty;
        public string PlayerName { get; set; } = string.Empty;
        public string Command { get; set; } = string.Empty;
        public string RefQueueId { get; set; } = string.Empty;
        public string Status { get; set; } = CommandHistoryStatus.Queued;
        public string ErrorCode { get; set; } = string.Empty;
        public string ErrorMessage { get; set; } = string.Empty;

        public string CreatedAt { get; set; } = string.Empty;
        public string StartedAt { get; set; } = string.Empty;
        public string EndedAt { get; set; } = string.Empty;
        public string MetadataJson { get; set; } = string.Empty;
    }
}
