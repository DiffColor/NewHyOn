using System;
using System.Windows;
using System.Windows.Controls;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Windows.Interop;
using TurtleTools;

namespace HyOnPlayer
{
    /// <summary>
    /// ExeControl.xaml 
    /// </summary>
    public partial class ExeControl : UserControl, IDisposable
    {
        [System.Runtime.InteropServices.StructLayoutAttribute(System.Runtime.InteropServices.LayoutKind.Sequential)]
        public struct HWND__ {
    
            /// int
            public int unused;
        }

        public ExeControl()
        {
            InitializeComponent();

            this.SizeChanged += new SizeChangedEventHandler(OnSizeChanged);
            this.Loaded += new RoutedEventHandler(OnVisibleChanged);
            this.Unloaded += ExeControl_Unloaded;
        }

        void ExeControl_Unloaded(object sender, RoutedEventArgs e)
        {
            Dispose();
        }

        public ExeControl(string exeName, string args, int offsetX=0, int offsetY=0, int marginW=0, int marginH=0, double scaleX=1, double scaleY=1)
        {
            InitializeComponent();

            ExeName = exeName;
            Args = args;

            this.offsetX = offsetX;
            this.offsetY = offsetY;
            this.marginW = marginW;
            this.marginH = marginH;

            this.scaleX = scaleX;
            this.scaleY = scaleY;

            this.SizeChanged += new SizeChangedEventHandler(OnSizeChanged);
            this.Loaded += new RoutedEventHandler(OnVisibleChanged);
        }

        ~ExeControl()
        {
            this.Dispose();
        }

        /// <summary>
        /// Track if the application has been created
        /// </summary>
        private bool _iscreated = false;
        
        /// <summary>
        /// Track if the control is disposed
        /// </summary>
        private bool _isdisposed = false;

        /// <summary>
        /// Handle to the application Window
        /// </summary>
        IntPtr _appWin;

        private Process _childp;

        /// <summary>
        /// The name of the exe to launch
        /// </summary>
        private string exeName = "";

        public string ExeName
        {
            get
            {
                return exeName;
            }
            set
            {
                exeName = value;				
            }
        }

        private string args = "";

        public string Args
        {
            get
            {
                return args;
            }
            set
            {
                args = value;
            }
        }

        int offsetX = 0;
        public int OffsetX
        {
            get
            {
                return offsetX;
            }
            set
            {
                offsetX = value;
            }
        }

        int offsetY = 0;
        public int OffsetY
        {
            get
            {
                return offsetY;
            }
            set
            {
                offsetY = value;
            }
        }

        int marginW = 0;
        public int MarginW
        {
            get
            {
                return marginW;
            }
            set
            {
                marginW = value;
            }
        }

        int marginH = 0;
        public int MarginH
        {
            get
            {
                return marginH;
            }
            set
            {
                marginH = value;
            }
        }

        double scaleX = 1;
        public double ScaleX
        {
            get
            {
                return scaleX;
            }
            set
            {
                scaleX = value;
            }
        }

        double scaleY = 1;
        public double ScaleY
        {
            get
            {
                return scaleY;
            }
            set
            {
                scaleY = value;
            }
        }

        public IntPtr GetHandle()
        {
            if (_childp == null)
            {
                return IntPtr.Zero;
            }

            return _childp.MainWindowHandle;
        }

        /// <summary>
        /// Force redraw of control when size changes
        /// </summary>
        /// <param name="e">Not used</param>
        protected void OnSizeChanged(object s, SizeChangedEventArgs e)
        {
            if (this._appWin != IntPtr.Zero)
            {
                WindowTools.MoveWindow(_appWin, offsetX, offsetY, (int)(((this.ActualWidth + marginW) * scaleX) * MainWindow.Instance.pixelDensity[0]), (int)(((this.ActualHeight + marginH) * scaleY) * MainWindow.Instance.pixelDensity[1]), true);
            }

            this.InvalidateVisual();
        }

        public void UpdatePosition(double x, double y)
        {
            if (this._appWin != IntPtr.Zero)
            {
                WindowTools.MoveWindow(_appWin, (int)x + offsetX, (int)y + offsetY, (int)(((this.ActualWidth + marginW) * scaleX) * MainWindow.Instance.pixelDensity[0]), (int)(((this.ActualHeight + marginH) * scaleY) * MainWindow.Instance.pixelDensity[1]), true);
            }

            this.InvalidateVisual();
        }


        /// <summary>
        /// Create control when visibility changes
        /// </summary>
        /// <param name="e">Not used</param>
        protected void OnVisibleChanged(object s, RoutedEventArgs e)
        {
            // If control needs to be initialized/created
            if (_iscreated == false)
            {

                // Mark that control is created
                _iscreated = true;

                // Initialize handle value to invalid
                _appWin = IntPtr.Zero;

                try
                {
                    var procInfo = new ProcessStartInfo(this.exeName);
                    //procInfo.WorkingDirectory = System.IO.Path.GetDirectoryName(this.exeName);
                    procInfo.Arguments = args;
                    // Start the process
                    _childp = Process.Start(procInfo);


                    //// Wait for process to be created and enter idle condition
                    //_childp.WaitForInputIdle();

                    //// Get the main handle
                    //_appWin = _childp.MainWindowHandle;

                    while (_appWin == IntPtr.Zero)
                    {
                        _childp.Refresh();      //update process info
                        if (_childp.HasExited)
                        {
                            return; //abort if the process finished before we got a handle.
                        }
                        _appWin = _childp.MainWindowHandle;  //cache the window handle
                    }

                }
                catch (Exception ex)
                {
                    Debug.Print(ex.Message + "Error");
                }

                // Put it into this form
                //var helper = new WindowInteropHelper(Window.GetWindow(this.ExeContainer));
                WindowTools.SetParent(_appWin, ExeContainer);

                // Remove border and whatnot
                WindowTools.RemoveWindowBorder(_appWin);

                // Move the window to overlay it on this window
                //MoveWindow(_appWin, offsetX, offsetY, (int)(this.ActualWidth*scaleX)+marginW, (int)(this.ActualHeight*scaleY)+marginH, false);
                WindowTools.MoveWindow(_appWin, offsetX, offsetY, (int)(((this.ActualWidth + marginW) * scaleX) * MainWindow.Instance.pixelDensity[0]), (int)(((this.ActualHeight + marginH) * scaleY) * MainWindow.Instance.pixelDensity[1]), true);
            }
        }
        
        protected virtual void Dispose(bool disposing)
        {
            if (!_isdisposed)
            {
                if (disposing)
                {
                    if (_iscreated && _appWin != IntPtr.Zero && !_childp.HasExited)
                    {
                        //if (_childp.CloseMainWindow())
                        //    _childp.Close();
                        //else
                        //    // Stop the application
                        //    _childp.Kill();

                        _childp.Kill();
                        _childp.WaitForExit();

                        // Clear internal handle
                        _appWin = IntPtr.Zero;
                    }
                }
                _isdisposed = true;
            }
        }

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
