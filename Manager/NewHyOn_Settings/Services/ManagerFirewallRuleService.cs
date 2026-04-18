using NewHyOn.Settings.Wpf.Models;
using NewHyOn.Shared.Windows;
using System.IO;

namespace NewHyOn.Settings.Wpf.Services;

public sealed class ManagerFirewallRuleService
{
    private const string ExecutableLocationGuide = "방화벽 설정을 위해 실행파일의 위치를 확인한 후 다시 저장해주세요.";
    private const string FirewallRetryGuide = "방화벽 설정을 확인한 후 다시 저장해주세요.";
    private const string ManagerAllowedAppRuleName = "newhyon_manager_allowed_app";
    private const string ManagerFtpPortRuleName = "newhyon_manager_ftp_port";
    private const string ManagerFtpPassivePortRuleName = "newhyon_manager_ftp_passive_port";

    public async Task<string?> TryApplyAsync(SettingsFormData formData, CancellationToken cancellationToken = default)
    {
        List<string> notices = new();

        await TryApplyPortRulesAsync(
            ManagerFtpPortRuleName,
            formData.FtpPort.ToString(),
            notices,
            cancellationToken);

        await TryApplyPortRulesAsync(
            ManagerFtpPassivePortRuleName,
            $"{formData.PasvMinPort}-{formData.PasvMaxPort}",
            notices,
            cancellationToken);

        string managerExePath = GetManagerExeFilePath();
        if (!File.Exists(managerExePath))
        {
            notices.Add(ExecutableLocationGuide);
        }
        else
        {
            try
            {
                await FirewallRuleSynchronizer.EnsureProgramRuleAsync(
                    ManagerAllowedAppRuleName,
                    managerExePath,
                    cancellationToken);
            }
            catch
            {
                notices.Add(FirewallRetryGuide);
            }
        }

        return notices.Count == 0 ? null : string.Join(Environment.NewLine, notices.Distinct());
    }

    private static string GetManagerExeFilePath()
    {
        return Path.Combine(AppContext.BaseDirectory, "NewHyOn Manager.exe");
    }

    private static async Task TryApplyPortRulesAsync(
        string ruleName,
        string localPort,
        List<string> notices,
        CancellationToken cancellationToken)
    {
        try
        {
            await FirewallRuleSynchronizer.EnsurePortRuleAsync(
                ruleName,
                "TCP",
                localPort,
                cancellationToken);
        }
        catch
        {
            notices.Add(FirewallRetryGuide);
        }
    }
}
