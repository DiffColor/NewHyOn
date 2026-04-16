using AndoW.Shared;

namespace NewHyOn.Player.Settings;

public sealed class WeeklyPlayScheduleInfo : WeeklyDayScheduleInfo
{
    public string DayCode { get; set; } = string.Empty;

    public string DayLabel { get; set; } = string.Empty;

    public string WPS_DayOfWeek
    {
        get => DayOfWeek;
        set => DayOfWeek = value;
    }

    public int WPS_Hour1
    {
        get => StartHour;
        set => StartHour = value;
    }

    public int WPS_Min1
    {
        get => StartMinute;
        set => StartMinute = value;
    }

    public int WPS_Hour2
    {
        get => EndHour;
        set => EndHour = value;
    }

    public int WPS_Min2
    {
        get => EndMinute;
        set => EndMinute = value;
    }

    public bool WPS_IsOnAir
    {
        get => IsOnAir;
        set => IsOnAir = value;
    }
}
