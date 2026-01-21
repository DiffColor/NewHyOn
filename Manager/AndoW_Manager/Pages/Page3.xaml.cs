using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using TurtleTools;

namespace AndoW_Manager
{
    /// <summary>
    /// Interaction logic for UserControl1.xaml
    /// </summary>
    public partial class Page3 : UserControl
    {
        public bool g_IsSelected = false;
     
        public int g_Idx = 0;

        string g_CurrentSelectedPlayerName = string.Empty;

        public PlayerInfoClass g_CurrentSelectedPlayerInfoClass = new PlayerInfoClass();

        public bool g_IsPlayerFolding = false;

        public List<PlayerInfoElement> g_PlayerInfoElementList = new List<PlayerInfoElement>();
        public PlayerGroupClass g_CurrentSelectedPlayerGroupClass = null;
        public List<PlayerGroupButtonElement> g_PlayerGroupButtonElementList = new List<PlayerGroupButtonElement>();

        public bool Is_ComboBoxInit = true;
        private readonly PlayerHeartbeatMonitor _heartbeatMonitor;
        private bool _heartbeatDisposed;
        private readonly Dictionary<string, PlayerHeartbeatState> _pendingHeartbeatStates =
            new Dictionary<string, PlayerHeartbeatState>(StringComparer.OrdinalIgnoreCase);
        private readonly object _heartbeatStateLock = new object();
        private bool _heartbeatUiUpdateScheduled;
        private readonly DispatcherTimer _heartbeatUiTimer;
        private Dictionary<string, PlayerInfoElement> _playerElementById =
            new Dictionary<string, PlayerInfoElement>(StringComparer.OrdinalIgnoreCase);
        private Dictionary<string, PlayerInfoElement> _playerElementByName =
            new Dictionary<string, PlayerInfoElement>(StringComparer.OrdinalIgnoreCase);
        private HashSet<PlayerInfoElement> _visiblePlayerElements = new HashSet<PlayerInfoElement>();
        private bool _visibleRefreshScheduled;

        public bool g_isUpdating = false;

        public static Page3 Instance { get; set; }

        public Page3()
        {
            InitializeComponent();

            Instance = this;

            _heartbeatMonitor = new PlayerHeartbeatMonitor();
            _heartbeatMonitor.HeartbeatsChanged += OnHeartbeatsChanged;
            SignalRServerTools.HeartbeatReceived += OnSignalRHeartbeatReceived;
            _heartbeatUiTimer = new DispatcherTimer(DispatcherPriority.Background, Dispatcher)
            {
                Interval = TimeSpan.FromMilliseconds(100)
            };
            _heartbeatUiTimer.Tick += HeartbeatUiTimer_Tick;
            _heartbeatMonitor.Start();
            Unloaded += OnPageUnloaded;
            InitEventHandler();

            ShowAndHideInfoGrid(false);
            
            //PlayerControlfPannel.Visibility = System.Windows.Visibility.Hidden;
            //DataTextBlk.Text = DateTime.Now.ToLongDateString();

            EditorSelector();
        }

        void EditorSelector()
        {
            //if (string.IsNullOrEmpty(Page3.Instance.g_CurrentSelectedPlayerInfoClass.PIF_PlayerName))
            //{
                int portCount = 0;
                foreach (PlayerInfoElement pie in g_PlayerInfoElementList)
                {
                    if (pie.g_PlayerInfoClass.PIF_IsLandScape) continue;

                    portCount++;
                }

                if (g_PlayerInfoElementList.Count - portCount < portCount)
                {
                    MainWindow.Instance.isPortraitEditor = true;
                }
            //}
            //else
            //{
            //    g_ParentWnd.isPortraitEditor = !Page3.Instance.g_CurrentSelectedPlayerInfoClass.PIF_IsLandScape;
            //}
        }
        //public DispatcherTimer clockTimer = new DispatcherTimer();

        //public void InitTimer()
        //{
        //    clockTimer.Tick += clockTimer_Tick;
        //    clockTimer.Interval = new TimeSpan(0, 0, 1);
        //    clockTimer.Start();
        //}

        public void ChanagePageListName(PlayerInfoClass paramCls)
        {
            DataShop.Instance.g_PlayerInfoManager.EditDeviceInfoClass(g_CurrentSelectedPlayerInfoClass, paramCls);
            g_CurrentSelectedPlayerInfoClass.CopyData(paramCls);

            DisplaySelectedPlayerInfoData();
        }

