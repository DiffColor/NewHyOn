using System.Diagnostics;
using System.Globalization;
using System.IO;
using StartApps.Models;

namespace StartApps.Services;

public sealed class FirewallRuleService
{
    private const int DefaultRethinkPort = 28015;
    private const int DefaultSignalRPort = 5000;
    private static readonly string[] LegacyRuleNames =
    [
        "vnc",
        "vnc1_port",
        "vnc2_port",
        "ftp_ports",
        "agent_port",
        "op_port",
        "sync_port",
        "agent"
    ];

    private readonly AppProfile _profile;
    private readonly object _cleanupGate = new();
    private bool _legacyCleanupCompleted;

    public FirewallRuleService(AppProfile profile)
    {
        _profile = profile;
    }

    public async Task EnsureRulesAsync(AppDefinition definition, string executablePath, CancellationToken cancellationToken = default)
    {
        if (!ShouldManage(definition.Type))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(executablePath))
        {
            throw new InvalidOperationException("방화벽 규칙에 사용할 실행 파일 경로가 비어 있습니다.");
        }

        var normalizedPath = Path.GetFullPath(executablePath);
        if (!File.Exists(normalizedPath))
        {
            throw new FileNotFoundException("방화벽 규칙에 사용할 실행 파일을 찾을 수 없습니다.", normalizedPath);
        }

        await CleanupLegacyRulesAsync(cancellationToken);

