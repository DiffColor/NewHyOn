using AndoW.Shared;
using Newtonsoft.Json;
using SharedUpdatePayload = AndoW.Shared.UpdatePayload;
using HyOnPlayer.DataManager;
using RethinkDb.Driver;
using RethinkDb.Driver.Ast;
using RethinkDb.Driver.Net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using TurtleTools;
using HyOnPlayer.Services;
using SharedWeeklyPlayScheduleInfo = AndoW.Shared.WeeklyPlayScheduleInfo;

namespace HyOnPlayer
{
    internal sealed class RemoteCommandService : IDisposable
    {
        private readonly MainWindow owner;
        private readonly RethinkCommandClient client;
        private readonly CommandQueueClient commandQueueClient;
        private readonly MultimediaTimer.Timer timer;
        private readonly UpdateService updateService;
        private readonly CommandHistoryClient historyClient;
        private readonly string managerHost;
        private readonly ScheduleEvaluator scheduleEvaluator;
        private int isExecuting;
        private int isHandlingCommand;
        private string currentCommand = string.Empty;
        private readonly int intervalMs;

        public RemoteCommandService(MainWindow owner, double intervalMs = 5000)
        {
            this.owner = owner;
            this.intervalMs = (int)Math.Max(1, intervalMs);
            managerHost = owner?.g_LocalSettingsManager?.Settings?.ManagerIP;
            client = new RethinkCommandClient(managerHost);
            commandQueueClient = new CommandQueueClient(managerHost);
            historyClient = new CommandHistoryClient(string.IsNullOrWhiteSpace(managerHost) ? "127.0.0.1" : managerHost);
            updateService = new UpdateService(owner);
            scheduleEvaluator = new ScheduleEvaluator(owner?.g_PlayerInfoManager);
            historyClient.PurgeOlderThanDays(30);
            timer = new MultimediaTimer.Timer
            {
                Mode = MultimediaTimer.TimerMode.OneShot,
                Period = this.intervalMs,
                Resolution = 1
            };
            timer.Tick += OnElapsed;
        }

        public void Start() => ScheduleNext();

        public void Stop() => timer.Stop();

        public void Dispose()
        {
            timer.Stop();
            timer.Dispose();
            historyClient.Dispose();
            commandQueueClient?.Dispose();
            updateService.Dispose();
        }

        private void OnElapsed(object sender, EventArgs e)
        {
            if (Interlocked.Exchange(ref isExecuting, 1) == 1)
            {
                return; // 현재 실행 중이면 이번 라운드는 건너뛴다.
            }

            ThreadPool.QueueUserWorkItem(_ =>
            {
                CheckCommand();
                Interlocked.Exchange(ref isExecuting, 0);
                ScheduleNext();
            });
        }

        private void CheckCommand()
        {
            var infoManager = owner?.g_PlayerInfoManager;
            if (infoManager == null)
            {
                return;
            }

            var playerInfo = infoManager.g_PlayerInfo;
            if (playerInfo == null || string.IsNullOrWhiteSpace(playerInfo.PIF_GUID))
            {
                return;
            }

            var entry = commandQueueClient.FetchNextPending(playerInfo.PIF_GUID);
            if (entry == null)
            {
                return;
            }

            commandQueueClient.MarkAttempt(entry.Id);
            HandleCommandEntry(playerInfo, entry, false);
            return;

            string playerName = infoManager.g_PlayerInfo.PIF_PlayerName;
            string localGuid = infoManager.g_PlayerInfo.PIF_GUID;

            PlayerInfoClass remotePlayer = null;
            if (!string.IsNullOrWhiteSpace(playerName))
            {
                remotePlayer = client.FetchPlayerByName(playerName);
            }

            if (remotePlayer == null)
            {
                // 서버에 플레이어가 아직 생성되지 않음 → 아무 것도 하지 않고 대기
                return;
            }

            if (!localGuid.Equals(remotePlayer.PIF_GUID)) {
                infoManager.g_PlayerInfo.PIF_GUID = remotePlayer.PIF_GUID;
                infoManager.SaveData();
            }
                        
            string command = client.FetchCommand(remotePlayer.PIF_GUID);
            if (string.IsNullOrWhiteSpace(command))
            {
                return;
            }

            command = command.Trim().ToLowerInvariant();
            HandleCommand(remotePlayer, command, true);
        }

