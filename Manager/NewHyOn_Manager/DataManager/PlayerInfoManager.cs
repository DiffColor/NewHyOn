using Newtonsoft.Json;
using NewHyOn.Shared.Auth;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TurtleTools;


namespace AndoW_Manager
{
    public class PlayerInfoManager : RethinkDbManagerBase<PlayerInfoClass>
    {
        private const int AuthValidationBatchSize = 16;
        private const int AuthValidationMaxConcurrency = 4;
        private static readonly TimeSpan AuthValidationRefreshInterval = TimeSpan.FromSeconds(30);
        private readonly object authValidationLock = new object();
        private readonly Queue<AuthValidationRequest> authValidationQueue = new Queue<AuthValidationRequest>();
        private readonly HashSet<string> queuedAuthValidationKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, AuthValidationCacheEntry> authValidationCache =
            new Dictionary<string, AuthValidationCacheEntry>(StringComparer.OrdinalIgnoreCase);
        private bool authValidationWorkerRunning;

        public List<PlayerInfoClass> g_PlayerInfoClassList = new List<PlayerInfoClass>();
        public event EventHandler AuthValidationChanged;

        public PlayerInfoManager()
            : base(RethinkDbConfigurator.GetDataDatabaseName(), nameof(PlayerInfoManager), "id")
        {
            EnsureWeeklyScheduleTable();
            ReloadFromDatabase();

            SetDefaultEditorStyle();
        }

        private void SetDefaultEditorStyle()
        {
            int countLand = 0;
            foreach (PlayerInfoClass pic in g_PlayerInfoClassList)
            {
                if(pic.PIF_IsLandScape)
                    countLand++;
            }

            if (countLand >= (float)g_PlayerInfoClassList.Count / 2)
            {
                MainWindow.Instance.isPortraitEditor = false;
            }
        }


        public void ReloadFromDatabase()
        {
            g_PlayerInfoClassList = SortPlayers(LoadAllDocuments());
        }

        public DataTable GetPlayerTempTableForAndroid(string playername)
        {
            DataTable tempTable = new DataTable("TB_PlayerInfo");
            tempTable.Columns.Add("PIF_PlayerName", typeof(string));
            tempTable.Columns.Add("PIF_CurrentPlayList", typeof(string));
            tempTable.Columns.Add("PIF_IsLandScape", typeof(bool));

            string normalizedPlayerName = NormalizePlayerName(playername);
            foreach (PlayerInfoClass tempClass in g_PlayerInfoClassList)
            {
                if (IsSamePlayerName(tempClass.PIF_PlayerName, normalizedPlayerName))
                {
                    DataRow dr = tempTable.NewRow();

                    dr["PIF_PlayerName"] = tempClass.PIF_PlayerName;
                    dr["PIF_CurrentPlayList"] = tempClass.PIF_CurrentPlayList;
                    dr["PIF_IsLandScape"] = tempClass.PIF_IsLandScape;

                    tempTable.Rows.Add(dr);

                    break;
                }
            }

            return tempTable;
        }

        public bool CheckExistSamename(string paramPlayerName)
        {
            string normalizedPlayerName = NormalizePlayerName(paramPlayerName);
            bool IsSameExist = false;
            foreach (PlayerInfoClass item in g_PlayerInfoClassList)
            {
                if (IsSamePlayerName(item.PIF_PlayerName, normalizedPlayerName))
                {
                    IsSameExist = true;
                    break;
                }

            }

            return IsSameExist;
        }

        public List<string> GetDisplayTypeDeviceNameList()
        {
            List<string> resultStrList = new List<string>();
            //resultStrList.Clear();

            //foreach (PageInfoClass item in g_PageInfoClassList)
            //{
            //    if (item.DIC_DeviceType == "LCD")
            //    {
            //        resultStrList.Add(item.DIC_DeviceName);
            //        break;
            //    }
            //}
            return resultStrList;
        }

        public PlayerInfoClass GetPlayerInfoByName(string paramPlayerName)
        {
            PlayerInfoClass resultCls = new PlayerInfoClass();
            resultCls.CleanDataField();
            string normalizedPlayerName = NormalizePlayerName(paramPlayerName);

            foreach (PlayerInfoClass item in g_PlayerInfoClassList)
            {
                if (IsSamePlayerName(item.PIF_PlayerName, normalizedPlayerName))
                {
                    resultCls.CopyData(item);
                    break;
                }
            }

            return resultCls;
        }

