using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNet.SignalR;
using Microsoft.AspNet.SignalR.Hubs;
using Microsoft.Owin.Cors;
using Microsoft.Owin.Hosting;
using Owin;

namespace TurtleTools
{
    public static class SignalRServerTools
    {
        internal const string DefaultHubPath = "/AndoW";
        private const int DefaultPort = 5000;
        private static readonly object SyncRoot = new object();
        private static IDisposable _webApp;
        public static event EventHandler<SignalRHeartbeatEventArgs> HeartbeatReceived;

        public static bool IsRunning()
        {
            lock (SyncRoot)
            {
                return _webApp != null;
            }
        }

        public static void StartSignalRServer()
        {
            lock (SyncRoot)
            {
                if (_webApp != null)
                {
                    return;
                }

                int port = ResolvePort();
                string hubPath = ResolveHubPath();
                string url = $"http://+:{port}";

                try
                {
                    _webApp = WebApp.Start(url, app => SignalRServerStartup.Configuration(app, hubPath));
                    Logger.WriteLog($"SignalR server started: {url}{hubPath}", Logger.GetLogFileName());
                }
                catch (Exception ex)
                {
                    Logger.WriteErrorLog($"SignalR server start failed: {ex}", Logger.GetLogFileName());
                    _webApp?.Dispose();
                    _webApp = null;
                }
            }
        }

        public static void StopSignalRServer()
        {
            IDisposable host = null;
            lock (SyncRoot)
            {
                host = _webApp;
                _webApp = null;
            }

            if (host == null)
            {
                return;
            }

            try
            {
                host.Dispose();
                Logger.WriteLog("SignalR server stopped.", Logger.GetLogFileName());
            }
            catch (Exception ex)
            {
                Logger.WriteErrorLog($"SignalR server stop failed: {ex}", Logger.GetLogFileName());
            }
        }

        internal static string ResolveHubPath()
        {
            string value = ConfigurationManager.AppSettings["SignalRHubPath"];
            if (string.IsNullOrWhiteSpace(value))
            {
                return DefaultHubPath;
            }

            if (!value.StartsWith("/"))
            {
                value = "/" + value;
            }

            return value;
        }

        internal static int GetSignalRPort()
        {
            return ResolvePort();
        }

        internal static void RaiseHeartbeatReceived(SignalRHeartbeatPayload payload)
        {
            if (payload == null || string.IsNullOrWhiteSpace(payload.ClientId))
            {
                return;
            }

            HeartbeatReceived?.Invoke(null, new SignalRHeartbeatEventArgs(payload));
        }

        public static bool TrySendCommandToClient(string clientId, SignalRCommandEnvelope envelope)
        {
            if (string.IsNullOrWhiteSpace(clientId) || envelope == null)
            {
                return false;
            }

            var connections = SignalRMsgHub.GetConnectionsByClientId(clientId);
            if (connections == null || connections.Count == 0)
            {
                return false;
            }

            var hubContext = GlobalHost.ConnectionManager.GetHubContext<SignalRMsgHub>();
            foreach (string connectionId in connections)
            {
                try
                {
                    //hubContext.Clients.Client(connectionId).ReceiveMessage(new SignalRMessage
                    //{
                    //    From = "Server",
                    //    To = clientId,
                    //    Command = envelope.Command ?? string.Empty,
                    //    DataType = "CommandQueue",
                    //    Data = envelope
                    //});

                    hubContext.Clients.Client(connectionId).ReceiveMessage(new SignalRMessage
                    {
                        From = "Server",
                        To = clientId,
                        Command = envelope.Command ?? string.Empty,
                        DataType = "CommandQueue",
                        Data = envelope
                    });
                }
                catch (Exception ex)
                {
                    Logger.WriteErrorLog($"SignalR command send failed: {connectionId}, ex={ex}", Logger.GetLogFileName());
                }
            }

            return true;
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
    }

