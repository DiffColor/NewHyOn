
using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

namespace TurtleTools
{
    public class ProcessTools
    {
        #region External Process
        public static bool CheckExeProcessAlive(string processName)
        {
            bool isExist = false;

            try
            {
                Process[] procs = Process.GetProcesses();

                foreach (Process aProc in procs)
                {
                    if (aProc.ProcessName.ToString().Equals(processName))
                    {
                        isExist = true;
                        break;
                    }
                }
            }
            catch (Exception ex) { Logger.WriteErrorLog(ex.ToString(), Logger.GetLogFileName()); }

            return isExist;
        }

        public static void KillExeProcess(string processName, bool isWait = false)
        {
            try
            {
                Process[] procs = Process.GetProcesses();

                foreach (Process aProc in procs)
                {
                    if (aProc.ProcessName.ToString().Equals(processName))
                    {
                        aProc.Kill();
                        
                        if(isWait)
                            aProc.WaitForExit();
                    }
                }
            }
            catch (Exception ex) { Logger.WriteErrorLog(ex.ToString(), Logger.GetLogFileName()); }
        }

        public static void KillProcessById(int id, bool isWait = false)
        {
            try
            {
                Process[] procs = Process.GetProcesses();

                foreach (Process aProc in procs)
                {
                    if (aProc.Id == id)
                    {
                        aProc.Kill();

                        if (isWait)
                            aProc.WaitForExit();
                    }
                }
            }
            catch (Exception ex) { Logger.WriteErrorLog(ex.ToString(), Logger.GetLogFileName()); }
        }

        public static void CloseExeProcess(string processName)
        {
            try
            {
                Process[] procs = Process.GetProcesses();

                foreach (Process aProc in procs)
                {
                    if (aProc.ProcessName.ToString().Equals(processName))
                    {
                        if (aProc.CloseMainWindow())
                            aProc.Close();
                        else
                            aProc.Kill();
                    }
                }
            }
            catch (Exception ex) { Logger.WriteErrorLog(ex.ToString(), Logger.GetLogFileName()); }
        }

        public static void KillVNCViewer()
        {
            KillExeProcess("vncviewer");
        }

        public static Process LaunchProcess(string exePath, string arg = "", bool isAdmin = false, bool isWait = false, int waitmills = 1000)
        {
            try
            {
                System.Diagnostics.Process proc = new System.Diagnostics.Process();
                proc.EnableRaisingEvents = true;
                proc.StartInfo.CreateNoWindow = true;

                if (File.Exists(exePath))
                {
                    proc.StartInfo.FileName = Path.GetFileName(exePath);
                    proc.StartInfo.WorkingDirectory = Path.GetDirectoryName(exePath);

                    if (!string.IsNullOrEmpty(arg))
                        proc.StartInfo.Arguments = arg;

                    if (isAdmin)
                        proc.StartInfo.Verb = "runas";

                    proc.Start();

                    if (isWait)
                    {
                        if (waitmills <= 0)
                            proc.WaitForExit();
                        else
                            proc.WaitForExit(waitmills);
                    }
                }

                return proc;
            }
            catch (Exception ex)
            {
                Logger.WriteErrorLog(ex.ToString(), Logger.GetLogFileName());
            }

            return null;
        }

        public static void ExecuteCommand(string command, string workingDir = "")
        {
            try
            {
                System.Diagnostics.Process commander = new System.Diagnostics.Process();

                commander.StartInfo.CreateNoWindow = true;
                commander.StartInfo.UseShellExecute = false;
                commander.StartInfo.WorkingDirectory = workingDir;
                    
                commander.StartInfo.FileName = "cmd.exe";
                commander.StartInfo.Arguments = "/c echo & " + command;

                commander.Start();
            }
            catch (Exception ex)
            {
                Logger.WriteErrorLog(ex.ToString(), Logger.GetLogFileName());
            }
        }
        #endregion

        [DllImport("user32.dll", EntryPoint = "SetWindowLongA", SetLastError = true)]
        public static extern int SetWindowLongA([System.Runtime.InteropServices.InAttribute()] System.IntPtr hWnd, int nIndex, int dwNewLong);
        [DllImport("user32.dll")]
        static extern IntPtr GetMenu(IntPtr hWnd);

