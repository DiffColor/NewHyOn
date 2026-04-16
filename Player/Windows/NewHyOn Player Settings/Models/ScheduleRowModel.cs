namespace NewHyOn.Player.Settings.Models;

public sealed class ScheduleRowModel
{
    public string DayCode { get; set; } = string.Empty;
    public string DayLabel { get; set; } = string.Empty;
    public bool IsOnAir { get; set; } = true;
    public int StartHour { get; set; }
    public int StartMinute { get; set; }
    public int EndHour { get; set; } = 23;
    public int EndMinute { get; set; } = 59;
}
