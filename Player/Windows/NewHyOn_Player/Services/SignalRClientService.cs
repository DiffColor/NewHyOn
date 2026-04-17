using System;
using AndoW.Shared;
using System.Collections.Generic;
using System.Configuration;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TurtleTools;
using SharedWeeklyPlayScheduleInfo = AndoW.Shared.WeeklyPlayScheduleInfo;

namespace NewHyOnPlayer
{
    internal sealed class SignalRClientService : IDisposable
    {
        private const int DefaultPort = 5000;
        private const string DefaultHubPath = "/Data";
        private const int ReconnectDelayMs = 5000;
        private const int ConnectTimeoutMs = 15000;
        private const int HeartbeatTimeoutMs = 5000;
        private const int MaxSignalRActionQueue = 10;

        private readonly MainWindow owner;
        private readonly RemoteCommandService commandService;
        private readonly object syncRoot = new object();
        private readonly object actionQueueLock = new object();
        private readonly ServerSettingsClient serverSettingsClient;
        private readonly Queue<SignalRAction> actionQueue = new Queue<SignalRAction>();
        private HubConnection connection;
        private int queuedActionProcessor;
        private int reconnectScheduled;
        private int stopping;
        private int terminalHeartbeatMode;
        private long heartbeatGeneration;
        private string currentUrl;
        private CancellationTokenSource reconnectDelayCts;

        public SignalRClientService(MainWindow owner, RemoteCommandService commandService)
        {
            this.owner = owner;
            this.commandService = commandService;
            serverSettingsClient = new ServerSettingsClient(owner?.g_LocalSettingsManager?.Settings?.ManagerIP);
            reconnectDelayCts = new CancellationTokenSource();
        }

        public void Start()
        {
            Interlocked.Exchange(ref stopping, 0);
            Interlocked.Exchange(ref terminalHeartbeatMode, 0);
            EnqueueAction("start", () => EnsureConnectionAsync(forceRefreshSettings: false));
        }

        public void Stop()
        {
            Interlocked.Exchange(ref stopping, 1);
            InvalidateHeartbeatQueue();
            CancelReconnectSchedule();
            ClearPendingActions();
            StopConnection();
        }

        public void StopForExit()
        {
            Interlocked.Exchange(ref stopping, 1);
            InvalidateHeartbeatQueue();
            CancelReconnectSchedule();
            ClearPendingActions();
            Task.Run(() => StopConnection());
        }

        public void Reconnect()
        {
            Interlocked.Exchange(ref stopping, 0);
            Interlocked.Exchange(ref terminalHeartbeatMode, 0);
            CancelReconnectSchedule();
            ClearPendingActions();
            StopConnection();
            EnqueueAction("reconnect", () => EnsureConnectionAsync(forceRefreshSettings: true));
        }

        public void Dispose()
        {
            Stop();
            serverSettingsClient?.Dispose();
        }

        private HubConnection BuildConnection(string url)
        {
            var hub = new HubConnectionBuilder()
                .AddNewtonsoftJsonProtocol()
                .WithUrl(AppendQueryString(url, BuildQueryString()), options =>
                {
                    options.Transports = HttpTransportType.WebSockets | HttpTransportType.LongPolling;
                })
                .Build();

            hub.On<SignalRMessage>("ReceiveMessage", OnReceiveMessage);
            hub.Closed += ex =>
            {
                OnClosed(hub, ex);
                return Task.CompletedTask;
            };

            return hub;
        }

        private string BuildQueryString()
        {
            var player = owner?.g_PlayerInfoManager?.g_PlayerInfo;
            if (player == null)
            {
                return string.Empty;
            }

            var query = new StringBuilder();
            if (!string.IsNullOrWhiteSpace(player.PIF_PlayerName))
            {
                AppendQueryValue(query, "playerName", player.PIF_PlayerName);
            }

            if (!string.IsNullOrWhiteSpace(player.PIF_GUID))
            {
                AppendQueryValue(query, "playerGuid", player.PIF_GUID);
            }

            return query.ToString();
        }

