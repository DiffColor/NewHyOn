using System;
using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using TurtleTools;

namespace AndoW_Manager
{
    /// <summary>
    /// VideoAndImageSubElement1.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class VideoAndImageSubElement1 : UserControl
    {
        public VideoAndImageSubElement1()
        {
            InitializeComponent();
            Unloaded += VideoAndImageSubElement1_Unloaded;
        }

        void VideoAndImageSubElement1_Unloaded(object sender, RoutedEventArgs e)
        {
            MediaElementOnMultimedia.MediaEnded -= MediaElementOnMultimedia_MediaEnded;            
        }

        public void AllStopContents()
        {
            MediaElementOnMultimedia.Stop();
            MediaElementOnMultimedia.MediaEnded -= MediaElementOnMultimedia_MediaEnded;
            MediaElementOnMultimedia.Visibility = Visibility.Hidden;

            this.ContentGrid.Background = new SolidColorBrush(Colors.Black);
        }

        public void DisplayVideo(string contentPath)
        {
            AllStopContents();

            MediaElementOnMultimedia.Visibility = Visibility.Visible;
            MediaElementOnMultimedia.Stretch = DataShop.Instance.g_ServerSettingsManager.sData.PreserveAspectRatio ? Stretch.Uniform : Stretch.Fill;
            MediaElementOnMultimedia.Source = new Uri(contentPath, UriKind.Relative);
            MediaElementOnMultimedia.Play();
            MediaElementOnMultimedia.MediaEnded += MediaElementOnMultimedia_MediaEnded;
        }

        void MediaElementOnMultimedia_MediaEnded(object sender, RoutedEventArgs e)
        {
            MediaElementOnMultimedia.Position = TimeSpan.FromSeconds(0);
            MediaElementOnMultimedia.Play();
        }


        public void PlayContents(string paramType, string paramContentPath)
        {
            switch (paramType)
            {
                case "Video":
                    DisplayVideo(paramContentPath);
                    break;
                case "Image":
                    MediaTools.DisplayImage(ContentGrid, paramContentPath);
                    break;
                default:
                    break;
            }
        
        }

        public void FreeResource()
        {
            this.ContentGrid.Background = null;
            MediaElementOnMultimedia.Source = null;
            GC.Collect();
        }
    }
}
