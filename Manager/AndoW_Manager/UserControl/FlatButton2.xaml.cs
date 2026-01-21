using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.ComponentModel;

namespace AndoW_Manager
{
    /// <summary>
    /// Interaction logic for UserControl1.xaml
    /// </summary>
    public partial class FlatButton2 : UserControl
    {
        public bool g_IsSelected = false;
     
        public int g_Idx = 0;

        MainWindow g_ParentWnd = null;
       // InputChannelInfoClass g_InputChannelInfoClass = new InputChannelInfoClass();

        public FlatButton2(MainWindow paramWnd)
        {
            InitializeComponent();
            InitEventHandler();
        }

        public void InitEventHandler()
        {
            this.PreviewMouseLeftButtonDown += UserControl1_PreviewMouseLeftButtonDown;

            this.PreviewMouseMove += new MouseEventHandler(PageListElement_PreviewMouseMove);
            this.MouseLeave += new MouseEventHandler(PageListElement_MouseLeave);

            ExitTextBlock.PreviewMouseMove += new MouseEventHandler(ExitTextBlock_PreviewMouseMove);
            ExitTextBlock.MouseLeave += new MouseEventHandler(ExitTextBlock_MouseLeave);
            ExitTextBlock.PreviewMouseLeftButtonUp += new MouseButtonEventHandler(ExitTextBlock_PreviewMouseLeftButtonUp);
        }

        void UserControl1_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            //this.g_ParentWnd.SelectInputChannelData(this.g_InputChannelInfoClass);
            //throw new NotImplementedException();
        }

        public void UpdateDataInfo(int idx)
        {
            g_Idx = idx;
            DisplayDataInfo();
        }

        public void DisplayDataInfo()
        {
            TextBlockOrderingNumber.Text = g_Idx.ToString();

        }

        void ExitTextBlock_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            //if (MessageTools.ShowMessageBox(string.Format("선택한 <{0}>를(을) 삭제하시겠습니까?", TextBlockPageName_Copy3.Text)) == true)
            //{
            //    //this.parentPage.DeleteStartStationNametByName(TextBlockPageName.Text);
            //}
        }

        //#FF212121
        void ExitTextBlock_MouseLeave(object sender, MouseEventArgs e)
        {
            Color c2 = (Color)TypeDescriptor.GetConverter(typeof(Color)).ConvertFromString("#FF808080");
            ExitTextBlock.Foreground = new SolidColorBrush(c2);
            //ExitTextBlock.Foreground = new SolidColorBrush(Colors.Black);
        }

        void ExitTextBlock_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            ExitTextBlock.Foreground = new SolidColorBrush(Colors.Gray);
        }

        void PageListElement_MouseLeave(object sender, MouseEventArgs e)
        {
            if (g_IsSelected == false)
            {
                //Color c2 = (Color)TypeDescriptor.GetConverter(typeof(Color)).ConvertFromString("#FF212121");
                //BackRectangle.Fill = new SolidColorBrush(c2);
                //BackRectangle.Fill = new SolidColorBrush(Colors.Black);
            }
        }

        void PageListElement_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (g_IsSelected == false)
            {
                //BackRectangle.Fill = new SolidColorBrush(Colors.Gray);
            }

            if (e.LeftButton == MouseButtonState.Pressed)
            {
                //object data = this.g_InputChannelInfoClass.ICF_ChannelName;
                //DragDrop.DoDragDrop(this, data, DragDropEffects.Copy);

               // DragDrop.DoDragDrop(this, (string)this.g_InputChannelInfoClass.ICF_ChannelName, DragDropEffects.Copy);
            }
        }

    }
}
