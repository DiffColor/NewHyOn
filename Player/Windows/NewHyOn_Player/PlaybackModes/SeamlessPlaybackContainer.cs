using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using TurtleTools;
using AndoW.Shared;
using NewHyOnPlayer.DataManager;
using SharedElementInfoClass = AndoW.Shared.ElementInfoClass;
using SharedPageInfoClass = AndoW.Shared.PageInfoClass;
using NewHyOnPlayer.Services;

namespace NewHyOnPlayer.PlaybackModes
{
    internal sealed class SeamlessPlaybackContainer : IPlaybackContainer
    {
        private readonly MainWindow owner;
        private readonly Canvas hostCanvas;
        private readonly PlaybackCoordinator coordinator;
        private readonly SeamlessLayoutRuntime[] layouts;
        private readonly int[] layoutActivationVersions;
        private readonly object stateLock = new object();
        private readonly SemaphoreSlim layoutPrepareGate = new SemaphoreSlim(1, 1);

        private int activeLayoutIndex = -1;
        private int nextPageLayoutIndex = -1;
        private int playbackVersion;
        private int completionHandling;
        private bool initialized;
        private int hostZOrderSeed;
        private DateTime lastScheduleEval = DateTime.MinValue;
        private string currentPageListName = string.Empty;
        private string pendingSchedulePlaylist = string.Empty;
        private string pendingScheduleId = string.Empty;
        private string lastScheduleEvalStateKey = string.Empty;
        private string lastMissingScheduleLogged = string.Empty;
        private string pendingPlaylistReload = string.Empty;
        private string pendingPlaylistReloadReason = string.Empty;
        private string lastPlaylistReloadStateKey = string.Empty;
        private string lastLocalFallbackPlaylist = string.Empty;
        private string requestedWarmScheduleStateKey = string.Empty;
        private string requestedWarmSchedulePlaylist = string.Empty;
        private bool requestedWarmScheduleAutoSwitch;
        private int reservedLayoutIndex = -1;
        private string reservedLayoutStateKey = string.Empty;
        private string reservedLayoutPlaylist = string.Empty;

        public SeamlessPlaybackContainer(MainWindow owner, Canvas hostCanvas)
        {
            this.owner = owner;
            this.hostCanvas = hostCanvas;
            coordinator = new PlaybackCoordinator(owner);
            layouts = new[]
            {
                new SeamlessLayoutRuntime("Layout-A"),
                new SeamlessLayoutRuntime("Layout-B"),
                new SeamlessLayoutRuntime("Layout-C")
            };
            layoutActivationVersions = new int[layouts.Length];

            for (int i = 0; i < layouts.Length; i++)
            {
                layouts[i].PlaybackCompleted += Layout_PlaybackCompleted;
                layouts[i].PlaybackPulse += Layout_PlaybackPulse;
            }
        }

        public void Initialize()
        {
            if (initialized)
            {
                return;
            }

            initialized = true;
            hostCanvas.Dispatcher.Invoke(() =>
            {
                hostCanvas.Children.Clear();
                for (int i = 0; i < layouts.Length; i++)
                {
                    hostCanvas.Children.Add(layouts[i].Host);
                    Canvas.SetLeft(layouts[i].Host, 0);
                    Canvas.SetTop(layouts[i].Host, 0);
                    Panel.SetZIndex(layouts[i].Host, i);
                }
            });
            hostZOrderSeed = layouts.Length;
        }

        public void PlayNextPage()
        {
            Initialize();
            if (activeLayoutIndex < 0)
            {
                owner?.SetInitialLoadingVisible(true);
            }
            Interlocked.Exchange(ref completionHandling, 0);
            int version = Interlocked.Increment(ref playbackVersion);
            Task.Run(async () =>
            {
                try
                {
                    SeamlessPagePlaybackRequest request = BuildNextPlaybackRequest();
                    if (request == null || request.CurrentPage == null || !IsVersionCurrent(version))
                    {
                        return;
                    }

                    await ShowPageInternalAsync(version, request.CurrentPage, request.NextPage, request.PlaylistName).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Logger.WriteErrorLog($"다음 seamless 페이지 전환 실패: {ex}", Logger.GetLogFileName());
                }
            });
        }

        public void PlayFirstPage()
        {
            Logger.WriteLog("PlayFirstPage Called.", Logger.GetLogFileName());
            owner.g_PageIndex = 0;
            PlayNextPage();
        }

        public void StartInitialPlayback(string defaultPlaylist)
        {
            UpdateCurrentPageListName(defaultPlaylist);
            EnsureLocalPlaybackReady();
            owner.g_PageIndex = 0;
            RequestScheduleEvaluation(force: true);

            bool switched = TryApplyScheduledSwitch(isPageBoundary: true, isContentBoundary: true);
            if (!switched)
            {
                Logger.WriteLog("PopPage in @MainWindow_Loaded Called.", Logger.GetLogFileName());
                PlayNextPage();
                return;
            }

            PlayNextPage();
        }

        public bool IsPresentationActive()
        {
            SeamlessLayoutRuntime active = GetActiveLayout();
            return active != null && active.IsPresentationActive;
        }

        public bool IsOnlySinglePage
        {
            get
            {
                return owner?.g_PageInfoManager?.g_PageInfoClassList != null
                    && owner.g_PageInfoManager.g_PageInfoClassList.Count <= 1;
            }
        }

        public int CurrentPageElapsedSeconds
        {
            get
            {
                SeamlessLayoutRuntime active = GetActiveLayout();
                if (active == null)
                {
                    return 0;
                }

                return (int)(active.CurrentElapsedMilliseconds / 1000);
            }
        }

        public int CurrentPageDurationSeconds
        {
            get
            {
                SeamlessLayoutRuntime active = GetActiveLayout();
                if (active == null || active.CurrentPlan == null)
                {
                    return 1;
                }

                return Math.Max(1, active.CurrentPlan.DurationSeconds);
            }
        }

        public string CurrentPageListName => currentPageListName;

        public string CurrentPageName => owner?.g_CurrentPageName ?? string.Empty;

        public string NextPageName
        {
            get
            {
                SharedPageInfoClass nextPage = GetPageDefinitionByIndex(owner?.g_PageIndex ?? 0);
                return nextPage?.PIC_PageName ?? string.Empty;
            }
        }


