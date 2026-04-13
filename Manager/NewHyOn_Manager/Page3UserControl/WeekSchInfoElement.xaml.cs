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
        private bool suppressTimeSync = false;

        public delegate void UpdateWIFD(WeeklyDayScheduleInfo paramCls);              // Contents Play
        public event UpdateWIFD EventUpdateWIFD;

        public WeekSchInfoElement()
        {
            InitializeComponent();
            InitComboBoxes();
            InitEventHandler();
            WeekStackPanel1.Visibility = Visibility.Visible;
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
            suppressTimeSync = true;
            DisplaytimeText.Text = string.Format("{0:D2}:{1:D2} ~ {2:D2}:{3:D2}",
                g_WeeklyPlayScheduleInfo.StartHour,
                g_WeeklyPlayScheduleInfo.StartMinute,
                g_WeeklyPlayScheduleInfo.EndHour,
                g_WeeklyPlayScheduleInfo.EndMinute);
            WeekStackPanel1.Visibility = Visibility.Visible;
            DisplaytimeText.Visibility = Visibility.Collapsed;

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
            suppressTimeSync = false;
        }

        private void InitEventHandler()
        {
            DispStartHourCombo.SelectionChanged += TimeCombo_SelectionChanged;
            DispStartMinCombo.SelectionChanged += TimeCombo_SelectionChanged;
            DispEndHourCombo.SelectionChanged += TimeCombo_SelectionChanged;
            DispEndMinCombo.SelectionChanged += TimeCombo_SelectionChanged;
        }

        private void TimeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (suppressTimeSync)
            {
                return;
            }

            SyncCurrentSelection();
        }

        private void SyncCurrentSelection()
        {
            if (DispStartHourCombo.SelectedIndex >= 0)
            {
                g_WeeklyPlayScheduleInfo.StartHour = DispStartHourCombo.SelectedIndex;
            }
            if (DispStartMinCombo.SelectedIndex >= 0)
            {
                g_WeeklyPlayScheduleInfo.StartMinute = DispStartMinCombo.SelectedIndex;
            }
            if (DispEndHourCombo.SelectedIndex >= 0)
            {
                g_WeeklyPlayScheduleInfo.EndHour = DispEndHourCombo.SelectedIndex;
            }
            if (DispEndMinCombo.SelectedIndex >= 0)
            {
                g_WeeklyPlayScheduleInfo.EndMinute = DispEndMinCombo.SelectedIndex;
            }
        }

        public void ApplyCurrentTimeTo(WeeklyDayScheduleInfo target)
        {
            if (target == null)
            {
                return;
            }

            DispStartHourCombo.IsDropDownOpen = false;
            DispStartMinCombo.IsDropDownOpen = false;
            DispEndHourCombo.IsDropDownOpen = false;
            DispEndMinCombo.IsDropDownOpen = false;

            SyncCurrentSelection();
            target.CopyData(g_WeeklyPlayScheduleInfo, true);
        }

        public void UpdateWeekInfo(WeeklyDayScheduleInfo paramCls, bool onlyTime = false)
        {
            g_WeeklyPlayScheduleInfo.CopyData(paramCls, onlyTime);
            DisplayThisElement();
        }
    }
}
