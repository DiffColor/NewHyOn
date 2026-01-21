using System;
using System.IO;
using System.Management;
using System.Net;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.Text;
using System.Text.RegularExpressions;
using System.Linq;
using System.Collections.Generic;
using System.Net.Sockets;
using ConfigPlayer;

namespace TurtleTools
{

    public enum FTP_TYPE { TurtleFTP, FileZilla }

    public class NetworkTools
    { 
        #region FTP

        public const int AGENT_PORT = 8001;
        public const int OPERATOR_PORT = 8008;
        public const int FTP_PORT = 10021;
        public const int SYNC_PORT = 8282;
        public const int FTP_PASV_MIN_PORT = 24000;
        public const int FTP_PASV_MAX_PORT = 24240;
        public const int FTP_THREAD_LIMIT = 8;

        public static string GetFTPServerBaseDir()
        {
            string path = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "FTPSrv");
            if(!Directory.Exists(path))
                Directory.CreateDirectory(path);
            return path;
        }

        public static string GetFTPServerExePath(FTP_TYPE type = FTP_TYPE.FileZilla)
        {
            string path = System.IO.Path.Combine(GetFTPServerBaseDir(), "FileZilla Server.exe");

            switch (type)
            {
                case FTP_TYPE.TurtleFTP:
                    path = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TurtleLabFTPServer_x86.exe");
                    break;

                case FTP_TYPE.FileZilla:
                default:
                    break;
            }

            return path;
        }

        public static string GetFtpConfigFilePath(FTP_TYPE type = FTP_TYPE.FileZilla)
        {
            string path = System.IO.Path.Combine(GetFTPServerBaseDir(), "FileZilla Server.xml");

            switch (type)
            {
                case FTP_TYPE.TurtleFTP:
                    path = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TurtleLabFTPServer.conf");
                    break;

                case FTP_TYPE.FileZilla:
                default:
                    break;
            }

            return path;
        }