        public SeamlessPlaybackStatus GetPrimaryPlaybackStatus()
        {
            SeamlessLayoutRuntime active = GetActiveLayout();
            return active != null ? active.GetPrimaryPlaybackStatus() : null;
        }

        public List<PlaybackDebugItem> GetDebugItems()
        {
            var merged = new List<PlaybackDebugItem>();
            for (int i = 0; i < layouts.Length; i++)
            {
                merged.AddRange(layouts[i].GetDebugItems());
            }

            return merged;
        }

        public void UpdateCurrentPageListName(string pageListName)
        {
            currentPageListName = pageListName ?? string.Empty;
            owner.g_PageIndex = 0;

            if (!string.IsNullOrWhiteSpace(currentPageListName))
            {
                owner.g_PageInfoManager.LoadData(currentPageListName);
            }

            if (owner.g_LocalSettingsManager?.Settings != null)
            {
                owner.g_LocalSettingsManager.Settings.IsOnlyOnePage =
                    owner.g_PageInfoManager?.g_PageInfoClassList != null
                    && owner.g_PageInfoManager.g_PageInfoClassList.Count == 1;
            }
        }

        public void RequestScheduleEvaluation(bool force = false)
        {
            EvaluateSchedule(force);
        }

        public void HandleWeeklyScheduleUpdated()
        {
            owner?.ScheduleEvaluatorService?.InvalidateWeeklyCache();
            RequestScheduleEvaluation(force: true);
            owner?.OnAirServiceInstance?.RefreshWeeklySchedule();
        }

        public void StartPlaybackFromOffAir()
        {
            try
            {
                var playerInfo = owner?.g_PlayerInfoManager?.g_PlayerInfo;
                string playlist = playerInfo?.PIF_CurrentPlayList;
                if (string.IsNullOrWhiteSpace(playlist))
                {
                    playlist = playerInfo?.PIF_DefaultPlayList;
                }

                if (!string.IsNullOrWhiteSpace(playlist))
                {
                    UpdateCurrentPageListName(playlist);
                }

                EnsureLocalPlaybackReady();
                RequestScheduleEvaluation(force: true);
                bool switched = TryApplyScheduledSwitch(isPageBoundary: true, isContentBoundary: true);
                if (!switched
                    && (owner?.g_PageInfoManager?.g_PageInfoClassList?.Count ?? 0) == 0
                    && !string.IsNullOrWhiteSpace(playerInfo?.PIF_DefaultPlayList))
                {
                    UpdateCurrentPageListName(playerInfo.PIF_DefaultPlayList);
                }

                PlayNextPage();
            }
            catch (Exception ex)
            {
                Logger.WriteLog(ex.ToString(), Logger.GetLogFileName());
            }
        }

        public void RequestPlaylistReload(string playlistName, string reason)
        {
            if (string.IsNullOrWhiteSpace(playlistName))
            {
                return;
            }

            pendingPlaylistReload = playlistName;
            pendingPlaylistReloadReason = reason ?? string.Empty;
            string timing = owner?.g_LocalSettingsManager?.Settings?.SwitchTiming ?? "Immediately";

            if (timing.Equals("Immediately", StringComparison.OrdinalIgnoreCase))
            {
                pendingPlaylistReload = string.Empty;
                pendingPlaylistReloadReason = string.Empty;
                UpdateCurrentPageListName(playlistName);
                PlayNextPage();
                return;
            }

            string stateKey = $"PENDING|{pendingPlaylistReload}|{pendingPlaylistReloadReason}";
            if (!string.Equals(lastPlaylistReloadStateKey, stateKey, StringComparison.Ordinal))
            {
                Logger.WriteLog($"플레이리스트 리로드 예약: {pendingPlaylistReload} ({pendingPlaylistReloadReason})", Logger.GetLogFileName());
                lastPlaylistReloadStateKey = stateKey;
            }
        }

        public void HideAll()
        {
            Interlocked.Increment(ref playbackVersion);
            Interlocked.Exchange(ref completionHandling, 0);
            for (int i = 0; i < layouts.Length; i++)
            {
                layouts[i].Deactivate();
            }

            activeLayoutIndex = -1;
            nextPageLayoutIndex = -1;
            ClearRequestedWarmScheduleState();
            ClearReservedLayoutState();
        }

        public void StopAll()
        {
            Interlocked.Increment(ref playbackVersion);
            Interlocked.Exchange(ref completionHandling, 0);
            for (int i = 0; i < layouts.Length; i++)
            {
                layouts[i].Clear();
            }

            activeLayoutIndex = -1;
            nextPageLayoutIndex = -1;
            ClearPendingScheduleState();
            ClearRequestedWarmScheduleState();
            ClearReservedLayoutState();
        }

        private async Task ShowPageInternalAsync(int version, SharedPageInfoClass currentPage, SharedPageInfoClass nextPage, string playlistName)
        {
            Task<SeamlessPagePlan> currentTask = coordinator.BuildPagePlanAsync(currentPage, playlistName);
            Task<SeamlessPagePlan> nextTask = nextPage != null
                ? coordinator.BuildPagePlanAsync(nextPage, playlistName)
                : Task.FromResult<SeamlessPagePlan>(null);

            SeamlessPagePlan currentPlan = await currentTask.ConfigureAwait(false);
            SeamlessPagePlan nextPlan = await nextTask.ConfigureAwait(false);
            if (currentPlan == null || !IsVersionCurrent(version))
            {
                return;
            }

            await PresentPreparedPlansAsync(version, currentPlan, nextPlan).ConfigureAwait(false);
        }

