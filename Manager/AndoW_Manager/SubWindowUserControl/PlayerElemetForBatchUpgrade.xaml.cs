using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using TurtleTools;


namespace AndoW_Manager
{
    /// <summary>
    /// ContentInfoElement.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class PlayerElemetForBatchUpgrade : UserControl
    {
        public bool g_IsSelected = false;

        public  PlayerInfoClass g_PlayerInfoClass = new PlayerInfoClass();

        public PlayerElemetForBatchUpgrade()
        {
            InitializeComponent();
            InitEventHandler();
            SelectBorder.Visibility = System.Windows.Visibility.Hidden;

            ShowAndHideSelectedBorder(false);
            SelectedCheckBox.Visibility = System.Windows.Visibility.Collapsed;
        }

        public void InitEventHandler()
        {
            PreviewMouseLeftButtonDown += PlayerElemetForBatchUpdate_PreviewMouseLeftButtonDown;
            PreviewMouseMove += PageListElement_PreviewMouseMove;
            MouseLeave += PageListElement_MouseLeave;
        }

        void PlayerElemetForBatchUpdate_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            bool isSelected = SelectedCheckBox.IsChecked == true;
            SelectThisElement(!isSelected);

            if (BatchUpgradeWindow.Instance != null)
            {
                BatchUpgradeWindow.Instance.RefreshCheckedPlayerList();
            }
        }

        public void SelectThisElement(bool isSelected)
        {
            if (isSelected)
            {
                g_IsSelected = true;
                SelectedCheckBox.IsChecked = true;
                TextBlockPageName.Foreground = ColorTools.GetSolidBrushByColorString("#FFFFFFFF");
                TextBlockOrderingNumber.Foreground = ColorTools.GetSolidBrushByColorString("#A5FFFFFF");
                SelectedDspRect.Fill = new SolidColorBrush(Colors.GreenYellow);
                ShowAndHideSelectedBorder(true);
            }
            else
            {
                g_IsSelected = false;
                SelectedCheckBox.IsChecked = false;
                TextBlockPageName.Foreground = ColorTools.GetSolidBrushByColorString("#FF1B1F5A");
                TextBlockOrderingNumber.Foreground = ColorTools.GetSolidBrushByColorString("#FF1B1F5A");
                SelectedDspRect.Fill = ColorTools.GetSolidBrushByColorString("#FFFFFFFF");
                ShowAndHideSelectedBorder(false);
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
            SelectBorder.Visibility = System.Windows.Visibility.Hidden;

            if (g_IsSelected)
            {
                TextBlockPageName.Foreground = ColorTools.GetSolidBrushByColorString("#FFFFFFFF");
                TextBlockOrderingNumber.Foreground = ColorTools.GetSolidBrushByColorString("#A5FFFFFF");
            }
            else
            {
                TextBlockPageName.Foreground = ColorTools.GetSolidBrushByColorString("#FF1B1F5A");
                TextBlockOrderingNumber.Foreground = ColorTools.GetSolidBrushByColorString("#FF1B1F5A");
            }
        }

        void PageListElement_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            SelectBorder.Visibility = System.Windows.Visibility.Visible;
            TextBlockPageName.Foreground = ColorTools.GetSolidBrushByColorString("#FFFFFFFF");
            TextBlockOrderingNumber.Foreground = ColorTools.GetSolidBrushByColorString("#A5FFFFFF");
        }

    }
}
