using System.Diagnostics;
using System.IO;
using System.Text;

namespace NewHyOn.Shared.Windows;

public static class FirewallRuleSynchronizer
{
    private const int Windows8MajorVersion = 6;
    private const int Windows8MinorVersion = 2;

    public static async Task EnsureProgramRuleAsync(string ruleName, string executablePath, CancellationToken cancellationToken = default)
    {
        EnsureWindows8OrLater();

        if (string.IsNullOrWhiteSpace(ruleName))
        {
            throw new InvalidOperationException("방화벽 규칙 이름이 비어 있습니다.");
        }

        if (string.IsNullOrWhiteSpace(executablePath))
        {
            throw new InvalidOperationException("방화벽 규칙에 사용할 실행 파일 경로가 비어 있습니다.");
        }

        string normalizedPath = Path.GetFullPath(executablePath);
        if (!File.Exists(normalizedPath))
        {
            throw new FileNotFoundException("방화벽 규칙에 사용할 실행 파일을 찾을 수 없습니다.", normalizedPath);
        }

        FirewallRuleInspection inspection = await InspectRuleAsync(ruleName, cancellationToken);
        if (inspection.IsCurrentProgramRule(normalizedPath))
        {
            return;
        }

        await DeleteRuleAsync(ruleName, cancellationToken);
        await ExecuteNetshAsync(CreateAuthorAppNetshCmdStr(ruleName, normalizedPath), cancellationToken, throwOnError: true);
    }

    public static async Task EnsurePortRuleAsync(string ruleName, string protocol, string localPort, CancellationToken cancellationToken = default)
    {
        EnsureWindows8OrLater();

        if (string.IsNullOrWhiteSpace(ruleName))
        {
            throw new InvalidOperationException("방화벽 규칙 이름이 비어 있습니다.");
        }

        if (string.IsNullOrWhiteSpace(protocol))
        {
            throw new InvalidOperationException("방화벽 포트 규칙 프로토콜이 비어 있습니다.");
        }

        if (string.IsNullOrWhiteSpace(localPort))
        {
            throw new InvalidOperationException("방화벽 포트 규칙에 사용할 포트가 비어 있습니다.");
        }

        string normalizedProtocol = protocol.Trim().ToUpperInvariant();
        FirewallRuleInspection inspection = await InspectRuleAsync(ruleName, cancellationToken);
        if (inspection.IsCurrentPortRule(normalizedProtocol, localPort))
        {
            return;
        }

        await DeleteRuleAsync(ruleName, cancellationToken);
        await ExecuteNetshAsync(CreateOpenPortNetshCmdStr(ruleName, normalizedProtocol, localPort), cancellationToken, throwOnError: true);
    }

    private static void EnsureWindows8OrLater()
    {
        Version version = Environment.OSVersion.Version;
        if (version.Major < Windows8MajorVersion || (version.Major == Windows8MajorVersion && version.Minor < Windows8MinorVersion))
        {
            throw new InvalidOperationException("방화벽 자동 설정은 Windows 8 이상에서만 지원합니다.");
        }
    }

    private static async Task<FirewallRuleInspection> InspectRuleAsync(string ruleName, CancellationToken cancellationToken)
    {
        var (exitCode, output, error) = await ExecuteNetshAsync(
            $"advfirewall firewall show rule name=\"{EscapeArgument(ruleName)}\"",
            cancellationToken,
            throwOnError: false);

        string combined = string.Join(
            Environment.NewLine,
            new[] { output, error }.Where(value => string.IsNullOrWhiteSpace(value) == false));

        return new FirewallRuleInspection(exitCode, combined);
    }

    private static async Task<(int ExitCode, string Output, string Error)> ExecuteNetshAsync(
        string arguments,
        CancellationToken cancellationToken,
        bool throwOnError)
    {
        using Process process = new()
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

        string output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
        string error = await process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);

        if (throwOnError && process.ExitCode != 0)
        {
            string message = string.IsNullOrWhiteSpace(error) ? output : error;
            throw new InvalidOperationException($"방화벽 규칙 적용에 실패했습니다. {message}".Trim());
        }

