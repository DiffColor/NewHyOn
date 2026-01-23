using System;
using AndoW.Shared;
using System.Collections.Generic;
using System.Configuration;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNet.SignalR.Client;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TurtleTools;
using SharedWeeklyPlayScheduleInfo = AndoW.Shared.WeeklyPlayScheduleInfo;

namespace HyOnPlayer
{
    internal sealed class SignalRClientService : IDisposable
    {
        private const int DefaultPort = 5000;
        private const string DefaultHubPath = "/Data";
        private const int ReconnectDelayMs = 5000;

        private readonly MainWindow owner;
        private readonly RemoteCommandService commandService;
        private readonly object syncRoot = new object();
        private readonly ServerSettingsClient serverSettingsClient;
        private HubConnection connection;
        private IHubProxy hubProxy;
        private int reconnecting;
        private int stopping;
        private string currentUrl;

        public SignalRClientService(MainWindow owner, RemoteCommandService commandService)
        {
            this.owner = owner;
            this.commandService = commandService;
            serverSettingsClient = new ServerSettingsClient(owner?.g_LocalSettingsManager?.Settings?.ManagerIP);
        }

        public void Start()
        {
            Task.Run(StartAsync);
        }

        public void Stop()
        {
            Interlocked.Exchange(ref stopping, 1);
            HubConnection local;

            lock (syncRoot)
            {
                local = connection;
                hubProxy = null;
                connection = null;
            }

            if (local == null)
            {
                return;
            }

            try
            {
                local.Stop();
                local.Dispose();
            }
            catch (Exception ex)
            {
                Logger.WriteErrorLog($"SignalR client stop failed: {ex}", Logger.GetLogFileName());
            }
        }

        public void Reconnect()
        {
            Stop();
            Interlocked.Exchange(ref stopping, 0);
            Interlocked.Exchange(ref reconnecting, 0);
            Start();
        }

        public void Dispose()
        {
            Stop();
            serverSettingsClient?.Dispose();
        }

        private async Task StartAsync()
        {
            bool connected = await EnsureConnectionAsync(forceRefreshSettings: false);
            if (!connected)
            {
                ScheduleReconnect(allowStartWhenDisconnected: true);
            }
        }

        private HubConnection BuildConnection(string url)
        {
            var hub = new HubConnection(url, BuildQueryString());
            hubProxy = hub.CreateHubProxy("MsgHub");
            hubProxy.On<SignalRMessage>("ReceiveMessage", OnReceiveMessage);          
            hub.Closed += OnClosed;
            hub.Error += ex => Logger.WriteErrorLog($"SignalR client error: {ex}", Logger.GetLogFileName());

            return hub;
        }

        private IDictionary<string, string> BuildQueryString()
        {
            var query = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var player = owner?.g_PlayerInfoManager?.g_PlayerInfo;
            if (player == null)
            {
                return query;
            }

            if (!string.IsNullOrWhiteSpace(player.PIF_PlayerName))
            {
                query["playerName"] = player.PIF_PlayerName;
            }

            if (!string.IsNullOrWhiteSpace(player.PIF_GUID))
            {
                query["playerGuid"] = player.PIF_GUID;
            }

            return query;
        }

        private void OnClosed()
        {
            if (Interlocked.CompareExchange(ref stopping, 0, 0) == 1)
            {
                return;
            }

            Logger.WriteLog("SignalR client disconnected. Reconnecting...", Logger.GetLogFileName());
            ScheduleReconnect(allowStartWhenDisconnected: false);
        }

        public void SendHeartbeat(HeartbeatPayload payload)
        {
            if (payload == null)
            {
                return;
            }

            HubConnection localConnection;
            IHubProxy localProxy;
            lock (syncRoot)
            {
                localConnection = connection;
                localProxy = hubProxy;
            }

            if (localConnection == null || localProxy == null || localConnection.State != ConnectionState.Connected)
            {
                ScheduleReconnect(allowStartWhenDisconnected: true);
                return;
            }

            Task.Run(async () =>
            {
                try
                {
                    await localProxy.Invoke("ReportHeartbeat", payload);
                }
                catch (Exception ex)
                {
                    Logger.WriteErrorLog($"SignalR heartbeat send failed: {ex.Message}", Logger.GetLogFileName());
                    ScheduleReconnect(allowStartWhenDisconnected: true);
                }
            });
        }

