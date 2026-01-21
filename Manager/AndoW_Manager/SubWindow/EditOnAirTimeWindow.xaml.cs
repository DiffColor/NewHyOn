using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Newtonsoft.Json;
using System;
using TurtleTools;
using System.Windows.Shapes;

namespace AndoW_Manager
{
    /// <summary>
    /// NotifyWindow.xaml에 대한 상호 작용 논리
    /// </summary>
     public partial class EditOnAirTimeWindow : Window
    {
         public WeeklyInfoManagerClass g_WeeklyInfoManagerClass = new WeeklyInfoManagerClass();
         public PlayerInfoClass g_CurrentSelectedPlayerInfoClass = new PlayerInfoClass();

         public EditOnAirTimeWindow(PlayerInfoClass paramCls)
        {
            InitializeComponent();
            g_CurrentSelectedPlayerInfoClass.CopyData(paramCls);
            InitEventHandler();
            InitWeeklyInfoEventHandler();

            TextAngleGrade1_Copy1.Text = paramCls.PIF_PlayerName;
        }

         public void InitWeeklyInfoEventHandler()
         {
             WeekSchInfoElement1.EventUpdateWIFD += WeekSchInfoElement1_EventUpdateWIFD;
             WeekSchInfoElement2.EventUpdateWIFD += WeekSchInfoElement1_EventUpdateWIFD;
             WeekSchInfoElement3.EventUpdateWIFD += WeekSchInfoElement1_EventUpdateWIFD;
             WeekSchInfoElement4.EventUpdateWIFD += WeekSchInfoElement1_EventUpdateWIFD;
             WeekSchInfoElement5.EventUpdateWIFD += WeekSchInfoElement1_EventUpdateWIFD;
             WeekSchInfoElement6.EventUpdateWIFD += WeekSchInfoElement1_EventUpdateWIFD;
             WeekSchInfoElement7.EventUpdateWIFD += WeekSchInfoElement1_EventUpdateWIFD;

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

             g_WeeklyInfoManagerClass.SaveWeeklySchedule(this.g_CurrentSelectedPlayerInfoClass.PIF_GUID, this.g_CurrentSelectedPlayerInfoClass.PIF_PlayerName);
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

             g_WeeklyInfoManagerClass.SaveWeeklySchedule(this.g_CurrentSelectedPlayerInfoClass.PIF_GUID, this.g_CurrentSelectedPlayerInfoClass.PIF_PlayerName);
             DisplayWeekOfDay();

         }

