using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using forms = System.Windows.Forms;

namespace TurtleTools
{
    /// <summary>
    /// Class used to preserve / restore state of the Windows
    /// </summary>
    public class WindowTools
    {
        //for Forms
        public static void DisplaySecondaryScreen(forms.Form frm)
        {
            if (System.Windows.Forms.SystemInformation.MonitorCount > 1)
            {
                System.Drawing.Rectangle workingArea = forms.Screen.AllScreens[1].WorkingArea;
                frm.Left = workingArea.Left;
                frm.Top = workingArea.Top;
                frm.Width = workingArea.Width;
                frm.Height = workingArea.Height;
                frm.WindowState = forms.FormWindowState.Maximized;
                frm.TopMost = true;
            }
        }


        //for WPF

        public static void Maximize(Window targetWindow)
        {
            if (targetWindow.WindowState == WindowState.Maximized)
                return;

            targetWindow.WindowState = WindowState.Maximized;
            targetWindow.WindowStyle = WindowStyle.None;
            targetWindow.Topmost = true;
        }

        public static void DisplaySecondaryScreen(Window window)
        {
            window.WindowStartupLocation = WindowStartupLocation.Manual;

            if(System.Windows.Forms.SystemInformation.MonitorCount > 1) 
            {
                System.Drawing.Rectangle workingArea = System.Windows.Forms.Screen.AllScreens[1].WorkingArea;
                window.Left = workingArea.Left;
                window.Top = workingArea.Top;
                window.Width = workingArea.Width;
                window.Height = workingArea.Height;
                window.Topmost = true; 
            }
        }

        public static double[] GetPixelDensity(FrameworkElement fe)
        {
            // get the handle of the window
            HwndSource windowhandlesource = PresentationSource.FromVisual(fe) as HwndSource;

            // work out the current screen's DPI
            Matrix screenmatrix = windowhandlesource.CompositionTarget.TransformToDevice;
            return new double[] { screenmatrix.M11, screenmatrix.M22 };
        }

        public static int GetOSInt()
        {
            int osInt = 8;
            OperatingSystem os = Environment.OSVersion;

            switch (os.Platform)
            {
                case PlatformID.Win32NT:
                    if (os.Version.Major == 6)
                    {
                        if (os.Version.Minor == 2)
                        {
                            osInt = 8;
                        }
                        else if(os.Version.Minor < 2)
                        {
                            osInt = 7;
                        }
                    }
                    else if(os.Version.Major == 5)
                    {
                        osInt = 6;
                    }
                    break;
                default:
                    osInt = 7;
                    break;
            }
            return osInt;
        }

        public static float getScalingFactor()
        {
            Graphics g = Graphics.FromHwnd(IntPtr.Zero);
            //IntPtr desktop = g.GetHdc();
            //int LogicalScreenHeight = GetDeviceCaps(desktop, (int)DeviceCap.VERTRES);
            //int PhysicalScreenHeight = GetDeviceCaps(desktop, (int)DeviceCap.DESKTOPVERTRES);

            //g.ReleaseHdc();

            //float ScreenScalingFactor = (float)PhysicalScreenHeight / (float)LogicalScreenHeight;

            //return ScreenScalingFactor; // 1.25 = 125%
            return g.DpiX / 96;
        }

        public static void HideMouseCursor()
        {
            //int osInt = WindowTools.GetOSInt();
            //if (osInt < 7)
            //{
                SetSystemCursor(CopyIcon(LoadCursor(IntPtr.Zero, arrowCursor)), waitCursor);
            //}
            //else
            //{
            //    IntPtr noneCursor = LoadCursorFromFile(FNDTools.GetAppDomainFilePath("none.cur"));
            //    SetSystemCursor(CopyIcon(noneCursor), arrowCursor);
            //    SetSystemCursor(CopyIcon(noneCursor), waitCursor);
            //}
        }

        public static void RestoreMouseCursor()
        {
            SystemParametersInfo(spi_setcursors, 0, true, 0);
        }

        public static void PreventSleep()
        {
            if (SetThreadExecutionState(ExectionStateFlags.ES_CONTINUOUS
                                        | ExectionStateFlags.ES_DISPLAY_REQUIRED
                                        | ExectionStateFlags.ES_SYSTEM_REQUIRED
                                        | ExectionStateFlags.ES_AWAYMODE_REQUIRED) == 0) //Away mode for Windows >= Vista
                SetThreadExecutionState(ExectionStateFlags.ES_CONTINUOUS
                    | ExectionStateFlags.ES_DISPLAY_REQUIRED
                    | ExectionStateFlags.ES_SYSTEM_REQUIRED); //Windows < Vista, forget away mode
        }

