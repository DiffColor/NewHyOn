extern alias USBDetector;

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

using TurtleTools;
using System.Collections.Generic;
using UsbDisk = USBDetector::USB_Detector.UsbDisk;
using UsbDiskCollection = USBDetector::USB_Detector.UsbDiskCollection;
using System.IO;


namespace AndoW_Manager
{
    /// <summary>
    /// ContentInfoElement.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class PageListNameElement : UserControl
    {
        public bool g_IsSelected = false;

        public string noticeTitle = string.Empty;
        public string noticeImageName = string.Empty;

        public  PageListInfoClass g_PageListInfoClass = new PageListInfoClass();

        public PageListNameElement()
        {
            InitializeComponent();
            InitEventHandler();
            SelectBorder.Visibility = System.Windows.Visibility.Hidden;

            ShowAndHideSelectedBorder(false);
        }

        public void InitEventHandler()
        {
            this.PreviewMouseLeftButtonDown += new MouseButtonEventHandler(PageListElement_PreviewMouseLeftButtonDown);
            this.PreviewMouseMove += new MouseEventHandler(PageListElement_PreviewMouseMove);
            this.MouseLeave += new MouseEventHandler(PageListElement_MouseLeave);
            this.PreviewMouseRightButtonDown += PageListNameElement_PreviewMouseRightButtonDown;
            DisplayCotextMenu.Opened += DisplayCotextMenu_Opened;

            ExitTextBlock.PreviewMouseMove += new MouseEventHandler(ExitTextBlock_PreviewMouseMove);
            ExitTextBlock.MouseLeave += new MouseEventHandler(ExitTextBlock_MouseLeave);
            ExitTextBlock.PreviewMouseLeftButtonUp += new MouseButtonEventHandler(ExitTextBlock_PreviewMouseLeftButtonUp);

            MC_CopyPlayList.Click += MC_CopyPlayList_Click;
            MC_CopyToUSB.Click += MC_CopyToUSB_Click;
            MC_CleanUSB.Click += MC_CleanUSB_Click;

            BTN0DO_Copy1.Click += BTN0DO_Copy1_Click;
        }

        void MC_CleanUSB_Click(object sender, RoutedEventArgs e)
        {
            UsbDiskCollection usbcol = Page2.Instance.g_USBManager.GetAvailableDisks();

            if (usbcol.Count < 1)
                MessageTools.ShowMessageBox("USB 드라이브가 없습니다.");

            List<string> usblist = new List<string>();
            foreach(UsbDisk usb in usbcol) 
            {
                string usbname = usb.Name.Replace(":", "");
                string usbpath = FNDTools.GetUSBRootPath(usbname);
                if (Directory.Exists(usbpath))
                    Directory.Delete(usbpath, true);
            }
        }

        void PageListNameElement_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            UpdateUsbMenuVisibility();
        }

        void DisplayCotextMenu_Opened(object sender, RoutedEventArgs e)
        {
            UpdateUsbMenuVisibility();
        }

        void MC_CopyToUSB_Click(object sender, RoutedEventArgs e)
        {
            if (Page2.Instance == null)
            {
                return;
            }

            Page2.Instance.ExportPlaylistToUsb(this.g_PageListInfoClass.PLI_PageListName);
        }

        void BTN0DO_Copy1_Click(object sender, RoutedEventArgs e)
        {
            EditPlayListNameWindow tmpWnd = new EditPlayListNameWindow(this);
            tmpWnd.ShowDialog();
        }

        void MC_CopyPlayList_Click(object sender, RoutedEventArgs e)
        {
            Page2.Instance.CopyPlayList(TextBlockPageName.Text);    
        }

        public void UpdateDataInfo(PageListInfoClass paramCls)
        {
            g_PageListInfoClass.CopyData(paramCls);
            DisplayDataInfo();
        }

        private void UpdateUsbMenuVisibility()
        {
            bool hasUsb = Page2.Instance != null && Page2.Instance.HasAvailableUsb();
            Visibility visibility = hasUsb ? Visibility.Visible : Visibility.Collapsed;

            MC_CopyToUSB.Visibility = visibility;
            MC_CleanUSB.Visibility = visibility;
            Sep1.Visibility = visibility;
            Sep2.Visibility = visibility;
        }

        public void DisplayDataInfo()
        {
            TextBlockPageName.Text = g_PageListInfoClass.PLI_PageListName;
            TextBlockPageName_Copy.Text = string.Format("({0})", g_PageListInfoClass.PLI_CreateTimeStr);
        }

        public void ShowAndHideSelectedBorder(bool IsShow)
        {
            if (IsShow == true)
            {
                SelectBorder_Copy.Visibility = System.Windows.Visibility.Visible;
            }
            else
            {
                SelectBorder_Copy.Visibility = System.Windows.Visibility.Hidden;
            }
        }

        void ExitTextBlock_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            Page2.Instance.DeleteThePageListName(this.g_PageListInfoClass);
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
            Page2.Instance.SelectPageList(TextBlockPageName.Text);    
        }    
    }
}
