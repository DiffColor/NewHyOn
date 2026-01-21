using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace AndoW_Manager.SubWindow
{
    /// <summary>
    /// EditSpecialWindow.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class EditSpecialWindow : System.Windows.Window
    {
        private SpecialScheduleViewData sThisData;
        private List<SpecialScheduleViewData> sSameData;

        public EditSpecialWindow()
        {
            InitializeComponent();
        }

        public EditSpecialWindow(SpecialScheduleViewData ss, List<SpecialScheduleViewData> sslist)
        {
            InitializeComponent();
            this.sThisData = ss;
            this.sSameData = sslist ?? new List<SpecialScheduleViewData>();
            SetData();
        }

        private void SetData()
        {
            if (sThisData == null)
                return;

            TargetListBox.Items.Clear();
            TargetPlayerListBox.Items.Clear();

            GroupFilterTextBox.Text = string.Empty;
            PlayerFilterTextBox.Text = string.Empty;

            DataShop.Instance.g_PlayerGroupManager.LoadDataFromDatabase();

            List<string> groupList = sSameData
                .Where(x => x != null && x.GroupNames != null)
                .SelectMany(x => x.GroupNames)
                .Where(x => string.IsNullOrWhiteSpace(x) == false)
                .Where(x => DataShop.Instance.g_PlayerGroupManager.GetGroupByName(x) != null)
                .Distinct(StringComparer.CurrentCultureIgnoreCase)
                .OrderBy(x => x, StringComparer.CurrentCultureIgnoreCase)
                .ToList();

            HashSet<string> playerSet = new HashSet<string>(
                sSameData
                    .SelectMany(x => x.Schedules)
                    .Where(x => x != null && x.PlayerNames != null)
                    .SelectMany(x => x.PlayerNames)
                    .Where(x => string.IsNullOrWhiteSpace(x) == false),
                StringComparer.CurrentCultureIgnoreCase);

            List<string> playerList = new List<string>();
            foreach (PlayerInfoClass player in DataShop.Instance.g_PlayerInfoManager.GetOrderedPlayers())
            {
                if (player == null || string.IsNullOrWhiteSpace(player.PIF_PlayerName))
                    continue;

                if (playerSet.Remove(player.PIF_PlayerName))
                    playerList.Add(player.PIF_PlayerName);
            }

            if (playerSet.Count > 0)
            {
                foreach (string playerName in playerSet.OrderBy(x => x, StringComparer.CurrentCultureIgnoreCase))
                    playerList.Add(playerName);
            }

            foreach (string group in groupList)
            {
                ListBoxItem item = new ListBoxItem();
                item.Content = group;
                TargetListBox.Items.Add(item);
            }

            SelectAllChBox.IsChecked = false;

            foreach (string player in playerList)
            {
                ListBoxItem item = new ListBoxItem();
                item.Content = player;
                TargetPlayerListBox.Items.Add(item);
            }

            SelectAllPlayerChBox.IsChecked = false;

            bool groupSelected = false;
            bool playerSelected = false;

            if (sThisData.IsGroupTarget && sThisData.GroupNames != null && sThisData.GroupNames.Count > 0)
            {
                groupSelected = SelectListBoxItems(TargetListBox, sThisData.GroupNames);
            }

            if (groupSelected == false)
            {
                if (sThisData.TargetPlayers != null && sThisData.TargetPlayers.Count > 0)
                    playerSelected = SelectListBoxItems(TargetPlayerListBox, sThisData.TargetPlayers);
                else
                    playerSelected = SelectListBoxItem(TargetPlayerListBox, sThisData.GroupName);
            }

            if (groupSelected == false && playerSelected == false)
            {
                string firstPlayer = playerList.FirstOrDefault();
                if (string.IsNullOrWhiteSpace(firstPlayer) == false)
                    playerSelected = SelectListBoxItem(TargetPlayerListBox, firstPlayer);
            }

            GroupRadio.IsChecked = playerSelected == false;
            SingleRadio.IsChecked = playerSelected;
            ShowTargetPanel(GroupRadio.IsChecked == true);
            ApplyTargetFilters();

            SelectPlaylistCombo.Items.Clear();

            foreach (string listname in DataShop.Instance.g_PageListInfoManager.g_PageListInfoClassList.Select(x => x.PLI_PageListName))
                SelectPlaylistCombo.Items.Add(listname);

            SelectPlaylistCombo.SelectedItem = sThisData.Playlist;

            if (sThisData.Days != null && sThisData.Days.Length > 6)
            {
                MonToggle.IsChecked = sThisData.Days[0];
                TueToggle.IsChecked = sThisData.Days[1];
                WedToggle.IsChecked = sThisData.Days[2];
                ThuToggle.IsChecked = sThisData.Days[3];
                FriToggle.IsChecked = sThisData.Days[4];
                SatToggle.IsChecked = sThisData.Days[5];
                SunToggle.IsChecked = sThisData.Days[6];
            }

            if (sThisData.StartDate != null)
                StartDatePicker.SelectedDate = sThisData.StartDate.Value.Date;
            if (sThisData.EndDate != null)
                EndDatePicker.SelectedDate = sThisData.EndDate.Value.Date;
            if (sThisData.StartTime != null)
                StartTimePicker.SelectedTime = DateTime.Today.Add(sThisData.StartTime.Value);
            if (sThisData.EndTime != null)
                EndTimePicker.SelectedTime = DateTime.Today.Add(sThisData.EndTime.Value);
        }

        private void EditSchduleBtn_Click(object sender, RoutedEventArgs e)
        {
            bool isGroupMode = GroupRadio.IsChecked == true;
            List<string> selectedGroups = isGroupMode ? GetSelectedNames(TargetListBox) : new List<string>();
            List<string> selectedPlayers = isGroupMode ? new List<string>() : GetSelectedNames(TargetPlayerListBox);

            if (selectedGroups.Count < 1 && selectedPlayers.Count < 1)
            {
                MainWindow.Instance.EnqueueSnackMsg("그룹 또는 플레이어를 선택해주세요.");
                return;
            }

            if (SelectPlaylistCombo.SelectedItem == null)
            {
                MainWindow.Instance.EnqueueSnackMsg("플레이 리스트를 선택해주세요.");
                return;
            }

            List<SpecialScheduleInfoClass> targets = GetTargetSchedules(selectedGroups, selectedPlayers, isGroupMode);
            if (targets.Count < 1)
            {
                MainWindow.Instance.EnqueueSnackMsg("선택한 대상에 해당하는 스케줄이 없습니다.");
                return;
            }

            DateTime? startDateTime = BuildDateTime(StartDatePicker.SelectedDate, StartTimePicker.SelectedTime);
            DateTime? endDateTime = BuildDateTime(EndDatePicker.SelectedDate, EndTimePicker.SelectedTime);
            if (startDateTime == null || endDateTime == null)
            {
                MainWindow.Instance.EnqueueSnackMsg("날짜와 시간을 확인해주세요.");
                return;
            }

            if (endDateTime.Value < startDateTime.Value)
                endDateTime = endDateTime.Value.AddDays(1);

            if (DateTime.Now.CompareTo(endDateTime.Value) > 0)
            {
                MainWindow.Instance.EnqueueSnackMsg("현재 시간보다 이전의 스케줄은 등록이 불가합니다.");
                return;
            }

            if (TryGetScheduleInput(out DateTime startDate, out DateTime endDate, out TimeSpan startTime, out TimeSpan endTime, out bool[] days) == false)
            {
                MainWindow.Instance.EnqueueSnackMsg("날짜와 시간을 확인해주세요.");
                return;
            }

            string playlist = SelectPlaylistCombo.SelectedItem.ToString();
            bool result = EditSpecialSchedule(targets, playlist, startDate, endDate, startTime, endTime, days);
            if (result)
            {
                MainWindow.Instance.EnqueueSnackMsg("특별 스케줄을 수정하였습니다.");
                DialogResult = true;
                Close();
            }
        }

        private bool EditSpecialSchedule(List<SpecialScheduleInfoClass> targets, string playlist, DateTime startDate, DateTime endDate, TimeSpan startTime, TimeSpan endTime, bool[] days)
        {
            if (targets == null || targets.Count < 1)
                return false;

            SpecialScheduleInfoManager manager = new SpecialScheduleInfoManager();
            manager.LoadAllSchedules();
            HashSet<string> excludedIds = new HashSet<string>(
                targets.Select(x => x?.GUID).Where(x => string.IsNullOrWhiteSpace(x) == false),
                StringComparer.CurrentCultureIgnoreCase);
            HashSet<string> updatedPlayers = new HashSet<string>(StringComparer.CurrentCultureIgnoreCase);

            foreach (SpecialScheduleInfoClass target in targets)
            {
                if (target == null)
                    continue;

                SpecialScheduleInfoClass candidate = BuildScheduleInfo(target, playlist, startDate, endDate, startTime, endTime, days);
                if (HasDuplicateSchedule(manager.g_SpecialScheduleInfoClassList, excludedIds, candidate))
                {
                    MainWindow.Instance.EnqueueSnackMsg("해당 대상에 중복되는 스케줄이 존재합니다. 확인 후 등록해주세요.");
                    return false;
                }
            }

            foreach (SpecialScheduleInfoClass target in targets)
            {
                if (target == null || string.IsNullOrWhiteSpace(target.GUID))
                    continue;

                SpecialScheduleInfoClass updated = BuildScheduleInfo(target, playlist, startDate, endDate, startTime, endTime, days);
                updated.GUID = target.GUID;
                manager.EditDeviceInfoClass(target, updated, string.Empty);

                if (updated.PlayerNames == null)
                    continue;

                foreach (string player in updated.PlayerNames)
                {
                    if (string.IsNullOrWhiteSpace(player) == false)
                        updatedPlayers.Add(player);
                }
            }

            SendScheduleUpdates(updatedPlayers);
            return true;
        }

        private bool HasDuplicateSchedule(IEnumerable<SpecialScheduleInfoClass> schedules, HashSet<string> excludedIds, SpecialScheduleInfoClass candidate)
        {
            if (schedules == null || candidate == null)
                return false;

            HashSet<string> candidatePlayers = new HashSet<string>(
                candidate.PlayerNames == null
                    ? new List<string>()
                    : candidate.PlayerNames.Where(x => string.IsNullOrWhiteSpace(x) == false),
                StringComparer.CurrentCultureIgnoreCase);
            if (candidatePlayers.Count < 1)
                return false;

            foreach (SpecialScheduleInfoClass schedule in schedules)
            {
                if (schedule == null)
                    continue;

                if (excludedIds != null && excludedIds.Contains(schedule.GUID))
                    continue;

                if (schedule.PlayerNames == null || schedule.PlayerNames.Count < 1)
                    continue;

                bool hasPlayer = schedule.PlayerNames.Any(player => candidatePlayers.Contains(player));
                if (hasPlayer == false)
                    continue;

                if (schedule.CheckOverlappedPeriod(candidate))
                    return true;
            }

            return false;
        }

        private bool TryGetScheduleInput(out DateTime startDate, out DateTime endDate, out TimeSpan startTime, out TimeSpan endTime, out bool[] days)
        {
            startDate = DateTime.MinValue;
            endDate = DateTime.MinValue;
            startTime = TimeSpan.Zero;
            endTime = TimeSpan.Zero;
            days = new bool[7];

            if (StartDatePicker.SelectedDate == null || EndDatePicker.SelectedDate == null)
                return false;

            if (StartTimePicker.SelectedTime == null || EndTimePicker.SelectedTime == null)
                return false;

            startDate = StartDatePicker.SelectedDate.Value.Date;
            endDate = EndDatePicker.SelectedDate.Value.Date;
            startTime = StartTimePicker.SelectedTime.Value.TimeOfDay;
            endTime = EndTimePicker.SelectedTime.Value.TimeOfDay;
            days = DayToggleStack.Children.Cast<ToggleButton>().Select(x => x.IsChecked == true).ToArray();

            if (endDate == startDate && endTime < startTime)
                endDate = endDate.AddDays(1);

            return days.Length > 6;
        }

        private static DateTime? BuildDateTime(DateTime? date, DateTime? time)
        {
            if (date == null || time == null)
                return null;

            return date.Value.Date.Add(time.Value.TimeOfDay);
        }

        private static SpecialScheduleInfoClass BuildScheduleInfo(SpecialScheduleInfoClass source, string playlist, DateTime startDate, DateTime endDate, TimeSpan startTime, TimeSpan endTime, bool[] days)
        {
            SpecialScheduleInfoClass schedule = new SpecialScheduleInfoClass();

            if (source != null)
            {
                schedule.PlayerNames = source.PlayerNames == null
                    ? new List<string>()
                    : source.PlayerNames.Where(x => string.IsNullOrWhiteSpace(x) == false)
                        .Distinct(StringComparer.CurrentCultureIgnoreCase)
                        .ToList();
                schedule.GroupNames = source.GroupNames == null
                    ? new List<string>()
                    : source.GroupNames.Where(x => string.IsNullOrWhiteSpace(x) == false)
                        .Distinct(StringComparer.CurrentCultureIgnoreCase)
                        .ToList();
                schedule.GUID = source.GUID;
            }

            schedule.PageListName = playlist ?? string.Empty;
            schedule.IsPeriodEnable = true;
            schedule.PeriodStartYear = startDate.Year;
            schedule.PeriodStartMonth = startDate.Month;
            schedule.PeriodStartDay = startDate.Day;
            schedule.PeriodEndYear = endDate.Year;
            schedule.PeriodEndMonth = endDate.Month;
            schedule.PeriodEndDay = endDate.Day;
            schedule.DisplayStartH = startTime.Hours;
            schedule.DisplayStartM = startTime.Minutes;
            schedule.DisplayEndH = endTime.Hours;
            schedule.DisplayEndM = endTime.Minutes;

            if (days != null && days.Length > 6)
            {
                schedule.DayOfWeek2 = days[0];
                schedule.DayOfWeek3 = days[1];
                schedule.DayOfWeek4 = days[2];
                schedule.DayOfWeek5 = days[3];
                schedule.DayOfWeek6 = days[4];
                schedule.DayOfWeek7 = days[5];
                schedule.DayOfWeek1 = days[6];
            }

            return schedule;
        }

        private void SendScheduleUpdates(IEnumerable<string> players)
        {
            if (players == null)
                return;

            foreach (string player in players.Distinct(StringComparer.CurrentCultureIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(player))
                    continue;

                var playerInfo = DataShop.Instance.g_PlayerInfoManager.GetPlayerInfoByName(player);
                if (playerInfo != null)
                {
                    MainWindow.Instance.EnqueueCommandForPlayer(playerInfo, RP_ORDER.updateschedule.ToString(), pushSignalR: true);
                }
            }
        }

        private void StartDate_CalendarOpened(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(StartDatePicker.Text))
                StartDatePicker.Text = DateTime.Now.ToString("yyyy-MM-dd");
        }

        private void EndDate_CalendarOpened(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(EndDatePicker.Text))
                EndDatePicker.Text = DateTime.Now.ToString("yyyy-MM-dd");
        }

        private void StartDatePicker_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            if (EndDatePicker.SelectedDate == null || StartDatePicker.SelectedDate == null)
                return;

            int _compare_int = ((DateTime)StartDatePicker.SelectedDate).CompareTo((DateTime)EndDatePicker.SelectedDate);

            if (_compare_int > 0)
            {
                //MainWindow.Instance.ShowSnackbar("시작 날짜는 종료 날짜보다 클 수 없습니다.", null, SymbolRegular.ErrorCircle24);
                MainWindow.Instance.EnqueueSnackMsg("시작 날짜는 종료 날짜보다 클 수 없습니다.");
                EndDatePicker.SelectedDate = StartDatePicker.SelectedDate;
            }
            else if (_compare_int == 0)
            {
                CheckSelectedTime();
            }
        }

        private void EndDatePicker_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            if (StartDatePicker.SelectedDate == null || EndDatePicker.SelectedDate == null)
                return;

            int _compare_int = ((DateTime)EndDatePicker.SelectedDate).CompareTo((DateTime)StartDatePicker.SelectedDate);

            if (_compare_int < 0)
            {
                //MainWindow.Instance.ShowSnackbar("종료 날짜는 시작 날짜보다 작을 수 없습니다.", null, SymbolRegular.ErrorCircle24);
                MainWindow.Instance.EnqueueSnackMsg("종료 날짜는 시작 날짜보다 작을 수 없습니다.");
                StartDatePicker.SelectedDate = EndDatePicker.SelectedDate;
            }
            else if (_compare_int == 0)
            {
                CheckSelectedTime();
            }
        }

        private void CheckSelectedTime()
        {
            if (StartDatePicker.SelectedDate == null || EndDatePicker.SelectedDate == null)
                return;

            if (StartTimePicker.SelectedTime == null || EndTimePicker.SelectedTime == null)
                return;

            DateTime startDateTime = StartDatePicker.SelectedDate.Value.Date.Add(StartTimePicker.SelectedTime.Value.TimeOfDay);
            DateTime endDateTime = EndDatePicker.SelectedDate.Value.Date.Add(EndTimePicker.SelectedTime.Value.TimeOfDay);

            if (endDateTime < startDateTime)
                EndDatePicker.SelectedDate = StartDatePicker.SelectedDate.Value.Date.AddDays(1);
        }

        private void StartTimePicker_SelectedTimeChanged(object sender, RoutedPropertyChangedEventArgs<DateTime?> e)
        {
            if (EndTimePicker.SelectedTime == null || StartTimePicker.SelectedTime == null)
                return;

            CheckSelectedTime();
        }

        private void EndTimePicker_SelectedTimeChanged(object sender, RoutedPropertyChangedEventArgs<DateTime?> e)
        {
            if (StartTimePicker.SelectedTime == null || EndTimePicker.SelectedTime == null)
                return;

            CheckSelectedTime();
        }

        private void SelectAllChBox_Checked(object sender, RoutedEventArgs e)
        {
            SelectAllTargets(TargetListBox, true);
        }

        private void SelectAllChBox_Unchecked(object sender, RoutedEventArgs e)
        {
            SelectAllTargets(TargetListBox, false);
        }

        private void SelectAllPlayerChBox_Checked(object sender, RoutedEventArgs e)
        {
            SelectAllTargets(TargetPlayerListBox, true);
        }

        private void SelectAllPlayerChBox_Unchecked(object sender, RoutedEventArgs e)
        {
            SelectAllTargets(TargetPlayerListBox, false);
        }

        void SelectAllTargets(ListBox listBox, bool select = true)
        {
            if (listBox == null)
                return;

            foreach (ListBoxItem item in listBox.Items.OfType<ListBoxItem>())
            {
                if (item.Visibility != Visibility.Visible)
                    continue;

                item.IsSelected = select;
            }
        }

        private void TargetListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            SelectionChanged();
        }

        private void TargetPlayerListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            SelectionChanged();
        }

        internal void SelectionChanged()
        {
            UpdateSelectAllState(TargetListBox, SelectAllChBox);
            UpdateSelectAllState(TargetPlayerListBox, SelectAllPlayerChBox);
        }

        private void GroupFilterTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplyTargetFilters();
        }

        private void PlayerFilterTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplyTargetFilters();
        }

        private void TargetToggle_Changed(object sender, RoutedEventArgs e)
        {
            ShowTargetPanel(GroupRadio.IsChecked == true);
        }

        private void ShowTargetPanel(bool showGroup)
        {
            if (GroupSelectBox != null)
                GroupSelectBox.Visibility = showGroup ? Visibility.Visible : Visibility.Collapsed;

            if (PlayerSelectBox != null)
                PlayerSelectBox.Visibility = showGroup ? Visibility.Collapsed : Visibility.Visible;

            if (TargetSelectBox != null)
                TargetSelectBox.Header = showGroup ? "그룹 선택" : "개별 선택";
        }

        private void UpdateSelectAllState(ListBox listBox, CheckBox checkBox)
        {
            if (listBox == null || checkBox == null)
                return;

            List<ListBoxItem> visibleItems = listBox.Items.OfType<ListBoxItem>()
                .Where(item => item.Visibility == Visibility.Visible)
                .ToList();
            int selectedCount = visibleItems.Where(item => item.IsSelected).Count();
            int itemCount = visibleItems.Count;

            if (selectedCount == 0)
                checkBox.IsChecked = false;
            else
            {
                bool isAllSelected = (selectedCount > 0 && itemCount == selectedCount);
                if (isAllSelected == false)
                    checkBox.IsChecked = null;
                else
                    checkBox.IsChecked = isAllSelected;
            }
        }

        private void ApplyTargetFilters()
        {
            ApplyTargetFilter(TargetListBox, GroupFilterTextBox.Text);
            ApplyTargetFilter(TargetPlayerListBox, PlayerFilterTextBox.Text);
            SelectionChanged();
        }

        private void ApplyTargetFilter(ListBox listBox, string filterText)
        {
            if (listBox == null)
                return;

            string keyword = (filterText ?? string.Empty).Trim();
            bool hasFilter = string.IsNullOrWhiteSpace(keyword) == false;

            foreach (ListBoxItem item in listBox.Items.OfType<ListBoxItem>())
            {
                if (item == null)
                    continue;

                if (hasFilter == false)
                {
                    item.Visibility = Visibility.Visible;
                    continue;
                }

                string content = item.Content as string ?? string.Empty;
                if (content.IndexOf(keyword, StringComparison.CurrentCultureIgnoreCase) >= 0)
                    item.Visibility = Visibility.Visible;
                else
                    item.Visibility = Visibility.Collapsed;
            }
        }

        private List<string> GetSelectedNames(ListBox listBox)
        {
            if (listBox == null)
                return new List<string>();

            return listBox.SelectedItems
                .Cast<ListBoxItem>()
                .Select(p => p.Content as string)
                .Where(x => string.IsNullOrWhiteSpace(x) == false)
                .ToList();
        }

        private List<SpecialScheduleInfoClass> GetTargetSchedules(IEnumerable<string> selectedGroups, IEnumerable<string> selectedPlayers, bool isGroupMode)
        {
            HashSet<string> groupSet = new HashSet<string>(selectedGroups ?? new List<string>(), StringComparer.CurrentCultureIgnoreCase);
            HashSet<string> playerSet = new HashSet<string>(selectedPlayers ?? new List<string>(), StringComparer.CurrentCultureIgnoreCase);
            HashSet<string> scheduleIds = new HashSet<string>(StringComparer.CurrentCultureIgnoreCase);
            List<SpecialScheduleInfoClass> targets = new List<SpecialScheduleInfoClass>();

            if (isGroupMode && groupSet.Count > 0)
            {
                foreach (SpecialScheduleViewData data in sSameData)
                {
                    if (data == null)
                        continue;

                    foreach (SpecialScheduleInfoClass schedule in data.Schedules)
                    {
                        if (schedule == null || schedule.GroupNames == null || schedule.GroupNames.Count < 1)
                            continue;

                        bool hasGroup = schedule.GroupNames.Any(group => groupSet.Contains(group));
                        if (hasGroup == false)
                            continue;

                        AddTargetSchedule(targets, scheduleIds, schedule);
                    }
                }
            }

            if (isGroupMode == false && playerSet.Count > 0)
            {
                foreach (SpecialScheduleViewData data in sSameData)
                {
                    if (data == null)
                        continue;

                    foreach (SpecialScheduleInfoClass schedule in data.Schedules)
                    {
                        if (schedule == null || schedule.PlayerNames == null || schedule.PlayerNames.Count < 1)
                            continue;

                        bool hasPlayer = schedule.PlayerNames.Any(player => playerSet.Contains(player));
                        if (hasPlayer == false)
                            continue;

                        AddTargetSchedule(targets, scheduleIds, schedule);
                    }
                }
            }

            return targets;
        }

        private void AddTargetSchedule(List<SpecialScheduleInfoClass> targets, HashSet<string> scheduleIds, SpecialScheduleInfoClass schedule)
        {
            if (schedule == null)
                return;

            string guid = schedule.GUID ?? string.Empty;
            if (string.IsNullOrWhiteSpace(guid) == false)
            {
                if (scheduleIds.Add(guid) == false)
                    return;
            }

            targets.Add(schedule);
        }

        private void BtnWin_close_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private bool SelectListBoxItem(ListBox listBox, string value)
        {
            if (listBox == null || string.IsNullOrWhiteSpace(value))
                return false;

            ListBoxItem item = listBox.Items
                .Cast<ListBoxItem>()
                .FirstOrDefault(x => string.Equals(x.Content as string, value, StringComparison.CurrentCultureIgnoreCase));
            if (item == null)
                return false;

            item.IsSelected = true;
            return true;
        }

        private bool SelectListBoxItems(ListBox listBox, IEnumerable<string> values)
        {
            if (listBox == null || values == null)
                return false;

            HashSet<string> valueSet = new HashSet<string>(
                values.Where(x => string.IsNullOrWhiteSpace(x) == false),
                StringComparer.CurrentCultureIgnoreCase);
            if (valueSet.Count < 1)
                return false;

            bool selected = false;
            foreach (ListBoxItem item in listBox.Items.Cast<ListBoxItem>())
            {
                if (item == null)
                    continue;

                string content = item.Content as string ?? string.Empty;
                if (valueSet.Contains(content) == false)
                    continue;

                item.IsSelected = true;
                selected = true;
            }

            return selected;
        }

        private void SelectPlaylistCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            MainWindow.Instance?.SetDimOverlay(false);
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            MainWindow.Instance?.SetDimOverlay(true);
        }
    }
}
