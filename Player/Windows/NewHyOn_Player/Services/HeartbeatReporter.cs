using System;
using System.Reflection;
using System.Threading;
using TurtleTools;

namespace NewHyOnPlayer
{
    internal sealed class HeartbeatReporter : IDisposable
    {
        private static readonly TimeSpan UpdateKeepAliveInterval = TimeSpan.FromSeconds(5);
        private static readonly TimeSpan UpdateProgressMinimumInterval = TimeSpan.FromMilliseconds(500);

        private readonly MainWindow owner;
        private readonly SignalRClientService signalRClientService;
        private readonly MultimediaTimer.Timer timer;
        private readonly object updateStateLock = new object();
        private readonly int intervalMs;
        private int isExecuting;
        private int terminalStopped;
        private bool disposed;
        private long updateReportingSessionId;
        private bool updateReportingActive;
        private string lastUpdateStatus = "UPDATING";
        private int lastUpdateProgress;
        private DateTime lastUpdateReportedAtUtc = DateTime.MinValue;

        public HeartbeatReporter(MainWindow owner, SignalRClientService signalRClientService, int intervalMs = 5000)
        {
            this.owner = owner;
            this.signalRClientService = signalRClientService;
            this.intervalMs = Math.Max(1000, intervalMs);

            timer = new MultimediaTimer.Timer
            {
                Mode = MultimediaTimer.TimerMode.OneShot,
                Period = this.intervalMs,
                Resolution = 1
            };
            timer.Tick += OnElapsed;
        }

        public void Start()
        {
            if (disposed || IsTerminalStopped()) return;
            ScheduleNext();
        }

        public void Stop()
        {
            if (disposed) return;
            timer.Stop();
        }

        public void SendHeartbeatNow()
        {
            if (IsTerminalStopped())
            {
                return;
            }

            ThreadPool.QueueUserWorkItem(_ => SendHeartbeatInternal(forceUpdateKeepAlive: true));
        }

        public long BeginUpdateReporting()
        {
            if (IsTerminalStopped())
            {
                return 0;
            }

            lock (updateStateLock)
            {
                updateReportingSessionId++;
                if (updateReportingSessionId <= 0)
                {
                    updateReportingSessionId = 1;
                }

                updateReportingActive = true;
                lastUpdateStatus = "UPDATING";
                lastUpdateProgress = 0;
                lastUpdateReportedAtUtc = DateTime.MinValue;
                return updateReportingSessionId;
            }
        }

