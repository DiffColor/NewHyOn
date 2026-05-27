using AndoW.Shared;
using Microsoft.Win32.SafeHandles;
using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using TurtleTools;
using SharedWeeklyPlayScheduleInfo = AndoW.Shared.WeeklyPlayScheduleInfo;

namespace NewHyOnPlayer.Services
{
    internal sealed class OnAirService : IDisposable
    {
        private readonly MainWindow owner;
        private readonly MultimediaTimer.Timer timer;
        private readonly int intervalMs;
        private int isChecking;
        private bool disposed;
        private bool lastOnAir = true;
        private bool blackScreenApplied;
        private bool hiddenApplied;
        private bool monitorPowerBlocked;
        private int hibernationResumeStarted;
        private SharedWeeklyPlayScheduleInfo cachedWeekly;
        private DateTime cachedLoadedAt = DateTime.MinValue;

        public OnAirService(MainWindow owner, int intervalMs = 15000)
        {
            this.owner = owner;
            this.intervalMs = Math.Max(5000, intervalMs);
            timer = new MultimediaTimer.Timer
            {
                Mode = MultimediaTimer.TimerMode.Periodic,
                Period = this.intervalMs,
                Resolution = 1
            };
            timer.Tick += OnTick;
        }

        public void Start()
        {
            if (disposed)
            {
                return;
            }

            timer.Start();
            ThreadPool.QueueUserWorkItem(_ => CheckOnAirNow());
        }

        public void Stop()
        {
            if (disposed)
            {
                return;
            }

            timer.Stop();
        }

        internal bool IsOnAirNow()
        {
            return IsOnAir(DateTime.Now);
        }

