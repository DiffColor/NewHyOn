using LiteDB;
using Newtonsoft.Json;

namespace AndoW.Shared
{
    public class WeeklyPlayScheduleInfo
    {
        [BsonId]
        [BsonField("id")]
        [JsonProperty("id")]
        public string Id { get; set; } = string.Empty;
        public string PlayerID { get; set; } = string.Empty;
        public string PlayerName { get; set; } = string.Empty;
        public DaySchedule MonSch { get; set; } = DaySchedule.CreateDefault();
        public DaySchedule TueSch { get; set; } = DaySchedule.CreateDefault();
        public DaySchedule WedSch { get; set; } = DaySchedule.CreateDefault();
        public DaySchedule ThuSch { get; set; } = DaySchedule.CreateDefault();
        public DaySchedule FriSch { get; set; } = DaySchedule.CreateDefault();
        public DaySchedule SatSch { get; set; } = DaySchedule.CreateDefault();
        public DaySchedule SunSch { get; set; } = DaySchedule.CreateDefault();
    }

    public class DaySchedule
    {
        public int StartHour { get; set; }
        public int StartMinute { get; set; }
        public int EndHour { get; set; }
        public int EndMinute { get; set; }
        public bool IsOnAir { get; set; } = true;

        public static DaySchedule CreateDefault()
        {
            return new DaySchedule();
        }
    }

    public class WeeklyDayScheduleInfo
    {
        public string PlayerName { get; set; } = string.Empty;
        public string DayOfWeek { get; set; } = string.Empty;
        public int StartHour { get; set; }
        public int StartMinute { get; set; }
        public int EndHour { get; set; } = 23;
        public int EndMinute { get; set; } = 59;
        public bool IsOnAir { get; set; } = true;
    }
}