        public static void AllowSleep()
        {
            SetThreadExecutionState(ExectionStateFlags.ES_CONTINUOUS);
        }

        public static void SetInScaledWindowPos(Window window, double w, double h, double x, double y, double wScale, double hScale)
        {
            window.Width = w * wScale;
            window.Height = h * hScale;
            Canvas.SetLeft(window, x * wScale);
            Canvas.SetTop(window, y * hScale);
        }
        public static void ConvertInScaledWindow(Window window, double wScale, double hScale)
        {
            SetInScaledWindowPos(window, window.Width, window.Height, Canvas.GetLeft(window), Canvas.GetTop(window), wScale, hScale);
        }

        public static void SetInScaledUserCtrl(UserControl userctrl, double w, double h, double x, double y, double wScale, double hScale)
        {
            userctrl.Width = w * wScale;
            userctrl.Height = h * hScale;
            Canvas.SetLeft(userctrl, x * wScale);
            Canvas.SetTop(userctrl, y * hScale);
        }

        public static void ConvertInScaledUserCtrl(UserControl userctrl, double wScale, double hScale)
        {
            SetInScaledUserCtrl(userctrl, userctrl.Width, userctrl.Height, Canvas.GetLeft(userctrl), Canvas.GetTop(userctrl), wScale, hScale);
        }

        public static Rectangle GetDesktopBound()
        {
            //Rectangle rect = new Rectangle(int.MaxValue, int.MaxValue, int.MinValue, int.MinValue);
            Rectangle rect = new Rectangle(0, 0, 0, 0);

            foreach (forms.Screen screen in forms.Screen.AllScreens)
                rect = Rectangle.Union(rect, screen.Bounds);

            return rect;
        }

        static long timeTick = 0;
        static int clickCount = 0;
        public static bool CheckIsSeveralClick(double limit, int count)
        {
            long currentTick = DateTime.Now.ToFileTime();
            long substract = currentTick - timeTick;

            if (substract < limit * 10000000)
            {
                if (clickCount >= count - 1)
                    return true;
            }
            else
            {
                timeTick = currentTick;
                clickCount = 0;
            }

            clickCount++;
            return false;
        }

        public static void DoEvents()
        {
            DispatcherFrame frame = new DispatcherFrame(true);
            Dispatcher.CurrentDispatcher.BeginInvoke
            (
            DispatcherPriority.Background,
            (SendOrPostCallback)delegate(object arg)
            {
                var f = arg as DispatcherFrame;
                f.Continue = false;
            },
            frame
            );
            Dispatcher.PushFrame(frame);

        } 

        public static void WaitNonBlocking(double millisecond)
        {
            if (millisecond < 1) return;
            DateTime _desired = DateTime.Now.AddMilliseconds(millisecond);
            while (DateTime.Now < _desired)
            {
                DoEvents();
            }
        }


        private static IntPtr HWND_TOP = IntPtr.Zero;
        private const int SM_CXSCREEN = 0;
        private const int SM_CYSCREEN = 1;
        public static int ScreenX
        {
            get { return GetSystemMetrics(SM_CXSCREEN); }
        }

        public static int ScreenY
        {
            get { return GetSystemMetrics(SM_CYSCREEN); }
        }

        public static void SetFullScreen(IntPtr hwnd)
        {
            SetWindowPos(hwnd, (IntPtr)ZOrderFlags.HWND_TOP, 0, 0, ScreenX, ScreenY, SetWindowPosFlags.SWP_SHOWWINDOW);
        }

        public static void SetParent(IntPtr appWin, Visual parent)
        {
            HwndSource helper = PresentationSource.FromVisual(parent) as HwndSource;
            WindowTools.SetParent(appWin, helper.Handle);
        }

