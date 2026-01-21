using QuartzTypeLib;
using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using TurtleTools;

namespace HyOnPlayer
{
    /// <summary>
    /// QuartsControl.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class QuartzControl : UserControl
    {
        #region dependency properties

        public delegate void MediaEnded();
        public event MediaEnded MediaEndedEvent;

        public static readonly DependencyProperty SourceProperty = DependencyProperty.Register
            (
                "Source",
                typeof(string),
                typeof(QuartzControl),
                new PropertyMetadata(null, OnSourceChanged)
            );

        public string Source
        {
            set
            {
                SetValue(SourceProperty, value);
            }

            get
            {
                return (string)GetValue(SourceProperty);
            }
        }

        private static void OnSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            QuartzControl qc = d as QuartzControl;
            qc.SourceChanged((string)e.NewValue);
        }

        private void SourceChanged(string source)
        {
            Load(source);

            switch (LoadedBehavior)
            {
                case LoadedBehaviorType.Pause:
                    Play();
                    Pause();
                    break;

                case LoadedBehaviorType.Play:
                    Play();
                    break;

                case LoadedBehaviorType.Manual:
                default:
                    break;
            }
        }

        public static readonly DependencyProperty LoadedBehaviorProperty = DependencyProperty.Register
            (
                "LoadedBehavior",
                typeof(LoadedBehaviorType),
                typeof(QuartzControl),
                new PropertyMetadata(LoadedBehaviorType.Manual, OnLoadedBehaviorChanged)
            );

        public LoadedBehaviorType LoadedBehavior
        {
            set
            {
                SetValue(LoadedBehaviorProperty, value);
            }

            get
            {
                return (LoadedBehaviorType)GetValue(LoadedBehaviorProperty);
            }
        }

