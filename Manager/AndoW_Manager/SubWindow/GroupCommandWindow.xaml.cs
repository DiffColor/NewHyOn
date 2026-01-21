using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using TurtleTools;
using Key = System.Windows.Input.Key;

namespace AndoW_Manager
{
    public partial class GroupCommandWindow : Window
    {
        private const string CommandChangePlaylist = "플레이리스트 변경";
        private const string CommandSyncPlaylist = "싱크 플레이리스트 변경";
        private const string CommandSyncAdjust = "싱크 조정";
        private const string CommandReboot = "플레이어 재부팅";
        private const string CommandPowerOff = "플레이어 전원 끄기";
        private const string CommandCheckUpdate = "업데이트 확인";

        public static GroupCommandWindow Instance { get; private set; }

        private readonly List<PlayerInfoClass> _selectedPlayers = new List<PlayerInfoClass>();
        private readonly HashSet<string> _initialSelectedPlayerNames = new HashSet<string>();
        private readonly HashSet<string> _allowedPlayerNames = new HashSet<string>(StringComparer.CurrentCultureIgnoreCase);
        private bool _usePerPlayerPlaylist = false;

        public GroupCommandWindow()
        {
            InitializeComponent();
            Instance = this;
            InitWindowChrome();
            InitEventHandler();
            InitCommandOptions();
        }
        
        private void InitEventHandler()
        {
            ExecuteBtn.Click += ExecuteBtn_Click;
            CloseBtn.Click += CloseBtn_Click;
            Loaded += GroupCommandWindow_Loaded;
            Closed += GroupCommandWindow_Closed;
            Closing += GroupCommandWindow_Closing;
            PreviewKeyDown += GroupCommandWindow_PreviewKeyDown;
        }

        private void GroupCommandWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            MainWindow.Instance?.SetDimOverlay(false);
        }

        private void GroupCommandWindow_Closed(object sender, EventArgs e)
        {
            Instance = null;
        }

