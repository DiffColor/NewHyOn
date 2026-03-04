using System;
using System.Diagnostics;
using System.Windows.Forms;
using TurtleTools;



namespace AndoWSettings
{
    public partial class Form1 : Form
    {
        private LocalConnectionSettings _connectionSettings;
        private LocalFtpSettings _ftpSettings;
        private LocalUiSettings _uiSettings;

        public Form1()
        {
            //Thread.CurrentThread.CurrentUICulture = new CultureInfo("en-US");

            try
            {
                InitializeComponent();
                InitEventHandler();
                LoadLocalSettings();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }

        }

        public void InitEventHandler()
        {
            SaveBtn.Click += SaveBtn_Click;  // 포트번호저장
        }

        void SaveBtn_Click(object sender, EventArgs e)
        {
            ProcessTools.KillExeProcess(FNDTools.GetManagerProcName());
            ProcessTools.KillExeProcess(FNDTools.GetHyOnManagerProcName());

            SaveTurtlePort();
        }

        public void SaveTurtlePort()
        {
            if (FTPPort.Text == string.Empty || 
                PasvMinPort.Text == string.Empty || 
                PasvMaxPort.Text == string.Empty)
            {
                MessageBox.Show("포트번호를 입력해 주세요.");
                return;
            }

            try
            {
                int ftpPortNum = Convert.ToInt32(FTPPort.Text);
                int ftpPasvMinPortNum = Convert.ToInt32(PasvMinPort.Text);
                int ftpPasvMaxPortNum = Convert.ToInt32(PasvMaxPort.Text);

                if (ftpPortNum <= 0 || ftpPortNum > 65535)
                {
                    MessageBox.Show("FTP 포트 범위를 확인해주세요.");
                    return;
                }

                if (ftpPasvMinPortNum <= 0 || ftpPasvMinPortNum > 65535 ||
                    ftpPasvMaxPortNum <= 0 || ftpPasvMaxPortNum > 65535 ||
                    ftpPasvMinPortNum > ftpPasvMaxPortNum)
                {
                    MessageBox.Show("패시브 포트 범위를 확인해주세요.");
                    return;
                }

                if (_connectionSettings == null)
                {
                    _connectionSettings = LocalSettingsStore.GetConnectionSettings();
                }

                if (_ftpSettings == null)
                {
                    _ftpSettings = LocalSettingsStore.GetFtpSettings();
                }

                if (_uiSettings == null)
                {
                    _uiSettings = LocalSettingsStore.GetUiSettings();
                }

                string dataServerIp = dataServerIpTextBox?.Text?.Trim() ?? string.Empty;
                string messageServerIp = messageServerIpTextBox?.Text?.Trim() ?? string.Empty;

                if (!string.IsNullOrWhiteSpace(dataServerIp))
                {
                    _connectionSettings.RethinkHost = dataServerIp;
                    _ftpSettings.Host = dataServerIp;
                }

                if (!string.IsNullOrWhiteSpace(messageServerIp))
                {
                    _connectionSettings.SignalRHost = messageServerIp;
                }

                _ftpSettings.Port = ftpPortNum;
                _ftpSettings.PasvMinPort = ftpPasvMinPortNum;
                _ftpSettings.PasvMaxPort = ftpPasvMaxPortNum;
                _ftpSettings.RootPath = NormalizeFtpRootPath(ftpRootPathTextBox?.Text);
                _uiSettings.PreserveAspectRatio = aspect_ratio_chbox.Checked;

                LocalSettingsStore.SaveConnectionSettings(_connectionSettings);
                LocalSettingsStore.SaveFtpSettings(_ftpSettings);
                LocalSettingsStore.SaveUiSettings(_uiSettings);

                var serverSettings = new ServerSettings
                {
                    DataServerIp = _connectionSettings.RethinkHost,
                    MessageServerIp = _connectionSettings.SignalRHost,
                    FTP_Port = _ftpSettings.Port,
                    FTP_PasvMinPort = _ftpSettings.PasvMinPort,
                    FTP_PasvMaxPort = _ftpSettings.PasvMaxPort,
                    FTP_RootPath = _ftpSettings.RootPath,
                    PreserveAspectRatio = _uiSettings.PreserveAspectRatio
                };

                new ServerSettingsManager().SaveData(serverSettings);

                MessageBox.Show("설정을 저장했습니다.");

                ProcessTools.KillExeProcess(FNDTools.GetManagerProcName());

                Application.Exit();
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show("포트번호가 제대로 입력되지 않았습니다.");
            }
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            DisplayServerSettings();
        }

        private void DisplayServerSettings()
        {
            if (_ftpSettings == null || _connectionSettings == null || _uiSettings == null)
            {
                LoadLocalSettings();
            }

            FTPPort.Text = (_ftpSettings?.Port ?? NetworkTools.FTP_PORT).ToString();
            PasvMinPort.Text = (_ftpSettings?.PasvMinPort ?? NetworkTools.FTP_PASV_MIN_PORT).ToString();
            PasvMaxPort.Text = (_ftpSettings?.PasvMaxPort ?? NetworkTools.FTP_PASV_MAX_PORT).ToString();
            ftpRootPathTextBox.Text = NormalizeFtpRootPath(_ftpSettings?.RootPath);

            aspect_ratio_chbox.Checked = _uiSettings?.PreserveAspectRatio ?? false;
            if (dataServerIpTextBox != null)
            {
                dataServerIpTextBox.Text = _connectionSettings?.RethinkHost ?? string.Empty;
            }
            if (messageServerIpTextBox != null)
            {
                messageServerIpTextBox.Text = _connectionSettings?.SignalRHost ?? string.Empty;
            }
        }

        private void LoadLocalSettings()
        {
            LocalSettingsStore.EnsureSeeded();
            _connectionSettings = LocalSettingsStore.GetConnectionSettings();
            _ftpSettings = LocalSettingsStore.GetFtpSettings();
            _uiSettings = LocalSettingsStore.GetUiSettings();
        }

        private void showipbtn_Click(object sender, EventArgs e)
        {
            Process.Start("CMD.exe", "/K ipconfig");
        }

        private static string NormalizeFtpRootPath(string rootPath)
        {
            if (string.IsNullOrWhiteSpace(rootPath))
            {
                return "/NewHyOnEnt";
            }

            string normalized = rootPath.Replace("\\", "/").Trim();
            if (!normalized.StartsWith("/"))
            {
                normalized = "/" + normalized;
            }

            normalized = normalized.TrimEnd('/');
            return string.IsNullOrWhiteSpace(normalized) ? "/" : normalized;
        }
    }
}
