using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Windows.Forms;

using DirectShowLib;
using MediaFoundation;
using MediaFoundation.EVR;
using MediaFoundation.Transform;
using MediaFoundation.Misc;
//using GenericSampleSourceFilterClasses;

namespace HyOnPlayer
{
    internal enum PlaybackState
    {
        Running,
        Paused,
        Stopped,
        Closed
    }

    // GraphEventCallback:
    // Defines a callback for the application to handle filter graph events.
    public interface GraphEventCallback
    {
        void OnGraphEvent(EventCode eventCode, IntPtr param1, IntPtr param2);
    }

    class DirectShowEVRPlayer : COMBase, IDisposable
    {
        #region Member vars

        //private ImageHandler[] m_ImageHandlers;
        private IPin[] m_pPins;

        private PlaybackState m_state;

        private Control m_hwndVideo;    // Video clipping window
        private IntPtr m_hwndEvent;     // Window to receive events
        private int m_EventMsg;         // Windows message for graph events

        private AMSeekingSeekingCapabilities m_seekCaps;        // Caps bits for IMediaSeeking

        private Guid m_clsidPresenter;   // CLSID of a custom presenter.
        private float m_fScale;
        private PointF m_ptHitTrack;

        // Filter graph interfaces.
        private IGraphBuilder m_pGraph;
        private IMediaControl m_pControl;
        private IMediaEventEx m_pEvent;
        private IMediaSeeking m_pSeek;
#if DEBUG
        private DsROTEntry m_rot;
#endif

        // EVR filter
        private IBaseFilter m_pEVR;
        private IBasicAudio m_pAudio;
        private IMFVideoDisplayControl m_pDisplay;
        private IMFVideoMixerControl m_pMixer;
        private IMFVideoPositionMapper m_pMapper;

        #endregion

        public DirectShowEVRPlayer(Control hwndVideo, IntPtr hwnd, int msg)
        {
            m_state = PlaybackState.Closed;
            m_hwndVideo = hwndVideo;
            m_hwndEvent = hwnd;
            m_EventMsg = msg;

            m_pGraph = null;
            m_pControl = null;
            m_pEvent = null;
            m_pSeek = null;
            m_pDisplay = null;
            m_pEVR = null;
            m_pMapper = null;
            m_seekCaps = 0;
            m_clsidPresenter = Guid.Empty;
        }
        ~DirectShowEVRPlayer()
        {
            Dispose();
        }

        public PlaybackState State() { return m_state; }

        public void OpenFile(string sFileName, Guid clsidPresenter)
        {
            int hr = S_Ok;

            IBaseFilter pSource = null;

            // Create a new filter graph. (This also closes the old one, if any.)
            InitializeGraph();

            m_clsidPresenter = clsidPresenter;

            // Add the source filter to the graph.
            hr = m_pGraph.AddSourceFilter(sFileName, null, out pSource);
            //DsError.ThrowExceptionForHR(hr);

            try
            {
                // Try to render the streams.
                RenderStreams(pSource);

                // Get the seeking capabilities.
                hr = m_pSeek.GetCapabilities(out m_seekCaps);
                //DsError.ThrowExceptionForHR(hr);

                // Update our state.
                m_state = PlaybackState.Stopped;
            }
            finally
            {
                Marshal.ReleaseComObject(pSource);
            }
        }

        public static void LoadGraphFile(IGraphBuilder graphBuilder, string fileName)
        {
            int hr = 0;
            IStorage storage = null;
            UCOMIStream stream = null;

            try
            {
                if (NativeMethods.StgIsStorageFile(fileName) != 0)
                    return;

                hr = NativeMethods.StgOpenStorage(
                    fileName,
                    null,
                    STGM.Transacted | STGM.Read | STGM.ShareDenyWrite,
                    IntPtr.Zero,
                    0,
                    out storage
                    );

                //Marshal.ThrowExceptionForHR(hr);

                hr = storage.OpenStream(
                    @"ActiveMovieGraph",
                    IntPtr.Zero,
                    STGM.Read | STGM.ShareExclusive,
                    0,
                    out stream
                    );

                if (hr >= 0)
                {
                    IPersistStream ips = (IPersistStream)graphBuilder;
                    System.Runtime.InteropServices.ComTypes.IStream istream = (System.Runtime.InteropServices.ComTypes.IStream)stream;
                    hr = ips.Load(istream);
                }
            }
            finally
            {
                if (stream != null)
                    Marshal.ReleaseComObject(stream);
                if (storage != null)
                    Marshal.ReleaseComObject(storage);
            }
        }

