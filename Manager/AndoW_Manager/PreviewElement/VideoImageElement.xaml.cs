using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.IO;
using TurtleTools;


namespace AndoW_Manager
{
    /// <summary>
    /// Interaction logic for VideoImageElement.xaml
    /// </summary>
    public partial class VideoImageElement : UserControl
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


        public ElementInfoClass g_ElementInfoClass = new ElementInfoClass();

        public int g_CurrentMediaIndex = 0;
        public int g_ContentListCount = 0;


        //public List<ContentsInfoClass> g_ContentsList = new List<ContentsInfoClass>();

        DispatcherTimer dspBGtimer = new DispatcherTimer();

        Page1 parentPage = null;

        VideoAndImageSubElement1 subElement1 = new VideoAndImageSubElement1();
        VideoAndImageSubElement1 subElement2 = new VideoAndImageSubElement1();

        public string g_EffectStyle = "Fade";
        public string g_PrevStyle = "Fade";

        public bool g_previewToggleState = true;
        public int g_availableMediaCount = 0;

        public void SetPreviewToggleState(bool state)
        {
            g_previewToggleState = state;
        }

        public bool GetPreviewToggleState()
        {
            return g_previewToggleState;
        }

        public VideoImageElement()
        {
            InitializeComponent();
            initTimer();

            pageTransitionControl.ShowPage(subElement1);
        }

        public VideoImageElement(Page1 main)
        {
            InitializeComponent();

            parentPage = main;
            initTimer();
        }

        public void PlayContentDisplay()
        {
            OrderingCanvasBGContents();
        }

        public void StopAndStartTimer(bool IsStop)
        {
            if (IsStop == true)
            {
                dspBGtimer.Stop();
            }
            else
            {
                dspBGtimer.Start();
            }
        }

        public void StopContentsDisplay()
        {
            dspBGtimer.Stop();

            g_CurrentMediaIndex = 0;

            if (MediaElementOnMultimedia != null)
            {
                MediaElementOnMultimedia.Stop();
                subElement1.AllStopContents();
                subElement2.AllStopContents();
            }
        }

        public void initTimer()
        {
            dspBGtimer.Tick += new EventHandler(dspBGtiemr_Tick);
        }

        void dspBGtiemr_Tick(object sender, EventArgs e)
        {
            OrderingCanvasBGContents();
        }

        public void UpdateElementInfoClass(ElementInfoClass paramCls)
        {
            this.g_ElementInfoClass.CopyData(paramCls);
            g_ContentListCount = g_ElementInfoClass.EIF_ContentsInfoClassList.Count;
            int playableCount = 0;

            foreach (ContentsInfoClass item in g_ElementInfoClass.EIF_ContentsInfoClassList)
            {
                if (item.CIF_ValidTime == true && item.CIF_FileExist == true)
                {
                    playableCount++;
                }
            }

           g_availableMediaCount = playableCount;
        }

       
        public void PlayContents()
        { /*
            StopPlayVideo();
            playManagerTimer.Stop();

            if (g_ContentsInfoClassList.Count > 0)
            {
            HereWeGo:

                if (g_CurIdx == g_ContentsInfoClassList.Count)
                {
                    g_CurIdx = 0;
                }

                if (g_ContentsInfoClassList[g_CurIdx].CIF_PlayMinute == "00" &&
                    g_ContentsInfoClassList[g_CurIdx].CIF_PlaySec == "00")
                {
                    g_CurIdx++;
                    goto HereWeGo;
                }

                if (g_ContentsInfoClassList[g_CurIdx].CIF_PlayMinute == string.Empty ||
                    g_ContentsInfoClassList[g_CurIdx].CIF_PlaySec == string.Empty)
                {
                    g_CurIdx++;
                    goto HereWeGo;
                }

                try
                {
                    if (g_ContentsInfoClassList[g_CurIdx].CIF_ContentType == "Image")
                    {
                        LocationImageRect.Visibility = System.Windows.Visibility.Visible;
                        PreviewMediaElement.Visibility = System.Windows.Visibility.Hidden;

                        SetLocationImage(FNDTools.GetADContentsTargetPath(g_ContentsInfoClassList[g_CurIdx].CIF_FileName),
                            LocationImageRect);
                    }
                    else
                    {
                        LocationImageRect.Visibility = System.Windows.Visibility.Hidden;
                        PreviewMediaElement.Visibility = System.Windows.Visibility.Visible;
                        //  g_FileFullPathForVideo = FNDTools.GetADContentsTargetPath(paramCls.CIF_FileName);
                        DisplayVideo(FNDTools.GetADContentsTargetPath(g_ContentsInfoClassList[g_CurIdx].CIF_FileName));
                    }

                    playManagerTimer.Interval = new TimeSpan(0, Int32.Parse(g_ContentsInfoClassList[g_CurIdx].CIF_PlayMinute),
                                                                Int32.Parse(g_ContentsInfoClassList[g_CurIdx].CIF_PlaySec));
                    g_CurIdx++;
                    playManagerTimer.Start();
                }
                catch (Exception ex)
                {
                    goto HereWeGo;
                }
            }
            else
            {

            }
         */
        }


