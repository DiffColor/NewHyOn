using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.IO;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Threading;
using System.ComponentModel;
using System.Text;
using SharedContentsInfoClass = AndoW.Shared.ContentsInfoClass;

namespace HyOnPlayer
{
    /// <summary>
    /// Interaction logic for WMKitMediaElement.xaml
    /// </summary>
    public partial class WMKitMediaElement : UserControl
    {
        ContentType prevType = ContentType.None;
        string pptFilename = string.Empty;
        string hdtvCh = string.Empty;
        string hdtvSource = SourceType.Cable.ToString();

        public ElementInfoClass g_ElementInfoClass = new ElementInfoClass();

        public int g_CurrentMediaIndex = 0;
        public int g_ContentListCount = 0;

        MainWindow parentPage = null;

        public bool g_previewToggleState = true;
        public int g_availableMediaCount = 0;

        int id = 0;

        public double g_TransformX = 1;
        public double g_TransformY = 1;
        HwndSource source;
        IntPtr hWnd;
        Size sz;
        Point loc;

        public WMKitMediaElement()
        {
            InitializeComponent();
        }

        public WMKitMediaElement(MainWindow main)
        {
            InitializeComponent();
            parentPage = main;

            SizeChanged += WMKitMediaElement_SizeChanged;
        }

        void WMKitMediaElement_SizeChanged(object sender, SizeChangedEventArgs e)
        {            
            if (prevType == ContentType.PPT || prevType == ContentType.HDTV)
            {
                if (hWnd == IntPtr.Zero)
                {
                    if (RefreshExeHandler() != IntPtr.Zero)
                    {
                        loc = ContentGrid.TranslatePoint(new Point(0, 0), parentPage);

                        if(prevType == ContentType.PPT) DisplayPPTContents(pptFilename);
                        if (prevType == ContentType.HDTV) DisplayHDTV(hdtvCh, hdtvSource);
                    }
                }
            }

        }

        public IntPtr RefreshExeHandler()
        {
            sz = GetActualSize(ExeRect);

            source = (HwndSource)HwndSource.FromVisual(ExeRect);
            
            if(source != null) 
                hWnd = source.Handle;

            return hWnd;
        }

        ~WMKitMediaElement()
        {
            StopContentsDisplay();
            StopVisibleContents();
        }

        private void KillExe()
        {
            try
            {
                pDocked.Kill();
            }
            catch (Exception e)
            {

            }
            finally
            {
                hWndDocked = IntPtr.Zero;
            }
        }

        long tickNum = 0;
        long playTime = 5;
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
            int playableCount = 0;

            foreach (SharedContentsInfoClass item in g_ElementInfoClass.EIF_ContentsInfoClassList)
            {
                if (item.CIF_ValidTime == true && item.CIF_FileExist == true)
                {
                    playableCount++;
                }
                else if (item.CIF_ContentType.Equals(ContentType.HDTV.ToString()))
                {
                    playableCount++;
                }
            }

           g_availableMediaCount = playableCount;
        }