        [DllImport("user32.dll")]
        static extern int GetMenuItemCount(IntPtr hMenu);

        [DllImport("user32.dll")]
        static extern bool DrawMenuBar(IntPtr hWnd);

        [DllImport("user32.dll")]
        static extern bool RemoveMenu(IntPtr hMenu, uint uPosition, uint uFlags);



        [DllImport("user32.dll")]
        public static extern int SetParent(IntPtr hWndChild, IntPtr hWndNewParent);
        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool MoveWindow(IntPtr hWnd, int X, int Y, int nWidth, int nHeight, bool bRepaint);
        [DllImport("user32.dll")]
        public static extern bool EnableWindow(IntPtr hWnd, bool enable);

        [DllImport("user32.dll")]
        public static extern bool GetWindowRect(IntPtr hwnd, ref RECT rectangle);
        [DllImport("user32.dll")]
        public static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);
        [DllImport("user32.dll")]
        public static extern bool RedrawWindow(IntPtr hWnd, IntPtr lprcUpdate, IntPtr hrgnUpdate, RedrawWindowFlags flags);

        [DllImport("user32.dll", ExactSpelling = true, CharSet = CharSet.Auto)]
        public static extern IntPtr GetParent(IntPtr hWnd);

        [DllImportAttribute("user32.dll")]
        public static extern bool SetCursorPos(int X, int Y);
        [DllImportAttribute("user32.dll")]
        public static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
        [DllImportAttribute("user32.dll")]
        public static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        //Import the SetForeground API to activate it
        [DllImport("User32.dll", EntryPoint = "SetForegroundWindow")]
        private static extern IntPtr SetForegroundWindowNative(IntPtr hWnd);


        public static int HWND_NOTOPMOST = -2;
        public static int HWND_TOP = 0;
        public static int HWND_BOTTOM = 1;
        public static int HWND_TOPMOST = -1;

        public static int SWP_HIDEWINDOW = 128;
        public static int SWP_NOACTIVATE = 10;
        public static int SWP_NOMOVE = 2;
        public static int SWP_NOREDRAW = 8;
        public static int SWP_NOSIZE = 1;
        public static int SWP_FRAMECHANGED = 20;
        public static int SWP_NOZORDER = 4;


        private const int WS_VISIBLE = 0x10000000;

        public static uint MF_BYPOSITION = 0x400;
        public static uint MF_REMOVE = 0x1000;
        public static int GWL_STYLE = -16;
        public static int WS_CHILD = 0x40000000; //child window
        public static int WS_BORDER = 0x00800000; //window with border
        public static int WS_DLGFRAME = 0x00400000; //window with double border but no title
        public static int WS_CAPTION = WS_BORDER | WS_DLGFRAME; //window with a title bar 
        public static int WS_SYSMENU = 0x00080000; //window menu  
        public const int WS_THICKFRAME = 0x00040000;
        public const UInt32 WS_POPUP = 0x80000000;



        [DllImport("user32.dll")]
        internal static extern bool GetWindowPlacement(IntPtr hWnd, ref WINDOWPLACEMENT lpwndpl);

        internal struct WINDOWPLACEMENT
        {
            public int length;
            public int flags;
            public ShowWindowCommands showCmd;
            public System.Drawing.Point ptMinPosition;
            public System.Drawing.Point ptMaxPosition;
            public System.Drawing.Rectangle rcNormalPosition;
        }

        internal enum ShowWindowCommands : int
        {
            Hide = 0,
            Normal = 1,
            Minimized = 2,
            Maximized = 3,
        }

        internal enum WNDSTATE : int
        {
            SW_HIDE = 0,
            SW_SHOWNORMAL = 1,
            SW_NORMAL = 1,
            SW_SHOWMINIMIZED = 2,
            SW_MAXIMIZE = 3,
            SW_SHOWNOACTIVATE = 4,
            SW_SHOW = 5,
            SW_MINIMIZE = 6,
            SW_SHOWMINNOACTIVE = 7,
            SW_SHOWNA = 8,
            SW_RESTORE = 9,
            SW_SHOWDEFAULT = 10,
            SW_MAX = 10
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr FindWindow(string strClassName, string strWindowName);


