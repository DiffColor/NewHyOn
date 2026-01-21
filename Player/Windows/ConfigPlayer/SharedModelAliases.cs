using AndoW.Shared;

namespace ConfigPlayer
{
    // 기존 코드 호환을 위한 래퍼 클래스들
    public class PageInfoClass : AndoW.Shared.PageInfoClass { }
    public class ElementInfoClass : AndoW.Shared.ElementInfoClass { }
    public class ContentsInfoClass : AndoW.Shared.ContentsInfoClass { }
    public class PageListInfoClass : AndoW.Shared.PageListInfoClass { }
    public class TextInfoClass : AndoW.Shared.TextInfoClass { }
    public class WeeklyPlayScheduleInfo : AndoW.Shared.WeeklyDayScheduleInfo
    {
        public void CopyData(WeeklyPlayScheduleInfo tmpData)
        {
            if (tmpData == null) return;

            DayOfWeek = tmpData.DayOfWeek;
            StartHour = tmpData.StartHour;
            StartMinute = tmpData.StartMinute;
            EndHour = tmpData.EndHour;
            EndMinute = tmpData.EndMinute;
            IsOnAir = tmpData.IsOnAir;
            PlayerName = tmpData.PlayerName;
        }

        public bool IsCheckSameData(WeeklyPlayScheduleInfo tmpData)
        {
            if (tmpData == null) return false;

            return DayOfWeek == tmpData.DayOfWeek &&
                   StartHour == tmpData.StartHour &&
                   StartMinute == tmpData.StartMinute &&
                   EndHour == tmpData.EndHour &&
                   EndMinute == tmpData.EndMinute &&
                   IsOnAir == tmpData.IsOnAir;
        }

        public string WPS_DayOfWeek { get => DayOfWeek; set => DayOfWeek = value; }
        public int WPS_Hour1 { get => StartHour; set => StartHour = value; }
        public int WPS_Min1 { get => StartMinute; set => StartMinute = value; }
        public int WPS_Hour2 { get => EndHour; set => EndHour = value; }
        public int WPS_Min2 { get => EndMinute; set => EndMinute = value; }
        public bool WPS_IsOnAir { get => IsOnAir; set => IsOnAir = value; }
    }
}
