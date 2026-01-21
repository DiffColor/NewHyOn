using DirectShowLib;
using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using WndStyle = DirectShowLib.WindowStyle;
using Forms = System.Windows.Forms;
using Microsoft.Win32;

namespace HyOnPlayer
{

    /// <summary>
    /// DirectShowEVRControl.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class DirectShowEVRControl : UserControl, GraphEventCallback
    {
        const int WM_GRAPH_EVENT = 0x8000 + 1;
        private const int VolumeFull = 0;
        private const int VolumeSilence = -10000;

        private const int WM_APP = 0x8000;
        private const int WM_GRAPHNOTIFY = WM_APP + 1;
        private const int EC_COMPLETE = 0x01;
        private const int WS_CHILD = 0x40000000;
        private const int WS_CLIPCHILDREN = 0x2000000;

        private string filepath = string.Empty;
        private string filename = string.Empty;

        private bool isAudioOnly = false;
        private PlayStateType currentState = PlayStateType.Stopped;

        private IntPtr owner = IntPtr.Zero;
        private double transformX = 1;
        private double transformY = 1;
        private Size transSZ;

        HwndSource msgHwnd;

        DirectShowEVRPlayer player;

        //private double currentPlaybackRate = 1.0;
        //private int currentVolume = 0;
        //private bool isFullScreen = false;

        #region GetSet
        public IntPtr Owner
        {
            set
            {
                owner = value;
            }
            get
            {
                return owner;
            }
        }

        public double TransformX
        {
            set
            {
                transformX = value;
            }
            get
            {
                return transformX;
            }
        }

        public double TransformY
        {
            set
            {
                transformY = value;
            }
            get
            {
                return transformY;
            }
        }

        //public bool AudioOnly
        //{
        //    set
        //    {
        //        isAudioOnly = value;
        //    }
        //    get
        //    {
        //        return isAudioOnly;
        //    }
        //}

        //public bool FullScreen
        //{
        //    set
        //    {
        //        isFullScreen = value;
        //    }

        //    get
        //    {
        //        return isFullScreen;
        //    }
        //}

        public string FilePath
        {
            set
            {
                filepath = value;
            }

            get
            {
                return filepath;
            }
        }

        //public string FileName
        //{
        //    set
        //    {
        //        filename = value;
        //    }

        //    get
        //    {
        //        return filename;
        //    }
        //}

        //public PlayStateType PlayState
        //{
        //    set
        //    {
        //        currentState = value;
        //    }

        //    get
        //    {
        //        return currentState;
        //    }
        //}
        #endregion

        private IntPtr hDrain = IntPtr.Zero;

        //#if DEBUG
        //        private DsROTEntry rot = null;
        //#endif


        #region Initialization and set dependency properties

        public static readonly DependencyProperty SourceProperty = DependencyProperty.Register
            ( 
                "Source", 
                typeof(string), 
                typeof(DirectShowEVRControl), 
                new PropertyMetadata(null, OnSourceChanged)
            );

        public static readonly DependencyProperty LoadedBehaviorProperty = DependencyProperty.Register
            (
                "LoadedBehavior", 
                typeof(LoadedBehaviorType), 
                typeof(DirectShowEVRControl), 
                new PropertyMetadata(LoadedBehaviorType.Manual, OnLoadedBehaviorChanged)
            );


        public static readonly DependencyProperty AspectRatioModeProperty = DependencyProperty.Register
            (
                "AspectRatioMode", 
                typeof(AspectRatioType), 
                typeof(DirectShowEVRControl),
                new PropertyMetadata(AspectRatioType.DependOnOwner, OnAspectRatioModeChanged)
            );

        public static readonly DependencyProperty SpeedRateProperty = DependencyProperty.Register
            (
                "SpeedRate", typeof(double),
                typeof(DirectShowEVRControl),
                new PropertyMetadata(1.0, OnSpeedRateChanged)
            );

        public static readonly DependencyProperty LoopProperty = DependencyProperty.Register
            (
                "Loop", typeof(bool), 
                typeof(DirectShowEVRControl),
                new PropertyMetadata(false, OnLoopChanged)
            );

        public static readonly DependencyProperty VolumeProperty = DependencyProperty.Register
            (
                "Volume", 
                typeof(int), 
                typeof(DirectShowEVRControl),
                new PropertyMetadata(1, OnVolumeChanged)
            );

        public static readonly DependencyProperty FullScreenModeProperty = DependencyProperty.Register
           (
               "FullScreenMode",
               typeof(bool),
               typeof(DirectShowEVRControl),
               new PropertyMetadata(false, OnFullScreenModeChanged)
           );

        public string Source
        {
            set
            {
                filepath = value;
                SetValue(SourceProperty, value);
            }

            get
            {
                return (string)GetValue(SourceProperty);
            }
        }

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

        public double SpeedRate
        {
            set
            {
                SetValue(SpeedRateProperty, value);
            }

            get
            {
                return (double)GetValue(SpeedRateProperty);
            }
        }

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

        // IBasicAudio volume range is from –10,000 to 0.
        public int Volume
        {
            set
            {
                int convertedValue;

                if (value <= 0)
                {
                    convertedValue = VolumeSilence;
                }
                else
                {
                    convertedValue = (1- value) * -10000;
                }

                SetValue(VolumeProperty, convertedValue);
            }

            get
            {
                return (int)GetValue(VolumeProperty);
            }
        }

        public bool FullScreenMode
        {
            set
            {
                SetValue(FullScreenModeProperty, value);
            }

            get
            {
                return (bool)GetValue(FullScreenModeProperty);
            }
        }

        public DirectShowEVRControl()
        {
            InitializeComponent();
        }


        void DirectShowEVRControl_Loaded(object sender, RoutedEventArgs e)
        {
           // // Create the interop host control.
           //Integration.WindowsFormsHost host =
           //     new Integration.WindowsFormsHost();

           // // Create the control.
           //Forms.PictureBox picbox = new Forms.PictureBox();

           // // Assign the MaskedTextBox control as the host control's child.
           //host.Child = picbox;

           // // Add the interop host control to the Grid
           // // control's collection of child controls.
           // this.RootGrid.Children.Add(host);


           // //HwndSource source = (HwndSource)HwndSource.FromVisual(this);
           // owner = picbox.Handle;
        }

        public IntPtr GetOwnerHandle()
        {
            return EVRdocker.Handle;
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            switch (msg)
            {
                case WM_GRAPH_EVENT:
                    {
                        player.HandleGraphEvent(this);
                        break;
                    }
                default:
                    break;

            }
            return IntPtr.Zero;
        }

        private void DirectShowEVRControl_Unloaded(object sender, RoutedEventArgs e)
        {
            player.Dispose();
        }

        void DirectShowEVRControl_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (Source == null) return;

            if (Owner == IntPtr.Zero)
            {
                Owner = GetOwnerHandle();
                if (Owner != IntPtr.Zero)
                {
                    msgHwnd = PresentationSource.FromVisual(this) as HwndSource;
                    msgHwnd.AddHook(WndProc);
                    player = new DirectShowEVRPlayer(EVRdocker, msgHwnd.Handle, WM_GRAPH_EVENT);

                    player.OpenFile(filepath, Guid.Empty);
                    player.Play();
                    player.SetVolume(Volume);
                }
            }

            //transSZ = new Size(e.NewSize.Width * transformX, e.NewSize.Height * transformY);
            ////ResizeVideoWindow((int)transSZ.Width, (int)transSZ.Height);
        }

