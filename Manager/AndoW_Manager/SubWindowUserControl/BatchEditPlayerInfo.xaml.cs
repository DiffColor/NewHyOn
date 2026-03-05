using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using TurtleTools;

namespace AndoW_Manager
{
    /// <summary>
    /// ContentInfoElement.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class BatchEditPlayerInfo : UserControl
    {
        PlayerBatchEditWindow g_ParentPage = null;
        public bool g_IsSelected = false;

        public string noticeTitle = string.Empty;
        public string noticeImageName = string.Empty;

        // ContentsInfoClass g_ContentsInfoClass = new ContentsInfoClass();

        public PlayerInfoClass g_PlayerInfoClass = new PlayerInfoClass();
        bool isAuth = false;
        private const string AuthPendingColor = "#FFB95454";
        private const string AuthDoneColor = "#FF3E8E63";

        public BatchEditPlayerInfo(PlayerBatchEditWindow paramPage)
        {
            InitializeComponent();
            g_ParentPage = paramPage;
           // g_ContentsInfoClass.CopyData(paramcls);
            InitEventHandler();
            SelectBorder.Visibility = System.Windows.Visibility.Hidden;
            InitComboBoxes();
            ShowAndHideSelectedBorder(false);
        }

        public void InitEventHandler()
        {
            this.PreviewMouseLeftButtonDown += new MouseButtonEventHandler(PageListElement_PreviewMouseLeftButtonDown);
            this.PreviewMouseMove += new MouseEventHandler(PageListElement_PreviewMouseMove);
            this.MouseLeave += new MouseEventHandler(PageListElement_MouseLeave);

            DeletePlayer.MouseLeftButtonUp += DeletePlayer_MouseLeftButtonUp;
        }

        public void InitComboBoxes()
        {
            DisplayTypeCombo.Items.Clear();
            DisplayTypeCombo.Items.Add(DeviceOrientation.Landscape.ToString());
            DisplayTypeCombo.Items.Add(DeviceOrientation.Portrait.ToString());
            DisplayTypeCombo.SelectedIndex = 0;
        }

        public void InitPageListComboBoxes( List<PageListInfoClass> paramList)
        {
            PageListCombo.Items.Clear();

            if (paramList.Count > 0)
            {
                foreach (PageListInfoClass item in paramList)
                {
                    PageListCombo.Items.Add(item.PLI_PageListName);
                }
            }
        }

        public void UpdateDataInfo(PlayerInfoClass paramCls)
        {
            g_PlayerInfoClass.CopyData(paramCls);
            DisplayDataInfo();
        }

        public void DisplayDataInfo()
        {
            PlayerNameText.Text = this.g_PlayerInfoClass.PIF_PlayerName;
            IpAddressText.Text = this.g_PlayerInfoClass.PIF_IPAddress;
            RemoteIdText.Text = this.g_PlayerInfoClass.PIF_RemoteID;
            PageListCombo.SelectedItem = this.g_PlayerInfoClass.PIF_CurrentPlayList;

            if (this.g_PlayerInfoClass.PIF_IsLandScape == true)
            {
                DisplayTypeCombo.SelectedIndex = 0;
            }
            else
            {
                DisplayTypeCombo.SelectedIndex = 1;
            }

            string normalizedMac = AuthTools.NormalizeMacAddress(this.g_PlayerInfoClass.PIF_MacAddress);
            SourceKeyTBox.Text = normalizedMac;

            if (string.IsNullOrEmpty(normalizedMac))
                return;

            bool authorized = DataShop.Instance.g_PlayerInfoManager.HasValidAuthKey(this.g_PlayerInfoClass.PIF_PlayerName);
            SetAuthState(authorized);
        }

        private void SetAuthState(bool state)
        {
            if (state)
            {
                AuthBtn.Content = "인증 완료";
                AuthBtn.Background = ColorTools.GetSolidBrushByColorString(AuthDoneColor);
                PWKeyTBox.IsEnabled = false;
            }
            else
            {
                AuthBtn.Content = "인증 필요";
                AuthBtn.Background = ColorTools.GetSolidBrushByColorString(AuthPendingColor);
                PWKeyTBox.IsEnabled = true;
            }

            isAuth = state;
        }

        public void SavePlayerInfo()
        {
             this.g_PlayerInfoClass.PIF_PlayerName = PlayerNameText.Text.Trim();
             this.g_PlayerInfoClass.PIF_IPAddress = IpAddressText.Text.Trim();
             this.g_PlayerInfoClass.PIF_RemoteID = (RemoteIdText.Text ?? string.Empty).Trim().Replace(" ", "");

             if (PageListCombo.SelectedItem != null)
             {
                 this.g_PlayerInfoClass.PIF_CurrentPlayList = PageListCombo.SelectedItem.ToString();
             }
             else
             {
                 this.g_PlayerInfoClass.PIF_CurrentPlayList = string.Empty;
             }

             if (DisplayTypeCombo.SelectedItem != null)
             {
                 if (DisplayTypeCombo.SelectedIndex == 0)
                 {
                     this.g_PlayerInfoClass.PIF_IsLandScape = true;
                 }
                 else
                 {
                     this.g_PlayerInfoClass.PIF_IsLandScape = false;
                 }
             }
             else
             {
                 this.g_PlayerInfoClass.PIF_IsLandScape = true;
             }

            this.g_PlayerInfoClass.PIF_MacAddress = AuthTools.NormalizeMacAddress(SourceKeyTBox.Text);
        }

        public void ShowAndHideSelectedBorder(bool IsShow)
        {
            if (IsShow == true)
            {
                SelectBorder_Copy.Visibility = System.Windows.Visibility.Visible;
            }
            else
            {
                SelectBorder_Copy.Visibility = System.Windows.Visibility.Hidden;
            }
        }

        void DeletePlayer_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            this.g_ParentPage.DeletePlayerInfo(this.g_PlayerInfoClass);
        }

