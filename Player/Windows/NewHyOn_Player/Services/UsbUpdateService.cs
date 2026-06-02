extern alias USBDetector;

using AndoW.Shared;
using LicenseHub.DeviceAuth.Core;
using NewHyOn.Shared.Auth;
using NewHyOnPlayer.DataManager;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Windows.Threading;
using TurtleTools;
using SharedPageInfoClass = AndoW.Shared.PageInfoClass;
using SharedWeeklyPlayScheduleInfo = AndoW.Shared.WeeklyPlayScheduleInfo;
using UsbManager = USBDetector::USB_Detector.UsbManager;
using UsbStateChange = USBDetector::USB_Detector.UsbStateChange;
using UsbStateChangedEventArgs = USBDetector::USB_Detector.UsbStateChangedEventArgs;

namespace NewHyOnPlayer
{
    internal sealed class UsbUpdateService : IDisposable
    {
        private const string UsbPackageDirName = "NewHyOn_USB";
        private const string ContentsDirName = "Contents";
        private const string PlaylistFileName = "playlist.bin";
        private const string WeeklyScheduleFileName = "weekly_schedule.bin";
        private const string SpecialScheduleFileName = "special_schedule.bin";
        private const string TargetsFileName = "targets.json";

        private readonly MainWindow owner;
        private readonly UsbManager usbManager;
        private bool disposed;

        public UsbUpdateService(MainWindow owner)
        {
            this.owner = owner;
            usbManager = new UsbManager();
            usbManager.StateChanged += UsbManager_StateChanged;
            Logger.WriteLog("UsbUpdateService detector initialized.", Logger.GetLogFileName());
        }

        public void Dispose()
        {
            disposed = true;
            usbManager.StateChanged -= UsbManager_StateChanged;
            usbManager.Dispose();
        }

        private void UsbManager_StateChanged(UsbStateChangedEventArgs args)
        {
            if (disposed || args == null)
            {
                return;
            }

            string diskName = args.Disk == null ? string.Empty : args.Disk.Name ?? string.Empty;
            Logger.WriteLog($"UsbUpdateService detector event. state={args.State}, disk={diskName}", Logger.GetLogFileName());

            if (args.State == UsbStateChange.Added)
            {
                string addedDiskName = diskName;
                ThreadPool.QueueUserWorkItem(_ => ProcessDisk(addedDiskName));
                return;
            }
        }

        private void ProcessDisk(string diskName)
        {
            try
            {
                if (disposed || string.IsNullOrWhiteSpace(diskName))
                {
                    return;
                }

                string driveRoot = NormalizeDriveRoot(diskName);
                if (string.IsNullOrWhiteSpace(driveRoot) || Directory.Exists(driveRoot) == false)
                {
                    Logger.WriteLog($"UsbUpdateService disk ignored: drive root not ready. disk={diskName}", Logger.GetLogFileName());
                    return;
                }

                string usbRoot = Path.Combine(driveRoot, UsbPackageDirName);
                if (Directory.Exists(usbRoot) == false)
                {
                    Logger.WriteLog($"UsbUpdateService disk ignored: missing {UsbPackageDirName}. disk={diskName}", Logger.GetLogFileName());
                    return;
                }

                TryProcessPackage(usbRoot);
            }
            catch (Exception ex)
            {
                Logger.WriteErrorLog($"UsbUpdateService process failed: {ex}", Logger.GetLogFileName());
            }
        }