        private static void OnSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            DirectShowEVRControl dsp = d as DirectShowEVRControl;
            dsp.SourceChanged((string)e.NewValue);
        }

        protected virtual void SourceChanged(string absPath)
        {
            if (Owner == IntPtr.Zero)
            {
                Owner = GetOwnerHandle();
            }

            if (Owner != IntPtr.Zero && player == null)
            {
                msgHwnd = PresentationSource.FromVisual(this) as HwndSource;
                if (msgHwnd == null) return;
                msgHwnd.AddHook(WndProc);
                player = new DirectShowEVRPlayer(EVRdocker, msgHwnd.Handle, WM_GRAPH_EVENT);
            }

            player.Stop();
            player.OpenFile(absPath, Guid.Empty);

            switch (LoadedBehavior)
            {
                case LoadedBehaviorType.Pause:
                    player.Pause();
                    break;

                case LoadedBehaviorType.Play:
                    player.Play();
                    player.SetVolume(Volume);
                    break;
            
                case LoadedBehaviorType.Manual:
                default:
                    break;

            }
        }

        private static void OnLoadedBehaviorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            DirectShowEVRControl dsp = d as DirectShowEVRControl;
            dsp.LoadedBehaviorChanged((LoadedBehaviorType)e.NewValue);
        }

        protected virtual void LoadedBehaviorChanged(LoadedBehaviorType loadedBehaviorType)
        {
            LoadedBehavior = loadedBehaviorType;
        }

        private static void OnAspectRatioModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            DirectShowEVRControl dsp = d as DirectShowEVRControl;
            dsp.AspectRatioModeChanged((AspectRatioType)e.NewValue);
        }

        protected virtual void AspectRatioModeChanged(AspectRatioType aspectRatioType)
        {
            AspectRatioMode = aspectRatioType;
        }

        private static void OnLoopChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            DirectShowEVRControl dsp = d as DirectShowEVRControl;
            dsp.LoopChanged((bool)e.NewValue);
        }

        protected virtual void LoopChanged(bool loop)
        {
            Loop = loop;
        }

        private static void OnSpeedRateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            DirectShowEVRControl dsp = d as DirectShowEVRControl;
            dsp.SpeedRateModeChanged((double)e.NewValue);
        }

        protected virtual void SpeedRateModeChanged(double speedRate)
        {
            SpeedRate = speedRate;
        }

        private static void OnVolumeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            DirectShowEVRControl dsp = d as DirectShowEVRControl;
            dsp.VolumeChanged((int)e.NewValue);
        }

        protected virtual void VolumeChanged(int vol)
        {
            Volume = vol;
        }

        private static void OnFullScreenModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            DirectShowEVRControl dsp = d as DirectShowEVRControl;
            dsp.FullScreenModeChanged((bool)e.NewValue);
        }

        protected virtual void FullScreenModeChanged(bool fullscreen)
        {
            FullScreenMode = fullscreen;
        }
        #endregion


        #region Public Media Related Methods
        public void Play(string filepath)
        {
            player.OpenFile(filename, Guid.Empty);
            player.Play();
            player.SetVolume(Volume);
        }

        public void Play()
        {
            if (filepath != null)
            {
                player.Play();
                player.SetVolume(Volume);
            }
        }

        public void Pause()
        {
            player.Pause();
        }

        public void Stop()
        {
            player.Stop();
        }

        public void Close()
        {
            player.Stop();
        }

        public void Dispose()
        {
            player.Dispose();
        }


        public void SetMute(bool mute)
        {
        }

        public void SetVolume(int vol)
        {
        }


        private void SetSpeedRate(double rate)
        {
        }
        #endregion


        public void OnGraphEvent(EventCode eventCode, IntPtr param1, IntPtr param2)
        {
            switch (eventCode)
            {
                case EventCode.Complete:
                    player.SetPosition(0);
                    player.Play();
                    break;

                default:
                    break;
            }
        }
    }
}
