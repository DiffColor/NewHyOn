from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[1]


class ManagerPayloadContractTests(unittest.TestCase):
    def test_command_queue_bootstraps_update_infrastructure_without_superseding_fifo(self):
        source = (
            ROOT / "Manager/NewHyOn_Manager/DataManager/CommandQueueManager.cs"
        ).read_text(encoding="utf-8-sig")
        enqueue = source.split(
            "public CommandQueueEntry EnqueueCommand", 1
        )[1].split("public void SupersedePending", 1)[0]
        main_window = (
            ROOT / "Manager/NewHyOn_Manager/MainWindow.xaml.cs"
        ).read_text(encoding="utf-8-sig")
        enqueue_window = main_window.split(
            "public bool EnqueueCommandForPlayer", 1
        )[1].split("private static bool RequiresAuthenticatedPlayer", 1)[0]

        self.assertIn("EnsureUpdateInfrastructure();", enqueue)
        self.assertIn('EnsureTable(databaseName, "UpdateLease", "id")', enqueue)
        self.assertIn("UpdateThrottleSettingsManager().LoadSettings()", enqueue)
        self.assertIn("UpdateLeaseManager.CleanupInvalidLeases", enqueue)
        self.assertIn('EnsureTable(databaseName, "UpdateQueue", "id")', enqueue)
        self.assertIn('EnsureTable(databaseName, "CommandHistory", "id")', enqueue)
        self.assertIn("RethinkDbContext.RunOrThrow(insert);", enqueue)
        self.assertNotIn("Upsert(entry);", enqueue)
        self.assertIn(".ThenBy(x => x.Id)", source)
        self.assertNotIn("SupersedePending", enqueue_window)
        self.assertNotIn('MarkStatus(entry.Id, playerId, "sent")', enqueue_window)

    def test_rethink_context_exposes_retrying_exception_propagating_write(self):
        source = (
            ROOT / "Manager/NewHyOn_Manager/TurtleTools/RethinkDbContext.cs"
        ).read_text(encoding="utf-8-sig")

        self.assertIn("public static void RunOrThrow(ReqlExpr expr)", source)
        self.assertIn("private static void RunOrThrowInternal(ReqlExpr expr, bool retried)", source)
        run_or_throw = source.split(
            "private static void RunOrThrowInternal", 1
        )[1].split("public static Connection GetRawConnection", 1)[0]
        self.assertIn("RunOrThrowInternal(expr, true);", run_or_throw)
        self.assertIn("throw;", run_or_throw)

    def test_regular_playlist_payload_omits_contract_and_prepares_pages(self):
        source = (
            ROOT / "Manager/NewHyOn_Manager/DataManager/UpdatePayloadBuilder.cs"
        ).read_text(encoding="utf-8-sig")
        build_payload = source.split(
            "public UpdatePayload BuildPayload", 1
        )[1].split("public string BuildPayloadBase64", 1)[0]

        self.assertIn("ApplyContentDetailsToPages(pages);", build_payload)
        self.assertIn("PageList = pageList", build_payload)
        self.assertIn("Pages = pages", build_payload)
        self.assertNotIn("BuildContractPayload", source)
        self.assertNotIn("Contract =", build_payload)

    def test_schedule_playlist_payload_omits_contract_and_prepares_pages(self):
        source = (
            ROOT / "Manager/NewHyOn_Manager/MainWindow.xaml.cs"
        ).read_text(encoding="utf-8-sig")
        schedule_builder = source.split(
            "private static string BuildSchedulePayloadBase64", 1
        )[1].split("private static string BuildWeeklyPayloadBase64", 1)[0]

        self.assertIn("builder.PreparePagesForPayload(pages);", schedule_builder)
        self.assertIn("PageList = pageList", schedule_builder)
        self.assertIn("Pages = pages", schedule_builder)
        self.assertNotIn("BuildContractPayload", schedule_builder)
        self.assertNotIn("Contract =", schedule_builder)

    def test_null_contract_properties_are_omitted_from_encoded_json(self):
        source = (
            ROOT / "Manager/NewHyOn_Manager/SharedModels/UpdatePayloadModels.cs"
        ).read_text(encoding="utf-8-sig")
        codec = source.split("public static class UpdatePayloadCodec", 1)[1]

        self.assertIn("NullValueHandling = NullValueHandling.Ignore", codec)
        self.assertIn("JsonConvert.SerializeObject(payload, CodecSettings)", codec)


if __name__ == "__main__":
    unittest.main()
