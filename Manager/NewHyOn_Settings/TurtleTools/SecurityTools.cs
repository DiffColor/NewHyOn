using Microsoft.Win32;
#if WINDOWS
using NetFwTypeLib;
#endif
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