        public static void CreateFTPConfigFile(FTP_TYPE type = FTP_TYPE.FileZilla, int port = FTP_PORT, int pasvmin = FTP_PASV_MIN_PORT, int pasvmax = FTP_PASV_MAX_PORT, int threads = FTP_THREAD_LIMIT, string homedir = "")
        {
            string content;

            if (string.IsNullOrEmpty(homedir))
                homedir = FNDTools.GetPagesRootDirPath();

            switch (type)
            {
                case FTP_TYPE.TurtleFTP:
                    content = "BindInterface All\r\nBindPort " + port + "\r\nCommandTimeout 300\r\nConnectTimeout 15\r\nMaxConnections 240\r\nLookupHosts On\r\n\r\n<User \"asdf\">\r\nPassword \"asdf\"\r\nMount \"/\" \"" + homedir.Replace(@"\", @"\\") + "\"\r\nAllow / All\r\n</User>";
                    File.WriteAllText(GetFtpConfigFilePath(type), content, Encoding.GetEncoding("ks_c_5601-1987"));
                    break;

                case FTP_TYPE.FileZilla:
                default:
                    content = 
                        "<FileZillaServer>\r\n"
                       +"   <Groups />\r\n"
                       +"       <Users>\r\n<User Name=\"asdf\">\r\n"
                       + "           <Option Name=\"Pass\">912ec803b2ce49e4a541068d495ab570</Option>\r\n"
                       +"           <Option Name=\"Salt\">6Sd|w_YPIGb{&quot;BqBx?v;hDnd@H4;&lt;sYG.@_ZL#1{&gt;.)3O],bSsLLEbv=^@_G|&lt;$&apos;</Option>\r\n"
                       +"           <Option Name=\"Group\" />\r\n"
                       +"           <Option Name=\"Bypass server userlimit\">0</Option>\r\n"
                       +"           <Option Name=\"User Limit\">0</Option>\r\n"
                       +"           <Option Name=\"IP Limit\">0</Option>\r\n"
                       +"           <Option Name=\"Enabled\">1</Option>\r\n"
                       +"           <Option Name=\"Comments\" />\r\n"
                       +"           <Option Name=\"ForceSsl\">0</Option>\r\n"
                       +"           <IpFilter>\r\n"
                       +"               <Disallowed />\r\n"
                       +"               <Allowed />\r\n"
                       +"           </IpFilter>\r\n"
                       +"           <Permissions>\r\n"
                       +"               <Permission Dir=\"" + homedir + "\">\r\n"
                       +"               <Option Name=\"FileRead\">1</Option>\r\n"
                       +"                 <Option Name=\"FileWrite\">1</Option>\r\n"
                       +"                 <Option Name=\"FileDelete\">1</Option>\r\n"
                       +"                 <Option Name=\"FileAppend\">1</Option>\r\n"
                       +"               <Option Name=\"DirCreate\">1</Option>\r\n"
                       +"               <Option Name=\"DirDelete\">1</Option>\r\n"
                       +"               <Option Name=\"DirList\">1</Option>\r\n"
                       +"               <Option Name=\"DirSubdirs\">1</Option>\r\n"
                       +"               <Option Name=\"IsHome\">1</Option>\r\n"
                       +"               <Option Name=\"AutoCreate\">0</Option>\r\n"
                       +"               </Permission>\r\n"
                       +"           </Permissions>\r\n"
                       +"           <SpeedLimits DlType=\"1\" DlLimit=\"10\" ServerDlLimitBypass=\"0\" UlType=\"1\" UlLimit=\"10\" ServerUlLimitBypass=\"0\">\r\n"
                       +"               <Download />\r\n"
                       +"               <Upload />\r\n"
                       +"           </SpeedLimits>\r\n"
                       +"       </User>\r\n"
                       +"   </Users>\r\n"
                       +"   <Settings>\r\n"
                       +"       <Item name=\"Serverports\" type=\"string\">" + port + "</Item>\r\n"
                       +"       <Item name=\"Number of Threads\" type=\"numeric\">" + threads + "</Item>\r\n"
                       +"       <Item name=\"Maximum user count\" type=\"numeric\">0</Item>\r\n"
                       +"       <Item name=\"Timeout\" type=\"numeric\">120</Item>\r\n"
                       +"       <Item name=\"No Transfer Timeout\" type=\"numeric\">600</Item>\r\n"
                       +"       <Item name=\"Check data connection IP\" type=\"numeric\">2</Item>\r\n"
                       +"       <Item name=\"Service name\" type=\"string\"></Item>\r\n"
                       +"       <Item name=\"Service display name\" type=\"string\"></Item>\r\n"
                       +"       <Item name=\"Force TLS session resumption\" type=\"numeric\">1</Item>\r\n"
                       +"       <Item name=\"Login Timeout\" type=\"numeric\">60</Item>\r\n"
                       +"       <Item name=\"Show Pass in Log\" type=\"numeric\">0</Item>\r\n"
                       +"       <Item name=\"Custom PASV IP type\" type=\"numeric\">2</Item>\r\n"
                       +"       <Item name=\"Custom PASV IP\" type=\"string\"></Item>\r\n"
                       +"       <Item name=\"Custom PASV min port\" type=\"numeric\">" + pasvmin + "</Item>\r\n"
                       +"       <Item name=\"Custom PASV max port\" type=\"numeric\">" + pasvmax + "</Item>\r\n"
                       +"       <Item name=\"Initial Welcome Message\" type=\"string\">Welcome!! Turtle World~!!</Item>\r\n"
                       +"       <Item name=\"Admin port\" type=\"numeric\">14147</Item>\r\n"
                       +"       <Item name=\"Admin Password\" type=\"string\"></Item>\r\n"
                       +"       <Item name=\"Admin IP Bindings\" type=\"string\"></Item>\r\n"
                       +"       <Item name=\"Admin IP Addresses\" type=\"string\"></Item>\r\n"
                       +"       <Item name=\"Enable logging\" type=\"numeric\">1</Item>\r\n"
                       +"       <Item name=\"Logsize limit\" type=\"numeric\">240</Item>\r\n"
                       +"       <Item name=\"Logfile type\" type=\"numeric\">1</Item>\r\n"
                       +"       <Item name=\"Logfile delete time\" type=\"numeric\">14</Item>\r\n"
                       +"       <Item name=\"Disable IPv6\" type=\"numeric\">0</Item>\r\n"
                       +"       <Item name=\"Enable HASH\" type=\"numeric\">0</Item>\r\n"
                       +"       <Item name=\"Download Speedlimit Type\" type=\"numeric\">0</Item>\r\n"
                       +"       <Item name=\"Upload Speedlimit Type\" type=\"numeric\">0</Item>\r\n"
                       +"       <Item name=\"Download Speedlimit\" type=\"numeric\">10</Item>\r\n"
                       +"       <Item name=\"Upload Speedlimit\" type=\"numeric\">10</Item>\r\n"
                       +"       <Item name=\"Buffer Size\" type=\"numeric\">32768</Item>\r\n"
                       +"       <Item name=\"Custom PASV IP server\" type=\"string\">http://ip.filezilla-project.org/ip.php</Item>\r\n"
                       +"       <Item name=\"Use custom PASV ports\" type=\"numeric\">1</Item>\r\n"
                       +"       <Item name=\"Mode Z Use\" type=\"numeric\">0</Item>\r\n"
                       +"       <Item name=\"Mode Z min level\" type=\"numeric\">1</Item>\r\n"
                       +"       <Item name=\"Mode Z max level\" type=\"numeric\">9</Item>\r\n"
                       +"       <Item name=\"Mode Z allow local\" type=\"numeric\">0</Item>\r\n"
                       +"       <Item name=\"Mode Z disallowed IPs\" type=\"string\"></Item>\r\n"
                       +"       <Item name=\"IP Bindings\" type=\"string\">*</Item>\r\n"
                       +"       <Item name=\"IP Filter Allowed\" type=\"string\"></Item>\r\n"
                       +"       <Item name=\"IP Filter Disallowed\" type=\"string\"></Item>\r\n"
                       +"       <Item name=\"Hide Welcome Message\" type=\"numeric\">1</Item>\r\n"
                       +"       <Item name=\"Enable SSL\" type=\"numeric\">0</Item>\r\n"
                       +"       <Item name=\"Allow explicit SSL\" type=\"numeric\">1</Item>\r\n"
                       +"       <Item name=\"SSL Key file\" type=\"string\"></Item>\r\n"
                       +"       <Item name=\"SSL Certificate file\" type=\"string\"></Item>\r\n"
                       +"       <Item name=\"Implicit SSL ports\" type=\"string\">990</Item>\r\n"
                       +"       <Item name=\"Force explicit SSL\" type=\"numeric\">0</Item>\r\n"
                       +"       <Item name=\"Network Buffer Size\" type=\"numeric\">262144</Item>\r\n"
                       +"       <Item name=\"Force PROT P\" type=\"numeric\">1</Item>\r\n"
                       +"       <Item name=\"SSL Key Password\" type=\"string\"></Item>\r\n"
                       +"       <Item name=\"Allow shared write\" type=\"numeric\">0</Item>\r\n"
                       +"       <Item name=\"No External IP On Local\" type=\"numeric\">1</Item>\r\n"
                       +"       <Item name=\"Active ignore local\" type=\"numeric\">1</Item>\r\n"
                       +"       <Item name=\"Autoban enable\" type=\"numeric\">0</Item>\r\n"
                       +"       <Item name=\"Autoban attempts\" type=\"numeric\">10</Item>\r\n"
                       +"       <Item name=\"Autoban type\" type=\"numeric\">0</Item>\r\n"
                       +"       <Item name=\"Autoban time\" type=\"numeric\">1</Item>\r\n"
                       +"       <Item name=\"Minimum TLS version\" type=\"numeric\">0</Item>\r\n"
                       +"       <SpeedLimits>\r\n"
                       +"           <Download />\r\n"
                       +"           <Upload />\r\n"
                       + "      </SpeedLimits>\r\n"
                       +"   </Settings>\r\n"
                       +"</FileZillaServer>\r\n";

                    FileTools.WriteUTF8XML(GetFtpConfigFilePath(type), content);
                    break;
            }
        }

        public static void SetFTPConfigHomeDir(FTP_TYPE type = FTP_TYPE.FileZilla, string homedir = "")
        {
            string configPath = GetFtpConfigFilePath(type);

            if (string.IsNullOrEmpty(homedir))
                homedir = FNDTools.GetPagesRootDirPath();

            if (!File.Exists(configPath))
                CreateFTPConfigFile(type, FTP_PORT, FTP_PASV_MIN_PORT, FTP_PASV_MAX_PORT, FTP_THREAD_LIMIT, homedir);

            switch (type)
            {
                case FTP_TYPE.TurtleFTP:
                    File.WriteAllLines(configPath,
                        File.ReadAllLines(configPath, Encoding.GetEncoding("ks_c_5601-1987")).Select(
                            x =>
                            {
                                x = x.TrimStart(new char[] { ' ', '\t' });

                                if (x.StartsWith("Mount"))
                                    return string.Format("Mount \"/\" \"{0}\"", homedir.Replace(@"\", @"\\"));
                                return x;
                            }).ToArray(), Encoding.GetEncoding("ks_c_5601-1987"));
                    break;

                case FTP_TYPE.FileZilla:
                default:
                    string[] strarr = File.ReadAllLines(configPath).Select(
                                        x =>
                                        {
                                            x = x.TrimStart(new char[] { ' ', '\t' });

                                            if (x.StartsWith("<Permission Dir="))
                                                return string.Format("<Permission Dir=\"{0}\">", homedir);

                                            return x;
                                        }).ToArray();
                    FileTools.WriteUTF8XML(GetFtpConfigFilePath(type), strarr);
                    break;
            }
        }

        public static void SetFTPConfigPort(FTP_TYPE type = FTP_TYPE.FileZilla, int port = FTP_PORT, int pasvMin = FTP_PASV_MIN_PORT, int pasvMax = FTP_PASV_MAX_PORT)
        {
            string configPath = GetFtpConfigFilePath(type);

            if (!File.Exists(configPath))
                CreateFTPConfigFile(type, port, pasvMin, pasvMax);


            switch (type)
            {
                case FTP_TYPE.TurtleFTP:
                    File.WriteAllLines(configPath,
                        File.ReadAllLines(configPath, Encoding.GetEncoding("ks_c_5601-1987")).Select(
                                    x =>
                                    {
                                        x = x.TrimStart(new char[] { ' ', '\t' });

                                        if (x.StartsWith("BindPort"))
                                            return ("BindPort " + port);
                                        return x;

                                    }).ToArray(), Encoding.GetEncoding("ks_c_5601-1987")
                    );
                    break;

                case FTP_TYPE.FileZilla:
                default:
                    string[] strarr = File.ReadAllLines(configPath).Select(
                                        x =>
                                        {
                                            x = x.TrimStart(new char[] { ' ', '\t' });

                                            if (x.StartsWith("<Item name=\"Serverports\" type=\"string\">"))
                                                return string.Format("<Item name=\"Serverports\" type=\"string\">{0}</Item>", port);
                                            if (x.StartsWith("<Item name=\"Custom PASV min port\" type=\"numeric\">"))
                                                return string.Format("<Item name=\"Custom PASV min port\" type=\"numeric\">{0}</Item>", pasvMin);
                                            if (x.StartsWith("<Item name=\"Custom PASV max port\" type=\"numeric\">"))
                                                return string.Format("<Item name=\"Custom PASV max port\" type=\"numeric\">{0}</Item>", pasvMax);
                                            return x;

                                        }).ToArray();

                    FileTools.WriteUTF8XML(GetFtpConfigFilePath(type), strarr);
                    break;
            }
        }

        public static void SetFTPThreadLimit(FTP_TYPE type = FTP_TYPE.FileZilla, int threads = FTP_THREAD_LIMIT)
        {
            string configPath = GetFtpConfigFilePath(type);

            if (!File.Exists(configPath))
                CreateFTPConfigFile(type, FTP_PORT, FTP_PASV_MIN_PORT, FTP_PASV_MAX_PORT, threads);

            string[] strarr = File.ReadAllLines(configPath).Select(
                                x =>
                                {
                                    x = x.TrimStart(new char[] { ' ', '\t' });

                                    if (x.StartsWith("<Item name=\"Number of Threads\" type=\"numeric\">"))
                                        return string.Format("<Item name=\"Number of Threads\" type=\"numeric\">{0}</Item>", threads);
                                    return x;

                                }).ToArray();

            FileTools.WriteUTF8XML(GetFtpConfigFilePath(type), strarr);
        }

        public static void StartFTPSrv(FTP_TYPE type = FTP_TYPE.FileZilla)
        {
            switch (type)
            {
                case FTP_TYPE.TurtleFTP:
                    ProcessTools.LaunchProcess(GetFTPServerExePath(type));
                    break;

                case FTP_TYPE.FileZilla:
                default:
                    string batchStr = "start uniserv.exe \"FileZilla Server.exe -compat-start\"";
                    ProcessTools.ExecuteCommand(batchStr, GetFTPServerBaseDir());
                    break;
            }
        }

        public static void StopFTPSrv()
        {
            string batchStr =
                                    "\"FileZilla Server.exe\" -compat-stop\r\n"
                            + " & " + "pskill.exe \"FileZilla server.exe\" c";
            ProcessTools.ExecuteCommand(batchStr, GetFTPServerBaseDir());
            ProcessTools.KillExeProcess("TurtleLabFTPServer_x86");
        }

        #endregion

        #region IP Address
        public static IPAddress GetAutoIP()
        {
            string strHostName = Dns.GetHostName();
            IPHostEntry iphostentry = Dns.GetHostEntry(strHostName);

            NetworkInterface[] adapters = NetworkInterface.GetAllNetworkInterfaces();

            foreach (NetworkInterface adapter in adapters)
            {
                if (adapter.NetworkInterfaceType == NetworkInterfaceType.Ethernet || 
                    adapter.NetworkInterfaceType == NetworkInterfaceType.Wireless80211)
                {
                    foreach (UnicastIPAddressInformation uip in adapter.GetIPProperties().UnicastAddresses)
                    {
                        if (uip.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                        {
                            foreach (IPAddress ip in iphostentry.AddressList)
                            {
                                if (uip.Address.ToString().Equals(ip.ToString()))
                                {
                                    return ip;
                                }
                            }
                        }
                    }
                }
            }
            return IPAddress.Loopback;
        }

        public static bool PingTest(string ipStr)
        {
            // Ping Test Values
            Ping pingSender = new Ping();
            PingOptions options = new PingOptions();
            PingReply reply;
            string data = "";
            byte[] buffer;
            int timeout = 240;

            try
            {
                options.DontFragment = true;

                buffer = Encoding.ASCII.GetBytes(data);
                reply = pingSender.Send(ipStr, timeout, buffer, options);

                if (reply.Status == IPStatus.Success)
                {
                    return true;
                }
            }
            catch (Exception e)
            {
            }
            return false;
        }

        public static IPAddress[] GetIPList()
        {
            String strHostName = Dns.GetHostName();
            IPHostEntry iphostentry = Dns.GetHostEntry(strHostName);
            return iphostentry.AddressList;
        }

        public static bool CheckIsIPv4(IPAddress ipaddress)
        {
            Regex regex = new Regex(@"^(\d{1,2}|1\d\d|2[0-4]\d|25[0-5])\.(\d{1,2}|1\d\d|2[0-4]\d|25[0-5])\.(\d{1,2}|1\d\d|2[0-4]\d|25[0-5])\.(\d{1,2}|1\d\d|2[0-4]\d|25[0-5])$");

            return regex.IsMatch(ipaddress.ToString());
        }

        public string GetLocalIP()
        {
            String strHostName = Dns.GetHostName();
            IPHostEntry iphostentry = Dns.GetHostEntry(strHostName);
            IPAddress gateway = GetGatewayAddress();

            String IPStr = "";

            foreach (IPAddress ipaddress in iphostentry.AddressList)
            {
                byte[] ipBytes = ipaddress.GetAddressBytes();
                byte[] gatewayBytes = gateway.GetAddressBytes();

                if (ipBytes[0] == gatewayBytes[0] &&
                ipBytes[1] == gatewayBytes[1])
                {
                    IPStr = ipaddress.ToString();
                    return IPStr;
                }

            }
            return IPStr;
        }
        public IPAddress GetGatewayAddress()
        {
            NetworkInterface[] adapters = NetworkInterface.GetAllNetworkInterfaces();
            foreach (NetworkInterface adapter in adapters)
            {
                IPInterfaceProperties adapterProperties = adapter.GetIPProperties();
                GatewayIPAddressInformationCollection addresses = adapterProperties.GatewayAddresses;

                if (addresses.Count > 0)
                {
                    foreach (GatewayIPAddressInformation address in addresses)
                    {
                        return address.Address;
                    }
                }
            }
            return IPAddress.None;
        }

        [DllImport("iphlpapi.dll", ExactSpelling = true)]
        static extern int SendARP(int DestIP, int SrcIP, byte[] pMacAddr, ref uint PhyAddrLen);

        public static string GetMacAddressFromIP(string ip)
        {
            IPAddress dst = IPAddress.Parse(ip);

            byte[] macAddr = new byte[6];
            uint macAddrLen = (uint)6;

            if (SendARP(BitConverter.ToInt32(dst.GetAddressBytes(), 0), 0, macAddr, ref macAddrLen) != 0)
                return string.Empty;

            string[] str = new string[(int)macAddrLen];

            for (int i = 0; i < macAddrLen; i++)
                str[i] = macAddr[i].ToString("x2");

            return string.Join("", str);
        }

        public static string GetMACAddressByWMI()
        {
            ManagementObjectSearcher objMOS = new ManagementObjectSearcher("Win32_NetworkAdapterConfiguration");
            ManagementObjectCollection objMOC = objMOS.Get();
            string MACAddress = String.Empty;
            foreach (ManagementObject objMO in objMOC)
            {
                if (MACAddress == String.Empty) // only return MAC Address from first card   
                {
                    MACAddress = objMO["MacAddress"].ToString();
                }
                objMO.Dispose();
            }
            MACAddress = MACAddress.Replace(":", "");
            return MACAddress;
        }

        public static string GetMacAddressByWMIFromIP(string ip)
        {
            string rtn = string.Empty;
            ObjectQuery oq = new System.Management.ObjectQuery("SELECT * FROM Win32_NetworkAdapterConfiguration WHERE IPEnabled='TRUE'");
            ManagementObjectSearcher query1 = new ManagementObjectSearcher(oq);
            foreach (ManagementObject mo in query1.Get())
            {
                string[] address = (string[])mo["IPAddress"];
                if (address[0] == ip && mo["MACAddress"] != null)
                {
                    rtn = mo["MACAddress"].ToString();
                    rtn = rtn.Replace(":", "");
                    break;
                }
            }
            return rtn;
        }

        public static string GetVPNIP()
        {
            string strHostName = Dns.GetHostName();
            IPHostEntry iphostentry = Dns.GetHostEntry(strHostName);

            NetworkInterface[] interfaces = NetworkInterface.GetAllNetworkInterfaces();

            foreach (NetworkInterface adapter in interfaces)
            {
                if (adapter.NetworkInterfaceType == NetworkInterfaceType.Ppp || adapter.Description.Contains("TAP-Windows Adapter"))
                {
                    foreach (UnicastIPAddressInformation uip in adapter.GetIPProperties().UnicastAddresses)
                    {
                        if (uip.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                        {
                            foreach (IPAddress ip in iphostentry.AddressList)
                            {
                                if (uip.Address.ToString().Equals(ip.ToString()))
                                {
                                    return ip.ToString();
                                }
                            }
                        }
                    }
                }
            }

            return null;
        }

        public static string GetMACAddressBySystemNet()
        {
            NetworkInterface[] nics = NetworkInterface.GetAllNetworkInterfaces();
            String sMacAddress = string.Empty;
            foreach (NetworkInterface adapter in nics)
            {
                if (sMacAddress == String.Empty)// only return MAC Address from first card  
                {
                    IPInterfaceProperties properties = adapter.GetIPProperties();
                    sMacAddress = adapter.GetPhysicalAddress().ToString();
                    sMacAddress = sMacAddress.Replace(":", "");
                    break;
                }
            } return sMacAddress;
        }

        public static List<string> GetAllMACAddressesBySystemNet()
        {
            List<string> addrList = new List<string>();

            NetworkInterface[] nics = NetworkInterface.GetAllNetworkInterfaces();
            String sMacAddress = string.Empty;
            foreach (NetworkInterface adapter in nics)
            {
                IPInterfaceProperties properties = adapter.GetIPProperties();
                sMacAddress = adapter.GetPhysicalAddress().ToString();
                sMacAddress = sMacAddress.Replace(":", "");
                addrList.Add(sMacAddress);
            }

            return addrList;
        }

        public static string GetAddressFromCurrentCallback()
        {
            OperationContext context = OperationContext.Current;
            MessageProperties messageProperties = context.IncomingMessageProperties;
            RemoteEndpointMessageProperty endpointProperty =
                            messageProperties[RemoteEndpointMessageProperty.Name]
                            as RemoteEndpointMessageProperty;

            return endpointProperty.Address;
        }
        #endregion

        public static bool IsValidUrl(string urlString)
        {
            Uri uri;
            return Uri.TryCreate(urlString, UriKind.Absolute, out uri)
                && (uri.Scheme == Uri.UriSchemeHttp
                 || uri.Scheme == Uri.UriSchemeHttps
                 || uri.Scheme == Uri.UriSchemeFile
                /*|| uri.Scheme == Uri.UriSchemeFtp
                || uri.Scheme == Uri.UriSchemeMailto*/);
        }


        public static string ConvertUrlStr(string _url)
        {
            if (!NetworkTools.IsValidUrl(_url))
            {
                _url = "http://" + _url;
            }

            return _url;
        }

        /* set swf trust zone */
        public static void SetFlashTrustZone(string targetpath)
        {
            // The folder for the roaming current user 
            string folder = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

            // Combine the base folder with your specific folder....
            string securityFolder = Path.Combine(folder, @"Macromedia\Flash Player\#Security\FlashPlayerTrust");

            // Check if folder exists and if not, create it
            if (!Directory.Exists(securityFolder))
                Directory.CreateDirectory(securityFolder);

            List<string> pathList = new List<string>();
            pathList.Add(string.Format("{0}", targetpath, Environment.NewLine));

            string fpath = Path.Combine(securityFolder, "DMZ.cfg");

            if (File.Exists(fpath))
            {
                foreach (string line in File.ReadAllLines(fpath))
                {
                    if (line.Equals(targetpath, StringComparison.CurrentCultureIgnoreCase))
                        return;
                    pathList.Add(string.Format("{0}", line, Environment.NewLine));
                }
                File.WriteAllLines(fpath, pathList.ToArray());
            }
            else
            {
                File.WriteAllText(fpath, string.Format("{0}", targetpath, Environment.NewLine));
            }
        }
    }
}
