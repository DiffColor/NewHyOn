from pathlib import Path
import json
import re
import unittest
import zipfile

ROOT = Path(__file__).resolve().parents[1]


def read(relative: str) -> str:
    return (ROOT / relative).read_text(encoding="utf-8")


class StartAppsNtpContractTests(unittest.TestCase):
    def test_ntp_type_button_and_card_are_first_class_ui(self):
        app_type = read("StartApps/Models/AppType.cs")
        main_xaml = read("StartApps/Views/Dialogs/MainWindow.xaml")
        entry_vm = read("StartApps/ViewModels/AppEntryViewModel.cs")

        self.assertRegex(app_type, r"\bNtp\b")
        self.assertIn('Content="NtpServer"', main_xaml)
        self.assertIn('Tag="Ntp"', main_xaml)
        self.assertIn('AppCardGradientNtp', main_xaml)
        self.assertIn('Value="Ntp"', main_xaml)
        self.assertIn('AppType.Ntp => "NtpServer"', entry_vm)

    def test_manager_seed_contains_enabled_ntp_server_with_any_remote_address(self):
        definitions = json.loads(read("StartApps/Profiles/manager.apps.json"))
        ntp = next(item for item in definitions if item.get("type") == "Ntp")

        self.assertEqual(ntp["name"], "NtpServer")
        self.assertEqual(ntp["zone"], "Parallel")
        self.assertIs(ntp["isEnabled"], True)
        self.assertIs(ntp["runAsAdministrator"], True)
        self.assertEqual(ntp["port"], 123)
        self.assertIn("-listen :123", ntp["arguments"])
        self.assertIn("-firewall-remote-address Any", ntp["arguments"])
        self.assertIs(ntp["showWindow"], False)
        self.assertEqual(ntp["windowStyle"], "Hidden")

    def test_ntp_archive_is_embedded_and_extracted_like_other_defaults(self):
        project = read("StartApps/StartApps.csproj")
        dependencies = read("StartApps/Services/AppDependencyService.cs")
        app_manager = read("StartApps/Services/AppManager.cs")
        data_store = read("StartApps/Services/AppDataStore.cs")
        main_vm = read("StartApps/ViewModels/MainWindowViewModel.cs")

        self.assertIn('<EmbeddedResource Include="ntpserver.zip" />', project)
        self.assertNotIn('BuildEmbeddedNtpServer', project)
        self.assertNotIn('EmbeddedNtpServerHash', project)
        self.assertIn('AppType.Ntp => Path.Combine(StorageRoot, NtpServerFolderName, NtpServerExeName)', dependencies)
        self.assertIn('AppType.Ntp => _dependencyService.GetExecutablePath(AppType.Ntp)', app_manager)
        self.assertIn('type == AppType.Ntp', dependencies)
        self.assertIn('NtpServerArchiveName = "ntpserver.zip"', dependencies)
        self.assertIn('ExtractEmbeddedZipAsync(NtpServerArchiveName', dependencies)
        self.assertIn('definition.Type == AppType.Ntp', main_vm)
        self.assertIn('string.Equals(definition.Name, "NTP", StringComparison.OrdinalIgnoreCase)', main_vm)
        self.assertIn('definition.Name = "NtpServer"', main_vm)
        self.assertIn('ManagerNtpMigrationId', data_store)
        self.assertIn('HasManagerNtpMigrationAsync', main_vm)
        self.assertIn('MarkManagerNtpMigrationAsync', main_vm)
        self.assertRegex(
            main_vm,
            r"RequiresExternalProcessCheck\(AppDefinition definition\)[\s\S]*?AppType\.Ntp",
        )
        self.assertIn("definition.Type == AppType.Ntp", app_manager)
        self.assertIn("matchesAnyExecutablePath", app_manager)

        archive_path = ROOT / "StartApps/ntpserver.zip"
        self.assertTrue(archive_path.is_file())
        with zipfile.ZipFile(archive_path) as archive:
            self.assertEqual(archive.namelist(), ["NtpServer.exe"])
            self.assertGreater(archive.getinfo("NtpServer.exe").file_size, 0)


if __name__ == "__main__":
    unittest.main()
