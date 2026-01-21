using System;
using System.Windows;
using TurtleTools;

namespace ContentViewer
{
    public enum ContentType { None, Video, Image, Browser, Flash, PPT, HDTV, IPTV, WebSiteURL, PDF }

    /// <summary>
    /// MainWindow.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class MainWindow : Window
    {
        ContentType mType;
        string mPath;
        int mWidth, mHeight;

        public MainWindow()
        {
            InitializeComponent();

            string[] args = Environment.GetCommandLineArgs();
            mPath = args[1];
            mType = (ContentType)Enum.Parse(typeof(ContentType), args[2]);
            mWidth = Convert.ToInt32(args[3]);
            mHeight = Convert.ToInt32(args[4]);
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            ContentGrid.Width = mWidth;
            ContentGrid.Height = mHeight;

            switch(mType)
            {
                case ContentType.Video:
                    DisplayVideo();
                    break;

                case ContentType.Image:
                    DisplayImage();
                    break;
            }
        }

        private void DisplayImage()
        {
            MediaTools.DisplayImage(ImageCtrl, mPath);
            ImageCtrl.Visibility = Visibility.Visible;
        }
        
        private void DisplayVideo()
        {
            try
            {
                MEDisplayElementTransform.Angle = MediaTools.GetVideoRotateAngle(mPath);
                MEDisplayElement.Source = new Uri(mPath);
                MEDisplayElement.MediaEnded += MEDisplayElement_MediaEnded;
                MEDisplayElement.Play();
                MEDisplayElement.Visibility = Visibility.Visible;
            }
            catch (Exception e) { }
        }
        
        private void ExitBtn_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void Rectangle_PreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            this.DragMove();
        }

        private void MEDisplayElement_MediaEnded(object sender, RoutedEventArgs e)
        {
            MEDisplayElement.Position = new TimeSpan(0, 0, 0);
            //MEDisplayElement.Play();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            MEDisplayElement.MediaEnded -= MEDisplayElement_MediaEnded;
            MEDisplayElement.Stop();
        }
    }
}
