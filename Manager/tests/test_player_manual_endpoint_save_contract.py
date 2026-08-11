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


class PlayerManualEndpointSaveContractTests(unittest.TestCase):
    def test_editor_copies_manual_ip_and_remote_id_to_player(self):
        source = read_source("SubWindowUserControl/BatchEditPlayerInfo.xaml.cs")
        body = method_body(source, "public void SavePlayerInfo()")

        self.assertIn("PIF_IPAddress = IpAddressText.Text.Trim();", body)
        self.assertIn("PIF_RemoteID = (RemoteIdText.Text ?? string.Empty).Trim().Replace(\" \", \"\");", body)

    def test_batch_save_does_not_replace_manual_endpoint_values(self):
        source = read_source("SubWindow/PlayerBatchEditWindow.xaml.cs")
        body = method_body(source, "public void SavePlayersInformation()")

        self.assertIn("item.SavePlayerInfo();", body)
        self.assertIn("_pinfo.CopyData(item.g_PlayerInfoClass);", body)
        self.assertNotIn("_pinfo.PIF_IPAddress =", body)
        self.assertNotIn("_pinfo.PIF_RemoteID =", body)
        self.assertIn("UpdatePlayerInfoList(pinfos);", body)


if __name__ == "__main__":
    unittest.main()
