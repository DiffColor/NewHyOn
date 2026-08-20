using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Newtonsoft.Json;
using System;
using TurtleTools;
using System.Windows.Shapes;
using System.Collections.Generic;
using System.Linq;

namespace AndoW_Manager
{
    /// <summary>
    /// NotifyWindow.xaml에 대한 상호 작용 논리
    /// </summary>
     public partial class EditOnAirTimeWindow : Window
    {
         public WeeklyInfoManagerClass g_WeeklyInfoManagerClass = new WeeklyInfoManagerClass();
         public PlayerInfoClass g_CurrentSelectedPlayerInfoClass = new PlayerInfoClass();
         private readonly PlayerGroupClass g_CurrentSelectedPlayerGroupClass;

         public EditOnAirTimeWindow(PlayerInfoClass paramCls)
        {
            InitializeComponent();
            g_CurrentSelectedPlayerInfoClass.CopyData(paramCls);
            if (Page3.Instance?.g_CurrentSelectedPlayerGroupClass != null)
            {
                g_CurrentSelectedPlayerGroupClass = new PlayerGroupClass();
                g_CurrentSelectedPlayerGroupClass.CopyData(Page3.Instance.g_CurrentSelectedPlayerGroupClass);
            }
            InitEventHandler();
            InitWeeklyInfoEventHandler();
            UpdateBatchApplyButtonLabel();

            TextAngleGrade1_Copy1.Text = paramCls.PIF_PlayerName;
        }

        public void InitWeeklyInfoEventHandler()
        {
            DayOfWeekRect1.PreviewMouseLeftButtonDown += DayOfWeekRect1_PreviewMouseLeftButtonDown;
            DayOfWeekRect2.PreviewMouseLeftButtonDown += DayOfWeekRect1_PreviewMouseLeftButtonDown;
            DayOfWeekRect3.PreviewMouseLeftButtonDown += DayOfWeekRect1_PreviewMouseLeftButtonDown;
            DayOfWeekRect4.PreviewMouseLeftButtonDown += DayOfWeekRect1_PreviewMouseLeftButtonDown;
            DayOfWeekRect5.PreviewMouseLeftButtonDown += DayOfWeekRect1_PreviewMouseLeftButtonDown;
            DayOfWeekRect6.PreviewMouseLeftButtonDown += DayOfWeekRect1_PreviewMouseLeftButtonDown;
            DayOfWeekRect7.PreviewMouseLeftButtonDown += DayOfWeekRect1_PreviewMouseLeftButtonDown;

            DayOfWeekText1.PreviewMouseLeftButtonDown += DayOfWeekText1_PreviewMouseLeftButtonDown;
            DayOfWeekText2.PreviewMouseLeftButtonDown += DayOfWeekText1_PreviewMouseLeftButtonDown;
            DayOfWeekText3.PreviewMouseLeftButtonDown += DayOfWeekText1_PreviewMouseLeftButtonDown;
            DayOfWeekText4.PreviewMouseLeftButtonDown += DayOfWeekText1_PreviewMouseLeftButtonDown;
            DayOfWeekText5.PreviewMouseLeftButtonDown += DayOfWeekText1_PreviewMouseLeftButtonDown;
            DayOfWeekText6.PreviewMouseLeftButtonDown += DayOfWeekText1_PreviewMouseLeftButtonDown;
            DayOfWeekText7.PreviewMouseLeftButtonDown += DayOfWeekText1_PreviewMouseLeftButtonDown;
        }

