using System.Collections.Generic;
using System.Windows;
using System;
using System.IO;
using System.Data;
using System.Windows.Threading;
using TurtleTools;
using System.Windows.Controls;
using System.Windows.Input;
using System.Linq;

namespace AndoW_Manager
{
    public partial class BatchUpgradeWindow : Window
    {      
        public static BatchUpgradeWindow Instance { get; set; }

        public BatchUpgradeWindow()
        {
            InitializeComponent();
            Instance = this;
            InitEventHandler();
        }


        public void InitEventHandler()
        {           
            MaximizeBTN.Click += MaximizeBTN_Click;

            this.Loaded += ShowPlayerLogWindow_Loaded;

            BTN0DO_Copy2.Click += BTN0DO_Copy2_Click;  // 업그레이드 시작

            BTN0DO_Copy1.Click += BTN0DO_Copy1_Click;   // 플레이어 모두선택
            BTN0DO_Copy3.Click += BTN0DO_Copy3_Click;   // 플레이어 모두해제

            KeyDown += BatchUpgradeWindow_KeyDown;

            this.Closing += BatchUpgradeWindow_Closing;
        }

        bool altF4Pressed = false;
        void BatchUpgradeWindow_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (Keyboard.Modifiers == ModifierKeys.Alt && e.SystemKey == System.Windows.Input.Key.F4)
            {
                altF4Pressed = true;
            }
        }

        void BatchUpgradeWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            MainWindow.Instance?.SetDimOverlay(false);

            if (altF4Pressed)
            {
                e.Cancel = true;
                altF4Pressed = false; 
                Hide();
                return;
            }

            Instance = null;
        }

        void BTN0DO_Copy3_Click(object sender, RoutedEventArgs e)   // 플레이어 모두해제
        {
            ReleaseAllSelectedPlayer();
        }


        public void RefreshCheckedPlayerList()
        {
            g_SelectedPlayerInfoList.Clear();
            g_SelectedPlayerNameList.Clear();
            playerNameListStr = string.Empty;

            foreach (PlayerElemetForBatchUpgrade item in PlayerWrapPanel.Children)
            {
                if (item.SelectedCheckBox.IsChecked == true)
                {
                    g_SelectedPlayerInfoList.Add(item.g_PlayerInfoClass);
                    g_SelectedPlayerNameList.Add(item.g_PlayerInfoClass.PIF_PlayerName);
                    if (playerNameListStr == string.Empty)
                    {
                        playerNameListStr = playerNameListStr + item.g_PlayerInfoClass.PIF_PlayerName;
                    }
                    else
                    {
                        playerNameListStr = playerNameListStr + ", " + item.g_PlayerInfoClass.PIF_PlayerName;
                    }

                }
            }
        }


        public void ReleaseAllSelectedPlayer()
        {
            foreach (PlayerElemetForBatchUpgrade item in PlayerWrapPanel.Children)
            {
                item.SelectThisElement(false);
            }

            RefreshCheckedPlayerList();
        }

        void BTN0DO_Copy1_Click(object sender, RoutedEventArgs e)   // 플레이어 모두선택
        {
            foreach (PlayerElemetForBatchUpgrade item in PlayerWrapPanel.Children)
            {
                item.SelectThisElement(true);
            }

            RefreshCheckedPlayerList();
        }

        public bool IsStopUpdate = false;

        void BTN0DO_Copy2_Click(object sender, RoutedEventArgs e)   //업그레이드 시작
        {
            if (!MainWindow.Instance.CheckFTPServerAlive())
            {
                MessageTools.ShowMessageBox("파일 전송 서비스가 실행되지 않았습니다.\r\n파일 전송 서비스를 확인해주세요.", "확인");
                return;
            }

            if (string.IsNullOrEmpty(APKFilePathTBox.Text) || File.Exists(APKFilePathTBox.Text) == false)
            {
                MessageTools.ShowMessageBox("플레이어 APK를 선택해주세요.", "확인");
            }
            else
            {
                BatchUpgrade(g_SelectedPlayerInfoList);
            }
        }

        List<PlayerInfoClass> g_SelectedPlayerInfoList = new List<PlayerInfoClass>();
        List<string> g_SelectedPlayerNameList = new List<string>();
        string playerNameListStr = string.Empty;


        public void BatchUpgrade(List<PlayerInfoClass> players)
        {
            if (g_SelectedPlayerInfoList.Count < 1)
            {
                MessageTools.ShowMessageBox("선택된 플레이어가 없습니다.", "확인");
                return;
            }
            
            foreach (PlayerInfoClass pic in players)
            {
                string pname = pic.PIF_PlayerName;
                var playerInfo = DataShop.Instance.g_PlayerInfoManager.GetPlayerInfoByName(pname);
                if (playerInfo != null)
                {
                    MainWindow.Instance.EnqueueCommandForPlayer(playerInfo, RP_ORDER.upgrade.ToString(), pushSignalR: true);
                }
            }

            MessageTools.ShowMessageBox("업그레이드를 요청했습니다.", "확인");
        }

        public void RefreshPlayerList()
        {
            PlayerWrapPanel.Children.Clear();

            if (DataShop.Instance.g_PlayerInfoManager.g_PlayerInfoClassList.Count > 0)
            {
                TextBlockPageName_Copy.Visibility = System.Windows.Visibility.Hidden;

                int idx = 1;

                foreach (PlayerInfoClass item in DataShop.Instance.g_PlayerInfoManager.g_PlayerInfoClassList)
                {
                    PlayerElemetForBatchUpgrade tmpElement = new PlayerElemetForBatchUpgrade();

                    tmpElement.UpdateDataInfo(item);

                    tmpElement.TextBlockOrderingNumber.Text = string.Format("{0:D2}", idx);
                    PlayerWrapPanel.Children.Add(tmpElement);
                    idx++;

                }
            }
        }

        void ShowPlayerLogWindow_Loaded(object sender, RoutedEventArgs e)
        {
            RefreshPlayerList();
            MainWindow.Instance?.SetDimOverlay(true);
        }

        void MaximizeBTN_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (this.WindowState == System.Windows.WindowState.Maximized)
                {
                    this.WindowState = System.Windows.WindowState.Normal;
                }
                else
                {
                    this.WindowState = System.Windows.WindowState.Maximized;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
        }

        private void BtnWin_close_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            this.Close();  
        }

        private void BtnWin_drag_PreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            this.DragMove();
        }

        private void ListCombo_Selected(object sender, SelectionChangedEventArgs e)
        {
            RefreshPlayerList();
        }

        private void Window_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
        }

        private void SelectFilePath_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.OpenFileDialog _ofd = new Microsoft.Win32.OpenFileDialog();

            _ofd.Filter = "APK File|*.apk";
            _ofd.RestoreDirectory = true;
            _ofd.Multiselect = false;

            try
            {
                if ((bool)_ofd.ShowDialog())
                {
                    APKFilePathTBox.Text = _ofd.FileName;
                    FileTools.CopyFile(_ofd.FileName, FNDTools.GetUpgradeAPKFTPFilePath());
                }
            }
            catch (System.Exception ex)
            {
                MessageTools.ShowMessageBox("APK 파일 오류 : " + ex.Message);
            }
        }
    }

}
