using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using AndoW.Shared;
using TurtleTools;


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

        DispatcherTimer checkTimer = new DispatcherTimer();

        public static MainWindow Instance { get; set; }

        public Page1 g_Page1 = null;
        public Page2 g_Page2 = null;
        public Page3 g_Page3 = null;
        public Page5 g_Page5 = null;

        public Dictionary<string, List<string>> g_fontsDic;

        public MainWindow()
        {
            this.InitializeComponent();

            Instance = this;

            ProcessTools.KillVNCViewer();
            NetworkTools.StopFTPSrv();

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

        public void InitPages()
        {
            g_Page1 = new Page1();
            g_Page2 = new Page2();
            g_Page3 = new Page3();
            g_Page5 = new Page5();

            RefreshSavedPageList();
            g_Page2.RefreshPageNameList();
            g_Page3.RefreshPlayerInfoList();
        }

        public void RefreshSavedPageList()
        {
            g_Page1.RefreshSavedPageList();
            g_Page2.RefreshSavedPageList();
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
            if(checkTimer != null)
                checkTimer.Stop();

            SignalRServerTools.StopSignalRServer();

            Process.GetCurrentProcess().Kill();
        }

        async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            RethinkDbConfigurator.EnsureConfigured();

            bool shouldShowDbDialog = !RethinkDbBootstrapper.IsRethinkDbRunning();
            DbLoadingWindow dbLoadingWindow = null;

            try
            {
                if (shouldShowDbDialog)
                {
                    dbLoadingWindow = new DbLoadingWindow();
                    dbLoadingWindow.Show();
                    dbLoadingWindow.Activate();
                    await Dispatcher.Yield(DispatcherPriority.Background);
                }

                bool dbReady = await Task.Run(() => RethinkDbBootstrapper.EnsureAndWaitTablesReadyAsync(RethinkDbConfigurator.GetDataDatabaseName()));

                if (!dbReady)
                {
                    dbLoadingWindow?.Close();
                    dbLoadingWindow = null;
                    MessageBox.Show("DB 상태를 확인해 주세요.", "DB 구동 실패", MessageBoxButton.OK, MessageBoxImage.Error);
                    this.Close();
                }
            }
            finally
            {
                dbLoadingWindow?.Close();
            }

            InitPages();

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

            CheckAndAddSecurityRules();

            NetworkTools.SetFTPConfigHomeDir();
            NetworkTools.StartFTPSrv();
            SignalRServerTools.StartSignalRServer();

            checkTimer.Tick += new EventHandler(checkTimer_Tick);
            checkTimer.Interval = new TimeSpan(0, 0, 4);
            checkTimer.Start();
        }

        private void checkTimer_Tick(object sender, EventArgs e)
        {
        }

        public void SendMsgToAndroid(string playername, string msg)
        {
            if (string.IsNullOrWhiteSpace(playername) || string.IsNullOrWhiteSpace(msg))
            {
                return;
            }
            
            try
            {
                DataShop.Instance.g_PlayerInfoManager.SetPendingCommand(playername, msg);
            }
            catch (Exception ex)
            {
                Logger.WriteErrorLog($"SendMsgToAndroid failed. player={playername}, msg={msg}, ex={ex}", Logger.GetLogFileName());
            }
        }

        public bool EnqueueCommandForPlayer(PlayerInfoClass player, string command, string payloadBase64 = "", bool pushSignalR = true)
        {
            if (player == null || string.IsNullOrWhiteSpace(player.PIF_GUID) || string.IsNullOrWhiteSpace(command))
            {
                return false;
            }
            string playerId = player.PIF_GUID.Trim();
            string normalizedCommand = command.Trim();
            string resolvedPayload = payloadBase64 ?? string.Empty;
            if (string.IsNullOrWhiteSpace(resolvedPayload)
                && string.Equals(normalizedCommand, RP_ORDER.updateschedule.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                resolvedPayload = BuildSchedulePayloadBase64(player);
            }

            if (IsAndroidPlayer(player))
            {
                SendMsgToAndroid(player.PIF_PlayerName, normalizedCommand);
                return true;
            }

            var queueManager = DataShop.Instance.g_CommandQueueManager;
            var entry = queueManager.EnqueueCommand(playerId, normalizedCommand, resolvedPayload, "manager");
            if (entry == null)
            {
                return false;
            }

            queueManager.SupersedePending(playerId, entry.Id);

            if (pushSignalR && SignalRServerTools.IsRunning())
            {
                var envelope = new SignalRCommandEnvelope
                {
                    CommandId = entry.Id,
                    Command = entry.Command,
                    PlayerId = playerId,
                    PayloadJson = entry.PayloadBase64,
                    CreatedAt = entry.CreatedAt
                };

                if (SignalRServerTools.TrySendCommandToClient(playerId, envelope))
                {
                    queueManager.MarkStatus(entry.Id, playerId, "sent");
                }
            }

            return true;
        }

        public bool SendUrgentUpdateList(PlayerInfoClass player)
        {
            if (player == null || string.IsNullOrWhiteSpace(player.PIF_GUID))
            {
                return false;
            }

            if (IsAndroidPlayer(player))
            {
                SendMsgToAndroid(player.PIF_PlayerName, RP_ORDER.updatelist.ToString());
                return true;
            }

            if (!SignalRServerTools.IsRunning())
            {
                return false;
            }

            string payloadBase64 = DataShop.Instance.g_UpdatePayloadBuilder.BuildPayloadBase64(player);
            var envelope = new SignalRCommandEnvelope
            {
                CommandId = Guid.NewGuid().ToString(),
                Command = RP_ORDER.updatelist.ToString(),
                PlayerId = player.PIF_GUID,
                PayloadJson = payloadBase64,
                CreatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                IsUrgent = true
            };

            return SignalRServerTools.TrySendCommandToClient(player.PIF_GUID, envelope);
        }

        private static bool IsAndroidPlayer(PlayerInfoClass player)
        {
            if (player == null || string.IsNullOrWhiteSpace(player.PIF_OSName))
            {
                return false;
            }

            return player.PIF_OSName.IndexOf("android", StringComparison.OrdinalIgnoreCase) >= 0;
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
                    var contract = builder.BuildContractPayload(player, pageList, pages);

                    playlistPayloads.Add(new SchedulePlaylistPayload
                    {
                        PlaylistName = pageList.PLI_PageListName,
                        PageList = pageList,
                        Pages = pages,
                        Contract = contract
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

        Dictionary<string, string> progDic = new Dictionary<string, string>();
        Dictionary<string, int> portDic = new Dictionary<string, int>();
        void CheckAndAddSecurityRules()
        {
            SecurityTools.SetICMP();

            var _serverSettings = DataShop.Instance.g_ServerSettingsManager.sData;
            if (_serverSettings != null)
            {
                if (SecurityTools.NeedToAddRule("ftp_ports"))
                    SecurityTools.OpenPasvFTPPorts("ftp_ports", _serverSettings.FTP_PasvMinPort, _serverSettings.FTP_PasvMaxPort);
            }

            if (SecurityTools.NeedToAddRule("signage_manager"))
                progDic.Add("signage_manager", FNDTools.GetManagerExeFilePath());

            if (SecurityTools.NeedToAddRule("ftp_srv"))
                progDic.Add("ftp_srv", NetworkTools.GetFTPServerExePath());
            
            SecurityTools.ReleaseFirewallRules(SecurityTools.CreateAuthorAppNetshCmdList(progDic));
            SecurityTools.ReleaseFirewallRules(SecurityTools.CreateOpenPortNetshCmdList(portDic));
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
            //ProcessTools.CheckExeProcessAlive();
            return true;
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
