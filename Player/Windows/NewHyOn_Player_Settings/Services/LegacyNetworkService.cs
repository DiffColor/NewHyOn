using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Management;
using System.Runtime.InteropServices;

namespace NewHyOn.Player.Settings.Services;

public static class LegacyNetworkService
{
    public const int SIGNALR_PORT = 5000;
    public const int FTP_PORT = 10021;
    public const int SYNC_PORT = 8282;
    public const int FTP_PASV_MIN_PORT = 24000;
    public const int FTP_PASV_MAX_PORT = 24240;

    [DllImport("iphlpapi.dll", ExactSpelling = true)]
    private static extern int SendARP(int destIp, int srcIp, byte[] macAddr, ref uint phyAddrLen);

    public static IPAddress GetAutoIp()
    {
        try
        {
            string hostName = Dns.GetHostName();
            IPHostEntry hostEntry = Dns.GetHostEntry(hostName);
            NetworkInterface[] adapters = NetworkInterface.GetAllNetworkInterfaces();
            if (adapters == null || adapters.Length == 0)
            {
                return IPAddress.Loopback;
            }

            foreach (NetworkInterface adapter in adapters)
            {
                if (adapter == null ||
                    adapter.NetworkInterfaceType is not (NetworkInterfaceType.Ethernet or NetworkInterfaceType.Wireless80211))
                {
                    continue;
                }

                foreach (UnicastIPAddressInformation address in adapter.GetIPProperties().UnicastAddresses)
                {
                    if (address.Address.AddressFamily != AddressFamily.InterNetwork)
                    {
                        continue;
                    }

                    foreach (IPAddress hostAddress in hostEntry.AddressList)
                    {
                        if (address.Address.Equals(hostAddress))
                        {
                            return hostAddress;
                        }
                    }
                }
            }
        }
        catch
        {
        }

        return IPAddress.Loopback;
    }

    public static string GetMacAddressFromIp(string ip)
    {
        IPAddress destination = IPAddress.Parse(ip);
        byte[] macAddress = new byte[6];
        uint macAddressLength = 6;
        if (SendARP(BitConverter.ToInt32(destination.GetAddressBytes(), 0), 0, macAddress, ref macAddressLength) != 0)
        {
            return string.Empty;
        }

        return string.Join(string.Empty, macAddress.Take((int)macAddressLength).Select(x => x.ToString("x2")));
    }

    public static string GetFirstMacAddress()
    {
        try
        {
            NetworkInterface[] adapters = NetworkInterface.GetAllNetworkInterfaces();
            if (adapters == null || adapters.Length == 0)
            {
                return string.Empty;
            }

            foreach (NetworkInterface adapter in adapters)
            {
                if (adapter == null)
                {
                    continue;
                }

                string macAddress = adapter.GetPhysicalAddress()?.ToString().Replace(":", string.Empty) ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(macAddress))
                {
                    return macAddress;
                }
            }
        }
        catch
        {
        }

        return string.Empty;
    }

    public static List<string> GetAllMacAddresses()
    {
        try
        {
            NetworkInterface[] adapters = NetworkInterface.GetAllNetworkInterfaces();
            if (adapters == null || adapters.Length == 0)
            {
                return new List<string>();
            }

            return adapters
                .Where(x => x != null)
                .Select(x => x.GetPhysicalAddress()?.ToString().Replace(":", string.Empty) ?? string.Empty)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch
        {
            return new List<string>();
        }
    }

    public static string GetUuid12()
    {
        try
        {
            using ManagementClass computerSystemProduct = new("Win32_ComputerSystemProduct");
            using ManagementObjectCollection instances = computerSystemProduct.GetInstances();
            foreach (ManagementObject instance in instances)
            {
                string? rawUuid = instance.Properties["UUID"]?.Value?.ToString();
                if (string.IsNullOrWhiteSpace(rawUuid))
                {
                    continue;
                }

                string[] parts = rawUuid.Split('-', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length > 0)
                {
                    return parts[^1];
                }
            }
        }
        catch
        {
        }

        return string.Empty;
    }
}
