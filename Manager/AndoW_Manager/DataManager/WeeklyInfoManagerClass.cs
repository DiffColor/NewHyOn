using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using TurtleTools;


namespace AndoW_Manager
{
    public class WeeklyInfoManagerClass : RethinkDbManagerBase<WeeklyPlayScheduleInfo>
    {
        public List<WeeklyDayScheduleInfo> PIF_WPS_InfoList { get; } = new List<WeeklyDayScheduleInfo>();
        public WeeklyPlayScheduleInfo CurrentSchedule { get; private set; }

        public WeeklyInfoManagerClass()
            : base(RethinkDbConfigurator.GetDataDatabaseName(), nameof(WeeklyInfoManagerClass), "id")
        {
        }

        public void SaveWeeklySchedule(string playerId, string playerName)
        {
            if (string.IsNullOrWhiteSpace(playerId))
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(playerName))
            {
                playerName = playerId;
            }

            if (PIF_WPS_InfoList.Count == 0)
            {
                InitWeeklySchData(playerId, playerName);
            }

            if (CurrentSchedule == null)
            {
                CurrentSchedule = CreateDefaultSchedule(playerId, playerName);
            }

            CurrentSchedule.Id = playerId;
            CurrentSchedule.PlayerID = playerId;
            CurrentSchedule.PlayerName = playerName;

            foreach (WeeklyDayScheduleInfo info in PIF_WPS_InfoList)
            {
                SetDaySchedule(info.DayOfWeek, info);
            }

            Upsert(CurrentSchedule);
        }

        public List<WeeklyDayScheduleInfo> GetLegacySchedules(string playerId, string playerName)
        {
            InitPlayerInfoListFromDataTable(playerId, playerName);
            var result = new List<WeeklyDayScheduleInfo>();
            foreach (var schedule in PIF_WPS_InfoList)
            {
                result.Add(schedule.Clone());
            }

            return result;
        }

        public void SaveLegacySchedules(string playerId, string playerName, IEnumerable<WeeklyDayScheduleInfo> schedules)
        {
            if (schedules == null)
            {
                return;
            }

            PIF_WPS_InfoList.Clear();
            foreach (var schedule in schedules)
            {
                var clone = schedule.Clone();
                clone.PlayerName = playerName;
                PIF_WPS_InfoList.Add(clone);
            }

            SaveWeeklySchedule(playerId, playerName);
        }

        public void InitPlayerInfoListFromDataTable(string playerId, string playerName)
        {
            if (string.IsNullOrWhiteSpace(playerId))
            {
                CurrentSchedule = CreateDefaultSchedule(playerId, playerName);
            }
            else
            {
                CurrentSchedule = FindOne(x => x.PlayerID == playerId) ?? CreateDefaultSchedule(playerId, playerName);
            }

            BuildWeekList(string.IsNullOrWhiteSpace(playerName) ? playerId : playerName);
        }

        public void InitWeeklySchData(string playerId, string playerName)
        {
            CurrentSchedule = CreateDefaultSchedule(playerId, playerName);
            BuildWeekList(string.IsNullOrWhiteSpace(playerName) ? playerId : playerName);
            SaveWeeklySchedule(playerId, playerName);
        }

        private void BuildWeekList(string playerName)
        {
            PIF_WPS_InfoList.Clear();
            foreach (var tuple in GetSchedules())
            {
                DaySchedule ds = tuple.schedule;
                WeeklyDayScheduleInfo info = new WeeklyDayScheduleInfo
                {
                    PlayerName = playerName,
                    DayOfWeek = tuple.day,
                    StartHour = ds.StartHour,
                    StartMinute = ds.StartMinute,
                    EndHour = ds.EndHour,
                    EndMinute = ds.EndMinute,
                    IsOnAir = ds.IsOnAir
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

        private WeeklyPlayScheduleInfo CreateDefaultSchedule(string playerId, string playerName)
        {
            return new WeeklyPlayScheduleInfo
            {
                Id = playerId,
                PlayerID = playerId,
                PlayerName = playerName,
                MonSch = DaySchedule.CreateDefault(),
                TueSch = DaySchedule.CreateDefault(),
                WedSch = DaySchedule.CreateDefault(),
                ThuSch = DaySchedule.CreateDefault(),
                FriSch = DaySchedule.CreateDefault(),
                SatSch = DaySchedule.CreateDefault(),
                SunSch = DaySchedule.CreateDefault(),
            };
        }

        private void SetDaySchedule(string day, WeeklyDayScheduleInfo info)
        {
            DaySchedule target = day switch
            {
                "SUN" => CurrentSchedule.SunSch,
                "MON" => CurrentSchedule.MonSch,
                "TUE" => CurrentSchedule.TueSch,
                "WED" => CurrentSchedule.WedSch,
                "THU" => CurrentSchedule.ThuSch,
                "FRI" => CurrentSchedule.FriSch,
                "SAT" => CurrentSchedule.SatSch,
                _ => null
            };

            if (target == null)
            {
                return;
            }

            target.StartHour = info.StartHour;
            target.StartMinute = info.StartMinute;
            target.EndHour = info.EndHour;
            target.EndMinute = info.EndMinute;
            target.IsOnAir = info.IsOnAir;
        }

        public void DeleteWeeklySchedule(string playerId)
        {
            if (string.IsNullOrWhiteSpace(playerId))
            {
                return;
            }

            DeleteMany(x => x.PlayerID == playerId);
        }
    }

    public class WeeklyPlayScheduleInfo
    {
        [JsonProperty("id")]
        public string Id { get; set; }
        public string PlayerID { get; set; }
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
        public int StartHour { get; set; } = 0;
        public int StartMinute { get; set; } = 0;
        public int EndHour { get; set; } = 0;
        public int EndMinute { get; set; } = 0;
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
        public int StartHour { get; set; } = 0;
        public int StartMinute { get; set; } = 0;
        public int EndHour { get; set; } = 23;
        public int EndMinute { get; set; } = 59;
        public bool IsOnAir { get; set; } = true;

        public void CopyData(WeeklyDayScheduleInfo tmpData, bool onlyTime = false)
        {
            StartHour = tmpData.StartHour;
            StartMinute = tmpData.StartMinute;
            EndHour = tmpData.EndHour;
            EndMinute = tmpData.EndMinute;

            if (onlyTime)
                return;

            DayOfWeek = tmpData.DayOfWeek;
            PlayerName = tmpData.PlayerName;
            IsOnAir = tmpData.IsOnAir;
        }

        public WeeklyDayScheduleInfo Clone()
        {
            return new WeeklyDayScheduleInfo
            {
                PlayerName = PlayerName,
                DayOfWeek = DayOfWeek,
                StartHour = StartHour,
                StartMinute = StartMinute,
                EndHour = EndHour,
                EndMinute = EndMinute,
                IsOnAir = IsOnAir
            };
        }

        #region Legacy compatibility
        public string WPS_PlayerName
        {
            get => PlayerName;
            set => PlayerName = value;
        }

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
        #endregion
    }
}
