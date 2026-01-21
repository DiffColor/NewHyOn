using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;


namespace AndoW_Manager
{
    /// <summary>
    /// WeekSchInfoElement.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class WeekSchInfoElement : UserControl
    {
        WeeklyDayScheduleInfo g_WeeklyPlayScheduleInfo = new WeeklyDayScheduleInfo();

        public delegate void UpdateWIFD(WeeklyDayScheduleInfo paramCls);              // Contents Play
        public event UpdateWIFD EventUpdateWIFD;

        bool g_IsEditing = false;

        public WeekSchInfoElement()
        {
            InitializeComponent();
            InitEventHandler();
            InitComboBoxes();
            WeekStackPanel1.Visibility = Visibility.Hidden;
        }

        public void InitComboBoxes()
        {
            for (int i = 0; i < 24; i++)
            {
                DispStartHourCombo.Items.Add(string.Format("{0:D2}",i));
                DispEndHourCombo.Items.Add(string.Format("{0:D2}", i));
            }

            for (int i = 0; i < 60; i++)
            {
                DispStartMinCombo.Items.Add(string.Format("{0:D2}", i));
                DispEndMinCombo.Items.Add(string.Format("{0:D2}", i));
            }
        }

        public void DisplayThisElement()
        {
            DisplaytimeText.Text = string.Format("{0:D2}:{1:D2} ~ {2:D2}:{3:D2}",
                g_WeeklyPlayScheduleInfo.StartHour,
                g_WeeklyPlayScheduleInfo.StartMinute,
                g_WeeklyPlayScheduleInfo.EndHour,
                g_WeeklyPlayScheduleInfo.EndMinute);

            if (g_IsEditing == true)
            {
                BorderBTN_Copy1.Content = "저장";
                BorderBTN_Copy1.Foreground = new SolidColorBrush(Colors.OrangeRed);
                WeekStackPanel1.Visibility = System.Windows.Visibility.Visible;
                DisplaytimeText.Visibility = System.Windows.Visibility.Hidden;
            }
            else
            {
                BorderBTN_Copy1.Content = "편집";
                BorderBTN_Copy1.Foreground = new SolidColorBrush(Colors.Black);
                WeekStackPanel1.Visibility = System.Windows.Visibility.Hidden;
                DisplaytimeText.Visibility = System.Windows.Visibility.Visible;
            }

            DispStartHourCombo.SelectedIndex = g_WeeklyPlayScheduleInfo.StartHour;
            DispStartMinCombo.SelectedIndex = g_WeeklyPlayScheduleInfo.StartMinute;
            DispEndHourCombo.SelectedIndex = g_WeeklyPlayScheduleInfo.EndHour;
            DispEndMinCombo.SelectedIndex = g_WeeklyPlayScheduleInfo.EndMinute;

            if (g_WeeklyPlayScheduleInfo.IsOnAir == true)
            {
                DisplaytimeText_Copy.Text = "방송";
            }
            else
            {
                DisplaytimeText_Copy.Text = "방송안함";
            }
        }

        public void InitEventHandler()
        {
            BorderBTN_Copy1.Click += BorderBTN_Copy1_Click;
        }

        void BorderBTN_Copy1_Click(object sender, RoutedEventArgs e)  // 저장
        {
            SetDataAndToggle();
        }

        public void SetDataAndToggle()
        {
            if (g_IsEditing == false)
            {
                g_IsEditing = true;
            }
            else
            {
                g_IsEditing = false;
            }

            if (g_IsEditing == false)
            {
                g_WeeklyPlayScheduleInfo.StartHour = DispStartHourCombo.SelectedIndex;
                g_WeeklyPlayScheduleInfo.StartMinute = DispStartMinCombo.SelectedIndex;
                g_WeeklyPlayScheduleInfo.EndHour = DispEndHourCombo.SelectedIndex;
                g_WeeklyPlayScheduleInfo.EndMinute = DispEndMinCombo.SelectedIndex;

                EventUpdateWIFD(this.g_WeeklyPlayScheduleInfo);
            }

            DisplayThisElement();
        }

        public void SetDataAndState() {

            g_IsEditing = false;

            g_WeeklyPlayScheduleInfo.StartHour = DispStartHourCombo.SelectedIndex;
            g_WeeklyPlayScheduleInfo.StartMinute = DispStartMinCombo.SelectedIndex;
            g_WeeklyPlayScheduleInfo.EndHour = DispEndHourCombo.SelectedIndex;
            g_WeeklyPlayScheduleInfo.EndMinute = DispEndMinCombo.SelectedIndex;

            EventUpdateWIFD(this.g_WeeklyPlayScheduleInfo);

            DisplayThisElement();
        }

        public void UpdateWeekInfo(WeeklyDayScheduleInfo paramCls, bool onlyTime = false)
        {
            g_WeeklyPlayScheduleInfo.CopyData(paramCls, onlyTime);
            DisplayThisElement();
        }
    }
}