        private void ScheduleNext()
        {
            timer.Stop();
            timer.Period = intervalMs;
            timer.Start();
        }

        private void HandleCommandEntry(PlayerInfoClass playerInfo, CommandQueueEntry entry, bool isUrgent)
        {
            if (playerInfo == null || entry == null)
            {
                return;
            }

            string command = entry.Command?.Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(command))
            {
                return;
            }

            var payload = DecodePayload(entry.PayloadBase64);
            bool handled = HandleCommandCore(playerInfo, command, payload, isUrgent);
            string playerId = playerInfo.PIF_GUID;
            if (handled)
            {
                commandQueueClient.MarkAck(entry.Id, playerId);
            }
            else
            {
                commandQueueClient.MarkFailed(entry.Id, playerId);
            }
        }

        private bool HandleCommandCore(PlayerInfoClass playerInfo, string command, SharedUpdatePayload payloadData, bool isUrgent)
        {
            if (playerInfo == null || string.IsNullOrWhiteSpace(command))
            {
                return false;
            }

            Interlocked.Exchange(ref isHandlingCommand, 1);
            currentCommand = command ?? string.Empty;
            try
            {
                string playerGuid = playerInfo.PIF_GUID;
                bool isClearQueue = string.Equals(command, "clearqueue", StringComparison.OrdinalIgnoreCase);

                if (!isClearQueue && HasActiveQueue())
                {
                    updateService.CancelActiveQueues("Cancelled due to new command");
                }

                switch (command)
                {
                    case "updatelist":
                        if (payloadData == null)
                        {
                            Logger.WriteLog("updatelist command missing payload.", Logger.GetLogFileName());
                            return false;
                        }
                        updateService.RunUpdateAsync(playerInfo, payloadData, isUrgent);
                        return true;
                    case "updateschedule":
                        {
                            string historyId = RecordCommandQueued(playerGuid, command);
                            bool applied = payloadData != null && ApplySchedulePayload(payloadData.Schedule);
                            if (applied)
                            {
                                RecordCommandDone(historyId, playerGuid, CommandHistoryStatus.Done, null, null);
                                return true;
                            }

                            Logger.WriteLog("updateschedule payload missing or invalid.", Logger.GetLogFileName());
                            RecordCommandDone(historyId, playerGuid, CommandHistoryStatus.Failed, "SCHEDULE_PAYLOAD", "payload missing");
                            return false;
                        }
                    case "updateweekly":
                        {
                            string historyId = RecordCommandQueued(playerGuid, command);
                            bool applied = payloadData != null && ApplyWeeklySchedule(payloadData.Schedule?.WeeklySchedule, playerInfo);
                            if (applied)
                            {
                                RecordCommandDone(historyId, playerGuid, CommandHistoryStatus.Done, null, null);
                                return true;
                            }

                            Logger.WriteLog("updateweekly payload missing or invalid.", Logger.GetLogFileName());
                            RecordCommandDone(historyId, playerGuid, CommandHistoryStatus.Failed, "WEEKLY_PAYLOAD", "payload missing");
                            return false;
                        }
                    case "reboot":
                        {
                            string historyId = RecordCommandQueued(playerGuid, command);
                            ProcessTools.ExecuteCommand("shutdown /r /t 0");
                            RecordCommandDone(historyId, playerGuid, CommandHistoryStatus.Done, null, null);
                            return true;
                        }
                    case "poweroff":
                        {
                            string historyId = RecordCommandQueued(playerGuid, command);
                            ProcessTools.ExecuteCommand("shutdown /s /t 0");
                            RecordCommandDone(historyId, playerGuid, CommandHistoryStatus.Done, null, null);
                            return true;
                        }
                    case "clearqueue":
                        {
                            string historyId = RecordCommandQueued(playerGuid, command);
                            int cancelled = updateService.CancelActiveQueues("Cancelled via command");
                            Logger.WriteLog($"clearqueue command received, cancelled={cancelled}", Logger.GetLogFileName());
                            RecordCommandDone(historyId, playerGuid, CommandHistoryStatus.Cancelled, null, $"cancelled={cancelled}");
                            return true;
                        }
                    default:
                        {
                            string historyId = RecordCommandQueued(playerGuid, command);
                            Logger.WriteLog($"Unknown remote command: {command}", Logger.GetLogFileName());
                            RecordCommandDone(historyId, playerGuid, CommandHistoryStatus.Failed, "UNKNOWN_COMMAND", null);
                            return false;
                        }
                }
            }
            catch (Exception ex)
            {
                Logger.WriteErrorLog(ex.ToString(), Logger.GetLogFileName());
                return false;
            }
            finally
            {
                currentCommand = string.Empty;
                Interlocked.Exchange(ref isHandlingCommand, 0);
            }
        }