        foreach (var command in BuildCommands(definition, normalizedPath))
        {
            await ExecutePowerShellAsync(command, cancellationToken);
        }
    }

    private async Task CleanupLegacyRulesAsync(CancellationToken cancellationToken)
    {
        lock (_cleanupGate)
        {
            if (_legacyCleanupCompleted)
            {
                return;
            }

            _legacyCleanupCompleted = true;
        }

        foreach (var ruleName in LegacyRuleNames)
        {
            await ExecutePowerShellAsync(BuildRemoveRuleCommand(ruleName), cancellationToken);
        }
    }

    private IEnumerable<string> BuildCommands(AppDefinition definition, string executablePath)
    {
        yield return BuildProgramRuleCommand(BuildProgramRuleName(definition), executablePath);

        foreach (var portRule in BuildPortRules(definition))
        {
            yield return BuildTcpPortRuleCommand(portRule.RuleName, portRule.LocalPort);
        }
    }

    private IEnumerable<(string RuleName, string LocalPort)> BuildPortRules(AppDefinition definition)
    {
        var mainPort = ResolveMainPort(definition);
        if (mainPort > 0)
        {
            yield return (BuildMainPortRuleName(definition), mainPort.ToString(CultureInfo.InvariantCulture));
        }

        if (definition.Type == AppType.Ftp)
        {
            var (minPort, maxPort) = ParsePassiveRange(definition.PassivePortRange);
            yield return (BuildPassivePortRuleName(definition), $"{minPort}-{maxPort}");
        }
    }

    private static int ResolveMainPort(AppDefinition definition) =>
        definition.Type switch
        {
            AppType.Rdb => definition.Port.GetValueOrDefault(DefaultRethinkPort),
            AppType.Msg or AppType.Msg472 or AppType.Msg90 => definition.Port.GetValueOrDefault(DefaultSignalRPort),
            AppType.Ftp => definition.Port.GetValueOrDefault(AppDependencyService.DefaultFtpPort),
            _ => 0
        };

    private static (int MinPort, int MaxPort) ParsePassiveRange(string? range)
    {
        const int defaultMin = 24000;
        const int defaultMax = 24240;

        if (string.IsNullOrWhiteSpace(range))
        {
            return (defaultMin, defaultMax);
        }

        var parts = range.Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var minPort = parts.Length > 0 && int.TryParse(parts[0], out var parsedMin) ? parsedMin : defaultMin;
        var maxPort = parts.Length > 1 && int.TryParse(parts[1], out var parsedMax) ? parsedMax : minPort;

        if (minPort <= 0 || minPort > 65535)
        {
            minPort = defaultMin;
        }

        if (maxPort <= 0 || maxPort > 65535)
        {
            maxPort = defaultMax;
        }

        if (maxPort < minPort)
        {
            (minPort, maxPort) = (maxPort, minPort);
        }

        return (minPort, maxPort);
    }

    private string BuildProgramRuleName(AppDefinition definition) =>
        $"StartApps|{_profile.Id}|{definition.Id}|Program";

    private string BuildMainPortRuleName(AppDefinition definition) =>
        $"StartApps|{_profile.Id}|{definition.Id}|MainPort";

    private string BuildPassivePortRuleName(AppDefinition definition) =>
        $"StartApps|{_profile.Id}|{definition.Id}|PassivePort";

    private static string BuildProgramRuleCommand(string ruleName, string executablePath)
    {
        var escapedRuleName = EscapePowerShell(ruleName);
        var escapedPath = EscapePowerShell(executablePath);
        return "$name = '" + escapedRuleName + "';"
             + "$program = '" + escapedPath + "';"
             + "$rules = Get-NetFirewallRule -DisplayName $name -ErrorAction SilentlyContinue;"
             + "if ($rules) { $rules | Remove-NetFirewallRule; }"
             + "New-NetFirewallRule -DisplayName $name -Direction Inbound -Action Allow -Enabled True -Profile Any -Program $program | Out-Null;";
    }

    private static string BuildTcpPortRuleCommand(string ruleName, string localPort)
    {
        var escapedRuleName = EscapePowerShell(ruleName);
        var escapedLocalPort = EscapePowerShell(localPort);
        return "$name = '" + escapedRuleName + "';"
             + "$localPort = '" + escapedLocalPort + "';"
             + "$rules = Get-NetFirewallRule -DisplayName $name -ErrorAction SilentlyContinue;"
             + "if ($rules) { $rules | Remove-NetFirewallRule; }"
             + "New-NetFirewallRule -DisplayName $name -Direction Inbound -Action Allow -Enabled True -Profile Any -Protocol TCP -LocalPort $localPort | Out-Null;";
    }

    private static string BuildRemoveRuleCommand(string ruleName)
    {
        var escapedRuleName = EscapePowerShell(ruleName);
        return "$name = '" + escapedRuleName + "';"
             + "$rules = Get-NetFirewallRule -DisplayName $name -ErrorAction SilentlyContinue;"
             + "if ($rules) { $rules | Remove-NetFirewallRule; }";
    }

    private static string EscapePowerShell(string value) => value.Replace("'", "''", StringComparison.Ordinal);

    private static bool ShouldManage(AppType type) =>
        type == AppType.Rdb
        || type == AppType.Ftp
        || type == AppType.Msg
        || type == AppType.Msg472
        || type == AppType.Msg90;

    private static async Task ExecutePowerShellAsync(string command, CancellationToken cancellationToken)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            }
        };

        process.StartInfo.ArgumentList.Add("-NoProfile");
        process.StartInfo.ArgumentList.Add("-NonInteractive");
        process.StartInfo.ArgumentList.Add("-ExecutionPolicy");
        process.StartInfo.ArgumentList.Add("Bypass");
        process.StartInfo.ArgumentList.Add("-Command");
        process.StartInfo.ArgumentList.Add(command);

        if (!process.Start())
        {
            throw new InvalidOperationException("PowerShell 방화벽 명령을 시작하지 못했습니다.");
        }

        await process.WaitForExitAsync(cancellationToken);

        var stdOut = await process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stdErr = await process.StandardError.ReadToEndAsync(cancellationToken);
        if (process.ExitCode != 0)
        {
            var message = string.IsNullOrWhiteSpace(stdErr) ? stdOut : stdErr;
            throw new InvalidOperationException($"방화벽 규칙 적용에 실패했습니다. {message}".Trim());
        }
    }
}