        public void SetVolume(int vol)
        {
            int hr = 0;

            if ((this.m_pGraph == null) || (this.m_pEVR == null))
                return;

            // Set volume
            hr = this.m_pAudio.put_Volume(vol);
            return;
        }

        public void Play()
        {
            if (m_state != PlaybackState.Paused && m_state != PlaybackState.Stopped)
            {
                //throw new COMException("Object in wrong state", DsResults.E_WrongState);
                return;
            }

            //Debug.Assert(m_pGraph != null); // If state is correct, the graph should exist.

            int hr = m_pControl.Run();
            //DsError.ThrowExceptionForHR(hr);

            m_state = PlaybackState.Running;
        }
        public void Pause()
        {
            if (m_state == PlaybackState.Closed)
            {
                //throw new COMException("Graph in wrong state", DsResults.E_WrongState);
                return;
            }

            //Debug.Assert(m_pGraph != null); // If state is correct, the graph should exist.

            int hr = m_pControl.Pause();
            //DsError.ThrowExceptionForHR(hr);

            m_state = PlaybackState.Paused;
        }
        public void Stop()
        {
            if (m_state != PlaybackState.Running && m_state != PlaybackState.Paused)
            {
                //throw new COMException("Graph in wrong state", DsResults.E_WrongState);
                return;
            }

            //Debug.Assert(m_pGraph != null); // If state is correct, the graph should exist.

            int hr = m_pControl.Stop();
            //DsError.ThrowExceptionForHR(hr);

            m_state = PlaybackState.Stopped;
        }
        public void Step(int dwFrames)
        {
            if (m_pGraph == null)
            {
                throw new COMException("Graph in wrong state", DsResults.E_WrongState);
            }

            IVideoFrameStep pStep = (IVideoFrameStep)m_pGraph;

            int hr = pStep.Step(dwFrames, null);
            //DsError.ThrowExceptionForHR(hr);

            // To step, the Filter Graph Manager first runs the graph. When
            // the step is complete, it pauses the graph. For the application,
            // we can just report our new state as paused.
            m_state = PlaybackState.Paused;
        }

        // Video functionality
        public bool HasVideo() { return m_pDisplay != null; }
        public void RepaintVideo()
        {
            if (m_pDisplay != null)
            {
                m_pDisplay.RepaintVideo();
            }
        }

        // Filter graph events
        public void HandleGraphEvent(GraphEventCallback pCB)
        {
            if (pCB == null)
            {
                return;
                //throw new COMException("No callback set", E_Pointer);
            }

            if (m_pEvent == null)
            {
                //throw new COMException("No event pointer", E_Unexpected);
                return;
            }

            EventCode evCode = 0;
            IntPtr param1, param2;

            // Get the events from the queue.
            while (Succeeded(m_pEvent.GetEvent(out evCode, out param1, out param2, 0)))
            {
                // Invoke the callback.
                pCB.OnGraphEvent(evCode, param1, param2);
                // Free the event data.
                int hr = m_pEvent.FreeEventParams(evCode, param1, param2);
                //DsError.ThrowExceptionForHR(hr);
            }
        }

