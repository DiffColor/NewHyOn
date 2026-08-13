using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using StartApps.Models;
using StartApps.Services;
using Wpf.Ui.Controls;
using PasswordBox = System.Windows.Controls.PasswordBox;
using TextBox = System.Windows.Controls.TextBox;
using Forms = System.Windows.Forms;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;

namespace StartApps.Views.Dialogs;

public partial class AppSettingsWindow : FluentWindow
{
    private readonly AppDependencyService _dependencyService;
    private readonly GlobalHotkeyService _hotkeyService;
    private readonly IReadOnlyList<AppDefinition> _existingDefinitions;
    private readonly AppDefinition _workingCopy;
    private bool _isUpdatingPathTextBoxes;

    public AppSettingsWindow(
        AppDefinition definition,
        AppDependencyService dependencyService,
        GlobalHotkeyService hotkeyService,
        IReadOnlyCollection<AppDefinition> existingDefinitions)
    {
        InitializeComponent();
        _dependencyService = dependencyService;
        _hotkeyService = hotkeyService;
        _existingDefinitions = existingDefinitions.Select(Clone).ToList();
        _workingCopy = Clone(definition);

        if (string.IsNullOrWhiteSpace(_workingCopy.ExecutablePath)
            && (_workingCopy.Type == AppType.Rdb
                || _workingCopy.Type == AppType.Ntp
                || _workingCopy.Type == AppType.Ftp
                || _workingCopy.Type == AppType.Msg
                || _workingCopy.Type == AppType.Msg472
                || _workingCopy.Type == AppType.Msg10))
        {
            _workingCopy.ExecutablePath = dependencyService.GetExecutablePath(_workingCopy.Type);
        }

        if (_workingCopy.Type == AppType.Ftp && string.IsNullOrWhiteSpace(_workingCopy.FtpHomeDirectory))
        {
            _workingCopy.FtpHomeDirectory = _dependencyService.GetDefaultFtpHomeDirectory();
        }

        DataContext = _workingCopy;
        Loaded += OnLoaded;
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        WindowStyleCombo.ItemsSource = Enum.GetValues<ProcessWindowStyle>();
        FtpPasswordBox.Password = _workingCopy.FtpPassword ?? string.Empty;
        UpdateFtpHomeDirectoryDisplay();
        UpdateExecutablePathDisplay();
        UpdateWorkingDirectoryDisplay();
        UpdateToggleShortcutDisplay();
    }

    public AppDefinition? ResultDefinition { get; private set; }

    private static AppDefinition Clone(AppDefinition definition)
    {
        return new AppDefinition
        {
            Id = definition.Id,
            Name = definition.Name,
            Type = definition.Type,
            Zone = definition.Zone,
            IsEnabled = definition.IsEnabled,
            ExecutablePath = definition.ExecutablePath,
            Arguments = definition.Arguments,
            ShowWindow = definition.ShowWindow,
            WindowStyle = definition.WindowStyle,
            RunAsAdministrator = definition.RunAsAdministrator,
            Port = definition.Port,
            MsgHubPath = definition.MsgHubPath,
            PassivePortRange = definition.PassivePortRange,
            WorkingDirectory = definition.WorkingDirectory,
            WaitForExitBeforeNext = definition.WaitForExitBeforeNext,
            DisplayOrder = definition.DisplayOrder,
            DelayMinutes = definition.DelayMinutes,
            DelaySeconds = definition.DelaySeconds,
            RequireNetworkAvailable = definition.RequireNetworkAvailable,
            ToggleShortcutModifiers = definition.ToggleShortcutModifiers,
            ToggleShortcutKey = definition.ToggleShortcutKey,
            FtpUsername = definition.FtpUsername,
            FtpPassword = definition.FtpPassword,
            FtpHomeDirectory = definition.FtpHomeDirectory,
            FtpAllowRead = definition.FtpAllowRead,
            FtpAllowWrite = definition.FtpAllowWrite
        };
    }

    private void OnSave(object sender, RoutedEventArgs e)
    {
        SyncPathInputs();

        if (string.IsNullOrWhiteSpace(_workingCopy.Name))
        {
            System.Windows.MessageBox.Show(this, "앱 이름을 입력하세요.");
            return;
        }

        if (_workingCopy.Type == AppType.App && string.IsNullOrWhiteSpace(_workingCopy.ExecutablePath))
        {
            System.Windows.MessageBox.Show(this, "실행 파일을 지정하세요.");
            return;
        }

        if (_workingCopy.Type == AppType.Ftp)
        {
            if (string.IsNullOrWhiteSpace(_workingCopy.FtpUsername))
            {
                System.Windows.MessageBox.Show(this, "FTP 사용자 이름을 입력하세요.");
                return;
            }

            if (string.IsNullOrWhiteSpace(_workingCopy.FtpPassword))
            {
                System.Windows.MessageBox.Show(this, "FTP 비밀번호를 입력하세요.");
                return;
            }

            if (string.IsNullOrWhiteSpace(_workingCopy.FtpHomeDirectory))
            {
                System.Windows.MessageBox.Show(this, "FTP 홈 디렉터리를 선택하세요.");
                return;
            }
        }
        else if (_workingCopy.Type == AppType.Msg
                 || _workingCopy.Type == AppType.Msg472
                 || _workingCopy.Type == AppType.Msg10)
        {
            if (_workingCopy.Port is null || _workingCopy.Port <= 0)
            {
                System.Windows.MessageBox.Show(this, "MSG 포트를 입력하세요.");
                return;
            }

            if (string.IsNullOrWhiteSpace(_workingCopy.MsgHubPath))
            {
                System.Windows.MessageBox.Show(this, "허브 경로를 입력하세요.");
                return;
            }
        }

        if (!ValidateToggleShortcut())
        {
            return;
        }

        NormalizeDelayInputs();

        ResultDefinition = _workingCopy;
        DialogResult = true;
    }

