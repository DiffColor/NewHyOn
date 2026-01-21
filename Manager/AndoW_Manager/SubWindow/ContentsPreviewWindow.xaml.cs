using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Media;
using TurtleTools;

namespace AndoW_Manager
{
    /// <summary>
    /// NotifyWindow.xaml에 대한 상호 작용 논리
    /// </summary>
     public partial class ContentsPreviewWindow : Window
    {
         ContentsInfoClass g_ContentsInfoClass = new ContentsInfoClass();

         public List<string> videoExtentionSetList = new List<string>();
         public List<string> imageExtentionSetList = new List<string>();

         public ContentsPreviewWindow(ContentsInfoClass paramCls)
        {
            InitializeComponent();
            g_ContentsInfoClass.CopyData(paramCls);
            ContentsNameText.Text = paramCls.CIF_FileName;
            InitExtentionSet();
            InitEventHandler();

            if (MainWindow.Instance.isPortraitEditor)
            {
                Width = 360;
                Height = 700;
            }
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

        public void InitEventHandler()
        {
            BTN0DO_Copy4.Click += new RoutedEventHandler(BTNPagesListNew1_Click);  //OK

            this.Loaded += ContentsPreviewWindow_Loaded;
            Unloaded += ContentsPreviewWindow_Unloaded;
        }

        void ContentsPreviewWindow_Unloaded(object sender, RoutedEventArgs e)
        {
            PreviewMediaElement.MediaEnded -= PreviewMediaElement_MediaEnded;
        }

        public void DisplayWebSiteURL(string paramURL)
        {
            ContentGrid.Background = new SolidColorBrush(Colors.White);
        }

        void ContentsPreviewWindow_Loaded(object sender, RoutedEventArgs e)
        {
            PlayContents();
        }

        public void PlayContents()
        {
            AllStopContents();
            
            if (g_ContentsInfoClass.CIF_ContentType == ContentType.WebSiteURL.ToString())
            {
                DisplayWebSiteURL(g_ContentsInfoClass.CIF_FileName);
                return;
            }

            string contentPath = FNDTools.GetContentFilePath(g_ContentsInfoClass);
            if (File.Exists(contentPath) == false)
                return;

            string fileExtension = new System.IO.FileInfo(contentPath).Extension.ToLowerInvariant();

            if (this.videoExtentionSetList.Contains(fileExtension) == true)
            {
                ImagePreviewRect.Visibility = System.Windows.Visibility.Visible;
                DisplayVideo(contentPath);
            }
            else if (this.imageExtentionSetList.Contains(fileExtension) == true)
            {
                ImagePreviewRect.Visibility = System.Windows.Visibility.Visible;
                MediaTools.DisplayImage(ImagePreviewRect, contentPath);
            }
        }

        public void DisplayVideo(string contentPath)
        {
            PreviewMediaElement.Visibility = Visibility.Visible;
            PreviewMediaElement.Stretch = DataShop.Instance.g_ServerSettingsManager.sData.PreserveAspectRatio ? Stretch.Uniform : Stretch.Fill;
            PreviewMediaElement.Source = new Uri(contentPath, UriKind.Relative);
            PreviewMediaElement.Play();
            PreviewMediaElement.MediaEnded += PreviewMediaElement_MediaEnded;
        }

        public void AllStopContents()
        {
            PreviewMediaElement.Stop();
            PreviewMediaElement.MediaEnded -= PreviewMediaElement_MediaEnded;

            PreviewMediaElement.Visibility = Visibility.Hidden;

            ContentGrid.Background = new SolidColorBrush(Colors.Black);
        }

        void PreviewMediaElement_MediaEnded(object sender, RoutedEventArgs e)
        {
            PreviewMediaElement.Position = TimeSpan.FromSeconds(0);
            PreviewMediaElement.Play();
        }

        void CancelBTN_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }

        void BTNPagesListNew1_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
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
