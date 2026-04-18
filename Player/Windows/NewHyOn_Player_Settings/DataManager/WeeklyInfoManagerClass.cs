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
        CurrentSchedule = !string.IsNullOrWhiteSpace(key)
            ? repository.FindById(key) ?? repository.FindOne(_ => true) ?? CreateDefaultSchedule(key, playerName)
            : repository.FindOne(_ => true) ?? CreateDefaultSchedule(key, playerName);

        EnsureCurrentScheduleDefaults(playerId, playerName);
        BuildWeekList(playerName);
    }

    public void ApplyRemoteWeeklySchedule(AndoW.Shared.WeeklyPlayScheduleInfo remoteSchedule, string playerId = "", string playerName = "")
    {
        string resolvedPlayerId = string.IsNullOrWhiteSpace(remoteSchedule?.PlayerID)
            ? playerId
            : remoteSchedule.PlayerID.Trim();
        string resolvedPlayerName = string.IsNullOrWhiteSpace(remoteSchedule?.PlayerName)
            ? playerName
            : remoteSchedule.PlayerName.Trim();
        string resolvedId = string.IsNullOrWhiteSpace(remoteSchedule?.Id)
            ? (string.IsNullOrWhiteSpace(resolvedPlayerId) ? resolvedPlayerName : resolvedPlayerId)
            : remoteSchedule.Id.Trim();

        CurrentSchedule = new AndoW.Shared.WeeklyPlayScheduleInfo
        {
            Id = resolvedId,
            PlayerID = string.IsNullOrWhiteSpace(resolvedPlayerId) ? resolvedPlayerName : resolvedPlayerId,
            PlayerName = resolvedPlayerName,
            MonSch = remoteSchedule?.MonSch ?? DaySchedule.CreateDefault(),
            TueSch = remoteSchedule?.TueSch ?? DaySchedule.CreateDefault(),
            WedSch = remoteSchedule?.WedSch ?? DaySchedule.CreateDefault(),
            ThuSch = remoteSchedule?.ThuSch ?? DaySchedule.CreateDefault(),
            FriSch = remoteSchedule?.FriSch ?? DaySchedule.CreateDefault(),
            SatSch = remoteSchedule?.SatSch ?? DaySchedule.CreateDefault(),
            SunSch = remoteSchedule?.SunSch ?? DaySchedule.CreateDefault()
        };

        EnsureCurrentScheduleDefaults(playerId, playerName);
        repository.Upsert(CurrentSchedule);
        BuildWeekList(CurrentSchedule.PlayerName);
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
        EnsureCurrentScheduleDefaults(CurrentSchedule.PlayerID, playerName);
        ScheduleList.Clear();
        AddDay("SUN", "일요일", CurrentSchedule.SunSch ?? DaySchedule.CreateDefault(), playerName);
        AddDay("MON", "월요일", CurrentSchedule.MonSch ?? DaySchedule.CreateDefault(), playerName);
        AddDay("TUE", "화요일", CurrentSchedule.TueSch ?? DaySchedule.CreateDefault(), playerName);
        AddDay("WED", "수요일", CurrentSchedule.WedSch ?? DaySchedule.CreateDefault(), playerName);
        AddDay("THU", "목요일", CurrentSchedule.ThuSch ?? DaySchedule.CreateDefault(), playerName);
        AddDay("FRI", "금요일", CurrentSchedule.FriSch ?? DaySchedule.CreateDefault(), playerName);
        AddDay("SAT", "토요일", CurrentSchedule.SatSch ?? DaySchedule.CreateDefault(), playerName);
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

    private void EnsureCurrentScheduleDefaults(string playerId, string playerName)
    {
        CurrentSchedule ??= CreateDefaultSchedule(playerId, playerName);

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

        CurrentSchedule.MonSch ??= DaySchedule.CreateDefault();
        CurrentSchedule.TueSch ??= DaySchedule.CreateDefault();
        CurrentSchedule.WedSch ??= DaySchedule.CreateDefault();
        CurrentSchedule.ThuSch ??= DaySchedule.CreateDefault();
        CurrentSchedule.FriSch ??= DaySchedule.CreateDefault();
        CurrentSchedule.SatSch ??= DaySchedule.CreateDefault();
        CurrentSchedule.SunSch ??= DaySchedule.CreateDefault();
    }

    private sealed class WeeklyRepository : LiteDbRepository<AndoW.Shared.WeeklyPlayScheduleInfo>
    {
        public WeeklyRepository()
            : base("WeeklyInfoManagerClass", "Id")
        {
        }
    }
}
