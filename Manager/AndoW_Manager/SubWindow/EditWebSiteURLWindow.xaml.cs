using System.Windows;
using TurtleTools;

namespace AndoW_Manager
{
    /// <summary>
    /// NotifyWindow.xaml에 대한 상호 작용 논리
    /// </summary>
     public partial class EditWebSiteURLWindow : Window
    {
         public string g_CurrentPlayerName = string.Empty;

         Page1 g_ParentElement = null;

         public EditWebSiteURLWindow(Page1 paramParent)
        {
            InitializeComponent();
            g_ParentElement = paramParent;
            InitEventHandler();       
        }

        public void InitEventHandler()
        {
            BTN0DO_Copy4.Click += new RoutedEventHandler(BTNPagesListNew1_Click);  //OK
            BTN0DO_Copy.Click += new RoutedEventHandler(CancelBTN_Click);  //Cancel
        }

        void CancelBTN_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }

        void BTNPagesListNew1_Click(object sender, RoutedEventArgs e)
        {
            if (TextBoxNewPlayerName_Copy.Text == string.Empty)
            {
                MessageTools.ShowMessageBox("웹사이트 주소를 입력해주세요.", "확인");
                return;
            }

            this.g_ParentElement.AddWebSiteURLToList(TextBoxNewPlayerName_Copy.Text);
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