        private void OnClosed(HubConnection closedConnection, Exception ex)
        {
            if (Interlocked.CompareExchange(ref stopping, 0, 0) == 1)
            {
                return;
            }

            lock (syncRoot)
            {
                if (connection == closedConnection)
                {
                    connection = null;
                    currentUrl = null;
                }
            }

            if (ex != null)
            {
                Logger.WriteErrorLog($"SignalR client closed with error: {ex}", Logger.GetLogFileName());
            }
            Logger.WriteLog("SignalR client disconnected. Reconnecting...", Logger.GetLogFileName());
            ScheduleReconnect();
        }

        public void SendHeartbeat(HeartbeatPayload payload, Func<bool> shouldSend = null)
        {
            if (payload == null)
            {
                return;
            }

            if (Interlocked.CompareExchange(ref terminalHeartbeatMode, 0, 0) == 1)
            {
                return;
            }

            long generation = Interlocked.Read(ref heartbeatGeneration);
            EnqueueAction("heartbeat", () => SendHeartbeatInternal(payload, shouldSend, generation));
        }

        public void SendStoppedAndStop(HeartbeatPayload payload)
        {
            Interlocked.Exchange(ref terminalHeartbeatMode, 1);
            Interlocked.Exchange(ref stopping, 1);
            InvalidateHeartbeatQueue();
            CancelReconnectSchedule();
            ClearPendingActions();

            Task.Run(() =>
            {
                try
                {
                    SendTerminalHeartbeat(payload);
                }
                finally
                {
                    StopConnection();
                }
            });
        }

