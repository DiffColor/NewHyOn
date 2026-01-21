using DirectShowLib;
using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using WndStyle = DirectShowLib.WindowStyle;
using Forms = System.Windows.Forms;
using Microsoft.Win32;
using MediaFoundation.EVR;
using MediaFoundation;
using MediaFoundation.Misc;
using System.Drawing;
using System.IO;

namespace HyOnPlayer
{
    public enum PlayStateType
    {
        Stopped,
        Paused,
        Running,
        Init
    };

    public enum MediaType
    {
        Audio,
        Video
    }

    public enum AspectRatioType 
    { 
        Maintain, 
        DependOnOwner 
    };

    public enum LoadedBehaviorType 
    { 
        Manual, 
        Play, 
        Pause 
    };

    public enum RenderType
    {
        VMR9,
        EVR
    };

    /// <summary>
    /// DirectShowControl.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class DirectShowControl : UserControl
    {
        public delegate void MediaEnded();
        public event MediaEnded MediaEndedEvent;

        private const int WMGraphNotify = 0x0400 + 13;
        private const int VolumeFull = 0;
        private const int VolumeSilence = -10000;

        private const int WM_APP = 0x8000;
        private const int WM_GRAPHNOTIFY = WM_APP + 1;
        private const int EC_COMPLETE = 0x01;
        private const int WS_CHILD = 0x40000000;
        private const int WS_CLIPCHILDREN = 0x2000000;

        private IGraphBuilder graphBuilder = null;
        private IMediaControl mediaControl = null;
        private IMediaEventEx mediaEventEx = null;
        private IVideoWindow videoWindow = null;
        private IBasicAudio basicAudio = null;
        private IBasicVideo basicVideo = null;
        private IMediaSeeking mediaSeeking = null;
        private IMediaPosition mediaPosition = null;
        private IVideoFrameStep frameStep = null;

        private IBaseFilter vmr9;
        private IVMRWindowlessControl9 windowlessCtrl;
        //private Compositor compositor;    //for drawing directx image

        private IBaseFilter evr;
        private IMFVideoDisplayControl m_pDisplay;
        private IMFVideoMixerControl m_pMixer;
        private IMFVideoPositionMapper m_pMapper; 
        private Guid m_clsidPresenter;   // CLSID of a custom presenter.

        private string filepath = string.Empty;
        private string filename = string.Empty;

        private bool isAudioOnly = false;
        private PlayStateType currentState = PlayStateType.Stopped;

        private IntPtr owner = IntPtr.Zero;
        private double transformX = 1;
        private double transformY = 1;
        private System.Windows.Size transSZ;

        HwndSource msgHwnd;

        #region dependency properties

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

        private IntPtr hDrain = IntPtr.Zero;

        #region Initialization and set dependency properties

        public static readonly DependencyProperty SourceProperty = DependencyProperty.Register
            ( 
                "Source", 
                typeof(string), 
                typeof(DirectShowControl), 
                new PropertyMetadata(null, OnSourceChanged)
            );

        public static readonly DependencyProperty LoadedBehaviorProperty = DependencyProperty.Register
            (
                "LoadedBehavior", 
                typeof(LoadedBehaviorType), 
                typeof(DirectShowControl), 
                new PropertyMetadata(LoadedBehaviorType.Manual, OnLoadedBehaviorChanged)
            );


        public static readonly DependencyProperty AspectRatioModeProperty = DependencyProperty.Register
            (
                "AspectRatioMode", 
                typeof(AspectRatioType), 
                typeof(DirectShowControl),
                new PropertyMetadata(AspectRatioType.Maintain, OnAspectRatioModeChanged)
            );

        public static readonly DependencyProperty SpeedRateProperty = DependencyProperty.Register
            (
                "SpeedRate", typeof(double),
                typeof(DirectShowControl),
                new PropertyMetadata(1.0, OnSpeedRateChanged)
            );

        public static readonly DependencyProperty LoopProperty = DependencyProperty.Register
            (
                "Loop", typeof(bool), 
                typeof(DirectShowControl),
                new PropertyMetadata(true, OnLoopChanged)
            );

        public static readonly DependencyProperty VolumeProperty = DependencyProperty.Register
            (
                "Volume", 
                typeof(double), 
                typeof(DirectShowControl),
                new PropertyMetadata(1.0, OnVolumeChanged)
            );

        public static readonly DependencyProperty FullScreenModeProperty = DependencyProperty.Register
           (
               "FullScreenMode",
               typeof(bool),
               typeof(DirectShowControl),
               new PropertyMetadata(false, OnFullScreenModeChanged)
           );

        public static readonly DependencyProperty RenderModeProperty = DependencyProperty.Register
           (
               "RenderMode",
               typeof(RenderType),
               typeof(DirectShowControl),
               new PropertyMetadata(RenderType.VMR9, OnRenderModeChanged)
           );

        private static void OnRenderModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            DirectShowControl dsp = d as DirectShowControl;
            dsp.RenderModeChanged((RenderType)e.NewValue);
        }

