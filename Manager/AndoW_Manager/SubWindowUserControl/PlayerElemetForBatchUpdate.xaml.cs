using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using TurtleTools;


namespace AndoW_Manager
{
    /// <summary>
    /// ContentInfoElement.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class PlayerElemetForBatchUpdate : UserControl
    {
        PlayListBatchUpdateWindow g_ParentPage = null;
        public bool g_IsSelected = false;

        public string noticeTitle = string.Empty;
        public string noticeImageName = string.Empty;

       // ContentsInfoClass g_ContentsInfoClass = new ContentsInfoClass();

        public  PlayerInfoClass g_PlayerInfoClass = new PlayerInfoClass();

        public PlayerElemetForBatchUpdate(PlayListBatchUpdateWindow paramPage)
        {
            InitializeComponent();
            g_ParentPage = paramPage;
           // g_ContentsInfoClass.CopyData(paramcls);
            InitEventHandler();
            SelectBorder.Visibility = System.Windows.Visibility.Hidden;

            ShowAndHideSelectedBorder(false);
            SelectedCheckBox.Visibility = System.Windows.Visibility.Collapsed;
        }

        public void InitEventHandler()
        {
            this.PreviewMouseLeftButtonDown += PlayerElemetForBatchUpdate_PreviewMouseLeftButtonDown;
            this.PreviewMouseMove += new MouseEventHandler(PageListElement_PreviewMouseMove);
            this.MouseLeave += new MouseEventHandler(PageListElement_MouseLeave);
        }

        void PlayerElemetForBatchUpdate_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            //if (WCFWindow1.Instance.IsExistPlayerConnectionByPlayerName(this.g_PlayerInfoClass.PIF_PlayerName) == false)
            //{
            //    MessageTools.ShowMessageBox(string.Format("현재 플레이어 [{0}] 가 오프라인 상태입니다.", this.g_PlayerInfoClass.PIF_PlayerName), "확인");
            //    Page3.Instance.SetPlayerStatus(this.g_PlayerInfoClass.PIF_PlayerName, "offline");
            //    return;
            //}

            if (SelectedCheckBox.IsChecked == true)
            {
                SelectedCheckBox.IsChecked = false;
                TextBlockPageName.Foreground = new SolidColorBrush(Colors.White);
                SelectedDspRect.Fill = new SolidColorBrush(Colors.White);

            }
            else
            {
                SelectedCheckBox.IsChecked = true;
                TextBlockPageName.Foreground = new SolidColorBrush(Colors.GreenYellow);
                SelectedDspRect.Fill = new SolidColorBrush(Colors.GreenYellow);
            }

            this.g_ParentPage.RefreshCheckedPlayerList();
        }

        public void SelectThisElement(bool IsParamSelect)
        {

            if (IsParamSelect == true)
            {
                SelectedCheckBox.IsChecked = true;
                TextBlockPageName.Foreground = new SolidColorBrush(Colors.GreenYellow);
                SelectedDspRect.Fill = new SolidColorBrush(Colors.GreenYellow);
            }
            else
            {
                SelectedCheckBox.IsChecked = false;
                TextBlockPageName.Foreground = new SolidColorBrush(Colors.White);
                SelectedDspRect.Fill = new SolidColorBrush(Colors.White);

            }

        }

        public void UpdateDataInfo(PlayerInfoClass paramCls)
        {
            g_PlayerInfoClass.CopyData(paramCls);
            DisplayDataInfo();
        }

        public void DisplayDataInfo()
        {
            TextBlockPageName.Text = g_PlayerInfoClass.PIF_PlayerName;
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

    }
}
