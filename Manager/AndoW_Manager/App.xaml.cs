using System;
using System.Threading;
using System.Windows;
using TurtleTools;

namespace AndoW_Manager
{
	/// <summary>
	/// Interaction logic for App.xaml
	/// </summary>
	public partial class App : Application
	{
        private Mutex _instanceMutex = null;
        string procName = "AndoW_Manager";

        protected override async void OnStartup(StartupEventArgs e)
        {
            _instanceMutex = new Mutex(true, procName, out bool createdNew);

            if (!createdNew)
            {
                _instanceMutex = null;
                Application.Current.Shutdown();
                return;
            }

            // 모든 창이 열릴 때 ModernScrollViewer 마우스 휠 스크롤 기능을 적용
            EventManager.RegisterClassHandler(typeof(Window), Window.LoadedEvent,
                new RoutedEventHandler((sender, args) => {
                    if (sender is Window window)
                    {
                        ModernScrollViewerBehavior.ApplyToWindow(window);
                    }
                }));

            base.OnStartup(e);
            StartupUri = new Uri("MainWindow.xaml", UriKind.Relative);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            if (_instanceMutex != null)
                _instanceMutex.ReleaseMutex();
            base.OnExit(e);
        }
	}
}
