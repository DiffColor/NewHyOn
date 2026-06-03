using AndoW.Shared;
using Newtonsoft.Json;
using SharedUpdatePayload = AndoW.Shared.UpdatePayload;
using NewHyOnPlayer.DataManager;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using TurtleTools;
using NewHyOnPlayer.Services;
using SharedWeeklyPlayScheduleInfo = AndoW.Shared.WeeklyPlayScheduleInfo;

namespace NewHyOnPlayer
{
    internal sealed class RemoteCommandService : IDisposable
    {
        private readonly MainWindow owner;
        private readonly CommandQueueClient commandQueueClient;
        private readonly MultimediaTimer.Timer timer;
        private readonly UpdateService updateService;
        private readonly CommandHistoryClient historyClient;
        private readonly ScheduleEvaluator scheduleEvaluator;
        private int isExecuting;
        private int isHandlingCommand;
        private string currentCommand = string.Empty;
        private readonly int intervalMs;

        public RemoteCommandService(MainWindow owner, double intervalMs = 5000)
        {
            this.owner = owner;
            this.intervalMs = (int)Math.Max(1, intervalMs);
            var managerHost = owner?.g_LocalSettingsManager?.Settings?.ManagerIP;
            commandQueueClient = new CommandQueueClient(managerHost);
            historyClient = new CommandHistoryClient(string.IsNullOrWhiteSpace(managerHost) ? "127.0.0.1" : managerHost);
            updateService = new UpdateService(owner);
            scheduleEvaluator = new ScheduleEvaluator(owner?.g_PlayerInfoManager);
            ThreadPool.QueueUserWorkItem(_ => historyClient.PurgeOlderThanDays(30));
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
        }

        private void ScheduleNext()
        {
            if (timer.IsRunning)
            {
                timer.Stop();
                timer.Period = intervalMs;
                timer.Start();
            }
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
                commandQueueClient.MarkFailed(entry.Id, playerInfo.PIF_GUID);
                return;
            }

            bool handled = false;
            string playerId = string.Empty;

            var payload = DecodePayload(entry.PayloadBase64);
            handled = HandleCommandCore(playerInfo, command, payload, isUrgent);
            playerId = playerInfo.PIF_GUID;
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

            string normalizedCommand = command.Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(normalizedCommand))
            {
                return false;
            }

