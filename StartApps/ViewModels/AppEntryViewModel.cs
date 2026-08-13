using System;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using StartApps.Models;

namespace StartApps.ViewModels;

public partial class AppEntryViewModel : ObservableObject
{
    public AppEntryViewModel(AppDefinition definition)
    {
        Definition = definition;
    }

    public AppDefinition Definition { get; }

    public AppExecutionZone Zone => Definition.Zone;

    public string DisplayName => string.IsNullOrWhiteSpace(Definition.Name)
        ? Definition.Type.ToString()
        : Definition.Name;

    public string TypeLabel => Definition.Type switch
    {
        AppType.Rdb => "RDB",
        AppType.Ntp => "NtpServer",
        AppType.Ftp => "FTP",
        AppType.Msg => "MSG",
        AppType.Msg472 => "MSG472",
        AppType.Msg10 => "MSG10",
        _ => "APP"
    };

    public bool IsEnabled
    {
        get => Definition.IsEnabled;
        set
        {
            if (Definition.IsEnabled != value)
            {
                Definition.IsEnabled = value;
                OnPropertyChanged();
            }
        }
    }

    [ObservableProperty]
    private bool _isRunning;

    [ObservableProperty]
    private bool _isProcessing;

    [ObservableProperty]
    private string _status = "대기";

    [ObservableProperty]
    private TimeSpan? _delayRemaining;

    [ObservableProperty]
    private bool _isExternallyRunning;

    partial void OnStatusChanged(string value)
    {
        OnPropertyChanged(nameof(StatusDisplay));
        OnPropertyChanged(nameof(StatusColor));
    }

    partial void OnDelayRemainingChanged(TimeSpan? value)
    {
        OnPropertyChanged(nameof(StatusDisplay));
    }

    public string StatusDisplay => DelayRemaining.HasValue
        ? $"실행 지연 {FormatDelay(DelayRemaining.Value)}"
        : Status;

    public System.Windows.Media.Brush StatusColor => Status switch
    {
        "오류: 실행 파일을 찾을 수 없습니다." => System.Windows.Media.Brushes.OrangeRed,
        "오류: 권한이 부족합니다." => System.Windows.Media.Brushes.OrangeRed,
        "오류: 포트 충돌이 발생했습니다." => System.Windows.Media.Brushes.OrangeRed,
        var value when value.StartsWith("오류") => System.Windows.Media.Brushes.OrangeRed,
        "실행 중" => System.Windows.Media.Brushes.LightGreen,
        _ => System.Windows.Media.Brushes.White
    };

    public void UpdateFrom(AppDefinition other)
    {
        Definition.Name = other.Name;
        Definition.Zone = other.Zone;
        Definition.IsEnabled = other.IsEnabled;
        Definition.Type = other.Type;
        Definition.ExecutablePath = other.ExecutablePath;
        Definition.Arguments = other.Arguments;
        Definition.ShowWindow = other.ShowWindow;
        Definition.WindowStyle = other.WindowStyle;
        Definition.RunAsAdministrator = other.RunAsAdministrator;
        Definition.Port = other.Port;
        Definition.MsgHubPath = other.MsgHubPath;
        Definition.PassivePortRange = other.PassivePortRange;
        Definition.WorkingDirectory = other.WorkingDirectory;
        Definition.WaitForExitBeforeNext = other.WaitForExitBeforeNext;
        Definition.DisplayOrder = other.DisplayOrder;
        Definition.FtpUsername = other.FtpUsername;
        Definition.FtpPassword = other.FtpPassword;
        Definition.FtpHomeDirectory = other.FtpHomeDirectory;
        Definition.FtpAllowRead = other.FtpAllowRead;
        Definition.FtpAllowWrite = other.FtpAllowWrite;
        Definition.DelayMinutes = other.DelayMinutes;
        Definition.DelaySeconds = other.DelaySeconds;
        Definition.RequireNetworkAvailable = other.RequireNetworkAvailable;
        Definition.ToggleShortcutModifiers = other.ToggleShortcutModifiers;
        Definition.ToggleShortcutKey = other.ToggleShortcutKey;
        OnPropertyChanged(nameof(IsEnabled));
        OnPropertyChanged(nameof(DisplayName));
        OnPropertyChanged(nameof(TypeLabel));
        OnPropertyChanged(nameof(Zone));
    }

    private static string FormatDelay(TimeSpan timeSpan)
    {
        if (timeSpan.TotalHours >= 1)
        {
            return timeSpan.ToString(@"hh\:mm\:ss");
        }

        return timeSpan.ToString(@"mm\:ss");
    }
}
