using NewHyOnPlayer.Properties;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using TurtleTools;
using NewHyOnPlayer.Services;
using NewHyOnPlayer.DataManager;
using NewHyOnPlayer.PlaybackModes;
using AndoW.Shared;
using forms = System.Windows.Forms;

namespace NewHyOnPlayer
{
    /// <summary>
    /// MainWindow.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class MainWindow : Window
    {
        public string g_CurrentPageName = string.Empty;

        //public Dictionary<string, PeriodData> sPeriodDics = new Dictionary<string, PeriodData>();

        private class StateObjClass
        {
        }

        #region DataManagers
        public PageInfoManager g_PageInfoManager = new PageInfoManager();
        public ElementInfoManager g_ElementInfoManager = new ElementInfoManager();
        public PlayerInfoManager g_PlayerInfoManager = new PlayerInfoManager();
        public LocalSettingsManager g_LocalSettingsManager = new LocalSettingsManager();
        public TTPlayerInfoManager g_TTPlayerInfoManager = new TTPlayerInfoManager();
        #endregion

        public int g_PageIndex = 0;
        public bool Is_PlaySpecialSch = false;
        public long g_TimeInterval = 1;

        public string g_PlayerName = string.Empty;
        public static MainWindow Instance { get; set; } = null;

        //const int WMGraphNotify = 0x0400 + 13;

        string g_CurrentBackImage = string.Empty;

        public double g_FitscaleValueX = 1;
        public double g_FitscaleValueY = 1;

        double g_FixedBaseWidth = 1920;
        double g_FixedBaseHeight = 1080;

        Cursor defaultCursor = Cursors.Arrow;

        public double screenW = 0;
        public double screenH = 0;

        public double[] pixelDensity = new double[2];

        public double longSide = 1920;          // default: 1920 pixel
        public long imageSizeLimit = 3000000;   // default: 3MB
        public Int64 imageQuality = 100L;       // default: 100L
        private RemoteCommandService commandService;
        private HeartbeatReporter heartbeatReporter;
        private RethinkSyncService rethinkSyncService;
        private SignalRClientService signalRClientService;
        private DebugWindow debugWindow;
        private KeyboardHook keyboardHook;
        private ScheduleEvaluator scheduleEvaluator;
        private OnAirService onAirService;
        private IPlaybackContainer playbackContainer;
        private SeamlessSyncCoordinator playbackSyncCoordinator;
        private PortInfoManager portInfoManager;
        private int commStarted;
        private bool isShortcutCursorVisible;
        private bool isShortcutTaskbarVisible;
        private bool isShortcutTestMode;
        private bool isShortcutTopmost = true;
        private readonly object periodLock = new object();
        private Dictionary<string, ContentPeriodPayload> contentPeriodMap = new Dictionary<string, ContentPeriodPayload>(StringComparer.OrdinalIgnoreCase);

        //public ulong memorylimit = 3000;   // 3GB

        public MainWindow()
        {
            //Thread.CurrentThread.CurrentUICulture = new CultureInfo("en-US");

            InitializeComponent();

            InitEventHandler();
            Instance = this;
            InitShortcutHook();

            defaultCursor = this.Cursor;
            this.Cursor = Cursors.None;
            DeleteZeroByteContentsFiles();
        }

        //public void LoadPeriodData()
        //{
        //    var _data = XmlTools.ReadXml<List<PeriodData>>(FNDTools.GetPeriodDataFilePath());
        //    if (_data == null)
        //    {
        //        _data = new List<PeriodData>();
        //        XmlTools.WriteXml(FNDTools.GetPeriodDataFilePath(), _data);
        //    }

        //    _data.ForEach(fe =>
        //    {
        //        if (string.IsNullOrEmpty(fe.StartTime)) fe.StartTime = "00:00";
        //        if (string.IsNullOrEmpty(fe.EndTime)) fe.EndTime = "23:59";
        //    });

        //    sPeriodDics = _data.ToDictionary(x => x.FileName, x => x);
        //}

        public void DeleteZeroByteContentsFiles()
        {
            try
            {
                List<string> zeroFileList = new List<string>();
                zeroFileList.Clear();

                string[] strListNames = Directory.GetFiles(FNDTools.GetContentsRootDirPath());
        
                foreach (string item in strListNames)
                {
                    if (new FileInfo(item).Length == 0)
                    {
                        zeroFileList.Add(item);
                    }
                }

                if (zeroFileList.Count > 0)
                {

                    foreach (string  delFilePath in zeroFileList)
                    {
                        try
                        {
                            File.Delete(delFilePath);
                            Logger.WriteLog(string.Format("{0} <-- 0k 파일 삭제 성공", delFilePath), Logger.GetLogFileName());
                        }
                        catch (Exception ex)
                        {
                            Logger.WriteLog(string.Format("{0} <-- 0k 파일 삭제 실패", delFilePath), Logger.GetLogFileName());
                        }
                    }                    
                }
            }
            catch (Exception ex)
            { 
            }
        }

        public void InitEventHandler()
        {
            this.Loaded += MainWindow_Loaded;
            this.Closing += MainWindow_Closing;
        }

        private void InitShortcutHook()
        {
            try
            {
                keyboardHook = new KeyboardHook();
                keyboardHook.KeyDown += KeyboardHook_KeyDown;
            }
            catch (Exception ex)
            {
                Logger.WriteErrorLog($"KeyboardHook init failed: {ex}", Logger.GetLogFileName());
            }
        }

        private void KeyboardHook_KeyDown(object sender, KeyEventArgs e)
        {
            try
            {
                Dispatcher.BeginInvoke(new Action(() => HandleShortcutKey(e.Key)));
            }
            catch (Exception ex)
            {
                Logger.WriteErrorLog($"Keyboard shortcut dispatch failed: {ex}", Logger.GetLogFileName());
            }
        }

        private void SetDebugWindowVisible(bool visible)
        {
            if (visible)
            {
                if (debugWindow == null)
                {
                    debugWindow = new DebugWindow(this)
                    {
                        Owner = this
                    };
                    debugWindow.Closed += (s, e) => debugWindow = null;
                }

                if (!debugWindow.IsVisible)
                {
                    debugWindow.Show();
                }

                debugWindow.Start();
                return;
            }

            if (debugWindow != null)
            {
                debugWindow.Close();
                debugWindow = null;
            }
        }

        private void HandleShortcutKey(System.Windows.Input.Key key)
        {
            switch (key)
            {
                case System.Windows.Input.Key.F1:
                    ApplyCursorVisibility(!isShortcutCursorVisible);
                    break;

                case System.Windows.Input.Key.F2:
                    ApplyTaskbarVisibility(!isShortcutTaskbarVisible);
                    break;

                case System.Windows.Input.Key.F3:
                    ToggleShortcutTestMode();
                    break;

                case System.Windows.Input.Key.F4:
                    isShortcutTopmost = !isShortcutTopmost;
                    Topmost = isShortcutTopmost;
                    break;

                case System.Windows.Input.Key.F9:
                    ForceCloseByShortcut();
                    break;

                case System.Windows.Input.Key.F11:
                    if (isShortcutTestMode)
                    {
                        return;
                    }

                    WindowState = WindowState == WindowState.Normal ? WindowState.Maximized : WindowState.Normal;
                    break;

                case System.Windows.Input.Key.F12:
                    if (isShortcutTestMode)
                    {
                        return;
                    }

                    OpenConfigPlayer();
                    break;
            }
        }

        private void InitializeShortcutState()
        {
            isShortcutTestMode = g_LocalSettingsManager?.Settings?.IsTestMode ?? false;
            isShortcutCursorVisible = !(g_LocalSettingsManager?.Settings?.HideCursor ?? true);
            isShortcutTaskbarVisible = Taskbar.IsTaskbarVisible();
            isShortcutTopmost = Topmost;

            ApplyCursorVisibility(isShortcutCursorVisible);
            ApplyTaskbarVisibility(isShortcutTaskbarVisible);

            if (isShortcutTestMode)
            {
                WindowStyle = WindowStyle.SingleBorderWindow;
                ResizeMode = ResizeMode.CanResize;
                Topmost = false;
                isShortcutTopmost = false;
                ApplyTaskbarVisibility(true);
                ApplyCursorVisibility(true);
                SetDebugWindowVisible(true);
            }
            else
            {
                ResizeMode = ResizeMode.NoResize;
                SetDebugWindowVisible(false);
            }
        }

        private void ToggleShortcutTestMode()
        {
            isShortcutTestMode = !isShortcutTestMode;
            if (g_LocalSettingsManager?.Settings != null)
            {
                g_LocalSettingsManager.Settings.IsTestMode = isShortcutTestMode;
            }

            if (isShortcutTestMode)
            {
                WindowStyle = WindowStyle.SingleBorderWindow;
                ResizeMode = ResizeMode.CanResize;
                isShortcutTopmost = false;
                Topmost = false;
                ApplyTaskbarVisibility(true);
                ApplyCursorVisibility(true);
                SetDebugWindowVisible(true);
            }
            else
            {
                WindowStyle = WindowStyle.None;
                ResizeMode = ResizeMode.NoResize;
                isShortcutTopmost = true;
                Topmost = true;
                SetDebugWindowVisible(false);
                ApplyTaskbarVisibility(!(g_LocalSettingsManager?.Settings?.HideCursor ?? true));
                ApplyCursorVisibility(!(g_LocalSettingsManager?.Settings?.HideCursor ?? true));
            }
        }

        private void ApplyCursorVisibility(bool visible)
        {
            isShortcutCursorVisible = visible;

            if (visible)
            {
                Cursor = defaultCursor;
                forms.Cursor.Show();
                WindowTools.RestoreMouseCursor();
            }
            else
            {
                Cursor = Cursors.None;
                forms.Cursor.Hide();
            }
        }

        private void ApplyTaskbarVisibility(bool visible)
        {
            isShortcutTaskbarVisible = visible;

            if (visible)
            {
                Taskbar.Show();
            }
            else
            {
                Taskbar.Hide();
            }
        }

        private void ForceCloseByShortcut()
        {
            try
            {
                Taskbar.Show();
                forms.Cursor.Show();
                Cursor = defaultCursor;
                WindowTools.RestoreMouseCursor();
            }
            catch
            {
            }

            Close();
        }

        private void OpenConfigPlayer()
        {
            try
            {
                string configPlayerPath = Path.Combine(AppContext.BaseDirectory, "NewHyOn Player Settings.exe");
                if (!File.Exists(configPlayerPath))
                {
                    Logger.WriteErrorLog($"NewHyOn Player Settings not found: {configPlayerPath}", Logger.GetLogFileName());
                    return;
                }

                if (!ProcessTools.CheckExeProcessAlive("NewHyOn Player Settings"))
                {
                    ProcessTools.LaunchProcess(configPlayerPath);
                }
            }
            catch (Exception ex)
            {
                Logger.WriteErrorLog($"NewHyOn Player Settings launch failed: {ex}", Logger.GetLogFileName());
            }
        }

        void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            bool signalRStoppedForExit = false;

            if (keyboardHook != null)
            {
                keyboardHook.KeyDown -= KeyboardHook_KeyDown;
                keyboardHook.Dispose();
                keyboardHook = null;
            }

            if (debugWindow != null)
            {
                debugWindow.Close();
                debugWindow = null;
            }

            if (heartbeatReporter != null)
            {
                heartbeatReporter.SendStoppedAndStopSignalR();
                heartbeatReporter.Dispose();
                heartbeatReporter = null;
                signalRStoppedForExit = true;
            }

            var signalRServiceLocal = signalRClientService;
            signalRClientService = null;
            if (!signalRStoppedForExit && signalRServiceLocal != null)
            {
                signalRServiceLocal.StopForExit();
            }

            var commandServiceLocal = commandService;
            commandService = null;
            if (commandServiceLocal != null)
            {
                commandServiceLocal.Stop();
                DisposeInBackground(commandServiceLocal);
            }

            var rethinkServiceLocal = rethinkSyncService;
            rethinkSyncService = null;
            if (rethinkServiceLocal != null)
            {
                rethinkServiceLocal.Stop();
                DisposeInBackground(rethinkServiceLocal);
            }

            if (onAirService != null)
            {
                onAirService.Stop();
                onAirService.Dispose();
                onAirService = null;
            }

            playbackSyncCoordinator?.Dispose();
            playbackSyncCoordinator = null;

            playbackContainer?.Dispose();
            playbackContainer = null;

            PreExiting();
            WindowTools.AllowSleep();   // 잠들어도 된다.

            Settings.Default.WindowLocation = new Point(this.Left, this.Top);
            Settings.Default.Save();
        }

