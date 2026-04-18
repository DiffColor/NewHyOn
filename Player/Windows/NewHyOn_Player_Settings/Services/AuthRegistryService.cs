using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Management;
using System.Text;

namespace NewHyOn.Player.Settings.Services;

public static class AuthRegistryService
{
    public static string EncodeAuthKey(string sourceKey)
    {
        string mixed = MixMultipleString(sourceKey);
        if (string.IsNullOrEmpty(mixed))
        {
            return string.Empty;
        }

        byte[] asciiBytes = Encoding.ASCII.GetBytes(mixed);
        byte[] transformed = MoveDiff(asciiBytes);
        return Encoding.ASCII.GetString(transformed);
    }

    public static void WriteDemoReg()
    {
        const string subKey = "ILYcode";
        const string valueKey = "NewHyOnInstalled";
        if (!string.IsNullOrEmpty(ReadRegKey(subKey, valueKey)))
        {
            return;
        }

        DateTime now = DateTime.Now;
        const string chars = "abceijklnopqvwxABCEIJLNOPQRSUVWXY";
        string redun1 = RandomSizeString(4, 8, chars);
        string redun2 = RandomSizeString(2, 8, chars);
        string redun3 = RandomSizeString(2, 8, chars);
        string redun4 = RandomSizeString(4, 8, chars);
        string value = string.Format(
            "{5}{2}{3}-{4}{6}{5}-{3}{1}{4}-{6}{0}{5}",
            now.Year,
            now.Month,
            now.Day,
            redun1,
            redun2,
            redun3,
            redun4);

        WriteRegKey(subKey, valueKey, value);
    }

    public static void WriteTryAuthReg()
    {
        const string subKey = "ILYcode";
        const string valueKey = "TryNewHyOn";
        string? keyValue = ReadRegKey(subKey, valueKey);
        if (string.IsNullOrEmpty(keyValue))
        {
            WriteRegKey(subKey, valueKey, "1");
            return;
        }

        int tryCount = int.Parse(keyValue) + 1;
        WriteRegKey(subKey, valueKey, tryCount.ToString());
    }

    public static bool ProhibitTrying()
    {
        string? keyValue = ReadRegKey("ILYcode", "TryNewHyOn");
        return !string.IsNullOrEmpty(keyValue) && int.TryParse(keyValue, out int tryCount) && tryCount > 2;
    }

    public static bool HasTryAuthHistory()
    {
        string? keyValue = ReadRegKey("ILYcode", "TryNewHyOn");
        return !string.IsNullOrWhiteSpace(keyValue);
    }

    public static string GetUuid12FromWmi()
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

    private static string MixMultipleString(string sourceKey)
    {
        if (sourceKey.Length < 12)
        {
            return string.Empty;
        }

        string left = string.Empty;
        string right = string.Empty;
        int index = 0;
        while (left.Length < sourceKey.Length / 2)
        {
            left += sourceKey.Substring(index, 2);
            index += 2;
            right += sourceKey.Substring(index, 2);
            index += 2;
        }

        return left + right;
    }

    private static byte[] MoveDiff(byte[] bytes)
    {
        List<byte> output = new();
        foreach (byte item in bytes)
        {
            int dec = Convert.ToInt32(item);
            if (dec >= 48 && dec <= 57)
            {
                output.Add(Convert.ToByte(48 + (57 - dec)));
            }
            else if (dec >= 65 && dec <= 90)
            {
                output.Add(Convert.ToByte(65 + (90 - dec)));
            }
            else
            {
                output.Add(Convert.ToByte(97 + (122 - dec)));
            }
        }

        return output.ToArray();
    }

    private static string RandomSizeString(int min, int max, string characters)
    {
        Random rng = new(Guid.NewGuid().GetHashCode());
        int size = rng.Next(min, max);
        char[] buffer = new char[size];
        for (int i = 0; i < size; i++)
        {
            buffer[i] = characters[rng.Next(characters.Length)];
        }

        return new string(buffer);
    }

    private static string? ReadRegKey(string subkeys, string valueKey, bool isHklm = false)
    {
        try
        {
            RegistryKey baseKey = isHklm ? Registry.LocalMachine : Registry.CurrentUser;
            using RegistryKey? software = baseKey.OpenSubKey("Software");
            if (software == null)
            {
                return null;
            }

            RegistryKey? current = software;
            foreach (string key in subkeys.Split('\\', StringSplitOptions.RemoveEmptyEntries))
            {
                current = current?.OpenSubKey(key);
                if (current == null)
                {
                    return null;
                }
            }

            return current.GetValue(valueKey)?.ToString();
        }
        catch
        {
            return null;
        }
    }

    private static void WriteRegKey(string subkeys, string valueKey, object value, RegistryValueKind valueKind = RegistryValueKind.String, bool isHklm = false)
    {
        try
        {
            RegistryKey baseKey = isHklm ? Registry.LocalMachine : Registry.CurrentUser;
            using RegistryKey software = baseKey.CreateSubKey("Software");
            RegistryKey current = software;
            foreach (string key in subkeys.Split('\\', StringSplitOptions.RemoveEmptyEntries))
            {
                current = current.CreateSubKey(key);
            }

            current.SetValue(valueKey, value, valueKind);
        }
        catch
        {
        }
    }
}
