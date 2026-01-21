using Microsoft.Win32;
using Microsoft.WindowsAPICodePack.Dialogs;
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Documents;
using System.Linq;
using TurtleTools;

namespace AndoW_Manager
{
    /// <summary>
    /// SavingFileWindow.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class PlayerBatchEditWindow : Window
    {
        public List<BatchEditPlayerInfo> g_BatchEditPlayerInfoElementList = new List<BatchEditPlayerInfo>();
        public List<PlayerInfoClass> g_PlayerInfoClassList = new List<PlayerInfoClass>();

        public PlayerBatchEditWindow()
        {
            InitializeComponent();
        }

        public void DeletePlayerInfo(PlayerInfoClass paramCls)
        {
            int idx = 0;

            try
            {
                foreach (PlayerInfoClass item in g_PlayerInfoClassList)
                {
                    if (item.PIF_GUID == paramCls.PIF_GUID)
                    {
                        //Service1.Instance.RequestExit(paramCls.PIF_PlayerName);
                        break;
                    }
                    idx++;
                }

                g_PlayerInfoClassList.RemoveAt(idx);

                RefreshPlayerInfoList();
            }
            catch (Exception e)
            {
                int dd = idx;
            }
        }

        public void InitPlayerInfoList(List<PlayerInfoClass> paramList)
        {
            g_PlayerInfoClassList.Clear();
            if (paramList.Count > 0)
            {
                var ordered = paramList
                    .Where(p => p != null)
                    .OrderBy(p => p.PIF_Order <= 0 ? int.MaxValue : p.PIF_Order)
                    .ThenBy(p => p.PIF_PlayerName, StringComparer.CurrentCultureIgnoreCase);

                foreach (PlayerInfoClass item in ordered)
                {
                    PlayerInfoClass tmpCls = new PlayerInfoClass();
                    tmpCls.CopyData(item);
                    g_PlayerInfoClassList.Add(tmpCls);

                }
            }
        }

        void ClearBtn_Click(object sender, RoutedEventArgs e)
        {
            DeleteAllPlayerInfo();
        }

        public void DeleteAllPlayerInfo()
        {
            if (MessageTools.ShowMessageBox("Delete All Players Info. Continue?", "예", "아니오") == true)
            {
                //DisconnectAllPlayers();
                g_PlayerInfoClassList.Clear();
                RefreshPlayerInfoList();
            }
        }

        //void DisconnectAllPlayers()
        //{
        //    //WCFWindow1.Instance.DisconnectAllConnections();
        //}

        public void SavePlayersInformation()
        {
            List<string> pnames = new List<string>();
            foreach(BatchEditPlayerInfo item in g_BatchEditPlayerInfoElementList)
            {
                pnames.Add(item.PlayerNameText.Text);
            }

            bool _wrongname = pnames.Any(n => FileTools.HasWrongPathCharacter(n)) || pnames.Any(n => n.Contains(","));

            if(_wrongname)
            {
                MessageTools.ShowMessageBox("플레이어 이름에 (,) 또는 사용할 수 없는 특수문자가 포함되어있습니다.", "확인");
                return;
            }

            bool _hasDup = pnames.GroupBy(n => n).Any(c => c.Count() > 1);

            if (_hasDup)
            {
                MessageTools.ShowMessageBox("동일한 플레이어 이름이 존재합니다. 다른 이름으로 입력해 주세요.", "확인");
            } else
            {
                List<PlayerInfoClass> pinfos = new List<PlayerInfoClass>();
                int order = 1;

                foreach (BatchEditPlayerInfo item in g_BatchEditPlayerInfoElementList)
                {
                    item.SavePlayerInfo();

                    PlayerInfoClass _pinfo = new PlayerInfoClass();
                    _pinfo.CopyData(item.g_PlayerInfoClass);
                    _pinfo.PIF_Order = order++;
                    _pinfo.PIF_IPAddress = DataShop.Instance.g_PlayerInfoManager.GetPlayerIP(_pinfo.PIF_PlayerName);
                    _pinfo.PIF_MacAddress = DataShop.Instance.g_PlayerInfoManager.GetPlayerMAC(_pinfo.PIF_PlayerName);
                    pinfos.Add(_pinfo);
                }

                DataShop.Instance.g_PlayerInfoManager.UpdatePlayerInfoList(pinfos);
                Page3.Instance.RefreshPlayerInfoList();

                this.Close();
            }
        }

        void BTN0DO_Copy1_Click(object sender, RoutedEventArgs e)
        {
        }

        public void AddNewPlayer()
        {
            PlayerInfoClass tmpNewPlayerInfo = new PlayerInfoClass();
            tmpNewPlayerInfo.PIF_PlayerName = string.Format("Player_{0}", g_BatchEditPlayerInfoElementList.Count + 1);
            tmpNewPlayerInfo.PIF_Order = g_PlayerInfoClassList.Count + 1;
            g_PlayerInfoClassList.Add(tmpNewPlayerInfo);

            BatchEditPlayerInfo tmpElement = new BatchEditPlayerInfo(this);
            tmpElement.UpdateDataInfo(tmpNewPlayerInfo);
            tmpElement.InitPageListComboBoxes(DataShop.Instance.g_PageListInfoManager.g_PageListInfoClassList);
            tmpElement.TextBlockOrderingNumber.Content = string.Format("{0:D2}", g_BatchEditPlayerInfoElementList.Count + 1);
            tmpElement.Margin = new Thickness(5, 2, 5, 0);
            ContentsElementsStackPannel1.Children.Add(tmpElement);

            g_BatchEditPlayerInfoElementList.Add(tmpElement);
        }

        void BTN0DO_Copy10_Click(object sender, RoutedEventArgs e)
        {

        }

        void PlayerBatchEditWindow_Loaded(object sender, RoutedEventArgs e)
        {
            DataShop.Instance.g_PlayerInfoManager.ReloadFromDatabase();
            InitPlayerInfoList(DataShop.Instance.g_PlayerInfoManager.GetOrderedPlayers().ToList());
            RefreshPlayerInfoList();
            MainWindow.Instance?.SetDimOverlay(true);
        }

        public void RefreshPlayerInfoList()
        {
            GC.Collect();
            g_BatchEditPlayerInfoElementList.Clear();
            ContentsElementsStackPannel1.Children.Clear();
            int idx = 1;

            foreach (PlayerInfoClass item in g_PlayerInfoClassList)
            {
                BatchEditPlayerInfo tmpElement = new BatchEditPlayerInfo(this);
                tmpElement.UpdateDataInfo(item);
                tmpElement.InitPageListComboBoxes(DataShop.Instance.g_PageListInfoManager.g_PageListInfoClassList);
                tmpElement.TextBlockOrderingNumber.Content = string.Format("{0:D2}", idx);
                tmpElement.Margin = new Thickness(5, 2, 5, 0);
                ContentsElementsStackPannel1.Children.Add(tmpElement);
                idx++;

                g_BatchEditPlayerInfoElementList.Add(tmpElement);
            }
        }

        public void closeThisWindow()
        {
            this.Close();
        }

        private void BtnWin_close_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            this.Close();
        }

