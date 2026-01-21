using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using TurtleTools;

namespace AndoW_Manager
{
    /// <summary>
    /// ContentInfoElement.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class PlayerInfoElement : UserControl
    {
        public bool g_IsSelected = false;

        public string noticeTitle = string.Empty;
        public string noticeImageName = string.Empty;

       public PlayerInfoClass g_PlayerInfoClass = new PlayerInfoClass();

       public DataTableForAndroid g_DataTableForAndroid = new DataTableForAndroid();

       bool _launcherEnabled = true;
       public bool LauncherEnabled
       {
           get { return _launcherEnabled; }
           set { _launcherEnabled = value;
                 if (!value) DisableContentReport();
               }
       }

        public PlayerInfoElement()
        {
            InitializeComponent();
            InitEventHandler();
            SelectBorder.Visibility = System.Windows.Visibility.Hidden;

            ShowAndHideSelectedBorder(false);

            DisplayPlayerStatus(PlayerStatus.Stopped);
        }

        public void DisableContentReport()
        {
            this.pagePrevieImgRect.Fill = ColorTools.GetSolidBrushByColorString("#FFD5D5DC"); 
            this.pagePrevieImgRect_Copy.Fill = ColorTools.GetSolidBrushByColorString("#FFD5D5DC");
            this.PowerOnOffTextBlk.Visibility = Visibility.Visible;
        }

        public void InitEventHandler()
        {
            this.PreviewMouseLeftButtonDown += new MouseButtonEventHandler(PageListElement_PreviewMouseLeftButtonDown);
            this.PreviewMouseRightButtonDown += PlayerInfoElement_PreviewMouseRightButtonDown;
            this.PreviewMouseMove += new MouseEventHandler(PageListElement_PreviewMouseMove);
            this.MouseLeave += new MouseEventHandler(PageListElement_MouseLeave);
            
            ExitTextBlock.PreviewMouseMove += new MouseEventHandler(ExitTextBlock_PreviewMouseMove);
            ExitTextBlock.MouseLeave += new MouseEventHandler(ExitTextBlock_MouseLeave);
            ExitTextBlock.PreviewMouseLeftButtonUp += new MouseButtonEventHandler(ExitTextBlock_PreviewMouseLeftButtonUp);
            DeletePlayerRect.PreviewMouseLeftButtonUp += new MouseButtonEventHandler(ExitTextBlock_PreviewMouseLeftButtonUp);

            PlaylistCombo.SelectionChanged += PlaylistCombo_SelectionChanged;  // 플레이리스트 콤보박스

            BTN0DO_Copy4.Click += BTN0DO_Copy4_Click;       // Next Page 

            BTN0DO_Copy2.Click += BTN0DO_Copy2_Click;   //Player Launcher 활성화
            BTN0DO_Copy3.Click += BTN0DO_Copy3_Click;   //Player Launcher 비활성화

            //Context Menu
            /*
               <MenuItem Header="Remote Control" ToolTip="Remote Control."  x:Name="MC_RemoteCotrol"/>
                <MenuItem Header="Prev Page" ToolTip="Play Previous Page."  x:Name="MC_PrevPage"/>
                <MenuItem Header="Next Page" ToolTip="Play Next Page."  x:Name="MC_NextPage"/>
                <MenuItem Header="Off System" ToolTip="Off System."  x:Name="MC_OffSystem"/>
                <MenuItem Header="Reboot System" ToolTip="Reboot System."  x:Name="MC_RebootSystem"/>
             */

            MC_RemoteCotrol.Click += MC_RemoteCotrol_Click;

            MC_PowerOn.Click += MC_PowerOn_Click;
            //MC_PowerOff.Click += MC_PowerOff_Click;
            MC_Reboot.Click += MC_Reboot_Click;

            MC_EditWeeklySch.Click += MC_EditWeeklySch_Click;
            //MC_NextPage.Click += MC_NextPage_Click;
            //MC_PlayFirstPage.Click += MC_PlayFirstPage_Click;   //  처음 페이지부터 재생
            //MC_GetSourceKey.Click += MC_GetSourceKey_Click;   // 소스키값 가져오기.
            //MC_SendAuthKey.Click += MC_SendAuthKey_Click;   // 인증키 전송
            
            BTN0DO_Copy5.Click += BTN0DO_Copy5_Click;      // 플레이리스트 업데이트

            //MC_Upgrade.Click += MC_Upgrade_Click;
            MC_Auth.Click += MC_Auth_Click;
            MC_ClearQueue.Click += MC_ClearQueue_Click;
        }

        private void MC_RemoteCotrol_Click(object sender, RoutedEventArgs e)
        {
            LaunchingRemoteControl();
        }

        public void LaunchingRemoteControl()
        {
            if (string.IsNullOrEmpty(this.g_PlayerInfoClass.PIF_IPAddress))
            {
                MessageTools.ShowMessageBox("플레이어 IP주소를 입력해주세요.", "확인");
                return;
            }

            ProcessTools.LaunchProcess(FNDTools.GetAnyDeskFilePath(), false, this.g_PlayerInfoClass.PIF_IPAddress);
        }

        void MC_Auth_Click(object sender, RoutedEventArgs e)
        {
            g_PlayerInfoClass.CopyData(DataShop.Instance.g_PlayerInfoManager.GetPlayerInfoByName(g_PlayerInfoClass.PIF_PlayerName));

            GetAuthWindow gaw = new GetAuthWindow(this.g_PlayerInfoClass);
            gaw.ShowDialog();
        }

        void MC_Upgrade_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.OpenFileDialog openFileDialog = new Microsoft.Win32.OpenFileDialog();

            openFileDialog.Filter = "APK Files|*.apk";

            try
            {
                if ((bool)openFileDialog.ShowDialog())
                {
                    FileTools.CopyFile(openFileDialog.FileName, FNDTools.GetUpgradeAPKFTPFilePath());

                    if (MessageTools.ShowMessageBox("플레이어의 업그레이드를 진행하시겠습니까?") == true)
                    {
                        MainWindow.Instance.EnqueueCommandForPlayer(this.g_PlayerInfoClass, RP_ORDER.upgrade.ToString(), pushSignalR: true);

                        MessageTools.ShowMessageBox("업그레이드를 요청했습니다.", "확인");
                    }
                }
            }
            catch (System.Exception ex)
            {
                MessageTools.ShowMessageBox("업그레이드를 실패하였습니다.");
            }
        }

