using AndoW.Shared;
using RethinkDb.Driver;
using RethinkDb.Driver.Ast;
using RethinkDb.Driver.Net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;
using TurtleTools;
using SharedWeeklyPlayScheduleInfo = AndoW.Shared.WeeklyPlayScheduleInfo;

namespace NewHyOnPlayer
{
    internal sealed class RethinkSyncService : IDisposable
    {
        private const string DatabaseName = "NewHyOn";
        private const string PlayerTable = "PlayerInfoManager";
        private const string WeeklyTable = "WeeklyInfoManagerClass";

        private static readonly RethinkDB R = RethinkDB.R;

        private readonly PlayerInfoManager manager;
        private readonly LocalSettingsManager localManager;
        private readonly MultimediaTimer.Timer timer;
        private readonly int intervalMs;
        private string currentHost = "127.0.0.1";
        private Connection connection;
        private int isSyncing;
        private bool disposed;
        private bool infoSyncedAfterConnect;
        private bool syncFailedNotified;

        public event Action PlayerSynced;
        public event Action<string> PlayerGuidChanged;
        public event Action WeeklyScheduleSynced;
        public event Action SyncFailed;

        public RethinkSyncService(PlayerInfoManager manager, LocalSettingsManager localManager, int intervalMs = 5000)
        {
            this.manager = manager;
            this.localManager = localManager;
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
            if (disposed) return;
            TriggerSyncNow(scheduleNext: true);
        }

        public void Stop()
        {
            timer.Stop();
        }

        public void TriggerSyncNow(bool scheduleNext = false)
        {
            TrySync(scheduleNext);
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
            ResetConnection();
        }

        private void OnElapsed(object sender, EventArgs e)
        {
            TrySync(scheduleNext: true);
        }

