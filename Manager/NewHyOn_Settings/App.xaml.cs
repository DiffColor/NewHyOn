using System.Threading;
using System.Windows;

namespace NewHyOn.Settings.Wpf;

public partial class App : Application
{
    private const string SingleInstanceMutexName = @"Local\NewHyOn.Settings.Wpf";
    private Mutex? singleInstanceMutex;

    protected override void OnStartup(StartupEventArgs e)
    {
        bool createdNew;
        singleInstanceMutex = new Mutex(true, SingleInstanceMutexName, out createdNew);
        if (!createdNew)
        {
            MessageBox.Show("NewHyOn Settings는 이미 실행 중입니다.", "중복 실행 차단", MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        base.OnStartup(e);
        MainWindow = new MainWindow();
        MainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        singleInstanceMutex?.ReleaseMutex();
        singleInstanceMutex?.Dispose();
        singleInstanceMutex = null;
        base.OnExit(e);
    }
}
