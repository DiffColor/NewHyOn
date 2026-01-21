using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
//using RethinkDb.Driver;
//using RethinkDb.Driver.Net;
using TurtleTools;

namespace HyOnPlayer
{
    internal sealed class HeartbeatReporter : IDisposable
    {
        private readonly MainWindow owner;
        private readonly SignalRClientService signalRClientService;
        //private readonly HeartbeatClient client;
        private readonly MultimediaTimer.Timer timer;
        private readonly int intervalMs;
        private int isExecuting;
        private bool disposed;
        private readonly string managerHost;

        public HeartbeatReporter(MainWindow owner, SignalRClientService signalRClientService, int intervalMs = 5000)
        {
            this.owner = owner;
            this.signalRClientService = signalRClientService;
            this.intervalMs = Math.Max(1000, intervalMs);
            managerHost = owner?.g_LocalSettingsManager?.Settings?.ManagerIP;
            //client = new HeartbeatClient(string.IsNullOrWhiteSpace(managerHost) ? "127.0.0.1" : managerHost);

            timer = new MultimediaTimer.Timer
            {
                Mode = MultimediaTimer.TimerMode.Periodic,
                Period = this.intervalMs,
                Resolution = 1
            };
            timer.Tick += OnElapsed;
        }

        public void Start()
        {
            if (disposed) return;
            timer.Start();
        }

        public void Stop()
        {
            if (disposed) return;
            timer.Stop();
        }

        public void SendHeartbeatNow()
        {
            ThreadPool.QueueUserWorkItem(_ => SendHeartbeatInternal());
        }