        // Seeking
        public bool CanSeek()
        {
            const AMSeekingSeekingCapabilities caps =
                AMSeekingSeekingCapabilities.CanSeekAbsolute |
                AMSeekingSeekingCapabilities.CanGetDuration;

            return ((m_seekCaps & caps) == caps);
        }
        public void SetPosition(long pos)
        {
            if (m_pControl == null || m_pSeek == null)
            {
                throw new COMException("pointers not set", E_Unexpected);
            }

            int hr;

            hr = m_pSeek.SetPositions(
                pos,
                AMSeekingSeekingFlags.AbsolutePositioning,
                null,
                AMSeekingSeekingFlags.NoPositioning);

            //DsError.ThrowExceptionForHR(hr);

            //if (m_ImageHandlers != null)
            //{
            //    for (int x = 1; x < m_ImageHandlers.Length; x++)
            //    {
            //        IMediaSeeking ims = m_pPins[x] as IMediaSeeking;

            //        hr = ims.SetPositions(
            //            pos,
            //            AMSeekingSeekingFlags.AbsolutePositioning,
            //            null,
            //            AMSeekingSeekingFlags.NoPositioning);
            //        DsError.ThrowExceptionForHR(hr);
            //    }
            //}

            // If playback is stopped, we need to put the graph into the paused
            // state to update the video renderer with the new frame, and then stop
            // the graph again. The IMediaControl::StopWhenReady does this.
            if (m_state == PlaybackState.Stopped)
            {
                hr = m_pControl.StopWhenReady();
                //DsError.ThrowExceptionForHR(hr);
            }
        }
        public void GetStopTime(out long pDuration)
        {
            if (m_pSeek == null)
            {
                throw new COMException("No seek pointer", E_Unexpected);
            }

            int hr = m_pSeek.GetStopPosition(out pDuration);

            // If we cannot get the stop time, try to get the duration.
            if (Failed(hr))
            {
                hr = m_pSeek.GetDuration(out pDuration);
            }
            //DsError.ThrowExceptionForHR(hr);
        }
        public void GetCurrentPosition(out long pTimeNow)
        {
            if (m_pSeek == null)
            {
                throw new COMException("No seek pointer", E_Unexpected);
            }

            int hr = m_pSeek.GetCurrentPosition(out pTimeNow);
            //DsError.ThrowExceptionForHR(hr);
        }

        // Subpicture stuff
        private void SetupGraph(Guid clsidPresenter)
        {
            m_clsidPresenter = clsidPresenter;

            // Get a ICaptureGraphBuilder2 to help build the graph
            ICaptureGraphBuilder2 icgb2 = (ICaptureGraphBuilder2)new CaptureGraphBuilder2();

            try
            {
                int hr;

                // Link the ICaptureGraphBuilder2 to the IFilterGraph2
                hr = icgb2.SetFiltergraph(m_pGraph);
                //DsError.ThrowExceptionForHR(hr);

                m_pEVR = (IBaseFilter)new EnhancedVideoRenderer();
                hr = m_pGraph.AddFilter(m_pEVR, "EVR");
                //DsError.ThrowExceptionForHR(hr);

                //InitializeEVR(m_pEVR, m_ImageHandlers.Length, out m_pDisplay);
                InitializeEVR(m_pEVR, 0, out m_pDisplay);

                //m_pPins = new IPin[m_ImageHandlers.Length];

                //for (int x = 0; x < m_ImageHandlers.Length; x++)
                //{
                //    AddGSSF(m_ImageHandlers[x], icgb2, out m_pPins[x]);
                //}

                // Get the seeking capabilities.
                hr = m_pSeek.GetCapabilities(out m_seekCaps);
                //DsError.ThrowExceptionForHR(hr);
            }
            finally
            {
                Marshal.ReleaseComObject(icgb2);
            }

            // Update our state.
            m_state = PlaybackState.Stopped;
        }

