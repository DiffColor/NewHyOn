using System;
using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using TurtleTools;


namespace AndoW_Manager
{
    /// <summary>
    /// Interaction logic for VideoImageElement4.xaml
    /// </summary>
    public partial class VideoImageElement4 : UserControl
    {
        //////////////////////////////////////////////
        // Common
        //public string elementName = string.Empty;
        //public string elementTypeName = string.Empty;
        //public double width = 0;
        //public double height = 0;
        //public double posLeft = 0;
        //public double posTop = 0;
        //public double rotationAngle = 0;
        //public int zindex = 0;
        ContentType prevType = ContentType.None;

        public ElementInfoClass g_ElementInfoClass = new ElementInfoClass();

        public int g_CurrentMediaIndex = 0;
        public int g_ContentListCount = 0;
        
        PagePreviewWindow parentPage = null;

        public bool g_previewToggleState = true;
        public int g_availableMediaCount = 0;
        
        public VideoImageElement4()
        {
            InitializeComponent();
        }

        public VideoImageElement4(PagePreviewWindow main)
        {
            InitializeComponent();

            parentPage = main;
            Unloaded += VideoImageElement4_Unloaded;

        }

        void VideoImageElement4_Unloaded(object sender, RoutedEventArgs e)
        {
            MediaElementOnMultimedia.MediaEnded -= MediaElementOnMultimedia_MediaEnded;
        }

        int tickNum = 0;
        int playTime = 5;
        bool isBusy = true;
        public void Tick()
        {
            if (isBusy)
            {
                return;
            }

            tickNum++;
            
            if (tickNum >= playTime)
            {
                isBusy = true;
                OrderingCanvasBGContents();
            }
        }

        public void StopContentsDisplay()
        {
            g_CurrentMediaIndex = 0; 
            isBusy = false;
            tickNum = 0;
        }

        public void UpdateElementInfoClass(ElementInfoClass paramCls)
        {
            this.g_ElementInfoClass.CopyData(paramCls);

            g_ContentListCount = g_ElementInfoClass.EIF_ContentsInfoClassList.Count;
            
            g_availableMediaCount = g_ContentListCount;
        }

        public void OrderingCanvasBGContents()
        {
            this.Dispatcher.Invoke(DispatcherPriority.Normal, (Action)(() =>
            {

                ContentsInfoClass content = new ContentsInfoClass();

                if (this.g_ElementInfoClass.EIF_ContentsInfoClassList.Count > 0)
                {

                hereWeGo:
                    
                    if (g_CurrentMediaIndex == 1 && g_ContentListCount == 1)
                    {
                        if (prevType != ContentType.Video)
                        {
                            return;
                        }
                    }

                    if (g_CurrentMediaIndex > (g_ContentListCount - 1))
                    {
                        g_CurrentMediaIndex = 0;
                    }

                    if (g_availableMediaCount < 1)
                    {
                        UpdateAvailableMediaCount();
                        return;
                    }

                    content = this.g_ElementInfoClass.EIF_ContentsInfoClassList[g_CurrentMediaIndex];


                    if (!g_previewToggleState)
                    {
                        StopContentsDisplay();
                        return;
                    }

                    int second = Convert.ToInt32(content.CIF_PlaySec);
                    int minute = Convert.ToInt32(content.CIF_PlayMinute);


                    playTime = (minute * 60) + second;

                    ////////////////////////////
                    //  여기서 컨텐츠 재생

                    if (this.g_ElementInfoClass.EIF_ContentsInfoClassList[g_CurrentMediaIndex].CIF_ContentType == ContentType.WebSiteURL.ToString())
                    {
                        PlayContents(this.g_ElementInfoClass.EIF_ContentsInfoClassList[g_CurrentMediaIndex].CIF_ContentType, content.CIF_FileName);
                    }
                    else
                    {
                        string playablePath = FNDTools.GetContentFilePath(content);
                        PlayContents(this.g_ElementInfoClass.EIF_ContentsInfoClassList[g_CurrentMediaIndex].CIF_ContentType, playablePath);
                    }

                    isBusy = false;
                    tickNum = 0;
                    g_CurrentMediaIndex++;
                }
            }));

        }

