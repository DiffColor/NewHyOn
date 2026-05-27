using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;

namespace TurtleTools
{
    internal static class SignalRClientTools
    {
        private const int DefaultPort = 5000;
        private const string DefaultHost = "127.0.0.1";
        internal const string DefaultHubPath = "/Data";
        private const int ReconnectDelayMs = 15000;
        private const int ConnectTimeoutMs = 15000;
        private const int InvokeTimeoutMs = 15000;
        private const int DisposeTimeoutMs = 5000;
        private static readonly object SyncRoot = new object();
        private static readonly SemaphoreSlim ConnectGate = new SemaphoreSlim(1, 1);
        private static HubConnection _connection;
        private static string _connectionUrl;
        private static string _lastConnectionError = string.Empty;
        private static int _reconnecting;
        private static int _stopping;

        public static event EventHandler<SignalRHeartbeatEventArgs> HeartbeatReceived;

        public static bool IsConnected()
        {
            lock (SyncRoot)
            {
                return _connection != null && _connection.State == HubConnectionState.Connected;
            }
        }

        public static bool IsConnecting()
        {
            lock (SyncRoot)
            {
                return _connection != null
                    && (_connection.State == HubConnectionState.Connecting
                        || _connection.State == HubConnectionState.Reconnecting);
            }
        }

        public static void StartSignalRClient()
        {
            Interlocked.Exchange(ref _stopping, 0);
            Task.Run(async () =>
            {
                if (!await EnsureConnectedCoreAsync())
                {
                    ScheduleReconnect();
                }
            });
        }

        public static async Task<bool> EnsureConnectedAsync()
        {
            Interlocked.Exchange(ref _stopping, 0);
            return await EnsureConnectedCoreAsync();
        }

