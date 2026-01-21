using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using HyonManager.Pages;
using HyonManager.DataClass;

namespace HyonManager.SubElement
{
    /// <summary>
    /// Interaction logic for NoticeElement.xaml
    /// </summary>
    public partial class NoticeElement : UserControl
    {
        EditPage1 parentPage = null;
        public bool g_IsSelected = false;

        public string noticeTitle = string.Empty;
        public string noticeImageName = string.Empty;

        public NoticeElement(EditPage1 pWnd)
        {
            InitializeComponent();
            parentPage = pWnd;
            
            InitEventHandler();
        }     
        
        public void InitEventHandler()
        {
            this.PreviewMouseLeftButtonDown += new MouseButtonEventHandler(PageListElement_PreviewMouseLeftButtonDown);
            //this.PreviewMouseMove += new MouseEventHandler(PageListElement_PreviewMouseMove);
            //this.MouseLeave += new MouseEventHandler(PageListElement_MouseLeave);

            ExitTextBlock.PreviewMouseMove += new MouseEventHandler(ExitTextBlock_PreviewMouseMove);
            ExitTextBlock.MouseLeave += new MouseEventHandler(ExitTextBlock_MouseLeave);
            ExitTextBlock.PreviewMouseLeftButtonUp += new MouseButtonEventHandler(ExitTextBlock_PreviewMouseLeftButtonUp);
        }

        void ExitTextBlock_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            //if (UtilityClass.AskDoItThisTask(string.Format("선택한 <{0}>를(을) 삭제하시겠습니까?", TextBlockPageName.Text)) == MessageBoxResult.Yes)
            //{
            //    this.parentPage.DeleteNoticeInfoSelected(noticeTitle, noticeImageName);
            //}
        }

        void ExitTextBlock_MouseLeave(object sender, MouseEventArgs e)
        {
            ExitTextBlock.Foreground = new SolidColorBrush(Colors.Gray);
        }

        void ExitTextBlock_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            ExitTextBlock.Foreground = new SolidColorBrush(Colors.White);
        }

        //void PageListElement_MouseLeave(object sender, MouseEventArgs e)
        //{
        //    if (g_IsSelected == false)
        //    {
        //        BackRectangle.Fill = new SolidColorBrush(Colors.Gray);
        //    }
        //}

        //void PageListElement_PreviewMouseMove(object sender, MouseEventArgs e)
        //{
        //    if (g_IsSelected == false)
        //    {
        //        BackRectangle.Fill = new SolidColorBrush(Colors.White);    
        //    }
        //}

        void PageListElement_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            //parentPage.SelectNoticeElement(this.noticeTitle, this.noticeImageName);    
        }    
    }
}
