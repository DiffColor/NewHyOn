using Microsoft.Win32;
using NetFwTypeLib;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;

namespace TurtleTools
{
    public class SecurityTools
    {
        public static readonly int VistaMajorVersion = 6; 

        #region Firewall
        public static List<string> CreateNetshCmdList(string procName, string procPath , Dictionary<string, int> ports)
        {
            // initialize and open ports command
            List<string> cmdList = CreateOpenPortNetshCmdList(ports);

            // set author command
            cmdList.Add(CreateAuthorAppNetshCmdStr(procName, procPath));
            //string ftpExecPath = NetworkTools.GetFtpExcuteFilePath();
            //cmdList.Add(CreateAuthorAppNetshCmdStr(System.IO.Path.GetFileNameWithoutExtension(ftpExecPath), ftpExecPath));

            return cmdList;
        }
        
        public static void DisableBlockingFTPTraffic(bool disable=true)
        {
            string state = (disable == true) ? "disable":"enable";
            ReleaseFirewallRules(new List<string>() { string.Format("netsh advfirewall set global StatefulFTP {0}", state) });
            //ProcessTools.ExecuteCommand(string.Format("netsh advfirewall set global StatefulFTP {0}", state));
        }

        //public static void OpenPasvFTPPorts(string name, int port = NetworkTools.FTP_PORT, int pasvmin = NetworkTools.FTP_PASV_MIN_PORT, int pasvmax = NetworkTools.FTP_PASV_MAX_PORT)
        //{
        //    if (System.Environment.OSVersion.Version.Major >= VistaMajorVersion)
        //    {
        //        string order = string.Format("netsh advfirewall firewall add rule name=\"{0}\" dir=in action=allow protocol=TCP localport={1},{2}-{3}", name, port, pasvmin, pasvmax);
        //        ReleaseFirewallRules(new List<string>() { order });
        //    }
        //    else
        //    {
        //        string order = string.Format("FOR /L %I IN ({0},1,{1}) DO netsh firewall add portopening TCP %I \"{2}\"%I", pasvmin, pasvmax, name);
        //        ProcessTools.ExecuteCommand(order);
        //    }
        //}
        public static void OpenPasvFTPPorts(string name, int pasvmin = NetworkTools.FTP_PASV_MIN_PORT, int pasvmax = NetworkTools.FTP_PASV_MAX_PORT)
        {
            if (System.Environment.OSVersion.Version.Major >= VistaMajorVersion)
            {
                string order = string.Format("netsh advfirewall firewall add rule name=\"{0}\" dir=in action=allow protocol=TCP localport={1}-{2}", name, pasvmin, pasvmax);
                ReleaseFirewallRules(new List<string>() { order });
            }
            else
            {
                string order = string.Format("FOR /L %I IN ({0},1,{1}) DO netsh firewall add portopening TCP %I \"{2}\"%I", pasvmin, pasvmax, name);
                ProcessTools.ExecuteCommand(order);
            }
        }

        //public static void DeletePasvFTPPorts(string name, int port = NetworkTools.FTP_PORT, int pasvmin = NetworkTools.FTP_PASV_MIN_PORT, int pasvmax = NetworkTools.FTP_PASV_MAX_PORT)
        //{
        //    if (System.Environment.OSVersion.Version.Major >= VistaMajorVersion)
        //    {
        //        string order = string.Format("netsh advfirewall firewall delete rule name=\"{0}\" dir=in protocol=TCP localport={1},{2}-{3}", name, port, pasvmin, pasvmax);
        //        ReleaseFirewallRules(new List<string>() { order });
        //    }
        //    else
        //    {
        //        string order = string.Format("FOR /L %I IN ({0},1,{1}) DO netsh firewall delete portopening TCP %I \"{2}\"%I", pasvmin, pasvmax, name);
        //        ProcessTools.ExecuteCommand(order);
        //    }
        //}
        public static void DeletePasvFTPPorts(string name, int pasvmin = NetworkTools.FTP_PASV_MIN_PORT, int pasvmax = NetworkTools.FTP_PASV_MAX_PORT)
        {
            if (System.Environment.OSVersion.Version.Major >= VistaMajorVersion)
            {
                string order = string.Format("netsh advfirewall firewall delete rule name=\"{0}\" dir=in protocol=TCP localport={1}-{2}", name, pasvmin, pasvmax);
                ReleaseFirewallRules(new List<string>() { order });
            }
            else
            {
                string order = string.Format("FOR /L %I IN ({0},1,{1}) DO netsh firewall delete portopening TCP %I \"{2}\"%I", pasvmin, pasvmax, name);
                ProcessTools.ExecuteCommand(order);
            }
        }

