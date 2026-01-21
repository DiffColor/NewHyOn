using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Linq;
using TurtleTools;
using System.IO;

namespace PageViewer
{
    /// <summary>
    /// MainWindow.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class MainWindow : Window
    {
        double mScale = 1;

        MultimediaTimer.Timer g_TickTimer = new MultimediaTimer.Timer();

        PreviewCanvas mCanvas;
        List<PreviewElement> mElements;

        List<MediaControl> mMedia = new List<MediaControl>();

        bool mDoScreenshot = false;
        int mShotCount = 0;
        string mThumbFilename = string.Empty;

        void InitTickTimer()
        {
            g_TickTimer.Mode = MultimediaTimer.TimerMode.Periodic;
            g_TickTimer.Period = 1000;  // 1 second
            g_TickTimer.Resolution = 1;
            g_TickTimer.SynchronizingObject = new DispatcherWinFormsCompatAdapter(this.Dispatcher);
            g_TickTimer.Tick += new System.EventHandler(TickTask);
        }

        void RunTickTimer()
        {
            g_TickTimer.Start();
        }

        void StopTickTimer()
        {
            g_TickTimer.Stop();
        }

        private void TickTask(object sender, EventArgs e)
        {
            foreach(MediaControl item in mMedia)
                item.Tick();

            if (mDoScreenshot)
            {
                if (mShotCount > 0)
                {
                    MediaTools.ConvertAndSavePngImage(ContentCanvas, Path.Combine(FNDTools.GetPagesRootDirPath(), mThumbFilename));
                    this.Close();
                    return;
                }
                mShotCount++;
            }
        }

        public MainWindow()
        {
            InitializeComponent();

            string[] args = Environment.GetCommandLineArgs();

            if (args.Length < 3)
            {
                this.Close();
                return;
            }

            ContentGrid.Width = Convert.ToInt32(args[1]);
            ContentGrid.Height = Convert.ToInt32(args[2]);

            if (args.Length > 4)
            {
                mDoScreenshot = Convert.ToBoolean(args[3]);
                if (mDoScreenshot)
                    mThumbFilename = args[4];
            }

            InitTickTimer();

            mCanvas = XmlTools.ReadXml<PreviewCanvas>(FNDTools.GetPreviewCanvasFilePath());
            if (mCanvas == null)
                mCanvas = new PreviewCanvas();

            mElements = XmlTools.ReadXml<List<PreviewElement>>(FNDTools.GetPreviewDataFilePath());
            if (mElements == null)
                mElements = new List<PreviewElement>();

            double _width, _height = 0;

            if(mCanvas.Width <= mCanvas.Height)
            {
                _height = ContentGrid.Height;
                mScale = _height / mCanvas.Height;
                _width = mCanvas.Width * mScale;
                double _halfmargin = (ContentGrid.Width - _width) / 2;
                ContentCanvas.Margin = new Thickness(_halfmargin, 0, _halfmargin, 0);
            } else
            {
                _width = ContentGrid.Width;
                mScale = _width / mCanvas.Width;
                _height = mCanvas.Height * mScale;
                double _halfmargin = (ContentGrid.Height - _height) / 2;
                ContentCanvas.Margin = new Thickness(0, _halfmargin, 0, _halfmargin);
            }

            CreateElements();
        }

        private void CreateElements()
        {
            foreach(PreviewElement element in mElements.OrderBy(x => x.Index).ThenBy(x => x.Index.ToString().Length))
            {
                switch ((DisplayType)Enum.Parse(typeof(DisplayType), element.ElementType))
                {
                    case DisplayType.Media:
                        MediaControl _mediactrl = new MediaControl();
                        _mediactrl.Width = element.Width * mScale;
                        _mediactrl.Height = element.Height * mScale;
                        _mediactrl.MEDisplayElement.Stretch = mCanvas.FillContent ? System.Windows.Media.Stretch.Fill : System.Windows.Media.Stretch.Uniform;
                        _mediactrl.ImageCtrl.Stretch = mCanvas.FillContent ? System.Windows.Media.Stretch.Fill : System.Windows.Media.Stretch.Uniform;
                        Canvas.SetLeft(_mediactrl, element.PosX * mScale);
                        Canvas.SetTop(_mediactrl, element.PosY * mScale);
                        Canvas.SetZIndex(_mediactrl, element.Index);
                        _mediactrl.SetData(element.DataList);
                        ContentCanvas.Children.Add(_mediactrl);
                        mMedia.Add(_mediactrl);
                        break;

                    case DisplayType.ScrollText:
                        ScrollTextControl _scrollctrl = new ScrollTextControl();
                        _scrollctrl.Width = element.Width * mScale;
                        _scrollctrl.Height = element.Height * mScale;
                        Canvas.SetLeft(_scrollctrl, element.PosX * mScale);
                        Canvas.SetTop(_scrollctrl, element.PosY * mScale);
                        Canvas.SetZIndex(_scrollctrl, element.Index);
                        _scrollctrl.SetData(element.DataList);
                        ContentCanvas.Children.Add(_scrollctrl);
                        break;

                    case DisplayType.WelcomeBoard:
                        TextImageControl _txtimgctrl = new TextImageControl();
                        _txtimgctrl.Width = element.Width * mScale;
                        _txtimgctrl.Height = element.Height * mScale;
                        Canvas.SetLeft(_txtimgctrl, element.PosX * mScale);
                        Canvas.SetTop(_txtimgctrl, element.PosY * mScale);
                        Canvas.SetZIndex(_txtimgctrl, element.Index);
                        _txtimgctrl.SetData(element.DataList);
                        ContentCanvas.Children.Add(_txtimgctrl);
                        break;
                }
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            foreach (MediaControl item in mMedia)
                item.PopContent();

            RunTickTimer();

            if (mDoScreenshot)
                this.Visibility = Visibility.Hidden;
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            StopTickTimer();
        }

        private void Rectangle_PreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            this.DragMove();
        }

        private void ExitBtn_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            this.Close();
        }
    }
}