    public static class SignalRServerStartup
    {
        public static void Configuration(IAppBuilder app, string hubPath)
        {
            app.Map(hubPath, map =>
            {
                map.UseCors(CorsOptions.AllowAll);
                GlobalHost.Configuration.MaxIncomingWebSocketMessageSize = 10 * 1024 * 1024;
                var longPollingProp = GlobalHost.Configuration.GetType().GetProperty("MaxIncomingLongPollingMessageSize");
                if (longPollingProp != null && longPollingProp.CanWrite)
                {
                    longPollingProp.SetValue(GlobalHost.Configuration, 10 * 1024 * 1024, null);
                }
                var config = new HubConfiguration
                {
                    EnableDetailedErrors = true
                };

                map.RunSignalR(config);
            });
        }
    }

    [HubName("SignalRMsgHub")]
    public class SignalRMsgHub : Hub
    {
        private static readonly List<string> ConnectedClients = new List<string>();
        private static readonly Dictionary<string, ClientIdentity> ConnectedClientInfos = new Dictionary<string, ClientIdentity>();
        private static readonly Dictionary<string, string> ConnectionClientIds = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, HashSet<string>> ClientIdConnections = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, DateTime> ConnectionLastSeen = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
        private static readonly TimeSpan StaleConnectionThreshold = TimeSpan.FromSeconds(30);
        private static readonly Timer CleanupTimer = new Timer(CleanupStaleConnections, null, StaleConnectionThreshold, StaleConnectionThreshold);
        private static readonly object ClientsLock = new object();

        public override Task OnConnected()
        {
            string clientId = Context.ConnectionId;
            ClientIdentity identity = BuildClientIdentity(Context);
            string resolvedClientId = ResolveClientId(null, identity, clientId);
            lock (ClientsLock)
            {
                if (!ConnectedClients.Contains(clientId))
                {
                    ConnectedClients.Add(clientId);
                }

                ConnectionClientIds.Remove(clientId);
                CacheClientId(clientId, resolvedClientId);

                if (identity != null)
                {
                    ConnectedClientInfos[clientId] = identity;
                }
            }

            TouchConnection(clientId);
            Logger.WriteLog($"SignalR client connected: {FormatClientIdentity(clientId, identity)}", Logger.GetLogFileName());
            var onlinePayload = CreateOnlinePayload(clientId, identity);
            SignalRServerTools.RaiseHeartbeatReceived(onlinePayload);
            return base.OnConnected();
        }

        public override async Task OnDisconnected(bool stopCalled)
        {
            string clientId = Context.ConnectionId;
            string cachedClientId = GetCachedClientId(clientId);
            ClientIdentity identity = null;
            lock (ClientsLock)
            {
                ConnectedClients.Remove(clientId);
                if (ConnectedClientInfos.TryGetValue(clientId, out ClientIdentity stored))
                {
                    identity = stored;
                    ConnectedClientInfos.Remove(clientId);
                }
                ConnectionClientIds.Remove(clientId);
                RemoveClientConnection(cachedClientId, clientId);
                ConnectionLastSeen.Remove(clientId);
            }

            Logger.WriteLog($"SignalR client disconnected: {FormatClientIdentity(clientId, identity)}", Logger.GetLogFileName());

            var message = new SignalRMessage()
            {
                From = "Server",
                To = "Others",
                Command = "Message",
                DataType = "StateMessage",
                Data = new StateMessage()
                {
                    Who = clientId,
                    State = "Disconnected",
                    Description = "Client disconnected"
                }
            };

            await ((Task)Clients.All.ReceiveMessage(message));
            var offlinePayload = CreateOfflinePayload(clientId, identity);
            SignalRServerTools.RaiseHeartbeatReceived(offlinePayload);
            await base.OnDisconnected(stopCalled);
        }

        public async Task JoinGroup(string groupName)
        {
            if (string.IsNullOrWhiteSpace(groupName))
            {
                return;
            }

            await Groups.Add(Context.ConnectionId, groupName);
            Logger.WriteLog($"SignalR join group: {Context.ConnectionId} -> {groupName}", Logger.GetLogFileName());
        }