        private void ScheduleReconnect()
        {
            if (Interlocked.CompareExchange(ref stopping, 0, 0) == 1)
            {
                return;
            }

            if (Interlocked.Exchange(ref reconnectScheduled, 1) == 1)
            {
                return;
            }

            CancellationTokenSource delayCts = ReplaceReconnectCancellation();
            Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(ReconnectDelayMs, delayCts.Token);
                    if (delayCts.IsCancellationRequested)
                    {
                        return;
                    }

                    if (Interlocked.CompareExchange(ref stopping, 0, 0) == 0)
                    {
                        EnqueueAction("scheduled-reconnect", () => EnsureConnectionAsync(forceRefreshSettings: true));
                    }
                }
                catch (TaskCanceledException)
                {
                }
                finally
                {
                    Interlocked.Exchange(ref reconnectScheduled, 0);
                }
            });
        }

        private async Task SendHeartbeatInternal(HeartbeatPayload payload, Func<bool> shouldSend, long generation)
        {
            if (Interlocked.CompareExchange(ref stopping, 0, 0) == 1)
            {
                return;
            }

            if (!IsCurrentHeartbeatGeneration(generation))
            {
                return;
            }

            if (shouldSend != null && !shouldSend())
            {
                return;
            }

            HubConnection localConnection = await EnsureConnectedConnectionForHeartbeatAsync(generation);
            if (localConnection == null)
            {
                return;
            }

            try
            {
                if (!IsCurrentHeartbeatGeneration(generation))
                {
                    return;
                }

                if (shouldSend != null && !shouldSend())
                {
                    return;
                }

                await WaitWithTimeoutAsync(localConnection.InvokeAsync("ReportHeartbeat", payload), HeartbeatTimeoutMs);
            }
            catch (Exception ex)
            {
                Logger.WriteErrorLog($"SignalR heartbeat send failed: {ex}", Logger.GetLogFileName());
                ResetConnectionIfCurrent(localConnection);

                if (!IsCurrentHeartbeatGeneration(generation))
                {
                    return;
                }

                if (shouldSend != null && !shouldSend())
                {
                    return;
                }

                HubConnection retriedConnection = await EnsureConnectedConnectionForHeartbeatAsync(generation);
                if (retriedConnection == null)
                {
                    return;
                }

                try
                {
                    if (!IsCurrentHeartbeatGeneration(generation))
                    {
                        return;
                    }

                    if (shouldSend != null && !shouldSend())
                    {
                        return;
                    }

                    await WaitWithTimeoutAsync(retriedConnection.InvokeAsync("ReportHeartbeat", payload), HeartbeatTimeoutMs);
                }
                catch (Exception retryEx)
                {
                    Logger.WriteErrorLog($"SignalR heartbeat retry failed: {retryEx}", Logger.GetLogFileName());
                    ResetConnectionIfCurrent(retriedConnection);
                    ScheduleReconnect();
                }
            }
        }

        private async Task<HubConnection> EnsureConnectedConnectionForHeartbeatAsync(long generation)
        {
            if (!IsCurrentHeartbeatGeneration(generation))
            {
                return null;
            }

            HubConnection localConnection;
            lock (syncRoot)
            {
                localConnection = connection;
            }

            if (localConnection != null && localConnection.State == HubConnectionState.Connected)
            {
                return localConnection;
            }

            bool connected = await EnsureConnectionAsync(forceRefreshSettings: false);
            if (!connected || !IsCurrentHeartbeatGeneration(generation))
            {
                ScheduleReconnect();
                return null;
            }

            lock (syncRoot)
            {
                localConnection = connection;
            }

            if (localConnection == null || localConnection.State != HubConnectionState.Connected)
            {
                ScheduleReconnect();
                return null;
            }

            return localConnection;
        }

        private void SendTerminalHeartbeat(HeartbeatPayload payload)
        {
            if (payload == null)
            {
                return;
            }

            HubConnection localConnection;
            lock (syncRoot)
            {
                localConnection = connection;
            }

            if (localConnection == null || localConnection.State != HubConnectionState.Connected)
            {
                Logger.WriteLog("SignalR terminal heartbeat skipped: connection unavailable.", Logger.GetLogFileName());
                return;
            }

            try
            {
                WaitWithTimeoutAsync(localConnection.InvokeAsync("ReportHeartbeat", payload), HeartbeatTimeoutMs)
                    .GetAwaiter()
                    .GetResult();
            }
            catch (Exception ex)
            {
                Logger.WriteErrorLog($"SignalR terminal heartbeat send failed: {ex}", Logger.GetLogFileName());
            }
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
            if (Interlocked.CompareExchange(ref stopping, 0, 0) == 1)
            {
                return false;
            }

            string url = BuildUrl(forceRefreshSettings);
            if (string.IsNullOrWhiteSpace(url))
            {
                return false;
            }

            HubConnection local;
            HubConnection previous = null;
            lock (syncRoot)
            {
                if (connection != null)
                {
                    bool sameUrl = string.Equals(currentUrl, url, StringComparison.OrdinalIgnoreCase);
                    if (sameUrl && connection.State == HubConnectionState.Connected)
                    {
                        return true;
                    }

                    previous = connection;
                    connection = null;
                    currentUrl = null;
                }

                local = BuildConnection(url);
                connection = local;
                currentUrl = url;
            }

            if (previous != null)
            {
                try
                {
                    StopAndDisposeConnection(previous);
                }
                catch (Exception ex)
                {
                    Logger.WriteErrorLog($"SignalR client stop failed: {ex}", Logger.GetLogFileName());
                }
            }

            if (local.State == HubConnectionState.Connected)
            {
                return true;
            }

            try
            {
                await WaitWithTimeoutAsync(local.StartAsync(), ConnectTimeoutMs);
                Logger.WriteLog($"SignalR client connected: {url}", Logger.GetLogFileName());
                return true;
            }
            catch (Exception ex)
            {
                Logger.WriteErrorLog($"SignalR client connect failed: {ex}", Logger.GetLogFileName());
                ResetConnectionIfCurrent(local);
                ScheduleReconnect();
                return false;
            }
        }

        private CancellationTokenSource ReplaceReconnectCancellation()
        {
            CancellationTokenSource next = new CancellationTokenSource();
            CancellationTokenSource old;
            lock (syncRoot)
            {
                old = reconnectDelayCts;
                reconnectDelayCts = next;
            }

            if (old != null)
            {
                try
                {
                    old.Cancel();
                    old.Dispose();
                }
                catch
                {
                }
            }

            return next;
        }

        private void CancelReconnectSchedule()
        {
            Interlocked.Exchange(ref reconnectScheduled, 0);
            ReplaceReconnectCancellation();
        }

        private void StopConnection()
        {
            HubConnection local;
            lock (syncRoot)
            {
                local = connection;
                connection = null;
                currentUrl = null;
            }

            if (local == null)
            {
                return;
            }

            try
            {
                StopAndDisposeConnection(local);
            }
            catch (Exception ex)
            {
                Logger.WriteErrorLog($"SignalR client stop failed: {ex}", Logger.GetLogFileName());
            }
        }

        private void ResetConnectionIfCurrent(HubConnection local)
        {
            bool shouldStop = false;
            lock (syncRoot)
            {
                if (connection == local)
                {
                    connection = null;
                    currentUrl = null;
                    shouldStop = true;
                }
            }

            if (!shouldStop)
            {
                return;
            }

            try
            {
                StopAndDisposeConnection(local);
            }
            catch (Exception ex)
            {
                Logger.WriteErrorLog($"SignalR client reset failed: {ex}", Logger.GetLogFileName());
            }
        }

        private async Task WaitWithTimeoutAsync(Task task, int timeoutMs)
        {
            Task completed = await Task.WhenAny(task, Task.Delay(timeoutMs));
            if (completed != task)
            {
                throw new TimeoutException($"SignalR operation timed out after {timeoutMs}ms.");
            }

            await task;
        }

        private void EnqueueAction(string label, Func<Task> action)
        {
            bool shouldStartProcessor = false;
            string droppedLabel = null;

            lock (actionQueueLock)
            {
                if (actionQueue.Count >= MaxSignalRActionQueue)
                {
                    droppedLabel = actionQueue.Dequeue().Label;
                }

                actionQueue.Enqueue(new SignalRAction(label, action));
                if (queuedActionProcessor == 0)
                {
                    queuedActionProcessor = 1;
                    shouldStartProcessor = true;
                }
            }

            if (!string.IsNullOrWhiteSpace(droppedLabel))
            {
                Logger.WriteLog($"SignalR action queue full. Dropped oldest action={droppedLabel}", Logger.GetLogFileName());
            }

            if (shouldStartProcessor)
            {
                Task.Run(ProcessActionQueueAsync);
            }
        }

        private void ClearPendingActions()
        {
            lock (actionQueueLock)
            {
                actionQueue.Clear();
            }
        }

        private void InvalidateHeartbeatQueue()
        {
            Interlocked.Increment(ref heartbeatGeneration);
        }

        private bool IsCurrentHeartbeatGeneration(long generation)
        {
            return generation == Interlocked.Read(ref heartbeatGeneration)
                   && Interlocked.CompareExchange(ref terminalHeartbeatMode, 0, 0) == 0;
        }

        private async Task ProcessActionQueueAsync()
        {
            while (true)
            {
                SignalRAction next;
                lock (actionQueueLock)
                {
                    if (actionQueue.Count == 0)
                    {
                        queuedActionProcessor = 0;
                        return;
                    }

                    next = actionQueue.Dequeue();
                }

                try
                {
                    await next.ExecuteAsync();
                }
                catch (Exception ex)
                {
                    Logger.WriteErrorLog($"SignalR action failed [{next.Label}]: {ex}", Logger.GetLogFileName());
                }
            }
        }

        private sealed class SignalRAction
        {
            public string Label { get; private set; }
            private readonly Func<Task> action;

            public SignalRAction(string label, Func<Task> action)
            {
                Label = label ?? "unknown";
                this.action = action;
            }

            public Task ExecuteAsync()
            {
                return action == null ? Task.CompletedTask : action();
            }
        }

        private static void AppendQueryValue(StringBuilder builder, string key, string value)
        {
            if (builder == null || string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            if (builder.Length > 0)
            {
                builder.Append("&");
            }

            builder.Append(Uri.EscapeDataString(key));
            builder.Append("=");
            builder.Append(Uri.EscapeDataString(value));
        }

        private static string AppendQueryString(string url, string queryString)
        {
            if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(queryString))
            {
                return url;
            }

            return url.Contains("?")
                ? $"{url}&{queryString}"
                : $"{url}?{queryString}";
        }

        private static void StopAndDisposeConnection(HubConnection hubConnection)
        {
            if (hubConnection == null)
            {
                return;
            }

            hubConnection.StopAsync().GetAwaiter().GetResult();
            hubConnection.DisposeAsync().AsTask().GetAwaiter().GetResult();
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
