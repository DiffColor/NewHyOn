using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using TurtleTools;

namespace AndoW_Manager
{
    /// <summary>
    /// SelectedContentInfoElement.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class SelectedContentInfoElement : UserControl
    {
        SelectAlreadyExistFileWindow g_ParentPage = null;
        public bool g_IsSelected = false;

        public string noticeTitle = string.Empty;
        public string noticeImageName = string.Empty;

        ContentsInfoClass g_ContentsInfoClass = new ContentsInfoClass();

        bool g_IsExtended = false;

        public SelectedContentInfoElement(SelectAlreadyExistFileWindow paramPage, ContentsInfoClass paramcls)
        {
            InitializeComponent();
            g_ParentPage = paramPage;
            g_ContentsInfoClass.CopyData(paramcls);
            InitEventHandler();
            DisplayThisElementInfo();
            SelectBorder.Visibility = System.Windows.Visibility.Hidden;

            ToolTip tt = new ToolTip();
            tt.Content = g_ContentsInfoClass.CIF_FileName;
            this.ToolTip = tt;
        }

        public void DisplayThisElementInfo()
        {
            TextBlockPageName.Text = g_ContentsInfoClass.CIF_FileName;
            TextBlockPageName_Copy.Text = string.Format("{0}:{1}", g_ContentsInfoClass.CIF_PlayMinute, g_ContentsInfoClass.CIF_PlaySec);
            scrollSpeedComboBox_Copy1.SelectedItem = g_ContentsInfoClass.CIF_PlayMinute.PadLeft(2, '0');
            scrollSpeedComboBox_Copy.SelectedItem = g_ContentsInfoClass.CIF_PlaySec.PadLeft(2, '0');
        }

        public void InitEventHandler()
        {
            this.PreviewMouseLeftButtonDown += new MouseButtonEventHandler(PageListElement_PreviewMouseLeftButtonDown);
            this.PreviewMouseMove += new MouseEventHandler(PageListElement_PreviewMouseMove);
            this.MouseLeave += new MouseEventHandler(PageListElement_MouseLeave);
            this.PreviewMouseDoubleClick += ContentInfoElement_PreviewMouseDoubleClick;

            BorderBTN_Copy1.Click += BorderBTN_Copy1_Click;
            BorderBTN_Copy.Click += BorderBTN_Copy_Click;

            ExitTextBlock.PreviewMouseMove += new MouseEventHandler(ExitTextBlock_PreviewMouseMove);
            ExitTextBlock.MouseLeave += new MouseEventHandler(ExitTextBlock_MouseLeave);
            ExitTextBlock.PreviewMouseLeftButtonUp += new MouseButtonEventHandler(ExitTextBlock_PreviewMouseLeftButtonUp);

            MC_PreviewContents.Click += MC_PreviewContents_Click;  // 미리보기
            MC_CopyContents.Click += MC_CopyContents_Click;  // 컨텐츠 항목복사
        }

        void MC_CopyContents_Click(object sender, RoutedEventArgs e)
        {
            ContentsInfoClass tmpContentInfo = new ContentsInfoClass();
            tmpContentInfo.CopyDataWithOutGUID(this.g_ContentsInfoClass);

        }

        void MC_PreviewContents_Click(object sender, RoutedEventArgs e)
        {
            PlayContentsPreview();
        }

        void ContentInfoElement_PreviewMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {

            PlayContentsPreview();
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
            this.Height = 23;

            this.g_ParentPage.EditContentsPlayTime(g_ContentsInfoClass);
            DisplayThisElementInfo();
        }

        void BorderBTN_Copy1_Click(object sender, RoutedEventArgs e)
        {
            if (g_IsExtended == false)
            {
                this.Height = 46;
                g_IsExtended = true;

                scrollSpeedComboBox_Copy1.SelectedItem = this.g_ContentsInfoClass.CIF_PlayMinute;
                scrollSpeedComboBox_Copy.SelectedItem = this.g_ContentsInfoClass.CIF_PlaySec;
            }
          
        }

        void ExitTextBlock_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            //if (FNDTools.AskDoItThisTask(string.Format("선택한 <{0}>를(을) 삭제하시겠습니까?", TextBlockPageName.Text)) == MessageBoxResult.Yes)
            //{
            //    this.parentPage.DeleteNoticeInfoSelected(noticeTitle, noticeImageName);
            //}

            this.g_ParentPage.DeleteContentsList(this.g_ContentsInfoClass);
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
                //Color c2 = (Color)TypeDescriptor.GetConverter(typeof(Color)).ConvertFromString("#B20080FF");
                //BackRectangle.Fill = new SolidColorBrush(c2);
               // TextBlockPageName.Foreground = new SolidColorBrush(Colors.White);
                SelectBorder.Visibility = System.Windows.Visibility.Hidden;

            }
        }

        void PageListElement_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (g_IsSelected == false)
            {
                //Color c2 = (Color)TypeDescriptor.GetConverter(typeof(Color)).ConvertFromString("#B272B2F1");
                //BackRectangle.Fill = new SolidColorBrush(c2);
               // TextBlockPageName.Foreground = new SolidColorBrush(Colors.Silver);
                SelectBorder.Visibility = System.Windows.Visibility.Visible;
            }
        }

        void PageListElement_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            //this.g_ParentPage.SelectedContentInfo(g_ContentsInfoClass,"Display");    
        }    
    }
}
