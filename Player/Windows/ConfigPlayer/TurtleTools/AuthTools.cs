using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Management;
using System.Text;

namespace TurtleTools
{
    class AuthTools
    {
        #region Encode
        public static string EncodeAuthKey(string sourcekey)
        {
            string mixedStr = MixMultipleString(sourcekey);

            if (string.IsNullOrEmpty(mixedStr)) return string.Empty;

            byte[] mixedBytes = ConvertStringToASCIIBytes(mixedStr);
            byte[] shiftedBytes = MoveDiff(mixedBytes);
            string encodedStr = ConvertASCIIBytesToString(shiftedBytes);

            return encodedStr;
        }

        // Mix Source String : AA BB CC DD EE FF => AA CC EE BB DD FF
        public static string MixMultipleString(string sourcekey)
        {
            if (sourcekey.Length < 12) return string.Empty;

            string retStr = string.Empty;
            string xStr = string.Empty;
            int idx = 0;

            while (retStr.Length < sourcekey.Length / 2)
            {
                retStr += sourcekey.Substring(idx, 2);
                idx += 2;
                xStr += sourcekey.Substring(idx, 2);
                idx += 2;
            }

            return retStr + xStr;
        }
        #endregion

        public static byte[] MoveDiff(byte[] bts)
        {
            List<byte> bytelist = new List<byte>();

            for (int i = 0; i < bts.Length; i++)
            {
                int dec = Convert.ToInt32(bts[i]);

                if (dec >= 48 && dec <= 57)
                {
                    bytelist.Add(Convert.ToByte(48 + (57 - dec)));
                }
                else if (dec >= 65 && dec <= 90)
                {
                    bytelist.Add(Convert.ToByte(65 + (90 - dec)));
                }
                else
                {
                    bytelist.Add(Convert.ToByte(97 + (122 - dec)));
                }
            }

            return bytelist.ToArray();
        }

        #region Decode
        public static string DecodeAuthKey(string authkey)
        {
            byte[] asciiBytes = ConvertStringToASCIIBytes(authkey);
            byte[] restoreBytes = MoveDiff(asciiBytes);
            string restoreStr = ConvertASCIIBytesToString(restoreBytes);
            string decodedStr = RestoreMixedStr(restoreStr);

            if (string.IsNullOrEmpty(authkey)) return string.Format("Wrong Auth Key : {0}", authkey);

            return decodedStr;
        }

        public static string RestoreMixedStr(string mixedkey)
        {
            if (mixedkey.Length < 12) return string.Empty;

            string retStr = string.Empty;
            int idx = 0;

            while (retStr.Length < mixedkey.Length)
            {
                retStr += mixedkey.Substring(idx, 2);
                retStr += mixedkey.Substring(idx + 6, 2);
                idx += 2;
            }

            return retStr;
        }
        #endregion

        public static byte[] ConvertStringToASCIIBytes(string mixedSourcekey)
        {
            return Encoding.ASCII.GetBytes(mixedSourcekey);
        }

        public static string ConvertASCIIBytesToString(byte[] keyBytes)
        {
            return System.Text.Encoding.ASCII.GetString(keyBytes);
        }


        public static byte ShiftNBitLeft(byte value, byte n)
        {
            return (byte)(value << n | value >> (8 - n));
        }

        public static byte ShiftNBitRight(byte value, byte n)
        {
            return (byte)(value >> n | value << (8 - n));
        }

        #region Registry Key & Value
        public static string ReadRegKey(string subkeys, string valueKey, bool isHKLM = false)
        {
            try { 
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
                foreach(string key in subkeys.Split('\\')) {
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
        #endregion

        public static int MulMinusMul(int num)
        {
            return ((num * 2) - 1) * 2;
        }
		
        public static string RandomSizeString(int min, int max, string characters)
        {
            Random _rng = new Random(Guid.NewGuid().GetHashCode());
            int size = _rng.Next(min, max);
            char[] buffer = new char[size];

            for (int i = 0; i < size; i++)
            {
                buffer[i] = characters[_rng.Next(characters.Length)];
            }
            return new string(buffer);
        }

        public static void WriteDemoReg()
        {
            string subKey = "ILYcode";
            string valueKey = "HyOnInstalled";

            DateTime dt = DateTime.Now;

            string keyValue = ReadRegKey(subKey, valueKey);

            if (string.IsNullOrEmpty(keyValue))
            {
                string garbage_chars = "abceijklnopqvwxABCEIJLNOPQRSUVWXY";     //datetime ignore chars

                string redun1 = RandomSizeString(4, 8, garbage_chars);
                string redun2 = RandomSizeString(2, 8, garbage_chars);
                string redun3 = RandomSizeString(2, 8, garbage_chars);
                string redun4 = RandomSizeString(4, 8, garbage_chars);

                string dtStr = String.Format("{5}{2}{3}-{4}{6}{5}-{3}{1}{4}-{6}{0}{5}", dt.Year, dt.Month, dt.Day, redun1, redun2, redun3, redun4);

                WriteRegKey(subKey, valueKey, dtStr);
            }
        }

        public static void WriteTryAuthReg()
        {
            string subKey = "ILYcode";
            string valueKey = "TryHyOn";

            string keyValue = ReadRegKey(subKey, valueKey);

            if (string.IsNullOrEmpty(keyValue))
            {
                WriteRegKey(subKey, valueKey, "1");
            }
            else
            {
                int tryInt = int.Parse(keyValue) + 1;
                WriteRegKey(subKey, valueKey, tryInt.ToString());
            }
        }

        public static bool ProhibitTring()
        {
            string subKey = "ILYcode";
            string valueKey = "TryHyOn";

            string keyValue = ReadRegKey(subKey, valueKey);

            if (!string.IsNullOrEmpty(keyValue)) 
            {
                int tryInt = int.Parse(keyValue);
                
                if (tryInt > 2)
                    return true;
            }

            return false;
        }

        public static string getUUID12()
        {
            string uuid = string.Empty;

            ManagementClass mc = new ManagementClass("Win32_ComputerSystemProduct");
            ManagementObjectCollection moc = mc.GetInstances();

            foreach (ManagementObject mo in moc)
            {
                string[] retarray = mo.Properties["UUID"].Value.ToString().Split('-');
                uuid = retarray[retarray.Length - 1];
                break;
            }

            return uuid;
        }
    }
}
