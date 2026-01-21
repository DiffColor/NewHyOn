using System;
using LiteDB;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace AndoW.Shared
{
    public class PlayerInfoClass
    {
        [BsonId]
        [BsonField("id")]
        [JsonIgnore] // LiteDB only
        public int Id { get; set; } = 0; // Local single-row storage

        [JsonProperty("id")]
        [BsonField("PIF_GUID")]
        public string PIF_GUID { get; set; } = string.Empty;

        public string PIF_PlayerName { get; set; } = string.Empty;
        public string PIF_IPAddress { get; set; } = string.Empty;
        public string PIF_CurrentPlayList { get; set; } = string.Empty;
        public string PIF_DefaultPlayList { get; set; } = string.Empty;
        public bool PIF_IsLandScape { get; set; } = true;
        public string PIF_OSName { get; set; } = string.Empty;
        public string PIF_MacAddress { get; set; } = string.Empty;

        [JsonProperty("command")]
        public string PendingCommand { get; set; } = string.Empty;

        public string PIF_AuthKey { get; set; } = string.Empty;

        public string UpdateStatus { get; set; } = string.Empty;
        public double UpdateProgress { get; set; }
        public string UpdateError { get; set; } = string.Empty;
        public int UpdateRetry { get; set; }
        public long UpdateNext { get; set; }

        public void CopyData(PlayerInfoClass pinfo)
        {
            if (pinfo == null) return;

            PIF_PlayerName = pinfo.PIF_PlayerName;
            PIF_IPAddress = pinfo.PIF_IPAddress;
            PIF_CurrentPlayList = pinfo.PIF_CurrentPlayList;
            PIF_DefaultPlayList = pinfo.PIF_DefaultPlayList;
            PIF_IsLandScape = pinfo.PIF_IsLandScape;
            PIF_GUID = pinfo.PIF_GUID;
            PIF_OSName = pinfo.PIF_OSName;
            PIF_MacAddress = pinfo.PIF_MacAddress;
            PendingCommand = pinfo.PendingCommand;
            PIF_AuthKey = pinfo.PIF_AuthKey;
            UpdateStatus = pinfo.UpdateStatus;
            UpdateProgress = pinfo.UpdateProgress;
            UpdateError = pinfo.UpdateError;
            UpdateRetry = pinfo.UpdateRetry;
            UpdateNext = pinfo.UpdateNext;
        }
    }

    public class LocalPlayerSettings
    {
        [BsonId]
        [BsonField("id")]
        public int Id { get; set; } = 1;

        public string ManagerIP { get; set; } = string.Empty;
        public bool IsTestMode { get; set; }
        public bool IsAllDayPlay { get; set; }
        public string EndTimeAction { get; set; } = "ApplicationClose";
        public int NetworkChkInterval { get; set; } = 3000;
        public bool IsOnlyOnePage { get; set; }
        public bool IsLocalPlay { get; set; }
        public bool HideCursor { get; set; } = true;
        public bool IsCheckInternetForFlash { get; set; } = true;
        public bool BlockMonitorOnEndTime { get; set; }
        public string TvSourceName { get; set; } = string.Empty;
        public int DisplayOrder { get; set; } = 0;

        public string SwitchTiming { get; set; } = "Immediately"; // Immediately, PageEnd, ContentEnd

        public bool IsSyncEnabled { get; set; }
        public bool IsLeading { get; set; }
        public List<string> SyncClientIps { get; set; } = new List<string>();
    }
}