        public static void SetWindowless(IntPtr wnd)
        {
            ////int style = GetWindowLong(wnd, GWL_STYLE);

            //////get menu
            ////IntPtr HMENU = GetMenu(wnd);
            //////get item count
            ////int count = GetMenuItemCount(HMENU);
            //////loop & remove
            ////for (int i = 0; i < count; i++)
            ////    RemoveMenu(HMENU, 0, (MF_BYPOSITION | MF_REMOVE));

            //////force a redraw
            ////DrawMenuBar(wnd);
            ////SetWindowLong(wnd, GWL_STYLE, (style & ~WS_SYSMENU));
            ////SetWindowLong(wnd, GWL_STYLE, (style & ~WS_CAPTION)); 

            SetWindowLongA(wnd, GWL_STYLE, WS_VISIBLE);
            ////SetWindowPos((int)wnd, HWND_BOTTOM, 0, 0, 100, 100, SWP_NOSIZE);

            //int style = GetWindowLong(wnd, GWL_STYLE);

            //style = style & ~((int)WS_CAPTION) & ~((int)WS_THICKFRAME); // Removes Caption bar and the sizing border
            //style |= ((int)WS_CHILD); // Must be a child window to be hosted
            //SetWindowLong(wnd, GWL_STYLE, style);
        }

        [DllImport("user32")]
        public static extern int SetWindowPos(int hwnd, int hWndInsertAfter, int x, int y, int cx, int cy, int wFlags);

        public static IntPtr SetForegroundWindow(IntPtr hWnd)
        {
            return SetForegroundWindowNative(hWnd);
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int left;
            public int top;
            public int right;
            public int bottom;
        }

        public static RECT GetWindowRect(string procname)
        {
            int p1 = 0;
            int p2 = 0;
            Point pt = new Point(0, 0);
            Size size = new Size(0, 0);
            WNDSTATE state = WNDSTATE.SW_NORMAL;

            GetWindowPos(GetHandleByName(procname), ref p1, ref p2, ref pt, ref size, ref state);

            RECT rect = new RECT();
            rect.left = pt.X;
            rect.top = pt.Y;
            rect.right = pt.X + size.Width;
            rect.bottom = pt.Y + size.Height;

            return rect;
        }

        private static void GetWindowPos(IntPtr hwnd, ref int ptrPhwnd, ref int ptrNhwnd, ref Point ptPoint, ref Size szSize, ref WNDSTATE intShowCmd)
        {
            WINDOWPLACEMENT wInf = new WINDOWPLACEMENT();
            wInf.length = System.Runtime.InteropServices.Marshal.SizeOf(wInf);
            GetWindowPlacement(hwnd, ref wInf);
            szSize = new Size(wInf.rcNormalPosition.Right - (wInf.rcNormalPosition.Left * 2), wInf.rcNormalPosition.Bottom - (wInf.rcNormalPosition.Top * 2));
            ptPoint = new Point(wInf.rcNormalPosition.Left, wInf.rcNormalPosition.Top);
        }

        public static void WaitInitRectByName(string procname)
        {
            RECT rect = new RECT();

            IntPtr hWnd = GetHandleByName(procname);

            int i = 0;
            int timeout = 7000; // 7 seconds

            while (rect.right == 0 && rect.bottom == 0)
            {
                if (i > timeout)
                {
                    break;
                }
                Thread.Sleep(250);
                hWnd = GetHandleByName(procname);
                GetWindowRect(hWnd, ref rect);
                i += 250;
            }
        }

        public static RECT GetProcessWindow(string procname)
        {
            RECT rect = new RECT();
            IntPtr handle = GetHandleByName(procname);
            GetWindowRect(handle, ref rect);

            return rect;
        }

        public static IntPtr GetHandleByName(string procname)
        {
            IntPtr hWnd = FindWindow(procname, null);

            if (hWnd == IntPtr.Zero)
                hWnd = FindWindow(null, procname);

            return hWnd;
        }

        public static void IsExeInitialized(string name)
        {
            int i = 0;
            int timeout = 7000; // 7 seconds

            while (CheckExeProcessAlive(name) == false)
            {
                if (i > timeout)
                {
                    break;
                }
                Thread.Sleep(250);
                i += 250;
            }
        }