        private void BtnWin_drag_PreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            this.DragMove();
        }

        private void SaveSourceKeyBtn_Click(object sender, RoutedEventArgs e)
        {
            string _data = string.Empty;

            foreach (PlayerInfoClass pic in g_PlayerInfoClassList)
            {
                string normalizedMac = AuthTools.NormalizeMacAddress(pic.PIF_MacAddress);
                if (string.IsNullOrEmpty(normalizedMac))
                    continue;

                if (DataShop.Instance.g_PlayerInfoManager.HasValidAuthKey(pic.PIF_PlayerName))
                    continue;

                _data += normalizedMac + Environment.NewLine;
            }
            
            if (string.IsNullOrEmpty(_data))
                MessageTools.ShowMessageBox("인증할 기기가 없습니다.", "확인");
            else
            {
                CommonOpenFileDialog dialog = new CommonOpenFileDialog();
                dialog.IsFolderPicker = true;
                if (dialog.ShowDialog() == CommonFileDialogResult.OK)
                {
                    FileTools.WriteNewTextFile(Path.Combine(dialog.FileName, "SourceKeys"), _data);
                    MessageTools.ShowMessageBox("소스파일을 저장하였습니다.", "확인");
                }

                this.Focus();
            }
        }

        private void UploadAuthKeyBtn_Click(object sender, RoutedEventArgs e)
        {
            CommonOpenFileDialog dialog = new CommonOpenFileDialog();
            dialog.Filters.Add(new CommonFileDialogFilter("AuthKeys", "*"));

            if (dialog.ShowDialog() == CommonFileDialogResult.OK)
            {
                if(Path.GetFileName(dialog.FileName).Equals("AuthKeys", StringComparison.CurrentCultureIgnoreCase))
                {
                    string[] _authkeys = File.ReadAllLines(dialog.FileName);
                    int applied = DataShop.Instance.g_PlayerInfoManager.ApplyAuthKeys(_authkeys);

                    if (applied > 0)
                    {
                        RefreshPlayerInfoList();
                        MessageTools.ShowMessageBox("기기 인증을 완료하였습니다.", "확인");
                    }
                    else
                    {
                        MessageTools.ShowMessageBox("적용 가능한 인증키가 없습니다.", "확인");
                    }
                } else
                {
                    MessageTools.ShowMessageBox("인증키 파일이 아닙니다.", "확인");
                }
            }

            this.Focus();
        }

        private void AddPlayerBtn_Click(object sender, RoutedEventArgs e)
        {
#if LIMIT
            if (ContentsElementsStackPannel1.Children.Count > 4)
            {
                MessageTools.ShowMessageBox("추가 인증이 필요합니다.", "확인");
                return;
            }
#endif
            AddNewPlayer();
            ContentsListScrollViewer1.ScrollToBottom();
        }

        private void CancelBtn_Click(object sender, RoutedEventArgs e)
        {
            GC.Collect();
            this.Close();
        }

        private void SaveBtn_Click(object sender, RoutedEventArgs e)
        {
            SavePlayersInformation();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            MainWindow.Instance?.SetDimOverlay(false);
        }
    }
}
