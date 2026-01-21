using AndoW.Shared;

namespace HyOnPlayer
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
            // 플레이어 원격 스키마 저장
            g_PlayerInfo.Id = 0;
            repository.Upsert(g_PlayerInfo);
        }
    }
}
