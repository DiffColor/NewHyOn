using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using AndoW.Shared;
using HyOnPlayer.DataManager;
using SharedWeeklyPlayScheduleInfo = AndoW.Shared.WeeklyPlayScheduleInfo;

namespace HyOnPlayer.Services
{
    internal sealed class ScheduleEvaluator
    {
        private readonly PlayerInfoManager playerInfoManager;
        private SharedWeeklyPlayScheduleInfo weeklyCache;
        private DateTime weeklyCacheLoadedAt = DateTime.MinValue;

        public ScheduleEvaluator(PlayerInfoManager playerInfoManager)
        {
            this.playerInfoManager = playerInfoManager;
        }

        public ScheduleDecision Evaluate(DateTime now, string fallbackPlaylist)
        {
            var player = playerInfoManager?.g_PlayerInfo;
            if (player == null)
            {
                return ScheduleDecision.DefaultEmpty();
            }

            string cacheKey = string.IsNullOrWhiteSpace(player.PIF_GUID)
                ? player.PIF_PlayerName
                : player.PIF_GUID;
            if (string.IsNullOrWhiteSpace(cacheKey))
            {
                return ScheduleDecision.DefaultEmpty();
            }

            var weekly = GetWeeklySchedule(player.PIF_GUID, player.PIF_PlayerName);
            if (weekly != null && !IsWeeklyOnAir(weekly, now))
            {
                return ScheduleDecision.DefaultEmpty();
            }

            SpecialScheduleCache cache = null;
            using (var repo = new SpecialScheduleCacheRepository())
            {
                cache = repo.FindById(cacheKey);
                if (cache == null && !string.IsNullOrWhiteSpace(player.PIF_PlayerName))
                {
                    cache = repo.FindOne(x => string.Equals(x.PlayerName, player.PIF_PlayerName, StringComparison.OrdinalIgnoreCase));
                }
            }

            if (cache == null || cache.Schedules == null || cache.Schedules.Count == 0)
            {
                return ScheduleDecision.Fallback(fallbackPlaylist);
            }

            DateTime localNow = now;
            var candidates = new List<ScheduleCandidate>();
            foreach (var schedule in cache.Schedules)
            {
                if (schedule == null)
                {
                    continue;
                }

                if (!IsActive(schedule, localNow))
                {
                    continue;
                }

                candidates.Add(new ScheduleCandidate
                {
                    ScheduleId = schedule.Id ?? string.Empty,
                    PlaylistName = schedule.PageListName ?? string.Empty,
                    UpdatedAt = ParseDate(cache.UpdatedAt)
                });
            }

            if (candidates.Count == 0)
            {
                return ScheduleDecision.Fallback(fallbackPlaylist);
            }

            var selected = candidates
                .OrderByDescending(c => c.UpdatedAt)
                .ThenBy(c => c.ScheduleId, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();

            if (selected == null || string.IsNullOrWhiteSpace(selected.PlaylistName))
            {
                return ScheduleDecision.Fallback(player.PIF_CurrentPlayList);
            }

            return new ScheduleDecision
            {
                PlaylistName = selected.PlaylistName,
                ScheduleId = selected.ScheduleId,
                IsFromSchedule = true
            };
        }

        public void InvalidateWeeklyCache()
        {
            weeklyCacheLoadedAt = DateTime.MinValue;
        }

        private SharedWeeklyPlayScheduleInfo GetWeeklySchedule(string playerId, string playerName)
        {
            try
            {
                if (weeklyCache != null && weeklyCacheLoadedAt > DateTime.MinValue && (DateTime.Now - weeklyCacheLoadedAt).TotalSeconds < 10)
                {
                    return weeklyCache;
                }

                using (var repo = new WeeklyScheduleRepository())
                {
                    SharedWeeklyPlayScheduleInfo schedule = null;

                    if (!string.IsNullOrWhiteSpace(playerId))
                    {
                        schedule = repo.FindOne(x => string.Equals(x.PlayerID, playerId, StringComparison.OrdinalIgnoreCase));
                    }

                    if (schedule == null && !string.IsNullOrWhiteSpace(playerName))
                    {
                        schedule = repo.FindOne(x => string.Equals(x.PlayerName, playerName, StringComparison.OrdinalIgnoreCase));
                    }

                    if (schedule == null)
                    {
                        schedule = repo.FindOne(x => true);
                    }

                    weeklyCache = schedule;
                    weeklyCacheLoadedAt = DateTime.Now;
                    return weeklyCache;
                }
            }
            catch
            {
                weeklyCache = null;
                weeklyCacheLoadedAt = DateTime.Now;
                return null;
            }
        }

        private static bool IsWeeklyOnAir(SharedWeeklyPlayScheduleInfo schedule, DateTime now)
        {
            if (schedule == null)
            {
                return true;
            }

            DaySchedule target = null;
            switch (now.DayOfWeek)
            {
                case DayOfWeek.Sunday:
                    target = schedule.SunSch;
                    break;
                case DayOfWeek.Monday:
                    target = schedule.MonSch;
                    break;
                case DayOfWeek.Tuesday:
                    target = schedule.TueSch;
                    break;
                case DayOfWeek.Wednesday:
                    target = schedule.WedSch;
                    break;
                case DayOfWeek.Thursday:
                    target = schedule.ThuSch;
                    break;
                case DayOfWeek.Friday:
                    target = schedule.FriSch;
                    break;
                case DayOfWeek.Saturday:
                    target = schedule.SatSch;
                    break;
            }

            if (target == null)
            {
                return true;
            }

            if (!target.IsOnAir)
            {
                return false;
            }

            int start = (target.StartHour * 60) + target.StartMinute;
            int end = (target.EndHour * 60) + target.EndMinute;
            int current = now.Hour * 60 + now.Minute;

            if (start == end)
            {
                return true;
            }

            if (end > start)
            {
                return current >= start && current < end;
            }

            return current >= start || current < end;
        }

        private static bool IsActive(SpecialSchedulePayload schedule, DateTime now)
        {
            if (schedule == null)
            {
                return false;
            }

            bool crossesMidnight = schedule.DisplayEndH < schedule.DisplayStartH
                || (schedule.DisplayEndH == schedule.DisplayStartH && schedule.DisplayEndM < schedule.DisplayStartM);
            bool usePreviousDay = false;
            if (crossesMidnight)
            {
                var end = new TimeSpan(schedule.DisplayEndH, schedule.DisplayEndM, 0);
                if (now.TimeOfDay < end)
                {
                    usePreviousDay = true;
                }
            }

            DateTime effectiveDate = usePreviousDay ? now.AddDays(-1).Date : now.Date;

            if (!IsPeriodValid(schedule, effectiveDate))
            {
                return false;
            }

            if (!IsDayValid(schedule, now, usePreviousDay))
            {
                return false;
            }

            return IsTimeValid(schedule, now);
        }

        private static bool IsPeriodValid(SpecialSchedulePayload schedule, DateTime date)
        {
            if (schedule == null) return false;
            if (!schedule.IsPeriodEnable)
            {
                return true;
            }

            if (schedule.PeriodStartYear <= 0 || schedule.PeriodEndYear <= 0)
            {
                return true;
            }

            DateTime start;
            DateTime end;
            try
            {
                start = new DateTime(schedule.PeriodStartYear, schedule.PeriodStartMonth, schedule.PeriodStartDay);
                end = new DateTime(schedule.PeriodEndYear, schedule.PeriodEndMonth, schedule.PeriodEndDay);
            }
            catch
            {
                return true;
            }

            return date >= start.Date && date <= end.Date;
        }

        private static bool IsDayValid(SpecialSchedulePayload schedule, DateTime now, bool usePreviousDay)
        {
            DayOfWeek targetDay = usePreviousDay ? now.AddDays(-1).DayOfWeek : now.DayOfWeek;
            switch (targetDay)
            {
                case DayOfWeek.Sunday: return schedule.DayOfWeek1;
                case DayOfWeek.Monday: return schedule.DayOfWeek2;
                case DayOfWeek.Tuesday: return schedule.DayOfWeek3;
                case DayOfWeek.Wednesday: return schedule.DayOfWeek4;
                case DayOfWeek.Thursday: return schedule.DayOfWeek5;
                case DayOfWeek.Friday: return schedule.DayOfWeek6;
                case DayOfWeek.Saturday: return schedule.DayOfWeek7;
                default: return false;
            }
        }

        private static bool IsTimeValid(SpecialSchedulePayload schedule, DateTime now)
        {
            int start = (schedule.DisplayStartH * 60) + schedule.DisplayStartM;
            int end = (schedule.DisplayEndH * 60) + schedule.DisplayEndM;
            int current = now.Hour * 60 + now.Minute;

            if (start == end)
            {
                return true;
            }

            if (end > start)
            {
                return current >= start && current < end;
            }

            // Overnight
            return current >= start || current < end;
        }

        private static DateTime ParseDate(string value)
        {
            if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var dt))
            {
                return dt;
            }
            return DateTime.MinValue;
        }

        private sealed class ScheduleCandidate
        {
            public string ScheduleId { get; set; } = string.Empty;
            public string PlaylistName { get; set; } = string.Empty;
            public DateTime UpdatedAt { get; set; }
        }
    }

    internal sealed class ScheduleDecision
    {
        public string PlaylistName { get; set; } = string.Empty;
        public string ScheduleId { get; set; } = string.Empty;
        public bool IsFromSchedule { get; set; }

        public static ScheduleDecision DefaultEmpty()
        {
            return new ScheduleDecision();
        }

        public static ScheduleDecision Fallback(string playlist)
        {
            return new ScheduleDecision
            {
                PlaylistName = playlist ?? string.Empty,
                ScheduleId = string.Empty,
                IsFromSchedule = false
            };
        }
    }
}
