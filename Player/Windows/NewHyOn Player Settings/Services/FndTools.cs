using System;
using System.IO;

namespace NewHyOn.Player.Settings.Services;

public static class FndTools
{
    public static string GetPlayerProcName() => "NewHyOn Player";

    public static string GetAgentProcName() => "NewHyOn Agent";

    public static string GetPcsProcName() => "PCScheduler";

    public static string GetEmergScrollProcName() => "EmergencyScrollText";

    public static string GetPptViewerProcName() => "PPTVIEW";

    public static string GetPlayerExeFilePath()
    {
        return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"{GetPlayerProcName()}.exe");
    }
}
