using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Threading;
using NewHyOnPlayer.DataManager;
using TurtleTools;
using SharedElementInfoClass = AndoW.Shared.ElementInfoClass;
using SharedPageInfoClass = AndoW.Shared.PageInfoClass;

namespace NewHyOnPlayer.PlaybackModes
{
    internal sealed class SeamlessSyncPlaybackContainer : IPlaybackContainer
    {
        private const long NoPendingPositionMilliseconds = -1L;
        private static readonly TimeSpan PositionSyncLeadTime = TimeSpan.FromMilliseconds(100);
        private static readonly TimeSpan PositionSyncRemainingThreshold = TimeSpan.FromSeconds(1);

        private readonly MainWindow owner;
        private readonly Canvas hostCanvas;
        private readonly PlaybackCoordinator coordinator;
        private readonly SeamlessMpvSurface surface;
        private readonly DispatcherTimer pageTimer;
        private readonly DispatcherTimer contentTimer;
        private readonly DispatcherTimer blankRecoveryTimer;
        private readonly SemaphoreSlim loadGate = new SemaphoreSlim(1, 1);

        private bool initialized;
        private bool presentationActive;
        private int playbackVersion;
        private int currentPageDurationSeconds = 1;
        private int currentPlaylistIndex = -1;
        private int pendingSyncIndex = -1;
        private long pendingSyncPositionMilliseconds = NoPendingPositionMilliseconds;
        private long pendingLoadedSeekPositionMilliseconds = NoPendingPositionMilliseconds;
        private string currentPageListName = string.Empty;
        private DateTime currentPageStartedAtUtc = DateTime.MinValue;
        private DateTime lastBlankRecoveryAttempt = DateTime.MinValue;
        private bool blankRecoveryPending;
        private SeamlessSlotPlan currentSlotPlan = new SeamlessSlotPlan();
        private SeamlessContentItem[] currentItems = Array.Empty<SeamlessContentItem>();

        public SeamlessSyncPlaybackContainer(MainWindow owner, Canvas hostCanvas)
        {
            this.owner = owner;
            this.hostCanvas = hostCanvas;
            coordinator = new PlaybackCoordinator(owner);
            surface = new SeamlessMpvSurface();

            Dispatcher dispatcher = hostCanvas != null ? hostCanvas.Dispatcher : Dispatcher.CurrentDispatcher;
            pageTimer = new DispatcherTimer(DispatcherPriority.Background, dispatcher);
            pageTimer.Tick += PageTimer_Tick;
            contentTimer = new DispatcherTimer(DispatcherPriority.Background, dispatcher);
            contentTimer.Tick += ContentTimer_Tick;
            blankRecoveryTimer = new DispatcherTimer(DispatcherPriority.Background, dispatcher)
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            blankRecoveryTimer.Tick += BlankRecoveryTimer_Tick;
            surface.MediaLoaded += Surface_MediaLoaded;
        }

        public event Action<int> PlaylistIndexChangeRequested;
        public event Action SyncIndexRequestNeeded;
        public event Action<bool> PresentationVisibilityChanged;

        public int CurrentPageElapsedSeconds
        {
            get
            {
                if (currentPageStartedAtUtc <= DateTime.MinValue)
                {
                    return 0;
                }

                return Math.Max(0, (int)(DateTime.UtcNow - currentPageStartedAtUtc).TotalSeconds);
            }
        }

        public int CurrentPageDurationSeconds
        {
            get { return Math.Max(1, currentPageDurationSeconds); }
        }

        public bool IsOnlySinglePage
        {
            get
            {
                return owner?.g_PageInfoManager?.g_PageInfoClassList != null
                    && owner.g_PageInfoManager.g_PageInfoClassList.Count <= 1;
            }
        }

        public string CurrentPageListName
        {
            get { return currentPageListName; }
        }

        public string CurrentPageName
        {
            get { return owner?.g_CurrentPageName ?? string.Empty; }
        }

        public string NextPageName
        {
            get
            {
                SharedPageInfoClass nextPage = GetPageDefinitionByIndex(owner?.g_PageIndex ?? 0);
                return nextPage?.PIC_PageName ?? string.Empty;
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
                hostCanvas.Children.Add(surface);
                Canvas.SetLeft(surface, 0);
                Canvas.SetTop(surface, 0);
                Panel.SetZIndex(surface, 0);
                surface.HideSurface();
            });
            blankRecoveryTimer.Start();
        }