        public static void RemoveWindowBorder(IntPtr appWin)
        {
            //SetWindowLongA(appWin, GetWindowLongFlags.GWL_STYLE, WindowStyleFlags.WS_VISIBLE);

            long lStyle = GetWindowLong(appWin, (int)GetWindowLongFlags.GWL_STYLE);
            lStyle &= (uint)~(WindowStyleFlags.WS_CAPTION | WindowStyleFlags.WS_THICKFRAME | WindowStyleFlags.WS_MINIMIZE | WindowStyleFlags.WS_MAXIMIZE | WindowStyleFlags.WS_SYSMENU);
            //lStyle &= (uint)(~WindowStyleFlags.WS_CAPTION);
            //lStyle = Style & ~WindowStyleFlags.WS_SYSMENU;
            //lStyle = Style & ~WindowStyleFlags.WS_THICKFRAME;
            //lStyle = Style & ~WindowStyleFlags.WS_MINIMIZE;
            //lStyle = Style & ~WindowStyleFlags.WS_MAXIMIZEBOX;
            SetWindowLong(appWin, (int)GetWindowLongFlags.GWL_STYLE, (uint)lStyle);

            long lExStyle = GetWindowLong(appWin, (int)GetWindowLongFlags.GWL_EXSTYLE);
            lExStyle &= (uint)~(WindowStyleExFlags.WS_EX_DLGMODALFRAME | WindowStyleExFlags.WS_EX_CLIENTEDGE | WindowStyleExFlags.WS_EX_STATICEDGE);
            SetWindowLong(appWin, (int)GetWindowLongFlags.GWL_EXSTYLE, (uint)lExStyle);
            
            SetWindowPos(appWin, (IntPtr)ZOrderFlags.HWND_NOTOPMOST, 0, 0, 0, 0, SetWindowPosFlags.SWP_FRAMECHANGED | SetWindowPosFlags.SWP_NOMOVE | SetWindowPosFlags.SWP_NOSIZE | SetWindowPosFlags.SWP_NOZORDER | SetWindowPosFlags.SWP_NOOWNERZORDER);
        }
        
        public static void SetTopmost(IntPtr appWin)
        {
            SetWindowPos(appWin, (IntPtr)ZOrderFlags.HWND_TOPMOST, 0, 0, 0, 0, SetWindowPosFlags.SWP_NOMOVE | SetWindowPosFlags.SWP_NOSIZE | SetWindowPosFlags.SWP_SHOWWINDOW);
        }

        public static void BringToTop(IntPtr appWin)
        {
            SetWindowPos(appWin, (IntPtr)ZOrderFlags.HWND_TOP, 0, 0, 0, 0, SetWindowPosFlags.SWP_NOMOVE | SetWindowPosFlags.SWP_NOSIZE | SetWindowPosFlags.SWP_SHOWWINDOW);
        }

        public static void BringToBottom(IntPtr appWin)
        {
            SetWindowPos(appWin, (IntPtr)ZOrderFlags.HWND_BOTTOM, 0, 0, 0, 0, SetWindowPosFlags.SWP_NOMOVE | SetWindowPosFlags.SWP_NOSIZE | SetWindowPosFlags.SWP_SHOWWINDOW);
        }

        public static void RemoveMenu(IntPtr appWin)
        {
            IntPtr HMENU = GetMenu(appWin);
            
            ////loop & remove
            //for (int i = 0; i < count; i++)
            //    RemoveMenu(HMENU, 0, (MF_BYPOSITION | MF_REMOVE));

            DrawMenuBar(appWin);
        }

        public static void TopmostNow(Window window)
        {
            window.Dispatcher.Invoke(DispatcherPriority.Input, new Action(() =>
            {
                window.Activate();
                window.Topmost = true;
                window.Topmost = false;
                window.Focus();
            }));
        }

#region Win32 API

        [Flags]
        public enum ExectionStateFlags : uint
        {
            ES_SYSTEM_REQUIRED = 0x00000001,
            ES_DISPLAY_REQUIRED = 0x00000002,
            // Legacy flag, should not be used.
            // ES_USER_PRESENT   = 0x00000004,
            ES_AWAYMODE_REQUIRED = 0x00000040,
            ES_CONTINUOUS = 0x80000000,
        }


        /// <summary>
        ///     Special window handles
        /// </summary>
        public enum ZOrderFlags
        {
            // ReSharper disable InconsistentNaming
            /// <summary>
            ///     Places the window at the top of the Z order.
            /// </summary>
            HWND_TOP = 0,
            /// <summary>
            ///     Places the window at the bottom of the Z order. If the hWnd parameter identifies a topmost window, the window loses its topmost status and is placed at the bottom of all other windows.
            /// </summary>
            HWND_BOTTOM = 1,
            /// <summary>
            ///     Places the window above all non-topmost windows. The window maintains its topmost position even when it is deactivated.
            /// </summary>
            HWND_TOPMOST = -1,
            /// <summary>
            ///     Places the window above all non-topmost windows (that is, behind all topmost windows). This flag has no effect if the window is already a non-topmost window.
            /// </summary>
            HWND_NOTOPMOST = -2
            // ReSharper restore InconsistentNaming
        }

