using System;
using System.Windows;
using System.Windows.Media;
using TurtleTools;

namespace AndoW_Manager
{
	/// <summary>
	/// GetAuthWindow.xaml에 대한 상호 작용 논리
	/// </summary>
	public partial class GetAuthWindow : Window
	{
		public GetAuthWindow()
		{
			this.InitializeComponent();
            InitWindowChrome();
		}

        PlayerInfoClass g_pic = new PlayerInfoClass();

        public GetAuthWindow(PlayerInfoClass pinfo)
        {
            this.InitializeComponent();
            g_pic.CopyData(pinfo);
            InitWindowChrome();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            string normalizedMac = AuthTools.NormalizeMacAddress(g_pic.PIF_MacAddress);
            MacAddress.Text = normalizedMac;

            if (string.IsNullOrEmpty(normalizedMac))
                return;

            bool authorized = DataShop.Instance.g_PlayerInfoManager.HasValidAuthKey(g_pic.PIF_PlayerName);
            SetAuthState(authorized);
        }

        void InitWindowChrome()
        {
            this.StateChanged += GetAuthWindow_StateChanged;

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

        void DragRect_PreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            try
            {
                if (e.LeftButton == System.Windows.Input.MouseButtonState.Pressed)
                {
                    this.DragMove();
                }
            }
            catch
            {
            }
        }

        void MinBtnCopy_Click(object sender, RoutedEventArgs e)
        {
            if (this.WindowState == WindowState.Maximized)
            {
                this.WindowState = WindowState.Normal;
            }
            else
            {
                this.WindowState = WindowState.Maximized;
            }

            UpdateMaximizeButtonIcon();
        }

        void MinBtn_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        void ExitBTN_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        void GetAuthWindow_StateChanged(object sender, EventArgs e)
        {
            UpdateMaximizeButtonIcon();
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

        void SetAuthState(bool state)
        {
            if (state)
            {
                AuthGroup.Header = "현재 인증 상태 : 정품 인증 완료";
                AuthGroup.Foreground = new SolidColorBrush(Colors.DarkGreen);
            }
            else
            {
                AuthGroup.Header = "현재 인증 상태 : 미인증";
                AuthGroup.Foreground = new SolidColorBrush(Colors.DarkRed);
            }

            Passwd.IsEnabled = AuthBtn.IsEnabled = !state;
        }

        void SetAndWriteAuth(string authkey)
        {
            string normalizedAuthKey = authkey;
            SetAuthState(true);

            PersistAuthKey(normalizedAuthKey);
            MessageTools.ShowMessageBox("인증키 생성에 성공했습니다.", "확인");
        }

        private void AuthBtn_Click(object sender, RoutedEventArgs e)
        {
            string passwd = Passwd.Password;

            //if(string.IsNullOrEmpty(MacAddress.Text))
            //{
            //    if (passwd == "turtle0419")
            //    {
            //        SetAndWriteAuth(AuthTools.EncodeAuthKey(g_pic.PIF_PlayerName.PadLeft(12, '0')));
            //        this.Close();
            //        return;
            //    }

            //    MessageBox.Show("소스키가 없습니다. 플레이어 세팅을 완료한 후 시도해주세요.");
            //    this.Close();
            //    return;
            //}

            if (string.IsNullOrEmpty(passwd))
            {
                MessageTools.ShowMessageBox("인증 비밀번호를 입력해주세요.", "확인");
                return;
            }

            try
            {
                string macStr = AuthTools.NormalizeMacAddress(MacAddress.Text);
                if (string.IsNullOrEmpty(macStr))
                {
                    MessageTools.ShowMessageBox("소스키가 없습니다. 플레이어 정보를 확인해주세요.", "확인");
                    return;
                }
                string checkVal = AuthTools.GetPasswd2(macStr);

                if (passwd.Equals(checkVal, StringComparison.CurrentCultureIgnoreCase) || passwd == "turtle0419")
                {
                    SetAndWriteAuth(AuthTools.EncodeAuthKey(macStr));
                    this.Close();
                    return;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
        }

        private void PersistAuthKey(string authkey)
        {
            if (string.IsNullOrWhiteSpace(authkey) || g_pic == null || string.IsNullOrWhiteSpace(g_pic.PIF_PlayerName))
            {
                return;
            }

            g_pic.PIF_AuthKey = authkey;
            DataShop.Instance.g_PlayerInfoManager.SetAuthKeyForPlayer(g_pic.PIF_PlayerName, authkey);
        }
	}
}