        private async Task PresentPreparedPlansAsync(int version, SeamlessPagePlan currentPlan, SeamlessPagePlan nextPlan)
        {
            if (!IsVersionCurrent(version))
            {
                return;
            }

            int targetLayoutIndex = GetActivationLayoutIndex(currentPlan);
            if (!layouts[targetLayoutIndex].Matches(currentPlan) || layouts[targetLayoutIndex].State != SeamlessLayoutState.Ready)
            {
                bool prepared = await PrepareLayoutAsync(targetLayoutIndex, currentPlan, version).ConfigureAwait(false);
                if (!prepared)
                {
                    return;
                }
            }

            if (!IsVersionCurrent(version))
            {
                return;
            }

            int previousActiveIndex = activeLayoutIndex;
            BringLayoutToFront(targetLayoutIndex);
            layouts[targetLayoutIndex].Activate();
            layoutActivationVersions[targetLayoutIndex] = version;
            activeLayoutIndex = targetLayoutIndex;
            if (nextPageLayoutIndex == targetLayoutIndex)
            {
                nextPageLayoutIndex = -1;
            }

            if (reservedLayoutIndex == targetLayoutIndex)
            {
                ClearReservedLayoutState();
            }

            if (previousActiveIndex < 0)
            {
                owner?.SetInitialLoadingVisible(false);
            }

            if (previousActiveIndex >= 0 && previousActiveIndex != targetLayoutIndex)
            {
                layouts[previousActiveIndex].Deactivate();
            }

            if (!IsVersionCurrent(version))
            {
                return;
            }

            int standbyIndex = GetStandbyLayoutIndex(targetLayoutIndex);
            nextPageLayoutIndex = -1;
            if (standbyIndex < 0 || standbyIndex >= layouts.Length)
            {
                return;
            }

            if (nextPlan == null)
            {
                if (standbyIndex != reservedLayoutIndex)
                {
                    layouts[standbyIndex].Clear();
                }
                return;
            }

            bool standbyPrepared = await PrepareLayoutAsync(standbyIndex, nextPlan, version).ConfigureAwait(false);
            if (!standbyPrepared || !IsVersionCurrent(version))
            {
                return;
            }

            layouts[standbyIndex].Deactivate();
            nextPageLayoutIndex = standbyIndex;
        }