        private void CloseBtn_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void GroupCommandWindow_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                Close();
            }
            else if (e.Key == Key.Enter)
            {
                ExecuteBtn_Click(this, null);
            }
        }
        
        private void GroupCommandWindow_Loaded(object sender, RoutedEventArgs e)
        {
            BuildPlayerList();
            MainWindow.Instance?.SetDimOverlay(true);
        }
        
        private void InitCommandOptions()
        {
            CommandComboBox.Items.Clear();

            CommandComboBox.Items.Add(CommandChangePlaylist);
            CommandComboBox.Items.Add(CommandSyncPlaylist);
            CommandComboBox.Items.Add(CommandSyncAdjust);
            CommandComboBox.Items.Add(CommandReboot);
            CommandComboBox.Items.Add(CommandPowerOff);
            CommandComboBox.Items.Add(CommandCheckUpdate);

            CommandComboBox.SelectedIndex = 0;
            
            UpdateCommandOptions();
        }
        
        private void CommandComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateCommandOptions();
        }
        
        private void UpdateCommandOptions()
        {
            OptionsPanel.Children.Clear();
            OptionsPanel.Visibility = Visibility.Collapsed;

            string selectedCommand = CommandComboBox.SelectedItem == null ? string.Empty : CommandComboBox.SelectedItem.ToString();
            bool isChangePlaylist = selectedCommand == CommandChangePlaylist;
            bool isSyncPlaylist = selectedCommand == CommandSyncPlaylist;
            _usePerPlayerPlaylist = isSyncPlaylist;

            if (isChangePlaylist || isSyncPlaylist)
            {
                PlaylistComboBox.Items.Clear();
                foreach (PageListInfoClass pageList in DataShop.Instance.g_PageListInfoManager.g_PageListInfoClassList)
                {
                    if (pageList != null && !string.IsNullOrWhiteSpace(pageList.PLI_PageListName))
                    {
                        PlaylistComboBox.Items.Add(pageList.PLI_PageListName);
                    }
                }

                if (PlaylistComboBox.Items.Count > 0)
                {
                    if (PlaylistComboBox.SelectedIndex < 0)
                    {
                        PlaylistComboBox.SelectedIndex = 0;
                    }
                }
                else
                {
                    PlaylistComboBox.SelectedIndex = -1;
                }

                PlaylistComboBox.IsEnabled = isChangePlaylist;
                OptionsPanel.Children.Add(PlaylistComboBox);
                OptionsPanel.Visibility = Visibility.Visible;
            }

            UpdatePlayerPlaylistVisibility(_usePerPlayerPlaylist);
        }

        private List<string> GetPlaylistNames()
        {
            List<string> names = new List<string>();

            foreach (PageListInfoClass pageList in DataShop.Instance.g_PageListInfoManager.g_PageListInfoClassList)
            {
                if (pageList != null && !string.IsNullOrWhiteSpace(pageList.PLI_PageListName))
                {
                    names.Add(pageList.PLI_PageListName);
                }
            }

            return names;
        }

        private void UpdatePlayerPlaylistVisibility(bool isEnabled)
        {
            foreach (UIElement child in SelectedPlayersListBox.Children)
            {
                if (child is PlayerElementForGroupCommand element)
                {
                    element.SetPlaylistSelectionEnabled(isEnabled);
                }
            }
        }

        private void BuildPlayerList()
        {
            _selectedPlayers.Clear();
            SelectedPlayersListBox.Children.Clear();

            int orderNumber = 1;
            bool hasAllowedPlayers = _allowedPlayerNames.Count > 0;
            List<string> playlistNames = GetPlaylistNames();

            foreach (PlayerInfoClass player in DataShop.Instance.g_PlayerInfoManager.g_PlayerInfoClassList)
            {
                if (player == null || string.IsNullOrWhiteSpace(player.PIF_PlayerName))
                {
                    continue;
                }

                if (hasAllowedPlayers && _allowedPlayerNames.Contains(player.PIF_PlayerName) == false)
                {
                    continue;
                }

                PlayerElementForGroupCommand element = new PlayerElementForGroupCommand();
                element.UpdateDataInfo(player);
                element.SetOrderingNumber(orderNumber);
                orderNumber++;
                element.UpdatePlaylistOptions(playlistNames, player.PIF_CurrentPlayList);
                element.SetPlaylistSelectionEnabled(_usePerPlayerPlaylist);

                bool isInitiallySelected = _initialSelectedPlayerNames.Contains(player.PIF_PlayerName);
                element.SelectThisElement(isInitiallySelected);

                if (isInitiallySelected)
                {
                    _selectedPlayers.Add(player);
                }

                SelectedPlayersListBox.Children.Add(element);
            }

            UpdateSelectedCountText();
        }

        public void SetInitialSelectedPlayers(IEnumerable<PlayerInfoClass> selectedPlayers)
        {
            _initialSelectedPlayerNames.Clear();

            if (selectedPlayers == null)
            {
                return;
            }

            foreach (PlayerInfoClass player in selectedPlayers)
            {
                if (player != null && !string.IsNullOrWhiteSpace(player.PIF_PlayerName))
                {
                    _initialSelectedPlayerNames.Add(player.PIF_PlayerName);
                }
            }
        }

        public void SetAllowedPlayerNames(IEnumerable<string> playerNames, bool selectAll)
        {
            _allowedPlayerNames.Clear();

            if (playerNames == null)
            {
                return;
            }

            foreach (string playerName in playerNames)
            {
                if (string.IsNullOrWhiteSpace(playerName) == false)
                {
                    _allowedPlayerNames.Add(playerName);
                }
            }

            if (selectAll)
            {
                _initialSelectedPlayerNames.Clear();
                foreach (string playerName in _allowedPlayerNames)
                {
                    _initialSelectedPlayerNames.Add(playerName);
                }
            }
        }

        public void RefreshSelectedPlayersList()
        {
            _selectedPlayers.Clear();

            foreach (UIElement child in SelectedPlayersListBox.Children)
            {
                if (child is PlayerElementForGroupCommand element &&
                    element.SelectedCheckBox.IsChecked == true &&
                    element.g_PlayerInfoClass != null &&
                    !string.IsNullOrWhiteSpace(element.g_PlayerInfoClass.PIF_PlayerName))
                {
                    _selectedPlayers.Add(element.g_PlayerInfoClass);
                }
            }

            UpdateSelectedCountText();
        }

        private void UpdateSelectedCountText()
        {
            SelectedCountText.Text = "선택된 플레이어 수: " + _selectedPlayers.Count;
        }
        
        private void ExecuteBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedPlayers.Count == 0)
            {
                MessageTools.ShowMessageBox("선택된 플레이어가 없습니다.", "확인");
                return;
            }
            
            string command = CommandComboBox.SelectedItem?.ToString() ?? string.Empty;
            string confirmMessage = string.Format("선택된 플레이어 {0}대에 '{1}' 명령을 실행하시겠습니까?",
                _selectedPlayers.Count, command);
                
            if (MessageTools.ShowMessageBox(confirmMessage, "확인", "예") == true)
            {
                ExecuteCommand();
            }
        }
        
        private void ExecuteCommand()
        {
            string selected = CommandComboBox.SelectedItem?.ToString();
            if (string.IsNullOrWhiteSpace(selected))
            {
                return;
            }

            if (selected == CommandChangePlaylist)
            {
                if (PlaylistComboBox.SelectedItem != null)
                {
                    ChangePlaylist(PlaylistComboBox.SelectedItem.ToString());
                }
            }
            else if (selected == CommandSyncPlaylist)
            {
                ChangePlaylistPerPlayer();
            }
            else if (selected == CommandSyncAdjust)
            {
                SyncAdjustPlayers();
            }
            else if (selected == CommandReboot)
            {
                RebootPlayers();
            }
            else if (selected == CommandPowerOff)
            {
                PowerOffPlayers();
            }
            else if (selected == CommandCheckUpdate)
            {
                CheckForUpdate();
            }
            
            Close();
        }
        
        private void ChangePlaylist(string playlistName)
        {
            int successCount = 0;

            DataShop.Instance.g_PageInfoManager.LoadPagesForList(playlistName);
            if (DataShop.Instance.g_PageInfoManager.g_PageInfoClassList.Count == 0)
            {
                MessageTools.ShowMessageBox("선택한 플레이리스트에 등록된 페이지가 없습니다.", "확인");
                return;
            }
            
            foreach (PlayerInfoClass player in _selectedPlayers)
            {
                try
                {
                    player.PIF_CurrentPlayList = playlistName;
                    DataShop.Instance.g_PlayerInfoManager.EditPlayerCurrentPlayList(player);

                    PlayerInfoElement element =
                        Page3.Instance.g_PlayerInfoElementList
                            .FirstOrDefault(e =>
                                e.g_PlayerInfoClass != null &&
                                e.g_PlayerInfoClass.PIF_PlayerName == player.PIF_PlayerName);

                    if (element != null)
                    {
                        element.PlaylistCombo.SelectedItem = playlistName;
                        Page3.Instance.ChanagePageListName(player);
                    }

                    string payloadBase64 = DataShop.Instance.g_UpdatePayloadBuilder.BuildPayloadBase64(player);
                    MainWindow.Instance.EnqueueCommandForPlayer(player, RP_ORDER.updatelist.ToString(), payloadBase64, pushSignalR: true);
                    successCount++;
                }
                catch (Exception ex)
                {
                    Logger.WriteErrorLog(ex.ToString(), Logger.GetLogFileName());
                }
            }
            
            MessageTools.ShowMessageBox(
                string.Format("{0}대 플레이어의 플레이리스트를 '{1}'로 변경했습니다.", successCount, playlistName),
                "확인");
        }

        private void ChangePlaylistPerPlayer()
        {
            int successCount = 0;
            int emptyPlaylistCount = 0;
            int missingSelectionCount = 0;
            Dictionary<string, bool> playlistHasPages = new Dictionary<string, bool>(StringComparer.CurrentCultureIgnoreCase);

            foreach (UIElement child in SelectedPlayersListBox.Children)
            {
                PlayerElementForGroupCommand element = child as PlayerElementForGroupCommand;
                if (element == null)
                {
                    continue;
                }

                if (element.SelectedCheckBox.IsChecked != true)
                {
                    continue;
                }

                PlayerInfoClass player = element.g_PlayerInfoClass;
                if (player == null || string.IsNullOrWhiteSpace(player.PIF_PlayerName))
                {
                    continue;
                }

                string playlistName = element.GetSelectedPlaylistName();
                if (string.IsNullOrWhiteSpace(playlistName))
                {
                    missingSelectionCount++;
                    continue;
                }

                bool hasPages;
                if (!playlistHasPages.TryGetValue(playlistName, out hasPages))
                {
                    DataShop.Instance.g_PageInfoManager.LoadPagesForList(playlistName);
                    hasPages = DataShop.Instance.g_PageInfoManager.g_PageInfoClassList.Count > 0;
                    playlistHasPages[playlistName] = hasPages;
                }

                if (!hasPages)
                {
                    emptyPlaylistCount++;
                    continue;
                }

                try
                {
                    player.PIF_CurrentPlayList = playlistName;
                    DataShop.Instance.g_PlayerInfoManager.EditPlayerCurrentPlayList(player);

                    if (Page3.Instance != null)
                    {
                        PlayerInfoElement playerElement =
                            Page3.Instance.g_PlayerInfoElementList
                                .FirstOrDefault(e =>
                                    e.g_PlayerInfoClass != null &&
                                    e.g_PlayerInfoClass.PIF_PlayerName == player.PIF_PlayerName);

                        if (playerElement != null)
                        {
                            playerElement.PlaylistCombo.SelectedItem = playlistName;
                            Page3.Instance.ChanagePageListName(player);
                        }
                    }

                    string payloadBase64 = DataShop.Instance.g_UpdatePayloadBuilder.BuildPayloadBase64(player);
                    MainWindow.Instance.EnqueueCommandForPlayer(player, RP_ORDER.updatelist.ToString(), payloadBase64, pushSignalR: true);
                    successCount++;
                }
                catch (Exception ex)
                {
                    Logger.WriteErrorLog(ex.ToString(), Logger.GetLogFileName());
                }
            }

            string message = string.Format("{0}대 플레이어에 플레이리스트 변경 명령을 보냈습니다.", successCount);
            if (missingSelectionCount > 0 || emptyPlaylistCount > 0)
            {
                message = string.Format("{0}{1}선택되지 않은 항목 {2}건, 비어있는 플레이리스트 {3}건은 제외했습니다.",
                    message, Environment.NewLine, missingSelectionCount, emptyPlaylistCount);
            }

            MessageTools.ShowMessageBox(message, "확인");
        }
        
        private void RebootPlayers()
        {
            int successCount = 0;
            
            foreach (PlayerInfoClass player in _selectedPlayers)
            {
                try
                {
                    MainWindow.Instance.EnqueueCommandForPlayer(player, RP_ORDER.reboot.ToString(), pushSignalR: true);
                    successCount++;
                }
                catch (Exception ex)
                {
                    Logger.WriteErrorLog(ex.ToString(), Logger.GetLogFileName());
                }
            }
            
            MessageTools.ShowMessageBox(
                string.Format("{0}대 플레이어에 재부팅 명령을 보냈습니다.", successCount),
                "확인");
        }
        
        private void PowerOffPlayers()
        {
            int successCount = 0;
            
            foreach (PlayerInfoClass player in _selectedPlayers)
            {
                try
                {
                    MainWindow.Instance.EnqueueCommandForPlayer(player, RP_ORDER.poweroff.ToString(), pushSignalR: true);
                    successCount++;
                }
                catch (Exception ex)
                {
                    Logger.WriteErrorLog(ex.ToString(), Logger.GetLogFileName());
                }
            }
            
            MessageTools.ShowMessageBox(
                string.Format("{0}대 플레이어에 전원 끄기 명령을 보냈습니다.", successCount),
                "확인");
        }
        
        private void CheckForUpdate()
        {
            int successCount = 0;
            
            foreach (PlayerInfoClass player in _selectedPlayers)
            {
                try
                {
                    MainWindow.Instance.EnqueueCommandForPlayer(player, RP_ORDER.check.ToString(), pushSignalR: true);
                    successCount++;
                }
                catch (Exception ex)
                {
                    Logger.WriteErrorLog(ex.ToString(), Logger.GetLogFileName());
                }
            }
            
            MessageTools.ShowMessageBox(
                string.Format("{0}대 플레이어에 업데이트 확인 명령을 보냈습니다.", successCount),
                "확인");
        }

        private void SyncAdjustPlayers()
        {
            int successCount = 0;

            foreach (PlayerInfoClass player in _selectedPlayers)
            {
                try
                {
                    MainWindow.Instance.EnqueueCommandForPlayer(player, RP_ORDER.sync.ToString(), pushSignalR: true);
                    successCount++;
                }
                catch (Exception ex)
                {
                    Logger.WriteErrorLog(ex.ToString(), Logger.GetLogFileName());
                }
            }

            MessageTools.ShowMessageBox(
                string.Format("{0}대 플레이어에 싱크 조정 명령을 보냈습니다.", successCount),
                "확인");
        }

        private void PlaylistComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
        }

        #region Window Chrome
        private void InitWindowChrome()
        {
            StateChanged += GroupCommandWindow_StateChanged;

            if (minBTN_Copy != null)
            {
                minBTN_Copy.Click += MinBtnCopy_Click;
            }

            if (minBTN != null)
            {
                minBTN.Click += MinBtn_Click;
            }

            if (ExitBTN != null)
            {
                ExitBTN.Click += ExitBTN_Click;
            }

            UpdateMaximizeButtonIcon();
        }

        private void DragRect_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            try
            {
                if (e.LeftButton == MouseButtonState.Pressed)
                {
                    DragMove();
                }
            }
            catch
            {
            }
        }

        private void MinBtnCopy_Click(object sender, RoutedEventArgs e)
        {
            if (WindowState == WindowState.Maximized)
            {
                WindowState = WindowState.Normal;
            }
            else
            {
                WindowState = WindowState.Maximized;
            }

            UpdateMaximizeButtonIcon();
        }

        private void MinBtn_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void ExitBTN_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void GroupCommandWindow_StateChanged(object sender, EventArgs e)
        {
            UpdateMaximizeButtonIcon();
        }

        private void UpdateMaximizeButtonIcon()
        {
            if (MaximizedIcon == null || WindowIcon == null)
            {
                return;
            }

            if (WindowState == WindowState.Maximized)
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
        #endregion
    }
}