        public void ReportUpdateNow(string status, int progress, bool force, long sessionId)
        {
            if (IsTerminalStopped())
            {
                return;
            }

            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    if (IsTerminalStopped())
                    {
                        return;
                    }

                    if (!TryBuildImmediateUpdatePayload(sessionId, status, progress, force, out var payload, out var shouldSend))
                    {
                        return;
                    }

                    SendHeartbeatBySignalR(payload, shouldSend);
                }
                catch (Exception ex)
                {
                    Logger.WriteErrorLog(ex.ToString(), Logger.GetLogFileName());
                }
            });
        }

        public void EndUpdateReporting(long sessionId, bool sendNormalHeartbeatNow)
        {
            if (IsTerminalStopped())
            {
                return;
            }

            bool shouldSendNormal = false;

            lock (updateStateLock)
            {
                if (sessionId > 0 && (!updateReportingActive || updateReportingSessionId != sessionId))
                {
                    return;
                }

                updateReportingActive = false;
                lastUpdateStatus = "UPDATING";
                lastUpdateProgress = 0;
                lastUpdateReportedAtUtc = DateTime.MinValue;
                shouldSendNormal = sendNormalHeartbeatNow;
            }

            if (shouldSendNormal)
            {
                SendHeartbeatNow();
            }
        }

        public void SendStoppedAndStopSignalR()
        {
            try
            {
                Interlocked.Exchange(ref terminalStopped, 1);
                Stop();

                lock (updateStateLock)
                {
                    updateReportingActive = false;
                    lastUpdateStatus = "UPDATING";
                    lastUpdateProgress = 0;
                    lastUpdateReportedAtUtc = DateTime.MinValue;
                }

                var payload = BuildPayload("stopped");
                if (payload != null)
                {
                    payload.Process = 0;
                    payload.CurrentPage = string.Empty;
                    payload.HdmiState = false;
                    signalRClientService?.SendStoppedAndStop(payload);
                    return;
                }
            }
            catch (Exception ex)
            {
                Logger.WriteErrorLog(ex.ToString(), Logger.GetLogFileName());
            }

            signalRClientService?.StopForExit();
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            Interlocked.Exchange(ref terminalStopped, 1);
            try
            {
                timer.Stop();
                timer.Dispose();
            }
            catch
            {
            }
        }

        private void OnElapsed(object sender, EventArgs e)
        {
            if (IsTerminalStopped())
            {
                return;
            }

            if (Interlocked.Exchange(ref isExecuting, 1) == 1)
            {
                ScheduleNext();
                return;
            }

            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    if (IsTerminalStopped())
                    {
                        return;
                    }

                    SendHeartbeatInternal(forceUpdateKeepAlive: false);
                }
                finally
                {
                    Interlocked.Exchange(ref isExecuting, 0);
                    ScheduleNext();
                }
            });
        }

        private void ScheduleNext()
        {
            if (disposed || IsTerminalStopped())
            {
                return;
            }

            timer.Stop();
            timer.Period = intervalMs;
            timer.Start();
        }

        private void SendHeartbeatInternal(bool forceUpdateKeepAlive)
        {
            if (IsTerminalStopped())
            {
                return;
            }

            try
            {
                if (TryBuildTimerUpdatePayload(forceUpdateKeepAlive, out var updatePayload, out var shouldSend))
                {
                    if (updatePayload != null)
                    {
                        SendHeartbeatBySignalR(updatePayload, shouldSend);
                    }
                    return;
                }

                var payload = BuildPayload();
                if (payload != null)
                {
                    SendHeartbeatBySignalR(payload, null);
                }
            }
            catch (Exception ex)
            {
                Logger.WriteErrorLog(ex.ToString(), Logger.GetLogFileName());
            }
        }

        private void SendHeartbeatBySignalR(HeartbeatPayload payload, Func<bool> shouldSend)
        {
            if (payload == null)
            {
                return;
            }

            if (IsTerminalStopped())
            {
                return;
            }

            signalRClientService?.SendHeartbeat(payload, shouldSend);
        }

        private bool TryBuildTimerUpdatePayload(bool force, out HeartbeatPayload payload, out Func<bool> shouldSend)
        {
            payload = null;
            shouldSend = null;

            string status = null;
            int progress = 0;
            long sessionId = 0;

            lock (updateStateLock)
            {
                if (!updateReportingActive)
                {
                    return false;
                }

                DateTime nowUtc = DateTime.UtcNow;
                if (!force && lastUpdateReportedAtUtc != DateTime.MinValue && nowUtc - lastUpdateReportedAtUtc < UpdateKeepAliveInterval)
                {
                    return true;
                }

                sessionId = updateReportingSessionId;
                status = lastUpdateStatus;
                progress = lastUpdateProgress;
                lastUpdateReportedAtUtc = nowUtc;
            }

            payload = BuildPayload(status, progress);
            if (payload == null)
            {
                return true;
            }

            shouldSend = () => IsCurrentUpdateSession(sessionId);
            return true;
        }

        private bool TryBuildImmediateUpdatePayload(long sessionId, string status, int progress, bool force, out HeartbeatPayload payload, out Func<bool> shouldSend)
        {
            payload = null;
            shouldSend = null;

            string normalizedStatus;
            int normalizedProgress;

            lock (updateStateLock)
            {
                if (!updateReportingActive || updateReportingSessionId != sessionId)
                {
                    return false;
                }

                normalizedStatus = NormalizeUpdateStatus(status);
                normalizedProgress = NormalizeHeartbeatProcess(progress);

                bool sameStatus = string.Equals(lastUpdateStatus, normalizedStatus, StringComparison.OrdinalIgnoreCase);
                if (!force &&
                    sameStatus &&
                    lastUpdateProgress == normalizedProgress)
                {
                    return false;
                }

                if (!force &&
                    sameStatus &&
                    lastUpdateReportedAtUtc != DateTime.MinValue &&
                    DateTime.UtcNow - lastUpdateReportedAtUtc < UpdateProgressMinimumInterval)
                {
                    return false;
                }

                lastUpdateStatus = normalizedStatus;
                lastUpdateProgress = normalizedProgress;
                lastUpdateReportedAtUtc = DateTime.UtcNow;
            }

            payload = BuildPayload(normalizedStatus, normalizedProgress);
            if (payload == null)
            {
                return false;
            }

            shouldSend = () => IsCurrentUpdateSession(sessionId);
            return true;
        }

        private bool IsCurrentUpdateSession(long sessionId)
        {
            lock (updateStateLock)
            {
                return !IsTerminalStopped() && updateReportingActive && updateReportingSessionId == sessionId;
            }
        }

        private bool IsTerminalStopped()
        {
            return Interlocked.CompareExchange(ref terminalStopped, 0, 0) == 1;
        }

        private static string NormalizeUpdateStatus(string status)
        {
            if (string.IsNullOrWhiteSpace(status))
            {
                return "UPDATING";
            }

            return status.Trim().ToUpperInvariant();
        }

        private HeartbeatPayload BuildPayload(string overrideStatus = null, int? overrideProcess = null)
        {
            var player = owner?.g_PlayerInfoManager?.g_PlayerInfo;
            if (player == null)
            {
                return null;
            }

            if (string.IsNullOrWhiteSpace(player.PIF_GUID))
            {
                owner?.RequestPlayerGuidSyncNow();
                Logger.WriteLog("Heartbeat skipped: player GUID is empty. Requesting RethinkDB sync.", Logger.GetLogFileName());
                return null;
            }

            string clientId = player.PIF_GUID;

            string status = overrideStatus;
            int process = overrideProcess ?? 0;

            if (string.IsNullOrWhiteSpace(status) && !overrideProcess.HasValue)
            {
                try
                {
                    var commandService = owner?.CommandService;
                    if (commandService != null && commandService.TryGetHeartbeatUpdate(out string updateStatus, out int updateProgress))
                    {
                        status = updateStatus;
                        process = updateProgress;
                    }
                }
                catch
                {
                }
            }

            if (string.IsNullOrWhiteSpace(status))
            {
                if (owner != null && owner.IsUpdating)
                {
                    status = "updating";
                }
                else if (owner != null && owner.IsPlaying)
                {
                    status = "playing";
                }
                else
                {
                    status = "idle";
                }
            }

            bool hdmiState = true;
            if (string.Equals(status, "stopped", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(status, "idle", StringComparison.OrdinalIgnoreCase))
            {
                hdmiState = false;
            }

            return new HeartbeatPayload
            {
                ClientId = clientId,
                Status = status,
                Process = NormalizeHeartbeatProcess(process),
                Version = GetHeartbeatVersion(),
                CurrentPage = owner?.g_CurrentPageName ?? string.Empty,
                HdmiState = hdmiState
            };
        }


        private static string GetHeartbeatVersion()
        {
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            if (version == null)
            {
                return "v0.0.0";
            }

            var fieldCount = version.Revision > 0 ? 4 : 3;
            return $"v{version.ToString(fieldCount)}";
        }


        private static int NormalizeHeartbeatProcess(int progress)
        {
            int value = progress;
            if (value < 0)
            {
                value = 0;
            }
            else if (value > 100)
            {
                value = 100;
            }

            return value;
        }
    }

    internal sealed class HeartbeatPayload
    {
        public string ClientId { get; set; }
        public string Status { get; set; }
        public int Process { get; set; }
        public string Version { get; set; }
        public string CurrentPage { get; set; }
        public bool HdmiState { get; set; }
    }
}