         void DayOfWeekText1_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
         {
             if (g_WeeklyInfoManagerClass.PIF_WPS_InfoList.Count == 0)
             {
                 return;
             }

             TextBlock tmpRect = (TextBlock)sender;

             switch (tmpRect.Name)
             {
                 case "DayOfWeekText1":
                     if (g_WeeklyInfoManagerClass.PIF_WPS_InfoList[0].IsOnAir == false)
                         g_WeeklyInfoManagerClass.PIF_WPS_InfoList[0].IsOnAir = true;
                     else
                         g_WeeklyInfoManagerClass.PIF_WPS_InfoList[0].IsOnAir = false;

                     WeekSchInfoElement1.UpdateWeekInfo(g_WeeklyInfoManagerClass.PIF_WPS_InfoList[0]);
                     break;
                 case "DayOfWeekText2":
                     if (g_WeeklyInfoManagerClass.PIF_WPS_InfoList[1].IsOnAir == false)
                         g_WeeklyInfoManagerClass.PIF_WPS_InfoList[1].IsOnAir = true;
                     else
                         g_WeeklyInfoManagerClass.PIF_WPS_InfoList[1].IsOnAir = false;


                     WeekSchInfoElement2.UpdateWeekInfo(g_WeeklyInfoManagerClass.PIF_WPS_InfoList[1]);
                     break;
                 case "DayOfWeekText3":
                     if (g_WeeklyInfoManagerClass.PIF_WPS_InfoList[2].IsOnAir == false)
                         g_WeeklyInfoManagerClass.PIF_WPS_InfoList[2].IsOnAir = true;
                     else
                         g_WeeklyInfoManagerClass.PIF_WPS_InfoList[2].IsOnAir = false;

                     WeekSchInfoElement3.UpdateWeekInfo(g_WeeklyInfoManagerClass.PIF_WPS_InfoList[2]);
                     break;
                 case "DayOfWeekText4":
                     if (g_WeeklyInfoManagerClass.PIF_WPS_InfoList[3].IsOnAir == false)
                         g_WeeklyInfoManagerClass.PIF_WPS_InfoList[3].IsOnAir = true;
                     else
                         g_WeeklyInfoManagerClass.PIF_WPS_InfoList[3].IsOnAir = false;

                     WeekSchInfoElement4.UpdateWeekInfo(g_WeeklyInfoManagerClass.PIF_WPS_InfoList[3]);
                     break;
                 case "DayOfWeekText5":
                     if (g_WeeklyInfoManagerClass.PIF_WPS_InfoList[4].IsOnAir == false)
                         g_WeeklyInfoManagerClass.PIF_WPS_InfoList[4].IsOnAir = true;
                     else
                         g_WeeklyInfoManagerClass.PIF_WPS_InfoList[4].IsOnAir = false;

                     WeekSchInfoElement5.UpdateWeekInfo(g_WeeklyInfoManagerClass.PIF_WPS_InfoList[4]);
                     break;
                 case "DayOfWeekText6":
                     if (g_WeeklyInfoManagerClass.PIF_WPS_InfoList[5].IsOnAir == false)
                         g_WeeklyInfoManagerClass.PIF_WPS_InfoList[5].IsOnAir = true;
                     else
                         g_WeeklyInfoManagerClass.PIF_WPS_InfoList[5].IsOnAir = false;

                     WeekSchInfoElement6.UpdateWeekInfo(g_WeeklyInfoManagerClass.PIF_WPS_InfoList[5]);
                     break;
                 case "DayOfWeekText7":
                     if (g_WeeklyInfoManagerClass.PIF_WPS_InfoList[6].IsOnAir == false)
                         g_WeeklyInfoManagerClass.PIF_WPS_InfoList[6].IsOnAir = true;
                     else
                         g_WeeklyInfoManagerClass.PIF_WPS_InfoList[6].IsOnAir = false;

                     WeekSchInfoElement7.UpdateWeekInfo(g_WeeklyInfoManagerClass.PIF_WPS_InfoList[6]);
                     break;
                 default:
                     break;
             }

            DisplayWeekOfDay();
        }

