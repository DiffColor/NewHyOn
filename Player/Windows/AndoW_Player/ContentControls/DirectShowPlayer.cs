
using DirectShowLib;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using WndStyle = DirectShowLib.WindowStyle;

namespace HyOnPlayer_WPF_DS
{
    //public enum PlayStateType
    //{
    //    Stopped,
    //    Paused,
    //    Running,
    //    Init
    //};

    //public enum MediaType
    //{
    //    Audio,
    //    Video
    //}

    //public enum StretchType { None, Uniform, UniformToFill, Fill };
    //public enum LoadedBehaviorType { Play, Pause, Stop, Close, Manual };

    class DirectShowPlayer : FrameworkElement
    {
        //private const int WMGraphNotify = 0x0400 + 13;
        //private const int VolumeFull = 0;
        //private const int VolumeSilence = -10000;

        //private IGraphBuilder graphBuilder = null;
        //private IMediaControl mediaControl = null;
        //private IMediaEventEx mediaEventEx = null;
        //private IVideoWindow videoWindow = null;
        //private IBasicAudio basicAudio = null;
        //private IBasicVideo basicVideo = null;
        //private IMediaSeeking mediaSeeking = null;
        //private IMediaPosition mediaPosition = null;
        //private IVideoFrameStep frameStep = null;

        //private string filepath = string.Empty;
        //private string filename = string.Empty;
        //private bool isAudioOnly = false;
        //private bool isFullScreen = false;
        //private PlayStateType currentState = PlayStateType.Stopped;
        //private double currentPlaybackRate = 1.0;

        //private IntPtr owner = IntPtr.Zero;
        //private Point loc;
        //private double transformX = 1;
        //private double transformY = 1;

        //public IntPtr Owner 
        //{
        //    set
        //    {
        //        owner = value;
        //    }
        //    get
        //    {
        //        return owner;
        //    }
        //}

        //public double TransformX
        //{
        //    set
        //    {
        //        transformX = value;
        //    }
        //    get
        //    {
        //        return transformX;
        //    }
        //}

        //public double TransformY
        //{
        //    set
        //    {
        //        transformY = value;
        //    }
        //    get
        //    {
        //        return transformY;
        //    }
        //}

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

        //public string FilePath
        //{
        //    set
        //    {
        //        filepath = value;
        //    }

        //    get
        //    {
        //        return filepath;
        //    }
        //}

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

        //private IntPtr hDrain = IntPtr.Zero;

        //#if DEBUG
        //private DsROTEntry rot = null;
        //#endif

        //#region Initialization and set dependency properties

        //#region Properties
        //public static readonly DependencyProperty SourceProperty;
        //public static readonly DependencyProperty StretchProperty;
        //public static readonly DependencyProperty LoadedBehaviorProperty;
        //public static readonly DependencyProperty SpeedRatioProperty;
        //public static readonly DependencyProperty LoopProperty;
        //public static readonly DependencyProperty VolumeProperty;

        //public string SourcePath
        //{
        //    set
        //    {
        //        SetValue(SourceProperty, value);
        //    }

        //    get
        //    {
        //        return (string)GetValue(SourceProperty);
        //    }
        //}

        //public StretchType Stretch
        //{
        //    set
        //    {
        //        SetValue(StretchProperty, value);
        //    }

        //    get
        //    {
        //        return (StretchType)GetValue(StretchProperty);
        //    }
        //}

        //public LoadedBehaviorType LoadedBehavior
        //{
        //    set
        //    {
        //        SetValue(LoadedBehaviorProperty, value);
        //    }

        //    get
        //    {
        //        return (LoadedBehaviorType)GetValue(LoadedBehaviorProperty);
        //    }
        //}

        //public double SpeedRatio
        //{
        //    set
        //    {
        //        SetValue(SpeedRatioProperty, value);
        //    }

        //    get
        //    {
        //        return double.Parse((string)GetValue(SpeedRatioProperty));
        //    }
        //}