        public void AddPlayerInfoClass(PlayerInfoClass paramCls)
        {
           // ReloadFromDatabase();

            PlayerInfoClass tmpCls = new PlayerInfoClass();
            tmpCls.CopyData(paramCls);
            tmpCls.PIF_PlayerName = NormalizePlayerName(tmpCls.PIF_PlayerName);
            if (string.IsNullOrWhiteSpace(tmpCls.PIF_PlayerName))
            {
                return;
            }

            if (tmpCls.PIF_Order <= 0)
            {
                tmpCls.PIF_Order = g_PlayerInfoClassList.Count + 1;
            }

            PlayerInfoClass existing = g_PlayerInfoClassList
                .FirstOrDefault(x => IsSamePlayerName(x.PIF_PlayerName, tmpCls.PIF_PlayerName));
            if (existing != null)
            {
                tmpCls.PIF_GUID = existing.PIF_GUID;
                tmpCls.PIF_Order = existing.PIF_Order <= 0 ? tmpCls.PIF_Order : existing.PIF_Order;
                existing.CopyData(tmpCls);
                SavePlayer(existing);
                g_PlayerInfoClassList = SortPlayers(g_PlayerInfoClassList);
                return;
            }

            g_PlayerInfoClassList.Add(tmpCls);
            g_PlayerInfoClassList = SortPlayers(g_PlayerInfoClassList);
            SavePlayer(tmpCls);
        }

        public void UpdatePlayerInfoListAtBatchWindow(List<PlayerInfoClass> paramList, string paramGroupName)
        {
            foreach (PlayerInfoClass item in paramList)
            {
                foreach (PlayerInfoClass originitem in g_PlayerInfoClassList)
                {
                    if (item.PIF_GUID == originitem.PIF_GUID)
                    {
                        originitem.CopyData(item);
                        SavePlayer(originitem);
                    }
                    //PlayerInfoClass tmpCls = new PlayerInfoClass();
                    //tmpCls.CopyData(item);
                    //g_PlayerInfoClassList.Add(tmpCls);
                }
            }

            g_PlayerInfoClassList = SortPlayers(g_PlayerInfoClassList);
        }

        public void UpdatePlayerInfoList(List<PlayerInfoClass> paramList)
        {
            if (paramList == null)
            {
                return;
            }

            int order = 1;
            foreach (PlayerInfoClass item in paramList.Where(x => x != null))
            {
                item.PIF_PlayerName = NormalizePlayerName(item.PIF_PlayerName);
                if (item.PIF_Order <= 0)
                {
                    item.PIF_Order = order;
                }
                order++;
            }

            bool hasDuplicateName = paramList
                .Where(x => x != null && string.IsNullOrWhiteSpace(x.PIF_PlayerName) == false)
                .GroupBy(x => NormalizePlayerName(x.PIF_PlayerName), StringComparer.CurrentCultureIgnoreCase)
                .Any(group => group.Count() > 1);
            if (hasDuplicateName)
            {
                throw new InvalidOperationException("동일한 플레이어 이름이 존재합니다.");
            }

            var incomingIds = new HashSet<string>(paramList
                .Where(x => x != null && string.IsNullOrWhiteSpace(x.PIF_GUID) == false)
                .Select(x => x.PIF_GUID), StringComparer.CurrentCultureIgnoreCase);

            foreach (PlayerInfoClass existing in g_PlayerInfoClassList.ToList())
            {
                if (string.IsNullOrWhiteSpace(existing.PIF_GUID))
                {
                    continue;
                }

                if (incomingIds.Contains(existing.PIF_GUID))
                {
                    continue;
                }

                g_PlayerInfoClassList.Remove(existing);
                RemovePlayerFromDatabase(existing);
            }

            foreach (PlayerInfoClass item in paramList.Where(x => x != null))
            {
                PlayerInfoClass tmpCls = new PlayerInfoClass();
                tmpCls.CopyData(item);
                if (string.IsNullOrWhiteSpace(tmpCls.PIF_GUID))
                {
                    tmpCls.PIF_GUID = Guid.NewGuid().ToString();
                }

                int index = g_PlayerInfoClassList.FindIndex(x => x.PIF_GUID == tmpCls.PIF_GUID);
                if (index >= 0)
                {
                    g_PlayerInfoClassList[index] = tmpCls;
                }
                else
                {
                    g_PlayerInfoClassList.Add(tmpCls);
                }

                SavePlayer(tmpCls);
            }

            g_PlayerInfoClassList = SortPlayers(g_PlayerInfoClassList);
        }


