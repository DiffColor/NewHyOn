using AndoW_Manager;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;

namespace TurtleTools
{
    public partial class SelectUSBWindow : Window
    {
        string g_listName;

        public SelectUSBWindow(List<string> usbs, string listname)
        {
            InitializeComponent();

            g_listName = listname;

            InitCombo(usbs);
        }

        private void InitCombo(List<string> usbs)
        {
            USBCombo.Items.Clear();

            foreach (string usb in usbs)
            {
                USBCombo.Items.Add(usb);
            }

            USBCombo.SelectedIndex = 0;
        }

        void ConfirmBtn_Click(object sender, RoutedEventArgs e)
        {
            if (USBCombo.SelectedValue == null)
            {
                MessageTools.ShowMessageBox("저장 위치를 선택해주세요.", "확인");
                return;
            }

            try
            {
                string usbname = USBCombo.SelectedValue.ToString();
                string usbRoot = PrepareUsbRoot(usbname);

                DataShop.Instance.g_PageInfoManager.LoadPagesForList(g_listName);

                List<PageInfoClass> pages = ClonePages(DataShop.Instance.g_PageInfoManager.g_PageInfoClassList);
                NormalizeContentPaths(pages);
                List<PlayerInfoClass> players = CollectPlayersForPlaylist(g_listName);
                CopyAuthKeyToUsb(usbname);

                var playlistSnapshot = new PlaylistExportBundle
                {
                    PlaylistName = g_listName,
                    PageList = ClonePageList(DataShop.Instance.g_PageListInfoManager.GetPageListByName(g_listName)),
                    Pages = pages,
                    Players = players,
                    ExportedAt = DateTime.Now
                };

                WriteJson(Path.Combine(usbRoot, "playlist.bin"), playlistSnapshot);

                var weeklySnapshot = new WeeklyScheduleExportBundle
                {
                    ExportedAt = DateTime.Now,
                    Items = BuildWeeklyScheduleSnapshots(players)
                };
                WriteJson(Path.Combine(usbRoot, "weekly_schedule.bin"), weeklySnapshot);

                var specialSnapshot = new SpecialScheduleExportBundle
                {
                    ExportedAt = DateTime.Now,
                    Items = BuildSpecialScheduleSnapshots(players)
                };
                WriteJson(Path.Combine(usbRoot, "special_schedule.bin"), specialSnapshot);

                string targetContentFolder = FNDTools.GetUSBContentPath(usbname);
                List<CopyFileInfo> copyfilelist = BuildCopyFileList(pages, targetContentFolder);

                SavingFileWindow form = new SavingFileWindow(copyfilelist);
                form.Owner = this;
                form.ShowDialog();

                this.Close();
            }
            catch (Exception ex)
            {
                Logger.WriteErrorLog(ex.ToString(), Logger.GetLogFileName());
                MessageTools.ShowMessageBox("선택한 저장 위치로 내보내는 중 오류가 발생했습니다.", "확인");
            }
        }


        private void CloseBtn_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            this.Close();
        }