            Interlocked.Exchange(ref isHandlingCommand, 1);
            currentCommand = normalizedCommand;
            try
            {
                string playerGuid = playerInfo.PIF_GUID;

                switch (normalizedCommand)
                {
                    case "updatelist":
                        if (!HasUsableUpdatePayload(payloadData))
                        {
                            Logger.WriteLog("updatelist command missing or invalid payload.", Logger.GetLogFileName());
                            return false;
                        }

                        if (HasActiveQueue())
                        {
                            updateService.CancelActiveQueues("Cancelled due to new updatelist command");
                        }

                        return updateService.RunUpdateAsync(playerInfo, payloadData, isUrgent);
                    case "updateschedule":
                        {
                            string historyId = RecordCommandQueued(playerGuid, normalizedCommand);
                            if (payloadData?.Schedule == null)
                            {
                                Logger.WriteLog("updateschedule payload missing or invalid.", Logger.GetLogFileName());
                                RecordCommandDone(historyId, playerGuid, CommandHistoryStatus.Failed, "SCHEDULE_PAYLOAD", "payload missing");
                                return false;
                            }

                            bool applied = ApplySchedulePayload(payloadData.Schedule);
                            if (!applied)
                            {
                                Logger.WriteLog("updateschedule payload apply failed.", Logger.GetLogFileName());
                                RecordCommandDone(historyId, playerGuid, CommandHistoryStatus.Failed, "SCHEDULE_PAYLOAD", "payload apply failed");
                                return false;
                            }

                            RecordCommandDone(historyId, playerGuid, CommandHistoryStatus.Done, null, null);
                            return true;
                        }
                    case "updateweekly":
                        {
                            string historyId = RecordCommandQueued(playerGuid, normalizedCommand);
                            bool applied = payloadData != null && ApplyWeeklySchedule(payloadData.Schedule?.WeeklySchedule, playerInfo);
                            if (applied)
                            {
                                owner?.HandleWeeklyScheduleUpdated();
                                RecordCommandDone(historyId, playerGuid, CommandHistoryStatus.Done, null, null);
                                return true;
                            }

                            Logger.WriteLog("updateweekly payload missing or invalid.", Logger.GetLogFileName());
                            RecordCommandDone(historyId, playerGuid, CommandHistoryStatus.Failed, "WEEKLY_PAYLOAD", "payload missing");
                            return false;
                        }
                    case "updatecontentperiod":
                        owner?.SyncSpecificContentPeriodsNow(ParseContentPeriodUpdateIds(payloadData), refreshPlayback: true);
                        return true;
                    case "reboot":
                        {
                            string historyId = RecordCommandQueued(playerGuid, normalizedCommand);
                            ProcessTools.ExecuteCommand("shutdown /r /t 0");
                            RecordCommandDone(historyId, playerGuid, CommandHistoryStatus.Done, null, null);
                            return true;
                        }
                    case "poweroff":
                        {
                            string historyId = RecordCommandQueued(playerGuid, normalizedCommand);
                            ProcessTools.ExecuteCommand("shutdown /s /t 0");
                            RecordCommandDone(historyId, playerGuid, CommandHistoryStatus.Done, null, null);
                            return true;
                        }
                    case "clearqueue":
                        {
                            string historyId = RecordCommandQueued(playerGuid, normalizedCommand);
                            int cancelled = updateService.CancelActiveQueues("Cancelled via command");
                            Logger.WriteLog($"clearqueue command received, cancelled={cancelled}", Logger.GetLogFileName());
                            string status = cancelled > 0 ? CommandHistoryStatus.Cancelled : CommandHistoryStatus.Done;
                            RecordCommandDone(historyId, playerGuid, status, null, $"cancelled={cancelled}");
                            return true;
                        }
                    case "check":
                        {
                            string historyId = RecordCommandQueued(playerGuid, normalizedCommand);
                            owner?.SendHeartbeatNow();
                            RecordCommandDone(historyId, playerGuid, CommandHistoryStatus.Done, null, null);
                            return true;
                        }
                    case "sync":
                        return HandleSyncCommand(playerGuid, normalizedCommand);
                    case "upgrade":
                        return RecordUnsupportedCommand(playerGuid, normalizedCommand);
                    case "getmac":
                        return HandleGetMacCommand(playerGuid, normalizedCommand);
                    default:
                        {
                            string historyId = RecordCommandQueued(playerGuid, normalizedCommand);
                            Logger.WriteLog($"Unknown remote command: {normalizedCommand}", Logger.GetLogFileName());
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

        private bool RecordUnsupportedCommand(string playerGuid, string command)
        {
            string historyId = RecordCommandQueued(playerGuid, command);
            Logger.WriteLog($"Unsupported remote command on Windows player: {command}", Logger.GetLogFileName());
            RecordCommandDone(historyId, playerGuid, CommandHistoryStatus.Failed, "UNSUPPORTED_COMMAND", null);
            return false;
        }

        private bool HandleSyncCommand(string playerGuid, string command)
        {
            string historyId = RecordCommandQueued(playerGuid, command);
            bool requested = owner?.RequestPlaybackSyncNow() ?? false;
            if (!requested)
            {
                Logger.WriteLog("sync command ignored: sync playback is inactive or not ready.", Logger.GetLogFileName());
                RecordCommandDone(historyId, playerGuid, CommandHistoryStatus.Failed, "SYNC_INACTIVE", null);
                return false;
            }

            RecordCommandDone(historyId, playerGuid, CommandHistoryStatus.Done, null, null);
            return true;
        }

        private bool HandleGetMacCommand(string playerGuid, string command)
        {
            string historyId = RecordCommandQueued(playerGuid, command);
            owner?.RequestPlayerGuidSyncNow();
            owner?.SendHeartbeatNow();
            RecordCommandDone(historyId, playerGuid, CommandHistoryStatus.Done, null, null);
            return true;
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

        private static bool HasUsableUpdatePayload(SharedUpdatePayload payloadData)
        {
            return payloadData != null
                   && payloadData.PageList != null
                   && payloadData.Pages != null
                   && payloadData.Pages.Count > 0;
        }

        private static IReadOnlyCollection<string> ParseContentPeriodUpdateIds(SharedUpdatePayload payload)
        {
            var items = payload?.ContentPeriodUpdateGuids;
            if (items == null || items.Count == 0)
            {
                return Array.Empty<string>();
            }

            return items
                .Where(x => string.IsNullOrWhiteSpace(x) == false)
                .Select(x => x.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
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
                            || q.Status == UpdateQueueStatus.Downloaded
                            || q.Status == UpdateQueueStatus.Validating
                            || q.Status == UpdateQueueStatus.Ready
                            || q.Status == UpdateQueueStatus.Applying)
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
                owner?.HandleWeeklyScheduleUpdated();
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

                var resolvedPlayerId = string.IsNullOrWhiteSpace(schedule.PlayerID) ? playerId ?? playerName : schedule.PlayerID;
                if (string.IsNullOrWhiteSpace(localId) || string.IsNullOrWhiteSpace(resolvedPlayerId))
                {
                    Logger.WriteErrorLog("ApplyWeeklySchedule skipped: missing id/playerId.", Logger.GetLogFileName());
                    return false;
                }

                var local = new SharedWeeklyPlayScheduleInfo
                {
                    Id = localId ?? string.Empty,
                    PlayerID = resolvedPlayerId,
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
            if (string.Equals(normalized, "authkey", StringComparison.OrdinalIgnoreCase))
            {
                Logger.WriteLog("authkey command received without payload.", Logger.GetLogFileName());
                return;
            }
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

            bool handled = false;
            string playerId = string.Empty;

            string normalized = envelope.Command.Trim().ToLowerInvariant();
            var payload = DecodePayload(envelope.PayloadJson);
            handled = HandleCommandCore(infoManager.g_PlayerInfo, normalized, payload, envelope.IsUrgent);
            if (!string.IsNullOrWhiteSpace(envelope.CommandId))
            {
                playerId = infoManager.g_PlayerInfo?.PIF_GUID;
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
}
