using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Windows;
using TurtleTools;

namespace NewHyOnPlayer
{
    /// <summary>
    /// App.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class App : Application
    {
        private Mutex _instanceMutex = null;
        string procName = "NewHyOn Player";

        protected override void OnStartup(StartupEventArgs e)
        {
            PlayerInfoManager g_PlayerInfoManager = new PlayerInfoManager();

            if (!EnsureAuthorized(g_PlayerInfoManager))
            {
                //if (CheckDemoExpired())
                //{
                    MessageBox.Show("인증이 필요한 프로그램입니다.");
                    Application.Current.Shutdown();
                    return;
                //}
            }

            bool createdNew;
            _instanceMutex = new Mutex(true, procName, out createdNew);
            
            if (!createdNew)
            {
                _instanceMutex = null;
                Application.Current.Shutdown();
                return;
            }

            base.OnStartup(e);
            StartupUri = new Uri("MainWindow.xaml", UriKind.Relative);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            if (_instanceMutex != null)
                _instanceMutex.ReleaseMutex();
            base.OnExit(e);
        }

        private bool EnsureAuthorized(PlayerInfoManager playerInfoManager)
        {
            if (playerInfoManager == null || playerInfoManager.g_PlayerInfo == null)
                return false;

            string dbAuthKey = playerInfoManager.g_PlayerInfo.PIF_AuthKey;
            if (IsDeviceAuthKey(dbAuthKey))
                return true;

            string fileAuthKey = ReadValidAuthKeyFromFile();
            if (string.IsNullOrWhiteSpace(fileAuthKey))
                return false;

            playerInfoManager.g_PlayerInfo.PIF_AuthKey = fileAuthKey;
            playerInfoManager.SaveData();
            return true;
        }

        private bool IsDeviceAuthKey(string encodedKey)
        {
            if (string.IsNullOrEmpty(encodedKey))
                return false;

            List<string> nics = NetworkTools.GetAllMACAddressesBySystemNet();

            foreach (string nic in nics)
            {
                if (encodedKey.Equals(AuthTools.EncodeAuthKey(nic), StringComparison.CurrentCultureIgnoreCase))
                    return true;
            }

            if (nics.Count < 1)
                if (encodedKey.Equals(AuthTools.EncodeAuthKey(AuthTools.getUUID12()), StringComparison.CurrentCultureIgnoreCase))
                    return true;

            return false;
        }

        private string ReadValidAuthKeyFromFile()
        {
            string authKeyPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "AuthKeys");
            if (!File.Exists(authKeyPath))
                return string.Empty;

            try
            {
                foreach (string line in File.ReadLines(authKeyPath))
                {
                    string candidate = line?.Trim() ?? string.Empty;
                    if (IsDeviceAuthKey(candidate))
                        return candidate;
                }
            }
            catch
            {
            }

            return string.Empty;
        }

        // 데모 프로그램은 최초 설치 후 15일 동안 사용 가능
        private bool CheckDemoExpired()
        {
            string subKey = "ILYcode";
            string valueKey = "HyOnInstalled";

            DateTime dt = DateTime.Now;
            //DateTime expireTime = new DateTime(2015, 7, 28);

            //DateTime createdTime = File.GetCreationTime(Assembly.GetExecutingAssembly().Location);

            //if (dtTime.CompareTo(createdTime) >= 0 && dtTime.CompareTo(expireTime) > 0)
            //{
            //    return true;
            //}

            string keyValue = AuthTools.ReadRegKey(subKey, valueKey);

            if (string.IsNullOrEmpty(keyValue))
            {

                string garbage_chars = "abceijklnopqvwxABCEIJLNOPQRSUVWXY";     //datetime ignore chars

                string redun1 = LogicTools.RandomSizeString(4, 8, garbage_chars);
                string redun2 = LogicTools.RandomSizeString(2, 8, garbage_chars);
                string redun3 = LogicTools.RandomSizeString(2, 8, garbage_chars);
                string redun4 = LogicTools.RandomSizeString(4, 8, garbage_chars);

                string dtStr = String.Format("{5}{2}{3}-{4}{6}{5}-{3}{1}{4}-{6}{0}{5}", dt.Year, dt.Month, dt.Day, redun1, redun2, redun3, redun4);

                AuthTools.WriteRegKey(subKey, valueKey, dtStr);
                return false;
            }

            string[] keyValueArr = keyValue.Split('-');
            int year = LogicTools.ConvertToUInt(keyValueArr[3]);
            int month = LogicTools.ConvertToUInt(keyValueArr[2]);
            int day = LogicTools.ConvertToUInt(keyValueArr[0]);
            DateTime installedTime = new DateTime(year, month, day);

            if (dt.CompareTo(installedTime) < 0 || dt.CompareTo(installedTime.AddDays(15)) > 0)
            {
                return true;
            }

            return false;
        }
    }
}
