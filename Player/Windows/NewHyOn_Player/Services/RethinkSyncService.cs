using AndoW.Shared;
using NewHyOn.Shared.Auth;
using RethinkDb.Driver;
using RethinkDb.Driver.Ast;
using RethinkDb.Driver.Net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;
using TurtleTools;
using SharedWeeklyPlayScheduleInfo = AndoW.Shared.WeeklyPlayScheduleInfo;

namespace NewHyOnPlayer
{
    internal sealed class RethinkSyncService : IDisposable
    {
        private const string DatabaseName = "NewHyOn";
        private const string PlayerTable = "PlayerInfoManager";
        private const string WeeklyTable = "WeeklyInfoManagerClass";
        private const string SpecialScheduleTable = "SpecialScheduleInfoManager";
        private const string PageListTable = "PageListInfoManager";
        private const string PageTable = "PageInfoManager";
        private const string PeriodTable = "PeriodData";

        private static readonly RethinkDB R = RethinkDB.R;

        private readonly PlayerInfoManager manager;
        private readonly LocalSettingsManager localManager;
        private readonly MultimediaTimer.Timer timer;
        private readonly int intervalMs;
        private string currentHost = "127.0.0.1";
        private Connection connection;
        private int isSyncing;
        private int initialSyncCompleted;
        private bool disposed;
        private bool infoSyncedAfterConnect;
        private bool syncFailedNotified;

        public event Action<bool> InitialSyncCompleted;
        public event Action PlayerSynced;
        public event Action<string> PlayerGuidChanged;
        public event Action WeeklyScheduleSynced;
        public event Action SpecialScheduleSynced;
        public event Action SyncFailed;

        public RethinkSyncService(PlayerInfoManager manager, LocalSettingsManager localManager, int intervalMs = 5000)
        {
            this.manager = manager;
            this.localManager = localManager;
            this.intervalMs = Math.Max(1000, intervalMs);

            timer = new MultimediaTimer.Timer
            {
                Mode = MultimediaTimer.TimerMode.OneShot,
                Period = this.intervalMs,
                Resolution = 1
            };
            timer.Tick += OnElapsed;
        }

        public void Start()
        {
            if (disposed) return;
            TriggerSyncNow(scheduleNext: true);
        }

        public void Stop()
        {
            timer.Stop();
        }

        public void TriggerSyncNow(bool scheduleNext = false)
        {
            TrySync(scheduleNext);
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            try
            {
                timer.Stop();
                timer.Dispose();
            }
            catch
            {
            }
            ResetConnection();
        }

        public List<ContentPeriodPayload> FetchContentPeriodsByGuids(IEnumerable<string> contentGuids)
        {
            var periodMap = FetchContentPeriods(contentGuids);
            return periodMap.Values
                .Where(x => x != null && string.IsNullOrWhiteSpace(x.ContentGuid) == false)
                .Select(x => new ContentPeriodPayload
                {
                    ContentGuid = x.ContentGuid ?? string.Empty,
                    FileName = x.FileName ?? string.Empty,
                    StartDate = x.StartDate ?? string.Empty,
                    EndDate = x.EndDate ?? string.Empty,
                    StartTime = x.StartTime ?? string.Empty,
                    EndTime = x.EndTime ?? string.Empty
                })
                .ToList();
        }

        private void OnElapsed(object sender, EventArgs e)
        {
            TrySync(scheduleNext: true);
        }

        private void TrySync(bool scheduleNext)
        {
            if (Interlocked.Exchange(ref isSyncing, 1) == 1)
            {
                return;
            }

            ThreadPool.QueueUserWorkItem(_ =>
            {
                bool syncSucceeded = false;
                try
                {
                    syncSucceeded = GetPlayerGuidOnce();
                }
                finally
                {
                    NotifyInitialSyncCompleted(syncSucceeded);
                    Interlocked.Exchange(ref isSyncing, 0);
                    if (scheduleNext)
                    {
                        ScheduleNext();
                    }
                }
            });
        }

        private void ScheduleNext()
        {
            if (disposed) return;
            timer.Stop();
            timer.Period = intervalMs;
            timer.Start();
        }