        public async Task LeaveGroup(string groupName)
        {
            if (string.IsNullOrWhiteSpace(groupName))
            {
                return;
            }

            await Groups.Remove(Context.ConnectionId, groupName);
            Logger.WriteLog($"SignalR leave group: {Context.ConnectionId} -> {groupName}", Logger.GetLogFileName());
        }

        public Task ReportHeartbeat(SignalRHeartbeatPayload payload)
        {
            TouchConnection(Context.ConnectionId);
            var resolved = NormalizeHeartbeatPayload(payload);
            SignalRServerTools.RaiseHeartbeatReceived(resolved);
            return Task.CompletedTask;
        }

        public Task<object> Heartbeat()
        {
            return Task.FromResult<object>(new
            {
                Status = "OK",
                Timestamp = DateTime.UtcNow,
                ConnectionId = Context.ConnectionId,
                Message = "Heartbeat"
            });
        }

        public Task<object> HealthCheck()
        {
            Logger.WriteLog($"SignalR health check: {Context.ConnectionId}", Logger.GetLogFileName());
            return Task.FromResult<object>(new
            {
                Status = "Healthy",
                Timestamp = DateTime.UtcNow,
                ConnectedClients = GetConnectedClientCount(),
                ServerUptime = DateTime.UtcNow - Process.GetCurrentProcess().StartTime.ToUniversalTime(),
                Message = "Server is running"
            });
        }

        public Task<object> GetConnectionStats()
        {
            Logger.WriteLog($"SignalR stats: {Context.ConnectionId}", Logger.GetLogFileName());
            return Task.FromResult<object>(new
            {
                ConnectionId = Context.ConnectionId,
                ConnectedClients = GetConnectedClientCount(),
                ServerStartTime = Process.GetCurrentProcess().StartTime.ToUniversalTime(),
                CurrentTime = DateTime.UtcNow,
                Uptime = DateTime.UtcNow - Process.GetCurrentProcess().StartTime.ToUniversalTime()
            });
        }

        public async Task<bool> SendMessageByClientProxy(IClientProxy client, object message)
        {
            try
            {
                Logger.WriteLog("SignalR send message by proxy requested.", Logger.GetLogFileName());
                await client.Invoke("ReceiveMessage", message);
                Logger.WriteLog("SignalR send message by proxy completed.", Logger.GetLogFileName());
                return true;
            }
            catch (Exception ex)
            {
                Logger.WriteErrorLog($"SignalR send message by proxy failed: {ex}", Logger.GetLogFileName());
            }

            return false;
        }

        public async Task<bool> SendMessagesByClientProxy(IClientProxy client, object[] messages)
        {
            try
            {
                Logger.WriteLog("SignalR send messages by proxy requested.", Logger.GetLogFileName());

                foreach (var message in messages)
                {
                    await client.Invoke("ReceiveMessage", message);
                }

                Logger.WriteLog("SignalR send messages by proxy completed.", Logger.GetLogFileName());
                return true;
            }
            catch (Exception ex)
            {
                Logger.WriteErrorLog($"SignalR send messages by proxy failed: {ex}", Logger.GetLogFileName());
            }

            return false;
        }

        public async Task<bool> SendMessage(string to_connection_id, object message)
        {
            try
            {
                Logger.WriteLog($"SignalR send message: {to_connection_id}", Logger.GetLogFileName());

                var client = Clients.Client(to_connection_id);
                if (client == null)
                {
                    Logger.WriteErrorLog($"SignalR client not found: {to_connection_id}", Logger.GetLogFileName());
                    return false;
                }

                await client.Invoke("ReceiveMessage", message);

                Logger.WriteLog($"SignalR send message completed: {to_connection_id}", Logger.GetLogFileName());
                return true;
            }
            catch (Exception ex)
            {
                Logger.WriteErrorLog($"SignalR send message failed: {ex}", Logger.GetLogFileName());
            }

            return false;
        }