        private void RenderModeChanged(RenderType type)
        {
            RenderMode = type;
        }

        public RenderType RenderMode
        {
            set
            {
                SetValue(RenderModeProperty, value);
            }

            get
            {
                return (RenderType)GetValue(RenderModeProperty);
            }
        }

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

        int ConvertVolume(double vol)
        {
            if (vol <= 0)
                return VolumeSilence;
            else
                return (int)((10000 * vol) - 10000);
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
        #endregion


        public DirectShowControl()
        {
            InitializeComponent();
        }

        void DirectShowControl_Loaded(object sender, RoutedEventArgs e)
        {
        }

        public IntPtr GetOwnerHandle()
        {
            return DSdocker.Handle;
        }

        public void HandleGraphEvent()
        {
            EventCode evCode;
            IntPtr evParam1, evParam2;

            try
            {
                if (this.mediaEventEx == null)
                    return;

                this.mediaEventEx.GetEvent(out evCode, out evParam1, out evParam2, 0);
                this.mediaEventEx.FreeEventParams(evCode, evParam1, evParam2);

                if (evCode == EventCode.Complete)
                {
                    SetZeroPosIfLoop();

                    if (MediaEndedEvent != null)
                        MediaEndedEvent();
                }
            }
            catch (Exception e) { }
        }

        public void SetZeroPosIfLoop()
        {
            if (Loop)
            {
                DsLong pos = new DsLong(0);

                int hr = this.mediaSeeking.SetPositions(pos, AMSeekingSeekingFlags.AbsolutePositioning,
                    null, AMSeekingSeekingFlags.NoPositioning);
                if (hr < 0)
                    this.mediaPosition.put_CurrentPosition(0);

                return;
            }

            StopClip();
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            switch (msg)
            {
                case WMGraphNotify:
                    {
                        HandleGraphEvent();
                        break;
                    }
            }

            if (this.videoWindow != null)
                this.videoWindow.NotifyOwnerMessage(hwnd, msg, wParam, lParam);

            return IntPtr.Zero;
        }

        private void DirectShowControl_Unloaded(object sender, RoutedEventArgs e)
        {
            DisposeControl();
        }

        void DirectShowControl_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            transSZ = new System.Windows.Size(e.NewSize.Width * transformX, e.NewSize.Height * transformY);
            ResizeVideoWindow((int)transSZ.Width, (int)transSZ.Height);

            if (Source == null || File.Exists(Source) == false)
            {
                Source = filepath = null;
                return;
            }

            if (Owner == IntPtr.Zero)
            {
                Owner = GetOwnerHandle();
                if (Owner != IntPtr.Zero && LoadedBehavior == LoadedBehaviorType.Play)
                {
                    PlayClip();
                }
            }

            if (msgHwnd == null)
            {
                try
                {
                    msgHwnd = PresentationSource.FromVisual(this) as HwndSource;
                    msgHwnd.AddHook(WndProc);
                    if (mediaEventEx == null) return;
                    this.mediaEventEx.SetNotifyWindow(msgHwnd.Handle, WMGraphNotify, IntPtr.Zero);
                }
                catch (Exception ex)
                {

                }
            }
        }

