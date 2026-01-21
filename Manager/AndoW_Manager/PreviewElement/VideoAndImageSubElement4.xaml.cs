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
using System.IO;

namespace HyonManager
{
    /// <summary>
    /// VideoAndImageSubElement4.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class VideoAndImageSubElement4 : UserControl
    {
        public VideoAndImageSubElement4()
        {
            InitializeComponent();
        }

        public void AllStopContents()
        {
            MediaElementOnMultimedia.LoadedBehavior = MediaState.Manual;
            MediaElementOnMultimedia.Stop();
            MediaElementOnMultimedia.Visibility = Visibility.Hidden;

            WebBrowser1.Navigate(new Uri("http://www.naver.com"));

            WebBrowser1.Visibility = Visibility.Hidden;
            this.ContentGrid.Background = new SolidColorBrush(Colors.Black);
        }

        public void DisplayVideo(string contentPath)
        {
            AllStopContents();

            //BGRectangle.Visibility = Visibility.Hidden;

            MediaElementOnMultimedia.Visibility = Visibility.Visible;
            MediaElementOnMultimedia.LoadedBehavior = MediaState.Manual;
            MediaElementOnMultimedia.Stop();
            MediaElementOnMultimedia.Source = new Uri(contentPath, UriKind.Relative);
            MediaElementOnMultimedia.LoadedBehavior = MediaState.Manual;
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
                    DisplayImage(paramContentPath);
                    break;
                case "Flash":
                    DisplayFlashContents(paramContentPath);
                    break;
                default:
                    break;
            }
        
        }

        public void DisplayFlashContents(string filePath)
        {
            //AllStopContents();

           // WebBrowser1.Visibility = Visibility.Visible;
            string htmlCode = string.Format("<html xmlns=\"http://www.w3.org/1999/xhtml\" xml:lang=\"en\" lang=\"en\">" +
                                   "<body  leftmargin=\"0px\" topmargin=\"0px\" marginwidth=\"0px\" marginheight=\"0px\" scroll=\"no\"><embed src=\"{0}\" allowScriptAccess=\"sameDomain\" width=\"100%\" height=\"100%\" type=\"application/x-shockwave-flash\" /></body></html>", filePath);

            WebBrowser1.NavigateToString(htmlCode);
            
        }

        public void DisplayImage(string contentPath)
        {
            AllStopContents();

            BitmapImage bmpImg = new BitmapImage();
            bmpImg.BeginInit();
            bmpImg.StreamSource = new FileStream(contentPath, FileMode.Open, FileAccess.Read);
            bmpImg.CacheOption = BitmapCacheOption.OnLoad;
            bmpImg.EndInit();
            //clean up the stream to avoid file access exceptions when attempting to delete images
            bmpImg.StreamSource.Dispose();

            ImageBrush myBrush = new ImageBrush();
            myBrush.ImageSource = bmpImg;

            this.ContentGrid.Background = null;
            this.ContentGrid.Background = myBrush;
            myBrush = null;
            bmpImg = null;

            GC.Collect();
        }

        public void FreeResource()
        {
            this.ContentGrid.Background = null;
            MediaElementOnMultimedia.Source = null;
            GC.Collect();
        }
    }
}
