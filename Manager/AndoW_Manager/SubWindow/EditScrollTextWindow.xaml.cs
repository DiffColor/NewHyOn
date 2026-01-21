using System.Windows;
using TurtleTools;

namespace AndoW_Manager
{
    /// <summary>
    /// EditoScrollTextWindow.xaml에 대한 상호 작용 논리
    /// </summary>
     public partial class EditScrollTextWindow : Window
    {
         public ScrollTextInfoElement g_Parent = null;

       public  ContentsInfoClass g_ContentsInfoClass = new ContentsInfoClass();


        public EditScrollTextWindow(ScrollTextInfoElement paranElement, ContentsInfoClass tmpCls)
        {
            InitializeComponent();
            g_Parent = paranElement;
            InitComboBoxes();
            g_ContentsInfoClass.CopyData(tmpCls);
            InitEventHandler();
        }

        public void InitComboBoxes()
        {

            cavasWidthCombo1.Items.Clear();  // 자막속도 관련 콤보박스
            for (int i = 1; i < 31; i++)
            {
                cavasWidthCombo1.Items.Add(i);
            }
            cavasWidthCombo1.SelectedIndex = 9;
        }

        public void InitEventHandler()
        {
            BTN0DO_Copy4.Click += new RoutedEventHandler(BTNPagesListNew1_Click);  //OK
            BTN0DO_Copy.Click += new RoutedEventHandler(CancelBTN_Click);  //Cancel

            BTN0DO_Copy1.Click += BTN0DO_Copy1_Click;

            this.Loaded += EditoScrollTextWindow_Loaded;
        }

        void EditoScrollTextWindow_Loaded(object sender, RoutedEventArgs e)
        {
            DisplayData();
        }

        public void DisplayData()
        {
            TextBoxNewPlayerName.Text = this.g_ContentsInfoClass.CIF_FileName;
            cavasWidthCombo1.SelectedItem = this.g_ContentsInfoClass.CIF_ScrollTextSpeedSec;
        }

        void BTN0DO_Copy1_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        void CancelBTN_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }

        void BTNPagesListNew1_Click(object sender, RoutedEventArgs e)
        {
            if (TextBoxNewPlayerName.Text == string.Empty || cavasWidthCombo1.SelectedIndex == -1)
            {
                MessageTools.ShowMessageBox("입력데이터가 잘못되었습니다. 다시 확인해주세요.", "확인");
                return;
                
            }

            this.g_Parent.g_ContentsInfoClass.CIF_FileName = TextBoxNewPlayerName.Text;
            this.g_Parent.g_ContentsInfoClass.CIF_ScrollTextSpeedSec =(int) cavasWidthCombo1.SelectedItem;
            this.g_Parent.DisplayData();
            Page1.Instance.SetScrollSpeedComboItem(cavasWidthCombo1.SelectedItem.ToString());
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
