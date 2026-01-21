using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using HyonManager.Pages;
using HyonManager.SubWindow;
using HyonManager.DataClass;
using HyonManager.SubElement;

namespace HyonManager.SubWindow
{
    /// <summary>
    /// NewPlayerWindow.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class EditPlayerWindow : Window
    {
        PlayerElement g_ParentPage = null;

        public bool g_IsPlayerTypeLand = true;
        PlayerInfoClass g_PlayerInfoClass = new PlayerInfoClass();

        public EditPlayerWindow(PlayerElement paramPage, PlayerInfoClass paramCls)
        {
            InitializeComponent();
            g_ParentPage = paramPage;
            g_PlayerInfoClass.CopyData(paramCls);
            InitEventHandler();


            if (g_PlayerInfoClass.PIF_PlayerType == "LandScape")
            {
                g_IsPlayerTypeLand = true;
            }
            else
            {
                g_IsPlayerTypeLand = false;
            }

            DisplayThisWindow();
        
        }

        public void DisplayThisWindow()
        {
            TextBoxNewPlayerName.Text = g_PlayerInfoClass.PIF_PlayrName;
            TextBoxNewPlayerName1.Text = g_PlayerInfoClass.PIF_PlayrIP;
          

            //if (TextBoxNewPlayerName1.Text == string.Empty)
            //{
            //    UtilityClass.ShowMessageBox("플레이어IP를 입력해주세요.");
            //    return;
            //}

            if (g_PlayerInfoClass.PIF_PlayerType == "LandScape")
            {
                RectangleLand.Stroke = new SolidColorBrush(Colors.Gold);
                RectanglePortrait.Stroke = new SolidColorBrush(Colors.Gray);
            }
            else
            {
                RectangleLand.Stroke = new SolidColorBrush(Colors.Gray);
                RectanglePortrait.Stroke = new SolidColorBrush(Colors.Gold);
            }

            WeekSchInfoElement1.UpdateWeekInfo(g_PlayerInfoClass.PIF_WPS_InfoList[0]);
            WeekSchInfoElement2.UpdateWeekInfo(g_PlayerInfoClass.PIF_WPS_InfoList[1]);
            WeekSchInfoElement3.UpdateWeekInfo(g_PlayerInfoClass.PIF_WPS_InfoList[2]);
            WeekSchInfoElement4.UpdateWeekInfo(g_PlayerInfoClass.PIF_WPS_InfoList[3]);
            WeekSchInfoElement5.UpdateWeekInfo(g_PlayerInfoClass.PIF_WPS_InfoList[4]);
            WeekSchInfoElement6.UpdateWeekInfo(g_PlayerInfoClass.PIF_WPS_InfoList[5]);
            WeekSchInfoElement7.UpdateWeekInfo(g_PlayerInfoClass.PIF_WPS_InfoList[6]);

            DisplayWeekOfDay();
      
        }


        public void InitEventHandler()
        {
            PageNavigator2.Click += PageNavigator2_Click;  // 취소

            LancScapeGrid.PreviewMouseMove += LancScapeGrid_PreviewMouseMove;
            LancScapeGrid.MouseLeave += LancScapeGrid_MouseLeave;
            LancScapeGrid.PreviewMouseLeftButtonUp += LancScapeGrid_PreviewMouseLeftButtonUp;

            PortraitGrid.PreviewMouseMove += PortraitGrid_PreviewMouseMove;
            PortraitGrid.MouseLeave += PortraitGrid_MouseLeave;
            PortraitGrid.PreviewMouseLeftButtonUp += LancScapeGrid_PreviewMouseLeftButtonUp;

            PageNavigator1.Click += PageNavigator1_Click;  // 수정

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

        void WeekSchInfoElement1_EventUpdateWIFD(WeeklyPlayScheduleInfo paramCls)
        {
            switch (paramCls.WPS_DayOfWeek)
            {
                case "SUN":
                    g_PlayerInfoClass.PIF_WPS_InfoList[0].CopyData(paramCls);
                    break;
                case "MON":
                    g_PlayerInfoClass.PIF_WPS_InfoList[1].CopyData(paramCls);
                    break;
                case "TUE":
                    g_PlayerInfoClass.PIF_WPS_InfoList[2].CopyData(paramCls);
                    break;
                case "WED":
                    g_PlayerInfoClass.PIF_WPS_InfoList[3].CopyData(paramCls);
                    break;
                case "THU":
                    g_PlayerInfoClass.PIF_WPS_InfoList[4].CopyData(paramCls);
                    break;
                case "FRI":
                    g_PlayerInfoClass.PIF_WPS_InfoList[5].CopyData(paramCls);
                    break;
                case "SAT":
                    g_PlayerInfoClass.PIF_WPS_InfoList[6].CopyData(paramCls);
                    break;

                default:
                    break;
            }
        }


        void DayOfWeekText1_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            TextBlock tmpRect = (TextBlock)sender;

            switch (tmpRect.Name)
            {
                case "DayOfWeekText1":
                    if (g_PlayerInfoClass.PIF_WPS_InfoList[0].WPS_IsOnAir == false)
                        g_PlayerInfoClass.PIF_WPS_InfoList[0].WPS_IsOnAir = true;
                    else
                        g_PlayerInfoClass.PIF_WPS_InfoList[0].WPS_IsOnAir = false;
                    break;
                case "DayOfWeekText2":
                    if (g_PlayerInfoClass.PIF_WPS_InfoList[1].WPS_IsOnAir == false)
                        g_PlayerInfoClass.PIF_WPS_InfoList[1].WPS_IsOnAir = true;
                    else
                        g_PlayerInfoClass.PIF_WPS_InfoList[1].WPS_IsOnAir = false;
                    break;
                case "DayOfWeekText3":
                    if (g_PlayerInfoClass.PIF_WPS_InfoList[2].WPS_IsOnAir == false)
                        g_PlayerInfoClass.PIF_WPS_InfoList[2].WPS_IsOnAir = true;
                    else
                        g_PlayerInfoClass.PIF_WPS_InfoList[2].WPS_IsOnAir = false;
                    break;
                case "DayOfWeekText4":
                    if (g_PlayerInfoClass.PIF_WPS_InfoList[3].WPS_IsOnAir == false)
                        g_PlayerInfoClass.PIF_WPS_InfoList[3].WPS_IsOnAir = true;
                    else
                        g_PlayerInfoClass.PIF_WPS_InfoList[3].WPS_IsOnAir = false;
                    break;
                case "DayOfWeekText5":
                    if (g_PlayerInfoClass.PIF_WPS_InfoList[4].WPS_IsOnAir == false)
                        g_PlayerInfoClass.PIF_WPS_InfoList[4].WPS_IsOnAir = true;
                    else
                        g_PlayerInfoClass.PIF_WPS_InfoList[4].WPS_IsOnAir = false;
                    break;
                case "DayOfWeekText6":
                    if (g_PlayerInfoClass.PIF_WPS_InfoList[5].WPS_IsOnAir == false)
                        g_PlayerInfoClass.PIF_WPS_InfoList[5].WPS_IsOnAir = true;
                    else
                        g_PlayerInfoClass.PIF_WPS_InfoList[5].WPS_IsOnAir = false;
                    break;
                case "DayOfWeekText7":
                    if (g_PlayerInfoClass.PIF_WPS_InfoList[6].WPS_IsOnAir == false)
                        g_PlayerInfoClass.PIF_WPS_InfoList[6].WPS_IsOnAir = true;
                    else
                        g_PlayerInfoClass.PIF_WPS_InfoList[6].WPS_IsOnAir = false;
                    break;
                default:
                    break;
            }

            DisplayWeekOfDay();
        }

        void DayOfWeekRect1_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            Rectangle tmpRect = (Rectangle)sender;

            switch (tmpRect.Name)
            {
                case "DayOfWeekRect1":
                    if (g_PlayerInfoClass.PIF_WPS_InfoList[0].WPS_IsOnAir == false)
                        g_PlayerInfoClass.PIF_WPS_InfoList[0].WPS_IsOnAir = true;
                    else
                        g_PlayerInfoClass.PIF_WPS_InfoList[0].WPS_IsOnAir = false;
                    break;
                case "DayOfWeekRect2":
                    if (g_PlayerInfoClass.PIF_WPS_InfoList[1].WPS_IsOnAir == false)
                        g_PlayerInfoClass.PIF_WPS_InfoList[1].WPS_IsOnAir = true;
                    else
                        g_PlayerInfoClass.PIF_WPS_InfoList[1].WPS_IsOnAir = false;
                    break;
                case "DayOfWeekRect3":
                    if (g_PlayerInfoClass.PIF_WPS_InfoList[2].WPS_IsOnAir == false)
                        g_PlayerInfoClass.PIF_WPS_InfoList[2].WPS_IsOnAir = true;
                    else
                        g_PlayerInfoClass.PIF_WPS_InfoList[2].WPS_IsOnAir = false;
                    break;
                case "DayOfWeekRect4":
                    if (g_PlayerInfoClass.PIF_WPS_InfoList[3].WPS_IsOnAir == false)
                        g_PlayerInfoClass.PIF_WPS_InfoList[3].WPS_IsOnAir = true;
                    else
                        g_PlayerInfoClass.PIF_WPS_InfoList[3].WPS_IsOnAir = false;
                    break;
                case "DayOfWeekRect5":
                    if (g_PlayerInfoClass.PIF_WPS_InfoList[4].WPS_IsOnAir == false)
                        g_PlayerInfoClass.PIF_WPS_InfoList[4].WPS_IsOnAir = true;
                    else
                        g_PlayerInfoClass.PIF_WPS_InfoList[4].WPS_IsOnAir = false;
                    break;
                case "DayOfWeekRect6":
                    if (g_PlayerInfoClass.PIF_WPS_InfoList[5].WPS_IsOnAir == false)
                        g_PlayerInfoClass.PIF_WPS_InfoList[5].WPS_IsOnAir = true;
                    else
                        g_PlayerInfoClass.PIF_WPS_InfoList[5].WPS_IsOnAir = false;
                    break;
                case "DayOfWeekRect7":
                    if (g_PlayerInfoClass.PIF_WPS_InfoList[6].WPS_IsOnAir == false)
                        g_PlayerInfoClass.PIF_WPS_InfoList[6].WPS_IsOnAir = true;
                    else
                        g_PlayerInfoClass.PIF_WPS_InfoList[6].WPS_IsOnAir = false;
                    break;
                default:
                    break;
            }

            DisplayWeekOfDay();
        }