        public static void ReleaseFirewallRules(List<string> commands)
        {
            try
            {
                ProcessStartInfo startInfo = new ProcessStartInfo();
                startInfo.FileName = "cmd.exe";
                startInfo.Verb = "runas";
                startInfo.Arguments = "/env /user:" + "Administrator" + " cmd";
                startInfo.WindowStyle = ProcessWindowStyle.Minimized;
                startInfo.CreateNoWindow = true;
                startInfo.UseShellExecute = false;
                startInfo.RedirectStandardInput = true;
                startInfo.RedirectStandardOutput = true;
                startInfo.RedirectStandardError = true;

                Process process = new Process();
                process.EnableRaisingEvents = false;
                process.StartInfo = startInfo;
                process.Start();
                foreach (string command in commands)
                {
                    process.StandardInput.Write(command + Environment.NewLine);
                }
                process.StandardInput.Close();

                //string result = process.StandardOutput.ReadToEnd();
                //string error = process.StandardError.ReadToEnd();

                process.WaitForExit();
                process.Close();
            }
            catch(Exception e)  { }
        }

        public static string CreateAuthorAppNetshCmdStr(string processName, string processPath)
        {
            string FirewallCmd = "netsh firewall add allowedprogram \"{1}\" \"{0}\" ENABLE";
            string AdvanceFirewallCmd = "netsh advfirewall firewall add rule name=\"{0}\" dir=in action=allow program=\"{1}\" enable=yes";

            string cmdStr = FirewallCmd;
            if (System.Environment.OSVersion.Version.Major >= VistaMajorVersion)
            {
                cmdStr = AdvanceFirewallCmd;
            }

            if (string.IsNullOrEmpty(processName))
            {
                processName = GetProcNameFromExecPath(processPath);
            }
            else if (string.IsNullOrEmpty(processPath))
            {
                processPath = GetExecPathFromProcName(processName);
            }

            return String.Format(cmdStr, processName, processPath);
        }

        public static List<string> CreateAuthorAppNetshCmdList(Dictionary<string, string> ALLOW_PROGS)
        {
            string FirewallCmd = "netsh firewall add allowedprogram \"{1}\" \"{0}\" ENABLE";
            string AdvanceFirewallCmd = "netsh advfirewall firewall add rule name=\"{0}\" dir=in action=allow program=\"{1}\" enable=yes";

            List<string> cmdList = new List<string>();

            string cmdStr = FirewallCmd;
            if (System.Environment.OSVersion.Version.Major >= VistaMajorVersion)
            {
                cmdStr = AdvanceFirewallCmd;
            }

            string key = string.Empty;
            string value = string.Empty;
            foreach (KeyValuePair<string, string> kvp in ALLOW_PROGS)
            {
                key = kvp.Key;
                value = kvp.Value;

                if (string.IsNullOrEmpty(key))
                {
                    key = GetProcNameFromExecPath(value);
                }
                else if (string.IsNullOrEmpty(value))
                {
                    value = GetExecPathFromProcName(key);
                }

                cmdList.Add(string.Format(cmdStr, key, value));
            }


            return cmdList;
        }
        
        public static List<string> CreateOpenPortNetshCmdList(Dictionary<string, int> OPEN_PORTS)
        {
            List<string> cmdList = new List<string>();

            string FirewallCmd = "netsh firewall add portopening TCP {1} \"{0}\"";
            string AdvanceFirewallCmd = "netsh advfirewall firewall add rule name=\"{0}\" dir=in action=allow protocol=TCP localport={1}";

            string cmdStr = FirewallCmd;
            if (System.Environment.OSVersion.Version.Major >= VistaMajorVersion)
            {
                cmdStr = AdvanceFirewallCmd;
            }

            foreach (KeyValuePair<string, int> kvp in OPEN_PORTS)
            {
                cmdList.Add(string.Format(cmdStr, kvp.Key, kvp.Value));
            }

            return cmdList;
        }