    private bool ValidateToggleShortcut()
    {
        if (_workingCopy.ToggleShortcutKey == System.Windows.Input.Key.None)
        {
            return true;
        }

        if (!AppHotkey.TryCreate(_workingCopy.ToggleShortcutModifiers, _workingCopy.ToggleShortcutKey, out var hotkey, out var errorMessage))
        {
            System.Windows.MessageBox.Show(this, errorMessage, "단축키 설정", System.Windows.MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        var duplicatedDefinition = _existingDefinitions
            .Where(x => x.Id != _workingCopy.Id && x.ToggleShortcutKey != System.Windows.Input.Key.None)
            .FirstOrDefault(x =>
                AppHotkey.TryCreate(x.ToggleShortcutModifiers, x.ToggleShortcutKey, out var existingHotkey, out _)
                && existingHotkey == hotkey);

        if (duplicatedDefinition != null)
        {
            var duplicatedName = string.IsNullOrWhiteSpace(duplicatedDefinition.Name)
                ? duplicatedDefinition.Type.ToString()
                : duplicatedDefinition.Name;
            System.Windows.MessageBox.Show(this, $"{duplicatedName} 앱과 동일한 단축키를 사용할 수 없습니다.", "단축키 중복", System.Windows.MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        var availability = _hotkeyService.CanRegister(hotkey, _workingCopy.Id);
        if (!availability.IsAvailable)
        {
            System.Windows.MessageBox.Show(this, availability.ErrorMessage ?? "단축키를 등록할 수 없습니다.", "단축키 등록 실패", System.Windows.MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        return true;
    }

    private void NormalizeDelayInputs()
    {
        if (_workingCopy.DelayMinutes < 0)
        {
            _workingCopy.DelayMinutes = 0;
        }

        if (_workingCopy.DelaySeconds < 0)
        {
            _workingCopy.DelaySeconds = 0;
        }

        if (_workingCopy.DelaySeconds >= 60)
        {
            _workingCopy.DelayMinutes += _workingCopy.DelaySeconds / 60;
            _workingCopy.DelaySeconds = _workingCopy.DelaySeconds % 60;
        }
    }

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void OnBrowseExecutable(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "실행 파일 (*.exe;*.bat)|*.exe;*.bat|모든 파일 (*.*)|*.*"
        };

        if (dialog.ShowDialog(this) == true)
        {
            _workingCopy.ExecutablePath = dialog.FileName;
            UpdateExecutablePathDisplay();
        }
    }

    private void OnBrowseWorkingDirectory(object sender, RoutedEventArgs e)
    {
        using var dialog = new Forms.FolderBrowserDialog();
        if (dialog.ShowDialog() == Forms.DialogResult.OK)
        {
            _workingCopy.WorkingDirectory = dialog.SelectedPath;
            UpdateWorkingDirectoryDisplay();
        }
    }

    private void OnBrowseFtpHome(object sender, RoutedEventArgs e)
    {
        using var dialog = new Forms.FolderBrowserDialog();
        dialog.SelectedPath = _workingCopy.FtpHomeDirectory;
        if (dialog.ShowDialog() == Forms.DialogResult.OK)
        {
            _workingCopy.FtpHomeDirectory = dialog.SelectedPath;
            UpdateFtpHomeDirectoryDisplay();
        }
    }

    private void UpdateFtpHomeDirectoryDisplay()
    {
        var fullPath = _workingCopy.FtpHomeDirectory ?? string.Empty;
        FtpHomeDirectoryBox.Text = BuildPathPreview(fullPath);
        FtpHomeDirectoryBox.ToolTip = fullPath;
    }

    private void UpdateExecutablePathDisplay(bool showFullPath = false)
    {
        UpdatePathTextBox(ExecutablePathBox, _workingCopy.ExecutablePath, showFullPath);
    }

    private void UpdateWorkingDirectoryDisplay(bool showFullPath = false)
    {
        UpdatePathTextBox(WorkingDirectoryBox, _workingCopy.WorkingDirectory, showFullPath);
    }

    private void UpdateToggleShortcutDisplay()
    {
        if (_workingCopy.ToggleShortcutKey == System.Windows.Input.Key.None)
        {
            ToggleShortcutText.Text = "등록 안 됨";
            return;
        }

        if (!AppHotkey.TryCreate(_workingCopy.ToggleShortcutModifiers, _workingCopy.ToggleShortcutKey, out var hotkey, out _))
        {
            ToggleShortcutText.Text = "잘못된 단축키";
            return;
        }

        ToggleShortcutText.Text = hotkey.ToDisplayString();
    }

    private void UpdatePathTextBox(TextBox textBox, string? fullPath, bool showFullPath)
    {
        _isUpdatingPathTextBoxes = true;
        try
        {
            var normalized = fullPath ?? string.Empty;
            textBox.Text = showFullPath ? normalized : BuildPathPreview(normalized);
            textBox.ToolTip = normalized;
        }
        finally
        {
            _isUpdatingPathTextBoxes = false;
        }
    }

    private void SyncPathInputs()
    {
        if (ExecutablePathBox.IsKeyboardFocusWithin)
        {
            _workingCopy.ExecutablePath = ExecutablePathBox.Text;
        }

        if (WorkingDirectoryBox.IsKeyboardFocusWithin)
        {
            _workingCopy.WorkingDirectory = WorkingDirectoryBox.Text;
        }
    }

    private static string BuildPathPreview(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        var normalized = path.Trim().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (string.IsNullOrEmpty(normalized))
        {
            return string.Empty;
        }

        var root = Path.GetPathRoot(normalized);
        if (!string.IsNullOrEmpty(root) &&
            string.Equals(
                root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                normalized,
                StringComparison.OrdinalIgnoreCase))
        {
            return normalized;
        }

        var segments = normalized.Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries);

        if (segments.Length >= 2)
        {
            return $@"...\{segments[^2]}\{segments[^1]}";
        }

        return $@"...\{segments[0]}";
    }

    private void OnExecutablePathBoxGotFocus(object sender, RoutedEventArgs e)
    {
        UpdateExecutablePathDisplay(showFullPath: true);
        ExecutablePathBox.SelectAll();
    }

    private void OnExecutablePathBoxLostFocus(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingPathTextBoxes)
        {
            return;
        }

        _workingCopy.ExecutablePath = ExecutablePathBox.Text;
        UpdateExecutablePathDisplay();
    }

    private void OnWorkingDirectoryBoxGotFocus(object sender, RoutedEventArgs e)
    {
        UpdateWorkingDirectoryDisplay(showFullPath: true);
        WorkingDirectoryBox.SelectAll();
    }

    private void OnWorkingDirectoryBoxLostFocus(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingPathTextBoxes)
        {
            return;
        }

        _workingCopy.WorkingDirectory = WorkingDirectoryBox.Text;
        UpdateWorkingDirectoryDisplay();
    }

    private void OnFtpPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (sender is PasswordBox passwordBox)
        {
            _workingCopy.FtpPassword = passwordBox.Password;
        }
    }

    private void OnCaptureToggleShortcut(object sender, RoutedEventArgs e)
    {
        var captureWindow = new ShortcutCaptureWindow
        {
            Owner = this
        };

        if (captureWindow.ShowDialog() == true && captureWindow.ResultHotkey is { } hotkey)
        {
            _workingCopy.ToggleShortcutModifiers = hotkey.Modifiers;
            _workingCopy.ToggleShortcutKey = hotkey.PrimaryKey;
            UpdateToggleShortcutDisplay();
        }
    }

    private void OnClearToggleShortcut(object sender, RoutedEventArgs e)
    {
        _workingCopy.ToggleShortcutModifiers = ModifierKeys.None;
        _workingCopy.ToggleShortcutKey = System.Windows.Input.Key.None;
        UpdateToggleShortcutDisplay();
    }

    private void OnLaunchFtpInterface(object sender, RoutedEventArgs e)
    {
        try
        {
            string interfacePath;
            string? workingDirectory = _workingCopy.WorkingDirectory;

            if (_workingCopy.Type == AppType.Ftp)
            {
                _dependencyService.ApplyFtpConfiguration(_workingCopy);
                workingDirectory = Path.GetDirectoryName(_dependencyService.GetExecutablePath(AppType.Ftp));
                interfacePath = _dependencyService.GetFtpInterfacePath();
            }
            else
            {
                interfacePath = _dependencyService.GetFtpInterfacePath();
            }

            if (!File.Exists(interfacePath))
            {
                System.Windows.MessageBox.Show(this, "FileZilla 인터페이스 파일을 찾을 수 없습니다. FTP 앱을 추가해 리소스를 내려받으세요.");
                return;
            }

            Process.Start(new ProcessStartInfo(interfacePath)
            {
                UseShellExecute = true,
                WorkingDirectory = workingDirectory ?? Path.GetDirectoryName(interfacePath)
            });
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(this, $"인터페이스 실행 중 오류가 발생했습니다.\n{ex.Message}");
        }
    }
}
