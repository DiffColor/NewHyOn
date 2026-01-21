using System;
using System.Runtime.InteropServices;
using System.Threading;
using frms = System.Windows.Forms;

namespace TurtleTools
{
    public class MessageTools
    {

        public static bool ShowMessageBox(string msg)
        {
            NotifyWindow wnd = new NotifyWindow(msg);
            wnd.ShowDialog();

            if (wnd.DialogResult == null) return true;

            bool IsWndResult = (bool)wnd.DialogResult;

            return IsWndResult;
        }
        public static bool ShowMessageBox(string msg, string btn)
        {
            NotifyWindow wnd = new NotifyWindow(msg, btn);
            wnd.ShowDialog();

            bool IsWndResult = (bool)wnd.DialogResult;

            return IsWndResult;
        }
        public static bool ShowMessageBox(string msg, string btn1, string btn2)
        {
            NotifyWindow wnd = new NotifyWindow(msg, btn1, btn2);
            wnd.ShowDialog();

            bool IsWndResult = (bool)wnd.DialogResult;

            return IsWndResult;
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr FindWindow(string strClassName, string strWindowName);

        [DllImport("user32.dll")]
        public static extern int SendMessage(int hWnd, uint Msg, int wParam, int lParam);
        [DllImport("user32.dll")]
        public static extern IntPtr PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);
        
        public const uint WM_SYSKEYDOWN = 0x104;
        public const uint WM_KEYDOWN = 0x100;

        public const int VK_LSHIFT = 0xA0;
        public const int VK_LCONTROL = 0xA2;

        public const int KEY_A = 0x41;
        public const int KEY_C = 0x43;
        public const int KEY_H = 0x48;
        public const int KEY_S = 0x53;
        public const int KEY_Z = 0x5A;

        public const uint KEY_NUM0 = 0x60;

        private const uint MOUSEEVENTF_MOVE = 0x0001;
        private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
        private const uint MOUSEEVENTF_LEFTUP = 0x0004;
        private const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
        private const uint MOUSEEVENTF_RIGHTUP = 0x0010;
        private const uint MOUSEEVENTF_ABSOLUTE = 0x8000;

        [DllImport("user32.dll", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.StdCall)]
        public static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint cButtons, UIntPtr dwExtraInfo);
        private void MouseMove(uint x, uint y)
        {
            mouse_event(MOUSEEVENTF_ABSOLUTE | MOUSEEVENTF_MOVE, x, y, 0, UIntPtr.Zero);
        }
        public static void MouseClick(uint x, uint y)
        {
            mouse_event(MOUSEEVENTF_ABSOLUTE | MOUSEEVENTF_LEFTDOWN, x, y, 0, UIntPtr.Zero);
        }
        public static void MouseDoubleClick(uint x, uint y)
        {
            MouseClick(x, y);
            Thread.Sleep(400);
            MouseClick(x, y);
        }

        public static void SendMessageByName(string processName, uint msg)
        {
            int hWnd = (int)FindWindow(null, processName);
            if (hWnd > 0)
            {
                SendMessage(hWnd, msg, 1, 1);
            }
        }

        public static void SendPostMessageToWnd(string processName, uint msg, int msg2=1, int msg3=1)
        {
            IntPtr hWnd = FindWindow(null, processName);
            if (hWnd.ToInt32() > 0)
            {
                PostMessage(hWnd, msg, new IntPtr(msg2), new IntPtr(msg3));
            }
        }

        public static void SendKeyByName(string processName, int key1, int key2)
        {
            int hWnd = (int)FindWindow(processName, null);
            if (hWnd > 0)
            {
                SendMessage(hWnd, WM_KEYDOWN, key1, key2);
            }
        }

        public static void SendPostMessageByName(string processName, uint msg)
        {
            IntPtr hWnd = FindWindow(null, processName);
            if (hWnd.ToInt32() > 0)
            {
                PostMessage(hWnd, msg, new IntPtr(1), new IntPtr(1));
            }
        }

        public static void SendPostKeyByName(string processName, IntPtr key1, IntPtr key2)
        {
            IntPtr hWnd = FindWindow(processName, null);
            if (hWnd != IntPtr.Zero)
            {
                PostMessage(hWnd, WM_KEYDOWN, key1, key2);
            }
        }

        public static void SendKeyByWnd(IntPtr hWnd, Messaging.VKeys key, Messaging.VKeys shift = Messaging.VKeys.NULL, Messaging.ShiftType type = Messaging.ShiftType.NONE)
        {
            Key k = new Key(key, shift, type);
            k.PressForeground(hWnd);
        }

        public static void SendKeyDownMsg(string keymsg)
        {
            frms.SendKeys.SendWait(keymsg);
        }
    }
}
