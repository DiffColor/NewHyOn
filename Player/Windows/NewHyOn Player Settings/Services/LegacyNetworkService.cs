using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
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
        string hostName = Dns.GetHostName();
        IPHostEntry hostEntry = Dns.GetHostEntry(hostName);
        foreach (NetworkInterface adapter in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (adapter.NetworkInterfaceType is not (NetworkInterfaceType.Ethernet or NetworkInterfaceType.Wireless80211))
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
        foreach (NetworkInterface adapter in NetworkInterface.GetAllNetworkInterfaces())
        {
            string macAddress = adapter.GetPhysicalAddress().ToString().Replace(":", string.Empty);
            if (!string.IsNullOrWhiteSpace(macAddress))
            {
                return macAddress;
            }
        }

        return string.Empty;
    }

    public static List<string> GetAllMacAddresses()
    {
        return NetworkInterface.GetAllNetworkInterfaces()
            .Select(x => x.GetPhysicalAddress().ToString().Replace(":", string.Empty))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static string GetUuid12()
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

        return string.Empty;
    }
}
