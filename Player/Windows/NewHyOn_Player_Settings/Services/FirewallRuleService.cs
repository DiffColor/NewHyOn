using NewHyOn.Shared.Windows;
using System.IO;

namespace NewHyOn.Player.Settings.Services;

public static class FirewallRuleService
{
    private const string ExecutableLocationGuide = "방화벽 설정을 위해 실행파일의 위치를 확인해주세요.";
    private const string FirewallRetryGuide = "방화벽 설정을 확인한 후 다시 저장해주세요.";
    private const string PlayerAllowedAppRuleName = "newhyon_player_allowed_app";
    private const string SyncRuleName = "newhyon_player_sync_port";

    public static async Task<string?> TryApplyPlayerRulesAsync(int syncPort, CancellationToken cancellationToken = default)
    {
        List<string> notices = new();

        if (syncPort > 0)
        {
            try
            {
                await FirewallRuleSynchronizer.EnsurePortRuleAsync(
                    SyncRuleName,
                    "UDP",
                    syncPort.ToString(),
                    cancellationToken);
            }
            catch
            {
                notices.Add(FirewallRetryGuide);
            }
        }

        string playerExePath = FndTools.GetPlayerExeFilePath();
        if (!File.Exists(playerExePath))
        {
            notices.Add(ExecutableLocationGuide);
        }
        else
        {
            try
            {
                await FirewallRuleSynchronizer.EnsureProgramRuleAsync(
                    PlayerAllowedAppRuleName,
                    playerExePath,
                    cancellationToken);
            }
            catch
            {
                notices.Add(FirewallRetryGuide);
            }
        }

        return notices.Count == 0 ? null : string.Join(Environment.NewLine, notices.Distinct());
    }
}
