using AndoW.Shared;
using System;
using System.Threading;
using HyOnPlayer.DataManager;
using HyOnPlayer;
using TurtleTools;
using SharedWeeklyPlayScheduleInfo = AndoW.Shared.WeeklyPlayScheduleInfo;

namespace HyOnPlayer.Services
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
        private bool monitorPowerBlocked;
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

            DaySchedule target = null;
            switch (now.DayOfWeek)
            {
                case DayOfWeek.Sunday:
                    target = weekly.SunSch;
                    break;
                case DayOfWeek.Monday:
                    target = weekly.MonSch;
                    break;
                case DayOfWeek.Tuesday:
                    target = weekly.TueSch;
                    break;
                case DayOfWeek.Wednesday:
                    target = weekly.WedSch;
                    break;
                case DayOfWeek.Thursday:
                    target = weekly.ThuSch;
                    break;
                case DayOfWeek.Friday:
                    target = weekly.FriSch;
                    break;
                case DayOfWeek.Saturday:
                    target = weekly.SatSch;
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
                    owner?.Dispatcher?.Invoke(() =>
                    {
                        try
                        {
                            owner.StopAllTimer();
                        }
                        catch
                        {
                        }
                        owner?.DoApplicationShutdown();
                    });
                    break;
                case PowerControlType.BlackScreen:
                    ApplyBlackScreen();
                    break;
                case PowerControlType.Hibernation:
                    ProcessTools.ExecuteCommand("shutdown /h");
                    break;
                default:
                    owner?.Dispatcher?.Invoke(() =>
                    {
                        try
                        {
                            owner.StopAllTimer();
                        }
                        catch
                        {
                        }
                        owner?.DoApplicationShutdown();
                    });
                    break;
            }
        }

        private void ApplyBlackScreen()
        {
            owner?.Dispatcher?.Invoke(() =>
            {
                try
                {
                    owner.HideAllContentsPlayWindow();
                    owner.Opacity = 0;
                }
                catch
                {
                }
            });
            blackScreenApplied = true;
            try
            {
                WindowTools.AllowSleep();
            }
            catch
            {
            }
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
                        owner.PopPage();
                    }
                    catch
                    {
                    }
                });
                try
                {
                    WindowTools.PreventSleep();
                }
                catch
                {
                }
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
                SendMessage(new IntPtr(-1), WM_SYSCOMMAND, new IntPtr(SC_MONITORPOWER), new IntPtr(on ? MONITOR_ON : MONITOR_OFF));
            }
            catch
            {
            }
        }

        private const int WM_SYSCOMMAND = 0x0112;
        private const int SC_MONITORPOWER = 0xF170;
        private const int MONITOR_ON = -1;
        private const int MONITOR_OFF = 2;

        [System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint = "SendMessageW")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
    }
}
