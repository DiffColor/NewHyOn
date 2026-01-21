using System;
using System.Threading;
using System.Windows.Forms;
using TurtleTools;

namespace AndoWSettings
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
            Mutex dup = new Mutex(true, "AndoW_Settings", out newProc);

            RethinkDbBootstrapper.EnsureRethinkDbReady();

            if (newProc)
            {
                RethinkDbConfigurator.EnsureConfigured();

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                // Make sure the application runs!
                Application.Run(new Form1());
                dup.ReleaseMutex();
            }
        }
    }
}
