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
using System.ComponentModel;
using HyonManager.DataClass;

namespace HyonManager.SubElement
{
    /// <summary>
    /// SavedPageElement.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class SavedPageElementPage1 : UserControl
    {   
        EditPage1 g_ParentPage = null;
        public bool g_IsSelected = false;

        public SavedPageElementPage1(EditPage1 paramPage)
        {
            InitializeComponent();
            g_ParentPage = paramPage;
            InitEventHandler();
        }

        public void InitEventHandler()
        {
            //this.PreviewMouseLeftButtonDown += new MouseButtonEventHandler(PageListElement_PreviewMouseLeftButtonDown);
            this.PreviewMouseMove += new MouseEventHandler(PageListElement_PreviewMouseMove);
            this.MouseLeave += new MouseEventHandler(PageListElement_MouseLeave);
            this.PreviewMouseDoubleClick += SavedPageElement_PreviewMouseDoubleClick;

            this.BorderBTN_Copy.Click += BorderBTN_Copy_Click;
        }

        void BorderBTN_Copy_Click(object sender, RoutedEventArgs e)
        {
            //if (UtilityClass.ShowMessageBox(string.Format("[{0}]을(를) 플레이어에 추가하시겠습니까?", pageNameTextBlock.Text)) == true)
            //{
                this.g_ParentPage.AddPageScheduleToPlayer(pageNameTextBlock.Text);
            //}
            //else
            //{

            //}
        }

        void SavedPageElement_PreviewMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            //if (UtilityClass.ShowMessageBox(string.Format("[{0}]을(를) 플레이어에 추가하시겠습니까?", pageNameTextBlock.Text)) == true)
            //{
                this.g_ParentPage.AddPageScheduleToPlayer(pageNameTextBlock.Text);
            //}
            //else
            //{

            //}
        }

        void PageListElement_MouseLeave(object sender, MouseEventArgs e)
        {
            if (g_IsSelected == false)
            {
                Color c2 = (Color)TypeDescriptor.GetConverter(typeof(Color)).ConvertFromString("#B2296EB2");
                BackRectangle.Fill = new SolidColorBrush(c2);
                //TextBlockPageName.Foreground = new SolidColorBrush(Colors.White);
            }
        }

        void PageListElement_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (g_IsSelected == false)
            {

                //#B272B2F1
                Color c2 = (Color)TypeDescriptor.GetConverter(typeof(Color)).ConvertFromString("#B272B2F1");
                BackRectangle.Fill = new SolidColorBrush(c2);
                //TextBlockPageName.Foreground = new SolidColorBrush(Colors.Blue);
            }
        }

        void PageListElement_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            //parentPage.SelectNoticeElement(this.noticeTitle, this.noticeImageName);    
        }    
    }
}