        private SharedUpdatePayload DecodePayload(string payloadBase64)
        {
            if (string.IsNullOrWhiteSpace(payloadBase64))
            {
                return null;
            }

            var payload = UpdatePayloadCodec.Decode(payloadBase64);
            if (payload != null)
            {
                return payload;
            }

            try
            {
                return JsonConvert.DeserializeObject<SharedUpdatePayload>(payloadBase64);
            }
            catch
            {
                return null;
            }
        }

        private void HandleCommand(PlayerInfoClass playerInfo, string command, bool clearCommand)
        {
            HandleCommandCore(playerInfo, command, null, false);
            return;

            Interlocked.Exchange(ref isHandlingCommand, 1);
            currentCommand = command ?? string.Empty;
            try
            {
                string playerGuid = playerInfo.PIF_GUID;
                string managerHost = this.managerHost;

                bool isClearQueue = string.Equals(command, "clearqueue", StringComparison.OrdinalIgnoreCase);

                // 새 명령이 오면 기존 큐를 즉시 취소하여 교체
                if (!isClearQueue && HasActiveQueue())
                {
                    updateService.CancelActiveQueues("Cancelled due to new command");
                }

                switch (command)
                {
                    case "updatelist":
                        updateService.RunUpdateAsync(playerInfo);
                        break;
                    case "updateschedule":
                        {
                            string historyId = RecordCommandQueued(playerInfo.PIF_GUID, command);
                            bool applied = ApplySchedulePayload(null);
                            if (applied)
                            {
                                RecordCommandDone(historyId, playerGuid, CommandHistoryStatus.Done, null, null);
                            }
                            else
                            {
                                Logger.WriteLog("updateschedule payload missing or invalid.", Logger.GetLogFileName());
                                RecordCommandDone(historyId, playerGuid, CommandHistoryStatus.Failed, "SCHEDULE_PAYLOAD", "payload missing");
                            }
                        }
                        break;
                    case "reboot":
                        {
                            string historyId = RecordCommandQueued(playerGuid, command);
                            ProcessTools.ExecuteCommand("shutdown /r /t 0");
                            RecordCommandDone(historyId, playerGuid, CommandHistoryStatus.Done, null, null);
                        }
                        break;
                    case "poweroff":
                        {
                            string historyId = RecordCommandQueued(playerGuid, command);
                            ProcessTools.ExecuteCommand("shutdown /s /t 0");
                            RecordCommandDone(historyId, playerGuid, CommandHistoryStatus.Done, null, null);
                        }
                        break;
                    case "clearqueue":
                        {
                            string historyId = RecordCommandQueued(playerGuid, command);
                            int cancelled = updateService.CancelActiveQueues("Cancelled via command");
                            Logger.WriteLog($"clearqueue command received, cancelled={cancelled}", Logger.GetLogFileName());
                            RecordCommandDone(historyId, playerGuid, CommandHistoryStatus.Cancelled, null, $"cancelled={cancelled}");
                        }
                        break;
                    default:
                        {
                            string historyId = RecordCommandQueued(playerGuid, command);
                            Logger.WriteLog($"Unknown remote command: {command}", Logger.GetLogFileName());
                            RecordCommandDone(historyId, playerGuid, CommandHistoryStatus.Failed, "UNKNOWN_COMMAND", null);
                        }
                        break;
                }

                if (clearCommand)
                {
                    client.ClearCommand(playerGuid);
                }
            }
            catch (Exception ex)
            {
                Logger.WriteErrorLog(ex.ToString(), Logger.GetLogFileName());
            }
            finally
            {
                currentCommand = string.Empty;
                Interlocked.Exchange(ref isHandlingCommand, 0);
            }
        }