        public void DisplayWeekOfDay()
         {
             if (g_WeeklyInfoManagerClass.PIF_WPS_InfoList.Count == 0)
             {
                 return;
             }

             if (g_WeeklyInfoManagerClass.PIF_WPS_InfoList[0].IsOnAir)
                 DayOfWeekRect1.Fill = ColorTools.GetSolidBrushByColorString("#FF73a0ec");
             else
                 DayOfWeekRect1.Fill = new SolidColorBrush(Colors.Gray);

             if (g_WeeklyInfoManagerClass.PIF_WPS_InfoList[1].IsOnAir)
                 DayOfWeekRect2.Fill = ColorTools.GetSolidBrushByColorString("#FF73a0ec");
             else
                 DayOfWeekRect2.Fill = new SolidColorBrush(Colors.Gray);

             if (g_WeeklyInfoManagerClass.PIF_WPS_InfoList[2].IsOnAir)
                 DayOfWeekRect3.Fill = ColorTools.GetSolidBrushByColorString("#FF73a0ec");
             else
                 DayOfWeekRect3.Fill = new SolidColorBrush(Colors.Gray);

             if (g_WeeklyInfoManagerClass.PIF_WPS_InfoList[3].IsOnAir)
                 DayOfWeekRect4.Fill = ColorTools.GetSolidBrushByColorString("#FF73a0ec");
             else
                 DayOfWeekRect4.Fill = new SolidColorBrush(Colors.Gray);

             if (g_WeeklyInfoManagerClass.PIF_WPS_InfoList[4].IsOnAir)
                 DayOfWeekRect5.Fill = ColorTools.GetSolidBrushByColorString("#FF73a0ec");
             else
                 DayOfWeekRect5.Fill = new SolidColorBrush(Colors.Gray);

             if (g_WeeklyInfoManagerClass.PIF_WPS_InfoList[5].IsOnAir)
                 DayOfWeekRect6.Fill = ColorTools.GetSolidBrushByColorString("#FF73a0ec");
             else
                 DayOfWeekRect6.Fill = new SolidColorBrush(Colors.Gray);

             if (g_WeeklyInfoManagerClass.PIF_WPS_InfoList[6].IsOnAir)
                 DayOfWeekRect7.Fill = ColorTools.GetSolidBrushByColorString("#FF73a0ec");
             else
                 DayOfWeekRect7.Fill = new SolidColorBrush(Colors.Gray);
         }

