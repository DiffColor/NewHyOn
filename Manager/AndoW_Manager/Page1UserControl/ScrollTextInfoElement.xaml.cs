using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using TurtleTools;

namespace AndoW_Manager
{
    /// <summary>
    /// ContentInfoElement.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class ScrollTextInfoElement : UserControl
    {
        bool g_IsSelected = false;

        public string noticeTitle = string.Empty;
        public string noticeImageName = string.Empty;

        public ContentsInfoClass g_ContentsInfoClass = new ContentsInfoClass();


        public bool Selected
        {
            get { return g_IsSelected; }
            set
            {
                g_IsSelected = (bool)value;
                if (g_IsSelected)
                {
                    SelectBorder.Visibility = Visibility.Visible;
                    SelectBorder.BorderBrush = ColorTools.GetSolidBrushByColorString("#FFB6BD17");
                }
                else
                {
                    SelectBorder.Visibility = Visibility.Hidden;
                    SelectBorder.BorderBrush = new SolidColorBrush(Colors.WhiteSmoke);
                }
            }
        }
  
        public ScrollTextInfoElement(ContentsInfoClass paramcls)
        {
            InitializeComponent();
            g_ContentsInfoClass.CopyData(paramcls);
            InitEventHandler();
            SelectBorder.Visibility = System.Windows.Visibility.Hidden;
        }

        public void InitEventHandler()
        {
            this.PreviewMouseLeftButtonDown += new MouseButtonEventHandler(PageListElement_PreviewMouseLeftButtonDown);
            this.PreviewMouseMove += new MouseEventHandler(PageListElement_PreviewMouseMove);
            this.MouseLeave += new MouseEventHandler(PageListElement_MouseLeave);

            ExitTextBlock.PreviewMouseMove += new MouseEventHandler(ExitTextBlock_PreviewMouseMove);
            ExitTextBlock.MouseLeave += new MouseEventHandler(ExitTextBlock_MouseLeave);
            ExitTextBlock.PreviewMouseLeftButtonUp += new MouseButtonEventHandler(ExitTextBlock_PreviewMouseLeftButtonUp);

            BTN0DO_Copy10.Click += BTN0DO_Copy10_Click;
        }

        void BTN0DO_Copy10_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            EditScrollTextWindow tmpWnd = new EditScrollTextWindow(this, this.g_ContentsInfoClass);
            tmpWnd.ShowDialog();

            Page1.Instance.RefreshScrollTextInfoList();
            Page1.Instance.SelectedContentInfo(g_ContentsInfoClass, DisplayType.ScrollText);
        }

        public void DisplayData()
        {
            TextBlockPageName.Text = this.g_ContentsInfoClass.CIF_DisplayFileName;
            Page1.Instance.EditScrollTextListToSelectedElement(this.g_ContentsInfoClass);
        }

        void ExitTextBlock_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            Page1.Instance.DeleteScrollTextList(g_ContentsInfoClass);
        }

        void ExitTextBlock_MouseLeave(object sender, MouseEventArgs e)
        {
            ExitTextBlock.Foreground = new SolidColorBrush(Colors.Gray);
        }

        void ExitTextBlock_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            ExitTextBlock.Foreground = new SolidColorBrush(Colors.White);
        }

        void PageListElement_MouseLeave(object sender, MouseEventArgs e)
        {
            if (g_IsSelected == false)
            {
                SelectBorder.Visibility = System.Windows.Visibility.Hidden;
            }
        }

        void PageListElement_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (g_IsSelected == false)
            {
                SelectBorder.Visibility = System.Windows.Visibility.Visible;

            }
        }

        void PageListElement_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            Page1.Instance.SelectedContentInfo(g_ContentsInfoClass, DisplayType.ScrollText);    
        }    
    }
}