        public void ShowAndHideInfoGrid(bool IsShow)
        {
            //if (IsShow == true)
            //{

            //    ContentsListGrid_Copy3.Visibility = System.Windows.Visibility.Visible;
            //    ContentsListGrid_Copy.Visibility = System.Windows.Visibility.Visible;
            //    ContentsListGrid_Copy1.Visibility = System.Windows.Visibility.Visible;
            //}
            //else
            //{
            //    ContentsListGrid_Copy3.Visibility = System.Windows.Visibility.Hidden;
            //    ContentsListGrid_Copy.Visibility = System.Windows.Visibility.Hidden;
            //    ContentsListGrid_Copy1.Visibility = System.Windows.Visibility.Hidden;
            //}
        }

        public void InitEventHandler()
        {
            this.PreviewMouseLeftButtonDown += UserControl1_PreviewMouseLeftButtonDown;

            this.PreviewMouseMove += new MouseEventHandler(PageListElement_PreviewMouseMove);
            this.MouseLeave += new MouseEventHandler(PageListElement_MouseLeave);

            //BTN0DO_Copy3.Click += BTN0DO_Copy3_Click;   // 플레이어 추가

            //BTN0DO_Copy1.Click += BTN0DO_Copy1_Click;   // 플레이어 접기
            //BTN0DO_Copy2.Click += BTN0DO_Copy2_Click;   // 플레이어 펼치기

            //BTN0DO_Copy12.Click += BTN0DO_Copy12_Click;   // 플레이어 정보수정

            //BTN0DO_Copy13.Click += BTN0DO_Copy13_Click;   // 스케줄 추가 SpecialSchedule
            //BTN0DO_Copy11.Click += BTN0DO_Copy11_Click;    // 스케줄 수정 SpecialSchedule
            BTN0DO_Copy.Click += BTN0DO_Copy_Click;    // PlayerInfo Batch Edit


            PlayerListBox.SelectionChanged += PlayerListBox_SelectionChanged;
            PlayerListBox.PreviewMouseLeftButtonDown += PlayerListBox_PreviewMouseLeftButtonDown;
            PlayerListBox.PreviewMouseWheel += PlayerListBox_PreviewMouseWheel;
            PlayerListScrollViewer.ScrollChanged += PlayerListScrollViewer_ScrollChanged;

            this.Loaded += Page3_Loaded;

            BTN0DO_Copy9.Click += BTN0DO_Copy9_Click;  // 모든 플레이어 처음페이지부터
            BTN0DO_Copy8.Click += BTN0DO_Copy8_Click;  // 모든 플레이어 다음페이지 재생

            AddGroupBtn.Click += AddGroupBtn_Click;
            GroupCommandBtn.Click += GroupCommandBtn_Click;

        }

        void BTN0DO_Copy8_Click(object sender, RoutedEventArgs e)    // 모든 플레이어 다음페이지 재생
        {
            foreach (string playername in MainWindow.Instance.onlineList)
            {
                string ipstr = DataShop.Instance.g_PlayerInfoManager.GetPlayerInfoByName(playername).PIF_IPAddress;
                //RPCaller.RPCall(ipstr, RP_ID.GoNext);
            }
        }

        void BTN0DO_Copy9_Click(object sender, RoutedEventArgs e)   // 모든 플레이어 처음페이지부터
        {
            foreach (string playername in MainWindow.Instance.onlineList)
            {
                string ipstr = DataShop.Instance.g_PlayerInfoManager.GetPlayerInfoByName(playername).PIF_IPAddress;
                //RPCaller.RPCall(ipstr, RP_ID.GoFirst);
            }
        }


        void Page3_Loaded(object sender, RoutedEventArgs e)
        {
            RefreshPlayerGroups();
            ScheduleVisibleRefresh();
        }

