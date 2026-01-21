using RethinkDb.Driver.Ast;
using RethinkDb.Driver.Model;
using AndoW_Manager.SubWindow;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices.ComTypes;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using TurtleTools;

namespace AndoW_Manager
{
    /// <summary>
    /// Page5.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class Page5 : UserControl
    {
        public static Page5 Instance { get; private set; }

        internal readonly ObservableCollection<SpecialCtrl> sSpecialControls = new ObservableCollection<SpecialCtrl>();
        public Dictionary<string, SpecialScheduleInfoClass> sSpecialDics = new Dictionary<string, SpecialScheduleInfoClass>(StringComparer.CurrentCultureIgnoreCase);
        Queue<CancellationTokenSource> sCTSs = new Queue<CancellationTokenSource>();

        public Page5()
        {
            InitializeComponent();
            Instance = this;
            SpecialItemsControl.ItemsSource = sSpecialControls;
        }


        public void LoadAllSpecials()
        {
            try
            {
                ShowProgress();

                sSpecialControls.Clear();
                sSpecialDics.Clear();

                RefreshGroupSources();

                List<SpecialScheduleInfoClass> schedules = LoadAllSchedules();
                List<SpecialScheduleViewData> viewItems = BuildSpecialViewItems(schedules);

                foreach (SpecialScheduleViewData viewItem in viewItems)
                {
                    SpecialCtrl ctrl = new SpecialCtrl(viewItem);
                    sSpecialControls.Add(ctrl);
                }
            }
            catch (Exception e) { }
            finally
            {
                ShowProgress(false);
            }
        }


        public void UpdatePlaylistCombo()
        {
            SelectPlaylistCombo.Items.Clear();
            SearchListNameCombo.Items.Clear();
            SearchListNameCombo.Items.Add("All");

            foreach (string listname in DataShop.Instance.g_PageListInfoManager.g_PageListInfoClassList.Select(x => x.PLI_PageListName))
            {
                SelectPlaylistCombo.Items.Add(listname);
                SearchListNameCombo.Items.Add(listname);
            }

            SearchListNameCombo.SelectedIndex = 0;
        }

        public void SetTodayValues()
        {
            DateTime _dt = DateTime.Now;

            string _now = _dt.ToString("yyyy-MM-dd HH:mm");

            StartDatePicker.SelectedDate = _dt.Date;
            EndDatePicker.SelectedDate = _dt.Date;

            StartTimePicker.SelectedTime = DateTime.ParseExact(_now, "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
            
            DateTime _dt2 = DateTime.ParseExact(_now, "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture).AddHours(1);
            EndTimePicker.SelectedTime = _dt2;

            if (_dt.Date.CompareTo(_dt2.Date) != 0)
                EndDatePicker.SelectedDate = ((DateTime)EndDatePicker.SelectedDate).AddDays(1);
        }

        public void AutoDefaultScheduleUpdate(string playlist)
        {
            if (string.IsNullOrWhiteSpace(playlist))
                return;

            List<string> _players = DataShop.Instance.g_PlayerInfoManager.g_PlayerInfoClassList
                .Where(x => x != null && x.PIF_CurrentPlayList.Equals(playlist, StringComparison.CurrentCultureIgnoreCase))
                .Select(x => x.PIF_PlayerName)
                .ToList();

            foreach (string player in _players)
            {
                var playerInfo = DataShop.Instance.g_PlayerInfoManager.GetPlayerInfoByName(player);
                if (playerInfo == null)
                {
                    continue;
                }

                string payloadBase64 = DataShop.Instance.g_UpdatePayloadBuilder.BuildPayloadBase64(playerInfo);
                MainWindow.Instance.EnqueueCommandForPlayer(playerInfo, RP_ORDER.updatelist.ToString(), payloadBase64, pushSignalR: true);
            }
        }

        private void AddSchduleBtn_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            bool isGroupMode = GroupRadio.IsChecked == true;
            List<string> _selected_groups = isGroupMode ? GetSelectedNames(TargetListBox) : new List<string>();
            List<string> _selected_players = isGroupMode ? new List<string>() : GetSelectedNames(TargetPlayerListBox);

            if (_selected_groups.Count < 1 && _selected_players.Count < 1)
            {
                MainWindow.Instance.EnqueueSnackMsg("그룹 또는 플레이어를 선택해주세요.");
                return;
            }

            if (SelectPlaylistCombo.SelectedItem == null)
            {
                MainWindow.Instance.EnqueueSnackMsg("플레이 리스트를 선택해주세요.");
                return;
            }

            if (isGroupMode)
            {
                List<string> _target_players = GetPlayersFromSelection(_selected_groups, null);
                if (_target_players.Count < 1)
                {
                    MainWindow.Instance.EnqueueSnackMsg("선택한 대상에 등록된 플레이어가 없습니다.");
                    return;
                }
            }

            string _playlist = SelectPlaylistCombo.SelectedItem.ToString();

            //if ((bool)SpecialSchBtn.IsChecked)
            //{
                DateTime? _start_dt = BuildDateTime(StartDatePicker.SelectedDate, StartTimePicker.SelectedTime);
                DateTime? _end_dt = BuildDateTime(EndDatePicker.SelectedDate, EndTimePicker.SelectedTime);
                if (_start_dt == null || _end_dt == null)
                {
                    MainWindow.Instance.EnqueueSnackMsg("날짜와 시간을 확인해주세요.");
                    return;
                }

                if (_end_dt.Value < _start_dt.Value)
                    _end_dt = _end_dt.Value.AddDays(1);

                if (DateTime.Now.CompareTo(_end_dt.Value) > 0)
                {
                    MainWindow.Instance.EnqueueSnackMsg("현재 시간보다 이전의 스케줄은 등록이 불가합니다.");
                    return;
                }

                if (TryGetScheduleInput(out DateTime _start_date, out DateTime _end_date, out TimeSpan _start_time, out TimeSpan _end_time, out bool[] _days) == false)
                {
                    MainWindow.Instance.EnqueueSnackMsg("날짜와 시간을 확인해주세요.");
                    return;
                }

                bool _result = AddSpecialSchedule(_selected_groups, _selected_players, isGroupMode, _playlist, _start_date, _end_date, _start_time, _end_time, _days);
                if (_result)
                    MainWindow.Instance.EnqueueSnackMsg("특별 스케줄을 모두 설정하였습니다.");
                else
                    MainWindow.Instance.EnqueueSnackMsg("중복되는 스케줄을 제외한 특별 스케줄을 설정하였습니다.");
            //} 
            //else
            //{
            //    ChangeDefaultSchedule(_target_players, _playlist);
            //    MainWindow.Instance.EnqueueSnackMsg("기본 스케줄을 설정하였습니다.");
            //}
        }

        private void ChangeDefaultSchedule(List<string> players, string playlist)
        {
            foreach (string player in players)
            {
                DataShop.Instance.g_PlayerInfoManager.EditPlayerCurrentPlayList(player, playlist);
                var playerInfo = DataShop.Instance.g_PlayerInfoManager.GetPlayerInfoByName(player);
                if (playerInfo == null)
                {
                    continue;
                }

                string payloadBase64 = DataShop.Instance.g_UpdatePayloadBuilder.BuildPayloadBase64(playerInfo);
                MainWindow.Instance.EnqueueCommandForPlayer(playerInfo, RP_ORDER.updatelist.ToString(), payloadBase64, pushSignalR: true);
            }
        }

        private bool AddSpecialSchedule(List<string> selectedGroups, List<string> selectedPlayers, bool isGroupMode,
            string playlist, DateTime startDate, DateTime endDate, TimeSpan startTime, TimeSpan endTime, bool[] days)
        {
            SpecialScheduleInfoManager manager = new SpecialScheduleInfoManager();
            manager.LoadAllSchedules();

            List<SpecialScheduleInfoClass> existing = manager.g_SpecialScheduleInfoClassList ?? new List<SpecialScheduleInfoClass>();
            HashSet<string> updatedPlayers = new HashSet<string>(StringComparer.CurrentCultureIgnoreCase);
            bool addedAll = true;
            bool addedAny = false;

            if (isGroupMode)
            {
                HashSet<string> uniqueGroups = new HashSet<string>(StringComparer.CurrentCultureIgnoreCase);
                List<string> groupList = new List<string>();
                foreach (string groupName in selectedGroups ?? new List<string>())
                {
                    if (string.IsNullOrWhiteSpace(groupName))
                        continue;

                    if (uniqueGroups.Add(groupName))
                        groupList.Add(groupName);
                }

                if (groupList.Count < 1)
                    return false;

                bool hasEmptyGroup = false;
                HashSet<string> groupPlayerSet = new HashSet<string>(StringComparer.CurrentCultureIgnoreCase);
                List<string> groupPlayers = new List<string>();
                foreach (string groupName in groupList)
                {
                    List<string> groupPlayerList = GetPlayersForGroup(groupName);
                    if (groupPlayerList.Count < 1)
                    {
                        hasEmptyGroup = true;
                        continue;
                    }

                    foreach (string player in groupPlayerList)
                    {
                        if (string.IsNullOrWhiteSpace(player))
                            continue;

                        if (groupPlayerSet.Add(player))
                            groupPlayers.Add(player);
                    }
                }

                if (hasEmptyGroup || groupPlayers.Count < 1)
                    addedAll = false;

                SpecialScheduleInfoClass newSchedule = BuildScheduleInfo(playlist, startDate, endDate, startTime, endTime, days);
                newSchedule.GroupNames = new List<string>(groupList);

                List<string> availablePlayers = FilterOverlappedPlayers(existing, groupPlayers, newSchedule, null);
                if (availablePlayers.Count < groupPlayers.Count)
                    addedAll = false;

                if (availablePlayers.Count > 0)
                {
                    newSchedule.PlayerNames = availablePlayers;
                    manager.AddSpecialScheduleInfoClass(newSchedule, string.Empty);
                    existing.Add(newSchedule);
                    addedAny = true;

                    foreach (string player in availablePlayers)
                        updatedPlayers.Add(player);
                }
                else
                {
                    addedAll = false;
                }
            }
            else
            {
                HashSet<string> uniquePlayers = new HashSet<string>(StringComparer.CurrentCultureIgnoreCase);
                List<string> playerList = new List<string>();
                foreach (string player in selectedPlayers ?? new List<string>())
                {
                    if (string.IsNullOrWhiteSpace(player))
                        continue;

                    if (uniquePlayers.Add(player))
                        playerList.Add(player);
                }

                if (playerList.Count < 1)
                    return false;

                SpecialScheduleInfoClass newSchedule = BuildScheduleInfo(playlist, startDate, endDate, startTime, endTime, days);
                List<string> availablePlayers = FilterOverlappedPlayers(existing, playerList, newSchedule, null);
                if (availablePlayers.Count < playerList.Count)
                    addedAll = false;

                if (availablePlayers.Count > 0)
                {
                    newSchedule.PlayerNames = availablePlayers;
                    manager.AddSpecialScheduleInfoClass(newSchedule, string.Empty);
                    existing.Add(newSchedule);
                    addedAny = true;

                    foreach (string player in availablePlayers)
                        updatedPlayers.Add(player);
                }
                else
                {
                    addedAll = false;
                }
            }

            if (addedAny)
                SendScheduleUpdates(updatedPlayers);

            ReloadSpecials();

            return addedAll;
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
            } else if(_compare_int == 0)
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

        //private void RadioButton_Checked(object sender, System.Windows.RoutedEventArgs e)
        //{
        //    if(IsLoaded)
        //        ShowSpecialStack((bool)SpecialSchBtn.IsChecked);
        //}

        public void ShowSpecialStack(bool isShow = true)
        {
            try
            {
                SpecialStack.Visibility = isShow ? Visibility.Visible : Visibility.Collapsed;

                //if(isShow)
                //{
                //    AddSpecialBtnStack.Visibility = SpecialStack.Visibility = Visibility.Visible;
                //    AddDefaultSchduleBtn.Visibility = Visibility.Collapsed;
                //} else
                //{
                //    AddSpecialBtnStack.Visibility = SpecialStack.Visibility = Visibility.Collapsed;
                //    AddDefaultSchduleBtn.Visibility = Visibility.Visible;
                //}
            }
            catch { }
        }

        private void SearchGroupNameCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            SortSpecialControls();
        }

        private void SearchPlayerNameCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            SortSpecialControls();
        }