        [Flags]
        public enum SetWindowPosFlags : uint
        {
            // ReSharper disable InconsistentNaming

            /// <summary>
            ///     If the calling thread and the thread that owns the window are attached to different input queues, the system posts the request to the thread that owns the window. This prevents the calling thread from blocking its execution while other threads process the request.
            /// </summary>
            SWP_ASYNCWINDOWPOS = 0x4000,

            /// <summary>
            ///     Prevents generation of the WM_SYNCPAINT message.
            /// </summary>
            SWP_DEFERERASE = 0x2000,

            /// <summary>
            ///     Draws a frame (defined in the window's class description) around the window.
            /// </summary>
            SWP_DRAWFRAME = 0x0020,

            /// <summary>
            ///     Applies new frame styles set using the SetWindowLong function. Sends a WM_NCCALCSIZE message to the window, even if the window's size is not being changed. If this flag is not specified, WM_NCCALCSIZE is sent only when the window's size is being changed.
            /// </summary>
            SWP_FRAMECHANGED = 0x0020,

            /// <summary>
            ///     Hides the window.
            /// </summary>
            SWP_HIDEWINDOW = 0x0080,

            /// <summary>
            ///     Does not activate the window. If this flag is not set, the window is activated and moved to the top of either the topmost or non-topmost group (depending on the setting of the hWndInsertAfter parameter).
            /// </summary>
            SWP_NOACTIVATE = 0x0010,

            /// <summary>
            ///     Discards the entire contents of the client area. If this flag is not specified, the valid contents of the client area are saved and copied back into the client area after the window is sized or repositioned.
            /// </summary>
            SWP_NOCOPYBITS = 0x0100,

            /// <summary>
            ///     Retains the current position (ignores X and Y parameters).
            /// </summary>
            SWP_NOMOVE = 0x0002,

            /// <summary>
            ///     Does not change the owner window's position in the Z order.
            /// </summary>
            SWP_NOOWNERZORDER = 0x0200,

            /// <summary>
            ///     Does not redraw changes. If this flag is set, no repainting of any kind occurs. This applies to the client area, the nonclient area (including the title bar and scroll bars), and any part of the parent window uncovered as a result of the window being moved. When this flag is set, the application must explicitly invalidate or redraw any parts of the window and parent window that need redrawing.
            /// </summary>
            SWP_NOREDRAW = 0x0008,

            /// <summary>
            ///     Same as the SWP_NOOWNERZORDER flag.
            /// </summary>
            SWP_NOREPOSITION = 0x0200,

            /// <summary>
            ///     Prevents the window from receiving the WM_WINDOWPOSCHANGING message.
            /// </summary>
            SWP_NOSENDCHANGING = 0x0400,

            /// <summary>
            ///     Retains the current size (ignores the cx and cy parameters).
            /// </summary>
            SWP_NOSIZE = 0x0001,

            /// <summary>
            ///     Retains the current Z order (ignores the hWndInsertAfter parameter).
            /// </summary>
            SWP_NOZORDER = 0x0004,

            /// <summary>
            ///     Displays the window.
            /// </summary>
            SWP_SHOWWINDOW = 0x0040,

            // ReSharper restore InconsistentNaming
        }

        [Flags]
        public enum WindowStyleFlags : uint
        {
            WS_OVERLAPPED = 0x00000000,
            WS_POPUP = 0x80000000,
            WS_CHILD = 0x40000000,
            WS_MINIMIZE = 0x20000000,
            WS_VISIBLE = 0x10000000,
            WS_DISABLED = 0x08000000,
            WS_CLIPSIBLINGS = 0x04000000,
            WS_CLIPCHILDREN = 0x02000000,
            WS_MAXIMIZE = 0x01000000,
            WS_BORDER = 0x00800000,
            WS_DLGFRAME = 0x00400000,
            WS_VSCROLL = 0x00200000,
            WS_HSCROLL = 0x00100000,
            WS_SYSMENU = 0x00080000,
            WS_THICKFRAME = 0x00040000,
            WS_GROUP = 0x00020000,
            WS_TABSTOP = 0x00010000,

