using System;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using TurtleMediaControl;
using TurtleTools;
using SharedContentsInfoClass = AndoW.Shared.ContentsInfoClass;

namespace HyOnPlayer
{
    /// <summary>
    /// ContentsPlayWindow.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class ContentsPlayWindow : Window
    {
        public ContentType prevType = ContentType.None;

        public ElementInfoClass g_ElementInfoClass = new ElementInfoClass();
        ExeControl exeControl;

        public int g_CurrentMediaIndex = 0;
        public int g_ContentListCount = 0;
        public int g_availableMediaCount = 0;
        public long tickNum = 0;
        public long playTime = 5;
        public bool isBusy = true;
        public double g_TransformX = 1;
        public double g_TransformY = 1;

        Size g_origSize = new Size(0, 0);
        Point g_origPos = new Point(0, 0);

        bool keep_ratio = true;
        bool needLoop = false;
        long video_duration = 10;
        string current_fpath = string.Empty;
     
        public ContentsPlayWindow(MainWindow paramWnd)
        {
            InitializeComponent();
            InitEventHandler();

            keep_ratio = MainWindow.Instance.g_TTPlayerInfoManager.g_PlayerInfo.TTInfo_Data1.Equals("YES", StringComparison.CurrentCultureIgnoreCase);

            MPVPlayer.Stretch = keep_ratio ? Stretch.Uniform : Stretch.Fill;
            TransImgCtrl.AspectRatioMode = keep_ratio ? ASPECT_RATIO_TYPES.Maintain : ASPECT_RATIO_TYPES.DependOnOwner;
            InitChangeEffect();
        }

        public void InitEventHandler()
        {
            this.Closing += ContentsPlayWindow_Closing;
        }

        HwndSource me = null;
        double longSide = 0.0;
        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            longSide = e.NewSize.Width > e.NewSize.Height ? e.NewSize.Width : e.NewSize.Height;
        }

