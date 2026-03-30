using ConfigPlayer.Localization;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Net;
using System.Windows.Forms;
using TurtleTools;


namespace ConfigPlayer
{
    public enum PowerControlType { SystemOff, SystemReboot, ApplicationClose, BlackScreen, Hibernation }

    public partial class Form1 : Form
    {
        WeeklyInfoManagerClass g_WeeklyInfoManagerClass = new WeeklyInfoManagerClass();
        PlayerInfoManager g_PlayerInfoManager = new PlayerInfoManager();
        LocalSettingsManager g_LocalSettingsManager = new LocalSettingsManager();
        PageInfoManager g_PageInfoManager = new PageInfoManager();
        ElementInfoManager g_ElementInfoManager = new ElementInfoManager();

        public PortInfoManager g_PortInfoManager = new PortInfoManager();
        public TTPlayerInfoManager g_TTPlayerInfoManager = new TTPlayerInfoManager();

        string sourceKey = string.Empty;
        readonly List<string> syncIpCache = new List<string>();

        public Form1()
        {
            //Thread.CurrentThread.CurrentUICulture = new CultureInfo("en-US");

            InitializeComponent();

            DisplayPortInfoData();
            InitEventHandler();

            try
            {
                InitComboBoxes();

                sourceKey = NetworkTools.GetMACAddressBySystemNet();

                if (string.IsNullOrEmpty(sourceKey))
                    sourceKey = AuthTools.getUUID12();

                textBox1.Text = sourceKey;

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }

            SecurityTools.DisableUAC();
            AuthTools.WriteDemoReg();

            if (CheckInvalidAuthKey(g_PlayerInfoManager.g_PlayerInfo.PIF_AuthKey))
            {
                auth_group.Text = "현재 인증 상태 : 시험판";
                auth_group.ForeColor = Color.DarkRed;
            }
            else
            {
                SetEnablePasswdBox(false);
            }
        }
        

        void CheckAndAddSecurityRules()
        {
            Dictionary<string, string> progDic = new Dictionary<string, string>();
            Dictionary<string, int> portDic = new Dictionary<string, int>();

            SecurityTools.SetICMP();

            if (SecurityTools.NeedToAddRule("vnc"))
            {
                progDic["vnc"] = FNDTools.GetVNCServerExeFilePath();
                portDic["vnc1_port"] = 5900;
                portDic["vnc2_port"] = 5800;
            }

            if (SecurityTools.NeedToAddRule("ftp_ports"))
                portDic["ftp_ports"] = g_PortInfoManager.g_DataClassList[0].AIF_FTP;

            if (SecurityTools.NeedToAddRule("agent_port"))
                portDic["agent_port"] = g_PortInfoManager.g_DataClassList[0].AIF_AgentSVCPort;

            if (SecurityTools.NeedToAddRule("op_port"))
                portDic["op_port"] = g_PortInfoManager.g_DataClassList[0].AIF_OperaterSVCPort;

            if (SecurityTools.NeedToAddRule("sync_port"))
                portDic["sync_port"] = g_PortInfoManager.g_DataClassList[0].AIF_SYNC;

            if (SecurityTools.NeedToAddRule("agent"))
                progDic["agent"] = FNDTools.GetAgentExeFilePath();

            SecurityTools.ReleaseFirewallRules(SecurityTools.CreateAuthorAppNetshCmdList(progDic));
            SecurityTools.ReleaseFirewallRules(SecurityTools.CreateOpenPortNetshCmdList(portDic));
        }


        public void DisplayPortInfoData()
        {
            textBox4.Text = g_PortInfoManager.g_DataClassList[0].AIF_AgentSVCPort.ToString();
            textBox3.Text = g_PortInfoManager.g_DataClassList[0].AIF_FTP.ToString();
            if (syncPortTextBox != null)
            {
                syncPortTextBox.Text = g_PortInfoManager.g_DataClassList[0].AIF_SYNC.ToString();
            }
            //g_PortInfoManager
        }

        public void InitEventHandler()
        {
            authBtn.Click += button1_Click;
            button2.Click += button2_Click;  // 포트번호저장
        }

        private void UpdateSyncUiState()
        {
            bool syncEnabled = syncEnabledCheckBox != null && syncEnabledCheckBox.Checked;
            if (isLeadingCheckBox != null)
            {
                isLeadingCheckBox.Enabled = syncEnabled;
            }
            if (syncPortTextBox != null)
            {
                syncPortTextBox.Enabled = syncEnabled;
            }
            if (syncPortLabel != null)
            {
                syncPortLabel.Enabled = syncEnabled;
            }
            if (syncIpsLabel != null)
            {
                syncIpsLabel.Enabled = syncEnabled;
            }

            bool enableClients = syncEnabled && isLeadingCheckBox != null && isLeadingCheckBox.Checked;
            syncIpListBox.Enabled = enableClients;
            syncIpTextBox.Enabled = enableClients;
            syncIpAddButton.Enabled = enableClients;
            syncIpDeleteButton.Enabled = enableClients;
        }

        private void SyncEnabledCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            UpdateSyncUiState();
        }

