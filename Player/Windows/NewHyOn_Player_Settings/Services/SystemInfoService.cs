using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace NewHyOn.Player.Settings.Services;

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
            .Distinct(System.StringComparer.OrdinalIgnoreCase)
            .OrderBy(address => address, System.StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
