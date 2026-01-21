using System.Windows;
using TurtleTools;

namespace AndoW_Manager
{
    /// <summary>
    /// NotifyWindow.xaml에 대한 상호 작용 논리
    /// </summary>
     public partial class EditPlayListNameWindow : Window
    {
         public string g_CurrentPlayerName = string.Empty;

         PageListNameElement g_ParentElement = null;

         public EditPlayListNameWindow(PageListNameElement paramParent)
        {
            InitializeComponent();
            g_ParentElement = paramParent;
            InitEventHandler();
            TextBlkMsg_Copy1.Text = paramParent.g_PageListInfoClass.PLI_PageListName;
            PrevNameTextBlk.Text = paramParent.g_PageListInfoClass.PLI_PageListName;
        
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
                MessageTools.ShowMessageBox("플레이리스트 이름을 입력해주세요.", "확인");
                return;
            }
            else
            {
                if (DataShop.Instance.g_PageListInfoManager.CheckExistSamename(TextBoxNewPlayerName_Copy.Text) == true)
                {
                    MessageTools.ShowMessageBox("같은 이름의 플레이리스트가 존재합니다.", "확인");
                }
                else
                {
                    Page2.Instance.EditPlayListName(PrevNameTextBlk.Text, TextBoxNewPlayerName_Copy.Text);
                    this.DialogResult = true;
                    this.Close();
                }
               
           
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
    }

}
