using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using AndoW.Shared;
using Newtonsoft.Json;
using TurtleTools;
using SharedDaySchedule = AndoW.Shared.DaySchedule;
using SharedWeeklyPlayScheduleInfo = AndoW.Shared.WeeklyPlayScheduleInfo;


namespace AndoW_Manager
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window
	{
        //TCPManagerClass g_TCPManagerClass = null;
        //public FavoriteDataManager g_FavoriteDataManager = new FavoriteDataManager();


        List<FlatButton1> g_LeftMenuBTNList = new List<FlatButton1>();

        public int g_static_Width = 1920;
        public int g_static_Height = 1080; 
        public bool isPortraitEditor = false;

        public double g_wLandScale, g_hLandScale, g_wPortScale, g_hPortScale;

        public List<string> onlineList = new List<string>();

        public static MainWindow Instance { get; set; }

        public Page1 g_Page1 = null;
        public Page2 g_Page2 = null;
        public Page3 g_Page3 = null;
        public Page5 g_Page5 = null;

        private bool _hasSavedPagesSnapshot;
        private Task _savedPageLoadTask;
        private CancellationTokenSource _savedPageLoadCts;
        private readonly SemaphoreSlim _savedPageReadGate = new SemaphoreSlim(1, 1);

        public Dictionary<string, List<string>> g_fontsDic;

        public MainWindow()
        {
            this.InitializeComponent();
            SetTopbarVersionText();

            Instance = this;

            ProcessTools.KillVNCViewer();

            //Task.Factory.StartNew(() =>
            //{
            //    g_fontsDic = SpecificTools.GetFontsDic();
            //});
            ThreadPool.QueueUserWorkItem(new WaitCallback(delegate (object state)
            {
                FNDTools.GetFontsTargetFolderPath();
                g_fontsDic = SpecificTools.GetFontsDic();
            }));

            InitLeftMenuBTNs();
            InitEventHandler();

            this.PageTransition1.TransitionType = WpfPageTransitions.PageTransitionType.Fade;
        }

        private void SetTopbarVersionText()
        {
            Version assemblyVersion = typeof(MainWindow).Assembly.GetName().Version;
            if (assemblyVersion == null)
            {
                Favorite_title_Copy.Text = "New HyOn";
                return;
            }

            int fieldCount = assemblyVersion.Revision > 0 ? 4 : 3;
            string versionText = assemblyVersion.ToString(fieldCount);
            Favorite_title_Copy.Text = $"New HyOn ( v{versionText} )";
        }

        public void InitPages()
        {
            Stopwatch initPagesTimer = Stopwatch.StartNew();

            g_Page1 = new Page1();
            LogStartupStage(initPagesTimer, "init-page1-created");
            g_Page2 = new Page2();
            LogStartupStage(initPagesTimer, "init-page2-created");
            g_Page3 = new Page3();
            LogStartupStage(initPagesTimer, "init-page3-created");
            g_Page5 = new Page5();
            LogStartupStage(initPagesTimer, "init-page5-created");

            _ = EnsureSavedPagesLoadedAsync();
            LogStartupStage(initPagesTimer, "init-saved-pages-load-started");
            g_Page2.RefreshPageNameList();
            LogStartupStage(initPagesTimer, "init-page-names-refreshed");
        }

        public void RefreshSavedPageList()
        {
            Dispatcher.VerifyAccess();
            CancelSavedPageLoad();
            _ = StartSavedPageLoad();
        }

        public Task EnsureSavedPagesLoadedAsync()
        {
            Dispatcher.VerifyAccess();
            if (_hasSavedPagesSnapshot)
            {
                return Task.CompletedTask;
            }

            if (_savedPageLoadTask != null && _savedPageLoadTask.IsCompleted == false)
            {
                return _savedPageLoadTask;
            }

            return StartSavedPageLoad();
        }

        private Task StartSavedPageLoad()
        {
            Dispatcher.VerifyAccess();
            CancellationTokenSource loadCts = new CancellationTokenSource();
            _savedPageLoadCts = loadCts;
            Task loadTask = LoadSavedPageSnapshotAsync(loadCts);
            _savedPageLoadTask = loadTask;
            return loadTask;
        }

        private async Task LoadSavedPageSnapshotAsync(CancellationTokenSource loadCts)
        {
            await Task.Yield();

            bool ownsReadGate = false;
            try
            {
                CancellationToken token = loadCts.Token;
                await _savedPageReadGate.WaitAsync(token);
                ownsReadGate = true;

                List<PageInfoClass> savedPages = await Task.Run(
                    () => DataShop.Instance.g_PageInfoManager.GetAllSavedPagesOrThrow(),
                    token);
                token.ThrowIfCancellationRequested();

                if (ReferenceEquals(_savedPageLoadCts, loadCts) == false)
                {
                    return;
                }

                PublishSavedPageSnapshot(savedPages);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                Logger.WriteErrorLog(
                    $"저장 화면구성 비동기 로드 실패: {ex.Message}",
                    Logger.GetLogFileName());
            }
            finally
            {
                if (ownsReadGate)
                {
                    _savedPageReadGate.Release();
                }

                if (ReferenceEquals(_savedPageLoadCts, loadCts))
                {
                    _savedPageLoadCts = null;
                    _savedPageLoadTask = null;
                }

                loadCts.Dispose();
            }
        }

        private void PublishSavedPageSnapshot(List<PageInfoClass> savedPages)
        {
            Dispatcher.VerifyAccess();

            Page1.SavedPageSnapshot previousPage1Snapshot = g_Page1.CaptureSavedPageSnapshot();
            Page2.SavedPageSnapshot previousPage2Snapshot = g_Page2.CaptureSavedPageSnapshot();
            bool previousHasSavedPagesSnapshot = _hasSavedPagesSnapshot;

            Page1.SavedPageSnapshot nextPage1Snapshot = g_Page1.PrepareSavedPageSnapshot(savedPages);
            Page2.SavedPageSnapshot nextPage2Snapshot = g_Page2.PrepareSavedPageSnapshot(savedPages);

            try
            {
                g_Page1.ApplySavedPageSnapshot(nextPage1Snapshot);
                g_Page2.ApplySavedPageSnapshot(nextPage2Snapshot);
                _hasSavedPagesSnapshot = true;
            }
            catch
            {
                RestoreSavedPageSnapshots(
                    previousPage1Snapshot,
                    previousPage2Snapshot,
                    previousHasSavedPagesSnapshot);
                throw;
            }
        }

        private void RestoreSavedPageSnapshots(
            Page1.SavedPageSnapshot previousPage1Snapshot,
            Page2.SavedPageSnapshot previousPage2Snapshot,
            bool previousHasSavedPagesSnapshot)
        {
            try
            {
                g_Page1.ApplySavedPageSnapshot(previousPage1Snapshot);
            }
            catch (Exception ex)
            {
                Logger.WriteErrorLog(
                    $"화면구성 Page1 snapshot 복원 실패: {ex.Message}",
                    Logger.GetLogFileName());
            }

            try
            {
                g_Page2.ApplySavedPageSnapshot(previousPage2Snapshot);
            }
            catch (Exception ex)
            {
                Logger.WriteErrorLog(
                    $"화면구성 Page2 snapshot 복원 실패: {ex.Message}",
                    Logger.GetLogFileName());
            }

            _hasSavedPagesSnapshot = previousHasSavedPagesSnapshot;
        }

        private void CancelSavedPageLoad()
        {
            Dispatcher.VerifyAccess();
            CancellationTokenSource loadCts = _savedPageLoadCts;
            try
            {
                if (loadCts != null && loadCts.IsCancellationRequested == false)
                {
                    loadCts.Cancel();
                }
            }
            catch (ObjectDisposedException)
            {
            }
        }


        public void InitLeftMenuBTNs()
        {
            g_LeftMenuBTNList.Clear();
            g_LeftMenuBTNList.Add(LeftMenuBTN1);
            g_LeftMenuBTNList.Add(LeftMenuBTN2);
            g_LeftMenuBTNList.Add(LeftMenuBTN4);
            g_LeftMenuBTNList.Add(LeftMenuBTN3);

            LeftMenuBTN1.DisBTNName("화면구성", "편집");
            LeftMenuBTN2.DisBTNName("플레이리스트", "편집");
            LeftMenuBTN3.DisBTNName("플레이어", "제어");
            LeftMenuBTN4.DisBTNName("스케줄", "스케줄");

            LeftMenuBTN1.ShowIcon01();
            LeftMenuBTN3.ShowIcon03();
            LeftMenuBTN4.ShowIcon04();

        }

        public void AddNewPageList(string paramPageListName)
        {
            PageListInfoClass tmpCls = new PageListInfoClass();
            tmpCls.PLI_PageListName = paramPageListName;
            DataShop.Instance.g_PageListInfoManager.AddPageListInfoClass(tmpCls);
            DataShop.Instance.g_PageInfoManager.LoadPagesForList(paramPageListName);
            DataShop.Instance.g_PageInfoManager.SavePageList(paramPageListName);
            g_Page2.RefreshPageNameList();

            g_Page3.UpdatePlayListForPlayer();
        }
        
        public void InitEventHandler()
        {
            maxBTN.Click += maxBTN_Click;
            ExitBTN.Click += new RoutedEventHandler(ExitBTN_Click);
            minBTN.Click += new RoutedEventHandler(minBTN_Click);

            LeftMenuBTN1.PreviewMouseLeftButtonDown += LeftMenuBTN1_PreviewMouseLeftButtonDown;
            LeftMenuBTN2.PreviewMouseLeftButtonDown += LeftMenuBTN1_PreviewMouseLeftButtonDown;
            LeftMenuBTN3.PreviewMouseLeftButtonDown += LeftMenuBTN1_PreviewMouseLeftButtonDown;
            LeftMenuBTN4.PreviewMouseLeftButtonDown += LeftMenuBTN1_PreviewMouseLeftButtonDown;

            this.Loaded += MainWindow_Loaded;

            this.Closing += MainWindow_Closing;
            this.StateChanged += MainWindow_StateChanged;

            BTN0DO_Copy1.Click += BTN0DO_Copy1_Click;

        }

        public void SetDimOverlay(bool show)
        {
            if (DimOverlay == null)
                return;

            DimOverlay.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        }

        void BTN0DO_Copy1_Click(object sender, RoutedEventArgs e)
        {
            //if (g_WCFWindow1 != null)
            //{
            //    g_WCFWindow1.Show();
            //   // g_WCFWindow1.Topmost = true;
            //}
        }

        void UpdateMaximizeButtonIcon()
        {
            if (MaximizedIcon == null || WindowIcon == null)
            {
                return;
            }

            if (this.WindowState == WindowState.Maximized)
            {
                MaximizedIcon.Visibility = Visibility.Collapsed;
                WindowIcon.Visibility = Visibility.Visible;
            }
            else
            {
                MaximizedIcon.Visibility = Visibility.Visible;
                WindowIcon.Visibility = Visibility.Collapsed;
            }
        }

        void maxBTN_Click(object sender, RoutedEventArgs e)
        {
            if (this.WindowState == System.Windows.WindowState.Maximized)
            {
                this.WindowState = System.Windows.WindowState.Normal;
            }
            else
            {
                this.WindowState = System.Windows.WindowState.Maximized;
            }

            UpdateMaximizeButtonIcon();

            if (this.g_Page1 != null)
            {
               this.g_Page1.AdjustCanvasSize();
            }
        }

        void MainWindow_StateChanged(object sender, EventArgs e)
        {
            g_Page1.ChangePortraitOrLandscape(isPortraitEditor);
            UpdateMaximizeButtonIcon();
        }

        void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            SignalRClientTools.StopSignalRClient();

            Process.GetCurrentProcess().Kill();
        }

        async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            Stopwatch startupTimer = Stopwatch.StartNew();
            LocalSettingsStore.EnsureSeeded();
            RethinkDbConfigurator.EnsureConfigured();

            DbLoadingWindow loadingWindow = new DbLoadingWindow();
            loadingWindow.SetStatus("데이터 연결 확인", "데이터 서버에 연결 중입니다.");
            loadingWindow.Show();
            loadingWindow.Activate();
            await Dispatcher.Yield(DispatcherPriority.Background);

            await WaitForDatabaseReadyAsync(loadingWindow);
            LogStartupStage(startupTimer, "database-ready");

            SignalRClientTools.StartSignalRClient();

            loadingWindow.SetStatus("데이터 준비", "초기 화면을 구성하고 있습니다.");
            await Dispatcher.Yield(DispatcherPriority.Render);
            InitPages();
            LogStartupStage(startupTimer, "pages-initialized");

            if (g_Page1 != null && g_Page1.g_CurrentPageInfo != null)
            {
                int rows = Math.Max(1, g_Page1.g_CurrentPageInfo.PIC_Rows);
                int columns = Math.Max(1, g_Page1.g_CurrentPageInfo.PIC_Columns);

                double landW = Math.Max(1, columns * 1920.0);
                double landH = Math.Max(1, rows * 1080.0);
                double portW = Math.Max(1, columns * 1080.0);
                double portH = Math.Max(1, rows * 1920.0);

                g_wLandScale = landW / portW;
                g_hLandScale = landH / portH;
                g_wPortScale = portW / landW;
                g_hPortScale = portH / landH;
            }
            else
            {
                g_wLandScale = g_hPortScale = 1920.0 / 1080.0;
                g_hLandScale = g_wPortScale = 1080.0 / 1920.0;
            }

            GotoPageByName("Page3");
            loadingWindow.Close();
            await WaitForNextRenderingAsync();
            LogStartupStage(startupTimer, "first-page-rendered");

            await g_Page3.RefreshPlayerInfoListIncrementallyAsync();
            LogStartupStage(startupTimer, "player-cards-populated");
        }

        private static Task WaitForNextRenderingAsync()
        {
            var completion = new TaskCompletionSource<bool>();
            EventHandler renderingHandler = null;
            renderingHandler = (sender, args) =>
            {
                CompositionTarget.Rendering -= renderingHandler;
                completion.TrySetResult(true);
            };
            CompositionTarget.Rendering += renderingHandler;
            return completion.Task;
        }

        private static void LogStartupStage(Stopwatch startupTimer, string stage)
        {
            Logger.WriteLog(
                $"Manager startup {stage}: {startupTimer.ElapsedMilliseconds}ms",
                Logger.GetLogFileName());
        }

        public bool EnqueueCommandForPlayer(PlayerInfoClass player, string command, string payloadBase64 = "", bool pushSignalR = true)
        {
            if (player == null || string.IsNullOrWhiteSpace(player.PIF_GUID) || string.IsNullOrWhiteSpace(command))
            {
                return false;
            }
            string playerId = player.PIF_GUID.Trim();
            string normalizedCommand = command.Trim();
            if (IsTizenUpdateCommandBlocked(player, normalizedCommand, string.IsNullOrWhiteSpace(payloadBase64), true))
            {
                return false;
            }

            if (RequiresAuthenticatedPlayer(normalizedCommand)
                && !DataShop.Instance.g_PlayerInfoManager.HasValidAuthKey(player))
            {
                NotifyUnauthenticatedCommandBlocked(player, normalizedCommand);
                return false;
            }

            string resolvedPayload = payloadBase64 ?? string.Empty;
            if (string.IsNullOrWhiteSpace(resolvedPayload)
                && string.Equals(normalizedCommand, RP_ORDER.updateschedule.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                resolvedPayload = BuildSchedulePayloadBase64(player);
            }
            if (string.IsNullOrWhiteSpace(resolvedPayload)
                && string.Equals(normalizedCommand, RP_ORDER.updateweekly.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                resolvedPayload = BuildWeeklyPayloadBase64(player);
            }

            var queueManager = DataShop.Instance.g_CommandQueueManager;
            var entry = queueManager.EnqueueCommand(playerId, normalizedCommand, resolvedPayload, "manager");
            if (entry == null)
            {
                return false;
            }

            if (pushSignalR && SignalRClientTools.IsConnected())
            {
                var envelope = new SignalRCommandEnvelope
                {
                    CommandId = entry.Id,
                    Command = entry.Command,
                    PlayerId = playerId,
                    PayloadJson = entry.PayloadBase64,
                    CreatedAt = entry.CreatedAt
                };

                SignalRClientTools.TrySendCommandToClient(playerId, envelope);
            }

            return true;
        }

        private static bool RequiresAuthenticatedPlayer(string command)
        {
            return string.Equals(command, RP_ORDER.updatelist.ToString(), StringComparison.OrdinalIgnoreCase)
                || string.Equals(command, RP_ORDER.updateschedule.ToString(), StringComparison.OrdinalIgnoreCase)
                || string.Equals(command, RP_ORDER.updateweekly.ToString(), StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsTizenUpdateCommandBlocked(PlayerInfoClass player, string command, bool validateCurrentPlaylist, bool showPopup)
        {
            TizenPlaylistBlockResult blockResult = null;
            bool blocked = false;

            if (string.Equals(command, RP_ORDER.updatelist.ToString(), StringComparison.OrdinalIgnoreCase) && validateCurrentPlaylist)
            {
                blocked = TizenPlaylistUpdatePolicy.TryValidatePlayerPlaylist(player, player?.PIF_DefaultPlayList, out blockResult) == false;
            }
            else if (string.Equals(command, RP_ORDER.updateschedule.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                blocked = TizenPlaylistUpdatePolicy.TryValidatePlayerSchedule(player, out blockResult) == false;
            }

            if (blocked && showPopup)
            {
                MessageTools.ShowMessageBox(TizenPlaylistUpdatePolicy.BuildBlockedMessage(new[] { blockResult }), "확인");
            }

            return blocked;
        }

        public bool SendUrgentUpdateList(PlayerInfoClass player)
        {
            if (player == null || string.IsNullOrWhiteSpace(player.PIF_GUID))
            {
                return false;
            }

            if (!DataShop.Instance.g_PlayerInfoManager.HasValidAuthKey(player))
            {
                NotifyUnauthenticatedCommandBlocked(player, RP_ORDER.updatelist.ToString());
                return false;
            }

            TizenPlaylistBlockResult blockResult;
            if (TizenPlaylistUpdatePolicy.TryValidatePlayerPlaylist(player, player.PIF_DefaultPlayList, out blockResult) == false)
            {
                MessageTools.ShowMessageBox(TizenPlaylistUpdatePolicy.BuildBlockedMessage(new[] { blockResult }), "확인");
                return false;
            }

            string payloadBase64 = DataShop.Instance.g_UpdatePayloadBuilder.BuildPayloadBase64(player);
            return EnqueueCommandForPlayer(player, RP_ORDER.updatelist.ToString(), payloadBase64, true);
        }

        public void NotifyContentPeriodChanged(IEnumerable<string> contentGuids)
        {
            var distinctGuids = (contentGuids ?? Enumerable.Empty<string>())
                .Where(x => string.IsNullOrWhiteSpace(x) == false)
                .Select(x => x.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (distinctGuids.Count == 0 || !SignalRClientTools.IsConnected())
            {
                return;
            }

            var onlinePlayers = new HashSet<string>(onlineList ?? new List<string>(), StringComparer.OrdinalIgnoreCase);
            if (onlinePlayers.Count == 0)
            {
                return;
            }

            var playerManager = DataShop.Instance.g_PlayerInfoManager;
            playerManager.ReloadFromDatabase();

            string payloadJson = UpdatePayloadCodec.Encode(new UpdatePayload
            {
                ContentPeriodUpdateGuids = distinctGuids
            });
            if (string.IsNullOrWhiteSpace(payloadJson))
            {
                return;
            }

            int blockedCount = 0;
            foreach (var player in playerManager.g_PlayerInfoClassList ?? new List<PlayerInfoClass>())
            {
                if (player == null
                    || string.IsNullOrWhiteSpace(player.PIF_GUID)
                    || string.IsNullOrWhiteSpace(player.PIF_PlayerName)
                    || !onlinePlayers.Contains(player.PIF_PlayerName))
                {
                    continue;
                }

                if (!playerManager.HasValidAuthKey(player))
                {
                    blockedCount++;
                    continue;
                }

                EnqueueCommandForPlayer(player, "updatecontentperiod", payloadJson, true);
            }

            if (blockedCount > 0)
            {
                EnqueueSnackMsg($"미인증 플레이어 {blockedCount}대의 콘텐츠 기간 업데이트 전송을 차단했습니다.");
            }
        }

        private void NotifyUnauthenticatedCommandBlocked(PlayerInfoClass player, string command)
        {
            string playerName = string.IsNullOrWhiteSpace(player?.PIF_PlayerName)
                ? "이름 없음"
                : player.PIF_PlayerName;
            EnqueueSnackMsg($"미인증 플레이어 명령을 차단했습니다. ({playerName}, {GetCommandDisplayName(command)})");
            g_Page3?.RefreshPlayerAuthenticationOverlays();
        }

        private static string GetCommandDisplayName(string command)
        {
            if (string.Equals(command, RP_ORDER.updatelist.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                return "온라인 플레이리스트";
            }

            if (string.Equals(command, RP_ORDER.updateschedule.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                return "온라인 스케줄";
            }

            if (string.Equals(command, RP_ORDER.updateweekly.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                return "주간 스케줄";
            }

            return string.IsNullOrWhiteSpace(command) ? "알 수 없는 명령" : command;
        }

        private static string BuildSchedulePayloadBase64(PlayerInfoClass player)
        {
            if (player == null)
            {
                return string.Empty;
            }

            string playerName = player.PIF_PlayerName ?? string.Empty;
            if (string.IsNullOrWhiteSpace(playerName))
            {
                return string.Empty;
            }

            var scheduleManager = new SpecialScheduleInfoManager();
            scheduleManager.LoadSchedulesForPlayer(playerName);

            var schedules = new List<SpecialSchedulePayload>();
            if (scheduleManager.g_SpecialScheduleInfoClassList != null)
            {
                foreach (var schedule in scheduleManager.g_SpecialScheduleInfoClassList)
                {
                    var mapped = MapSpecialSchedule(schedule);
                    if (mapped != null)
                    {
                        schedules.Add(mapped);
                    }
                }
            }

            var playlistPayloads = new List<SchedulePlaylistPayload>();
            try
            {
                var targetPlaylists = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var schedule in scheduleManager.g_SpecialScheduleInfoClassList ?? new List<SpecialScheduleInfoClass>())
                {
                    if (!string.IsNullOrWhiteSpace(schedule?.PageListName))
                    {
                        targetPlaylists.Add(schedule.PageListName);
                    }
                }

                var pageListManager = DataShop.Instance.g_PageListInfoManager;
                var pageManager = DataShop.Instance.g_PageInfoManager;
                var builder = DataShop.Instance.g_UpdatePayloadBuilder;

                pageListManager.LoadDataFromDatabase();

                foreach (var playlistName in targetPlaylists)
                {
                    var pageList = pageListManager.GetPageListByName(playlistName);
                    if (pageList == null)
                    {
                        continue;
                    }

                    pageManager.LoadPagesForList(pageList.PLI_PageListName);
                    var pages = pageManager.g_PageInfoClassList?.ToList() ?? new List<PageInfoClass>();
                    builder.PreparePagesForPayload(pages);
                    playlistPayloads.Add(new SchedulePlaylistPayload
                    {
                        PlaylistName = pageList.PLI_PageListName,
                        PageList = pageList,
                        Pages = pages
                    });
                }
            }
            catch (Exception ex)
            {
                Logger.WriteErrorLog($"BuildSchedulePayloadBase64 playlist mapping failed: {ex}", Logger.GetLogFileName());
            }

            var schedulePayload = new ScheduleUpdatePayload
            {
                PlayerId = player.PIF_GUID ?? string.Empty,
                PlayerName = playerName,
                GeneratedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                SpecialSchedules = schedules,
                Playlists = playlistPayloads
            };

            var payload = new UpdatePayload
            {
                Schedule = schedulePayload
            };

            return UpdatePayloadCodec.Encode(payload);
        }

        private static string BuildWeeklyPayloadBase64(PlayerInfoClass player)
        {
            if (player == null)
            {
                return string.Empty;
            }

            string playerName = player.PIF_PlayerName ?? string.Empty;
            var weeklySchedule = LoadWeeklyScheduleSnapshot(player.PIF_GUID, playerName);
            if (weeklySchedule == null)
            {
                return string.Empty;
            }

            var payload = new UpdatePayload
            {
                Schedule = new ScheduleUpdatePayload
                {
                    PlayerId = player.PIF_GUID ?? string.Empty,
                    PlayerName = playerName,
                    GeneratedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    WeeklySchedule = weeklySchedule
                }
            };

            return UpdatePayloadCodec.Encode(payload);
        }

        private static SharedWeeklyPlayScheduleInfo LoadWeeklyScheduleSnapshot(string playerId, string playerName)
        {
            var weeklyManager = new WeeklyInfoManagerClass();
            weeklyManager.InitPlayerInfoListFromDataTable(playerId, playerName);
            return CloneWeeklySchedule(weeklyManager.CurrentSchedule);
        }

        private static SharedWeeklyPlayScheduleInfo CloneWeeklySchedule(AndoW_Manager.WeeklyPlayScheduleInfo source)
        {
            if (source == null)
            {
                return null;
            }

            return new SharedWeeklyPlayScheduleInfo
            {
                Id = source.Id,
                PlayerID = source.PlayerID,
                PlayerName = source.PlayerName,
                MonSch = CloneDaySchedule(source.MonSch),
                TueSch = CloneDaySchedule(source.TueSch),
                WedSch = CloneDaySchedule(source.WedSch),
                ThuSch = CloneDaySchedule(source.ThuSch),
                FriSch = CloneDaySchedule(source.FriSch),
                SatSch = CloneDaySchedule(source.SatSch),
                SunSch = CloneDaySchedule(source.SunSch)
            };
        }

        private static SharedDaySchedule CloneDaySchedule(AndoW_Manager.DaySchedule source)
        {
            if (source == null)
            {
                return SharedDaySchedule.CreateDefault();
            }

            return new SharedDaySchedule
            {
                StartHour = source.StartHour,
                StartMinute = source.StartMinute,
                EndHour = source.EndHour,
                EndMinute = source.EndMinute,
                IsOnAir = source.IsOnAir
            };
        }

        private static SpecialSchedulePayload MapSpecialSchedule(SpecialScheduleInfoClass schedule)
        {
            if (schedule == null)
            {
                return null;
            }

            return new SpecialSchedulePayload
            {
                Id = schedule.GUID ?? string.Empty,
                PageListName = schedule.PageListName ?? string.Empty,
                DayOfWeek1 = schedule.DayOfWeek1,
                DayOfWeek2 = schedule.DayOfWeek2,
                DayOfWeek3 = schedule.DayOfWeek3,
                DayOfWeek4 = schedule.DayOfWeek4,
                DayOfWeek5 = schedule.DayOfWeek5,
                DayOfWeek6 = schedule.DayOfWeek6,
                DayOfWeek7 = schedule.DayOfWeek7,
                IsPeriodEnable = schedule.IsPeriodEnable,
                DisplayStartH = schedule.DisplayStartH,
                DisplayStartM = schedule.DisplayStartM,
                DisplayEndH = schedule.DisplayEndH,
                DisplayEndM = schedule.DisplayEndM,
                PeriodStartYear = schedule.PeriodStartYear,
                PeriodStartMonth = schedule.PeriodStartMonth,
                PeriodStartDay = schedule.PeriodStartDay,
                PeriodEndYear = schedule.PeriodEndYear,
                PeriodEndMonth = schedule.PeriodEndMonth,
                PeriodEndDay = schedule.PeriodEndDay
            };
        }

        void LeftMenuBTN1_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            FlatButton1 tmpBtn = (FlatButton1)sender;

            switch (tmpBtn.Name)
            {
                case "LeftMenuBTN1":
                    GotoPageByName("Page1");
                    break;
                case "LeftMenuBTN2":
                    GotoPageByName("Page2");
                    break;
                case "LeftMenuBTN4":
                    GotoPageByName("Page5");
                    break;
                case "LeftMenuBTN3":
                    GotoPageByName("Page3");
                    break;

                default:
                    break;
            }
        }

       
        void minBTN_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

		void sizeBTN_Checked(object sender, RoutedEventArgs e)
		{
			this.WindowState = WindowState.Maximized;
		}

		void sizeBTN_Unchecked(object sender, RoutedEventArgs e)
		{
			this.WindowState = WindowState.Normal;
		}
		
        void ExitBTN_Click(object sender, RoutedEventArgs e)
        {          
           Application.Current.Shutdown();           
            
        }

        private void BtnWin_drag_PreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            this.DragMove();
        }

        public void GotoPageByName(string paramName)
        {

            switch (paramName)
            {
                case "Page1":

                    if (LeftMenuBTN1.Selected)
                        return;

                    LeftMenuBTN1.ShowAndHideSelectedBorder(true);
                    LeftMenuBTN2.ShowAndHideSelectedBorder(false);
                    LeftMenuBTN3.ShowAndHideSelectedBorder(false);
                    LeftMenuBTN4.ShowAndHideSelectedBorder(false);

                    this.PageTransition1.ShowPage(g_Page1);

                    break;

                case "Page2":

                    if (LeftMenuBTN2.Selected)
                        return;

                    LeftMenuBTN1.ShowAndHideSelectedBorder(false);
                    LeftMenuBTN2.ShowAndHideSelectedBorder(true);
                    LeftMenuBTN3.ShowAndHideSelectedBorder(false);
                    LeftMenuBTN4.ShowAndHideSelectedBorder(false);

                    this.PageTransition1.ShowPage(g_Page2);

                    break;

                case "Page3":

                    if (LeftMenuBTN3.Selected)
                        return;

                    LeftMenuBTN1.ShowAndHideSelectedBorder(false);
                    LeftMenuBTN2.ShowAndHideSelectedBorder(false);
                    LeftMenuBTN3.ShowAndHideSelectedBorder(true);
                    LeftMenuBTN4.ShowAndHideSelectedBorder(false);

                    this.PageTransition1.ShowPage(g_Page3);
                    break;
                case "Page5":

                    if (LeftMenuBTN4.Selected)
                        return;

                    LeftMenuBTN1.ShowAndHideSelectedBorder(false);
                    LeftMenuBTN2.ShowAndHideSelectedBorder(false);
                    LeftMenuBTN3.ShowAndHideSelectedBorder(false);
                    LeftMenuBTN4.ShowAndHideSelectedBorder(true);

                    this.PageTransition1.ShowPage(g_Page5);
                    break;

                default:
                    break;
            }

        }

        private void Window_Closed(object sender, EventArgs e)
        {
            Environment.Exit(0);
        }


        //public void C2S_ReportingCurrentPage(string playerName, string pageName)
        //{
        //    this.g_Page3.C2S_ReportingCurrentPage(playerName, pageName);
        //}

        internal bool CheckFTPServerAlive()
        {
            if (FtpTransferTools.TryTestConnection(out string error))
            {
                return true;
            }

            if (!string.IsNullOrWhiteSpace(error))
            {
                Logger.WriteErrorLog(error, Logger.GetLogFileName());
            }

            return false;
        }

        private async Task WaitForDatabaseReadyAsync(DbLoadingWindow loadingWindow)
        {
            while (true)
            {
                bool connected = await Task.Run(() => RethinkDbContext.TryConnect(false));
                if (connected)
                {
                    return;
                }

                Logger.WriteErrorLog("RethinkDB 연결 실패. 15초 후 재시도합니다.", Logger.GetLogFileName());
                loadingWindow?.SetStatus(
                    "데이터 연결 실패",
                    "데이터 서버 연결이 필요합니다.\r\n설정 확인 후 15초마다 재시도합니다.");
                await Task.Delay(TimeSpan.FromSeconds(15));
            }
        }

        string prevEnqueMsg = string.Empty;
        DateTime prevMsgDT = DateTime.MinValue;
        public void EnqueueSnackMsg(string msg)
        {
            DateTime _now_dt = DateTime.Now;

            if (prevEnqueMsg.Equals(msg, StringComparison.OrdinalIgnoreCase)
                && (_now_dt - prevMsgDT).TotalSeconds < 2)
                return;

            SnackbarCtrl.MessageQueue.Enqueue(msg);
            prevEnqueMsg = msg;
            prevMsgDT = _now_dt;
        }
    }

    public class IPItem
    {
        public string IP { get; set; }
        public int PORT { get; set; }
        public string Description { get; set; }

        public IPItem()
        {
            IP = "127.0.0.1";
            PORT = 8002;
            Description = string.Empty;
        }

        public IPItem(string ip, int port, string desc = "")
        {
            this.IP = ip;
            this.PORT = port;
            this.Description = desc;
        }
    }
}
