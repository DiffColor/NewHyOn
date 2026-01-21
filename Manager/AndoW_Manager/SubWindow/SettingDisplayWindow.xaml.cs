using System.Windows;

namespace AndoW_Manager
{
    /// <summary>
    /// NotifyWindow.xaml에 대한 상호 작용 논리
    /// </summary>
     public partial class SettingDisplayWindow : Window
    {
      
         public SettingDisplayWindow()
        {
            InitializeComponent();
        }


        void CancelBTN_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }

        void BTNPagesListNew1_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
            this.Close();
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
           // Application.Current.Shutdown();
            this.Close();

        }

        private void BtnWin_drag_PreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            this.DragMove();
        }

    }

}