        private void SearchListNameCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            SortSpecialControls();
        }

        private void SearchDatePicker_CalendarOpened(object sender, System.Windows.RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(SearchDatePicker.Text))
                SearchDatePicker.Text = DateTime.Now.ToString("yyyy-MM-dd");
        }

        private void SearchDatePicker_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            SortSpecialControls();
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
            ShowTargetPanel((bool)GroupRadio.IsChecked);
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

        private void ShowTargetPanel(bool showGroup)
        {
            if (GroupSelectBox != null)
                GroupSelectBox.Visibility = showGroup ? Visibility.Visible : Visibility.Collapsed;

            if (PlayerSelectBox != null)
                PlayerSelectBox.Visibility = showGroup ? Visibility.Collapsed : Visibility.Visible;

            if(TargetSelectBox != null)
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

        private void InitTargetListBox()
        {
            RefreshGroupSources();
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

        private void SelectPlaylistCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }

        void SortSpecialControls()
        {
            CancellationTokenSource _cts;

            if (sCTSs.Count > 0)
            {
                try
                {
                    _cts = sCTSs.Dequeue();
                    _cts.Cancel();
                }
                catch (Exception ex) { }
            }

            CancellationTokenSource _new_cts = new CancellationTokenSource();
            sCTSs.Enqueue(_new_cts);

            ThreadPool.QueueUserWorkItem(new WaitCallback(delegate (object state)
            {
                this.Dispatcher.Invoke(DispatcherPriority.Background, new Action(() =>
                {
                    ApplySpecialFilters(SearchDatePicker.SelectedDate, SearchGroupNameCombo.SelectedItem as string, SearchListNameCombo.SelectedItem as string, SearchPlayerNameCombo.SelectedItem as string);

                    WindowTools.DoEvents();

                    _new_cts.Dispose();
                    _new_cts = null;
                }));
            }), _new_cts);
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            InitTargetListBox();
            UpdatePlaylistCombo();
            ShowTargetPanel(GroupRadio.IsChecked == true);

            if (SpecialItemsControl.Items.Count < 1)
            {
                LoadAllSpecials();
                SortSpecialControls();

                SelectAllSchChBox.IsChecked = false;
            }
        }

        private void UserControl_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (IsVisible)
                SetTodayValues();
        }

        private void SelectAllSchChBox_Checked(object sender, RoutedEventArgs e)
        {
            SelectAll(true);
        }

        private void SelectAllSchChBox_Unchecked(object sender, RoutedEventArgs e)
        {
            SelectAll(false);
        }

        public void SelectAll(bool select = true)
        {
            foreach (SpecialCtrl sc in SpecialItemsControl.Items.OfType<SpecialCtrl>().Where(item => item.IsVisible))
                sc.SelectChBox.IsChecked = select;
        }

        private void SpecialItemsControl_SizeChanged(object sender, SizeChangedEventArgs e)
        {
        }

        internal void ScheduleSelectionChanged()
        {
            int _sel_cnt = SpecialItemsControl.Items.OfType<SpecialCtrl>().Where(item => (bool)item.SelectChBox.IsChecked).Count();
            int _vis_cnt = SpecialItemsControl.Items.OfType<SpecialCtrl>().Where(item => item.IsVisible).Count();

            if (_sel_cnt == 0)
                SelectAllSchChBox.IsChecked = false;
            else
            {
                bool _state = (_sel_cnt > 0 && _vis_cnt == _sel_cnt);
                if (_state == false)
                    SelectAllSchChBox.IsChecked = null;
                else
                    SelectAllSchChBox.IsChecked = _state;
            }
        }

        internal void ShowProgress(bool show = true)
        {
            if (show)
            {
                ProgressGrid.Visibility = Visibility.Visible;
                ProgressBar.IsIndeterminate = true;
            }
            else
            {
                 ProgressGrid.Visibility = Visibility.Collapsed;
                 ProgressBar.IsIndeterminate = false;
            }
        }

        internal void UpdateSpecialItems(Change<SpecialScheduleInfoClass> data)
        {
            ReloadSpecials();
        }

        private void DeleteOldDataBtn_Click(object sender, RoutedEventArgs e)
        {
            ShowProgress();
            try
            {
                List<SpecialScheduleInfoClass> expired = sSpecialDics.Values
                    .Where(x => IsExpiredSchedule(x))
                    .ToList();

                DeleteSchedules(expired);
            }
            catch { }

            ShowProgress(false);
        }

        private void SelectedDelBtn_Click(object sender, RoutedEventArgs e)
        {
            ShowProgress();

            try
            {
                if (SelectAllSchChBox.IsChecked != null && (bool)SelectAllSchChBox.IsChecked)
                {
                    DeleteAllSchedules();
                }
                else
                {
                    List<SpecialScheduleViewData> selectedItems = SpecialItemsControl.Items
                        .OfType<SpecialCtrl>()
                        .Where(item => item.IsVisible && item.SelectChBox.IsChecked == true)
                        .Select(sel => sel.sSS)
                        .Where(x => x != null)
                        .ToList();

                    DeleteSchedules(selectedItems);
                }

                SelectAllSchChBox.IsChecked = false;

            }
            catch (Exception ex) { }

            ShowProgress(false);
        }

        internal void SetSelectedData(SpecialScheduleViewData ss)
        {
            SelectPlaylistCombo.SelectedItem = ss.Playlist;
            if (ss.StartDate != null)
                StartDatePicker.SelectedDate = ss.StartDate.Value.Date;
            if (ss.EndDate != null)
                EndDatePicker.SelectedDate = ss.EndDate.Value.Date;
            if (ss.StartTime != null)
                StartTimePicker.SelectedTime = DateTime.Today.Add(ss.StartTime.Value);
            if (ss.EndTime != null)
                EndTimePicker.SelectedTime = DateTime.Today.Add(ss.EndTime.Value);
        }

        internal void OpenEditSpecialWindow(SpecialScheduleViewData viewData)
        {
            if (viewData == null)
                return;

            List<SpecialScheduleViewData> sameGroups = GetSameScheduleGroups(viewData);
            EditSpecialWindow window = new EditSpecialWindow(viewData, sameGroups);
            bool? result = window.ShowDialog();
            if (result == true)
            {
                ReloadSpecials();
            }
        }

        internal void DeleteSchedules(SpecialScheduleViewData viewData)
        {
            if (viewData == null)
                return;

            DeleteSchedules(new List<SpecialScheduleViewData> { viewData });
        }

        private void DeleteSchedules(IEnumerable<SpecialScheduleViewData> viewItems)
        {
            if (viewItems == null)
                return;

            List<SpecialScheduleInfoClass> schedules = viewItems
                .Where(x => x != null)
                .SelectMany(x => x.Schedules)
                .Where(x => x != null)
                .ToList();

            DeleteSchedules(schedules);
        }

        private void DeleteSchedules(IEnumerable<SpecialScheduleInfoClass> schedules)
        {
            if (schedules == null)
                return;

            List<SpecialScheduleInfoClass> scheduleList = schedules
                .Where(x => x != null)
                .ToList();

            if (scheduleList.Count == 0)
                return;

            SpecialScheduleInfoManager manager = new SpecialScheduleInfoManager();
            HashSet<string> updatedPlayers = new HashSet<string>(StringComparer.CurrentCultureIgnoreCase);

            foreach (SpecialScheduleInfoClass schedule in scheduleList)
            {
                if (schedule == null)
                    continue;

                if (schedule.PlayerNames != null)
                {
                    foreach (string player in schedule.PlayerNames)
                    {
                        if (string.IsNullOrWhiteSpace(player) == false)
                            updatedPlayers.Add(player);
                    }
                }

                manager.DeleteScheduleInfoClass(schedule, string.Empty);
            }

            SendScheduleUpdates(updatedPlayers);
            ReloadSpecials();
        }

        private void DeleteAllSchedules()
        {
            SpecialScheduleInfoManager manager = new SpecialScheduleInfoManager();
            HashSet<string> updatedPlayers = new HashSet<string>(StringComparer.CurrentCultureIgnoreCase);

            manager.LoadAllSchedules();
            List<SpecialScheduleInfoClass> schedules = manager.g_SpecialScheduleInfoClassList
                .Where(x => x != null)
                .ToList();

            foreach (SpecialScheduleInfoClass schedule in schedules)
            {
                if (schedule.PlayerNames != null)
                {
                    foreach (string player in schedule.PlayerNames)
                    {
                        if (string.IsNullOrWhiteSpace(player) == false)
                            updatedPlayers.Add(player);
                    }
                }

                manager.DeleteScheduleInfoClass(schedule, string.Empty);
            }

            SendScheduleUpdates(updatedPlayers);
            ReloadSpecials();
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
                if (playerInfo == null)
                {
                    continue;
                }

                MainWindow.Instance.EnqueueCommandForPlayer(playerInfo, RP_ORDER.updateschedule.ToString(), pushSignalR: true);
            }
        }

        private void ReloadSpecials()
        {
            LoadAllSpecials();
            SortSpecialControls();
        }

        private void RefreshGroupSources()
        {
            TargetListBox.Items.Clear();
            TargetPlayerListBox.Items.Clear();
            SearchGroupNameCombo.Items.Clear();
            SearchGroupNameCombo.Items.Add("All");
            SearchPlayerNameCombo.Items.Clear();
            SearchPlayerNameCombo.Items.Add("All");

            List<string> groupNames = BuildGroupOnlyNameList();
            foreach (string groupName in groupNames)
            {
                ListBoxItem item = new ListBoxItem();
                item.Content = groupName;
                TargetListBox.Items.Add(item);
            }

            List<string> playerNames = BuildPlayerNameList();
            foreach (string playerName in playerNames)
            {
                ListBoxItem item = new ListBoxItem();
                item.Content = playerName;
                TargetPlayerListBox.Items.Add(item);
            }

            foreach (string playerName in playerNames)
            {
                SearchPlayerNameCombo.Items.Add(playerName);
            }

            List<string> searchGroupNames = BuildGroupNameList();
            foreach (string groupName in searchGroupNames)
            {
                SearchGroupNameCombo.Items.Add(groupName);
            }

            SelectAllChBox.IsChecked = false;
            SelectAllPlayerChBox.IsChecked = false;
            SearchGroupNameCombo.SelectedIndex = 0;
            SearchPlayerNameCombo.SelectedIndex = 0;
            ApplyTargetFilters();
        }

        private List<string> BuildGroupOnlyNameList()
        {
            DataShop.Instance.g_PlayerGroupManager.LoadDataFromDatabase();

            HashSet<string> groupNames = new HashSet<string>(StringComparer.CurrentCultureIgnoreCase);
            foreach (PlayerGroupClass group in DataShop.Instance.g_PlayerGroupManager.g_PlayerGroupClassList)
            {
                if (group == null)
                    continue;

                if (string.IsNullOrWhiteSpace(group.PG_GroupName) == false)
                    groupNames.Add(group.PG_GroupName);
            }

            return groupNames.OrderBy(x => x, StringComparer.CurrentCultureIgnoreCase).ToList();
        }

        private List<string> BuildPlayerNameList()
        {
            HashSet<string> usedNames = new HashSet<string>(StringComparer.CurrentCultureIgnoreCase);
            List<string> playerNames = new List<string>();

            foreach (PlayerInfoClass player in DataShop.Instance.g_PlayerInfoManager.GetOrderedPlayers())
            {
                if (player == null || string.IsNullOrWhiteSpace(player.PIF_PlayerName))
                    continue;

                if (usedNames.Add(player.PIF_PlayerName))
                    playerNames.Add(player.PIF_PlayerName);
            }

            return playerNames;
        }

        private List<string> BuildGroupNameList()
        {
            DataShop.Instance.g_PlayerGroupManager.LoadDataFromDatabase();

            HashSet<string> groupNames = new HashSet<string>(StringComparer.CurrentCultureIgnoreCase);

            foreach (PlayerGroupClass group in DataShop.Instance.g_PlayerGroupManager.g_PlayerGroupClassList)
            {
                if (group == null)
                    continue;

                if (string.IsNullOrWhiteSpace(group.PG_GroupName) == false)
                    groupNames.Add(group.PG_GroupName);
            }

            return groupNames.OrderBy(x => x, StringComparer.CurrentCultureIgnoreCase).ToList();
        }

        private List<string> GetPlayersFromSelection(IEnumerable<string> selectedGroupNames, IEnumerable<string> selectedPlayerNames)
        {
            HashSet<string> players = new HashSet<string>(StringComparer.CurrentCultureIgnoreCase);

            DataShop.Instance.g_PlayerGroupManager.LoadDataFromDatabase();

            if (selectedGroupNames != null)
            {
                foreach (string groupName in selectedGroupNames)
                {
                    if (string.IsNullOrWhiteSpace(groupName))
                        continue;

                    PlayerGroupClass group = DataShop.Instance.g_PlayerGroupManager.GetGroupByName(groupName);
                    if (group == null || group.PG_AssignedPlayerNames == null || group.PG_AssignedPlayerNames.Count < 1)
                        continue;

                    foreach (string player in group.PG_AssignedPlayerNames)
                    {
                        if (string.IsNullOrWhiteSpace(player) == false)
                            players.Add(player);
                    }
                }
            }

            if (selectedPlayerNames != null)
            {
                foreach (string player in selectedPlayerNames)
                {
                    if (string.IsNullOrWhiteSpace(player) == false)
                        players.Add(player);
                }
            }

            return players.ToList();
        }

        private List<string> GetPlayersForGroup(string groupName)
        {
            List<string> players = new List<string>();
            if (string.IsNullOrWhiteSpace(groupName))
                return players;

            DataShop.Instance.g_PlayerGroupManager.LoadDataFromDatabase();

            PlayerGroupClass group = DataShop.Instance.g_PlayerGroupManager.GetGroupByName(groupName);
            if (group == null || group.PG_AssignedPlayerNames == null || group.PG_AssignedPlayerNames.Count < 1)
                return players;

            HashSet<string> dedupe = new HashSet<string>(StringComparer.CurrentCultureIgnoreCase);
            foreach (string player in group.PG_AssignedPlayerNames)
            {
                if (string.IsNullOrWhiteSpace(player))
                    continue;

                if (dedupe.Add(player))
                    players.Add(player);
            }

            return players;
        }

        private static List<string> FilterOverlappedPlayers(IEnumerable<SpecialScheduleInfoClass> schedules, IEnumerable<string> players,
            SpecialScheduleInfoClass candidate, string excludeGuid)
        {
            List<string> availablePlayers = new List<string>();
            if (players == null || candidate == null)
                return availablePlayers;

            HashSet<string> dedupe = new HashSet<string>(StringComparer.CurrentCultureIgnoreCase);
            foreach (string player in players)
            {
                if (string.IsNullOrWhiteSpace(player))
                    continue;

                if (dedupe.Add(player) == false)
                    continue;

                if (HasOverlappingSchedule(schedules, player, candidate, excludeGuid))
                    continue;

                availablePlayers.Add(player);
            }

            return availablePlayers;
        }

        private static bool HasOverlappingSchedule(IEnumerable<SpecialScheduleInfoClass> schedules, string playerName,
            SpecialScheduleInfoClass candidate, string excludeGuid)
        {
            if (schedules == null || candidate == null || string.IsNullOrWhiteSpace(playerName))
                return false;

            foreach (SpecialScheduleInfoClass schedule in schedules)
            {
                if (schedule == null)
                    continue;

                if (string.IsNullOrWhiteSpace(excludeGuid) == false &&
                    string.Equals(schedule.GUID, excludeGuid, StringComparison.CurrentCultureIgnoreCase))
                    continue;

                if (IsPlayerInSchedule(schedule, playerName) == false)
                    continue;

                if (schedule.CheckOverlappedPeriod(candidate))
                    return true;
            }

            return false;
        }

        private static bool IsPlayerInSchedule(SpecialScheduleInfoClass schedule, string playerName)
        {
            if (schedule == null || schedule.PlayerNames == null || schedule.PlayerNames.Count < 1)
                return false;

            if (string.IsNullOrWhiteSpace(playerName))
                return false;

            return schedule.PlayerNames.Any(player => string.Equals(player, playerName, StringComparison.CurrentCultureIgnoreCase));
        }

        private List<SpecialScheduleInfoClass> LoadAllSchedules()
        {
            List<SpecialScheduleInfoClass> result = new List<SpecialScheduleInfoClass>();
            SpecialScheduleInfoManager manager = new SpecialScheduleInfoManager();
            manager.LoadAllSchedules();
            foreach (SpecialScheduleInfoClass schedule in manager.g_SpecialScheduleInfoClassList)
            {
                if (schedule == null)
                    continue;

                result.Add(schedule);
                if (string.IsNullOrWhiteSpace(schedule.GUID) == false)
                    sSpecialDics[schedule.GUID] = schedule;
            }

            return result;
        }

        private List<SpecialScheduleViewData> BuildSpecialViewItems(IEnumerable<SpecialScheduleInfoClass> schedules)
        {
            List<SpecialScheduleViewData> viewItems = new List<SpecialScheduleViewData>();

            foreach (SpecialScheduleInfoClass schedule in schedules)
            {
                if (schedule == null)
                    continue;

                List<string> schedulePlayers = new List<string>();
                if (schedule.PlayerNames != null && schedule.PlayerNames.Count > 0)
                {
                    HashSet<string> dedupe = new HashSet<string>(StringComparer.CurrentCultureIgnoreCase);
                    foreach (string player in schedule.PlayerNames)
                    {
                        if (string.IsNullOrWhiteSpace(player))
                            continue;

                        if (dedupe.Add(player))
                            schedulePlayers.Add(player);
                    }
                }

                schedulePlayers = schedulePlayers
                    .OrderBy(x => x, StringComparer.CurrentCultureIgnoreCase)
                    .ToList();

                string scheduleKey = BuildScheduleKey(schedule);
                List<string> scheduleGroups = new List<string>();
                if (schedule.GroupNames != null && schedule.GroupNames.Count > 0)
                {
                    HashSet<string> dedupeGroups = new HashSet<string>(StringComparer.CurrentCultureIgnoreCase);
                    foreach (string group in schedule.GroupNames)
                    {
                        if (string.IsNullOrWhiteSpace(group))
                            continue;

                        if (dedupeGroups.Add(group))
                            scheduleGroups.Add(group);
                    }
                }

                scheduleGroups = scheduleGroups
                    .OrderBy(x => x, StringComparer.CurrentCultureIgnoreCase)
                    .ToList();

                bool isGroupTarget = scheduleGroups.Count > 0;
                string groupDisplayName = scheduleGroups.Count > 0 ? string.Join(", ", scheduleGroups) : string.Empty;

                string sortKey = string.Empty;
                string displayName = string.Empty;
                if (scheduleGroups.Count > 0)
                {
                    sortKey = scheduleGroups[0];
                    displayName = BuildTargetDisplayName(true, groupDisplayName);
                }
                else if (schedulePlayers.Count > 0)
                {
                    sortKey = schedulePlayers[0];
                    displayName = BuildTargetDisplayName(false, string.Join(", ", schedulePlayers));
                }

                SpecialScheduleViewData viewData = CreateViewData(sortKey, schedule, scheduleKey, scheduleGroups);
                viewData.IsGroupTarget = isGroupTarget;
                viewData.TargetPlayers = schedulePlayers;
                viewData.TargetDisplayName = displayName;
                viewData.Schedules.Add(schedule);
                viewItems.Add(viewData);
            }

            return viewItems
                .OrderBy(x => x.GroupName, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(x => x.StartDate ?? DateTime.MinValue)
                .ThenBy(x => x.StartTime ?? TimeSpan.Zero)
                .ThenBy(x => x.Playlist, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }

        private Dictionary<string, List<string>> BuildPlayerGroupMap()
        {
            DataShop.Instance.g_PlayerGroupManager.LoadDataFromDatabase();
            Dictionary<string, List<string>> map = new Dictionary<string, List<string>>(StringComparer.CurrentCultureIgnoreCase);

            foreach (PlayerGroupClass group in DataShop.Instance.g_PlayerGroupManager.g_PlayerGroupClassList)
            {
                if (group == null || string.IsNullOrWhiteSpace(group.PG_GroupName))
                    continue;

                if (group.PG_AssignedPlayerNames == null)
                    continue;

                foreach (string player in group.PG_AssignedPlayerNames)
                {
                    if (string.IsNullOrWhiteSpace(player))
                        continue;

                    if (map.TryGetValue(player, out List<string> groups) == false)
                    {
                        groups = new List<string>();
                        map.Add(player, groups);
                    }

                    if (groups.Contains(group.PG_GroupName) == false)
                        groups.Add(group.PG_GroupName);
                }
            }

            return map;
        }

        private Dictionary<string, HashSet<string>> BuildGroupPlayersMap()
        {
            DataShop.Instance.g_PlayerGroupManager.LoadDataFromDatabase();
            Dictionary<string, HashSet<string>> map = new Dictionary<string, HashSet<string>>(StringComparer.CurrentCultureIgnoreCase);

            foreach (PlayerGroupClass group in DataShop.Instance.g_PlayerGroupManager.g_PlayerGroupClassList)
            {
                if (group == null || string.IsNullOrWhiteSpace(group.PG_GroupName))
                    continue;

                if (group.PG_AssignedPlayerNames == null)
                    continue;

                HashSet<string> players = new HashSet<string>(StringComparer.CurrentCultureIgnoreCase);
                foreach (string player in group.PG_AssignedPlayerNames)
                {
                    if (string.IsNullOrWhiteSpace(player))
                        continue;

                    players.Add(player);
                }

                if (players.Count < 1)
                    continue;

                map[group.PG_GroupName] = players;
            }

            return map;
        }

        private static string BuildScheduleKey(SpecialScheduleInfoClass schedule)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append(schedule.PageListName ?? string.Empty);
            builder.Append('|');
            builder.Append(BuildDateKey(schedule.IsPeriodEnable, schedule.PeriodStartYear, schedule.PeriodStartMonth, schedule.PeriodStartDay));
            builder.Append('|');
            builder.Append(BuildDateKey(schedule.IsPeriodEnable, schedule.PeriodEndYear, schedule.PeriodEndMonth, schedule.PeriodEndDay));
            builder.Append('|');
            builder.Append(BuildTimeKey(schedule.DisplayStartH, schedule.DisplayStartM));
            builder.Append('|');
            builder.Append(BuildTimeKey(schedule.DisplayEndH, schedule.DisplayEndM));
            builder.Append('|');
            builder.Append(BuildDaysKey(schedule));
            return builder.ToString();
        }

        private static string BuildDateKey(bool isPeriodEnable, int year, int month, int day)
        {
            if (isPeriodEnable == false)
                return string.Empty;

            if (year <= 0 || month <= 0 || day <= 0)
                return string.Empty;

            return string.Format(CultureInfo.InvariantCulture, "{0:D4}{1:D2}{2:D2}", year, month, day);
        }

        private static string BuildTimeKey(int hour, int minute)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0:D2}{1:D2}", hour, minute);
        }

        private static string BuildDaysKey(SpecialScheduleInfoClass schedule)
        {
            return string.Concat(
                schedule.DayOfWeek2 ? "1" : "0",
                schedule.DayOfWeek3 ? "1" : "0",
                schedule.DayOfWeek4 ? "1" : "0",
                schedule.DayOfWeek5 ? "1" : "0",
                schedule.DayOfWeek6 ? "1" : "0",
                schedule.DayOfWeek7 ? "1" : "0",
                schedule.DayOfWeek1 ? "1" : "0");
        }

        private static string BuildTargetDisplayName(bool isGroupTarget, string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return string.Empty;

            return string.Concat(isGroupTarget ? "그룹: " : "플레이어: ", name);
        }

        private static void AddSchedulesForGroup(SpecialScheduleViewData viewData, IEnumerable<SpecialScheduleInfoClass> schedules, HashSet<string> groupPlayers)
        {
            if (viewData == null || schedules == null || groupPlayers == null || groupPlayers.Count < 1)
                return;

            foreach (SpecialScheduleInfoClass schedule in schedules)
            {
                if (schedule == null)
                    continue;

                if (schedule.PlayerNames == null || schedule.PlayerNames.Count < 1)
                    continue;

                bool hasMatch = schedule.PlayerNames.Any(player => groupPlayers.Contains(player));
                if (hasMatch == false)
                    continue;

                viewData.Schedules.Add(schedule);
            }
        }

        private static void AddSchedulesForPlayer(SpecialScheduleViewData viewData, IEnumerable<SpecialScheduleInfoClass> schedules, string playerName)
        {
            if (viewData == null || schedules == null || string.IsNullOrWhiteSpace(playerName))
                return;

            foreach (SpecialScheduleInfoClass schedule in schedules)
            {
                if (schedule == null)
                    continue;

                if (schedule.PlayerNames == null || schedule.PlayerNames.Count < 1)
                    continue;

                bool hasMatch = schedule.PlayerNames.Any(player => string.Equals(player, playerName, StringComparison.CurrentCultureIgnoreCase));
                if (hasMatch == false)
                    continue;

                viewData.Schedules.Add(schedule);
            }
        }

        private static SpecialScheduleViewData CreateViewData(string groupName, SpecialScheduleInfoClass schedule, string scheduleKey, List<string> groupNames)
        {
            SpecialScheduleViewData viewData = new SpecialScheduleViewData();
            viewData.GroupName = groupName;
            viewData.GroupNames = groupNames == null ? new List<string>() : new List<string>(groupNames);
            viewData.Playlist = schedule.PageListName ?? string.Empty;
            viewData.ScheduleKey = scheduleKey;
            viewData.StartDate = BuildPeriodDate(schedule.IsPeriodEnable, schedule.PeriodStartYear, schedule.PeriodStartMonth, schedule.PeriodStartDay);
            viewData.EndDate = BuildPeriodDate(schedule.IsPeriodEnable, schedule.PeriodEndYear, schedule.PeriodEndMonth, schedule.PeriodEndDay);
            viewData.StartTime = BuildTime(schedule.DisplayStartH, schedule.DisplayStartM);
            viewData.EndTime = BuildTime(schedule.DisplayEndH, schedule.DisplayEndM);
            viewData.Days = BuildDays(schedule);
            return viewData;
        }

        private static DateTime? BuildPeriodDate(bool isPeriodEnable, int year, int month, int day)
        {
            if (isPeriodEnable == false)
                return null;

            if (year <= 0 || month <= 0 || day <= 0)
                return null;

            try
            {
                return new DateTime(year, month, day);
            }
            catch
            {
                return null;
            }
        }

        private static TimeSpan? BuildTime(int hour, int minute)
        {
            if (hour < 0 || hour > 23 || minute < 0 || minute > 59)
                return null;

            return new TimeSpan(hour, minute, 0);
        }

        private static bool[] BuildDays(SpecialScheduleInfoClass schedule)
        {
            return new[]
            {
                schedule.DayOfWeek2,
                schedule.DayOfWeek3,
                schedule.DayOfWeek4,
                schedule.DayOfWeek5,
                schedule.DayOfWeek6,
                schedule.DayOfWeek7,
                schedule.DayOfWeek1
            };
        }

        private static SpecialScheduleInfoClass BuildScheduleInfo(string playlist, DateTime startDate, DateTime endDate, TimeSpan startTime, TimeSpan endTime, bool[] days)
        {
            SpecialScheduleInfoClass schedule = new SpecialScheduleInfoClass();
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

        private void ApplySpecialFilters(DateTime? selectedDate, string groupName, string listName, string playerName)
        {
            Dictionary<string, List<string>> playerGroupMap = null;
            if (string.IsNullOrWhiteSpace(groupName) == false && string.Equals(groupName, "All", StringComparison.CurrentCultureIgnoreCase) == false)
                playerGroupMap = BuildPlayerGroupMap();

            foreach (SpecialCtrl ctrl in sSpecialControls)
            {
                bool visible = IsVisibleForFilter(ctrl.sSS, selectedDate, groupName, listName, playerName, playerGroupMap);
                ctrl.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
            }

            ScheduleSelectionChanged();
        }

        private static bool IsVisibleForFilter(SpecialScheduleViewData data, DateTime? selectedDate, string groupName, string listName, string playerName, Dictionary<string, List<string>> playerGroupMap)
        {
            if (data == null)
                return false;

            if (string.IsNullOrWhiteSpace(playerName) == false && string.Equals(playerName, "All", StringComparison.CurrentCultureIgnoreCase) == false)
            {
                if (data.TargetPlayers == null || data.TargetPlayers.Count < 1)
                    return false;

                bool hasPlayer = data.TargetPlayers.Any(x => string.Equals(x, playerName, StringComparison.CurrentCultureIgnoreCase));
                if (hasPlayer == false)
                    return false;
            }

            if (string.IsNullOrWhiteSpace(groupName) == false && string.Equals(groupName, "All", StringComparison.CurrentCultureIgnoreCase) == false)
            {
                if (data.GroupNames != null && data.GroupNames.Count > 0)
                {
                    bool hasGroup = data.GroupNames.Any(x => string.Equals(x, groupName, StringComparison.CurrentCultureIgnoreCase));
                    if (hasGroup == false)
                        return false;
                }
                else
                {
                    if (data.TargetPlayers == null || data.TargetPlayers.Count < 1)
                        return false;

                    if (playerGroupMap == null)
                        return false;

                    bool hasGroup = false;
                    foreach (string player in data.TargetPlayers)
                    {
                        if (string.IsNullOrWhiteSpace(player))
                            continue;

                        if (playerGroupMap.TryGetValue(player, out List<string> groups) == false || groups == null)
                            continue;

                        if (groups.Any(x => string.Equals(x, groupName, StringComparison.CurrentCultureIgnoreCase)))
                        {
                            hasGroup = true;
                            break;
                        }
                    }

                    if (hasGroup == false)
                        return false;
                }
            }

            if (string.IsNullOrWhiteSpace(listName) == false && string.Equals(listName, "All", StringComparison.CurrentCultureIgnoreCase) == false)
            {
                if (string.Equals(data.Playlist, listName, StringComparison.CurrentCultureIgnoreCase) == false)
                    return false;
            }

            if (selectedDate != null)
            {
                DateTime target = selectedDate.Value.Date;
                if (data.StartDate != null && data.EndDate != null)
                {
                    if (target < data.StartDate.Value.Date || target > data.EndDate.Value.Date)
                        return false;
                }
            }

            return true;
        }

        private static bool IsExpiredSchedule(SpecialScheduleInfoClass schedule)
        {
            if (schedule == null)
                return false;

            DateTime? endDate = BuildPeriodDate(schedule.IsPeriodEnable, schedule.PeriodEndYear, schedule.PeriodEndMonth, schedule.PeriodEndDay);
            if (endDate == null)
                return false;

            return endDate.Value.Date.CompareTo(DateTime.Now.Date) < 0;
        }

        private List<SpecialScheduleViewData> GetSameScheduleGroups(SpecialScheduleViewData source)
        {
            if (source == null)
                return new List<SpecialScheduleViewData>();

            return sSpecialControls
                .Select(x => x.sSS)
                .Where(x => x != null && string.Equals(x.ScheduleKey, source.ScheduleKey, StringComparison.CurrentCultureIgnoreCase))
                .ToList();
        }
    }
}