        private void DisposeInBackground(IDisposable disposable)
        {
            if (disposable == null)
            {
                return;
            }

            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    disposable.Dispose();
                }
                catch
                {
                }
            });
        }

        private void EnsureCommunicationStarted()
        {
            if (Interlocked.Exchange(ref commStarted, 1) == 1)
            {
                heartbeatReporter?.SendHeartbeatNow();
                return;
            }
            signalRClientService?.Start();
            heartbeatReporter?.Start();
            heartbeatReporter?.SendHeartbeatNow();
        }
        
        void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            NetworkTools.SetFlashTrustZone(FNDTools.GetContentsRootDirPath());
            WindowTools.PreventSleep();     // 잠들면 안돼!!

            SpecificTools.DisableWindowHWAcceleration(this, g_TTPlayerInfoManager.g_PlayerInfo.TTInfo_DAta2.Equals("NO", StringComparison.CurrentCultureIgnoreCase));

            // RethinkDB에서 플레이어 정보를 동기화하여 GUID/플레이리스트 등을 맞춘다.
            bool rethinkInitFailed = false;
            try
            {
                rethinkSyncService = new RethinkSyncService(g_PlayerInfoManager, g_LocalSettingsManager, 5000);
                rethinkSyncService.PlayerSynced += () =>
                {
                    g_PlayerName = g_PlayerInfoManager.g_PlayerInfo.PIF_PlayerName;
                    EnsureCommunicationStarted();
                };
                rethinkSyncService.PlayerGuidChanged += guid =>
                {
                    signalRClientService?.Reconnect();
                    heartbeatReporter?.SendHeartbeatNow();
                };
                rethinkSyncService.SyncFailed += () =>
                {
                    EnsureCommunicationStarted();
                };
                rethinkSyncService.WeeklyScheduleSynced += () =>
                {
                    HandleWeeklyScheduleUpdated();
                };
                rethinkSyncService.SpecialScheduleSynced += () =>
                {
                    HandleWeeklyScheduleUpdated();
                };
            }
            catch (Exception ex)
            {
                Logger.WriteErrorLog(ex.ToString(), Logger.GetLogFileName());
                rethinkInitFailed = true;
            }

            commandService = new RemoteCommandService(this);
            commandService.Start();

            signalRClientService = new SignalRClientService(this, commandService);

            heartbeatReporter = new HeartbeatReporter(this, signalRClientService);

            rethinkSyncService?.Start();
            if (rethinkInitFailed)
            {
                EnsureCommunicationStarted();
            }
            scheduleEvaluator = new ScheduleEvaluator(g_PlayerInfoManager);
            onAirService = new OnAirService(this);
            onAirService.Start();
            portInfoManager = new PortInfoManager();

            ChangePlayerStyle();
            InitializeShortcutState();

            AdjustCanvasSize();

            pixelDensity = WindowTools.GetPixelDensity(this);

            longSide = screenW > screenH ? screenW : screenH;

            g_PlayerName = g_PlayerInfoManager.g_PlayerInfo.PIF_PlayerName;

            EnsurePlaybackContainer();
            playbackSyncCoordinator?.Start();
            LoadContentPeriodCache();
            SetInitialLoadingVisible(true);
            playbackContainer?.StartInitialPlayback(g_PlayerInfoManager.g_PlayerInfo.PIF_DefaultPlayList);
            HandleWeeklyScheduleUpdated();

            //LoadPeriodData();


            if (ProcessTools.CheckExeProcessAlive(FNDTools.GetPCSProcName()) == false)
            {
                if (File.Exists(FNDTools.GetPCSchedulerExeFilePath()))
                {
                    ProcessTools.LaunchProcess(FNDTools.GetPCSchedulerExeFilePath());
                    Logger.WriteLog("PC Scheduler 실행", Logger.GetLogFileName());
                }
            }

            onAirService.Start();
        }

        private void LoadContentPeriodCache()
        {
            try
            {
                using (var repo = new ContentPeriodRepository())
                {
                    var list = repo.LoadAll();
                    var map = new Dictionary<string, ContentPeriodPayload>(StringComparer.OrdinalIgnoreCase);
                    foreach (var period in list ?? new List<ContentPeriodPayload>())
                    {
                        if (period == null || string.IsNullOrWhiteSpace(period.ContentGuid))
                        {
                            continue;
                        }

                        NormalizePeriod(period);
                        map[period.ContentGuid] = period;
                    }

                    lock (periodLock)
                    {
                        contentPeriodMap = map;
                    }
                }
            }
            catch
            {
                lock (periodLock)
                {
                    contentPeriodMap = new Dictionary<string, ContentPeriodPayload>(StringComparer.OrdinalIgnoreCase);
                }
            }
        }

        public void RefreshContentPeriodCache(IEnumerable<ContentPeriodPayload> periods)
        {
            if (periods == null)
            {
                return;
            }

            lock (periodLock)
            {
                foreach (var period in periods)
                {
                    if (period == null || string.IsNullOrWhiteSpace(period.ContentGuid))
                    {
                        continue;
                    }

                    NormalizePeriod(period);
                    contentPeriodMap[period.ContentGuid] = period;
                }
            }
        }

        public bool TryGetContentPeriod(string contentGuid, out ContentPeriodPayload period)
        {
            period = null;
            if (string.IsNullOrWhiteSpace(contentGuid))
            {
                return false;
            }

            lock (periodLock)
            {
                return contentPeriodMap.TryGetValue(contentGuid, out period);
            }
        }

        private static void NormalizePeriod(ContentPeriodPayload period)
        {
            if (period == null)
            {
                return;
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
        }

        private void EnsurePlaybackContainer()
        {
            if (playbackContainer != null)
            {
                return;
            }

            bool isSyncPlaybackEnabled = g_LocalSettingsManager?.Settings?.IsSyncEnabled ?? false;
            if (isSyncPlaybackEnabled)
            {
                var syncContainer = new SeamlessSyncPlaybackContainer(this, DesignerCanvas);
                syncContainer.Initialize();
                playbackContainer = syncContainer;

                playbackSyncCoordinator?.Dispose();
                playbackSyncCoordinator = new SeamlessSyncCoordinator(this, portInfoManager);
                playbackSyncCoordinator.AttachPlaybackContainer(syncContainer);
                return;
            }

            playbackSyncCoordinator?.Dispose();
            playbackSyncCoordinator = null;

            playbackContainer = new SeamlessPlaybackContainer(this, DesignerCanvas);
            playbackContainer.Initialize();
        }

        public void ChangePlayerStyle()
        {
            this.WindowStyle = WindowStyle.None;

            this.Left = Convert.ToDouble(g_TTPlayerInfoManager.g_PlayerInfo.TTInfo_DAta6);
            this.Top = Convert.ToDouble(g_TTPlayerInfoManager.g_PlayerInfo.TTInfo_Data7);

            MainScrollViewer.Width = g_FixedBaseWidth = Width = Convert.ToDouble(g_TTPlayerInfoManager.g_PlayerInfo.TTInfo_DAta8);
            MainScrollViewer.Height = g_FixedBaseHeight = Height = Convert.ToDouble(g_TTPlayerInfoManager.g_PlayerInfo.TTInfo_Data9);

            this.WindowState = WindowState.Normal;

            this.Dispatcher.Invoke(DispatcherPriority.Normal,
                new Action(() =>
                {
                    this.Topmost = !g_LocalSettingsManager.Settings.IsTestMode;
                }));

            DesignerCanvas.Width = g_FixedBaseWidth;
            DesignerCanvas.Height = g_FixedBaseHeight;

            if (g_LocalSettingsManager.Settings.HideCursor)
            {
                Taskbar.Hide();
                forms.Cursor.Hide();
            }
        }

        public void SetBaseSizeFromPageSize(double width, double height)
        {
            this.Dispatcher.Invoke(DispatcherPriority.Normal,
                new Action(() =>
                {
                    DesignerCanvas.Width = g_FixedBaseWidth = width;
                    DesignerCanvas.Height = g_FixedBaseHeight = height;
                    AdjustCanvasSize();
                }));
        }

        internal Size GetSeamlessViewportSize()
        {
            double width = 0;
            double height = 0;

            Dispatcher.Invoke(DispatcherPriority.Normal, new Action(() =>
            {
                width = MainScrollViewer?.ActualWidth ?? 0;
                height = MainScrollViewer?.ActualHeight ?? 0;

                if (width <= 0)
                {
                    width = DesignerCanvas?.ActualWidth > 0 ? DesignerCanvas.ActualWidth : DesignerCanvas?.Width ?? 0;
                }

                if (height <= 0)
                {
                    height = DesignerCanvas?.ActualHeight > 0 ? DesignerCanvas.ActualHeight : DesignerCanvas?.Height ?? 0;
                }
            }));

            if (width <= 0)
            {
                width = g_FixedBaseWidth > 0 ? g_FixedBaseWidth : 1920;
            }

            if (height <= 0)
            {
                height = g_FixedBaseHeight > 0 ? g_FixedBaseHeight : 1080;
            }

            return new Size(width, height);
        }

        internal bool IsPreserveAspectRatioEnabled()
        {
            string value = g_TTPlayerInfoManager?.g_PlayerInfo?.TTInfo_Data1 ?? string.Empty;
            return value.Equals("YES", StringComparison.OrdinalIgnoreCase);
        }

        internal void SetInitialLoadingVisible(bool visible, string message = null)
        {
            Dispatcher.Invoke(DispatcherPriority.Normal, new Action(() =>
            {
                if (InitialLoadingOverlay == null)
                {
                    return;
                }

                InitialLoadingOverlay.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
                if (InitialLoadingText != null && !string.IsNullOrWhiteSpace(message))
                {
                    InitialLoadingText.Text = message;
                }
            }));
        }

        public void DoApplicationShutdown()
        {
            Application.Current.Shutdown();
        }

        public void StopPlayback()
        {
            try
            {
                playbackContainer?.StopAll();
            }
            catch
            {
            }

            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

        public void HidePlayback()
        {
            playbackContainer?.HideAll();
        }

        bool g_IsUpdating = false;
        internal bool IsUpdating => g_IsUpdating;
        internal int CurrentPageElapsedSeconds => playbackContainer?.CurrentPageElapsedSeconds ?? 0;
        internal int CurrentPageDurationSeconds => playbackContainer?.CurrentPageDurationSeconds ?? 1;
        internal bool IsOnlySeamlessPage => playbackContainer?.IsOnlySinglePage ?? true;
        internal string CurrentPageListName => playbackContainer?.CurrentPageListName ?? string.Empty;
        internal string CurrentPageName => playbackContainer?.CurrentPageName ?? g_CurrentPageName;
        internal string NextPageName => playbackContainer?.NextPageName ?? string.Empty;
        internal bool IsPlaying => playbackContainer?.IsPresentationActive() ?? false;
        internal bool IsSyncPlaybackActive => playbackSyncCoordinator?.IsSyncPlaybackActive ?? (g_LocalSettingsManager?.Settings?.IsSyncEnabled ?? false);
        internal bool IsSyncLeader => playbackSyncCoordinator?.IsSyncLeader ?? (IsSyncPlaybackActive && (g_LocalSettingsManager?.Settings?.IsLeading ?? false));
        internal RemoteCommandService CommandService => commandService;
        internal ScheduleEvaluator ScheduleEvaluatorService => scheduleEvaluator;
        internal OnAirService OnAirServiceInstance => onAirService;
        internal long BeginUpdateHeartbeatReporting() => heartbeatReporter?.BeginUpdateReporting() ?? 0;
        internal void SendHeartbeatNow()
        {
            heartbeatReporter?.SendHeartbeatNow();
        }

        internal void ReportUpdateHeartbeatNow(string status, int progress, bool force, long sessionId)
        {
            heartbeatReporter?.ReportUpdateNow(status, progress, force, sessionId);
        }

        internal void EndUpdateHeartbeatReporting(long sessionId, bool sendNormalHeartbeatNow)
        {
            heartbeatReporter?.EndUpdateReporting(sessionId, sendNormalHeartbeatNow);
        }

        public void UpdateCurrentPageListName(string pageListName)
        {
            playbackContainer?.UpdateCurrentPageListName(pageListName);
        }

        public void PopPage()
        {
            playbackContainer?.PlayNextPage();
        }

        public void PlayFirstPage()
        {
            playbackContainer?.PlayFirstPage();
        }

        public void AdjustCanvasSize()
        {
            MainScrollViewer.UpdateLayout();
            DesignerCanvas.UpdateLayout();

            g_FitscaleValueX = MainScrollViewer.ActualWidth / DesignerCanvas.Width;
            g_FitscaleValueY = MainScrollViewer.ActualHeight / DesignerCanvas.Height;

            ScaleTransform scale = new ScaleTransform(g_FitscaleValueX, g_FitscaleValueY);
            DesignerCanvas.RenderTransform = scale;
        }

        internal void RequestScheduleEvaluation(bool force = false)
        {
            playbackContainer?.RequestScheduleEvaluation(force);
        }

        internal void HandleWeeklyScheduleUpdated()
        {
            playbackContainer?.HandleWeeklyScheduleUpdated();
        }

        internal void RequestPlaylistReload(string playlistName, string reason)
        {
            playbackContainer?.RequestPlaylistReload(playlistName, reason);
        }

        internal void RequestWeeklyScheduleSyncNow()
        {
            rethinkSyncService?.TriggerSyncNow();
        }

        internal void RequestPlayerGuidSyncNow()
        {
            rethinkSyncService?.TriggerSyncNow();
        }

        internal void StartPlaybackFromOffAir()
        {
            playbackContainer?.StartPlaybackFromOffAir();
        }

        internal void ResetPlaybackCursor()
        {
            g_PageIndex = 0;
        }

        private void Window_LocationChanged(object sender, EventArgs e)
        {
        }

        void PreExiting()
        {
            try
            {
                StopPlayback();
                if (!g_IsUpdating)
                {
                    Taskbar.Show();
                    forms.Cursor.Show();
                    this.Cursor = defaultCursor;
                    WindowTools.RestoreMouseCursor();
                }
            }
            catch (Exception ex)
            {
            }
        }
        internal List<PlaybackDebugItem> GetPlaybackDebugItems()
        {
            if (playbackContainer != null)
            {
                return playbackContainer.GetDebugItems();
            }

            return new List<PlaybackDebugItem>();
        }
    }
}