            WS_MINIMIZEBOX = 0x00020000,
            WS_MAXIMIZEBOX = 0x00010000,

            WS_CAPTION = WS_BORDER | WS_DLGFRAME,
            WS_TILED = WS_OVERLAPPED,
            WS_ICONIC = WS_MINIMIZE,
            WS_SIZEBOX = WS_THICKFRAME,
            WS_TILEDWINDOW = WS_OVERLAPPEDWINDOW,

            WS_OVERLAPPEDWINDOW = WS_OVERLAPPED | WS_CAPTION | WS_SYSMENU | WS_THICKFRAME | WS_MINIMIZEBOX | WS_MAXIMIZEBOX,
            WS_POPUPWINDOW = WS_POPUP | WS_BORDER | WS_SYSMENU,
            WS_CHILDWINDOW = WS_CHILD,

            //Extended Window Styles

            WS_EX_DLGMODALFRAME = 0x00000001,
            WS_EX_NOPARENTNOTIFY = 0x00000004,
            WS_EX_TOPMOST = 0x00000008,
            WS_EX_ACCEPTFILES = 0x00000010,
            WS_EX_TRANSPARENT = 0x00000020,

            //#if(WINVER >= 0x0400)

            WS_EX_MDICHILD = 0x00000040,
            WS_EX_TOOLWINDOW = 0x00000080,
            WS_EX_WINDOWEDGE = 0x00000100,
            WS_EX_CLIENTEDGE = 0x00000200,
            WS_EX_CONTEXTHELP = 0x00000400,

            WS_EX_RIGHT = 0x00001000,
            WS_EX_LEFT = 0x00000000,
            WS_EX_RTLREADING = 0x00002000,
            WS_EX_LTRREADING = 0x00000000,
            WS_EX_LEFTSCROLLBAR = 0x00004000,
            WS_EX_RIGHTSCROLLBAR = 0x00000000,

            WS_EX_CONTROLPARENT = 0x00010000,
            WS_EX_STATICEDGE = 0x00020000,
            WS_EX_APPWINDOW = 0x00040000,

            WS_EX_OVERLAPPEDWINDOW = (WS_EX_WINDOWEDGE | WS_EX_CLIENTEDGE),
            WS_EX_PALETTEWINDOW = (WS_EX_WINDOWEDGE | WS_EX_TOOLWINDOW | WS_EX_TOPMOST),

            //#endif /* WINVER >= 0x0400 */

            //#if(WIN32WINNT >= 0x0500)

            WS_EX_LAYERED = 0x00080000,

            //#endif /* WIN32WINNT >= 0x0500 */

            //#if(WINVER >= 0x0500)

            WS_EX_NOINHERITLAYOUT = 0x00100000, // Disable inheritence of mirroring by children
            WS_EX_LAYOUTRTL = 0x00400000, // Right to left mirroring

            //#endif /* WINVER >= 0x0500 */

            //#if(WIN32WINNT >= 0x0500)

            WS_EX_COMPOSITED = 0x02000000,
            WS_EX_NOACTIVATE = 0x08000000

            //#endif /* WIN32WINNT >= 0x0500 */
        }

        [Flags]
        public enum WindowStyleExFlags : uint
        {
            WS_EX_ACCEPTFILES = 0x00000010,
            WS_EX_APPWINDOW = 0x00040000,
            WS_EX_CLIENTEDGE = 0x00000200,
            WS_EX_COMPOSITED = 0x02000000,
            WS_EX_CONTEXTHELP = 0x00000400,
            WS_EX_CONTROLPARENT = 0x00010000,
            WS_EX_DLGMODALFRAME = 0x00000001,
            WS_EX_LAYERED = 0x00080000,
            WS_EX_LAYOUTRTL = 0x00400000,
            WS_EX_LEFT = 0x00000000,
            WS_EX_LEFTSCROLLBAR = 0x00004000,
            WS_EX_LTRREADING = 0x00000000,
            WS_EX_MDICHILD = 0x00000040,
            WS_EX_NOACTIVATE = 0x08000000,
            WS_EX_NOINHERITLAYOUT = 0x00100000,
            WS_EX_NOPARENTNOTIFY = 0x00000004,
            WS_EX_OVERLAPPEDWINDOW = WS_EX_WINDOWEDGE | WS_EX_CLIENTEDGE,
            WS_EX_PALETTEWINDOW = WS_EX_WINDOWEDGE | WS_EX_TOOLWINDOW | WS_EX_TOPMOST,
            WS_EX_RIGHT = 0x00001000,
            WS_EX_RIGHTSCROLLBAR = 0x00000000,
            WS_EX_RTLREADING = 0x00002000,
            WS_EX_STATICEDGE = 0x00020000,
            WS_EX_TOOLWINDOW = 0x00000080,
            WS_EX_TOPMOST = 0x00000008,
            WS_EX_TRANSPARENT = 0x00000020,
            WS_EX_WINDOWEDGE = 0x00000100
        }