        public void SetWindowSize()
        {
            transSZ = new System.Windows.Size(RootGrid.ActualWidth * transformX, RootGrid.ActualHeight * transformY);
            ResizeVideoWindow((int)transSZ.Width, (int)transSZ.Height);

            if (msgHwnd == null)
            {
                try
                {
                    msgHwnd = PresentationSource.FromVisual(this) as HwndSource;
                    if (msgHwnd == null) return;
                    msgHwnd.AddHook(WndProc);
                    this.mediaEventEx.SetNotifyWindow(msgHwnd.Handle, WMGraphNotify, IntPtr.Zero);
                }
                catch
                {

                }
            }
        }
        
        private static void OnSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            DirectShowControl dsp = d as DirectShowControl;
            dsp.SourceChanged((string)e.NewValue);
        }

        protected virtual void SourceChanged(string absPath)
        {
            if (string.IsNullOrEmpty(absPath) || File.Exists(absPath) == false)
            {
                filepath = null;
                Stop();
                return;
            }

            filepath = absPath;

            if (Owner == IntPtr.Zero)
                Owner = GetOwnerHandle();

            LoadClip(absPath);

            switch (LoadedBehavior)
            {
                case LoadedBehaviorType.Pause:
                    PlayClip();
                    PauseClip();
                    break;

                case LoadedBehaviorType.Play:
                    PlayClip();
                    break;
            
                case LoadedBehaviorType.Manual:
                default:
                    break;

            }
        }

        private static void OnLoadedBehaviorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            DirectShowControl dsp = d as DirectShowControl;
            dsp.LoadedBehaviorChanged((LoadedBehaviorType)e.NewValue);
        }

        protected virtual void LoadedBehaviorChanged(LoadedBehaviorType loadedBehaviorType)
        {
            LoadedBehavior = loadedBehaviorType;
        }

        private static void OnAspectRatioModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            DirectShowControl dsp = d as DirectShowControl;
            dsp.AspectRatioModeChanged((AspectRatioType)e.NewValue);
        }

        protected virtual void AspectRatioModeChanged(AspectRatioType aspectRatioType)
        {
            AspectRatioMode = aspectRatioType;
        }

        VMR9AspectRatioMode ConvertAspectRatioMode(AspectRatioType type)
        {
            return type == AspectRatioType.Maintain ? VMR9AspectRatioMode.LetterBox : VMR9AspectRatioMode.None;
        }

        private static void OnLoopChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            DirectShowControl dsp = d as DirectShowControl;
            dsp.LoopChanged((bool)e.NewValue);
        }

        protected virtual void LoopChanged(bool loop)
        {
            Loop = loop;
        }

        private static void OnSpeedRateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            DirectShowControl dsp = d as DirectShowControl;
            dsp.SpeedRateModeChanged((double)e.NewValue);
        }

        protected virtual void SpeedRateModeChanged(double speedRate)
        {
            SpeedRate = speedRate;
        }

        private static void OnVolumeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            DirectShowControl dsp = d as DirectShowControl;
            dsp.VolumeChanged((double)e.NewValue);
        }

        protected virtual void VolumeChanged(double vol)
        {
            Volume = vol;
        }

        private static void OnFullScreenModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            DirectShowControl dsp = d as DirectShowControl;
            dsp.FullScreenModeChanged((bool)e.NewValue);
        }

        protected virtual void FullScreenModeChanged(bool fullscreen)
        {
            FullScreenMode = fullscreen;
        }
        #endregion


        #region Init Video Window
        private int InitVideoWindow(int nMultiplier, int nDivider)
        {
            int hr = 0;
            int lHeight, lWidth;

            if (this.basicVideo == null)
                return 0;

            hr = this.basicVideo.GetVideoSize(out lWidth, out lHeight);

            if (hr == DsResults.E_NoInterface)
                return 0;

            lWidth = lWidth * nMultiplier / nDivider;
            lHeight = lHeight * nMultiplier / nDivider;
            

            hr = this.videoWindow.SetWindowPosition(0, 0, lWidth, lHeight);

            return hr;
        }

        private void ConfigureVMR9InWindowlessMode()
        {
            int hr = 0;

            IVMRFilterConfig9 filterConfig = (IVMRFilterConfig9)vmr9;

            hr = filterConfig.SetNumberOfStreams(1);

            hr = filterConfig.SetRenderingMode(VMR9Mode.Windowless);

            windowlessCtrl = (IVMRWindowlessControl9)vmr9;

            hr = windowlessCtrl.SetVideoClippingWindow(Owner);
            
            hr = windowlessCtrl.SetAspectRatioMode(ConvertAspectRatioMode(AspectRatioMode));
        }

        private void AddHandlers()
        {
            DSdocker.Paint += new Forms.PaintEventHandler(DSdocker_Paint); // for WM_PAINT
            DSdocker.Resize += new EventHandler(DSdocker_ResizeMove); // for WM_SIZE
            DSdocker.Move += new EventHandler(DSdocker_ResizeMove); // for WM_MOVE
            SystemEvents.DisplaySettingsChanged += new EventHandler(SystemEvents_DisplaySettingsChanged); // for WM_DISPLAYCHANGE
        }

        private void RemoveHandlers()
        {
            if (msgHwnd != null)
            {
                msgHwnd.RemoveHook(WndProc);
            }

            DSdocker.Paint -= DSdocker_Paint; // for WM_PAINT
            DSdocker.Resize -= DSdocker_ResizeMove; // for WM_SIZE
            DSdocker.Move -= DSdocker_ResizeMove; // for WM_MOVE
            SystemEvents.DisplaySettingsChanged -= SystemEvents_DisplaySettingsChanged; // for WM_DISPLAYCHANGE
        }

        private void DSdocker_ResizeMove(object sender, EventArgs e)
        {
            if (windowlessCtrl != null)
            {
                int hr = windowlessCtrl.SetVideoPosition(null, DsRect.FromRectangle(DSdocker.ClientRectangle));
            }
            else
            {
                if (m_pDisplay != null)
                {
                    int hr = m_pDisplay.SetVideoPosition(null, new MFRect(DSdocker.ClientRectangle));
                }
            }
        }

        private void SystemEvents_DisplaySettingsChanged(object sender, EventArgs e)
        {
            if (windowlessCtrl != null)
            {
                int hr = windowlessCtrl.DisplayModeChanged();
            }
        }

        private void DSdocker_Paint(object sender, Forms.PaintEventArgs e)
        {
            try
            {
                if (windowlessCtrl != null)
                {
                    IntPtr hdc = e.Graphics.GetHdc();
                    int hr = windowlessCtrl.RepaintVideo(DSdocker.Handle, hdc);
                    e.Graphics.ReleaseHdc(hdc);
                }
            }
            catch (Exception ex)
            {
            }
        }

        #endregion


        #region Playback Methods
        private void LoadClip(string filepath)
        {
            int hr = 0;
            try
            {
                if (Owner == null) return;

                if (filepath == string.Empty)
                    return;

                this.graphBuilder = (IGraphBuilder)new FilterGraph();
                
                if(RenderMode == RenderType.EVR) 
                {
                    evr = (IBaseFilter) new EnhancedVideoRenderer();
                    hr = graphBuilder.AddFilter(evr, "EVR");
                    InitializeEVR(evr, 0, out m_pDisplay);
                }
                else
                {
                    vmr9 = (IBaseFilter)new VideoMixingRenderer9();
                    ConfigureVMR9InWindowlessMode();
                    hr = graphBuilder.AddFilter(vmr9, "Video Mixing Renderer 9");
                }
                AddHandlers();
                DSdocker_ResizeMove(null, null);

                hr = this.graphBuilder.RenderFile(filepath, null);

                this.mediaControl = (IMediaControl)this.graphBuilder;
                this.mediaEventEx = (IMediaEventEx)this.graphBuilder;
                this.mediaSeeking = (IMediaSeeking)this.graphBuilder;
                this.mediaPosition = (IMediaPosition)this.graphBuilder;

                this.videoWindow = this.graphBuilder as IVideoWindow;
                this.basicVideo = this.graphBuilder as IBasicVideo;

                this.basicAudio = this.graphBuilder as IBasicAudio;

                CheckVisibility();

                if (msgHwnd != null)
                {
                    msgHwnd.AddHook(WndProc);
                    hr = this.mediaEventEx.SetNotifyWindow(msgHwnd.Handle, WMGraphNotify, IntPtr.Zero);
                }

                if (!this.isAudioOnly)
                {
                    hr = this.videoWindow.put_Owner(Owner);

                    hr = this.videoWindow.put_WindowStyle(WndStyle.Child | WndStyle.ClipSiblings | WndStyle.ClipChildren);

                    hr = InitVideoWindow(1, 1);

                    GetFrameStepInterface();
                }

                SetWindowSize();
            }
            catch (Exception e)
            {
            }
        }

        private void InitializeEVR(IBaseFilter pEVR, int dwStreams, out IMFVideoDisplayControl ppDisplay)
        {
            IMFVideoRenderer pRenderer;
            IMFVideoDisplayControl pDisplay;
            IEVRFilterConfig pConfig;
            IMFVideoPresenter pPresenter;

            if (m_clsidPresenter != Guid.Empty)
            {
                Type type = Type.GetTypeFromCLSID(m_clsidPresenter);

                try
                {
                    pPresenter = (IMFVideoPresenter)Activator.CreateInstance(type);

                    pRenderer = (IMFVideoRenderer)pEVR;

                    pRenderer.InitializeRenderer(null, pPresenter);
                }
                finally
                {
                    //Marshal.ReleaseComObject(pPresenter);
                }
            }

            object o;
            IMFGetService pGetService = null;
            pGetService = (IMFGetService)pEVR;
            pGetService.GetService(MFServices.MR_VIDEO_RENDER_SERVICE, typeof(IMFVideoDisplayControl).GUID, out o);

            try
            {
                pDisplay = (IMFVideoDisplayControl)o;
            }
            catch
            {
                Marshal.ReleaseComObject(o);
                throw;
            }

            try
            {
                pDisplay.SetVideoWindow(DSdocker.Handle);

                if(AspectRatioMode == AspectRatioType.DependOnOwner) {
                    pDisplay.SetAspectRatioMode(MFVideoAspectRatioMode.None);
                }

                if (dwStreams > 1)
                {
                    pConfig = (IEVRFilterConfig)pEVR;
                    pConfig.SetNumberOfStreams(dwStreams);
                }

                Rectangle r = DSdocker.ClientRectangle;
                MFRect rc = new MFRect(r.Left, r.Top, r.Right, r.Bottom);

                pDisplay.SetVideoPosition(null, rc);

                ppDisplay = pDisplay;
            }
            finally
            {
                //Marshal.ReleaseComObject(pDisplay);
            }
            m_pMixer = null;
        }

        private void PlayClip()
        {
            try
            {
                DSdocker.Visible = true;

                int hr = 0;
                hr = this.mediaControl.Run();

                SetSpeedRate(SpeedRate);
                SetVolume(ConvertVolume(Volume));

                this.currentState = PlayStateType.Running;
            }
            catch
            {
            }
        }

        private void PauseClip()
        {
            if (this.mediaControl == null)
                return;

            if ((this.currentState == PlayStateType.Paused) || (this.currentState == PlayStateType.Stopped))
            {
                if (this.mediaControl.Run() >= 0)
                    this.currentState = PlayStateType.Running;
            }
            else
            {
                if (this.mediaControl.Pause() >= 0)
                    this.currentState = PlayStateType.Paused;
            }
        }


        private void StopClip()
        {
            int hr = 0;
            DsLong pos = new DsLong(0);

            if ((this.mediaControl == null) || (this.mediaSeeking == null))
                return;

            if ((this.currentState == PlayStateType.Paused) || (this.currentState == PlayStateType.Running))
            {
                hr = this.mediaControl.Stop();
                this.currentState = PlayStateType.Stopped;

                hr = this.mediaSeeking.SetPositions(pos, AMSeekingSeekingFlags.AbsolutePositioning, null, AMSeekingSeekingFlags.NoPositioning);
            }

            DSdocker.Visible = false;
        }


        private void DisposeControl()
        {
            int hr = 0;

            if (this.mediaControl != null)
                hr = this.mediaControl.Stop();

            this.currentState = PlayStateType.Stopped;
            this.isAudioOnly = true;

            CloseInterfaces();

            FilePath = string.Empty;
            Source = null;

            this.currentState = PlayStateType.Init;

        }
        #endregion


        #region Event Methods
        private void ResizeVideoWindow(int width, int height)
        {
            int hr = 0;

            if (this.videoWindow != null && !this.isAudioOnly)
                hr = this.videoWindow.SetWindowPosition(0, 0, width, height);
        }

        private void MoveVideoWindow(int left, int top)
        {
            int hr = 0;

            if (this.videoWindow != null)
                hr = this.videoWindow.SetWindowPosition(left, top, (int)transSZ.Width, (int)transSZ.Height);
        }
        private void CheckVisibility()
        {
            int hr = 0;
            OABool lVisible;

            if ((this.videoWindow == null) || (this.basicVideo == null))
            {
                this.isAudioOnly = true;
                return;
            }
            else
            {
                this.isAudioOnly = false;
            }

            hr = this.videoWindow.get_Visible(out lVisible);
            if (hr < 0)
            {
                if (hr == unchecked((int)0x80004002)) //E_NOINTERFACE
                    this.isAudioOnly = true;
            }
        }
        
        private bool GetFrameStepInterface()
        {
            int hr = 0;

            IVideoFrameStep frameStepTest = null;

            frameStepTest = (IVideoFrameStep)this.graphBuilder;

            hr = frameStepTest.CanStep(0, null);
            if (hr == 0)
            {
                this.frameStep = frameStepTest;
                return true;
            }
            else
            {
                this.frameStep = null;
                return false;
            }
        }
        #endregion

        #region Public Media Related Methods
        public void Play(string filepath)
        {
            LoadClip(filepath);
            PlayClip();
        }

        public void Play()
        {
            if (filepath != null)
            {
                PlayClip();
            }
        }

        public void Pause()
        {
            PauseClip();
        }

        public void Stop()
        {
            StopClip();
        }


        public void Close()
        {
            StopClip();

            FilePath = string.Empty;
            Source = null;
        }

        public void Dispose()
        {
            DisposeControl();
        }


        public void SetMute(bool mute)
        {
            int hr = 0;

            if ((this.graphBuilder == null) || (this.basicAudio == null))
                return;

            int currentVol;

            hr = this.basicAudio.get_Volume(out currentVol);

            if(Volume != currentVol) Volume = currentVol;

            if (hr < 0)
                return;

            if (Volume == VolumeFull)
                Volume = VolumeSilence;
            else
                Volume = VolumeFull;

            hr = this.basicAudio.put_Volume(ConvertVolume(Volume));

            return;
        }

        public void SetVolume(int vol)
        {
            int hr = 0;

            if ((this.graphBuilder == null) || (this.basicAudio == null))
                return;
            
            hr = this.basicAudio.put_Volume(vol);
            return;
        }

        public void SetFullScreen(bool isFullScreenMode)
        {
            int hr = 0;
            OABool lMode;

            if ((this.isAudioOnly) || (this.videoWindow == null))
                return;

            hr = this.videoWindow.get_FullScreenMode(out lMode);

            if (lMode == OABool.False)
            {
                hr = this.videoWindow.get_MessageDrain(out hDrain);

                HwndSource source = PresentationSource.FromDependencyObject(Parent) as HwndSource;
                hr = this.videoWindow.put_MessageDrain(source.Handle);

                lMode = OABool.True;
                hr = this.videoWindow.put_FullScreenMode(lMode);
            }
            else
            {
                lMode = OABool.False;
                hr = this.videoWindow.put_FullScreenMode(lMode);

                hr = this.videoWindow.put_MessageDrain(hDrain);

                hr = this.videoWindow.SetWindowForeground(OABool.True);
            }

            FullScreenMode = isFullScreenMode;

            return;
        }

        public void StepOneFrame()
        {
            int hr = 0;

            if (this.frameStep != null)
            {
                if (this.currentState != PlayStateType.Paused)
                    PauseClip();

                hr = this.frameStep.Step(1, null);
            }

            return;
        }

        private void StepFrames(int nFramesToStep)
        {
            int hr = 0;

            if (this.frameStep != null)
            {
                hr = this.frameStep.CanStep(nFramesToStep, null);
                if (hr == 0)
                {
                    if (this.currentState != PlayStateType.Paused)
                        PauseClip();

                    hr = this.frameStep.Step(nFramesToStep, null);
                }
            }

            return;
        }


        private void SetSpeedRate(double rate)
        {
            int hr = 0;

            if (this.mediaPosition != null)
                hr = this.mediaPosition.put_Rate(rate);

            return;
        }

        private void ModifyRate(double dRateAdjust)
        {
            int hr = 0;
            double dRate;

            if ((this.mediaPosition != null) && (dRateAdjust != 0.0))
            {
                hr = this.mediaPosition.get_Rate(out dRate);
                if (hr == 0)
                {
                    double dNewRate = dRate + dRateAdjust;
                    hr = this.mediaPosition.put_Rate(dNewRate);

                    if (hr == 0)
                        SpeedRate = dNewRate;
                }
            }

            return;
        }
        #endregion

        #region Resource Managements
        private void CloseInterfaces()
        {
            int hr = 0;

            try
            {
                lock (this)
                {
                    if (!this.isAudioOnly)
                    {
                        hr = this.videoWindow.put_Visible(OABool.False);
                        hr = this.videoWindow.put_Owner(IntPtr.Zero);
                    }

                    if (this.mediaEventEx != null)
                        hr = this.mediaEventEx.SetNotifyWindow(IntPtr.Zero, 0, IntPtr.Zero);

                    RemoveHandlers();

                    if (vmr9 != null)
                    {
                        Marshal.ReleaseComObject(vmr9);
                        vmr9 = null;
                        windowlessCtrl = null;
                    }

                    if (evr != null)
                    {
                        Marshal.ReleaseComObject(evr);
                        evr = null;
                    }

                    if (m_pDisplay != null)
                    {
                        Marshal.ReleaseComObject(m_pDisplay);
                        m_pDisplay = null;
                    }

                    if (m_pMapper != null)
                    {
                        Marshal.ReleaseComObject(m_pMapper);
                        m_pMapper = null;
                    }

                    if (m_pMixer != null)
                    {
                        Marshal.ReleaseComObject(m_pMixer);
                        m_pMixer = null;
                    }

                    if (this.mediaEventEx != null)
                        this.mediaEventEx = null;
                    if (this.mediaSeeking != null)
                        this.mediaSeeking = null;
                    if (this.mediaPosition != null)
                        this.mediaPosition = null;
                    if (this.mediaControl != null)
                        this.mediaControl = null;
                    if (this.basicAudio != null)
                        this.basicAudio = null;
                    if (this.basicVideo != null)
                        this.basicVideo = null;
                    if (this.videoWindow != null)
                        this.videoWindow = null;
                    if (this.frameStep != null)
                        this.frameStep = null;
                    if (this.graphBuilder != null)
                        Marshal.ReleaseComObject(this.graphBuilder); this.graphBuilder = null;

                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                }
            }
            catch
            {
            }
        }
        #endregion

        public void SetPosition(double pos)
        {
            this.mediaPosition.put_CurrentPosition(pos);
        }
    }
}