        public async Task<bool> SendMessages(string to_connection_id, object[] messages)
        {
            try
            {
                Logger.WriteLog($"SignalR send messages: {to_connection_id}", Logger.GetLogFileName());

                var client = Clients.Client(to_connection_id);
                if (client == null)
                {
                    Logger.WriteErrorLog($"SignalR client not found: {to_connection_id}", Logger.GetLogFileName());
                    return false;
                }

                foreach (var message in messages)
                {
                    await client.Invoke("ReceiveMessage", message);
                }

                Logger.WriteLog($"SignalR send messages completed: {to_connection_id}", Logger.GetLogFileName());
                return true;
            }
            catch (Exception ex)
            {
                Logger.WriteErrorLog($"SignalR send messages failed: {ex}", Logger.GetLogFileName());
            }

            return false;
        }

        public async Task<bool> SendMessageToGroup(string groupName, object message)
        {
            try
            {
                Logger.WriteLog($"SignalR group message requested: {groupName}", Logger.GetLogFileName());

                await ((Task)Clients.OthersInGroup(groupName).ReceiveMessage(message));

                Logger.WriteLog($"SignalR group message completed: {groupName}", Logger.GetLogFileName());
                return true;
            }
            catch (Exception ex)
            {
                Logger.WriteErrorLog($"SignalR group message failed: {ex}", Logger.GetLogFileName());
            }

            return true;
        }

        public async Task<bool> SendMessagesToGroup(string groupName, object[] messages)
        {
            try
            {
                Logger.WriteLog($"SignalR group messages requested: {groupName}", Logger.GetLogFileName());

                foreach (var message in messages)
                {
                    await ((Task)Clients.OthersInGroup(groupName).ReceiveMessage(message));
                }

                Logger.WriteLog($"SignalR group messages completed: {groupName}", Logger.GetLogFileName());
                return true;
            }
            catch (Exception ex)
            {
                Logger.WriteErrorLog($"SignalR group messages failed: {ex}", Logger.GetLogFileName());
            }

            return true;
        }

        public async Task<bool> SendMessageToAll(object message)
        {
            Logger.WriteLog($"SignalR broadcast requested: {Context.ConnectionId}", Logger.GetLogFileName());

            try
            {
                await ((Task)Clients.AllExcept(Context.ConnectionId).ReceiveMessage(message));

                Logger.WriteLog($"SignalR broadcast completed: {Context.ConnectionId}", Logger.GetLogFileName());
                return true;
            }
            catch (Exception ex)
            {
                Logger.WriteErrorLog($"SignalR broadcast failed: {ex}", Logger.GetLogFileName());
            }

            return false;
        }

        public async Task<bool> SendMessagesToAll(object[] messages)
        {
            Logger.WriteLog($"SignalR bulk broadcast requested: {Context.ConnectionId}, count={messages.Length}", Logger.GetLogFileName());

            try
            {
                foreach (var message in messages)
                {
                    await ((Task)Clients.AllExcept(Context.ConnectionId).ReceiveMessage(message));
                }

                Logger.WriteLog($"SignalR bulk broadcast completed: {messages.Length}", Logger.GetLogFileName());
                return true;
            }
            catch (Exception ex)
            {
                Logger.WriteErrorLog($"SignalR bulk broadcast failed: {ex}", Logger.GetLogFileName());
            }

            return false;
        }

        private static int GetConnectedClientCount()
        {
            lock (ClientsLock)
            {
                return ConnectedClients.Count;
            }
        }

        private static ClientIdentity BuildClientIdentity(HubCallerContext context)
        {
            string playerName = GetQueryValue(context, "playerName");
            string playerGuid = GetQueryValue(context, "playerGuid");
            if (string.IsNullOrWhiteSpace(playerName) && string.IsNullOrWhiteSpace(playerGuid))
            {
                return null;
            }

            return new ClientIdentity(playerName, playerGuid);
        }

        private static string GetQueryValue(HubCallerContext context, string key)
        {
            if (context == null || context.QueryString == null)
            {
                return string.Empty;
            }

            string value = context.QueryString[key];
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }

