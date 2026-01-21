using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using TurtleTools;

namespace PageViewer
{
    /// <summary>
    /// MediaControl.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class MediaControl : UserControl
    {
        List<PreviewData> mData = new List<PreviewData>();

        int mCurrentIndex = 0;
        ContentType mPrevType = ContentType.None;

        int mTickCount = 0;
        int mPlaytime = 5;
        bool isBusy = false;


        public MediaControl()
        {
            InitializeComponent();
        }

        public void SetData(List<PreviewData> data)
        {
            mData = data;
        }

        public void Tick()
        {
            if (isBusy)
                return;

            mTickCount++;

            if (mTickCount >= mPlaytime)
                PopContent();
        }

        public void PopContent()
        {
            isBusy = true;
            mTickCount = 0;

            this.Dispatcher.Invoke(DispatcherPriority.Normal, (Action)(() =>
            {
                if (mData.Count > 0)
                {
                    if (mCurrentIndex == 1 && mData.Count == 1)
                    {
                        if (mPrevType != ContentType.Video)
                            return;
                    }

                    if (mCurrentIndex > (mData.Count - 1))
                        mCurrentIndex = 0;

                    mPlaytime = mData[mCurrentIndex].Playtime;

                    PlayContent();
                }
            }));

            mCurrentIndex++;
            isBusy = false;
        }

        public void PlayContent() {

            try
            {
                StopPlayingContent();

                switch ((ContentType)Enum.Parse(typeof(ContentType), mData[mCurrentIndex].DataType))
                {
                    case ContentType.Video:
                        DisplayVideo();
                        mPrevType = ContentType.Video;
                        break;

                    case ContentType.Image:
                        DisplayImage();
                        mPrevType = ContentType.Image;
                        break;
                }


            } catch(Exception e) { }
        }

        private void DisplayImage()
        {
            string _fpath = mData[mCurrentIndex].FilePath;
            if (File.Exists(_fpath) == false)
                _fpath = FNDTools.GetContentsFilePath(Path.GetFileName(_fpath));

            MediaTools.DisplayImage(ImageCtrl, _fpath);
            ImageCtrl.Visibility = Visibility.Visible;
        }

        private void DisplayVideo()
        {
            try
            {
                string _fpath = mData[mCurrentIndex].FilePath;
                if (File.Exists(_fpath) == false)
                    _fpath = FNDTools.GetContentsFilePath(Path.GetFileName(_fpath));

                MEDisplayElementTransform.Angle = MediaTools.GetVideoRotateAngle(_fpath);
                MEDisplayElement.Source = new Uri(_fpath);
                MEDisplayElement.MediaEnded += MEDisplayElement_MediaEnded;
                MEDisplayElement.Play();
                MEDisplayElement.Visibility = Visibility.Visible;
            }
            catch (Exception e) { }
        }

        private void MEDisplayElement_MediaEnded(object sender, RoutedEventArgs e)
        {
            MEDisplayElement.Position = new TimeSpan(0, 0, 0);
        }


        private void StopPlayingContent()
        {
            isBusy = true;

            this.Dispatcher.Invoke(DispatcherPriority.Normal, (Action)(() =>
            {
                switch (mPrevType)
                {
                    case ContentType.None:
                        break;

                    case ContentType.Image:
                        ImageCtrl.Source = MediaTools.GetBitmapSourceFromHBitmap(Properties.Resources.one_black_pixel);
                        ImageCtrl.Visibility = Visibility.Hidden;
                        break;

                    case ContentType.Video:
                        MEDisplayElement.Stop();
                        MEDisplayElement.MediaEnded -= MEDisplayElement_MediaEnded;
                        MEDisplayElement.Visibility = Visibility.Hidden;
                        break;

                    default:
                        break;

                }
            }));

            if (mPrevType != ContentType.None)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }
        }

    }
}
