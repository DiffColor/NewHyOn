using AndoW.Shared;

namespace HyOnPlayer
{
    // 기존 코드 호환을 위한 래퍼 클래스들
    public class PageInfoClass : AndoW.Shared.PageInfoClass { }
    public class ElementInfoClass : AndoW.Shared.ElementInfoClass { }
    public class ContentsInfoClass : AndoW.Shared.ContentsInfoClass { }
    public class PageListInfoClass : AndoW.Shared.PageListInfoClass { }
    public class TextInfoClass : AndoW.Shared.TextInfoClass { }
    public class WeeklyPlayScheduleInfo : AndoW.Shared.WeeklyPlayScheduleInfo
    {
        public void CopyData(WeeklyPlayScheduleInfo tmpData)
        {
            if (tmpData == null) return;

            Id = tmpData.Id;
            PlayerID = tmpData.PlayerID;
            PlayerName = tmpData.PlayerName;
            MonSch = tmpData.MonSch ?? AndoW.Shared.DaySchedule.CreateDefault();
            TueSch = tmpData.TueSch ?? AndoW.Shared.DaySchedule.CreateDefault();
            WedSch = tmpData.WedSch ?? AndoW.Shared.DaySchedule.CreateDefault();
            ThuSch = tmpData.ThuSch ?? AndoW.Shared.DaySchedule.CreateDefault();
            FriSch = tmpData.FriSch ?? AndoW.Shared.DaySchedule.CreateDefault();
            SatSch = tmpData.SatSch ?? AndoW.Shared.DaySchedule.CreateDefault();
            SunSch = tmpData.SunSch ?? AndoW.Shared.DaySchedule.CreateDefault();
        }

        public bool IsCheckSameData(WeeklyPlayScheduleInfo tmpData)
        {
            if (tmpData == null) return false;

            return PlayerID == tmpData.PlayerID &&
                   PlayerName == tmpData.PlayerName &&
                   IsSameDay(MonSch, tmpData.MonSch) &&
                   IsSameDay(TueSch, tmpData.TueSch) &&
                   IsSameDay(WedSch, tmpData.WedSch) &&
                   IsSameDay(ThuSch, tmpData.ThuSch) &&
                   IsSameDay(FriSch, tmpData.FriSch) &&
                   IsSameDay(SatSch, tmpData.SatSch) &&
                   IsSameDay(SunSch, tmpData.SunSch);
        }

        private static bool IsSameDay(AndoW.Shared.DaySchedule a, AndoW.Shared.DaySchedule b)
        {
            if (a == null && b == null) return true;
            if (a == null || b == null) return false;
            return a.StartHour == b.StartHour
                   && a.StartMinute == b.StartMinute
                   && a.EndHour == b.EndHour
                   && a.EndMinute == b.EndMinute
                   && a.IsOnAir == b.IsOnAir;
        }
    }
}
