using System;
using System.IO;
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
    public partial class ContentInfoElement : UserControl
    {
        public bool g_IsSelected = false;
        public bool g_PreventMouse = false;

        public string noticeTitle = string.Empty;
        public string noticeImageName = string.Empty;

       public ContentsInfoClass g_ContentsInfoClass = new ContentsInfoClass();

        bool g_IsExtended = false;
        public PeriodData sPD = null;
        public bool ShowPeriodInfo { get; set; } = false;
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

        public ContentInfoElement(ContentsInfoClass paramcls)
        {
            InitializeComponent();
            InitEventHandler();
            g_ContentsInfoClass.CopyData(paramcls);
            DisplayThisElementInfo();
            SelectBorder.Visibility = System.Windows.Visibility.Hidden;

            ToolTip tt = new ToolTip();
            tt.Content = g_ContentsInfoClass.CIF_DisplayFileName;
            this.ToolTip = tt;
        }

        public void DisplayThisElementInfo()
        {
            TextBlockPageName.Text = g_ContentsInfoClass.CIF_DisplayFileName;
            TextBlockPageName_Copy.Text = string.Format("{0}:{1}", g_ContentsInfoClass.CIF_PlayMinute, g_ContentsInfoClass.CIF_PlaySec);
            var timeItemsCsv = Application.Current.TryFindResource("TimeItems") as string;
            if (!string.IsNullOrEmpty(timeItemsCsv))
            {
                string minute = g_ContentsInfoClass.CIF_PlayMinute.PadLeft(2, '0');
                string second = g_ContentsInfoClass.CIF_PlaySec.PadLeft(2, '0');
                scrollSpeedComboBox_Copy1.SelectedItem = minute;
                scrollSpeedComboBox_Copy.SelectedItem = second;
            }

            if (this.g_ContentsInfoClass.CIF_ContentType == ContentType.WebSiteURL.ToString())
            {
                TextBlockPageName.Foreground = ColorTools.GetSolidBrushByColorString("#FF1B1B1D");
             
            }
            else
            {
                TextBlockPageName.Foreground = ColorTools.GetSolidBrushByColorString("#FF005245"); 
            }

            sPD = null;
            PeriodTBlock.Visibility = Visibility.Collapsed;
            if (ShowPeriodInfo && Page1.Instance != null && IsFileBasedContent(g_ContentsInfoClass))
            {
                sPD = Page1.Instance.GetPeriodData(g_ContentsInfoClass.CIF_StrGUID);
            }

            if (sPD != null)
            {
                PeriodTBlock.Text = BuildPeriodDisplayText(sPD);
                PeriodTBlock.Visibility = Visibility.Visible;

                if (DateTime.TryParse(sPD.EndDate, out var endDate) && endDate.Date < DateTime.Today)
                {
                    PeriodTBlock.Foreground = ColorTools.GetSolidBrushByColorString("#FFE75A5A");
                }
                else
                {
                    PeriodTBlock.Foreground = ColorTools.GetSolidBrushByColorString("#FF2B6CB0");
                }
            }
        }

        public void InitEventHandler()
        {
            this.PreviewMouseLeftButtonDown += new MouseButtonEventHandler(PageListElement_PreviewMouseLeftButtonDown);
            this.PreviewMouseMove += new MouseEventHandler(PageListElement_PreviewMouseMove);
            this.MouseLeave += new MouseEventHandler(PageListElement_MouseLeave);
            this.PreviewMouseDoubleClick += ContentInfoElement_PreviewMouseDoubleClick;

            BorderBTN_Copy1.Click += BorderBTN_Copy1_Click;
            BorderBTN_Copy.Click += BorderBTN_Copy_Click;

            DeletContents.PreviewMouseLeftButtonUp += new MouseButtonEventHandler(DeletContents_PreviewMouseLeftButtonUp);

            if (this.g_ContentsInfoClass.CIF_ContentType.Equals(ContentType.PDF.ToString()))
            {
                MC_SlideInterval.Visibility = Visibility.Visible;
                MC_SlideInterval.Click += MC_SlideInterval_Click; ;  // PDF 슬라이드 시간 편집
            }

            MC_PreviewContents.Click += MC_PreviewContents_Click;  // 미리보기
            MC_CopyContents.Click += MC_CopyContents_Click;  // 컨텐츠 항목복사
        }

        private void MC_SlideInterval_Click(object sender, RoutedEventArgs e)
        {
            //PDF2SWFWindow pdfwin = new PDF2SWFWindow(this, g_ContentsInfoClass);
            //pdfwin.ShowDialog();

            //double runningTime = g_ParentPage.UpdateNGetTimePDFSWF(g_ContentsInfoClass.CIF_FileFullPath, g_ContentsInfoClass.CIF_ScrollTextSpeedSec);

            //scrollSpeedComboBox_Copy1.SelectedItem = g_ContentsInfoClass.CIF_PlayMinute = string.Format("{0:D2}", (int)(runningTime / 60));
            //scrollSpeedComboBox_Copy.SelectedItem = g_ContentsInfoClass.CIF_PlaySec = string.Format("{0:D2}", (int)(runningTime % 60));
            
            //TextBlockPageName_Copy.Text = string.Format("{0}:{1}", g_ContentsInfoClass.CIF_PlayMinute, g_ContentsInfoClass.CIF_PlaySec);

            //this.g_ParentPage.EditContentsPlayTime(g_ContentsInfoClass);
        }

        void MC_CopyContents_Click(object sender, RoutedEventArgs e)
        {
            ContentsInfoClass tmpContentInfo = new ContentsInfoClass();
            tmpContentInfo.CopyDataWithOutGUID(this.g_ContentsInfoClass);

            Page1.Instance.UpdateContentsListToSelectedElement(tmpContentInfo);
        }

        void MC_PreviewContents_Click(object sender, RoutedEventArgs e)
        {
            PlayContentsPreview();
        }

        void ContentInfoElement_PreviewMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            PlayContentsPreview();
            e.Handled = true;
        }

        public void PlayContentsPreview()
        {
            string viewerPath = FNDTools.GetContentViewerPath();
            if (File.Exists(viewerPath))
            {
                string fpath = g_ContentsInfoClass.CIF_FileFullPath;
                if (g_ContentsInfoClass.CIF_ContentType != ContentType.Browser.ToString()
                    && g_ContentsInfoClass.CIF_ContentType != ContentType.WebSiteURL.ToString())
                {
                    if (string.IsNullOrWhiteSpace(fpath) || File.Exists(fpath) == false)
                    {
                        fpath = FNDTools.GetTargetContentsFilePath(g_ContentsInfoClass.CIF_FileName);
                    }
                }

                string args = string.Format("\"{0}\" {1} 1280 720", fpath, g_ContentsInfoClass.CIF_ContentType);
                ProcessTools.LaunchProcess(viewerPath, false, args);
                return;
            }

            ContentsPreviewWindow tmpWindow = new ContentsPreviewWindow(this.g_ContentsInfoClass);
            tmpWindow.ShowDialog();
        }

        void BorderBTN_Copy_Click(object sender, RoutedEventArgs e)
        {
            g_IsExtended = false;
            this.g_ContentsInfoClass.CIF_PlayMinute = scrollSpeedComboBox_Copy1.SelectedItem.ToString();
            this.g_ContentsInfoClass.CIF_PlaySec = scrollSpeedComboBox_Copy.SelectedItem.ToString();
            this.Height = double.NaN;
            this.MinHeight = 30;

            Page1.Instance.EditContentsPlayTime(g_ContentsInfoClass);
            DisplayThisElementInfo();
            SelectOnColor.Visibility = Visibility.Collapsed;
        }

        void BorderBTN_Copy1_Click(object sender, RoutedEventArgs e)
        {
            EditPeriodWindow editWindow = new EditPeriodWindow(g_ContentsInfoClass);
            editWindow.Owner = Window.GetWindow(this);
            bool? result = editWindow.ShowDialog();
            if (result == true)
            {
                SetPlayTime(editWindow.TargetContent.CIF_PlayMinute, editWindow.TargetContent.CIF_PlaySec);
            }
        }

        private void SetPlayTime(string minute, string sec)
        {
            if (string.IsNullOrWhiteSpace(minute))
            {
                minute = "00";
            }

            if (string.IsNullOrWhiteSpace(sec))
            {
                sec = "00";
            }

            string minuteValue = minute.PadLeft(2, '0');
            string secValue = sec.PadLeft(2, '0');

            g_ContentsInfoClass.CIF_PlayMinute = minuteValue;
            g_ContentsInfoClass.CIF_PlaySec = secValue;

            scrollSpeedComboBox_Copy1.SelectedItem = minuteValue;
            scrollSpeedComboBox_Copy.SelectedItem = secValue;

            Page1.Instance.EditContentsPlayTime(g_ContentsInfoClass);
            DisplayThisElementInfo();
        }

        void DeletContents_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            Page1.Instance.DeleteContentsList(this);
        }

        void PageListElement_MouseLeave(object sender, MouseEventArgs e)
        {
            if (g_PreventMouse)
                return;

            if (g_IsSelected == false)
                SelectBorder.Visibility = System.Windows.Visibility.Hidden;
            
            SelectBorder.Background = new SolidColorBrush(Colors.Transparent);
        }

        void PageListElement_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (g_PreventMouse)
                return;

            if (g_IsSelected == false)
                SelectBorder.Visibility = System.Windows.Visibility.Visible;

            SelectBorder.Background = ColorTools.GetSolidBrushByColorString("#19000000");
        }

        void PageListElement_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (g_PreventMouse)
                return;

            Page1.Instance.SelectedContentInfo(g_ContentsInfoClass, DisplayType.Media);
        }

        private void scrollSpeedComboBox_Copy_DropDownOpened(object sender, System.EventArgs e)
        {
            if (scrollSpeedComboBox_Copy1.SelectedIndex > 0) return;

            string selectedString = scrollSpeedComboBox_Copy.SelectedItem as string;
            if (int.TryParse(selectedString, out int sec) && sec < 5)
            {
                scrollSpeedComboBox_Copy.SelectedItem = "05";
            }
        }

        private void scrollSpeedComboBox_Copy1_DropDownClosed(object sender, System.EventArgs e)
        {
            string selectedString1 = scrollSpeedComboBox_Copy1.SelectedItem as string;
            string selectedString = scrollSpeedComboBox_Copy.SelectedItem as string;

            if (int.TryParse(selectedString1, out int minutes) && minutes == 0)
            {
                if (int.TryParse(selectedString, out int seconds) && seconds < 5)
                {
                    scrollSpeedComboBox_Copy.SelectedItem = "05";
                }
            }
        }    

        private static bool IsFileBasedContent(ContentsInfoClass content)
        {
            if (content == null)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(content.CIF_ContentType))
            {
                return true;
            }

            return !content.CIF_ContentType.Equals(ContentType.WebSiteURL.ToString(), System.StringComparison.OrdinalIgnoreCase)
                && !content.CIF_ContentType.Equals(ContentType.Browser.ToString(), System.StringComparison.OrdinalIgnoreCase);
        }

        private static string BuildPeriodDisplayText(PeriodData period)
        {
            if (period == null)
            {
                return string.Empty;
            }

            bool hasStartDate = string.IsNullOrWhiteSpace(period.StartDate) == false;
            bool hasEndDate = string.IsNullOrWhiteSpace(period.EndDate) == false;
            bool hasTime = string.IsNullOrWhiteSpace(period.StartTime) == false
                && string.IsNullOrWhiteSpace(period.EndTime) == false;

            if (hasTime && !hasStartDate && !hasEndDate)
            {
                return string.Format("(매일 {0} ~ {1})", period.StartTime, period.EndTime);
            }

            string start = hasStartDate ? period.StartDate : DateTime.Today.ToString("yyyy-MM-dd");
            string end = hasEndDate ? period.EndDate : "2099-12-31";

            if (hasTime)
            {
                start = string.Format("{0} {1}", start, period.StartTime);
                end = string.Format("{0} {1}", end, period.EndTime);
            }

            return string.Format("({0} ~ {1})", start, end);
        }
    }
}
