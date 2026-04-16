using System;
using System.Diagnostics;

namespace NewHyOn.Player.Settings.Services;

public static class ProcessService
{
    public static void KillProcessByName(string processName)
    {
        try
        {
            foreach (Process process in Process.GetProcesses())
            {
                if (!process.ProcessName.Equals(processName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                process.Kill();
            }
        }
        catch
        {
        }
    }

    public static void ShowIpConfiguration()
    {
        Process.Start(new ProcessStartInfo("cmd.exe", "/K ipconfig")
        {
            UseShellExecute = true
        });
    }
}