        private bool HasActiveQueue()
        {
            try
            {
                using (var repo = new UpdateQueueRepository())
                {
                    var queues = repo.LoadAll();
                    foreach (var q in queues)
                    {
                        if (q.Status == UpdateQueueStatus.Queued
                            || q.Status == UpdateQueueStatus.Downloading
                            || q.Status == UpdateQueueStatus.Validating)
                        {
                            return true;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.WriteErrorLog(ex.ToString(), Logger.GetLogFileName());
            }
            return false;
        }

        private string RecordCommandQueued(string playerId, string command)
        {
            return historyClient.CreateQueued(playerId ?? string.Empty, string.Empty, command ?? string.Empty);
        }

        private void RecordCommandStarted(string historyId)
        {
            if (string.IsNullOrWhiteSpace(historyId)) return;
            historyClient.MarkInProgress(historyId);
        }

        private void RecordCommandDone(string historyId, string playerId, string status, string errorCode, string errorMessage)
        {
            if (string.IsNullOrWhiteSpace(historyId)) return;
            historyClient.MarkDone(historyId, status ?? CommandHistoryStatus.Done, errorCode, errorMessage);
        }

        private bool ApplySchedulePayload(ScheduleUpdatePayload payload)
        {
            if (payload == null)
            {
                return false;
            }

            string cacheId = string.IsNullOrWhiteSpace(payload.PlayerId) ? payload.PlayerName : payload.PlayerId;
            if (string.IsNullOrWhiteSpace(cacheId))
            {
                return false;
            }

            var schedules = new List<SpecialSchedulePayload>();
            if (payload.SpecialSchedules != null)
            {
                foreach (var schedule in payload.SpecialSchedules)
                {
                    if (schedule != null)
                    {
                        schedules.Add(schedule);
                    }
                }
            }

            var playlists = new List<SchedulePlaylistPayload>();
            if (payload.Playlists != null)
            {
                foreach (var playlist in payload.Playlists)
                {
                    if (playlist == null || playlist.PageList == null || playlist.Pages == null || playlist.Pages.Count == 0)
                    {
                        continue;
                    }

                    playlists.Add(playlist);
                }
            }

            var cache = new SpecialScheduleCache
            {
                Id = cacheId,
                PlayerId = payload.PlayerId ?? string.Empty,
                PlayerName = payload.PlayerName ?? string.Empty,
                UpdatedAt = string.IsNullOrWhiteSpace(payload.GeneratedAt)
                    ? DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                    : payload.GeneratedAt,
                Schedules = schedules,
                Playlists = playlists
            };

            try
            {
                using (var repo = new SpecialScheduleCacheRepository())
                {
                    repo.Upsert(cache);
                }

                var playerInfo = owner?.g_PlayerInfoManager?.g_PlayerInfo;
                EnqueueScheduleDownloads(playerInfo, playlists);
                ApplyWeeklySchedule(payload.WeeklySchedule, playerInfo);
                owner?.RequestScheduleEvaluation(force: true);
            }
            catch (Exception ex)
            {
                Logger.WriteErrorLog(ex.ToString(), Logger.GetLogFileName());
                return false;
            }

            return true;
        }

        internal void HandleWeeklyScheduleFromSignalR(SharedWeeklyPlayScheduleInfo schedule)
        {
            var playerInfo = owner?.g_PlayerInfoManager?.g_PlayerInfo;
            if (playerInfo == null)
            {
                return;
            }

            if (ApplyWeeklySchedule(schedule, playerInfo))
            {
                owner?.RequestScheduleEvaluation(force: true);
            }
        }

        private bool ApplyWeeklySchedule(SharedWeeklyPlayScheduleInfo schedule, PlayerInfoClass playerInfo)
        {
            if (schedule == null)
            {
                return false;
            }

            try
            {
                var playerId = playerInfo?.PIF_GUID;
                var playerName = playerInfo?.PIF_PlayerName;

                var localId = string.IsNullOrWhiteSpace(schedule.Id)
                    ? (string.IsNullOrWhiteSpace(playerId) ? playerName : playerId)
                    : schedule.Id;

                var local = new SharedWeeklyPlayScheduleInfo
                {
                    Id = localId ?? string.Empty,
                    PlayerID = string.IsNullOrWhiteSpace(schedule.PlayerID) ? playerId ?? playerName : schedule.PlayerID,
                    PlayerName = string.IsNullOrWhiteSpace(schedule.PlayerName) ? playerName ?? playerId : schedule.PlayerName,
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

                scheduleEvaluator?.InvalidateWeeklyCache();
                return true;
            }
            catch (Exception ex)
            {
                Logger.WriteErrorLog($"ApplyWeeklySchedule error: {ex}", Logger.GetLogFileName());
                return false;
            }
        }

        internal string CurrentCommand => currentCommand;

        internal bool IsHandlingCommand => Interlocked.CompareExchange(ref isHandlingCommand, 1, 1) == 1;

        internal bool TryGetHeartbeatUpdate(out string status, out int progress)
        {
            if (updateService == null)
            {
                status = string.Empty;
                progress = 0;
                return false;
            }

            return updateService.TryGetHeartbeatUpdate(out status, out progress);
        }

        internal void HandleCommandFromSignalR(string command)
        {
            if (string.IsNullOrWhiteSpace(command))
            {
                return;
            }

            var infoManager = owner?.g_PlayerInfoManager;
            if (infoManager == null)
            {
                return;
            }

            string normalized = command.Trim().ToLowerInvariant();
            HandleCommandCore(infoManager.g_PlayerInfo, normalized, null, false);
        }

        internal void HandleCommandFromSignalR(SignalRCommandEnvelope envelope)
        {
            if (envelope == null || string.IsNullOrWhiteSpace(envelope.Command))
            {
                return;
            }

            var infoManager = owner?.g_PlayerInfoManager;
            if (infoManager == null)
            {
                return;
            }

            string normalized = envelope.Command.Trim().ToLowerInvariant();
            var payload = DecodePayload(envelope.PayloadJson);
            bool handled = HandleCommandCore(infoManager.g_PlayerInfo, normalized, payload, envelope.IsUrgent);
            if (!string.IsNullOrWhiteSpace(envelope.CommandId))
            {
                string playerId = infoManager.g_PlayerInfo?.PIF_GUID;
                if (handled)
                {
                    commandQueueClient.MarkAck(envelope.CommandId, playerId);
                }
                else
                {
                    commandQueueClient.MarkFailed(envelope.CommandId, playerId);
                }
            }
        }

        internal void EnsurePlaylistDownloadFromCache(string playlistName)
        {
            var playerInfo = owner?.g_PlayerInfoManager?.g_PlayerInfo;
            if (playerInfo == null || string.IsNullOrWhiteSpace(playlistName))
            {
                return;
            }

            string cacheKey = string.IsNullOrWhiteSpace(playerInfo.PIF_GUID)
                ? playerInfo.PIF_PlayerName
                : playerInfo.PIF_GUID;
            if (string.IsNullOrWhiteSpace(cacheKey))
            {
                return;
            }

            using (var repo = new SpecialScheduleCacheRepository())
            {
                var cache = repo.FindById(cacheKey);
                var target = cache?.Playlists?
                    .FirstOrDefault(p => string.Equals(
                        string.IsNullOrWhiteSpace(p.PlaylistName) ? p.PageList?.PLI_PageListName : p.PlaylistName,
                        playlistName,
                        StringComparison.OrdinalIgnoreCase));
                if (target != null)
                {
                    EnqueueScheduleDownloads(playerInfo, new[] { target });
                }
            }
        }

        private void EnqueueScheduleDownloads(PlayerInfoClass playerInfo, IEnumerable<SchedulePlaylistPayload> playlists)
        {
            if (playerInfo == null || playlists == null)
            {
                return;
            }

            var distinct = playlists
                .Where(p => p != null && p.PageList != null && p.Pages != null && p.Pages.Count > 0)
                .GroupBy(p => string.IsNullOrWhiteSpace(p.PlaylistName) ? p.PageList.PLI_PageListName : p.PlaylistName, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First());

            foreach (var playlist in distinct)
            {
                var payload = new SharedUpdatePayload
                {
                    PageList = playlist.PageList,
                    Pages = playlist.Pages,
                    Contract = playlist.Contract
                };

                Logger.WriteLog($"스케줄 수신: 플레이리스트 다운로드 예약 -> {payload.PageList?.PLI_PageListName}", Logger.GetLogFileName());
                updateService.EnqueueFromSchedule(playerInfo, payload);
            }
        }
    }

    internal sealed class RethinkCommandClient
    {
        private const string DatabaseName = "AndoW";
        private const string PlayerTableName = "PlayerInfoManager";

        private static readonly RethinkDB R = RethinkDB.R;
        private readonly object syncRoot = new object();
        private Connection connection;
        private string host = "127.0.0.1";
        private int port = 28015;
        private string username = "admin";
        private string password = "turtle04!9";

        public RethinkCommandClient(string managerHost = "127.0.0.1")
        {
            if (!string.IsNullOrWhiteSpace(managerHost))
            {
                host = managerHost;
            }
        }

        public string FetchCommand(string playerGuid)
        {
            if (string.IsNullOrWhiteSpace(playerGuid))
            {
                return null;
            }

            try
            {
                var map = R.Db(DatabaseName)
                    .Table(PlayerTableName)
                    .Get(playerGuid)
                    .Pluck("command")
                    .RunAtom<Dictionary<string, object>>(GetConnection());

                if (map == null || !map.ContainsKey("command"))
                {
                    return null;
                }

                return map["command"]?.ToString();
            }
            catch (Exception ex)
            {
                Logger.WriteErrorLog(ex.ToString(), Logger.GetLogFileName());
                ResetConnection();
                return null;
            }
        }

        public void ClearCommand(string playerGuid)
        {
            if (string.IsNullOrWhiteSpace(playerGuid))
            {
                return;
            }

            try
            {
                R.Db(DatabaseName)
                    .Table(PlayerTableName)
                    .Get(playerGuid)
                    .Update(new { command = string.Empty })
                    .Run(GetConnection());
            }
            catch (Exception ex)
            {
                Logger.WriteErrorLog(ex.ToString(), Logger.GetLogFileName());
                ResetConnection();
            }
        }

        public PlayerInfoClass FetchPlayerByName(string playerName)
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
                    .Table(PlayerTableName)
                    .Filter(row => row["PIF_PlayerName"].Downcase().Eq(lowered))
                    .Limit(1);

                var cursor = query.RunCursor<PlayerInfoClass>(conn);
                if (cursor == null)
                {
                    return null;
                }

                return cursor.FirstOrDefault();
            }
            catch (Exception ex)
            {
                TurtleTools.Logger.WriteErrorLog(ex.ToString(), TurtleTools.Logger.GetLogFileName());
                return null;
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
                    .Connect();

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
            }
        }
    }
}