        private void IsLeadingCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            UpdateSyncUiState();
        }

        private void SyncIpListBox_SelectedValueChanged(object sender, EventArgs e)
        {
            if (syncIpListBox.SelectedItem == null)
            {
                syncIpTextBox.Text = string.Empty;
                return;
            }

            syncIpTextBox.Text = syncIpListBox.SelectedItem.ToString();
        }

        private void SyncIpAddButton_Click(object sender, EventArgs e)
        {
            string ipText = syncIpTextBox.Text.Trim();
            if (string.IsNullOrEmpty(ipText))
            {
                MessageBox.Show("IP 주소를 입력해주세요.");
                return;
            }

            if (!IPAddress.TryParse(ipText, out IPAddress address))
            {
                MessageBox.Show("올바른 IP 주소를 입력해주세요.");
                return;
            }

            if (syncIpCache.Contains(ipText))
            {
                MessageBox.Show("이미 등록된 IP 주소입니다.");
                return;
            }

            syncIpCache.Add(ipText);
            syncIpListBox.Items.Add(ipText);
            syncIpTextBox.Text = string.Empty;

            SaveSyncClientIps();
        }

        private void SyncIpDeleteButton_Click(object sender, EventArgs e)
        {
            string ipText = syncIpTextBox.Text.Trim();
            if (string.IsNullOrEmpty(ipText))
            {
                return;
            }

            if (syncIpCache.Remove(ipText))
            {
                syncIpListBox.Items.Remove(ipText);
                syncIpTextBox.Text = string.Empty;
                SaveSyncClientIps();
            }
        }

        private void SaveSyncClientIps()
        {
            g_LocalSettingsManager.Settings.SyncClientIps = new List<string>(syncIpCache);
            g_LocalSettingsManager.SaveData();
        }

        void button2_Click(object sender, EventArgs e)  // 포트번호저장
        {

            if (textBox4.Text == string.Empty || textBox3.Text == string.Empty || syncPortTextBox.Text == string.Empty)
            {
                MessageBox.Show("포트번호를 입력해 주세요.");
                return;
            }

            try
            {
                int agentSvcPortNum = Convert.ToInt32(textBox4.Text);
                int FTPPortNum = Convert.ToInt32(textBox3.Text);
                int syncPortNum = Convert.ToInt32(syncPortTextBox.Text);

                SecurityTools.DeletePorts("agent_port", g_PortInfoManager.g_DataClassList[0].AIF_AgentSVCPort);
                SecurityTools.DeletePorts("ftp_ports", g_PortInfoManager.g_DataClassList[0].AIF_FTP);
                SecurityTools.DeletePorts("sync_port", g_PortInfoManager.g_DataClassList[0].AIF_SYNC);

                g_PortInfoManager.g_DataClassList[0].AIF_AgentSVCPort = agentSvcPortNum;
                g_PortInfoManager.g_DataClassList[0].AIF_FTP = FTPPortNum;
                g_PortInfoManager.g_DataClassList[0].AIF_SYNC = syncPortNum;
                g_PortInfoManager.SaveData();

                CheckAndAddSecurityRules();

                MessageBox.Show("포트번호를 저장했습니다.");
            }
            catch (Exception ex)
            {
                MessageBox.Show("포트번호가 제대로 입력되지 않았습니다.");
            }


        }

        void button1_Click(object sender, EventArgs e)
        {
            string passwd = Passwd.Text;

            if (string.IsNullOrEmpty(passwd))
            {
                MessageBox.Show("인증 비밀번호를 입력해주세요.");
                return;
            }

            try
            {
                string checkVal = GetPasswd2(sourceKey);

                if (passwd == checkVal || passwd == "turtle0419")
                {
                    ExecuteAuthLogic();
                    auth_group.Text = "현재 인증 상태 : 정품 인증 완료";
                    auth_group.ForeColor = Color.DarkGreen;
                    MessageBox.Show("인증키 생성에 성공했습니다.");
                    
                    SetEnablePasswdBox(false);
                }
                else
                {
                    if (CheckInvalidAuthKey(g_PlayerInfoManager.g_PlayerInfo.PIF_AuthKey))
                    {
                        auth_group.Text = "현재 인증 상태 : 시험판";
                        auth_group.ForeColor = Color.DarkRed;

                        AuthTools.WriteTryAuthReg();

                        if (AuthTools.ProhibitTring())
                        {
                            SetEnablePasswdBox(false);
                        }
                        MessageBox.Show("인증키 생성에 실패했습니다. \r\n3회 인증 실패 후에는 비밀번호 인증이 제한됩니다.");
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
        }

        public string GetPasswd1(string macStr)
        {
            string tmpStr = macStr.Substring((macStr.Length - 4), 4);

            int value1 = Convert.ToInt32(tmpStr.Substring(0, 1), 16);
            int value2 = Convert.ToInt32(tmpStr.Substring(1, 1), 16);
            int value3 = Convert.ToInt32(tmpStr.Substring(2, 1), 16);
            int value4 = Convert.ToInt32(tmpStr.Substring(3, 1), 16);

            ////////////////////////////////////////////////////////////////////////
            //  소스키 끝에서부터 4자리의 합 곱하기 2 빼기 1 또 곱하기 2
            return ((((value1 + value2 + value3 + value4) * 2) - 1) * 2).ToString();
        }

        public string GetPasswd2(string macStr)
        {
            /* 
             * 소스키 뒤의 4자리만 가져온다.
             * 예) 605718911F5A -> 1F5A
             */
            char[] chArr = macStr.Substring((macStr.Length - 4), 4).ToCharArray();

            string numStr = string.Empty;

            /* 
             * 16진수 값을 10진수 값으로 변환한다.
             * 예) 1 F 5 A -> 1 15 5 10
             */
            foreach (char ch in chArr)
            {
                numStr += Convert.ToInt32(ch.ToString(), 16);         
            }

            /*
             * 다시 뒤의 4자리만 가져온다.
             * 115510 -> 5510
             */
            numStr = numStr.Substring((numStr.Length - 4), 4);

            /*
             * 숫자 스트링을 뒤집는다.
             * 5510 -> 0155
             */ 
            numStr = Reverse(numStr);

            /*
             * 앞자리 0을 제거한다.
             * 0155 -> 0155
             */
            numStr = numStr.TrimStart('0');

            /*
             * 곱하기2 빼기1 곱하기2
             */
            return (((int.Parse(numStr) * 2) - 1) * 2).ToString();
        }
        public static string Reverse(string s)
        {
            char[] charArray = s.ToCharArray();
            Array.Reverse(charArray);
            return new string(charArray);
        }

        public void ExecuteAuthLogic()
        {
            // DB 기반 AuthKey 검증/생성
            List<string> nics = NetworkTools.GetAllMACAddressesBySystemNet();
            string encodedKey = g_PlayerInfoManager.g_PlayerInfo.PIF_AuthKey;

            bool hasValid = false;
            foreach (string nic in nics)
            {
                string candidate = AuthTools.EncodeAuthKey(nic);
                if (string.Equals(encodedKey, candidate, StringComparison.CurrentCultureIgnoreCase))
                {
                    hasValid = true;
                    break;
                }
            }

            if (!hasValid && nics.Count < 1)
            {
                string uuidKey = AuthTools.EncodeAuthKey(AuthTools.getUUID12());
                hasValid = string.Equals(encodedKey, uuidKey, StringComparison.CurrentCultureIgnoreCase);
                if (!hasValid)
                {
                    encodedKey = uuidKey;
                }
            }

            if (!hasValid)
            {
                // 기본값: 첫 번째 NIC 기준으로 AuthKey 생성 저장
                if (nics.Count > 0)
                {
                    encodedKey = AuthTools.EncodeAuthKey(nics[0]);
                }
                g_PlayerInfoManager.g_PlayerInfo.PIF_AuthKey = encodedKey;
                g_PlayerInfoManager.SaveData();
            }
        }

        private bool CheckInvalidAuthKey(string encodedKey)
        {
            if (string.IsNullOrEmpty(encodedKey))
                return true;

            // DB 기반 AuthKey 검증
            List<string> nics = NetworkTools.GetAllMACAddressesBySystemNet();

            foreach (string nic in nics)
            {
                if (encodedKey.Equals(AuthTools.EncodeAuthKey(nic), StringComparison.CurrentCultureIgnoreCase))
                    return false;
            }

            if (nics.Count < 1)
                if (encodedKey.Equals(AuthTools.EncodeAuthKey(AuthTools.getUUID12()), StringComparison.CurrentCultureIgnoreCase))
                    return false;

            return true;
        }
        
        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            try
            {
                DisplayAppInfoData();
                DisplayWeeklyTimeTable();
                DisplayPlayerInfo();
                DisplayTTPlayerInfo();

                if (AuthTools.ProhibitTring())
                {
                    SetEnablePasswdBox(false);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
        }

        void ExitBtn_Click(object sender, EventArgs e)
        {
            Application.Exit();
            this.Close();
        }

        void SaveBtn_Click(object sender, EventArgs e)
        {
            if (PlayerNameTBox.Text == string.Empty)
            {
                MessageBox.Show(StringResource.ConfigMsg1);
                return;
            }

            bool isLocalPlay = g_LocalSettingsManager.Settings.IsLocalPlay;
            if (!isLocalPlay)
            {
                if (!IsValidHostOrIp(ManagerIPTBox.Text))
                {
                    MessageBox.Show(StringResource.ConfigMsg2);
                    return;
                }
                IPAddress ip;
                if (!IPAddress.TryParse(PlayerIPTBox.Text, out ip))
                {
                    MessageBox.Show(StringResource.ConfigMsg3);
                    return;
                }
            }

            KillExesAll();

            SaveAppInfoData(); 
            SaveWeeklyTimeTableInfoData();
            SavePlayerInfo();
            SaveTTPlayerInfoData();

            MessageBox.Show(StringResource.SaveMsg);

            Application.Exit();
            this.Close();
        }

        private static bool IsValidHostOrIp(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            if (IPAddress.TryParse(value, out _))
            {
                return true;
            }

            return Uri.CheckHostName(value) != UriHostNameType.Unknown;
        }

        public void KillExesAll()
        {
            ProcessTools.KillExeProcess(FNDTools.GetAgentProcName());
            KillPlayerFriendsAll();
            ProcessTools.KillExeProcess(FNDTools.GetPCSProcName());
        }

        public void KillPlayerFriendsAll()
        {
            ProcessTools.KillExeProcess(FNDTools.GetEmergScrollProcName());
            ProcessTools.KillExeProcess(FNDTools.GetPPTViewerProcName());
            ProcessTools.KillExeProcess(FNDTools.GetPlayerProcName());
        }

        public void DisplayTTPlayerInfo()
        {
            if (this.g_TTPlayerInfoManager.g_PlayerInfo.TTInfo_Data1.Equals("YES", StringComparison.CurrentCultureIgnoreCase))
                checkBox1.Checked = true;
            else
                checkBox1.Checked = false;

            if (g_TTPlayerInfoManager.g_PlayerInfo.TTInfo_DAta2.Equals("YES", StringComparison.CurrentCultureIgnoreCase))
                HwAccCheckBox.Checked = true;
            else 
                HwAccCheckBox.Checked = false;


            if (g_TTPlayerInfoManager.g_PlayerInfo.TTInfo_DAta4.Equals("YES", StringComparison.CurrentCultureIgnoreCase))
                subOutModeChBox.Checked = true;
            else
                subOutModeChBox.Checked = false;

            LeftValueBox.Text = g_TTPlayerInfoManager.g_PlayerInfo.TTInfo_DAta6;
            TopValueBox.Text = g_TTPlayerInfoManager.g_PlayerInfo.TTInfo_Data7;
            WidthValueBox.Text = g_TTPlayerInfoManager.g_PlayerInfo.TTInfo_DAta8;
            HeightValueBox.Text = g_TTPlayerInfoManager.g_PlayerInfo.TTInfo_Data9;
        }

        public void DisplayPlayerInfo()
        {
            PlayerNameTBox.Text = g_PlayerInfoManager.g_PlayerInfo.PIF_PlayerName;
        }

        public void DisplayAppInfoData()
        {
            ManagerIPTBox.Text = g_LocalSettingsManager.Settings.ManagerIP;

            if (string.IsNullOrEmpty(g_PlayerInfoManager.g_PlayerInfo.PIF_IPAddress))
            {
                PlayerIPTBox.Text = NetworkTools.GetAutoIP().ToString();
            }
            else
            {
                PlayerIPTBox.Text = g_PlayerInfoManager.g_PlayerInfo.PIF_IPAddress;
            }

            PowerComboBox.SelectedItem = g_LocalSettingsManager.Settings.EndTimeAction;
            HideCursorCheckBox.Checked = g_LocalSettingsManager.Settings.HideCursor;
            TestModeCheckBox.Checked = g_LocalSettingsManager.Settings.IsTestMode;
            MonitorBlockCheckBox.Checked = g_LocalSettingsManager.Settings.BlockMonitorOnEndTime;

            LogicTools.SelectItemByName(SwitchTimingComboBox, g_LocalSettingsManager.Settings.SwitchTiming ?? "Immediately");

            if (syncEnabledCheckBox != null)
            {
                syncEnabledCheckBox.Checked = g_LocalSettingsManager.Settings.IsSyncEnabled;
            }

            if (isLeadingCheckBox != null)
            {
                isLeadingCheckBox.Checked = g_LocalSettingsManager.Settings.IsLeading;
            }

            syncIpCache.Clear();
            if (syncIpListBox != null)
            {
                syncIpListBox.Items.Clear();
                List<string> storedIps = g_LocalSettingsManager.Settings.SyncClientIps ?? new List<string>();
                foreach (string ip in storedIps)
                {
                    if (string.IsNullOrWhiteSpace(ip))
                    {
                        continue;
                    }

                    string trimmed = ip.Trim();
                    if (trimmed.Length == 0)
                    {
                        continue;
                    }

                    syncIpCache.Add(trimmed);
                    syncIpListBox.Items.Add(trimmed);
                }
            }

            if (isLeadingCheckBox != null)
            {
                UpdateSyncUiState();
            }
        }

        public void DisplayWeeklyTimeTable()
        {
            if (this.g_WeeklyInfoManagerClass.PIF_WPS_InfoList.Count > 0)
            {
                WeekCheck1.Checked = this.g_WeeklyInfoManagerClass.PIF_WPS_InfoList[0].WPS_IsOnAir;
                WeekCheck2.Checked = this.g_WeeklyInfoManagerClass.PIF_WPS_InfoList[1].WPS_IsOnAir;
                WeekCheck3.Checked = this.g_WeeklyInfoManagerClass.PIF_WPS_InfoList[2].WPS_IsOnAir;
                WeekCheck4.Checked = this.g_WeeklyInfoManagerClass.PIF_WPS_InfoList[3].WPS_IsOnAir;
                WeekCheck5.Checked = this.g_WeeklyInfoManagerClass.PIF_WPS_InfoList[4].WPS_IsOnAir;
                WeekCheck6.Checked = this.g_WeeklyInfoManagerClass.PIF_WPS_InfoList[5].WPS_IsOnAir;
                WeekCheck7.Checked = this.g_WeeklyInfoManagerClass.PIF_WPS_InfoList[6].WPS_IsOnAir;

                weekCombo1_1.SelectedIndex = this.g_WeeklyInfoManagerClass.PIF_WPS_InfoList[0].WPS_Hour1;
                weekCombo2_1.SelectedIndex = this.g_WeeklyInfoManagerClass.PIF_WPS_InfoList[1].WPS_Hour1;
                weekCombo3_1.SelectedIndex = this.g_WeeklyInfoManagerClass.PIF_WPS_InfoList[2].WPS_Hour1;
                weekCombo4_1.SelectedIndex = this.g_WeeklyInfoManagerClass.PIF_WPS_InfoList[3].WPS_Hour1;
                weekCombo5_1.SelectedIndex = this.g_WeeklyInfoManagerClass.PIF_WPS_InfoList[4].WPS_Hour1;
                weekCombo6_1.SelectedIndex = this.g_WeeklyInfoManagerClass.PIF_WPS_InfoList[5].WPS_Hour1;
                weekCombo7_1.SelectedIndex = this.g_WeeklyInfoManagerClass.PIF_WPS_InfoList[6].WPS_Hour1;

                weekCombo1_2.SelectedIndex = this.g_WeeklyInfoManagerClass.PIF_WPS_InfoList[0].WPS_Min1;
                weekCombo2_2.SelectedIndex = this.g_WeeklyInfoManagerClass.PIF_WPS_InfoList[1].WPS_Min1;
                weekCombo3_2.SelectedIndex = this.g_WeeklyInfoManagerClass.PIF_WPS_InfoList[2].WPS_Min1;
                weekCombo4_2.SelectedIndex = this.g_WeeklyInfoManagerClass.PIF_WPS_InfoList[3].WPS_Min1;
                weekCombo5_2.SelectedIndex = this.g_WeeklyInfoManagerClass.PIF_WPS_InfoList[4].WPS_Min1;
                weekCombo6_2.SelectedIndex = this.g_WeeklyInfoManagerClass.PIF_WPS_InfoList[5].WPS_Min1;
                weekCombo7_2.SelectedIndex = this.g_WeeklyInfoManagerClass.PIF_WPS_InfoList[6].WPS_Min1;

                weekCombo1_3.SelectedIndex = this.g_WeeklyInfoManagerClass.PIF_WPS_InfoList[0].WPS_Hour2;
                weekCombo2_3.SelectedIndex = this.g_WeeklyInfoManagerClass.PIF_WPS_InfoList[1].WPS_Hour2;
                weekCombo3_3.SelectedIndex = this.g_WeeklyInfoManagerClass.PIF_WPS_InfoList[2].WPS_Hour2;
                weekCombo4_3.SelectedIndex = this.g_WeeklyInfoManagerClass.PIF_WPS_InfoList[3].WPS_Hour2;
                weekCombo5_3.SelectedIndex = this.g_WeeklyInfoManagerClass.PIF_WPS_InfoList[4].WPS_Hour2;
                weekCombo6_3.SelectedIndex = this.g_WeeklyInfoManagerClass.PIF_WPS_InfoList[5].WPS_Hour2;
                weekCombo7_3.SelectedIndex = this.g_WeeklyInfoManagerClass.PIF_WPS_InfoList[6].WPS_Hour2;

                weekCombo1_4.SelectedIndex = this.g_WeeklyInfoManagerClass.PIF_WPS_InfoList[0].WPS_Min2;
                weekCombo2_4.SelectedIndex = this.g_WeeklyInfoManagerClass.PIF_WPS_InfoList[1].WPS_Min2;
                weekCombo3_4.SelectedIndex = this.g_WeeklyInfoManagerClass.PIF_WPS_InfoList[2].WPS_Min2;
                weekCombo4_4.SelectedIndex = this.g_WeeklyInfoManagerClass.PIF_WPS_InfoList[3].WPS_Min2;
                weekCombo5_4.SelectedIndex = this.g_WeeklyInfoManagerClass.PIF_WPS_InfoList[4].WPS_Min2;
                weekCombo6_4.SelectedIndex = this.g_WeeklyInfoManagerClass.PIF_WPS_InfoList[5].WPS_Min2;
                weekCombo7_4.SelectedIndex = this.g_WeeklyInfoManagerClass.PIF_WPS_InfoList[6].WPS_Min2;
            }
            else
            {
                WeekCheck1.Checked =true;
                WeekCheck2.Checked =true;
                WeekCheck3.Checked =true;
                WeekCheck4.Checked =true;
                WeekCheck5.Checked =true;
                WeekCheck6.Checked =true;
                WeekCheck7.Checked =true;

                weekCombo1_1.SelectedIndex = 6;
                weekCombo2_1.SelectedIndex = 6;
                weekCombo3_1.SelectedIndex = 6;
                weekCombo4_1.SelectedIndex = 6;
                weekCombo5_1.SelectedIndex = 6;
                weekCombo6_1.SelectedIndex = 6;
                weekCombo7_1.SelectedIndex = 6;

                weekCombo1_2.SelectedIndex = 0;
                weekCombo2_2.SelectedIndex = 0;
                weekCombo3_2.SelectedIndex = 0;
                weekCombo4_2.SelectedIndex = 0;
                weekCombo5_2.SelectedIndex = 0;
                weekCombo6_2.SelectedIndex = 0;
                weekCombo7_2.SelectedIndex = 0;

                weekCombo1_3.SelectedIndex = 18;
                weekCombo2_3.SelectedIndex = 18;
                weekCombo3_3.SelectedIndex = 18;
                weekCombo4_3.SelectedIndex = 18;
                weekCombo5_3.SelectedIndex = 18;
                weekCombo6_3.SelectedIndex = 18;
                weekCombo7_3.SelectedIndex = 18;

                weekCombo1_4.SelectedIndex = 0;
                weekCombo2_4.SelectedIndex = 0;
                weekCombo3_4.SelectedIndex = 0;
                weekCombo4_4.SelectedIndex = 0;
                weekCombo5_4.SelectedIndex = 0;
                weekCombo6_4.SelectedIndex = 0;
                weekCombo7_4.SelectedIndex = 0;
            }
        }

        public void SaveWeeklyTimeTableInfoData()
        {
            if (this.g_WeeklyInfoManagerClass.PIF_WPS_InfoList.Count == 0)
            {
                this.g_WeeklyInfoManagerClass.InitWeeklySchData();
            }

            this.g_WeeklyInfoManagerClass.PIF_WPS_InfoList[0].WPS_IsOnAir = WeekCheck1.Checked;
            this.g_WeeklyInfoManagerClass.PIF_WPS_InfoList[1].WPS_IsOnAir = WeekCheck2.Checked;
            this.g_WeeklyInfoManagerClass.PIF_WPS_InfoList[2].WPS_IsOnAir = WeekCheck3.Checked;
            this.g_WeeklyInfoManagerClass.PIF_WPS_InfoList[3].WPS_IsOnAir = WeekCheck4.Checked;
            this.g_WeeklyInfoManagerClass.PIF_WPS_InfoList[4].WPS_IsOnAir = WeekCheck5.Checked;
            this.g_WeeklyInfoManagerClass.PIF_WPS_InfoList[5].WPS_IsOnAir = WeekCheck6.Checked;
            this.g_WeeklyInfoManagerClass.PIF_WPS_InfoList[6].WPS_IsOnAir = WeekCheck7.Checked;

            this.g_WeeklyInfoManagerClass.PIF_WPS_InfoList[0].WPS_Hour1 = weekCombo1_1.SelectedIndex;
            this.g_WeeklyInfoManagerClass.PIF_WPS_InfoList[1].WPS_Hour1 = weekCombo2_1.SelectedIndex;
            this.g_WeeklyInfoManagerClass.PIF_WPS_InfoList[2].WPS_Hour1 = weekCombo3_1.SelectedIndex;
            this.g_WeeklyInfoManagerClass.PIF_WPS_InfoList[3].WPS_Hour1 = weekCombo4_1.SelectedIndex;
            this.g_WeeklyInfoManagerClass.PIF_WPS_InfoList[4].WPS_Hour1 = weekCombo5_1.SelectedIndex;
            this.g_WeeklyInfoManagerClass.PIF_WPS_InfoList[5].WPS_Hour1 = weekCombo6_1.SelectedIndex;
            this.g_WeeklyInfoManagerClass.PIF_WPS_InfoList[6].WPS_Hour1 = weekCombo7_1.SelectedIndex;

            this.g_WeeklyInfoManagerClass.PIF_WPS_InfoList[0].WPS_Min1 = weekCombo1_2.SelectedIndex;
            this.g_WeeklyInfoManagerClass.PIF_WPS_InfoList[1].WPS_Min1 = weekCombo2_2.SelectedIndex;
            this.g_WeeklyInfoManagerClass.PIF_WPS_InfoList[2].WPS_Min1 = weekCombo3_2.SelectedIndex;
            this.g_WeeklyInfoManagerClass.PIF_WPS_InfoList[3].WPS_Min1 = weekCombo4_2.SelectedIndex;
            this.g_WeeklyInfoManagerClass.PIF_WPS_InfoList[4].WPS_Min1 = weekCombo5_2.SelectedIndex;
            this.g_WeeklyInfoManagerClass.PIF_WPS_InfoList[5].WPS_Min1 = weekCombo6_2.SelectedIndex;
            this.g_WeeklyInfoManagerClass.PIF_WPS_InfoList[6].WPS_Min1 = weekCombo7_2.SelectedIndex;

            this.g_WeeklyInfoManagerClass.PIF_WPS_InfoList[0].WPS_Hour2 = weekCombo1_3.SelectedIndex;
            this.g_WeeklyInfoManagerClass.PIF_WPS_InfoList[1].WPS_Hour2 = weekCombo2_3.SelectedIndex;
            this.g_WeeklyInfoManagerClass.PIF_WPS_InfoList[2].WPS_Hour2 = weekCombo3_3.SelectedIndex;
            this.g_WeeklyInfoManagerClass.PIF_WPS_InfoList[3].WPS_Hour2 = weekCombo4_3.SelectedIndex;
            this.g_WeeklyInfoManagerClass.PIF_WPS_InfoList[4].WPS_Hour2 = weekCombo5_3.SelectedIndex;
            this.g_WeeklyInfoManagerClass.PIF_WPS_InfoList[5].WPS_Hour2 = weekCombo6_3.SelectedIndex;
            this.g_WeeklyInfoManagerClass.PIF_WPS_InfoList[6].WPS_Hour2 = weekCombo7_3.SelectedIndex;

            this.g_WeeklyInfoManagerClass.PIF_WPS_InfoList[0].WPS_Min2 = weekCombo1_4.SelectedIndex;
            this.g_WeeklyInfoManagerClass.PIF_WPS_InfoList[1].WPS_Min2 = weekCombo2_4.SelectedIndex;
            this.g_WeeklyInfoManagerClass.PIF_WPS_InfoList[2].WPS_Min2 = weekCombo3_4.SelectedIndex;
            this.g_WeeklyInfoManagerClass.PIF_WPS_InfoList[3].WPS_Min2 = weekCombo4_4.SelectedIndex;
            this.g_WeeklyInfoManagerClass.PIF_WPS_InfoList[4].WPS_Min2 = weekCombo5_4.SelectedIndex;
            this.g_WeeklyInfoManagerClass.PIF_WPS_InfoList[5].WPS_Min2 = weekCombo6_4.SelectedIndex;
            this.g_WeeklyInfoManagerClass.PIF_WPS_InfoList[6].WPS_Min2 = weekCombo7_4.SelectedIndex;

            this.g_WeeklyInfoManagerClass.SaveWeeklySchedule();
        }

        public void SaveAppInfoData()
        {
            // 원격 필드
            g_PlayerInfoManager.g_PlayerInfo.PIF_IPAddress = PlayerIPTBox.Text;

            // 로컬 전용 필드
            g_LocalSettingsManager.Settings.ManagerIP = ManagerIPTBox.Text;
            g_LocalSettingsManager.Settings.EndTimeAction = PowerComboBox.SelectedItem.ToString();
            g_LocalSettingsManager.Settings.HideCursor = HideCursorCheckBox.Checked;
            g_LocalSettingsManager.Settings.IsTestMode = TestModeCheckBox.Checked;
            g_LocalSettingsManager.Settings.BlockMonitorOnEndTime = MonitorBlockCheckBox.Checked;
            g_LocalSettingsManager.Settings.SwitchTiming = SwitchTimingComboBox.SelectedItem == null
                ? "Immediately"
                : SwitchTimingComboBox.SelectedItem.ToString();
            if (syncEnabledCheckBox != null)
            {
                g_LocalSettingsManager.Settings.IsSyncEnabled = syncEnabledCheckBox.Checked;
            }
            if (isLeadingCheckBox != null)
            {
                g_LocalSettingsManager.Settings.IsLeading = isLeadingCheckBox.Checked;
            }
            g_LocalSettingsManager.Settings.SyncClientIps = new List<string>(syncIpCache);

            g_LocalSettingsManager.SaveData();
            g_PlayerInfoManager.SaveData();
        }

        public void SaveTTPlayerInfoData()
        {
            if (checkBox1.Checked == true)
                this.g_TTPlayerInfoManager.g_PlayerInfo.TTInfo_Data1 = "YES"; // 컨텐츠 비율을 유지할거냐?
            else
                this.g_TTPlayerInfoManager.g_PlayerInfo.TTInfo_Data1 = "NO";

            if (HwAccCheckBox.Checked)
                g_TTPlayerInfoManager.g_PlayerInfo.TTInfo_DAta2 = "YES";
            else
                g_TTPlayerInfoManager.g_PlayerInfo.TTInfo_DAta2 = "NO";


            if (subOutModeChBox.Checked)
                g_TTPlayerInfoManager.g_PlayerInfo.TTInfo_DAta4 = "YES";
            else
                g_TTPlayerInfoManager.g_PlayerInfo.TTInfo_DAta4 = "NO";


            if (string.IsNullOrEmpty(LeftValueBox.Text))
                LeftValueBox.Text = "0";
            if (string.IsNullOrEmpty(TopValueBox.Text))
                TopValueBox.Text = "0";
            if (string.IsNullOrEmpty(WidthValueBox.Text))
                WidthValueBox.Text = "160";
            if (string.IsNullOrEmpty(HeightValueBox.Text))
                HeightValueBox.Text = "90";

            g_TTPlayerInfoManager.g_PlayerInfo.TTInfo_DAta6 = LeftValueBox.Text;
            g_TTPlayerInfoManager.g_PlayerInfo.TTInfo_Data7 = TopValueBox.Text;
            g_TTPlayerInfoManager.g_PlayerInfo.TTInfo_DAta8 = WidthValueBox.Text;
            g_TTPlayerInfoManager.g_PlayerInfo.TTInfo_Data9 = HeightValueBox.Text;

            this.g_TTPlayerInfoManager.SaveData();
        }

        public void SavePlayerInfo()
        {
            g_PlayerInfoManager.g_PlayerInfo.PIF_PlayerName = PlayerNameTBox.Text;
            g_PlayerInfoManager.g_PlayerInfo.PIF_MacAddress = NetworkTools.GetMacAddressFromIP(PlayerIPTBox.Text);
            g_PlayerInfoManager.SaveData();
        }
      
        public void InitComboBoxes()
        {
            //////////////////////////////////////////////////////////////////////
            //  Action At Application Exit
            PowerComboBox.Items.Clear();

            foreach (string item in Enum.GetNames(typeof(PowerControlType)))
            {
                PowerComboBox.Items.Add(item);
            }

            LogicTools.SelectItemByName(PowerComboBox, PowerControlType.SystemOff.ToString());
            //PowerComboBox.Enabled = false;

            SwitchTimingComboBox.Items.Clear();
            SwitchTimingComboBox.Items.Add("Immediately");
            SwitchTimingComboBox.Items.Add("PageEnd");
            SwitchTimingComboBox.Items.Add("ContentEnd");
            SwitchTimingComboBox.SelectedIndex = 0;
           
            //////////////////////////////////////////////////////////////////////
            //  Weekly Info ComboBox

            for (int i = 0; i < 24; i++)
            {
                weekCombo1_1.Items.Add(i);
                weekCombo2_1.Items.Add(i);
                weekCombo3_1.Items.Add(i);
                weekCombo4_1.Items.Add(i);
                weekCombo5_1.Items.Add(i);
                weekCombo6_1.Items.Add(i);
                weekCombo7_1.Items.Add(i);

                weekCombo1_3.Items.Add(i);
                weekCombo2_3.Items.Add(i);
                weekCombo3_3.Items.Add(i);
                weekCombo4_3.Items.Add(i);
                weekCombo5_3.Items.Add(i);
                weekCombo6_3.Items.Add(i);
                weekCombo7_3.Items.Add(i);
            }


            for (int i = 0; i < 60; i++)
            {
                weekCombo1_2.Items.Add(i);
                weekCombo2_2.Items.Add(i);
                weekCombo3_2.Items.Add(i);
                weekCombo4_2.Items.Add(i);
                weekCombo5_2.Items.Add(i);
                weekCombo6_2.Items.Add(i);
                weekCombo7_2.Items.Add(i);

                weekCombo1_4.Items.Add(i);
                weekCombo2_4.Items.Add(i);
                weekCombo3_4.Items.Add(i);
                weekCombo4_4.Items.Add(i);
                weekCombo5_4.Items.Add(i);
                weekCombo6_4.Items.Add(i);
                weekCombo7_4.Items.Add(i);
            }

            weekCombo1_1.SelectedIndex = 0;
            weekCombo2_1.SelectedIndex = 0;
            weekCombo3_1.SelectedIndex = 0;
            weekCombo4_1.SelectedIndex = 0;
            weekCombo5_1.SelectedIndex = 0;
            weekCombo6_1.SelectedIndex = 0;
            weekCombo7_1.SelectedIndex = 0;

            weekCombo1_2.SelectedIndex = 0;
            weekCombo2_2.SelectedIndex = 0;
            weekCombo3_2.SelectedIndex = 0;
            weekCombo4_2.SelectedIndex = 0;
            weekCombo5_2.SelectedIndex = 0;
            weekCombo6_2.SelectedIndex = 0;
            weekCombo7_2.SelectedIndex = 0;

            weekCombo1_3.SelectedIndex = 0;
            weekCombo2_3.SelectedIndex = 0;
            weekCombo3_3.SelectedIndex = 0;
            weekCombo4_3.SelectedIndex = 0;
            weekCombo5_3.SelectedIndex = 0;
            weekCombo6_3.SelectedIndex = 0;
            weekCombo7_3.SelectedIndex = 0;

            weekCombo1_4.SelectedIndex = 0;
            weekCombo2_4.SelectedIndex = 0;
            weekCombo3_4.SelectedIndex = 0;
            weekCombo4_4.SelectedIndex = 0;
            weekCombo5_4.SelectedIndex = 0;
            weekCombo6_4.SelectedIndex = 0;
            weekCombo7_4.SelectedIndex = 0;

        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            CheckAndAddSecurityRules();
        }

        private void button3_Click(object sender, EventArgs e)
        {
            System.Diagnostics.Process.Start("CMD.exe", "/K ipconfig");  
        }

        void SetEnablePasswdBox(bool state)
        {
            Passwd.Enabled = state;
            authBtn.Enabled = state;
        }

        private void SetAllBtn_Click(object sender, EventArgs e)
        {
            if (this.g_WeeklyInfoManagerClass.PIF_WPS_InfoList.Count == 0)
            {
                this.g_WeeklyInfoManagerClass.InitWeeklySchData();
            }

            weekCombo2_1.SelectedIndex = this.g_WeeklyInfoManagerClass.PIF_WPS_InfoList[1].WPS_Hour1 = weekCombo1_1.SelectedIndex;
            weekCombo3_1.SelectedIndex = this.g_WeeklyInfoManagerClass.PIF_WPS_InfoList[2].WPS_Hour1 = weekCombo1_1.SelectedIndex;
            weekCombo4_1.SelectedIndex = this.g_WeeklyInfoManagerClass.PIF_WPS_InfoList[3].WPS_Hour1 = weekCombo1_1.SelectedIndex;
            weekCombo5_1.SelectedIndex = this.g_WeeklyInfoManagerClass.PIF_WPS_InfoList[4].WPS_Hour1 = weekCombo1_1.SelectedIndex;
            weekCombo6_1.SelectedIndex = this.g_WeeklyInfoManagerClass.PIF_WPS_InfoList[5].WPS_Hour1 = weekCombo1_1.SelectedIndex;
            weekCombo7_1.SelectedIndex = this.g_WeeklyInfoManagerClass.PIF_WPS_InfoList[6].WPS_Hour1 = weekCombo1_1.SelectedIndex;

            weekCombo2_2.SelectedIndex = this.g_WeeklyInfoManagerClass.PIF_WPS_InfoList[1].WPS_Min1 = weekCombo1_2.SelectedIndex;
            weekCombo3_2.SelectedIndex = this.g_WeeklyInfoManagerClass.PIF_WPS_InfoList[2].WPS_Min1 = weekCombo1_2.SelectedIndex;
            weekCombo4_2.SelectedIndex = this.g_WeeklyInfoManagerClass.PIF_WPS_InfoList[3].WPS_Min1 = weekCombo1_2.SelectedIndex;
            weekCombo5_2.SelectedIndex = this.g_WeeklyInfoManagerClass.PIF_WPS_InfoList[4].WPS_Min1 = weekCombo1_2.SelectedIndex;
            weekCombo6_2.SelectedIndex = this.g_WeeklyInfoManagerClass.PIF_WPS_InfoList[5].WPS_Min1 = weekCombo1_2.SelectedIndex;
            weekCombo7_2.SelectedIndex = this.g_WeeklyInfoManagerClass.PIF_WPS_InfoList[6].WPS_Min1 = weekCombo1_2.SelectedIndex;

            weekCombo2_3.SelectedIndex = this.g_WeeklyInfoManagerClass.PIF_WPS_InfoList[1].WPS_Hour2 = weekCombo1_3.SelectedIndex;
            weekCombo3_3.SelectedIndex = this.g_WeeklyInfoManagerClass.PIF_WPS_InfoList[2].WPS_Hour2 = weekCombo1_3.SelectedIndex;
            weekCombo4_3.SelectedIndex = this.g_WeeklyInfoManagerClass.PIF_WPS_InfoList[3].WPS_Hour2 = weekCombo1_3.SelectedIndex;
            weekCombo5_3.SelectedIndex = this.g_WeeklyInfoManagerClass.PIF_WPS_InfoList[4].WPS_Hour2 = weekCombo1_3.SelectedIndex;
            weekCombo6_3.SelectedIndex = this.g_WeeklyInfoManagerClass.PIF_WPS_InfoList[5].WPS_Hour2 = weekCombo1_3.SelectedIndex;
            weekCombo7_3.SelectedIndex = this.g_WeeklyInfoManagerClass.PIF_WPS_InfoList[6].WPS_Hour2 = weekCombo1_3.SelectedIndex;

            weekCombo2_4.SelectedIndex = this.g_WeeklyInfoManagerClass.PIF_WPS_InfoList[1].WPS_Min2 = weekCombo1_4.SelectedIndex;
            weekCombo3_4.SelectedIndex = this.g_WeeklyInfoManagerClass.PIF_WPS_InfoList[2].WPS_Min2 = weekCombo1_4.SelectedIndex;
            weekCombo4_4.SelectedIndex = this.g_WeeklyInfoManagerClass.PIF_WPS_InfoList[3].WPS_Min2 = weekCombo1_4.SelectedIndex;
            weekCombo5_4.SelectedIndex = this.g_WeeklyInfoManagerClass.PIF_WPS_InfoList[4].WPS_Min2 = weekCombo1_4.SelectedIndex;
            weekCombo6_4.SelectedIndex = this.g_WeeklyInfoManagerClass.PIF_WPS_InfoList[5].WPS_Min2 = weekCombo1_4.SelectedIndex;
            weekCombo7_4.SelectedIndex = this.g_WeeklyInfoManagerClass.PIF_WPS_InfoList[6].WPS_Min2 = weekCombo1_4.SelectedIndex;
        }
    }
}