        private void InitializeGraph()
        {
            int hr = 0;

            TearDownGraph();

            // Create the Filter Graph Manager.
            m_pGraph = (IGraphBuilder)new FilterGraph();
#if DEBUG
            m_rot = new DsROTEntry(m_pGraph);
#endif

            // Query for graph interfaces. (These interfaces are exposed by the graph
            // manager regardless of which filters are in the graph.)
            m_pControl = (IMediaControl)m_pGraph;
            m_pEvent = (IMediaEventEx)m_pGraph;
            m_pSeek = (IMediaSeeking)m_pGraph;

            m_pAudio = (IBasicAudio)m_pGraph;

            // Set up event notification.
            hr = m_pEvent.SetNotifyWindow(m_hwndEvent, m_EventMsg, IntPtr.Zero);
            //DsError.ThrowExceptionForHR(hr);
        }
        private void TearDownGraph()
        {
#if DEBUG
            if (m_rot != null)
            {
                m_rot.Dispose();
                m_rot = null;
            }
#endif

            // Stop sending event messages
            if (m_pEvent != null)
            {
                m_pEvent.SetNotifyWindow(IntPtr.Zero, 0, IntPtr.Zero);
                m_pEvent = null;
            }

            if (m_pControl != null)
            {
                m_pControl.Stop();
                m_pControl = null;
            }

            if (m_pDisplay != null)
            {
                //Marshal.ReleaseComObject(m_pDisplay);
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

            if (m_pEVR != null)
            {
                Marshal.ReleaseComObject(m_pEVR);
                m_pEVR = null;
            }

            if (m_pGraph != null)
            {
                Marshal.ReleaseComObject(m_pGraph);
                m_pGraph = null;
            }

            m_state = PlaybackState.Closed;
            m_seekCaps = 0;
            m_pSeek = null;
        }

        private void RenderStreams(IBaseFilter pSource)
        {
            int hr;

            bool bRenderedAudio = false;
            bool bRenderedVideo = false;

            //bool hasAudio = true;

            IBaseFilter pEVR = (IBaseFilter)new EnhancedVideoRenderer();
            IBaseFilter pAudioRenderer = (IBaseFilter)new DSoundRender();

            try
            {
                // Add the EVR to the graph.
                hr = m_pGraph.AddFilter(pEVR, "EVR");
                //DsError.ThrowExceptionForHR(hr);

                InitializeEVR(pEVR, 1, out m_pDisplay);

                // Add the DSound Renderer to the graph.
                hr = m_pGraph.AddFilter(pAudioRenderer, "Audio Renderer");
                //DsError.ThrowExceptionForHR(hr);
                //if (hr < 0)
                //{
                //    hasAudio = false;
                //}

                ICaptureGraphBuilder2 cgb;
                cgb = (ICaptureGraphBuilder2)new CaptureGraphBuilder2();

                try
                {
                    hr = cgb.SetFiltergraph(m_pGraph);
                    //DsError.ThrowExceptionForHR(hr);

                    // Connect the streams.
                    hr = cgb.RenderStream(null, DirectShowLib.MediaType.Video, pSource, null, pEVR);
                    //DsError.ThrowExceptionForHR(hr);

                    //if (hasAudio)
                    //{
                    hr = cgb.RenderStream(null, DirectShowLib.MediaType.Audio, pSource, null, pAudioRenderer);
                    //DsError.ThrowExceptionForHR(hr);
                    //}

                    // If we are using a splitter, the two lines above did nothing.  We
                    // ignore the errors from the next 2 statements in case the 2 lines above
                    // *did* do something.
                    hr = cgb.RenderStream(null, null, pSource, null, pEVR);
                    //DsError.ThrowExceptionForHR(hr);

                    //if (hasAudio)
                    //{
                    hr = cgb.RenderStream(null, null, pSource, null, pAudioRenderer);
                    //DsError.ThrowExceptionForHR(hr);
                    //}

                    IPin pPin = DsFindPin.ByConnectionStatus(pEVR, PinConnectedStatus.Unconnected, 0);

                    if (pPin == null)
                    {
                        bRenderedVideo = true;
                    }
                    else
                    {
                        Marshal.ReleaseComObject(pPin);
                    }

                    pPin = DsFindPin.ByConnectionStatus(pAudioRenderer, PinConnectedStatus.Unconnected, 0);

                    if (pPin == null)
                    {
                        bRenderedAudio = true;
                    }
                    else
                    {
                        Marshal.ReleaseComObject(pPin);
                    }
                }
                finally
                {
                    Marshal.ReleaseComObject(cgb);
                }

                // Remove un-used renderers.

                if (!bRenderedVideo)
                {
                    m_pGraph.RemoveFilter(pEVR);
                    // If we removed the EVR, then we also need to release our
                    // pointer to the EVR display interfaace
                    //Marshal.ReleaseComObject(m_pDisplay);
                    m_pDisplay = null;
                }
                else
                {
                    // EVR is still in the graph. Cache the interface pointer.
                    //Debug.Assert(pEVR != null);
                    m_pEVR = pEVR;
                }

                if (!bRenderedAudio)
                {
                    m_pGraph.RemoveFilter(pAudioRenderer);
                }
            }
            finally
            {
                Marshal.ReleaseComObject(pAudioRenderer);
            }
        }

        private void InitializeEVR(IBaseFilter pEVR, int dwStreams, out IMFVideoDisplayControl ppDisplay)
        {
            IMFVideoRenderer pRenderer;
            IMFVideoDisplayControl pDisplay;
            IEVRFilterConfig pConfig;
            IMFVideoPresenter pPresenter;

            // Before doing anything else, set any custom presenter or mixer.

            // Presenter?
            if (m_clsidPresenter != Guid.Empty)
            {
                Type type = Type.GetTypeFromCLSID(m_clsidPresenter);

                try
                {

                    // An error here means that the custom presenter sample from
                    // http://mfnet.sourceforge.net hasn't been installed or
                    // registered.
                    pPresenter = (IMFVideoPresenter)Activator.CreateInstance(type);

                    pRenderer = (IMFVideoRenderer)pEVR;

                    pRenderer.InitializeRenderer(null, pPresenter);
                }
                finally
                {
                    //Marshal.ReleaseComObject(pPresenter);
                }
            }

            // Continue with the rest of the set-up.

            // Set the video window.
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
                // Set the number of streams.
                pDisplay.SetVideoWindow(m_hwndVideo.Handle);

                if (dwStreams > 1)
                {
                    pConfig = (IEVRFilterConfig)pEVR;
                    pConfig.SetNumberOfStreams(dwStreams);
                }

                // Set the display position to the entire window.
                Rectangle r = m_hwndVideo.ClientRectangle;
                MFRect rc = new MFRect(r.Left, r.Top, r.Right, r.Bottom);

                pDisplay.SetVideoPosition(null, rc);

                // Return the IMFVideoDisplayControl pointer to the caller.
                ppDisplay = pDisplay;
            }
            finally
            {
                //Marshal.ReleaseComObject(pDisplay);
            }
            m_pMixer = null;
        }

