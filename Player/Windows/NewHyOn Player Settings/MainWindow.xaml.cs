using NewHyOn.Player.Settings.Models;
using NewHyOn.Player.Settings.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace NewHyOn.Player.Settings;

public partial class MainWindow : Window
{
    private readonly PlayerConfigurationService configurationService = new();

    public ObservableCollection<ScheduleRowModel> ScheduleRows { get; } = new();
    public ObservableCollection<string> SyncClientIps { get; } = new();

    public IReadOnlyList<int> HourOptions { get; } = Enumerable.Range(0, 24).ToList();
    public IReadOnlyList<int> MinuteOptions { get; } = Enumerable.Range(0, 60).ToList();
    public IReadOnlyList<string> EndTimeActions { get; } = new[]
    {
        "SystemOff",
        "SystemReboot",
        "ApplicationClose",
        "BlackScreen",
        "Hibernation"
    };

    public IReadOnlyList<string> SwitchTimingOptions { get; } = new[]
    {
        "Immediately",
        "PageEnd",
        "ContentEnd"
    };

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;
        LoadSnapshot();
    }

    private void LoadSnapshot()
    {
        ConfigPlayerSnapshot snapshot = configurationService.Load();
        ManagerIpTextBox.Text = snapshot.ManagerIp;
        PlayerIpTextBox.Text = snapshot.PlayerIp;
        PlayerNameTextBox.Text = snapshot.PlayerName;
        SourceKeyTextBox.Text = snapshot.SourceKey;
        SignalRPortTextBox.Text = LegacyNetworkService.SIGNALR_PORT.ToString();
        FtpPortTextBox.Text = snapshot.FtpPort;
        SyncPortTextBox.Text = snapshot.SyncPort;
        LedLeftTextBox.Text = snapshot.LedLeft;
        LedWidthTextBox.Text = snapshot.LedWidth;
        LedTopTextBox.Text = snapshot.LedTop;
        LedHeightTextBox.Text = snapshot.LedHeight;
        LedTransferPortTextBox.Text = snapshot.LedTransferPort;

        EndTimeActionComboBox.SelectedItem = snapshot.EndTimeAction;
        SwitchTimingComboBox.SelectedItem = snapshot.SwitchTiming;

        PreserveAspectRatioCheckBox.IsChecked = snapshot.PreserveAspectRatio;
        HwAccelerationCheckBox.IsChecked = snapshot.EnableHardwareAcceleration;
        SubOutputModeCheckBox.IsChecked = snapshot.EnableSubMonitorOutput;
        TestModeCheckBox.IsChecked = snapshot.IsTestMode;
        HideCursorCheckBox.IsChecked = snapshot.HideCursor;
        MonitorBlockCheckBox.IsChecked = snapshot.BlockMonitorOnEndTime;
        SyncEnabledCheckBox.IsChecked = snapshot.IsSyncEnabled;
        IsLeadingCheckBox.IsChecked = snapshot.IsLeading;

        SyncClientIps.Clear();
        foreach (string ip in snapshot.SyncClientIps)
        {
            SyncClientIps.Add(ip);
        }

        ScheduleRows.Clear();
        foreach (ScheduleRowModel row in snapshot.WeeklySchedules)
        {
            ScheduleRows.Add(row);
        }

        ApplyAuthState(snapshot.AuthStatusText, snapshot.IsLicensed, snapshot.IsAuthInputEnabled);
        UpdateSyncUiState();
    }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void TitleBar_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            return;
        }

        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void MinimizeButton_OnClick(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void CloseButton_OnClick(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void ExitButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private async void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            ConfigPlayerSnapshot snapshot = BuildSnapshot();
            configurationService.SaveAll(snapshot);
            string? firewallError = await configurationService.TryApplyFirewallRulesAsync(snapshot.SyncPort);
            if (string.IsNullOrWhiteSpace(firewallError))
            {
                CustomDialog.Show(this, "저장 완료", "플레이어 정보를 저장했습니다.", "설정이 정상적으로 반영되었습니다.");
                Close();
                return;
            }

            CustomDialog.Show(
                this,
                "저장 완료",
                "플레이어 정보를 저장했습니다.",
                firewallError);
        }
        catch (Exception ex)
        {
            CustomDialog.Show(this, "저장 실패", ex.Message, "입력값과 환경을 다시 확인해 주세요.");
        }
    }

    private async void PortSaveButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            configurationService.SavePorts(FtpPortTextBox.Text, SyncPortTextBox.Text);
            string? firewallError = await configurationService.TryApplyFirewallRulesAsync(SyncPortTextBox.Text);
            if (string.IsNullOrWhiteSpace(firewallError))
            {
                CustomDialog.Show(this, "저장 완료", "포트번호를 저장했습니다.", "네트워크 설정을 반영했습니다.");
                return;
            }

            CustomDialog.Show(
                this,
                "저장 완료",
                "포트번호를 저장했습니다.",
                firewallError);
        }
        catch (Exception ex)
        {
            CustomDialog.Show(this, "저장 실패", ex.Message, "포트 값을 다시 확인해 주세요.");
        }
    }

    private void AuthButton_Click(object sender, RoutedEventArgs e)
    {
        AuthResult result = configurationService.Authenticate(AuthPasswordBox.Password);
        ApplyAuthState(result.StatusText, result.IsLicensed, !result.DisablePasswordInput);
        CustomDialog.Show(this, result.Success ? "인증 완료" : "인증 실패", result.Message, result.StatusText);
        if (result.Success)
        {
            AuthPasswordBox.Password = string.Empty;
        }
    }

    private void ShowIpButton_Click(object sender, RoutedEventArgs e)
    {
        IReadOnlyCollection<string> addresses = SystemInfoService.GetLocalIpv4Addresses();
        string message = addresses.Count == 0
            ? "확인 가능한 IPv4 주소가 없습니다."
            : string.Join(Environment.NewLine, addresses);

        CustomDialog.Show(this, "IP 확인", message, "현재 장비에서 확인된 IPv4 주소입니다.");
    }

    private void SyncStateChanged(object sender, RoutedEventArgs e)
    {
        UpdateSyncUiState();
    }

    private void SyncIpAddButton_Click(object sender, RoutedEventArgs e)
    {
        string ipText = SyncIpTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(ipText))
        {
            CustomDialog.Show(this, "입력 필요", "IP 주소를 입력해주세요.", "동기화 클라이언트 추가를 진행할 수 없습니다.");
            return;
        }

        if (!IPAddress.TryParse(ipText, out _))
        {
            CustomDialog.Show(this, "입력 오류", "올바른 IP 주소를 입력해주세요.", "IPv4 형식을 확인해 주세요.");
            return;
        }

        if (SyncClientIps.Contains(ipText, StringComparer.OrdinalIgnoreCase))
        {
            CustomDialog.Show(this, "중복 등록", "이미 등록된 IP 주소입니다.", "기존 목록을 먼저 확인해 주세요.");
            return;
        }

        SyncClientIps.Add(ipText);
        SyncIpTextBox.Text = string.Empty;
        configurationService.PersistSyncClientIps(SyncClientIps);
    }

    private void SyncIpDeleteButton_Click(object sender, RoutedEventArgs e)
    {
        string ipText = SyncIpTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(ipText) && SyncIpListBox.SelectedItem is string selectedIp)
        {
            ipText = selectedIp;
        }

        if (string.IsNullOrWhiteSpace(ipText))
        {
            return;
        }

        string? target = SyncClientIps.FirstOrDefault(x => x.Equals(ipText, StringComparison.OrdinalIgnoreCase));
        if (target == null)
        {
            return;
        }

        SyncClientIps.Remove(target);
        SyncIpTextBox.Text = string.Empty;
        configurationService.PersistSyncClientIps(SyncClientIps);
    }

    private void SyncIpListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SyncIpListBox.SelectedItem is string selectedIp)
        {
            SyncIpTextBox.Text = selectedIp;
        }
    }

    private void SetAllButton_Click(object sender, RoutedEventArgs e)
    {
        if (ScheduleRows.Count == 0)
        {
            return;
        }

        ScheduleRowModel source = ScheduleRows[0];
        for (int index = 1; index < ScheduleRows.Count; index++)
        {
            ScheduleRows[index].StartHour = source.StartHour;
            ScheduleRows[index].StartMinute = source.StartMinute;
            ScheduleRows[index].EndHour = source.EndHour;
            ScheduleRows[index].EndMinute = source.EndMinute;
        }

        ScheduleRows.Clear();
        foreach (ScheduleRowModel row in BuildSnapshot().WeeklySchedules)
        {
            ScheduleRows.Add(row);
        }
    }

    private void UpdateSyncUiState()
    {
        bool syncEnabled = SyncEnabledCheckBox.IsChecked == true;
        IsLeadingCheckBox.IsEnabled = syncEnabled;
        SyncPortTextBox.IsEnabled = syncEnabled;

        bool enableClients = syncEnabled && IsLeadingCheckBox.IsChecked == true;
        SyncIpListBox.IsEnabled = enableClients;
        SyncIpTextBox.IsEnabled = enableClients;
        SyncIpAddButton.IsEnabled = enableClients;
        SyncIpDeleteButton.IsEnabled = enableClients;
    }

    private void ApplyAuthState(string statusText, bool isLicensed, bool authInputEnabled)
    {
        AuthStatusTextBlock.Text = statusText;

        Brush color = isLicensed ? (Brush)FindResource("SuccessBrush") : Brushes.DarkRed;
        AuthStatusTextBlock.Foreground = color;

        AuthPasswordBox.IsEnabled = authInputEnabled;
        AuthButton.IsEnabled = authInputEnabled;
    }

    private ConfigPlayerSnapshot BuildSnapshot()
    {
        return new ConfigPlayerSnapshot
        {
            ManagerIp = ManagerIpTextBox.Text.Trim(),
            PlayerIp = PlayerIpTextBox.Text.Trim(),
            PlayerName = PlayerNameTextBox.Text.Trim(),
            SourceKey = SourceKeyTextBox.Text.Trim(),
            SignalRPort = LegacyNetworkService.SIGNALR_PORT.ToString(),
            FtpPort = FtpPortTextBox.Text.Trim(),
            SyncPort = SyncPortTextBox.Text.Trim(),
            PreserveAspectRatio = PreserveAspectRatioCheckBox.IsChecked == true,
            EnableHardwareAcceleration = HwAccelerationCheckBox.IsChecked == true,
            EnableSubMonitorOutput = SubOutputModeCheckBox.IsChecked == true,
            IsTestMode = TestModeCheckBox.IsChecked == true,
            HideCursor = HideCursorCheckBox.IsChecked == true,
            BlockMonitorOnEndTime = MonitorBlockCheckBox.IsChecked == true,
            EndTimeAction = EndTimeActionComboBox.SelectedItem?.ToString() ?? "BlackScreen",
            SwitchTiming = SwitchTimingComboBox.SelectedItem?.ToString() ?? "Immediately",
            IsSyncEnabled = SyncEnabledCheckBox.IsChecked == true,
            IsLeading = IsLeadingCheckBox.IsChecked == true,
            SyncClientIps = SyncClientIps.ToList(),
            LedLeft = LedLeftTextBox.Text.Trim(),
            LedWidth = LedWidthTextBox.Text.Trim(),
            LedTop = LedTopTextBox.Text.Trim(),
            LedHeight = LedHeightTextBox.Text.Trim(),
            LedTransferPort = LedTransferPortTextBox.Text.Trim(),
            WeeklySchedules = ScheduleRows
                .Select(row => new ScheduleRowModel
                {
                    DayCode = row.DayCode,
                    DayLabel = row.DayLabel,
                    IsOnAir = row.IsOnAir,
                    StartHour = row.StartHour,
                    StartMinute = row.StartMinute,
                    EndHour = row.EndHour,
                    EndMinute = row.EndMinute
                })
                .ToList()
        };
    }
}