         void DayOfWeekRect1_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
         {
             if (g_WeeklyInfoManagerClass.PIF_WPS_InfoList.Count == 0)
             {
                 return;
             }

             Rectangle tmpRect = (Rectangle)sender;

             switch (tmpRect.Name)
             {
                 case "DayOfWeekRect1":
                     if (g_WeeklyInfoManagerClass.PIF_WPS_InfoList[0].IsOnAir == false)
                         g_WeeklyInfoManagerClass.PIF_WPS_InfoList[0].IsOnAir = true;
                     else
                         g_WeeklyInfoManagerClass.PIF_WPS_InfoList[0].IsOnAir = false;

                     WeekSchInfoElement1.UpdateWeekInfo(g_WeeklyInfoManagerClass.PIF_WPS_InfoList[0]);
                     break;
                 case "DayOfWeekRect2":
                     if (g_WeeklyInfoManagerClass.PIF_WPS_InfoList[1].IsOnAir == false)
                         g_WeeklyInfoManagerClass.PIF_WPS_InfoList[1].IsOnAir = true;
                     else
                         g_WeeklyInfoManagerClass.PIF_WPS_InfoList[1].IsOnAir = false;


                     WeekSchInfoElement2.UpdateWeekInfo(g_WeeklyInfoManagerClass.PIF_WPS_InfoList[1]);
                     break;
                 case "DayOfWeekRect3":
                     if (g_WeeklyInfoManagerClass.PIF_WPS_InfoList[2].IsOnAir == false)
                         g_WeeklyInfoManagerClass.PIF_WPS_InfoList[2].IsOnAir = true;
                     else
                         g_WeeklyInfoManagerClass.PIF_WPS_InfoList[2].IsOnAir = false;

                     WeekSchInfoElement3.UpdateWeekInfo(g_WeeklyInfoManagerClass.PIF_WPS_InfoList[2]);
                     break;
                 case "DayOfWeekRect4":
                     if (g_WeeklyInfoManagerClass.PIF_WPS_InfoList[3].IsOnAir == false)
                         g_WeeklyInfoManagerClass.PIF_WPS_InfoList[3].IsOnAir = true;
                     else
                         g_WeeklyInfoManagerClass.PIF_WPS_InfoList[3].IsOnAir = false;

                     WeekSchInfoElement4.UpdateWeekInfo(g_WeeklyInfoManagerClass.PIF_WPS_InfoList[3]);
                     break;
                 case "DayOfWeekRect5":
                     if (g_WeeklyInfoManagerClass.PIF_WPS_InfoList[4].IsOnAir == false)
                         g_WeeklyInfoManagerClass.PIF_WPS_InfoList[4].IsOnAir = true;
                     else
                         g_WeeklyInfoManagerClass.PIF_WPS_InfoList[4].IsOnAir = false;

                     WeekSchInfoElement5.UpdateWeekInfo(g_WeeklyInfoManagerClass.PIF_WPS_InfoList[4]);
                     break;
                 case "DayOfWeekRect6":
                     if (g_WeeklyInfoManagerClass.PIF_WPS_InfoList[5].IsOnAir == false)
                         g_WeeklyInfoManagerClass.PIF_WPS_InfoList[5].IsOnAir = true;
                     else
                         g_WeeklyInfoManagerClass.PIF_WPS_InfoList[5].IsOnAir = false;

                     WeekSchInfoElement6.UpdateWeekInfo(g_WeeklyInfoManagerClass.PIF_WPS_InfoList[5]);
                     break;
                 case "DayOfWeekRect7":
                     if (g_WeeklyInfoManagerClass.PIF_WPS_InfoList[6].IsOnAir == false)
                         g_WeeklyInfoManagerClass.PIF_WPS_InfoList[6].IsOnAir = true;
                     else
                         g_WeeklyInfoManagerClass.PIF_WPS_InfoList[6].IsOnAir = false;

                     WeekSchInfoElement7.UpdateWeekInfo(g_WeeklyInfoManagerClass.PIF_WPS_InfoList[6]);
                     break;
                 default:
                     break;
             }

            DisplayWeekOfDay();

        }

        private void SyncWeeklyInfoFromUI()
        {
            if (g_WeeklyInfoManagerClass.PIF_WPS_InfoList.Count == 0)
            {
                return;
            }

            ApplyWeekTime(WeekSchInfoElement1, 0);
            ApplyWeekTime(WeekSchInfoElement2, 1);
            ApplyWeekTime(WeekSchInfoElement3, 2);
            ApplyWeekTime(WeekSchInfoElement4, 3);
            ApplyWeekTime(WeekSchInfoElement5, 4);
            ApplyWeekTime(WeekSchInfoElement6, 5);
            ApplyWeekTime(WeekSchInfoElement7, 6);
        }

        private void ApplyWeekTime(WeekSchInfoElement element, int index)
        {
            if (element == null)
            {
                return;
            }

            if (g_WeeklyInfoManagerClass.PIF_WPS_InfoList.Count <= index)
            {
                return;
            }

            element.ApplyCurrentTimeTo(g_WeeklyInfoManagerClass.PIF_WPS_InfoList[index]);
        }

        public void InitEventHandler()
         {
             BTN0DO_Copy2.Click += BTN0DO_Copy2_Click;  // 취소
             ExitBTN.Click +=ExitBTN_Click;
             this.Loaded += EditOnAirTimeWindow_Loaded;

             BTN0DO_Copy.Click += BTN0DO_Copy_Click;  // 모든 플레이어에 적용

             BTN0DO_Copy5.Click += BTN0DO_Copy5_Click;  //  업데이트
         }