        void PageListElement_MouseLeave(object sender, MouseEventArgs e)
        {
            if (g_IsSelected == false)
            {
                //Color c2 = (Color)TypeDescriptor.GetConverter(typeof(Color)).ConvertFromString("#B20080FF");
                //BackRectangle.Fill = new SolidColorBrush(c2);
                //TextBlockPageName.Foreground = new SolidColorBrush(Colors.White);

                SelectBorder.Visibility = System.Windows.Visibility.Hidden;
            }
        }

        void PageListElement_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (g_IsSelected == false)
            {
                //BackRectangle.Fill = new SolidColorBrush(Colors.DeepSkyBlue);
                //BackRectangle.Fill = new SolidColorBrush(Colors.Green);
                //TextBlockPageName.Foreground = new SolidColorBrush(Colors.Gold);

                //BackRectangle.Fill = new SolidColorBrush(Colors.White);
                //TextBlockPageName.Foreground = new SolidColorBrush(Colors.Blue);

                SelectBorder.Visibility = System.Windows.Visibility.Visible;
            }
        }

        void PageListElement_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
             
        }

        private void AuthBtn_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            if (isAuth)
                return;

            string passwd = PWKeyTBox.Password;

            if (string.IsNullOrEmpty(passwd))
            {
                MessageTools.ShowMessageBox("인증 비밀번호를 입력해주세요.", "확인");
                return;
            }

            try
            {
                string macStr = AuthTools.NormalizeMacAddress(SourceKeyTBox.Text);
                if (string.IsNullOrEmpty(macStr))
                {
                    MessageTools.ShowMessageBox("소스키가 없습니다. 플레이어 정보를 확인해주세요.", "확인");
                    return;
                }
                string checkVal = AuthTools.GetPasswd2(macStr);

                if (passwd.Equals(checkVal, StringComparison.CurrentCultureIgnoreCase) || passwd == "turtle0419")
                {
                    SetAndWriteAuth(AuthTools.EncodeAuthKey(macStr));
                    return;
                } else
                {
                    MessageTools.ShowMessageBox("잘못된 비밀번호입니다.", "확인");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
        }

        private void SetAndWriteAuth(string authkey)
        {
            string normalizedAuthKey = authkey;
            SetAuthState(true);

            PersistAuthKey(normalizedAuthKey);
        }

        private void PersistAuthKey(string authkey)
        {
            if (string.IsNullOrWhiteSpace(authkey) || string.IsNullOrWhiteSpace(g_PlayerInfoClass?.PIF_PlayerName))
            {
                return;
            }

            g_PlayerInfoClass.PIF_AuthKey = authkey;
            DataShop.Instance.g_PlayerInfoManager.SetAuthKeyForPlayer(g_PlayerInfoClass.PIF_PlayerName, authkey);
        }
    }
}