        public void OrderingCanvasBGContents()
        {
            //Task.Factory.StartNew(() =>
            //{
                this.Dispatcher.Invoke(DispatcherPriority.Normal, (Action)(() =>
                 {
                     SharedContentsInfoClass content = new SharedContentsInfoClass();

                     if (this.g_ElementInfoClass.EIF_ContentsInfoClassList.Count > 0)
                     {

                     hereWeGo:

                         if (g_CurrentMediaIndex == 1 && g_ContentListCount == 1)
                         {
                             if (prevType == ContentType.HDTV)
                             {
                                 isBusy = true;
                                 return;
                             }
                             else if (prevType != ContentType.Video) return;
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

                         if (CheckContentsInfoIsValid(this.g_ElementInfoClass.EIF_ContentsInfoClassList[g_CurrentMediaIndex]) == false)
                         {
                             g_CurrentMediaIndex++;
                             goto hereWeGo;
                         }

                         int second = Convert.ToInt32(content.CIF_PlaySec);
                         int minute = Convert.ToInt32(content.CIF_PlayMinute);

                         playTime = (minute * 60) + second;

                         PlayContents(content);

                         //parentPage.SendReportCurrentPlayContents(content.CIF_FileName);

                         isBusy = false;
                         tickNum = 0;
                         g_CurrentMediaIndex++;
                     }
                 }));
            //}, TaskCreationOptions.LongRunning);
        }

        public void PlayContents(SharedContentsInfoClass cic)
        {
            StopVisibleContents();
            
            switch ((ContentType)Enum.Parse(typeof(ContentType), cic.CIF_ContentType))
            {
                case ContentType.Video:
                    DisplayVideo(cic.CIF_FileFullPath);
                    prevType = ContentType.Video;
                    break;

                case ContentType.HDTV:
                    DisplayHDTV(cic.CIF_ReservedData1, cic.CIF_ReservedData2);
                    prevType = ContentType.HDTV;
                    break;

                case ContentType.Image:
                    DisplayImage(cic.CIF_FileFullPath);
                    prevType = ContentType.Image;
                    break;
                case ContentType.Flash:
                    DisplayFlashContents(cic.CIF_FileFullPath);
                    prevType = ContentType.Flash;
                    break;
                case ContentType.PPT:
                    try
                    {
                        DisplayPPTContents(cic.CIF_FileFullPath);
                    }
                    catch (Exception e)
                    {
                    }
                    prevType = ContentType.PPT;
                    break;

                default:
                    break;
            }
        }


        private Process pDocked;
        private IntPtr hWndDocked = IntPtr.Zero;
        private void DisplayPPTContents(string pptFilePath)
        {
            ExeRect.Visibility = Visibility.Visible;
            pptFilename = pptFilePath;
            try
            {
                if (hWnd == IntPtr.Zero)
                {
                    if (RefreshExeHandler() == IntPtr.Zero) return;
                }

                pDocked = new Process();
                pDocked.StartInfo.UseShellExecute = true;

                String arg = String.Format("\"{0}\" /f", pptFilePath);
                pDocked = Process.Start(@"pptviewer\pptview.exe", arg);
                while (hWndDocked == IntPtr.Zero)
                {
                    pDocked.WaitForInputIdle(); //wait for the window to be ready for input;
                    pDocked.Refresh();              //update process info
                    if (pDocked.HasExited)
                    {
                        return; //abort if the process finished before we got a handle.
                    }
                    hWndDocked = pDocked.MainWindowHandle;  //cache the window handle
                }

                ProcessTools.SetParent(hWndDocked, hWnd);
                ProcessTools.MoveWindow(hWndDocked, (int)loc.X, (int)loc.Y, (int)sz.Width, (int)sz.Height, true);

                MessageTools.SendKeyDownMsg("^h");
            }
            catch (Exception e)
            {
            }
        }

        public void AborthHDTVThread()
        {
            if (currentHDTVThr != null)
            {
                currentHDTVThr.Abort();
            }
        }

        Thread currentHDTVThr;
        private void DisplayHDTV(string channel, string source)
        {
            ExeRect.Visibility = Visibility.Visible;
            hdtvCh = channel;
            hdtvSource = source;

            currentHDTVThr = new Thread(new ThreadStart(() =>
            {
                this.Dispatcher.Invoke(DispatcherPriority.Normal, (Action)(() =>
                {
                    try
                    {
                        if (hWnd == IntPtr.Zero)
                        {
                            if (RefreshExeHandler() == IntPtr.Zero) return;
                        }

                        if (pDocked != null)
                        {
                            if (hdtvSource.Equals(SourceType.Antenna.ToString()))
                            {
                                MessageTools.SendKeyDownMsg("+A");
                            }

                            MessageTools.SendKeyDownMsg(channel);

                            ProcessTools.SetParent(hWndDocked, hWnd);
                            ProcessTools.IsExeInitialized(hWndDocked);
                            ProcessTools.MoveWindow(hWndDocked, (int)loc.X, (int)loc.Y, (int)sz.Width, (int)sz.Height, true);

                            ProcessTools.EnableWindow(hWndDocked, false);
                            return;
                        }

                        pDocked = new Process();
                        pDocked.StartInfo.UseShellExecute = true;

                        string arg = string.Format("/dtv:{0}", channel);
                        //arg = "C:\\father.mp4";  //test content
                        pDocked = Process.Start(@"tvp\PotPlayerMini.exe", arg);

                        if (hdtvSource.Equals(SourceType.Antenna.ToString()))
                        {
                            MessageTools.SendKeyDownMsg("+A");
                        }

                        while (hWndDocked == IntPtr.Zero)
                        {
                            pDocked.WaitForInputIdle(); //wait for the window to be ready for input;
                            pDocked.Refresh();              //update process info
                            if (pDocked.HasExited)
                            {
                                return; //abort if the process finished before we got a handle.
                            }
                            hWndDocked = pDocked.MainWindowHandle;  //cache the window handle
                        }

                        ProcessTools.SetParent(hWndDocked, hWnd);
                        ProcessTools.MoveWindow(hWndDocked, 0, (int)MainWindow.Instance.Height, (int)sz.Width, (int)sz.Height, true);
                        ProcessTools.IsExeInitialized(hWndDocked);
                        ProcessTools.MoveWindow(hWndDocked, (int)loc.X, (int)loc.Y, (int)sz.Width, (int)sz.Height, true);

                        ProcessTools.EnableWindow(hWndDocked, false);
                    }
                    catch (Exception e)
                    {
                    }

                }));
            }));
            currentHDTVThr.Start();
        }


        Size GetActualSize(FrameworkElement control)
        {
            Size startSize = new Size(control.ActualWidth, control.ActualHeight);

            //if (startSize == new Size(0,0))
            //{
            //    startSize = new Size(control.Width, control.Height);
            //}

            //// go up parent tree until reaching root
            //var parent = LogicalTreeHelper.GetParent(control);
            //while (parent != null && parent as FrameworkElement != null && parent.GetType() != typeof(Window))
            //{
            //    // try to find a scale transform
            //    FrameworkElement fp = parent as FrameworkElement;
            //    ScaleTransform scale = FindScaleTransform(fp.RenderTransform);
            //    if (scale != null)
            //    {
                    //startSize.Width *= scale.ScaleX;
                    //startSize.Height *= scale.ScaleY;
            //    }
            //    parent = LogicalTreeHelper.GetParent(parent);
            //}
            //// return new size

            startSize.Width *= g_TransformX;
            startSize.Height *= g_TransformY;
            return startSize;
        }

        public void PlayNextContent()
        {
            isBusy = true;
            OrderingCanvasBGContents();
        }

        public void PlayPrevContent()
        {
            isBusy = true;
            g_CurrentMediaIndex -= 2;
            if (g_CurrentMediaIndex < 0)
            {
                g_CurrentMediaIndex = 0;
            }
            OrderingCanvasBGContents();
        }


        //DirectShow Routine
        //public void DisplayVideo(string contentPath)
        //{
        //    //DirectShowPlayer dsp = new DirectShowPlayer();
        //    //HwndSource source = (HwndSource)HwndSource.FromVisual(this);
        //    //IntPtr hWnd = source.Handle;
        //    //dsp.OpenClip(hWnd, contentPath);
        //    MediaElementOnMultimedia.Visibility = Visibility.Visible;
        //    MediaElementOnMultimedia.TransformX = g_TransformX;
        //    MediaElementOnMultimedia.TransformY = g_TransformY;
        //    MediaElementOnMultimedia.SourcePath = contentPath;
        //}

        //WPF Media Kit Routine
        public void DisplayVideo(string contentPath)
        {
            try
            {
                MediaElementOnMultimedia.Visibility = Visibility.Visible;
                MediaElementOnMultimedia.Source = new Uri(contentPath, UriKind.Absolute);
                MediaElementOnMultimedia.Play();
            }
            catch (Exception e)
            {
            }
        }

        public void DisplayImage(string contentPath)
        {
            FileInfo fi = new FileInfo(contentPath);

            if (!fi.Exists)
            {
                return;
            }
            else if (fi.Length <= 0)
            {
                return;
            }

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

        public void StopVisibleContents()
        {
            this.Dispatcher.Invoke(DispatcherPriority.Normal, (Action)(() =>
            {
                switch (prevType)
                {
                    case ContentType.None:
                        break;

                    case ContentType.PPT:
                        KillExe();
                        ExeRect.Visibility = Visibility.Hidden;
                        break;

                    case ContentType.HDTV:
                        AborthHDTVThread();
                        MessageTools.SendKeyDownMsg("+X");
                        ExeRect.Visibility = Visibility.Hidden;
                        break;

                    case ContentType.Image:
                        this.ContentGrid.Background = new SolidColorBrush(Colors.Black);
                        break;

                    case ContentType.Video:
                        MediaElementOnMultimedia.Stop();
                        //MediaElementOnMultimedia.CloseAndRelease();
                        ////MediaElementOnMultimedia.StopClip();
                        //MediaElementOnMultimedia.SourcePath = string.Empty;
                        //MediaElementOnMultimedia.Visibility = Visibility.Hidden;
                        break;

                    case ContentType.Flash:
                    case ContentType.Browser:
                        WebBrowser1.Navigate(new Uri("about:blank"));
                        WebBrowser1.Visibility = Visibility.Hidden;
                        break;

                    default:
                        break;
                }
            }));
        }

        public void DisplayFlashContents(string filePath)
        {
            WebBrowser1.Visibility = Visibility.Visible;
        
            string htmlCode = string.Format(
                    "<html xmlns=\"http://www.w3.org/1999/xhtml\" xml:lang=\"ko\" lang=\"ko\">" +
                    "<head><meta charset=\"utf-8\"></head>" + 
                    "<body leftmargin=\"0\" topmargin=\"0\" rightmargin=\"0\" bottommargin=\"0\" scroll=\"no\"><embed src=\"{0}\" allowScriptAccess=\"sameDomain\" type=\"application/x-shockwave-flash\" width=\"100%\" height=\"100%\" loop=\"true\" /></body></html>", filePath);

            WebBrowser1.NavigateToString(htmlCode);
        }


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
                bool existFile = new FileInfo(content.CIF_FileFullPath).Exists;

                if (content.CIF_FileExist != existFile)
                {
                    UpdateAvailableMediaCount();
                    IsValid = false;
                }

                if (!existFile)
                {
                    IsValid = false;
                }

            }
            catch (Exception ex)
            {
                IsValid = false;
            }

            if (content.CIF_ContentType.Equals(ContentType.HDTV.ToString()))
            {
                IsValid = true;
            }

            return IsValid;
        }

        public void HideAllControl()
        {
            MediaElementOnMultimedia.Visibility = Visibility.Hidden;
            WebBrowser1.Visibility = Visibility.Hidden;
        }

        public void UpdateAvailableMediaCount()
        {
            g_availableMediaCount = 0;

            foreach (SharedContentsInfoClass item in this.g_ElementInfoClass.EIF_ContentsInfoClassList)
            {
                if (item.CIF_ValidTime && item.CIF_FileExist)
                {
                    g_availableMediaCount++;
                }
            }
        }

        public int GetAvailableMediaCount()
        {
            int count = 0;
            foreach (SharedContentsInfoClass item in this.g_ElementInfoClass.EIF_ContentsInfoClassList)
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
            this.ContentGrid.Background = null;
            GC.Collect();
        }
    }
}