        //public bool Loop
        //{
        //    set
        //    {
        //        SetValue(LoopProperty, value);
        //    }

        //    get
        //    {
        //        return bool.Parse((string)GetValue(LoopProperty));
        //    }
        //}

        //// IBasicAudio volume range is from –10,000 to 0.
        //public int Volume
        //{
        //    set
        //    {
        //        int convertedValue;

        //        if (value <= 0)
        //        {
        //            convertedValue = VolumeSilence;
        //        }
        //        else
        //        {
        //            convertedValue = (100-value) * -100;
        //        }

        //        SetValue(VolumeProperty, value);
        //    }

        //    get
        //    {
        //        return int.Parse((string)GetValue(VolumeProperty));
        //    }
        //}
        //#endregion

//        public DirectShowPlayer()
//        {
//            Loaded += DirectShowPlayer_Loaded;
//            SizeChanged += DirectShowPlayer_SizeChanged;
//        }

//        void DirectShowPlayer_Loaded(object sender, RoutedEventArgs e)
//        {
//            HwndSource source = PresentationSource.FromVisual(this) as HwndSource;
//            source.AddHook(WndProc);
//        }

//        void DirectShowPlayer_SizeChanged(object sender, SizeChangedEventArgs e)
//        {
//            System.Console.WriteLine(this.ActualWidth + " / " + this.Height + " / " + e.NewSize.Width + " / " + e.NewSize.Height);
//            if (owner == IntPtr.Zero)
//            {
//                owner = GetOwnerHandle();
//                if (owner != IntPtr.Zero && LoadedBehavior == LoadedBehaviorType.Play) Play();
//            }

//            //ResizeVideoWindow((int)(e.NewSize.Width * transformX), (int)(e.NewSize.Height * transformY));
//        }

//        static DirectShowPlayer()
//        {
//            SourceProperty = DependencyProperty.Register("Source", typeof(string), typeof(DirectShowPlayer),
//                                new PropertyMetadata(null, OnSourceChanged));

//            LoadedBehaviorProperty = DependencyProperty.Register("LoadedBehavior", typeof(LoadedBehaviorType), typeof(DirectShowPlayer), 
//                                        new PropertyMetadata(LoadedBehaviorType.Manual, OnLoadedBehaviorChanged));

//            StretchProperty = DependencyProperty.Register("Stretch", typeof(StretchType), typeof(DirectShowPlayer),
//                                new PropertyMetadata(StretchType.None, OnStretchChanged));
            
//            SpeedRatioProperty = DependencyProperty.Register("SpeedRatio", typeof(double), typeof(DirectShowPlayer), 
//                                    new PropertyMetadata(1.0, OnSpeedRatioChanged));
            
//            LoopProperty = DependencyProperty.Register("Loop", typeof(bool), typeof(DirectShowPlayer), 
//                                new PropertyMetadata(false, OnLoopChanged));
            
//            VolumeProperty = DependencyProperty.Register("Volume", typeof(int), typeof(DirectShowPlayer), 
//                                new PropertyMetadata(100, OnVolumeChanged));

//        }

//        private static void OnSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
//        {
//            DirectShowPlayer dsp = d as DirectShowPlayer;
//            dsp.SourceChanged((string)e.NewValue);
//        }

//        protected virtual void SourceChanged(string absPath)
//        {
//            filepath = absPath;
//            if (owner != IntPtr.Zero)
//            {
//                StopClip();
//                Play();
//            }
//        }

//        private static void OnLoadedBehaviorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
//        {
//        }

//        private static void OnStretchChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
//        {
//        }

//        private static void OnLoopChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
//        {
//        }

//        private static void OnSpeedRatioChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
//        {
//        }

//        private static void OnVolumeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
//        {
//        }

//        //private IntPtr GetOwnerHandle()
//        //{
//        //    IntPtr hWnd = IntPtr.Zero;

//        //    //DependencyObject parentObject = VisualTreeHelper.GetParent(this);
//        //    //if (parentObject is UIElement)
//        //    //{
//        //    //    HwndSource source = (HwndSource)HwndSource.FromDependencyObject(parentObject);
//        //    //    hWnd = source.Handle;
//        //    //}
//        //    return hWnd;
//        //}

//        public IntPtr GetOwnerHandle()
//        {
//            HwndSource source = (HwndSource)HwndSource.FromVisual(this);
//            return source.Handle;
//        }

//        #endregion

//        #region Play Methods
//        private void Play()
//        {
//            try
//            {
//                if (owner == null) return;
//                // If no filename specified by command line, show file open dialog
//                if (FilePath == string.Empty)
//                {
//                    return;
//                }

//                // Start playing the media file
//                PlayMovieInWindow(filepath);
//            }
//            catch
//            {
//                CloseClip();
//            }
//        }

//        private void PlayMovieInWindow(string filename)
//        {
//            int hr = 0;

//            if (filename == string.Empty)
//                return;

//            this.graphBuilder = (IGraphBuilder)new FilterGraph();

//            // Have the graph builder construct its the appropriate graph automatically
//            hr = this.graphBuilder.RenderFile(filename, null);
//            DsError.ThrowExceptionForHR(hr);

//            // QueryInterface for DirectShow interfaces
//            this.mediaControl = (IMediaControl)this.graphBuilder;
//            this.mediaEventEx = (IMediaEventEx)this.graphBuilder;
//            this.mediaSeeking = (IMediaSeeking)this.graphBuilder;
//            this.mediaPosition = (IMediaPosition)this.graphBuilder;

//            // Query for video interfaces, which may not be relevant for audio files
//            this.videoWindow = this.graphBuilder as IVideoWindow;
//            this.basicVideo = this.graphBuilder as IBasicVideo;

//            // Query for audio interfaces, which may not be relevant for video-only files
//            this.basicAudio = this.graphBuilder as IBasicAudio;

//            // Is this an audio-only file (no video component)?
//            CheckVisibility();

//            // Have the graph signal event via window callbacks for performance
//            hr = this.mediaEventEx.SetNotifyWindow(owner, WMGraphNotify, IntPtr.Zero);
//            DsError.ThrowExceptionForHR(hr);

//            if (!this.isAudioOnly)
//            {
//                // Setup the video window
//                hr = this.videoWindow.put_Owner(owner);
//                DsError.ThrowExceptionForHR(hr);

//                hr = this.videoWindow.put_WindowStyle(WndStyle.Child | WndStyle.ClipSiblings | WndStyle.ClipChildren);
//                DsError.ThrowExceptionForHR(hr);

//                hr = InitVideoWindow(1, 1);
//                DsError.ThrowExceptionForHR(hr);

//                GetFrameStepInterface();
//            }
//            else
//            {
//                // Initialize the default player size and enable playback menu items
//                //hr = InitPlayerWindow();
//                DsError.ThrowExceptionForHR(hr);

//                //EnablePlaybackMenu(true, MediaType.Audio);
//            }

//            // Complete window initialization
//            //CheckSizeMenu(menuFileSizeNormal);
//            this.isFullScreen = false;
//            this.currentPlaybackRate = 1.0;
//            //UpdateMainTitle();

//            #if DEBUG
//            rot = new DsROTEntry(this.graphBuilder);
//            #endif

//            //this.Focus();

//            // Run the graph to play the media file
//            hr = this.mediaControl.Run();
//            DsError.ThrowExceptionForHR(hr);

//            this.currentState = PlayStateType.Running;
//        }

//        private void CloseClip()
//        {
//            int hr = 0;

//            // Stop media playback
//            if (this.mediaControl != null)
//                hr = this.mediaControl.Stop();

//            // Clear global flags
//            this.currentState = PlayStateType.Stopped;
//            this.isAudioOnly = true;
//            this.isFullScreen = false;

//            // Free DirectShow interfaces
//            CloseInterfaces();

//            // Clear file name to allow selection of new file with open dialog
//            FilePath = string.Empty;

//            // No current media state
//            this.currentState = PlayStateType.Init;

//            //UpdateMainTitle();
//            //InitPlayerWindow();
//        }

//        private void PauseClip()
//        {
//            if (this.mediaControl == null)
//                return;

//            // Toggle play/pause behavior
//            if ((this.currentState == PlayStateType.Paused) || (this.currentState == PlayStateType.Stopped))
//            {
//                if (this.mediaControl.Run() >= 0)
//                    this.currentState = PlayStateType.Running;
//            }
//            else
//            {
//                if (this.mediaControl.Pause() >= 0)
//                    this.currentState = PlayStateType.Paused;
//            }

//            //UpdateMainTitle();
//        }

//        public void StopClip()
//        {
//            int hr = 0;
//            DsLong pos = new DsLong(0);

//            if ((this.mediaControl == null) || (this.mediaSeeking == null))
//                return;

//            // Stop and reset postion to beginning
//            if ((this.currentState == PlayStateType.Paused) || (this.currentState == PlayStateType.Running))
//            {
//                hr = this.mediaControl.Stop();
//                this.currentState = PlayStateType.Stopped;

//                // Seek to the beginning
//                hr = this.mediaSeeking.SetPositions(pos, AMSeekingSeekingFlags.AbsolutePositioning, null, AMSeekingSeekingFlags.NoPositioning);

//                // Display the first frame to indicate the reset condition
//                hr = this.mediaControl.Pause();
//            }
//            //UpdateMainTitle();
//        }

//        #endregion



//        private int InitVideoWindow(int nMultiplier, int nDivider)
//        {
//            int hr = 0;
//            int lHeight, lWidth;

//            if (this.basicVideo == null)
//                return 0;

//            // Read the default video size
//            hr = this.basicVideo.GetVideoSize(out lWidth, out lHeight);
//            if (hr == DsResults.E_NoInterface)
//                return 0;

//            //EnablePlaybackMenu(true, MediaType.Video);

//            // Account for requests of normal, half, or double size
//            lWidth = lWidth * nMultiplier / nDivider;
//            lHeight = lHeight * nMultiplier / nDivider;

//            //this.ClientSize = new Size(lWidth, lHeight);
//            //Application.DoEvents();

//            hr = this.videoWindow.SetWindowPosition(0, 0, lWidth, lHeight);

//            return hr;
//        }

//        private void MoveVideoWindow(int left, int top)
//        {
//            int hr = 0;

//            // track the movement of the container window and resize as needed
//            if (this.videoWindow != null)
//            {
//                hr = this.videoWindow.SetWindowPosition(left, top, (int)this.Width, (int)this.Height);
//                DsError.ThrowExceptionForHR(hr);
//            }
//        }

//        private void ResizeVideoWindow(int width, int height)
//        {
//            int hr = 0;

//            // track the movement of the container window and resize as needed
//            if (this.videoWindow != null)
//            {
//                hr = this.videoWindow.SetWindowPosition(0, 0, width, height);

//                DsError.ThrowExceptionForHR(hr);
//            }
//        }

//        private void CheckVisibility()
//        {
//            int hr = 0;
//            OABool lVisible;

//            if ((this.videoWindow == null) || (this.basicVideo == null))
//            {
//                // Audio-only files have no video interfaces.  This might also
//                // be a file whose video component uses an unknown video codec.
//                this.isAudioOnly = true;
//                return;
//            }
//            else
//            {
//                // Clear the global flag
//                this.isAudioOnly = false;
//            }

//            hr = this.videoWindow.get_Visible(out lVisible);
//            if (hr < 0)
//            {
//                // If this is an audio-only clip, get_Visible() won't work.
//                //
//                // Also, if this video is encoded with an unsupported codec,
//                // we won't see any video, although the audio will work if it is
//                // of a supported format.
//                if (hr == unchecked((int)0x80004002)) //E_NOINTERFACE
//                {
//                    this.isAudioOnly = true;
//                }
//                else
//                    DsError.ThrowExceptionForHR(hr);
//            }
//        }

//        //
//        // Some video renderers support stepping media frame by frame with the
//        // IVideoFrameStep interface.  See the interface documentation for more
//        // details on frame stepping.
//        //
//        private bool GetFrameStepInterface()
//        {
//            int hr = 0;

//            IVideoFrameStep frameStepTest = null;

//            // Get the frame step interface, if supported
//            frameStepTest = (IVideoFrameStep)this.graphBuilder;

//            // Check if this decoder can step
//            hr = frameStepTest.CanStep(0, null);
//            if (hr == 0)
//            {
//                this.frameStep = frameStepTest;
//                return true;
//            }
//            else
//            {
//                // BUG 1560263 found by husakm (thanks)...
//                // Marshal.ReleaseComObject(frameStepTest);
//                this.frameStep = null;
//                return false;
//            }
//        }

//        private void CloseInterfaces()
//        {
//            int hr = 0;

//            try
//            {
//                lock (this)
//                {
//                    // Relinquish ownership (IMPORTANT!) after hiding video window
//                    if (!this.isAudioOnly)
//                    {
//                        hr = this.videoWindow.put_Visible(OABool.False);
//                        DsError.ThrowExceptionForHR(hr);
//                        hr = this.videoWindow.put_Owner(IntPtr.Zero);
//                        DsError.ThrowExceptionForHR(hr);
//                    }

//                    if (this.mediaEventEx != null)
//                    {
//                        hr = this.mediaEventEx.SetNotifyWindow(IntPtr.Zero, 0, IntPtr.Zero);
//                        DsError.ThrowExceptionForHR(hr);
//                    }

//#if DEBUG
//                    if (rot != null)
//                    {
//                        rot.Dispose();
//                        rot = null;
//                    }
//#endif
//                    // Release and zero DirectShow interfaces
//                    if (this.mediaEventEx != null)
//                        this.mediaEventEx = null;
//                    if (this.mediaSeeking != null)
//                        this.mediaSeeking = null;
//                    if (this.mediaPosition != null)
//                        this.mediaPosition = null;
//                    if (this.mediaControl != null)
//                        this.mediaControl = null;
//                    if (this.basicAudio != null)
//                        this.basicAudio = null;
//                    if (this.basicVideo != null)
//                        this.basicVideo = null;
//                    if (this.videoWindow != null)
//                        this.videoWindow = null;
//                    if (this.frameStep != null)
//                        this.frameStep = null;
//                    if (this.graphBuilder != null)
//                        Marshal.ReleaseComObject(this.graphBuilder); this.graphBuilder = null;

//                    GC.Collect();
//                }
//            }
//            catch
//            {
//            }
//        }

////        /*
////         * Media Related methods
////         */


////        private int ToggleMute()
////        {
////            int hr = 0;

////            if ((this.graphBuilder == null) || (this.basicAudio == null))
////                return 0;

////            // Read current volume
////            hr = this.basicAudio.get_Volume(out this.currentVolume);
////            if (hr == -1) //E_NOTIMPL
////            {
////                // Fail quietly if this is a video-only media file
////                return 0;
////            }
////            else if (hr < 0)
////            {
////                return hr;
////            }

////            // Switch volume levels
////            if (this.currentVolume == VolumeFull)
////                this.currentVolume = VolumeSilence;
////            else
////                this.currentVolume = VolumeFull;

////            // Set new volume
////            hr = this.basicAudio.put_Volume(this.currentVolume);

////            UpdateMainTitle();
////            return hr;
////        }

////        private int ToggleFullScreen()
////        {
////            int hr = 0;
////            OABool lMode;

////            // Don't bother with full-screen for audio-only files
////            if ((this.isAudioOnly) || (this.videoWindow == null))
////                return 0;

////            // Read current state
////            hr = this.videoWindow.get_FullScreenMode(out lMode);
////            DsError.ThrowExceptionForHR(hr);

////            if (lMode == OABool.False)
////            {
////                // Save current message drain
////                hr = this.videoWindow.get_MessageDrain(out hDrain);
////                DsError.ThrowExceptionForHR(hr);

////                // Set message drain to application main window
////                hr = this.videoWindow.put_MessageDrain(this.Handle);
////                DsError.ThrowExceptionForHR(hr);

////                // Switch to full-screen mode
////                lMode = OABool.True;
////                hr = this.videoWindow.put_FullScreenMode(lMode);
////                DsError.ThrowExceptionForHR(hr);
////                this.isFullScreen = true;
////            }
////            else
////            {
////                // Switch back to windowed mode
////                lMode = OABool.False;
////                hr = this.videoWindow.put_FullScreenMode(lMode);
////                DsError.ThrowExceptionForHR(hr);

////                // Undo change of message drain
////                hr = this.videoWindow.put_MessageDrain(hDrain);
////                DsError.ThrowExceptionForHR(hr);

////                // Reset video window
////                hr = this.videoWindow.SetWindowForeground(OABool.True);
////                DsError.ThrowExceptionForHR(hr);

////                // Reclaim keyboard focus for player application
////                //this.Focus();
////                this.isFullScreen = false;
////            }

////            return hr;
////        }

////        private int StepOneFrame()
////        {
////            int hr = 0;

////            // If the Frame Stepping interface exists, use it to step one frame
////            if (this.frameStep != null)
////            {
////                // The graph must be paused for frame stepping to work
////                if (this.currentState != PlayState.Paused)
////                    PauseClip();

////                // Step the requested number of frames, if supported
////                hr = this.frameStep.Step(1, null);
////            }

////            return hr;
////        }

////        private int StepFrames(int nFramesToStep)
////        {
////            int hr = 0;

////            // If the Frame Stepping interface exists, use it to step frames
////            if (this.frameStep != null)
////            {
////                // The renderer may not support frame stepping for more than one
////                // frame at a time, so check for support.  S_OK indicates that the
////                // renderer can step nFramesToStep successfully.
////                hr = this.frameStep.CanStep(nFramesToStep, null);
////                if (hr == 0)
////                {
////                    // The graph must be paused for frame stepping to work
////                    if (this.currentState != PlayState.Paused)
////                        PauseClip();

////                    // Step the requested number of frames, if supported
////                    hr = this.frameStep.Step(nFramesToStep, null);
////                }
////            }

////            return hr;
////        }

////        private int ModifyRate(double dRateAdjust)
////        {
////            int hr = 0;
////            double dRate;

////            // If the IMediaPosition interface exists, use it to set rate
////            if ((this.mediaPosition != null) && (dRateAdjust != 0.0))
////            {
////                hr = this.mediaPosition.get_Rate(out dRate);
////                if (hr == 0)
////                {
////                    // Add current rate to adjustment value
////                    double dNewRate = dRate + dRateAdjust;
////                    hr = this.mediaPosition.put_Rate(dNewRate);

////                    // Save global rate
////                    if (hr == 0)
////                    {
////                        this.currentPlaybackRate = dNewRate;
////                        UpdateMainTitle();
////                    }
////                }
////            }

////            return hr;
////        }

////        private int SetRate(double rate)
////        {
////            int hr = 0;

////            // If the IMediaPosition interface exists, use it to set rate
////            if (this.mediaPosition != null)
////            {
////                hr = this.mediaPosition.put_Rate(rate);
////                if (hr >= 0)
////                {
////                    this.currentPlaybackRate = rate;
////                    UpdateMainTitle();
////                }
////            }

////            return hr;
////        }

//        private void HandleGraphEvent()
//        {
//            int hr = 0;
//            EventCode evCode;
//            IntPtr evParam1, evParam2;

//            // Make sure that we don't access the media event interface
//            // after it has already been released.
//            if (this.mediaEventEx == null)
//                return;

//            // Process all queued events
//            while (this.mediaEventEx.GetEvent(out evCode, out evParam1, out evParam2, 0) == 0)
//            {
//                // Free memory associated with callback, since we're not using it
//                hr = this.mediaEventEx.FreeEventParams(evCode, evParam1, evParam2);

//                // If this is the end of the clip, reset to beginning
//                if (evCode == EventCode.Complete)
//                {
//                    DsLong pos = new DsLong(0);
//                    // Reset to first frame of movie
//                    hr = this.mediaSeeking.SetPositions(pos, AMSeekingSeekingFlags.AbsolutePositioning,
//                      null, AMSeekingSeekingFlags.NoPositioning);
//                    if (hr < 0)
//                    {
//                        // Some custom filters (like the Windows CE MIDI filter)
//                        // may not implement seeking interfaces (IMediaSeeking)
//                        // to allow seeking to the start.  In that case, just stop
//                        // and restart for the same effect.  This should not be
//                        // necessary in most cases.
//                        hr = this.mediaControl.Stop();
//                        hr = this.mediaControl.Run();
//                    }
//                }
//            }
//        }

//        /*
//         * WinForm Related methods
//         */

//        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
//        {
//            switch (msg)
//            {
//                case WMGraphNotify:
//                    {
//                        HandleGraphEvent();
//                        break;
//                    }
//            }

//            // Pass this message to the video window for notification of system changes
//            if (this.videoWindow != null)
//                this.videoWindow.NotifyOwnerMessage(hwnd, msg, wParam, lParam);

//            return IntPtr.Zero;
//        }

////        private int initplayerwindow()
////        {
////            // reset to a default size for audio and after closing a clip
////            this.clientsize = new size(240, 120);

////            // check the 'full size' menu item
////            checksizemenu(menufilesizenormal);
////            enableplaybackmenu(false, mediatype.audio);

////            return 0;
////        }

////        private void CheckSizeMenu(MenuItem item)
////        {
////            menuFileSizeHalf.Checked = false;
////            menuFileSizeThreeQuarter.Checked = false;
////            menuFileSizeNormal.Checked = false;
////            menuFileSizeDouble.Checked = false;

////            item.Checked = true;
////        }

////        private void EnablePlaybackMenu(bool bEnable, MediaType nMediaType)
////        {
////            // Enable/disable menu items related to playback (pause, stop, mute)
////            menuFilePause.Enabled = bEnable;
////            menuFileStop.Enabled = bEnable;
////            menuFileMute.Enabled = bEnable;
////            menuRateIncrease.Enabled = bEnable;
////            menuRateDecrease.Enabled = bEnable;
////            menuRateNormal.Enabled = bEnable;
////            menuRateHalf.Enabled = bEnable;
////            menuRateDouble.Enabled = bEnable;

////            // Enable/disable menu items related to video size
////            bool isVideo = (nMediaType == MediaType.Video) ? true : false;

////            menuSingleStep.Enabled = isVideo;
////            menuFileSizeHalf.Enabled = isVideo;
////            menuFileSizeDouble.Enabled = isVideo;
////            menuFileSizeNormal.Enabled = isVideo;
////            menuFileSizeThreeQuarter.Enabled = isVideo;
////            menuFileFullScreen.Enabled = isVideo;
////        }

////        private void MainForm_KeyDown(object sender, System.Windows.Forms.KeyEventArgs e)
////        {
////            switch (e.KeyCode)
////            {
////                case Keys.Space:
////                    {
////                        StepOneFrame();
////                        break;
////                    }
////                case Keys.Left:
////                    {
////                        ModifyRate(-0.25);
////                        break;
////                    }
////                case Keys.Right:
////                    {
////                        ModifyRate(+0.25);
////                        break;
////                    }
////                case Keys.Down:
////                    {
////                        SetRate(1.0);
////                        break;
////                    }
////                case Keys.P:
////                    {
////                        PauseClip();
////                        break;
////                    }
////                case Keys.S:
////                    {
////                        StopClip();
////                        break;
////                    }
////                case Keys.M:
////                    {
////                        ToggleMute();
////                        break;
////                    }
////                case Keys.F:
////                case Keys.Return:
////                    {
////                        ToggleFullScreen();
////                        break;
////                    }
////                case Keys.H:
////                    {
////                        InitVideoWindow(1, 2);
////                        CheckSizeMenu(menuFileSizeHalf);
////                        break;
////                    }
////                case Keys.N:
////                    {
////                        InitVideoWindow(1, 1);
////                        CheckSizeMenu(menuFileSizeNormal);
////                        break;
////                    }
////                case Keys.D:
////                    {
////                        InitVideoWindow(2, 1);
////                        CheckSizeMenu(menuFileSizeDouble);
////                        break;
////                    }
////                case Keys.T:
////                    {
////                        InitVideoWindow(3, 4);
////                        CheckSizeMenu(menuFileSizeThreeQuarter);
////                        break;
////                    }
////                case Keys.Escape:
////                    {
////                        if (this.isFullScreen)
////                            ToggleFullScreen();
////                        else
////                            CloseClip();
////                        break;
////                    }
////                case Keys.F12 | Keys.Q | Keys.X:
////                    {
////                        CloseClip();
////                        break;
////                    }
////            }
////        }

////        private void menuFileOpenClip_Click(object sender, System.EventArgs e)
////        {
////            // If we have ANY file open, close it and shut down DirectShow
////            if (this.currentState != PlayState.Init)
////                CloseClip();

////            // Open the new clip
////            OpenClip();
////        }

////        private void menuFileClose_Click(object sender, System.EventArgs e)
////        {
////            CloseClip();
////        }

////        private void menuFileExit_Click(object sender, System.EventArgs e)
////        {
////            CloseClip();
////            this.Close();
////        }

////        private void menuFilePause_Click(object sender, System.EventArgs e)
////        {
////            PauseClip();
////        }

////        private void menuFileStop_Click(object sender, System.EventArgs e)
////        {
////            StopClip();
////        }

////        private void menuFileMute_Click(object sender, System.EventArgs e)
////        {
////            ToggleMute();
////        }

////        private void menuFileFullScreen_Click(object sender, System.EventArgs e)
////        {
////            ToggleFullScreen();
////        }

////        private void menuFileSize_Click(object sender, System.EventArgs e)
////        {
////            if (sender == menuFileSizeHalf) InitVideoWindow(1, 2);
////            if (sender == menuFileSizeNormal) InitVideoWindow(1, 1);
////            if (sender == menuFileSizeDouble) InitVideoWindow(2, 1);
////            if (sender == menuFileSizeThreeQuarter) InitVideoWindow(3, 4);

////            CheckSizeMenu((MenuItem)sender);
////        }

////        private void menuSingleStep_Click(object sender, System.EventArgs e)
////        {
////            StepOneFrame();
////        }

////        private void menuRate_Click(object sender, System.EventArgs e)
////        {
////            if (sender == menuRateDecrease) ModifyRate(-0.25);
////            if (sender == menuRateIncrease) ModifyRate(+0.25);
////            if (sender == menuRateNormal) SetRate(1.0);
////            if (sender == menuRateHalf) SetRate(0.5);
////            if (sender == menuRateDouble) SetRate(2.0);
////        }

////        private void MainForm_Move(object sender, System.EventArgs e)
////        {
////            if (!this.isAudioOnly)
////                MoveVideoWindow();
////        }

////        private void MainForm_Resize(object sender, System.EventArgs e)
////        {
////            if (!this.isAudioOnly)
////                MoveVideoWindow();
////        }

////        private void MainForm_Closing(object sender, System.ComponentModel.CancelEventArgs e)
////        {
////            StopClip();
////            CloseInterfaces();
////        }
    }
}