        void MC_PlayFirstPage_Click(object sender, RoutedEventArgs e)
        {
            if (CheckIsPlayerOffline() == true)
            {
                MessageTools.ShowMessageBox("플레이어가 중지 상태입니다.", "확인");
                Page3.Instance.SetPlayerNetworkStatus(this.g_PlayerInfoClass.PIF_PlayerName, PlayerStatus.Stopped, isConnected: false);
                return;
            }

            //RPCaller.RPCall(this.g_PlayerInfoClass.PIF_IPAddress, RP_ID.GoFirst);
        }

        void MC_SendAuthKey_Click(object sender, RoutedEventArgs e)  // 인증키 전송
        {
            if (CheckIsPlayerOffline() == true)
            {
                MessageTools.ShowMessageBox("플레이어가 중지 상태입니다.", "확인");
                Page3.Instance.SetPlayerNetworkStatus(this.g_PlayerInfoClass.PIF_PlayerName, PlayerStatus.Stopped, isConnected: false);
                return;
            }

            List<string> authKeys = DataShop.Instance.g_PlayerInfoManager.GetAllAuthKeys();
            if (authKeys == null || authKeys.Count == 0)
            {
                MessageTools.ShowMessageBox("등록된 인증키가 없습니다.", "확인");
                return;
            }

            //String jsonDataStr = JsonConvert.SerializeObject(authKeys, Formatting.Indented);
            //RPCaller.RPCall(this.g_PlayerInfoClass.PIF_PlayerName, RP_ID.SendAuthKey, jsonDataStr);
        }

