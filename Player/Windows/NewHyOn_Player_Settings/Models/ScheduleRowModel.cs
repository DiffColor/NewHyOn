using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace NewHyOn.Player.Settings.Models;

public sealed class ScheduleRowModel : INotifyPropertyChanged
{
    private string dayCode = string.Empty;
    private string dayLabel = string.Empty;
    private bool isOnAir = true;
    private int startHour;
    private int startMinute;
    private int endHour;
    private int endMinute;

    public event PropertyChangedEventHandler? PropertyChanged;

    public string DayCode
    {
        get => dayCode;
        set => SetProperty(ref dayCode, value);
    }

    public string DayLabel
    {
        get => dayLabel;
        set => SetProperty(ref dayLabel, value);
    }

    public bool IsOnAir
    {
        get => isOnAir;
        set => SetProperty(ref isOnAir, value);
    }

    public int StartHour
    {
        get => startHour;
        set => SetProperty(ref startHour, value);
    }

    public int StartMinute
    {
        get => startMinute;
        set => SetProperty(ref startMinute, value);
    }

    public int EndHour
    {
        get => endHour;
        set => SetProperty(ref endHour, value);
    }

    public int EndMinute
    {
        get => endMinute;
        set => SetProperty(ref endMinute, value);
    }

    private void SetProperty<T>(ref T field, T value, [CallerMemberName] string propertyName = "")
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
