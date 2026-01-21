using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace AndoW.Shared
{
    public sealed class CommandQueueEntry
    {
        [JsonProperty("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        public List<string> PlayerIds { get; set; } = new List<string>();
        public string Command { get; set; } = string.Empty;

        [JsonProperty("payloadJson")]
        public string PayloadBase64 { get; set; } = string.Empty;

        public Dictionary<string, string> Status { get; set; } = new Dictionary<string, string>();
        public string CreatedAt { get; set; } = string.Empty;
        public string UpdatedAt { get; set; } = string.Empty;
        public string ExpiresAt { get; set; } = string.Empty;
        public int AttemptCount { get; set; }
        public string LastAttemptAt { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
        public string ReplacedBy { get; set; } = string.Empty;
    }
}
