import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
SOURCE_PATH = ROOT / "NewHyOn_Manager" / "SubWindow" / "EditOnAirTimeWindow.xaml.cs"
XAML_PATH = ROOT / "NewHyOn_Manager" / "SubWindow" / "EditOnAirTimeWindow.xaml"


class BroadcastBatchGroupContractTests(unittest.TestCase):
    def test_broadcast_batch_targets_selected_group_or_all_players(self):
        source = SOURCE_PATH.read_text(encoding="utf-8-sig")

        self.assertIn("GetBatchTargetPlayers", source)
        self.assertIn("g_CurrentSelectedPlayerGroupClass", source)
        self.assertIn("PG_AssignedPlayerNames", source)
        self.assertIn("DataShop.Instance.g_PlayerInfoManager.g_PlayerInfoClassList", source)

    def test_broadcast_batch_label_names_current_group_and_ellipsizes(self):
        source = SOURCE_PATH.read_text(encoding="utf-8-sig")
        xaml = XAML_PATH.read_text(encoding="utf-8-sig")

        self.assertIn("그룹에 일괄 적용", source)
        self.assertIn("모든 플레이어에 일괄 적용", source)
        self.assertIn('TextTrimming="CharacterEllipsis"', xaml)
        self.assertIn('Text="요일복사"', xaml)


if __name__ == "__main__":
    unittest.main()