        public void PlayContents(string paramType, string paramContentPath)
        {
            AllStopContents();

            if (paramType.Equals("WebSiteURL") == false && string.IsNullOrWhiteSpace(paramContentPath))
                return;

            if (paramType.Equals("WebSiteURL") == false && File.Exists(paramContentPath) == false)
                return;

            string _fpath = paramContentPath;

            if (File.Exists(_fpath) == false)
            {
                _fpath = FNDTools.GetTargetContentsFilePath(Path.GetFileName(_fpath));
                if (File.Exists(_fpath) == false)
                    return;
            }

            switch (paramType)
            {
                case "Video":
                    DisplayVideo(paramContentPath);
                    prevType = ContentType.Video;
                    break;
                case "Image":
                    MediaTools.DisplayImage(ContentGrid, paramContentPath, DataShop.Instance.g_ServerSettingsManager.sData.PreserveAspectRatio);
                    prevType = ContentType.Image;
                    break;
                default:
                    break;
            }

        }

        public void DisplayVideo(string contentPath)
        {
            try
            {
                MediaElementOnMultimedia.Visibility = Visibility.Visible;
                MediaElementOnMultimedia.Stretch = DataShop.Instance.g_ServerSettingsManager.sData.PreserveAspectRatio ? Stretch.Uniform : Stretch.Fill;
                MediaElementOnMultimedia.Source = new Uri(contentPath, UriKind.Absolute);
                MediaElementOnMultimedia.Play();

                MediaElementOnMultimedia.MediaEnded += MediaElementOnMultimedia_MediaEnded;
            }
            catch (Exception e)
            {
            }
        }

        void MediaElementOnMultimedia_MediaEnded(object sender, RoutedEventArgs e)
        {
            MediaElementOnMultimedia.Position = TimeSpan.FromSeconds(0);
            MediaElementOnMultimedia.Play();
        }

        public void AllStopContents()
        {
            switch (prevType)
            {
                case ContentType.None:
                    break;

                case ContentType.Image:
                    this.ContentGrid.Background = new SolidColorBrush(Colors.Black);
                    break;

                case ContentType.Video:
                    MediaElementOnMultimedia.Stop();
                    MediaElementOnMultimedia.MediaEnded -= MediaElementOnMultimedia_MediaEnded;
                    MediaElementOnMultimedia.Visibility = Visibility.Hidden;
                    break;

                default:
                    break;

            }
        }

        public bool CheckContentsInfoIsValid(ContentsInfoClass content)
        {
            bool IsValid = false;

            if (content.CIF_FileName == string.Empty ||
                   content.CIF_PlayMinute == string.Empty ||
                   content.CIF_PlaySec == string.Empty)
            {
                IsValid = false;
            }
            else
            {
                IsValid = true;
            }

            if (content.CIF_PlayMinute == "00" && content.CIF_PlaySec == "00")
            {
                IsValid = false;
            }
            else
            {
                IsValid = true;
            }


            try
            {
                string playablePath = FNDTools.GetContentFilePath(content);
                bool existFile = File.Exists(playablePath);

                if (content.CIF_FileExist != existFile)
                {
                    UpdateAvailableMediaCount();
                    IsValid = false;
                }
                else
                {
                    IsValid = true;
                }

                if (!existFile)
                {
                    IsValid = false;
                }
                else
                {
                    IsValid = true;
                }

            }
            catch (Exception ex)
            {
                IsValid = false;
            }

            return IsValid;
        }

        public void HideAllControl()
        {
            MediaElementOnMultimedia.Visibility = Visibility.Hidden;
        }

        public void DisplayCanvasBackGroundImage(int idx)
        {
            HideAllControl();

            if (this.g_ElementInfoClass.EIF_ContentsInfoClassList[idx].CIF_FileName != string.Empty)
            {
                switch (this.g_ElementInfoClass.EIF_ContentsInfoClassList[idx].CIF_ContentType)
                {
                    case "Video":                        
                        MediaElementOnMultimedia.Visibility = Visibility.Visible;
                        MediaElementOnMultimedia.Source =
                            new Uri(FNDTools.GetContentFilePath(this.g_ElementInfoClass.EIF_ContentsInfoClassList[idx]), UriKind.Relative);
                        break;

                    case "Image":
                        MediaTools.DisplayImage(ContentGrid, FNDTools.GetContentFilePath(this.g_ElementInfoClass.EIF_ContentsInfoClassList[idx]));
                        break;

                    default:
                        break;
                }
            }
        }

        public void UpdateAvailableMediaCount()
        {
            g_availableMediaCount = 0;

            foreach (ContentsInfoClass item in this.g_ElementInfoClass.EIF_ContentsInfoClassList)
            {
                if (item.CIF_ValidTime && item.CIF_FileExist)
                {
                    g_availableMediaCount++;
                }
            }
        }
    }
}