        private bool GetPlayerGuidOnce()
        {
            try
            {
                if (manager == null || manager.g_PlayerInfo == null || localManager == null || localManager.Settings == null)
                {
                    return false;
                }

                RefreshLocalNetworkInfo();

                string host = localManager.Settings.ManagerIP;
                UpdateHost(host);

                string playerName = NormalizePlayerName(manager.g_PlayerInfo.PIF_PlayerName);
                if (!string.Equals(playerName, manager.g_PlayerInfo.PIF_PlayerName, StringComparison.Ordinal))
                {
                    manager.g_PlayerInfo.PIF_PlayerName = playerName;
                    manager.SaveData();
                }
                if (string.IsNullOrWhiteSpace(playerName))
                {
                    return false;
                }

                string localGuid = manager.g_PlayerInfo.PIF_GUID;
                string remoteGuid = null;
                bool lookupSucceeded = TryResolveRemoteGuid(playerName, localGuid, out remoteGuid);
                if (!lookupSucceeded)
                {
                    NotifySyncFailed();
                    return false;
                }

                syncFailedNotified = false;

                if (string.IsNullOrWhiteSpace(remoteGuid))
                {
                    Logger.WriteLog(
                        $"RethinkSyncService waiting for manager-registered player data. localGuid='{localGuid}', playerName='{playerName}'.",
                        Logger.GetLogFileName());
                    return false;
                }

                bool guidChanged = !string.Equals(remoteGuid, localGuid, StringComparison.OrdinalIgnoreCase);
                if (guidChanged)
                {
                    manager.g_PlayerInfo.PIF_GUID = remoteGuid;
                    manager.SaveData();
                }

                UpdatePlayerInfoToRethink(remoteGuid);

                bool shouldSyncSchedulesForConnection = guidChanged || !infoSyncedAfterConnect;
                bool shouldNotifyPlayerSynced = shouldSyncSchedulesForConnection;
                infoSyncedAfterConnect = true;

                if (guidChanged)
                {
                    PlayerGuidChanged?.Invoke(remoteGuid);
                }

                if (shouldSyncSchedulesForConnection)
                {
                    SyncWeeklySchedule(remoteGuid, manager.g_PlayerInfo.PIF_PlayerName);
                    SyncSpecialSchedule(remoteGuid, manager.g_PlayerInfo.PIF_PlayerName);
                }

                if (shouldNotifyPlayerSynced)
                {
                    PlayerSynced?.Invoke();
                }

                return true;
            }
            catch (Exception ex)
            {
                Logger.WriteErrorLog(ex.ToString(), Logger.GetLogFileName());
                ResetConnection();
                NotifySyncFailed();
                return false;
            }
        }

        private void NotifyInitialSyncCompleted(bool syncSucceeded)
        {
            if (Interlocked.Exchange(ref initialSyncCompleted, 1) == 1)
            {
                return;
            }

            InitialSyncCompleted?.Invoke(syncSucceeded);
        }

        private void UpdateHost(string host)
        {
            string normalized = string.IsNullOrWhiteSpace(host) ? "127.0.0.1" : host;
            if (!string.Equals(normalized, currentHost, StringComparison.OrdinalIgnoreCase))
            {
                currentHost = normalized;
                ResetConnection();
            }
        }

        private bool TryResolveRemoteGuid(string playerName, string localGuid, out string guid)
        {
            guid = null;

            if (string.IsNullOrWhiteSpace(playerName))
            {
                return true;
            }

            string normalizedName = NormalizePlayerName(playerName);

            try
            {
                var conn = GetConnection();
                if (conn == null)
                {
                    return false;
                }

                if (!string.IsNullOrWhiteSpace(localGuid))
                {
                    PlayerInfoClass playerByGuid = R.Db(DatabaseName)
                        .Table(PlayerTable)
                        .Get(localGuid)
                        .RunAtom<PlayerInfoClass>(conn);
                    if (playerByGuid != null && IsSamePlayerName(playerByGuid.PIF_PlayerName, normalizedName))
                    {
                        guid = playerByGuid.PIF_GUID;
                        return true;
                    }
                }

                var candidates = R.Db(DatabaseName)
                    .Table(PlayerTable)
                    .RunCursor<PlayerInfoClass>(conn)
                    .BufferedItems
                    .Where(player => IsSamePlayerName(player?.PIF_PlayerName, normalizedName))
                    .ToList();
                PlayerInfoClass playerInfo = SelectCanonicalPlayer(candidates, localGuid);
                guid = playerInfo == null ? string.Empty : playerInfo.PIF_GUID;
                return true;
            }
            catch (Exception ex)
            {
                Logger.WriteErrorLog(ex.ToString(), Logger.GetLogFileName());
                return false;
            }
        }

