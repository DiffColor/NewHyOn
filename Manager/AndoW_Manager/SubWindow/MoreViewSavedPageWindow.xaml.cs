using System.Collections.Generic;
//using System.Threading.Tasks;
using System.Windows;
using System.Windows.Documents;
using System.IO;
using System;
using TurtleTools;

namespace AndoW_Manager
{
    /// <summary>
    /// NotifyWindow.xaml에 대한 상호 작용 논리
    /// </summary>
     public partial class MoreViewSavedPageWindow : Window
    {
      //  public DesignerCanvas g_DesignerCanvas = null;

        private static MoreViewSavedPageWindow instance = null;
        public static MoreViewSavedPageWindow Instance
        {
            get
            {
                return instance;
            }
        }


        public MoreViewSavedPageWindow()
        {
            InitializeComponent();
            instance = this;
            InitEventHandler();
        }

        public void RefreshSavedPageList()
        {
            List<PageInfoClass> savedPages = DataShop.Instance.g_PageInfoManager.GetAllSavedPages();

            wrapPanelTemplate.Children.Clear();

            if (savedPages.Count == 0)
            {
                return;
            }

            int pageIdx = 1;
            foreach (PageInfoClass pageInfo in savedPages)
            {
                string pageName = pageInfo.PIC_PageName;

                MoreViewSavedPageElement tmpElement = new MoreViewSavedPageElement(this);

                tmpElement.g_PreviewThumbBase64 = pageInfo.PIC_Thumb;
                tmpElement.Width = 232;
                tmpElement.Height = 25;
                tmpElement.Margin = new Thickness(4, 3, 0, 0);

                tmpElement.pageNameTextBlock.Text = pageName;
                tmpElement.pageNameTextBlock_Copy.Text = pageIdx.ToString();
                wrapPanelTemplate.Children.Add(tmpElement);

                pageIdx++;
            }
        }

        public void InitEventHandler()
        {
            BTN0DO_Copy.Click += new RoutedEventHandler(CancelBTN_Click);  //Cancel
            this.Loaded += LoadPageWindow_Loaded;
        }

        void LoadPageWindow_Loaded(object sender, RoutedEventArgs e)
        {
            RefreshSavedPageList();
          
        }

        public void MoreViewSavedPageWindowClose()
        {
            this.Close();
        }
        void CancelBTN_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
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
