using NewHyOnPlayer.Properties;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
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

        public int DisplayLimit = 10;

        ////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // 컨텐츠 재생 서브윈도우
        public List<ContentsPlayWindow> g_ContentsPlayWindowList = new List<ContentsPlayWindow>();  // 컨텐츠를 재생하는 윈도우
        //
        ////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        public string g_CurrentPageName = string.Empty;

        MultimediaTimer.Timer g_TickTimer = new MultimediaTimer.Timer();

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
        string g_CurrentPageListName = string.Empty;


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
        private SeamlessPlaybackContainer playbackContainer;
        private DateTime lastScheduleEval = DateTime.MinValue;
        private string pendingSchedulePlaylist = string.Empty;
        private string pendingScheduleId = string.Empty;
        private string lastScheduleEvalStateKey = string.Empty;
        private string lastMissingScheduleLogged = string.Empty;
        private string pendingPlaylistReload = string.Empty;
        private string pendingPlaylistReloadReason = string.Empty;
        private string lastPlaylistReloadStateKey = string.Empty;
        private string lastLocalFallbackPlaylist = string.Empty;
        private bool isScheduleSwitching;
        private PortInfoManager portInfoManager;
        private UdpSyncService syncService;
        private readonly object syncStateLock = new object();
        private int? pendingSyncIndex;
        private int lastPreparedFromIndex = -1;
        private int lastCommitFromIndex = -1;
        private bool hasReceivedSyncMessage;
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

            StopSyncService();

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

            StartSyncService();

            ChangePlayerStyle();
            InitializeShortcutState();

            AdjustCanvasSize();

            pixelDensity = WindowTools.GetPixelDensity(this);

            longSide = screenW > screenH ? screenW : screenH;
                                   
            g_PlayerName = g_PlayerInfoManager.g_PlayerInfo.PIF_PlayerName;

            CreateContentsPlayWindowForReady();
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

        private void StartSyncService()
        {
            if (!IsSyncPlaybackActive || syncService != null)
            {
                return;
            }

            hasReceivedSyncMessage = false;
            pendingSyncIndex = null;
            lastPreparedFromIndex = -1;
            lastCommitFromIndex = -1;

            int port = NetworkTools.SYNC_PORT;
            if (portInfoManager != null && portInfoManager.g_DataClassList.Count > 0)
            {
                port = portInfoManager.g_DataClassList[0].AIF_SYNC;
            }
            if (port <= 0)
            {
                port = NetworkTools.SYNC_PORT;
            }

            syncService = new UdpSyncService();
            syncService.MessageReceived += OnSyncMessageReceived;
            syncService.Start(port);
        }

        private void StopSyncService()
        {
            if (syncService == null)
            {
                return;
            }

            syncService.MessageReceived -= OnSyncMessageReceived;
            syncService.Stop();
            syncService.Dispose();
            syncService = null;
        }

        private void OnSyncMessageReceived(UdpSyncMessage message)
        {
            if (!IsSyncPlaybackActive || message == null)
            {
                return;
            }

            hasReceivedSyncMessage = true;

            if (message.Type == UdpSyncMessageType.Prepare)
            {
                lock (syncStateLock)
                {
                    pendingSyncIndex = message.Index;
                }
                return;
            }

            if (message.Type == UdpSyncMessageType.Commit)
            {
                bool shouldApply = false;
                lock (syncStateLock)
                {
                    if (pendingSyncIndex.HasValue && pendingSyncIndex.Value == message.Index)
                    {
                        pendingSyncIndex = null;
                        shouldApply = true;
                    }
                }

                if (shouldApply)
                {
                    Dispatcher.BeginInvoke(DispatcherPriority.Normal,
                        new Action(() => ApplySyncIndex(message.Index)));
                }
            }
        }

        private void ApplySyncIndex(int index)
        {
            if (playbackContainer != null)
            {
                playbackContainer.TryApplySyncIndex(index);
                return;
            }

            foreach (ContentsPlayWindow item in g_ContentsPlayWindowList)
            {
                if (item.IsVisible)
                {
                    item.TryApplySyncIndex(index);
                }
            }
        }

        private void HandleSyncLeaderTick(SeamlessSyncStatus status)
        {
            if (!IsSyncLeader || syncService == null || status == null)
            {
                return;
            }

            int currentIndex = status.CurrentIndex;
            int nextIndex = status.NextIndex;
            if (currentIndex < 0 || nextIndex < 0)
            {
                return;
            }

            long remaining = status.DurationSeconds - status.ElapsedSeconds;
            if (remaining <= 1)
            {
                if (lastPreparedFromIndex != currentIndex)
                {
                    syncService.SendPrepare(BuildSyncTargets(), nextIndex);
                    lastPreparedFromIndex = currentIndex;
                }
            }

            if (status.ElapsedSeconds >= status.DurationSeconds)
            {
                if (lastCommitFromIndex != currentIndex)
                {
                    syncService.SendCommit(BuildSyncTargets(), nextIndex);
                    lastCommitFromIndex = currentIndex;
                }
            }
        }

        private List<IPEndPoint> BuildSyncTargets()
        {
            List<IPEndPoint> targets = new List<IPEndPoint>();
            if (!IsSyncPlaybackActive)
            {
                return targets;
            }

            int port = NetworkTools.SYNC_PORT;
            if (portInfoManager != null && portInfoManager.g_DataClassList.Count > 0)
            {
                port = portInfoManager.g_DataClassList[0].AIF_SYNC;
            }
            if (port <= 0)
            {
                port = NetworkTools.SYNC_PORT;
            }

            HashSet<string> ipSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var configuredIps = g_LocalSettingsManager?.Settings?.SyncClientIps;
            if (configuredIps != null)
            {
                foreach (string ip in configuredIps)
                {
                    if (!string.IsNullOrWhiteSpace(ip))
                    {
                        ipSet.Add(ip.Trim());
                    }
                }
            }

            string selfIp = g_PlayerInfoManager?.g_PlayerInfo?.PIF_IPAddress;
            if (!string.IsNullOrWhiteSpace(selfIp))
            {
                ipSet.Add(selfIp.Trim());
            }

            try
            {
                IPAddress autoIp = NetworkTools.GetAutoIP();
                if (autoIp != null)
                {
                    ipSet.Add(autoIp.ToString());
                }
            }
            catch (Exception)
            {
            }

            ipSet.Add(IPAddress.Loopback.ToString());

            foreach (string ip in ipSet)
            {
                if (IPAddress.TryParse(ip, out IPAddress parsed))
                {
                    targets.Add(new IPEndPoint(parsed, port));
                }
            }

            return targets;
        }

        public void CreateContentsPlayWindowForReady()
        {
            if (playbackContainer == null)
            {
                playbackContainer = new SeamlessPlaybackContainer(this, DesignerCanvas);
                playbackContainer.Initialize();
            }

            if (g_ContentsPlayWindowList.Count > 0)
            {
                foreach (ContentsPlayWindow item in g_ContentsPlayWindowList)
                {
                    try
                    {
                        item.Close();
                    }
                    catch
                    {
                    }
                }
            }

            g_ContentsPlayWindowList.Clear();
        }

        double testMarginLeft = 0;
        double testMarginTop = 0;
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

        public void PlayPrevContents()
        {
            this.Dispatcher.Invoke(DispatcherPriority.Normal,
                new Action(() =>
                {
                    if (playbackContainer != null)
                    {
                        playbackContainer.PlayPreviousPage();
                        return;
                    }

                    Logger.WriteLog("PlayPrevContents Called.", Logger.GetLogFileName());

                    if (g_PageIndex > 1)
                    {
                        g_PageIndex = g_PageIndex - 2;
                    }
                    else if (g_PageIndex == 1)
                    {
                        g_PageIndex = g_PageInfoManager.g_PageInfoClassList.Count - 1;
                    }
                    else
                    {
                        g_PageIndex = g_PageInfoManager.g_PageInfoClassList.Count - 2;
                    }
                    PopPage();
                })
            );
        }

        public void PlayNextContents()
        {
            this.Dispatcher.Invoke(DispatcherPriority.Normal,
                new Action(() =>
                {
                    if (playbackContainer != null)
                    {
                        playbackContainer.PlayNextPage();
                        return;
                    }

                    StopTickTimer();
                    Logger.WriteLog("PlayNextContents Called.", Logger.GetLogFileName());
                    PopPage();
                })
            );
        }


        public void PlayFirstPage()
        {
            this.Dispatcher.Invoke(DispatcherPriority.Normal,
               new Action(() =>
               {
                   if (playbackContainer != null)
                   {
                       playbackContainer.PlayFirstPage();
                       return;
                   }

                   g_PageIndex = 0;
                   StopTickTimer();
                   Logger.WriteLog("PlayNextContents Called.", Logger.GetLogFileName());
                   PopPage();
               })
           );
        }

        public void DoApplicationShutdown()
        {
            Application.Current.Shutdown();
        }

        public void StopAllTimer()
        {
            try
            {
                playbackContainer?.StopAll();

                foreach (ContentsPlayWindow item in g_ContentsPlayWindowList)
                {
                    item.StopContentsDisplay();
                    item.StopVisibleContents();
                    item.Hide();
                }
            }
            catch (Exception ex)
            {

            }

            if (g_TickTimer != null)
            {
                g_TickTimer.Dispose();
            }

            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

        bool g_IsUpdating = false;
        internal bool IsUpdating => g_IsUpdating;
        internal int CurrentPageElapsedSeconds => playbackContainer != null ? playbackContainer.CurrentPageElapsedSeconds : g_TickCount;
        internal int CurrentPageDurationSeconds => playbackContainer != null ? playbackContainer.CurrentPageDurationSeconds : (int)g_TimeInterval;
        internal bool IsOnlySeamlessPage => playbackContainer != null ? playbackContainer.IsOnlySinglePage : g_PageInfoManager?.g_PageInfoClassList != null && g_PageInfoManager.g_PageInfoClassList.Count <= 1;
        internal string CurrentPageListName => playbackContainer != null ? playbackContainer.CurrentPageListName : g_CurrentPageListName;
        internal string CurrentPageName => playbackContainer != null ? playbackContainer.CurrentPageName : g_CurrentPageName;
        internal string NextPageName => playbackContainer != null ? playbackContainer.NextPageName : GetNextPageName();
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
            if (playbackContainer != null)
            {
                playbackContainer.UpdateCurrentPageListName(pageListName);
                return;
            }

            g_CurrentPageListName = pageListName;

            CheckOnlyOnePage();

            g_PageIndex = 0;
        }

        public void CheckOnlyOnePage()
        {
            if (playbackContainer != null)
            {
                return;
            }

            this.g_PageInfoManager.LoadData(g_CurrentPageListName);


            if (this.g_PageInfoManager.g_PageInfoClassList.Count == 1)
            {
                this.g_LocalSettingsManager.Settings.IsOnlyOnePage = true;
            }
            else
            {
                this.g_LocalSettingsManager.Settings.IsOnlyOnePage = false;
            }
        }

        private string GetNextPageName()
        {
            if (g_PageInfoManager == null || g_PageInfoManager.g_PageInfoClassList == null || g_PageInfoManager.g_PageInfoClassList.Count == 0)
            {
                return string.Empty;
            }

            int nextIndex = g_PageIndex;
            if (nextIndex >= g_PageInfoManager.g_PageInfoClassList.Count)
            {
                nextIndex = 0;
            }

            return g_PageInfoManager.g_PageInfoClassList[nextIndex].PIC_PageName;
        }

        public void PopPage()
        {
            playbackContainer?.PlayNextPage();
        }

        internal void HandleSeamlessSyncLeaderTick(SeamlessSyncStatus status)
        {
            HandleSyncLeaderTick(status);
        }

        public void ReOrderingContentsPlayWindowZOrder()
        {
            if (playbackContainer != null)
            {
                return;
            }

            List<WindowZIdxForTTClass> orderedList = (from item in g_WindowZIdxForTTClassList
                                                         orderby item.AI_Zorder
                                                         select item as WindowZIdxForTTClass).ToList();

            this.Dispatcher.Invoke(DispatcherPriority.Normal, (Action)(() =>
            {
                foreach (WindowZIdxForTTClass item in orderedList)
                {
                    if (item.AI_WindowType != WindowType.contentsPlayWindow)
                    {
                        continue;
                    }

                    if (item.AI_WindowIndex < 0 || item.AI_WindowIndex >= g_ContentsPlayWindowList.Count)
                    {
                        continue;
                    }

                    if (g_ContentsPlayWindowList[item.AI_WindowIndex].IsVisible)
                    {
                        g_ContentsPlayWindowList[item.AI_WindowIndex].Activate();
                    }
                }
            }));
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

        void InitTickTimer()
        {
            g_TickTimer.Mode = MultimediaTimer.TimerMode.Periodic;
            g_TickTimer.Period = 1000;  // 1 second
            g_TickTimer.Resolution = 1;
            g_TickTimer.SynchronizingObject = new DispatcherWinFormsCompatAdapter(this.Dispatcher);
            g_TickTimer.Tick += new System.EventHandler(TickTask);
        }
        
        public void RunTickTimer()
        {
            if (playbackContainer != null)
            {
                g_IsTickTimerStopped = true;
                return;
            }

            g_TickTimer.Start();
            g_IsTickTimerStopped = false;
        }

        public void StopTickTimer()
        {
            g_TickTimer.Stop();
            g_TickCount = 0;
            g_IsTickTimerStopped = true;
        }

        int g_TickCount = 0;
        bool g_IsTickTimerStopped = true;
        internal bool IsPlaying => playbackContainer != null ? playbackContainer.IsPresentationActive() : g_IsTickTimerStopped == false;
        internal bool IsSyncPlaybackActive => g_LocalSettingsManager?.Settings?.IsSyncEnabled ?? false;
        internal bool IsSyncLeader => IsSyncPlaybackActive && (g_LocalSettingsManager?.Settings?.IsLeading ?? false);
        internal bool ShouldHoldForSyncContent => IsSyncPlaybackActive && (IsSyncLeader || hasReceivedSyncMessage);
        internal bool HasPendingSyncPrepare
        {
            get
            {
                lock (syncStateLock)
                {
                    return pendingSyncIndex.HasValue;
                }
            }
        }

        internal void RequestScheduleEvaluation(bool force = false)
        {
            playbackContainer?.RequestScheduleEvaluation(force);
        }

        internal void HandleWeeklyScheduleUpdated()
        {
            playbackContainer?.HandleWeeklyScheduleUpdated();
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

        private void EvaluateSchedule(bool force)
        {
            if (scheduleEvaluator == null)
            {
                return;
            }

            DateTime now = DateTime.Now;
            lastScheduleEval = now;
            var decision = scheduleEvaluator.Evaluate(DateTime.Now, g_PlayerInfoManager?.g_PlayerInfo?.PIF_DefaultPlayList);
            if (decision == null || string.IsNullOrWhiteSpace(decision.PlaylistName))
            {
                pendingSchedulePlaylist = string.Empty;
                pendingScheduleId = string.Empty;
                lastScheduleEvalStateKey = "NONE";
                lastMissingScheduleLogged = string.Empty;
                return;
            }

            string current = g_PlayerInfoManager?.g_PlayerInfo?.PIF_CurrentPlayList ?? string.Empty;
            if (string.Equals(decision.PlaylistName, current, StringComparison.OrdinalIgnoreCase))
            {
                pendingSchedulePlaylist = string.Empty;
                pendingScheduleId = decision.ScheduleId ?? string.Empty;
                lastMissingScheduleLogged = string.Empty;
                string stateKey = $"KEEP|{pendingScheduleId}|{decision.PlaylistName}";
                if (!string.Equals(lastScheduleEvalStateKey, stateKey, StringComparison.Ordinal))
                {
                    Logger.WriteLog($"스케줄 평가: 현재 플레이리스트 유지 ({decision.PlaylistName})", Logger.GetLogFileName());
                    lastScheduleEvalStateKey = stateKey;
                }
                return;
            }

            pendingSchedulePlaylist = decision.PlaylistName;
            pendingScheduleId = decision.ScheduleId ?? string.Empty;
            lastMissingScheduleLogged = string.Empty;
            string pendingStateKey = $"PENDING|{pendingScheduleId}|{pendingSchedulePlaylist}";
            if (!string.Equals(lastScheduleEvalStateKey, pendingStateKey, StringComparison.Ordinal))
            {
                Logger.WriteLog($"스케줄 평가: 전환 예약 -> {pendingSchedulePlaylist}", Logger.GetLogFileName());
                lastScheduleEvalStateKey = pendingStateKey;
            }
        }

        private bool TryApplyScheduledSwitch(bool isPageBoundary, bool isContentBoundary)
        {
            return TryApplyScheduledSwitchCore(isPageBoundary, isContentBoundary, seamlessMode: false);
        }

        private bool TryApplyScheduledSwitchCore(bool isPageBoundary, bool isContentBoundary, bool seamlessMode)
        {
            if (string.IsNullOrWhiteSpace(pendingSchedulePlaylist))
            {
                return false;
            }

            string timing = g_LocalSettingsManager?.Settings?.SwitchTiming ?? "Immediately";
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

            var playerInfo = g_PlayerInfoManager?.g_PlayerInfo;
            if (playerInfo == null)
            {
                return false;
            }

            if (!HasPlayableContent(pendingSchedulePlaylist))
            {
                HandleMissingScheduleContent(pendingSchedulePlaylist);
                return false;
            }

            lastMissingScheduleLogged = string.Empty;
            isScheduleSwitching = true;
            playerInfo.PIF_CurrentPlayList = pendingSchedulePlaylist;
            g_PlayerInfoManager.SaveData();

            pendingSchedulePlaylist = string.Empty;

            g_PageInfoManager.LoadData(playerInfo.PIF_CurrentPlayList);
            g_PageIndex = 0;
            if (!seamlessMode)
            {
                StopTickTimer();
                this.Dispatcher.Invoke(DispatcherPriority.Normal,
                        new Action(() =>
                        {
                            PopPage();
                        }));
            }
            ApplyScheduleTransition();
            isScheduleSwitching = false;
            return true;
        }

        internal void RequestPlaylistReload(string playlistName, string reason)
        {
            playbackContainer?.RequestPlaylistReload(playlistName, reason);
        }

        private bool TryApplyPendingPlaylistReload(bool isPageBoundary, bool isContentBoundary)
        {
            return TryApplyPendingPlaylistReloadCore(isPageBoundary, isContentBoundary, seamlessMode: false);
        }

        private bool TryApplyPendingPlaylistReloadCore(bool isPageBoundary, bool isContentBoundary, bool seamlessMode)
        {
            if (string.IsNullOrWhiteSpace(pendingPlaylistReload))
            {
                return false;
            }

            string timing = g_LocalSettingsManager?.Settings?.SwitchTiming ?? "Immediately";
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

            var playerInfo = g_PlayerInfoManager?.g_PlayerInfo;
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

            g_PageInfoManager.LoadData(current);
            g_PageIndex = 0;
            if (!seamlessMode)
            {
                StopTickTimer();
                this.Dispatcher.Invoke(DispatcherPriority.Normal,
                        new Action(() =>
                        {
                            PopPage();
                        }));
            }
            ApplyScheduleTransition();
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

        private bool PageHasPlayableContent(PageInfoClass page)
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

            var info = g_PlayerInfoManager?.g_PlayerInfo;
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
                g_PlayerInfoManager.SaveData();
            }

            bool needsReload = !string.Equals(g_CurrentPageListName, playlistName, StringComparison.OrdinalIgnoreCase)
                               || g_PageInfoManager?.g_PageInfoClassList == null
                               || g_PageInfoManager.g_PageInfoClassList.Count == 0;
            if (needsReload)
            {
                UpdateCurrentPageListName(playlistName);
                g_PageInfoManager.LoadData(playlistName);
                g_PageIndex = 0;
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
                var playerInfo = g_PlayerInfoManager?.g_PlayerInfo;
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
                commandService?.EnsurePlaylistDownloadFromCache(playlistName);
            }
        }

        private void ApplyScheduleTransition()
        {
            try
            {
                Opacity = 0.0;

                var animation = new DoubleAnimation
                {
                    From = 0.0,
                    To = 1.0,
                    Duration = TimeSpan.FromMilliseconds(350),
                    AccelerationRatio = 0.1,
                    DecelerationRatio = 0.9
                };

                BeginAnimation(Window.OpacityProperty, animation);
            }
            catch (Exception ex)
            {
                Logger.WriteErrorLog(ex.ToString(), Logger.GetLogFileName());
            }
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
                        if (page?.PIC_Elements == null) continue;
                        foreach (var element in page.PIC_Elements)
                        {
                            if (element?.EIF_ContentsInfoClassList == null) continue;
                            foreach (var content in element.EIF_ContentsInfoClassList)
                            {
                                if (content == null || string.IsNullOrWhiteSpace(content.CIF_FileName)) continue;
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

        private void TickTask(object sender, EventArgs e)
        {
            try
            {
                this.Dispatcher.Invoke(DispatcherPriority.Normal,
                    new Action(() =>
                    {
                        if (IsFocused == false)
                            Focus();

                        if (playbackContainer != null)
                        {
                            return;
                        }

                        EvaluateSchedule(force: false);

                        string switchTiming = g_LocalSettingsManager?.Settings?.SwitchTiming ?? "Immediately";
                        if (switchTiming.Equals("Immediately", StringComparison.OrdinalIgnoreCase))
                        {
                            if (TryApplyScheduledSwitch(isPageBoundary: true, isContentBoundary: true))
                            {
                                return;
                            }
                            if (TryApplyPendingPlaylistReload(isPageBoundary: true, isContentBoundary: true))
                            {
                                return;
                            }
                        }
                        else if (switchTiming.Equals("ContentEnd", StringComparison.OrdinalIgnoreCase))
                        {
                            if (TryApplyScheduledSwitch(isPageBoundary: false, isContentBoundary: true))
                            {
                                return;
                            }
                            if (TryApplyPendingPlaylistReload(isPageBoundary: false, isContentBoundary: true))
                            {
                                return;
                            }
                        }

                        bool blockPageTimer = IsSyncPlaybackActive && !IsSyncLeader;
                        if (blockPageTimer && g_TickCount >= g_TimeInterval)
                        {
                            g_TickCount = 0;
                        }

                        if (g_TickCount >= g_TimeInterval)
                        {
                            // 페이지 종료 시점에 맞춰 최신 스케줄을 강제로 평가한다.
                            EvaluateSchedule(force: true);
                            StopTickTimer();
                            if (switchTiming.Equals("PageEnd", StringComparison.OrdinalIgnoreCase)
                                && TryApplyScheduledSwitch(isPageBoundary: true, isContentBoundary: false))
                            {
                                return;
                            }
                            if (switchTiming.Equals("PageEnd", StringComparison.OrdinalIgnoreCase)
                                && TryApplyPendingPlaylistReload(isPageBoundary: true, isContentBoundary: false))
                            {
                                return;
                            }

                            PopPage();

                            return;
                        }

                        bool _ready = !blockPageTimer && g_TimeInterval - g_TickCount == 1;
                        ContentsPlayWindow syncLeaderWindow = null;

                        foreach (ContentsPlayWindow item in g_ContentsPlayWindowList)
                        {
                            if (item.IsVisible)
                            {
                                item.Tick();
                                if (_ready && !g_LocalSettingsManager.Settings.IsOnlyOnePage)
                                    item.SetLoopState(false);
                                if (syncLeaderWindow == null)
                                {
                                    syncLeaderWindow = item;
                                }
                            }
                        }

                        if (syncLeaderWindow != null)
                        {
                            HandleSyncLeaderTick(new SeamlessSyncStatus
                            {
                                CurrentIndex = syncLeaderWindow.CurrentContentIndex,
                                NextIndex = syncLeaderWindow.GetNextContentIndex(),
                                ElapsedSeconds = syncLeaderWindow.CurrentContentElapsedSeconds,
                                DurationSeconds = syncLeaderWindow.CurrentContentDurationSeconds
                            });
                        }

                        g_TickCount++;

                    })
                );
            }
            catch (Exception ex)
            {
                Logger.WriteLog(ex.ToString(), Logger.GetLogFileName());
            }
        }

       
        void PreExiting()
        {
            try
            {
                StopAllTimer();
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
        public List<WindowZIdxForTTClass> g_WindowZIdxForTTClassList = new List<WindowZIdxForTTClass>();  // 전체 서브윈도우를 위한

        private static bool TryParseDisplayType(string rawType, out DisplayType displayType)
        {
            displayType = DisplayType.None;
            if (string.IsNullOrWhiteSpace(rawType))
            {
                return false;
            }

            return Enum.TryParse(rawType, true, out displayType);
        }


        public void HideAllContentsPlayWindow()
        {
            playbackContainer?.HideAll();

            if (g_ContentsPlayWindowList.Count > 0)
            {
                foreach (ContentsPlayWindow item in g_ContentsPlayWindowList)
                {
                    if (item.IsVisible)
                    {
                        item.StopContentsDisplay();
                        item.StopVisibleContents();
                        item.Hide();
                    }
                }
            }
        }


        public void UpdateContentsPlayingWindow(ElementInfoClass tmpInfoCls, int idx)
        {
            g_ContentsPlayWindowList[idx].Name = tmpInfoCls.EIF_Name;
            g_ContentsPlayWindowList[idx].Visibility = System.Windows.Visibility.Visible;
            
            g_ContentsPlayWindowList[idx].Width = tmpInfoCls.EIF_Width * g_FitscaleValueX;
            g_ContentsPlayWindowList[idx].Height = tmpInfoCls.EIF_Height * g_FitscaleValueY;
            g_ContentsPlayWindowList[idx].g_TransformX = g_FitscaleValueX;
            g_ContentsPlayWindowList[idx].g_TransformY = g_FitscaleValueY;

            g_ContentsPlayWindowList[idx].UpdateElementInfoClass(tmpInfoCls);

            MovedWindowForOne(idx);
        }

        private void Window_LocationChanged(object sender, EventArgs e)
        {
            if (playbackContainer != null)
            {
                return;
            }

            if (IsLoaded)
            {
                MovedWindowForAll();
            }
        }

        void MovedWindowForAll()
        {
            if (playbackContainer != null)
            {
                return;
            }

            foreach (ContentsPlayWindow cpw in g_ContentsPlayWindowList)
            {
                cpw.Left = cpw.Owner.Left
                            + testMarginLeft
                            + (cpw.g_ElementInfoClass.EIF_PosLeft*g_FitscaleValueX);

                cpw.Top = cpw.Owner.Top
                            + testMarginTop
                            + (cpw.g_ElementInfoClass.EIF_PosTop*g_FitscaleValueY);
            }
        }

        void MovedWindowForOne(int paramIdx)
        {
            if (playbackContainer != null)
            {
                return;
            }

            g_ContentsPlayWindowList[paramIdx].Left = g_ContentsPlayWindowList[paramIdx].Owner.Left
                           + testMarginLeft
                           + (g_ContentsPlayWindowList[paramIdx].g_ElementInfoClass.EIF_PosLeft * g_FitscaleValueX);

            g_ContentsPlayWindowList[paramIdx].Top = g_ContentsPlayWindowList[paramIdx].Owner.Top
                        + testMarginTop
                        + (g_ContentsPlayWindowList[paramIdx].g_ElementInfoClass.EIF_PosTop * g_FitscaleValueY);
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
