using System;
using System.Net;
using System.Net.Sockets;

namespace TurtleTools
{
    public class WOL_Sender
    {
        /// Test a MACAddress byte Array.
        protected static bool Test(byte[] macAddress)
        {
            if (macAddress == null) return false;
            if (macAddress.Length != 6) return false;

            return true;
        }

        /// Test a MACAddress string.
        protected static bool Test(string macAddress)
        {

            var valid_chars = "0123456789ABCDEFabcdef";

            if (string.IsNullOrEmpty(macAddress)) return false;
            if (macAddress.Length != 12) return false;

            foreach (var c in macAddress)
            {
                if (valid_chars.IndexOf(c) < 0)
                {
                    return false;
                }
            }

            return true;
        }

        /// Parse a MACAddress string into a byte array.
        protected static byte[] Parse(string macAddress)
        {
            byte[] mac = new byte[6];

            if (!Test(macAddress))
                throw new ArgumentException(
                    "Invalid MACAddress string.",
                    "macAddress",
                    null);

            for (var i = 0; i < 6; i++)
            {
                var t = macAddress.Substring((i * 2), 2);
                mac[i] = Convert.ToByte(t, 16);
            }

            return mac;
        }

        /// Attempt to parse a MACAddress string
        ///   without throwing an Exception.
        protected static bool TryParse(string macAddress, out byte[] Address)
        {
            try
            {
                Address = Parse(macAddress);
                return true;
            }
            catch
            {
                Address = null;
                return false;
            }
        }

        /// Convert a byte array MACAddress to a string.
        static string ToString(byte[] macAddress)
        {
            if (!Test(macAddress))
                throw new ArgumentException(
                    "Invalid MACAddress byte array.",
                    "macAddress",
                    null);

            return BitConverter.ToString(macAddress).Replace("-", "");
        }


        /// Sends a Wake-On-LAN 'magic' packet to
        ///   the specified MACAddress string.
        public static void SendWOLPacket(string macAddress)
        {

            string filteredMacStr = macAddress.Replace(":", "").Replace("-", "");

            if (!Test(filteredMacStr))
                throw new ArgumentException(
                    "Invalid MACAddress string.",
                    "macAddress",
                    null);

            byte[] mac = Parse(filteredMacStr);

            SendWOLPacket(mac);
        }

        /// Sends a Wake-On-LAN 'magic' packet to
        ///   the specified MACAddress byte array.
        public static void SendWOLPacket(byte[] macAddress)
        {

            if (!Test(macAddress))
                throw new ArgumentException(
                    "Invalid MACAddress byte array.",
                    "macAddress",
                    null);

            // WOL 'magic' packet is sent over UDP.
            using (UdpClient client = new UdpClient())
            {

                // Send to: 255.255.255.0:40000 over UDP.
                client.Connect(IPAddress.Broadcast, 40000);

                // Two parts to a 'magic' packet:
                //     First is 0xFFFFFFFFFFFF,
                //     Second is 16 * MACAddress.
                byte[] packet = new byte[17 * 6];

                // Set to: 0xFFFFFFFFFFFF.
                for (int i = 0; i < 6; i++)
                {
                    packet[i] = 0xFF;
                }

                // Set to: 16 * MACAddress
                for (int i = 1; i <= 16; i++)
                {
                    for (int j = 0; j < 6; j++)
                    {
                        packet[i * 6 + j] = macAddress[j];
                    }
                }

                // Send WOL 'magic' packet.
                client.Send(packet, packet.Length);
            }
        }

    }
}
