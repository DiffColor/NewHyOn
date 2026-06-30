using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using TurtleTools;

namespace AndoW_Manager
{
    /// <summary>
    /// NotifyWindow.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class EditContentsListWindow : Window
    {
        ContentsInfoClass g_CurSelectedInfo = new ContentsInfoClass();
        List<ContentsInfoClass> g_ContentsInfoClassList = new List<ContentsInfoClass>();
        public EditContentsListWindow(List<ContentsInfoClass> paramList)
        {
            InitializeComponent();
            InitEventHandler();
            
            g_ContentsInfoClassList.Clear();
            if (paramList.Count > 0)
            {
                foreach (ContentsInfoClass item in paramList)
                {
                    ContentsInfoClass tmpInfo = new ContentsInfoClass();
                    tmpInfo.CopyData(item);
                    g_ContentsInfoClassList.Add(tmpInfo);                 
                }
            }
            RefreshContentsList();
            RefreshPosComboBox();
            ResetTimeComboSelection();
        }

        public void RefreshPosComboBox()
        {
            scrollSpeedComboBox_Copy2.Items.Clear();

            int idx = 1;
            foreach (ContentsInfoClass item in g_ContentsInfoClassList)
            {
                scrollSpeedComboBox_Copy2.Items.Add(idx);
                idx++;
            }

            scrollSpeedComboBox_Copy2.SelectedIndex = 0;
        }

        public void DeleteContentsInfo(string guidStr)
        {
           
            int idx = 0;
            foreach (ContentsInfoClass item in g_ContentsInfoClassList)
            {
                if (item.CIF_StrGUID == guidStr)
                {
                    break;
                }
                idx++;
            }

            if (g_ContentsInfoClassList.Count > idx)
            {
                g_ContentsInfoClassList.RemoveAt(idx);
                RefreshContentsList();
            }
        }

        private void ResetTimeComboSelection()
        {
            scrollSpeedComboBox_Copy1.SelectedIndex = 0;
            scrollSpeedComboBox_Copy1.Text = "00";
            scrollSpeedComboBox_Copy.SelectedIndex = 0;
            BTNResetVideoLength.Visibility = Visibility.Collapsed;
        }

        public void InitEventHandler()
        {
            BTN0DO_Copy4.Click += new RoutedEventHandler(BTNPagesListNew1_Click);  //OK
            BTN0DO_Copy.Click += new RoutedEventHandler(CancelBTN_Click);  //Cancel

            BTN0DO_Copy1.Click += BTN0DO_Copy1_Click;  // Save Selected ContentsData
            BTN0DO_Copy2.Click += BTN0DO_Copy2_Click;  // Goto Shift Selected ContentsInfo
            BTNResetVideoLength.Click += BTNResetVideoLength_Click;  // Reset selected video duration from metadata

            this.Closing += EditContentsListWindow_Closing;
        }

        void EditContentsListWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (MessageTools.ShowMessageBox("변경된 정보를 저장하시겠습니까?", "예", "아니오") == true)
            {
                Page1.Instance.UpdateContentsListByEditWindow(this.g_ContentsInfoClassList);
            }
        }

        void BTN0DO_Copy2_Click(object sender, RoutedEventArgs e)
        {
            if (this.g_CurSelectedInfo.CIF_FileName != string.Empty)
            {
                int idx = 0;
                foreach (ContentsInfoClass item in g_ContentsInfoClassList)
                {
                    if (item.CIF_StrGUID == g_CurSelectedInfo.CIF_StrGUID)
                    {                       
                        break;
                    }
                    idx++;
                }

                if (g_ContentsInfoClassList.Count > idx)
                {
                    ContentsInfoClass tmpCls = new ContentsInfoClass();
                    tmpCls.CopyData(g_CurSelectedInfo);
                    g_ContentsInfoClassList.RemoveAt(idx);
                    g_ContentsInfoClassList.Insert(scrollSpeedComboBox_Copy2.SelectedIndex, tmpCls);
                    RefreshContentsList();
                }
            }
        }

