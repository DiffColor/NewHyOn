using AndoW.Shared;
using SharedUpdatePayload = AndoW.Shared.UpdatePayload;
using FluentFTP;
using NewHyOnPlayer.DataManager;
using Newtonsoft.Json;
using RethinkDb.Driver;
using RethinkDb.Driver.Net;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TurtleTools;

namespace NewHyOnPlayer
{
    internal sealed class UpdateService : IDisposable
    {
        private const int FTP_PORT = 10021;
        private const string FTP_LOGIN = "asdf";
        private const string FTP_PASSWORD = "Emfndhk!";
        private const int FtpTransferBufferSizeBytes = 2 * 1024 * 1024;

        private readonly MainWindow owner;
        private readonly object syncRoot = new object();
        private readonly object heartbeatSync = new object();
        private int isRunning;
        private int isProcessingTick;
        private readonly UpdateQueueRepository queueRepository = new UpdateQueueRepository();
        private readonly Timer processorTimer;
        private const int ProcessorIntervalMs = 5000;
        private bool disposed;
        private static readonly TimeSpan StaleChunkThreshold = TimeSpan.FromMinutes(2);
        private int isUpdateRequested;
        private readonly string managerHost;
        private readonly ServerSettingsClient serverSettingsClient;
        private const string RethinkDbName = "NewHyOn";
        private const string UpdateQueueTableName = "UpdateQueue";
        private const string RethinkUser = "admin";
        private const string RethinkPassword = "turtle04!9";
        private readonly CommandHistoryClient historyClient;
        private readonly RethinkDB R = RethinkDB.R;
        private readonly UpdateQueueRethinkClient queueRethinkClient;
        private readonly UpdateLeaseClient leaseClient;
        private readonly UpdateThrottleSettingsClient throttleSettingsClient;
        private UpdateLeaseEntry activeLease;
        private DateTime nextLeaseRenewAt;
        private readonly object leaseLock = new object();
        private int urgentUpdateInProgress;
        private string heartbeatStatus = string.Empty;
        private int heartbeatProgress;
        private int forceRefreshFtpSettings;
        private static readonly TimeSpan ProgressReportInterval = TimeSpan.FromMilliseconds(500);
        private long lastProgressReportTicks;
        private int lastProgressReportPercent;
        private long updateHeartbeatSessionId;
        private bool updateHeartbeatReportingActive;

        public UpdateService(MainWindow owner)
        {
            this.owner = owner;
            managerHost = owner?.g_LocalSettingsManager?.Settings?.ManagerIP;
            serverSettingsClient = new ServerSettingsClient(managerHost);
            historyClient = new CommandHistoryClient(string.IsNullOrWhiteSpace(managerHost) ? "127.0.0.1" : managerHost);
            queueRethinkClient = new UpdateQueueRethinkClient(string.IsNullOrWhiteSpace(managerHost) ? "127.0.0.1" : managerHost);
            leaseClient = new UpdateLeaseClient(string.IsNullOrWhiteSpace(managerHost) ? "127.0.0.1" : managerHost);
            throttleSettingsClient = new UpdateThrottleSettingsClient(string.IsNullOrWhiteSpace(managerHost) ? "127.0.0.1" : managerHost);
            processorTimer = new Timer(ProcessorTick, null, 0, ProcessorIntervalMs);
            StartBackgroundInitialization();
        }

        private void StartBackgroundInitialization()
        {
            ThreadPool.QueueUserWorkItem(_ =>
            {
                if (disposed)
                {
                    return;
                }

                try
                {
                    var selfId = owner?.g_PlayerInfoManager?.g_PlayerInfo?.PIF_GUID;
                    if (!string.IsNullOrWhiteSpace(selfId))
                    {
                        leaseClient?.ReleaseByPlayer(selfId);
                    }
                }
                catch
                {
                }

                if (disposed)
                {
                    return;
                }

                try
                {
                    EnsureUpdateQueueTable(managerHost);
                }
                catch
                {
                }

                if (disposed)
                {
                    return;
                }

                try
                {
                    RecoverStalledQueues();
                }
                catch
                {
                }

                if (disposed)
                {
                    return;
                }

                try
                {
                    ProcessQueue();
                }
                catch
                {
                }
            });
        }

        public bool IsRunning => Interlocked.CompareExchange(ref isRunning, 1, 1) == 1;

        internal bool TryGetHeartbeatUpdate(out string status, out int progress)
        {
            lock (heartbeatSync)
            {
                status = heartbeatStatus;
                progress = heartbeatProgress;
                return !string.IsNullOrWhiteSpace(status);
            }
        }

        public void Dispose()
        {
            disposed = true;
            processorTimer?.Change(Timeout.Infinite, Timeout.Infinite);
            processorTimer?.Dispose();
            queueRethinkClient?.Dispose();
            ReleaseLease();
            throttleSettingsClient?.Dispose();
            leaseClient?.Dispose();
            serverSettingsClient?.Dispose();
        }

        public void RunUpdateAsync(PlayerInfoClass player)
        {
            RunUpdateAsync(player, null, false);
        }

