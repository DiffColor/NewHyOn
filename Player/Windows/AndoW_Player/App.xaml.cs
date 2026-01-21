using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows;
using TurtleTools;

namespace HyOnPlayer
{
    /// <summary>
    /// App.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class App : Application
    {
        private Mutex _instanceMutex = null;
        string procName = "HyOn Player";

        protected override void OnStartup(StartupEventArgs e)
        {
            bool createdNew;
            _instanceMutex = new Mutex(true, procName, out createdNew);

            if (!createdNew)
            {
                _instanceMutex = null;
                Application.Current.Shutdown();
                return;
            }

            EnsureAuthKey();

            base.OnStartup(e);
            StartupUri = new Uri("MainWindow.xaml", UriKind.Relative);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            if (_instanceMutex != null)
                _instanceMutex.ReleaseMutex();
            base.OnExit(e);
        }

        private void EnsureAuthKey()
        {
            try
            {
                var playerRepo = new PlayerInfoManager();
                var info = playerRepo.g_PlayerInfo;

                if (string.IsNullOrWhiteSpace(info.PIF_AuthKey))
                {
                    List<string> nics = NetworkTools.GetAllMACAddressesBySystemNet();
                    if (nics.Count > 0)
                    {
                        info.PIF_AuthKey = AuthTools.EncodeAuthKey(nics[0]);
                    }
                    else
                    {
                        info.PIF_AuthKey = string.Empty;
                    }
                    playerRepo.SaveData();
                }
            }
            catch
            {
                // auth 키 확보 실패 시에도 앱은 계속 진행
            }
        }
    }
}