        public void EditPlayerCurrentPlayList(PlayerInfoClass paramCls)
        {
            foreach (PlayerInfoClass item in g_PlayerInfoClassList)
            {
                if (IsSamePlayerName(item.PIF_PlayerName, paramCls.PIF_PlayerName))
                {
                    item.PIF_CurrentPlayList = paramCls.PIF_CurrentPlayList;
                    SavePlayer(item);
                    break;
                }
            }
        }

        public void EditPlayerCurrentPlayList(string pname, string listname)
        {
            foreach (PlayerInfoClass item in g_PlayerInfoClassList)
            {
                if (IsSamePlayerName(item.PIF_PlayerName, pname))
                {
                    item.PIF_CurrentPlayList = listname;
                    SavePlayer(item);
                    break;
                }
            }
        }

        public void ClearPlaylistReferences(string playlistName)
        {
            if (string.IsNullOrWhiteSpace(playlistName))
            {
                return;
            }

            foreach (PlayerInfoClass item in g_PlayerInfoClassList)
            {
                if (item == null)
                {
                    continue;
                }

                bool changed = false;

                if (!string.IsNullOrWhiteSpace(item.PIF_CurrentPlayList)
                    && item.PIF_CurrentPlayList.Equals(playlistName, StringComparison.CurrentCultureIgnoreCase))
                {
                    item.PIF_CurrentPlayList = string.Empty;
                    changed = true;
                }

                if (!string.IsNullOrWhiteSpace(item.PIF_DefaultPlayList)
                    && item.PIF_DefaultPlayList.Equals(playlistName, StringComparison.CurrentCultureIgnoreCase))
                {
                    item.PIF_DefaultPlayList = string.Empty;
                    changed = true;
                }

                if (changed)
                {
                    SavePlayer(item);
                }
            }
        }

        public void DeleteDataClassInfo(PlayerInfoClass oldCls)
        {
            if (oldCls == null)
            {
                return;
            }

            PlayerInfoClass target = g_PlayerInfoClassList.FirstOrDefault(item =>
                string.IsNullOrWhiteSpace(oldCls.PIF_GUID) == false &&
                item.PIF_GUID == oldCls.PIF_GUID);
            if (target == null)
            {
                target = g_PlayerInfoClassList.FirstOrDefault(item => IsSamePlayerName(item.PIF_PlayerName, oldCls.PIF_PlayerName));
            }
            if (target == null)
            {
                return;
            }

            g_PlayerInfoClassList.Remove(target);

            if (!string.IsNullOrWhiteSpace(target.PIF_GUID))
            {
                var weeklyManager = new WeeklyInfoManagerClass();
                weeklyManager.DeleteWeeklySchedule(target.PIF_GUID);
            }

            RemovePlayerFromDatabase(target);
        }


        public void EditDeviceInfoClass(PlayerInfoClass oldCls, PlayerInfoClass newCls)
        {
            try
            {
                if (oldCls == null || newCls == null)
                {
                    return;
                }

                PlayerInfoClass tmpCls = new PlayerInfoClass();
                tmpCls.CopyData(newCls);
                tmpCls.PIF_PlayerName = NormalizePlayerName(tmpCls.PIF_PlayerName);
                if (string.IsNullOrWhiteSpace(tmpCls.PIF_PlayerName))
                {
                    return;
                }

                int idx = g_PlayerInfoClassList.FindIndex(item =>
                    string.IsNullOrWhiteSpace(oldCls.PIF_GUID) == false &&
                    item.PIF_GUID == oldCls.PIF_GUID);
                if (idx < 0)
                {
                    idx = g_PlayerInfoClassList.FindIndex(item => IsSamePlayerName(item.PIF_PlayerName, oldCls.PIF_PlayerName));
                }
                if (idx < 0)
                {
                    return;
                }

                bool duplicatedName = g_PlayerInfoClassList.Any(item =>
                    item != null &&
                    item.PIF_GUID != g_PlayerInfoClassList[idx].PIF_GUID &&
                    IsSamePlayerName(item.PIF_PlayerName, tmpCls.PIF_PlayerName));
                if (duplicatedName)
                {
                    return;
                }

                //g_DeviceInfoClassList.Add(tmpCls);
                g_PlayerInfoClassList[idx] = tmpCls;
                SavePlayer(tmpCls);
            }
            catch { }
        }

