import unittest
from pathlib import Path


MANAGER_ROOT = Path(__file__).resolve().parents[1] / "NewHyOn_Manager"


def read_source(relative_path: str) -> str:
    return (MANAGER_ROOT / relative_path).read_text(encoding="utf-8-sig")


def method_body(source: str, signature: str) -> str:
    start = source.index(signature)
    opening_brace = source.index("{", start)
    depth = 0
    for index in range(opening_brace, len(source)):
        if source[index] == "{":
            depth += 1
        elif source[index] == "}":
            depth -= 1
            if depth == 0:
                return source[opening_brace + 1:index]
    raise AssertionError(f"메서드 본문을 찾을 수 없습니다: {signature}")


class StartupRenderContractTests(unittest.TestCase):
    def test_initial_page_does_not_wait_for_signalr(self):
        source = read_source("MainWindow.xaml.cs")
        loaded_body = method_body(source, "async void MainWindow_Loaded")

        self.assertIn("SignalRClientTools.StartSignalRClient();", loaded_body)
        self.assertNotIn("WaitForSignalRReadyAsync", loaded_body)
        self.assertNotIn("EnsureConnectedAsync", loaded_body)
        self.assertLess(
            loaded_body.index("SignalRClientTools.StartSignalRClient();"),
            loaded_body.index("InitPages();"),
        )

    def test_startup_records_database_pages_and_first_render_timings(self):
        source = read_source("MainWindow.xaml.cs")
        loaded_body = method_body(source, "async void MainWindow_Loaded")

        self.assertIn("Stopwatch.StartNew()", loaded_body)
        self.assertIn('"database-ready"', loaded_body)
        self.assertIn('"pages-initialized"', loaded_body)
        self.assertIn('"first-page-rendered"', loaded_body)
        self.assertIn("DispatcherPriority.Render", loaded_body)

    def test_initial_page_renders_before_player_cards_are_populated_incrementally(self):
        main_source = read_source("MainWindow.xaml.cs")
        page_source = read_source("Pages/Page3.xaml.cs")
        init_pages_body = method_body(main_source, "public void InitPages()")
        loaded_body = method_body(main_source, "async void MainWindow_Loaded")
        incremental_signature = "public async Task RefreshPlayerInfoListIncrementallyAsync()"
        self.assertIn(incremental_signature, page_source)
        incremental_body = method_body(
            page_source,
            incremental_signature,
        )

        self.assertNotIn("RefreshPlayerInfoList", init_pages_body)
        self.assertLess(
            loaded_body.index('LogStartupStage(startupTimer, "first-page-rendered")'),
            loaded_body.index("await g_Page3.RefreshPlayerInfoListIncrementallyAsync()"),
        )
        self.assertIn("await Dispatcher.Yield(DispatcherPriority.Background)", incremental_body)
        self.assertNotIn("GC.Collect", incremental_body)

    def test_incremental_player_population_is_cancelled_on_rebuild_and_unload(self):
        page_source = read_source("Pages/Page3.xaml.cs")
        incremental_body = method_body(
            page_source,
            "public async Task RefreshPlayerInfoListIncrementallyAsync()",
        )
        synchronous_body = method_body(page_source, "public void RefreshPlayerInfoList()")
        unloaded_body = method_body(page_source, "private void OnPageUnloaded")

        self.assertIn("CancellationTokenSource", incremental_body)
        self.assertIn("cancellationToken.ThrowIfCancellationRequested()", incremental_body)
        self.assertIn("CancelPlayerInfoListRefresh();", synchronous_body)
        self.assertIn("CancelPlayerInfoListRefresh();", unloaded_body)

    def test_player_cards_do_not_query_tizen_playlist_compatibility_on_ui_thread(self):
        card_source = read_source("Page3UserControl/PlayerInfoElement.xaml.cs")
        update_body = method_body(
            card_source,
            "public void UpdateDataInfo(",
        )
        refresh_combo_body = method_body(card_source, "public void RefreshPlayListComboBox")

        self.assertNotIn("IsSingleScreenPlaylist", update_body)
        self.assertNotIn("IsSingleScreenPlaylist", refresh_combo_body)
        self.assertIn("singleScreenPlaylistNames", update_body)
        self.assertIn("singleScreenPlaylistNames", refresh_combo_body)

    def test_tizen_playlist_compatibility_is_loaded_once_off_ui_thread(self):
        page_source = read_source("Pages/Page3.xaml.cs")
        manager_source = read_source("DataManager/PageInfoManager.cs")
        incremental_body = method_body(
            page_source,
            "public async Task RefreshPlayerInfoListIncrementallyAsync()",
        )
        compatibility_body = method_body(
            page_source,
            "private async Task<HashSet<string>> LoadSingleScreenPlaylistNamesAsync",
        )
        definitions_body = method_body(
            manager_source,
            "public List<PageInfoClass> GetPageDefinitionsByIds",
        )

        self.assertIn("await LoadSingleScreenPlaylistNamesAsync", incremental_body)
        self.assertLess(
            incremental_body.index("await LoadSingleScreenPlaylistNamesAsync"),
            incremental_body.index("AddPlayerInfoElement"),
        )
        self.assertIn("Task.Run", compatibility_body)
        self.assertIn("GetPageDefinitionsByIds", compatibility_body)
        self.assertEqual(1, definitions_body.count("LoadAllDocuments()"))

    def test_first_page_render_marker_waits_for_wpf_rendering_callback(self):
        source = read_source("MainWindow.xaml.cs")
        loaded_body = method_body(source, "async void MainWindow_Loaded")

        self.assertIn("await WaitForNextRenderingAsync()", loaded_body)
        self.assertIn("CompositionTarget.Rendering", source)

    def test_page_transition_replaces_content_without_cross_fade_overlap(self):
        source = read_source("Controls/PageTransition.cs")
        show_body = method_body(source, "public void ShowPage(object content)")

        self.assertNotIn("_previousPresenter.Content = _currentPresenter.Content", show_body)
        self.assertNotIn("CreateAnimation", show_body)
        self.assertLess(
            show_body.index("_previousPresenter.Content = null"),
            show_body.index("_currentPresenter.Content = content"),
        )

    def test_schedule_page_loads_specials_after_first_frame_off_ui_thread(self):
        source = read_source("Pages/Page5.xaml.cs")
        xaml = read_source("Pages/Page5.xaml")
        loaded_signature = "private async void UserControl_Loaded"
        ensure_signature = "private Task EnsureSpecialsLoadedAsync"
        apply_signature = "private void ApplySpecialSnapshot"
        async_signature = "private async Task LoadAllSpecialsAsync"
        self.assertIn(loaded_signature, source)
        self.assertIn(ensure_signature, source)
        self.assertIn(apply_signature, source)
        self.assertIn(async_signature, source)
        loaded_body = method_body(source, loaded_signature)
        async_body = method_body(source, async_signature)
        ensure_body = method_body(source, ensure_signature)
        apply_body = method_body(source, apply_signature)
        cancel_body = method_body(source, "private void CancelSpecialLoad")
        reload_body = method_body(source, "private void ReloadSpecials")

        self.assertNotIn("LoadAllSpecials();", loaded_body)
        self.assertIn("await EnsureSpecialsLoadedAsync();", loaded_body)
        self.assertIn("_isSchedulePageInitialized", loaded_body)
        self.assertNotIn("SpecialItemsControl.Items.Count", loaded_body)
        self.assertIn("_hasLoadedSpecials", ensure_body)
        self.assertIn("_specialLoadTask", ensure_body)
        self.assertIn("_specialLoadTask.IsCompleted == false", ensure_body)
        self.assertIn("Task.Run", async_body)
        self.assertIn("await Task.Yield();", async_body)
        self.assertIn("LoadAllScheduleSnapshot", async_body)
        self.assertIn("cancellationToken.ThrowIfCancellationRequested()", async_body)
        self.assertIn("ApplySpecialSnapshot", async_body)
        self.assertIn("ReferenceEquals(_specialLoadCts, loadCts)", async_body)
        self.assertIn("ShowProgress(false)", async_body)
        self.assertNotIn("sSpecialControls.Clear()", async_body)
        self.assertNotIn("sSpecialControls.Add", async_body)
        self.assertIn("Dispatcher.VerifyAccess()", apply_body)
        self.assertIn("new ObservableCollection<SpecialCtrl>", apply_body)
        self.assertIn("ApplySpecialFiltersToControls(nextControls", apply_body)
        self.assertLess(
            apply_body.index("ApplySpecialFiltersToControls(nextControls"),
            apply_body.index("sSpecialControls = nextControls"),
        )
        self.assertIn("SpecialItemsControl.ItemsSource =", apply_body)
        self.assertIn("_hasLoadedSpecials = true", apply_body)
        self.assertIn("sSpecialControls = previousControls", apply_body)
        self.assertIn("sSpecialDics = previousSchedules", apply_body)
        self.assertIn("SpecialItemsControl.ItemsSource = previousItemsSource", apply_body)
        self.assertIn("_hasLoadedSpecials = previousHasLoadedSpecials", apply_body)
        self.assertNotIn("_specialLoadCts = null", cancel_body)
        self.assertIn("StartSpecialLoad(_hasLoadedSpecials == false)", reload_body)
        self.assertNotIn("Unloaded += UserControl_Unloaded", source)
        self.assertNotIn("private void UserControl_Unloaded", source)
        self.assertIn('x:Name="ProgressGrid" Grid.Row="1" Visibility="Collapsed"', xaml)

    def test_playlist_page_virtualizes_saved_pages_and_loads_previews_lazily(self):
        xaml = read_source("Pages/Page2.xaml")
        page_source = read_source("Pages/Page2.xaml.cs")
        card_source = read_source("Page2UserControl/SavedPageElement2.xaml.cs")
        refresh_body = method_body(page_source, "public void ApplySavedPageSnapshot")
        names_body = method_body(page_source, "public void RefreshPageNameList")
        preview_signature = "private async void SavedPageElement_Loaded"

        self.assertIn("wpftk:VirtualizingItemsControl", xaml)
        self.assertNotIn('x:Name="wrapPanelTemplate"', xaml)
        self.assertNotIn("LoadPreviewImage", refresh_body)
        self.assertNotIn("GC.Collect", names_body)
        self.assertIn(preview_signature, card_source)
        preview_body = method_body(card_source, preview_signature)
        unloaded_body = method_body(card_source, "private void SavedPageElement_Unloaded")
        self.assertIn("Task.Run", preview_body)
        self.assertIn("CancelPreviewLoad", unloaded_body)

    def test_saved_page_snapshot_is_loaded_once_off_ui_thread_and_published_atomically(self):
        main_source = read_source("MainWindow.xaml.cs")
        page1_source = read_source("Pages/Page1.xaml.cs")
        page2_source = read_source("Pages/Page2.xaml.cs")

        init_body = method_body(main_source, "public void InitPages")
        ensure_body = method_body(main_source, "public Task EnsureSavedPagesLoadedAsync")
        load_body = method_body(main_source, "private async Task LoadSavedPageSnapshotAsync")
        reload_body = method_body(main_source, "public void RefreshSavedPageList")
        button_body = method_body(page1_source, "void BTN0DO_Copy26_Click")
        page1_prepare = method_body(page1_source, "public SavedPageSnapshot PrepareSavedPageSnapshot")
        page1_apply = method_body(page1_source, "public void ApplySavedPageSnapshot")
        page2_prepare = method_body(page2_source, "public SavedPageSnapshot PrepareSavedPageSnapshot")
        page2_apply = method_body(page2_source, "public void ApplySavedPageSnapshot")

        self.assertIn("EnsureSavedPagesLoadedAsync();", init_body)
        self.assertIn("_hasSavedPagesSnapshot", ensure_body)
        self.assertIn("_savedPageLoadTask.IsCompleted == false", ensure_body)
        self.assertIn("await Task.Yield();", load_body)
        self.assertIn("Task.Run", load_body)
        self.assertIn("GetAllSavedPagesOrThrow()", load_body)
        self.assertIn("PublishSavedPageSnapshot(savedPages)", load_body)
        self.assertIn("ReferenceEquals(_savedPageLoadCts, loadCts)", load_body)
        self.assertIn("CancelSavedPageLoad();", reload_body)
        self.assertIn("StartSavedPageLoad();", reload_body)
        self.assertIn("EnsureSavedPagesLoadedAsync();", button_body)
        self.assertNotIn("GetAllSavedPages", page1_prepare)
        self.assertNotIn("GetAllSavedPages", page2_prepare)
        self.assertNotIn("Children.Clear()", page1_apply)
        self.assertIn("newControls", page1_prepare)
        self.assertIn("_savedPageElements = snapshot.Elements", page2_apply)
        self.assertIn("SavedPagesItemsControl.ItemsSource = snapshot.ItemsSource", page2_apply)

    def test_saved_page_snapshot_distinguishes_empty_results_from_database_failures(self):
        context_source = read_source("TurtleTools/RethinkDbContext.cs")
        manager_base_source = read_source("TurtleTools/RethinkDbManagerBase.cs")
        page_manager_source = read_source("DataManager/PageInfoManager.cs")
        main_source = read_source("MainWindow.xaml.cs")

        throwing_read = method_body(
            context_source,
            "private static List<T> RunListOrThrowInternal<T>",
        )
        load_body = method_body(main_source, "private async Task LoadSavedPageSnapshotAsync")

        self.assertIn("RunListOrThrow<T>", manager_base_source)
        self.assertIn("LoadAllDocumentsOrThrow()", page_manager_source)
        self.assertIn("GetAllSavedPagesOrThrow()", load_body)
        self.assertNotIn("catch (ReqlNonExistenceError)", throwing_read)
        self.assertIn("cursor?.ToList() ?? new List<T>()", throwing_read)
        self.assertIn("Logger.WriteErrorLog", throwing_read)
        self.assertIn("throw;", throwing_read)

    def test_saved_page_snapshot_prepares_both_pages_and_rolls_back_both_on_commit_failure(self):
        main_source = read_source("MainWindow.xaml.cs")
        page1_source = read_source("Pages/Page1.xaml.cs")
        page2_source = read_source("Pages/Page2.xaml.cs")

        publish_body = method_body(main_source, "private void PublishSavedPageSnapshot")
        restore_body = method_body(main_source, "private void RestoreSavedPageSnapshots")
        page1_prepare = method_body(page1_source, "public SavedPageSnapshot PrepareSavedPageSnapshot")
        page1_apply = method_body(page1_source, "public void ApplySavedPageSnapshot")
        page2_prepare = method_body(page2_source, "public SavedPageSnapshot PrepareSavedPageSnapshot")
        page2_apply = method_body(page2_source, "public void ApplySavedPageSnapshot")

        self.assertIn("Dispatcher.VerifyAccess()", publish_body)
        self.assertIn("CaptureSavedPageSnapshot()", publish_body)
        self.assertIn("previousHasSavedPagesSnapshot", publish_body)
        self.assertLess(
            publish_body.index("g_Page2.PrepareSavedPageSnapshot"),
            publish_body.index("g_Page1.ApplySavedPageSnapshot"),
        )
        self.assertIn("RestoreSavedPageSnapshots", publish_body)
        self.assertIn("_hasSavedPagesSnapshot = previousHasSavedPagesSnapshot", restore_body)
        self.assertIn("g_Page1.ApplySavedPageSnapshot(previousPage1Snapshot)", restore_body)
        self.assertIn("g_Page2.ApplySavedPageSnapshot(previousPage2Snapshot)", restore_body)

        self.assertIn("newControls", page1_prepare)
        self.assertIn("new WrapPanel", page1_prepare)
        self.assertNotIn("ContentsListScrollViewer2.Content =", page1_prepare)
        self.assertIn("ContentsListScrollViewer2.Content = snapshot.Panel", page1_apply)
        self.assertNotIn("new SavedPageElement", page1_apply)

        self.assertIn("new ObservableCollection<SavedPageElement2>", page2_prepare)
        self.assertNotIn("SavedPagesItemsControl.ItemsSource =", page2_prepare)
        self.assertLess(
            page2_apply.index("SavedPagesItemsControl.ItemsSource = snapshot.ItemsSource"),
            page2_apply.index("_savedPageElements = snapshot.Elements"),
        )

    def test_saved_page_load_state_is_dispatcher_owned_and_cancel_tolerates_disposal(self):
        main_source = read_source("MainWindow.xaml.cs")
        reload_body = method_body(main_source, "public void RefreshSavedPageList")
        ensure_body = method_body(main_source, "public Task EnsureSavedPagesLoadedAsync")
        start_body = method_body(main_source, "private Task StartSavedPageLoad")
        cancel_body = method_body(main_source, "private void CancelSavedPageLoad")

        self.assertIn("Dispatcher.VerifyAccess()", reload_body)
        self.assertIn("Dispatcher.VerifyAccess()", ensure_body)
        self.assertIn("Dispatcher.VerifyAccess()", start_body)
        self.assertIn("catch (ObjectDisposedException)", cancel_body)

    def test_saved_page_reload_does_not_overlap_an_uncancellable_database_read(self):
        main_source = read_source("MainWindow.xaml.cs")
        load_body = method_body(main_source, "private async Task LoadSavedPageSnapshotAsync")

        self.assertIn("SemaphoreSlim _savedPageReadGate", main_source)
        self.assertIn("await _savedPageReadGate.WaitAsync(token)", load_body)
        self.assertIn("_savedPageReadGate.Release()", load_body)
        self.assertLess(
            load_body.index("await _savedPageReadGate.WaitAsync(token)"),
            load_body.index("GetAllSavedPagesOrThrow()"),
        )

    def test_player_selection_has_one_owner_and_updates_only_changed_cards(self):
        page_source = read_source("Pages/Page3.xaml.cs")
        card_source = read_source("Page3UserControl/PlayerInfoElement.xaml.cs")
        event_init_body = method_body(card_source, "public void InitEventHandler()")
        selection_body = method_body(
            page_source,
            "private void PlayerListBox_SelectionChanged",
        )
        preview_body = method_body(
            page_source,
            "private void PlayerListBox_PreviewMouseLeftButtonDown",
        )
        select_player_body = method_body(
            page_source,
            "public void SelectPlayerInfo(PlayerInfoClass paramCls)",
        )
        incremental_visual_body = method_body(
            page_source,
            "private static void UpdateSelectionVisuals(SelectionChangedEventArgs e)",
        )
        current_visual_body = method_body(
            card_source,
            "public void ShowAndHideSelectedBorder(bool IsShow)",
        )

        self.assertNotIn("PageListElement_PreviewMouseLeftButtonDown", event_init_body)
        self.assertEqual(1, selection_body.count("SelectPlayerInfo("))
        self.assertIn("IsCurrentPlayerInfo", selection_body)
        self.assertIn("ItemsControl.ContainerFromElement", preview_body)
        self.assertIn("SelectPlayerInfo", preview_body)
        self.assertIn("UpdateSelectionVisuals(e)", selection_body)
        self.assertNotIn("UpdateSelectionVisuals();", selection_body)
        self.assertNotIn("UpdateSelectionVisuals", select_player_body)
        self.assertIn("e.RemovedItems", incremental_visual_body)
        self.assertIn("e.AddedItems", incremental_visual_body)
        self.assertIn("SetMultiSelection", incremental_visual_body)
        self.assertIn("if (g_IsSelected == IsShow", current_visual_body)


if __name__ == "__main__":
    unittest.main()
