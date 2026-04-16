using Microsoft.Win32;
using System;
using System.Linq;

namespace NewHyOn.Player.Settings.Services;

public static class SystemPolicyService
{
    public static void DisableUac()
    {
        WriteRegKey("Microsoft\\Windows\\CurrentVersion\\Policies\\System", "EnableLUA", 0, RegistryValueKind.DWord, true);
        WriteRegKey("Microsoft\\Windows\\CurrentVersion\\Policies\\System", "ConsentPromptBehaviorAdmin", 0, RegistryValueKind.DWord, true);
        WriteRegKey("Microsoft\\Windows\\CurrentVersion\\Policies\\System", "PromptOnSecureDesktop", 0, RegistryValueKind.DWord, true);
        WriteRegKey(
            "Microsoft\\Windows\\CurrentVersion\\Action Center\\Checks\\{C8E6F269-B90A-4053-A3BE-499AFCEC98C4}.check.0",
            "CheckSetting",
            StringToByteArray("23004100430042006C006F00620000000000000000000000010000000000000000000000"),
            RegistryValueKind.Binary);
    }

    private static byte[] StringToByteArray(string hex)
    {
        return Enumerable.Range(0, hex.Length)
            .Where(x => x % 2 == 0)
            .Select(x => Convert.ToByte(hex.Substring(x, 2), 16))
            .ToArray();
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
