using System;
using System.Threading;
using System.Windows.Forms;

namespace ConfigPlayer
{
    static class Program
    {
        /// <summary>
        /// 해당 응용 프로그램의 주 진입점입니다.
        /// </summary>
        [STAThread]
        static void Main()
        {
            bool newProc;
            Mutex dup = new Mutex(true, "ConfigPlayer", out newProc);

            if (newProc)
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                // Make sure the application runs!
                Application.Run(new Form1());
                dup.ReleaseMutex();
            }
        }
    }
}
