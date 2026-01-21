using System;
using System.Collections.Generic;
using System.Linq;
using TurtleTools;

namespace AndoW_Manager
{
    public class PlayerGroupManager : RethinkDbManagerBase<PlayerGroupClass>
    {
        public List<PlayerGroupClass> g_PlayerGroupClassList = new List<PlayerGroupClass>();

        public PlayerGroupManager()
            : base(RethinkDbConfigurator.GetDataDatabaseName(), nameof(PlayerGroupManager), "id")
        {
            LoadDataFromDatabase();
        }

        public void LoadDataFromDatabase()
        {
            g_PlayerGroupClassList = LoadAllDocuments() ?? new List<PlayerGroupClass>();
            foreach (PlayerGroupClass group in g_PlayerGroupClassList)
            {
                if (group.PG_AssignedPlayerNames == null)
                {
                    group.PG_AssignedPlayerNames = new List<string>();
                }
            }
        }

        public void AddPlayerGroup(PlayerGroupClass paramCls)
        {
            PlayerGroupClass tmpCls = new PlayerGroupClass();
            tmpCls.CopyData(paramCls);
            EnsureId(tmpCls);
            if (tmpCls.PG_AssignedPlayerNames == null)
            {
                tmpCls.PG_AssignedPlayerNames = new List<string>();
            }

            g_PlayerGroupClassList.Add(tmpCls);
            SaveGroup(tmpCls);
        }

        public void UpdatePlayerGroup(PlayerGroupClass oldCls, PlayerGroupClass newCls)
        {
            int idx = g_PlayerGroupClassList.FindIndex(x => x.PG_GUID == oldCls.PG_GUID);
            if (idx < 0)
            {
                return;
            }

            PlayerGroupClass tmpCls = new PlayerGroupClass();
            tmpCls.CopyData(newCls);
            EnsureId(tmpCls);
            if (tmpCls.PG_AssignedPlayerNames == null)
            {
                tmpCls.PG_AssignedPlayerNames = new List<string>();
            }

            g_PlayerGroupClassList[idx] = tmpCls;
            SaveGroup(tmpCls);
        }

        public void DeletePlayerGroup(PlayerGroupClass paramCls)
        {
            int idx = g_PlayerGroupClassList.FindIndex(x => x.PG_GUID == paramCls.PG_GUID);
            if (idx < 0)
            {
                return;
            }

            PlayerGroupClass removed = g_PlayerGroupClassList[idx];
            g_PlayerGroupClassList.RemoveAt(idx);
            DeleteById(removed.PG_GUID);
        }

        public PlayerGroupClass GetGroupByName(string groupName)
        {
            return g_PlayerGroupClassList.FirstOrDefault(x =>
                x.PG_GroupName.Equals(groupName, StringComparison.CurrentCultureIgnoreCase));
        }

        public PlayerGroupClass GetGroupByGUID(string guid)
        {
            return g_PlayerGroupClassList.FirstOrDefault(x =>
                x.PG_GUID.Equals(guid, StringComparison.CurrentCultureIgnoreCase));
        }

        public List<PlayerGroupClass> GetGroupsByPlayerName(string playerName)
        {
            if (string.IsNullOrWhiteSpace(playerName))
            {
                return new List<PlayerGroupClass>();
            }

            return g_PlayerGroupClassList
                .Where(x => x.HasPlayer(playerName))
                .ToList();
        }

        private void SaveGroup(PlayerGroupClass group)
        {
            EnsureId(group);
            Upsert(group);
        }

        private static void EnsureId(PlayerGroupClass group)
        {
            if (group == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(group.PG_GUID))
            {
                group.PG_GUID = Guid.NewGuid().ToString();
            }
        }
    }
}
