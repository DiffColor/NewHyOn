using NewHyOnPlayer.Services;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Windows;

namespace NewHyOnPlayer
{
    /// <summary>
    /// App.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class App : Application
    {
        private const string ProcName = "NewHyOn Player";
        private const string SettingsExecutableName = "NewHyOn Player Settings.exe";

        private Mutex _instanceMutex;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            bool createdNew;
            _instanceMutex = new Mutex(true, ProcName, out createdNew);
            if (!createdNew)
            {
                _instanceMutex = null;
                Shutdown();
                return;
            }

            try
            {
                var validation = LicenseHubAuthService.Validate();
                if (!validation.IsValid)
                {
                    OpenSettingsAndShutdown();
                    return;
                }

                ShowMainWindow();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    string.Format("인증 확인 중 오류가 발생했습니다.{0}{1}", Environment.NewLine, ex.Message),
                    "인증 오류",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                Shutdown();
            }
        }

        private void OpenSettingsAndShutdown()
        {
            ReleaseInstanceMutex();

            MessageBox.Show(
                "인증이 필요하여 Windows 설정 프로그램을 엽니다.",
                "미인증 플레이어",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);

            string settingsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, SettingsExecutableName);
            if (File.Exists(settingsPath))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = settingsPath,
                    UseShellExecute = true,
                    WorkingDirectory = Path.GetDirectoryName(settingsPath) ?? AppDomain.CurrentDomain.BaseDirectory
                });
            }
            else
            {
                MessageBox.Show(
                    string.Format("인증이 필요하지만 설정 프로그램을 찾을 수 없습니다.{0}{1}", Environment.NewLine, settingsPath),
                    "인증 필요",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }

            Shutdown();
            Process.GetCurrentProcess().Kill();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            ReleaseInstanceMutex();
            base.OnExit(e);
        }

        private void ReleaseInstanceMutex()
        {
            if (_instanceMutex == null)
            {
                return;
            }

            _instanceMutex.ReleaseMutex();
            _instanceMutex.Dispose();
            _instanceMutex = null;
        }

        private void ShowMainWindow()
        {
            var window = new MainWindow();
            MainWindow = window;
            ShutdownMode = ShutdownMode.OnMainWindowClose;
            window.Show();
        }
    }
}