        void BTN0DO_Copy5_Click(object sender, RoutedEventArgs e)
        {
            //if (CheckIsPlayerOffline() == true)
            //{
            //    MessageTools.ShowMessageBox("플레이어가 중지 상태입니다.", "확인");
            //    Page3.Instance.SetPlayerNetworkStatus(this.g_CurrentSelectedPlayerInfoClass.PIF_PlayerName, PlayerStatus.Stopped);
            //    return;
            //}

            try
            {
                SyncWeeklyInfoFromUI();
                g_WeeklyInfoManagerClass.SaveWeeklySchedule(this.g_CurrentSelectedPlayerInfoClass.PIF_GUID, this.g_CurrentSelectedPlayerInfoClass.PIF_PlayerName);

                MainWindow.Instance.EnqueueCommandForPlayer(g_CurrentSelectedPlayerInfoClass, RP_ORDER.updateweekly.ToString(), pushSignalR: true);

                MessageTools.ShowMessageBox("방송시간 설정을 업데이트했습니다.", "확인");
            }
            catch (Exception ex)
            {
                MessageTools.ShowMessageBox("방송시간 설정을 실패했습니다.", "확인");
            }
        }

        public bool CheckIsPlayerOffline()
         {
             if (MainWindow.Instance.onlineList.Contains(this.g_CurrentSelectedPlayerInfoClass.PIF_PlayerName) == true)
             {
                 return false;
             }
             else
             {
                 return true;
             }
             
         }

        void BTN0DO_Copy_Click(object sender, RoutedEventArgs e)
        {
            SyncWeeklyInfoFromUI();

            List<PlayerInfoClass> targetPlayers = GetBatchTargetPlayers();
            if (targetPlayers.Count == 0)
            {
                MessageTools.ShowMessageBox("일괄 적용할 플레이어가 없습니다.", "확인");
                return;
            }

            int successCount = 0;
            int failedCount = 0;
            foreach (PlayerInfoClass item in targetPlayers)
            {
                try
                {
                    g_WeeklyInfoManagerClass.SaveWeeklySchedule(item.PIF_GUID, item.PIF_PlayerName);
                    if (MainWindow.Instance.EnqueueCommandForPlayer(item, RP_ORDER.updateweekly.ToString(), pushSignalR: true))
                    {
                        successCount++;
                    }
                    else
                    {
                        failedCount++;
                    }
                }
                catch (Exception)
                {
                    failedCount++;
                }
            }

            string targetName = g_CurrentSelectedPlayerGroupClass == null
                ? "모든 플레이어"
                : $"'{g_CurrentSelectedPlayerGroupClass.PG_GroupName}' 그룹";
            string resultMessage = failedCount == 0
                ? $"{targetName} {successCount}대에 방송시간 적용을 요청했습니다."
                : $"{targetName} 방송시간 적용 요청: 성공 {successCount}대, 실패 {failedCount}대";
            MessageTools.ShowMessageBox(resultMessage, "확인");
        }

        private List<PlayerInfoClass> GetBatchTargetPlayers()
        {
            List<PlayerInfoClass> allPlayers = DataShop.Instance.g_PlayerInfoManager.g_PlayerInfoClassList
                .Where(player => player != null)
                .ToList();
            if (g_CurrentSelectedPlayerGroupClass == null)
            {
                return allPlayers;
            }

            var memberNames = new HashSet<string>(
                g_CurrentSelectedPlayerGroupClass.PG_AssignedPlayerNames ?? new List<string>(),
                StringComparer.CurrentCultureIgnoreCase);
            return allPlayers
                .Where(player => memberNames.Contains(player.PIF_PlayerName))
                .ToList();
        }