        public void SetMacAddressForPlayer(string playerName, string macAddr)
        {
            string normalizedMac = AuthTools.NormalizeMacAddress(macAddr);
            if (string.IsNullOrWhiteSpace(normalizedMac))
            {
                normalizedMac = string.Empty;
            }

            List<PlayerInfoClass> changedPlayers = new List<PlayerInfoClass>();
            string normalizedPlayerName = NormalizePlayerName(playerName);
            foreach (PlayerInfoClass item in g_PlayerInfoClassList)
            {
                if (IsSamePlayerName(item.PIF_PlayerName, normalizedPlayerName))
                {
                    item.PIF_MacAddress = normalizedMac;
                    changedPlayers.Add(item);
                } else if(string.IsNullOrWhiteSpace(normalizedMac) == false && item.PIF_MacAddress == normalizedMac)
                {
                    item.PIF_MacAddress = string.Empty;
                    changedPlayers.Add(item);
                }
            }

            foreach (PlayerInfoClass player in changedPlayers.Distinct())
            {
                SavePlayer(player);
            }
        }

        public void SetIPForPlayer(string playerName, string ip)
        {
            List<PlayerInfoClass> changedPlayers = new List<PlayerInfoClass>();
            string normalizedPlayerName = NormalizePlayerName(playerName);
            foreach (PlayerInfoClass item in g_PlayerInfoClassList)
            {
                if (IsSamePlayerName(item.PIF_PlayerName, normalizedPlayerName))
                {
                    item.PIF_IPAddress = ip;
                    changedPlayers.Add(item);
                } else if(string.IsNullOrWhiteSpace(ip) == false && item.PIF_IPAddress == ip)
                {
                    item.PIF_IPAddress = string.Empty;
                    changedPlayers.Add(item);
                }
            }

            foreach (PlayerInfoClass player in changedPlayers.Distinct())
            {
                SavePlayer(player);
            }
        }
        public string GetPlayerIP(string pname)
        {
            try
            {
                object _obj = g_PlayerInfoClassList.FirstOrDefault(x => IsSamePlayerName(x.PIF_PlayerName, pname));

                if (_obj == null)
                    return string.Empty;

                PlayerInfoClass _pic = _obj as PlayerInfoClass;

                return _pic.PIF_IPAddress;
            }
            catch (Exception e) { }

            return string.Empty;
        }

        public string GetPlayerMAC(string pname)
        {
            try
            {
                object _obj = g_PlayerInfoClassList.FirstOrDefault(x => IsSamePlayerName(x.PIF_PlayerName, pname));

                if (_obj == null)
                    return string.Empty;

                PlayerInfoClass _pic = _obj as PlayerInfoClass;

                return _pic.PIF_MacAddress;
            }
            catch (Exception e) { }

            return string.Empty;
        }

        private PlayerInfoClass GetTrackedPlayer(string playerName)
        {
            if (string.IsNullOrWhiteSpace(playerName))
            {
                return null;
            }

            PlayerInfoClass player = g_PlayerInfoClassList
                .FirstOrDefault(x => IsSamePlayerName(x.PIF_PlayerName, playerName));
            if (player != null)
            {
                return player;
            }

            player = Find(x => IsSamePlayerName(x.PIF_PlayerName, playerName))
                .FirstOrDefault();
            if (player != null)
            {
                g_PlayerInfoClassList.Add(player);
            }
            return player;
        }

        private void SavePlayer(PlayerInfoClass player)
        {
            if (player == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(player.PIF_GUID))
            {
                player.PIF_GUID = Guid.NewGuid().ToString();
            }

            Upsert(player);
            EnsureWeeklyScheduleForPlayer(player);
        }

        private static void EnsureWeeklyScheduleTable()
        {
            new WeeklyInfoManagerClass();
        }

        private static void EnsureWeeklyScheduleForPlayer(PlayerInfoClass player)
        {
            if (player == null || string.IsNullOrWhiteSpace(player.PIF_GUID))
            {
                return;
            }

            var weeklyManager = new WeeklyInfoManagerClass();
            weeklyManager.EnsureWeeklySchedule(player.PIF_GUID, player.PIF_PlayerName);
        }

        private void RemovePlayerFromDatabase(PlayerInfoClass player)
        {
            if (player == null || string.IsNullOrWhiteSpace(player.PIF_GUID))
            {
                return;
            }

            DeleteMany(x => x.PIF_GUID == player.PIF_GUID);
        }

