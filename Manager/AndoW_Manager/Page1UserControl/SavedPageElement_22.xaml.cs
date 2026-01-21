using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.IO;

using TurtleTools;

namespace AndoW_Manager
{
    /// <summary>
    /// SavedPageElement_22.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class SavedPageElement_22 : UserControl
    {   
        public bool g_IsSelected = false;

        public string g_PreviewImgFilePath = string.Empty;

        public SavedPageElement_22()
        {
            InitializeComponent();
            InitEventHandler();
            SelectBorder.Visibility = System.Windows.Visibility.Hidden;
        }

        public void InitEventHandler()
        {
            //this.PreviewMouseLeftButtonDown += new MouseButtonEventHandler(PageListElement_PreviewMouseLeftButtonDown);
            this.PreviewMouseMove += new MouseEventHandler(PageListElement_PreviewMouseMove);
            this.MouseLeave += new MouseEventHandler(PageListElement_MouseLeave);
            this.PreviewMouseDoubleClick += SavedPageElement_PreviewMouseDoubleClick;

            BorderBTN_Copy.Click += BorderBTN_Copy_Click;

            MC_DeletePage.Click += MC_DeletePage_Click;
        }

        void MC_DeletePage_Click(object sender, RoutedEventArgs e)
        {
            string pageName = pageNameTextBlock.Text;

            if (File.Exists(this.g_PreviewImgFilePath))
            {
                new FileInfo(this.g_PreviewImgFilePath).Delete();
            }

            DataShop.Instance.g_PageInfoManager.DeletePagesByPageName(pageName);
            Page2.Instance.RemovePageAndList(pageName);
            MainWindow.Instance.RefreshSavedPageList();
        }

        void BorderBTN_Copy_Click(object sender, RoutedEventArgs e)
        {
            if (MessageTools.ShowMessageBox(string.Format("페이지 [{0}]을(를) 로드하시겠습니까?", pageNameTextBlock.Text), "예", "아니오") == true)
            {
                Page1.Instance.LoadSelectedPage(pageNameTextBlock.Text);
            }
            else
            {

            }
        }

        void SavedPageElement_PreviewMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (MessageTools.ShowMessageBox(string.Format("페이지 [{0}]을(를) 로드하시겠습니까?", pageNameTextBlock.Text), "예", "아니오") == true)
            {
                Page1.Instance.LoadSelectedPage(pageNameTextBlock.Text);
            }
            else
            { 
            
            }
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
            //parentPage.SelectNoticeElement(this.noticeTitle, this.noticeImageName);    
        }    
    }
}