        public void RefreshWeeklySchedule()
        {
            if (disposed)
            {
                return;
            }

            cachedWeekly = null;
            cachedLoadedAt = DateTime.MinValue;

            if (Interlocked.Exchange(ref isChecking, 1) == 1)
            {
                return;
            }

            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    CheckOnAirNow();
                }
                finally
                {
                    Interlocked.Exchange(ref isChecking, 0);
                }
            });
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            try
            {
                RestoreMonitor();
                timer.Stop();
                timer.Dispose();
            }
            catch
            {
            }
        }

        private void OnTick(object sender, EventArgs e)
        {
            if (Interlocked.Exchange(ref isChecking, 1) == 1)
            {
                return;
            }

            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    CheckOnAirNow();
                }
                finally
                {
                    Interlocked.Exchange(ref isChecking, 0);
                }
            });
        }

        private void CheckOnAirNow()
        {
            try
            {
                bool isOnAir = IsOnAir(DateTime.Now);
                if (isOnAir)
                {
                    if (!lastOnAir)
                    {
                        lastOnAir = true;
                        RestoreFromOffAir();
                        ProcessTools.ExecuteCommand("shutdown /r /t 0");
                    }
                    return;
                }

                if (!lastOnAir)
                {
                    return;
                }

                lastOnAir = false;
                HandleOffAirAction();
            }
            catch (Exception ex)
            {
                Logger.WriteErrorLog($"OnAirService error: {ex}", Logger.GetLogFileName());
            }
        }

        private bool IsOnAir(DateTime now)
        {
            var settings = owner?.g_LocalSettingsManager?.Settings;
            if (settings == null)
            {
                return true;
            }

            if (settings.IsAllDayPlay)
            {
                return true;
            }

            var player = owner?.g_PlayerInfoManager?.g_PlayerInfo;
            var weekly = GetWeeklySchedule(player);
            if (weekly == null)
            {
                return true;
            }

            DaySchedule today = GetDaySchedule(weekly, now.DayOfWeek);
            if (today == null)
            {
                return true;
            }

            DaySchedule yesterday = GetDaySchedule(weekly, now.AddDays(-1).DayOfWeek) ?? DaySchedule.CreateDefault();

            int current = TimeToInt(now);
            int todayStart = TimeToInt(today.StartHour, today.StartMinute);

            if (!yesterday.IsOnAir && current < todayStart)
            {
                return false;
            }

            int yesterdayStart = TimeToInt(yesterday.StartHour, yesterday.StartMinute);
            int yesterdayEnd = TimeToInt(yesterday.EndHour, yesterday.EndMinute);

            if ((yesterdayEnd - yesterdayStart) > 0 && current < todayStart)
            {
                return false;
            }

            if (current >= yesterdayEnd && current < todayStart)
            {
                return false;
            }

            int todayEnd = TimeToInt(today.EndHour, today.EndMinute);
            if ((todayEnd - todayStart) > 0 && current >= todayEnd)
            {
                return false;
            }

            return today.IsOnAir;
        }

        private SharedWeeklyPlayScheduleInfo GetWeeklySchedule(PlayerInfoClass player)
        {
            if (player == null)
            {
                return null;
            }

            if (cachedWeekly != null && (DateTime.Now - cachedLoadedAt).TotalSeconds < 30)
            {
                return cachedWeekly;
            }

            try
            {
                using (var repo = new WeeklyScheduleRepository())
                {
                    SharedWeeklyPlayScheduleInfo schedule = null;
                    if (!string.IsNullOrWhiteSpace(player.PIF_GUID))
                    {
                        schedule = repo.FindOne(x => string.Equals(x.PlayerID, player.PIF_GUID, StringComparison.OrdinalIgnoreCase));
                    }

                    if (schedule == null && !string.IsNullOrWhiteSpace(player.PIF_PlayerName))
                    {
                        schedule = repo.FindOne(x => string.Equals(x.PlayerName, player.PIF_PlayerName, StringComparison.OrdinalIgnoreCase));
                    }

                    if (schedule == null)
                    {
                        schedule = repo.FindOne(x => true);
                    }

                    cachedWeekly = schedule;
                    cachedLoadedAt = DateTime.Now;
                    return cachedWeekly;
                }
            }
            catch (Exception ex)
            {
                Logger.WriteErrorLog($"OnAirService schedule load error: {ex}", Logger.GetLogFileName());
                cachedWeekly = null;
                cachedLoadedAt = DateTime.Now;
                return null;
            }
        }

        private void HandleOffAirAction()
        {
            owner?.Dispatcher?.Invoke(() => {
                owner.ResetPlaybackCursor();
            });

            var settings = owner?.g_LocalSettingsManager?.Settings;
            string actionValue = settings?.EndTimeAction ?? PowerControlType.ApplicationClose.ToString();

            if (settings != null && settings.BlockMonitorOnEndTime)
            {
                BlockMonitor();
                return;
            }

            if (!Enum.TryParse(actionValue, true, out PowerControlType action))
            {
                action = PowerControlType.ApplicationClose;
            }

            Logger.WriteLog($"방송시간이 아니므로 종료 동작 수행: {action}", Logger.GetLogFileName());

            switch (action)
            {
                case PowerControlType.SystemOff:
                    ProcessTools.ExecuteCommand("shutdown /s /t 0");
                    break;
                case PowerControlType.SystemReboot:
                    ProcessTools.ExecuteCommand("shutdown /r /t 0");
                    break;
                case PowerControlType.ApplicationClose:
                    ApplyHidden();
                    break;
                case PowerControlType.BlackScreen:
                    ApplyBlackScreen();
                    break;
                case PowerControlType.Hibernation:
                    ApplyHibernation();
                    break;
                default:
                    owner?.Dispatcher?.Invoke(() =>
                    {
                        try
                        {
                            owner.StopPlayback();
                        }
                        catch
                        {
                        }
                        //owner?.DoApplicationShutdown();
                    });
                    break;
            }
        }

        private void ApplyHibernation()
        {
            Logger.WriteLog("<<<<<< Hibernation >>>>>>>>>>", Logger.GetLogFileName());

            var player = owner?.g_PlayerInfoManager?.g_PlayerInfo;
            var weekly = GetWeeklySchedule(player);
            DateTime? wakeAt = FindNextWakeUpTime(weekly, DateTime.Now);
            if (wakeAt.HasValue)
            {
                SetWakeUpAlarm(wakeAt.Value);
            }

            WindowTools.AllowSleep();
            Thread.Sleep(3000);

            if (!Application.SetSuspendState(PowerState.Suspend, false, false))
            {
                Logger.WriteErrorLog("Hibernation suspend request failed.", Logger.GetLogFileName());
            }
        }

        private void SetWakeUpAlarm(DateTime wakeAt)
        {
            long wakeTime = wakeAt.ToFileTime();
            Interlocked.Exchange(ref hibernationResumeStarted, 0);

            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    using (SafeWaitHandle handle = CreateWaitableTimer(IntPtr.Zero, true, "NewHyOnPlayerWakeUpTimer"))
                    {
                        if (handle == null || handle.IsInvalid)
                        {
                            Logger.WriteErrorLog($"CreateWaitableTimer failed: {Marshal.GetLastWin32Error()}", Logger.GetLogFileName());
                            return;
                        }

                        if (!SetWaitableTimer(handle, ref wakeTime, 0, IntPtr.Zero, IntPtr.Zero, true))
                        {
                            Logger.WriteErrorLog($"SetWaitableTimer failed: {Marshal.GetLastWin32Error()}", Logger.GetLogFileName());
                            return;
                        }

                        using (EventWaitHandle waitHandle = new EventWaitHandle(false, EventResetMode.AutoReset))
                        {
                            waitHandle.SafeWaitHandle = handle;
                            waitHandle.WaitOne();
                        }
                    }

                    ResumeFromHibernation();
                }
                catch (Exception ex)
                {
                    Logger.WriteErrorLog($"WakeUpAlarm error: {ex}", Logger.GetLogFileName());
                }
            });

            Logger.WriteLog($"잠들 시간: {DateTime.Now} / 깨어날 시간: {wakeAt}", Logger.GetLogFileName());
        }

        private void ResumeFromHibernation()
        {
            if (Interlocked.Exchange(ref hibernationResumeStarted, 1) == 1)
            {
                return;
            }

            Thread.Sleep(15000);
            Logger.WriteLog("<<<<<< Resume >>>>>>>>>>", Logger.GetLogFileName());
            Logger.WriteLog($"깨어난 시간: {DateTime.Now}", Logger.GetLogFileName());
            ProcessTools.ExecuteCommand("shutdown -r -t 0 -f");
        }

        private DateTime? FindNextWakeUpTime(SharedWeeklyPlayScheduleInfo weekly, DateTime now)
        {
            if (weekly == null || CountOnAirDays(weekly) <= 0)
            {
                return null;
            }

            DaySchedule today = GetDaySchedule(weekly, now.DayOfWeek);
            int current = TimeToInt(now);
            if (today != null && today.IsOnAir)
            {
                int todayStart = TimeToInt(today.StartHour, today.StartMinute);
                if (current < todayStart)
                {
                    return BuildWakeUpTime(now.Date, today);
                }
            }

            int todayIndex = (int)now.DayOfWeek;
            for (int offset = 1; offset <= 7; offset++)
            {
                DayOfWeek dayOfWeek = (DayOfWeek)((todayIndex + offset) % 7);
                DaySchedule schedule = GetDaySchedule(weekly, dayOfWeek);
                if (schedule == null || !schedule.IsOnAir)
                {
                    continue;
                }

                return BuildWakeUpTime(now.Date.AddDays(offset), schedule);
            }

            return null;
        }

        private static DateTime BuildWakeUpTime(DateTime date, DaySchedule schedule)
        {
            return date
                .AddHours(schedule.StartHour)
                .AddMinutes(schedule.StartMinute)
                .AddSeconds(30);
        }

        private static int CountOnAirDays(SharedWeeklyPlayScheduleInfo weekly)
        {
            int count = 0;
            for (int i = 0; i < 7; i++)
            {
                DaySchedule schedule = GetDaySchedule(weekly, (DayOfWeek)i);
                if (schedule != null && schedule.IsOnAir)
                {
                    count++;
                }
            }

            return count;
        }

        private static DaySchedule GetDaySchedule(SharedWeeklyPlayScheduleInfo weekly, DayOfWeek dayOfWeek)
        {
            if (weekly == null)
            {
                return null;
            }

            switch (dayOfWeek)
            {
                case DayOfWeek.Sunday:
                    return weekly.SunSch;
                case DayOfWeek.Monday:
                    return weekly.MonSch;
                case DayOfWeek.Tuesday:
                    return weekly.TueSch;
                case DayOfWeek.Wednesday:
                    return weekly.WedSch;
                case DayOfWeek.Thursday:
                    return weekly.ThuSch;
                case DayOfWeek.Friday:
                    return weekly.FriSch;
                case DayOfWeek.Saturday:
                    return weekly.SatSch;
                default:
                    return null;
            }
        }

        private static int TimeToInt(DateTime time)
        {
            return (time.Hour * 100) + time.Minute;
        }

        private static int TimeToInt(int hour, int minute)
        {
            return (hour * 100) + minute;
        }

        private void ApplyBlackScreen()
        {
            owner?.Dispatcher?.Invoke(() =>
            {
                try
                {
                    owner.StopPlayback();
                    owner.HidePlayback();
                    owner.Opacity = 0;
                }
                catch
                {
                }
            });
            blackScreenApplied = true;
        }

        private void ApplyHidden()
        {
            if (hiddenApplied)
            {
                return;
            }

            hiddenApplied = true;
            owner?.Dispatcher?.Invoke(() =>
            {
                try
                {
                    owner.StopPlayback();
                    owner.HidePlayback();
                    owner.Hide();
                }
                catch
                {
                }
            });
        }


        private void RestoreFromOffAir()
        {
            RestoreMonitor();

            if (blackScreenApplied)
            {
                blackScreenApplied = false;
                owner?.Dispatcher?.Invoke(() =>
                {
                    try
                    {
                        owner.Opacity = 1;
                        owner.Show();
                        owner.PopPage();
                    }
                    catch
                    {
                    }
                });
            }

            if (hiddenApplied)
            {
                hiddenApplied = false;
                owner?.Dispatcher?.Invoke(() =>
                {
                    try
                    {
                        owner.Show();
                        owner.Opacity = 1;
                        owner.StartPlaybackFromOffAir();
                    }
                    catch
                    {
                    }
                });
            }

            Logger.WriteLog("방송시간 재진입: 재생 유지", Logger.GetLogFileName());
        }

        private void BlockMonitor()
        {
            if (monitorPowerBlocked)
            {
                return;
            }

            monitorPowerBlocked = true;
            try
            {
                SendMonitorPower(false);
            }
            catch
            {
            }
        }

        private void RestoreMonitor()
        {
            if (!monitorPowerBlocked)
            {
                return;
            }

            monitorPowerBlocked = false;
            try
            {
                SendMonitorPower(true);
            }
            catch
            {
            }
        }

        private void SendMonitorPower(bool on)
        {
            try
            {
                PostMessage(new IntPtr(-1), WM_SYSCOMMAND, new IntPtr(SC_MONITORPOWER), new IntPtr(on ? MONITOR_ON : MONITOR_OFF));
            }
            catch
            {
            }
        }

        private const int WM_SYSCOMMAND = 0x0112;
        private const int SC_MONITORPOWER = 0xF170;
        private const int MONITOR_ON = -1;
        private const int MONITOR_OFF = 2;

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern SafeWaitHandle CreateWaitableTimer(IntPtr lpTimerAttributes, bool bManualReset, string lpTimerName);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetWaitableTimer(
            SafeWaitHandle hTimer,
            ref long pDueTime,
            int lPeriod,
            IntPtr pfnCompletionRoutine,
            IntPtr lpArgToCompletionRoutine,
            bool fResume);

        [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool PostMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
    }
}
