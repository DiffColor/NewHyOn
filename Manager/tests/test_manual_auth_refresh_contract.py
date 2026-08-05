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


class PlayerAuthRefreshContractTests(unittest.TestCase):
    def test_automatic_lifecycle_does_not_request_auth_refresh(self):
        source = read_source("MainWindow.xaml.cs")

        automatic_bodies = [
            method_body(source, "public void InitPages()"),
            method_body(source, "async void MainWindow_Loaded"),
        ]

        for body in automatic_bodies:
            self.assertNotIn("RequestAuthValidation", body)
            self.assertNotIn("AuthValidationChanged", body)

        self.assertNotRegex(source, r"checkTimer_Tick[\s\S]*RequestAuthValidationForAll")

    def test_player_list_rebuild_does_not_start_bulk_auth_refresh(self):
        source = read_source("Pages/Page3.xaml.cs")
        body = method_body(source, "public void RefreshPlayerInfoList()")

        self.assertNotIn("RequestAuthValidation", body)

    def test_player_card_binding_starts_single_background_auth_refresh(self):
        source = read_source("Page3UserControl/PlayerInfoElement.xaml.cs")
        bind_body = method_body(
            source,
            "public void UpdateDataInfo(",
        )
        refresh_body = method_body(source, "private async Task StartAuthenticationRefreshAsync()")

        self.assertIn("StartAuthenticationRefreshAsync();", bind_body)
        self.assertIn("CancellationTokenSource", source)
        self.assertIn("RefreshAuthenticationStateAsync", refresh_body)
        self.assertIn("cancellationToken", refresh_body)

    def test_player_card_auth_refresh_moves_database_io_off_ui_thread(self):
        source = read_source("Page3UserControl/PlayerInfoElement.xaml.cs")
        refresh_body = method_body(source, "private async Task StartAuthenticationRefreshAsync()")

        self.assertIn("Task.Run", refresh_body)
        self.assertLess(
            refresh_body.index("Task.Run"),
            refresh_body.index("RefreshAuthenticationStateAsync"),
        )

    def test_player_card_removal_cancels_background_auth_refresh(self):
        card_source = read_source("Page3UserControl/PlayerInfoElement.xaml.cs")
        page_source = read_source("Pages/Page3.xaml.cs")

        unloaded_body = method_body(card_source, "private void PlayerInfoElement_Unloaded")
        cancel_body = method_body(card_source, "public void CancelAuthenticationRefresh()")
        rebuild_body = method_body(page_source, "public void RefreshPlayerInfoList()")
        prepare_body = method_body(page_source, "private void PreparePlayerInfoListRefresh()")

        self.assertIn("CancelAuthenticationRefresh();", unloaded_body)
        self.assertIn("Cancel()", cancel_body)
        self.assertIn("PreparePlayerInfoListRefresh();", rebuild_body)
        self.assertLess(
            prepare_body.index("CancelAuthenticationRefresh"),
            prepare_body.index("PlayerListBox.Items.Clear"),
        )

    def test_single_player_auth_refresh_is_cancellable_and_does_not_load_all_players(self):
        source = read_source("DataManager/PlayerInfoManager.cs")
        body = method_body(
            source,
            "public async Task<bool> RefreshAuthenticationStateAsync",
        )

        self.assertIn("FindById", body)
        self.assertIn("cancellationToken.ThrowIfCancellationRequested", body)
        self.assertNotIn("LoadAllDocuments", body)

    def test_single_document_db_read_forwards_cancellation_token(self):
        source = read_source("TurtleTools/RethinkDbContext.cs")
        body = method_body(source, "private static async Task<T> RunSingleOrDefaultInternalAsync")

        self.assertIn("RunAtomAsync<T>", body)
        self.assertIn("cancellationToken", body)
        self.assertIn("catch (OperationCanceledException)", body)

    def test_bulk_auth_refresh_api_is_not_available(self):
        source = read_source("DataManager/PlayerInfoManager.cs")

        self.assertNotIn("RequestAuthValidationForAll", source)

    def test_manual_refresh_updates_authentication_overlays(self):
        source = read_source("Pages/Page3.xaml.cs")
        body = method_body(source, "private void RefreshExistingPlayerInfoCardsFromManager()")

        self.assertIn("RefreshPlayerAuthenticationOverlays", body)


if __name__ == "__main__":
    unittest.main()