        private static PlayerInfoClass SelectCanonicalPlayer(IEnumerable<PlayerInfoClass> candidates, string localGuid)
        {
            var list = candidates?
                .Where(player => player != null && !string.IsNullOrWhiteSpace(player.PIF_GUID))
                .ToList() ?? new List<PlayerInfoClass>();
            if (list.Count == 0)
            {
                return null;
            }

            return list
                .OrderByDescending(player => !string.IsNullOrWhiteSpace(localGuid)
                    && string.Equals(player.PIF_GUID, localGuid, StringComparison.OrdinalIgnoreCase))
                .ThenByDescending(HasPlayableConfiguration)
                .ThenBy(player => player.PIF_GUID, StringComparer.OrdinalIgnoreCase)
                .First();
        }

        private static bool HasPlayableConfiguration(PlayerInfoClass player)
        {
            if (player == null)
            {
                return false;
            }

            return !string.IsNullOrWhiteSpace(player.PIF_CurrentPlayList)
                || !string.IsNullOrWhiteSpace(player.PIF_DefaultPlayList);
        }

        private static bool IsSamePlayerName(string left, string right)
        {
            return string.Equals(NormalizePlayerName(left), NormalizePlayerName(right), StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizePlayerName(string playerName)
        {
            return (playerName ?? string.Empty).Trim();
        }

        private Connection GetConnection()
        {
            try
            {
                if (connection != null && connection.Open)
                {
                    return connection;
                }

                connection = R.Connection()
                    .Hostname(currentHost)
                    .Port(28015)
                    .User("admin", "turtle04!9")
                    .Connect();

                infoSyncedAfterConnect = false;
                return connection;
            }
            catch (Exception ex)
            {
                Logger.WriteErrorLog(ex.ToString(), Logger.GetLogFileName());
                ResetConnection();
                return null;
            }
        }

        private bool EnsureTableExists(string tableName)
        {
            if (string.IsNullOrWhiteSpace(tableName))
            {
                return false;
            }

            try
            {
                var conn = GetConnection();
                if (conn == null)
                {
                    return false;
                }

                var tables = R.Db(DatabaseName).TableList().RunAtom<List<string>>(conn) ?? new List<string>();
                if (tables.Any(x => string.Equals(x, tableName, StringComparison.OrdinalIgnoreCase)))
                {
                    return true;
                }

                R.Db(DatabaseName).TableCreate(tableName).Run(conn);
                Logger.WriteLog($"RethinkSyncService created missing table: {tableName}", Logger.GetLogFileName());
                return true;
            }
            catch (Exception ex)
            {
                Logger.WriteErrorLog($"EnsureTableExists({tableName}) error: {ex}", Logger.GetLogFileName());
                ResetConnection();
                return false;
            }
        }

        private void UpdatePlayerInfoToRethink(string remoteGuid)
        {
            try
            {
                var player = manager?.g_PlayerInfo;
                if (player == null || string.IsNullOrWhiteSpace(remoteGuid))
                {
                    return;
                }

                var conn = GetConnection();
                if (conn == null)
                {
                    return;
                }

                var exists = R.Db(DatabaseName)
                    .Table(PlayerTable)
                    .Get(remoteGuid)
                    .RunAtom<Dictionary<string, object>>(conn);
                if (exists == null)
                {
                    return;
                }

                ResolveCurrentNetworkInfo(out string localIp, out string mac, out List<string> allMacs);
                string osName = string.IsNullOrWhiteSpace(player.PIF_OSName)
                    ? Environment.OSVersion.ToString()
                    : player.PIF_OSName;
                string authPayload = ResolveOutboundAuthPayload();

                if (!string.IsNullOrWhiteSpace(localIp))
                {
                    UpdateLocalNetworkInfo(localIp);
                }

                var payload = new Dictionary<string, object>
                {
                    ["PIF_PlayerName"] = player.PIF_PlayerName ?? string.Empty,
                    ["PIF_CurrentPlayList"] = player.PIF_CurrentPlayList ?? string.Empty,
                    ["PIF_DefaultPlayList"] = player.PIF_DefaultPlayList ?? string.Empty,
                    ["PIF_IPAddress"] = string.IsNullOrWhiteSpace(localIp) ? player.PIF_IPAddress ?? string.Empty : localIp,
                    ["PIF_OSName"] = osName,
                    ["PIF_MacAddress"] = player.PIF_MacAddress ?? string.Empty,
                    ["PIF_AuthKey"] = authPayload,
                    ["command"] = player.PendingCommand ?? string.Empty
                };

                R.Db(DatabaseName)
                    .Table(PlayerTable)
                    .Get(remoteGuid)
                    .Update(payload)
                    .RunNoReply(conn);
            }
            catch (Exception ex)
            {
                Logger.WriteErrorLog(ex.ToString(), Logger.GetLogFileName());
                ResetConnection();
            }
        }

        private string ResolveOutboundAuthPayload()
        {
            LicenseHubLocalLicenseFile license = LicenseHubLocalLicenseStore.Read();
            var validation = LicenseHubLocalValidator.ValidateForCurrentDevice(license);
            if (!validation.IsValid)
            {
                ClearStoredAuthKey();
                return string.Empty;
            }

            string authMarker = LicenseHubAuthMarker.Build(license);
            if (string.IsNullOrWhiteSpace(authMarker))
            {
                ClearStoredAuthKey();
                return string.Empty;
            }

            var player = manager?.g_PlayerInfo;
            if (player != null && !string.Equals(player.PIF_AuthKey ?? string.Empty, authMarker, StringComparison.Ordinal))
            {
                player.PIF_AuthKey = authMarker;
                manager.SaveData();
            }

            return authMarker;
        }

        private void ClearStoredAuthKey()
        {
            var player = manager?.g_PlayerInfo;
            if (player == null || string.IsNullOrWhiteSpace(player.PIF_AuthKey))
            {
                return;
            }

            player.PIF_AuthKey = string.Empty;
            manager.SaveData();
        }

        private void ResetConnection()
        {
            try
            {
                if (connection != null)
                {
                    connection.Close(false);
                    connection.Dispose();
                }
            }
            catch
            {
            }
            finally
            {
                connection = null;
                infoSyncedAfterConnect = false;
            }
        }

        private void RefreshLocalNetworkInfo()
        {
            ResolveCurrentNetworkInfo(out string localIp, out string mac, out List<string> allMacs);
            if (!string.IsNullOrWhiteSpace(localIp))
            {
                UpdateLocalNetworkInfo(localIp);
            }
        }

        private void UpdateLocalNetworkInfo(string localIp)
        {
            var player = manager?.g_PlayerInfo;
            if (player == null)
            {
                return;
            }

            bool changed = false;
            if (!string.IsNullOrWhiteSpace(localIp) &&
                !string.Equals(player.PIF_IPAddress ?? string.Empty, localIp, StringComparison.OrdinalIgnoreCase))
            {
                player.PIF_IPAddress = localIp;
                changed = true;
            }

            if (changed)
            {
                manager.SaveData();
            }
        }

        private void ResolveCurrentNetworkInfo(out string localIp, out string mac, out List<string> allMacs)
        {
            localIp = string.Empty;
            mac = string.Empty;
            allMacs = new List<string>();

            try
            {
                foreach (NetworkInterface adapter in NetworkInterface.GetAllNetworkInterfaces())
                {
                    string addr = adapter.GetPhysicalAddress()?.ToString() ?? string.Empty;
                    if (!string.IsNullOrWhiteSpace(addr))
                    {
                        allMacs.Add(addr);
                    }
                }
            }
            catch
            {
            }

            IPAddress currentIp = null;
            try
            {
                currentIp = NetworkTools.GetAutoIP();
            }
            catch
            {
            }

            if (currentIp != null && currentIp.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(currentIp))
            {
                localIp = currentIp.ToString();
            }

            if (currentIp != null)
            {
                mac = ResolveMacForIp(currentIp);
            }

            if (string.IsNullOrWhiteSpace(mac) && allMacs.Count > 0)
            {
                mac = allMacs[0];
            }
        }

        private string ResolveMacForIp(IPAddress ip)
        {
            if (ip == null)
            {
                return string.Empty;
            }

            try
            {
                foreach (NetworkInterface adapter in NetworkInterface.GetAllNetworkInterfaces())
                {
                    var properties = adapter.GetIPProperties();
                    foreach (var addr in properties.UnicastAddresses)
                    {
                        if (addr.Address.AddressFamily == AddressFamily.InterNetwork && addr.Address.Equals(ip))
                        {
                            return adapter.GetPhysicalAddress()?.ToString() ?? string.Empty;
                        }
                    }
                }
            }
            catch
            {
            }

            return string.Empty;
        }

        private PlayerInfoClass FetchPlayerByGuid(string playerGuid)
        {
            if (string.IsNullOrWhiteSpace(playerGuid))
            {
                return null;
            }

            try
            {
                var conn = GetConnection();
                if (conn == null)
                {
                    return null;
                }

                return R.Db(DatabaseName)
                    .Table(PlayerTable)
                    .Get(playerGuid)
                    .RunAtom<PlayerInfoClass>(conn);
            }
            catch (Exception ex)
            {
                Logger.WriteErrorLog(ex.ToString(), Logger.GetLogFileName());
                ResetConnection();
                return null;
            }
        }

        private void NotifySyncFailed()
        {
            if (syncFailedNotified)
            {
                return;
            }

            syncFailedNotified = true;
            SyncFailed?.Invoke();
        }

        private void SyncSpecialSchedule(string playerId, string playerName)
        {
            if (string.IsNullOrWhiteSpace(playerName))
            {
                return;
            }

            try
            {
                var schedules = FetchSpecialSchedules(playerName);
                var mappedSchedules = schedules
                    .Select(MapSpecialSchedule)
                    .Where(x => x != null)
                    .ToList();

                var playlistNames = mappedSchedules
                    .Where(x => !string.IsNullOrWhiteSpace(x.PageListName))
                    .Select(x => x.PageListName.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                var playlists = new List<SchedulePlaylistPayload>();
                foreach (string playlistName in playlistNames)
                {
                    var playlist = BuildSchedulePlaylistPayload(playerId, playerName, playlistName);
                    if (playlist != null)
                    {
                        playlists.Add(playlist);
                    }
                }

                string cacheId = string.IsNullOrWhiteSpace(playerId) ? playerName : playerId;
                if (string.IsNullOrWhiteSpace(cacheId))
                {
                    return;
                }

                var cache = new SpecialScheduleCache
                {
                    Id = cacheId,
                    PlayerId = playerId ?? string.Empty,
                    PlayerName = playerName ?? string.Empty,
                    UpdatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    Schedules = mappedSchedules,
                    Playlists = playlists
                };

                using (var repo = new SpecialScheduleCacheRepository())
                {
                    repo.Upsert(cache);
                }

                Logger.WriteLog($"RethinkSyncService special schedule synced. playerId={cache.PlayerId}, playerName={cache.PlayerName}, schedules={cache.Schedules.Count}, playlists={cache.Playlists.Count}", Logger.GetLogFileName());
                SpecialScheduleSynced?.Invoke();
            }
            catch (Exception ex)
            {
                Logger.WriteErrorLog($"SyncSpecialSchedule error: {ex}", Logger.GetLogFileName());
            }
        }

        private List<RethinkSpecialSchedule> FetchSpecialSchedules(string playerName)
        {
            if (string.IsNullOrWhiteSpace(playerName))
            {
                return new List<RethinkSpecialSchedule>();
            }

            var conn = GetConnection();
            if (conn == null)
            {
                return new List<RethinkSpecialSchedule>();
            }

            return R.Db(DatabaseName)
                .Table(SpecialScheduleTable)
                .Filter(row => row["PlayerNames"].Contains(playerName))
                .RunCursor<RethinkSpecialSchedule>(conn)
                .ToList();
        }

        private SchedulePlaylistPayload BuildSchedulePlaylistPayload(string playerId, string playerName, string playlistName)
        {
            if (string.IsNullOrWhiteSpace(playlistName))
            {
                return null;
            }

            var pageList = FetchPageList(playlistName);
            if (pageList == null || pageList.PLI_Pages == null || pageList.PLI_Pages.Count == 0)
            {
                return null;
            }

            var pages = FetchPages(pageList.PLI_Pages);
            if (pages == null || pages.Count == 0)
            {
                return null;
            }

            return new SchedulePlaylistPayload
            {
                PlaylistName = pageList.PLI_PageListName ?? playlistName,
                PageList = pageList,
                Pages = pages,
                Contract = BuildContractPayload(playerId, playerName, pageList, pages)
            };
        }

        private AndoW.Shared.PageListInfoClass FetchPageList(string playlistName)
        {
            var conn = GetConnection();
            if (conn == null || string.IsNullOrWhiteSpace(playlistName))
            {
                return null;
            }

            return R.Db(DatabaseName)
                .Table(PageListTable)
                .Filter(row => row["PLI_PageListName"].Eq(playlistName))
                .Limit(1)
                .RunCursor<AndoW.Shared.PageListInfoClass>(conn)
                .FirstOrDefault();
        }

        private List<AndoW.Shared.PageInfoClass> FetchPages(IEnumerable<string> pageIds)
        {
            var conn = GetConnection();
            var result = new List<AndoW.Shared.PageInfoClass>();
            if (conn == null || pageIds == null)
            {
                return result;
            }

            foreach (string pageId in pageIds.Where(x => !string.IsNullOrWhiteSpace(x)))
            {
                try
                {
                    var page = R.Db(DatabaseName)
                        .Table(PageTable)
                        .Get(pageId)
                        .RunAtom<AndoW.Shared.PageInfoClass>(conn);
                    if (page != null)
                    {
                        result.Add(page);
                    }
                }
                catch (Exception ex)
                {
                    Logger.WriteErrorLog($"FetchPages pageId={pageId} error: {ex}", Logger.GetLogFileName());
                }
            }

            return result;
        }

        private AndoW.Shared.ContractPlaylistPayload BuildContractPayload(string playerId,
                                                             string playerName,
                                                             AndoW.Shared.PageListInfoClass pageList,
                                                             List<AndoW.Shared.PageInfoClass> pages)
        {
            var payload = new AndoW.Shared.ContractPlaylistPayload
            {
                PlayerId = playerId ?? string.Empty,
                PlayerName = playerName ?? string.Empty,
                PlayerLandscape = manager?.g_PlayerInfo?.PIF_IsLandScape ?? false,
                PlaylistId = pageList?.PLI_PageListName ?? string.Empty,
                PlaylistName = pageList?.PLI_PageListName ?? string.Empty,
                Pages = new List<AndoW.Shared.ContractPagePayload>()
            };

            var orderedIds = pageList?.PLI_Pages ?? new List<string>();
            foreach (var page in pages ?? new List<AndoW.Shared.PageInfoClass>())
            {
                if (page == null)
                {
                    continue;
                }

                var pageEntry = new AndoW.Shared.ContractPagePayload
                {
                    PageId = page.PIC_GUID ?? string.Empty,
                    PageName = page.PIC_PageName ?? string.Empty,
                    OrderIndex = orderedIds.IndexOf(page.PIC_GUID),
                    PlayHour = page.PIC_PlaytimeHour,
                    PlayMinute = page.PIC_PlaytimeMinute,
                    PlaySecond = page.PIC_PlaytimeSecond,
                    Volume = page.PIC_Volume,
                    Landscape = page.PIC_IsLandscape,
                    Elements = new List<AndoW.Shared.ContractElementPayload>()
                };
                if (pageEntry.OrderIndex < 0)
                {
                    pageEntry.OrderIndex = payload.Pages.Count;
                }

                List<AndoW.Shared.ElementInfoClass> elements = page.PIC_Elements == null
                    ? new List<AndoW.Shared.ElementInfoClass>()
                    : page.PIC_Elements;
                foreach (var element in elements)
                {
                    if (element == null)
                    {
                        continue;
                    }

                    string elementId = $"{page.PIC_GUID}_{element.EIF_Name}";
                    var elementEntry = new AndoW.Shared.ContractElementPayload
                    {
                        ElementId = elementId,
                        PageId = page.PIC_GUID ?? string.Empty,
                        Name = element.EIF_Name ?? string.Empty,
                        Type = element.EIF_Type ?? string.Empty,
                        Width = element.EIF_Width,
                        Height = element.EIF_Height,
                        PosLeft = element.EIF_PosLeft,
                        PosTop = element.EIF_PosTop,
                        ZIndex = element.EIF_ZIndex,
                        Muted = element.EIF_IsMuted,
                        Contents = new List<AndoW.Shared.ContractContentPayload>()
                    };

                    var contents = element.EIF_ContentsInfoClassList ?? new List<AndoW.Shared.ContentsInfoClass>();
                    for (int idx = 0; idx < contents.Count; idx++)
                    {
                        var content = contents[idx];
                        if (content == null)
                        {
                            continue;
                        }

                        elementEntry.Contents.Add(new AndoW.Shared.ContractContentPayload
                        {
                            Uid = $"{elementId}_{idx}",
                            ElementId = elementId,
                            FileName = content.CIF_FileName ?? string.Empty,
                            FileFullPath = content.CIF_FileFullPath ?? string.Empty,
                            ContentType = content.CIF_ContentType ?? string.Empty,
                            PlayMinute = content.CIF_PlayMinute ?? string.Empty,
                            PlaySecond = content.CIF_PlaySec ?? string.Empty,
                            Valid = content.CIF_ValidTime,
                            ScrollSpeedSec = content.CIF_ScrollTextSpeedSec,
                            RemoteChecksum = string.IsNullOrWhiteSpace(content.CIF_FileHash) ? content.CIF_StrGUID ?? string.Empty : content.CIF_FileHash,
                            FileSize = content.CIF_FileSize,
                            FileExist = content.CIF_FileExist
                        });
                    }

                    pageEntry.Elements.Add(elementEntry);
                }

                payload.Pages.Add(pageEntry);
            }

            payload.Pages = payload.Pages.OrderBy(x => x.OrderIndex).ToList();
            return payload;
        }

        private Dictionary<string, RethinkContentPeriod> FetchContentPeriods(IEnumerable<string> contentGuids)
        {
            var result = new Dictionary<string, RethinkContentPeriod>(StringComparer.OrdinalIgnoreCase);
            var ids = contentGuids?.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).ToList()
                ?? new List<string>();
            if (ids.Count == 0)
            {
                return result;
            }

            var conn = GetConnection();
            if (conn == null)
            {
                return result;
            }

            try
            {
                var periods = R.Db(DatabaseName)
                    .Table(PeriodTable)
                    .GetAll(ids)
                    .RunCursor<RethinkContentPeriod>(conn);

                foreach (var period in periods ?? Enumerable.Empty<RethinkContentPeriod>())
                {
                    if (period == null || string.IsNullOrWhiteSpace(period.ContentGuid))
                    {
                        continue;
                    }

                    result[period.ContentGuid] = period;
                }
            }
            catch (Exception ex)
            {
                Logger.WriteErrorLog($"FetchContentPeriods error: {ex}", Logger.GetLogFileName());
            }

            return result;
        }

        private static SpecialSchedulePayload MapSpecialSchedule(RethinkSpecialSchedule schedule)
        {
            if (schedule == null)
            {
                return null;
            }

            return new SpecialSchedulePayload
            {
                Id = schedule.Id ?? string.Empty,
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

        private void SyncWeeklySchedule(string playerId, string playerName)
        {
            if (string.IsNullOrWhiteSpace(playerId) && string.IsNullOrWhiteSpace(playerName))
            {
                return;
            }

            try
            {
                if (!EnsureTableExists(WeeklyTable))
                {
                    return;
                }

                var conn = GetConnection();
                if (conn == null)
                {
                    return;
                }

                RethinkWeeklySchedule schedule = null;
                if (!string.IsNullOrWhiteSpace(playerId))
                {
                    schedule = R.Db(DatabaseName)
                        .Table(WeeklyTable)
                        .Get(playerId)
                        .RunAtom<RethinkWeeklySchedule>(conn);
                }

                if (schedule == null && !string.IsNullOrWhiteSpace(playerName))
                {
                    schedule = R.Db(DatabaseName)
                        .Table(WeeklyTable)
                        .Filter(row => row["PlayerName"].Downcase().Eq(playerName.ToLowerInvariant()))
                        .Limit(1)
                        .RunCursor<RethinkWeeklySchedule>(conn)
                        .FirstOrDefault();
                }

                if (schedule == null)
                {
                    return;
                }

                var localId = string.IsNullOrWhiteSpace(schedule.Id)
                    ? (string.IsNullOrWhiteSpace(playerId) ? playerName : playerId)
                    : schedule.Id?.Trim();
                if (string.IsNullOrWhiteSpace(localId))
                {
                    return;
                }

                var resolvedPlayerId = string.IsNullOrWhiteSpace(schedule.PlayerID)
                    ? (string.IsNullOrWhiteSpace(playerId) ? playerName : playerId)
                    : schedule.PlayerID?.Trim();
                if (string.IsNullOrWhiteSpace(resolvedPlayerId))
                {
                    return;
                }

                var resolvedPlayerName = string.IsNullOrWhiteSpace(schedule.PlayerName)
                    ? (string.IsNullOrWhiteSpace(playerName) ? playerId : playerName)
                    : schedule.PlayerName?.Trim();

                var local = new SharedWeeklyPlayScheduleInfo
                {
                    Id = localId,
                    PlayerID = resolvedPlayerId,
                    PlayerName = resolvedPlayerName ?? string.Empty,
                    MonSch = schedule.MonSch ?? DaySchedule.CreateDefault(),
                    TueSch = schedule.TueSch ?? DaySchedule.CreateDefault(),
                    WedSch = schedule.WedSch ?? DaySchedule.CreateDefault(),
                    ThuSch = schedule.ThuSch ?? DaySchedule.CreateDefault(),
                    FriSch = schedule.FriSch ?? DaySchedule.CreateDefault(),
                    SatSch = schedule.SatSch ?? DaySchedule.CreateDefault(),
                    SunSch = schedule.SunSch ?? DaySchedule.CreateDefault()
                };

                using (var repo = new WeeklyScheduleRepository())
                {
                    repo.DeleteMany(x =>
                        (!string.IsNullOrWhiteSpace(local.Id)
                            && string.Equals(x.Id, local.Id, StringComparison.OrdinalIgnoreCase))
                        || (!string.IsNullOrWhiteSpace(local.PlayerID)
                            && string.Equals(x.PlayerID, local.PlayerID, StringComparison.OrdinalIgnoreCase))
                        || (!string.IsNullOrWhiteSpace(local.PlayerName)
                            && string.Equals(x.PlayerName, local.PlayerName, StringComparison.OrdinalIgnoreCase)));
                    repo.Upsert(local);
                }

                WeeklyScheduleSynced?.Invoke();
            }
            catch (Exception ex)
            {
                Logger.WriteErrorLog($"SyncWeeklySchedule error: {ex}", Logger.GetLogFileName());
            }
        }


        private sealed class RethinkSpecialSchedule
        {
            public string Id { get; set; }
            public List<string> PlayerNames { get; set; } = new List<string>();
            public string PageListName { get; set; }
            public bool DayOfWeek1 { get; set; }
            public bool DayOfWeek2 { get; set; }
            public bool DayOfWeek3 { get; set; }
            public bool DayOfWeek4 { get; set; }
            public bool DayOfWeek5 { get; set; }
            public bool DayOfWeek6 { get; set; }
            public bool DayOfWeek7 { get; set; }
            public bool IsPeriodEnable { get; set; }
            public int DisplayStartH { get; set; }
            public int DisplayStartM { get; set; }
            public int DisplayEndH { get; set; }
            public int DisplayEndM { get; set; }
            public int PeriodStartYear { get; set; }
            public int PeriodStartMonth { get; set; }
            public int PeriodStartDay { get; set; }
            public int PeriodEndYear { get; set; }
            public int PeriodEndMonth { get; set; }
            public int PeriodEndDay { get; set; }
        }

        private sealed class RethinkContentPeriod
        {
            public string Id { get; set; }
            public string ContentGuid { get; set; }
            public string FileName { get; set; }
            public string StartDate { get; set; }
            public string EndDate { get; set; }
            public string StartTime { get; set; }
            public string EndTime { get; set; }
        }

        private sealed class RethinkWeeklySchedule
        {
            public string Id { get; set; }
            public string PlayerID { get; set; }
            public string PlayerName { get; set; }
            public DaySchedule MonSch { get; set; }
            public DaySchedule TueSch { get; set; }
            public DaySchedule WedSch { get; set; }
            public DaySchedule ThuSch { get; set; }
            public DaySchedule FriSch { get; set; }
            public DaySchedule SatSch { get; set; }
            public DaySchedule SunSch { get; set; }
        }

    }
}
