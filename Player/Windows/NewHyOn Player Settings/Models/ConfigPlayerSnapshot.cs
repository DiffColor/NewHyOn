using System.Collections.Generic;

namespace NewHyOn.Player.Settings.Models;

public sealed class ConfigPlayerSnapshot
{
    public string ManagerIp { get; set; } = string.Empty;
    public string PlayerIp { get; set; } = string.Empty;
    public string PlayerName { get; set; } = string.Empty;
    public string SourceKey { get; set; } = string.Empty;
    public string AuthStatusText { get; set; } = string.Empty;
    public bool IsLicensed { get; set; }
    public bool IsAuthInputEnabled { get; set; } = true;
    public string SignalRPort { get; set; } = string.Empty;
    public string FtpPort { get; set; } = string.Empty;
    public string SyncPort { get; set; } = string.Empty;
    public bool PreserveAspectRatio { get; set; }
    public bool EnableHardwareAcceleration { get; set; }
    public bool EnableSubMonitorOutput { get; set; }
    public bool IsTestMode { get; set; }
    public bool HideCursor { get; set; }
    public bool BlockMonitorOnEndTime { get; set; }
    public string EndTimeAction { get; set; } = "BlackScreen";
    public string SwitchTiming { get; set; } = "Immediately";
    public bool IsSyncEnabled { get; set; }
    public bool IsLeading { get; set; }
    public List<string> SyncClientIps { get; set; } = new();
    public string LedLeft { get; set; } = "0";
    public string LedWidth { get; set; } = "160";
    public string LedTop { get; set; } = "0";
    public string LedHeight { get; set; } = "90";
    public string LedTransferPort { get; set; } = string.Empty;
    public List<ScheduleRowModel> WeeklySchedules { get; set; } = new();
}
