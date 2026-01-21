using System;
using LiteDB;
using Newtonsoft.Json;

namespace HyOnPlayer.DataManager
{
    public static class UpdateQueueStatus
    {
        public const string Queued = "QUEUED";
        public const string Downloading = "DOWNLOADING";
        public const string Downloaded = "DOWNLOADED";
        public const string Validating = "VALIDATING";
        public const string Ready = "READY";
        public const string Applying = "APPLYING";
        public const string Done = "DONE";
        public const string Failed = "FAILED";
    }

    public class UpdateQueue
    {
        private static readonly TimeZoneInfo KstZone = TimeZoneInfo.FindSystemTimeZoneById("Korea Standard Time");

        [BsonId]
        [BsonField("id")]
        [JsonProperty("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        public string PlayerId { get; set; } = string.Empty;
        public string PlayerName { get; set; } = string.Empty;
        public string PlaylistId { get; set; } = string.Empty;

        public string PayloadJson { get; set; } = string.Empty;
        public string DownloadJson { get; set; } = string.Empty;
        public double DownloadProgress { get; set; } = 0.0;
        public double ValidateProgress { get; set; } = 0.0;
        public string LastError { get; set; } = string.Empty;

        public string Status { get; set; } = UpdateQueueStatus.Queued;
        public int RetryCount { get; set; } = 0;
        public long NextAttemptTicks { get; set; } = DateTime.Now.Ticks;
        public long CreatedTicks { get; set; } = DateTime.Now.Ticks;
        public string HistoryId { get; set; } = string.Empty;
        public bool IsScheduleQueue { get; set; }

        public DateTime NextAttempt
        {
            get { return new DateTime(NextAttemptTicks, DateTimeKind.Local); }
            set { NextAttemptTicks = value.Ticks; }
        }

        [BsonIgnore]
        [JsonIgnore]
        public DateTime NextAttemptKst
        {
            get { return TimeZoneInfo.ConvertTime(NextAttempt, KstZone); }
        }

        [BsonIgnore]
        [JsonIgnore]
        public DateTime CreatedKst
        {
            get { return TimeZoneInfo.ConvertTime(new DateTime(CreatedTicks, DateTimeKind.Local), KstZone); }
        }
    }
}
