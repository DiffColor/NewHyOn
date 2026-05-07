using System;
using System.Windows;
using System.Windows.Input;
using TurtleTools;

namespace AndoW_Manager
{
    /// <summary>
    /// EditPeriodWindow.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class EditPeriodWindow : Window
    {
        public ContentsInfoClass TargetContent { get; private set; }
        private bool _periodCleared;
        private bool _isAdjustingTimeSelection;

        public EditPeriodWindow(ContentsInfoClass content)
        {
            InitializeComponent();
            TargetContent = content ?? new ContentsInfoClass();
            InitCombo();
            Loaded += EditPeriodWindow_Loaded;
        }

        private void InitCombo()
        {
            MinCombo.Items.Clear();
            SecCombo.Items.Clear();
            StartHourCombo.Items.Clear();
            StartMinuteCombo.Items.Clear();
            EndHourCombo.Items.Clear();
            EndMinuteCombo.Items.Clear();

            StartHourCombo.Items.Add(string.Empty);
            StartMinuteCombo.Items.Add(string.Empty);
            EndHourCombo.Items.Add(string.Empty);
            EndMinuteCombo.Items.Add(string.Empty);

            for (int i = 0; i < 60; i++)
            {
                string value = string.Format("{0:D2}", i);
                MinCombo.Items.Add(value);
                SecCombo.Items.Add(value);
                StartMinuteCombo.Items.Add(value);
                EndMinuteCombo.Items.Add(value);
            }

            for (int i = 0; i < 24; i++)
            {
                string value = string.Format("{0:D2}", i);
                StartHourCombo.Items.Add(value);
                EndHourCombo.Items.Add(value);
            }

            MinCombo.SelectedIndex = 0;
            SecCombo.SelectedIndex = 10;
            StartHourCombo.SelectedIndex = 0;
            StartMinuteCombo.SelectedIndex = 0;
            EndHourCombo.SelectedIndex = 0;
            EndMinuteCombo.SelectedIndex = 0;
        }

        private void EditPeriodWindow_Loaded(object sender, RoutedEventArgs e)
        {
            if (TargetContent == null)
            {
                return;
            }

            MinCombo.SelectedItem = TargetContent.CIF_PlayMinute.PadLeft(2, '0');
            SecCombo.SelectedItem = TargetContent.CIF_PlaySec.PadLeft(2, '0');

            var period = Page1.Instance?.GetPeriodData(TargetContent.CIF_StrGUID);
            if (period != null)
            {
                if (DateTime.TryParse(period.StartDate, out var start))
                {
                    StartDatePicker.SelectedDate = start;
                }
                else if (!string.IsNullOrWhiteSpace(period.StartDate))
                {
                    StartDatePicker.Text = period.StartDate;
                }

                if (DateTime.TryParse(period.EndDate, out var end))
                {
                    EndDatePicker.SelectedDate = end;
                }
                else if (!string.IsNullOrWhiteSpace(period.EndDate))
                {
                    EndDatePicker.Text = period.EndDate;
                }

                ApplyTimeToInputs(period.StartTime, StartHourCombo, StartMinuteCombo);
                ApplyTimeToInputs(period.EndTime, EndHourCombo, EndMinuteCombo);
            }
        }

        private void SaveBtn_Click(object sender, RoutedEventArgs e)
        {
            if (TargetContent == null)
            {
                DialogResult = false;
                Close();
                return;
            }

            EnsureMinuteDefaultsToZero(StartHourCombo, StartMinuteCombo);
            EnsureMinuteDefaultsToZero(EndHourCombo, EndMinuteCombo);

            string startDate = StartDatePicker.Text;
            string endDate = EndDatePicker.Text;
            string startTime = GetSelectedTime(StartHourCombo, StartMinuteCombo);
            string endTime = GetSelectedTime(EndHourCombo, EndMinuteCombo);
            string validationError = ValidatePeriodInputs();
            if (!string.IsNullOrWhiteSpace(validationError))
            {
                MessageTools.ShowMessageBox(validationError, "확인");
                return;
            }

            if (Page1.Instance != null
                && !Page1.Instance.TryNormalizePeriodDataInputs(
                    startDate,
                    endDate,
                    startTime,
                    endTime,
                    out startDate,
                    out endDate,
                    out startTime,
                    out endTime,
                    out validationError))
            {
                MessageTools.ShowMessageBox(validationError, "확인");
                return;
            }

            TargetContent.CIF_PlayMinute = MinCombo.SelectedItem?.ToString() ?? "00";
            TargetContent.CIF_PlaySec = SecCombo.SelectedItem?.ToString() ?? "10";

            bool hasStart = string.IsNullOrWhiteSpace(startDate) == false;
            bool hasEnd = string.IsNullOrWhiteSpace(endDate) == false;
            bool hasTime = string.IsNullOrWhiteSpace(startTime) == false || string.IsNullOrWhiteSpace(endTime) == false;

            if (_periodCleared && !hasStart && !hasEnd && !hasTime)
            {
                Page1.Instance?.DeletePeriodData(TargetContent);
            }
            else
            {
                Page1.Instance?.SetPeriodData(TargetContent, startDate, endDate, startTime, endTime);
            }

            DialogResult = true;
            Close();
        }

        private void CancelBtn_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private string ValidatePeriodInputs()
        {
            bool hasStartHour = string.IsNullOrWhiteSpace(StartHourCombo?.SelectedItem?.ToString()) == false;
            bool hasStartMinute = string.IsNullOrWhiteSpace(StartMinuteCombo?.SelectedItem?.ToString()) == false;
            bool hasEndHour = string.IsNullOrWhiteSpace(EndHourCombo?.SelectedItem?.ToString()) == false;
            bool hasEndMinute = string.IsNullOrWhiteSpace(EndMinuteCombo?.SelectedItem?.ToString()) == false;

            if (hasStartHour != hasStartMinute)
            {
                return "표출 시작 시간은 시와 분을 모두 입력해주세요.";
            }

            if (hasEndHour != hasEndMinute)
            {
                return "표출 종료 시간은 시와 분을 모두 입력해주세요.";
            }

            return string.Empty;
        }

        private void BtnWin_close_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void ClearDateBtn_Click(object sender, RoutedEventArgs e)
        {
            StartDatePicker.SelectedDate = null;
            EndDatePicker.SelectedDate = null;
            StartDatePicker.Text = string.Empty;
            EndDatePicker.Text = string.Empty;
            StartHourCombo.SelectedIndex = 0;
            StartMinuteCombo.SelectedIndex = 0;
            EndHourCombo.SelectedIndex = 0;
            EndMinuteCombo.SelectedIndex = 0;

            _periodCleared = true;
        }

        private void TitleBar_Drag(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        private void StartDatePicker_CalendarOpened(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(StartDatePicker.Text))
            {
                StartDatePicker.SelectedDate = DateTime.Today;
            }
        }

        private void EndDatePicker_CalendarOpened(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(EndDatePicker.Text))
            {
                EndDatePicker.SelectedDate = DateTime.Today;
            }
        }