        private void PlayerListBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is ScrollViewer)
            {
                PlayerListBox.SelectedItems.Clear();
                g_CurrentSelectedPlayerInfoClass.CleanDataField();
                UpdateSelectionVisuals();
                UpdateGroupButtonState();
            }
        }

        private void PlayerListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (PlayerListBox.SelectedItems.Count > 0)
            {
                var last = PlayerListBox.SelectedItems[PlayerListBox.SelectedItems.Count - 1] as PlayerInfoElement;
                if (last != null)
                {
                    SelectPlayerInfo(last.g_PlayerInfoClass);
                }
            }
            else
            {
                g_CurrentSelectedPlayerInfoClass.CleanDataField();
            }

            UpdateSelectionVisuals();
            UpdateGroupButtonState();
        }

        private void PlayerListBox_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (e.Handled)
            {
                return;
            }

            var viewer = PlayerListScrollViewer;
            if (viewer == null)
            {
                return;
            }

            e.Handled = true;
            var eventArgs = new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta)
            {
                RoutedEvent = UIElement.MouseWheelEvent,
                Source = sender
            };
            viewer.RaiseEvent(eventArgs);
        }

        private void UpdateSelectionVisuals()
        {
            foreach (PlayerInfoElement item in g_PlayerInfoElementList)
            {
                bool isSelected = PlayerListBox.SelectedItems.Contains(item);
                bool isCurrent =
                    g_CurrentSelectedPlayerInfoClass != null &&
                    item.g_PlayerInfoClass != null &&
                    !string.IsNullOrEmpty(item.g_PlayerInfoClass.PIF_PlayerName) &&
                    string.Equals(item.g_PlayerInfoClass.PIF_PlayerName,
                                  g_CurrentSelectedPlayerInfoClass.PIF_PlayerName,
                                  StringComparison.CurrentCulture);

                item.ShowAndHideSelectedBorder(isCurrent);

                // 다중 선택된 모든 항목에 SelectIcon 표시
                item.SelectIcon.Visibility = isSelected ? Visibility.Visible : Visibility.Hidden;
            }
        }

        private void AddToGroupMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (g_CurrentSelectedPlayerGroupClass == null)
            {
                MessageTools.ShowMessageBox("먼저 플레이어를 추가할 그룹을 선택해주세요.");
                return;
            }

            if (PlayerListBox.SelectedItems.Count == 0)
            {
                MessageTools.ShowMessageBox("그룹에 추가할 플레이어를 선택해주세요.");
                return;
            }

            AddSelectedPlayersToGroup();
        }

        private void AddGroupBtn_Click(object sender, RoutedEventArgs e)
        {
            PlayerGroupEditWindow wnd = new PlayerGroupEditWindow();
            if (wnd.ShowDialog() == true)
            {
                DataShop.Instance.g_PlayerGroupManager.AddPlayerGroup(wnd.g_PlayerGroupClass);
                RefreshPlayerGroups();
            }
        }

        public void SelectAllPlayerInGroup()
        {
            if (g_CurrentSelectedPlayerGroupClass == null)
            {
                return;
            }

            PlayerListBox.SelectedItems.Clear();

            foreach (PlayerInfoElement element in PlayerListBox.Items)
            {
                PlayerListBox.SelectedItems.Add(element);
            }

            UpdateSelectionVisuals();
            UpdateGroupButtonState();
        }

        private void GroupCommandBtn_Click(object sender, RoutedEventArgs e)
        {
            GroupCommandWindow _wnd = new GroupCommandWindow();

            if (g_CurrentSelectedPlayerGroupClass != null &&
                g_CurrentSelectedPlayerGroupClass.PG_AssignedPlayerNames != null &&
                g_CurrentSelectedPlayerGroupClass.PG_AssignedPlayerNames.Count > 0)
            {
                _wnd.SetAllowedPlayerNames(g_CurrentSelectedPlayerGroupClass.PG_AssignedPlayerNames, true);
            }
            else
            {
                List<PlayerInfoClass> initiallySelected = new List<PlayerInfoClass>();
                foreach (PlayerInfoElement element in PlayerListBox.SelectedItems)
                {
                    if (element != null && element.g_PlayerInfoClass != null)
                    {
                        initiallySelected.Add(element.g_PlayerInfoClass);
                    }
                }

                _wnd.SetInitialSelectedPlayers(initiallySelected);
            }
            _wnd.ShowDialog();
        }

        public void DeselectPlayerGroup()
        {
            g_CurrentSelectedPlayerGroupClass = null;

            foreach (PlayerGroupButtonElement element in g_PlayerGroupButtonElementList)
            {
                element.ShowSelection(false);
            }

            ShowAllPlayers();
            UpdateGroupButtonState();
        }

        public void RefreshPlayerGroups()
        {
            GroupButtonsPanel.Children.Clear();
            g_PlayerGroupButtonElementList.Clear();

            DataShop.Instance.g_PlayerGroupManager.LoadDataFromDatabase();

            foreach (PlayerGroupClass group in DataShop.Instance.g_PlayerGroupManager.g_PlayerGroupClassList)
            {
                PlayerGroupButtonElement groupButton = new PlayerGroupButtonElement();
                groupButton.UpdateGroupInfo(group);
                groupButton.Margin = new Thickness(0, 0, 0, 5);

                GroupButtonsPanel.Children.Add(groupButton);
                g_PlayerGroupButtonElementList.Add(groupButton);
            }

            if (GroupButtonsPanel.Children.Count == 0)
            {
                Border emptyBorder = new Border();
                emptyBorder.Background = Brushes.Transparent;
                emptyBorder.MouseLeftButtonDown += (s, e) => DeselectPlayerGroup();
                GroupButtonsPanel.Children.Add(emptyBorder);
            }

            if (g_CurrentSelectedPlayerGroupClass != null)
            {
                PlayerGroupClass existingGroup = DataShop.Instance.g_PlayerGroupManager.GetGroupByGUID(g_CurrentSelectedPlayerGroupClass.PG_GUID);
                if (existingGroup != null)
                {
                    SelectPlayerGroup(existingGroup);
                }
                else
                {
                    g_CurrentSelectedPlayerGroupClass = null;
                    UpdateGroupButtonState();
                    ShowAllPlayers();
                }
            }
            else
            {
                UpdateGroupButtonState();
                ShowAllPlayers();
            }
        }

        public void SelectPlayerGroup(PlayerGroupClass paramCls)
        {
            g_CurrentSelectedPlayerGroupClass = paramCls;

            foreach (PlayerGroupButtonElement element in g_PlayerGroupButtonElementList)
            {
                if (element.g_PlayerGroupClass.PG_GUID == paramCls.PG_GUID)
                {
                    element.ShowSelection(true);
                }
                else
                {
                    element.ShowSelection(false);
                }
            }

            FilterPlayersByGroup(paramCls);
            UpdateGroupButtonState();
            UpdateSelectionVisuals();
        }

        private void FilterPlayersByGroup(PlayerGroupClass group)
        {
            if (group == null)
            {
                return;
            }

            PlayerListBox.Items.Clear();
            var players = group.PG_AssignedPlayerNames ?? new List<string>();

            foreach (PlayerInfoElement element in g_PlayerInfoElementList)
            {
                bool isInGroup = players.Contains(element.g_PlayerInfoClass.PIF_PlayerName);
                if (isInGroup)
                {
                    PlayerListBox.Items.Add(element);
                }
            }
        }

        public void ShowAllPlayers()
        {
            PlayerListBox.Items.Clear();

            foreach (PlayerInfoElement element in g_PlayerInfoElementList)
            {
                PlayerListBox.Items.Add(element);
            }

            UpdateSelectionVisuals();
        }

        private void UpdateGroupButtonState()
        {
            // 플레이어 선택 여부와 관계없이 항상 클릭 가능하게 두고,
            // 선택이 없을 때는 GroupCommandBtn_Click에서 안내 메시지를 띄운다.
            GroupCommandBtn.IsEnabled = true;
        }

        public void AddSelectedPlayersToGroup()
        {
            if (g_CurrentSelectedPlayerGroupClass == null || PlayerListBox.SelectedItems.Count == 0)
            {
                return;
            }

            foreach (PlayerInfoElement element in PlayerListBox.SelectedItems)
            {
                g_CurrentSelectedPlayerGroupClass.AddPlayer(element.g_PlayerInfoClass.PIF_PlayerName);
            }

            DataShop.Instance.g_PlayerGroupManager.UpdatePlayerGroup(g_CurrentSelectedPlayerGroupClass, g_CurrentSelectedPlayerGroupClass);
            RefreshPlayerGroups();

            SelectPlayerGroup(g_CurrentSelectedPlayerGroupClass);

            MessageTools.ShowMessageBox(
                string.Format("{0}개의 플레이어가 '{1}' 그룹에 추가되었습니다.",
                PlayerListBox.SelectedItems.Count,
                g_CurrentSelectedPlayerGroupClass.PG_GroupName));
        }
        void BTN0DO_Copy_Click(object sender, RoutedEventArgs e)  // PlayerInfo Batch Edit
        {
            PlayerBatchEditWindow _wnd = new PlayerBatchEditWindow();
            _wnd.InitPlayerInfoList(DataShop.Instance.g_PlayerInfoManager.g_PlayerInfoClassList);
            _wnd.ShowDialog();
        }

        //public void DeletePlayerInfoFromPlayerElement

        void BTN0DO_Copy2_Click(object sender, RoutedEventArgs e)   // 플레이어 펼치기
        {
            g_IsPlayerFolding = false;
            RefreshPlayerInfoList();
        }

        void BTN0DO_Copy1_Click(object sender, RoutedEventArgs e)    // 플레이어 접기
        {
            g_IsPlayerFolding = true;
            RefreshPlayerInfoList();
        }

        public void SelectPlayerInfo(PlayerInfoClass paramCls)
        {
            g_CurrentSelectedPlayerInfoClass.CopyData(paramCls);

            DisplaySelectedPlayerInfoData();
            UpdateSelectionVisuals();
        }


        public void DisplaySelectedPlayerInfoData()
        {
            UpdateSelectedPlayerStatusText();
        }

        public void UpdatePlayListForPlayer()
        {
            UpdatePListComboForPlayer();
        }

        public void UpdatePListComboForPlayer()
        {
            foreach (PlayerInfoElement item in g_PlayerInfoElementList)
            {
                item.RefreshPlayListComboBox();
            }
        }

        public void RefreshPlayerInfoList()
        {
            Is_ComboBoxInit = true;

            PlayerListBox.Items.Clear();
            g_PlayerInfoElementList.Clear();
            GC.Collect();

            var orderedPlayers = DataShop.Instance.g_PlayerInfoManager.GetOrderedPlayers();
            if (orderedPlayers.Any())
            {
                int idx = 1;
                foreach (PlayerInfoClass item in orderedPlayers)
                {
                    PlayerInfoElement tmpElement = new PlayerInfoElement();

                    tmpElement.UpdateDataInfo(item, DataShop.Instance.g_PageListInfoManager.g_PageListInfoClassList);

                    if (this.g_IsPlayerFolding == true)
                    {
                        tmpElement.Height = 40;
                        tmpElement.IsShowMiniControlfSet(true);
                    }
                    else
                    {
                        tmpElement.Height = 241;
                        tmpElement.IsShowMiniControlfSet(false);
                    }

                    tmpElement.TextBlockOrderingNumber.Text = string.Format("{0:D2}", idx);
                    idx++;

                    g_PlayerInfoElementList.Add(tmpElement);

                    if (g_CurrentSelectedPlayerGroupClass == null ||
                        g_CurrentSelectedPlayerGroupClass.PG_AssignedPlayerNames.Contains(item.PIF_PlayerName))
                    {
                        PlayerListBox.Items.Add(tmpElement);
                    }
                }
            }

            RefreshPlayerElementLookup();

            ScheduleVisibleRefresh();

            Is_ComboBoxInit = false;
            UpdateSelectionVisuals();
            UpdateGroupButtonState();
        }

        private void UpdateSelectedPlayerStatusText()
        {
            string playerName = g_CurrentSelectedPlayerInfoClass?.PIF_PlayerName;
            string status = GetDisplayedStatusText(playerName);
            if (string.IsNullOrWhiteSpace(status))
            {
                status = "-";
            }
        }

        private string GetDisplayedStatusText(string playerName)
        {
            if (string.IsNullOrWhiteSpace(playerName))
            {
                return string.Empty;
            }

            var element = FindPlayerElement(playerName);
            return element?.GetCurrentStatusText() ?? string.Empty;
        }

        private bool IsSelectedPlayer(string playerName)
        {
            if (string.IsNullOrWhiteSpace(playerName))
            {
                return false;
            }

            return string.Equals(g_CurrentSelectedPlayerInfoClass?.PIF_PlayerName,
                playerName,
                StringComparison.CurrentCultureIgnoreCase);
        }

        void UserControl1_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            //this.g_ParentWnd.SelectInputChannelData(this.g_InputChannelInfoClass);
            //throw new NotImplementedException();
        }

        public void UpdateDataInfo(int idx)
        {
            g_Idx = idx;
            DisplayDataInfo();
        }

        public void DisplayDataInfo()
        {
            //TextBlockOrderingNumber.Text = g_Idx.ToString();

        }

        void ExitTextBlock_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            //if (MessageTools.ShowMessageBox(string.Format("선택한 <{0}>를(을) 삭제하시겠습니까?", TextBlockPageName_Copy3.Text)) == true)
            //{
            //    //this.parentPage.DeleteStartStationNametByName(TextBlockPageName.Text);
            //}
        }

        //#FF212121
        void ExitTextBlock_MouseLeave(object sender, MouseEventArgs e)
        {
            Color c2 = (Color)TypeDescriptor.GetConverter(typeof(Color)).ConvertFromString("#FF808080");
            //ExitTextBlock.Foreground = new SolidColorBrush(c2);
            //ExitTextBlock.Foreground = new SolidColorBrush(Colors.Black);
        }

        void ExitTextBlock_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            //ExitTextBlock.Foreground = new SolidColorBrush(Colors.Gray);
        }

        void PageListElement_MouseLeave(object sender, MouseEventArgs e)
        {
            if (g_IsSelected == false)
            {
                //Color c2 = (Color)TypeDescriptor.GetConverter(typeof(Color)).ConvertFromString("#FF212121");
                //BackRectangle.Fill = new SolidColorBrush(c2);
                //BackRectangle.Fill = new SolidColorBrush(Colors.Black);
            }
        }

        void PageListElement_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (g_IsSelected == false)
            {
                //BackRectangle.Fill = new SolidColorBrush(Colors.Gray);
            }

            if (e.LeftButton == MouseButtonState.Pressed)
            {
                //object data = this.g_InputChannelInfoClass.ICF_ChannelName;
                //DragDrop.DoDragDrop(this, data, DragDropEffects.Copy);

               // DragDrop.DoDragDrop(this, (string)this.g_InputChannelInfoClass.ICF_ChannelName, DragDropEffects.Copy);
            }
        }

        private void RefreshPlayerElementLookup()
        {
            _playerElementById = new Dictionary<string, PlayerInfoElement>(StringComparer.OrdinalIgnoreCase);
            _playerElementByName = new Dictionary<string, PlayerInfoElement>(StringComparer.OrdinalIgnoreCase);

            foreach (PlayerInfoElement element in g_PlayerInfoElementList)
            {
                if (element == null || element.g_PlayerInfoClass == null)
                {
                    continue;
                }

                string guid = element.g_PlayerInfoClass.PIF_GUID;
                if (!string.IsNullOrWhiteSpace(guid))
                {
                    _playerElementById[guid] = element;
                }

                string name = element.g_PlayerInfoClass.PIF_PlayerName;
                if (!string.IsNullOrWhiteSpace(name))
                {
                    _playerElementByName[name] = element;
                }
            }
        }

        private PlayerInfoElement FindPlayerElement(string playerIdOrName)
        {
            if (string.IsNullOrWhiteSpace(playerIdOrName))
            {
                return null;
            }

            if (_playerElementById != null && _playerElementById.TryGetValue(playerIdOrName, out var byId))
            {
                return byId;
            }

            if (_playerElementByName != null && _playerElementByName.TryGetValue(playerIdOrName, out var byName))
            {
                return byName;
            }

            var element = g_PlayerInfoElementList
                .FirstOrDefault(item =>
                    !string.IsNullOrWhiteSpace(item.g_PlayerInfoClass.PIF_GUID) &&
                    item.g_PlayerInfoClass.PIF_GUID.Equals(playerIdOrName, StringComparison.OrdinalIgnoreCase));

            if (element != null)
            {
                return element;
            }

            return g_PlayerInfoElementList
                .FirstOrDefault(item =>
                    item.g_PlayerInfoClass.PIF_PlayerName.Equals(playerIdOrName, StringComparison.CurrentCultureIgnoreCase));
        }

        public void SetPlayerNetworkStatus(string playerIdOrName, PlayerStatus status, int process=0, string version="", string pagename="", string hdmi_state="", bool? isConnected = null)
        {
            var element = FindPlayerElement(playerIdOrName);
            if (element != null)
            {
                element.DisplayPlayerStatus(status, process, version, pagename, hdmi_state, isConnected);
            }
        }

        private void OnHeartbeatsChanged(object sender, IReadOnlyList<PlayerHeartbeatState> states)
        {
            if (states == null || states.Count == 0)
            {
                return;
            }

            lock (_heartbeatStateLock)
            {
                foreach (var state in states)
                {
                    if (state == null || string.IsNullOrWhiteSpace(state.ClientId))
                    {
                        continue;
                    }

                    _pendingHeartbeatStates[state.ClientId] = state;
                }

                if (_heartbeatUiUpdateScheduled)
                {
                    return;
                }

                _heartbeatUiUpdateScheduled = true;
            }

            Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(StartHeartbeatUiTimer));
        }

        private void OnSignalRHeartbeatReceived(object sender, SignalRHeartbeatEventArgs e)
        {
            if (e == null || e.Payload == null)
            {
                return;
            }

            _heartbeatMonitor.UpdateFromSignalR(e.Payload);
        }

        private void FlushHeartbeatUiUpdates()
        {
            List<PlayerHeartbeatState> batch;
            lock (_heartbeatStateLock)
            {
                if (_pendingHeartbeatStates.Count == 0)
                {
                    return;
                }

                batch = _pendingHeartbeatStates.Values.ToList();
                _pendingHeartbeatStates.Clear();
            }

            ApplyHeartbeatStates(batch);
        }

        private void StartHeartbeatUiTimer()
        {
            if (!_heartbeatUiTimer.IsEnabled)
            {
                _heartbeatUiTimer.Start();
            }
        }

        private void HeartbeatUiTimer_Tick(object sender, EventArgs e)
        {
            FlushHeartbeatUiUpdates();

            lock (_heartbeatStateLock)
            {
                if (_pendingHeartbeatStates.Count == 0)
                {
                    _heartbeatUiTimer.Stop();
                    _heartbeatUiUpdateScheduled = false;
                }
            }
        }

        private void PlayerListScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (e.HorizontalChange == 0 && e.VerticalChange == 0 &&
                e.ViewportWidthChange == 0 && e.ViewportHeightChange == 0 &&
                e.ExtentWidthChange == 0 && e.ExtentHeightChange == 0)
            {
                return;
            }

            ScheduleVisibleRefresh();
        }

        private void ScheduleVisibleRefresh()
        {
            if (_visibleRefreshScheduled)
            {
                return;
            }

            _visibleRefreshScheduled = true;
            Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
            {
                _visibleRefreshScheduled = false;
                UpdateVisiblePlayerElements();

                var snapshot = _heartbeatMonitor?.GetCurrentStatesSnapshot();
                if (snapshot == null || snapshot.Count == 0)
                {
                    return;
                }

                ApplyHeartbeatStates(snapshot);
            }));
        }

        private void UpdateVisiblePlayerElements()
        {
            _visiblePlayerElements = GetVisiblePlayerElements() ?? new HashSet<PlayerInfoElement>();
        }

        private HashSet<PlayerInfoElement> GetVisiblePlayerElements()
        {
            var visible = new HashSet<PlayerInfoElement>();
            if (PlayerListBox == null || PlayerListScrollViewer == null)
            {
                return visible;
            }

            double viewportWidth = PlayerListScrollViewer.ViewportWidth;
            double viewportHeight = PlayerListScrollViewer.ViewportHeight;
            if (viewportWidth <= 0 || viewportHeight <= 0)
            {
                return null;
            }

            Rect viewport = new Rect(0, 0, viewportWidth, viewportHeight);
            foreach (object item in PlayerListBox.Items)
            {
                var element = item as PlayerInfoElement;
                if (element == null || !element.IsVisible)
                {
                    continue;
                }

                try
                {
                    Rect bounds = element.TransformToAncestor(PlayerListScrollViewer)
                        .TransformBounds(new Rect(new Point(0, 0), element.RenderSize));
                    if (bounds.IntersectsWith(viewport))
                    {
                        visible.Add(element);
                    }
                }
                catch (InvalidOperationException)
                {
                }
            }

            return visible;
        }

        private void ApplyHeartbeatStates(IEnumerable<PlayerHeartbeatState> updatedStates)
        {
            if (g_PlayerInfoElementList.Count == 0)
            {
                return;
            }

            if (_visiblePlayerElements == null || _visiblePlayerElements.Count == 0)
            {
                return;
            }
            foreach (var state in updatedStates)
            {
                var element = FindPlayerElement(state.ClientId);
                if (element != null)
                {
                    if (!_visiblePlayerElements.Contains(element))
                    {
                        continue;
                    }

                    bool? isConnected = state.LastHeartbeat.HasValue;
                    element.ApplyPlayerStatus(state.Status, state.Process, state.Version, state.CurrentPageName, state.HdmiState, isConnected);
                    if (IsSelectedPlayer(element.g_PlayerInfoClass?.PIF_PlayerName))
                    {
                        UpdateSelectedPlayerStatusText();
                    }
                }
            }
        }

        private void OnPageUnloaded(object sender, RoutedEventArgs e)
        {
            if (_heartbeatDisposed)
            {
                return;
            }

            //_heartbeatDisposed = true;
            //_heartbeatMonitor.HeartbeatsChanged -= OnHeartbeatsChanged;
            //SignalRServerTools.HeartbeatReceived -= OnSignalRHeartbeatReceived;
            //_heartbeatMonitor.Dispose();
        }

        //public void SetPlayerStatusByIP(string targetIP, string paramStatus)
        //{
        //    foreach (PlayerInfoElement item in g_PlayerInfoElementList)
        //    {
        //        if (item.g_PlayerInfoClass.PIF_IPAddress == targetIP)
        //        {
        //            item.DisplayPlayerStatus(paramStatus);
        //            break;
        //        }
        //    }
        //}

        public void SetPlayerMacAddr(string playerName, string macAddr)
        {
            string normalized = AuthTools.NormalizeMacAddress(macAddr);
            foreach (PlayerInfoElement item in g_PlayerInfoElementList)
            { 
                if (item.g_PlayerInfoClass.PIF_PlayerName == playerName)
                {
                    item.g_PlayerInfoClass.PIF_MacAddress = normalized;
                    break;
                }
            }
        }


        public void SetLauncherState(string playerName, bool isEnable)
        {
            foreach (PlayerInfoElement item in g_PlayerInfoElementList)
            {
                if (item.g_PlayerInfoClass.PIF_PlayerName == playerName)
                {
                    item.LauncherEnabled = isEnable;
                    break;
                }
            }
        }

        private void EditPListBtn_Click(object sender, RoutedEventArgs e)
        {
            MainWindow.Instance.GotoPageByName("Page2");
        }

        private void BatchUpdateBtn_Click(object sender, RoutedEventArgs e)
        {
            PlayListBatchUpdateWindow _wnd = new PlayListBatchUpdateWindow();
            _wnd.ShowDialog();
        }

        private void BatchUpgradeBtn_Click(object sender, RoutedEventArgs e)
        {
            BatchUpgradeWindow _wnd = new BatchUpgradeWindow();
            _wnd.ShowDialog();
        }

        private void UpdateThrottleSettingsBtn_Click(object sender, RoutedEventArgs e)
        {
            UpdateThrottleSettingsWindow _wnd = new UpdateThrottleSettingsWindow();
            _wnd.ShowDialog();
        }

        PlayerInfoElement leadCtrl;
        private void PlayerListBox_LayoutUpdated(object sender, EventArgs e)
        {
            if (leadCtrl != null)
            {
                Point pt = leadCtrl.TranslatePoint(new Point(0, 0), PlayerListBox);
                if (Point.Equals(org_pt, pt) == false)
                {
                    List<PlayerInfoClass> datalist = new List<PlayerInfoClass>();

                    int idx = 1;
                    foreach (PlayerInfoElement pie in PlayerListBox.Items)
                    {
                        pie.TextBlockOrderingNumber.Text = string.Format("{0:D2}", idx);
                        pie.g_PlayerInfoClass.PIF_Order = idx;
                        idx++;

                        PlayerInfoClass orderedInfo = new PlayerInfoClass();
                        orderedInfo.CopyData(pie.g_PlayerInfoClass);
                        datalist.Add(orderedInfo);
                    }
                    org_pt = pt;

                    DataShop.Instance.g_PlayerInfoManager.UpdatePlayerInfoList(datalist);
                }
                leadCtrl = null;
            }
        }

        Point org_pt;
        private void PlayerListBox_Drop(object sender, DragEventArgs e)
        {
            leadCtrl = PlayerListBox.SelectedItem as PlayerInfoElement;

            if (leadCtrl != null)
                org_pt = leadCtrl.TranslatePoint(new Point(0, 0), PlayerListBox);
        }

        //internal void SetDownloading(string playername, bool state)
        //{
        //    foreach (PlayerInfoElement pie in g_PlayerInfoElementList)
        //    {
        //        if (pie.g_PlayerInfoClass.PIF_PlayerName.Equals(playername))
        //        {
        //            this.Dispatcher.Invoke(DispatcherPriority.Normal, (Action)(() =>
        //            {
        //                if (state)
        //                {
        //                    pie.CurrentPlayingPageName.Text = "Downloading";
        //                } else {
        //                    pie.CurrentPlayingPageName.Text = "";
        //                }
        //            }));
        //            break;
        //        }
        //    }

        //    g_isUpdating = state;
        //}
    }
}
