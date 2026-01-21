
namespace HyOnPlayer
{
    public enum PowerControlType { SystemOff, SystemReboot, ApplicationClose, BlackScreen, Hibernation }
    public enum DisplayType { None, Media, ScrollText, WelcomeBoard }
    public enum ContentType { None, Video, Image }
    public enum DeviceOrientation { Landscape, Portrait }

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
    }

    public class PeriodData
    {
        public string FileName { get; set; } = string.Empty;
        public string StartDate { get; set; }
        public string EndDate { get; set; }
        public string StartTime { get; set; }
        public string EndTime { get; set; }

        public PeriodData()
        {

        }

        public PeriodData(string fname, string startdate, string enddate, string starttime, string endtime)
        {
            FileName = fname;
            StartDate = startdate;
            EndDate = enddate;
            StartTime = starttime;
            EndTime = endtime;
        }
    }
}