        public void DisplayWeekOfDay()
        {
            if (g_PlayerInfoClass.PIF_WPS_InfoList[0].WPS_IsOnAir)
                DayOfWeekRect1.Fill = new SolidColorBrush(Colors.GreenYellow);
            else
                DayOfWeekRect1.Fill = new SolidColorBrush(Colors.Gray);

            if (g_PlayerInfoClass.PIF_WPS_InfoList[1].WPS_IsOnAir)
                DayOfWeekRect2.Fill = new SolidColorBrush(Colors.GreenYellow);
            else
                DayOfWeekRect2.Fill = new SolidColorBrush(Colors.Gray);

            if (g_PlayerInfoClass.PIF_WPS_InfoList[2].WPS_IsOnAir)
                DayOfWeekRect3.Fill = new SolidColorBrush(Colors.GreenYellow);
            else
                DayOfWeekRect3.Fill = new SolidColorBrush(Colors.Gray);

            if (g_PlayerInfoClass.PIF_WPS_InfoList[3].WPS_IsOnAir)
                DayOfWeekRect4.Fill = new SolidColorBrush(Colors.GreenYellow);
            else
                DayOfWeekRect4.Fill = new SolidColorBrush(Colors.Gray);

            if (g_PlayerInfoClass.PIF_WPS_InfoList[4].WPS_IsOnAir)
                DayOfWeekRect5.Fill = new SolidColorBrush(Colors.GreenYellow);
            else
                DayOfWeekRect5.Fill = new SolidColorBrush(Colors.Gray);

            if (g_PlayerInfoClass.PIF_WPS_InfoList[5].WPS_IsOnAir)
                DayOfWeekRect6.Fill = new SolidColorBrush(Colors.GreenYellow);
            else
                DayOfWeekRect6.Fill = new SolidColorBrush(Colors.Gray);

            if (g_PlayerInfoClass.PIF_WPS_InfoList[6].WPS_IsOnAir)
                DayOfWeekRect7.Fill = new SolidColorBrush(Colors.GreenYellow);
            else
                DayOfWeekRect7.Fill = new SolidColorBrush(Colors.Gray);
        }