        private static string FormatClientIdentity(string connectionId, ClientIdentity identity)
        {
            if (identity == null)
            {
                return connectionId;
            }

            string playerName = NormalizeIdentityValue(identity.PlayerName);
            string playerGuid = NormalizeIdentityValue(identity.PlayerGuid);
            return $"{connectionId}, player={playerName}, guid={playerGuid}";
        }

        private static string NormalizeIdentityValue(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "-" : value;
        }

        private SignalRHeartbeatPayload NormalizeHeartbeatPayload(SignalRHeartbeatPayload payload)
        {
            ClientIdentity identity = GetIdentityByConnectionId(Context.ConnectionId);
            string clientId = ResolveClientId(payload?.ClientId, identity, Context.ConnectionId);
            if (string.IsNullOrWhiteSpace(clientId))
            {
                return null;
            }

            CacheClientId(Context.ConnectionId, clientId);
            SignalRHeartbeatPayload normalized = payload ?? new SignalRHeartbeatPayload();
            normalized.ClientId = clientId;
            if (normalized.Timestamp == default(DateTime))
            {
                normalized.Timestamp = DateTime.Now;
            }

            return normalized;
        }

        private static ClientIdentity GetIdentityByConnectionId(string connectionId)
        {
            if (string.IsNullOrWhiteSpace(connectionId))
            {
                return null;
            }

            lock (ClientsLock)
            {
                ConnectedClientInfos.TryGetValue(connectionId, out ClientIdentity identity);
                return identity;
            }
        }

        private static void CacheClientId(string connectionId, string clientId)
        {
            if (string.IsNullOrWhiteSpace(connectionId) || string.IsNullOrWhiteSpace(clientId))
            {
                return;
            }

            lock (ClientsLock)
            {
                ConnectionClientIds[connectionId] = clientId;
                AddClientConnection(clientId, connectionId);
            }
        }

        private static string GetCachedClientId(string connectionId)
        {
            if (string.IsNullOrWhiteSpace(connectionId))
            {
                return string.Empty;
            }

            lock (ClientsLock)
            {
                if (ConnectionClientIds.TryGetValue(connectionId, out string clientId))
                {
                    return clientId;
                }
            }

            return string.Empty;
        }

        private static void AddClientConnection(string clientId, string connectionId)
        {
            if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(connectionId))
            {
                return;
            }

            if (!ClientIdConnections.TryGetValue(clientId, out HashSet<string> connections))
            {
                connections = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                ClientIdConnections[clientId] = connections;
            }

            connections.Add(connectionId);
        }

        private static void TouchConnection(string connectionId)
        {
            if (string.IsNullOrWhiteSpace(connectionId))
            {
                return;
            }

            lock (ClientsLock)
            {
                ConnectionLastSeen[connectionId] = DateTime.Now;
            }
        }

        private static void CleanupStaleConnections(object state)
        {
            List<string> staleConnections = null;
            lock (ClientsLock)
            {
                if (ConnectionLastSeen.Count == 0)
                {
                    return;
                }

                DateTime now = DateTime.Now;
                foreach (var kv in ConnectionLastSeen)
                {
                    if (now - kv.Value >= StaleConnectionThreshold)
                    {
                        if (staleConnections == null)
                        {
                            staleConnections = new List<string>();
                        }
                        staleConnections.Add(kv.Key);
                    }
                }

                if (staleConnections == null || staleConnections.Count == 0)
                {
                    return;
                }

                foreach (string connectionId in staleConnections)
                {
                    ConnectionLastSeen.Remove(connectionId);
                    ConnectedClients.Remove(connectionId);
                    ConnectedClientInfos.Remove(connectionId);
                    string cachedClientId = string.Empty;
                    if (ConnectionClientIds.TryGetValue(connectionId, out string clientId))
                    {
                        cachedClientId = clientId;
                        ConnectionClientIds.Remove(connectionId);
                    }
                    RemoveClientConnection(cachedClientId, connectionId);
                }
            }
        }

        private static void RemoveClientConnection(string clientId, string connectionId)
        {
            if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(connectionId))
            {
                return;
            }

            if (!ClientIdConnections.TryGetValue(clientId, out HashSet<string> connections))
            {
                return;
            }

