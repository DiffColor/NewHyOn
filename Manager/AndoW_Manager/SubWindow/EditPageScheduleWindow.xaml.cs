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
    public partial class EditPageScheduleWindow : Window
    {
        PageScheduleElement g_ParentPage = null;

        public bool g_IsPlayerTypeLand = true;

        public ScheduleDataInfoClass g_ScheduleDataInfoClass = new ScheduleDataInfoClass();


        public EditPageScheduleWindow(PageScheduleElement paramPage, ScheduleDataInfoClass paramCls)
        {
            InitializeComponent();
            g_ParentPage = paramPage;
            g_ScheduleDataInfoClass.CopyData(paramCls);
            InitEventHandler();

            InitComboBoxes();
            DIsplayPlayerType();

            DisplayWeekOfDay();
            DisplayCalendar();
            
            NeedPeriodCheckBox.IsChecked = g_ScheduleDataInfoClass.SDI_IsPeriodEnable;
        }

        public void InitComboBoxes()
        { 
        //PlayTimeMin

            for (int i = 0; i < 60; i++)
            {
                PlayTimeMin.Items.Add(string.Format("{0:D2}",i));
                PlayTimeSec.Items.Add(string.Format("{0:D2}",i));                
                DispStartMinCombo.Items.Add(string.Format("{0:D2}",i));
                DispEndMinCombo.Items.Add(string.Format("{0:D2}", i));                
            }

            for (int i = 0; i < 24; i++)
            {
                DispStartHourCombo.Items.Add(string.Format("{0:D2}", i));      
                DispEndHourCombo.Items.Add(string.Format("{0:D2}", i));                
            }

            PlayTimeMin.SelectedItem = g_ScheduleDataInfoClass.SDI_PlayMin;
            PlayTimeSec.SelectedItem = g_ScheduleDataInfoClass.SDI_PlaySec;

            DispStartHourCombo.SelectedItem = g_ScheduleDataInfoClass.SDI_DisplayStartH;
            DispStartMinCombo.SelectedItem = g_ScheduleDataInfoClass.SDI_DisplayStartM;
            DispEndHourCombo.SelectedItem = g_ScheduleDataInfoClass.SDI_DisplayEndH;
            DispEndMinCombo.SelectedItem = g_ScheduleDataInfoClass.SDI_DisplayEndM;
        }

        public void DisplayWeekOfDay()
        {
            if (g_ScheduleDataInfoClass.SDI_DayOfWeek1)
                DayOfWeekRect1.Fill = new SolidColorBrush(Colors.GreenYellow);
            else
                DayOfWeekRect1.Fill = new SolidColorBrush(Colors.Gray);

            if (g_ScheduleDataInfoClass.SDI_DayOfWeek2)
                DayOfWeekRect2.Fill = new SolidColorBrush(Colors.GreenYellow);
            else
                DayOfWeekRect2.Fill = new SolidColorBrush(Colors.Gray);

            if (g_ScheduleDataInfoClass.SDI_DayOfWeek3)
                DayOfWeekRect3.Fill = new SolidColorBrush(Colors.GreenYellow);
            else
                DayOfWeekRect3.Fill = new SolidColorBrush(Colors.Gray);

            if (g_ScheduleDataInfoClass.SDI_DayOfWeek4)
                DayOfWeekRect4.Fill = new SolidColorBrush(Colors.GreenYellow);
            else
                DayOfWeekRect4.Fill = new SolidColorBrush(Colors.Gray);

            if (g_ScheduleDataInfoClass.SDI_DayOfWeek5)
                DayOfWeekRect5.Fill = new SolidColorBrush(Colors.GreenYellow);
            else
                DayOfWeekRect5.Fill = new SolidColorBrush(Colors.Gray);

            if (g_ScheduleDataInfoClass.SDI_DayOfWeek6)
                DayOfWeekRect6.Fill = new SolidColorBrush(Colors.GreenYellow);
            else
                DayOfWeekRect6.Fill = new SolidColorBrush(Colors.Gray);

            if (g_ScheduleDataInfoClass.SDI_DayOfWeek7)
                DayOfWeekRect7.Fill = new SolidColorBrush(Colors.GreenYellow);
            else
                DayOfWeekRect7.Fill = new SolidColorBrush(Colors.Gray);
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

            PageNavigator1.Click += PageNavigator1_Click;  // 저장

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

            NeedPeriodCheckBox.Checked += NeedPeriodCheckBox_Checked;
            NeedPeriodCheckBox.Unchecked += NeedPeriodCheckBox_Unchecked;

            DatePickerStart.CalendarClosed += DatePickerStart_CalendarClosed;
            DatePickerEnd.CalendarClosed += DatePickerEnd_CalendarClosed;

            PlayTimeMin.SelectionChanged += PlayTimeMin_SelectionChanged;
            PlayTimeSec.SelectionChanged += PlayTimeSec_SelectionChanged;

            DispStartHourCombo.SelectionChanged += DispStartHourCombo_SelectionChanged;
            DispStartMinCombo.SelectionChanged += DispStartMinCombo_SelectionChanged;

            DispEndHourCombo.SelectionChanged += DispEndHourCombo_SelectionChanged;
            DispEndMinCombo.SelectionChanged += DispEndMinCombo_SelectionChanged;

        }

   

        void DispEndMinCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            g_ScheduleDataInfoClass.SDI_DisplayEndM = DispEndMinCombo.SelectedItem.ToString();
        }

        void DispEndHourCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            g_ScheduleDataInfoClass.SDI_DisplayEndH = DispEndHourCombo.SelectedItem.ToString();
        }

        void DispStartMinCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            g_ScheduleDataInfoClass.SDI_DisplayStartM = DispStartMinCombo.SelectedItem.ToString();
        }

        void DispStartHourCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            g_ScheduleDataInfoClass.SDI_DisplayStartH = DispStartHourCombo.SelectedItem.ToString();
        }

        void PlayTimeSec_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            g_ScheduleDataInfoClass.SDI_PlaySec = PlayTimeSec.SelectedItem.ToString();
        }

        void PlayTimeMin_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            g_ScheduleDataInfoClass.SDI_PlayMin = PlayTimeMin.SelectedItem.ToString();
        }

        public void DisplayCalendar()
        {
            if (this.g_ScheduleDataInfoClass.SDI_PeriodStartYear > 0)
            {
                DatePickerStart.SelectedDate = new DateTime(this.g_ScheduleDataInfoClass.SDI_PeriodStartYear,
                                   this.g_ScheduleDataInfoClass.SDI_PeriodStartMonth,
                                    this.g_ScheduleDataInfoClass.SDI_PeriodStartDay);

                DatePickerEnd.SelectedDate = new DateTime(this.g_ScheduleDataInfoClass.SDI_PeriodEndYear,
                                     this.g_ScheduleDataInfoClass.SDI_PeriodEndMonth,
                                      this.g_ScheduleDataInfoClass.SDI_PeriodEndDay);
            }
          
        }

        void DatePickerEnd_CalendarClosed(object sender, RoutedEventArgs e)
        {
            try
            {
                if (DatePickerEnd.SelectedDate.Value != null)
                {
                    DateTime selectDate = DatePickerEnd.SelectedDate.Value;

                    this.g_ScheduleDataInfoClass.SDI_PeriodEndYear = selectDate.Year;
                    this.g_ScheduleDataInfoClass.SDI_PeriodEndMonth = selectDate.Month;
                    this.g_ScheduleDataInfoClass.SDI_PeriodEndDay = selectDate.Day;
                }
            }
            catch (Exception ex)
            { 
            
            }
           
         
        }

        void DatePickerStart_CalendarClosed(object sender, RoutedEventArgs e)
        {
            try
            {
                if (DatePickerStart.SelectedDate.Value != null)
                {
                    DateTime selectDate = DatePickerStart.SelectedDate.Value;

                    this.g_ScheduleDataInfoClass.SDI_PeriodStartYear = selectDate.Year;  // 기간설정
                    this.g_ScheduleDataInfoClass.SDI_PeriodStartMonth = selectDate.Month;
                    this.g_ScheduleDataInfoClass.SDI_PeriodStartDay = selectDate.Day;
                }
            }
            catch (Exception ex)
            {

            }
            //MessageBox.Show(DatePickerStart.SelectedDate.ToString());
        }

        void NeedPeriodCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            if (NeedPeriodCheckBox.IsChecked == true)
            {
                this.g_ScheduleDataInfoClass.SDI_IsPeriodEnable = true;
            }
            else
            {
                this.g_ScheduleDataInfoClass.SDI_IsPeriodEnable = false;
            }
        }

        void NeedPeriodCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            if (NeedPeriodCheckBox.IsChecked == true)
            {
                this.g_ScheduleDataInfoClass.SDI_IsPeriodEnable = true;
            }
            else
            {
                this.g_ScheduleDataInfoClass.SDI_IsPeriodEnable = false;
            }
        }

        void DayOfWeekText1_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            TextBlock tmpRect = (TextBlock)sender;

            switch (tmpRect.Name)
            {
                case "DayOfWeekText1":
                    if (g_ScheduleDataInfoClass.SDI_DayOfWeek1 == false)
                        g_ScheduleDataInfoClass.SDI_DayOfWeek1 = true;
                    else
                        g_ScheduleDataInfoClass.SDI_DayOfWeek1 = false;
                    break;
                case "DayOfWeekText2":
                    if (g_ScheduleDataInfoClass.SDI_DayOfWeek2 == false)
                        g_ScheduleDataInfoClass.SDI_DayOfWeek2 = true;
                    else
                        g_ScheduleDataInfoClass.SDI_DayOfWeek2 = false;
                    break;
                case "DayOfWeekText3":
                    if (g_ScheduleDataInfoClass.SDI_DayOfWeek3 == false)
                        g_ScheduleDataInfoClass.SDI_DayOfWeek3 = true;
                    else
                        g_ScheduleDataInfoClass.SDI_DayOfWeek3 = false;
                    break;
                case "DayOfWeekText4":
                    if (g_ScheduleDataInfoClass.SDI_DayOfWeek4 == false)
                        g_ScheduleDataInfoClass.SDI_DayOfWeek4 = true;
                    else
                        g_ScheduleDataInfoClass.SDI_DayOfWeek4 = false;
                    break;
                case "DayOfWeekText5":
                    if (g_ScheduleDataInfoClass.SDI_DayOfWeek5 == false)
                        g_ScheduleDataInfoClass.SDI_DayOfWeek5 = true;
                    else
                        g_ScheduleDataInfoClass.SDI_DayOfWeek5 = false;
                    break;
                case "DayOfWeekText6":
                    if (g_ScheduleDataInfoClass.SDI_DayOfWeek6 == false)
                        g_ScheduleDataInfoClass.SDI_DayOfWeek6 = true;
                    else
                        g_ScheduleDataInfoClass.SDI_DayOfWeek6 = false;
                    break;
                case "DayOfWeekText7":
                    if (g_ScheduleDataInfoClass.SDI_DayOfWeek7 == false)
                        g_ScheduleDataInfoClass.SDI_DayOfWeek7 = true;
                    else
                        g_ScheduleDataInfoClass.SDI_DayOfWeek7 = false;
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
                    if (g_ScheduleDataInfoClass.SDI_DayOfWeek1 == false)
                        g_ScheduleDataInfoClass.SDI_DayOfWeek1 = true;
                    else
                        g_ScheduleDataInfoClass.SDI_DayOfWeek1 = false;
                    break;
                case "DayOfWeekRect2":
                    if (g_ScheduleDataInfoClass.SDI_DayOfWeek2 == false)
                        g_ScheduleDataInfoClass.SDI_DayOfWeek2 = true;
                    else
                        g_ScheduleDataInfoClass.SDI_DayOfWeek2 = false;
                    break;
                case "DayOfWeekRect3":
                    if (g_ScheduleDataInfoClass.SDI_DayOfWeek3 == false)
                        g_ScheduleDataInfoClass.SDI_DayOfWeek3 = true;
                    else
                        g_ScheduleDataInfoClass.SDI_DayOfWeek3 = false;
                    break;
                case "DayOfWeekRect4":
                    if (g_ScheduleDataInfoClass.SDI_DayOfWeek4 == false)
                        g_ScheduleDataInfoClass.SDI_DayOfWeek4 = true;
                    else
                        g_ScheduleDataInfoClass.SDI_DayOfWeek4 = false;
                    break;
                case "DayOfWeekRect5":
                    if (g_ScheduleDataInfoClass.SDI_DayOfWeek5 == false)
                        g_ScheduleDataInfoClass.SDI_DayOfWeek5 = true;
                    else
                        g_ScheduleDataInfoClass.SDI_DayOfWeek5 = false;
                    break;
                case "DayOfWeekRect6":
                    if (g_ScheduleDataInfoClass.SDI_DayOfWeek6 == false)
                        g_ScheduleDataInfoClass.SDI_DayOfWeek6 = true;
                    else
                        g_ScheduleDataInfoClass.SDI_DayOfWeek6 = false;
                    break;
                case "DayOfWeekRect7":
                    if (g_ScheduleDataInfoClass.SDI_DayOfWeek7 == false)
                        g_ScheduleDataInfoClass.SDI_DayOfWeek7 = true;
                    else
                        g_ScheduleDataInfoClass.SDI_DayOfWeek7 = false;
                    break;                
                default:
                    break;
            }

            DisplayWeekOfDay();
        }

        void PageNavigator1_Click(object sender, RoutedEventArgs e)
        {
            if (g_ScheduleDataInfoClass.SDI_IsPeriodEnable == true)
            {
                if (g_ScheduleDataInfoClass.SDI_PeriodStartYear == 0  || g_ScheduleDataInfoClass.SDI_PeriodEndYear == 0)
                {
                    UtilityClass.ShowMessageBox("기간설정기능을 선택하셨을때는, 기간설정 입력을 완료해야합니다.");
                    return;
                }
            }
            this.g_ParentPage.EditScheduleDataInfoClassInfo(this.g_ScheduleDataInfoClass);
            
            this.Close();
            //if (TextBoxNewPlayerName.Text == string.Empty)
            //{
            //    UtilityClass.ShowMessageBox("플레이어 이름을 입력해주세요.");
            //    return;
            //}

            //if (TextBoxNewPlayerName1.Text == string.Empty)
            //{
            //    UtilityClass.ShowMessageBox("플레이어IP를 입력해주세요.");
            //    return;
            //}

            //PlayerInfoClass tmpCls = new PlayerInfoClass();
            //tmpCls.PIF_PlayrName = TextBoxNewPlayerName.Text;
            //tmpCls.PIF_PlayrIP = TextBoxNewPlayerName1.Text;

            //if (g_IsPlayerTypeLand == true)
            //{
            //    tmpCls.PIF_PlayerType = "LandScape";
            //}
            //else
            //{
            //    tmpCls.PIF_PlayerType = "Portrait";    
            //}

            //tmpCls.PIF_DataFilename = string.Format("{0}_Shedule.xml", TextBoxNewPlayerName.Text);

            //this.g_ParentPage.AddPlayerInfoClass(tmpCls);

            
        }

        public void TogglePlayerType()
        {
            if (this.g_ScheduleDataInfoClass.SDI_ScheduleType == "General")
            {
                this.g_ScheduleDataInfoClass.SDI_ScheduleType = "Reservation";
            }
            else
            {
                this.g_ScheduleDataInfoClass.SDI_ScheduleType = "General";
            }
            DIsplayPlayerType();
        }

        public void DIsplayPlayerType()
        {
            if (this.g_ScheduleDataInfoClass.SDI_ScheduleType == "General")
            {
                RectangleLand.Stroke = new SolidColorBrush(Colors.Gold);
                RectanglePortrait.Stroke = new SolidColorBrush(Colors.Gray);

                ReserveGrid.Visibility = Visibility.Hidden;
            }
            else
            {
                RectangleLand.Stroke = new SolidColorBrush(Colors.Gray);
                RectanglePortrait.Stroke = new SolidColorBrush(Colors.Gold);
                ReserveGrid.Visibility = Visibility.Visible;
            }
        }

        void LancScapeGrid_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            TogglePlayerType();
        }

        

        void PortraitGrid_MouseLeave(object sender, MouseEventArgs e)
        {
            portraitText.Foreground = new SolidColorBrush(Colors.White);
        }

        void PortraitGrid_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            portraitText.Foreground = new SolidColorBrush(Colors.Gold);
        }

        void LancScapeGrid_MouseLeave(object sender, MouseEventArgs e)
        {
            landscapeText.Foreground = new SolidColorBrush(Colors.White);
        }

        void LancScapeGrid_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            landscapeText.Foreground = new SolidColorBrush(Colors.Gold);
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