        public enum GetWindowLongFlags
        {
            /// <summary>Sets a new address for the window procedure.</summary>
            /// <remarks>You cannot change this attribute if the window does not belong to the same process as the calling thread.</remarks>
            GWL_WNDPROC = -4,

            /// <summary>Sets a new application instance handle.</summary>
            GWLP_HINSTANCE = -6,

            GWLP_HWNDPARENT = -8,

            /// <summary>Sets a new identifier of the child window.</summary>
            /// <remarks>The window cannot be a top-level window.</remarks>
            GWL_ID = -12,

            /// <summary>Sets a new window style.</summary>
            GWL_STYLE = -16,

            /// <summary>Sets a new extended window style.</summary>
            /// <remarks>See <see cref="ExWindowStyles"/>.</remarks>
            GWL_EXSTYLE = -20,

            /// <summary>Sets the user data associated with the window.</summary>
            /// <remarks>This data is intended for use by the application that created the window. Its value is initially zero.</remarks>
            GWL_USERDATA = -21,

            /// <summary>Sets the return value of a message processed in the dialog box procedure.</summary>
            /// <remarks>Only applies to dialog boxes.</remarks>
            DWLP_MSGRESULT = 0,

            /// <summary>Sets new extra information that is private to the application, such as handles or pointers.</summary>
            /// <remarks>Only applies to dialog boxes.</remarks>
            DWLP_USER = 8,

            /// <summary>Sets the new address of the dialog box procedure.</summary>
            /// <remarks>Only applies to dialog boxes.</remarks>
            DWLP_DLGPROC = 4
        }

        public enum ShowWindowCommands : uint
        {
            /// <summary>
            ///        Hides the window and activates another window.
            /// </summary>
            SW_HIDE = 0,

            /// <summary>
            ///        Activates and displays a window. If the window is minimized or maximized, the system restores it to its original size and position. An application should specify this flag when displaying the window for the first time.
            /// </summary>
            SW_SHOWNORMAL = 1,

            /// <summary>
            ///        Activates and displays a window. If the window is minimized or maximized, the system restores it to its original size and position. An application should specify this flag when displaying the window for the first time.
            /// </summary>
            SW_NORMAL = 1,

            /// <summary>
            ///        Activates the window and displays it as a minimized window.
            /// </summary>
            SW_SHOWMINIMIZED = 2,

            /// <summary>
            ///        Activates the window and displays it as a maximized window.
            /// </summary>
            SW_SHOWMAXIMIZED = 3,

            /// <summary>
            ///        Maximizes the specified window.
            /// </summary>
            SW_MAXIMIZE = 3,

            /// <summary>
            ///        Displays a window in its most recent size and position. This value is similar to <see cref="ShowWindowCommands.SW_SHOWNORMAL"/>, except the window is not activated.
            /// </summary>
            SW_SHOWNOACTIVATE = 4,

            /// <summary>
            ///        Activates the window and displays it in its current size and position.
            /// </summary>
            SW_SHOW = 5,

            /// <summary>
            ///        Minimizes the specified window and activates the next top-level window in the z-order.
            /// </summary>
            SW_MINIMIZE = 6,

            /// <summary>
            ///        Displays the window as a minimized window. This value is similar to <see cref="ShowWindowCommands.SW_SHOWMINIMIZED"/>, except the window is not activated.
            /// </summary>
            SW_SHOWMINNOACTIVE = 7,

            /// <summary>
            ///        Displays the window in its current size and position. This value is similar to <see cref="ShowWindowCommands.SW_SHOW"/>, except the window is not activated.
            /// </summary>
            SW_SHOWNA = 8,

