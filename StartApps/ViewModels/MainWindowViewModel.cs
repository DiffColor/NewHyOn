using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StartApps.Models;
using StartApps.Services;
using Wpf.Ui.Appearance;

namespace StartApps.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    private readonly AppManager _appManager;
    private readonly AppDependencyService _dependencyService;
    private readonly AppProfile _profile;
    private bool _initialized;
    private bool _isSequentialQueueProcessing;
    private readonly Dictionary<Guid, CancellationTokenSource> _startCancellationTokens = new();
    private readonly object _startCancellationGate = new();
    private static readonly TimeSpan DelayTickInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan NetworkRetryInterval = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan ProcessCheckInterval = TimeSpan.FromMinutes(1);
    private const string ExternalProcessStatus = "다른 프로세스 실행 중";
    private DateTimeOffset _nextProcessCheckAt;
    private CancellationTokenSource? _processMonitorCts;

    public ObservableCollection<AppEntryViewModel> ParallelApps { get; } = new();
    public ObservableCollection<AppEntryViewModel> SequentialApps { get; } = new();

    [ObservableProperty]
    private string _appTitle = "StartApps";

    [ObservableProperty]
    private int _activeAppCount;

    [ObservableProperty]
    private TimeSpan _parallelCheckRemaining = TimeSpan.Zero;

    [ObservableProperty]
    private TimeSpan _sequentialCheckRemaining = TimeSpan.Zero;

    public string NextParallelCheckDisplay => $"다음 체크 {FormatCountdown(ParallelCheckRemaining)}";

    public string NextSequentialCheckDisplay => $"다음 체크 {FormatCountdown(SequentialCheckRemaining)}";

    [ObservableProperty]
    private bool _canAddFtp = true;

    partial void OnParallelCheckRemainingChanged(TimeSpan value)
    {
        OnPropertyChanged(nameof(NextParallelCheckDisplay));
    }

    partial void OnSequentialCheckRemainingChanged(TimeSpan value)
    {
        OnPropertyChanged(nameof(NextSequentialCheckDisplay));
    }

    public MainWindowViewModel(AppManager appManager, AppDependencyService dependencyService, AppProfile profile)
    {
        _appManager = appManager;
        _dependencyService = dependencyService;
        _profile = profile;
        AppTitle = _profile.DisplayName;
        _appManager.RuntimeStateChanged += OnRuntimeStateChanged;
    }

    public async Task InitializeAsync()
    {
        if (_initialized)
        {
            return;
        }

        _initialized = true;
        var definitions = (await _appManager.LoadAsync()).ToList();
        if (definitions.Count == 0)
        {
            definitions.AddRange(_dependencyService.CreateDefaultAppDefinitions());
            await _appManager.SaveAsync(definitions);
        }

        var ordered = definitions
            .Where(d => d.Zone != AppExecutionZone.Sequential)
            .OrderBy(d => d.DisplayOrder)
            .ThenBy(d => d.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(d => d.Id)
            .Concat(definitions
                .Where(d => d.Zone == AppExecutionZone.Sequential)
                .OrderBy(d => d.DisplayOrder)
                .ThenBy(d => d.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(d => d.Id))
            .ToList();

        var requiresSave = false;
        foreach (var definition in ordered)
        {
            requiresSave |= await PrepareDefinitionAsync(definition);
            var entry = AddEntry(definition);
            if (_appManager.TryAttachToRunningProcess(definition))
            {
                entry.IsRunning = true;
                entry.IsExternallyRunning = true;
                entry.Status = "실행 중";
            }
        }

        if (requiresSave)
        {
            await _appManager.SaveAsync(ordered);
        }

        RefreshCounters();
        RefreshFtpState();

        foreach (var entry in EnumerateEntries().Where(e => e.IsEnabled && e.Definition.Zone != AppExecutionZone.Sequential))
        {
            if (_appManager.IsRunning(entry.Definition.Id))
            {
                entry.Status = "실행 중";
                continue;
            }

            _ = StartEntryAsync(entry, persist: false);
        }

        await EnsureSequentialExecutionAsync();
        StartProcessMonitor();
    }

    [RelayCommand]
    private void ToggleTheme()
    {
        var current = ApplicationThemeManager.GetAppTheme();
        var next = current == ApplicationTheme.Dark ? ApplicationTheme.Light : ApplicationTheme.Dark;
        ApplicationThemeManager.Apply(next);
    }

    public AppEntryViewModel AddEntry(AppDefinition definition)
    {
        var entry = new AppEntryViewModel(definition);
        var targetCollection = definition.Zone == AppExecutionZone.Sequential ? SequentialApps : ParallelApps;
        targetCollection.Add(entry);
        RefreshFtpState();
        return entry;
    }

    public async Task<AppEntryViewModel> AddOrUpdateAsync(AppDefinition definition)
    {
        await PrepareDefinitionAsync(definition);

        var existing = EnumerateEntries().FirstOrDefault(e => e.Definition.Id == definition.Id);
        if (existing == null)
        {
            var created = AddEntry(definition);
            await SaveStateAsync();
            RefreshCounters();
            return created;
        }

        var previousZone = existing.Definition.Zone;
        existing.UpdateFrom(definition);

        if (previousZone != existing.Definition.Zone)
        {
            var oldCollection = previousZone == AppExecutionZone.Sequential ? SequentialApps : ParallelApps;
            var newCollection = existing.Definition.Zone == AppExecutionZone.Sequential ? SequentialApps : ParallelApps;
            oldCollection.Remove(existing);
            newCollection.Add(existing);
        }

        await SaveStateAsync();
        RefreshCounters();
        RefreshFtpState();
        return existing;
    }

    public IEnumerable<AppEntryViewModel> EnumerateEntries()
    {
        foreach (var entry in ParallelApps)
        {
            yield return entry;
        }

        foreach (var entry in SequentialApps)
        {
            yield return entry;
        }
    }

    public async Task ToggleAppAsync(AppEntryViewModel entry)
    {
        var hasPendingStart = HasPendingStart(entry);
        if (entry.IsProcessing && !hasPendingStart)
        {
            return;
        }

        if (!entry.IsEnabled)
        {
            entry.IsEnabled = true;
            RefreshCounters();
            if (entry.Definition.Zone == AppExecutionZone.Sequential)
            {
                await EnsureSequentialExecutionAsync(isUserInitiated: true);
            }
            else
            {
                await StartEntryAsync(entry, isUserInitiated: true);
            }
        }
        else
        {
            entry.IsEnabled = false;
            entry.DelayRemaining = null;

            var canceled = TryCancelPendingStart(entry);
            if (entry.IsRunning)
            {
                await StopEntryAsync(entry);
                return;
            }

            entry.Status = canceled ? "실행이 취소되었습니다." : "중지";
            await SaveStateAsync();
            RefreshCounters();
            if (entry.Definition.Zone == AppExecutionZone.Sequential)
            {
                await EnsureSequentialExecutionAsync();
            }
        }
    }

    public async Task StartEntryAsync(AppEntryViewModel entry, bool persist = true, bool isUserInitiated = false)
    {
        if (!entry.IsEnabled)
        {
            return;
        }

        if (entry.IsProcessing)
        {
            return;
        }

        entry.IsExternallyRunning = false;
        entry.IsProcessing = true;
        var cancellation = RegisterStartCancellation(entry.Definition.Id);
        var token = cancellation.Token;
        entry.DelayRemaining = null;

        try
        {
            if (_appManager.IsRunning(entry.Definition.Id))
            {
                entry.IsRunning = true;
                entry.Status = "실행 중";
                return;
            }

            var blockedByExternalProcess = await ShouldBlockStartForExternalProcessAsync(entry, isUserInitiated, token);
            if (blockedByExternalProcess)
            {
                return;
            }

            if (entry.Definition.RequireNetworkAvailable)
            {
                await WaitForNetworkAsync(entry, token);
            }

            token.ThrowIfCancellationRequested();

            entry.Status = "실행 준비 중";
            var delay = GetConfiguredDelay(entry.Definition);
            if (delay > TimeSpan.Zero)
            {
                entry.Status = "실행 지연 중";
                await RunDelayCountdownAsync(entry, delay, token);
            }

            token.ThrowIfCancellationRequested();

            if (!entry.IsEnabled)
            {
                return;
            }

            await _appManager.StartAsync(entry.Definition, token);
            entry.IsRunning = true;
            entry.Status = "실행 중";
        }
        catch (OperationCanceledException)
        {
            entry.Status = "실행이 취소되었습니다.";
        }
        catch (Exception ex)
        {
            entry.Status = MapErrorMessage(ex);
        }
        finally
        {
            entry.DelayRemaining = null;
            ReleaseStartCancellation(entry.Definition.Id, cancellation);
            entry.IsProcessing = false;
            if (persist)
            {
                await SaveStateAsync();
            }
            RefreshCounters();
            if (entry.Definition.Zone == AppExecutionZone.Sequential)
            {
                await EnsureSequentialExecutionAsync();
            }
        }
    }

    public async Task StopEntryAsync(AppEntryViewModel entry)
    {
        TryCancelPendingStart(entry);
        entry.DelayRemaining = null;
        entry.IsProcessing = true;
        try
        {
            _appManager.Stop(entry.Definition, forceKill: true);
            entry.IsRunning = false;
            entry.Status = "중지";
        }
        finally
        {
            entry.IsProcessing = false;
            await SaveStateAsync();
            RefreshCounters();
            if (entry.Definition.Zone == AppExecutionZone.Sequential)
            {
                await EnsureSequentialExecutionAsync();
            }
        }
    }

    public async Task MoveAppAsync(AppEntryViewModel entry, AppExecutionZone zone, int targetIndex)
    {
        var sourceCollection = entry.Definition.Zone == AppExecutionZone.Sequential ? SequentialApps : ParallelApps;
        sourceCollection.Remove(entry);

        entry.Definition.Zone = zone;
        var targetCollection = zone == AppExecutionZone.Sequential ? SequentialApps : ParallelApps;

        if (targetIndex < 0 || targetIndex >= targetCollection.Count)
        {
            targetCollection.Add(entry);
        }
        else
        {
            targetCollection.Insert(targetIndex, entry);
        }

        await SaveStateAsync();
    }

    public async Task DeleteEntryAsync(AppEntryViewModel entry)
    {
        var hasPendingStart = HasPendingStart(entry);
        if (entry.IsProcessing && !hasPendingStart)
        {
            return;
        }

        TryCancelPendingStart(entry);
        entry.DelayRemaining = null;

        if (entry.IsRunning)
        {
            _appManager.Stop(entry.Definition, forceKill: true);
            entry.IsRunning = false;
            entry.Status = "중지";
        }

        entry.IsEnabled = false;

        var sourceCollection = entry.Definition.Zone == AppExecutionZone.Sequential ? SequentialApps : ParallelApps;
        sourceCollection.Remove(entry);

        await SaveStateAsync();
        RefreshCounters();
        RefreshFtpState();
        if (entry.Definition.Zone == AppExecutionZone.Sequential)
        {
            await EnsureSequentialExecutionAsync();
        }
    }

    public async Task ApplyEntryStateAsync(AppEntryViewModel entry)
    {
        if (!entry.IsEnabled)
        {
            TryCancelPendingStart(entry);
            entry.DelayRemaining = null;
            if (entry.IsRunning)
            {
                await StopEntryAsync(entry);
            }
            else if (entry.Definition.Zone == AppExecutionZone.Sequential)
            {
                await EnsureSequentialExecutionAsync();
            }
            return;
        }

        if (entry.Definition.Zone == AppExecutionZone.Sequential)
        {
            await EnsureSequentialExecutionAsync(isUserInitiated: true);
        }
        else if (!entry.IsRunning)
        {
            await StartEntryAsync(entry, isUserInitiated: true);
        }
    }

    private Task<bool> PrepareDefinitionAsync(AppDefinition definition)
    {
        var changed = false;

        if (definition.Type == AppType.Ftp)
        {
            changed |= ApplyRunAsAdministratorDefault(definition, defaultValue: true);
            if (definition.Port is null or 21)
            {
                definition.Port = AppDependencyService.DefaultFtpPort;
                changed = true;
            }
            if (string.IsNullOrWhiteSpace(definition.PassivePortRange))
            {
                definition.PassivePortRange = "24000-24240";
            }
            if (string.IsNullOrWhiteSpace(definition.FtpHomeDirectory))
            {
                definition.FtpHomeDirectory = _dependencyService.GetDefaultFtpHomeDirectory();
            }
            else
            {
                Directory.CreateDirectory(definition.FtpHomeDirectory);
            }
            definition.WorkingDirectory = Path.GetDirectoryName(_dependencyService.GetExecutablePath(AppType.Ftp));
            definition.ExecutablePath = _dependencyService.GetExecutablePath(AppType.Ftp);
        }
        else if (definition.Type == AppType.Msg
                 || definition.Type == AppType.Msg472
                 || definition.Type == AppType.Msg90)
        {
            changed |= ApplyRunAsAdministratorDefault(definition, defaultValue: false);
            definition.Port ??= 5000;
            if (string.IsNullOrWhiteSpace(definition.MsgHubPath))
            {
                definition.MsgHubPath = "/Data";
            }
            definition.WorkingDirectory = Path.GetDirectoryName(_dependencyService.GetExecutablePath(definition.Type));
            definition.ExecutablePath = _dependencyService.GetExecutablePath(definition.Type);
        }
        else if (definition.Type == AppType.Rdb && string.IsNullOrWhiteSpace(definition.ExecutablePath))
        {
            changed |= ApplyRunAsAdministratorDefault(definition, defaultValue: true);
            definition.Port ??= 28015;
            definition.ExecutablePath = _dependencyService.GetExecutablePath(AppType.Rdb);
            definition.WorkingDirectory = Path.GetDirectoryName(definition.ExecutablePath);
        }
        else if (definition.Type == AppType.Rdb)
        {
            changed |= ApplyRunAsAdministratorDefault(definition, defaultValue: true);
            definition.Port ??= 28015;
            if (string.IsNullOrWhiteSpace(definition.WorkingDirectory))
            {
                definition.WorkingDirectory = Path.GetDirectoryName(_dependencyService.GetExecutablePath(AppType.Rdb));
            }
        }
        else if (definition.Type == AppType.App)
        {
            changed |= ApplyRunAsAdministratorDefault(definition, defaultValue: false);
        }

        return Task.FromResult(changed);
    }

    private static bool ApplyRunAsAdministratorDefault(AppDefinition definition, bool defaultValue)
    {
        if (definition.RunAsAdministrator.HasValue)
        {
            return false;
        }

        definition.RunAsAdministrator = defaultValue;
        return true;
    }

    private async Task EnsureSequentialExecutionAsync(bool isUserInitiated = false)
    {
        if (_isSequentialQueueProcessing)
        {
            return;
        }

        _isSequentialQueueProcessing = true;
        try
        {
            foreach (var entry in SequentialApps)
            {
                if (!entry.IsEnabled)
                {
                    continue;
                }

                if (entry.IsProcessing)
                {
                    return;
                }

                var shouldWaitForExit = entry.Definition.WaitForExitBeforeNext;
                var isRunning = entry.IsRunning || _appManager.IsRunning(entry.Definition.Id);
                if (isRunning)
                {
                    if (entry.IsExternallyRunning || !shouldWaitForExit)
                    {
                        continue;
                    }

                    return;
                }

                await StartEntryAsync(entry, isUserInitiated: isUserInitiated);

                if (!shouldWaitForExit)
                {
                    continue;
                }

                return;
            }
        }
        finally
        {
            _isSequentialQueueProcessing = false;
        }
    }

    private void RefreshFtpState()
    {
        CanAddFtp = !EnumerateEntries().Any(e => e.Definition.Type == AppType.Ftp);
    }

    private async Task SaveStateAsync()
    {
        ApplyDisplayOrder(ParallelApps);
        ApplyDisplayOrder(SequentialApps);
        var definitions = EnumerateEntries().Select(e => e.Definition).ToList();
        await _appManager.SaveAsync(definitions);
    }

    private static void ApplyDisplayOrder(IList<AppEntryViewModel> entries)
    {
        for (var i = 0; i < entries.Count; i++)
        {
            entries[i].Definition.DisplayOrder = i;
        }
    }

    private void RefreshCounters()
    {
        ActiveAppCount = EnumerateEntries().Count(e => e.IsEnabled);
    }

    private CancellationTokenSource RegisterStartCancellation(Guid appId)
    {
        lock (_startCancellationGate)
        {
            if (_startCancellationTokens.TryGetValue(appId, out var existing))
            {
                existing.Cancel();
                existing.Dispose();
            }

            var source = new CancellationTokenSource();
            _startCancellationTokens[appId] = source;
            return source;
        }
    }

    private void ReleaseStartCancellation(Guid appId, CancellationTokenSource source)
    {
        lock (_startCancellationGate)
        {
            if (_startCancellationTokens.TryGetValue(appId, out var current) && current == source)
            {
                _startCancellationTokens.Remove(appId);
            }
        }

        source.Dispose();
    }

    private bool HasPendingStart(AppEntryViewModel entry)
    {
        lock (_startCancellationGate)
        {
            return _startCancellationTokens.ContainsKey(entry.Definition.Id);
        }
    }

    private bool TryCancelPendingStart(AppEntryViewModel entry)
    {
        CancellationTokenSource? source;
        lock (_startCancellationGate)
        {
            _startCancellationTokens.TryGetValue(entry.Definition.Id, out source);
        }

        if (source == null)
        {
            return false;
        }

        source.Cancel();
        return true;
    }

    private static TimeSpan GetConfiguredDelay(AppDefinition definition)
    {
        var minutes = Math.Max(0, definition.DelayMinutes);
        var seconds = Math.Max(0, definition.DelaySeconds);
        var delay = TimeSpan.FromMinutes(minutes) + TimeSpan.FromSeconds(seconds);
        return delay < TimeSpan.Zero ? TimeSpan.Zero : delay;
    }

    private static string FormatCountdown(TimeSpan timeSpan)
    {
        if (timeSpan.TotalHours >= 1)
        {
            return timeSpan.ToString(@"hh\:mm\:ss");
        }

        return timeSpan.ToString(@"mm\:ss");
    }

    private void UpdateProcessCheckRemaining(TimeSpan remaining)
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;

        void Apply()
        {
            ParallelCheckRemaining = remaining;
            SequentialCheckRemaining = remaining;
        }

        if (dispatcher != null && !dispatcher.CheckAccess())
        {
            dispatcher.Invoke(Apply);
        }
        else
        {
            Apply();
        }
    }

    private async Task RunDelayCountdownAsync(AppEntryViewModel entry, TimeSpan delay, CancellationToken cancellationToken)
    {
        var target = DateTimeOffset.Now + delay;
        entry.DelayRemaining = delay;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var remaining = target - DateTimeOffset.Now;
            if (remaining <= TimeSpan.Zero)
            {
                break;
            }

            entry.DelayRemaining = remaining;
            var nextTick = remaining > DelayTickInterval ? DelayTickInterval : remaining;
            await Task.Delay(nextTick, cancellationToken);
        }

        entry.DelayRemaining = null;
    }

    private async Task WaitForNetworkAsync(AppEntryViewModel entry, CancellationToken cancellationToken)
    {
        while (!IsNetworkAvailable())
        {
            cancellationToken.ThrowIfCancellationRequested();
            entry.Status = $"네트워크 연결 대기 ({NetworkRetryInterval.TotalSeconds:0}초 후 재시도)";
            await Task.Delay(NetworkRetryInterval, cancellationToken);
        }
    }

    private static bool IsNetworkAvailable()
    {
        if (!NetworkInterface.GetIsNetworkAvailable())
        {
            return false;
        }

        return NetworkInterface.GetAllNetworkInterfaces()
            .Any(n =>
                n.OperationalStatus == OperationalStatus.Up &&
                n.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                n.NetworkInterfaceType != NetworkInterfaceType.Tunnel &&
                n.NetworkInterfaceType != NetworkInterfaceType.Unknown);
    }

    private async Task<bool> ShouldBlockStartForExternalProcessAsync(AppEntryViewModel entry, bool isUserInitiated, CancellationToken cancellationToken)
    {
        if (!RequiresExternalProcessCheck(entry.Definition))
        {
            return false;
        }

        var externalProcesses = _appManager.FindExternalInstanceProcesses(entry.Definition);
        if (externalProcesses.Count == 0)
        {
            return false;
        }

        cancellationToken.ThrowIfCancellationRequested();
        entry.Status = ExternalProcessStatus;

        if (!isUserInitiated)
        {
            return true;
        }

        var processDescriptions = string.Join(Environment.NewLine,
            externalProcesses.Select(p => $"PID {p.ProcessId} - {p.ExecutablePath}"));
        var message =
            $"{entry.DisplayName}과(와) 동일한 프로세스가 이미 실행 중입니다.{Environment.NewLine}{processDescriptions}{Environment.NewLine}{Environment.NewLine}해당 프로세스를 종료하고 다시 시작할까요?";

        var owner = System.Windows.Application.Current?.MainWindow;
        var confirm = System.Windows.MessageBox.Show(owner, message, "다른 프로세스 실행 중", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (confirm != MessageBoxResult.Yes)
        {
            return true;
        }

        var terminated = _appManager.TryTerminateProcesses(externalProcesses);
        if (!terminated)
        {
            entry.Status = "다른 프로세스를 종료하지 못했습니다.";
            return true;
        }

        await Task.Delay(TimeSpan.FromMilliseconds(300), cancellationToken);
        return false;
    }

    private static bool RequiresExternalProcessCheck(AppDefinition definition) =>
        definition.Type == AppType.Rdb || definition.Type == AppType.Ftp;

    private void OnRuntimeStateChanged(object? sender, AppRuntimeState e)
    {
        void Apply()
        {
            var entry = EnumerateEntries().FirstOrDefault(x => x.Definition.Id == e.AppId);
            if (entry == null)
            {
                return;
            }

            entry.IsRunning = e.IsRunning;
            entry.Status = e.IsRunning ? "실행 중" : (MapExternalStatus(e.Message));
            if (!e.IsRunning)
            {
                entry.IsExternallyRunning = false;
            }

            if (entry.Definition.Zone == AppExecutionZone.Sequential && !entry.IsRunning)
            {
                _ = EnsureSequentialExecutionAsync();
            }
        }

        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher != null && !dispatcher.CheckAccess())
        {
            dispatcher.Invoke(Apply);
        }
        else
        {
            Apply();
        }
    }

    private static string MapErrorMessage(Exception exception)
    {
        var message = exception.Message?.ToLowerInvariant() ?? string.Empty;
        if (message.Contains("not found") || message.Contains("file"))
        {
            return "오류: 실행 파일을 찾을 수 없습니다.";
        }

        if (message.Contains("access") || message.Contains("permission"))
        {
            return "오류: 권한이 부족합니다.";
        }

        if (message.Contains("port") || message.Contains("address"))
        {
            return "오류: 포트 충돌이 발생했습니다.";
        }

        return "오류: 실행 중 문제가 발생했습니다.";
    }

    private static string MapExternalStatus(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return "중지";
        }

        var lower = message.ToLowerInvariant();
        if (lower.Contains("error") || lower.Contains("fail"))
        {
            return "오류: 실행 중 문제가 발생했습니다.";
        }

        if (lower.Contains("port"))
        {
            return "오류: 포트 충돌이 발생했습니다.";
        }

        return message;
    }

    private void StartProcessMonitor()
    {
        if (_processMonitorCts != null)
        {
            return;
        }

        _processMonitorCts = new CancellationTokenSource();
        var token = _processMonitorCts.Token;
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        _nextProcessCheckAt = DateTimeOffset.Now + ProcessCheckInterval;
        UpdateProcessCheckRemaining(ProcessCheckInterval);

        async Task MonitorAsync()
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(ProcessCheckInterval, token);
                    if (dispatcher != null && !dispatcher.CheckAccess())
                    {
                        await dispatcher.InvokeAsync(() => CheckProcessesAsync(token));
                    }
                    else
                    {
                        await CheckProcessesAsync(token);
                    }

                    _nextProcessCheckAt = DateTimeOffset.Now + ProcessCheckInterval;
                    UpdateProcessCheckRemaining(ProcessCheckInterval);
                }
                catch (OperationCanceledException)
                {
                    // stop requested
                }
                catch
                {
                    // ignore and keep monitoring
                }
            }
        }

        async Task CountdownAsync()
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    var remaining = _nextProcessCheckAt - DateTimeOffset.Now;
                    if (remaining < TimeSpan.Zero)
                    {
                        remaining = TimeSpan.Zero;
                    }

                    UpdateProcessCheckRemaining(remaining);
                    await Task.Delay(DelayTickInterval, token);
                }
                catch (OperationCanceledException)
                {
                    // stop requested
                }
                catch
                {
                    // ignore and keep counting
                }
            }
        }

        _ = MonitorAsync();
        _ = CountdownAsync();
    }

    private async Task CheckProcessesAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var isBusy = _isSequentialQueueProcessing || EnumerateEntries().Any(e => e.IsProcessing || e.DelayRemaining.HasValue);
        if (isBusy)
        {
            return;
        }

        var sequentialNeedsCheck = false;

        foreach (var entry in EnumerateEntries())
        {
            cancellationToken.ThrowIfCancellationRequested();

            var isRunning = _appManager.IsRunning(entry.Definition.Id);
            entry.IsRunning = isRunning;
            if (!isRunning)
            {
                entry.IsExternallyRunning = false;
                if (entry.Status == "실행 중")
                {
                    entry.Status = "중지";
                }
            }

            if (entry.Definition.Zone == AppExecutionZone.Sequential)
            {
                if (entry.IsEnabled && !isRunning)
                {
                    sequentialNeedsCheck = true;
                }
                continue;
            }

            if (!entry.IsEnabled || entry.DelayRemaining.HasValue || entry.IsProcessing)
            {
                continue;
            }

            if (!isRunning)
            {
                await StartEntryAsync(entry, persist: false);
            }
        }

        if (sequentialNeedsCheck)
        {
            await EnsureSequentialExecutionAsync();
        }
    }
}
