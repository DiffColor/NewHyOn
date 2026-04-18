using NewHyOn.Player.Settings.Models;
using NewHyOn.Player.Settings.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace NewHyOn.Player.Settings;

public partial class MainWindow : Window
{
    private readonly PlayerConfigurationService configurationService = new();
    private CancellationTokenSource? transferServerSyncCancellation;
    private bool isSaving;
    private bool transferServerSyncStarted;
    private bool suppressManagerIpTextChanged;
    private string savedManagerIpAtLoad = string.Empty;
    private string savedPlayerNameAtLoad = string.Empty;

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
        Loaded += MainWindow_Loaded;
        Closed += MainWindow_Closed;
        LoadSnapshot();
    }

    private void LoadSnapshot()
    {
        ConfigPlayerSnapshot snapshot = configurationService.Load();
        savedManagerIpAtLoad = snapshot.ManagerIp;
        savedPlayerNameAtLoad = snapshot.PlayerName;
        suppressManagerIpTextChanged = true;
        ManagerIpTextBox.Text = snapshot.ManagerIp;
        suppressManagerIpTextChanged = false;
        PlayerIpTextBox.Text = snapshot.PlayerIp;
        PlayerNameTextBox.Text = snapshot.PlayerName;
        SourceKeyTextBox.Text = snapshot.SourceKey;
        SignalRPortTextBox.Text = snapshot.SignalRPort;
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
        foreach (ScheduleRowModel row in GetVisibleScheduleRows(snapshot.WeeklySchedules))
        {
            ScheduleRows.Add(row);
        }

        ApplyTransferServerSettings(snapshot);
        ApplyAuthState(snapshot.AuthStatusText, snapshot.IsLicensed, snapshot.IsAuthInputEnabled);
        UpdateSyncUiState();
    }

    private void MainWindow_Loaded(object? sender, RoutedEventArgs e)
    {
        if (transferServerSyncStarted)
        {
            return;
        }

        transferServerSyncStarted = true;
        ScheduleTransferServerSync(
            dataServerAddressOverride: savedManagerIpAtLoad,
            playerNameOverride: savedPlayerNameAtLoad);
    }

    private void MainWindow_Closed(object? sender, EventArgs e)
    {
        CancelTransferServerSync();
    }

    private void ScheduleTransferServerSync(
        int delayMilliseconds = 0,
        string? dataServerAddressOverride = null,
        string? playerNameOverride = null)
    {
        CancelTransferServerSync();

        transferServerSyncCancellation = new CancellationTokenSource();
        StartTransferServerSyncInBackground(
            transferServerSyncCancellation.Token,
            delayMilliseconds,
            dataServerAddressOverride,
            playerNameOverride);
    }

    private void CancelTransferServerSync()
    {
        if (transferServerSyncCancellation == null)
        {
            return;
        }

        if (!transferServerSyncCancellation.IsCancellationRequested)
        {
            transferServerSyncCancellation.Cancel();
        }

        transferServerSyncCancellation.Dispose();
        transferServerSyncCancellation = null;
    }

    private void StartTransferServerSyncInBackground(
        CancellationToken cancellationToken,
        int delayMilliseconds = 0,
        string? dataServerAddressOverride = null,
        string? playerNameOverride = null)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                if (delayMilliseconds > 0)
                {
                    await Task.Delay(delayMilliseconds).ConfigureAwait(false);
                }

                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                await RunTransferServerSyncInBackgroundAsync(
                    cancellationToken,
                    dataServerAddressOverride,
                    playerNameOverride).ConfigureAwait(false);
            }
            catch
            {
            }
        }, CancellationToken.None);
    }

    private async Task RunTransferServerSyncInBackgroundAsync(
        CancellationToken cancellationToken,
        string? dataServerAddressOverride = null,
        string? playerNameOverride = null)
    {
        try
        {
            string dataServerAddress = string.IsNullOrWhiteSpace(dataServerAddressOverride)
                ? string.Empty
                : dataServerAddressOverride.Trim();
            string playerName = string.IsNullOrWhiteSpace(playerNameOverride)
                ? string.Empty
                : playerNameOverride.Trim();

            await Dispatcher.InvokeAsync(() =>
            {
                if (IsLoaded)
                {
                    ApplyTransferServerStatus("저장된 데이터 서버 주소에서 전송 서버 설정을 확인하고 있습니다...", null);
                }

                if (string.IsNullOrWhiteSpace(dataServerAddressOverride))
                {
                    dataServerAddress = ManagerIpTextBox.Text.Trim();
                }

                if (string.IsNullOrWhiteSpace(playerNameOverride))
                {
                    playerName = PlayerNameTextBox.Text.Trim();
                }
            });

            ConfigPlayerSnapshot snapshot = string.IsNullOrWhiteSpace(playerNameOverride)
                ? await configurationService
                    .SyncTransferServerSettingsAsync(dataServerAddress, cancellationToken)
                    .ConfigureAwait(false)
                : await configurationService
                    .SyncStoredPlayerConfigurationFromServerAsync(dataServerAddress, playerName, cancellationToken)
                    .ConfigureAwait(false);

            if (cancellationToken.IsCancellationRequested ||
                Dispatcher.HasShutdownStarted ||
                Dispatcher.HasShutdownFinished)
            {
                return;
            }

            await Dispatcher.InvokeAsync(() =>
            {
                if (!IsLoaded)
                {
                    return;
                }

                if (!string.IsNullOrWhiteSpace(dataServerAddressOverride) &&
                    !string.Equals(ManagerIpTextBox.Text.Trim(), dataServerAddress.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                if (!string.IsNullOrWhiteSpace(playerNameOverride) &&
                    !string.Equals(PlayerNameTextBox.Text.Trim(), playerName.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                ApplySynchronizedServerSnapshot(snapshot);
            });
        }
        catch
        {
            await TryUpdateTransferServerFailureStatusAsync(cancellationToken);
        }
    }

    private async Task TryUpdateTransferServerFailureStatusAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (cancellationToken.IsCancellationRequested ||
                Dispatcher.HasShutdownStarted ||
                Dispatcher.HasShutdownFinished)
            {
                return;
            }

            await Dispatcher.InvokeAsync(() =>
            {
                if (!IsLoaded)
                {
                    return;
                }

                ApplyTransferServerStatus("서버 접속 또는 조회에 실패해 로컬값만 표시합니다.", false);
            });
        }
        catch
        {
        }
    }

    private void ApplyTransferServerSettings(ConfigPlayerSnapshot snapshot)
    {
        MessageServerAddressTextBox.Text = ResolveTransferServerDisplayValue(
            snapshot.LocalMessageServerAddress,
            snapshot.RemoteMessageServerAddress);
        FtpRootPathTextBox.Text = ResolveTransferServerDisplayValue(
            snapshot.LocalFtpRootPath,
            snapshot.RemoteFtpRootPath);

        bool? successState = snapshot.IsTransferServerSyncSuccessful
            ? true
            : snapshot.IsTransferServerStatusError
                ? false
                : null;
        ApplyTransferServerStatus(snapshot.TransferServerStatusText, successState);
    }

    private void ApplyTransferServerStatus(string statusText, bool? successState)
    {
        TransferServerStatusTextBlock.Text = statusText;

        Brush foreground = successState switch
        {
            true => (Brush)FindResource("SuccessBrush"),
            false => Brushes.DarkRed,
            _ => (Brush)FindResource("SecondaryTextBrush")
        };

        TransferServerStatusTextBlock.Foreground = foreground;
    }

    private void ApplySynchronizedServerSnapshot(ConfigPlayerSnapshot snapshot)
    {
        ApplyTransferServerSettings(snapshot);
        FtpPortTextBox.Text = snapshot.FtpPort;

        ScheduleRows.Clear();
        foreach (ScheduleRowModel row in GetVisibleScheduleRows(snapshot.WeeklySchedules))
        {
            ScheduleRows.Add(row);
        }

        ApplyAuthState(snapshot.AuthStatusText, snapshot.IsLicensed, snapshot.IsAuthInputEnabled);
    }

    private static string ResolveTransferServerDisplayValue(string localValue, string remoteValue)
    {
        return string.IsNullOrWhiteSpace(remoteValue)
            ? localValue
            : remoteValue;
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
        Application.Current.Shutdown();
    }

    private async void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (isSaving)
        {
            return;
        }

        SetSaveInProgress(true);

        try
        {
            CancelTransferServerSync();
            List<string> saveWarnings = new();
            string? transferServerWarning = await TryRefreshTransferServerSettingsBeforeSaveAsync();
            if (!string.IsNullOrWhiteSpace(transferServerWarning))
            {
                saveWarnings.Add(transferServerWarning);
            }

            ConfigPlayerSnapshot snapshot = BuildSnapshot();
            configurationService.SaveAll(snapshot);

            string? firewallError = await TryApplyFirewallRulesSafelyAsync(snapshot.SyncPort);
            if (!string.IsNullOrWhiteSpace(firewallError))
            {
                saveWarnings.Add(firewallError);
            }

            if (saveWarnings.Count == 0)
            {
                CustomDialog.Show(this, "저장 완료", "플레이어 정보를 저장했습니다.", "설정이 정상적으로 반영되었습니다.");
                Close();
                return;
            }

            string warningSubtitle = string.Join(Environment.NewLine, saveWarnings.Distinct());
            CustomDialogResult dialogResult = CustomDialog.ShowChoice(
                this,
                "로컬 저장 완료",
                "플레이어 정보를 로컬에 저장했습니다.",
                warningSubtitle,
                "종료",
                "닫기");

            if (dialogResult == CustomDialogResult.Primary)
            {
                Close();
            }
        }
        catch (Exception ex)
        {
            CustomDialog.Show(
                this,
                "저장 실패",
                "설정을 저장하지 못했습니다.",
                SimplifyDialogIssue(ex.Message, "저장 중 문제가 발생했습니다."));
        }
        finally
        {
            SetSaveInProgress(false);
            if (IsLoaded)
            {
                ScheduleTransferServerSync();
            }
        }
    }

    private void SetSaveInProgress(bool inProgress)
    {
        isSaving = inProgress;
        SaveButton.IsEnabled = inProgress == false;
        ExitButton.IsEnabled = inProgress == false;
        ShowIpButton.IsEnabled = inProgress == false;
        MainContentGrid.IsHitTestVisible = inProgress == false;
        SaveButtonSpinner.Visibility = inProgress ? Visibility.Visible : Visibility.Collapsed;
        SaveButtonTextBlock.Text = inProgress ? "저장 중" : "저 장";

        if (inProgress)
        {
            DoubleAnimation animation = new()
            {
                From = 0,
                To = 360,
                Duration = TimeSpan.FromSeconds(0.85),
                RepeatBehavior = RepeatBehavior.Forever
            };

            SaveButtonSpinnerRotateTransform.BeginAnimation(RotateTransform.AngleProperty, animation);
            return;
        }

        SaveButtonSpinnerRotateTransform.BeginAnimation(RotateTransform.AngleProperty, null);
        SaveButtonSpinnerRotateTransform.Angle = 0;
    }

    private async Task<string?> TryRefreshTransferServerSettingsBeforeSaveAsync()
    {
        try
        {
            ApplyTransferServerStatus("입력된 데이터 서버 주소로 전송 서버 설정을 다시 확인하고 있습니다...", null);
            ConfigPlayerSnapshot transferSnapshot = await configurationService
                .SyncTransferServerSettingsAsync(ManagerIpTextBox.Text.Trim());

            ApplyTransferServerSettings(transferSnapshot);
            if (!string.IsNullOrWhiteSpace(transferSnapshot.FtpPort))
            {
                FtpPortTextBox.Text = transferSnapshot.FtpPort;
            }

            return transferSnapshot.IsTransferServerStatusError
                ? SimplifyDialogIssue(transferSnapshot.TransferServerStatusText, "데이터 서버 조회 실패")
                : null;
        }
        catch (Exception ex)
        {
            string message = SimplifyDialogIssue(ex.Message, "데이터 서버 조회 실패");
            ApplyTransferServerStatus(message, false);
            return message;
        }
    }

    private async Task<string?> TryApplyFirewallRulesSafelyAsync(string syncPort)
    {
        try
        {
            return await configurationService.TryApplyFirewallRulesAsync(syncPort);
        }
        catch (Exception ex)
        {
            return SimplifyDialogIssue(ex.Message, "방화벽 설정 실패");
        }
    }

    private void ManagerIpTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (suppressManagerIpTextChanged || !IsLoaded)
        {
            return;
        }

        ApplyTransferServerStatus("입력된 데이터 서버 주소로 전송 서버 설정을 확인하고 있습니다...", null);
        ScheduleTransferServerSync(500);
    }

    private void AuthButton_Click(object sender, RoutedEventArgs e)
    {
        AuthResult result = configurationService.Authenticate(AuthPasswordBox.Password);
        ApplyAuthState(result.StatusText, result.IsLicensed, !result.DisablePasswordInput);
        CustomDialog.Show(
            this,
            result.Success ? "인증 완료" : "인증 실패",
            result.Message,
            result.Success ? result.StatusText : SimplifyDialogIssue(result.StatusText, "인증 실패"));
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

        CustomDialog.Show(this, "주소 확인", message, "현재 장비에서 확인된 IPv4 주소입니다.");
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
            CustomDialog.Show(this, "입력 필요", "클라이언트 주소를 입력해주세요.", "동기화 클라이언트 추가를 진행할 수 없습니다.");
            return;
        }

        if (!IPAddress.TryParse(ipText, out _))
        {
            CustomDialog.Show(this, "입력 오류", "올바른 클라이언트 주소를 입력해주세요.", "IPv4 형식을 확인해 주세요.");
            return;
        }

        if (SyncClientIps.Contains(ipText, StringComparer.OrdinalIgnoreCase))
        {
            CustomDialog.Show(this, "중복 등록", "이미 등록된 클라이언트 주소입니다.", "기존 목록을 먼저 확인해 주세요.");
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
        if (string.IsNullOrWhiteSpace(statusText))
        {
            statusText = "인증 상태 : 미인증";
            isLicensed = false;
            authInputEnabled = true;
        }

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
            LocalDataServerAddress = ManagerIpTextBox.Text.Trim(),
            LocalMessageServerAddress = MessageServerAddressTextBox.Text.Trim(),
            LocalFtpRootPath = FtpRootPathTextBox.Text.Trim(),
            LocalFtpPort = FtpPortTextBox.Text.Trim(),
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

    private static string SimplifyDialogIssue(string? rawMessage, string fallback)
    {
        if (string.IsNullOrWhiteSpace(rawMessage))
        {
            return fallback;
        }

        string normalized = string.Join(
            " ",
            rawMessage
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim())
                .Where(x => string.IsNullOrWhiteSpace(x) == false));

        if (string.IsNullOrWhiteSpace(normalized))
        {
            return fallback;
        }

        if (normalized.Contains("데이터 서버 주소 형식이 올바르지", StringComparison.Ordinal))
        {
            return "데이터 서버 주소 형식 오류";
        }

        if (normalized.Contains("데이터 서버 접속 또는 조회에 실패", StringComparison.Ordinal))
        {
            return ExtractFailureDetail(normalized, "데이터 서버 조회 실패");
        }

        if (normalized.Contains("데이터 서버 조회", StringComparison.Ordinal))
        {
            return ExtractFailureDetail(normalized, "데이터 서버 조회 실패");
        }

        if (normalized.Contains("NewHyOn 데이터베이스", StringComparison.Ordinal))
        {
            return "데이터 서버에 NewHyOn 데이터베이스가 없습니다.";
        }

        if (normalized.Contains("ServerSettings 테이블", StringComparison.Ordinal))
        {
            return "데이터 서버에 ServerSettings 테이블이 없습니다.";
        }

        if (normalized.Contains("원격 전송 서버 설정을 찾지 못", StringComparison.Ordinal))
        {
            return "전송 서버 설정을 찾지 못했습니다.";
        }

        if (normalized.Contains("방화벽", StringComparison.Ordinal))
        {
            return ExtractFailureDetail(normalized, "방화벽 설정 실패");
        }

        return normalized;
    }

    private static string ExtractFailureDetail(string message, string label)
    {
        int start = message.IndexOf('(');
        int end = message.LastIndexOf(')');
        if (start >= 0 && end > start)
        {
            string detail = message[(start + 1)..end].Trim();
            if (!string.IsNullOrWhiteSpace(detail))
            {
                return $"{label}: {detail}";
            }
        }

        int colonIndex = message.IndexOf(':');
        if (colonIndex >= 0 && colonIndex < message.Length - 1)
        {
            string detail = message[(colonIndex + 1)..].Trim();
            if (!string.IsNullOrWhiteSpace(detail))
            {
                return $"{label}: {detail}";
            }
        }

        return label;
    }

    private static IReadOnlyList<ScheduleRowModel> GetVisibleScheduleRows(IEnumerable<ScheduleRowModel>? source)
    {
        Dictionary<string, ScheduleRowModel> existing = (source ?? Array.Empty<ScheduleRowModel>())
            .Where(x => x != null && !string.IsNullOrWhiteSpace(x.DayCode))
            .ToDictionary(x => x.DayCode, x => x, StringComparer.OrdinalIgnoreCase);

        ScheduleRowModel[] defaults =
        {
            new() { DayCode = "SUN", DayLabel = "일요일" },
            new() { DayCode = "MON", DayLabel = "월요일" },
            new() { DayCode = "TUE", DayLabel = "화요일" },
            new() { DayCode = "WED", DayLabel = "수요일" },
            new() { DayCode = "THU", DayLabel = "목요일" },
            new() { DayCode = "FRI", DayLabel = "금요일" },
            new() { DayCode = "SAT", DayLabel = "토요일" }
        };

        return defaults
            .Select(row => existing.TryGetValue(row.DayCode, out ScheduleRowModel? current)
                ? current
                : row)
            .ToList();
    }
}
