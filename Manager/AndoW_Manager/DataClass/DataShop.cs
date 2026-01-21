
using System.ComponentModel;
using TurtleTools;

namespace AndoW_Manager
{
    public enum DisplayType { None, Media, HDTV, IPTV, ScrollText, WelcomeBoard }
    public enum ContentType { None, Video, Image, Browser, Flash, PPT, HDTV, IPTV, WebSiteURL, PDF }
    [TypeConverter(typeof(EnumDescriptionTypeConverter))]
    public enum DeviceOrientation
    {
        [Description("가로")]
        Landscape,
        [Description("세로")]
        Portrait
    }
    public enum PlayerStatus { Playing, Stopped, Updating }
    public enum RP_STATUS { playing, stopped, updating };
    public enum RP_ORDER { updatelist, updateschedule, upgrade, reboot, check, getmac, poweroff, clearqueue, sync };
    
    public class DataShop
    {
        /// <summary>
        /// The singleton instance.
        /// This is a singleton for convenience.
        /// </summary>
        private static DataShop instance = new DataShop();
        public static DataShop Instance
        {
            get
            {
                return instance;
            }
        }

       
        public PageInfoManager g_PageInfoManager = new PageInfoManager();
        public SpecialScheduleInfoManager g_SpecialScheduleInfoManager = new SpecialScheduleInfoManager();
        public PlayerInfoManager g_PlayerInfoManager = new PlayerInfoManager();
        public PageListInfoManager g_PageListInfoManager = new PageListInfoManager();
        public TextInfoManager g_TextInfoManager = new TextInfoManager();
        public ServerSettingsManager g_ServerSettingsManager = new ServerSettingsManager();
        public PlayerGroupManager g_PlayerGroupManager = new PlayerGroupManager();
        public CommandQueueManager g_CommandQueueManager = new CommandQueueManager();
        public UpdateThrottleSettingsManager g_UpdateThrottleSettingsManager = new UpdateThrottleSettingsManager();
        public UpdatePayloadBuilder g_UpdatePayloadBuilder = new UpdatePayloadBuilder();

        private DataShop()
        {
        }
    }

}