        void PageNavigator1_Click(object sender, RoutedEventArgs e)
        {
            if (TextBoxNewPlayerName.Text == string.Empty)
            {
                UtilityClass.ShowMessageBox("플레이어 이름을 입력해주세요.");
                return;
            }

            if (TextBoxNewPlayerName1.Text == string.Empty)
            {
                UtilityClass.ShowMessageBox("플레이어IP를 입력해주세요.");
                return;
            }

            g_PlayerInfoClass.PIF_PlayrName = TextBoxNewPlayerName.Text;
            g_PlayerInfoClass.PIF_PlayrIP = TextBoxNewPlayerName1.Text;

            if (g_IsPlayerTypeLand == true)
            {
                g_PlayerInfoClass.PIF_PlayerType = "LandScape";
            }
            else
            {
                g_PlayerInfoClass.PIF_PlayerType = "Portrait";    
            }

            g_PlayerInfoClass.PIF_DataFilename = string.Format("{0}_Shedule.xml", TextBoxNewPlayerName.Text);

            this.g_ParentPage.EditPlayerInfo(g_PlayerInfoClass);

            //UtilityClass.ShowMessageBox("플레이어를 추가했습니다.");
            this.Close();
        }

        public void DisplayPlayerType()
        {
            if (this.g_IsPlayerTypeLand == true)
            {
                this.g_IsPlayerTypeLand = false;
                RectangleLand.Stroke = new SolidColorBrush(Colors.Gray);
                RectanglePortrait.Stroke = new SolidColorBrush(Colors.Gold);
                //

            }
            else
            {
                this.g_IsPlayerTypeLand = true;
                RectangleLand.Stroke = new SolidColorBrush(Colors.Gold);
                RectanglePortrait.Stroke = new SolidColorBrush(Colors.Gray);
            }
        }

        void LancScapeGrid_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            DisplayPlayerType();
        }

        

        void PortraitGrid_MouseLeave(object sender, MouseEventArgs e)
        {
            portraitText.Foreground = new SolidColorBrush(Colors.Gray);
        }

        void PortraitGrid_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            portraitText.Foreground = new SolidColorBrush(Colors.White);
        }

        void LancScapeGrid_MouseLeave(object sender, MouseEventArgs e)
        {
            landscapeText.Foreground = new SolidColorBrush(Colors.Gray);
        }

        void LancScapeGrid_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            landscapeText.Foreground = new SolidColorBrush(Colors.White);
        }

        void PageNavigator2_Click(object sender, RoutedEventArgs e)
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

    }
}