        public static bool NeedToAddRule(string ruleName)
        {
            ProcessStartInfo info = null;
            string result = string.Empty;
            try
            {
                using (Process proc = new Process())
                {
                    string AdvanceFirewallCmd = string.Format("advfirewall firewall show rule name=\"{0}\"", ruleName);

                    string args = AdvanceFirewallCmd;
                    if (System.Environment.OSVersion.Version.Major < VistaMajorVersion)
                    {
                        DeleteRule(ruleName);
                        return true;
                    }

                    info = new ProcessStartInfo("netsh", args);
                    proc.StartInfo = info;
                    proc.StartInfo.UseShellExecute = false;
                    proc.StartInfo.CreateNoWindow = true;
                    proc.StartInfo.RedirectStandardOutput = true;
                    proc.Start();

                    while ((result = proc.StandardOutput.ReadLine()) != null)
                    {
                        if (result.Replace(" ", String.Empty) == "Enabled:Yes")
                        {
                            return false;
                        }
                        if (result.Replace(" ", String.Empty) == "사용:예")
                        {
                            return false;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
            }
            return true;
        }

        public static void SetICMP()
        {
            ProcessStartInfo info = null;
            try
            {
                using (Process proc = new Process())
                {
                    string FirewallCmd = "firewall set icmpsetting 8 enable";
                    string AdvanceFirewallCmd = "advfirewall firewall add rule name=\"ICMP Allow incoming V4 echo request\" protocol=icmpv4:8,any dir=in action=allow";

                    string args = FirewallCmd;
                    if (System.Environment.OSVersion.Version.Major >= VistaMajorVersion)
                    {
                        args = AdvanceFirewallCmd;
                    }

                    info = new ProcessStartInfo("netsh", args);
                    proc.StartInfo = info;
                    proc.StartInfo.UseShellExecute = false;
                    proc.StartInfo.CreateNoWindow = true;
                    proc.StartInfo.RedirectStandardOutput = true;
                    proc.Start();
                }
            }
            catch (Exception ex)
            {
            }
        }

        public static void DeleteRule(string ruleName, string progPath = "")
        {
            ProcessStartInfo info = null;
            try
            {
                using (Process proc = new Process())
                {
                    string FirewallCmd = string.Format("firewall delete allowedprogram \"{0}\"", progPath);
                    string AdvanceFirewallCmd = string.Format("advfirewall firewall delete rule name=\"{0}\" program=\"{1}\"", ruleName, progPath);

                    string args = FirewallCmd;
                    if (System.Environment.OSVersion.Version.Major >= VistaMajorVersion)
                    {
                        args = AdvanceFirewallCmd;
                    }

                    info = new ProcessStartInfo("netsh", args);
                    proc.StartInfo = info;
                    proc.StartInfo.UseShellExecute = false;
                    proc.StartInfo.CreateNoWindow = true;
                    proc.StartInfo.RedirectStandardOutput = true;
                    proc.Start();
                }
            }
            catch (Exception ex)
            {
            }
        }

        public static void DeletePorts(string ruleName, int port)
        {
            ProcessStartInfo info = null;
            try
            {
                using (Process proc = new Process())
                {
                    string FirewallCmd = string.Format("firewall delete portopening protocol=TCP port=\"{0}\"", port);
                    string AdvanceFirewallCmd = string.Format("advfirewall firewall delete rule name=\"{0}\" protocol=TCP localport=\"{1}\"", ruleName, port);

                    string args = FirewallCmd;
                    if (System.Environment.OSVersion.Version.Major >= VistaMajorVersion)
                    {
                        args = AdvanceFirewallCmd;
                    }

                    info = new ProcessStartInfo("netsh", args);
                    proc.StartInfo = info;
                    proc.StartInfo.UseShellExecute = false;
                    proc.StartInfo.CreateNoWindow = true;
                    proc.StartInfo.RedirectStandardOutput = true;
                    proc.Start();
                }
            }
            catch (Exception ex)
            {
            }
        }

        public static string GetExecPathFromProcName(string procName)
        {
            Process[] process = Process.GetProcessesByName(procName);
            if (process != null)
            {
                if (process.Length > 0)
                {
                    return process[0].MainModule.FileName;
                }
            }
            return string.Empty;
        }

        public static string GetProcNameFromExecPath(string execPath)
        {
            return AssemblyName.GetAssemblyName(execPath).ToString();
        }

        private static readonly Type NetFwMgrType = Type.GetTypeFromProgID("HNetCfg.FwMgr", false);
        private static readonly Type policyType = Type.GetTypeFromProgID("HNetCfg.FwPolicy2");  // Windows 7/8/8.1
        private static readonly Type NetFwPortType = Type.GetTypeFromProgID("HNetCfg.FWOpenPort", false);

        public static void DisableFirewall()
        {
            if (System.Environment.OSVersion.Version.Major >= VistaMajorVersion)
            {
                if (IsFirewallEnabled())
                {
                    INetFwPolicy2 firewall = (INetFwPolicy2)Activator.CreateInstance(policyType);
                    firewall.set_FirewallEnabled(
                                   NET_FW_PROFILE_TYPE2_.NET_FW_PROFILE2_PRIVATE, false);
                    firewall.set_FirewallEnabled(
                                   NET_FW_PROFILE_TYPE2_.NET_FW_PROFILE2_PUBLIC, false);
                    firewall.set_FirewallEnabled(
                                   NET_FW_PROFILE_TYPE2_.NET_FW_PROFILE2_DOMAIN, false);
                }
            }
            else
            {
                if (IsFirewallEnabled())
                {
                    INetFwMgr mgr = (INetFwMgr)Activator.CreateInstance(NetFwMgrType);
                    mgr.LocalPolicy.CurrentProfile.FirewallEnabled = false;
                }
            }
        }

        public static void GloballyOpenPort(Dictionary<string, int> portDics)
        {
            INetFwMgr mgr = (INetFwMgr)Activator.CreateInstance(NetFwMgrType);

            try
            {
                foreach (KeyValuePair<string, int> kvp in portDics)
                {
                    INetFwOpenPort port = (INetFwOpenPort)Activator.CreateInstance(NetFwPortType);
                    port.Name = kvp.Key;
                    port.Port = kvp.Value;
                    port.Scope = NET_FW_SCOPE_.NET_FW_SCOPE_ALL;
                    port.Protocol = NET_FW_IP_PROTOCOL_.NET_FW_IP_PROTOCOL_TCP;
                    port.IpVersion = NET_FW_IP_VERSION_.NET_FW_IP_VERSION_ANY;
                    mgr.LocalPolicy.CurrentProfile.GloballyOpenPorts.Add(port);
                }
            } catch (Exception ex) {}
        }

        public static bool IsFirewallEnabled()
        {
            INetFwMgr mgr = (INetFwMgr)Activator.CreateInstance(NetFwMgrType);
            return mgr.LocalPolicy.CurrentProfile.FirewallEnabled;
        }
        #endregion

        #region Registry Key & Value
        public static string ReadRegKey(string subkeys, string valueKey, bool isHKLM = false)
        {
            try
            {
                // Opening the registry key
                RegistryKey baseKey;

                if (isHKLM)
                {
                    baseKey = Registry.LocalMachine;
                }
                else
                {
                    baseKey = Registry.CurrentUser;
                }

                RegistryKey rKey = baseKey.OpenSubKey("Software");
                foreach (string key in subkeys.Split('\\'))
                {
                    // Open a subKey as read-only
                    rKey = rKey.OpenSubKey(key);
                    // If the RegistrySubKey doesn't exist -> (null)
                    if (rKey == null)
                    {
                        return null;
                    }
                }
                // If the RegistryKey exists I get its value
                // or null is returned.
                return (string)rKey.GetValue(valueKey);
            }
            catch (Exception e)
            {
                return null;
            }
        }

        public static void WriteRegKey(string subkeys, string valueKey, object value, RegistryValueKind valueKind = RegistryValueKind.String, bool isHKLM = false)
        {
            try
            {
                RegistryKey baseKey;

                if (isHKLM)
                {
                    baseKey = Registry.LocalMachine;
                }
                else
                {
                    baseKey = Registry.CurrentUser;
                }

                RegistryKey rKey = baseKey.CreateSubKey("Software");
                foreach (string key in subkeys.Split('\\'))
                {
                    rKey = rKey.CreateSubKey(key);
                }

                rKey.SetValue(valueKey, value, valueKind);
            }
            catch (Exception e)
            {
            }
        }

        public static byte[] StringToByteArray(string hex)
        {
            return Enumerable.Range(0, hex.Length)
                             .Where(x => x % 2 == 0)
                             .Select(x => Convert.ToByte(hex.Substring(x, 2), 16))
                             .ToArray();
        }
        #endregion


        public static void DisableUAC()
        {
            string HKLM_SubKey = "Microsoft\\Windows\\CurrentVersion\\Policies\\System";

            string HKLM_ValueKey1 = "EnableLUA";
            WriteRegKey(HKLM_SubKey, HKLM_ValueKey1, 0, RegistryValueKind.DWord, true);

            string HKLM_ValueKey2 = "ConsentPromptBehaviorAdmin";
            WriteRegKey(HKLM_SubKey, HKLM_ValueKey2, 0, RegistryValueKind.DWord, true);

            string HKLM_ValueKey3 = "PromptOnSecureDesktop";
            WriteRegKey(HKLM_SubKey, HKLM_ValueKey3, 0, RegistryValueKind.DWord, true);

            string HKCU_SubKey = "Microsoft\\Windows\\CurrentVersion\\Action Center\\Checks\\{C8E6F269-B90A-4053-A3BE-499AFCEC98C4}.check.0";
            string HKCU_ValueKey = "CheckSetting";
            WriteRegKey(HKCU_SubKey, HKCU_ValueKey, StringToByteArray("23004100430042006C006F00620000000000000000000000010000000000000000000000"), RegistryValueKind.Binary);
        }

        public static bool CheckIs32Bits()
        {
            string pa = Environment.GetEnvironmentVariable("PROCESSOR_ARCHITECTURE");
            return (String.IsNullOrEmpty(pa) || String.Compare(pa, 0, "x86", 0, 3, true) == 0);
        }
    }
}
