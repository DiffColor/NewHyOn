using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using TurtleTools;

namespace AndoW_Manager
{
    /// <summary>
    /// NotifyWindow.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class PagePreviewWindow : Window
    {
        //////////////////////////////////////////////////////////////////
        // 미리보기용
        public List<VideoImageElement4> g_VideoImageElementList = new List<VideoImageElement4>();
        public List<ScrollTextElement4> g_ScrollTextElementList = new List<ScrollTextElement4>();
        public List<TextElementForEditor4> g_TextElementForPreviewList = new List<TextElementForEditor4>();

        //System.Timers.Timer saveTimer = new System.Timers.Timer();
        System.Timers.Timer g_SaveTimer = null;
        public string g_CurrentPageName = string.Empty;

        System.Threading.Timer g_TickTimer;
        private class StateObjClass
        {
            //public int SomeValue;
            //public System.Threading.Timer TimerReference;
            //public bool TimerCanceled;
        }

        List<DisplayElementForEditor> g_ctrlList = new List<DisplayElementForEditor>();

        int g_thumbWidth = 1920 / 4;
        int g_thumbHeight = 1080 / 4;

        bool g_IsSavePage = false;
        public PagePreviewWindow(bool IsSavePage, string paramPageName)
        {
            InitializeComponent();

            g_IsSavePage = IsSavePage;
            InitEventHandler();

            g_CurrentPageName = paramPageName;

            if (g_IsSavePage == true)
            {
                NotiText.Visibility = System.Windows.Visibility.Visible;
                BtnWin_close.Visibility = System.Windows.Visibility.Hidden;
            }
            else
            {
                NotiText.Visibility = System.Windows.Visibility.Hidden;
                BtnWin_close.Visibility = System.Windows.Visibility.Visible;
            }

            if (MainWindow.Instance.isPortraitEditor)
            {
                //Width = 574;
                //Height = 760;
                Width = 360;
                Height = 640;

                DesignerCanvas1.Width = 1080;
                DesignerCanvas1.Height = 1920;
            }
        }

        public void InitTimer()
        {
            g_SaveTimer = new System.Timers.Timer();
            g_SaveTimer.Interval = 3000;
            g_SaveTimer.Elapsed += saveTimer_Elapsed;
            g_SaveTimer.Start();
        }

        void saveTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            if (g_SaveTimer != null)
            {
                g_SaveTimer.Stop();
            }

            this.Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.Normal, (Action)(() =>
            {
                StopPreview();
                this.Close();
            }));
        }

        public static System.Drawing.Bitmap Combine(Bitmap baseBmp, List<Bitmap> bmps, List<System.Windows.Point> points, int desiredW, int desiredH)
        {
            System.Drawing.Bitmap finalImage = null;
            System.Drawing.Bitmap bbmp = null;

            try
            {
                finalImage = new System.Drawing.Bitmap(desiredW, desiredH);
                bbmp = baseBmp;

                using (System.Drawing.Graphics g = System.Drawing.Graphics.FromImage(bbmp))
                {
                    for (int i = 0; i < bmps.Count; i++)
                    {
                        if (bmps[i] == null) continue;

                        g.DrawImage(bmps[i],
                          new System.Drawing.Rectangle((int)points[i].X, (int)points[i].Y, bmps[i].Width, bmps[i].Height));
                    }

                    using (System.Drawing.Graphics gg = System.Drawing.Graphics.FromImage(finalImage))
                    {
                        gg.Clear(System.Drawing.Color.Black);
                        gg.DrawImage(bbmp, new System.Drawing.Rectangle(0, 0, desiredW, desiredH));
                    }
                }
                return finalImage;
            }
            catch (Exception ex)
            {
                if (finalImage != null)
                    finalImage.Dispose();
                if (bbmp != null)
                    bbmp.Dispose();

                throw ex;
            }
            finally
            {
                foreach (System.Drawing.Bitmap bmp in bmps)
                {
                    if (bmp != null)
                        bmp.Dispose();
                }
            }
        }

        public void InitEventHandler()
        {
            this.Loaded += ContentsPreviewWindow_Loaded;
            this.Closing += PagePreviewWindow_Closing;
        }

        void PagePreviewWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            StopPreview();

            if (g_SaveTimer != null)
            {
                g_SaveTimer.Stop();
            }

            StopTickTimer();
        }

        void ContentsPreviewWindow_Loaded(object sender, RoutedEventArgs e)
        {
            //DesignerCanvas1.Width = Convert.ToDouble(Page1.Instance.cavasWidthCombo.SelectedItem.ToString());
            //DesignerCanvas1.Height = Convert.ToDouble(Page1.Instance.cavasHeightCombo.SelectedItem.ToString());

           // if (GuideBorder.ActualWidth > GuideBorder.ActualHeight)
            //DesignerCanvas1.Width = Page1.Instance.GuideBorder.ActualWidth;
            //DesignerCanvas1.Height = Page1.Instance.GuideBorder.ActualHeight;

            //if (Page1.Instance.GuideBorder.ActualWidth > Page1.Instance.GuideBorder.ActualHeight)
            //{

            //GuidBorder.Width = Page1.Instance.GuideBorder.ActualWidth * 0.8;
            //GuidBorder.Height = Page1.Instance.GuideBorder.ActualHeight * 0.8;

            //FrameRect.Width = (Page1.Instance.GuideBorder.ActualWidth * 0.8) + 30;
            //FrameRect.Height = (Page1.Instance.GuideBorder.ActualHeight * 0.8) + 30;

            GuidBorder.Width = this.Width * 0.8;
            GuidBorder.Height = this.Height * 0.8;

            FrameRect.Width = GuidBorder.Width + 12;
            FrameRect.Height = GuidBorder.Height + 12;
            //}
            //else
            //{
            //    DesignerCanvas1.Width = 1080 ;
            //    DesignerCanvas1.Height = 1920;
            //}

            AdjustCanvasSizeForPreview();
            ShowPreview();

            if (g_IsSavePage == true)
            {
                InitTimer();
            }
        }

        double g_FitscaleValueXForPreview = 0;
        double g_FitscaleValueYForPreview = 0;

        public void AdjustCanvasSizeForPreview()
        {
            GuidBorder.UpdateLayout();
            DesignerCanvas1.UpdateLayout();

            FrameRect.UpdateLayout();
            //FrameRect

            g_FitscaleValueXForPreview = GuidBorder.ActualWidth / DesignerCanvas1.Width;
            g_FitscaleValueYForPreview = GuidBorder.ActualHeight / DesignerCanvas1.Height;

         
            //if (DesignerCanvas1.ActualWidth > DesignerCanvas1.ActualHeight)
            //{
            //    if (g_FitscaleValueXForPreview > g_FitscaleValueYForPreview)
            //    {
            //        ScaleTransform scale = new ScaleTransform(g_FitscaleValueXForPreview, g_FitscaleValueYForPreview);
            //        DesignerCanvas1.RenderTransform = scale;

            //    }
            //    else
            //    {
            //        ScaleTransform scale = new ScaleTransform(g_FitscaleValueXForPreview, g_FitscaleValueXForPreview);
            //        DesignerCanvas1.RenderTransform = scale;
            //    }
            //}
            //else
            //{
            //    if (g_FitscaleValueXForPreview < g_FitscaleValueYForPreview)
            //    {
            //        ScaleTransform scale = new ScaleTransform(g_FitscaleValueXForPreview, g_FitscaleValueYForPreview);
            //        DesignerCanvas1.RenderTransform = scale;
            //    }
            //    else
            //    {
            //        ScaleTransform scale = new ScaleTransform(g_FitscaleValueYForPreview, g_FitscaleValueYForPreview);
            //        DesignerCanvas1.RenderTransform = scale;

            //    }
            //}
            ScaleTransform scale = new ScaleTransform(g_FitscaleValueXForPreview, g_FitscaleValueYForPreview);
            DesignerCanvas1.RenderTransform = scale;
        }

        public void ShowPreview()
        {
            //if (Page1.Instance.g_DspElmtList.Count > 0)
            //{
                foreach (DisplayElementForEditor item in Page1.Instance.g_DspElmtList)
                {
                    CreateVideoAndImageForPreview(item.g_ElementInfoClass);
                    //g_ctrlList.Add(item);
                }
            //}

            //if (Page1.Instance.g_ScrollTextForEditorList.Count > 0)
            //{
                foreach (ScrollTextForEditor item in Page1.Instance.g_ScrollTextForEditorList)
                {
                    CreateScrollTextForPreview(item.g_ElementInfoClass);
                    //g_ctrlList.Add(item);
                }
            //}

            //if (Page1.Instance.g_WelcomBoardForEditorList.Count > 0)
            //{
                foreach (WelcomeBoardForEditor item in Page1.Instance.g_WelcomBoardForEditorList)
                {
                    CreateTextElementForPreview(item.g_ElementInfoClass, item.g_TextInfoClass);
                    //g_ctrlList.Add(item);
                }
            //}
            RunTickTimer();
        }

        void RunTickTimer()
        {
            StateObjClass StateObj = new StateObjClass();
            System.Threading.TimerCallback TimerDelegate =
                                            new System.Threading.TimerCallback(TickTask);

            PlayContents();
            g_TickTimer = new System.Threading.Timer(TimerDelegate, StateObj, 0, 1000);
        }

        void StopTickTimer()
        {
            if (g_TickTimer != null)
            {
                g_TickTimer.Dispose();
            }
        }

        void PlayContents()
        {
            foreach (VideoImageElement4 vie in g_VideoImageElementList)
            {
                vie.OrderingCanvasBGContents();
            }
        }

        //public void RunTimer()
        //{
        //    StateObjClass StateObj = new StateObjClass();
        //    //StateObj.TimerCanceled = false;
        //    //StateObj.SomeValue = 1;
        //    System.Threading.TimerCallback TimerDelegate =
        //        new System.Threading.TimerCallback(TickTask);

        //    // Create a timer that calls a procedure every 2 seconds.
        //    // Note: There is no Start method; the timer starts running as soon as 
        //    // the instance is created.
        //    g_TickTimer =
        //        new System.Threading.Timer(TimerDelegate, StateObj, 0, 1000);

        //    // Save a reference for Dispose.
        //    //StateObj.TimerReference = g_TickTimer;

        //    //// Run for ten loops.
        //    //while (StateObj.SomeValue < 10)
        //    //{
        //    //    // Wait one second.
        //    //    System.Threading.Thread.Sleep(1000);
        //    //}

        //    // Request Dispose of the timer object.
        //    //StateObj.TimerCanceled = true;
        //}

        private void TickTask(object StateObj)
        {
            //StateObjClass State = (StateObjClass)StateObj;
            //// Use the interlocked class to increment the counter variable.
            ////System.Threading.Interlocked.Increment(ref State.SomeValue);
            //if (State.TimerCanceled)
            //// Dispose Requested.
            //{
            //    State.TimerReference.Dispose();
            //}
            try
            {
                foreach (VideoImageElement4 ctrl in g_VideoImageElementList)
                {
                    ctrl.Tick();
                }
            }
            catch (Exception e)
            {
            }
        }

        public void StopPreview()
        {
            if (g_VideoImageElementList.Count > 0)
            {
                foreach (VideoImageElement4 item in g_VideoImageElementList)
                {
                    item.StopContentsDisplay();
                }
            }

            if (g_ScrollTextElementList.Count > 0)
            {
                foreach (ScrollTextElement4 item in g_ScrollTextElementList)
                {
                    item.StopAnimation();
                }
            }

            this.DesignerCanvas1.Children.Clear();
            g_VideoImageElementList.Clear();
            g_ScrollTextElementList.Clear();
            g_TextElementForPreviewList.Clear();
        }


        public void CreateTextElementForPreview(ElementInfoClass paramCls, TextInfoClass paramClsTextInfo)
        {
            TextElementForEditor4 textElement = new TextElementForEditor4(this);
            textElement.Name = paramCls.EIF_Name;
            textElement.Width = paramCls.EIF_Width;
            textElement.Height = paramCls.EIF_Height;
            Canvas.SetLeft(textElement, paramCls.EIF_PosLeft);
            Canvas.SetTop(textElement, paramCls.EIF_PosTop);

            if (MainWindow.Instance.isPortraitEditor)
            {
                WindowTools.ConvertInScaledUserCtrl(textElement, MainWindow.Instance.g_wPortScale, MainWindow.Instance.g_hPortScale);
            }  

            DesignerCanvas1.Children.Add(textElement);            // <--------------- 캔버스에 차일드 추가

            Canvas.SetZIndex(textElement, paramCls.EIF_ZIndex);
            textElement.UpdateTextInfoClsFromPage(paramClsTextInfo);

            g_TextElementForPreviewList.Add(textElement);

        }


        public void CreateScrollTextForPreview(ElementInfoClass paramCls)
        {
            ScrollTextElement4 scrollElement = new ScrollTextElement4(this);
            scrollElement.Name = paramCls.EIF_Name;
            scrollElement.Width = paramCls.EIF_Width;
            scrollElement.Height = paramCls.EIF_Height;
            Canvas.SetLeft(scrollElement, paramCls.EIF_PosLeft);
            Canvas.SetTop(scrollElement, paramCls.EIF_PosTop);

            if (MainWindow.Instance.isPortraitEditor)
            {
                WindowTools.ConvertInScaledUserCtrl(scrollElement, MainWindow.Instance.g_wPortScale, MainWindow.Instance.g_hPortScale);
            }  

            DesignerCanvas1.Children.Add(scrollElement);            // <--------------- 캔버스에 차일드 추가

            Canvas.SetZIndex(scrollElement, paramCls.EIF_ZIndex);
            scrollElement.UpdateScrollTextList(paramCls);

            g_ScrollTextElementList.Add(scrollElement);
        }


        public void CreateVideoAndImageForPreview(ElementInfoClass paramCls)
        {
            //////////////////////////////////////////////////////////////////////
            // Common
            VideoImageElement4 videoImgElement = new VideoImageElement4(this);
            videoImgElement.Name = paramCls.EIF_Name;
            videoImgElement.Width = paramCls.EIF_Width;
            videoImgElement.Height = paramCls.EIF_Height;
            Canvas.SetLeft(videoImgElement, paramCls.EIF_PosLeft);
            Canvas.SetTop(videoImgElement, paramCls.EIF_PosTop);

            if (MainWindow.Instance.isPortraitEditor)
            {
                WindowTools.ConvertInScaledUserCtrl(videoImgElement, MainWindow.Instance.g_wPortScale, MainWindow.Instance.g_hPortScale);
            }      

            DesignerCanvas1.Children.Add(videoImgElement);            // <--------------- 캔버스에 차일드 추가

            Canvas.SetZIndex(videoImgElement, paramCls.EIF_ZIndex);

            ///////////////////////////////////////////////////////////////////////            
            // Init ContentList
            videoImgElement.UpdateElementInfoClass(paramCls);
            //videoImgElement.OrderingCanvasBGContents();

            g_VideoImageElementList.Add(videoImgElement);
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