        private void BtnWin_drag_PreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            this.DragMove();
        }

        private static string PrepareUsbRoot(string usbName)
        {
            string usbRoot = FNDTools.GetUSBRootPath(usbName);
            FileTools.DeleteDirectory(usbRoot);
            return FNDTools.GetUSBRootPath(usbName);
        }

        private static void CopyAuthKeyToUsb(string usbName)
        {
            string target = FNDTools.GetUSBAuthKeyPath(usbName);
            List<string> authKeys = DataShop.Instance.g_PlayerInfoManager.GetAllAuthKeys();

            if (authKeys == null || authKeys.Count == 0)
            {
                if (File.Exists(target))
                {
                    File.Delete(target);
                }
                return;
            }

            string directory = Path.GetDirectoryName(target);
            if (string.IsNullOrEmpty(directory) == false)
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllLines(target, authKeys);
        }

        private static List<PageInfoClass> ClonePages(IEnumerable<PageInfoClass> pages)
        {
            List<PageInfoClass> clones = new List<PageInfoClass>();
            if (pages == null)
            {
                return clones;
            }

            foreach (PageInfoClass page in pages)
            {
                if (page == null)
                {
                    continue;
                }

                PageInfoClass clone = new PageInfoClass();
                clone.CopyData(page);
                clones.Add(clone);
            }

            return clones;
        }

        private static void NormalizeContentPaths(IEnumerable<PageInfoClass> pages)
        {
            if (pages == null)
            {
                return;
            }

            foreach (PageInfoClass page in pages)
            {
                if (page?.PIC_Elements == null)
                {
                    continue;
                }

                foreach (ElementInfoClass element in page.PIC_Elements)
                {
                    if (element?.EIF_ContentsInfoClassList == null)
                    {
                        continue;
                    }

                    foreach (ContentsInfoClass content in element.EIF_ContentsInfoClassList)
                    {
                        if (content == null)
                        {
                            continue;
                        }

                        if (string.IsNullOrWhiteSpace(content.CIF_FileFullPath) && string.IsNullOrWhiteSpace(content.CIF_FileName) == false)
                        {
                            content.CIF_FileFullPath = FNDTools.GetTargetContentsFilePath(content.CIF_FileName);
                        }
                    }
                }
            }
        }

        private static List<PlayerInfoClass> CollectPlayersForPlaylist(string playlistName)
        {
            var players = DataShop.Instance?.g_PlayerInfoManager?.g_PlayerInfoClassList;
            if (players == null || string.IsNullOrWhiteSpace(playlistName))
            {
                return new List<PlayerInfoClass>();
            }

            return players
                .Where(p => p != null && p.PIF_CurrentPlayList.Equals(playlistName, StringComparison.CurrentCultureIgnoreCase))
                .Select(ClonePlayer)
                .ToList();
        }

        private static PlayerInfoClass ClonePlayer(PlayerInfoClass source)
        {
            if (source == null)
            {
                return null;
            }

            PlayerInfoClass clone = new PlayerInfoClass();
            clone.CopyData(source);
            return clone;
        }

        private static PageListInfoClass ClonePageList(PageListInfoClass source)
        {
            if (source == null)
            {
                return null;
            }

            PageListInfoClass clone = new PageListInfoClass();
            clone.CopyData(source);
            clone.Id = source.Id;
            return clone;
        }

        private static void WriteJson(string filePath, object data)
        {
            if (string.IsNullOrWhiteSpace(filePath) || data == null)
            {
                return;
            }

            try
            {
                SecureJsonTools.WriteEncryptedJson(filePath, data);
            }
            catch (Exception ex)
            {
                Logger.WriteErrorLog(ex.ToString(), Logger.GetLogFileName());
            }
        }

        private static List<WeeklyScheduleExportItem> BuildWeeklyScheduleSnapshots(IEnumerable<PlayerInfoClass> players)
        {
            List<WeeklyScheduleExportItem> result = new List<WeeklyScheduleExportItem>();
            if (players == null)
            {
                return result;
            }

            WeeklyInfoManagerClass weeklyManager = new WeeklyInfoManagerClass();

            foreach (PlayerInfoClass player in players)
            {
                if (player == null)
                {
                    continue;
                }

                weeklyManager.InitPlayerInfoListFromDataTable(player.PIF_GUID, player.PIF_PlayerName);
                if (weeklyManager.CurrentSchedule == null)
                {
                    continue;
                }

                WeeklyScheduleExportItem item = new WeeklyScheduleExportItem
                {
                    Player = ClonePlayer(player),
                    Schedule = CloneWeeklySchedule(weeklyManager.CurrentSchedule),
                    DailySchedules = weeklyManager.PIF_WPS_InfoList.Select(x => x?.Clone()).Where(x => x != null).ToList()
                };

                result.Add(item);
            }

            return result;
        }

        private static WeeklyPlayScheduleInfo CloneWeeklySchedule(WeeklyPlayScheduleInfo source)
        {
            if (source == null)
            {
                return null;
            }

            return new WeeklyPlayScheduleInfo
            {
                Id = source.Id,
                PlayerID = source.PlayerID,
                PlayerName = source.PlayerName,
                MonSch = CloneDaySchedule(source.MonSch),
                TueSch = CloneDaySchedule(source.TueSch),
                WedSch = CloneDaySchedule(source.WedSch),
                ThuSch = CloneDaySchedule(source.ThuSch),
                FriSch = CloneDaySchedule(source.FriSch),
                SatSch = CloneDaySchedule(source.SatSch),
                SunSch = CloneDaySchedule(source.SunSch),
            };
        }

        private static DaySchedule CloneDaySchedule(DaySchedule source)
        {
            if (source == null)
            {
                return null;
            }

            return new DaySchedule
            {
                StartHour = source.StartHour,
                StartMinute = source.StartMinute,
                EndHour = source.EndHour,
                EndMinute = source.EndMinute
            };
        }

        private static List<PlayerSpecialScheduleExport> BuildSpecialScheduleSnapshots(IEnumerable<PlayerInfoClass> players)
        {
            List<PlayerSpecialScheduleExport> result = new List<PlayerSpecialScheduleExport>();
            if (players == null)
            {
                return result;
            }

            SpecialScheduleInfoManager manager = new SpecialScheduleInfoManager();

            foreach (PlayerInfoClass player in players)
            {
                if (player == null)
                {
                    continue;
                }

                manager.LoadSchedulesForPlayer(player.PIF_PlayerName);
                if (manager.g_SpecialScheduleInfoClassList == null || manager.g_SpecialScheduleInfoClassList.Count == 0)
                {
                    continue;
                }

                List<SpecialScheduleInfoClass> schedules = new List<SpecialScheduleInfoClass>();

                foreach (SpecialScheduleInfoClass schedule in manager.g_SpecialScheduleInfoClassList)
                {
                    schedules.Add(CloneSpecialSchedule(schedule));
                }

                result.Add(new PlayerSpecialScheduleExport
                {
                    Player = ClonePlayer(player),
                    Schedules = schedules
                });
            }

            return result;
        }

        private static SpecialScheduleInfoClass CloneSpecialSchedule(SpecialScheduleInfoClass source)
        {
            if (source == null)
            {
                return null;
            }

            SpecialScheduleInfoClass clone = new SpecialScheduleInfoClass();
            clone.CopyData(source);
            return clone;
        }

        private static List<CopyFileInfo> BuildCopyFileList(IEnumerable<PageInfoClass> pages, string targetContentFolder)
        {
            List<CopyFileInfo> copyList = new List<CopyFileInfo>();
            if (pages == null)
            {
                return copyList;
            }

            HashSet<string> dedupeKeys = new HashSet<string>(StringComparer.CurrentCultureIgnoreCase);
            foreach (PageInfoClass page in pages)
            {
                if (page?.PIC_Elements == null)
                {
                    continue;
                }

                foreach (ElementInfoClass element in page.PIC_Elements)
                {
                    if (element?.EIF_ContentsInfoClassList == null)
                    {
                        continue;
                    }

                    if (Enum.TryParse(element.EIF_Type, out DisplayType displayType) == false || displayType != DisplayType.Media)
                    {
                        continue;
                    }

                    foreach (ContentsInfoClass content in element.EIF_ContentsInfoClassList)
                    {
                        if (content == null)
                        {
                            continue;
                        }

                        string fileName = GetTargetFileName(content);
                        if (string.IsNullOrWhiteSpace(fileName))
                        {
                            continue;
                        }

                        string sourcePath = ResolveContentPath(content);
                        string dedupeKey = $"{fileName}|{sourcePath}";
                        if (dedupeKeys.Add(dedupeKey) == false)
                        {
                            continue;
                        }

                        string targetPath = string.IsNullOrWhiteSpace(targetContentFolder)
                            ? fileName
                            : Path.Combine(targetContentFolder, fileName);

                        copyList.Add(new CopyFileInfo
                        {
                            CFI_FileName = fileName,
                            CFI_FileSourceFullPath = sourcePath,
                            CFI_TargetFileName = targetPath,
                            CFI_PageName = page.PIC_PageName
                        });
                    }
                }
            }

            return copyList;
        }

        private static string ResolveContentPath(ContentsInfoClass content)
        {
            return FNDTools.GetContentFilePath(content);
        }

        private static string GetTargetFileName(ContentsInfoClass content)
        {
            if (content == null)
            {
                return string.Empty;
            }

            string fileName = content.CIF_FileName;
            if (string.IsNullOrWhiteSpace(fileName) && string.IsNullOrWhiteSpace(content.CIF_FileFullPath) == false)
            {
                fileName = Path.GetFileName(content.CIF_FileFullPath);
            }

            if (Enum.TryParse(content.CIF_ContentType, out ContentType contentType) && contentType == ContentType.PDF)
            {
                if (string.IsNullOrWhiteSpace(fileName) == false)
                {
                    return Path.ChangeExtension(fileName, ".swf");
                }
            }

            return fileName;
        }

        private class PlaylistExportBundle
        {
            public string PlaylistName { get; set; }
            public PageListInfoClass PageList { get; set; }
            public List<PageInfoClass> Pages { get; set; } = new List<PageInfoClass>();
            public List<PlayerInfoClass> Players { get; set; } = new List<PlayerInfoClass>();
            public DateTime ExportedAt { get; set; }
        }

        private class WeeklyScheduleExportBundle
        {
            public DateTime ExportedAt { get; set; }
            public List<WeeklyScheduleExportItem> Items { get; set; } = new List<WeeklyScheduleExportItem>();
        }

        private class WeeklyScheduleExportItem
        {
            public PlayerInfoClass Player { get; set; }
            public WeeklyPlayScheduleInfo Schedule { get; set; }
            public List<WeeklyDayScheduleInfo> DailySchedules { get; set; } = new List<WeeklyDayScheduleInfo>();
        }

        private class SpecialScheduleExportBundle
        {
            public DateTime ExportedAt { get; set; }
            public List<PlayerSpecialScheduleExport> Items { get; set; } = new List<PlayerSpecialScheduleExport>();
        }

        private class PlayerSpecialScheduleExport
        {
            public PlayerInfoClass Player { get; set; }
            public List<SpecialScheduleInfoClass> Schedules { get; set; } = new List<SpecialScheduleInfoClass>();
        }
    }

}
