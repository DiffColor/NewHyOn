using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using TurtleTools;


namespace AndoW_Manager
{
    public class PlayerInfoManager : RethinkDbManagerBase<PlayerInfoClass>
    {
        public List<PlayerInfoClass> g_PlayerInfoClassList = new List<PlayerInfoClass>();

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

            foreach (PlayerInfoClass tempClass in g_PlayerInfoClassList)
            {
                if (tempClass.PIF_PlayerName.Equals(playername, StringComparison.CurrentCultureIgnoreCase)) 
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
            bool IsSameExist = false;
            foreach (PlayerInfoClass item in g_PlayerInfoClassList)
            {
                if (item.PIF_PlayerName.Equals(paramPlayerName, StringComparison.CurrentCultureIgnoreCase))
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

            foreach (PlayerInfoClass item in g_PlayerInfoClassList)
            {
                if (item.PIF_PlayerName == paramPlayerName)
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

            if (tmpCls.PIF_Order <= 0)
            {
                tmpCls.PIF_Order = g_PlayerInfoClassList.Count + 1;
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
                if (item.PIF_Order <= 0)
                {
                    item.PIF_Order = order;
                }
                order++;
            }

            var incomingIds = new HashSet<string>(paramList
                .Where(x => string.IsNullOrWhiteSpace(x.PIF_GUID) == false)
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

            foreach (PlayerInfoClass item in paramList)
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
                if (item.PIF_PlayerName == paramCls.PIF_PlayerName)
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
                if (item.PIF_PlayerName .Equals(pname))
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

            int idx = 0;

            foreach (PlayerInfoClass item in g_PlayerInfoClassList)
            {
                if (item.PIF_GUID == oldCls.PIF_GUID)
                {
                    break;
                }
                idx++;

            }

            g_PlayerInfoClassList.RemoveAt(idx);

            if (!string.IsNullOrWhiteSpace(oldCls.PIF_GUID))
            {
                var weeklyManager = new WeeklyInfoManagerClass();
                weeklyManager.DeleteWeeklySchedule(oldCls.PIF_GUID);
            }

            RemovePlayerFromDatabase(oldCls);
        }


        public void EditDeviceInfoClass(PlayerInfoClass oldCls, PlayerInfoClass newCls)
        {
            try
            {
                int idx = 0;

                foreach (PlayerInfoClass item in g_PlayerInfoClassList)
                {
                    if (item.PIF_PlayerName == oldCls.PIF_PlayerName)
                    {
                        break;
                    }
                    idx++;

                }

                g_PlayerInfoClassList.RemoveAt(idx);

                PlayerInfoClass tmpCls = new PlayerInfoClass();
                tmpCls.CopyData(newCls);
                //g_DeviceInfoClassList.Add(tmpCls);
                g_PlayerInfoClassList.Insert(idx, tmpCls);
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
            foreach (PlayerInfoClass item in g_PlayerInfoClassList)
            {
                if (item.PIF_PlayerName == playerName)
                {
                    item.PIF_MacAddress = normalizedMac;
                    changedPlayers.Add(item);
                } else if(item.PIF_MacAddress == normalizedMac)
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
            foreach (PlayerInfoClass item in g_PlayerInfoClassList)
            {
                if (item.PIF_PlayerName == playerName)
                {
                    item.PIF_IPAddress = ip;
                    changedPlayers.Add(item);
                } else if(item.PIF_IPAddress == ip)
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
                object _obj = g_PlayerInfoClassList.SingleOrDefault(x => x.PIF_PlayerName == pname);

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
                object _obj = g_PlayerInfoClassList.SingleOrDefault(x => x.PIF_PlayerName == pname);

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
                .FirstOrDefault(x => x.PIF_PlayerName.Equals(playerName, StringComparison.CurrentCultureIgnoreCase));
            if (player != null)
            {
                return player;
            }

            player = Find(x => x.PIF_PlayerName.Equals(playerName, StringComparison.CurrentCultureIgnoreCase))
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
                .FirstOrDefault(x => x.PIF_PlayerName.Equals(playerName, StringComparison.CurrentCultureIgnoreCase));

            if (player == null)
            {
                var dbPlayer = Find(x => x.PIF_PlayerName.Equals(playerName, StringComparison.CurrentCultureIgnoreCase))
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

            string normalizedMac = AuthTools.NormalizeMacAddress(player.PIF_MacAddress);
            if (string.IsNullOrWhiteSpace(normalizedMac))
            {
                return false;
            }

            string expectedKey = AuthTools.EncodeAuthKey(normalizedMac);
            return string.Equals(player.PIF_AuthKey, expectedKey, StringComparison.CurrentCultureIgnoreCase);
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
