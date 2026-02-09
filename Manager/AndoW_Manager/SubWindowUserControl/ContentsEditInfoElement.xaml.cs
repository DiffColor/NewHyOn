using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.IO;
using TurtleTools;

namespace AndoW_Manager
{
    /// <summary>
    /// ContentInfoElement.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class ContentsEditInfoElement : UserControl
    {
        EditContentsListWindow g_ParentPage = null;
        public bool g_IsSelected = false;

        public string noticeTitle = string.Empty;
        public string noticeImageName = string.Empty;

        public ContentsInfoClass g_ContentsInfoClass = new ContentsInfoClass();

       public ContentsEditInfoElement(EditContentsListWindow paramPage)
       {
            InitializeComponent();
            g_ParentPage = paramPage;
           // g_ContentsInfoClass.CopyData(paramcls);
            InitEventHandler();
            SelectBorder.Visibility = System.Windows.Visibility.Hidden;

            ShowAndHideSelectedBorder(false);
        }

        public void InitEventHandler()
        {
            this.PreviewMouseLeftButtonDown += new MouseButtonEventHandler(PageListElement_PreviewMouseLeftButtonDown);
            this.PreviewMouseMove += new MouseEventHandler(PageListElement_PreviewMouseMove);
            this.MouseLeave += new MouseEventHandler(PageListElement_MouseLeave);
            
            //BTN0DO_Copy1.Click += BTN0DO_Copy1_Click;  // 플레이 시간저장

            ExitTextBlock.PreviewMouseMove += new MouseEventHandler(ExitTextBlock_PreviewMouseMove);
            ExitTextBlock.MouseLeave += new MouseEventHandler(ExitTextBlock_MouseLeave);
            ExitTextBlock.PreviewMouseLeftButtonUp += new MouseButtonEventHandler(ExitTextBlock_PreviewMouseLeftButtonUp);

        }
     
        public void ShowAndHideSelectedBorder(bool IsShow)
        {
            if (IsShow == true)
            {
                SelectBorder_Copy1.Visibility = System.Windows.Visibility.Visible;
            }
            else
            {
                SelectBorder_Copy1.Visibility = System.Windows.Visibility.Hidden;
            }
        }

        public void UpdateDataInfo(ContentsInfoClass paramCls)
        {
            g_ContentsInfoClass.CopyData(paramCls);
            DisplayDataInfo();
        }

        public void DisplayDataInfo()
        {
            TextBlockOrderingNumber_Copy.Text = g_ContentsInfoClass.CIF_DisplayFileName;
            CurrentPlayingPageName_Copy1.Text = g_ContentsInfoClass.CIF_ContentType;
            CurrentPlayingPageName_Copy2.Text = string.Format("{0}:{1}",
                g_ContentsInfoClass.CIF_PlayMinute,
                g_ContentsInfoClass.CIF_PlaySec); 
        }

        void BTN0DO_Copy3_Click(object sender, RoutedEventArgs e)   //Player 종료
        {
           
        }

        void ExitTextBlock_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            this.g_ParentPage.DeleteContentsInfo(this.g_ContentsInfoClass.CIF_StrGUID);
        }

        void ExitTextBlock_MouseLeave(object sender, MouseEventArgs e)
        {
            //ExitTextBlock.Foreground = new SolidColorBrush(Colors.Black);
            ExitTextBlock.Foreground = ColorTools.GetSolidBrushByColorString("#FF575757");
            //
        }

        void ExitTextBlock_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            ExitTextBlock.Foreground = new SolidColorBrush(Colors.White);
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

        void PageListElement_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            this.g_ParentPage.SelectContentsInfo(g_ContentsInfoClass);    
        }
    }


}
