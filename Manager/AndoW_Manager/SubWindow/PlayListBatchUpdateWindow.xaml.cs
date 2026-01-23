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
    public partial class PlayListBatchUpdateWindow : Window
    {      
        private static PlayListBatchUpdateWindow instance = null;

        public static PlayListBatchUpdateWindow Instance
        {
            get
            {
                return instance;
            }
        }

        public PlayListBatchUpdateWindow()
        {
            InitializeComponent();
            instance = this;
            InitEventHandler();
        }

    
        public void InitComboBoxes()
        {
            ListCombo.Items.Clear();

            foreach (PageListInfoClass item in DataShop.Instance.g_PageListInfoManager.g_PageListInfoClassList)
            {
                if (string.IsNullOrEmpty(item.PLI_PageDirection))
                    continue;

                ListCombo.Items.Add(item.PLI_PageListName);
            }
        }

        public void InitEventHandler()
        {           
            BTN0DO_Copy.Click += new RoutedEventHandler(CancelBTN_Click);  // 업데이트 중지
            MaximizeBTN.Click += MaximizeBTN_Click;

            this.Loaded += ShowPlayerLogWindow_Loaded;

            BTN0DO_Copy2.Click += BTN0DO_Copy2_Click;  //업데이트시작

            BTN0DO_Copy1.Click += BTN0DO_Copy1_Click;   // 플레이어 모두선택
            BTN0DO_Copy3.Click += BTN0DO_Copy3_Click;   // 플레이어 모두해제

            KeyDown += PlayListBatchUpdateWindow_KeyDown;

            this.Closing += PlayListBatchUpdateWindow_Closing;
        }

        bool altF4Pressed = false;
        void PlayListBatchUpdateWindow_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (Keyboard.Modifiers == ModifierKeys.Alt && e.SystemKey == System.Windows.Input.Key.F4)
            {
                altF4Pressed = true;
            }
        }

        void PlayListBatchUpdateWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            MainWindow.Instance?.SetDimOverlay(false);

            if (altF4Pressed)
            {
                e.Cancel = true;
                altF4Pressed = false; 
                Hide();
                return;
            }

            instance = null;
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

            foreach (PlayerElemetForBatchUpdate item in PlayerWrapPanel.Children)
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

            //TextAngleGrade5_Copy.Text = string.Format("0 / {0}", g_SelectedPlayerNameList.Count);
        }


        public void ReleaseAllSelectedPlayer()
        {
            foreach (PlayerElemetForBatchUpdate item in PlayerWrapPanel.Children)
            {
                item.SelectThisElement(false);
            }

            RefreshCheckedPlayerList();
        }

        void BTN0DO_Copy1_Click(object sender, RoutedEventArgs e)   // 플레이어 모두선택
        {
            foreach (PlayerElemetForBatchUpdate item in PlayerWrapPanel.Children)
            {
                item.SelectThisElement(true);
            }

            RefreshCheckedPlayerList();
        }

        public bool IsStopUpdate = false;

        void BTN0DO_Copy2_Click(object sender, RoutedEventArgs e)   //업데이트시작
        {
            if (!MainWindow.Instance.CheckFTPServerAlive())
            {
                MessageTools.ShowMessageBox("FTP 서버에 연결할 수 없습니다.\r\n설정을 확인해주세요.", "확인");
                return;
            }

            if (ListCombo.SelectedIndex == -1)
            {
                MessageTools.ShowMessageBox("업데이트할 화면구성을 선택해주세요.", "확인");
            }
            else
            {
                //StartPlaylistBatchUpdate();
                DoUpdatePlayListToAllPlayer();
            }
        }

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        //
        List<PlayerInfoClass> g_SelectedPlayerInfoList = new List<PlayerInfoClass>();
        List<string> g_SelectedPlayerNameList = new List<string>();
        string playerNameListStr = string.Empty;


     
        public void UpdateBatchUpdateStatus(string paramPlayerName)
        {
            //TextAngleGrade5_Copy.Text = string.Format("{0} / {1}",g_BatchIndex,  g_SelectedPlayerNameList.Count);
            //TextAngleGrade5_Copy3.Text = paramPlayerName;

            ////WCFWindow1.Instance.CallBack_ReportingBatchUpdateState(paramPlayerName, g_SelectedPlayerNameList.Count, g_BatchIndex);

            //// 여기서 오퍼레이터한테 업데이트 현황을 알려준다.
        }

        //int g_BatchIndex = 0;
        public void DoUpdatePlayListToPlayerStepByStep()
        {
        //    try
        //    {
        //        DataShop.Instance.g_PlayerInfoManager.EditPlayerCurrentPlayList(g_SelectedPlayerInfoList[g_BatchIndex]);
        //        SaveFileForUpdateAtBatchUpdate(g_SelectedPlayerInfoList[g_BatchIndex]);

        //        //UpdateBatchUpdateStatus(g_SelectedPlayerInfoList[g_BatchIndex].PIF_PlayerName);
        //        MessageTools.ShowMessageBox("일괄 업데이트를 요청하였습니다.", "확인");
        //    }
        //    catch (Exception ex)
        //    {
        //        Logger.WriteErrorLog(ex.ToString(), Logger.GetLogFileName());
        //        Logger.WriteLog(string.Format("{0} <-- 플레이리스트 업데이트 실패", g_SelectedPlayerInfoList[g_BatchIndex].PIF_PlayerName), Logger.GetLogFileName());
        //    }

        //    g_BatchIndex++;   // 여기서 증가하는게 맞나?

        //    if (g_SelectedPlayerInfoList.Count == g_BatchIndex)
        //    {
        //        ////WCFWindow1.Instance.g_IsNowOnBatchUpdateAtSvc = false;  // 일괄업데이트가 완료되었따~!
        //        ReleaseAllSelectedPlayer();
        //        //TextAngleGrade5_Copy3.Text = "---";
        //        MessageTools.ShowMessageBox("일괄 업데이트를 요청하였습니다.", "확인");
        //    }
        }

        public void DoUpdatePlayListToAllPlayer()
        {
            try
            {
                SaveFileForUpdateAtBatchUpdate(g_SelectedPlayerInfoList, ListCombo.SelectedItem.ToString());
                ReleaseAllSelectedPlayer();
                MessageTools.ShowMessageBox("일괄 업데이트를 요청하였습니다.", "확인");
            }
            catch (Exception ex)
            {
                Logger.WriteErrorLog(ex.ToString(), Logger.GetLogFileName());
                Logger.WriteLog(string.Format("일괄 업데이트 실패"), Logger.GetLogFileName());
            }
        }

        public void StartPlaylistBatchUpdate()
        {
            if (g_SelectedPlayerInfoList.Count == 0)
            {
                MessageTools.ShowMessageBox("선택된 플레이어가 없습니다.", "확인");
            }
            else
            {
                if (MessageTools.ShowMessageBox(string.Format("선택한 플레이어 [{0}] 에 대한 일괄 업데이트를 시작하시겠습니까?", playerNameListStr), "예", "아니오") == true)
                {
                    foreach (PlayerInfoClass item in g_SelectedPlayerInfoList)
                    {
                        item.PIF_CurrentPlayList = ListCombo.SelectedItem.ToString();
                    }

                    //WCFWindow1.Instance.RefreshBatchUpdatePlayerNameList(g_SelectedPlayerNameList);
                    //WCFWindow1.Instance.g_IsNowOnBatchUpdateAtSvc = true;

                    DataShop.Instance.g_PageInfoManager.LoadPagesForList(ListCombo.SelectedItem.ToString());

                    if (DataShop.Instance.g_PageInfoManager.g_PageInfoClassList.Count > 0)
                    {
                        //g_BatchIndex = 0;    //<---------------- 일괄업데이트 본격적으로 스타트 하는 지점
                        DoUpdatePlayListToPlayerStepByStep();
                    }
                    else
                    {
                        MessageTools.ShowMessageBox("비어있는 화면구성은 전송할 수 없습니다.", "확인");
                    }

                }
            }

        }
        /*
        public void StartPlaylistBatchUpdate()
        {


            if (g_SelectedPlayerInfoList.Count == 0)
            {
                MessageTools.ShowMessageBox("선택된 플레이어가 없습니다.", "확인");
            }
            else
            {
                if (MessageTools.ShowMessageBox(string.Format("선택한 플레이어 [{0}] 에 대한 일괄 업데이트를 시작하시겠습니까?", playerNameListStr)) == true)
                {
                    WCFWindow1.Instance.RefreshBatchUpdatePlayerNameList(g_SelectedPlayerNameList);
                    WCFWindow1.Instance.g_IsNowPlaylistBatchUpdate = true;

                    //UpdateGrid.Visibility = System.Windows.Visibility.Visible;

                    ILYCODEDataShop.Instance.g_PageInfoManager.LoadPagesForList(scrollSpeedComboBox_Copy2.SelectedItem.ToString());

                    if (ILYCODEDataShop.Instance.g_PageInfoManager.g_PageInfoClassList.Count > 0)
                    {
                        int playerCnt = 1;
                        foreach (PlayerInfoClass item in g_SelectedPlayerInfoList)
                        {

                            if (IsStopUpdate == true)
                            {
                                break;
                            }

                            item.PIF_CurrentPlayList = scrollSpeedComboBox_Copy2.SelectedItem.ToString();


                            try
                            {
                                ILYCODEDataShop.Instance.g_PlayerInfoManager.EditPlayerCurrentPlayList(item);

                                // ILYCODEDataShop.Instance.g_PlayerInfoManager.EditPlayerCurrentPlayList(g_PlayerInfoClass);
                                // this.g_ParentPage.ChanagePageListName(this.g_PlayerInfoClass);

                               // WCFWindow1.Instance.g_BatchUpdateEvent.Reset();
                                //1. 먼저 재생할 페이지를 먼저 보낸후 
                                TransferFileListToPlayerAtBatchUpdate(item);
                                //2. 변경할 페이지리스트 이름을 보낸다.

                                //TextAngleGrade5_Copy5.Text = string.Format("{0} / {1}", playerCnt, tempPlayerInfoList.Count);


                                /////////////////////////////////////////////////////////////////////////////////////////////////////////
                                //이벤트로 기다리게 해야한다.  <---!!!!!!!!!!!!!  ****************************************************

                               // WCFWindow1.Instance.g_BatchUpdateEvent.WaitOne();

                            }
                            catch (Exception ex)
                            {
                                Logger.WriteErrorLog(ex.ToString());
                                Logger.WriteLog(string.Format("{0} <-- 플레이리스트 업데이트 실패", item.PIF_PlayerName));

                            }

                            playerCnt++;
                        }


                     //   WCFWindow1.Instance.g_IsNowPlaylistBatchUpdate = false;

                        //UpdateGrid.Visibility = System.Windows.Visibility.Hidden;
                       // this.Close();



                    }
                    else
                    {
                        MessageTools.ShowMessageBox("비어있는 플레이리스트는 전송할수 없습니다.", "확인");
                    }

                }
            }
        
        }
        */

        public List<string> g_FileListIncludedPages = new List<string>();

        public void SaveFileForUpdateAtBatchUpdate(List<PlayerInfoClass> players, string listname)
        {
            foreach (PlayerInfoClass pic in players)
            {
                pic.PIF_CurrentPlayList = listname;
                string pname = pic.PIF_PlayerName;
                DataShop.Instance.g_PlayerInfoManager.EditPlayerCurrentPlayList(pic);
                Page3.Instance.ChanagePageListName(pic);
                var playerInfo = DataShop.Instance.g_PlayerInfoManager.GetPlayerInfoByName(pname);
                if (playerInfo != null)
                {
                    string payloadBase64 = DataShop.Instance.g_UpdatePayloadBuilder.BuildPayloadBase64(playerInfo);
                    MainWindow.Instance.EnqueueCommandForPlayer(playerInfo, RP_ORDER.updatelist.ToString(), payloadBase64, pushSignalR: true);
                }
            }
        }

        public ElementInfoControlClass g_ElementInfoControlClass = new ElementInfoControlClass();

        public void LoadPageForUpdateFileOnPage3(string pageName)
        {
            this.g_ElementInfoControlClass.LoadData_ElementInfo(pageName);

            if (this.g_ElementInfoControlClass.g_ElementInfoClassList.Count > 0)
            {
               
                foreach (ElementInfoClass item in this.g_ElementInfoControlClass.g_ElementInfoClassList)
                {
                    switch ((DisplayType)Enum.Parse(typeof(DisplayType), item.EIF_Type))
                    {
                        case DisplayType.Media:
                            if (item.EIF_ContentsInfoClassList.Count > 0)
                            {
                                foreach (ContentsInfoClass contentInfo in item.EIF_ContentsInfoClassList)
                                {
                                    g_FileListIncludedPages.Add(contentInfo.CIF_FileName);
                                }
                            }
                            break;

                        case DisplayType.HDTV:
                        case DisplayType.IPTV:
                            break;

                        case DisplayType.ScrollText:
                            break;

                        case DisplayType.WelcomeBoard:
                            DataShop.Instance.g_TextInfoManager.LoadTextInfo(pageName, item.EIF_Name);
                            TextInfoClass textInfo = DataShop.Instance.g_TextInfoManager.g_DataClassList.FirstOrDefault();
                            if (textInfo != null && textInfo.CIF_IsBGImageExist == true)
                            {
                                g_FileListIncludedPages.Add(textInfo.CIF_BGImageFileName);
                            }
                            break;
                    }
                }
            }
        }

        public void RefreshPlayerList()
        {
            PlayerWrapPanel.Children.Clear();

            if (DataShop.Instance.g_PlayerInfoManager.g_PlayerInfoClassList.Count > 0)
            {
                TextBlockPageName_Copy.Visibility = System.Windows.Visibility.Hidden;

                int idx = 1;

                string direction = null;

                if (ListCombo.SelectedIndex > -1)
                {
                    direction = DataShop.Instance.g_PageListInfoManager.GetPageDirection(ListCombo.SelectedItem.ToString());
                }

                foreach (PlayerInfoClass item in DataShop.Instance.g_PlayerInfoManager.g_PlayerInfoClassList)
                {
                    if (string.IsNullOrEmpty(direction) == false)
                    {
                        if (item.PIF_IsLandScape != direction.Equals(DeviceOrientation.Landscape.ToString()))
                            continue;
                    }

                    PlayerElemetForBatchUpdate tmpElement = new PlayerElemetForBatchUpdate(this);

                    tmpElement.Width = 275;
                    tmpElement.UpdateDataInfo(item);

                    tmpElement.TextBlockOrderingNumber.Text = string.Format("{0:D2}", idx);
                    tmpElement.Margin = new Thickness(2, 2, 2, 0);
                    PlayerWrapPanel.Children.Add(tmpElement);
                    idx++;

                }
            }
        }

        void ShowPlayerLogWindow_Loaded(object sender, RoutedEventArgs e)
        {
            InitComboBoxes();
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


        void CancelBTN_Click(object sender, RoutedEventArgs e)   // 일괄업데이트 중지
        {
            if (MessageTools.ShowMessageBox("일괄업데이트를 중지하시겠습니까?", "예", "아니오") == true)
            {
               //WCFWindow1.Instance.g_IsNowOnBatchUpdateAtSvc = false;
               //WCFWindow1.Instance.g_PlayerNameListToUpdate.Clear();
            }
        }

        private void BtnWin_close_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            this.Hide();  
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
            InitComboBoxes();
        }
    }

}
