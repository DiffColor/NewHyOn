using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace NewHyOn.Player.Settings.Services;

public static class SystemInfoService
{
    public static IReadOnlyCollection<string> GetLocalIpv4Addresses()
    {
        try
        {
            NetworkInterface[] interfaces = NetworkInterface.GetAllNetworkInterfaces();
            if (interfaces == null || interfaces.Length == 0)
            {
                return System.Array.Empty<string>();
            }

            return interfaces
                .Where(networkInterface =>
                    networkInterface != null &&
                    networkInterface.OperationalStatus == OperationalStatus.Up &&
                    networkInterface.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                .SelectMany(networkInterface => networkInterface.GetIPProperties().UnicastAddresses)
                .Where(unicastAddress => unicastAddress.Address.AddressFamily == AddressFamily.InterNetwork)
                .Select(unicastAddress => unicastAddress.Address.ToString())
                .Distinct(System.StringComparer.OrdinalIgnoreCase)
                .OrderBy(address => address, System.StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        catch
        {
            return System.Array.Empty<string>();
        }
    }
}