         void WeekSchInfoElement1_EventUpdateWIFD(WeeklyDayScheduleInfo paramCls)
         {
             if (g_WeeklyInfoManagerClass.PIF_WPS_InfoList.Count == 0)
             {
                 return;
             }

             switch (paramCls.DayOfWeek)
             {
                 case "SUN":
                     g_WeeklyInfoManagerClass.PIF_WPS_InfoList[0].CopyData(paramCls);
                     break;
                 case "MON":
                     g_WeeklyInfoManagerClass.PIF_WPS_InfoList[1].CopyData(paramCls);
                     break;
                 case "TUE":
                     g_WeeklyInfoManagerClass.PIF_WPS_InfoList[2].CopyData(paramCls);
                     break;
                 case "WED":
                     g_WeeklyInfoManagerClass.PIF_WPS_InfoList[3].CopyData(paramCls);
                     break;
                 case "THU":
                     g_WeeklyInfoManagerClass.PIF_WPS_InfoList[4].CopyData(paramCls);
                     break;
                 case "FRI":
                     g_WeeklyInfoManagerClass.PIF_WPS_InfoList[5].CopyData(paramCls);
                     break;
                 case "SAT":
                     g_WeeklyInfoManagerClass.PIF_WPS_InfoList[6].CopyData(paramCls);
                     break;

                 default:
                     break;
             }

             g_WeeklyInfoManagerClass.SaveWeeklySchedule(this.g_CurrentSelectedPlayerInfoClass.PIF_GUID, this.g_CurrentSelectedPlayerInfoClass.PIF_PlayerName);
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
                //if (this.g_CurrentSelectedPlayerInfoClass.PIF_OSName.Equals("Android"))
                //{
                //    string filelistJSON = JsonConvert.SerializeObject(this.g_WeeklyInfoManagerClass.g_Dt_WeekSchInfo, Formatting.Indented);
                //       // RPCaller.RPCall(g_CurrentSelectedPlayerInfoClass.PIF_IPAddress, RP_ID.UpdateWeeklySchedule, filelistJSON);
                //}

                MainWindow.Instance.EnqueueCommandForPlayer(g_CurrentSelectedPlayerInfoClass, RP_ORDER.updateschedule.ToString(), pushSignalR: true);

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
             foreach (PlayerInfoClass item in DataShop.Instance.g_PlayerInfoManager.g_PlayerInfoClassList)
             {
                 g_WeeklyInfoManagerClass.SaveWeeklySchedule(item.PIF_GUID, item.PIF_PlayerName);

                 if (item.PIF_OSName.Equals("Android"))
                 {
                     try
                     {
                         string filelistJSON = JsonConvert.SerializeObject(g_WeeklyInfoManagerClass.PIF_WPS_InfoList, Formatting.Indented);
                         //RPCaller.RPCall(g_CurrentSelectedPlayerInfoClass.PIF_IPAddress, RP_ID.UpdateWeeklySchedule, filelistJSON);
                     }
                     catch (Exception ex)
                     {
                     }
                 }
             }

             MessageTools.ShowMessageBox("모든 플레이어에 적용했습니다.", "확인");
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
            WeekSchInfoElement1.SetDataAndState();

            WeekSchInfoElement1.UpdateWeekInfo(g_WeeklyInfoManagerClass.PIF_WPS_InfoList[0], true);
            WeekSchInfoElement2.UpdateWeekInfo(g_WeeklyInfoManagerClass.PIF_WPS_InfoList[0], true);
            WeekSchInfoElement3.UpdateWeekInfo(g_WeeklyInfoManagerClass.PIF_WPS_InfoList[0], true);
            WeekSchInfoElement4.UpdateWeekInfo(g_WeeklyInfoManagerClass.PIF_WPS_InfoList[0], true);
            WeekSchInfoElement5.UpdateWeekInfo(g_WeeklyInfoManagerClass.PIF_WPS_InfoList[0], true);
            WeekSchInfoElement6.UpdateWeekInfo(g_WeeklyInfoManagerClass.PIF_WPS_InfoList[0], true);
            WeekSchInfoElement7.UpdateWeekInfo(g_WeeklyInfoManagerClass.PIF_WPS_InfoList[0], true);

            g_WeeklyInfoManagerClass.PIF_WPS_InfoList[0].CopyData(g_WeeklyInfoManagerClass.PIF_WPS_InfoList[0], true);
            g_WeeklyInfoManagerClass.PIF_WPS_InfoList[1].CopyData(g_WeeklyInfoManagerClass.PIF_WPS_InfoList[0], true);
            g_WeeklyInfoManagerClass.PIF_WPS_InfoList[2].CopyData(g_WeeklyInfoManagerClass.PIF_WPS_InfoList[0], true);
            g_WeeklyInfoManagerClass.PIF_WPS_InfoList[3].CopyData(g_WeeklyInfoManagerClass.PIF_WPS_InfoList[0], true);
            g_WeeklyInfoManagerClass.PIF_WPS_InfoList[4].CopyData(g_WeeklyInfoManagerClass.PIF_WPS_InfoList[0], true);
            g_WeeklyInfoManagerClass.PIF_WPS_InfoList[5].CopyData(g_WeeklyInfoManagerClass.PIF_WPS_InfoList[0], true);
            g_WeeklyInfoManagerClass.PIF_WPS_InfoList[6].CopyData(g_WeeklyInfoManagerClass.PIF_WPS_InfoList[0], true);

            g_WeeklyInfoManagerClass.SaveWeeklySchedule(this.g_CurrentSelectedPlayerInfoClass.PIF_GUID, this.g_CurrentSelectedPlayerInfoClass.PIF_PlayerName);

            DisplayWeekOfDay();
        }
    }

}
