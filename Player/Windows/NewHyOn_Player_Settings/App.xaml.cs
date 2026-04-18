using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace NewHyOn.Player.Settings;

public partial class App : Application
{
    private const string SingleInstanceMutexName = @"Local\NewHyOn.Player.Settings";
    private Mutex? singleInstanceMutex;

    protected override void OnStartup(StartupEventArgs e)
    {
        bool createdNew;
        singleInstanceMutex = new Mutex(true, SingleInstanceMutexName, out createdNew);
        if (!createdNew)
        {
            MessageBox.Show("NewHyOn Player Settings는 이미 실행 중입니다.", "중복 실행 차단", MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        DispatcherUnhandledException += OnDispatcherUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
        base.OnStartup(e);
        MainWindow = new MainWindow();
        MainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        DispatcherUnhandledException -= OnDispatcherUnhandledException;
        TaskScheduler.UnobservedTaskException -= OnUnobservedTaskException;
        singleInstanceMutex?.ReleaseMutex();
        singleInstanceMutex?.Dispose();
        singleInstanceMutex = null;
        base.OnExit(e);
    }

    private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        e.Handled = true;
    }

    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        e.SetObserved();
    }
}
