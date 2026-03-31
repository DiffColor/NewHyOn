using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using StartApps.Models;

namespace StartApps.Services;

public sealed class FirewallRuleService
{
    private const int VistaMajorVersion = 6;
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

        foreach (var rule in BuildRules(definition, normalizedPath))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!await NeedToApplyRuleAsync(rule, cancellationToken))
            {
                continue;
            }

            await DeleteRuleAsync(rule.Name, cancellationToken);
            await ExecuteNetshAsync(rule.AddArguments, cancellationToken, throwOnError: true);
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
            await DeleteRuleAsync(ruleName, cancellationToken);
        }
    }

    private IEnumerable<FirewallRuleSpec> BuildRules(AppDefinition definition, string executablePath)
    {
        yield return FirewallRuleSpec.CreateProgramRule(
            BuildProgramRuleName(definition),
            executablePath);

        var mainPort = ResolveMainPort(definition);
        if (mainPort > 0)
        {
            yield return FirewallRuleSpec.CreatePortRule(
                BuildMainPortRuleName(definition),
                mainPort.ToString(CultureInfo.InvariantCulture));
        }

        if (definition.Type == AppType.Ftp)
        {
            var (minPort, maxPort) = ParsePassiveRange(definition.PassivePortRange);
            yield return FirewallRuleSpec.CreatePortRule(
                BuildPassivePortRuleName(definition),
                $"{minPort}-{maxPort}");
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

    private async Task<bool> NeedToApplyRuleAsync(FirewallRuleSpec rule, CancellationToken cancellationToken)
    {
        if (Environment.OSVersion.Version.Major < VistaMajorVersion)
        {
            return true;
        }

        var (exitCode, output, error) = await ExecuteNetshAsync(
            $"advfirewall firewall show rule name=\"{EscapeArgument(rule.Name)}\"",
            cancellationToken,
            throwOnError: false);

        if (exitCode != 0)
        {
            return true;
        }

        var combined = string.Join(
            Environment.NewLine,
            new[] { output, error }.Where(value => !string.IsNullOrWhiteSpace(value)));

        if (string.IsNullOrWhiteSpace(combined))
        {
            return true;
        }

        var normalizedOutput = Normalize(combined);
        if (normalizedOutput.Contains("norulesmatchthespecifiedcriteria", StringComparison.OrdinalIgnoreCase)
            || normalizedOutput.Contains("지정한규칙을찾을수없습니다", StringComparison.OrdinalIgnoreCase)
            || normalizedOutput.Contains("지정한조건과일치하는규칙이없습니다", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!IsRuleEnabled(normalizedOutput))
        {
            return true;
        }

        if (rule.ProgramPath != null && !normalizedOutput.Contains(Normalize(rule.ProgramPath), StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (rule.LocalPort != null && !normalizedOutput.Contains($"localport:{Normalize(rule.LocalPort)}", StringComparison.OrdinalIgnoreCase)
            && !normalizedOutput.Contains($"로컬포트:{Normalize(rule.LocalPort)}", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    private static bool IsRuleEnabled(string normalizedOutput) =>
        normalizedOutput.Contains("enabled:yes", StringComparison.OrdinalIgnoreCase)
        || normalizedOutput.Contains("사용:예", StringComparison.OrdinalIgnoreCase);

    private static async Task DeleteRuleAsync(string ruleName, CancellationToken cancellationToken)
    {
        await ExecuteNetshAsync(
            $"advfirewall firewall delete rule name=\"{EscapeArgument(ruleName)}\"",
            cancellationToken,
            throwOnError: false);
    }

    private static async Task<(int ExitCode, string Output, string Error)> ExecuteNetshAsync(
        string arguments,
        CancellationToken cancellationToken,
        bool throwOnError)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "netsh",
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            }
        };

        if (!process.Start())
        {
            throw new InvalidOperationException("방화벽 명령을 시작하지 못했습니다.");
        }

        await process.WaitForExitAsync(cancellationToken);

        var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
        var error = await process.StandardError.ReadToEndAsync(cancellationToken);

        if (throwOnError && process.ExitCode != 0)
        {
            var message = string.IsNullOrWhiteSpace(error) ? output : error;
            throw new InvalidOperationException($"방화벽 규칙 적용에 실패했습니다. {message}".Trim());
        }

        return (process.ExitCode, output, error);
    }

    private static string EscapeArgument(string value) => value.Replace("\"", "\"\"", StringComparison.Ordinal);

    private static string Normalize(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var ch in value)
        {
            if (!char.IsWhiteSpace(ch) && ch != '"')
            {
                builder.Append(char.ToLowerInvariant(ch));
            }
        }

        return builder.ToString().Replace('\\', '/');
    }

    private static bool ShouldManage(AppType type) =>
        type == AppType.Rdb
        || type == AppType.Ftp
        || type == AppType.Msg
        || type == AppType.Msg472
        || type == AppType.Msg90;

    private sealed record FirewallRuleSpec(string Name, string AddArguments, string? ProgramPath, string? LocalPort)
    {
        public static FirewallRuleSpec CreateProgramRule(string name, string programPath)
        {
            var args = $"advfirewall firewall add rule name=\"{EscapeArgument(name)}\" dir=in action=allow program=\"{EscapeArgument(programPath)}\" enable=yes";
            return new FirewallRuleSpec(name, args, programPath, null);
        }

        public static FirewallRuleSpec CreatePortRule(string name, string localPort)
        {
            var args = $"advfirewall firewall add rule name=\"{EscapeArgument(name)}\" dir=in action=allow protocol=TCP localport={EscapeArgument(localPort)}";
            return new FirewallRuleSpec(name, args, null, localPort);
        }
    }
}
