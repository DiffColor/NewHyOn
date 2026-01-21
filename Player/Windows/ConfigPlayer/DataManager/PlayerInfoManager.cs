using System;
using AndoW.LiteDb;
using AndoW.Shared;

namespace ConfigPlayer
{
    public class PlayerInfoManager
    {
        private readonly PlayerInfoRepository repository = new PlayerInfoRepository();
        public PlayerInfoClass g_PlayerInfo = new PlayerInfoClass();

        public PlayerInfoManager()
        {
            LoadData();
        }

        public void LoadData()
        {
            PlayerInfoClass stored = repository.FindOne(_ => true);
            if (stored == null)
            {
                NewPlayerInfo();
            }
            else
            {
                g_PlayerInfo = stored;
            }
        }

        public void NewPlayerInfo()
        {
            g_PlayerInfo = new PlayerInfoClass();
            SaveData();
        }

        public void EditPlayerPlayList(string playlist)
        {
            g_PlayerInfo.PIF_CurrentPlayList = playlist;
            SaveData();
        }

        public void EditMacAddress(string macAddress)
        {
            g_PlayerInfo.PIF_MacAddress = macAddress;
            SaveData();
        }

        public void SaveData()
        {
            if (string.IsNullOrWhiteSpace(g_PlayerInfo.PIF_GUID))
            {
                g_PlayerInfo.PIF_GUID = Guid.NewGuid().ToString();
            }

            if (string.IsNullOrWhiteSpace(g_PlayerInfo.PIF_AuthKey))
            {
                g_PlayerInfo.PIF_AuthKey = Guid.NewGuid().ToString("N");
            }

            g_PlayerInfo.Id = 0;
            repository.Upsert(g_PlayerInfo);
        }

        private class PlayerInfoRepository : LiteDbRepository<PlayerInfoClass>
        {
            public PlayerInfoRepository()
                : base("PlayerInfoManager", "Id")
            {
            }
        }
    }
}
