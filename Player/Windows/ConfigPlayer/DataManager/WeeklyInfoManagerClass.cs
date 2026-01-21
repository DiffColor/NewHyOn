using System.Collections.Generic;
using AndoW.LiteDb;
using AndoW.Shared;

namespace ConfigPlayer
{
    public class WeeklyInfoManagerClass
    {
        private readonly WeeklyRepository repository = new WeeklyRepository();

        public List<WeeklyPlayScheduleInfo> PIF_WPS_InfoList = new List<WeeklyPlayScheduleInfo>();
        public AndoW.Shared.WeeklyPlayScheduleInfo CurrentSchedule { get; private set; }

        public WeeklyInfoManagerClass()
        {
            LoadWeeklySchedule();
        }

        public void SaveWeeklySchedule(string playerId = "", string playerName = "")
        {
            if (CurrentSchedule == null)
            {
                return;
            }

            string key = string.IsNullOrWhiteSpace(playerId) ? playerName : playerId;
            if (string.IsNullOrWhiteSpace(CurrentSchedule.Id))
            {
                CurrentSchedule.Id = key;
            }
            ApplyWeekListToSchedule(playerId, playerName);
            repository.Upsert(CurrentSchedule);
        }

        public void LoadWeeklySchedule(string playerId = "", string playerName = "")
        {
            string key = string.IsNullOrWhiteSpace(playerId) ? playerName : playerId;
            if (!string.IsNullOrWhiteSpace(key))
            {
                CurrentSchedule = repository.FindById(key);
            }
            if (CurrentSchedule == null)
            {
                CurrentSchedule = repository.FindOne(x => true);
            }
            if (CurrentSchedule == null)
            {
                InitWeeklySchData(playerId, playerName);
            }
            else
            {
                BuildWeekList(playerName);
            }
        }

        public void InitWeeklySchData(string playerId = "", string playerName = "")
        {
            string key = string.IsNullOrWhiteSpace(playerId) ? playerName : playerId;
            CurrentSchedule = new AndoW.Shared.WeeklyPlayScheduleInfo
            {
                Id = key,
                PlayerID = string.IsNullOrWhiteSpace(playerId) ? playerName : playerId,
                PlayerName = playerName
            };
            BuildWeekList(playerName);
        }

        private void ApplyWeekListToSchedule(string playerId, string playerName)
        {
            if (CurrentSchedule == null)
            {
                InitWeeklySchData(playerId, playerName);
            }

            if (string.IsNullOrWhiteSpace(CurrentSchedule.Id))
            {
                CurrentSchedule.Id = string.IsNullOrWhiteSpace(playerId) ? playerName : playerId;
            }

            if (string.IsNullOrWhiteSpace(CurrentSchedule.PlayerID))
            {
                CurrentSchedule.PlayerID = string.IsNullOrWhiteSpace(playerId) ? playerName : playerId;
            }

            if (string.IsNullOrWhiteSpace(CurrentSchedule.PlayerName))
            {
                CurrentSchedule.PlayerName = playerName;
            }

            foreach (var info in PIF_WPS_InfoList)
            {
                if (info == null)
                {
                    continue;
                }

                DaySchedule target = null;
                if (info.DayOfWeek == "SUN")
                    target = CurrentSchedule.SunSch;
                else if (info.DayOfWeek == "MON")
                    target = CurrentSchedule.MonSch;
                else if (info.DayOfWeek == "TUE")
                    target = CurrentSchedule.TueSch;
                else if (info.DayOfWeek == "WED")
                    target = CurrentSchedule.WedSch;
                else if (info.DayOfWeek == "THU")
                    target = CurrentSchedule.ThuSch;
                else if (info.DayOfWeek == "FRI")
                    target = CurrentSchedule.FriSch;
                else if (info.DayOfWeek == "SAT")
                    target = CurrentSchedule.SatSch;

                if (target == null)
                {
                    continue;
                }

                target.StartHour = info.StartHour;
                target.StartMinute = info.StartMinute;
                target.EndHour = info.EndHour;
                target.EndMinute = info.EndMinute;
                target.IsOnAir = info.IsOnAir;
            }
        }

        private void BuildWeekList(string playerName)
        {
            PIF_WPS_InfoList.Clear();
            foreach (var tuple in GetSchedules())
            {
                WeeklyPlayScheduleInfo info = new WeeklyPlayScheduleInfo
                {
                    PlayerName = playerName,
                    DayOfWeek = tuple.day,
                    StartHour = tuple.schedule.StartHour,
                    StartMinute = tuple.schedule.StartMinute,
                    EndHour = tuple.schedule.EndHour,
                    EndMinute = tuple.schedule.EndMinute,
                    IsOnAir = tuple.schedule.IsOnAir
                };
                PIF_WPS_InfoList.Add(info);
            }
        }

        private IEnumerable<(string day, DaySchedule schedule)> GetSchedules()
        {
            yield return ("SUN", CurrentSchedule.SunSch);
            yield return ("MON", CurrentSchedule.MonSch);
            yield return ("TUE", CurrentSchedule.TueSch);
            yield return ("WED", CurrentSchedule.WedSch);
            yield return ("THU", CurrentSchedule.ThuSch);
            yield return ("FRI", CurrentSchedule.FriSch);
            yield return ("SAT", CurrentSchedule.SatSch);
        }

        private class WeeklyRepository : LiteDbRepository<AndoW.Shared.WeeklyPlayScheduleInfo>
        {
            public WeeklyRepository() : base("WeeklyInfoManagerClass", "Id") { }
        }
    }
}