        public void DisplayComparePics(bool IsNext)
        {
            /*
            ImageTransSlider_Copy.Value = 1;
            ImageTransSlider1_Copy.Value = 0;

            if (g_IsAutoMode == true)
            {
                Storyboard sbdLabelRotation = (Storyboard)FindResource("morphin001");

                sbdLabelRotation.Stop();
                sbdLabelRotation.Begin();
            }
            
            if (ILYCODEDataShop.Instance.g_ComparePicInfoManager.g_DataClassList.Count > 0)
            {
                this.ShowSelectedComparePicture(ILYCODEDataShop.Instance.g_ComparePicInfoManager.g_DataClassList[g_ComparePicIdx]);
                RefreshSurgeryTypeNameList(ILYCODEDataShop.Instance.g_ComparePicInfoManager.g_DataClassList[g_ComparePicIdx]);
                transValTextBlk1_Copy3.Text = ILYCODEDataShop.Instance.g_ComparePicInfoManager.g_DataClassList[g_ComparePicIdx].AIF_Name;



                if (IsNext == true)  // Next
                {
                    g_ComparePicIdx++;

                    if (ILYCODEDataShop.Instance.g_ComparePicInfoManager.g_DataClassList.Count == g_ComparePicIdx)
                    {
                        g_ComparePicIdx = 0;
                    }

                }
                else
                {
                    g_ComparePicIdx--;

                    if (g_ComparePicIdx < 0)
                    {
                        g_ComparePicIdx = ILYCODEDataShop.Instance.g_ComparePicInfoManager.g_DataClassList.Count - 1;
                    }
                }
            }
            */

        }
        public void OrderingCanvasBGContents()
        {
            MultimediaPath.Visibility = Visibility.Hidden;
            dspBGtimer.Stop();

            ContentsInfoClass content = new ContentsInfoClass();

            if (this.g_ElementInfoClass.EIF_ContentsInfoClassList.Count > 0)
            {

            hereWeGo:

                if (g_CurrentMediaIndex == 1 && g_ContentListCount == 1)
                {
                    if (!MediaElementOnMultimedia.IsVisible)
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

                    dspBGtimer.Interval = new TimeSpan(0, 0, 1);

                    if (subElement1.IsVisible)
                    {
                        subElement1.AllStopContents();
                    }
                    else if (subElement2.IsVisible)
                    {
                        subElement2.AllStopContents();
                    }

                    MultimediaPath.Visibility = Visibility.Visible;
                    dspBGtimer.Start();
                    return;
                }

                content = this.g_ElementInfoClass.EIF_ContentsInfoClassList[g_CurrentMediaIndex];

                if (CheckContentsInfoIsValid(this.g_ElementInfoClass.EIF_ContentsInfoClassList[g_CurrentMediaIndex]) == false)
                {
                    g_CurrentMediaIndex++;
                    goto hereWeGo;
                }

                if (!g_previewToggleState)
                {
                    StopContentsDisplay();
                    return;
                }

                int second = Convert.ToInt32(content.CIF_PlaySec);
                int minute = Convert.ToInt32(content.CIF_PlayMinute);

                dspBGtimer.Interval = new TimeSpan(0, minute, second);

                pageTransitionControl.TransitionType = WpfPageTransitions.PageTransitionType.Grow;

                if (g_EffectStyle.Equals("None", StringComparison.CurrentCultureIgnoreCase))
                {
                    if (this.pageTransitionControl.Visibility == Visibility.Visible)
                    {
                        this.pageTransitionControl.Visibility = Visibility.Hidden;
                        this.ContentGrid.Background = new SolidColorBrush(Colors.Black);
                    }

                    DisplayCanvasBackGroundImage(g_CurrentMediaIndex);
                }
                else
                {
                    if (this.ContentGrid.Background != null)
                    {
                        this.ContentGrid.Background = null;
                    }

                    if (this.pageTransitionControl.Visibility == Visibility.Hidden)
                    {
                        this.pageTransitionControl.Visibility = Visibility.Visible;
                    }


                    SetPageTransitionEffectStyle(g_EffectStyle);

                    // 이미지 전환 효과때문에 넣은거임
                    if (subElement2.IsVisible)
                    {
                        string playablePath = FNDTools.GetContentFilePath(content);
                        subElement1.PlayContents(this.g_ElementInfoClass.EIF_ContentsInfoClassList[g_CurrentMediaIndex].CIF_ContentType, playablePath);
                        pageTransitionControl.ShowPage(subElement1);
                    }
                    else
                    {
                        string playablePath = FNDTools.GetContentFilePath(content);
                        subElement2.PlayContents(this.g_ElementInfoClass.EIF_ContentsInfoClassList[g_CurrentMediaIndex].CIF_ContentType, playablePath);
                        pageTransitionControl.ShowPage(subElement2);
                    }
                }

                dspBGtimer.Start();
                g_CurrentMediaIndex++;
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

        public void SetPageTransitionEffectStyle(string paramStyle)
        {
            switch (paramStyle)
            {
                case "Fade":
                    this.pageTransitionControl.TransitionType = WpfPageTransitions.PageTransitionType.Fade;
                    break;
                case "Slide":
                    this.pageTransitionControl.TransitionType = WpfPageTransitions.PageTransitionType.Slide;
                    break;
                case "Slide&Fade":
                    this.pageTransitionControl.TransitionType = WpfPageTransitions.PageTransitionType.SlideAndFade;
                    break;
                case "Grow":
                    this.pageTransitionControl.TransitionType = WpfPageTransitions.PageTransitionType.Grow;
                    break;
                case "Grow&Fade":
                    this.pageTransitionControl.TransitionType = WpfPageTransitions.PageTransitionType.GrowAndFade;
                    break;
                case "Flip":
                    this.pageTransitionControl.TransitionType = WpfPageTransitions.PageTransitionType.Flip;
                    break;
                case "Flip&Fade":
                    this.pageTransitionControl.TransitionType = WpfPageTransitions.PageTransitionType.FlipAndFade;
                    break;
                case "Spin":
                    this.pageTransitionControl.TransitionType = WpfPageTransitions.PageTransitionType.Spin;
                    break;
                case "Spin&Fade":
                    this.pageTransitionControl.TransitionType = WpfPageTransitions.PageTransitionType.SpinAndFade;
                    break;
                default:
                    break;
            }
        }


        public void HideAllControl()
        {
            MediaElementOnMultimedia.Stop();
            MediaElementOnMultimedia.MediaEnded -= MediaElementOnMultimedia_MediaEnded;
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
                        MediaElementOnMultimedia.Stretch = DataShop.Instance.g_ServerSettingsManager.sData.PreserveAspectRatio ? Stretch.Uniform : Stretch.Fill;
                        MediaElementOnMultimedia.Source =
                            new Uri(FNDTools.GetContentFilePath(this.g_ElementInfoClass.EIF_ContentsInfoClassList[idx]), UriKind.Relative);
                        MediaElementOnMultimedia.Play();
                        MediaElementOnMultimedia.MediaEnded += MediaElementOnMultimedia_MediaEnded;
                        break;

                    case "Image":                        
                        MediaTools.DisplayImage(ContentGrid, FNDTools.GetContentFilePath(this.g_ElementInfoClass.EIF_ContentsInfoClassList[idx]));
                        break;

                    default:
                        break;
                }
            }
        }

