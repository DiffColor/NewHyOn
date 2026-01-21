using System.Windows;

namespace AndoW_Manager
{
    /// <summary>
    /// PDF2SWFWindow.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class PDF2SWFWindow : Window
    {
        public ContentInfoElement g_Parent = null;
        public ContentsInfoClass g_ContentsInfoClass = new ContentsInfoClass();

        public PDF2SWFWindow(ContentInfoElement paranElement, ContentsInfoClass tmpCls)
        {
            InitializeComponent();
            g_Parent = paranElement;
            InitComboBoxes();
            g_ContentsInfoClass.CopyData(tmpCls);
            InitEventHandler();
        }

        public void InitComboBoxes()
        {

            slideSpeedCombo.Items.Clear();  
            for (int i = 5; i < 31; i++)
            {
                slideSpeedCombo.Items.Add(i);
            }
            slideSpeedCombo.SelectedIndex = 4;
        }

        public void InitEventHandler()
        {
            BTN0DO_Copy4.Click += new RoutedEventHandler(BTNPagesListNew1_Click);  //OK
            BTN0DO_Copy.Click += new RoutedEventHandler(CancelBTN_Click);  //Cancel

            BTN0DO_Copy1.Click += BTN0DO_Copy1_Click;

            this.Loaded += PDF2SWFWindow_Loaded;
        }

        void PDF2SWFWindow_Loaded(object sender, RoutedEventArgs e)
        {
            DisplayData();
        }

        public void DisplayData()
        {
            slideSpeedCombo.SelectedItem = this.g_ContentsInfoClass.CIF_ScrollTextSpeedSec;
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
            this.g_Parent.g_ContentsInfoClass.CIF_ScrollTextSpeedSec = (int)slideSpeedCombo.SelectedItem;
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