        private void Layout_PlaybackCompleted(SeamlessLayoutRuntime completedLayout)
        {
            if (completedLayout == null)
            {
                return;
            }

            int completedLayoutIndex = GetLayoutIndex(completedLayout);
            if (completedLayoutIndex < 0)
            {
                return;
            }

            int completedActivationVersion = layoutActivationVersions[completedLayoutIndex];
            if (!IsCompletionCurrent(completedLayout, completedActivationVersion))
            {
                return;
            }

            SeamlessLayoutRuntime active = GetActiveLayout();
            if (!ReferenceEquals(active, completedLayout))
            {
                return;
            }

            if (IsOnlySinglePage)
            {
                bool isCurrent = false;
                bool switched = false;
                Application.Current?.Dispatcher.Invoke(() =>
                {
                    isCurrent = IsCompletionCurrent(completedLayout, completedActivationVersion);
                    if (!isCurrent)
                    {
                        return;
                    }

                    RequestScheduleEvaluation(force: true);
                    switched = TryApplyScheduledSwitch(isPageBoundary: true, isContentBoundary: false)
                        || TryApplyPendingPlaylistReload(isPageBoundary: true, isContentBoundary: false);
                });

                if (!isCurrent)
                {
                    return;
                }

                if (switched)
                {
                    PlayNextPage();
                }
                else
                {
                    completedLayout.RestartLoop();
                }
                return;
            }

            if (Interlocked.Exchange(ref completionHandling, 1) == 1)
            {
                return;
            }

            Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    if (!IsCompletionCurrent(completedLayout, completedActivationVersion))
                    {
                        return;
                    }

                    RequestScheduleEvaluation(force: true);
                    if (TryApplyScheduledSwitch(isPageBoundary: true, isContentBoundary: false)
                        || TryApplyPendingPlaylistReload(isPageBoundary: true, isContentBoundary: false))
                    {
                        PlayNextPage();
                        return;
                    }

                    PlayNextPage();
                }
                finally
                {
                    Interlocked.Exchange(ref completionHandling, 0);
                }
            }));
        }

        private void Layout_PlaybackPulse(SeamlessLayoutRuntime sourceLayout, SeamlessPlaybackPulse pulse)
        {
            if (sourceLayout == null || pulse == null)
            {
                return;
            }

            int layoutIndex = GetLayoutIndex(sourceLayout);
            if (layoutIndex < 0)
            {
                return;
            }

            int activationVersion = layoutActivationVersions[layoutIndex];
            if (!IsLayoutCurrent(sourceLayout, activationVersion))
            {
                return;
            }

            Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
            {
                if (!IsLayoutCurrent(sourceLayout, activationVersion))
                {
                    return;
                }

                if (pulse.IsSecondTick)
                {
                    RequestScheduleEvaluation(force: false);
                }

                string switchTiming = owner?.g_LocalSettingsManager?.Settings?.SwitchTiming ?? "Immediately";
                bool switched = false;
                if (switchTiming.Equals("Immediately", StringComparison.OrdinalIgnoreCase))
                {
                    switched = TryApplyScheduledSwitch(isPageBoundary: false, isContentBoundary: pulse.IsContentBoundary)
                        || TryApplyPendingPlaylistReload(isPageBoundary: false, isContentBoundary: pulse.IsContentBoundary);
                }
                else if (switchTiming.Equals("ContentEnd", StringComparison.OrdinalIgnoreCase) && pulse.IsContentBoundary)
                {
                    switched = TryApplyScheduledSwitch(isPageBoundary: false, isContentBoundary: true)
                        || TryApplyPendingPlaylistReload(isPageBoundary: false, isContentBoundary: true);
                }

                if (switched)
                {
                    PlayNextPage();
                    return;
                }

            }));
        }

        private SeamlessPagePlaybackRequest BuildNextPlaybackRequest()
        {
            try
            {
                EnsureLocalPlaybackReady();
                if (owner?.g_PageInfoManager == null
                    || owner.g_PageInfoManager.g_PageInfoClassList == null
                    || owner.g_PageInfoManager.g_PageInfoClassList.Count == 0)
                {
                    Thread.Sleep(250);
                    return null;
                }

                WindowTools.DeleteNotifyIcons();

                if (owner.g_PageInfoManager.g_PageInfoClassList.Count == owner.g_PageIndex)
                {
                    owner.g_PageIndex = 0;
                }

                Logger.WriteLog(string.Format("g_PageInfoList.Count : {0} / g_PageIndex : {1}", owner.g_PageInfoManager.g_PageInfoClassList.Count, owner.g_PageIndex), Logger.GetLogFileName());

                SharedPageInfoClass currentPage = GetPageDefinitionByIndex(owner.g_PageIndex);
                if (currentPage == null)
                {
                    return null;
                }

                owner.g_CurrentPageName = currentPage.PIC_PageName;
                int playTimeHour = currentPage.PIC_PlaytimeHour;
                int playTimeMin = currentPage.PIC_PlaytimeMinute;
                int playTimeSec = currentPage.PIC_PlaytimeSecond;

                owner.g_PageIndex++;
                owner.g_TimeInterval = (playTimeHour * 60 * 60) + (playTimeMin * 60) + playTimeSec;

                string playlistName = currentPageListName;
                if (string.IsNullOrWhiteSpace(playlistName))
                {
                    playlistName = owner?.g_PlayerInfoManager?.g_PlayerInfo?.PIF_CurrentPlayList;
                }

                DetectMissingContentsForPlaylist(playlistName);

                owner.g_ElementInfoManager.g_ElementInfoClassList = new List<ElementInfoClass>();
                if (currentPage.PIC_Elements != null)
                {
                    foreach (SharedElementInfoClass element in currentPage.PIC_Elements)
                    {
                        ElementInfoClass clone = new ElementInfoClass();
                        clone.CopyData(element);
                        owner.g_ElementInfoManager.g_ElementInfoClassList.Add(clone);
                    }
                }

                owner.SendHeartbeatNow();

                return new SeamlessPagePlaybackRequest
                {
                    CurrentPage = currentPage,
                    NextPage = GetPageDefinitionByIndex(owner.g_PageIndex),
                    PlaylistName = playlistName
                };
            }
            catch (Exception ex)
            {
                Logger.WriteLog("seamless 다음 페이지 요청 생성 실패로 인해 g_PageIndex를 0 으로 세팅.", Logger.GetLogFileName());
                owner.g_PageIndex = 0;
                Logger.WriteLog(ex.ToString(), Logger.GetLogFileName());
                return null;
            }
        }

        private void EvaluateSchedule(bool force)
        {
            if (owner?.ScheduleEvaluatorService == null)
            {
                return;
            }

            DateTime now = DateTime.Now;
            if (!force
                && lastScheduleEval > DateTime.MinValue
                && (now - lastScheduleEval).TotalMilliseconds < 700)
            {
                return;
            }

            lastScheduleEval = now;
            var decision = owner.ScheduleEvaluatorService.Evaluate(now, owner.g_PlayerInfoManager?.g_PlayerInfo?.PIF_DefaultPlayList);
            if (decision == null || string.IsNullOrWhiteSpace(decision.PlaylistName))
            {
                ClearPendingScheduleState();
                ClearRequestedWarmScheduleState();
                ClearReservedLayoutState();
                lastScheduleEvalStateKey = "NONE";
                lastMissingScheduleLogged = string.Empty;
                return;
            }

            string current = owner.g_PlayerInfoManager?.g_PlayerInfo?.PIF_CurrentPlayList ?? string.Empty;
            if (string.Equals(decision.PlaylistName, current, StringComparison.OrdinalIgnoreCase))
            {
                ClearPendingScheduleState();
                lastMissingScheduleLogged = string.Empty;
                string stateKey = $"KEEP|{decision.ScheduleId}|{decision.PlaylistName}";
                if (!string.Equals(lastScheduleEvalStateKey, stateKey, StringComparison.Ordinal))
                {
                    Logger.WriteLog($"스케줄 평가: 현재 플레이리스트 유지 ({decision.PlaylistName})", Logger.GetLogFileName());
                    lastScheduleEvalStateKey = stateKey;
                }

                TryWarmUpcomingSchedule(decision, now);
            }
            else
            {
                pendingSchedulePlaylist = decision.PlaylistName;
                pendingScheduleId = decision.ScheduleId ?? string.Empty;
                string pendingStateKey = BuildPendingScheduleStateKey(pendingScheduleId, pendingSchedulePlaylist);
                lastMissingScheduleLogged = string.Empty;

                if (!string.Equals(lastScheduleEvalStateKey, pendingStateKey, StringComparison.Ordinal))
                {
                    Logger.WriteLog($"스케줄 평가: 전환 예약 -> {pendingSchedulePlaylist}", Logger.GetLogFileName());
                    lastScheduleEvalStateKey = pendingStateKey;
                }

                WarmupPendingScheduleLayout(
                    pendingStateKey,
                    pendingSchedulePlaylist,
                    autoSwitchWhenReady: true);
            }
        }

        private bool TryApplyScheduledSwitch(bool isPageBoundary, bool isContentBoundary)
        {
            if (string.IsNullOrWhiteSpace(pendingSchedulePlaylist))
            {
                return false;
            }

            string timing = owner?.g_LocalSettingsManager?.Settings?.SwitchTiming ?? "Immediately";
            bool allow = false;
            if (timing.Equals("Immediately", StringComparison.OrdinalIgnoreCase))
            {
                allow = true;
            }
            else if (timing.Equals("ContentEnd", StringComparison.OrdinalIgnoreCase) && isContentBoundary)
            {
                allow = true;
            }
            else if (timing.Equals("PageEnd", StringComparison.OrdinalIgnoreCase) && isPageBoundary)
            {
                allow = true;
            }

            if (!allow)
            {
                return false;
            }

            if (initialized
                && activeLayoutIndex >= 0
                && !HasPreparedScheduleLayout(pendingSchedulePlaylist))
            {
                string pendingStateKey = BuildPendingScheduleStateKey(pendingScheduleId, pendingSchedulePlaylist);
                WarmupPendingScheduleLayout(
                    pendingStateKey,
                    pendingSchedulePlaylist,
                    autoSwitchWhenReady: true);
                return false;
            }

            return ApplyPendingSchedulePlaylist();
        }

        private bool TryApplyPendingPlaylistReload(bool isPageBoundary, bool isContentBoundary)
        {
            if (string.IsNullOrWhiteSpace(pendingPlaylistReload))
            {
                return false;
            }

            string timing = owner?.g_LocalSettingsManager?.Settings?.SwitchTiming ?? "Immediately";
            bool allow = false;
            if (timing.Equals("Immediately", StringComparison.OrdinalIgnoreCase))
            {
                allow = true;
            }
            else if (timing.Equals("ContentEnd", StringComparison.OrdinalIgnoreCase) && isContentBoundary)
            {
                allow = true;
            }
            else if (timing.Equals("PageEnd", StringComparison.OrdinalIgnoreCase) && isPageBoundary)
            {
                allow = true;
            }

            if (!allow)
            {
                return false;
            }

            var playerInfo = owner?.g_PlayerInfoManager?.g_PlayerInfo;
            if (playerInfo == null)
            {
                return false;
            }

            string current = playerInfo.PIF_CurrentPlayList ?? string.Empty;
            if (!string.Equals(current, pendingPlaylistReload, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            pendingPlaylistReload = string.Empty;
            pendingPlaylistReloadReason = string.Empty;

            UpdateCurrentPageListName(current);
            return true;
        }

        private bool HasPlayableContent(string playlistName)
        {
            if (string.IsNullOrWhiteSpace(playlistName))
            {
                return false;
            }

            try
            {
                using (var plRepo = new PageListRepository())
                using (var pageRepo = new PageRepository())
                {
                    var pageList = plRepo.FindOne(x => string.Equals(x.PLI_PageListName, playlistName, StringComparison.OrdinalIgnoreCase));
                    if (pageList == null || pageList.PLI_Pages == null || pageList.PLI_Pages.Count == 0)
                    {
                        return false;
                    }

                    var pages = pageRepo.LoadAll()
                        .Where(p => pageList.PLI_Pages.Any(id => string.Equals(id, p.PIC_GUID, StringComparison.OrdinalIgnoreCase)))
                        .ToList();
                    foreach (var page in pages)
                    {
                        if (PageHasPlayableContent(page))
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

        private bool PageHasPlayableContent(SharedPageInfoClass page)
        {
            if (page?.PIC_Elements == null)
            {
                return false;
            }

            foreach (var element in page.PIC_Elements)
            {
                if (!TryParseDisplayType(element?.EIF_Type, out DisplayType displayType)
                    || displayType != DisplayType.Media)
                {
                    continue;
                }

                if (element?.EIF_ContentsInfoClassList == null)
                {
                    continue;
                }

                foreach (var content in element.EIF_ContentsInfoClassList)
                {
                    if (content == null || string.IsNullOrWhiteSpace(content.CIF_FileName))
                    {
                        continue;
                    }

                    string path = FNDTools.GetContentsFilePath(content.CIF_FileName);
                    if (!File.Exists(path))
                    {
                        continue;
                    }

                    try
                    {
                        if (new FileInfo(path).Length > 0)
                        {
                            return true;
                        }
                    }
                    catch
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private bool TryFindPlayablePlaylist(out string playlistName)
        {
            playlistName = string.Empty;

            try
            {
                using (var plRepo = new PageListRepository())
                using (var pageRepo = new PageRepository())
                {
                    var pageLists = plRepo.LoadAll() ?? new List<PageListInfoClass>();
                    if (pageLists.Count == 0)
                    {
                        return false;
                    }

                    var pages = pageRepo.LoadAll() ?? new List<PageInfoClass>();
                    var pageMap = pages
                        .Where(p => p != null && !string.IsNullOrWhiteSpace(p.PIC_GUID))
                        .ToDictionary(p => p.PIC_GUID, p => p, StringComparer.OrdinalIgnoreCase);

                    foreach (var pageList in pageLists)
                    {
                        if (pageList?.PLI_Pages == null || pageList.PLI_Pages.Count == 0)
                        {
                            continue;
                        }

                        foreach (var pageId in pageList.PLI_Pages)
                        {
                            if (string.IsNullOrWhiteSpace(pageId))
                            {
                                continue;
                            }

                            if (!pageMap.TryGetValue(pageId, out var page))
                            {
                                continue;
                            }

                            if (PageHasPlayableContent(page))
                            {
                                playlistName = pageList.PLI_PageListName ?? string.Empty;
                                return !string.IsNullOrWhiteSpace(playlistName);
                            }
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

        private void ApplyLocalPlaylist(string playlistName, bool updateDefaultIfEmpty, string reason)
        {
            if (string.IsNullOrWhiteSpace(playlistName))
            {
                return;
            }

            var info = owner?.g_PlayerInfoManager?.g_PlayerInfo;
            if (info == null)
            {
                return;
            }

            bool changed = !string.Equals(info.PIF_CurrentPlayList, playlistName, StringComparison.OrdinalIgnoreCase);
            bool updated = false;
            if (changed)
            {
                info.PIF_CurrentPlayList = playlistName;
                updated = true;
            }

            if (updateDefaultIfEmpty && string.IsNullOrWhiteSpace(info.PIF_DefaultPlayList))
            {
                info.PIF_DefaultPlayList = playlistName;
                updated = true;
            }

            if (updated)
            {
                owner.g_PlayerInfoManager.SaveData();
            }

            bool needsReload = !string.Equals(currentPageListName, playlistName, StringComparison.OrdinalIgnoreCase)
                               || owner?.g_PageInfoManager?.g_PageInfoClassList == null
                               || owner.g_PageInfoManager.g_PageInfoClassList.Count == 0;
            if (needsReload)
            {
                UpdateCurrentPageListName(playlistName);
            }

            if (!string.IsNullOrWhiteSpace(reason)
                && !string.Equals(lastLocalFallbackPlaylist, playlistName, StringComparison.OrdinalIgnoreCase))
            {
                Logger.WriteLog($"로컬 재생 플레이리스트 적용({reason}): {playlistName}", Logger.GetLogFileName());
                lastLocalFallbackPlaylist = playlistName;
            }
        }

        private bool EnsureLocalPlaybackReady()
        {
            try
            {
                var playerInfo = owner?.g_PlayerInfoManager?.g_PlayerInfo;
                if (playerInfo == null)
                {
                    return false;
                }

                string current = playerInfo.PIF_CurrentPlayList;
                string fallback = playerInfo.PIF_DefaultPlayList;

                if (!string.IsNullOrWhiteSpace(current) && HasPlayableContent(current))
                {
                    ApplyLocalPlaylist(current, updateDefaultIfEmpty: false, reason: null);
                    return true;
                }

                if (!string.IsNullOrWhiteSpace(fallback) && HasPlayableContent(fallback))
                {
                    ApplyLocalPlaylist(fallback, updateDefaultIfEmpty: false, reason: "default");
                    return true;
                }

                if (TryFindPlayablePlaylist(out var playlist))
                {
                    bool updateDefault = string.IsNullOrWhiteSpace(fallback) || !HasPlayableContent(fallback);
                    ApplyLocalPlaylist(playlist, updateDefaultIfEmpty: updateDefault, reason: "fallback");
                    return true;
                }

                if (!string.Equals(lastLocalFallbackPlaylist, "__NONE__", StringComparison.Ordinal))
                {
                    Logger.WriteLog("로컬 데이터에서 재생 가능한 플레이리스트를 찾지 못했습니다.", Logger.GetLogFileName());
                    lastLocalFallbackPlaylist = "__NONE__";
                }
                return false;
            }
            catch (Exception ex)
            {
                Logger.WriteErrorLog(ex.ToString(), Logger.GetLogFileName());
                return false;
            }
        }

        private void HandleMissingScheduleContent(string playlistName)
        {
            if (string.IsNullOrWhiteSpace(playlistName))
            {
                return;
            }

            if (!string.Equals(lastMissingScheduleLogged, playlistName, StringComparison.OrdinalIgnoreCase))
            {
                Logger.WriteLog($"플레이리스트({playlistName})에 필요한 파일이 없어 전환을 대기합니다. 다운로드를 재시도합니다.", Logger.GetLogFileName());
                lastMissingScheduleLogged = playlistName;
                owner?.CommandService?.EnsurePlaylistDownloadFromCache(playlistName);
            }
        }

        private void WarmupPendingScheduleLayout(string stateKey, string playlistName, bool autoSwitchWhenReady)
        {
            if (string.IsNullOrWhiteSpace(playlistName)
                || string.IsNullOrWhiteSpace(stateKey))
            {
                return;
            }

            if (!HasPlayableContent(playlistName))
            {
                return;
            }

            if (!initialized || activeLayoutIndex < 0 || activeLayoutIndex >= layouts.Length)
            {
                return;
            }

            if (HasPreparedScheduleLayout(playlistName))
            {
                if (autoSwitchWhenReady)
                {
                    Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        TryStartPreparedScheduleSwitch(playlistName);
                    }));
                }
                return;
            }

            if (string.Equals(requestedWarmScheduleStateKey, stateKey, StringComparison.Ordinal)
                && string.Equals(requestedWarmSchedulePlaylist, playlistName, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            requestedWarmScheduleStateKey = stateKey;
            requestedWarmSchedulePlaylist = playlistName;
            requestedWarmScheduleAutoSwitch = autoSwitchWhenReady;

            Task.Run(async () =>
            {
                try
                {
                    SeamlessPagePlan firstPlan = await BuildInitialPagePlanAsync(playlistName).ConfigureAwait(false);
                    if (firstPlan == null)
                    {
                        return;
                    }

                    int version = Volatile.Read(ref playbackVersion);
                    if (!IsWarmScheduleStillValid(stateKey, playlistName, autoSwitchWhenReady, version))
                    {
                        return;
                    }

                    int reserveIndex = GetReservedLayoutIndex();
                    if (reserveIndex < 0 || reserveIndex >= layouts.Length)
                    {
                        return;
                    }

                    bool prepared = await PrepareLayoutAsync(reserveIndex, firstPlan, version).ConfigureAwait(false);
                    if (!prepared || !IsWarmScheduleStillValid(stateKey, playlistName, autoSwitchWhenReady, version))
                    {
                        return;
                    }

                    layouts[reserveIndex].Deactivate();
                    reservedLayoutIndex = reserveIndex;
                    reservedLayoutStateKey = stateKey;
                    reservedLayoutPlaylist = playlistName;
                    Logger.WriteLog($"스케줄 전환 선준비 완료: {playlistName}", Logger.GetLogFileName());

                    if (autoSwitchWhenReady)
                    {
                        Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
                        {
                            TryStartPreparedScheduleSwitch(playlistName);
                        }));
                    }
                }
                catch (Exception ex)
                {
                    if (!HasPreparedScheduleLayout(playlistName))
                    {
                        Logger.WriteErrorLog($"스케줄 전환 선준비 실패({playlistName}): {ex}", Logger.GetLogFileName());
                    }
                }
                finally
                {
                    if (string.Equals(requestedWarmScheduleStateKey, stateKey, StringComparison.Ordinal)
                        && string.Equals(requestedWarmSchedulePlaylist, playlistName, StringComparison.OrdinalIgnoreCase))
                    {
                        ClearRequestedWarmScheduleState();
                    }
                }
            });
        }

        private bool IsWarmScheduleStillValid(string stateKey, string playlistName, bool autoSwitchWhenReady, int version)
        {
            if (!IsVersionCurrent(version))
            {
                return false;
            }

            if (!string.Equals(requestedWarmScheduleStateKey, stateKey, StringComparison.Ordinal))
            {
                return false;
            }

            if (!string.Equals(requestedWarmSchedulePlaylist, playlistName, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return requestedWarmScheduleAutoSwitch == autoSwitchWhenReady;
        }

        private async Task<SeamlessPagePlan> BuildInitialPagePlanAsync(string playlistName)
        {
            SharedPageInfoClass firstPage = GetFirstPageDefinitionForPlaylist(playlistName);
            if (firstPage == null)
            {
                return null;
            }

            return await coordinator.BuildPagePlanAsync(firstPage, playlistName).ConfigureAwait(false);
        }

        private SharedPageInfoClass GetFirstPageDefinitionForPlaylist(string playlistName)
        {
            if (string.IsNullOrWhiteSpace(playlistName))
            {
                return null;
            }

            try
            {
                using (var pageListRepo = new PageListRepository())
                using (var pageRepo = new PageRepository())
                {
                    var pageList = pageListRepo.FindOne(x => string.Equals(x.PLI_PageListName, playlistName, StringComparison.OrdinalIgnoreCase));
                    if (pageList?.PLI_Pages == null || pageList.PLI_Pages.Count == 0)
                    {
                        return null;
                    }

                    var orderedIds = pageList.PLI_Pages.ToList();
                    var pageMap = pageRepo.Find(x => orderedIds.Contains(x.PIC_GUID))
                        .Where(x => x != null && !string.IsNullOrWhiteSpace(x.PIC_GUID))
                        .ToDictionary(x => x.PIC_GUID, x => x, StringComparer.OrdinalIgnoreCase);

                    foreach (string pageId in orderedIds)
                    {
                        if (string.IsNullOrWhiteSpace(pageId))
                        {
                            continue;
                        }

                        if (!pageMap.TryGetValue(pageId, out var storedPage) || storedPage == null)
                        {
                            continue;
                        }

                        PageInfoClass clone = new PageInfoClass();
                        clone.CopyData(storedPage);
                        return clone;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.WriteErrorLog($"플레이리스트 첫 페이지 조회 실패({playlistName}): {ex}", Logger.GetLogFileName());
            }

            return null;
        }

        private void DetectMissingContentsForPlaylist(string playlistName)
        {
            if (string.IsNullOrWhiteSpace(playlistName))
            {
                return;
            }

            try
            {
                using (var plRepo = new PageListRepository())
                using (var pageRepo = new PageRepository())
                {
                    var pageList = plRepo.FindOne(x => string.Equals(x.PLI_PageListName, playlistName, StringComparison.OrdinalIgnoreCase));
                    if (pageList == null || pageList.PLI_Pages == null || pageList.PLI_Pages.Count == 0)
                    {
                        return;
                    }

                    var pages = pageRepo.LoadAll()
                        .Where(p => pageList.PLI_Pages.Any(id => string.Equals(id, p.PIC_GUID, StringComparison.OrdinalIgnoreCase)))
                        .ToList();

                    List<string> missing = new List<string>();
                    foreach (var page in pages)
                    {
                        if (page?.PIC_Elements == null)
                        {
                            continue;
                        }

                        foreach (var element in page.PIC_Elements)
                        {
                            if (element?.EIF_ContentsInfoClassList == null)
                            {
                                continue;
                            }

                            foreach (var content in element.EIF_ContentsInfoClassList)
                            {
                                if (content == null || string.IsNullOrWhiteSpace(content.CIF_FileName))
                                {
                                    continue;
                                }

                                string path = FNDTools.GetContentsFilePath(content.CIF_FileName);
                                if (!File.Exists(path))
                                {
                                    missing.Add(content.CIF_FileName);
                                }
                            }
                        }
                    }

                    if (missing.Count > 0)
                    {
                        Logger.WriteLog($"누락된 컨텐츠 발견({playlistName}): {string.Join(",", missing.Distinct())}", Logger.GetLogFileName());
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.WriteErrorLog(ex.ToString(), Logger.GetLogFileName());
            }
        }

        private SharedPageInfoClass GetPageDefinitionByIndex(int index)
        {
            if (owner?.g_PageInfoManager == null
                || owner.g_PageInfoManager.g_PageInfoClassList == null
                || owner.g_PageInfoManager.g_PageInfoClassList.Count == 0)
            {
                return null;
            }

            if (index < 0)
            {
                index = 0;
            }

            if (index >= owner.g_PageInfoManager.g_PageInfoClassList.Count)
            {
                index = 0;
            }

            string pageName = owner.g_PageInfoManager.g_PageInfoClassList[index].PIC_PageName;
            return owner.g_PageInfoManager.GetPageDefinition(pageName);
        }

        private static bool TryParseDisplayType(string rawType, out DisplayType displayType)
        {
            displayType = DisplayType.None;
            if (string.IsNullOrWhiteSpace(rawType))
            {
                return false;
            }

            return Enum.TryParse(rawType, true, out displayType);
        }

        private int GetActivationLayoutIndex(SeamlessPagePlan currentPlan)
        {
            lock (stateLock)
            {
                if (nextPageLayoutIndex >= 0
                    && nextPageLayoutIndex < layouts.Length
                    && nextPageLayoutIndex != activeLayoutIndex
                    && layouts[nextPageLayoutIndex].State == SeamlessLayoutState.Ready
                    && layouts[nextPageLayoutIndex].Matches(currentPlan))
                {
                    return nextPageLayoutIndex;
                }

                for (int i = 0; i < layouts.Length; i++)
                {
                    if (i == activeLayoutIndex || i == nextPageLayoutIndex)
                    {
                        continue;
                    }

                    if (layouts[i].State == SeamlessLayoutState.Ready && layouts[i].Matches(currentPlan))
                    {
                        return i;
                    }
                }

                if (activeLayoutIndex < 0)
                {
                    return 0;
                }

                for (int i = 0; i < layouts.Length; i++)
                {
                    if (i == activeLayoutIndex || i == reservedLayoutIndex)
                    {
                        continue;
                    }

                    return i;
                }

                for (int i = 0; i < layouts.Length; i++)
                {
                    if (i == activeLayoutIndex)
                    {
                        continue;
                    }

                    return i;
                }

                return 0;
            }
        }

        private int GetStandbyLayoutIndex(int targetLayoutIndex)
        {
            lock (stateLock)
            {
                for (int i = 0; i < layouts.Length; i++)
                {
                    if (i == targetLayoutIndex || i == reservedLayoutIndex)
                    {
                        continue;
                    }

                    return i;
                }

                for (int i = 0; i < layouts.Length; i++)
                {
                    if (i == targetLayoutIndex)
                    {
                        continue;
                    }

                    return i;
                }

                return -1;
            }
        }

        private int GetReservedLayoutIndex()
        {
            lock (stateLock)
            {
                if (!initialized || activeLayoutIndex < 0)
                {
                    return -1;
                }

                for (int i = 0; i < layouts.Length; i++)
                {
                    if (i == activeLayoutIndex || i == nextPageLayoutIndex)
                    {
                        continue;
                    }

                    return i;
                }

                return -1;
            }
        }

        private bool IsReservedLayoutReady(string stateKey, string playlistName)
        {
            return reservedLayoutIndex >= 0
                && reservedLayoutIndex < layouts.Length
                && layouts[reservedLayoutIndex].State == SeamlessLayoutState.Ready
                && string.Equals(reservedLayoutStateKey, stateKey, StringComparison.Ordinal)
                && string.Equals(reservedLayoutPlaylist, playlistName, StringComparison.OrdinalIgnoreCase);
        }

        private bool HasPreparedScheduleLayout(string playlistName)
        {
            return reservedLayoutIndex >= 0
                && reservedLayoutIndex < layouts.Length
                && layouts[reservedLayoutIndex].State == SeamlessLayoutState.Ready
                && string.Equals(reservedLayoutPlaylist, playlistName, StringComparison.OrdinalIgnoreCase);
        }

        private bool ApplyPendingSchedulePlaylist()
        {
            var playerInfo = owner?.g_PlayerInfoManager?.g_PlayerInfo;
            if (playerInfo == null || string.IsNullOrWhiteSpace(pendingSchedulePlaylist))
            {
                return false;
            }

            if (!HasPlayableContent(pendingSchedulePlaylist))
            {
                HandleMissingScheduleContent(pendingSchedulePlaylist);
                return false;
            }

            lastMissingScheduleLogged = string.Empty;
            playerInfo.PIF_CurrentPlayList = pendingSchedulePlaylist;
            owner.g_PlayerInfoManager.SaveData();

            string switchedPlaylist = pendingSchedulePlaylist;
            ClearPendingScheduleState();

            UpdateCurrentPageListName(playerInfo.PIF_CurrentPlayList);

            if (!string.Equals(reservedLayoutPlaylist, switchedPlaylist, StringComparison.OrdinalIgnoreCase))
            {
                ClearReservedLayoutState();
            }

            return true;
        }

        private void TryStartPreparedScheduleSwitch(string playlistName)
        {
            if (string.IsNullOrWhiteSpace(playlistName))
            {
                return;
            }

            if (!string.Equals(pendingSchedulePlaylist, playlistName, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (!HasPreparedScheduleLayout(playlistName))
            {
                return;
            }

            if (ApplyPendingSchedulePlaylist())
            {
                PlayNextPage();
            }
        }

        private void TryWarmUpcomingSchedule(ScheduleDecision decision, DateTime now)
        {
            if (decision == null
                || string.IsNullOrWhiteSpace(decision.NextPlaylistName)
                || decision.NextSwitchAt <= DateTime.MinValue
                || string.Equals(decision.NextPlaylistName, decision.PlaylistName, StringComparison.OrdinalIgnoreCase))
            {
                ClearRequestedWarmScheduleState();
                if (!string.Equals(reservedLayoutPlaylist, pendingSchedulePlaylist, StringComparison.OrdinalIgnoreCase))
                {
                    ClearReservedLayoutState();
                }
                return;
            }

            if (decision.NextSwitchAt > now.AddMinutes(1))
            {
                ClearRequestedWarmScheduleState();
                if (!string.Equals(reservedLayoutPlaylist, pendingSchedulePlaylist, StringComparison.OrdinalIgnoreCase))
                {
                    ClearReservedLayoutState();
                }
                return;
            }

            string warmStateKey = BuildWarmScheduleStateKey(decision.NextScheduleId, decision.NextPlaylistName, decision.NextSwitchAt);
            WarmupPendingScheduleLayout(
                warmStateKey,
                decision.NextPlaylistName,
                autoSwitchWhenReady: false);
        }

        private static string BuildPendingScheduleStateKey(string scheduleId, string playlistName)
        {
            return $"PENDING|{scheduleId ?? string.Empty}|{playlistName ?? string.Empty}";
        }

        private static string BuildWarmScheduleStateKey(string scheduleId, string playlistName, DateTime switchAt)
        {
            return $"WARM|{scheduleId ?? string.Empty}|{playlistName ?? string.Empty}|{switchAt:O}";
        }

        private void ClearPendingScheduleState()
        {
            pendingSchedulePlaylist = string.Empty;
            pendingScheduleId = string.Empty;
        }

        private void ClearRequestedWarmScheduleState()
        {
            requestedWarmScheduleStateKey = string.Empty;
            requestedWarmSchedulePlaylist = string.Empty;
            requestedWarmScheduleAutoSwitch = false;
        }

        private void ClearReservedLayoutState()
        {
            reservedLayoutIndex = -1;
            reservedLayoutStateKey = string.Empty;
            reservedLayoutPlaylist = string.Empty;
        }

        private bool IsVersionCurrent(int version)
        {
            return version == Volatile.Read(ref playbackVersion);
        }

        private async Task<bool> PrepareLayoutAsync(int layoutIndex, SeamlessPagePlan plan, int version)
        {
            await layoutPrepareGate.WaitAsync().ConfigureAwait(false);
            try
            {
                Size viewportSize = owner != null
                    ? owner.GetSeamlessViewportSize()
                    : new Size(plan?.CanvasWidth ?? 1920, plan?.CanvasHeight ?? 1080);
                bool preserveAspectRatio = owner?.IsPreserveAspectRatioEnabled() ?? true;
                await layouts[layoutIndex].PrepareAsync(plan, viewportSize.Width, viewportSize.Height, preserveAspectRatio).ConfigureAwait(false);
                return IsVersionCurrent(version);
            }
            catch (Exception ex)
            {
                Logger.WriteErrorLog($"레이아웃 준비 실패({layouts[layoutIndex].LayoutName}): {ex}", Logger.GetLogFileName());
                layouts[layoutIndex].Clear();
                return false;
            }
            finally
            {
                layoutPrepareGate.Release();
            }
        }

        private SeamlessLayoutRuntime GetActiveLayout()
        {
            if (activeLayoutIndex < 0 || activeLayoutIndex >= layouts.Length)
            {
                return null;
            }

            return layouts[activeLayoutIndex];
        }

        private int GetLayoutIndex(SeamlessLayoutRuntime layout)
        {
            for (int i = 0; i < layouts.Length; i++)
            {
                if (ReferenceEquals(layouts[i], layout))
                {
                    return i;
                }
            }

            return -1;
        }

        private void BringLayoutToFront(int layoutIndex)
        {
            if (layoutIndex < 0 || layoutIndex >= layouts.Length)
            {
                return;
            }

            int zIndex = Interlocked.Increment(ref hostZOrderSeed);
            hostCanvas.Dispatcher.Invoke(() =>
            {
                Panel.SetZIndex(layouts[layoutIndex].Host, zIndex);
            });
        }

        private bool IsCompletionCurrent(SeamlessLayoutRuntime completedLayout, int completedActivationVersion)
        {
            return IsLayoutCurrent(completedLayout, completedActivationVersion);
        }

        private bool IsLayoutCurrent(SeamlessLayoutRuntime layout, int activationVersion)
        {
            if (activationVersion != Volatile.Read(ref playbackVersion))
            {
                return false;
            }

            SeamlessLayoutRuntime active = GetActiveLayout();
            return ReferenceEquals(active, layout);
        }
        public void Dispose()
        {
            StopAll();
        }
    }
}