        void MediaElementOnMultimedia_MediaEnded(object sender, RoutedEventArgs e)
        {
            MediaElementOnMultimedia.Position = TimeSpan.FromSeconds(0);
            MediaElementOnMultimedia.Play();
        }

        public void UpdateAvailableMediaCount()
        {
            g_availableMediaCount = 0;

            foreach (ContentsInfoClass item in this.g_ElementInfoClass.EIF_ContentsInfoClassList)
            {
                //if ((item.CIF_PlayMinute).Equals("00") && (item.CIF_PlaySec).Equals("00"))
                //{
                //    item.CIF_ValidTime = false;
                //}
                //else
                //{
                //    item.CIF_ValidTime = true;
                //}

                //if (File.Exists(item.CIF_FileFullPath).Exists == false)
                //{
                //    item.CIF_FileExist = false;
                //}
                //else
                //{
                //    item.CIF_FileExist = true;
                //}

                if (item.CIF_ValidTime && item.CIF_FileExist)
                {
                    g_availableMediaCount++;
                }
            }
        }

        public int GetAvailableMediaCount()
        {
            int count = 0;
            foreach (ContentsInfoClass item in this.g_ElementInfoClass.EIF_ContentsInfoClassList)
            {
                if (item.CIF_ValidTime && item.CIF_FileExist)
                {
                    count++;
                }
            }
            return count;
        }

        public void SetAvailableMediaCount(int count)
        {
            g_availableMediaCount = count;
        }

        public void FreeResource()
        {
            subElement1.AllStopContents();
            subElement2.AllStopContents();
            subElement1.FreeResource();
            subElement2.FreeResource();
            this.ContentGrid.Background = null;
            GC.Collect();
        }

        private void UserControl_Unloaded(object sender, RoutedEventArgs e)
        {
            HideAllControl();
            FreeResource();
        }
    }
}
