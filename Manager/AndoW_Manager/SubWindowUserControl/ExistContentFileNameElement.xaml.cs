using System;
using System.Collections.Generic;
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
    public partial class ExistContentFileNameElement : UserControl
    {
        public bool g_IsSelected = false;
        public string g_FileName = string.Empty;
        public string g_FileFullPath = string.Empty;

        ContentsInfoClass g_ContentsInfoClass = new ContentsInfoClass();

        public List<string> videoExtentionSetList = new List<string>();
        public List<string> imageExtentionSetList = new List<string>();

        SelectAlreadyExistFileWindow g_ParentWnd = null;

        public ExistContentFileNameElement(SelectAlreadyExistFileWindow paramWnd, string paramFileName, string paramFullPath)
        {
            InitializeComponent();
            g_ParentWnd = paramWnd;
            g_FileName = paramFileName;
            g_FileFullPath = paramFullPath;
            InitExtentionSet();

            g_ContentsInfoClass.CIF_FileFullPath = paramFullPath;
            g_ContentsInfoClass.CIF_FileName = paramFileName;

            TextBlockPageName.Text = g_FileName;

            InitEventHandler();
            SelectBorder.Visibility = System.Windows.Visibility.Hidden;
        }

        public void InitEventHandler()
        {
            this.PreviewMouseLeftButtonDown += new MouseButtonEventHandler(PageListElement_PreviewMouseLeftButtonDown);
            this.PreviewMouseMove += new MouseEventHandler(PageListElement_PreviewMouseMove);
            this.MouseLeave += new MouseEventHandler(PageListElement_MouseLeave);
            this.MouseDoubleClick += ExistContentFileNameElement_MouseDoubleClick;
            BTN0DO_Copy.Click += BTN0DO_Copy_Click;

            ExitTextBlock.PreviewMouseMove += new MouseEventHandler(ExitTextBlock_PreviewMouseMove);
            ExitTextBlock.MouseLeave += new MouseEventHandler(ExitTextBlock_MouseLeave);
            ExitTextBlock.PreviewMouseLeftButtonUp += new MouseButtonEventHandler(ExitTextBlock_PreviewMouseLeftButtonUp);
        }

        void BTN0DO_Copy_Click(object sender, RoutedEventArgs e)
        {
            ContentsPreviewWindow tmpWindow = new ContentsPreviewWindow(this.g_ContentsInfoClass);
            tmpWindow.ShowDialog();
        }


        public void InitExtentionSet()
        {
            videoExtentionSetList.Clear();
            imageExtentionSetList.Clear();

            ///-----------Video 확장자 설정---------------------///
            videoExtentionSetList.Add(".avi");
            videoExtentionSetList.Add(".mp4");
            videoExtentionSetList.Add(".3gp");
            videoExtentionSetList.Add(".mov");
            videoExtentionSetList.Add(".mpg");
            videoExtentionSetList.Add(".mpeg");
            videoExtentionSetList.Add(".m2ts");
            videoExtentionSetList.Add(".ts");
            videoExtentionSetList.Add(".wmv");
            videoExtentionSetList.Add(".asf");

            ///-----------Image 확장자 설정---------------------///
            imageExtentionSetList.Add(".jpg");
            imageExtentionSetList.Add(".jpeg");
            imageExtentionSetList.Add(".bmp");
            imageExtentionSetList.Add(".png");
            imageExtentionSetList.Add(".gif");
        }

        void ExistContentFileNameElement_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            AddSelectListOfContent();
        }


      
        public void AddSelectListOfContent()
        {
             ContentsInfoClass temInfoClass = new ContentsInfoClass();
             temInfoClass.CIF_FileName = g_ContentsInfoClass.CIF_FileName;
             temInfoClass.CIF_FileFullPath = g_ContentsInfoClass.CIF_FileFullPath;
             string playablePath = FNDTools.GetContentFilePath(temInfoClass);
             temInfoClass.CIF_ContentType = GetContentTypeHyonString(playablePath);

            try
            {
                if (temInfoClass.CIF_ContentType.Equals("Video", StringComparison.CurrentCultureIgnoreCase))
                {
                    TimeSpan _ts = MediaTools.GetVideoDuration(playablePath);

                    temInfoClass.CIF_PlayMinute = string.Format("{0:D2}", _ts.Minutes);
                    temInfoClass.CIF_PlaySec = string.Format("{0:D2}", _ts.Seconds);
                }              
            }
            catch (Exception ex)
            {
                MessageTools.ShowMessageBox("영상은 목록에 추가했지만, 코덱이 맞지 않아 Running Time이 기본으로 설정됩니다.", "확인");
                temInfoClass.CIF_PlayMinute = "00";
                temInfoClass.CIF_PlaySec = "05";
            }

            g_ParentWnd.g_SelectedContentsList.Add(temInfoClass);
            g_ParentWnd.RefreshSelectedContentList();
        }

        public string GetContentTypeHyonString(string filePath)
        {
            string typeStr = ContentType.None.ToString();
            string fileExtension = new System.IO.FileInfo(filePath).Extension.ToLowerInvariant();

            if (videoExtentionSetList.Contains(fileExtension))
            {
                typeStr = ContentType.Video.ToString();
            }
            else if (imageExtentionSetList.Contains(fileExtension))
            {
                typeStr = ContentType.Image.ToString();
            }

            return typeStr;
        }

        void ExitTextBlock_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            //if (FNDTools.AskDoItThisTask(string.Format("선택한 <{0}>를(을) 삭제하시겠습니까?", TextBlockPageName.Text)) == MessageBoxResult.Yes)
            //{
            //    this.parentPage.DeleteNoticeInfoSelected(noticeTitle, noticeImageName);
            //}

            //this.g_ParentPage.DeleteScrollTextList(g_ContentsInfoClass);
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
            //this.g_ParentPage.SelectedContentInfo(g_ContentsInfoClass, "ScrollText");    
        }    
    }
}