        return (process.ExitCode, output, error);
    }

    private static async Task DeleteRuleAsync(string ruleName, CancellationToken cancellationToken)
    {
        await ExecuteNetshAsync(
            $"advfirewall firewall delete rule name=\"{EscapeArgument(ruleName)}\"",
            cancellationToken,
            throwOnError: false);
    }

    private static string CreateAuthorAppNetshCmdStr(string ruleName, string executablePath)
    {
        return $"advfirewall firewall add rule name=\"{EscapeArgument(ruleName)}\" dir=in action=allow program=\"{EscapeArgument(executablePath)}\" enable=yes profile=private,public";
    }

    private static string CreateOpenPortNetshCmdStr(string ruleName, string protocol, string localPort)
    {
        return $"advfirewall firewall add rule name=\"{EscapeArgument(ruleName)}\" dir=in action=allow protocol={protocol} localport={EscapeArgument(localPort)} enable=yes profile=private,public";
    }

    private static string EscapeArgument(string value) => value.Replace("\"", "\"\"", StringComparison.Ordinal);

    private static string Normalize(string value)
    {
        StringBuilder builder = new(value.Length);
        foreach (char ch in value)
        {
            if (!char.IsWhiteSpace(ch) && ch != '"')
            {
                builder.Append(char.ToLowerInvariant(ch));
            }
        }

        return builder.ToString().Replace('\\', '/');
    }

    private sealed record FirewallRuleInspection(int ExitCode, string RawOutput)
    {
        private string NormalizedOutput => Normalize(RawOutput);

        public bool IsCurrentProgramRule(string executablePath)
        {
            if (!NeedToAddRule())
            {
                return NormalizedOutput.Contains(Normalize(executablePath), StringComparison.OrdinalIgnoreCase);
            }

            return false;
        }

        public bool IsCurrentPortRule(string protocol, string localPort)
        {
            if (!NeedToAddRule())
            {
                return ContainsProtocol(protocol) && ContainsLocalPort(localPort);
            }

            return false;
        }

        private bool NeedToAddRule()
        {
            if (ExitCode != 0 || string.IsNullOrWhiteSpace(RawOutput))
            {
                return true;
            }

            if (NormalizedOutput.Contains("norulesmatchthespecifiedcriteria", StringComparison.OrdinalIgnoreCase)
                || NormalizedOutput.Contains("지정한규칙을찾을수없습니다", StringComparison.OrdinalIgnoreCase)
                || NormalizedOutput.Contains("지정한조건과일치하는규칙이없습니다", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return !IsRuleEnabled() || !IsRuleProfilesCompatible();
        }

        private bool IsRuleEnabled()
        {
            return RawOutput.Split(Environment.NewLine)
                .Select(line => line.Replace(" ", string.Empty))
                .Any(line => line.Equals("Enabled:Yes", StringComparison.OrdinalIgnoreCase)
                    || line.Equals("사용:예", StringComparison.OrdinalIgnoreCase));
        }

        private bool IsRuleProfilesCompatible()
        {
            if (NormalizedOutput.Contains("profiles:any", StringComparison.OrdinalIgnoreCase)
                || NormalizedOutput.Contains("프로필:모두", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            bool hasPrivate = NormalizedOutput.Contains("private", StringComparison.OrdinalIgnoreCase)
                || NormalizedOutput.Contains("개인", StringComparison.OrdinalIgnoreCase);
            bool hasPublic = NormalizedOutput.Contains("public", StringComparison.OrdinalIgnoreCase)
                || NormalizedOutput.Contains("공용", StringComparison.OrdinalIgnoreCase);
            return hasPrivate && hasPublic;
        }

        private bool ContainsProtocol(string protocol)
        {
            string normalizedProtocol = Normalize(protocol);
            return NormalizedOutput.Contains($"protocol:{normalizedProtocol}", StringComparison.OrdinalIgnoreCase)
                || NormalizedOutput.Contains($"프로토콜:{normalizedProtocol}", StringComparison.OrdinalIgnoreCase);
        }

        private bool ContainsLocalPort(string localPort)
        {
            string normalizedPort = Normalize(localPort);
            return NormalizedOutput.Contains($"localport:{normalizedPort}", StringComparison.OrdinalIgnoreCase)
                || NormalizedOutput.Contains($"로컬포트:{normalizedPort}", StringComparison.OrdinalIgnoreCase);
        }
    }
}