        private void TrySync(bool scheduleNext)
        {
            if (Interlocked.Exchange(ref isSyncing, 1) == 1)
            {
                return;
            }

            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    GetPlayerGuidOnce();
                }
                finally
                {
                    Interlocked.Exchange(ref isSyncing, 0);
                    if (scheduleNext)
                    {
                        ScheduleNext();
                    }
                }
            });
        }

        private void ScheduleNext()
        {
            if (disposed) return;
            timer.Stop();
            timer.Period = intervalMs;
            timer.Start();
        }

        private void GetPlayerGuidOnce()
        {
            try
            {
                if (manager == null || manager.g_PlayerInfo == null || localManager == null || localManager.Settings == null)
                {
                    return;
                }

                RefreshLocalNetworkInfo();

                string host = localManager.Settings.ManagerIP;
                UpdateHost(host);

                string playerName = manager.g_PlayerInfo.PIF_PlayerName;
                string localGuid = manager.g_PlayerInfo.PIF_GUID;

                string remoteGuid = null;
                bool createdRemotePlayer = false;
                if (!string.IsNullOrWhiteSpace(playerName))
                {
                    bool lookupSucceeded = TryFetchGuidByName(playerName, out remoteGuid);
                    if (!lookupSucceeded)
                    {
                        NotifySyncFailed();
                        return;
                    }

                    syncFailedNotified = false;

                    if (string.IsNullOrWhiteSpace(remoteGuid))
                    {
                        string createdGuid = CreateNewGuidNotExists();
                        if (string.IsNullOrWhiteSpace(createdGuid))
                        {
                            NotifySyncFailed();
                            return;
                        }

                        remoteGuid = createdGuid;
                        createdRemotePlayer = true;
                    }
                }

                if (string.IsNullOrWhiteSpace(remoteGuid) && !string.IsNullOrWhiteSpace(localGuid))
                {
                    remoteGuid = localGuid;
                }

                if (string.IsNullOrWhiteSpace(remoteGuid))
                {
                    return;
                }

                bool guidChanged = !string.Equals(remoteGuid, localGuid, StringComparison.OrdinalIgnoreCase);
                if (guidChanged)
                {
                    manager.g_PlayerInfo.PIF_GUID = remoteGuid;
                    manager.SaveData();
                }

                if (createdRemotePlayer)
                {
                    UpsertPlayerInfoToRethink();
                }
                else
                {
                    UpdatePlayerInfoToRethink();
                }

                SyncAuthKey(manager.g_PlayerInfo, remoteGuid);

                bool shouldNotifyPlayerSynced = createdRemotePlayer || guidChanged || !infoSyncedAfterConnect;
                infoSyncedAfterConnect = true;

                if (guidChanged)
                {
                    PlayerGuidChanged?.Invoke(remoteGuid);
                }

                if (shouldNotifyPlayerSynced)
                {
                    PlayerSynced?.Invoke();
                }

                SyncWeeklySchedule(remoteGuid, manager.g_PlayerInfo.PIF_PlayerName);
            }
            catch (Exception ex)
            {
                Logger.WriteErrorLog(ex.ToString(), Logger.GetLogFileName());
                ResetConnection();
            }
        }

        private void UpdateHost(string host)
        {
            string normalized = string.IsNullOrWhiteSpace(host) ? "127.0.0.1" : host;
            if (!string.Equals(normalized, currentHost, StringComparison.OrdinalIgnoreCase))
            {
                currentHost = normalized;
                ResetConnection();
            }
        }

        private bool TryFetchGuidByName(string playerName, out string guid)
        {
            guid = null;

            if (string.IsNullOrWhiteSpace(playerName))
            {
                return true;
            }

            string lowered = playerName.ToLowerInvariant();

            try
            {
                var conn = GetConnection();
                if (conn == null)
                {
                    return false;
                }

                ReqlExpr query = R.Db(DatabaseName)
                    .Table(PlayerTable)
                    .Filter(row => row["PIF_PlayerName"].Downcase().Eq(lowered))
                    .Limit(1);

                PlayerInfoClass playerInfo = query.RunCursor<PlayerInfoClass>(conn).BufferedItems.FirstOrDefault();
                guid = playerInfo == null ? string.Empty : playerInfo.PIF_GUID;
                return true;
            }
            catch (Exception ex)
            {
                Logger.WriteErrorLog(ex.ToString(), Logger.GetLogFileName());
                return false;
            }
        }

        private Connection GetConnection()
        {
            try
            {
                if (connection != null && connection.Open)
                {
                    return connection;
                }

                connection = R.Connection()
                    .Hostname(currentHost)
                    .Port(28015)
                    .User("admin", "turtle04!9")
                    .Connect();

                infoSyncedAfterConnect = false;
                return connection;
            }
            catch (Exception ex)
            {
                Logger.WriteErrorLog(ex.ToString(), Logger.GetLogFileName());
                ResetConnection();
                return null;
            }
        }

        private void UpdatePlayerInfoToRethink()
        {
            try
            {
                var player = manager?.g_PlayerInfo;
                if (player == null || string.IsNullOrWhiteSpace(player.PIF_GUID))
                {
                    return;
                }

                var conn = GetConnection();
                if (conn == null)
                {
                    return;
                }

                var exists = R.Db(DatabaseName)
                    .Table(PlayerTable)
                    .Get(player.PIF_GUID)
                    .RunAtom<Dictionary<string, object>>(conn);
                if (exists == null)
                {
                    return;
                }

                ResolveCurrentNetworkInfo(out string localIp, out string mac, out List<string> allMacs);
                string authKeyToStore = EnsureAuthKeyForCurrentNic(player, mac, allMacs);
                string osName = string.IsNullOrWhiteSpace(player.PIF_OSName)
                    ? Environment.OSVersion.ToString()
                    : player.PIF_OSName;

                if (!string.IsNullOrWhiteSpace(localIp) || !string.IsNullOrWhiteSpace(mac))
                {
                    UpdateLocalNetworkInfo(localIp, mac);
                }

                var payload = new Dictionary<string, object>
                {
                    ["PIF_PlayerName"] = player.PIF_PlayerName ?? string.Empty,
                    ["PIF_CurrentPlayList"] = player.PIF_CurrentPlayList ?? string.Empty,
                    ["PIF_IPAddress"] = string.IsNullOrWhiteSpace(localIp) ? player.PIF_IPAddress ?? string.Empty : localIp,
                    ["PIF_OSName"] = osName,
                    ["PIF_MacAddress"] = string.IsNullOrWhiteSpace(mac) ? player.PIF_MacAddress ?? string.Empty : mac,
                    ["command"] = player.PendingCommand ?? string.Empty
                };
                if (!string.IsNullOrWhiteSpace(authKeyToStore) && IsAuthKeyMatchedAnyNic(authKeyToStore, allMacs))
                {
                    payload["PIF_AuthKey"] = authKeyToStore;
                }

                R.Db(DatabaseName)
                    .Table(PlayerTable)
                    .Get(player.PIF_GUID)
                    .Update(payload)
                    .RunNoReply(conn);
            }
            catch (Exception ex)
            {
                Logger.WriteErrorLog(ex.ToString(), Logger.GetLogFileName());
                ResetConnection();
            }
        }

        private void UpsertPlayerInfoToRethink()
        {
            try
            {
                var player = manager?.g_PlayerInfo;
                if (player == null || string.IsNullOrWhiteSpace(player.PIF_GUID))
                {
                    return;
                }

                var conn = GetConnection();
                if (conn == null)
                {
                    return;
                }

                ResolveCurrentNetworkInfo(out string localIp, out string mac, out List<string> allMacs);
                string authKeyToStore = EnsureAuthKeyForCurrentNic(player, mac, allMacs);
                string osName = string.IsNullOrWhiteSpace(player.PIF_OSName)
                    ? Environment.OSVersion.ToString()
                    : player.PIF_OSName;

                if (!string.IsNullOrWhiteSpace(localIp) || !string.IsNullOrWhiteSpace(mac))
                {
                    UpdateLocalNetworkInfo(localIp, mac);
                }

                var payload = new Dictionary<string, object>
                {
                    ["id"] = player.PIF_GUID,
                    ["PIF_PlayerName"] = player.PIF_PlayerName ?? string.Empty,
                    ["PIF_CurrentPlayList"] = player.PIF_CurrentPlayList ?? string.Empty,
                    ["PIF_IPAddress"] = string.IsNullOrWhiteSpace(localIp) ? player.PIF_IPAddress ?? string.Empty : localIp,
                    ["PIF_OSName"] = osName,
                    ["PIF_MacAddress"] = string.IsNullOrWhiteSpace(mac) ? player.PIF_MacAddress ?? string.Empty : mac,
                    ["command"] = player.PendingCommand ?? string.Empty
                };
                if (!string.IsNullOrWhiteSpace(authKeyToStore) && IsAuthKeyMatchedAnyNic(authKeyToStore, allMacs))
                {
                    payload["PIF_AuthKey"] = authKeyToStore;
                }

                R.Db(DatabaseName)
                    .Table(PlayerTable)
                    .Insert(payload)
                    .OptArg("conflict", "update")
                    .RunNoReply(conn);
            }
            catch (Exception ex)
            {
                Logger.WriteErrorLog(ex.ToString(), Logger.GetLogFileName());
                ResetConnection();
            }
        }

        private void ResetConnection()
        {
            try
            {
                if (connection != null)
                {
                    connection.Close(false);
                    connection.Dispose();
                }
            }
            catch
            {
            }
            finally
            {
                connection = null;
                infoSyncedAfterConnect = false;
            }
        }

        private void SyncAuthKey(PlayerInfoClass localPlayer, string remoteGuid)
        {
            if (localPlayer == null || string.IsNullOrWhiteSpace(remoteGuid))
            {
                return;
            }

            ResolveCurrentNetworkInfo(out _, out string currentMac, out List<string> allMacs);
            string localKeyForCurrent = EnsureAuthKeyForCurrentNic(localPlayer, currentMac, allMacs);

            PlayerInfoClass remote = FetchPlayerByGuid(remoteGuid);
            if (remote == null)
            {
                if (!string.IsNullOrWhiteSpace(localKeyForCurrent) && IsAuthKeyMatchedAnyNic(localKeyForCurrent, allMacs))
                {
                    UpsertPlayerInfoToRethink();
                }
                return;
            }

            string localKey = localKeyForCurrent?.Trim() ?? string.Empty;
            string remoteKey = remote.PIF_AuthKey?.Trim() ?? string.Empty;
            bool localMatchesAny = IsAuthKeyMatchedAnyNic(localKey, allMacs);

            if (string.IsNullOrWhiteSpace(localKey) && !string.IsNullOrWhiteSpace(remoteKey))
            {
                if (IsAuthKeyMatchedAnyNic(remoteKey, allMacs))
                {
                    localPlayer.PIF_AuthKey = remoteKey;
                    manager.SaveData();
                }
                return;
            }

            if (!string.IsNullOrWhiteSpace(localKey) && string.IsNullOrWhiteSpace(remoteKey))
            {
                if (localMatchesAny)
                {
                    UpdateAuthKeyInRethink(remoteGuid, localKey);
                }
                return;
            }

            if (!string.IsNullOrWhiteSpace(localKey) && !string.IsNullOrWhiteSpace(remoteKey)
                && !string.Equals(localKey, remoteKey, StringComparison.OrdinalIgnoreCase))
            {
                if (localMatchesAny)
                {
                    UpdateAuthKeyInRethink(remoteGuid, localKey);
                }
                else
                {
                    Logger.WriteLog($"AuthKey mismatch detected. local={localKey}, remote={remoteKey}", Logger.GetLogFileName());
                }
            }
        }

        private void RefreshLocalNetworkInfo()
        {
            ResolveCurrentNetworkInfo(out string localIp, out string mac, out List<string> allMacs);
            EnsureAuthKeyForCurrentNic(manager?.g_PlayerInfo, mac, allMacs);
            if (!string.IsNullOrWhiteSpace(localIp) || !string.IsNullOrWhiteSpace(mac))
            {
                UpdateLocalNetworkInfo(localIp, mac);
            }
        }

        private void UpdateLocalNetworkInfo(string localIp, string mac)
        {
            var player = manager?.g_PlayerInfo;
            if (player == null)
            {
                return;
            }

            bool changed = false;
            if (!string.IsNullOrWhiteSpace(localIp) &&
                !string.Equals(player.PIF_IPAddress ?? string.Empty, localIp, StringComparison.OrdinalIgnoreCase))
            {
                player.PIF_IPAddress = localIp;
                changed = true;
            }

            if (!string.IsNullOrWhiteSpace(mac) &&
                !string.Equals(player.PIF_MacAddress ?? string.Empty, mac, StringComparison.OrdinalIgnoreCase))
            {
                player.PIF_MacAddress = mac;
                changed = true;
            }

            if (changed)
            {
                manager.SaveData();
            }
        }

        private void ResolveCurrentNetworkInfo(out string localIp, out string mac, out List<string> allMacs)
        {
            localIp = string.Empty;
            mac = string.Empty;
            allMacs = new List<string>();

            try
            {
                foreach (NetworkInterface adapter in NetworkInterface.GetAllNetworkInterfaces())
                {
                    string addr = adapter.GetPhysicalAddress()?.ToString() ?? string.Empty;
                    if (!string.IsNullOrWhiteSpace(addr))
                    {
                        allMacs.Add(addr);
                    }
                }
            }
            catch
            {
            }

            IPAddress currentIp = null;
            try
            {
                currentIp = NetworkTools.GetAutoIP();
            }
            catch
            {
            }

            if (currentIp != null && currentIp.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(currentIp))
            {
                localIp = currentIp.ToString();
            }

            if (currentIp != null)
            {
                mac = ResolveMacForIp(currentIp);
            }

            if (string.IsNullOrWhiteSpace(mac) && allMacs.Count > 0)
            {
                mac = allMacs[0];
            }
        }

        private string ResolveMacForIp(IPAddress ip)
        {
            if (ip == null)
            {
                return string.Empty;
            }

            try
            {
                foreach (NetworkInterface adapter in NetworkInterface.GetAllNetworkInterfaces())
                {
                    var properties = adapter.GetIPProperties();
                    foreach (var addr in properties.UnicastAddresses)
                    {
                        if (addr.Address.AddressFamily == AddressFamily.InterNetwork && addr.Address.Equals(ip))
                        {
                            return adapter.GetPhysicalAddress()?.ToString() ?? string.Empty;
                        }
                    }
                }
            }
            catch
            {
            }

            return string.Empty;
        }

        private string EnsureAuthKeyForCurrentNic(PlayerInfoClass player, string currentMac, List<string> allMacs)
        {
            if (player == null)
            {
                return string.Empty;
            }

            string localKey = player.PIF_AuthKey?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(localKey) || string.IsNullOrWhiteSpace(currentMac))
            {
                return localKey;
            }

            if (IsAuthKeyMatchedMac(localKey, currentMac))
            {
                return localKey;
            }

            if (!IsAuthKeyMatchedAnyNic(localKey, allMacs))
            {
                return localKey;
            }

            string currentKey = AuthTools.EncodeAuthKey(currentMac);
            if (!string.Equals(localKey, currentKey, StringComparison.OrdinalIgnoreCase))
            {
                player.PIF_AuthKey = currentKey;
                manager?.SaveData();
            }

            return currentKey;
        }

        private bool IsAuthKeyMatchedMac(string authKey, string mac)
        {
            if (string.IsNullOrWhiteSpace(authKey) || string.IsNullOrWhiteSpace(mac))
            {
                return false;
            }

            string expected = AuthTools.EncodeAuthKey(mac);
            return string.Equals(authKey.Trim(), expected, StringComparison.OrdinalIgnoreCase);
        }

        private bool IsAuthKeyMatchedAnyNic(string authKey, List<string> macs)
        {
            if (string.IsNullOrWhiteSpace(authKey) || macs == null || macs.Count == 0)
            {
                return false;
            }

            string localKey = authKey.Trim();
            foreach (string mac in macs)
            {
                if (string.IsNullOrWhiteSpace(mac))
                {
                    continue;
                }

                string expected = AuthTools.EncodeAuthKey(mac);
                if (string.Equals(localKey, expected, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private PlayerInfoClass FetchPlayerByGuid(string playerGuid)
        {
            if (string.IsNullOrWhiteSpace(playerGuid))
            {
                return null;
            }

            try
            {
                var conn = GetConnection();
                if (conn == null)
                {
                    return null;
                }

                return R.Db(DatabaseName)
                    .Table(PlayerTable)
                    .Get(playerGuid)
                    .RunAtom<PlayerInfoClass>(conn);
            }
            catch (Exception ex)
            {
                Logger.WriteErrorLog(ex.ToString(), Logger.GetLogFileName());
                ResetConnection();
                return null;
            }
        }

        private void UpdateAuthKeyInRethink(string playerGuid, string authKey)
        {
            if (string.IsNullOrWhiteSpace(playerGuid) || string.IsNullOrWhiteSpace(authKey))
            {
                return;
            }

            try
            {
                var conn = GetConnection();
                if (conn == null)
                {
                    return;
                }

                var payload = new Dictionary<string, object>
                {
                    ["PIF_AuthKey"] = authKey
                };

                R.Db(DatabaseName)
                    .Table(PlayerTable)
                    .Get(playerGuid)
                    .Update(payload)
                    .RunNoReply(conn);
            }
            catch (Exception ex)
            {
                Logger.WriteErrorLog(ex.ToString(), Logger.GetLogFileName());
                ResetConnection();
            }
        }

        private void NotifySyncFailed()
        {
            if (syncFailedNotified)
            {
                return;
            }

            syncFailedNotified = true;
            SyncFailed?.Invoke();
        }

        private string CreateNewGuidNotExists()
        {
            var conn = GetConnection();
            if (conn == null)
            {
                return null;
            }

            for (int attempt = 0; attempt < 5; attempt++)
            {
                string candidate = Guid.NewGuid().ToString();
                var exists = R.Db(DatabaseName)
                    .Table(PlayerTable)
                    .Get(candidate)
                    .RunAtom<Dictionary<string, object>>(conn);
                if (exists == null)
                {
                    return candidate;
                }
            }

            return Guid.NewGuid().ToString();
        }

        private void SyncWeeklySchedule(string playerId, string playerName)
        {
            if (string.IsNullOrWhiteSpace(playerId) && string.IsNullOrWhiteSpace(playerName))
            {
                return;
            }

            try
            {
                var conn = GetConnection();
                if (conn == null)
                {
                    return;
                }

                RethinkWeeklySchedule schedule = null;
                if (!string.IsNullOrWhiteSpace(playerId))
                {
                    schedule = R.Db(DatabaseName)
                        .Table(WeeklyTable)
                        .Get(playerId)
                        .RunAtom<RethinkWeeklySchedule>(conn);
                }

                if (schedule == null && !string.IsNullOrWhiteSpace(playerName))
                {
                    schedule = R.Db(DatabaseName)
                        .Table(WeeklyTable)
                        .Filter(row => row["PlayerName"].Downcase().Eq(playerName.ToLowerInvariant()))
                        .Limit(1)
                        .RunCursor<RethinkWeeklySchedule>(conn)
                        .FirstOrDefault();
                }

                if (schedule == null)
                {
                    return;
                }

                var localId = string.IsNullOrWhiteSpace(schedule.Id)
                    ? (string.IsNullOrWhiteSpace(playerId) ? playerName : playerId)
                    : schedule.Id?.Trim();
                if (string.IsNullOrWhiteSpace(localId))
                {
                    return;
                }

                var resolvedPlayerId = string.IsNullOrWhiteSpace(schedule.PlayerID)
                    ? (string.IsNullOrWhiteSpace(playerId) ? playerName : playerId)
                    : schedule.PlayerID?.Trim();
                if (string.IsNullOrWhiteSpace(resolvedPlayerId))
                {
                    return;
                }

                var resolvedPlayerName = string.IsNullOrWhiteSpace(schedule.PlayerName)
                    ? (string.IsNullOrWhiteSpace(playerName) ? playerId : playerName)
                    : schedule.PlayerName?.Trim();

                var local = new SharedWeeklyPlayScheduleInfo
                {
                    Id = localId,
                    PlayerID = resolvedPlayerId,
                    PlayerName = resolvedPlayerName ?? string.Empty,
                    MonSch = schedule.MonSch ?? DaySchedule.CreateDefault(),
                    TueSch = schedule.TueSch ?? DaySchedule.CreateDefault(),
                    WedSch = schedule.WedSch ?? DaySchedule.CreateDefault(),
                    ThuSch = schedule.ThuSch ?? DaySchedule.CreateDefault(),
                    FriSch = schedule.FriSch ?? DaySchedule.CreateDefault(),
                    SatSch = schedule.SatSch ?? DaySchedule.CreateDefault(),
                    SunSch = schedule.SunSch ?? DaySchedule.CreateDefault()
                };

                using (var repo = new WeeklyScheduleRepository())
                {
                    repo.DeleteMany(x =>
                        (!string.IsNullOrWhiteSpace(local.Id)
                            && string.Equals(x.Id, local.Id, StringComparison.OrdinalIgnoreCase))
                        || (!string.IsNullOrWhiteSpace(local.PlayerID)
                            && string.Equals(x.PlayerID, local.PlayerID, StringComparison.OrdinalIgnoreCase))
                        || (!string.IsNullOrWhiteSpace(local.PlayerName)
                            && string.Equals(x.PlayerName, local.PlayerName, StringComparison.OrdinalIgnoreCase)));
                    repo.Upsert(local);
                }

                WeeklyScheduleSynced?.Invoke();
            }
            catch (Exception ex)
            {
                Logger.WriteErrorLog($"SyncWeeklySchedule error: {ex}", Logger.GetLogFileName());
            }
        }

        private sealed class RethinkWeeklySchedule
        {
            public string Id { get; set; }
            public string PlayerID { get; set; }
            public string PlayerName { get; set; }
            public DaySchedule MonSch { get; set; }
            public DaySchedule TueSch { get; set; }
            public DaySchedule WedSch { get; set; }
            public DaySchedule ThuSch { get; set; }
            public DaySchedule FriSch { get; set; }
            public DaySchedule SatSch { get; set; }
            public DaySchedule SunSch { get; set; }
        }

    }
}