        private void StartDatePicker_SelectedDateChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (StartDatePicker.SelectedDate == null)
            {
                return;
            }

            if (EndDatePicker.SelectedDate == null && string.IsNullOrWhiteSpace(EndDatePicker.Text))
            {
                EndDatePicker.SelectedDate = StartDatePicker.SelectedDate.Value.Date;
            }
        }

        private void EndDatePicker_SelectedDateChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            return;
        }

        private void StartHourCombo_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            NormalizeTimeSelection(StartHourCombo, StartMinuteCombo, changedByHour: true);
        }

        private void EndHourCombo_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            NormalizeTimeSelection(EndHourCombo, EndMinuteCombo, changedByHour: true);
        }

        private void StartMinuteCombo_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            NormalizeTimeSelection(StartHourCombo, StartMinuteCombo, changedByHour: false);
        }

        private void EndMinuteCombo_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            NormalizeTimeSelection(EndHourCombo, EndMinuteCombo, changedByHour: false);
        }

        private static void ApplyTimeToInputs(string value, System.Windows.Controls.ComboBox hourCombo, System.Windows.Controls.ComboBox minuteCombo)
        {
            if (hourCombo == null || minuteCombo == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            if (TimeSpan.TryParse(value, out var parsed))
            {
                hourCombo.SelectedItem = parsed.Hours.ToString("D2");
                minuteCombo.SelectedItem = parsed.Minutes.ToString("D2");
            }
        }

        private static void EnsureMinuteDefaultsToZero(System.Windows.Controls.ComboBox hourCombo, System.Windows.Controls.ComboBox minuteCombo)
        {
            if (hourCombo == null || minuteCombo == null)
            {
                return;
            }

            string hour = hourCombo.SelectedItem?.ToString();
            string minute = minuteCombo.SelectedItem?.ToString();
            if (string.IsNullOrWhiteSpace(hour))
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(minute))
            {
                minuteCombo.SelectedItem = "00";
            }
        }

        private void NormalizeTimeSelection(System.Windows.Controls.ComboBox hourCombo, System.Windows.Controls.ComboBox minuteCombo, bool changedByHour)
        {
            if (_isAdjustingTimeSelection || hourCombo == null || minuteCombo == null)
            {
                return;
            }

            try
            {
                _isAdjustingTimeSelection = true;

                string hour = hourCombo.SelectedItem?.ToString();
                string minute = minuteCombo.SelectedItem?.ToString();
                bool hasHour = string.IsNullOrWhiteSpace(hour) == false;
                bool hasMinute = string.IsNullOrWhiteSpace(minute) == false;

                if (changedByHour)
                {
                    if (!hasHour)
                    {
                        hourCombo.SelectedIndex = 0;
                        minuteCombo.SelectedIndex = 0;
                        return;
                    }

                    if (!hasMinute)
                    {
                        minuteCombo.SelectedItem = "00";
                    }

                    return;
                }

                if (!hasMinute || !hasHour)
                {
                    hourCombo.SelectedIndex = 0;
                    minuteCombo.SelectedIndex = 0;
                }
            }
            finally
            {
                _isAdjustingTimeSelection = false;
            }
        }

        private static string GetSelectedTime(System.Windows.Controls.ComboBox hourCombo, System.Windows.Controls.ComboBox minuteCombo)
        {
            string hour = hourCombo?.SelectedItem?.ToString();
            string minute = minuteCombo?.SelectedItem?.ToString();
            if (string.IsNullOrWhiteSpace(hour) || string.IsNullOrWhiteSpace(minute))
            {
                return string.Empty;
            }

            return string.Format("{0}:{1}", hour.PadLeft(2, '0'), minute.PadLeft(2, '0'));
        }
    }
}
