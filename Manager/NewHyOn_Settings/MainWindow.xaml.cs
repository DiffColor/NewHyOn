using NewHyOn.Settings.Wpf.Models;
using NewHyOn.Settings.Wpf.Services;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;

namespace NewHyOn.Settings.Wpf;

public partial class MainWindow : Window
{
    private readonly SettingsRepository _settingsRepository = new();
    private readonly ManagerFirewallRuleService _firewallRuleService = new();
    private bool _isSaving;

    public MainWindow()
    {
        InitializeComponent();
        LoadSettings();
    }

    private void HeaderDragArea_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
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

    private void ZoomButton_OnClick(object sender, RoutedEventArgs e)
    {
    }

    private void CloseButton_OnClick(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void ShowIpButton_OnClick(object sender, RoutedEventArgs e)
    {
        var addresses = SystemInfoService.GetLocalIpv4Addresses();
        var message = addresses.Count == 0
            ? "확인 가능한 IPv4 주소가 없습니다."
            : string.Join(Environment.NewLine, addresses);

        CustomDialog.Show(this, "IP 확인", message, "현재 장비에서 확인된 IPv4 주소입니다.");
    }

    private async void SaveButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (_isSaving)
        {
            return;
        }

        if (TryBuildFormData(out var formData, out var validationMessage) == false)
        {
            CustomDialog.Show(this, "저장 실패", validationMessage, "입력값을 다시 확인해 주세요.");
            return;
        }

        SetSaveInProgress(true);

        try
        {
            await SaveSettingsAsync(formData!);
        }
        catch (Exception ex)
        {
            CustomDialog.Show(this, "저장 실패", $"설정 저장에 실패했습니다.{Environment.NewLine}{ex.Message}", "저장 중 오류가 발생했습니다.");
        }
        finally
        {
            SetSaveInProgress(false);
        }
    }

    private void LoadSettings()
    {
        var settings = _settingsRepository.LoadWithRemotePriority().Data;
        DataServerIpTextBox.Text = settings.DataServerIp;
        MessageServerIpTextBox.Text = settings.MessageServerIp;
        FtpPortTextBox.Text = settings.FtpPort.ToString();
        PasvMinPortTextBox.Text = settings.PasvMinPort.ToString();
        PasvMaxPortTextBox.Text = settings.PasvMaxPort.ToString();
        FtpRootPathTextBox.Text = settings.FtpRootPath;
        PreserveAspectRatioCheckBox.IsChecked = settings.PreserveAspectRatio;
    }

    private bool TryBuildFormData(out SettingsFormData? formData, out string message)
    {
        formData = null;
        message = string.Empty;

        var dataServerIp = DataServerIpTextBox.Text?.Trim() ?? string.Empty;
        var messageServerIp = MessageServerIpTextBox.Text?.Trim() ?? string.Empty;
        var ftpRootPath = FtpRootPathTextBox.Text?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(dataServerIp))
        {
            message = "데이터 서버 IP를 입력해 주세요.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(messageServerIp))
        {
            message = "메시지 서버 IP를 입력해 주세요.";
            return false;
        }

        if (TryParsePort(FtpPortTextBox.Text, out var ftpPort) == false)
        {
            message = "FTP 포트 범위를 확인해주세요.";
            return false;
        }

        if (TryParsePort(PasvMinPortTextBox.Text, out var pasvMinPort) == false ||
            TryParsePort(PasvMaxPortTextBox.Text, out var pasvMaxPort) == false ||
            pasvMinPort > pasvMaxPort)
        {
            message = "패시브 포트 범위를 확인해주세요.";
            return false;
        }

        formData = new SettingsFormData
        {
            DataServerIp = dataServerIp,
            MessageServerIp = messageServerIp,
            FtpPort = ftpPort,
            PasvMinPort = pasvMinPort,
            PasvMaxPort = pasvMaxPort,
            FtpRootPath = ftpRootPath,
            PreserveAspectRatio = PreserveAspectRatioCheckBox.IsChecked == true
        };

        return true;
    }

    private static bool TryParsePort(string? raw, out int value)
    {
        value = 0;
        return int.TryParse(raw, out value) && value > 0 && value <= 65535;
    }

    private async Task SaveSettingsAsync(SettingsFormData formData)
    {
        var saveResult = await Task.Run(() => _settingsRepository.SaveWithRemoteSync(formData));
        string? firewallError = await _firewallRuleService.TryApplyAsync(formData);
        if (saveResult.SavedLocallyOnly)
        {
            string message = saveResult.Message ?? $"데이터서버 저장에 실패했습니다.{Environment.NewLine}설정은 로컬에만 저장했습니다.";
            if (string.IsNullOrWhiteSpace(firewallError) == false)
            {
                message = $"{message}{Environment.NewLine}{Environment.NewLine}{firewallError}";
            }

            var dialogResult = CustomDialog.ShowChoice(
                this,
                "서버 저장 실패",
                message,
                "서버의 상태를 확인해주세요.",
                "종료",
                "닫기");

            if (dialogResult == CustomDialogResult.Primary)
            {
                Close();
                return;
            }

            if (dialogResult == CustomDialogResult.Secondary)
            {
                return;
            }

            return;
        }

        await Task.Run(() => SystemInfoService.TryKillProcess("NewHyOn Manager"));
        if (string.IsNullOrWhiteSpace(firewallError))
        {
            CustomDialog.Show(this, "저장 완료", "설정을 데이터서버와 로컬에 저장했습니다.", "즉시 반영될 수 있도록 프로세스를 정리합니다.");
            Close();
            return;
        }

        CustomDialog.Show(
            this,
            "저장 완료",
            "설정을 데이터서버와 로컬에 저장했습니다.",
            firewallError);
    }

    private void SetSaveInProgress(bool isSaving)
    {
        _isSaving = isSaving;
        SaveButton.IsEnabled = isSaving == false;
        SaveButtonSpinner.Visibility = isSaving ? Visibility.Visible : Visibility.Collapsed;
        SaveButtonTextBlock.Text = isSaving ? "저장 중" : "저장";

        if (isSaving)
        {
            var animation = new DoubleAnimation
            {
                From = 0,
                To = 360,
                Duration = TimeSpan.FromSeconds(0.85),
                RepeatBehavior = RepeatBehavior.Forever
            };

            SaveButtonSpinnerRotateTransform.BeginAnimation(System.Windows.Media.RotateTransform.AngleProperty, animation);
            return;
        }

        SaveButtonSpinnerRotateTransform.BeginAnimation(System.Windows.Media.RotateTransform.AngleProperty, null);
        SaveButtonSpinnerRotateTransform.Angle = 0;
    }
}