        void BTN0DO_Copy1_Click(object sender, RoutedEventArgs e)
        {
            if (this.g_CurSelectedInfo.CIF_FileName != string.Empty)
            {
                int selectedDurationSeconds = ReadSelectedDurationSeconds();
                ApplyDurationToContent(g_CurSelectedInfo, selectedDurationSeconds);
                
                foreach (ContentsInfoClass item in g_ContentsInfoClassList)
                {
                    if (item.CIF_StrGUID == g_CurSelectedInfo.CIF_StrGUID)
                    {
                        item.CIF_PlayMinute = g_CurSelectedInfo.CIF_PlayMinute;
                        item.CIF_PlaySec = g_CurSelectedInfo.CIF_PlaySec;
                        break;
                    }
                }

                RefreshContentsList();
            }
            else
            {
                MessageTools.ShowMessageBox("컨텐츠를 먼저 선택해주세요.", "확인");
            }
        }

        void BTNResetVideoLength_Click(object sender, RoutedEventArgs e)
        {
            if (this.g_CurSelectedInfo.CIF_FileName == string.Empty)
            {
                MessageTools.ShowMessageBox("컨텐츠를 먼저 선택해주세요.", "확인");
                return;
            }

            if (IsVideoContent(g_CurSelectedInfo) == false)
            {
                MessageTools.ShowMessageBox("영상 컨텐츠만 원본 길이로 초기화할 수 있습니다.", "확인");
                return;
            }

            int metadataDurationSeconds = ResolveVideoMetadataDurationSeconds(g_CurSelectedInfo);
            if (metadataDurationSeconds <= 0)
            {
                MessageTools.ShowMessageBox("영상 메타데이터의 재생시간을 찾지 못했습니다.", "확인");
                return;
            }

            SetTimeComboSelection(metadataDurationSeconds);
            ApplyDurationToContent(g_CurSelectedInfo, metadataDurationSeconds);
        }

        private int ReadSelectedDurationSeconds()
        {
            int minute = ParseDurationPart(scrollSpeedComboBox_Copy1.Text, 0);
            int second = ParseDurationPart(
                scrollSpeedComboBox_Copy.SelectedItem != null
                    ? scrollSpeedComboBox_Copy.SelectedItem.ToString()
                    : scrollSpeedComboBox_Copy.Text,
                0);
            second = Math.Max(0, Math.Min(59, second));
            return Math.Max(1, (minute * 60) + second);
        }

        private static int ParseDurationPart(string value, int fallback)
        {
            int parsed;
            if (int.TryParse(value, out parsed) == false || parsed < 0)
            {
                return fallback;
            }

            return parsed;
        }

        private void SetTimeComboSelection(int totalSeconds)
        {
            int safeSeconds = Math.Max(1, totalSeconds);
            string minute = (safeSeconds / 60).ToString("D2");
            string second = (safeSeconds % 60).ToString("D2");
            scrollSpeedComboBox_Copy1.Text = minute;
            scrollSpeedComboBox_Copy1.SelectedItem = minute;
            scrollSpeedComboBox_Copy.SelectedItem = second;
        }

        private static void ApplyDurationToContent(ContentsInfoClass content, int totalSeconds)
        {
            int safeSeconds = Math.Max(1, totalSeconds);
            content.CIF_PlayMinute = (safeSeconds / 60).ToString("D2");
            content.CIF_PlaySec = (safeSeconds % 60).ToString("D2");
        }

