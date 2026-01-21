using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace AndoW_Manager
{
    public class PlayerGroupClass
    {
        public string PG_GroupName { get; set; } = string.Empty;

        [JsonProperty("id")]
        public string PG_GUID { get; set; } = Guid.NewGuid().ToString();

        [JsonIgnore]
        public string Id
        {
            get { return PG_GUID; }
            set { PG_GUID = value; }
        }

        public string PG_LogoImagePath { get; set; } = string.Empty;

        public List<string> PG_AssignedPlayerNames { get; set; } = new List<string>();

        public void CopyData(PlayerGroupClass paramCls)
        {
            if (paramCls == null)
            {
                return;
            }

            PG_GroupName = paramCls.PG_GroupName;
            PG_GUID = paramCls.PG_GUID;
            PG_LogoImagePath = paramCls.PG_LogoImagePath;

            PG_AssignedPlayerNames.Clear();
            if (paramCls.PG_AssignedPlayerNames != null)
            {
                foreach (string playerName in paramCls.PG_AssignedPlayerNames)
                {
                    PG_AssignedPlayerNames.Add(playerName);
                }
            }
        }

        public bool HasPlayer(string playerName)
        {
            return PG_AssignedPlayerNames != null && PG_AssignedPlayerNames.Contains(playerName);
        }

        public void AddPlayer(string playerName)
        {
            if (string.IsNullOrWhiteSpace(playerName))
            {
                return;
            }

            if (PG_AssignedPlayerNames == null)
            {
                PG_AssignedPlayerNames = new List<string>();
            }

            if (!PG_AssignedPlayerNames.Contains(playerName))
            {
                PG_AssignedPlayerNames.Add(playerName);
            }
        }

        public void RemovePlayer(string playerName)
        {
            PG_AssignedPlayerNames?.Remove(playerName);
        }

        public void ClearPlayers()
        {
            PG_AssignedPlayerNames?.Clear();
        }
    }
}