        public void SetPendingCommand(string playerName, string command)
        {
            if (string.IsNullOrWhiteSpace(playerName))
            {
                return;
            }

            var player = g_PlayerInfoClassList
                .FirstOrDefault(x => IsSamePlayerName(x.PIF_PlayerName, playerName));

            if (player == null)
            {
                var dbPlayer = Find(x => IsSamePlayerName(x.PIF_PlayerName, playerName))
                    .FirstOrDefault();
                if (dbPlayer == null)
                {
                    return;
                }

                player = dbPlayer;
                g_PlayerInfoClassList.Add(player);
            }

            player.PendingCommand = command ?? string.Empty;
            Upsert(player);
        }

        public void SetAuthKeyForPlayer(string playerName, string authKey)
        {
            PlayerInfoClass player = GetTrackedPlayer(playerName);
            if (player == null)
            {
                return;
            }

            player.PIF_AuthKey = authKey ?? string.Empty;
            SavePlayer(player);
        }

        public bool HasValidAuthKey(string playerName)
        {
            PlayerInfoClass player = GetTrackedPlayer(playerName);
            return HasValidAuthKey(player);
        }

        public bool HasValidAuthKey(PlayerInfoClass player)
        {
            if (player == null)
            {
                return false;
            }

            string authKey = player.PIF_AuthKey?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(authKey))
            {
                return false;
            }

            LicenseHubLocalLicenseFile license = LicenseHubLocalLicenseStore.TryDeserialize(authKey);
            if (license != null)
            {
                string deviceFingerprint = ResolveLicenseHubDeviceFingerprint(player, license);
                if (string.IsNullOrWhiteSpace(deviceFingerprint))
                {
                    UpdateAuthValidationCache(player, BuildAuthSignature(authKey, deviceFingerprint), false);
                    return false;
                }

                string signature = BuildAuthSignature(authKey, deviceFingerprint);
                if (TryGetCachedAuthState(player, signature, out bool cachedValid, out bool shouldRefresh))
                {
                    if (shouldRefresh)
                    {
                        QueueAuthValidation(player, license, deviceFingerprint, signature, forceRefresh: true);
                    }

                    return cachedValid;
                }

                QueueAuthValidation(player, license, deviceFingerprint, signature, forceRefresh: false);
                return false;
            }

            bool legacyValid = HasValidLegacyAuthKey(player, authKey);
            UpdateAuthValidationCache(player, BuildAuthSignature(authKey, player.PIF_MacAddress), legacyValid);
            return legacyValid;
        }

        public void RequestAuthValidationForAll(IEnumerable<PlayerInfoClass> players, bool forceRefresh = false)
        {
            if (players == null)
            {
                return;
            }

            foreach (PlayerInfoClass player in players.ToList())
            {
                RequestAuthValidation(player, forceRefresh);
            }
        }

        public void RequestAuthValidation(PlayerInfoClass player, bool forceRefresh = false)
        {
            if (player == null)
            {
                return;
            }

            string authKey = player.PIF_AuthKey?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(authKey))
            {
                UpdateAuthValidationCache(player, BuildAuthSignature(authKey, player.PIF_MacAddress), false);
                return;
            }

            LicenseHubLocalLicenseFile license = LicenseHubLocalLicenseStore.TryDeserialize(authKey);
            if (license == null)
            {
                bool legacyValid = HasValidLegacyAuthKey(player, authKey);
                UpdateAuthValidationCache(player, BuildAuthSignature(authKey, player.PIF_MacAddress), legacyValid);
                return;
            }

            string deviceFingerprint = ResolveLicenseHubDeviceFingerprint(player, license);
            string signature = BuildAuthSignature(authKey, deviceFingerprint);
            if (string.IsNullOrWhiteSpace(deviceFingerprint))
            {
                UpdateAuthValidationCache(player, signature, false);
                return;
            }

