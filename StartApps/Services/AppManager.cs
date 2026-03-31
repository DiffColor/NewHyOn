using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using StartApps.Models;

namespace StartApps.Services;

public class AppManager
{
    private readonly AppDataStore _dataStore;
    private readonly AppDependencyService _dependencyService;
    private readonly FirewallRuleService _firewallRuleService;
    private readonly Dictionary<Guid, Process> _runningProcesses = new();
    private readonly object _gate = new();

    public event EventHandler<AppRuntimeState>? RuntimeStateChanged;

    public AppManager(AppDataStore dataStore, AppDependencyService dependencyService, FirewallRuleService firewallRuleService)
    {
        _dataStore = dataStore;
        _dependencyService = dependencyService;
        _firewallRuleService = firewallRuleService;
    }

    public Task<IList<AppDefinition>> LoadAsync() => _dataStore.LoadAsync();

    public Task SaveAsync(IEnumerable<AppDefinition> definitions) => _dataStore.SaveAsync(definitions);

    public async Task StartAsync(AppDefinition definition, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await _dependencyService.EnsureDependenciesAsync(definition.Type, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        if (definition.Type == AppType.Ftp)
        {
            _dependencyService.ApplyFtpConfiguration(definition);
        }

        var processInfo = BuildProcessStartInfo(definition);
        try
        {
            await _firewallRuleService.EnsureRulesAsync(definition, processInfo.FileName, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Firewall rule sync failed for {definition.Name}: {ex}");
        }

        var process = new Process
        {
            StartInfo = processInfo,
            EnableRaisingEvents = true
        };

        process.Exited += (_, _) =>
        {
            lock (_gate)
            {
                _runningProcesses.Remove(definition.Id);
            }
            RuntimeStateChanged?.Invoke(this, new AppRuntimeState(definition.Id, false, "프로세스 종료"));
        };

        if (!process.Start())
        {
            RuntimeStateChanged?.Invoke(this, new AppRuntimeState(definition.Id, false, "프로세스를 시작할 수 없습니다."));
            return;
        }

        lock (_gate)
        {
            _runningProcesses[definition.Id] = process;
        }

        definition.LastStartedAt = DateTimeOffset.Now;
        RuntimeStateChanged?.Invoke(this, new AppRuntimeState(definition.Id, true));
    }

    public void Stop(AppDefinition definition, bool forceKill = false)
    {
        lock (_gate)
        {
            if (!_runningProcesses.TryGetValue(definition.Id, out var process))
            {
                RuntimeStateChanged?.Invoke(this, new AppRuntimeState(definition.Id, false));
                return;
            }

            try
            {
                if (!process.HasExited)
                {
                    if (forceKill)
                    {
                        process.Kill(true);
                    }
                    else
                    {
                        process.CloseMainWindow();
                        process.WaitForExit(TimeSpan.FromSeconds(5));
                        if (!process.HasExited)
                        {
                            process.Kill(true);
                        }
                    }
                }
            }
            catch
            {
                // ignored
            }
            finally
            {
                _runningProcesses.Remove(definition.Id);
                RuntimeStateChanged?.Invoke(this, new AppRuntimeState(definition.Id, false, "앱이 중지되었습니다."));
            }
        }
    }

    public bool IsRunning(Guid appId)
    {
        lock (_gate)
        {
            return _runningProcesses.TryGetValue(appId, out var process) && !process.HasExited;
        }
    }

    public IReadOnlyDictionary<Guid, Process> RunningProcesses
    {
        get
        {
            lock (_gate)
            {
                return new ReadOnlyDictionary<Guid, Process>(_runningProcesses);
            }
        }
    }

    public bool TryAttachToRunningProcess(AppDefinition definition)
    {
        var executablePath = ResolveExecutablePath(definition);
        if (string.IsNullOrWhiteSpace(executablePath) || !File.Exists(executablePath))
        {
            return false;
        }

        var normalizedPath = Path.GetFullPath(executablePath);
        var processName = Path.GetFileNameWithoutExtension(normalizedPath);
        if (string.IsNullOrWhiteSpace(processName))
        {
            return false;
        }

        foreach (var process in Process.GetProcessesByName(processName))
        {
            try
            {
                var processPath = process.MainModule?.FileName;
                if (string.IsNullOrWhiteSpace(processPath))
                {
                    continue;
                }

                if (!string.Equals(Path.GetFullPath(processPath), normalizedPath, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                lock (_gate)
                {
                    if (_runningProcesses.ContainsKey(definition.Id))
                    {
                        return true;
                    }

                    process.EnableRaisingEvents = true;
                    process.Exited += (_, _) =>
                    {
                        lock (_gate)
                        {
                            _runningProcesses.Remove(definition.Id);
                        }
                        RuntimeStateChanged?.Invoke(this, new AppRuntimeState(definition.Id, false, "프로세스 종료"));
                    };

                    _runningProcesses[definition.Id] = process;
                }

                RuntimeStateChanged?.Invoke(this, new AppRuntimeState(definition.Id, true));
                return true;
            }
            catch
            {
                // Access denied, ignore and continue.
            }
        }

        return false;
    }

    public IReadOnlyList<ExternalProcessInfo> FindExternalInstanceProcesses(AppDefinition definition)
    {
        if (definition.Type != AppType.Rdb && definition.Type != AppType.Ftp)
        {
            return Array.Empty<ExternalProcessInfo>();
        }

        var executablePath = ResolveExecutablePath(definition);
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            return Array.Empty<ExternalProcessInfo>();
        }

        var normalizedTargetPath = Path.GetFullPath(executablePath);
        var processName = Path.GetFileNameWithoutExtension(normalizedTargetPath);
        if (string.IsNullOrWhiteSpace(processName))
        {
            return Array.Empty<ExternalProcessInfo>();
        }

        var external = new List<ExternalProcessInfo>();
        foreach (var process in Process.GetProcessesByName(processName))
        {
            try
            {
                var processPath = process.MainModule?.FileName;
                if (string.IsNullOrWhiteSpace(processPath))
                {
                    continue;
                }

                var normalizedProcessPath = Path.GetFullPath(processPath);
                if (IsTrackedProcess(process.Id))
                {
                    continue;
                }

                if (string.Equals(normalizedProcessPath, normalizedTargetPath, StringComparison.OrdinalIgnoreCase))
                {
                    external.Add(new ExternalProcessInfo(process.Id, process.ProcessName, normalizedProcessPath));
                }
            }
            catch
            {
                // Access denied, ignore and continue.
            }
            finally
            {
                try
                {
                    process.Dispose();
                }
                catch
                {
                    // ignored
                }
            }
        }

        return external;
    }

    public bool TryTerminateProcesses(IEnumerable<ExternalProcessInfo> processes)
    {
        var success = true;
        foreach (var processInfo in processes)
        {
            try
            {
                using var process = Process.GetProcessById(processInfo.ProcessId);
                if (process.HasExited)
                {
                    continue;
                }

                process.Kill(true);
                process.WaitForExit(TimeSpan.FromSeconds(5));
            }
            catch
            {
                success = false;
            }
        }

        return success;
    }

    private ProcessStartInfo BuildProcessStartInfo(AppDefinition definition)
    {
        var executablePath = ResolveExecutablePath(definition);
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            throw new InvalidOperationException("실행 파일이 지정되지 않았습니다.");
        }

        var workingDirectory = !string.IsNullOrWhiteSpace(definition.WorkingDirectory)
            && Directory.Exists(definition.WorkingDirectory)
            ? definition.WorkingDirectory
            : Path.GetDirectoryName(executablePath) ?? Environment.CurrentDirectory;

        var startInfo = new ProcessStartInfo
        {
            FileName = executablePath,
            WorkingDirectory = workingDirectory,
            WindowStyle = definition.WindowStyle,
            UseShellExecute = definition.RunAsAdministrator == true
        };

        if (definition.RunAsAdministrator == true)
        {
            startInfo.Verb = "runas";
        }

        if (!startInfo.UseShellExecute)
        {
            startInfo.CreateNoWindow = !definition.ShowWindow;
        }

        switch (definition.Type)
        {
            case AppType.Rdb:
                var rethinkPort = definition.Port.GetValueOrDefault(28015).ToString(CultureInfo.InvariantCulture);
                startInfo.Arguments = $"--bind all --driver-port {rethinkPort} --initial-password turtle04!9";
                break;
            case AppType.Ftp:
                startInfo.Arguments = "-compat-start";
                break;
            case AppType.Msg:
            case AppType.Msg472:
            case AppType.Msg90:
                startInfo.Arguments = BuildSignalrArguments(definition);
                break;
            default:
                startInfo.Arguments = definition.Arguments ?? string.Empty;
                break;
        }

        return startInfo;
    }

    private bool IsTrackedProcess(int processId)
    {
        lock (_gate)
        {
            return _runningProcesses.Values.Any(x => !x.HasExited && x.Id == processId);
        }
    }

    private string ResolveExecutablePath(AppDefinition definition)
    {
        if (!string.IsNullOrWhiteSpace(definition.ExecutablePath))
        {
            return definition.ExecutablePath;
        }

        return definition.Type switch
        {
            AppType.Rdb => _dependencyService.GetExecutablePath(AppType.Rdb),
            AppType.Ftp => _dependencyService.GetExecutablePath(AppType.Ftp),
            AppType.Msg => _dependencyService.GetExecutablePath(AppType.Msg),
            AppType.Msg472 => _dependencyService.GetExecutablePath(AppType.Msg472),
            AppType.Msg90 => _dependencyService.GetExecutablePath(AppType.Msg90),
            _ => string.Empty
        };
    }

    private static string BuildSignalrArguments(AppDefinition definition)
    {
        var port = definition.Port ?? 5000;
        var hubPath = string.IsNullOrWhiteSpace(definition.MsgHubPath) ? "/Data" : definition.MsgHubPath.Trim();
        var hubArg = QuoteArgumentIfNeeded(hubPath);
        var portText = port.ToString(CultureInfo.InvariantCulture);
        if (definition.Type == AppType.Msg90)
        {
            return $"--port={portText} --hubPath={hubArg}";
        }

        return $"-p {portText} -h {hubArg}";
    }

    private static string QuoteArgumentIfNeeded(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        if (value.Contains(' ') || value.Contains('\t'))
        {
            if (value.StartsWith('"') && value.EndsWith('"'))
            {
                return value;
            }

            return $"\"{value}\"";
        }

        return value;
    }
}
