using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows;
using frms = System.Windows.Forms;

namespace TurtleTools
{
    public class MessageTools
    {
        public static void ShowGeneralMessageBox(string msg, string title = "TurtleLab")
        {
            MessageBox.Show(msg, title);
        }

        public static bool ShowGeneralMessageBoxQuestion(string msg, string title = "TurtleLab")
        {
            return MessageBox.Show(msg, title, MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes;
        }

        /* WPF */
        //public static bool ShowMessageBox(string msg, Window window = null)
        //{
        //    NotifyWindow wnd = new NotifyWindow(msg, window);
        //    wnd.ShowDialog();

        //    if (wnd.DialogResult == null) return true;

        //    bool IsWndResult = (bool)wnd.DialogResult;

        //    return IsWndResult;
        //}
        //public static bool ShowMessageBox(string msg, string btn, Window window = null)
        //{
        //    NotifyWindow wnd = new NotifyWindow(msg, btn, window);
        //    wnd.ShowDialog();

        //    bool IsWndResult = (bool)wnd.DialogResult;

        //    return IsWndResult;
        //}
        //public static bool ShowMessageBox(string msg, string btn1, string btn2, Window window = null)
        //{
        //    NotifyWindow wnd = new NotifyWindow(msg, btn1, btn2, window);
        //    wnd.ShowDialog();

        //    bool IsWndResult = (bool)wnd.DialogResult;

        //    return IsWndResult;
        //}

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern IntPtr FindWindow(string strClassName, string strWindowName);


        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern int SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, ref COPYDATASTRUCT lParam);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern int SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, StringBuilder lParam);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern int SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, [MarshalAs(UnmanagedType.LPStr)] string lParam);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern int SendMessage(IntPtr hWnd, uint Msg, int wParam, int lParam);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = false)]
        public static extern int SendMessage(HandleRef hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "SendMessageW")]
        public static extern int SendMessageW(IntPtr hWnd, uint Msg, IntPtr wParam, [MarshalAs(UnmanagedType.LPWStr)] string lParam);

        [DllImport("user32.dll")]
        public static extern int PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);
        
        [DllImport("user32.dll")]
        public static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, uint dwExtraInfo);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.StdCall)]
        public static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint cButtons, UIntPtr dwExtraInfo);
        public static void MouseMove(uint x, uint y)
        {
            mouse_event((uint)Messaging.MOUSEEVENTF.ABSOLUTE | (uint)Messaging.MOUSEEVENTF.MOVE, x, y, 0, UIntPtr.Zero);
        }

        public static void MouseClick(uint x, uint y)
        {
            mouse_event((uint)Messaging.MOUSEEVENTF.ABSOLUTE | (uint)Messaging.MOUSEEVENTF.LEFTDOWN, x, y, 0, UIntPtr.Zero);
        }

        public static void MouseClickDownUp(uint x, uint y)
        {
            var oldPos = frms.Cursor.Position;

            frms.Cursor.Position = new System.Drawing.Point((int)x, (int)y);

            //mouse_event((uint)Messaging.MOUSEEVENTF.ABSOLUTE | (uint)Messaging.MOUSEEVENTF.MOVE, x, y, 0, UIntPtr.Zero);
            mouse_event((uint)Messaging.MOUSEEVENTF.LEFTDOWN, 0, 0, 0, UIntPtr.Zero);
            mouse_event((uint)Messaging.MOUSEEVENTF.LEFTUP, 0, 0, 0, UIntPtr.Zero);

            frms.Cursor.Position = oldPos;
        }

        public static void MouseDoubleClick(uint x, uint y)
        {
            MouseClickDownUp(x, y);
            Thread.Sleep(400);
            MouseClickDownUp(x, y);
        }


        public static void SendMessageByName(string processName, uint msg)
        {
            IntPtr hWnd = FindWindow(null, processName);
            if (hWnd != IntPtr.Zero)
                SendMessage(hWnd, msg, 1, 1);
        }

        public static void SendPostMessageToWnd(string processName, uint msg, int msg2=1, int msg3=1)
        {
            IntPtr hWnd = FindWindow(null, processName);
            if (hWnd != IntPtr.Zero)
                PostMessage(hWnd, msg, new IntPtr(msg2), new IntPtr(msg3));
        }

        public static void SendKeyByName(string processName, int key1, int key2)
        {
            IntPtr hWnd = FindWindow(processName, null);
            if (hWnd != IntPtr.Zero)
                SendMessage(hWnd, (uint)Messaging.WindowsMessages.WM_KEYDOWN, key1, key2);
        }

        public static void SendPostMessageByName(string processName, uint msg)
        {
            IntPtr hWnd = FindWindow(null, processName);
            if (hWnd != IntPtr.Zero)
                PostMessage(hWnd, msg, new IntPtr(1), new IntPtr(1));
        }

        public static void SendPostKeyByName(string processName, IntPtr key1, IntPtr key2)
        {
            IntPtr hWnd = FindWindow(processName, null);
            if (hWnd != IntPtr.Zero)
                PostMessage(hWnd, (uint)Messaging.WindowsMessages.WM_KEYDOWN, key1, key2);
        }

        public static void SendKeyByWnd(IntPtr hWnd, Messaging.VKeys key, Messaging.VKeys shift = Messaging.VKeys.NULL, Messaging.ShiftType type = Messaging.ShiftType.NONE)
        {
            Key k = new Key(key, shift, type);
            k.PressForeground(hWnd);
        }

        /*
         *  키입력 방식 설명
         *  https://msdn.microsoft.com/ko-kr/library/system.windows.forms.sendkeys.send(v=vs.110).aspx
         */
        public static void SendKeyDownMsg(string keymsg)
        {
            frms.SendKeys.SendWait(keymsg);
        }


        /*
         * Send or Receive Nested Window Messages (ex: json)
         */

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct COPYDATASTRUCT
        {
            public IntPtr dwData;
            public int cbData;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string lpData;
        }

        public class DataObject
        {
            public string data { get; private set; }
            public MessageCode dataType { get; private set; }

            public DataObject(string relData, MessageCode mesCode)
            {
                this.data = relData;
                this.dataType = mesCode;
            }
        }

        public const uint WM_COPYDATA = 0x004A;

        public enum MessageCode
        {
            NULL = 0,
            MSG1,
            MSG2,
            MSG3,
            MSG4,
            MSG5,
            MSG6,
            MSG7,
            MSG8,
            MSG9,
            MSG10,
            MSG11,
            MSG12,
        }

        public static void SendDataMessage(string wtitle, string data, MessageCode code = MessageCode.NULL)
        {
            COPYDATASTRUCT sendData = new COPYDATASTRUCT();
            sendData.dwData = new IntPtr((int)code);
            sendData.cbData = data.Length * sizeof(char);
            sendData.lpData = data;

            SendMessage(FindWindow(null, wtitle), WM_COPYDATA, IntPtr.Zero, ref sendData);
        }

        public static DataObject ReceiveDataMessage(IntPtr lParam)
        {
            DataObject data = null;
            try
            {
                COPYDATASTRUCT cds = (COPYDATASTRUCT)Marshal.PtrToStructure(lParam, typeof(COPYDATASTRUCT));
                data = new DataObject(cds.lpData.Substring(0, cds.cbData / 2), (MessageCode)cds.dwData);
            }
            catch { data = null; }
            return data;
        }

    }
}