            QueueAuthValidation(player, license, deviceFingerprint, signature, forceRefresh);
        }

        private static bool HasValidLegacyAuthKey(PlayerInfoClass player, string authKey)
        {
            string normalizedMac = AuthTools.NormalizeMacAddress(player.PIF_MacAddress);
            if (string.IsNullOrWhiteSpace(normalizedMac))
            {
                return false;
            }

            string expectedKey = AuthTools.EncodeAuthKey(normalizedMac);
            return string.Equals(authKey, expectedKey, StringComparison.CurrentCultureIgnoreCase);
        }

        private bool TryGetCachedAuthState(PlayerInfoClass player, string signature, out bool isValid, out bool shouldRefresh)
        {
            isValid = false;
            shouldRefresh = false;

            string cacheKey = GetAuthCacheKey(player);
            if (string.IsNullOrWhiteSpace(cacheKey) || string.IsNullOrWhiteSpace(signature))
            {
                return false;
            }

            lock (authValidationLock)
            {
                if (!authValidationCache.TryGetValue(cacheKey, out AuthValidationCacheEntry entry))
                {
                    return false;
                }

                if (!string.Equals(entry.Signature, signature, StringComparison.Ordinal))
                {
                    return false;
                }

                isValid = entry.IsValid;
                shouldRefresh = DateTime.UtcNow - entry.CheckedAtUtc >= AuthValidationRefreshInterval;
                return true;
            }
        }

        private void QueueAuthValidation(
            PlayerInfoClass player,
            LicenseHubLocalLicenseFile license,
            string deviceFingerprint,
            string signature,
            bool forceRefresh)
        {
            string cacheKey = GetAuthCacheKey(player);
            if (string.IsNullOrWhiteSpace(cacheKey) ||
                license == null ||
                string.IsNullOrWhiteSpace(deviceFingerprint) ||
                string.IsNullOrWhiteSpace(signature))
            {
                return;
            }

            string queueKey = cacheKey + "|" + signature;
            lock (authValidationLock)
            {
                if (!forceRefresh &&
                    authValidationCache.TryGetValue(cacheKey, out AuthValidationCacheEntry entry) &&
                    string.Equals(entry.Signature, signature, StringComparison.Ordinal) &&
                    DateTime.UtcNow - entry.CheckedAtUtc < AuthValidationRefreshInterval)
                {
                    return;
                }

                if (!queuedAuthValidationKeys.Add(queueKey))
                {
                    return;
                }

                authValidationQueue.Enqueue(new AuthValidationRequest
                {
                    CacheKey = cacheKey,
                    QueueKey = queueKey,
                    Signature = signature,
                    DeviceFingerprint = deviceFingerprint,
                    License = CloneLicense(license)
                });

                if (authValidationWorkerRunning)
                {
                    return;
                }

                authValidationWorkerRunning = true;
                Task.Run(() => ProcessAuthValidationQueueAsync());
            }
        }

        private async Task ProcessAuthValidationQueueAsync()
        {
            while (true)
            {
                List<AuthValidationRequest> batch = DequeueAuthValidationBatch();
                if (batch.Count == 0)
                {
                    lock (authValidationLock)
                    {
                        if (authValidationQueue.Count == 0)
                        {
                            authValidationWorkerRunning = false;
                            return;
                        }
                    }

                    continue;
                }

                using (SemaphoreSlim semaphore = new SemaphoreSlim(AuthValidationMaxConcurrency))
                {
                    Task[] tasks = batch
                        .Select(request => ProcessAuthValidationRequestAsync(request, semaphore))
                        .ToArray();
                    await Task.WhenAll(tasks).ConfigureAwait(false);
                }
            }
        }

        private List<AuthValidationRequest> DequeueAuthValidationBatch()
        {
            List<AuthValidationRequest> batch = new List<AuthValidationRequest>();
            lock (authValidationLock)
            {
                while (authValidationQueue.Count > 0 && batch.Count < AuthValidationBatchSize)
                {
                    AuthValidationRequest request = authValidationQueue.Dequeue();
                    queuedAuthValidationKeys.Remove(request.QueueKey);
                    batch.Add(request);
                }
            }

            return batch;
        }

        private async Task ProcessAuthValidationRequestAsync(AuthValidationRequest request, SemaphoreSlim semaphore)
        {
            await semaphore.WaitAsync().ConfigureAwait(false);
            try
            {
                ValidationResult result = await Task.Run(() =>
                    LicenseHubLocalValidator.ValidateForDeviceFingerprint(
                        request.License,
                        request.DeviceFingerprint)).ConfigureAwait(false);

                UpdateAuthValidationCache(request.CacheKey, request.Signature, result.IsValid);
            }
            catch
            {
                UpdateAuthValidationCache(request.CacheKey, request.Signature, false);
            }
            finally
            {
                semaphore.Release();
            }
        }

        private void UpdateAuthValidationCache(PlayerInfoClass player, string signature, bool isValid)
        {
            string cacheKey = GetAuthCacheKey(player);
            if (string.IsNullOrWhiteSpace(cacheKey))
            {
                return;
            }

            UpdateAuthValidationCache(cacheKey, signature, isValid);
        }

        private void UpdateAuthValidationCache(string cacheKey, string signature, bool isValid)
        {
            bool changed = false;
            lock (authValidationLock)
            {
                if (!authValidationCache.TryGetValue(cacheKey, out AuthValidationCacheEntry entry) ||
                    !string.Equals(entry.Signature, signature, StringComparison.Ordinal) ||
                    entry.IsValid != isValid)
                {
                    changed = true;
                }

                authValidationCache[cacheKey] = new AuthValidationCacheEntry
                {
                    Signature = signature ?? string.Empty,
                    IsValid = isValid,
                    CheckedAtUtc = DateTime.UtcNow
                };
            }

            if (changed)
            {
                AuthValidationChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        private static LicenseHubLocalLicenseFile CloneLicense(LicenseHubLocalLicenseFile license)
        {
            if (license == null)
            {
                return null;
            }

            return new LicenseHubLocalLicenseFile
            {
                ProductId = license.ProductId,
                DeviceFingerprint = license.DeviceFingerprint,
                DeviceId = license.DeviceId,
                LicenseToken = license.LicenseToken,
                SavedAt = license.SavedAt
            };
        }

        private static string BuildAuthSignature(string authKey, string deviceFingerprint)
        {
            return (authKey ?? string.Empty).Trim() + "|" + (deviceFingerprint ?? string.Empty).Trim();
        }

        private static string GetAuthCacheKey(PlayerInfoClass player)
        {
            if (player == null)
            {
                return string.Empty;
            }

            if (!string.IsNullOrWhiteSpace(player.PIF_GUID))
            {
                return "id:" + player.PIF_GUID.Trim();
            }

            if (!string.IsNullOrWhiteSpace(player.PIF_PlayerName))
            {
                return "name:" + NormalizePlayerName(player.PIF_PlayerName);
            }

            return string.Empty;
        }

        public bool ApplyAuthKey(string encodedKey)
        {
            if (string.IsNullOrWhiteSpace(encodedKey))
            {
                return false;
            }

            foreach (PlayerInfoClass player in g_PlayerInfoClassList)
            {
                if (player == null)
                {
                    continue;
                }
                LicenseHubLocalLicenseFile license = LicenseHubLocalLicenseStore.TryDeserialize(encodedKey);
                if (license != null)
                {
                    string deviceFingerprint = ResolveLicenseHubDeviceFingerprint(player, license);
                    if (LicenseHubLocalValidator.ValidateForDeviceFingerprint(license, deviceFingerprint).IsValid)
                    {
                        player.PIF_AuthKey = encodedKey;
                        player.PIF_MacAddress = deviceFingerprint;
                        SavePlayer(player);
                        return true;
                    }

                    continue;
                }

                string normalizedMac = AuthTools.NormalizeMacAddress(player.PIF_MacAddress);
                if (string.IsNullOrWhiteSpace(normalizedMac))
                {
                    continue;
                }
                string expected = AuthTools.EncodeAuthKey(normalizedMac);
                if (string.Equals(expected, encodedKey, StringComparison.CurrentCultureIgnoreCase))
                {
                    player.PIF_AuthKey = encodedKey;
                    SavePlayer(player);
                    return true;
                }
            }
            return false;
        }

        public int ApplyAuthKeys(IEnumerable<string> authKeys)
        {
            if (authKeys == null)
            {
                return 0;
            }
            int applied = 0;
            foreach (string rawKey in authKeys)
            {
                string key = rawKey?.Trim();
                if (string.IsNullOrWhiteSpace(key))
                {
                    continue;
                }
                if (ApplyAuthKey(key))
                {
                    applied++;
                }
            }
            return applied;
        }

        private static string ResolveLicenseHubDeviceFingerprint(PlayerInfoClass player, LicenseHubLocalLicenseFile license)
        {
            string licenseFingerprint = license?.DeviceFingerprint?.Trim() ?? string.Empty;
            string storedIdentity = player?.PIF_MacAddress?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(storedIdentity))
            {
                return licenseFingerprint;
            }

            if (string.IsNullOrWhiteSpace(licenseFingerprint))
            {
                return storedIdentity;
            }

            return string.Equals(storedIdentity, licenseFingerprint, StringComparison.OrdinalIgnoreCase)
                ? storedIdentity
                : string.Empty;
        }

        public List<string> GetAllAuthKeys()
        {
            return g_PlayerInfoClassList
                .Where(p => p != null && string.IsNullOrWhiteSpace(p.PIF_AuthKey) == false)
                .Select(p => p.PIF_AuthKey)
                .Distinct(StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }

        public IEnumerable<PlayerInfoClass> GetOrderedPlayers()
        {
            return SortPlayers(g_PlayerInfoClassList);
        }

        private List<PlayerInfoClass> SortPlayers(IEnumerable<PlayerInfoClass> players)
        {
            return players?
                .Where(p => p != null)
                .OrderBy(p => p.PIF_Order <= 0 ? int.MaxValue : p.PIF_Order)
                .ThenBy(p => p.PIF_PlayerName, StringComparer.CurrentCultureIgnoreCase)
                .ToList() ?? new List<PlayerInfoClass>();
        }

        private static string NormalizePlayerName(string playerName)
        {
            return (playerName ?? string.Empty).Trim();
        }

        private static bool IsSamePlayerName(string left, string right)
        {
            return string.Equals(NormalizePlayerName(left), NormalizePlayerName(right), StringComparison.CurrentCultureIgnoreCase);
        }

        private sealed class AuthValidationRequest
        {
            public string CacheKey { get; set; } = string.Empty;
            public string QueueKey { get; set; } = string.Empty;
            public string Signature { get; set; } = string.Empty;
            public string DeviceFingerprint { get; set; } = string.Empty;
            public LicenseHubLocalLicenseFile License { get; set; }
        }

        private sealed class AuthValidationCacheEntry
        {
            public string Signature { get; set; } = string.Empty;
            public bool IsValid { get; set; }
            public DateTime CheckedAtUtc { get; set; } = DateTime.MinValue;
        }

    }

    public class PlayerInfoClass
    {
        public string PIF_PlayerName = string.Empty;
        public string PIF_IPAddress = string.Empty;
        [JsonProperty("TTInfo_Data03")]
        public string PIF_RemoteID = string.Empty;
        public string PIF_CurrentPlayList = string.Empty;
        public string PIF_DefaultPlayList = string.Empty;
        public bool PIF_IsLandScape = true;
        [JsonProperty("id")]
        public string PIF_GUID = string.Empty;
        public int PIF_Order = 0;
        [JsonIgnore]
        public string Id
        {
            get { return PIF_GUID; }
            set { PIF_GUID = value; }
        }
        public string PIF_OSName = string.Empty;
        public string PIF_MacAddress = string.Empty;
        [JsonProperty("command")]
        public string PendingCommand { get; set; } = string.Empty;
        public string PIF_AuthKey = string.Empty;
        //public string PIF_DataFileName_1 = string.Empty;  // 플레이어에 저장된 컨텐츠파일이름
        //public string PIF_DataFileName_2 = string.Empty;  // 월별 스케줄 파일이름
        //public string PIF_DataFileName_3 = string.Empty;  // 주간 스케줄 파일이름


        public PlayerInfoClass()
        {
            this.PIF_GUID = Guid.NewGuid().ToString();
        }

        public void CopyData(PlayerInfoClass pinfo)
        {
            this.PIF_PlayerName = pinfo.PIF_PlayerName;
            this.PIF_IPAddress = pinfo.PIF_IPAddress;
            this.PIF_RemoteID = pinfo.PIF_RemoteID;
            this.PIF_CurrentPlayList = pinfo.PIF_CurrentPlayList;
            this.PIF_DefaultPlayList = pinfo.PIF_DefaultPlayList;
            this.PIF_IsLandScape = pinfo.PIF_IsLandScape;
            this.PIF_GUID = pinfo.PIF_GUID;
            this.PIF_Order = pinfo.PIF_Order;
            this.PIF_OSName = pinfo.PIF_OSName;
            this.PIF_MacAddress = pinfo.PIF_MacAddress;
            this.PendingCommand = pinfo.PendingCommand;
            this.PIF_AuthKey = pinfo.PIF_AuthKey;
        }

        public void CleanDataField()
        {
            PIF_PlayerName = string.Empty;
            PIF_IPAddress = string.Empty;
            PIF_RemoteID = string.Empty;
            PIF_CurrentPlayList = string.Empty;
            PIF_DefaultPlayList = string.Empty;
            PIF_IsLandScape = true;
            PIF_GUID = string.Empty;
            PIF_Order = 0;
            PIF_OSName = string.Empty;
            PIF_MacAddress = string.Empty;
            PendingCommand = string.Empty;
            PIF_AuthKey = string.Empty;
        }
    }
}
