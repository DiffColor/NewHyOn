using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using PageInfoClass = AndoW_Manager.PageInfoClass;
using PageListInfoClass = AndoW_Manager.PageListInfoClass;

namespace AndoW.Shared
{
    public sealed class UpdatePayload
    {
        public PageListInfoClass PageList { get; set; }

        public List<PageInfoClass> Pages { get; set; }

        public ContractPlaylistPayload Contract { get; set; }

        public ScheduleUpdatePayload Schedule { get; set; }
    }

    public sealed class ScheduleUpdatePayload
    {
        public string PlayerId { get; set; } = string.Empty;

        public string PlayerName { get; set; } = string.Empty;

        public string GeneratedAt { get; set; } = string.Empty;

        public List<SpecialSchedulePayload> SpecialSchedules { get; set; } = new List<SpecialSchedulePayload>();

        public List<SchedulePlaylistPayload> Playlists { get; set; } = new List<SchedulePlaylistPayload>();

        public WeeklyPlayScheduleInfo WeeklySchedule { get; set; }
    }

    public sealed class SchedulePlaylistPayload
    {
        public string PlaylistName { get; set; } = string.Empty;

        public PageListInfoClass PageList { get; set; }

        public List<PageInfoClass> Pages { get; set; } = new List<PageInfoClass>();

        public ContractPlaylistPayload Contract { get; set; }
    }

    public sealed class SpecialSchedulePayload
    {
        public string Id { get; set; } = string.Empty;

        public string PageListName { get; set; } = string.Empty;

        public bool DayOfWeek1 { get; set; }

        public bool DayOfWeek2 { get; set; }

        public bool DayOfWeek3 { get; set; }

        public bool DayOfWeek4 { get; set; }

        public bool DayOfWeek5 { get; set; }

        public bool DayOfWeek6 { get; set; }

        public bool DayOfWeek7 { get; set; }

        public bool IsPeriodEnable { get; set; }

        public int DisplayStartH { get; set; }

        public int DisplayStartM { get; set; }

        public int DisplayEndH { get; set; }

        public int DisplayEndM { get; set; }

        public int PeriodStartYear { get; set; }

        public int PeriodStartMonth { get; set; }

        public int PeriodStartDay { get; set; }

        public int PeriodEndYear { get; set; }

        public int PeriodEndMonth { get; set; }

        public int PeriodEndDay { get; set; }
    }

    public sealed class ContractPlaylistPayload
    {
        public string PlayerId { get; set; } = string.Empty;

        public string PlayerName { get; set; } = string.Empty;

        public bool PlayerLandscape { get; set; }

        public string PlaylistId { get; set; } = string.Empty;

        public string PlaylistName { get; set; } = string.Empty;

        public List<ContractPagePayload> Pages { get; set; } = new List<ContractPagePayload>();
    }

    public sealed class ContractPagePayload
    {
        public string PageId { get; set; } = string.Empty;

        public string PageName { get; set; } = string.Empty;

        public int OrderIndex { get; set; }

        public int PlayHour { get; set; }

        public int PlayMinute { get; set; }

        public int PlaySecond { get; set; }

        public int Volume { get; set; }

        public bool Landscape { get; set; }

        public List<ContractElementPayload> Elements { get; set; } = new List<ContractElementPayload>();
    }

    public sealed class ContractElementPayload
    {
        public string ElementId { get; set; } = string.Empty;

        public string PageId { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public string Type { get; set; } = string.Empty;

        public double Width { get; set; }

        public double Height { get; set; }

        public double PosTop { get; set; }

        public double PosLeft { get; set; }

        public int ZIndex { get; set; }

        public List<ContractContentPayload> Contents { get; set; } = new List<ContractContentPayload>();
    }

    public sealed class ContractContentPayload
    {
        public string Uid { get; set; } = string.Empty;

        public string ElementId { get; set; } = string.Empty;

        public string FileName { get; set; } = string.Empty;

        public string FileFullPath { get; set; } = string.Empty;

        public string ContentType { get; set; } = string.Empty;

        public string PlayMinute { get; set; } = string.Empty;

        public string PlaySecond { get; set; } = string.Empty;

        public bool Valid { get; set; }

        public int ScrollSpeedSec { get; set; }

        public string RemoteChecksum { get; set; } = string.Empty;

        public long FileSize { get; set; }

        public bool FileExist { get; set; }
    }

    public static class UpdatePayloadCodec
    {
        private static readonly JsonSerializerSettings CodecSettings = new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore,
            TypeNameHandling = TypeNameHandling.None
        };

        public static string Encode(UpdatePayload payload)
        {
            if (payload == null)
            {
                return string.Empty;
            }

            string json = JsonConvert.SerializeObject(payload, CodecSettings);
            if (string.IsNullOrWhiteSpace(json))
            {
                return string.Empty;
            }

            byte[] data = Encoding.UTF8.GetBytes(json);
            return Convert.ToBase64String(data);
        }

        public static UpdatePayload Decode(string base64)
        {
            if (string.IsNullOrWhiteSpace(base64))
            {
                return null;
            }

            try
            {
                string json;
                try
                {
                    byte[] data = Convert.FromBase64String(base64);
                    json = Encoding.UTF8.GetString(data);
                }
                catch
                {
                    json = base64;
                }

                if (string.IsNullOrWhiteSpace(json))
                {
                    return null;
                }

                return JsonConvert.DeserializeObject<UpdatePayload>(json, CodecSettings);
            }
            catch
            {
                return null;
            }
        }
    }

    public sealed class UpdateThrottleSettings
    {
        [JsonProperty("id")]
        public string Id { get; set; } = "global";

        public int MaxConcurrentDownloads { get; set; } = 8;

        public int RetryIntervalSeconds { get; set; } = 60;

        public int LeaseTtlSeconds { get; set; } = 3600;

        public int LeaseRenewIntervalSeconds { get; set; } = 30;

        public int SettingsRefreshSeconds { get; set; } = 1800;

        public string UpdatedAt { get; set; } = string.Empty;
    }

    public sealed class UpdateLeaseEntry
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();

        public string PlayerId { get; set; } = string.Empty;

        public string QueueId { get; set; } = string.Empty;

        public string LeaseExpiresAt { get; set; } = string.Empty;

        public string LastRenewAt { get; set; } = string.Empty;
    }
}