        void MC_GetSourceKey_Click(object sender, RoutedEventArgs e)
        {          
            if (CheckIsPlayerOffline() == true)
            {
                MessageTools.ShowMessageBox("플레이어가 중지 상태입니다.", "확인");
                Page3.Instance.SetPlayerNetworkStatus(this.g_PlayerInfoClass.PIF_PlayerName, PlayerStatus.Stopped, isConnected: false);
                return;
            }

             

        }

        void MC_EditWeeklySch_Click(object sender, RoutedEventArgs e)
        {
            EditOnAirTimeWindow tmpWnd = new EditOnAirTimeWindow(this.g_PlayerInfoClass);
            tmpWnd.ShowDialog();
        }


        void BTN0DO_Copy2_Click(object sender, RoutedEventArgs e)
        {
            if (CheckIsPlayerOffline() == true)
            {
                MessageTools.ShowMessageBox("플레이어가 중지 상태입니다.", "확인");
                Page3.Instance.SetPlayerNetworkStatus(this.g_PlayerInfoClass.PIF_PlayerName, PlayerStatus.Stopped, isConnected: false);
                return;
            }

            //RPCaller.RPCall(g_PlayerInfoClass.PIF_IPAddress, RP_ID.StartPlaying);
        }


        void BTN0DO_Copy5_Click(object sender, RoutedEventArgs e)
        {
            //if (!MainWindow.Instance.CheckFTPServerAlive())
            //{
            //    MessageTools.ShowMessageBox("파일 전송 서비스가 실행되지 않았습니다.\r\n파일 전송 서비스를 확인해주세요.", "확인");
            //    return;
            //}

            if(PlaylistCombo.SelectedItem == null)
            {
                MessageTools.ShowMessageBox("플레이리스트를 선택 후 전송해주세요.", "확인");
                return;
            }

            bool authorized = DataShop.Instance.g_PlayerInfoManager
                .HasValidAuthKey(this.g_PlayerInfoClass.PIF_PlayerName);

            if(authorized)
                ChangePlayList();
            else
                MessageTools.ShowMessageBox("미인증 플레이어입니다. 인증 후에 전송이 가능합니다.", "확인");
        }