        public void RunUpdateAsync(PlayerInfoClass player, SharedUpdatePayload payloadData, bool isUrgent)
        {
            if (player == null) return;

            if (isUrgent)
            {
                if (Interlocked.Exchange(ref urgentUpdateInProgress, 1) == 1)
                {
                    Logger.WriteLog("UpdateService: urgent update already running, skip.", Logger.GetLogFileName());
                    return;
                }
            }
            else
            {
                if (Interlocked.Exchange(ref isUpdateRequested, 1) == 1)
                {
                    Logger.WriteLog("UpdateService: update already requested, skip.", Logger.GetLogFileName());
                    return;
                }
            }

            Task.Run(() =>
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(managerHost))
                    {
                        Logger.WriteLog("UpdateService: manager host is empty; cannot proceed update.", Logger.GetLogFileName());
                        return;
                    }

                    if (!isUrgent && HasActiveQueue(player.PIF_GUID))
                    {
                        Logger.WriteLog($"UpdateService: active queue exists for player {player.PIF_GUID}, skip enqueue.", Logger.GetLogFileName());
                        return;
                    }

                    if (isUrgent)
                    {
                        RunUrgentUpdate(payloadData, player);
                        return;
                    }

                    EnqueueAndProcess(player, managerHost, payloadData);
                }
                finally
                {
                    if (isUrgent)
                    {
                        Interlocked.Exchange(ref urgentUpdateInProgress, 0);
                    }
                    else
                    {
                        Interlocked.Exchange(ref isUpdateRequested, 0);
                    }
                }
            });
        }

        public void EnqueueFromSchedule(PlayerInfoClass player, SharedUpdatePayload payloadData)
        {
            if (player == null || payloadData == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(managerHost))
            {
                Logger.WriteLog("UpdateService: manager host is empty; cannot enqueue schedule payload.", Logger.GetLogFileName());
                return;
            }

            Task.Run(() =>
            {
                try
                {
                    EnqueueAndProcess(player, managerHost, payloadData, true);
                }
                catch (Exception ex)
                {
                    Logger.WriteErrorLog($"UpdateService: schedule enqueue failed. {ex}", Logger.GetLogFileName());
                }
            });
        }

        private void EnqueueAndProcess(PlayerInfoClass player, string managerHost, SharedUpdatePayload payloadData, bool isSchedulePayload = false)
        {
            try
            {
                var queue = BuildQueueFromPayload(player, payloadData, isSchedulePayload);
                if (queue == null)
                {
                    Logger.WriteLog("UpdateService: payload invalid, skip enqueue.", Logger.GetLogFileName());
                    return;
                }

                // 同一 플레이어/플레이리스트/페이로드가 기존에 대기 중이면 중복 적재 방지
                var existing = queueRepository.LoadAll() ?? new List<UpdateQueue>();
                bool duplicate = existing.Any(q =>
                    q != null
                    && q.IsScheduleQueue == isSchedulePayload
                    && (q.Status == UpdateQueueStatus.Queued
                        || q.Status == UpdateQueueStatus.Downloading
                        || q.Status == UpdateQueueStatus.Downloaded
                        || q.Status == UpdateQueueStatus.Validating
                        || q.Status == UpdateQueueStatus.Ready
                        || q.Status == UpdateQueueStatus.Applying)
                    && string.Equals(q.PlayerId, queue.PlayerId, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(q.PlaylistId, queue.PlaylistId, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(q.PayloadJson ?? string.Empty, queue.PayloadJson ?? string.Empty, StringComparison.Ordinal));
                if (duplicate)
                {
                    Logger.WriteLog("UpdateService: duplicate schedule payload detected, skip enqueue.", Logger.GetLogFileName());
                    return;
                }

                queueRepository.Upsert(queue);
                Logger.WriteLog("UpdateService: enqueue update.", Logger.GetLogFileName());
                SyncQueueToRethink(queue);

                historyClient.UpsertQueued(queue.Id, queue.PlayerId, queue.PlayerName, "updatelist");
                ProcessQueue();
                return;

                var rethinkClient = new RethinkContentClient(managerHost);
                // 1) 메타 조회
                var pageList = rethinkClient.FetchPageList(player.PIF_CurrentPlayList, player.PIF_PlayerName);
                if (pageList == null || pageList.PLI_Pages == null || pageList.PLI_Pages.Count == 0)
                {
                    Logger.WriteLog("UpdateService: PageList not found.", Logger.GetLogFileName());
                    return;
                }

                var pages = rethinkClient.FetchPages(pageList.PLI_Pages);
                if (pages == null || pages.Count == 0)
                {
                    Logger.WriteLog("UpdateService: Pages not found.", Logger.GetLogFileName());
                    return;
                }

                // 2) 다운로드 리스트 작성
                var downloads = BuildDownloadList(pages);

                // 큐에 적재
                var contract = BuildContractPayload(player, pageList, pages);
                var payload = new UpdatePayload { PageList = pageList, Pages = pages, Contract = contract };
                var now = DateTime.Now;
                long nowTicks = now.Ticks;
                string queueId = BuildQueueId(player.PIF_GUID, nowTicks);
                var payloadQueue = new UpdateQueue
                {
                    Id = queueId,
                    PlayerId = player.PIF_GUID,
                    PlayerName = player.PIF_PlayerName,
                    PlaylistId = pageList.PLI_PageListName,
                    PayloadJson = JsonConvert.SerializeObject(payload),
                    DownloadJson = JsonConvert.SerializeObject(downloads),
                    Status = UpdateQueueStatus.Queued,
                    RetryCount = 0,
                    NextAttempt = new DateTime(nowTicks, DateTimeKind.Local),
                    CreatedTicks = nowTicks,
                    HistoryId = queueId
                };
                queueRepository.Upsert(payloadQueue);
                Logger.WriteLog("UpdateService: enqueue update.", Logger.GetLogFileName());
                SyncQueueToRethink(payloadQueue);

                historyClient.UpsertQueued(payloadQueue.Id, payloadQueue.PlayerId, payloadQueue.PlayerName, "updatelist");
                ProcessQueue();
            }
            catch (Exception ex)
            {
                Logger.WriteErrorLog($"UpdateService error: {ex}", Logger.GetLogFileName());
            }
        }

        private void RunUrgentUpdate(SharedUpdatePayload payloadData, PlayerInfoClass player)
        {
            try
            {
                if (payloadData == null)
                {
                    Logger.WriteLog("UpdateService: urgent payload missing.", Logger.GetLogFileName());
                    return;
                }

                ReleaseLease();

                var queue = BuildQueueFromPayload(player, payloadData, false);
                if (queue == null)
                {
                    Logger.WriteLog("UpdateService: urgent payload invalid.", Logger.GetLogFileName());
                    return;
                }

                queueRepository.Upsert(queue);
                SyncQueueToRethink(queue);
                historyClient.UpsertQueued(queue.Id, queue.PlayerId, queue.PlayerName, "updatelist");

                lock (syncRoot)
                {
                    ExecuteQueue(queue, true);
                }
            }
            catch (Exception ex)
            {
                Logger.WriteErrorLog($"UpdateService urgent error: {ex}", Logger.GetLogFileName());
            }
        }

        private UpdateQueue BuildQueueFromPayload(PlayerInfoClass player, SharedUpdatePayload payloadData, bool isSchedulePayload)
        {
            if (player == null)
            {
                return null;
            }

            if (string.IsNullOrWhiteSpace(player.PIF_GUID))
            {
                Logger.WriteLog("UpdateService: player GUID empty.", Logger.GetLogFileName());
                return null;
            }

            if (payloadData == null || payloadData.PageList == null || payloadData.Pages == null || payloadData.Pages.Count == 0)
            {
                return null;
            }

            var localPages = ConvertPages(payloadData.Pages);
            var downloads = BuildDownloadList(localPages);
            var now = DateTime.Now;
            long nowTicks = now.Ticks;
            string queueId = BuildQueueId(player.PIF_GUID, nowTicks);

            return new UpdateQueue
            {
                Id = queueId,
                PlayerId = player.PIF_GUID,
                PlayerName = player.PIF_PlayerName,
                PlaylistId = payloadData.PageList.PLI_PageListName,
                PayloadJson = UpdatePayloadCodec.Encode(payloadData),
                DownloadJson = JsonConvert.SerializeObject(downloads),
                Status = UpdateQueueStatus.Queued,
                RetryCount = 0,
                NextAttempt = new DateTime(nowTicks, DateTimeKind.Local),
                CreatedTicks = nowTicks,
                HistoryId = queueId,
                IsScheduleQueue = isSchedulePayload
            };
        }

        private PageListInfoClass ConvertPageList(AndoW.Shared.PageListInfoClass source)
        {
            if (source == null)
            {
                return null;
            }

            var local = new PageListInfoClass();
            local.CopyData(source);
            return local;
        }

        private List<PageInfoClass> ConvertPages(IEnumerable<AndoW.Shared.PageInfoClass> pages)
        {
            var list = new List<PageInfoClass>();
            if (pages == null)
            {
                return list;
            }

            foreach (var page in pages)
            {
                if (page == null)
                {
                    continue;
                }

                var local = new PageInfoClass();
                local.CopyData(page);
                list.Add(local);
            }

            return list;
        }

        private SharedUpdatePayload DecodePayload(string payloadJson)
        {
            if (string.IsNullOrWhiteSpace(payloadJson))
            {
                return null;
            }

            var payload = UpdatePayloadCodec.Decode(payloadJson);
            if (payload != null)
            {
                return payload;
            }

            try
            {
                return JsonConvert.DeserializeObject<SharedUpdatePayload>(payloadJson);
            }
            catch
            {
                return null;
            }
        }

        private void ProcessQueue()
        {
            if (disposed) return;
            if (IsRunning) return;
            lock (syncRoot)
            {
                var queues = queueRepository.LoadAll();
                var pending = queues
                    .Where(q => q.Status == UpdateQueueStatus.Queued
                             || q.Status == UpdateQueueStatus.Downloading
                             || q.Status == UpdateQueueStatus.Downloaded
                             || q.Status == UpdateQueueStatus.Validating
                             || q.Status == UpdateQueueStatus.Ready
                             || q.Status == UpdateQueueStatus.Applying
                             || (q.Status == UpdateQueueStatus.Failed && q.NextAttempt <= DateTime.Now))
                    .OrderBy(q => q.CreatedTicks)
                    .ToList();

                if (pending.Count == 0)
                {
                    return;
                }

                if (Interlocked.Exchange(ref isRunning, 1) == 1)
                {
                    return;
                }

                try
                {
                    foreach (var q in pending)
                    {
                        ExecuteQueue(q, false);
                    }
                }
                catch (Exception ex)
                {
                    Logger.WriteErrorLog($"UpdateService queue error: {ex}", Logger.GetLogFileName());
                }
                finally
                {
                    Interlocked.Exchange(ref isRunning, 0);
                }
            }
        }

        private void ExecuteQueue(UpdateQueue queue, bool ignoreLease)
        {
            if (queue == null)
            {
                return;
            }

            bool ignoreLeaseForThisRun = ignoreLease;
            if (!ignoreLease && queue.RetryCount >= 3 && !string.IsNullOrWhiteSpace(queue.LastError)
                && queue.LastError.StartsWith("LEASE", StringComparison.OrdinalIgnoreCase))
            {
                ignoreLeaseForThisRun = true; // lease가 계속 busy면 일정 횟수 후 무시하고 진행
            }

            if (queue.Status == UpdateQueueStatus.Ready || queue.Status == UpdateQueueStatus.Applying)
            {
                if (!ignoreLease)
                {
                    ReleaseLeaseIfOwner(queue.Id);
                }
                ApplyQueue(queue);
                return;
            }

            // NextAttempt 도래 전이면 스킵
            if (queue.NextAttempt > DateTime.Now)
            {
                return;
            }

            var downloadEntries = JsonConvert.DeserializeObject<List<DownloadEntry>>(queue.DownloadJson ?? "[]") ?? new List<DownloadEntry>();
            bool hasDownloads = downloadEntries.Count > 0;
            var payload = DecodePayload(queue.PayloadJson);
            if (payload == null || payload.PageList == null || payload.Pages == null || payload.Pages.Count == 0)
            {
                queue.Status = UpdateQueueStatus.Failed;
                queue.RetryCount++;
                queue.NextAttempt = DateTime.Now.Add(RetryPolicy(queue.RetryCount));
                queue.LastError = "Invalid payload";
                queueRepository.Upsert(queue);
                LogStatus(queue, "Payload invalid, marked FAILED");
                SendQueueStatus(queue);
                SyncQueueToRethink(queue);
                DeleteRemoteQueueRecord(queue.PlayerId, queue.Id, managerHost);
                RecordHistoryDone(queue.HistoryId, CommandHistoryStatus.Failed, "INVALID_PAYLOAD", null);
                if (!ignoreLease)
                {
                    ReleaseLeaseIfOwner(queue.Id);
                }
                return;
            }

            if (!ignoreLeaseForThisRun && hasDownloads && (queue.Status == UpdateQueueStatus.Queued || queue.Status == UpdateQueueStatus.Downloading))
            {
                var settings = GetThrottleSettings();
                if (!EnsureDownloadLease(queue, settings))
                {
                    ScheduleLeaseRetry(queue, settings, "LEASE_BUSY");
                    return;
                }
            }

            if (queue.Status == UpdateQueueStatus.Queued)
            {
                queue.Status = UpdateQueueStatus.Downloading;
                queue.LastError = string.Empty;
                queueRepository.Upsert(queue);
                LogStatus(queue, "Downloading started");
                SendQueueStatus(queue);
                SyncQueueToRethink(queue);
                RecordHistoryInProgress(queue.HistoryId, queue.Id);
            }

            if (queue.Status == UpdateQueueStatus.Downloading || queue.Status == UpdateQueueStatus.Queued)
            {
                bool downloaded = DownloadAll(managerHost ?? string.Empty, queue, downloadEntries, ignoreLeaseForThisRun);
                if (!downloaded)
                {
                    if (!ignoreLeaseForThisRun && !string.IsNullOrWhiteSpace(queue.LastError)
                        && queue.LastError.StartsWith("LEASE", StringComparison.OrdinalIgnoreCase))
                    {
                        return;
                    }

                    queue.Status = UpdateQueueStatus.Queued;
                    queue.RetryCount++;
                    queue.NextAttempt = DateTime.Now.Add(RetryPolicy(queue.RetryCount));
                    queue.DownloadProgress = CalculateDownloadProgress(downloadEntries);
                    queueRepository.Upsert(queue);
                    LogStatus(queue, "Downloading failed, will retry");
                    SendQueueStatus(queue);
                    SyncQueueToRethink(queue);
                    if (!ignoreLease)
                    {
                        ReleaseLeaseIfOwner(queue.Id);
                    }
                    return;
                }

                queue.Status = UpdateQueueStatus.Downloaded;
                queue.LastError = string.Empty;
                queueRepository.Upsert(queue);
                LogStatus(queue, "Downloaded all");
                SendQueueStatus(queue);
                SyncQueueToRethink(queue);
                if (!ignoreLeaseForThisRun)
                {
                    ReleaseLeaseIfOwner(queue.Id);
                }
            }

            if (queue.Status == UpdateQueueStatus.Downloaded || queue.Status == UpdateQueueStatus.Validating)
            {
                queue.Status = UpdateQueueStatus.Validating;
                queue.LastError = string.Empty;
                queueRepository.Upsert(queue);
                LogStatus(queue, "Validating started");
                SendQueueStatus(queue);
                SyncQueueToRethink(queue);
            }

            bool valid = ValidateFiles(queue, downloadEntries);
            if (!valid)
            {
                queue.Status = UpdateQueueStatus.Queued;
                queue.RetryCount++;
                queue.NextAttempt = DateTime.Now.Add(RetryPolicy(queue.RetryCount));
                queue.DownloadProgress = CalculateDownloadProgress(downloadEntries);
                queueRepository.Upsert(queue);
                LogStatus(queue, "Validating failed, will retry");
                SendQueueStatus(queue);
                SyncQueueToRethink(queue);
                return;
            }

            queue.Status = UpdateQueueStatus.Ready;
            queue.DownloadProgress = 1.0;
            queue.ValidateProgress = 1.0;
            queue.LastError = string.Empty;
            queueRepository.Upsert(queue);
            LogStatus(queue, "Validation passed, READY -> APPLY");
            SendQueueStatus(queue);
            SyncQueueToRethink(queue);

            // 데이터는 즉시 LiteDB에 반영하고, 재생 전환은 PageEnd/ContentEnd에서 수행된다.
            ApplyQueue(queue);
        }

        private void ApplyQueue(UpdateQueue queue)
        {
            if (queue == null)
            {
                return;
            }

            ReleaseLeaseIfOwner(queue.Id);

            var downloadEntries = JsonConvert.DeserializeObject<List<DownloadEntry>>(queue.DownloadJson ?? "[]") ?? new List<DownloadEntry>();

            var payload = DecodePayload(queue.PayloadJson);
            if (payload == null || payload.PageList == null || payload.Pages == null || payload.Pages.Count == 0)
            {
                queue.Status = UpdateQueueStatus.Failed;
                queue.RetryCount++;
                queue.NextAttempt = DateTime.Now.Add(RetryPolicy(queue.RetryCount));
                queue.LastError = "Invalid payload";
                queueRepository.Upsert(queue);
                LogStatus(queue, "Apply failed: invalid payload");
                SendQueueStatus(queue);
                SyncQueueToRethink(queue);
                RecordHistoryDone(queue.HistoryId, CommandHistoryStatus.Failed, "INVALID_PAYLOAD", null);
                ReleasePlayerLease(queue.PlayerId);
                DeleteRemoteQueueRecord(queue.PlayerId, queue.Id, managerHost);
                queueRepository.DeleteById(queue.Id);
                return;
            }

            var localPageList = ConvertPageList(payload.PageList);
            var localPages = ConvertPages(payload.Pages);

            queue.Status = UpdateQueueStatus.Applying;
            queue.LastError = string.Empty;
            queueRepository.Upsert(queue);
            LogStatus(queue, "Applying started");
            SendQueueStatus(queue);
            SyncQueueToRethink(queue);

            // 백업 후 적용
            var backupPageList = new PageListRepository().LoadAll();
            var backupPages = new PageRepository().LoadAll();
            try
            {
                ApplyToLiteDb(localPageList, localPages);
                SaveContentPeriods(payload.ContentPeriods);
            }
            catch (Exception ex)
            {
                Logger.WriteErrorLog($"UpdateService apply error: {ex}", Logger.GetLogFileName());
                // 롤백
                var plRepo = new PageListRepository();
                var pRepo = new PageRepository();
                plRepo.ReplaceAll(backupPageList);
                pRepo.ReplaceAll(backupPages);
                queue.Status = UpdateQueueStatus.Failed;
                queue.RetryCount++;
                queue.NextAttempt = DateTime.Now.Add(RetryPolicy(queue.RetryCount));
                queue.LastError = ex.Message;
                queueRepository.Upsert(queue);
                CleanupTemp(downloadEntries);
                LogStatus(queue, "Apply failed, rolled back");
                SendQueueStatus(queue);
                SyncQueueToRethink(queue);
                DeleteRemoteQueueRecord(queue.PlayerId, queue.Id, managerHost);
                ReleasePlayerLease(queue.PlayerId);
                RecordHistoryDone(queue.HistoryId, CommandHistoryStatus.Failed, "APPLY_FAIL", ex.Message);
                return;
            }

            // 사용되지 않는 파일 정리
            CleanupUnusedFiles(localPages);
            CleanupTemp(downloadEntries);

            owner?.Dispatcher.BeginInvoke(new Action(() =>
            {
                var playerInfoManager = owner.g_PlayerInfoManager;
                if (playerInfoManager?.g_PlayerInfo != null && !string.IsNullOrWhiteSpace(payload.PageList?.PLI_PageListName))
                {
                    if (!queue.IsScheduleQueue)
                    {
                        playerInfoManager.g_PlayerInfo.PIF_CurrentPlayList = payload.PageList.PLI_PageListName;
                        playerInfoManager.g_PlayerInfo.PIF_DefaultPlayList = payload.PageList.PLI_PageListName;
                        playerInfoManager.SaveData();
                        owner?.RequestPlaylistReload(payload.PageList.PLI_PageListName, "schedule-update");
                    }
                    else
                    {
                        // 스케줄 큐는 데이터만 반영하고 전환은 PageEnd/ContentEnd에서 처리한다.
                        playerInfoManager.SaveData();
                    }
                }
            }));

            queue.Status = UpdateQueueStatus.Done;
            queue.LastError = string.Empty;
            queueRepository.Upsert(queue);
            SendQueueStatus(queue);
            SyncQueueToRethink(queue);
            DeleteRemoteQueueRecord(queue.PlayerId, queue.Id, managerHost);
            ReleasePlayerLease(queue.PlayerId);
            RecordHistoryDone(queue.HistoryId, CommandHistoryStatus.Done, null, null);

            Logger.WriteLog("UpdateService: update applied successfully.", Logger.GetLogFileName());
            LogStatus(queue, "Apply succeeded");
            queueRepository.DeleteById(queue.Id);
        }

        private void SaveContentPeriods(IEnumerable<ContentPeriodPayload> periods)
        {
            if (periods == null)
            {
                return;
            }

            var list = new List<ContentPeriodPayload>();
            foreach (var period in periods)
            {
                if (period == null || string.IsNullOrWhiteSpace(period.ContentGuid))
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(period.StartDate))
                {
                    period.StartDate = DateTime.Today.ToString("yyyy-MM-dd");
                }

                if (string.IsNullOrWhiteSpace(period.EndDate))
                {
                    period.EndDate = "2099-12-31";
                }

                if (DateTime.TryParse(period.StartDate, out var start) && DateTime.TryParse(period.EndDate, out var end))
                {
                    if (end.Date < start.Date)
                    {
                        period.EndDate = start.ToString("yyyy-MM-dd");
                    }
                }

                list.Add(period);
            }

            if (list.Count == 0)
            {
                return;
            }

            using (var repo = new ContentPeriodRepository())
            {
                repo.UpsertMany(list);
            }

            owner?.RefreshContentPeriodCache(list);
        }

        private TimeSpan RetryPolicy(int retryCount)
        {
            // Android와 동일한 지수 백오프: 30초 → 60초 → 120초 → 180초(최대)
            int attemptIndex = Math.Max(1, retryCount);
            double delayMs = 30000 * Math.Pow(2, Math.Max(0, attemptIndex - 1));
            delayMs = Math.Min(delayMs, 180000); // 3분 상한
            return TimeSpan.FromMilliseconds(delayMs);
        }

        private UpdateThrottleSettings GetThrottleSettings()
        {
            try
            {
                return throttleSettingsClient?.GetSettings() ?? new UpdateThrottleSettings
                {
                    MaxConcurrentDownloads = 8,
                    RetryIntervalSeconds = 60,
                    LeaseTtlSeconds = 3600,
                    LeaseRenewIntervalSeconds = 30,
                    SettingsRefreshSeconds = 1800
                };
            }
            catch
            {
                return new UpdateThrottleSettings
                {
                    MaxConcurrentDownloads = 8,
                    RetryIntervalSeconds = 60,
                    LeaseTtlSeconds = 3600,
                    LeaseRenewIntervalSeconds = 30,
                    SettingsRefreshSeconds = 1800
                };
            }
        }

        private bool EnsureDownloadLease(UpdateQueue queue, UpdateThrottleSettings settings)
        {
            if (queue == null)
            {
                return false;
            }

            if (settings == null)
            {
                return true;
            }

            lock (leaseLock)
            {
                try
                {
                    // LastRenewAt이 1분 이상 지난 임대는 강제 해제해서 고아 임대가 남지 않도록 함
                    leaseClient?.ReleaseStaleByLastRenew(60);
                }
                catch { }

                if (activeLease != null && string.Equals(activeLease.QueueId, queue.Id, StringComparison.OrdinalIgnoreCase))
                {
                    return TryRenewLeaseIfNeeded(settings);
                }

                ReleaseLeaseInternal();

                var lease = leaseClient?.TryAcquire(queue.PlayerId, queue.Id, settings.MaxConcurrentDownloads, settings.LeaseTtlSeconds);
                if (lease == null && !HasLocalActiveDownloads(queue.PlayerId))
                {
                    // 해당 플레이어의 기존 임대가 남아 있을 때 강제 해제 후 한 번 더 시도
                    leaseClient?.ReleaseByPlayer(queue.PlayerId);
                    lease = leaseClient?.TryAcquire(queue.PlayerId, queue.Id, settings.MaxConcurrentDownloads, settings.LeaseTtlSeconds);
                }
                if (lease == null)
                {
                    return false;
                }

                activeLease = lease;
                int renewSeconds = settings.LeaseRenewIntervalSeconds <= 0 ? 30 : settings.LeaseRenewIntervalSeconds;
                nextLeaseRenewAt = DateTime.Now.AddSeconds(renewSeconds);
                return true;
            }
        }

        private bool TryRenewLeaseIfNeeded(UpdateThrottleSettings settings)
        {
            lock (leaseLock)
            {
                if (settings == null || activeLease == null)
                {
                    return false;
                }

                if (DateTime.Now < nextLeaseRenewAt)
                {
                    return true;
                }

                bool renewed = leaseClient?.Renew(activeLease.Id, settings.LeaseTtlSeconds) ?? false;
                if (!renewed)
                {
                    ReleaseLeaseInternal();
                    return false;
                }

                int renewSeconds = settings.LeaseRenewIntervalSeconds <= 0 ? 30 : settings.LeaseRenewIntervalSeconds;
                nextLeaseRenewAt = DateTime.Now.AddSeconds(renewSeconds);
                return true;
            }
        }

        private void ReleaseLease()
        {
            lock (leaseLock)
            {
                ReleaseLeaseInternal();
            }
        }

        private void ReleaseLeaseInternal()
        {
            if (activeLease == null)
            {
                return;
            }

            try
            {
                leaseClient?.Release(activeLease.Id);
            }
            catch
            {
            }

            activeLease = null;
            nextLeaseRenewAt = DateTime.MinValue;
        }

        private void ReleaseLeaseIfOwner(string queueId)
        {
            if (string.IsNullOrWhiteSpace(queueId))
            {
                return;
            }

            lock (leaseLock)
            {
                if (activeLease != null && string.Equals(activeLease.QueueId, queueId, StringComparison.OrdinalIgnoreCase))
                {
                    ReleaseLeaseInternal();
                }
            }
        }

        private bool HasLocalActiveDownloads(string playerId)
        {
            lock (syncRoot)
            {
                var queues = queueRepository.LoadAll() ?? new List<UpdateQueue>();
                return queues.Any(q =>
                    q != null
                    && (q.Status == UpdateQueueStatus.Downloading
                        || q.Status == UpdateQueueStatus.Downloaded
                        || q.Status == UpdateQueueStatus.Validating
                        || q.Status == UpdateQueueStatus.Applying)
                    && (string.IsNullOrWhiteSpace(playerId)
                        || string.Equals(q.PlayerId, playerId, StringComparison.OrdinalIgnoreCase)));
            }
        }

        private void ScheduleLeaseRetry(UpdateQueue queue, UpdateThrottleSettings settings, string reason)
        {
            if (queue == null)
            {
                return;
            }

            int retrySeconds = settings?.RetryIntervalSeconds ?? 60;
            if (retrySeconds <= 0)
            {
                retrySeconds = 60;
            }

            queue.Status = UpdateQueueStatus.Queued;
            queue.NextAttempt = DateTime.Now.AddSeconds(retrySeconds);
            queue.LastError = reason ?? "LEASE_WAIT";
            queueRepository.Upsert(queue);
            LogStatus(queue, $"Lease wait: {queue.LastError}");
            SendQueueStatus(queue);
            SyncQueueToRethink(queue);
        }

        private static string BuildQueueId(string playerId, long ticks)
        {
            string trimmed = (playerId ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(trimmed))
            {
                return Guid.NewGuid().ToString();
            }
            return $"{trimmed}:{ticks}";
        }

        private List<DownloadEntry> BuildDownloadList(IEnumerable<PageInfoClass> pages)
        {
            var list = new List<DownloadEntry>();
            foreach (var page in pages)
            {
                if (page?.PIC_Elements == null) continue;
                foreach (var element in page.PIC_Elements)
                {
                    if (element?.EIF_ContentsInfoClassList == null) continue;
                    for (int idx = 0; idx < element.EIF_ContentsInfoClassList.Count; idx++)
                    {
                        var content = element.EIF_ContentsInfoClassList[idx];
                        if (content == null || string.IsNullOrWhiteSpace(content.CIF_FileName)) continue;
                        var entry = new DownloadEntry
                        {
                            FileName = content.CIF_FileName,
                            RemotePath = content.CIF_RelativePath,
                            SizeBytes = content.CIF_FileSize,
                            Checksum = string.IsNullOrWhiteSpace(content.CIF_FileHash) ? content.CIF_StrGUID : content.CIF_FileHash
                        };
                        BuildChunks(entry);
                        list.Add(entry);
                    }
                }
            }
            return list;
        }

        private ContractPlaylistPayload BuildContractPayload(PlayerInfoClass player, PageListInfoClass pageList, List<PageInfoClass> pages)
        {
            var payload = new ContractPlaylistPayload
            {
                playerId = player?.PIF_GUID ?? string.Empty,
                playerName = player?.PIF_PlayerName ?? string.Empty,
                playerLandscape = player?.PIF_IsLandScape ?? false,
                playlistId = pageList?.PLI_PageListName ?? string.Empty,
                playlistName = pageList?.PLI_PageListName ?? string.Empty,
                pages = new List<ContractPagePayload>()
            };

            var orderedIds = pageList?.PLI_Pages ?? new List<string>();

            foreach (var page in pages ?? new List<PageInfoClass>())
            {
                var pageEntry = new ContractPagePayload
                {
                    pageId = page.PIC_GUID,
                    pageName = page.PIC_PageName,
                    orderIndex = orderedIds.IndexOf(page.PIC_GUID),
                    playHour = page.PIC_PlaytimeHour,
                    playMinute = page.PIC_PlaytimeMinute,
                    playSecond = page.PIC_PlaytimeSecond,
                    volume = page.PIC_Volume,
                    landscape = page.PIC_IsLandscape,
                    elements = new List<ContractElementPayload>()
                };
                if (pageEntry.orderIndex < 0)
                {
                    pageEntry.orderIndex = payload.pages.Count;
                }

                if (page.PIC_Elements != null)
                {
                    foreach (var element in page.PIC_Elements)
                    {
                        var elementId = $"{page.PIC_GUID}_{element.EIF_Name}";
                        var elementEntry = new ContractElementPayload
                        {
                            elementId = elementId,
                            pageId = page.PIC_GUID,
                            name = element.EIF_Name,
                            type = element.EIF_Type,
                            width = element.EIF_Width,
                            height = element.EIF_Height,
                            posLeft = element.EIF_PosLeft,
                            posTop = element.EIF_PosTop,
                            zIndex = element.EIF_ZIndex,
                            muted = element.EIF_IsMuted,
                            contents = new List<ContractContentPayload>()
                        };

                        var contents = element.EIF_ContentsInfoClassList ?? new List<AndoW.Shared.ContentsInfoClass>();
                        for (int idx = 0; idx < contents.Count; idx++)
                        {
                            var content = contents[idx];
                            var uid = $"{elementId}_{idx}";
                            var contentEntry = new ContractContentPayload
                            {
                                uid = uid,
                                elementId = elementId,
                                fileName = content.CIF_FileName,
                                fileFullPath = content.CIF_FileFullPath,
                                contentType = content.CIF_ContentType,
                                playMinute = content.CIF_PlayMinute,
                                playSecond = content.CIF_PlaySec,
                                valid = content.CIF_ValidTime,
                                scrollSpeedSec = content.CIF_ScrollTextSpeedSec,
                                remoteChecksum = string.IsNullOrWhiteSpace(content.CIF_FileHash) ? content.CIF_StrGUID : content.CIF_FileHash,
                                fileSize = content.CIF_FileSize,
                                fileExist = content.CIF_FileExist
                            };
                            elementEntry.contents.Add(contentEntry);
                        }
                        pageEntry.elements.Add(elementEntry);
                    }
                }
                payload.pages.Add(pageEntry);
            }

            payload.pages = payload.pages.OrderBy(p => p.orderIndex).ToList();
            return payload;
        }

        private static double CalculateDownloadProgress(List<DownloadEntry> entries)
        {
            var list = entries ?? new List<DownloadEntry>();
            long totalBytes = CalculateTotalBytes(list);
            if (totalBytes <= 0)
            {
                int total = list.Count;
                int done = list.Count(e => e != null && e.Status == DownloadStatus.Done);
                return Math.Min(1.0, (double)done / Math.Max(1, total));
            }

            long downloadedBytes = CalculateDownloadedBytes(list);
            if (downloadedBytes < 0)
            {
                downloadedBytes = 0;
            }
            if (downloadedBytes > totalBytes)
            {
                downloadedBytes = totalBytes;
            }

            return Math.Min(1.0, (double)downloadedBytes / totalBytes);
        }

        private static long CalculateTotalBytes(List<DownloadEntry> entries)
        {
            if (entries == null) return 0;
            long total = 0;
            foreach (var entry in entries)
            {
                if (entry == null) continue;
                if (entry.SizeBytes > 0)
                {
                    total += entry.SizeBytes;
                }
                else if (entry.Chunks != null && entry.Chunks.Count > 0)
                {
                    foreach (var chunk in entry.Chunks)
                    {
                        if (chunk == null) continue;
                        if (chunk.Length > 0) total += chunk.Length;
                    }
                }
            }
            return total;
        }

        private static long CalculateDownloadedBytes(List<DownloadEntry> entries)
        {
            if (entries == null) return 0;
            long downloaded = 0;
            foreach (var entry in entries)
            {
                if (entry == null) continue;
                if (entry.Status == DownloadStatus.Done)
                {
                    if (entry.SizeBytes > 0)
                    {
                        downloaded += entry.SizeBytes;
                        continue;
                    }
                }

                if (entry.Chunks == null || entry.Chunks.Count == 0)
                {
                    continue;
                }

                foreach (var chunk in entry.Chunks)
                {
                    if (chunk == null) continue;
                    long chunkTotal = chunk.Length > 0 ? chunk.Length : chunk.DownloadedBytes;
                    long chunkDone = chunk.DownloadedBytes;
                    if (chunkDone < 0) chunkDone = 0;
                    if (chunkTotal > 0 && chunkDone > chunkTotal)
                    {
                        chunkDone = chunkTotal;
                    }
                    downloaded += chunkDone;
                }
            }
            return downloaded;
        }

        private void ReportDownloadProgress(UpdateQueue queue, List<DownloadEntry> entries, bool force)
        {
            if (queue == null)
            {
                return;
            }

            double progress = CalculateDownloadProgress(entries);
            int percent = (int)Math.Round(Math.Min(1.0, Math.Max(0.0, progress)) * 100);
            long nowTicks = DateTime.Now.Ticks;

            if (!force)
            {
                if (percent == lastProgressReportPercent &&
                    nowTicks - lastProgressReportTicks < ProgressReportInterval.Ticks)
                {
                    return;
                }
            }

            lastProgressReportPercent = percent;
            lastProgressReportTicks = nowTicks;
            queue.DownloadProgress = progress;
            UpdateHeartbeatSnapshot(queue, progress, force, true);
        }

        private static void ResetChunksToPending(DownloadEntry entry)
        {
            if (entry == null || entry.Chunks == null) return;

            long now = DateTime.Now.Ticks;
            foreach (var chunk in entry.Chunks)
            {
                if (chunk == null) continue;
                chunk.Status = ChunkStatus.Pending;
                chunk.DownloadedBytes = 0;
                chunk.LastUpdatedTicks = now;
            }
        }

        private bool DownloadAll(string managerHost, UpdateQueue queue, List<DownloadEntry> entries, bool ignoreLease)
        {
            if (entries == null || entries.Count == 0)
            {
                SendQueueStatus(queue);
                SyncQueueToRethink(queue);
                return true;
            }

            var settings = ignoreLease ? null : GetThrottleSettings();
            if (!ignoreLease && !EnsureDownloadLease(queue, settings))
            {
                ScheduleLeaseRetry(queue, settings, "LEASE_BUSY");
                return false;
            }

            ResetStaleChunks(queue, entries);
            int total = entries.Count;
            int completed = 0;
            foreach (var entry in entries)
            {
                try
                {
                    if (!ignoreLease && !EnsureDownloadLease(queue, settings))
                    {
                        ScheduleLeaseRetry(queue, settings, "LEASE_BUSY");
                        return false;
                    }

                    entry.Status = DownloadStatus.Downloading;
                    MarkChunkStatus(entry, ChunkStatus.Downloading, 0);
                    queue.DownloadJson = JsonConvert.SerializeObject(entries);
                    queue.DownloadProgress = CalculateDownloadProgress(entries);
                    queueRepository.Upsert(queue);
                    LogStatus(queue, $"Downloading {entry.FileName}");
                    SendQueueStatus(queue);
                    SyncQueueToRethink(queue);

                    bool success = DownloadSingle(managerHost, queue, entries, entry);
                    if (!success)
                    {
                        if (!ignoreLease && !string.IsNullOrWhiteSpace(entry.LastError)
                            && entry.LastError.StartsWith("LEASE", StringComparison.OrdinalIgnoreCase))
                        {
                            ScheduleLeaseRetry(queue, settings, entry.LastError);
                            return false;
                        }

                        entry.Status = DownloadStatus.Queued;
                        entry.Attempts++;
                        ResetChunksToPending(entry);
                        queue.DownloadJson = JsonConvert.SerializeObject(entries);
                        queue.DownloadProgress = CalculateDownloadProgress(entries);
                        queue.RetryCount++;
                        queue.NextAttempt = DateTime.Now.Add(RetryPolicy(queue.RetryCount));
                        queue.Status = UpdateQueueStatus.Queued;
                        queueRepository.Upsert(queue);
                        queue.LastError = string.IsNullOrWhiteSpace(entry.LastError)
                            ? $"Download failed: {entry.FileName}"
                            : $"{entry.FileName}: {entry.LastError}";
                        LogStatus(queue, $"Download failed: {entry.FileName}");
                        SendQueueStatus(queue);
                        SyncQueueToRethink(queue);
                        return false;
                    }

                    entry.Status = DownloadStatus.Done;
                    entry.Attempts += 1;
                    MarkChunkDone(entry, entry.SizeBytes);
                    entry.LastError = string.Empty;
                    queue.DownloadJson = JsonConvert.SerializeObject(entries);
                    completed++;
                    queue.DownloadProgress = CalculateDownloadProgress(entries);
                    queueRepository.Upsert(queue);
                    LogStatus(queue, $"Downloaded {entry.FileName}");
                    SendQueueStatus(queue);
                    SyncQueueToRethink(queue);
                }
                catch (Exception ex)
                {
                    Logger.WriteErrorLog($"Download failed: {entry.FileName} / {ex}", Logger.GetLogFileName());
                    entry.Status = DownloadStatus.Queued;
                    entry.LastError = ex.Message;
                    entry.Attempts++;
                    ResetChunksToPending(entry);
                    queue.DownloadJson = JsonConvert.SerializeObject(entries);
                    queue.DownloadProgress = CalculateDownloadProgress(entries);
                    queue.RetryCount++;
                    queue.NextAttempt = DateTime.Now.Add(RetryPolicy(queue.RetryCount));
                    queue.Status = UpdateQueueStatus.Queued;
                    queueRepository.Upsert(queue);
                    queue.LastError = $"Download failed: {entry.FileName} / {ex.Message}";
                    LogStatus(queue, queue.LastError);
                    SendQueueStatus(queue);
                    SyncQueueToRethink(queue);
                    return false;
                }
            }

            SendQueueStatus(queue);
            SyncQueueToRethink(queue);
            return true;
        }

        private bool DownloadSingle(string managerHost, UpdateQueue queue, List<DownloadEntry> entries, DownloadEntry entry)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.FileName))
                return true;

            string existingPath = FNDTools.GetContentsFilePath(entry.FileName);
            if (VerifyFile(existingPath, entry))
                return true;

            string tempFilePath = FNDTools.GetTempContentFilePath(entry.FileName);
            long nowTicks = DateTime.Now.Ticks;
            EnsureTempFileLength(tempFilePath, entry.SizeBytes);

            var chunks = entry.Chunks ?? new List<DownloadChunk>();
            if (chunks.Count == 0)
            {
                BuildChunks(entry);
                chunks = entry.Chunks;
            }

            foreach (var chunk in chunks.Where(c => c != null && c.Status != ChunkStatus.Done))
            {
                chunk.Status = ChunkStatus.Downloading;
                chunk.DownloadedBytes = 0;
                chunk.LastUpdatedTicks = nowTicks;

                bool chunkOk = DownloadChunkRange(queue, entries, entry, chunk, tempFilePath);
                if (!chunkOk)
                {
                    chunk.Status = ChunkStatus.Pending;
                    chunk.DownloadedBytes = 0;
                    chunk.LastUpdatedTicks = DateTime.Now.Ticks;
                    entry.LastError = string.IsNullOrWhiteSpace(entry.LastError) ? "CHUNK_FAIL" : entry.LastError;
                    return false;
                }
            }

            // 모든 청크 완료 후 최종 검증
            if (!VerifyFile(tempFilePath, entry))
            {
                entry.LastError = "HASH_MISMATCH";
                return false;
            }

            if (File.Exists(existingPath))
                File.Delete(existingPath);

            File.Move(tempFilePath, existingPath);

            entry.LastError = string.Empty;
            return true;
        }

        private bool VerifyFile(string path, DownloadEntry entry)
        {
            if (!File.Exists(path)) return false;
            if (entry.SizeBytes > 0)
            {
                var fi = new FileInfo(path);
                if (fi.Length != entry.SizeBytes) return false;
            }

            string checksum = entry.Checksum ?? string.Empty;

            if (IsXxHash64(checksum))
            {
                string hash = XXHash64.ComputePartialSignature(path);
                return string.Equals(hash, checksum, StringComparison.OrdinalIgnoreCase);
            }
            return true;
        }

        private bool ValidateFiles(UpdateQueue queue, IEnumerable<DownloadEntry> entries)
        {
            var list = (entries ?? Enumerable.Empty<DownloadEntry>()).ToList();
            int total = list.Count;
            int validated = 0;

            foreach (var entry in list)
            {
                string finalPath = FNDTools.GetContentsFilePath(entry.FileName);
                if (!VerifyFile(finalPath, entry))
                {
                    try
                    {
                        if (File.Exists(finalPath))
                        {
                            File.Delete(finalPath);
                        }
                        string tempPath = FNDTools.GetTempContentFilePath(entry.FileName);
                        if (File.Exists(tempPath))
                        {
                            File.Delete(tempPath);
                        }
                    }
                    catch { }

                    entry.Status = DownloadStatus.Queued;
                    entry.Attempts++;
                    entry.LastError = $"Validation failed: {entry.FileName}";
                    ResetChunksToPending(entry);

                    queue.DownloadJson = JsonConvert.SerializeObject(list);
                    queue.DownloadProgress = CalculateDownloadProgress(list);
                    queue.ValidateProgress = Math.Min(1.0, (double)validated / Math.Max(1, total));
                    queue.LastError = entry.LastError;
                    queueRepository.Upsert(queue);

                    LogStatus(queue, queue.LastError);
                    SendQueueStatus(queue);
                    SyncQueueToRethink(queue);
                    return false;
                }
                validated++;
                queue.ValidateProgress = Math.Min(1.0, (double)validated / Math.Max(1, total));
                queueRepository.Upsert(queue);
                SendQueueStatus(queue);
                SyncQueueToRethink(queue);
            }
            return true;
        }

        private bool IsHex(string checksum)
        {
            if (string.IsNullOrWhiteSpace(checksum)) return false;
            foreach (char c in checksum)
            {
                bool isHex = (c >= '0' && c <= '9')
                             || (c >= 'a' && c <= 'f')
                             || (c >= 'A' && c <= 'F');
                if (!isHex) return false;
            }
            return true;
        }

        private bool IsXxHash64(string checksum) => checksum != null && checksum.Length == 16 && IsHex(checksum);

        private void SendQueueStatus(UpdateQueue queue)
        {
            try
            {
                var player = owner?.g_PlayerInfoManager?.g_PlayerInfo;
                if (queue == null || player == null || string.IsNullOrWhiteSpace(player.PIF_GUID))
                {
                    return;
                }

                double downloadProgress = Math.Min(1.0, Math.Max(0.0, queue.DownloadProgress));
                double validateProgress = Math.Min(1.0, Math.Max(0.0, queue.ValidateProgress));
                double progress = downloadProgress;
                if (progress <= 0 && validateProgress > 0)
                {
                    progress = validateProgress;
                }
                bool sendNormalHeartbeatNow = ShouldSendNormalHeartbeatNow(queue);
                UpdateHeartbeatSnapshot(queue, progress, false, sendNormalHeartbeatNow);
            }
            catch
            {
                // 보고 실패는 치명적이지 않으므로 무시
            }
        }  

        private void UpdateHeartbeatSnapshot(UpdateQueue queue, double progress, bool forceImmediateReport, bool sendNormalHeartbeatNow)
        {
            if (queue == null)
            {
                ClearHeartbeatSnapshot(sendNormalHeartbeatNow);
                return;
            }

            bool active = queue.Status == UpdateQueueStatus.Queued
                          || queue.Status == UpdateQueueStatus.Downloading
                          || queue.Status == UpdateQueueStatus.Downloaded
                          || queue.Status == UpdateQueueStatus.Validating
                          || queue.Status == UpdateQueueStatus.Ready
                          || queue.Status == UpdateQueueStatus.Applying;
            if (!active)
            {
                ClearHeartbeatSnapshot(sendNormalHeartbeatNow);
                return;
            }

            int percent = (int)Math.Round(Math.Min(1.0, Math.Max(0.0, progress)) * 100);
            string heartbeatStatus = NormalizeHeartbeatStatus(queue.Status);
            long sessionId = 0;
            lock (heartbeatSync)
            {
                this.heartbeatStatus = heartbeatStatus;
                heartbeatProgress = percent;
                sessionId = EnsureUpdateHeartbeatSessionLocked();
            }

            if (sessionId > 0)
            {
                owner?.ReportUpdateHeartbeatNow(heartbeatStatus, percent, forceImmediateReport, sessionId);
            }
        }

        private static bool ShouldSendNormalHeartbeatNow(UpdateQueue queue)
        {
            if (queue == null)
            {
                return true;
            }

            // 플레이리스트 적용 완료 직후에는 재생 전환 heartbeat가 바로 뒤따르므로
            // 중간에 idle/stopped heartbeat를 보내지 않는다.
            if (!queue.IsScheduleQueue &&
                string.Equals(queue.Status, UpdateQueueStatus.Done, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return true;
        }

        private void ClearHeartbeatSnapshot(bool sendNormalHeartbeatNow)
        {
            long sessionId = 0;
            lock (heartbeatSync)
            {
                heartbeatStatus = string.Empty;
                heartbeatProgress = 0;
                if (updateHeartbeatReportingActive)
                {
                    sessionId = updateHeartbeatSessionId;
                    updateHeartbeatReportingActive = false;
                    updateHeartbeatSessionId = 0;
                }
            }

            if (sessionId > 0)
            {
                owner?.EndUpdateHeartbeatReporting(sessionId, sendNormalHeartbeatNow);
            }
        }

        private long EnsureUpdateHeartbeatSessionLocked()
        {
            if (updateHeartbeatReportingActive && updateHeartbeatSessionId > 0)
            {
                return updateHeartbeatSessionId;
            }

            updateHeartbeatSessionId = owner?.BeginUpdateHeartbeatReporting() ?? 0;
            updateHeartbeatReportingActive = updateHeartbeatSessionId > 0;
            return updateHeartbeatSessionId;
        }

        private static string NormalizeHeartbeatStatus(string status)
        {
            if (string.IsNullOrWhiteSpace(status))
            {
                return "UPDATING";
            }

            return status.Trim().ToUpperInvariant();
        }

        private void RecordHistoryInProgress(string historyId, string refQueueId)
        {
            if (string.IsNullOrWhiteSpace(historyId)) return;
            historyClient.MarkInProgress(historyId, refQueueId);
        }

        private void RecordHistoryDone(string historyId, string status, string errorCode, string errorMessage)
        {
            if (string.IsNullOrWhiteSpace(historyId)) return;
            historyClient.MarkDone(historyId, status, errorCode, errorMessage);
        }

        private void SyncQueueToRethink(UpdateQueue queue)
        {
            try
            {
                queueRethinkClient?.UpsertQueue(queue);
            }
            catch
            {
            }
        }

        private void DeleteRemoteQueueRecord(string playerGuid, string queueId, string host)
        {
            if (string.IsNullOrWhiteSpace(queueId) || string.IsNullOrWhiteSpace(playerGuid))
            {
                return;
            }

            queueRethinkClient?.DeleteQueueRecord(queueId, playerGuid);
        }

        private void EnsureUpdateQueueTable(string host)
        {
            try
            {
                using (var conn = R.Connection()
                    .Hostname(string.IsNullOrWhiteSpace(host) ? "127.0.0.1" : host)
                    .Port(28015)
                    .User(RethinkUser, RethinkPassword)
                    .Timeout(3000)
                    .Connect())
                {
                    var tables = R.Db(RethinkDbName).TableList().Run<List<string>>(conn);
                    if (tables == null || !tables.Contains(UpdateQueueTableName))
                    {
                        R.Db(RethinkDbName).TableCreate(UpdateQueueTableName).Run(conn);
                    }
                }
            }
            catch
            {
                // 테이블 생성 실패는 치명적이지 않으므로 무시
            }
        }

        private void BuildChunks(DownloadEntry entry)
        {
            if (entry == null) return;
            entry.Chunks.Clear();
            long size = Math.Max(0, entry.SizeBytes);
            if (size <= 0)
            {
                entry.Chunks.Add(new DownloadChunk
                {
                    Index = 0,
                    Offset = 0,
                    Length = 0,
                    Status = ChunkStatus.Pending,
                    DownloadedBytes = 0,
                    LastUpdatedTicks = DateTime.Now.Ticks
                });
                return;
            }

            const long ChunkSize = 4 * 1024 * 1024; // 4MB
            int idx = 0;
            long offset = 0;
            while (offset < size)
            {
                long length = Math.Min(ChunkSize, size - offset);
                entry.Chunks.Add(new DownloadChunk
                {
                    Index = idx,
                    Offset = offset,
                    Length = length,
                    Status = ChunkStatus.Pending,
                    DownloadedBytes = 0,
                    LastUpdatedTicks = DateTime.Now.Ticks
                });
                offset += length;
                idx++;
            }
        }

        private void ResetStaleChunks(UpdateQueue queue, List<DownloadEntry> entries)
        {
            if (entries == null) return;
            long now = DateTime.Now.Ticks;
            bool changed = false;
            foreach (var entry in entries)
            {
                if (entry?.Chunks == null) continue;
                foreach (var chunk in entry.Chunks)
                {
                    if (chunk == null) continue;
                    long elapsedTicks = now - chunk.LastUpdatedTicks;
                    if (chunk.Status == ChunkStatus.Downloading && elapsedTicks > StaleChunkThreshold.Ticks)
                    {
                        chunk.Status = ChunkStatus.Pending;
                        chunk.DownloadedBytes = 0;
                        chunk.LastUpdatedTicks = now;
                        changed = true;
                    }
                }
            }
            if (changed && queue != null)
            {
                queue.DownloadJson = JsonConvert.SerializeObject(entries);
                queueRepository.Upsert(queue);
            }
        }

        private void MarkChunkStatus(DownloadEntry entry, string status, long downloadedBytes)
        {
            if (entry?.Chunks == null) return;
            long now = DateTime.Now.Ticks;
            foreach (var chunk in entry.Chunks)
            {
                chunk.Status = status;
                if (downloadedBytes >= 0)
                {
                    chunk.DownloadedBytes = downloadedBytes;
                }
                chunk.LastUpdatedTicks = now;
            }
        }

        private void MarkChunkDone(DownloadEntry entry, long downloadedBytes)
        {
            MarkChunkStatus(entry, ChunkStatus.Done, downloadedBytes);
        }

        private void ApplyToLiteDb(PageListInfoClass pageList, List<PageInfoClass> pages)
        {
            lock (syncRoot)
            {
                var pageListRepo = new PageListRepository();
                var pageRepo = new PageRepository();
                try
                {
                    // 기존 데이터에 병합하여 필요한 리스트/페이지만 교체한다.
                    var existingLists = pageListRepo.LoadAll() ?? new List<PageListInfoClass>();
                    existingLists.RemoveAll(pl => string.Equals(pl.PLI_PageListName, pageList?.PLI_PageListName, StringComparison.OrdinalIgnoreCase));
                    if (pageList != null)
                    {
                        existingLists.Add(pageList);
                    }
                    pageListRepo.ReplaceAll(existingLists);

                    var existingPages = pageRepo.LoadAll() ?? new List<PageInfoClass>();
                    var incomingPageIds = new HashSet<string>((pages ?? new List<PageInfoClass>()).Select(p => p?.PIC_GUID ?? string.Empty), StringComparer.OrdinalIgnoreCase);
                    existingPages.RemoveAll(p => incomingPageIds.Contains(p?.PIC_GUID ?? string.Empty));
                    if (pages != null && pages.Count > 0)
                    {
                        existingPages.AddRange(pages);
                    }
                    pageRepo.ReplaceAll(existingPages);
                }
                finally
                {
                    pageListRepo.Dispose();
                    pageRepo.Dispose();
                }
            }
        }

        private void CleanupTemp(IEnumerable<DownloadEntry> entries)
        {
            foreach (var entry in entries ?? Enumerable.Empty<DownloadEntry>())
            {
                string stagingPath = Path.Combine(FNDTools.GetTempRootDirPath(), entry.RemotePath.Replace("/", Path.DirectorySeparatorChar.ToString()));
                if (File.Exists(stagingPath))
                {
                    try { File.Delete(stagingPath); } catch { }
                }
            }
        }

        private void CleanupUnusedFiles(IEnumerable<PageInfoClass> pages)
        {
            var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            void Collect(IEnumerable<PageInfoClass> source)
            {
                foreach (var page in source ?? Enumerable.Empty<PageInfoClass>())
                {
                    if (page?.PIC_Elements == null) continue;
                    foreach (var element in page.PIC_Elements)
                    {
                        if (element?.EIF_ContentsInfoClassList == null) continue;
                        foreach (var content in element.EIF_ContentsInfoClassList)
                        {
                            if (content == null || string.IsNullOrWhiteSpace(content.CIF_FileName)) continue;
                            used.Add(FNDTools.GetContentsFilePath(content.CIF_FileName));
                        }
                    }
                }
            }

            // Apply payload pages and all pages persisted in LiteDB.
            Collect(pages);
            try
            {
                using (var pageRepo = new PageRepository())
                {
                    Collect(pageRepo.LoadAll());
                }
            }
            catch
            {
                // ignore cleanup collection errors
            }

            var root = FNDTools.GetContentsRootDirPath();
            if (!Directory.Exists(root)) return;
            foreach (var file in Directory.GetFiles(root, "*", SearchOption.AllDirectories))
            {
                var full = Path.GetFullPath(file);
                if (!used.Contains(full))
                {
                    try { File.Delete(full); } catch { }
                }
            }

            // 빈 폴더 정리
            foreach (var dir in Directory.GetDirectories(root, "*", SearchOption.AllDirectories))
            {
                try
                {
                    if (Directory.Exists(dir) && Directory.GetFiles(dir).Length == 0 && Directory.GetDirectories(dir).Length == 0)
                    {
                        Directory.Delete(dir, false);
                    }
                }
                catch { }
            }
        }

        private void EnsureTempFileLength(string path, long size)
        {
            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(dir))
            {
                Directory.CreateDirectory(dir);
            }

            using (var fs = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read))
            {
                if (size > 0 && fs.Length < size)
                {
                    fs.SetLength(size);
                }
            }
        }

        private bool DownloadChunkRange(UpdateQueue queue, List<DownloadEntry> entries, DownloadEntry entry, DownloadChunk chunk, string tempFilePath)
        {
            if (chunk == null)
            {
                return true;
            }

            if (string.IsNullOrWhiteSpace(entry?.RemotePath))
            {
                entry.LastError = "REMOTE_PATH_EMPTY";
                return false;
            }

            try
            {
                if (!TryResolveFtpEndpoint(out var ftp, out string reason))
                {
                    entry.LastError = $"FTP_SETTINGS_MISSING:{reason}";
                    Logger.WriteErrorLog($"FTP endpoint resolve failed: {reason}", Logger.GetLogFileName());
                    return false;
                }

                using (var client = new FtpClient(ftp.Host, FTP_LOGIN, FTP_PASSWORD, ftp.Port))
                {
                    client.Config.TransferChunkSize = FtpTransferBufferSizeBytes;
                    client.Config.LocalFileBufferSize = FtpTransferBufferSizeBytes;
                    client.Connect();
                    long offset = chunk.Offset;
                    long length = chunk.Length > 0 ? chunk.Length : Math.Max(0, entry.SizeBytes - offset);
                    string remotePath = BuildRemotePath(ftp.RootPath, entry.RemotePath);

                    using (var remote = client.OpenRead(remotePath, FtpDataType.Binary, offset))
                    using (var local = new FileStream(tempFilePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read))
                    {
                        if (entry.SizeBytes > 0 && local.Length < entry.SizeBytes)
                        {
                            local.SetLength(entry.SizeBytes);
                        }

                        local.Seek(offset, SeekOrigin.Begin);

                        byte[] buffer = new byte[FtpTransferBufferSizeBytes];
                        long remaining = length;
                        while (remaining > 0)
                        {
                            if (activeLease != null)
                            {
                                var leaseSettings = GetThrottleSettings();
                                if (!TryRenewLeaseIfNeeded(leaseSettings))
                                {
                                    entry.LastError = "LEASE_LOST";
                                    return false;
                                }
                            }

                            int read = remote.Read(buffer, 0, (int)Math.Min(buffer.Length, remaining));
                            if (read <= 0)
                            {
                                break;
                            }
                            local.Write(buffer, 0, read);
                            long updated = chunk.DownloadedBytes + read;
                            if (chunk.Length > 0 && updated > chunk.Length)
                            {
                                updated = chunk.Length;
                            }
                            chunk.DownloadedBytes = updated;
                            chunk.LastUpdatedTicks = DateTime.Now.Ticks;
                            ReportDownloadProgress(queue, entries, false);
                            remaining -= read;
                        }

                        if (remaining > 0 && length > 0)
                        {
                            return false;
                        }
                    }
                }

                chunk.Status = ChunkStatus.Done;
                chunk.DownloadedBytes = chunk.Length;
                chunk.LastUpdatedTicks = DateTime.Now.Ticks;
                ReportDownloadProgress(queue, entries, true);
                return true;
            }
            catch (IOException ioEx)
            {
                Interlocked.Exchange(ref forceRefreshFtpSettings, 1);
                entry.LastError = $"IO_ERROR:{ioEx.Message}";
                Logger.WriteErrorLog($"Chunk download IO failed: {entry.FileName} / {ioEx}", Logger.GetLogFileName());
                return false;
            }
            catch (Exception ex)
            {
                Interlocked.Exchange(ref forceRefreshFtpSettings, 1);
                entry.LastError = $"FTP_ERROR:{ex.Message}";
                Logger.WriteErrorLog($"Chunk download failed: {entry.FileName} / {ex}", Logger.GetLogFileName());
                return false;
            }
        }

        private bool TryResolveFtpEndpoint(out FtpEndpoint endpoint, out string reason)
        {
            string rethinkHost = owner?.g_LocalSettingsManager?.Settings?.ManagerIP;
            serverSettingsClient?.UpdateHost(rethinkHost);
            bool forceRefresh = Interlocked.Exchange(ref forceRefreshFtpSettings, 0) == 1;
            var settings = serverSettingsClient?.GetSettings(forceRefresh);

            string host = settings?.DataServerIp;
            if (string.IsNullOrWhiteSpace(host))
            {
                reason = "DataServerIp is empty (ServerSettings)";
                Interlocked.Exchange(ref forceRefreshFtpSettings, 1);
                endpoint = default;
                return false;
            }

            int port = settings?.FTP_Port ?? 0;
            if (port <= 0)
            {
                reason = "FTP_Port is empty (ServerSettings)";
                Interlocked.Exchange(ref forceRefreshFtpSettings, 1);
                endpoint = default;
                return false;
            }

            endpoint = new FtpEndpoint(host.Trim(), port, settings?.FTP_RootPath);
            reason = null;
            return true;
        }

        private static string BuildRemotePath(string rootPath, string relativePath)
        {
            string normalizedRoot = NormalizeRemotePath(rootPath, "/NewHyOn");
            string normalizedRelative = NormalizeRemotePath(relativePath, "/");

            if (string.IsNullOrWhiteSpace(normalizedRelative) || normalizedRelative == "/")
            {
                return normalizedRoot;
            }

            if (string.Equals(normalizedRelative, normalizedRoot, StringComparison.OrdinalIgnoreCase)
                || normalizedRelative.StartsWith(normalizedRoot + "/", StringComparison.OrdinalIgnoreCase))
            {
                return normalizedRelative;
            }

            return normalizedRoot.TrimEnd('/') + "/" + normalizedRelative.TrimStart('/');
        }

        private static string NormalizeRemotePath(string path, string fallback)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return fallback;
            }

            string normalized = path.Replace("\\", "/").Trim();
            if (!normalized.StartsWith("/"))
            {
                normalized = "/" + normalized;
            }

            normalized = normalized.TrimEnd('/');
            return string.IsNullOrWhiteSpace(normalized) ? "/" : normalized;
        }

        private readonly struct FtpEndpoint
        {
            public string Host { get; }
            public int Port { get; }
            public string RootPath { get; }

            public FtpEndpoint(string host, int port, string rootPath)
            {
                Host = string.IsNullOrWhiteSpace(host) ? "127.0.0.1" : host;
                Port = port > 0 ? port : FTP_PORT;
                RootPath = NormalizeRemotePath(rootPath, "/NewHyOn");
            }
        }

        private class DownloadEntry
        {
            public string FileName { get; set; }
            public string RemotePath { get; set; }
            public long SizeBytes { get; set; }
            public string Checksum { get; set; }
            public string Status { get; set; } = DownloadStatus.Queued;
            public int Attempts { get; set; } = 0;
            public string LastError { get; set; } = string.Empty;
            public List<DownloadChunk> Chunks { get; set; } = new List<DownloadChunk>();
        }

        private class DownloadChunk
        {
            public int Index { get; set; }
            public long Offset { get; set; }
            public long Length { get; set; }
            public string Status { get; set; } = ChunkStatus.Pending;
            public long DownloadedBytes { get; set; }
            public long LastUpdatedTicks { get; set; }
        }

        private static class DownloadStatus
        {
            public const string Queued = "QUEUED";
            public const string Downloading = "DOWNLOADING";
            public const string Done = "DONE";
            public const string Failed = "FAILED";
        }

        private static class ChunkStatus
        {
            public const string Pending = "pending";
            public const string Downloading = "downloading";
            public const string Done = "done";
            public const string Failed = "failed";
        }

        private void LogStatus(UpdateQueue queue, string message)
        {
            string logLine = $"[UpdateQueue {queue.Id}] {message} | Status={queue.Status} Download={queue.DownloadProgress:P1} Validate={queue.ValidateProgress:P1}";
            Logger.WriteLog(logLine, Logger.GetLogFileName());
            WriteStatusFile(logLine);
        }

        private void WriteStatusFile(string line)
        {
            try
            {
                string dir = FNDTools.GetLogRootDirPath();
                Directory.CreateDirectory(dir);
                string path = Path.Combine(dir, "UpdateStatus.log");
                File.AppendAllText(path, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {line}{Environment.NewLine}", Encoding.UTF8);
            }
            catch
            {
            }
        }

        public int CancelActiveQueues(string reason)
        {
            lock (syncRoot)
            {
                var queues = queueRepository.LoadAll();
                int cancelled = 0;
                foreach (var q in queues)
                {
                    if (q.Status == UpdateQueueStatus.Failed || q.Status == UpdateQueueStatus.Done)
                    {
                        continue;
                    }
                    var downloads = JsonConvert.DeserializeObject<List<DownloadEntry>>(q.DownloadJson ?? "[]") ?? new List<DownloadEntry>();
                    CleanupTemp(downloads);
                    queueRepository.DeleteById(q.Id);
                    cancelled++;
                    LogStatus(q, $"Cancelled: {reason}");
                    ReleasePlayerLease(q.PlayerId);
                    DeleteRemoteQueueRecord(q.PlayerId, q.Id, managerHost);
                }
                if (cancelled > 0)
                {
                    ReleaseLease();
                    ClearHeartbeatSnapshot(true);
                }
                return cancelled;
            }
        }

        private void RecoverStalledQueues()
        {
            lock (syncRoot)
            {
                var queues = queueRepository.LoadAll();
                foreach (var q in queues)
                {
                    if (q.Status == UpdateQueueStatus.Downloading
                        || q.Status == UpdateQueueStatus.Downloaded
                        || q.Status == UpdateQueueStatus.Validating
                        || q.Status == UpdateQueueStatus.Applying)
                    {
                        q.Status = UpdateQueueStatus.Queued;
                        q.LastError = "Recovered from interrupted state";
                        q.NextAttempt = DateTime.Now;
                        q.DownloadProgress = 0;
                        q.ValidateProgress = 0;
                        queueRepository.Upsert(q);
                        LogStatus(q, "Recovered stalled queue to QUEUED");
                        SyncQueueToRethink(q);
                    }
                }
            }
        }

        private void ProcessorTick(object state)
        {
            if (disposed)
            {
                return;
            }
            if (Interlocked.Exchange(ref isProcessingTick, 1) == 1)
            {
                return;
            }
            try
            {
                ProcessQueue();
            }
            finally
            {
                Interlocked.Exchange(ref isProcessingTick, 0);
            }
        }

        private bool HasActiveQueue(string playerId)
        {
            lock (syncRoot)
            {
                var queues = queueRepository.LoadAll();
                foreach (var q in queues)
                {
                    bool activeStatus = q.Status == UpdateQueueStatus.Queued
                                        || q.Status == UpdateQueueStatus.Downloading
                                        || q.Status == UpdateQueueStatus.Downloaded
                                        || q.Status == UpdateQueueStatus.Validating
                                        || q.Status == UpdateQueueStatus.Ready
                                        || q.Status == UpdateQueueStatus.Applying;
                    if (activeStatus && (string.IsNullOrWhiteSpace(playerId) || string.Equals(q.PlayerId, playerId, StringComparison.OrdinalIgnoreCase)))
                    {
                        return true;
                    }
                }
                return false;
            }
        }

        private void ReleasePlayerLease(string playerId)
        {
            if (leaseClient == null || string.IsNullOrWhiteSpace(playerId))
            {
                return;
            }
            try
            {
                leaseClient.ReleaseByPlayer(playerId);
            }
            catch
            {
            }
        }
    }

    internal sealed class RethinkContentClient
    {
        private const string Database = "NewHyOn";
        private static readonly RethinkDB R = RethinkDB.R;

        private readonly string host;
        private Connection connection;

        public RethinkContentClient(string host)
        {
            this.host = string.IsNullOrWhiteSpace(host) ? "127.0.0.1" : host;
        }

        public PageListInfoClass FetchPageList(string playlistIdOrName, string playerName)
        {
            var conn = GetConnection();
            if (conn == null) return null;

            // 우선 ID로 조회, 없으면 이름으로 조회
            Dictionary<string, object> map = null;
            try
            {
                map = R.Db(Database).Table("PageListInfoManager").Get(playlistIdOrName).RunAtom<Dictionary<string, object>>(conn);
            }
            catch { /* ignore */ }

            if (map == null && !string.IsNullOrWhiteSpace(playlistIdOrName))
            {
                var cursor = R.Db(Database).Table("PageListInfoManager")
                    .Filter(r => r["PLI_PageListName"].Downcase().Eq(playlistIdOrName.ToLowerInvariant()))
                    .Limit(1)
                    .RunCursor<Dictionary<string, object>>(conn);
                map = cursor.FirstOrDefault();
            }
            if (map == null) return null;

            if (map.TryGetValue("id", out var idObj) && idObj != null)
            {
                map["Id"] = idObj.ToString();
                map.Remove("id");
            }

            string json = JsonConvert.SerializeObject(map);
            var pl = JsonConvert.DeserializeObject<PageListInfoClass>(json);
            return pl;
        }

        public List<PageInfoClass> FetchPages(IEnumerable<string> pageIds)
        {
            var conn = GetConnection();
            if (conn == null) return new List<PageInfoClass>();
            var ids = pageIds?.ToList() ?? new List<string>();
            if (ids.Count == 0) return new List<PageInfoClass>();

            var list = R.Db(Database).Table("PageInfoManager").GetAll(ids).RunCursor<PageInfoClass>(conn);
            
            // 정렬: 입력 순서대로
            var ordered = new List<PageInfoClass>();
            foreach (var id in ids)
            {
                var found = list.FirstOrDefault(p => p.PIC_GUID == id);
                if (found != null) ordered.Add(found);
            }
            return ordered;
        }

        private Connection GetConnection()
        {
            try
            {
                if (connection != null && connection.Open) return connection;
                connection = R.Connection()
                    .Hostname(host)
                    .Port(28015)
                    .User("admin", "turtle04!9")
                    .Connect();
                return connection;
            }
            catch (Exception ex)
            {
                Logger.WriteErrorLog($"RethinkContentClient connection error: {ex}", Logger.GetLogFileName());
                connection = null;
                return null;
            }
        }
    }

    internal sealed class UpdatePayload
    {
        public PageListInfoClass PageList { get; set; }
        public List<PageInfoClass> Pages { get; set; }
        public ContractPlaylistPayload Contract { get; set; }
    }

    internal sealed class ContractPlaylistPayload
    {
        public string playerId { get; set; }
        public string playerName { get; set; }
        public bool playerLandscape { get; set; }
        public string playlistId { get; set; }
        public string playlistName { get; set; }
        public List<ContractPagePayload> pages { get; set; } = new List<ContractPagePayload>();
    }

    internal sealed class ContractPagePayload
    {
        public string pageId { get; set; }
        public string pageName { get; set; }
        public int orderIndex { get; set; }
        public int playHour { get; set; }
        public int playMinute { get; set; }
        public int playSecond { get; set; }
        public int volume { get; set; }
        public bool landscape { get; set; }
        public List<ContractElementPayload> elements { get; set; } = new List<ContractElementPayload>();
    }

    internal sealed class ContractElementPayload
    {
        public string elementId { get; set; }
        public string pageId { get; set; }
        public string name { get; set; }
        public string type { get; set; }
        public double width { get; set; }
        public double height { get; set; }
        public double posTop { get; set; }
        public double posLeft { get; set; }
        public int zIndex { get; set; }
        public bool muted { get; set; } = true;
        public List<ContractContentPayload> contents { get; set; } = new List<ContractContentPayload>();
    }

    internal sealed class ContractContentPayload
    {
        public string uid { get; set; }
        public string elementId { get; set; }
        public string fileName { get; set; }
        public string fileFullPath { get; set; }
        public string contentType { get; set; }
        public string playMinute { get; set; }
        public string playSecond { get; set; }
        public bool valid { get; set; }
        public int scrollSpeedSec { get; set; }
        public string remoteChecksum { get; set; }
        public long fileSize { get; set; }
        public bool fileExist { get; set; }
    }

}
