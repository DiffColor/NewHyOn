using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace AndoWSettings
{
    static class Program
    {
        private const string ManagerAllowedAppRuleName = "newhyon_manager_allowed_app";

        /// <summary>
        /// 해당 응용 프로그램의 주 진입점입니다.
        /// </summary>
        [STAThread]
        static void Main()
        {
            bool newProc;
            Mutex dup = new Mutex(true, "NewHyOn_Settings", out newProc);

            if (newProc)
            {
                try
                {
                    EnsureAllowedProgramRule(ManagerAllowedAppRuleName, FNDTools.GetManagerExeFilePath());
                }
                catch (Exception ex)
                {
                    MessageBox.Show("매니저 방화벽 허용앱 등록에 실패했습니다.\r\n" + ex.Message);
                }

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                // Make sure the application runs!
                Application.Run(new Form1());
                dup.ReleaseMutex();
            }
        }

        private static void EnsureAllowedProgramRule(string ruleName, string executablePath)
        {
            if (string.IsNullOrWhiteSpace(ruleName) || string.IsNullOrWhiteSpace(executablePath) || File.Exists(executablePath) == false)
            {
                return;
            }

            if (HasRule(ruleName))
            {
                ExecuteNetsh(string.Format("advfirewall firewall set rule name=\"{0}\" new dir=in action=allow program=\"{1}\" enable=yes", ruleName, executablePath));
            }
            else
            {
                ExecuteNetsh(string.Format("advfirewall firewall add rule name=\"{0}\" dir=in action=allow program=\"{1}\" enable=yes", ruleName, executablePath));
            }

            ExecuteNetsh(string.Format("advfirewall firewall set rule name=\"{0}\" new enable=yes profile=private,public", ruleName));
        }

        private static bool HasRule(string ruleName)
        {
            using (Process process = new Process())
            {
                process.StartInfo = new ProcessStartInfo("netsh", string.Format("advfirewall firewall show rule name=\"{0}\"", ruleName))
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                process.Start();
                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();
                process.WaitForExit();

                string combined = string.Concat(output, Environment.NewLine, error);
                if (string.IsNullOrWhiteSpace(combined))
                {
                    return false;
                }

                string normalized = combined.Replace(" ", string.Empty).ToLowerInvariant();
                return normalized.Contains("norulesmatchthespecifiedcriteria") == false
                    && normalized.Contains("지정한규칙을찾을수없습니다") == false
                    && normalized.Contains("지정한조건과일치하는규칙이없습니다") == false;
            }
        }

        private static void ExecuteNetsh(string arguments)
        {
            using (Process process = new Process())
            {
                process.StartInfo = new ProcessStartInfo("netsh", arguments)
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                process.Start();
                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();
                process.WaitForExit();

                if (process.ExitCode != 0)
                {
                    string message = string.IsNullOrWhiteSpace(error) ? output : error;
                    throw new InvalidOperationException(message);
                }
            }
        }
    }
}