        public void StartInitialPlayback(string defaultPlaylist)
        {
            if (!string.IsNullOrWhiteSpace(defaultPlaylist))
            {
                UpdateCurrentPageListName(defaultPlaylist);
            }
            else
            {
                EnsureCurrentPlaylistLoaded();
            }

            owner.g_PageIndex = 0;
            PlayNextPage();
        }

        public bool IsPresentationActive()
        {
            return presentationActive;
        }

        public bool TryGetPresentationState(out bool isVisible, out int playlistIndex, out TimeSpan position)
        {
            isVisible = presentationActive;
            playlistIndex = -1;
            position = TimeSpan.Zero;

            if (!presentationActive)
            {
                return true;
            }

            if (currentItems == null || currentItems.Length == 0)
            {
                return false;
            }

            int activeIndex = GetCurrentPlaylistIndex();
            if (activeIndex < 0)
            {
                return false;
            }

            playlistIndex = activeIndex;
            if (surface.IsMediaLoaded)
            {
                position = NormalizeSyncPosition(activeIndex, surface.Position + PositionSyncLeadTime);
            }

            return true;
        }

        public List<PlaybackDebugItem> GetDebugItems()
        {
            int currentIndex = GetCurrentPlaylistIndex();
            SeamlessContentItem currentItem = GetItemByIndex(currentIndex);
            SeamlessContentItem nextItem = GetItemByIndex(GetNextIndex(currentIndex));

            return new List<PlaybackDebugItem>
            {
                new PlaybackDebugItem
                {
                    ElementName = currentSlotPlan != null ? currentSlotPlan.ElementName : string.Empty,
                    CurrentContentName = currentItem?.Source?.CIF_FileName ?? string.Empty,
                    NextContentName = nextItem?.Source?.CIF_FileName ?? string.Empty,
                    ElapsedSeconds = CurrentPageElapsedSeconds,
                    DurationSeconds = CurrentPageDurationSeconds,
                    IsVisible = presentationActive,
                    LayoutName = "Sync-Single",
                    SlotIndex = 0,
                    LayoutState = presentationActive ? SeamlessLayoutState.Active : SeamlessLayoutState.Ready,
                    SlotState = presentationActive ? SeamlessSlotState.Active : SeamlessSlotState.Ready
                }
            };
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
            if (owner?.ScheduleEvaluatorService == null)
            {
                return;
            }

            var playerInfo = owner?.g_PlayerInfoManager?.g_PlayerInfo;
            string fallbackPlaylist = playerInfo?.PIF_DefaultPlayList;
            var decision = owner.ScheduleEvaluatorService.Evaluate(DateTime.Now, fallbackPlaylist);
            if (decision == null || string.IsNullOrWhiteSpace(decision.PlaylistName))
            {
                return;
            }

            string current = playerInfo?.PIF_CurrentPlayList ?? string.Empty;
            if (string.Equals(current, decision.PlaylistName, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (playerInfo != null)
            {
                playerInfo.PIF_CurrentPlayList = decision.PlaylistName;
                owner.g_PlayerInfoManager.SaveData();
            }

            UpdateCurrentPageListName(decision.PlaylistName);
            PlayFirstPage();
        }

        public void HandleWeeklyScheduleUpdated()
        {
            owner?.ScheduleEvaluatorService?.InvalidateWeeklyCache();
            RequestScheduleEvaluation(force: true);
            owner?.OnAirServiceInstance?.RefreshWeeklySchedule();
        }

        public void HandleContentPeriodUpdated(IReadOnlyCollection<string> contentGuids)
        {
            bool isSyncPlaybackActive = owner?.IsSyncPlaybackActive ?? false;
            if (isSyncPlaybackActive && !IsLeaderSyncEnabled())
            {
                return;
            }

            string playlistName = currentPageListName;
            if (string.IsNullOrWhiteSpace(playlistName))
            {
                playlistName = owner?.g_PlayerInfoManager?.g_PlayerInfo?.PIF_CurrentPlayList;
            }

            hostCanvas.Dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    int playableCount = CountPlayableItems(currentItems);
                    if (playableCount == 0)
                    {
                        if (HasPlayableContent(playlistName))
                        {
                            HideAll();
                        }
                        else
                        {
                            HideAll();
                            owner?.SetInitialLoadingVisible(false);
                        }
                        return;
                    }

                    SeamlessContentItem currentItem = GetItemByIndex(currentPlaylistIndex);
                    if (!presentationActive || currentPlaylistIndex < 0 || !IsContentPlayableNow(currentItem))
                    {
                        int startIndex = currentPlaylistIndex >= 0 ? currentPlaylistIndex + 1 : 0;
                        ApplyPlaylistIndexOnly(startIndex, IsLeaderSyncEnabled());
                    }
                }
                catch (Exception ex)
                {
                    Logger.WriteErrorLog($"sync 기간 변경 재평가 실패: {ex}", Logger.GetLogFileName());
                }
            }));
        }

        public void RequestPlaylistReload(string playlistName, string reason)
        {
            if (string.IsNullOrWhiteSpace(playlistName))
            {
                return;
            }

            UpdateCurrentPageListName(playlistName);
            PlayFirstPage();
        }

        public void StartPlaybackFromOffAir()
        {
            string playlist = owner?.g_PlayerInfoManager?.g_PlayerInfo?.PIF_CurrentPlayList;
            if (string.IsNullOrWhiteSpace(playlist))
            {
                playlist = owner?.g_PlayerInfoManager?.g_PlayerInfo?.PIF_DefaultPlayList;
            }

            if (!string.IsNullOrWhiteSpace(playlist))
            {
                UpdateCurrentPageListName(playlist);
            }

            PlayFirstPage();
        }

        public void PlayNextPage()
        {
            Initialize();
            if (!(owner?.IsPlaybackAllowedNow() ?? true))
            {
                blankRecoveryPending = false;
                owner?.SetInitialLoadingVisible(false);
                return;
            }

            EnsureCurrentPlaylistLoaded();
            StopPageTimer();
            StopContentTimer();

            int version = Interlocked.Increment(ref playbackVersion);
            Task.Run(async () =>
            {
                try
                {
                    SharedPageInfoClass page = BuildCurrentPage();
                    if (page == null || !IsVersionCurrent(version))
                    {
                        return;
                    }

                    await LoadCurrentPageAsync(version, page).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Logger.WriteErrorLog($"sync mpv 컨테이너 페이지 로드 실패: {ex}", Logger.GetLogFileName());
                }
                finally
                {
                    blankRecoveryPending = false;
                }
            });
        }

        public void PlayFirstPage()
        {
            owner.g_PageIndex = 0;
            PlayNextPage();
        }

        public void HideAll()
        {
            Interlocked.Increment(ref playbackVersion);
            StopPageTimer();
            StopContentTimer();
            SetPresentationActive(false);
            blankRecoveryPending = false;
            currentPageStartedAtUtc = DateTime.MinValue;
            hostCanvas.Dispatcher.Invoke(() =>
            {
                surface.PauseIfActive();
                surface.HideSurface();
            });
        }

        public void StopAll()
        {
            Interlocked.Increment(ref playbackVersion);
            StopPageTimer();
            StopContentTimer();
            SetPresentationActive(false);
            blankRecoveryPending = false;
            currentPageStartedAtUtc = DateTime.MinValue;
            currentPlaylistIndex = -1;
            currentSlotPlan = new SeamlessSlotPlan();
            currentItems = Array.Empty<SeamlessContentItem>();
            pendingSyncIndex = -1;
            Interlocked.Exchange(ref pendingSyncPositionMilliseconds, NoPendingPositionMilliseconds);
            Interlocked.Exchange(ref pendingLoadedSeekPositionMilliseconds, NoPendingPositionMilliseconds);
            hostCanvas.Dispatcher.Invoke(() =>
            {
                surface.Stop();
            });
        }

        public bool TryApplySyncPlaylistIndexOnly(int playlistIndex)
        {
            if (playlistIndex < 0)
            {
                return false;
            }

            Interlocked.Exchange(ref pendingSyncIndex, playlistIndex);
            Interlocked.Exchange(ref pendingSyncPositionMilliseconds, NoPendingPositionMilliseconds);
            if (currentItems == null || currentItems.Length == 0)
            {
                return false;
            }

            return ApplyPlaylistIndexOnly(playlistIndex, false);
        }

        public bool TryApplySyncPlaylistIndexWithPosition(int playlistIndex, TimeSpan position)
        {
            if (playlistIndex < 0)
            {
                return false;
            }

            Interlocked.Exchange(ref pendingSyncIndex, playlistIndex);
            Interlocked.Exchange(ref pendingSyncPositionMilliseconds, Math.Max(0L, (long)position.TotalMilliseconds));
            if (currentItems == null || currentItems.Length == 0)
            {
                return false;
            }

            return ApplyPlaylistIndexWithPosition(playlistIndex, position);
        }

        public int GetCurrentPlaylistIndex()
        {
            if (currentPlaylistIndex >= 0)
            {
                return currentPlaylistIndex;
            }

            int pendingIndex = Interlocked.CompareExchange(ref pendingSyncIndex, -1, -1);
            if (pendingIndex >= 0)
            {
                return pendingIndex;
            }

            return currentItems != null && currentItems.Length > 0 ? 0 : -1;
        }

        public void Dispose()
        {
            StopAll();
            pageTimer.Tick -= PageTimer_Tick;
            contentTimer.Tick -= ContentTimer_Tick;
            blankRecoveryTimer.Tick -= BlankRecoveryTimer_Tick;
            blankRecoveryTimer.Stop();
            surface.MediaLoaded -= Surface_MediaLoaded;
            loadGate.Dispose();
        }

        private void BlankRecoveryTimer_Tick(object sender, EventArgs e)
        {
            if (!initialized || presentationActive)
            {
                return;
            }

            if (!(owner?.IsPlaybackAllowedNow() ?? true))
            {
                return;
            }

            bool isSyncPlaybackActive = owner?.IsSyncPlaybackActive ?? false;
            if (isSyncPlaybackActive && !IsLeaderSyncEnabled())
            {
                return;
            }

            if (blankRecoveryPending)
            {
                return;
            }

            string playlistName = currentPageListName;
            if (string.IsNullOrWhiteSpace(playlistName))
            {
                playlistName = owner?.g_PlayerInfoManager?.g_PlayerInfo?.PIF_CurrentPlayList;
            }

            if (string.IsNullOrWhiteSpace(playlistName) || !HasPlayableContent(playlistName))
            {
                return;
            }

            DateTime now = DateTime.UtcNow;
            if ((now - lastBlankRecoveryAttempt).TotalSeconds < 2)
            {
                return;
            }

            lastBlankRecoveryAttempt = now;
            blankRecoveryPending = true;
            UpdateCurrentPageListName(playlistName);
            PlayNextPage();
        }

        private void EnsureCurrentPlaylistLoaded()
        {
            if (!string.IsNullOrWhiteSpace(currentPageListName))
            {
                return;
            }

            string playlist = owner?.g_PlayerInfoManager?.g_PlayerInfo?.PIF_CurrentPlayList;
            if (string.IsNullOrWhiteSpace(playlist))
            {
                playlist = owner?.g_PlayerInfoManager?.g_PlayerInfo?.PIF_DefaultPlayList;
            }

            if (!string.IsNullOrWhiteSpace(playlist))
            {
                UpdateCurrentPageListName(playlist);
            }
        }

        private SharedPageInfoClass BuildCurrentPage()
        {
            if (owner?.g_PageInfoManager?.g_PageInfoClassList == null
                || owner.g_PageInfoManager.g_PageInfoClassList.Count == 0)
            {
                return null;
            }

            if (owner.g_PageIndex < 0 || owner.g_PageIndex >= owner.g_PageInfoManager.g_PageInfoClassList.Count)
            {
                owner.g_PageIndex = 0;
            }

            SharedPageInfoClass currentPage = GetPageDefinitionByIndex(owner.g_PageIndex);
            if (currentPage == null)
            {
                return null;
            }

            owner.g_CurrentPageName = currentPage.PIC_PageName;
            owner.g_PageIndex++;
            if (owner.g_PageIndex >= owner.g_PageInfoManager.g_PageInfoClassList.Count)
            {
                owner.g_PageIndex = 0;
            }

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
            return currentPage;
        }

        private string GetCurrentContentFileName()
        {
            if (currentItems == null || currentItems.Length == 0)
            {
                return string.Empty;
            }

            int index = currentPlaylistIndex;
            if (index < 0 || index >= currentItems.Length)
            {
                index = 0;
            }

            return currentItems[index]?.Source?.CIF_FileName ?? string.Empty;
        }

        private async Task LoadCurrentPageAsync(int version, SharedPageInfoClass page)
        {
            await loadGate.WaitAsync().ConfigureAwait(false);
            try
            {
                SeamlessPagePlan plan = await coordinator.BuildPagePlanAsync(page, currentPageListName).ConfigureAwait(false);
                if (plan == null || !IsVersionCurrent(version))
                {
                    return;
                }

                SeamlessSlotPlan slotPlan = SelectPrimarySlot(plan);
                SeamlessContentItem[] items = slotPlan != null && slotPlan.Items != null
                    ? slotPlan.Items.ToArray()
                    : Array.Empty<SeamlessContentItem>();
                int pendingIndex = Interlocked.CompareExchange(ref pendingSyncIndex, -1, -1);
                long pendingPositionMilliseconds = Interlocked.Read(ref pendingSyncPositionMilliseconds);
                bool hasPendingSyncIndex = pendingIndex >= 0;
                bool hasPendingSyncPosition = pendingPositionMilliseconds >= 0;
                int startIndex = pendingIndex;
                if (!hasPendingSyncIndex)
                {
                    startIndex = 0;
                }
                int firstPlayableIndex = FindNextPlayableIndex(items, startIndex);
                bool hasPlayableItems = firstPlayableIndex >= 0;

                currentSlotPlan = slotPlan ?? new SeamlessSlotPlan
                {
                    Width = plan.CanvasWidth,
                    Height = plan.CanvasHeight
                };
                currentItems = items;
                currentPlaylistIndex = -1;
                currentPageDurationSeconds = Math.Max(1, plan.DurationSeconds);

                hostCanvas.Dispatcher.Invoke(() =>
                {
                    owner.SetBaseSizeFromPageSize(plan.CanvasWidth, plan.CanvasHeight);
                    ApplySurfaceLayout(currentSlotPlan);

                    if (!hasPlayableItems)
                    {
                        surface.Stop();
                        SetPresentationActive(false);
                    }
                });

                if (!IsVersionCurrent(version))
                {
                    return;
                }

                if (hasPlayableItems)
                {
                    if (ShouldWaitForLeaderSyncIndex(hasPendingSyncIndex))
                    {
                        RequestLeaderSyncIndex();
                    }
                    else if (hasPendingSyncIndex && hasPendingSyncPosition)
                    {
                        ApplyPlaylistIndexWithPosition(startIndex, TimeSpan.FromMilliseconds(pendingPositionMilliseconds));
                    }
                    else
                    {
                        ApplyPlaylistIndexOnly(startIndex, IsLeaderSyncEnabled());
                    }
                }
                else
                {
                    StopPageTimer();
                    StopContentTimer();
                    currentPageStartedAtUtc = DateTime.MinValue;
                    owner.SetInitialLoadingVisible(false);
                    return;
                }

                currentPageStartedAtUtc = DateTime.UtcNow;
                owner.SetInitialLoadingVisible(false);
                StartPageTimer(currentPageDurationSeconds);
            }
            finally
            {
                loadGate.Release();
            }
        }

        private void ApplySurfaceLayout(SeamlessSlotPlan slotPlan)
        {
            Canvas.SetLeft(surface, slotPlan.Left);
            Canvas.SetTop(surface, slotPlan.Top);
            Panel.SetZIndex(surface, slotPlan.ZIndex);
            surface.Width = Math.Max(0, slotPlan.Width);
            surface.Height = Math.Max(0, slotPlan.Height);
        }

        private SeamlessSlotPlan SelectPrimarySlot(SeamlessPagePlan plan)
        {
            if (plan?.Slots == null || plan.Slots.Count == 0)
            {
                return new SeamlessSlotPlan
                {
                    Width = plan != null ? plan.CanvasWidth : 1920,
                    Height = plan != null ? plan.CanvasHeight : 1080
                };
            }

            foreach (SeamlessSlotPlan candidate in plan.Slots)
            {
                if (candidate?.Items != null && candidate.Items.Any(x => x != null && x.IsVideo && IsContentPlayableNow(x)))
                {
                    return candidate;
                }
            }

            foreach (SeamlessSlotPlan candidate in plan.Slots)
            {
                if (candidate != null && HasPlayableItems(candidate.Items))
                {
                    return candidate;
                }
            }

            return new SeamlessSlotPlan
            {
                Width = plan.CanvasWidth,
                Height = plan.CanvasHeight
            };
        }

        private bool HasPlayableItems(IReadOnlyList<SeamlessContentItem> items)
        {
            return FindNextPlayableIndex(items, 0) >= 0;
        }

        private int CountPlayableItems(IReadOnlyList<SeamlessContentItem> items)
        {
            if (items == null || items.Count == 0)
            {
                return 0;
            }

            int count = 0;
            for (int i = 0; i < items.Count; i++)
            {
                if (IsContentPlayableNow(items[i]))
                {
                    count++;
                }
            }

            return count;
        }

        private bool IsContentPlayableNow(SeamlessContentItem item)
        {
            if (item?.Source == null)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(item.Source.CIF_StrGUID))
            {
                return true;
            }

            return owner.IsContentPeriodAllowed(item.Source.CIF_StrGUID, DateTime.Now);
        }

        private int FindNextPlayableIndex(IReadOnlyList<SeamlessContentItem> items, int startIndex)
        {
            if (items == null || items.Count == 0)
            {
                return -1;
            }

            int count = items.Count;
            int safeStartIndex = startIndex < 0 ? 0 : startIndex % count;

            for (int offset = 0; offset < count; offset++)
            {
                int candidateIndex = (safeStartIndex + offset) % count;
                if (IsContentPlayableNow(items[candidateIndex]))
                {
                    return candidateIndex;
                }
            }

            return -1;
        }

        private SharedPageInfoClass GetPageDefinitionByIndex(int index)
        {
            if (owner?.g_PageInfoManager?.g_PageInfoClassList == null
                || owner.g_PageInfoManager.g_PageInfoClassList.Count == 0)
            {
                return null;
            }

            if (index < 0 || index >= owner.g_PageInfoManager.g_PageInfoClassList.Count)
            {
                index = 0;
            }

            string pageName = owner.g_PageInfoManager.g_PageInfoClassList[index].PIC_PageName;
            return owner.g_PageInfoManager.GetPageDefinition(pageName);
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

                    if (!owner.IsContentPeriodAllowed(content.CIF_StrGUID, DateTime.Now))
                    {
                        continue;
                    }

                    if (content.CIF_PlayMinute == "00" && content.CIF_PlaySec == "00")
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

        private static bool TryParseDisplayType(string rawType, out DisplayType displayType)
        {
            displayType = DisplayType.None;
            return string.IsNullOrWhiteSpace(rawType) == false
                && Enum.TryParse(rawType, true, out displayType)
                && displayType != DisplayType.ScrollText
                && displayType != DisplayType.WelcomeBoard;
        }

        private void StartPageTimer(int durationSeconds)
        {
            hostCanvas.Dispatcher.Invoke(() =>
            {
                pageTimer.Stop();

                if (!ShouldRunPageTimer())
                {
                    return;
                }

                pageTimer.Interval = TimeSpan.FromSeconds(Math.Max(1, durationSeconds));
                pageTimer.Start();
            });
        }

        private void StopPageTimer()
        {
            hostCanvas.Dispatcher.Invoke(() =>
            {
                pageTimer.Stop();
            });
        }

        private void StartContentTimerForCurrentIndex()
        {
            hostCanvas.Dispatcher.Invoke(() =>
            {
                contentTimer.Stop();

                if (!presentationActive || !IsLeaderSyncEnabled())
                {
                    return;
                }

                if (currentItems == null || currentItems.Length <= 1 || currentPlaylistIndex < 0)
                {
                    return;
                }

                long durationMilliseconds = GetItemDurationMilliseconds(currentPlaylistIndex);
                contentTimer.Interval = TimeSpan.FromMilliseconds(Math.Max(1, durationMilliseconds));
                contentTimer.Start();
            });
        }

        private void StopContentTimer()
        {
            hostCanvas.Dispatcher.Invoke(() =>
            {
                contentTimer.Stop();
            });
        }

        private void PageTimer_Tick(object sender, EventArgs e)
        {
            StopPageTimer();
            StopContentTimer();

            if (!ShouldRunPageTimer())
            {
                return;
            }

            PlayNextPage();
        }

        private void ContentTimer_Tick(object sender, EventArgs e)
        {
            StopContentTimer();

            if (!presentationActive || !IsLeaderSyncEnabled())
            {
                return;
            }

            int nextIndex = GetNextIndex(currentPlaylistIndex);
            if (nextIndex < 0)
            {
                return;
            }

            ApplyPlaylistIndexOnly(nextIndex, true);
        }

        private SeamlessContentItem GetItemByIndex(int index)
        {
            if (currentItems == null || currentItems.Length == 0)
            {
                return null;
            }

            if (index < 0)
            {
                index = 0;
            }

            if (index >= currentItems.Length)
            {
                index %= currentItems.Length;
            }

            return currentItems[index];
        }

        private int GetNextIndex(int currentIndex)
        {
            return FindNextPlayableIndex(currentItems, currentIndex + 1);
        }

        public bool TryAdvanceLeaderToNextSyncIndex()
        {
            if (!IsLeaderSyncEnabled() || !presentationActive)
            {
                return false;
            }

            int nextIndex = GetNextIndex(currentPlaylistIndex);
            if (nextIndex < 0)
            {
                return false;
            }

            return ApplyPlaylistIndexOnly(nextIndex, true);
        }

        public bool TryGetLeaderSyncPosition(out int playlistIndex, out TimeSpan position)
        {
            playlistIndex = -1;
            position = TimeSpan.Zero;

            if (!IsLeaderSyncEnabled() || !presentationActive || currentItems == null || currentItems.Length == 0 || !surface.IsMediaLoaded)
            {
                return false;
            }

            int activeIndex = currentPlaylistIndex;
            if (activeIndex < 0)
            {
                return false;
            }

            TimeSpan currentPosition = surface.Position;
            long remainingMilliseconds = GetRemainingMilliseconds(activeIndex, currentPosition);
            if (remainingMilliseconds <= PositionSyncRemainingThreshold.TotalMilliseconds)
            {
                return false;
            }

            playlistIndex = activeIndex;
            position = NormalizeSyncPosition(activeIndex, currentPosition + PositionSyncLeadTime);
            return true;
        }

        private bool ApplyPlaylistIndexOnly(int playlistIndex, bool broadcastBeforeApply)
        {
            return ApplyPlaylistIndexCore(playlistIndex, broadcastBeforeApply, null);
        }

        private bool ApplyPlaylistIndexWithPosition(int playlistIndex, TimeSpan position)
        {
            return ApplyPlaylistIndexCore(playlistIndex, false, position);
        }

        private bool ApplyPlaylistIndexCore(int playlistIndex, bool broadcastBeforeApply, TimeSpan? syncPosition)
        {
            if (currentItems == null || currentItems.Length == 0)
            {
                return false;
            }

            int normalizedIndex = NormalizeIndex(playlistIndex);
            if (normalizedIndex < 0)
            {
                return false;
            }

            normalizedIndex = FindNextPlayableIndex(currentItems, normalizedIndex);
            if (normalizedIndex < 0)
            {
                return false;
            }

            TimeSpan? normalizedSyncPosition = syncPosition.HasValue
                ? (TimeSpan?)NormalizeSyncPosition(normalizedIndex, syncPosition.Value)
                : null;

            if (broadcastBeforeApply)
            {
                PlaylistIndexChangeRequested?.Invoke(normalizedIndex);
            }

            SeamlessContentItem item = GetItemByIndex(normalizedIndex);
            if (item == null)
            {
                return false;
            }

            bool applied = false;
            hostCanvas.Dispatcher.Invoke(() =>
            {
                bool preserveAspectRatio = owner.IsPreserveAspectRatioEnabled();
                surface.Configure(currentSlotPlan.IsMuted, preserveAspectRatio);
                surface.HideSurface();

                if (currentItems.Length <= 1)
                {
                    applied = surface.LoadPlaylist(currentItems, normalizedIndex, true);
                }
                else
                {
                    surface.Load(item, true);
                    applied = true;
                }
            });

            if (!applied)
            {
                return false;
            }

            currentPlaylistIndex = normalizedIndex;
            SetPresentationActive(true);
            Interlocked.Exchange(ref pendingSyncIndex, -1);

            if (normalizedSyncPosition.HasValue)
            {
                long syncPositionMilliseconds = Math.Max(0L, (long)normalizedSyncPosition.Value.TotalMilliseconds);
                Interlocked.Exchange(ref pendingSyncPositionMilliseconds, NoPendingPositionMilliseconds);
                Interlocked.Exchange(ref pendingLoadedSeekPositionMilliseconds, syncPositionMilliseconds);
                ApplyPendingLoadedSeekPosition();
            }
            else
            {
                Interlocked.Exchange(ref pendingSyncPositionMilliseconds, NoPendingPositionMilliseconds);
                Interlocked.Exchange(ref pendingLoadedSeekPositionMilliseconds, NoPendingPositionMilliseconds);
            }
            StartContentTimerForCurrentIndex();
            return true;
        }

        private int NormalizeIndex(int index)
        {
            if (currentItems == null || currentItems.Length == 0)
            {
                return -1;
            }

            if (index < 0)
            {
                return 0;
            }

            if (index >= currentItems.Length)
            {
                return index % currentItems.Length;
            }

            return index;
        }

        private long GetItemDurationMilliseconds(int index)
        {
            SeamlessContentItem item = GetItemByIndex(index);
            if (item == null)
            {
                return 1000L;
            }

            return Math.Max(1, item.DurationSeconds) * 1000L;
        }

        private long GetRemainingMilliseconds(int index, TimeSpan currentPosition)
        {
            long durationMilliseconds = GetItemDurationMilliseconds(index);
            long currentPositionMilliseconds = Math.Max(0L, (long)currentPosition.TotalMilliseconds);
            long remainingMilliseconds = durationMilliseconds - currentPositionMilliseconds;
            return Math.Max(0L, remainingMilliseconds);
        }

        private TimeSpan NormalizeSyncPosition(int index, TimeSpan position)
        {
            long durationMilliseconds = GetItemDurationMilliseconds(index);
            long maxPositionMilliseconds = Math.Max(0L, durationMilliseconds - 100L);
            long requestedMilliseconds = Math.Max(0L, (long)position.TotalMilliseconds);
            return TimeSpan.FromMilliseconds(Math.Min(requestedMilliseconds, maxPositionMilliseconds));
        }

        private bool IsLeaderSyncEnabled()
        {
            return owner?.IsSyncLeader ?? false;
        }

        private bool ShouldRunPageTimer()
        {
            bool isSyncPlaybackActive = owner?.IsSyncPlaybackActive ?? false;
            return !isSyncPlaybackActive || IsLeaderSyncEnabled();
        }

        private bool ShouldWaitForLeaderSyncIndex(bool hasPendingSyncIndex)
        {
            if (hasPendingSyncIndex)
            {
                return false;
            }

            if (currentItems == null || currentItems.Length == 0)
            {
                return false;
            }

            bool isSyncPlaybackActive = owner?.IsSyncPlaybackActive ?? false;
            return isSyncPlaybackActive && !IsLeaderSyncEnabled();
        }

        private void RequestLeaderSyncIndex()
        {
            StopContentTimer();
            currentPlaylistIndex = -1;
            Interlocked.Exchange(ref pendingSyncIndex, -1);
            Interlocked.Exchange(ref pendingSyncPositionMilliseconds, NoPendingPositionMilliseconds);
            Interlocked.Exchange(ref pendingLoadedSeekPositionMilliseconds, NoPendingPositionMilliseconds);

            hostCanvas.Dispatcher.Invoke(() =>
            {
                surface.Stop();
            });

            SyncIndexRequestNeeded?.Invoke();
        }

        private void SetPresentationActive(bool isActive, bool notify = true)
        {
            if (presentationActive == isActive)
            {
                return;
            }

            presentationActive = isActive;
            if (notify)
            {
                PresentationVisibilityChanged?.Invoke(isActive);
            }
        }

        private void Surface_MediaLoaded()
        {
            ApplyPendingLoadedSeekPosition();
            if (!presentationActive)
            {
                return;
            }

            hostCanvas.Dispatcher.BeginInvoke(DispatcherPriority.Send, new Action(() =>
            {
                if (presentationActive)
                {
                    surface.ShowSurface();
                }
            }));
        }

        private void ApplyPendingLoadedSeekPosition()
        {
            long pendingMilliseconds = Interlocked.Read(ref pendingLoadedSeekPositionMilliseconds);
            if (pendingMilliseconds < 0)
            {
                return;
            }

            hostCanvas.Dispatcher.BeginInvoke(DispatcherPriority.Send, new Action(() =>
            {
                long latestPendingMilliseconds = Interlocked.Read(ref pendingLoadedSeekPositionMilliseconds);
                if (latestPendingMilliseconds < 0)
                {
                    return;
                }

                TimeSpan latestPendingPosition = TimeSpan.FromMilliseconds(latestPendingMilliseconds);
                if (!surface.TrySeekToPosition(latestPendingPosition))
                {
                    return;
                }

                Interlocked.Exchange(ref pendingLoadedSeekPositionMilliseconds, NoPendingPositionMilliseconds);
            }));
        }

        private bool IsVersionCurrent(int version)
        {
            return version == Volatile.Read(ref playbackVersion);
        }
    }
}