        private static void OnLoadedBehaviorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            QuartzControl qc = d as QuartzControl;
            qc.LoadedBehaviorChanged((LoadedBehaviorType)e.NewValue);
        }

        private void LoadedBehaviorChanged(LoadedBehaviorType type)
        {
            LoadedBehavior = (LoadedBehaviorType)type;
        }

        public static readonly DependencyProperty AspectRatioModeProperty = DependencyProperty.Register
            (
                "AspectRatioMode",
                typeof(AspectRatioType),
                typeof(QuartzControl),
                new PropertyMetadata(AspectRatioType.Maintain, OnAspectRatioModeChanged)
            );

        public AspectRatioType AspectRatioMode
        {
            set
            {
                SetValue(AspectRatioModeProperty, value);
            }

            get
            {
                return (AspectRatioType)GetValue(AspectRatioModeProperty);
            }
        }

        private static void OnAspectRatioModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            QuartzControl qc = d as QuartzControl;
            qc.AspectRatioModeChanged((AspectRatioType)e.NewValue);
        }

        private void AspectRatioModeChanged(AspectRatioType type)
        {
            AspectRatioMode = type;
            SetWindowSize();
        }


        public static readonly DependencyProperty LoopProperty = DependencyProperty.Register
            (
                "Loop",
                typeof(bool),
                typeof(QuartzControl),
                new PropertyMetadata(true, OnLoopChanged)
            );

        public bool Loop
        {
            set
            {
                SetValue(LoopProperty, value);
            }

            get
            {
                return (bool)GetValue(LoopProperty);
            }
        }

        private static void OnLoopChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            QuartzControl qc = d as QuartzControl;
            qc.LoopChanged((bool)e.NewValue);
        }

        private void LoopChanged(bool loop)
        {
            Loop = loop;
        }

        public static readonly DependencyProperty VolumeProperty = DependencyProperty.Register
            (
                "Volume",
                typeof(double),
                typeof(QuartzControl),
                new PropertyMetadata(1.0, OnVolumeChanged)
            );

        public double Volume
        {
            set
            {
                SetValue(VolumeProperty, value);
            }

            get
            {
                return (double)GetValue(VolumeProperty);
            }
        }

        private static void OnVolumeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            QuartzControl qc = d as QuartzControl;
            qc.VolumeChanged((double)e.NewValue);
        }

        private void VolumeChanged(double volume)
        {
            Volume = volume;
            SetVolume();
        }

        #endregion

        public QuartzControl()
        {
            InitializeComponent();
        }

        private const int WM_APP = 0x8000;
        private const int WM_GRAPHNOTIFY = WM_APP + 1;

        public enum DSEvents
        {
            None,
            EC_COMPLETE = 0x01, 
            EC_USERABORT = 0x02,  
            EC_ERRORABORT = 0x03,  
            EC_TIME = 0x04,  
            EC_REPAINT = 0x05,  
            EC_STREAM_ERROR_STOPPED = 0x06,  
            EC_STREAM_ERROR_STILLPLAYING = 0x07,  
            EC_ERROR_STILLPLAYING = 0x08,  
            EC_PALETTE_CHANGED = 0x09,  
            EC_VIDEO_SIZE_CHANGED = 0x0a,  
            EC_QUALITY_CHANGE = 0x0b,  
            EC_SHUTTING_DOWN = 0x0c,  
            EC_CLOCK_CHANGED = 0x0d,  
            EC_PAUSED = 0x0e,  
            EC_OPENING_FILE = 0x10,  
            EC_BUFFERING_DATA = 0x11,   
            EC_FULLSCREEN_LOST = 0x12,   
            EC_ACTIVATE = 0x13,  
            EC_NEED_RESTART = 0x14,   
            EC_WINDOW_DESTROYED = 0x15,  
            EC_DISPLAY_CHANGED = 0x16,  
            EC_STARVATION = 0x17,  
            EC_OLE_EVENT = 0x18,  
            EC_NOTIFY_WINDOW = 0x19
        }


        private const int WS_CHILD = 0x40000000;
        private const int WS_CLIPCHILDREN = 0x2000000;

        private FilgraphManager m_objFilterGraph = null;
        private IBasicVideo m_objBasicVideo = null;
        private IBasicAudio m_objBasicAudio = null;
        private IVideoWindow m_objVideoWindow = null;
        private IMediaEvent m_objMediaEvent = null;
        private IMediaEventEx m_objMediaEventEx = null;
        private IMediaPosition m_objMediaPosition = null;
        private IMediaControl m_objMediaControl = null;

        HwndSource me = null;

        private void Load(string contentPath)
        {
            if (string.IsNullOrEmpty(contentPath) || File.Exists(contentPath) == false)
            {
                Stop();
                return;
            }

            m_objFilterGraph = new FilgraphManager();
            m_objFilterGraph.RenderFile(contentPath);

            m_objBasicVideo = m_objFilterGraph as IBasicVideo;
            m_objBasicAudio = m_objFilterGraph as IBasicAudio;

            m_objVideoWindow = m_objFilterGraph as IVideoWindow;
            m_objVideoWindow.Owner = (int)this.QuartzPanel.Handle;
            m_objVideoWindow.WindowStyle = WS_CHILD | WS_CLIPCHILDREN;

            m_objMediaEvent = m_objFilterGraph as IMediaEvent;
            m_objMediaEventEx = m_objFilterGraph as IMediaEventEx;

            me = (HwndSource)HwndSource.FromVisual(this);
            me.AddHook(WndProc);
            m_objMediaEventEx.SetNotifyWindow((int)me.Handle, WM_GRAPHNOTIFY, 0);

            m_objMediaPosition = m_objFilterGraph as IMediaPosition;
            m_objMediaControl = m_objFilterGraph as IMediaControl;

            SetWindowSize();
            SetVolume();
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            switch (msg)
            {
                case WM_GRAPHNOTIFY:
                    {
                        HandleGraphEvent();
                        break;
                    }
            }

            return IntPtr.Zero;
        }

        public void HandleGraphEvent()
        {
            int lEventCode;
            int lParam1;
            int lParam2;

            try
            {
                if (m_objMediaEventEx == null)
                    return;

                m_objMediaEventEx.GetEvent(out lEventCode, out lParam1, out lParam2, 0);
                m_objMediaEventEx.FreeEventParams(lEventCode, lParam1, lParam2);

                if(lEventCode == (int)DSEvents.EC_COMPLETE) 
                {
                    if (MediaEndedEvent != null)
                        MediaEndedEvent();

                    if (Loop)
                        m_objMediaPosition.CurrentPosition = 0;
                    else
                        Stop();
                }
            }
            catch (Exception ee)
            {
            }
        }

        private void CleanUp()
        {
            if (m_objMediaControl != null)
                m_objMediaControl.Stop();

            if (m_objMediaEventEx != null)
                m_objMediaEventEx.SetNotifyWindow(0, 0, 0);

            if (m_objVideoWindow != null)
            {
                m_objVideoWindow.Visible = 0;
                m_objVideoWindow.Owner = 0;
            }

            if (m_objMediaControl != null) m_objMediaControl = null;
            if (m_objMediaPosition != null) m_objMediaPosition = null;
            if (m_objMediaEventEx != null) m_objMediaEventEx = null;
            if (m_objMediaEvent != null) m_objMediaEvent = null;
            if (m_objVideoWindow != null) m_objVideoWindow = null;
            if (m_objBasicAudio != null) m_objBasicAudio = null;
            if (m_objFilterGraph != null) m_objFilterGraph = null;
            //if (m_objMediaSeeking != null) m_objMediaSeeking = null;

            GC.Collect();
        }

        public void Play()
        {
            if (string.IsNullOrEmpty(Source))
                return;

            if (m_objMediaControl == null)
                return;

            QuartzPanel.Visible = true;

            m_objMediaControl.Run();
        }

        public void Pause()
        {
            if (string.IsNullOrEmpty(Source))
                return;

            if (m_objMediaControl == null)
                return;

            m_objMediaControl.Pause();
        }

        public void Stop()
        {
            if (m_objMediaControl == null)
                return;

            m_objMediaControl.Stop();

            QuartzPanel.Visible = false;
        }

        public void Close()
        {
            if (m_objMediaControl == null)
                return;

            m_objMediaControl.Stop();

            QuartzPanel.Visible = false;

            Source = null;
        }

        private void UserControl_Unloaded(object sender, RoutedEventArgs e)
        {
            CleanUp();
        }

        private void UserControl_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            SetWindowSize();
        }

        void SetWindowSize()
        {
            if (m_objVideoWindow == null)
                return;

            int x = QuartzPanel.ClientRectangle.Left, y = QuartzPanel.ClientRectangle.Top, 
                w = QuartzPanel.ClientRectangle.Width, h = QuartzPanel.ClientRectangle.Height;

            if (AspectRatioMode == AspectRatioType.Maintain)
            {
                //m_objBasicVideo.GetVideoSize(out vw, out vh);
                System.Drawing.Size sz = MediaTools.GetVideoSize(Source);
                int vw = sz.Width;
                int vh = sz.Height;

                double ratio = (double)vh / (double)vw;

                if (vw >= vh)
                {
                    h = (int)(QuartzPanel.ClientRectangle.Width * ratio);
                    y = (QuartzPanel.ClientRectangle.Height - h) / 2;
                    if (y < 0)
                    {
                        ratio = (double)QuartzPanel.ClientRectangle.Height / (double)h;
                        h = (int)(h * ratio);
                        y = 0;
                        w = (int)(QuartzPanel.ClientRectangle.Width * ratio);
                        x = (QuartzPanel.ClientRectangle.Width - w) / 2;
                    }
                }
                else
                {
                    w = (int)(QuartzPanel.ClientRectangle.Height / ratio);
                    x = (QuartzPanel.ClientRectangle.Width - w) / 2;
                    if (x < 0)
                    {
                        ratio = (double)QuartzPanel.ClientRectangle.Width / (double)w;
                        w = (int)(w * ratio);
                        x = 0;
                        h = (int)(QuartzPanel.ClientRectangle.Height / ratio);
                        y = (QuartzPanel.ClientRectangle.Height - h) / 2;
                    }
                }
            }

            m_objVideoWindow.SetWindowPosition(x, y, w, h);
        }

        private void SetVolume()
        {
            try
            {
                if (m_objBasicAudio == null)
                    return;

                m_objBasicAudio.Volume = (int)((1 - Volume) * -10000);
            }
            catch (Exception ee) { }
        }

        public void SetPosition(double pos)
        {
            m_objMediaPosition.CurrentPosition = pos;
        }
    }
}
