using HyOnPlayer.Properties;
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
using HyOnPlayer.Services;
using HyOnPlayer.DataManager;
using AndoW.Shared;
using forms = System.Windows.Forms;
using SharedElementInfoClass = AndoW.Shared.ElementInfoClass;

namespace HyOnPlayer
{
    /// <summary>
    /// MainWindow.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class MainWindow : Window
    {

        public int DisplayLimit = 10;
        public int WelcomeLimit = 10;
        public int ScrollLimit = 2;

        ////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // 컨텐츠 재생 서브윈도우
        public List<ContentsPlayWindow> g_ContentsPlayWindowList = new List<ContentsPlayWindow>();  // 컨텐츠를 재생하는 윈도우
        public List<ScrollTextPlayWindow> g_ScrollTextPlayWindowList = new List<ScrollTextPlayWindow>();  // 스크롤 텍스트를 재생하는 윈도우
        public List<WelcomeBoardWindow> g_WelcomeBoardWindowList = new List<WelcomeBoardWindow>();
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
        private ScheduleEvaluator scheduleEvaluator;
        private OnAirService onAirService;
        private DateTime lastScheduleEval = DateTime.MinValue;
        private string pendingSchedulePlaylist = string.Empty;
        private string pendingScheduleId = string.Empty;
        private string lastMissingScheduleLogged = string.Empty;
        private bool isScheduleSwitching;
        private PortInfoManager portInfoManager;
        private UdpSyncService syncService;
        private readonly object syncStateLock = new object();
        private int? pendingSyncIndex;
        private int lastPreparedFromIndex = -1;
        private int lastCommitFromIndex = -1;
        private bool hasReceivedSyncMessage;

        //public ulong memorylimit = 3000;   // 3GB

        public MainWindow()
        {
            //Thread.CurrentThread.CurrentUICulture = new CultureInfo("en-US");

            InitializeComponent();

            InitEventHandler();
            Instance = this;

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
            this.PreviewKeyDown += MainWindow_PreviewKeyDown;
        }

        private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.F1)
            {
                ToggleDebugWindow();
                e.Handled = true;
            }
        }

        private void ToggleDebugWindow()
        {
            if (debugWindow == null)
            {
                debugWindow = new DebugWindow(this)
                {
                    Owner = this
                };
                debugWindow.Closed += (s, e) => debugWindow = null;
            }

            if (debugWindow.IsVisible)
            {
                debugWindow.Close();
                debugWindow = null;
            }
            else
            {
                debugWindow.Show();
                debugWindow.Start();
            }
        }

        void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (debugWindow != null)
            {
                debugWindow.Close();
                debugWindow = null;
            }

            if (signalRClientService != null)
            {
                signalRClientService.Stop();
                signalRClientService.Dispose();
                signalRClientService = null;
            }

            if (commandService != null)
            {
                commandService.Stop();
                commandService.Dispose();
                commandService = null;
            }

            if (rethinkSyncService != null)
            {
                rethinkSyncService.Stop();
                rethinkSyncService.Dispose();
                rethinkSyncService = null;
            }

            if (heartbeatReporter != null)
            {
                heartbeatReporter.SendStopped();
                heartbeatReporter.Dispose();
                heartbeatReporter = null;
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
        
        void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            NetworkTools.SetFlashTrustZone(FNDTools.GetContentsRootDirPath());
            WindowTools.PreventSleep();     // 잠들면 안돼!!

            SpecificTools.DisableWindowHWAcceleration(this, g_TTPlayerInfoManager.g_PlayerInfo.TTInfo_DAta2.Equals("NO", StringComparison.CurrentCultureIgnoreCase));

            // RethinkDB에서 플레이어 정보를 동기화하여 GUID/플레이리스트 등을 맞춘다.
            try
            {
                rethinkSyncService = new RethinkSyncService(g_PlayerInfoManager, g_LocalSettingsManager, 5000);
                rethinkSyncService.PlayerSynced += () =>
                {
                    g_PlayerName = g_PlayerInfoManager.g_PlayerInfo.PIF_PlayerName;
                    heartbeatReporter?.SendHeartbeatNow();
                    if (signalRClientService != null && !string.IsNullOrWhiteSpace(g_PlayerInfoManager.g_PlayerInfo.PIF_GUID))
                    {
                        signalRClientService.Start();
                    }
                };
                rethinkSyncService.PlayerGuidChanged += guid =>
                {
                    signalRClientService?.Reconnect();
                };
                rethinkSyncService.WeeklyScheduleSynced += () =>
                {
                    scheduleEvaluator?.InvalidateWeeklyCache();
                    RequestScheduleEvaluation(force: true);
                };
            }
            catch (Exception ex)
            {
                Logger.WriteErrorLog(ex.ToString(), Logger.GetLogFileName());
            }

            commandService = new RemoteCommandService(this);
            commandService.Start();

            signalRClientService = new SignalRClientService(this, commandService);
            if (!string.IsNullOrWhiteSpace(g_PlayerInfoManager.g_PlayerInfo.PIF_GUID))
            {
                signalRClientService.Start();
            }

            heartbeatReporter = new HeartbeatReporter(this, signalRClientService);
            heartbeatReporter.Start();
            heartbeatReporter.SendHeartbeatNow();

            rethinkSyncService?.Start();
            scheduleEvaluator = new ScheduleEvaluator(g_PlayerInfoManager);
            onAirService = new OnAirService(this);
            onAirService.Start();
            portInfoManager = new PortInfoManager();
            StartSyncService();

            ChangePlayerStyle();

            AdjustCanvasSize();

            pixelDensity = WindowTools.GetPixelDensity(this);

            longSide = screenW > screenH ? screenW : screenH;
                                   
            g_PlayerName = g_PlayerInfoManager.g_PlayerInfo.PIF_PlayerName;

            CreateContentsPlayWindowForReady();

            UpdateCurrentPageListName(g_PlayerInfoManager.g_PlayerInfo.PIF_DefaultPlayList);

            InitTickTimer();

            this.g_PageInfoManager.LoadData(this.g_PlayerInfoManager.g_PlayerInfo.PIF_DefaultPlayList);

            g_PageIndex = 0;

            //LoadPeriodData();

            this.Dispatcher.Invoke(DispatcherPriority.Normal,
                        new Action(() =>
                        {
                            EvaluateSchedule(force: true);
                            if (TryApplyScheduledSwitch(isPageBoundary: true, isContentBoundary: true) == false)
                            {
                                Logger.WriteLog("PopPage in @MainWindow_Loaded Called.", Logger.GetLogFileName());
                                PopPage();
                            }
                        })
                    );


            if (ProcessTools.CheckExeProcessAlive(FNDTools.GetPCSProcName()) == false)
            {
                if (File.Exists(FNDTools.GetPCSchedulerExeFilePath()))
                {
                    ProcessTools.LaunchProcess(FNDTools.GetPCSchedulerExeFilePath());
                    Logger.WriteLog("PC Scheduler 실행", Logger.GetLogFileName());
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
            foreach (ContentsPlayWindow item in g_ContentsPlayWindowList)
            {
                if (item.IsVisible)
                {
                    item.TryApplySyncIndex(index);
                }
            }
        }

        private void HandleSyncLeaderTick(ContentsPlayWindow item)
        {
            if (!IsSyncLeader || syncService == null || item == null)
            {
                return;
            }

            int currentIndex = item.CurrentContentIndex;
            int nextIndex = item.GetNextContentIndex();
            if (currentIndex < 0 || nextIndex < 0)
            {
                return;
            }

            long remaining = item.CurrentContentDurationSeconds - item.CurrentContentElapsedSeconds;
            if (remaining <= 1)
            {
                if (lastPreparedFromIndex != currentIndex)
                {
                    syncService.SendPrepare(BuildSyncTargets(), nextIndex);
                    lastPreparedFromIndex = currentIndex;
                }
            }

            if (item.CurrentContentElapsedSeconds >= item.CurrentContentDurationSeconds)
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
            g_ContentsPlayWindowList.Clear();

            for (int i = 0; i < DisplayLimit; i++)
            {
                ContentsPlayWindow videoImgElement = new ContentsPlayWindow(this);                
                videoImgElement.Owner = Application.Current.MainWindow;
                videoImgElement.ShowInTaskbar = false;
                videoImgElement.Width = 0;
                videoImgElement.Height = 0;
                g_ContentsPlayWindowList.Add(videoImgElement);
                videoImgElement.Show();
                videoImgElement.Hide();

                SpecificTools.DisableWindowHWAcceleration(videoImgElement, g_TTPlayerInfoManager.g_PlayerInfo.TTInfo_DAta2.Equals("NO", StringComparison.CurrentCultureIgnoreCase));
            }

            g_WelcomeBoardWindowList.Clear();
            for (int i = 0; i < WelcomeLimit; i++)
            {
                WelcomeBoardWindow welcomeTmpWnd = new WelcomeBoardWindow();
                welcomeTmpWnd.Owner = Application.Current.MainWindow;
                welcomeTmpWnd.ShowInTaskbar = false;
                welcomeTmpWnd.Width = 0;
                welcomeTmpWnd.Height = 0;
                g_WelcomeBoardWindowList.Add(welcomeTmpWnd);
                welcomeTmpWnd.Show();
                welcomeTmpWnd.Hide();

                SpecificTools.DisableWindowHWAcceleration(welcomeTmpWnd, g_TTPlayerInfoManager.g_PlayerInfo.TTInfo_DAta2.Equals("NO", StringComparison.CurrentCultureIgnoreCase));
            }

            g_ScrollTextPlayWindowList.Clear();
            for (int i = 0; i < ScrollLimit; i++)
            {
                ScrollTextPlayWindow scrollWnd = new ScrollTextPlayWindow();
                scrollWnd.Owner = Application.Current.MainWindow;
                scrollWnd.ShowInTaskbar = false;
                scrollWnd.Width = 0;
                scrollWnd.Height = 0;
                g_ScrollTextPlayWindowList.Add(scrollWnd);
                scrollWnd.Show();
                scrollWnd.Hide();

                SpecificTools.DisableWindowHWAcceleration(scrollWnd, g_TTPlayerInfoManager.g_PlayerInfo.TTInfo_DAta2.Equals("NO", StringComparison.CurrentCultureIgnoreCase));
            }
        }

        double testMarginLeft = 0;
        double testMarginTop = 0;
        public void ChangePlayerStyle()
        {
            this.WindowStyle = WindowStyle.None;

            this.Left = Convert.ToDouble(g_TTPlayerInfoManager.g_PlayerInfo.TTInfo_DAta6);
            this.Top = Convert.ToDouble(g_TTPlayerInfoManager.g_PlayerInfo.TTInfo_Data7);

            MainScrollViewer.Width = Width = Convert.ToDouble(g_TTPlayerInfoManager.g_PlayerInfo.TTInfo_DAta8);
            MainScrollViewer.Height = Height = Convert.ToDouble(g_TTPlayerInfoManager.g_PlayerInfo.TTInfo_Data9);

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

        public void PlayPrevContents()
        {
            this.Dispatcher.Invoke(DispatcherPriority.Normal,
                new Action(() =>
                {
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
                foreach (ContentsPlayWindow item in g_ContentsPlayWindowList)
                {
                    item.StopContentsDisplay();
                    item.StopVisibleContents();
                    item.Hide();
                }

                foreach (ScrollTextPlayWindow item in g_ScrollTextPlayWindowList)
                {
                    item.StopAnimation();
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
        internal int CurrentPageElapsedSeconds => g_TickCount;
        internal int CurrentPageDurationSeconds => (int)g_TimeInterval;
        internal string CurrentPageListName => g_CurrentPageListName;
        internal string CurrentPageName => g_CurrentPageName;
        internal string NextPageName => GetNextPageName();
        internal RemoteCommandService CommandService => commandService;

        public void UpdateCurrentPageListName(string pageListName)
        {
            g_CurrentPageListName = pageListName;

            CheckOnlyOnePage();

            g_PageIndex = 0;
        }

        public void CheckOnlyOnePage()
        {
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
            try
            {
                if (g_PageInfoManager.g_PageInfoClassList.Count == 0)  // 재생할 컨텐츠 리스트가 없으면 그냥 리턴한다.
                {
                    Thread.Sleep(250);
                    return;
                }

                WindowTools.DeleteNotifyIcons();

                if (g_PageInfoManager.g_PageInfoClassList.Count == g_PageIndex)
                {
                    if (this.g_LocalSettingsManager.Settings.IsOnlyOnePage == true)
                    {
                        RunTickTimer();
                        return;
                    }

                    g_PageIndex = 0;
                }

                Logger.WriteLog(string.Format("g_PageInfoList.Count : {0} / g_PageIndex : {1}", g_PageInfoManager.g_PageInfoClassList.Count, g_PageIndex), Logger.GetLogFileName());

                string pageNameForPlaying = g_CurrentPageName = g_PageInfoManager.g_PageInfoClassList[g_PageIndex].PIC_PageName;
                int playTimeHour = g_PageInfoManager.g_PageInfoClassList[g_PageIndex].PIC_PlaytimeHour;
                int playTimeMin = g_PageInfoManager.g_PageInfoClassList[g_PageIndex].PIC_PlaytimeMinute;
                int playTimeSec = g_PageInfoManager.g_PageInfoClassList[g_PageIndex].PIC_PlaytimeSecond;

                g_PageIndex++;
                g_TimeInterval = (playTimeHour * 60 * 60) + (playTimeMin * 60) + playTimeSec;

                PlayPage(pageNameForPlaying);  

                foreach (ContentsPlayWindow item in g_ContentsPlayWindowList)
                {
                    if (item.IsVisible)
                    {
                        item.InitChangeEffect();
                        item.OrderingCanvasBGContents();
                        item.UpdateLayout();
                    }
                }

                RunTickTimer();

                ReOrderingContentsPlayWindowZOrder();

                heartbeatReporter?.SendHeartbeatNow();
            }
            catch (IndexOutOfRangeException ex)
            {
                Logger.WriteLog("g_PageIndex 인덱스 오류로 인해 g_PageIndex를 0 으로 세팅.", Logger.GetLogFileName());
                g_PageIndex = 0;
                RunTickTimer();
                Logger.WriteLog(ex.ToString(), Logger.GetLogFileName());
            }
            catch (Exception ex)
            {
                Logger.WriteLog(ex.ToString(), Logger.GetLogFileName());
            }
        }

        public void ReOrderingContentsPlayWindowZOrder()
        {
            List<WindowZIdxForTTClass> orderedList = (from item in g_WindowZIdxForTTClassList
                                                         orderby item.AI_Zorder
                                                         select item as WindowZIdxForTTClass).ToList();

            this.Dispatcher.Invoke(DispatcherPriority.Normal, (Action)(() =>
            {
                foreach (WindowZIdxForTTClass item in orderedList)
                {

                    switch (item.AI_WindowType)
                    {
                        case WindowType.contentsPlayWindow:
                            if (g_ContentsPlayWindowList[item.AI_WindowIndex].IsVisible)
                                g_ContentsPlayWindowList[item.AI_WindowIndex].Activate();
                            break;
                        case WindowType.scrollTextWindow:
                            if (g_ScrollTextPlayWindowList[item.AI_WindowIndex].IsVisible)
                                g_ScrollTextPlayWindowList[item.AI_WindowIndex].Activate();
                            break;
                        case WindowType.welcomeBoardWindow:
                            if (g_WelcomeBoardWindowList[item.AI_WindowIndex].IsVisible)
                                g_WelcomeBoardWindowList[item.AI_WindowIndex].Activate();
                            break;
                        default:
                            break;
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

            if (DesignerCanvas.ActualWidth > DesignerCanvas.ActualHeight)
            {
                if (g_FitscaleValueX > g_FitscaleValueY)
                {
                    ScaleTransform scale = new ScaleTransform(g_FitscaleValueX, g_FitscaleValueY);
                    DesignerCanvas.RenderTransform = scale;
                }
                else
                {
                    ScaleTransform scale = new ScaleTransform(g_FitscaleValueX, g_FitscaleValueX);
                    DesignerCanvas.RenderTransform = scale;
                }
            }
            else
            {
                if (g_FitscaleValueX < g_FitscaleValueY)
                {
                    ScaleTransform scale = new ScaleTransform(g_FitscaleValueX, g_FitscaleValueY);
                    DesignerCanvas.RenderTransform = scale;
                }
                else
                {
                    ScaleTransform scale = new ScaleTransform(g_FitscaleValueY, g_FitscaleValueY);
                    DesignerCanvas.RenderTransform = scale;
                }
            }
        }

        void InitTickTimer()
        {
            g_TickTimer.Mode = MultimediaTimer.TimerMode.Periodic;
            g_TickTimer.Period = 1000;  // 1 second
            g_TickTimer.Resolution = 1;
            g_TickTimer.SynchronizingObject = new DispatcherWinFormsCompatAdapter(this.Dispatcher);
            g_TickTimer.Tick += new System.EventHandler(TickTask);
        }
        

        void RunTickTimer()
        {
            g_TickTimer.Start();
            g_IsTickTimerStopped = false;
        }

        void StopTickTimer()
        {
            g_TickTimer.Stop();
            g_TickCount = 0;
            g_IsTickTimerStopped = true;
        }

        int g_TickCount = 0;
        bool g_IsTickTimerStopped = true;
        internal bool IsPlaying => g_IsTickTimerStopped == false;
        internal bool IsSyncPlaybackActive => g_LocalSettingsManager?.Settings?.IsSyncEnabled ?? false;
        internal bool IsSyncLeader => IsSyncPlaybackActive && (g_LocalSettingsManager?.Settings?.IsLeading ?? false);
        internal bool ShouldHoldForSyncContent => IsSyncPlaybackActive && (IsSyncLeader || hasReceivedSyncMessage);

        internal void RequestScheduleEvaluation(bool force = false)
        {
            EvaluateSchedule(force);
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
                lastMissingScheduleLogged = string.Empty;
                return;
            }

            string current = g_PlayerInfoManager?.g_PlayerInfo?.PIF_CurrentPlayList ?? string.Empty;
            if (string.Equals(decision.PlaylistName, current, StringComparison.OrdinalIgnoreCase))
            {
                pendingSchedulePlaylist = string.Empty;
                pendingScheduleId = decision.ScheduleId ?? string.Empty;
                lastMissingScheduleLogged = string.Empty;
                Logger.WriteLog($"스케줄 평가: 현재 플레이리스트 유지 ({decision.PlaylistName})", Logger.GetLogFileName());
                return;
            }

            pendingSchedulePlaylist = decision.PlaylistName;
            pendingScheduleId = decision.ScheduleId ?? string.Empty;
            lastMissingScheduleLogged = string.Empty;
            Logger.WriteLog($"스케줄 평가: 전환 예약 -> {pendingSchedulePlaylist}", Logger.GetLogFileName());
        }

        private bool TryApplyScheduledSwitch(bool isPageBoundary, bool isContentBoundary)
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
            StopTickTimer();
            PopPage();
            ApplyScheduleTransition();
            isScheduleSwitching = false;
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
                        if (page?.PIC_Elements == null) continue;
                        foreach (var element in page.PIC_Elements)
                        {
                            if (element?.EIF_ContentsInfoClassList == null) continue;
                            foreach (var content in element.EIF_ContentsInfoClassList)
                            {
                                if (content == null || string.IsNullOrWhiteSpace(content.CIF_FileName)) continue;
                                string path = FNDTools.GetContentsFilePath(content.CIF_FileName);
                                if (File.Exists(path))
                                {
                                    return true;
                                }
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

                        EvaluateSchedule(force: false);

                        string switchTiming = g_LocalSettingsManager?.Settings?.SwitchTiming ?? "Immediately";
                        if (switchTiming.Equals("Immediately", StringComparison.OrdinalIgnoreCase))
                        {
                            if (TryApplyScheduledSwitch(isPageBoundary: true, isContentBoundary: true))
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
                            PopPage();

                            return;
                        }
                        else
                        {
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
                                HandleSyncLeaderTick(syncLeaderWindow);
                            }
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


        public List<WindowIdxForZorderClass> g_WindowIdxForZorderClassList = new List<WindowIdxForZorderClass>();  // 컨텐츠 재생 윈도우를 위한 
        public List<WindowIdxForZorderClass> g_WndZIdxForScrollList = new List<WindowIdxForZorderClass>();  // 자막을 위한

        public List<WindowZIdxForTTClass> g_WindowZIdxForTTClassList = new List<WindowZIdxForTTClass>();  // 전체 서브윈도우를 위한


        public void PlayPage(string paramPageName)
        {
            g_ElementInfoManager.LoadData(paramPageName);

            PageInfoClass currentPage = g_PageInfoManager.GetPageDefinition(paramPageName);
            DetectMissingContentsForPlaylist(g_PlayerInfoManager?.g_PlayerInfo?.PIF_CurrentPlayList);
            if (currentPage != null)
            {
                g_ElementInfoManager.g_ElementInfoClassList = new List<ElementInfoClass>();
                if (currentPage.PIC_Elements != null)
                {
                    foreach (SharedElementInfoClass element in currentPage.PIC_Elements)
                    {
                        ElementInfoClass clone = new ElementInfoClass();
                        clone.CopyData(element);
                        g_ElementInfoManager.g_ElementInfoClassList.Add(clone);
                    }
                }

                g_FixedBaseWidth = currentPage.PIC_CanvasWidth;
                g_FixedBaseHeight = currentPage.PIC_CanvasHeight;
            }

            int diplayCnt = 0;
            int subtitleCnt = 0;
            int textElementCnt = 0;

            HideAllContentsPlayWindow();

            g_WindowIdxForZorderClassList.Clear();   // <-------- 서브윈도우의 Z-Idx를 위한초기작업

            g_WindowZIdxForTTClassList.Clear();


            if (g_ElementInfoManager.g_ElementInfoClassList.Count > 0)
            {
                foreach (ElementInfoClass item in g_ElementInfoManager.g_ElementInfoClassList)
                {

                    switch ((DisplayType)Enum.Parse(typeof(DisplayType), item.EIF_Type))
                    {
                        case DisplayType.Media:
                            UpdateContentsPlayingWindow(item, diplayCnt);
                            WindowZIdxForTTClass tmpCls = new WindowZIdxForTTClass();
                            tmpCls.AI_WindowIndex = diplayCnt;
                            tmpCls.AI_Zorder = item.EIF_ZIndex;
                            tmpCls.AI_WindowType = WindowType.contentsPlayWindow;
                            g_WindowZIdxForTTClassList.Add(tmpCls);

                            diplayCnt++;
                            break;

                        case DisplayType.ScrollText:
                            UpdateSubTitleWindow(item, subtitleCnt);
                            WindowZIdxForTTClass tmpCls2 = new WindowZIdxForTTClass();
                            tmpCls2.AI_WindowIndex = subtitleCnt;
                            tmpCls2.AI_Zorder = item.EIF_ZIndex;
                            tmpCls2.AI_WindowType = WindowType.scrollTextWindow;
                            g_WindowZIdxForTTClassList.Add(tmpCls2);

                            subtitleCnt++;
                            break;

                        case DisplayType.WelcomeBoard:
                            UpdateWelcomeBoardDispWindow(item, textElementCnt, paramPageName);
                            WindowZIdxForTTClass tmpCls3 = new WindowZIdxForTTClass();
                            tmpCls3.AI_WindowIndex = textElementCnt;
                            tmpCls3.AI_Zorder = item.EIF_ZIndex;
                            tmpCls3.AI_WindowType = WindowType.welcomeBoardWindow;
                            g_WindowZIdxForTTClassList.Add(tmpCls3);

                            textElementCnt++;
                            break;
                        default:
                            break;
                    }
                }
            }
        }

        public void HideAllContentsPlayWindow()
        {
            if (g_ScrollTextPlayWindowList.Count > 0)
            {
                foreach (ScrollTextPlayWindow item in g_ScrollTextPlayWindowList)
                {
                    if (item.IsVisible)
                    {
                        item.StopAnimation();
                        item.Hide();
                    }
                }
            }

            if (g_WelcomeBoardWindowList.Count > 0)
            {
                foreach (WelcomeBoardWindow item in g_WelcomeBoardWindowList)
                {
                    if (item.IsVisible)
                    {
                        item.Hide();
                    }
                }
            }

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

        public void UpdateWelcomeBoardDispWindow(ElementInfoClass tmpInfoCls, int idx, string paramPageName)
        {
           
            g_WelcomeBoardWindowList[idx].Name = tmpInfoCls.EIF_Name;
            g_WelcomeBoardWindowList[idx].Visibility = System.Windows.Visibility.Visible;

            g_WelcomeBoardWindowList[idx].Width = tmpInfoCls.EIF_Width * g_FitscaleValueX;
            g_WelcomeBoardWindowList[idx].Height = tmpInfoCls.EIF_Height * g_FitscaleValueY;
            g_WelcomeBoardWindowList[idx].UpdateTextInfoClsFromPage(tmpInfoCls, paramPageName);

            MovedWindowForWelcomeWindowForOne(idx);
        }

        public void UpdateSubTitleWindow(ElementInfoClass tmpInfoCls, int idx)
        {
            g_ScrollTextPlayWindowList[idx].Name = tmpInfoCls.EIF_Name;
            g_ScrollTextPlayWindowList[idx].Visibility = System.Windows.Visibility.Visible;

            g_ScrollTextPlayWindowList[idx].Width = tmpInfoCls.EIF_Width * g_FitscaleValueX;
            g_ScrollTextPlayWindowList[idx].Height = tmpInfoCls.EIF_Height * g_FitscaleValueY;

            double scaledHeight = tmpInfoCls.EIF_Height * g_FitscaleValueY;
            g_ScrollTextPlayWindowList[idx].UpdateScrollTextList(tmpInfoCls, scaledHeight);

            MovedWindowForScrollTextForOne(idx);
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
            if (IsLoaded)
            {
                MovedWindowForAll();
                MovedWindowForScrollTextForAll();
                MovedWindowForWelcomeWindowForAll();
            }
        }

        void MovedWindowForAll()
        {
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

        void MovedWindowForWelcomeWindowForAll()
        {
            foreach (WelcomeBoardWindow cpw in g_WelcomeBoardWindowList)
            {
                cpw.Left = cpw.Owner.Left
                            + testMarginLeft
                            + (cpw.g_ElementInfoClass.EIF_PosLeft * g_FitscaleValueX);

                cpw.Top = cpw.Owner.Top
                            + testMarginTop
                            + (cpw.g_ElementInfoClass.EIF_PosTop * g_FitscaleValueY);
            }
        }

        void MovedWindowForScrollTextForAll()
        {
            foreach (ScrollTextPlayWindow cpw in g_ScrollTextPlayWindowList)
            {
                cpw.Left = cpw.Owner.Left
                            + testMarginLeft
                            + (cpw.g_ElementInfoClass.EIF_PosLeft * g_FitscaleValueX);

                cpw.Top = cpw.Owner.Top
                            + testMarginTop
                            + (cpw.g_ElementInfoClass.EIF_PosTop * g_FitscaleValueY);
            }
        }

        void MovedWindowForOne(int paramIdx)
        {
            g_ContentsPlayWindowList[paramIdx].Left = g_ContentsPlayWindowList[paramIdx].Owner.Left
                           + testMarginLeft
                           + (g_ContentsPlayWindowList[paramIdx].g_ElementInfoClass.EIF_PosLeft * g_FitscaleValueX);

            g_ContentsPlayWindowList[paramIdx].Top = g_ContentsPlayWindowList[paramIdx].Owner.Top
                        + testMarginTop
                        + (g_ContentsPlayWindowList[paramIdx].g_ElementInfoClass.EIF_PosTop * g_FitscaleValueY);
        }

        void MovedWindowForWelcomeWindowForOne(int paramIdx)
        {
            try
            {
                g_WelcomeBoardWindowList[paramIdx].Left = g_WelcomeBoardWindowList[paramIdx].Owner.Left
                                + testMarginLeft
                                + (g_WelcomeBoardWindowList[paramIdx].g_ElementInfoClass.EIF_PosLeft * g_FitscaleValueX);

                g_WelcomeBoardWindowList[paramIdx].Top = g_WelcomeBoardWindowList[paramIdx].Owner.Top
                            + testMarginTop
                            + (g_WelcomeBoardWindowList[paramIdx].g_ElementInfoClass.EIF_PosTop * g_FitscaleValueY);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
        }

        void MovedWindowForScrollTextForOne(int paramIdx)
        {
            g_ScrollTextPlayWindowList[paramIdx].Left = g_ScrollTextPlayWindowList[paramIdx].Owner.Left
                            + testMarginLeft
                            + (g_ScrollTextPlayWindowList[paramIdx].g_ElementInfoClass.EIF_PosLeft * g_FitscaleValueX);

            g_ScrollTextPlayWindowList[paramIdx].Top = g_ScrollTextPlayWindowList[paramIdx].Owner.Top
                        + testMarginTop
                        + (g_ScrollTextPlayWindowList[paramIdx].g_ElementInfoClass.EIF_PosTop * g_FitscaleValueY);
        }
    }
}