        void PlayerInfoElement_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            Page3.Instance.SelectPlayerInfo(g_PlayerInfoClass);    
        }
        

        void MC_Reboot_Click(object sender, RoutedEventArgs e)
        {
            //if (CheckIsPlayerOffline() == true)
            //{
            //    MessageTools.ShowMessageBox("플레이어가 중지 상태입니다.", "확인");
            //    Page3.Instance.SetPlayerNetworkStatus(this.g_PlayerInfoClass.PIF_PlayerName, PlayerStatus.Stopped, isConnected: false);
            //    return;
            //}

            if (MessageTools.ShowMessageBox("플레이어 재시작을 요청하시겠습니까?"))
            {
                MainWindow.Instance.EnqueueCommandForPlayer(g_PlayerInfoClass, RP_ORDER.reboot.ToString(), pushSignalR: true);
                MessageTools.ShowMessageBox("재시작을 요청했습니다.", "확인");
            }
        }

        void MC_ClearQueue_Click(object sender, RoutedEventArgs e)
        {

            if (!MessageTools.ShowMessageBox("진행 중인 업데이트 큐를 삭제하시겠습니까?", "확인", "취소"))
            {
                return;
            }
            MainWindow.Instance.EnqueueCommandForPlayer(g_PlayerInfoClass, RP_ORDER.clearqueue.ToString(), pushSignalR: true);
            MessageTools.ShowMessageBox("삭제 명령을 전송했습니다.", "확인");
        }


        void MC_PowerOn_Click(object sender, RoutedEventArgs e)
        {
            //if (CheckIsPlayerOffline() == false)
            //{
            //    MessageTools.ShowMessageBox("플레이어가 온라인 상태입니다.", "확인");
            //    return;
            //}

            //if (string.IsNullOrEmpty(this.g_PlayerInfoClass.PIF_MacAddress))
            //{
            //    MessageTools.ShowMessageBox("MacAddress가 등록되지 않은 플레이어입니다.", "확인");
            //    return;
            //}

            if (MessageTools.ShowMessageBox("플레이어를 깨우시겠습니까?"))
            {
                MainWindow.Instance.EnqueueCommandForPlayer(g_PlayerInfoClass, RP_ORDER.updateschedule.ToString(), pushSignalR: true);
                WOL_Sender.SendWOLPacket(this.g_PlayerInfoClass.PIF_MacAddress);
            }
        }

        void MC_PowerOff_Click(object sender, RoutedEventArgs e)
        {
            //if (CheckIsPlayerOffline() == true)
            //{
            //    MessageTools.ShowMessageBox("플레이어가 중지 상태입니다.", "확인");
            //    Page3.Instance.SetPlayerNetworkStatus(this.g_PlayerInfoClass.PIF_PlayerName, PlayerStatus.Stopped, isConnected: false);
            //    return;
            //}

            if (MessageTools.ShowMessageBox("플레이어 전원종료를 요청하시겠습니까?"))
            {
                MainWindow.Instance.EnqueueCommandForPlayer(g_PlayerInfoClass, RP_ORDER.poweroff.ToString(), pushSignalR: true);
            }
        }

        void MC_NextPage_Click(object sender, RoutedEventArgs e)
        {
            if (CheckIsPlayerOffline() == true)
            {
                MessageTools.ShowMessageBox("플레이어가 중지 상태입니다.", "확인");
                Page3.Instance.SetPlayerNetworkStatus(this.g_PlayerInfoClass.PIF_PlayerName, PlayerStatus.Stopped, isConnected: false);
                return;
            }

            //RPCaller.RPCall(g_PlayerInfoClass.PIF_IPAddress, RP_ID.GoNext);
        }

        void BTN0DO_Copy4_Click(object sender, RoutedEventArgs e)  // Next Page 
        {
            if (CheckIsPlayerOffline() == true || !CheckIsLauncherState())
            {
                MessageTools.ShowMessageBox("플레이어가 중지 상태입니다.", "확인");
                Page3.Instance.SetPlayerNetworkStatus(this.g_PlayerInfoClass.PIF_PlayerName, PlayerStatus.Stopped, isConnected: false);
                return;
            }

            //RPCaller.RPCall(g_PlayerInfoClass.PIF_IPAddress, RP_ID.GoNext);
        }

        void PlaylistCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)   // 플레이리스트 변경...
        {
            /*
            if (this.g_ParentPage.Is_ComboBoxInit == false)
            {
                if (this.g_PlayerInfoClass.PIF_CurrentPlayList == scrollSpeedComboBox_Copy1.SelectedItem.ToString())
                {
                    MessageTools.ShowMessageBox("이전 플레이 리스트와 동일합니다.", "확인");
                    return;
                }
                else
                {
                    g_PlayerInfoClass.PIF_CurrentPlayList = scrollSpeedComboBox_Copy1.SelectedItem.ToString();
                    this.g_ParentPage.ChanagePageListName(this.g_PlayerInfoClass);

                    //1. 먼저 재생할 페이지를 먼저 보낸후 
                    TransferFileListToPlayer();
                    //2. 변경할 페이지리스트 이름을 보낸다.
                  
                }
            }
             */
        }

        internal void SetCurrentPreview(string pagename)
        {
            BitmapImage preview = LoadThumbnailBitmap(pagename);
            if (preview != null)
            {
                ApplyPreviewBitmap(preview);
                return;
            }

            ApplyPreviewPlaceholder();
        }

        public bool CheckIsPlayerOffline()
        {
            if (MainWindow.Instance.onlineList.Contains(this.g_PlayerInfoClass.PIF_PlayerName) == true)
            {
                return false;
            }
            else
            {
                return true;
            }
        }

        public bool CheckIsLauncherState()
        {
            return LauncherEnabled;
        }

        public void ChangePlayList()
        {
            if (CheckIsPlayerOffline() == true)
            {
                MessageTools.ShowMessageBox("플레이어가 중지 상태입니다.", "확인");
                Page3.Instance.SetPlayerNetworkStatus(this.g_PlayerInfoClass.PIF_PlayerName, PlayerStatus.Stopped, isConnected: false);
                return;
            }

            if (MessageTools.ShowMessageBox(string.Format("[{0}]의 플레이리스트를 변경하시겠습니까?", this.g_PlayerInfoClass.PIF_PlayerName), "예", "아니오") == true)
            {
                if (PlaylistCombo.SelectedItem == null)
                    return;

                DataShop.Instance.g_PageInfoManager.LoadPagesForList(PlaylistCombo.SelectedItem.ToString());

                if (DataShop.Instance.g_PageInfoManager.g_PageInfoClassList.Count > 0)
                {
                    g_PlayerInfoClass.PIF_CurrentPlayList = PlaylistCombo.SelectedItem.ToString();
                    DataShop.Instance.g_PlayerInfoManager.EditPlayerCurrentPlayList(g_PlayerInfoClass);
                    Page3.Instance.ChanagePageListName(this.g_PlayerInfoClass);

                    MainWindow.Instance.SendUrgentUpdateList(this.g_PlayerInfoClass);
                }
                else
                {
                    MessageTools.ShowMessageBox("비어있는 플레이리스트는 전송할수 없습니다.", "확인");
                    PlaylistCombo.SelectedItem = g_PlayerInfoClass.PIF_CurrentPlayList;
                }
            }
        }

        private void SavePlayerFiles()
        {
            
        }


        public ElementInfoControlClass g_ElementInfoControlClass = new ElementInfoControlClass();
        public List<string> g_FileListIncludedPages = new List<string>();
        public List<string> g_FontListIncludedPages = new List<string>();
        //public void LoadPageForUpdateFileOnPage3(string pageName)
        //{
        //    this.g_ElementInfoControlClass.LoadDataFromXML_ElementInfo(pageName);

        //    if (this.g_ElementInfoControlClass.g_ElementInfoClassList.Count > 0)
        //    {
        //        foreach (ElementInfoClass item in this.g_ElementInfoControlClass.g_ElementInfoClassList)
        //        {
        //            if (item.EIF_Type == "Display")
        //            {
        //                if (item.EIF_ContentsInfoClassList.Count > 0)
        //                {
        //                    foreach (ContentsInfoClass contentInfo in item.EIF_ContentsInfoClassList)
        //                    {
        //                        g_FileListIncludedPages.Add(contentInfo.CIF_FileName);
        //                    }
        //                }
        //            }
        //            else if (item.EIF_Type == "ScrollText")
        //            {
        //                //LoadScrollTextElementFromData(item);
        //            }
        //            else if (item.EIF_Type == "TextElement")
        //            {
        //                ILYCODEDataShop.Instance.g_TextInfoManager.LoadTextInfo(pageName, item.EIF_Name);

        //                if (ILYCODEDataShop.Instance.g_TextInfoManager.g_DataClassList[0].CIF_IsBGImageExist == true)
        //                {
                           
        //                    g_FileListIncludedPages.Add(ILYCODEDataShop.Instance.g_TextInfoManager.g_DataClassList[0].CIF_BGImageFileName);

        //                }
        //            }
        //        }
        //    }
        //}

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
                                    if (contentInfo.CIF_ContentType.Equals(ContentType.PDF.ToString()))
                                    {
                                        g_FileListIncludedPages.Add(Path.ChangeExtension(contentInfo.CIF_FileName, ".swf"));
                                        continue;
                                    }

                                    g_FileListIncludedPages.Add(contentInfo.CIF_FileName);

                                    string srcpath = FNDTools.GetContentFilePath(contentInfo);
                                    if (string.IsNullOrWhiteSpace(srcpath))
                                        continue;
                                    string targetpath = FNDTools.GetTargetContentsFilePath(contentInfo.CIF_FileName);

                                    if (contentInfo.CIF_ContentType == ContentType.WebSiteURL.ToString())
                                        continue;

                                    FileInfo fi = new FileInfo(targetpath);
                                    if (fi.Exists)
                                        if (fi.Length > 0)
                                            continue;

                                    FileTools.CopyFile(srcpath, targetpath);
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

        public void ShowAndHideSelectedBorder(bool IsShow)
        {
            g_IsSelected = IsShow;

            if (IsShow == true)
            {
                SelectBorder.Visibility = Visibility.Visible;
                SelectBorder_Copy1.Visibility = System.Windows.Visibility.Visible;
                SelectIcon.Visibility = Visibility.Visible;
                TextBlockOrderingNumber_Copy.Foreground = ColorTools.GetSolidBrushByColorString("#FF88FF00");
            }
            else
            {
                SelectBorder.Visibility = Visibility.Hidden;
                SelectBorder_Copy1.Visibility = System.Windows.Visibility.Hidden;
                SelectIcon.Visibility = Visibility.Hidden;
                TextBlockOrderingNumber_Copy.Foreground = ColorTools.GetSolidBrushByColorString("#FFFFFFFF");
            }
        }

        void PlayerInfoElement_PreviewMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (this.ActualHeight == 40)
            {
                this.Height = 183;
                IsShowMiniControlfSet(false);
            }
            else
            {
                this.Height = 40;
                IsShowMiniControlfSet(true);
            }
        }

        public void IsShowMiniControlfSet(bool IsFolding)
        {
            if (IsFolding == true)
            {
                BTN0DO_Copy6.Visibility = System.Windows.Visibility.Visible;
                 
            }
            else
            {
                BTN0DO_Copy6.Visibility = System.Windows.Visibility.Hidden;
            }
        }

        public void UpdateDataInfo(PlayerInfoClass paramCls, List<PageListInfoClass> paramPageList)
        {
            g_PlayerInfoClass.CopyData(paramCls);

            PlaylistCombo.Items.Clear();

            string direction = g_PlayerInfoClass.PIF_IsLandScape ? DeviceOrientation.Landscape.ToString() : DeviceOrientation.Portrait.ToString();

            var pagelist = from page in paramPageList
                            where page.PLI_PageDirection == direction
                            select page;

            foreach (PageListInfoClass item in pagelist)
            {
                PlaylistCombo.Items.Add(item.PLI_PageListName);
            }

            DisplayDataInfo();
        }

        public void RefreshPlayListComboBox()
        {
            PlaylistCombo.Items.Clear();

            string direction = g_PlayerInfoClass.PIF_IsLandScape ? DeviceOrientation.Landscape.ToString() : DeviceOrientation.Portrait.ToString();

            var pagelist = from page in DataShop.Instance.g_PageListInfoManager.g_PageListInfoClassList
                           where page.PLI_PageDirection == direction
                           select page;

            foreach (PageListInfoClass item in pagelist)
            {
                PlaylistCombo.Items.Add(item.PLI_PageListName);
            }

            PlaylistCombo.SelectedItem = g_PlayerInfoClass.PIF_CurrentPlayList;
        }

        public void DisplayDataInfo()
        {
            TextBlockOrderingNumber_Copy.Text = g_PlayerInfoClass.PIF_PlayerName;

            if (g_PlayerInfoClass.PIF_IsLandScape == true)
            {
                pagePrevieImgRect.Visibility = System.Windows.Visibility.Visible;
                pagePrevieImgRect_Copy.Visibility = System.Windows.Visibility.Hidden;
            }
            else
            {
                pagePrevieImgRect.Visibility = System.Windows.Visibility.Hidden;
                pagePrevieImgRect_Copy.Visibility = System.Windows.Visibility.Visible;
            }

            PlaylistCombo.SelectedItem = g_PlayerInfoClass.PIF_CurrentPlayList;
        }

        private BitmapImage LoadThumbnailBitmap(string pageName)
        {
            try
            {
                PageInfoClass definition = DataShop.Instance.g_PageInfoManager.GetPageDefinition(pageName);
                return MediaTools.CreateBitmapFromBase64(definition?.PIC_Thumb);
            }
            catch
            {
                return null;
            }
        }

        private void ApplyPreviewBitmap(BitmapImage bitmap)
        {
            if (bitmap == null)
            {
                return;
            }

            ImageBrush brush = new ImageBrush(bitmap)
            {
                Stretch = Stretch.UniformToFill
            };

            if (g_PlayerInfoClass.PIF_IsLandScape)
            {
                pagePrevieImgRect.Fill = brush;
                PowerOnOffTextBlk.Visibility = Visibility.Collapsed;
            }
            else
            {
                pagePrevieImgRect_Copy.Fill = brush;
                PowerOnOffTextBlk.Visibility = Visibility.Collapsed;
            }
        }

        private void ApplyPreviewPlaceholder()
        {
            SolidColorBrush placeholderBrush = new SolidColorBrush(Color.FromRgb(0x22, 0x22, 0x22));

            if (g_PlayerInfoClass.PIF_IsLandScape)
            {
                pagePrevieImgRect.Fill = placeholderBrush;
                PowerOnOffTextBlk.Visibility = Visibility.Visible;
            }
            else
            {
                pagePrevieImgRect_Copy.Fill = placeholderBrush;
                PowerOnOffTextBlk.Visibility = Visibility.Visible;
            }
        }

        void BTN0DO_Copy3_Click(object sender, RoutedEventArgs e)   //Player 종료
        {
            if (CheckIsPlayerOffline() == true)
            {
                MessageTools.ShowMessageBox("플레이어가 중지 상태입니다.", "확인");
                Page3.Instance.SetPlayerNetworkStatus(this.g_PlayerInfoClass.PIF_PlayerName, PlayerStatus.Stopped, isConnected: false);
                return;
            }

            //RPCaller.RPCall(g_PlayerInfoClass.PIF_IPAddress, RP_ID.StopPlaying);
        }

        void ExitTextBlock_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (MessageTools.ShowMessageBox("선택한 플레이어를 삭제하시겠습니까?", "예", "아니오") == true)
            {
                DataShop.Instance.g_PlayerInfoManager.DeleteDataClassInfo(this.g_PlayerInfoClass);
                Page3.Instance.RefreshPlayerInfoList();
            }

        }

        void ExitTextBlock_MouseLeave(object sender, MouseEventArgs e)
        {
            ExitTextBlock.Foreground = new SolidColorBrush(Colors.Gray);
        }

        void ExitTextBlock_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            ExitTextBlock.Foreground = new SolidColorBrush(Colors.White);
        }

        void PageListElement_MouseLeave(object sender, MouseEventArgs e)
        {
            if (g_IsSelected == false)
            {
                SelectBorder.Visibility = System.Windows.Visibility.Hidden;
            }
        }

        void PageListElement_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (g_IsSelected == false)
            {
                SelectBorder.Visibility = System.Windows.Visibility.Visible;
            }
        }

        void PageListElement_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            Page3.Instance.SelectPlayerInfo(g_PlayerInfoClass);    
        }


        public void C2S_ReportingCurrentPage(string pageName)
        {
            BitmapImage preview = LoadThumbnailBitmap(pageName);
            if (preview != null)
            {
                ApplyPreviewBitmap(preview);
            }
            else
            {
                ApplyPreviewPlaceholder();
            }

            ApplyPlayerStatus(PlayerStatus.Playing, pagename: pageName);
        }

        public string GetCurrentStatusText()
        {
            return CurrentPlayingPageName?.Text ?? string.Empty;
        }

        public void DisplayPlayerStatus(PlayerStatus status, int process=0, string version="", string pagename="", string hdmi_state = "", bool? isConnected = null)
        {
            this.Dispatcher.Invoke(DispatcherPriority.Normal,
                     new Action(() =>
                     {
                         ApplyPlayerStatus(status, process, version, pagename, hdmi_state, isConnected);
                     }));
        }

        private void UpdateOnlineState(bool? isConnected)
        {
            if (MainWindow.Instance == null || isConnected.HasValue == false)
            {
                return;
            }

            var list = MainWindow.Instance.onlineList;
            string playerName = g_PlayerInfoClass.PIF_PlayerName;
            bool connected = isConnected.Value;

            if (connected)
            {
                if (!list.Contains(playerName))
                {
                    list.Add(playerName);
                }
            }
            else
            {
                list.Remove(playerName);
            }
        }

        private void ApplyConnectionIndicator(bool? isConnected)
        {
            if (!isConnected.HasValue)
            {
                return;
            }
            bool connected = isConnected.Value;
            PowerOnOffTextBlk.Text = connected ? "ON" : "OFF";
            PowerOnOffTextBlk.Foreground = connected
                ? new SolidColorBrush(Colors.YellowGreen)
                : new SolidColorBrush(Colors.Gray);
            DspOnliePlayerRect.Fill = connected
                ? new SolidColorBrush(Colors.YellowGreen)
                : new SolidColorBrush(Colors.Gray);
        }

        internal void ApplyPlayerStatus(PlayerStatus status, int process = 0, string version = "", string pagename = "", string hdmi_state = "", bool? isConnected = null)
        {
            UpdateOnlineState(isConnected);
            ApplyConnectionIndicator(isConnected);

            string statusText = "중지됨";
            Brush statusBrush = new SolidColorBrush(Colors.Gray);
            switch (status)
            {
                case PlayerStatus.Playing:
                    statusText = "재생중";
                    statusBrush = new SolidColorBrush(Colors.Black);
                    pversion.Content = version;
                    if (!LauncherEnabled)
                    {
                        DisableContentReport();
                    }
                    else
                    {
                        SetCurrentPreview(pagename);
                        if (!string.IsNullOrWhiteSpace(pagename))
                        {
                            statusText = pagename;
                        }
                    }
                    break;

                case PlayerStatus.Updating:
                    statusText = "업데이트 중";
                    statusBrush = new SolidColorBrush(Colors.Blue);

                    if (!LauncherEnabled)
                    {
                        DisableContentReport();
                    }
                    else if (!string.IsNullOrWhiteSpace(pagename))
                    {
                        SetCurrentPreview(pagename);
                    }
                    break;

                case PlayerStatus.Stopped:
                    statusText = "중지됨";
                    statusBrush = new SolidColorBrush(Colors.Gray);
                    DisableContentReport();
                    break;

                default:
                    break;
            }

            CurrentPlayingPageName.Text = statusText;
            CurrentPlayingPageName.Foreground = statusBrush;
            UpdatingProgress.Width = this.ActualWidth * (process / 100.0);

            if (string.IsNullOrEmpty(hdmi_state))
            {
            }
            else
            {
                bool _state = Boolean.Parse(hdmi_state);
                if (_state)
                {
                    ConnectedLabel.Visibility = Visibility.Visible;
                    DisconnectedLabel.Visibility = Visibility.Collapsed;
                }
                else
                {
                    ConnectedLabel.Visibility = Visibility.Collapsed;
                    DisconnectedLabel.Visibility = Visibility.Visible;
                }
            }
        }
    }

    [DataContract]
    public class FileInfoToUpdateClass
    {
        [DataMember]
        public string FIT_FileName { get; set; }

        [DataMember]
        public string FIT_SrcFileFullPath { get; set; }

        [DataMember]
        public string FIT_PageName { get; set; }
    }
}