        void ContentsPlayWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            StopContentsDisplay();
            StopVisibleContents();
        }
        
        public void Tick()
        {
            if (this.g_ElementInfoClass.EIF_ContentsInfoClassList.Count > 0)
            {
                if (isBusy)
                    return;

                bool syncHold = MainWindow.Instance != null && MainWindow.Instance.ShouldHoldForSyncContent;

                if (!syncHold && prevType == ContentType.Video && g_ContentListCount > 1)
                {
                    if (playTime - tickNum < video_duration)
                    {
                        tickNum = 0;
                        isBusy = true;
                        SetLoopState(false);
                        return;
                    }
                }

                if (syncHold)
                {
                    if (tickNum >= playTime)
                    {
                        tickNum = playTime;
                        if (prevType == ContentType.Video)
                        {
                            SetLoopState(true);
                        }
                        return;
                    }
                }
                else if (tickNum >= playTime)
                {
                    tickNum = 0;
                    isBusy = true;
                    OrderingCanvasBGContents();
                    tickNum = 0;
                }

                tickNum++;
            }
        }

        public void SetLoopState(bool loop)
        {
            MPVPlayer.Loop = needLoop = loop;
        }

        public void OrderingCanvasBGContents()
        {
            SharedContentsInfoClass content = new SharedContentsInfoClass();

            if (this.g_ElementInfoClass.EIF_ContentsInfoClassList.Count < 1)
            {
                Thread.Sleep(250);
                return;
            }

            hereWeGo:

                if (g_CurrentMediaIndex == 1 && g_ContentListCount == 1)
                    return;

                if (g_CurrentMediaIndex > (g_ContentListCount - 1))
                    g_CurrentMediaIndex = 0;

                if (g_availableMediaCount < 1)
                    return;

                content = this.g_ElementInfoClass.EIF_ContentsInfoClassList[g_CurrentMediaIndex];

                if (CheckContentsInfoIsValid(this.g_ElementInfoClass.EIF_ContentsInfoClassList[g_CurrentMediaIndex]) == false)
                {
                    g_CurrentMediaIndex++;
                    goto hereWeGo;
                }

                //if (MainWindow.Instance.sPeriodDics.TryGetValue(content.CIF_FileName, out PeriodData _pdata))
                //{
                //    DateTime _dt = DateTime.Now;
                //    int _now = Convert.ToInt32(_dt.ToString("yyyyMMdd"));
                //    int _start = Convert.ToInt32(ConvertTools.ParseNumbers(_pdata.StartDate));
                //    int _end = Convert.ToInt32(ConvertTools.ParseNumbers(_pdata.EndDate));

                //    if (string.IsNullOrEmpty(_pdata.StartTime) || string.IsNullOrEmpty(_pdata.EndTime))
                //    {
                //        if (_start > _now || _end < _now)
                //        {
                //            g_CurrentMediaIndex++;
                //            goto hereWeGo;
                //        }
                //    }
                //    else
                //    {
                //        int _nowtime = Convert.ToInt32(_dt.ToString("HHmm"));
                //        int _starttime = Convert.ToInt32(ConvertTools.ParseNumbers(_pdata.StartTime));
                //        int _endtime = Convert.ToInt32(ConvertTools.ParseNumbers(_pdata.EndTime));

                //        if ((_start > _now || _end < _now) || (_starttime > _nowtime || _endtime < _nowtime))
                //        {
                //            g_CurrentMediaIndex++;
                //            goto hereWeGo;
                //        }
                //    }
                //}

                int second = Convert.ToInt32(content.CIF_PlaySec);
                int minute = Convert.ToInt32(content.CIF_PlayMinute);

                playTime = (minute * 60) + second;

                PlayContents(content);

                isBusy = false;
                g_CurrentMediaIndex++;
        }

        //public ContentsInfoClass GetValidPeriodContentInfo(IEnumerable<ContentsInfoClass> list)
        //{
        //    ContentsInfoClass _next_cic = null;

        //    foreach (ContentsInfoClass _cic in list)
        //    {
        //        if (MainWindow.Instance.sPeriodDics.TryGetValue(_cic.CIF_FileName, out PeriodData _pdata))
        //        {
        //            DateTime _dt = DateTime.Now;
        //            int _now = Convert.ToInt32(_dt.ToString("yyyyMMdd"));
        //            int _start = Convert.ToInt32(ConvertTools.ParseNumbers(_pdata.StartDate));
        //            int _end = Convert.ToInt32(ConvertTools.ParseNumbers(_pdata.EndDate));

        //            if(string.IsNullOrEmpty(_pdata.StartTime) || string.IsNullOrEmpty(_pdata.EndTime))
        //            {
        //                if (_start > _now || _end < _now)
        //                {
        //                    _next_cic = _cic;
        //                    break;
        //                }
        //            } else
        //            {
        //                int _nowtime = Convert.ToInt32(_dt.ToString("HHmm"));
        //                int _starttime = Convert.ToInt32(ConvertTools.ParseNumbers(_pdata.StartTime));
        //                int _endtime = Convert.ToInt32(ConvertTools.ParseNumbers(_pdata.EndTime));

        //                if ((_start > _now || _end < _now) || (_starttime > _nowtime || _endtime < _nowtime))
        //                {
        //                    _next_cic = _cic;
        //                    break;
        //                }
        //            }
        //        }
        //        else
        //        {
        //            _next_cic = _cic;
        //            break;
        //        }
        //    }

        //    return _next_cic;
        //}

        public bool CheckContentsInfoIsValid(SharedContentsInfoClass content)
        {
            bool IsValid = true;

            if (content.CIF_FileName == string.Empty ||
                   content.CIF_PlayMinute == string.Empty ||
                   content.CIF_PlaySec == string.Empty)
            {
                IsValid = false;
            }

            if (content.CIF_PlayMinute == "00" && content.CIF_PlaySec == "00")
            {
                IsValid = false;
            }

            try
            {
                string fpath = FNDTools.GetContentsFilePath(content.CIF_FileName);

                bool existFile = new FileInfo(fpath).Exists;

                if (!existFile)
                {
                    IsValid = false;
                }

                if (new FileInfo(fpath).Length == 0)  // 파일사이즈가 0일때도 재생안한다.
                {
                    IsValid = false;
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
            MPVPlayer.Visibility = Visibility.Hidden;
            TransImgCtrl.Visibility = Visibility.Hidden;
        }

        public void PlayContents(SharedContentsInfoClass cic)
        {
            current_fpath = FNDTools.GetContentsFilePath(cic.CIF_FileName);

            //StopVisibleContents(cic.CIF_ContentType == ContentType.Image.ToString());

            switch ((ContentType)Enum.Parse(typeof(ContentType), cic.CIF_ContentType))
            {
                case ContentType.Video:
                    DisplayVideo(current_fpath);
                    prevType = ContentType.Video;
                    break;

                case ContentType.Image:
                    DisplayImage(current_fpath);
                    prevType = ContentType.Image;
                    break;

                default:
                    break;
            }
        }

        public void KeepWindowUIData(double w, double h, double x, double y)
        {
            g_origSize.Width = this.ActualWidth;
            g_origSize.Height = this.ActualHeight;
            g_origPos.X = this.Left;
            g_origPos.Y = this.Top;
        }

        public void RestoreWindowUIData()
        {
            this.Width = g_origSize.Width;
            this.Height = g_origSize.Height;
            this.Left = g_origPos.X;
            this.Top = g_origPos.Y;
        }

        Size GetActualSize(FrameworkElement control)
        {
            control.UpdateLayout();
            Size startSize = new Size(control.ActualWidth, control.ActualHeight);

           
            startSize.Width *= g_TransformX;
            startSize.Height *= g_TransformY;
            return startSize;
        }

        public void DisplayImage(string contentPath)
        {
            if (File.Exists(contentPath))
            {
                try
                {
                    if (prevType == ContentType.Video)
                    {
                        TransImgCtrl.DirectSource = contentPath;
                        StopVideo();
                        TransImgCtrl.Visibility = Visibility.Visible;
                        MPVPlayer.Visibility = Visibility.Hidden;
                    }
                    else
                    {
                        TransImgCtrl.ChangeNowSource = contentPath;
                        if(!TransImgCtrl.IsVisible)
                            TransImgCtrl.Visibility = Visibility.Visible;
                    }
                }
                catch (Exception ex)
                {
                    Logger.WriteErrorLog(string.Format("@DisplayImage __Player. {0}", ex.ToString()), Logger.GetLogFileName());
                }
            }
        }
        
        public void DisplayVideo(string contentPath)
        {
            try
            {
                int angle = MediaTools.GetVideoRotateAngle(contentPath);
                video_duration = (long)MediaTools.GetVideoDuration(contentPath).TotalSeconds;
                needLoop = (playTime > video_duration || g_ContentListCount < 2);

                if (angle > 0)
                    MPVPlayer.LayoutTransform = new RotateTransform(angle);

                MPVPlayer.Load(contentPath);
                MPVPlayer.Loop = needLoop;
            }
            catch (Exception ex)
            {
                Logger.WriteLog(ex.ToString(), Logger.GetLogFileName());
            }
        }

        public void StopContentsDisplay()
        {
            this.g_ElementInfoClass.EIF_ContentsInfoClassList.Clear();
            g_CurrentMediaIndex = 0;
            isBusy = false;
            tickNum = 0;
        }


        //private void KillExe()
        //{
        //    try
        //    {
        //        //if (pDocked != null) pDocked.Kill();

        //        //if (exeWin != null) exeWin.Close();
        //    }
        //    catch (Exception e)
        //    {

        //    }
        //    finally
        //    {
        //        //hWndDocked = IntPtr.Zero;
        //    }
        //}



        public void StopVisibleContents(bool prepareNext = false)
        {
            this.Dispatcher.Invoke(DispatcherPriority.Normal, (Action)(() =>
            {
                switch (prevType)
                {
                    case ContentType.None:
                        break;

                    case ContentType.Image:
                        if (prepareNext)
                            return;
                        ReleaseImage();
                        break;

                    case ContentType.Video:
                        StopVideo();
                        break;

                    default:
                        break;
                }
            }));
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

        private void StopVideo()
        {
            MPVPlayer.Stop();
            MPVPlayer.LayoutTransform = new RotateTransform(0);
        }

        private void ReleaseImage(string thumb_fpath = "")
        {
            TransImgCtrl.Visibility = Visibility.Hidden;
            if (string.IsNullOrEmpty(thumb_fpath))
                TransImgCtrl.ChangeNowSource = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "one_black_pixel.png");
            else
                TransImgCtrl.DirectChangeNowSource(MediaTools.GetVideoThumb(thumb_fpath, video_duration-1));
        }

        public void UpdateElementInfoClass(ElementInfoClass paramCls)
        {
            this.g_ElementInfoClass.CopyData(paramCls);
            g_ContentListCount = g_ElementInfoClass.EIF_ContentsInfoClassList.Count;
            int playableCount = 0;

            foreach (SharedContentsInfoClass item in g_ElementInfoClass.EIF_ContentsInfoClassList)
            {
                if (item.CIF_ValidTime == true && item.CIF_FileExist == true)
                {
                    playableCount++;
                }
            }

            g_availableMediaCount = playableCount;
        }

        public SharedContentsInfoClass GetCurrentContent()
        {
            int idx = GetCurrentContentIndex();
            if (idx < 0 || idx >= g_ElementInfoClass.EIF_ContentsInfoClassList.Count)
            {
                return null;
            }

            return g_ElementInfoClass.EIF_ContentsInfoClassList[idx];
        }

        public SharedContentsInfoClass GetNextContent()
        {
            if (g_ContentListCount == 0 || g_ElementInfoClass.EIF_ContentsInfoClassList.Count == 0)
            {
                return null;
            }

            int idx = g_CurrentMediaIndex;
            if (idx >= g_ElementInfoClass.EIF_ContentsInfoClassList.Count)
            {
                idx = 0;
            }

            return g_ElementInfoClass.EIF_ContentsInfoClassList[idx];
        }

        private int GetCurrentContentIndex()
        {
            if (g_ContentListCount == 0 || g_ElementInfoClass.EIF_ContentsInfoClassList.Count == 0)
            {
                return -1;
            }

            int idx = g_CurrentMediaIndex - 1;
            if (idx < 0)
            {
                idx = g_ContentListCount - 1;
            }
            if (idx >= g_ElementInfoClass.EIF_ContentsInfoClassList.Count)
            {
                idx = g_ElementInfoClass.EIF_ContentsInfoClassList.Count - 1;
            }

            return idx;
        }

        public int CurrentContentIndex => GetCurrentContentIndex();

        public int GetNextContentIndex()
        {
            if (g_ContentListCount == 0 || g_ElementInfoClass.EIF_ContentsInfoClassList.Count == 0)
            {
                return -1;
            }

            int idx = g_CurrentMediaIndex;
            if (idx >= g_ElementInfoClass.EIF_ContentsInfoClassList.Count)
            {
                idx = 0;
            }

            return idx;
        }

        public bool TryApplySyncIndex(int index)
        {
            if (index < 0 || g_ElementInfoClass.EIF_ContentsInfoClassList.Count == 0)
            {
                return false;
            }

            if (index >= g_ElementInfoClass.EIF_ContentsInfoClassList.Count)
            {
                return false;
            }

            g_CurrentMediaIndex = index;
            tickNum = 0;
            isBusy = true;
            OrderingCanvasBGContents();
            tickNum = 0;
            return true;
        }

        public long CurrentContentElapsedSeconds => tickNum;

        public long CurrentContentDurationSeconds => playTime;

        private void Player_FileEndedEvent()
        {
            if (MainWindow.Instance != null && MainWindow.Instance.ShouldHoldForSyncContent)
            {
                SetLoopState(true);
                return;
            }

            if (needLoop)
                return;

            this.Dispatcher.Invoke(DispatcherPriority.Normal,
                    new Action(() =>
                    {
                        OrderingCanvasBGContents();
                    }));
        }

        private void Player_FileLoadedEvent()
        {
            this.Dispatcher.Invoke(DispatcherPriority.Normal,
                    new Action(() =>
                    {
                        MPVPlayer.Visibility = Visibility.Visible;
                        if (prevType == ContentType.Image)
                            ReleaseImage();
                    }));
        }

        internal void InitChangeEffect()
        {
            TransImgCtrl.ChangeEffect = TRANSITION_EFFECTS.Fade;
            TransImgCtrl.Duration = 0.3;
            TransImgCtrl.AccelRatio = 0.1;

            TransImgCtrl.Init();
        }

        //public void ClearImageQueue()
        //{
        //    preloadImg.Clear();
        //}

    }
}
