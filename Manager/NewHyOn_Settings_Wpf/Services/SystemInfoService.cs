using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace NewHyOn.Settings.Wpf.Services;

public static class SystemInfoService
{
    public static IReadOnlyCollection<string> GetLocalIpv4Addresses()
    {
        return NetworkInterface.GetAllNetworkInterfaces()
            .Where(networkInterface =>
                networkInterface.OperationalStatus == OperationalStatus.Up &&
                networkInterface.NetworkInterfaceType != NetworkInterfaceType.Loopback)
            .SelectMany(networkInterface => networkInterface.GetIPProperties().UnicastAddresses)
            .Where(unicastAddress => unicastAddress.Address.AddressFamily == AddressFamily.InterNetwork)
            .Select(unicastAddress => unicastAddress.Address.ToString())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(address => address, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static void TryKillProcess(string processName)
    {
        if (string.IsNullOrWhiteSpace(processName))
        {
            return;
        }

        foreach (var process in Process.GetProcesses())
        {
            try
            {
                if (string.Equals(process.ProcessName, processName, StringComparison.OrdinalIgnoreCase))
                {
                    process.Kill();
                }
            }
            catch
            {
            }
        }
    }
}
