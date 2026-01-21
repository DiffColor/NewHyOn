using AndoW_Manager.SubWindow;
using System.Collections.Generic;
using System.Windows.Controls;

namespace AndoW_Manager
{
    /// <summary>
    /// SpecialCtrl.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class SpecialCtrl : UserControl
    {
        SpecialScheduleViewData _ss;
        public SpecialScheduleViewData sSS
        {
            get { return _ss; }
            set
            {
                _ss = value;
                UpdateData(_ss);
            }
        }

        public SpecialCtrl()
        {
            InitializeComponent();
        }

        public SpecialCtrl(SpecialScheduleViewData ss)
        {
            InitializeComponent();
            sSS = ss;
        }

        private void UpdateData(SpecialScheduleViewData ss)
        {
            if (ss == null)
            {
                ClearData();
                return;
            }

            string displayName = string.IsNullOrWhiteSpace(ss.TargetDisplayName) ? ss.GroupName : ss.TargetDisplayName;
            GroupNameTBlock.Text = displayName;
            PlaylistTBlock.Text = ss.Playlist;
            StartDateTBlock.Text = ss.StartDateText;
            EndDateTBlock.Text = ss.EndDateText;
            StartTimeTBlock.Text = ss.StartTimeText;
            EndTimeTBlock.Text = ss.EndTimeText;

            if (ss.Days != null && ss.Days.Length > 6)
            {
                MonToggle.IsChecked = ss.Days[0];
                TueToggle.IsChecked = ss.Days[1];
                WedToggle.IsChecked = ss.Days[2];
                ThuToggle.IsChecked = ss.Days[3];
                FriToggle.IsChecked = ss.Days[4];
                SatToggle.IsChecked = ss.Days[5];
                SunToggle.IsChecked = ss.Days[6];
            }
            else
            {
                ClearDayToggles();
            }
        }

        private void DelClass_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            if (sSS == null)
                return;

            Page5.Instance?.DeleteSchedules(sSS);
        }

        private void EditClass_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            //MainWindow.Instance.DialogHost.DialogContent = new NewClassPage(sCD);
            //MainWindow.Instance.DialogHost.IsOpen = true;

            //MainWindow.Instance.OpenDialog("NewClassCard", sCD);
        }

        private void SelectChBox_Checked(object sender, System.Windows.RoutedEventArgs e)
        {
            Page5.Instance?.ScheduleSelectionChanged();
        }

        private void SelectChBox_Unchecked(object sender, System.Windows.RoutedEventArgs e)
        {
            Page5.Instance?.ScheduleSelectionChanged();
        }

        private void UserControl_PreviewMouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (_ss == null)
                return;

            Page5.Instance?.SetSelectedData(_ss);
        }

        private void EditBtn_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            if (sSS == null)
                return;

            Page5.Instance?.OpenEditSpecialWindow(sSS);
        }

        private void ClearData()
        {
            GroupNameTBlock.Text = string.Empty;
            PlaylistTBlock.Text = string.Empty;
            StartDateTBlock.Text = string.Empty;
            EndDateTBlock.Text = string.Empty;
            StartTimeTBlock.Text = string.Empty;
            EndTimeTBlock.Text = string.Empty;
            ClearDayToggles();
        }

        private void ClearDayToggles()
        {
            MonToggle.IsChecked = false;
            TueToggle.IsChecked = false;
            WedToggle.IsChecked = false;
            ThuToggle.IsChecked = false;
            FriToggle.IsChecked = false;
            SatToggle.IsChecked = false;
            SunToggle.IsChecked = false;
        }
    }
}
