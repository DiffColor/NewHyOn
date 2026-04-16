using AndoW.LiteDb;
using AndoW.Shared;
using System.Collections.Generic;

namespace NewHyOn.Player.Settings.DataManager;

public sealed class WeeklyInfoManagerClass
{
    private readonly WeeklyRepository repository = new();

    public List<WeeklyPlayScheduleInfo> ScheduleList { get; } = new();
    public AndoW.Shared.WeeklyPlayScheduleInfo CurrentSchedule { get; private set; } = new();

    public WeeklyInfoManagerClass()
    {
        LoadWeeklySchedule();
    }

    public void SaveWeeklySchedule(string playerId = "", string playerName = "")
    {
        string key = string.IsNullOrWhiteSpace(playerId) ? playerName : playerId;
        if (string.IsNullOrWhiteSpace(CurrentSchedule.Id))
        {
            CurrentSchedule.Id = key;
        }

        CurrentSchedule.PlayerID = string.IsNullOrWhiteSpace(playerId) ? playerName : playerId;
        CurrentSchedule.PlayerName = playerName;

        foreach (WeeklyPlayScheduleInfo row in ScheduleList)
        {
            DaySchedule target = row.DayCode switch
            {
                "SUN" => CurrentSchedule.SunSch,
                "MON" => CurrentSchedule.MonSch,
                "TUE" => CurrentSchedule.TueSch,
                "WED" => CurrentSchedule.WedSch,
                "THU" => CurrentSchedule.ThuSch,
                "FRI" => CurrentSchedule.FriSch,
                "SAT" => CurrentSchedule.SatSch,
                _ => CurrentSchedule.MonSch
            };

            target.StartHour = row.WPS_Hour1;
            target.StartMinute = row.WPS_Min1;
            target.EndHour = row.WPS_Hour2;
            target.EndMinute = row.WPS_Min2;
            target.IsOnAir = row.WPS_IsOnAir;
        }

        repository.Upsert(CurrentSchedule);
    }

    public void LoadWeeklySchedule(string playerId = "", string playerName = "")
    {
        string key = string.IsNullOrWhiteSpace(playerId) ? playerName : playerId;
        if (!string.IsNullOrWhiteSpace(key))
        {
            CurrentSchedule = repository.FindById(key) ?? CurrentSchedule;
        }

        CurrentSchedule ??= repository.FindOne(_ => true) ?? CreateDefaultSchedule(key, playerName);
        BuildWeekList(playerName);
    }

    private AndoW.Shared.WeeklyPlayScheduleInfo CreateDefaultSchedule(string playerId, string playerName)
    {
        return new AndoW.Shared.WeeklyPlayScheduleInfo
        {
            Id = playerId,
            PlayerID = playerId,
            PlayerName = playerName
        };
    }

    private void BuildWeekList(string playerName)
    {
        ScheduleList.Clear();
        AddDay("SUN", "일요일", CurrentSchedule.SunSch, playerName);
        AddDay("MON", "월요일", CurrentSchedule.MonSch, playerName);
        AddDay("TUE", "화요일", CurrentSchedule.TueSch, playerName);
        AddDay("WED", "수요일", CurrentSchedule.WedSch, playerName);
        AddDay("THU", "목요일", CurrentSchedule.ThuSch, playerName);
        AddDay("FRI", "금요일", CurrentSchedule.FriSch, playerName);
        AddDay("SAT", "토요일", CurrentSchedule.SatSch, playerName);
    }

    private void AddDay(string dayCode, string dayLabel, DaySchedule schedule, string playerName)
    {
        ScheduleList.Add(new WeeklyPlayScheduleInfo
        {
            PlayerName = playerName,
            DayCode = dayCode,
            DayLabel = dayLabel,
            WPS_DayOfWeek = dayCode,
            WPS_Hour1 = schedule.StartHour,
            WPS_Min1 = schedule.StartMinute,
            WPS_Hour2 = schedule.EndHour,
            WPS_Min2 = schedule.EndMinute,
            WPS_IsOnAir = schedule.IsOnAir
        });
    }

    private sealed class WeeklyRepository : LiteDbRepository<AndoW.Shared.WeeklyPlayScheduleInfo>
    {
        public WeeklyRepository()
            : base("WeeklyInfoManagerClass", "Id")
        {
        }
    }
}