        private void UpdateBatchApplyButtonLabel()
        {
            string groupName = g_CurrentSelectedPlayerGroupClass?.PG_GroupName?.Trim();
            string label = string.IsNullOrWhiteSpace(groupName)
                ? "모든 플레이어에 일괄 적용"
                : $"'{groupName}' 그룹에 일괄 적용";
            TextAngleGrade1.Text = label;
            BTN0DO_Copy.ToolTip = label;
        }

        void EditOnAirTimeWindow_Loaded(object sender, RoutedEventArgs e)
        {
             g_WeeklyInfoManagerClass.InitPlayerInfoListFromDataTable(g_CurrentSelectedPlayerInfoClass.PIF_GUID, g_CurrentSelectedPlayerInfoClass.PIF_PlayerName);

             UpdateWeeklyInfo();             
        }

        public void UpdateWeeklyInfo()
        {
            WeekSchInfoElement1.UpdateWeekInfo(g_WeeklyInfoManagerClass.PIF_WPS_InfoList[0]);
            WeekSchInfoElement2.UpdateWeekInfo(g_WeeklyInfoManagerClass.PIF_WPS_InfoList[1]);
            WeekSchInfoElement3.UpdateWeekInfo(g_WeeklyInfoManagerClass.PIF_WPS_InfoList[2]);
            WeekSchInfoElement4.UpdateWeekInfo(g_WeeklyInfoManagerClass.PIF_WPS_InfoList[3]);
            WeekSchInfoElement5.UpdateWeekInfo(g_WeeklyInfoManagerClass.PIF_WPS_InfoList[4]);
            WeekSchInfoElement6.UpdateWeekInfo(g_WeeklyInfoManagerClass.PIF_WPS_InfoList[5]);
            WeekSchInfoElement7.UpdateWeekInfo(g_WeeklyInfoManagerClass.PIF_WPS_InfoList[6]);

            DisplayWeekOfDay();
        }

        void BTN0DO_Copy2_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
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

        void minBTN_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        void sizeBTN_Checked(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Maximized;
        }

        void sizeBTN_Unchecked(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Normal;
        }

        void ExitBTN_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void BtnWin_drag_PreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            this.DragMove();
        }

        private void SetAllBtn_Click(object sender, RoutedEventArgs e)
        {
            if (g_WeeklyInfoManagerClass.PIF_WPS_InfoList.Count == 0)
            {
                return;
            }

            WeekSchInfoElement1.ApplyCurrentTimeTo(g_WeeklyInfoManagerClass.PIF_WPS_InfoList[0]);

            WeeklyDayScheduleInfo source = g_WeeklyInfoManagerClass.PIF_WPS_InfoList[0];

            ApplyWeekSchedule(source, 0, WeekSchInfoElement1);
            ApplyWeekSchedule(source, 1, WeekSchInfoElement2);
            ApplyWeekSchedule(source, 2, WeekSchInfoElement3);
            ApplyWeekSchedule(source, 3, WeekSchInfoElement4);
            ApplyWeekSchedule(source, 4, WeekSchInfoElement5);
            ApplyWeekSchedule(source, 5, WeekSchInfoElement6);
            ApplyWeekSchedule(source, 6, WeekSchInfoElement7);

            DisplayWeekOfDay();
        }

        private void ApplyWeekSchedule(WeeklyDayScheduleInfo source, int index, WeekSchInfoElement element)
        {
            if (source == null || element == null || g_WeeklyInfoManagerClass.PIF_WPS_InfoList.Count <= index)
            {
                return;
            }

            WeeklyDayScheduleInfo target = g_WeeklyInfoManagerClass.PIF_WPS_InfoList[index];
            target.StartHour = source.StartHour;
            target.StartMinute = source.StartMinute;
            target.EndHour = source.EndHour;
            target.EndMinute = source.EndMinute;
            target.IsOnAir = source.IsOnAir;

            element.UpdateWeekInfo(target);
        }
    }

}