        private static bool IsVideoContent(ContentsInfoClass content)
        {
            if (content == null)
            {
                return false;
            }

            if (string.Equals(content.CIF_ContentType, ContentType.Video.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            string fileName = string.IsNullOrWhiteSpace(content.CIF_FileFullPath)
                ? content.CIF_FileName
                : content.CIF_FileFullPath;
            return string.IsNullOrWhiteSpace(fileName) == false && MediaTools.CheckIsVideoFile(fileName);
        }

        private int ResolveVideoMetadataDurationSeconds(ContentsInfoClass content)
        {
            ContentDetails details = ResolveVideoMetadata(content);
            if (details == null || details.VideoLength <= 0)
            {
                return 0;
            }

            return Math.Max(1, (int)Math.Floor(details.VideoLength));
        }

        private ContentDetails ResolveVideoMetadata(ContentsInfoClass content)
        {
            if (content == null)
            {
                return null;
            }

            try
            {
                ContentDetailsManager manager = new ContentDetailsManager();
                string storageId = Path.GetFileNameWithoutExtension(content.CIF_FileName ?? string.Empty);
                if (string.IsNullOrWhiteSpace(storageId) == false)
                {
                    ContentDetails byId = manager.FindById(storageId);
                    if (byId != null && byId.VideoLength > 0)
                    {
                        return byId;
                    }
                }

                if (string.IsNullOrWhiteSpace(content.CIF_FileHash) == false)
                {
                    ContentDetails byHash = manager.FindByPartialHash(content.CIF_FileHash)
                        .FirstOrDefault(item => item.VideoLength > 0);
                    if (byHash != null)
                    {
                        return byHash;
                    }
                }

                string originalFileName = ResolveOriginalFileName(content);
                if (string.IsNullOrWhiteSpace(originalFileName) == false)
                {
                    ContentDetails byOriginalName = manager.FindByFileName(originalFileName)
                        .FirstOrDefault(item => item.VideoLength > 0);
                    if (byOriginalName != null)
                    {
                        return byOriginalName;
                    }
                }

                string fileName = Path.GetFileName(content.CIF_FileName ?? string.Empty);
                if (string.IsNullOrWhiteSpace(fileName) == false)
                {
                    return manager.FindByFileName(fileName)
                        .FirstOrDefault(item => item.VideoLength > 0);
                }
            }
            catch (Exception ex)
            {
                Logger.WriteErrorLog(ex.ToString(), Logger.GetLogFileName());
            }

            return null;
        }

        private static string ResolveOriginalFileName(ContentsInfoClass content)
        {
            if (content == null)
            {
                return string.Empty;
            }

            if (string.IsNullOrWhiteSpace(content.CIF_FileFullPath) == false)
            {
                try
                {
                    string fileName = Path.GetFileName(content.CIF_FileFullPath.Trim().Trim('"'));
                    if (string.IsNullOrWhiteSpace(fileName) == false)
                    {
                        return fileName;
                    }
                }
                catch
                {
                }
            }

            return content.CIF_DisplayFileName;
        }


        public void RefreshContentsList()
        {
            wrapPanelTemplate.Children.Clear();
            int idx = 1;
            foreach (ContentsInfoClass item in g_ContentsInfoClassList)
            {
                ContentsEditInfoElement tmpElement = new ContentsEditInfoElement(this);
                tmpElement.UpdateDataInfo(item);
                tmpElement.TextBlockOrderingNumber.Text = string.Format("[{0:D3}]", idx);
                tmpElement.Margin = new Thickness(4, 4, 0, 0);
                wrapPanelTemplate.Children.Add(tmpElement);
                idx++;
            }
        }
        
        public void SelectContentsInfo(ContentsInfoClass paramCls)
        {
            g_CurSelectedInfo.CopyData(paramCls);
            DisplaySelectedContentsInfo();
                  
        }

        public void DisplaySelectedContentsInfo()
        {
            if (g_CurSelectedInfo.CIF_FileName != string.Empty)
            {
                TextAngleGrade5_Copy1.Text = g_CurSelectedInfo.CIF_DisplayFileName;

                scrollSpeedComboBox_Copy1.SelectedItem = g_CurSelectedInfo.CIF_PlayMinute;
                scrollSpeedComboBox_Copy1.Text = g_CurSelectedInfo.CIF_PlayMinute;
                scrollSpeedComboBox_Copy.SelectedItem = g_CurSelectedInfo.CIF_PlaySec;
                TextAngleGrade5_Copy4.Text = g_CurSelectedInfo.CIF_ContentType;
                BTNResetVideoLength.Visibility = IsVideoContent(g_CurSelectedInfo) ? Visibility.Visible : Visibility.Collapsed;

                int idx = 0;
                foreach (ContentsInfoClass item in g_ContentsInfoClassList)
                {
                    if (item.CIF_StrGUID == g_CurSelectedInfo.CIF_StrGUID)
                    {
                        break;
                    }
                    idx++;
                }

                if (g_ContentsInfoClassList.Count > idx)
                {
                    scrollSpeedComboBox_Copy2.SelectedIndex = idx;
                }
            }

        }


        void CancelBTN_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }

        void BTNPagesListNew1_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
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
    }

}