        private static string NormalizeDriveRoot(string driveName)
        {
            if (string.IsNullOrWhiteSpace(driveName))
            {
                return string.Empty;
            }

            string value = driveName.Trim();
            if (value.EndsWith(@"\", StringComparison.Ordinal))
            {
                return value;
            }

            if (value.EndsWith(":", StringComparison.Ordinal))
            {
                return value + @"\";
            }

            return value + @":\";
        }

        private bool TryProcessPackage(string usbRoot)
        {
            try
            {
                Logger.WriteLog($"UsbUpdateService package detected: {usbRoot}", Logger.GetLogFileName());

                UsbPackageSelection selection = ResolvePackageSelection(usbRoot);
                if (selection == null)
                {
                    return false;
                }

                string packageId = BuildPackageIdentity(selection.PackageDir, selection.ContentRoot);
                if (string.IsNullOrWhiteSpace(packageId))
                {
                    return false;
                }

                var package = ReadPackage(selection.PackageDir, packageId, selection.IsTargetedStructure);
                if (package == null)
                {
                    return false;
                }

                string stagingContentsPath = selection.IsTargetedStructure
                    ? string.Empty
                    : StageContents(usbRoot, packageId);
                package.ContentSourcePath = selection.ContentRoot;
                package.IsTargetedStructure = selection.IsTargetedStructure;
                package.StagingContentsPath = stagingContentsPath;

                ApplyPackage(package);
                Logger.WriteLog($"UsbUpdateService package applied. playlist={package.Playlist.PageList.PLI_PageListName}", Logger.GetLogFileName());
                return true;
            }
            catch (Exception ex)
            {
                Logger.WriteErrorLog($"UsbUpdateService apply failed: {ex}", Logger.GetLogFileName());
                return false;
            }
        }

        private UsbPackageSelection ResolvePackageSelection(string usbRoot)
        {
            string targetsPath = Path.Combine(usbRoot, TargetsFileName);
            if (!File.Exists(targetsPath))
            {
                string playlistPath = Path.Combine(usbRoot, PlaylistFileName);
                if (!File.Exists(playlistPath))
                {
                    Logger.WriteLog($"UsbUpdateService package ignored: missing {PlaylistFileName}.", Logger.GetLogFileName());
                    return null;
                }

                return new UsbPackageSelection
                {
                    PackageDir = usbRoot,
                    ContentRoot = Path.Combine(usbRoot, ContentsDirName),
                    IsTargetedStructure = false
                };
            }

            UsbTargetsManifest manifest;
            try
            {
                manifest = JsonConvert.DeserializeObject<UsbTargetsManifest>(File.ReadAllText(targetsPath));
            }
            catch (Exception ex)
            {
                Logger.WriteErrorLog($"UsbUpdateService targets.json read failed: {ex}", Logger.GetLogFileName());
                return null;
            }

            HashSet<string> identities = BuildCurrentDeviceIdentityCandidates();
            if (identities.Count == 0)
            {
                Logger.WriteLog("UsbUpdateService targeted package ignored: current device identity empty.", Logger.GetLogFileName());
                return null;
            }

            UsbTargetInfo matched = null;
            foreach (UsbTargetInfo target in manifest?.Targets ?? new List<UsbTargetInfo>())
            {
                string deviceIdentity = NormalizeDeviceIdentity(target?.DeviceIdentity);
                string folder = NormalizeDeviceIdentity(target?.Folder);
                if ((!string.IsNullOrWhiteSpace(deviceIdentity) && identities.Contains(deviceIdentity))
                    || (!string.IsNullOrWhiteSpace(folder) && identities.Contains(folder)))
                {
                    matched = target;
                    break;
                }
            }

            if (matched == null)
            {
                Logger.WriteLog("UsbUpdateService targeted package ignored: current device is not a target.", Logger.GetLogFileName());
                return null;
            }

            string matchedFolder = SanitizeDeviceIdentity(string.IsNullOrWhiteSpace(matched.Folder) ? matched.DeviceIdentity : matched.Folder);
            string packageDir = Path.Combine(usbRoot, matchedFolder);
            if (!Directory.Exists(packageDir))
            {
                Logger.WriteLog($"UsbUpdateService targeted package ignored: missing target folder. folder={matchedFolder}", Logger.GetLogFileName());
                return null;
            }

            if (!File.Exists(Path.Combine(packageDir, PlaylistFileName)))
            {
                Logger.WriteLog($"UsbUpdateService targeted package ignored: missing target {PlaylistFileName}. folder={matchedFolder}", Logger.GetLogFileName());
                return null;
            }

            return new UsbPackageSelection
            {
                PackageDir = packageDir,
                ContentRoot = Path.Combine(usbRoot, ContentsDirName),
                IsTargetedStructure = true
            };
        }

        private UsbUpdatePackage ReadPackage(string usbRoot, string packageId, bool isTargetedStructure)
        {
            string playlistPath = Path.Combine(usbRoot, PlaylistFileName);
            if (!File.Exists(playlistPath))
            {
                Logger.WriteLog($"UsbUpdateService package ignored: missing {PlaylistFileName}.", Logger.GetLogFileName());
                return null;
            }

            PlaylistExportBundle playlist;
            try
            {
                playlist = SecureJsonTools.ReadEncryptedJson<PlaylistExportBundle>(playlistPath);
            }
            catch (Exception ex)
            {
                Logger.WriteErrorLog($"UsbUpdateService playlist decrypt failed: {ex}", Logger.GetLogFileName());
                return null;
            }

            if (playlist?.PageList == null || playlist.Pages == null || playlist.Pages.Count == 0)
            {
                Logger.WriteLog("UsbUpdateService package ignored: playlist data empty.", Logger.GetLogFileName());
                return null;
            }

            if (string.IsNullOrWhiteSpace(playlist.PageList.PLI_PageListName))
            {
                playlist.PageList.PLI_PageListName = playlist.PlaylistName ?? string.Empty;
            }

            if (string.IsNullOrWhiteSpace(playlist.PageList.PLI_PageListName))
            {
                Logger.WriteLog("UsbUpdateService package ignored: playlist name empty.", Logger.GetLogFileName());
                return null;
            }

            PlayerInfoClass matchedPlayer = isTargetedStructure
                ? (playlist.Players ?? new List<PlayerInfoClass>()).FirstOrDefault(x => x != null)
                : ResolvePackagePlayer(playlist.Players);
            if (playlist.Players != null && playlist.Players.Count > 0 && matchedPlayer == null)
            {
                Logger.WriteLog("UsbUpdateService package ignored: current player is not included in playlist package.", Logger.GetLogFileName());
                return null;
            }

            WeeklyScheduleExportBundle weekly = ReadOptionalEncrypted<WeeklyScheduleExportBundle>(Path.Combine(usbRoot, WeeklyScheduleFileName));
            SpecialScheduleExportBundle special = ReadOptionalEncrypted<SpecialScheduleExportBundle>(Path.Combine(usbRoot, SpecialScheduleFileName));

            NormalizePlaylistContentPaths(playlist);

            return new UsbUpdatePackage
            {
                UsbRoot = usbRoot,
                PackageId = packageId,
                Playlist = playlist,
                Weekly = weekly,
                Special = special,
                MatchedPlayer = matchedPlayer
            };
        }

        private T ReadOptionalEncrypted<T>(string path)
        {
            if (!File.Exists(path))
            {
                return default(T);
            }

            try
            {
                return SecureJsonTools.ReadEncryptedJson<T>(path);
            }
            catch (Exception ex)
            {
                Logger.WriteErrorLog($"UsbUpdateService optional package read failed. file={Path.GetFileName(path)}, error={ex}", Logger.GetLogFileName());
                return default(T);
            }
        }

        private string StageContents(string usbRoot, string packageId)
        {
            string source = Path.Combine(usbRoot, ContentsDirName);
            string stagingRoot = Path.Combine(FNDTools.GetUpdateRootDirPath(), "UsbStaging", BuildShortHash(packageId));
            string stagingContents = Path.Combine(stagingRoot, ContentsDirName);

            if (Directory.Exists(stagingRoot))
            {
                Directory.Delete(stagingRoot, true);
            }

            Directory.CreateDirectory(stagingContents);
            if (!Directory.Exists(source))
            {
                return stagingContents;
            }

            CopyDirectoryVerified(source, stagingContents);
            return stagingContents;
        }

        private void ApplyPackage(UsbUpdatePackage package)
        {
            string playlistName = package.Playlist.PageList.PLI_PageListName;

            owner.Dispatcher.Invoke(DispatcherPriority.Normal, new Action(() =>
            {
                owner.SetInitialLoadingVisible(true, "USB 업데이트 중");
                owner.StopPlayback();
            }));

            if (package.IsTargetedStructure)
            {
                CopyReferencedContentsToLocal(package.ContentSourcePath, package.Playlist?.Pages);
            }
            else
            {
                CopyStagedContentsToLocal(package.StagingContentsPath);
            }
            ApplyDataToLiteDb(package);

            owner.Dispatcher.BeginInvoke(new Action(() =>
            {
                owner.g_PageIndex = 0;
                owner.HandleWeeklyScheduleUpdated();
                owner.RequestPlaylistReload(playlistName, "usb-update");
                owner.SendHeartbeatNow();
                owner.SetInitialLoadingVisible(false);
            }), DispatcherPriority.Normal);
        }

        private void CopyStagedContentsToLocal(string stagingContentsPath)
        {
            if (string.IsNullOrWhiteSpace(stagingContentsPath) || !Directory.Exists(stagingContentsPath))
            {
                return;
            }

            CopyDirectoryVerified(stagingContentsPath, FNDTools.GetContentsRootDirPath());
        }

        private void CopyReferencedContentsToLocal(string sourceContentsPath, IEnumerable<PageInfoClass> pages)
        {
            if (string.IsNullOrWhiteSpace(sourceContentsPath) || !Directory.Exists(sourceContentsPath))
            {
                return;
            }

            Dictionary<string, ContentCopySpec> specs = CollectContentCopySpecs(pages);
            if (specs.Count == 0)
            {
                return;
            }

            string targetRoot = FNDTools.GetContentsRootDirPath();
            Directory.CreateDirectory(targetRoot);

            foreach (ContentCopySpec spec in specs.Values)
            {
                string sourceFile = ResolveSourceContentFile(sourceContentsPath, spec);
                if (string.IsNullOrWhiteSpace(sourceFile) || !File.Exists(sourceFile))
                {
                    throw new IOException($"USB content missing. file={spec.FileName}");
                }

                string targetFile = Path.Combine(targetRoot, spec.FileName);
                if (IsSameExpectedContent(sourceFile, targetFile, spec))
                {
                    continue;
                }

                if (!FileTools.CopyFile(sourceFile, targetFile, true, true))
                {
                    throw new IOException($"USB content copy failed. source={sourceFile}, target={targetFile}");
                }
            }
        }

        private static Dictionary<string, ContentCopySpec> CollectContentCopySpecs(IEnumerable<PageInfoClass> pages)
        {
            var result = new Dictionary<string, ContentCopySpec>(StringComparer.OrdinalIgnoreCase);
            if (pages == null)
            {
                return result;
            }

            foreach (var page in pages)
            {
                foreach (var element in page?.PIC_Elements ?? new List<ElementInfoClass>())
                {
                    if (!string.Equals(element?.EIF_Type, "Media", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    foreach (var content in element.EIF_ContentsInfoClassList ?? new List<ContentsInfoClass>())
                    {
                        AddContentCopySpec(result, content);
                    }
                }
            }

            return result;
        }

        private static void AddContentCopySpec(Dictionary<string, ContentCopySpec> specs, ContentsInfoClass content)
        {
            if (specs == null || content == null || !IsFileBasedContent(content))
            {
                return;
            }

            string fileName = ResolveContentFileName(content);
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return;
            }

            if (!specs.TryGetValue(fileName, out ContentCopySpec existing))
            {
                specs[fileName] = new ContentCopySpec
                {
                    FileName = fileName,
                    RelativePath = content.CIF_RelativePath ?? string.Empty,
                    SizeBytes = Math.Max(0, content.CIF_FileSize),
                    Hash = string.IsNullOrWhiteSpace(content.CIF_FileHash) ? content.CIF_StrGUID : content.CIF_FileHash
                };
                return;
            }

            if (existing.SizeBytes <= 0 && content.CIF_FileSize > 0)
            {
                existing.SizeBytes = content.CIF_FileSize;
            }

            if (string.IsNullOrWhiteSpace(existing.Hash))
            {
                existing.Hash = string.IsNullOrWhiteSpace(content.CIF_FileHash) ? content.CIF_StrGUID : content.CIF_FileHash;
            }

            if (string.IsNullOrWhiteSpace(existing.RelativePath) && !string.IsNullOrWhiteSpace(content.CIF_RelativePath))
            {
                existing.RelativePath = content.CIF_RelativePath;
            }
        }

        private static bool IsFileBasedContent(ContentsInfoClass content)
        {
            if (content == null || string.IsNullOrWhiteSpace(content.CIF_ContentType))
            {
                return true;
            }

            return !string.Equals(content.CIF_ContentType, "WebSiteURL", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(content.CIF_ContentType, "Browser", StringComparison.OrdinalIgnoreCase);
        }

        private static string ResolveContentFileName(ContentsInfoClass content)
        {
            if (content == null)
            {
                return string.Empty;
            }

            if (!string.IsNullOrWhiteSpace(content.CIF_FileName))
            {
                return Path.GetFileName(content.CIF_FileName);
            }

            if (!string.IsNullOrWhiteSpace(content.CIF_RelativePath))
            {
                return Path.GetFileName(content.CIF_RelativePath);
            }

            return string.IsNullOrWhiteSpace(content.CIF_FileFullPath)
                ? string.Empty
                : Path.GetFileName(content.CIF_FileFullPath);
        }

        private static string ResolveSourceContentFile(string sourceContentsPath, ContentCopySpec spec)
        {
            if (string.IsNullOrWhiteSpace(sourceContentsPath) || spec == null || string.IsNullOrWhiteSpace(spec.FileName))
            {
                return string.Empty;
            }

            string direct = Path.Combine(sourceContentsPath, spec.FileName);
            if (File.Exists(direct))
            {
                return direct;
            }

            string relativeName = string.IsNullOrWhiteSpace(spec.RelativePath)
                ? string.Empty
                : Path.GetFileName(spec.RelativePath);
            if (!string.IsNullOrWhiteSpace(relativeName))
            {
                string relative = Path.Combine(sourceContentsPath, relativeName);
                if (File.Exists(relative))
                {
                    return relative;
                }
            }

            return direct;
        }

        private static bool IsSameExpectedContent(string sourceFile, string targetFile, ContentCopySpec spec)
        {
            if (!File.Exists(sourceFile) || !File.Exists(targetFile))
            {
                return false;
            }

            long sourceLength = new FileInfo(sourceFile).Length;
            long targetLength = new FileInfo(targetFile).Length;
            if (sourceLength != targetLength)
            {
                return false;
            }

            if (spec != null && spec.SizeBytes > 0 && targetLength != spec.SizeBytes)
            {
                return false;
            }

            string hash = spec?.Hash ?? string.Empty;
            if (hash.Length == 16)
            {
                return string.Equals(
                    XXHash64.ComputePartialSignature(sourceFile),
                    XXHash64.ComputePartialSignature(targetFile),
                    StringComparison.OrdinalIgnoreCase);
            }

            return true;
        }

        private void CopyDirectoryVerified(string sourceRoot, string targetRoot)
        {
            Directory.CreateDirectory(targetRoot);
            foreach (string sourceFile in Directory.GetFiles(sourceRoot, "*", SearchOption.AllDirectories))
            {
                string relative = GetRelativePath(sourceRoot, sourceFile);
                string targetFile = Path.Combine(targetRoot, relative);
                string targetDir = Path.GetDirectoryName(targetFile);
                if (!string.IsNullOrWhiteSpace(targetDir))
                {
                    Directory.CreateDirectory(targetDir);
                }

                if (IsSameFileContent(sourceFile, targetFile))
                {
                    continue;
                }

                if (!FileTools.CopyFile(sourceFile, targetFile, true, true))
                {
                    throw new IOException($"USB content copy failed. source={sourceFile}, target={targetFile}");
                }
            }
        }

        private void ApplyDataToLiteDb(UsbUpdatePackage package)
        {
            var playlist = package.Playlist;
            string playlistName = playlist.PageList.PLI_PageListName;

            using (var pageListRepo = new PageListRepository())
            using (var pageRepo = new PageRepository())
            {
                var pageLists = pageListRepo.LoadAll() ?? new List<PageListInfoClass>();
                pageLists.RemoveAll(x => x != null && string.Equals(x.PLI_PageListName, playlistName, StringComparison.OrdinalIgnoreCase));
                pageLists.Add(playlist.PageList);
                pageListRepo.ReplaceAll(pageLists);

                var pages = pageRepo.LoadAll() ?? new List<PageInfoClass>();
                var incomingPageIds = new HashSet<string>(playlist.Pages.Select(x => x?.PIC_GUID ?? string.Empty), StringComparer.OrdinalIgnoreCase);
                pages.RemoveAll(x => x != null && incomingPageIds.Contains(x.PIC_GUID ?? string.Empty));
                pages.AddRange(playlist.Pages);
                pageRepo.ReplaceAll(pages);
            }

            ApplyPlayerInfo(package);
            ApplyWeeklySchedule(package);
            ApplySpecialSchedule(package);
        }

        private void ApplyPlayerInfo(UsbUpdatePackage package)
        {
            var manager = owner.g_PlayerInfoManager;
            var local = manager?.g_PlayerInfo;
            if (local == null)
            {
                return;
            }

            PlayerInfoClass packagePlayer = package.MatchedPlayer;
            if (packagePlayer != null)
            {
                if (!string.IsNullOrWhiteSpace(packagePlayer.PIF_GUID))
                {
                    local.PIF_GUID = packagePlayer.PIF_GUID;
                }

                if (!string.IsNullOrWhiteSpace(packagePlayer.PIF_PlayerName))
                {
                    local.PIF_PlayerName = packagePlayer.PIF_PlayerName;
                }

                if (!string.IsNullOrWhiteSpace(packagePlayer.PIF_MacAddress))
                {
                    local.PIF_MacAddress = packagePlayer.PIF_MacAddress;
                }

                local.PIF_IsLandScape = packagePlayer.PIF_IsLandScape;
                if (!string.IsNullOrWhiteSpace(packagePlayer.PIF_OSName))
                {
                    local.PIF_OSName = packagePlayer.PIF_OSName;
                }
            }

            string playlistName = package.Playlist.PageList.PLI_PageListName;
            local.PIF_CurrentPlayList = playlistName;
            local.PIF_DefaultPlayList = playlistName;
            manager.SaveData();
            owner.g_PlayerName = local.PIF_PlayerName;
        }

        private void ApplyWeeklySchedule(UsbUpdatePackage package)
        {
            var item = SelectWeeklyItem(package);
            if (item?.Schedule == null)
            {
                return;
            }

            var player = owner.g_PlayerInfoManager?.g_PlayerInfo;
            string playerId = player?.PIF_GUID;
            string playerName = player?.PIF_PlayerName;

            var local = new SharedWeeklyPlayScheduleInfo
            {
                Id = string.IsNullOrWhiteSpace(item.Schedule.Id) ? (string.IsNullOrWhiteSpace(playerId) ? playerName : playerId) : item.Schedule.Id.Trim(),
                PlayerID = string.IsNullOrWhiteSpace(item.Schedule.PlayerID) ? (string.IsNullOrWhiteSpace(playerId) ? playerName : playerId) : item.Schedule.PlayerID.Trim(),
                PlayerName = string.IsNullOrWhiteSpace(item.Schedule.PlayerName) ? (playerName ?? string.Empty) : item.Schedule.PlayerName.Trim(),
                MonSch = item.Schedule.MonSch ?? DaySchedule.CreateDefault(),
                TueSch = item.Schedule.TueSch ?? DaySchedule.CreateDefault(),
                WedSch = item.Schedule.WedSch ?? DaySchedule.CreateDefault(),
                ThuSch = item.Schedule.ThuSch ?? DaySchedule.CreateDefault(),
                FriSch = item.Schedule.FriSch ?? DaySchedule.CreateDefault(),
                SatSch = item.Schedule.SatSch ?? DaySchedule.CreateDefault(),
                SunSch = item.Schedule.SunSch ?? DaySchedule.CreateDefault()
            };

            if (string.IsNullOrWhiteSpace(local.Id) || string.IsNullOrWhiteSpace(local.PlayerID))
            {
                return;
            }

            using (var repo = new WeeklyScheduleRepository())
            {
                repo.DeleteMany(x =>
                    (!string.IsNullOrWhiteSpace(local.Id) && string.Equals(x.Id, local.Id, StringComparison.OrdinalIgnoreCase))
                    || (!string.IsNullOrWhiteSpace(local.PlayerID) && string.Equals(x.PlayerID, local.PlayerID, StringComparison.OrdinalIgnoreCase))
                    || (!string.IsNullOrWhiteSpace(local.PlayerName) && string.Equals(x.PlayerName, local.PlayerName, StringComparison.OrdinalIgnoreCase)));
                repo.Upsert(local);
            }
        }

        private WeeklyScheduleExportItem SelectWeeklyItem(UsbUpdatePackage package)
        {
            var items = package.Weekly?.Items;
            if (items == null || items.Count == 0)
            {
                return null;
            }

            var match = items.FirstOrDefault(x => IsSamePlayer(x?.Player, owner.g_PlayerInfoManager?.g_PlayerInfo, package.MatchedPlayer));
            if (match != null)
            {
                return match;
            }

            return items.Count == 1 ? items[0] : null;
        }

        private void ApplySpecialSchedule(UsbUpdatePackage package)
        {
            if (package.Special == null)
            {
                return;
            }

            var player = owner.g_PlayerInfoManager?.g_PlayerInfo;
            if (player == null)
            {
                return;
            }

            string cacheId = string.IsNullOrWhiteSpace(player.PIF_GUID) ? player.PIF_PlayerName : player.PIF_GUID;
            if (string.IsNullOrWhiteSpace(cacheId))
            {
                return;
            }

            var schedules = new List<SpecialSchedulePayload>();
            foreach (var item in SelectSpecialItems(package))
            {
                foreach (var schedule in item.Schedules ?? new List<SpecialScheduleInfoExport>())
                {
                    if (schedule == null || !ScheduleMatchesPlayer(schedule, player, package.MatchedPlayer))
                    {
                        continue;
                    }

                    schedules.Add(MapSpecialSchedule(schedule));
                }
            }

            schedules = schedules
                .Where(x => x != null)
                .GroupBy(x => string.IsNullOrWhiteSpace(x.Id) ? x.PageListName : x.Id, StringComparer.OrdinalIgnoreCase)
                .Select(x => x.First())
                .ToList();

            var cache = new SpecialScheduleCache
            {
                Id = cacheId,
                PlayerId = player.PIF_GUID ?? string.Empty,
                PlayerName = player.PIF_PlayerName ?? string.Empty,
                UpdatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                Schedules = schedules,
                Playlists = BuildSpecialSchedulePlaylists(package, schedules)
            };

            using (var repo = new SpecialScheduleCacheRepository())
            {
                repo.Upsert(cache);
            }
        }

        private IEnumerable<PlayerSpecialScheduleExport> SelectSpecialItems(UsbUpdatePackage package)
        {
            var items = package.Special?.Items;
            if (items == null || items.Count == 0)
            {
                return Enumerable.Empty<PlayerSpecialScheduleExport>();
            }

            var matches = items
                .Where(x => IsSamePlayer(x?.Player, owner.g_PlayerInfoManager?.g_PlayerInfo, package.MatchedPlayer))
                .ToList();
            if (matches.Count > 0)
            {
                return matches;
            }

            return items.Count == 1 ? new[] { items[0] } : Enumerable.Empty<PlayerSpecialScheduleExport>();
        }

        private List<SchedulePlaylistPayload> BuildSpecialSchedulePlaylists(UsbUpdatePackage package, IEnumerable<SpecialSchedulePayload> schedules)
        {
            var result = new List<SchedulePlaylistPayload>();
            var playlistNames = (schedules ?? Enumerable.Empty<SpecialSchedulePayload>())
                .Where(x => x != null && !string.IsNullOrWhiteSpace(x.PageListName))
                .Select(x => x.PageListName.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (string playlistName in playlistNames)
            {
                SchedulePlaylistPayload payload = BuildSchedulePlaylistPayload(package, playlistName);
                if (payload != null)
                {
                    result.Add(payload);
                }
            }

            return result;
        }

        private SchedulePlaylistPayload BuildSchedulePlaylistPayload(UsbUpdatePackage package, string playlistName)
        {
            if (string.Equals(package.Playlist.PageList.PLI_PageListName, playlistName, StringComparison.OrdinalIgnoreCase))
            {
                return new SchedulePlaylistPayload
                {
                    PlaylistName = playlistName,
                    PageList = package.Playlist.PageList,
                    Pages = package.Playlist.Pages.Cast<SharedPageInfoClass>().ToList()
                };
            }

            using (var pageListRepo = new PageListRepository())
            using (var pageRepo = new PageRepository())
            {
                var pageList = pageListRepo.FindOne(x => x != null && string.Equals(x.PLI_PageListName, playlistName, StringComparison.OrdinalIgnoreCase));
                if (pageList?.PLI_Pages == null || pageList.PLI_Pages.Count == 0)
                {
                    return null;
                }

                var pageMap = (pageRepo.LoadAll() ?? new List<PageInfoClass>())
                    .Where(x => x != null && !string.IsNullOrWhiteSpace(x.PIC_GUID))
                    .GroupBy(x => x.PIC_GUID, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(x => x.Key, x => x.First(), StringComparer.OrdinalIgnoreCase);

                var pages = new List<SharedPageInfoClass>();
                foreach (string pageId in pageList.PLI_Pages)
                {
                    if (!string.IsNullOrWhiteSpace(pageId) && pageMap.TryGetValue(pageId, out var page))
                    {
                        pages.Add(page);
                    }
                }

                if (pages.Count == 0)
                {
                    return null;
                }

                return new SchedulePlaylistPayload
                {
                    PlaylistName = playlistName,
                    PageList = pageList,
                    Pages = pages
                };
            }
        }

        private static SpecialSchedulePayload MapSpecialSchedule(SpecialScheduleInfoExport schedule)
        {
            if (schedule == null)
            {
                return null;
            }

            return new SpecialSchedulePayload
            {
                Id = schedule.GUID ?? string.Empty,
                PageListName = schedule.PageListName ?? string.Empty,
                DayOfWeek1 = schedule.DayOfWeek1,
                DayOfWeek2 = schedule.DayOfWeek2,
                DayOfWeek3 = schedule.DayOfWeek3,
                DayOfWeek4 = schedule.DayOfWeek4,
                DayOfWeek5 = schedule.DayOfWeek5,
                DayOfWeek6 = schedule.DayOfWeek6,
                DayOfWeek7 = schedule.DayOfWeek7,
                IsPeriodEnable = schedule.IsPeriodEnable,
                DisplayStartH = schedule.DisplayStartH,
                DisplayStartM = schedule.DisplayStartM,
                DisplayEndH = schedule.DisplayEndH,
                DisplayEndM = schedule.DisplayEndM,
                PeriodStartYear = schedule.PeriodStartYear,
                PeriodStartMonth = schedule.PeriodStartMonth,
                PeriodStartDay = schedule.PeriodStartDay,
                PeriodEndYear = schedule.PeriodEndYear,
                PeriodEndMonth = schedule.PeriodEndMonth,
                PeriodEndDay = schedule.PeriodEndDay
            };
        }

        private static bool ScheduleMatchesPlayer(SpecialScheduleInfoExport schedule, PlayerInfoClass current, PlayerInfoClass packagePlayer)
        {
            if (schedule?.PlayerNames == null || schedule.PlayerNames.Count == 0)
            {
                return true;
            }

            return ContainsIgnoreCase(schedule.PlayerNames, current?.PIF_PlayerName)
                || ContainsIgnoreCase(schedule.PlayerNames, packagePlayer?.PIF_PlayerName);
        }

        private PlayerInfoClass ResolvePackagePlayer(IEnumerable<PlayerInfoClass> players)
        {
            var list = players?.Where(x => x != null).ToList() ?? new List<PlayerInfoClass>();
            if (list.Count == 0)
            {
                return null;
            }

            var current = owner.g_PlayerInfoManager?.g_PlayerInfo;
            return list.FirstOrDefault(x => IsSamePlayer(x, current, null));
        }

        private bool IsSamePlayer(PlayerInfoClass candidate, PlayerInfoClass current, PlayerInfoClass packagePlayer)
        {
            if (candidate == null)
            {
                return false;
            }

            if (packagePlayer != null && !string.IsNullOrWhiteSpace(packagePlayer.PIF_GUID)
                && string.Equals(candidate.PIF_GUID, packagePlayer.PIF_GUID, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (current == null)
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(candidate.PIF_GUID) && !string.IsNullOrWhiteSpace(current.PIF_GUID)
                && string.Equals(candidate.PIF_GUID, current.PIF_GUID, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (!string.IsNullOrWhiteSpace(candidate.PIF_PlayerName) && !string.IsNullOrWhiteSpace(current.PIF_PlayerName)
                && string.Equals(candidate.PIF_PlayerName, current.PIF_PlayerName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (!string.IsNullOrWhiteSpace(candidate.PIF_MacAddress) && !string.IsNullOrWhiteSpace(current.PIF_MacAddress)
                && string.Equals(NormalizeMac(candidate.PIF_MacAddress), NormalizeMac(current.PIF_MacAddress), StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return false;
        }

        private static void NormalizePlaylistContentPaths(PlaylistExportBundle playlist)
        {
            foreach (var page in playlist.Pages ?? new List<PageInfoClass>())
            {
                if (page?.PIC_Elements == null)
                {
                    continue;
                }

                foreach (var element in page.PIC_Elements)
                {
                    if (element?.EIF_ContentsInfoClassList == null)
                    {
                        continue;
                    }

                    foreach (var content in element.EIF_ContentsInfoClassList)
                    {
                        if (content == null)
                        {
                            continue;
                        }

                        if (string.IsNullOrWhiteSpace(content.CIF_FileName) && !string.IsNullOrWhiteSpace(content.CIF_FileFullPath))
                        {
                            content.CIF_FileName = Path.GetFileName(content.CIF_FileFullPath);
                        }

                        if (!string.IsNullOrWhiteSpace(content.CIF_FileName))
                        {
                            content.CIF_FileFullPath = FNDTools.GetContentsFilePath(content.CIF_FileName);
                            content.CIF_RelativePath = Path.Combine(ContentsDirName, content.CIF_FileName).Replace('\\', '/');
                            content.CIF_FileExist = File.Exists(content.CIF_FileFullPath);
                        }
                    }
                }
            }
        }

        private string BuildPackageIdentity(string packageRoot, string contentRoot)
        {
            string playlistPath = Path.Combine(packageRoot, PlaylistFileName);
            if (!File.Exists(playlistPath))
            {
                return string.Empty;
            }

            var sb = new StringBuilder();
            sb.Append(Path.GetFullPath(packageRoot)).Append('|');
            AppendFileIdentity(sb, playlistPath);
            AppendFileIdentity(sb, Path.Combine(packageRoot, WeeklyScheduleFileName));
            AppendFileIdentity(sb, Path.Combine(packageRoot, SpecialScheduleFileName));

            string contentsPath = contentRoot;
            if (Directory.Exists(contentsPath))
            {
                var files = Directory.GetFiles(contentsPath, "*", SearchOption.AllDirectories)
                    .OrderBy(x => x, StringComparer.OrdinalIgnoreCase);
                foreach (string file in files)
                {
                    sb.Append('|').Append(GetRelativePath(contentsPath, file));
                    AppendFileIdentity(sb, file);
                }
            }

            return BuildShortHash(sb.ToString());
        }

        private static void AppendFileIdentity(StringBuilder sb, string path)
        {
            if (!File.Exists(path))
            {
                sb.Append("|missing");
                return;
            }

            var info = new FileInfo(path);
            sb.Append('|').Append(info.Name).Append(':').Append(info.Length).Append(':').Append(info.LastWriteTimeUtc.Ticks);
        }

        private static bool IsSameFileContent(string sourceFile, string targetFile)
        {
            if (!File.Exists(sourceFile) || !File.Exists(targetFile))
            {
                return false;
            }

            if (new FileInfo(sourceFile).Length != new FileInfo(targetFile).Length)
            {
                return false;
            }

            return string.Equals(
                XXHash64.ComputePartialSignature(sourceFile),
                XXHash64.ComputePartialSignature(targetFile),
                StringComparison.OrdinalIgnoreCase);
        }

        private static string GetRelativePath(string root, string path)
        {
            Uri rootUri = new Uri(EnsureTrailingSeparator(Path.GetFullPath(root)));
            Uri pathUri = new Uri(Path.GetFullPath(path));
            return Uri.UnescapeDataString(rootUri.MakeRelativeUri(pathUri).ToString())
                .Replace('/', Path.DirectorySeparatorChar);
        }

        private static string EnsureTrailingSeparator(string path)
        {
            if (path.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal))
            {
                return path;
            }

            return path + Path.DirectorySeparatorChar;
        }

        private static string BuildShortHash(string text)
        {
            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(text ?? string.Empty));
                return BitConverter.ToString(hash, 0, 12).Replace("-", string.Empty);
            }
        }

        private static string NormalizeMac(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            return value.Replace(":", string.Empty).Replace("-", string.Empty).Trim();
        }

        private HashSet<string> BuildCurrentDeviceIdentityCandidates()
        {
            var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            try
            {
                AddDeviceIdentityCandidate(result, LicenseHubDeviceFingerprint.Generate().Fingerprint);
            }
            catch (Exception ex)
            {
                Logger.WriteErrorLog($"UsbUpdateService fingerprint generation failed: {ex}", Logger.GetLogFileName());
            }

            AddDeviceIdentityCandidate(result, owner.g_PlayerInfoManager?.g_PlayerInfo?.PIF_MacAddress);
            return result;
        }

        private static void AddDeviceIdentityCandidate(HashSet<string> values, string value)
        {
            if (values == null)
            {
                return;
            }

            string normalized = NormalizeDeviceIdentity(value);
            if (!string.IsNullOrWhiteSpace(normalized))
            {
                values.Add(normalized);
            }
        }

        private static string NormalizeDeviceIdentity(string value)
        {
            return SanitizeDeviceIdentity(value);
        }

        private static string SanitizeDeviceIdentity(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            string trimmed = value.Trim().ToUpperInvariant();
            HashSet<char> invalidChars = new HashSet<char>(Path.GetInvalidFileNameChars());
            StringBuilder builder = new StringBuilder(trimmed.Length);
            foreach (char ch in trimmed)
            {
                builder.Append(invalidChars.Contains(ch) ? '_' : ch);
            }

            return builder.ToString();
        }

        private static bool ContainsIgnoreCase(IEnumerable<string> values, string value)
        {
            if (string.IsNullOrWhiteSpace(value) || values == null)
            {
                return false;
            }

            return values.Any(x => string.Equals(x?.Trim(), value.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        private sealed class UsbUpdatePackage
        {
            public string UsbRoot { get; set; }
            public string PackageId { get; set; }
            public string StagingContentsPath { get; set; }
            public string ContentSourcePath { get; set; }
            public bool IsTargetedStructure { get; set; }
            public PlaylistExportBundle Playlist { get; set; }
            public WeeklyScheduleExportBundle Weekly { get; set; }
            public SpecialScheduleExportBundle Special { get; set; }
            public PlayerInfoClass MatchedPlayer { get; set; }
        }

        private sealed class UsbPackageSelection
        {
            public string PackageDir { get; set; }
            public string ContentRoot { get; set; }
            public bool IsTargetedStructure { get; set; }
        }

        private sealed class ContentCopySpec
        {
            public string FileName { get; set; }
            public string RelativePath { get; set; }
            public long SizeBytes { get; set; }
            public string Hash { get; set; }
        }

        private sealed class UsbTargetsManifest
        {
            [JsonProperty("version")]
            public int Version { get; set; }

            [JsonProperty("exportedAt")]
            public string ExportedAt { get; set; }

            [JsonProperty("targets")]
            public List<UsbTargetInfo> Targets { get; set; } = new List<UsbTargetInfo>();
        }

        private sealed class UsbTargetInfo
        {
            [JsonProperty("folder")]
            public string Folder { get; set; }

            [JsonProperty("playerName")]
            public string PlayerName { get; set; }

            [JsonProperty("playerGuid")]
            public string PlayerGuid { get; set; }

            [JsonProperty("deviceIdentity")]
            public string DeviceIdentity { get; set; }
        }

        private sealed class PlaylistExportBundle
        {
            public string PlaylistName { get; set; }
            public PageListInfoClass PageList { get; set; }
            public List<PageInfoClass> Pages { get; set; } = new List<PageInfoClass>();
            public List<PlayerInfoClass> Players { get; set; } = new List<PlayerInfoClass>();
            public DateTime ExportedAt { get; set; }
        }

        private sealed class WeeklyScheduleExportBundle
        {
            public DateTime ExportedAt { get; set; }
            public List<WeeklyScheduleExportItem> Items { get; set; } = new List<WeeklyScheduleExportItem>();
        }

        private sealed class WeeklyScheduleExportItem
        {
            public PlayerInfoClass Player { get; set; }
            public SharedWeeklyPlayScheduleInfo Schedule { get; set; }
            public List<WeeklyDayScheduleInfo> DailySchedules { get; set; } = new List<WeeklyDayScheduleInfo>();
        }

        private sealed class SpecialScheduleExportBundle
        {
            public DateTime ExportedAt { get; set; }
            public List<PlayerSpecialScheduleExport> Items { get; set; } = new List<PlayerSpecialScheduleExport>();
        }

        private sealed class PlayerSpecialScheduleExport
        {
            public PlayerInfoClass Player { get; set; }
            public List<SpecialScheduleInfoExport> Schedules { get; set; } = new List<SpecialScheduleInfoExport>();
        }

        private sealed class SpecialScheduleInfoExport
        {
            [JsonProperty("id")]
            public string GUID = string.Empty;
            public List<string> PlayerNames = new List<string>();
            public List<string> GroupNames = new List<string>();
            public string PageListName = string.Empty;
            public bool DayOfWeek1;
            public bool DayOfWeek2;
            public bool DayOfWeek3;
            public bool DayOfWeek4;
            public bool DayOfWeek5;
            public bool DayOfWeek6;
            public bool DayOfWeek7;
            public bool IsPeriodEnable;
            public int DisplayStartH;
            public int DisplayStartM;
            public int DisplayEndH;
            public int DisplayEndM;
            public int PeriodStartYear;
            public int PeriodStartMonth;
            public int PeriodStartDay;
            public int PeriodEndYear;
            public int PeriodEndMonth;
            public int PeriodEndDay;
        }
    }
}