        public static ulong GetProcessMemoryBytes(string processName)
        {
            ulong memsize = 0;
            using (PerformanceCounter PC = new PerformanceCounter())
            {
                try
                {
                    PC.CategoryName = "Process";
                    PC.CounterName = "Working Set - Private";
                    PC.InstanceName = processName;
                    memsize = Convert.ToUInt64(PC.NextValue());
                }
                catch (Exception ee) { }
                finally
                {
                    PC.Close();
                }
            }

            return memsize;
        }

        public static ulong GetProcessMemoryKB(string processName)
        {
            ulong mem_kb = Convert.ToUInt64(GetProcessMemoryBytes(processName) / 1024);
            return mem_kb;
        }

        public static ulong GetProcessMemoryMB(string processName)
        {
            ulong mem_mb = Convert.ToUInt64(GetProcessMemoryKB(processName) / 1024);
            return mem_mb;
        }


        [StructLayout(LayoutKind.Sequential)]
        internal struct MEMORYSTATUSEX
        {
            internal uint dwLength;
            internal uint dwMemoryLoad;
            internal ulong ullTotalPhys;
            internal ulong ullAvailPhys;
            internal ulong ullTotalPageFile;
            internal ulong ullAvailPageFile;
            internal ulong ullTotalVirtual;
            internal ulong ullAvailVirtual;
            internal ulong ullAvailExtendedVirtual;
        }
        [return: MarshalAs(UnmanagedType.Bool)]
        [DllImport("Kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

        public static ulong GetTotalPhysicalMemoryBytes()
        {
            MEMORYSTATUSEX statEX = new MEMORYSTATUSEX();
            statEX.dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
            GlobalMemoryStatusEx(ref statEX);

            return statEX.ullTotalPhys;
        }

        public static ulong GetTotalPhysicalMemoryKB()
        {
            ulong mem_kb = Convert.ToUInt64(GetTotalPhysicalMemoryBytes() / 1024);
            return mem_kb;
        }

        public static ulong GetTotalPhysicalMemoryMB()
        {
            ulong mem_mb = Convert.ToUInt64(GetTotalPhysicalMemoryKB() / 1024);
            return mem_mb;
        }
    }
}

[Flags()]
public enum RedrawWindowFlags : uint
{
    /// <summary>
    /// Invalidates the rectangle or region that you specify in lprcUpdate or hrgnUpdate.
    /// You can set only one of these parameters to a non-NULL value. If both are NULL, RDW_INVALIDATE invalidates the entire window.
    /// </summary>
    Invalidate = 0x1,

    /// <summary>Causes the OS to post a WM_PAINT message to the window regardless of whether a portion of the window is invalid.</summary>
    InternalPaint = 0x2,

    /// <summary>
    /// Causes the window to receive a WM_ERASEBKGND message when the window is repainted.
    /// Specify this value in combination with the RDW_INVALIDATE value; otherwise, RDW_ERASE has no effect.
    /// </summary>
    Erase = 0x4,

    /// <summary>
    /// Validates the rectangle or region that you specify in lprcUpdate or hrgnUpdate.
    /// You can set only one of these parameters to a non-NULL value. If both are NULL, RDW_VALIDATE validates the entire window.
    /// This value does not affect internal WM_PAINT messages.
    /// </summary>
    Validate = 0x8,

    NoInternalPaint = 0x10,

    /// <summary>Suppresses any pending WM_ERASEBKGND messages.</summary>
    NoErase = 0x20,

    /// <summary>Excludes child windows, if any, from the repainting operation.</summary>
    NoChildren = 0x40,

    /// <summary>Includes child windows, if any, in the repainting operation.</summary>
    AllChildren = 0x80,

    /// <summary>Causes the affected windows, which you specify by setting the RDW_ALLCHILDREN and RDW_NOCHILDREN values, to receive WM_ERASEBKGND and WM_PAINT messages before the RedrawWindow returns, if necessary.</summary>
    UpdateNow = 0x100,

    /// <summary>
    /// Causes the affected windows, which you specify by setting the RDW_ALLCHILDREN and RDW_NOCHILDREN values, to receive WM_ERASEBKGND messages before RedrawWindow returns, if necessary.
    /// The affected windows receive WM_PAINT messages at the ordinary time.
    /// </summary>
    EraseNow = 0x200,

    Frame = 0x400,

    NoFrame = 0x800
}