        public void SendStopped()
        {
            try
            {
                var payload = BuildPayload("stopped");
                if (payload != null)
                {
                    payload.Process = 0;
                    payload.CurrentPage = string.Empty;
                    payload.HdmiState = false;
                    SendHeartbeatBySignalR(payload);
                }
            }
            catch (Exception ex)
            {
                Logger.WriteErrorLog(ex.ToString(), Logger.GetLogFileName());
            }
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
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
            if (Interlocked.Exchange(ref isExecuting, 1) == 1)
            {
                return;
            }

            Stop();
            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    SendHeartbeatInternal();
                }
                finally
                {
                    Interlocked.Exchange(ref isExecuting, 0);
                    Start();
                }
            });
        }

        private void SendHeartbeatInternal()
        {
            try
            {
                var payload = BuildPayload();
                if (payload != null)
                {
                    SendHeartbeatBySignalR(payload);
                }
            }
            catch (Exception ex)
            {
                Logger.WriteErrorLog(ex.ToString(), Logger.GetLogFileName());
            }
        }

        private void SendHeartbeatBySignalR(HeartbeatPayload payload)
        {
            if (payload == null)
            {
                return;
            }

            signalRClientService?.SendHeartbeat(payload);
        }

        private HeartbeatPayload BuildPayload(string overrideStatus = null)
        {
            var player = owner?.g_PlayerInfoManager?.g_PlayerInfo;
            if (player == null)
            {
                return null;
            }

            if (string.IsNullOrWhiteSpace(player.PIF_GUID))
            {
                return null;
            }

            string status = overrideStatus;
            int process = 0;

            if (string.IsNullOrWhiteSpace(status))
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

            return new HeartbeatPayload
            {
                ClientId = player.PIF_GUID,
                Status = status,
                Process = process,
                Version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.0.0",
                CurrentPage = owner?.g_CurrentPageName ?? string.Empty,
                HdmiState = true,
                Timestamp = DateTime.Now
            };
        }
    }

    /*
    internal sealed class HeartbeatClient
    {
        private const string DatabaseName = "AndoW";
        private const string HeartbeatTable = "ClientHeartbeat";

        private static readonly RethinkDB R = RethinkDB.R;
        private readonly object syncRoot = new object();
        private Connection connection;
        private string host = "127.0.0.1";
        private int port = 28015;
        private string username = "admin";
        private string password = "turtle04!9";
        private bool heartbeatTableReady;

        public HeartbeatClient(string host = "127.0.0.1")
        {
            if (!string.IsNullOrWhiteSpace(host))
            {
                this.host = host;
            }
        }

        public void SendHeartbeat(HeartbeatPayload payload)
        {
            if (payload == null || string.IsNullOrWhiteSpace(payload.ClientId))
            {
                return;
            }

            try
            {
                EnsureHeartbeatTable();

                var map = new Dictionary<string, object>
                {
                    ["id"] = payload.ClientId,
                    ["status"] = payload.Status ?? string.Empty,
                    ["process"] = payload.Process,
                    ["version"] = payload.Version ?? string.Empty,
                    ["currentPage"] = payload.CurrentPage ?? string.Empty,
                    ["hdmiState"] = payload.HdmiState,
                    ["heartbeatTs"] = payload.TimestampString
                };

                R.Db(DatabaseName)
                    .Table(HeartbeatTable)
                    .Insert(map)
                    .OptArg("conflict", "replace")
                    .RunNoReply(GetConnection());
            }
            catch (Exception ex)
            {
                Logger.WriteErrorLog(ex.ToString(), Logger.GetLogFileName());
                ResetConnection();
            }
        }

        public void SendHeartbeatStopped(HeartbeatPayload payload)
        {
            if (payload == null || string.IsNullOrWhiteSpace(payload.ClientId))
            {
                return;
            }

            payload.Status = "stopped";
            payload.Process = 0;
            payload.CurrentPage = string.Empty;
            payload.HdmiState = false;

            try
            {
                EnsureHeartbeatTable();

                var map = new Dictionary<string, object>
                {
                    ["id"] = payload.ClientId,
                    ["status"] = payload.Status,
                    ["process"] = payload.Process,
                    ["version"] = payload.Version ?? string.Empty,
                    ["currentPage"] = payload.CurrentPage,
                    ["hdmiState"] = payload.HdmiState,
                    ["heartbeatTs"] = payload.TimestampString
                };

                R.Db(DatabaseName)
                    .Table(HeartbeatTable)
                    .Insert(map)
                    .OptArg("conflict", "replace")
                    .RunNoReply(GetConnection());
            }
            catch (Exception ex)
            {
                Logger.WriteErrorLog(ex.ToString(), Logger.GetLogFileName());
                ResetConnection();
            }
        }

        private Connection GetConnection()
        {
            lock (syncRoot)
            {
                if (connection != null && connection.Open)
                {
                    return connection;
                }

                connection = R.Connection()
                    .Hostname(host)
                    .Port(port)
                    .User(username, password)
                    .Timeout(3000)
                    .Connect();

                heartbeatTableReady = false;
                return connection;
            }
        }

        private void ResetConnection()
        {
            lock (syncRoot)
            {
                if (connection != null)
                {
                    try
                    {
                        connection.Close(false);
                        connection.Dispose();
                    }
                    catch
                    {
                    }
                    connection = null;
                }
                heartbeatTableReady = false;
            }
        }

        private void EnsureHeartbeatTable()
        {
            if (heartbeatTableReady)
            {
                return;
            }

            lock (syncRoot)
            {
                if (heartbeatTableReady)
                {
                    return;
                }

                try
                {
                    var tables = R.Db(DatabaseName).TableList().Run<List<string>>(GetConnection());
                    if (!tables.Contains(HeartbeatTable))
                    {
                        R.Db(DatabaseName)
                            .TableCreate(HeartbeatTable)
                            .OptArg("primary_key", "id")
                            .Run(GetConnection());
                    }
                    heartbeatTableReady = true;
                }
                catch (Exception ex)
                {
                    Logger.WriteErrorLog(ex.ToString(), Logger.GetLogFileName());
                    heartbeatTableReady = false;
                }
            }
        }
    }
    */

    internal sealed class HeartbeatPayload
    {
        public string ClientId { get; set; }
        public string Status { get; set; }
        public int Process { get; set; }
        public string Version { get; set; }
        public string CurrentPage { get; set; }
        public bool HdmiState { get; set; }
        public DateTime Timestamp { get; set; }

        public string TimestampString => Timestamp.ToString("yyyy-MM-dd HH:mm:ss");
    }
}