        #region IDisposable Members

        public void Dispose()
        {
            GC.SuppressFinalize(this);
            TearDownGraph();

            //if (m_ImageHandlers != null)
            //{
            //    foreach (ImageHandler ih in m_ImageHandlers)
            //    {
            //        ih.Dispose();
            //    }
            //    m_ImageHandlers = null;
            //}
            m_hwndVideo = null;
        }

        #endregion
    }
    internal sealed class NativeMethods
    {
        private NativeMethods() { }

        [DllImport("ole32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
        public static extern int StgCreateDocfile(
          [In, MarshalAs(UnmanagedType.LPWStr)] string pwcsName,
          [In] STGM grfMode,
          [In] int reserved,
          [Out] out IStorage ppstgOpen
          );

        [DllImport("ole32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
        public static extern int StgIsStorageFile([In, MarshalAs(UnmanagedType.LPWStr)] string pwcsName);

        [DllImport("ole32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
        public static extern int StgOpenStorage(
          [In, MarshalAs(UnmanagedType.LPWStr)] string pwcsName,
          [In] IStorage pstgPriority,
          [In] STGM grfMode,
          [In] IntPtr snbExclude,
          [In] int reserved,
          [Out] out IStorage ppstgOpen
          );

    }
    [Flags]
    internal enum STGM
    {
        Read = 0x00000000,
        Write = 0x00000001,
        ReadWrite = 0x00000002,
        ShareDenyNone = 0x00000040,
        ShareDenyRead = 0x00000030,
        ShareDenyWrite = 0x00000020,
        ShareExclusive = 0x00000010,
        Priority = 0x00040000,
        Create = 0x00001000,
        Convert = 0x00020000,
        FailIfThere = 0x00000000,
        Direct = 0x00000000,
        Transacted = 0x00010000,
        NoScratch = 0x00100000,
        NoSnapShot = 0x00200000,
        Simple = 0x08000000,
        DirectSWMR = 0x00400000,
        DeleteOnRelease = 0x04000000,
    }
    [Flags]
    internal enum STGC
    {
        Default = 0,
        Overwrite = 1,
        OnlyIfCurrent = 2,
        DangerouslyCommitMerelyToDiskCache = 4,
        Consolidate = 8
    }
    [Guid("0000000b-0000-0000-C000-000000000046"),
    InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IStorage
    {
        [PreserveSig]
        int CreateStream(
        [In, MarshalAs(UnmanagedType.LPWStr)] string pwcsName,
        [In] STGM grfMode,
        [In] int reserved1,
        [In] int reserved2,
        [Out] out UCOMIStream ppstm
        );
        [PreserveSig]
        int OpenStream(
          [In, MarshalAs(UnmanagedType.LPWStr)] string pwcsName,
          [In] IntPtr reserved1,
          [In] STGM grfMode,
          [In] int reserved2,
          [Out] out UCOMIStream ppstm
          );

        [PreserveSig]
        int CreateStorage(
          [In, MarshalAs(UnmanagedType.LPWStr)] string pwcsName,
          [In] STGM grfMode,
          [In] int reserved1,
          [In] int reserved2,
          [Out] out IStorage ppstg
          );

        [PreserveSig]
        int OpenStorage(
          [In, MarshalAs(UnmanagedType.LPWStr)] string pwcsName,
          [In] IStorage pstgPriority,
          [In] STGM grfMode,
          [In] int snbExclude,
          [In] int reserved,
          [Out] out IStorage ppstg
          );

        [PreserveSig]
        int CopyTo(
          [In] int ciidExclude,
          [In] Guid[] rgiidExclude,
          [In] string[] snbExclude,
          [In] IStorage pstgDest
          );

        [PreserveSig]
        int MoveElementTo(
          [In, MarshalAs(UnmanagedType.LPWStr)] string pwcsName,
          [In] IStorage pstgDest,
          [In, MarshalAs(UnmanagedType.LPWStr)] string pwcsNewName,
          [In] STGM grfFlags
          );

        [PreserveSig]
        int Commit([In] STGC grfCommitFlags);

        [PreserveSig]
        int Revert();

        [PreserveSig]
        int EnumElements(
          [In] int reserved1,
          [In] IntPtr reserved2,
          [In] int reserved3,
          [Out, MarshalAs(UnmanagedType.Interface)] out object ppenum
          );

        [PreserveSig]
        int DestroyElement([In, MarshalAs(UnmanagedType.LPWStr)] string pwcsName);

        [PreserveSig]
        int RenameElement(
          [In, MarshalAs(UnmanagedType.LPWStr)] string pwcsOldName,
          [In, MarshalAs(UnmanagedType.LPWStr)] string pwcsNewName
          );

        [PreserveSig]
        int SetElementTimes(
          [In, MarshalAs(UnmanagedType.LPWStr)] string pwcsName,
          [In] FILETIME pctime,
          [In] FILETIME patime,
          [In] FILETIME pmtime
          );

        [PreserveSig]
        int SetClass([In, MarshalAs(UnmanagedType.LPStruct)] Guid clsid);

        [PreserveSig]
        int SetStateBits(
          [In] int grfStateBits,
          [In] int grfMask
          );

        [PreserveSig]
        int Stat(
          [Out] out STATSTG pStatStg,
          [In] int grfStatFlag
          );
    }
}