            /// <summary>
            ///        Activates and displays the window. If the window is minimized or maximized, the system restores it to its original size and position. An application should specify this flag when restoring a minimized window.
            /// </summary>
            SW_RESTORE = 9
        }


        [Flags]
        public enum DeviceCapFlags
        {
            VERTRES = 10,
            DESKTOPVERTRES = 117
        }

        const int arrowCursor = 32512;
        const int waitCursor = 32514;
        const uint spi_setcursors = 0x0057;


        [DllImport("gdi32.dll")]
        public static extern int GetDeviceCaps(IntPtr hdc, int nIndex);

        [DllImport("user32.dll")]
        static extern IntPtr LoadCursor(IntPtr hInstance, int lpCursorName);

        [DllImport("user32.dll")]
        static extern IntPtr LoadCursorFromFile(string lpFileName);

        [DllImport("user32.dll")]
        static extern IntPtr CopyIcon(IntPtr hIcon);

        [DllImport("user32.dll")]
        static extern bool SetSystemCursor(IntPtr hcur, uint id);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool SystemParametersInfo(uint uiAction, uint uiParam, bool pvParam, uint fWinIni);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern ExectionStateFlags SetThreadExecutionState(ExectionStateFlags esFlags);

        [DllImport("user32.dll", EntryPoint = "GetSystemMetrics")]
        public static extern int GetSystemMetrics(int which);
        
        [DllImport("user32.dll", EntryPoint = "GetWindowThreadProcessId", SetLastError = true, CharSet = CharSet.Unicode, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
        public static extern long GetWindowThreadProcessId(long hWnd, long lpdwProcessId);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern long SetParent(IntPtr hWndChild, IntPtr hWndNewParent);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongA", SetLastError = true)]
        public static extern long GetWindowLong(IntPtr hwnd, int nIndex);

        [DllImport("user32")]
        public static extern int SetWindowLong(IntPtr hWnd, int nIndex, uint dwNewLong);
        
        [DllImport("user32.dll", EntryPoint = "SetWindowLongA", SetLastError = true)]
        public static extern int SetWindowLongA([System.Runtime.InteropServices.InAttribute()] System.IntPtr hWnd, int nIndex, uint dwNewLong);

        //[DllImport("user32.dll")]
        //public static extern void SetWindowPos(IntPtr hwnd, IntPtr hwndInsertAfter, int x, int y, int width, int height, uint flags);
        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int width, int height, SetWindowPosFlags uFlags);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool MoveWindow(IntPtr hwnd, int x, int y, int cx, int cy, bool repaint);
        
        public static uint MF_BYPOSITION = 0x400;
        public static uint MF_REMOVE = 0x1000;
        [DllImport("user32.dll")]
        static extern IntPtr GetMenu(IntPtr hWnd);
        [DllImport("user32.dll")]
        static extern bool RemoveMenu(IntPtr hMenu, uint uPosition, uint uFlags);
        [DllImport("user32.dll")]
        static extern int GetMenuItemCount(IntPtr hMenu);
        [DllImport("user32.dll")]
        static extern bool DrawMenuBar(IntPtr hWnd);

        [DllImport("gdi32.dll")]
        public static extern int CreateRoundRectRgn(int x1, int y1, int x2, int y2, int x3, int y3);

        [DllImport("user32.dll")]
        public static extern int SetWindowRgn(IntPtr hwnd, int hRgn, Boolean bRedraw);

        [DllImport("gdi32.dll", EntryPoint = "DeleteObject", CharSet = CharSet.Ansi)]
        public static extern int DeleteObject(int hObject); 
#endregion

        #region "Refresh Notification Area Icons"

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int left;
            public int top;
            public int right;
            public int bottom;
        }
        [DllImport("user32.dll")]
        public static extern IntPtr FindWindowEx(IntPtr hwndParent, IntPtr hwndChildAfter, string lpszClass, string lpszWindow);
        [DllImport("user32.dll")]
        public static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);
        [DllImport("user32.dll")]
        public static extern IntPtr SendMessage(IntPtr hWnd, uint msg, int wParam, int lParam);

