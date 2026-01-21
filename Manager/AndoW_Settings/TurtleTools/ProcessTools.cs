
using System;
using System.Diagnostics;
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
        
        public static void LaunchProcess(string exePath, bool isAdmin = false, string arg = "", bool isWait = false)
        {
            try
            {
                System.Diagnostics.Process view = new System.Diagnostics.Process();
                view.EnableRaisingEvents = true;
                view.StartInfo.CreateNoWindow = true;

                if (File.Exists(exePath))
                {
                    view.StartInfo.FileName = Path.GetFileName(exePath);
                    view.StartInfo.WorkingDirectory = Path.GetDirectoryName(exePath);
                    view.StartInfo.UseShellExecute = false;

                    if (!string.IsNullOrEmpty(arg))
                    {
                        view.StartInfo.Arguments = arg;
                    }

                    if (isAdmin)
                    {
                        view.StartInfo.UseShellExecute = true;
                        view.StartInfo.Verb = "runas";
                    }
                    view.Start();

                    if (isWait)
                        view.WaitForExit();
                }

            }
            catch (Exception ex)
            {
                Logger.WriteErrorLog(ex.ToString(), Logger.GetLogFileName());
            }
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

        public static void WaitInitRectByName(IntPtr wnd)
        {
            RECT Rect1 = new RECT();
            RECT Rect2 = new RECT();

            GetWindowRect(wnd, ref Rect1);
            int i = 0;
            int timeout = 7000; // 7 seconds

            while (Rect1.Equals(Rect2) || (Rect2.right == 0 && Rect2.bottom == 0))
            {
                if (i > timeout)
                {
                    break;
                }
                Thread.Sleep(25);
                GetWindowRect(wnd, ref Rect2);
                i += 25;
            }
        }

        public static RECT GetProcessWindow(string procname)
        {
            RECT rect = new RECT();
            IntPtr handle = GetHandleByName(procname);
            GetWindowRect(handle, ref rect);

            return rect;
        }

        public static IntPtr GetHandleByName(string name)
        {
            Process[] processes = Process.GetProcessesByName(name);

            foreach (Process p in processes)
            {
                return p.MainWindowHandle;
            }

            return IntPtr.Zero;
        }

        public static void IsExeInitialized(string name)
        {
            int i = 0;
            int timeout = 12000; // 12 seconds

            while (GetHandleByName(name) == IntPtr.Zero)
            {
                if (i > timeout)
                {
                    break;
                }
                Thread.Sleep(1000);
                i += 1000;
            }
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