        private void ScheduleReconnect(bool allowStartWhenDisconnected)
        {
            if (Interlocked.Exchange(ref reconnecting, 1) == 1)
            {
                return;
            }

            Task.Run(async () =>
            {
                try
                {
                    while (Interlocked.CompareExchange(ref stopping, 0, 0) == 0)
                    {
                        await Task.Delay(ReconnectDelayMs);
                        HubConnection local;
                        lock (syncRoot)
                        {
                            local = connection;
                        }

                        if (local == null && !allowStartWhenDisconnected)
                        {
                            return;
                        }

                        bool connected = await EnsureConnectionAsync(forceRefreshSettings: true);
                        if (connected)
                        {
                            return;
                        }
                    }
                }
                finally
                {
                    Interlocked.Exchange(ref reconnecting, 0);
                }
            });
        }

        private void OnReceiveMessage(SignalRMessage message)
        {
            if (message == null)
            {
                return;
            }

            if (string.Equals(message.DataType, "CommandQueue", StringComparison.OrdinalIgnoreCase))
            {
                var envelope = ExtractCommandEnvelope(message);
                if (envelope == null)
                {
                    return;
                }

                commandService?.HandleCommandFromSignalR(envelope);
                return;
            }

            if (string.Equals(message.DataType, "StateMessage", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (string.Equals(message.DataType, "WeeklySchedule", StringComparison.OrdinalIgnoreCase)
                || string.Equals(message.Command, "weekly-schedule-updated", StringComparison.OrdinalIgnoreCase))
            {
                var weekly = ExtractWeeklySchedule(message);
                if (weekly != null)
                {
                    commandService?.HandleWeeklyScheduleFromSignalR(weekly);
                }
                return;
            }

            string command = ExtractCommand(message);
            if (string.IsNullOrWhiteSpace(command))
            {
                return;
            }

            commandService?.HandleCommandFromSignalR(command);
        }

        private SignalRCommandEnvelope ExtractCommandEnvelope(SignalRMessage message)
        {
            if (message == null || message.Data == null)
            {
                return null;
            }

            if (message.Data is SignalRCommandEnvelope envelope)
            {
                return envelope;
            }

            try
            {
                if (message.Data is JObject jObject)
                {
                    return jObject.ToObject<SignalRCommandEnvelope>();
                }

                if (message.Data is string raw)
                {
                    if (string.IsNullOrWhiteSpace(raw))
                    {
                        return null;
                    }

                    return JsonConvert.DeserializeObject<SignalRCommandEnvelope>(raw);
                }

                string json = JsonConvert.SerializeObject(message.Data);
                if (string.IsNullOrWhiteSpace(json))
                {
                    return null;
                }

                return JsonConvert.DeserializeObject<SignalRCommandEnvelope>(json);
            }
            catch (Exception ex)
            {
                Logger.WriteErrorLog($"SignalR command envelope parse failed: {ex.Message}", Logger.GetLogFileName());
                return null;
            }
        }

        private string ExtractCommand(SignalRMessage message)
        {
            string command = message.Command ?? string.Empty;
            string dataCommand = message.Data as string;

            if (string.Equals(command, "Update", StringComparison.OrdinalIgnoreCase))
            {
                if (!string.IsNullOrWhiteSpace(dataCommand))
                {
                    command = dataCommand;
                }
                else
                {
                    command = "updatelist";
                }
            }

            if (string.IsNullOrWhiteSpace(command))
            {
                return null;
            }

            return command.Trim().ToLowerInvariant();
        }

        private SharedWeeklyPlayScheduleInfo ExtractWeeklySchedule(SignalRMessage message)
        {
            if (message == null || message.Data == null)
            {
                return null;
            }

            try
            {
                if (message.Data is SharedWeeklyPlayScheduleInfo weekly)
                {
                    return weekly;
                }

                if (message.Data is JObject jObj)
                {
                    return jObj.ToObject<WeeklyPlayScheduleInfo>();
                }

                if (message.Data is string raw)
                {
                    if (string.IsNullOrWhiteSpace(raw))
                    {
                        return null;
                    }

                    return JsonConvert.DeserializeObject<SharedWeeklyPlayScheduleInfo>(raw);
                }

                string json = JsonConvert.SerializeObject(message.Data);
                if (string.IsNullOrWhiteSpace(json))
                {
                    return null;
                }

                return JsonConvert.DeserializeObject<SharedWeeklyPlayScheduleInfo>(json);
            }
            catch (Exception ex)
            {
                Logger.WriteErrorLog($"SignalR weekly schedule parse failed: {ex.Message}", Logger.GetLogFileName());
                return null;
            }
        }

        private string BuildUrl()
        {
            return BuildUrl(forceRefreshSettings: false);
        }

        private string BuildUrl(bool forceRefreshSettings)
        {
            string rethinkHost = owner?.g_LocalSettingsManager?.Settings?.ManagerIP;
            serverSettingsClient?.UpdateHost(rethinkHost);
            var settings = serverSettingsClient?.GetSettings(forceRefreshSettings);

            string host = settings?.MessageServerIp;
            if (string.IsNullOrWhiteSpace(host))
            {
                Logger.WriteLog("SignalR client skipped: ServerSettings(MessageServerIp) not found in RethinkDB.", Logger.GetLogFileName());
                return null;
            }

            int port = ResolvePort();
            string hubPath = ResolveHubPath();
            if (!hubPath.StartsWith("/"))
            {
                hubPath = "/" + hubPath;
            }

            return $"http://{host}:{port}{hubPath}";
        }

        private async Task<bool> EnsureConnectionAsync(bool forceRefreshSettings)
        {
            string url = BuildUrl(forceRefreshSettings);
            if (string.IsNullOrWhiteSpace(url))
            {
                return false;
            }

            HubConnection local = null;
            HubConnection old = null;
            lock (syncRoot)
            {
                if (connection != null)
                {
                    if (string.Equals(currentUrl, url, StringComparison.OrdinalIgnoreCase))
                    {
                        local = connection;
                    }
                    else
                    {
                        old = connection;
                        hubProxy = null;
                        connection = null;
                        currentUrl = null;
                    }
                }

                if (local == null)
                {
                    local = BuildConnection(url);
                    connection = local;
                    currentUrl = url;
                }
            }

            if (old != null)
            {
                try
                {
                    old.Stop();
                    old.Dispose();
                }
                catch (Exception ex)
                {
                    Logger.WriteErrorLog($"SignalR client stop failed: {ex}", Logger.GetLogFileName());
                }
            }

            if (local.State == ConnectionState.Connected)
            {
                return true;
            }

            try
            {
                await local.Start();
                Logger.WriteLog($"SignalR client connected: {url}", Logger.GetLogFileName());
                return true;
            }
            catch (Exception ex)
            {
                Logger.WriteErrorLog($"SignalR client connect failed: {ex}", Logger.GetLogFileName());
                return false;
            }
        }

        private static int ResolvePort()
        {
            string value = ConfigurationManager.AppSettings["SignalRPort"];
            if (int.TryParse(value, out int port) && port > 0 && port <= 65535)
            {
                return port;
            }

            return DefaultPort;
        }

        private static string ResolveHubPath()
        {
            string value = ConfigurationManager.AppSettings["SignalRHubPath"];
            if (string.IsNullOrWhiteSpace(value))
            {
                return DefaultHubPath;
            }

            return value;
        }
    }

    public class SignalRMessage
    {
        public string From { get; set; } = "Server";

        public string To { get; set; } = "All";

        public string Command { get; set; } = "Update";

        public string DataType { get; set; } = "String";

        public object Data { get; set; } = null;
    }

    public class SignalRCommandEnvelope
    {
        public string CommandId { get; set; }
        public string Command { get; set; }
        public string PlayerId { get; set; }
        public string PayloadJson { get; set; }
        public string CreatedAt { get; set; }
        public bool IsUrgent { get; set; }
    }

    public class StateMessage
    {
        public string Who { get; set; } = "Unknown";

        public string State { get; set; } = "Disconnected";

        public string Description { get; set; } = "";
    }

    public class ProgressData
    {
        public string FromGUID { get; set; }

        public int Porgress { get; set; }
    }
}
