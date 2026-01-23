using AndoW.Shared;
using RethinkDb.Driver;
using RethinkDb.Driver.Ast;
using RethinkDb.Driver.Net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using TurtleTools;
using SharedWeeklyPlayScheduleInfo = AndoW.Shared.WeeklyPlayScheduleInfo;

namespace HyOnPlayer
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

        public event Action PlayerSynced;
        public event Action<string> PlayerGuidChanged;
        public event Action WeeklyScheduleSynced;

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

                string host = localManager.Settings.ManagerIP;
                UpdateHost(host);

                string playerName = manager.g_PlayerInfo.PIF_PlayerName;
                string localGuid = manager.g_PlayerInfo.PIF_GUID;

                string remoteGuid = null;
                if (!string.IsNullOrWhiteSpace(playerName))
                {
                    remoteGuid = FetchGuidByName(playerName);
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
                    UpdatePlayerInfoToRethink();
                    infoSyncedAfterConnect = true;
                    PlayerGuidChanged?.Invoke(remoteGuid);
                    PlayerSynced?.Invoke();
                    return;
                }

                if (!infoSyncedAfterConnect)
                {
                    UpdatePlayerInfoToRethink();
                    infoSyncedAfterConnect = true;
                }

                SyncWeeklySchedule(remoteGuid ?? localGuid, playerName);
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

        private string FetchGuidByName(string playerName)
        {
            if (string.IsNullOrWhiteSpace(playerName))
            {
                return null;
            }

            string lowered = playerName.ToLowerInvariant();

            try
            {
                var conn = GetConnection();
                if (conn == null)
                {
                    return null;
                }

                ReqlExpr query = R.Db(DatabaseName)
                    .Table(PlayerTable)
                    .Filter(row => row["PIF_PlayerName"].Downcase().Eq(lowered))
                    .Limit(1);

                PlayerInfoClass playerInfo = query.RunCursor<PlayerInfoClass>(conn).BufferedItems.FirstOrDefault();
                return playerInfo == null ? string.Empty : playerInfo.PIF_GUID;
            }
            catch (Exception ex)
            {
                Logger.WriteErrorLog(ex.ToString(), Logger.GetLogFileName());
                return null;
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

                string localIp = NetworkTools.GetAutoIP()?.ToString() ?? string.Empty;
                string mac = NetworkTools.GetMACAddressBySystemNet();
                string osName = string.IsNullOrWhiteSpace(player.PIF_OSName)
                    ? Environment.OSVersion.ToString()
                    : player.PIF_OSName;

                var payload = new Dictionary<string, object>
                {
                    ["PIF_PlayerName"] = player.PIF_PlayerName ?? string.Empty,
                    ["PIF_CurrentPlayList"] = player.PIF_CurrentPlayList ?? string.Empty,
                    ["PIF_IsLandScape"] = player.PIF_IsLandScape,
                    ["PIF_IPAddress"] = string.IsNullOrWhiteSpace(player.PIF_IPAddress) ? localIp : player.PIF_IPAddress,
                    ["PIF_OSName"] = osName,
                    ["PIF_MacAddress"] = string.IsNullOrWhiteSpace(player.PIF_MacAddress) ? mac : player.PIF_MacAddress,
                    ["command"] = player.PendingCommand ?? string.Empty
                };

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

                string localIp = NetworkTools.GetAutoIP()?.ToString() ?? string.Empty;
                string mac = NetworkTools.GetMACAddressBySystemNet();
                string osName = string.IsNullOrWhiteSpace(player.PIF_OSName)
                    ? Environment.OSVersion.ToString()
                    : player.PIF_OSName;

                var payload = new Dictionary<string, object>
                {
                    ["id"] = player.PIF_GUID,
                    ["PIF_PlayerName"] = player.PIF_PlayerName ?? string.Empty,
                    ["PIF_CurrentPlayList"] = player.PIF_CurrentPlayList ?? string.Empty,
                    ["PIF_IsLandScape"] = player.PIF_IsLandScape,
                    ["PIF_IPAddress"] = string.IsNullOrWhiteSpace(player.PIF_IPAddress) ? localIp : player.PIF_IPAddress,
                    ["PIF_OSName"] = osName,
                    ["PIF_MacAddress"] = string.IsNullOrWhiteSpace(player.PIF_MacAddress) ? mac : player.PIF_MacAddress,
                    ["command"] = player.PendingCommand ?? string.Empty
                };

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
