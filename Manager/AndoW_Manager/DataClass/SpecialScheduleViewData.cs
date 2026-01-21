using System;
using System.Collections.Generic;
using System.Globalization;

namespace AndoW_Manager
{
    public class SpecialScheduleViewData
    {
        public List<string> GroupNames { get; set; } = new List<string>();
        public string GroupName { get; set; } = string.Empty;
        public bool IsGroupTarget { get; set; } = true;
        public string TargetDisplayName { get; set; } = string.Empty;
        public List<string> TargetPlayers { get; set; } = new List<string>();
        public string Playlist { get; set; } = string.Empty;
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public TimeSpan? StartTime { get; set; }
        public TimeSpan? EndTime { get; set; }
        public bool[] Days { get; set; } = new bool[7];
        public string ScheduleKey { get; set; } = string.Empty;
        public List<SpecialScheduleInfoClass> Schedules { get; } = new List<SpecialScheduleInfoClass>();

        public string StartDateText => FormatDate(StartDate);
        public string EndDateText => FormatDate(EndDate);
        public string StartTimeText => FormatTime(StartTime);
        public string EndTimeText => FormatTime(EndTime);

        private static string FormatDate(DateTime? date)
        {
            if (date == null)
            {
                return string.Empty;
            }

            return date.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        private static string FormatTime(TimeSpan? time)
        {
            if (time == null)
            {
                return string.Empty;
            }

            TimeSpan value = time.Value;
            return string.Format(CultureInfo.InvariantCulture, "{0:D2}:{1:D2}", value.Hours, value.Minutes);
        }
    }
}
