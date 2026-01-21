using System.Windows;

namespace TurtleTools
{
    /// <summary>
    /// NotifyWindow.xaml에 대한 상호 작용 논리
    /// </summary>
     public partial class NotifyWindow : Window
    {
        public NotifyWindow(string msg)
        {
            InitializeComponent();
            InitEventHandler();
            TextBlkMsg.Text = msg;
        }

        public NotifyWindow(string msg, string btn2)
        {
            InitializeComponent();
            InitEventHandler();
            TextBlkMsg.Text = msg;
            RBtn_Text.Text = btn2;
            LBtn.Visibility = Visibility.Hidden;
        }

        public NotifyWindow(string msg, string btn1, string btn2)
        {
            InitializeComponent();
            InitEventHandler();
            TextBlkMsg.Text = msg;
            LBtn_Text.Text = btn1;
            RBtn_Text.Text = btn2;
        }

        public void InitEventHandler()
        {
            LBtn.Click += new RoutedEventHandler(BTNPagesListNew1_Click);  //OK
            RBtn.Click += new RoutedEventHandler(CancelBTN_Click);  //Cancel
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


        private void BtnWin_close_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            this.Close();
        }

        private void BtnWin_drag_PreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            this.DragMove();
        }
    }

}