        private static async Task<bool> EnsureConnectedCoreAsync()
        {
            if (Interlocked.CompareExchange(ref _stopping, 0, 0) == 1)
            {
                return false;
            }

            await ConnectGate.WaitAsync();

            try
            {
                if (Interlocked.CompareExchange(ref _stopping, 0, 0) == 1)
                {
                    return false;
                }

                string url = BuildUrl();
                if (string.IsNullOrWhiteSpace(url))
                {
                    _lastConnectionError = "host not configured";
                    Logger.WriteLog("SignalR client skipped: host not configured.", Logger.GetLogFileName());
                    return false;
                }

                HubConnection oldConnection = null;
                HubConnection local;
                lock (SyncRoot)
                {
                    if (_connection != null
                        && _connection.State == HubConnectionState.Connected
                        && string.Equals(_connectionUrl, url, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }

                    oldConnection = _connection;
                    local = BuildConnection(url);
                    _connection = local;
                    _connectionUrl = url;
                }

                DisposeConnectionQuietly(oldConnection);

                try
                {
                    using (var cts = new CancellationTokenSource(ConnectTimeoutMs))
                    {
                        await local.StartAsync(cts.Token);
                    }

                    if (Interlocked.CompareExchange(ref _stopping, 0, 0) == 1)
                    {
                        DisposeIfCurrent(local);
                        return false;
                    }

                    if (!await RegisterManagerGroup(local))
                    {
                        DisposeIfCurrent(local);
                        return false;
                    }

                    _lastConnectionError = string.Empty;
                    Logger.WriteLog($"SignalR client connected: {url}", Logger.GetLogFileName());
                    return true;
                }
                catch (OperationCanceledException ex)
                {
                    _lastConnectionError = $"connect timeout after {ConnectTimeoutMs}ms";
                    Logger.WriteErrorLog($"SignalR client connect timed out: url={url}, error={ex}", Logger.GetLogFileName());
                    DisposeIfCurrent(local);
                    return false;
                }
                catch (Exception ex)
                {
                    _lastConnectionError = ex.Message;
                    Logger.WriteErrorLog($"SignalR client connect failed: url={url}, error={ex}", Logger.GetLogFileName());

                    DisposeIfCurrent(local);
                    return false;
                }
            }
            finally
            {
                ConnectGate.Release();
            }
        }

        public static string GetConnectionStatus()
        {
            lock (SyncRoot)
            {
                string state = _connection == null ? "None" : _connection.State.ToString();
                return $"url={_connectionUrl ?? BuildUrl()}, state={state}, lastError={_lastConnectionError}";
            }
        }

        public static void StopSignalRClient()
        {
            Interlocked.Exchange(ref _stopping, 1);
            HubConnection local;

            lock (SyncRoot)
            {
                local = _connection;
                _connection = null;
                _connectionUrl = null;
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

        public static bool TrySendCommandToClient(string clientId, SignalRCommandEnvelope envelope)
        {
            if (string.IsNullOrWhiteSpace(clientId) || envelope == null)
            {
                return false;
            }

            HubConnection localConnection;
            lock (SyncRoot)
            {
                localConnection = _connection;
            }

            if (localConnection == null || localConnection.State != HubConnectionState.Connected)
            {
                ScheduleReconnect();
                return false;
            }

            try
            {
                var sendTask = localConnection.InvokeAsync<bool>("SendCommandToClient", clientId, envelope);
                var completedTask = Task.WhenAny(sendTask, Task.Delay(InvokeTimeoutMs)).GetAwaiter().GetResult();
                if (!ReferenceEquals(completedTask, sendTask))
                {
                    _lastConnectionError = $"command send timeout after {InvokeTimeoutMs}ms";
                    Logger.WriteErrorLog($"SignalR command send timed out: clientId={clientId}", Logger.GetLogFileName());
                    ObserveFault(sendTask);
                    DisposeIfCurrent(localConnection);
                    ScheduleReconnect();
                    return false;
                }

                return sendTask.GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                Logger.WriteErrorLog($"SignalR command send failed: {ex}", Logger.GetLogFileName());
                DisposeIfCurrent(localConnection);
                ScheduleReconnect();
                return false;
            }
        }

        private static HubConnection BuildConnection(string url)
        {
            var hub = new HubConnectionBuilder()
                .AddNewtonsoftJsonProtocol()
                .WithUrl(url, options =>
                {
                    options.Transports = HttpTransportType.WebSockets | HttpTransportType.LongPolling;
                })
                .Build();

            hub.On<SignalRHeartbeatPayload>("ReceiveHeartbeat", OnReceiveHeartbeat);
            hub.Closed += ex =>
            {
                OnClosed(hub, ex);
                return Task.CompletedTask;
            };

            return hub;
        }

        private static async Task<bool> RegisterManagerGroup(HubConnection connection)
        {
            lock (SyncRoot)
            {
                if (!ReferenceEquals(_connection, connection)
                    || connection == null
                    || connection.State != HubConnectionState.Connected)
                {
                    _lastConnectionError = "connection changed before manager registration";
                    return false;
                }
            }

            try
            {
                var registerTask = connection.InvokeAsync("RegisterManager");
                var completedTask = await Task.WhenAny(registerTask, Task.Delay(InvokeTimeoutMs));
                if (!ReferenceEquals(completedTask, registerTask))
                {
                    _lastConnectionError = $"manager registration timeout after {InvokeTimeoutMs}ms";
                    Logger.WriteErrorLog("SignalR manager registration timed out.", Logger.GetLogFileName());
                    ObserveFault(registerTask);
                    return false;
                }

                await registerTask;
                return true;
            }
            catch (Exception ex)
            {
                _lastConnectionError = ex.Message;
                Logger.WriteErrorLog($"SignalR manager registration failed: {ex}", Logger.GetLogFileName());
                return false;
            }
        }

        private static void OnClosed(HubConnection closedConnection, Exception ex)
        {
            if (Interlocked.CompareExchange(ref _stopping, 0, 0) == 1)
            {
                return;
            }

            lock (SyncRoot)
            {
                if (!ReferenceEquals(_connection, closedConnection))
                {
                    return;
                }

                _connection = null;
                _connectionUrl = null;
            }

            if (ex != null)
            {
                _lastConnectionError = ex.Message;
                Logger.WriteErrorLog($"SignalR client closed with error: {ex}", Logger.GetLogFileName());
            }
            Logger.WriteLog("SignalR client disconnected. Reconnecting...", Logger.GetLogFileName());
            ScheduleReconnect();
        }

        private static void ScheduleReconnect()
        {
            if (Interlocked.Exchange(ref _reconnecting, 1) == 1)
            {
                return;
            }

            Task.Run(async () =>
            {
                try
                {
                    while (Interlocked.CompareExchange(ref _stopping, 0, 0) == 0)
                    {
                        await Task.Delay(ReconnectDelayMs);
                        if (Interlocked.CompareExchange(ref _stopping, 0, 0) == 1)
                        {
                            return;
                        }

                        if (await EnsureConnectedCoreAsync())
                        {
                            Logger.WriteLog("SignalR client reconnected.", Logger.GetLogFileName());
                            return;
                        }

                        Logger.WriteErrorLog($"SignalR client reconnect failed: {GetConnectionStatus()}", Logger.GetLogFileName());
                    }
                }
                finally
                {
                    Interlocked.Exchange(ref _reconnecting, 0);
                }
            });
        }

        private static void OnReceiveHeartbeat(SignalRHeartbeatPayload payload)
        {
            if (payload == null)
            {
                return;
            }

            HeartbeatReceived?.Invoke(null, new SignalRHeartbeatEventArgs(payload));
        }

        private static string BuildUrl()
        {
            string host = ResolveHost();
            if (string.IsNullOrWhiteSpace(host))
            {
                host = DefaultHost;
            }

            int port = ResolvePort();
            string hubPath = ResolveHubPath();
            if (!hubPath.StartsWith("/"))
            {
                hubPath = "/" + hubPath;
            }

            return $"http://{host}:{port}{hubPath}";
        }

        private static string ResolveHost()
        {
            var settings = LocalSettingsStore.GetConnectionSettings();
            return string.IsNullOrWhiteSpace(settings?.SignalRHost) ? DefaultHost : settings.SignalRHost.Trim();
        }

        private static int ResolvePort()
        {
            var settings = LocalSettingsStore.GetConnectionSettings();
            if (settings?.SignalRPort > 0 && settings.SignalRPort <= 65535)
            {
                return settings.SignalRPort;
            }

            return DefaultPort;
        }

        private static string ResolveHubPath()
        {
            var settings = LocalSettingsStore.GetConnectionSettings();
            if (string.IsNullOrWhiteSpace(settings?.SignalRHubPath))
            {
                return DefaultHubPath;
            }

            return settings.SignalRHubPath.Trim();
        }

        private static void StopAndDisposeConnection(HubConnection connection)
        {
            if (connection == null)
            {
                return;
            }

            WaitForTask(connection.StopAsync(), DisposeTimeoutMs, "SignalR client stop");
            WaitForTask(connection.DisposeAsync().AsTask(), DisposeTimeoutMs, "SignalR client dispose");
        }

        private static void WaitForTask(Task task, int timeoutMs, string operationName)
        {
            var completedTask = Task.WhenAny(task, Task.Delay(timeoutMs)).GetAwaiter().GetResult();
            if (!ReferenceEquals(completedTask, task))
            {
                ObserveFault(task);
                throw new TimeoutException($"{operationName} timed out after {timeoutMs}ms");
            }

            task.GetAwaiter().GetResult();
        }

        private static void ObserveFault(Task task)
        {
            task.ContinueWith(t =>
            {
                var ignored = t.Exception;
            }, TaskContinuationOptions.OnlyOnFaulted);
        }

        private static void DisposeConnectionQuietly(HubConnection connection)
        {
            if (connection == null)
            {
                return;
            }

            try
            {
                StopAndDisposeConnection(connection);
            }
            catch
            {
            }
        }

        private static void DisposeIfCurrent(HubConnection connection)
        {
            if (connection == null)
            {
                return;
            }

            bool shouldDispose = false;
            lock (SyncRoot)
            {
                if (ReferenceEquals(_connection, connection))
                {
                    _connection = null;
                    _connectionUrl = null;
                    shouldDispose = true;
                }
            }

            if (shouldDispose)
            {
                DisposeConnectionQuietly(connection);
            }
        }
    }
}