            connections.Remove(connectionId);
            if (connections.Count == 0)
            {
                ClientIdConnections.Remove(clientId);
            }
        }

        internal static List<string> GetConnectionsByClientId(string clientId)
        {
            if (string.IsNullOrWhiteSpace(clientId))
            {
                return new List<string>();
            }

            lock (ClientsLock)
            {
                if (ClientIdConnections.TryGetValue(clientId, out HashSet<string> connections))
                {
                    return connections.ToList();
                }

                var recovered = ConnectionClientIds
                    .Where(pair => string.Equals(pair.Value, clientId, StringComparison.OrdinalIgnoreCase))
                    .Select(pair => pair.Key)
                    .Where(id => !string.IsNullOrWhiteSpace(id))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
                if (recovered.Count > 0)
                {
                    ClientIdConnections[clientId] = new HashSet<string>(recovered, StringComparer.OrdinalIgnoreCase);
                    return recovered;
                }
            }

            return new List<string>();
        }

        private static string ResolveClientId(string payloadClientId, ClientIdentity identity, string connectionId)
        {
            if (!string.IsNullOrWhiteSpace(payloadClientId))
            {
                return payloadClientId.Trim();
            }

            if (identity != null)
            {
                if (!string.IsNullOrWhiteSpace(identity.PlayerGuid))
                {
                    return identity.PlayerGuid;
                }
            }

            string cachedClientId = GetCachedClientId(connectionId);
            if (!string.IsNullOrWhiteSpace(cachedClientId))
            {
                return cachedClientId;
            }

            return string.Empty;
        }

        private static SignalRHeartbeatPayload CreateOfflinePayload(string connectionId, ClientIdentity identity)
        {
            string clientId = ResolveClientId(null, identity, connectionId);
            if (string.IsNullOrWhiteSpace(clientId))
            {
                return null;
            }

            return new SignalRHeartbeatPayload
            {
                ClientId = clientId,
                Status = "disconnected",
                Process = 0,
                Version = string.Empty,
                CurrentPage = string.Empty,
                HdmiState = false,
                Timestamp = DateTime.Now
            };
        }

        private static SignalRHeartbeatPayload CreateOnlinePayload(string connectionId, ClientIdentity identity)
        {
            string clientId = ResolveClientId(null, identity, connectionId);
            if (string.IsNullOrWhiteSpace(clientId))
            {
                return null;
            }

            return new SignalRHeartbeatPayload
            {
                ClientId = clientId,
                Status = "idle",
                Process = 0,
                Version = string.Empty,
                CurrentPage = string.Empty,
                HdmiState = true,
                Timestamp = DateTime.Now
            };
        }

        private sealed class ClientIdentity
        {
            public ClientIdentity(string playerName, string playerGuid)
            {
                PlayerName = playerName ?? string.Empty;
                PlayerGuid = playerGuid ?? string.Empty;
            }

            public string PlayerName { get; }
            public string PlayerGuid { get; }
        }
    }

    public sealed class SignalRHeartbeatPayload
    {
        public string ClientId { get; set; }
        public string Status { get; set; }
        public int Process { get; set; }
        public string Version { get; set; }
        public string CurrentPage { get; set; }
        public bool HdmiState { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public sealed class SignalRHeartbeatEventArgs : EventArgs
    {
        public SignalRHeartbeatEventArgs(SignalRHeartbeatPayload payload)
        {
            Payload = payload;
        }

        public SignalRHeartbeatPayload Payload { get; }
    }

    public sealed class SignalRCommandEnvelope
    {
        public string CommandId { get; set; }
        public string Command { get; set; }
        public string PlayerId { get; set; }
        public string PayloadJson { get; set; }
        public string CreatedAt { get; set; }
        public bool IsUrgent { get; set; }
    }

    public class SignalRMessage
    {
        public string From { get; set; } = "Server";

        public string To { get; set; } = "All";

        public string Command { get; set; } = "Update";

        public string DataType { get; set; } = "String";

        public object Data { get; set; } = null;
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