        public static void RefreshTrayArea()
        {
            IntPtr trayShellHandle = FindWindow("Shell_TrayWnd", null);
            IntPtr taryNotiHandle = FindWindowEx(trayShellHandle, IntPtr.Zero, "TrayNotifyWnd", null);
            IntPtr sysPagerHandle = FindWindowEx(taryNotiHandle, IntPtr.Zero, "SysPager", null);
            
            IntPtr notiAreaHandle = FindWindowEx(sysPagerHandle, IntPtr.Zero, "ToolbarWindow32", "Notification Area");

            if (notiAreaHandle == IntPtr.Zero)
            {
                IntPtr notiAreaKorHandle = FindWindowEx(sysPagerHandle, IntPtr.Zero, "ToolbarWindow32", "알림 영역");
                if (notiAreaKorHandle != IntPtr.Zero)
                    RefreshTrayArea(notiAreaKorHandle);
            }
            else
                RefreshTrayArea(notiAreaHandle);


            IntPtr notiUserDefAreaHandle = FindWindowEx(sysPagerHandle, IntPtr.Zero, "ToolbarWindow32", "User Promoted Notification Area");

            if (notiUserDefAreaHandle == IntPtr.Zero)
            {
                IntPtr notiUserDefAreaKorHandle = FindWindowEx(sysPagerHandle, IntPtr.Zero, "ToolbarWindow32", "사용자 지정 알림 영역");
                if (notiUserDefAreaKorHandle != IntPtr.Zero)
                    RefreshTrayArea(notiUserDefAreaKorHandle);
            }
            else
                RefreshTrayArea(notiUserDefAreaHandle);
            

            IntPtr overfNotiWinHandle = FindWindow("NotifyIconOverflowWindow", null);
            IntPtr overfNotiAreaHandle = FindWindowEx(overfNotiWinHandle, IntPtr.Zero, "ToolbarWindow32", "Overflow Notification Area");

            if (overfNotiAreaHandle == IntPtr.Zero)
            {
                IntPtr overfNotiAreaKorHandle = FindWindowEx(overfNotiWinHandle, IntPtr.Zero, "ToolbarWindow32", "오버플로 알림 영역");
                if (overfNotiAreaKorHandle != IntPtr.Zero)
                    RefreshTrayArea(overfNotiAreaKorHandle);
            }
            else
                RefreshTrayArea(overfNotiAreaHandle);
        }

        private static void RefreshTrayArea(IntPtr windowHandle)
        {
            const uint wmMousemove = 0x0200;
            RECT rect;
            GetClientRect(windowHandle, out rect);
            for (var x = 0; x < rect.right; x += 5)
                for (var y = 0; y < rect.bottom; y += 5)
                    SendMessage(windowHandle, wmMousemove, 0, (y << 16) + x);
        }


        /*
         * HKEY_CURRENT_USER\Software\Classes\Local Settings\Software\Microsoft\Windows\CurrentVersion\TrayNotify
         * IconStreams , PastIconsStream
         */
        public static void DeleteNotifyIcons()
        {
            string subKeys = @"Classes\Local Settings\Software\Microsoft\Windows\CurrentVersion\TrayNotify";
            string valueKey1 = "IconStreams";
            string valueKey2 = "PastIconsStream";

            DeleteRegKey(subKeys, valueKey1);
            DeleteRegKey(subKeys, valueKey2);
        }

        public static void DeleteRegKey(string subkeys, string valueKey, bool isHKLM = false)
        {
            try
            {
                RegistryKey baseKey;

                if (isHKLM)
                {
                    baseKey = Registry.LocalMachine;
                }
                else
                {
                    baseKey = Registry.CurrentUser;
                }

                RegistryKey rKey = baseKey.CreateSubKey("Software");
                foreach (string key in subkeys.Split('\\'))
                {
                    rKey = rKey.CreateSubKey(key);
                }

                rKey.DeleteValue(valueKey);
            }
            catch (Exception e)
            {
            }
        }
        #endregion

        const int WM_SYSCOMMAND = 0x0112;
        const int SC_MONITORPOWER = 0xF170;
        const int MONITOR_ON = -1;
        const int MONITOR_OFF = 2;
        const int MONITOR_STANBY = 1;

        [DllImport("user32.dll")]
        private static extern int SendMessage(int hWnd, int hMsg, int wParam, int lParam);

        public static void SetLCDPower(int hWnd, bool on)
        {
            if (on)
                SendMessage(hWnd, WM_SYSCOMMAND, SC_MONITORPOWER, MONITOR_ON);
            else
                SendMessage(hWnd, WM_SYSCOMMAND, SC_MONITORPOWER, MONITOR_OFF);
        }
    }
}
