using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using TurtleTools;


namespace AndoW_Manager
{
    public class SpecialScheduleInfoManager : RethinkDbManagerBase<SpecialScheduleInfoClass>
    {
        public List<SpecialScheduleInfoClass> g_SpecialScheduleInfoClassList = new List<SpecialScheduleInfoClass>();

        public SpecialScheduleInfoManager()
            : base(RethinkDbConfigurator.GetDataDatabaseName(), nameof(SpecialScheduleInfoManager), "id")
        {
        }

        public void LoadSchedulesForPlayer(string paramPlayerName)
        {
            if (string.IsNullOrWhiteSpace(paramPlayerName))
            {
                g_SpecialScheduleInfoClassList = new List<SpecialScheduleInfoClass>();
                return;
            }

            g_SpecialScheduleInfoClassList = Find(x =>
                x.PlayerNames != null &&
                x.PlayerNames.Any(player => string.Equals(player, paramPlayerName, StringComparison.CurrentCultureIgnoreCase)));
        }

        public void LoadAllSchedules()
        {
            g_SpecialScheduleInfoClassList = LoadAllDocuments();
        }

        public bool CheckExistSamename(string paramGUID)
        {
            bool IsSameExist = false;
            foreach (SpecialScheduleInfoClass item in g_SpecialScheduleInfoClassList)
            {
                if (item.GUID == paramGUID)
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

        public SpecialScheduleInfoClass GetDeviceInfoByName(string paramDeviceName)
        {
            SpecialScheduleInfoClass resultCls = new SpecialScheduleInfoClass();

            //foreach (PageInfoClass item in g_PageInfoClassList)
            //{
            //    if (item.DIC_DeviceName == paramDeviceName)
            //    {
            //        resultCls.CopyData(item);
            //        break;
            //    }
            //}

            return resultCls;
        }

        public void AddSpecialScheduleInfoClass(SpecialScheduleInfoClass paramCls, string paramPlayerName)
        {
            if (paramCls == null)
                return;

            SpecialScheduleInfoClass tmpCls = new SpecialScheduleInfoClass();
            tmpCls.CopyData(paramCls);

            if ((tmpCls.PlayerNames == null || tmpCls.PlayerNames.Count < 1) && string.IsNullOrWhiteSpace(paramPlayerName) == false)
                tmpCls.PlayerNames = new List<string> { paramPlayerName };

            Upsert(tmpCls);
            LoadAllSchedules();
        }

        public void EditDeviceInfoClass(SpecialScheduleInfoClass oldCls, SpecialScheduleInfoClass newCls,  string paramPlayerName)
        {  
            if (oldCls == null || newCls == null)
                return;

            SpecialScheduleInfoClass tmpCls = new SpecialScheduleInfoClass();
            tmpCls.CopyData(newCls);
            if (string.IsNullOrWhiteSpace(tmpCls.GUID))
                tmpCls.GUID = oldCls.GUID;

            Upsert(tmpCls);
            LoadAllSchedules();
        }


        public void DeleteScheduleInfoClass(SpecialScheduleInfoClass paramCls, string paramPlayerName)
        {
            if (paramCls == null || string.IsNullOrWhiteSpace(paramCls.GUID))
                return;

            DeleteById(paramCls.GUID);
            LoadAllSchedules();
        }

        public void SaveSchedulesForPlayer(string playerName)
        {
            if (g_SpecialScheduleInfoClassList == null || g_SpecialScheduleInfoClassList.Count < 1)
            {
                LoadAllSchedules();
                return;
            }

            InsertMany(g_SpecialScheduleInfoClassList);
            LoadAllSchedules();
        }
    }

    public class SpecialScheduleInfoClass
    {
        [JsonProperty("id")]
        public string GUID = string.Empty;
        [JsonIgnore]
        public string Id
        {
            get { return GUID; }
            set { GUID = value; }
        }
        public List<string> PlayerNames = new List<string>();
        public List<string> GroupNames = new List<string>();
        [JsonProperty("GroupName", NullValueHandling = NullValueHandling.Ignore)]
        private string LegacyGroupName
        {
            get { return null; }
            set
            {
                if (string.IsNullOrWhiteSpace(value))
                    return;

                if (GroupNames == null)
                    GroupNames = new List<string>();

                if (GroupNames.Contains(value, StringComparer.CurrentCultureIgnoreCase) == false)
                    GroupNames.Add(value);
            }
        }
        public string PageListName = string.Empty;
        public bool DayOfWeek1 = false;  // sun
        public bool DayOfWeek2 = true;  // mon
        public bool DayOfWeek3 = true;  // tue
        public bool DayOfWeek4 = true;  // wed
        public bool DayOfWeek5 = true;  // thu
        public bool DayOfWeek6 = true;  // fri
        public bool DayOfWeek7 = false;  // sat
        public bool IsPeriodEnable = false;  //  기간설정을 할것인지
        public int DisplayStartH = 12;  // 표출시간설정
        public int DisplayStartM = 0;
        public int DisplayEndH = 13;
        public int DisplayEndM = 0;
        public int PeriodStartYear = 0;  // 기간설정
        public int PeriodStartMonth = 0;
        public int PeriodStartDay = 0;
        public int PeriodEndYear = 0;
        public int PeriodEndMonth = 0;
        public int PeriodEndDay = 0;

        public SpecialScheduleInfoClass()
        {
            GUID = Guid.NewGuid().ToString();
        }

        public void Clear()
        {
            PlayerNames = new List<string>();
            GroupNames = new List<string>();
            GUID = string.Empty;
            PageListName = string.Empty;
            DayOfWeek1 = false;  // sun
            DayOfWeek2 = true;  // mon
            DayOfWeek3 = true;  // tue
            DayOfWeek4 = true;  // wed
            DayOfWeek5 = true;  // thu
            DayOfWeek6 = true;  // fri
            DayOfWeek7 = false;  // sat
            IsPeriodEnable = false;  //  기간설정을 할것인지
            DisplayStartH = 12;  // 표출시간설정
            DisplayStartM = 0;
            DisplayEndH = 13;
            DisplayEndM = 0;
            PeriodStartYear = 0;  // 기간설정
            PeriodStartMonth = 0;
            PeriodStartDay = 0;
            PeriodEndYear = 0;
            PeriodEndMonth = 0;
            PeriodEndDay = 0;
        }

        public void CopyDataExceptGUID(SpecialScheduleInfoClass tmpData)
        {
            if (tmpData == null)
                return;

            this.PlayerNames = tmpData.PlayerNames == null
                ? new List<string>()
                : tmpData.PlayerNames.Where(x => string.IsNullOrWhiteSpace(x) == false)
                    .Distinct(StringComparer.CurrentCultureIgnoreCase)
                    .ToList();
            this.GroupNames = tmpData.GroupNames == null
                ? new List<string>()
                : tmpData.GroupNames.Where(x => string.IsNullOrWhiteSpace(x) == false)
                    .Distinct(StringComparer.CurrentCultureIgnoreCase)
                    .ToList();
           // this.GUID = tmpData.GUID;
            this.PageListName = tmpData.PageListName;
            this.DayOfWeek1 = tmpData.DayOfWeek1;
            this.DayOfWeek2 = tmpData.DayOfWeek2;
            this.DayOfWeek3 = tmpData.DayOfWeek3;
            this.DayOfWeek4 = tmpData.DayOfWeek4;
            this.DayOfWeek5 = tmpData.DayOfWeek5;
            this.DayOfWeek6 = tmpData.DayOfWeek6;
            this.DayOfWeek7 = tmpData.DayOfWeek7;
            this.IsPeriodEnable = tmpData.IsPeriodEnable;  //  기간설정을 할것인지
            this.DisplayStartH = tmpData.DisplayStartH;   // 표출시간설정
            this.DisplayStartM = tmpData.DisplayStartM;
            this.DisplayEndH = tmpData.DisplayEndH;
            this.DisplayEndM = tmpData.DisplayEndM;

            this.PeriodStartYear = tmpData.PeriodStartYear;
            this.PeriodStartMonth = tmpData.PeriodStartMonth;
            this.PeriodStartDay = tmpData.PeriodStartDay;
            this.PeriodEndYear = tmpData.PeriodEndYear;
            this.PeriodEndMonth = tmpData.PeriodEndMonth;
            this.PeriodEndDay = tmpData.PeriodEndDay;
        }
        public void CopyData(SpecialScheduleInfoClass tmpData)
        {
            if (tmpData == null)
                return;

            this.PlayerNames = tmpData.PlayerNames == null
                ? new List<string>()
                : tmpData.PlayerNames.Where(x => string.IsNullOrWhiteSpace(x) == false)
                    .Distinct(StringComparer.CurrentCultureIgnoreCase)
                    .ToList();
            this.GroupNames = tmpData.GroupNames == null
                ? new List<string>()
                : tmpData.GroupNames.Where(x => string.IsNullOrWhiteSpace(x) == false)
                    .Distinct(StringComparer.CurrentCultureIgnoreCase)
                    .ToList();
            this.GUID = tmpData.GUID;
            this.PageListName = tmpData.PageListName;
            this.DayOfWeek1 = tmpData.DayOfWeek1;
            this.DayOfWeek2 = tmpData.DayOfWeek2;
            this.DayOfWeek3 = tmpData.DayOfWeek3;
            this.DayOfWeek4 = tmpData.DayOfWeek4;
            this.DayOfWeek5 = tmpData.DayOfWeek5;
            this.DayOfWeek6 = tmpData.DayOfWeek6;
            this.DayOfWeek7 = tmpData.DayOfWeek7;
            this.IsPeriodEnable = tmpData.IsPeriodEnable;  //  기간설정을 할것인지
            this.DisplayStartH = tmpData.DisplayStartH;   // 표출시간설정
            this.DisplayStartM = tmpData.DisplayStartM;
            this.DisplayEndH = tmpData.DisplayEndH;
            this.DisplayEndM = tmpData.DisplayEndM; 
            this.PeriodStartYear = tmpData.PeriodStartYear;
            this.PeriodStartMonth = tmpData.PeriodStartMonth;
            this.PeriodStartDay = tmpData.PeriodStartDay;
            this.PeriodEndYear = tmpData.PeriodEndYear;
            this.PeriodEndMonth = tmpData.PeriodEndMonth;
            this.PeriodEndDay = tmpData.PeriodEndDay;
        }

        public bool CheckOverlappedPeriod(SpecialScheduleInfoClass newSchCls)
        {
            bool IsSamePeriodExist = false;

            string thisStartDateStr = string.Format("{0}{1:D2}{2:D2}", PeriodStartYear, 
                PeriodStartMonth, PeriodStartDay);
            string thisEndDateStr   = string.Format("{0}{1:D2}{2:D2}", PeriodEndYear, 
                PeriodEndMonth, PeriodEndDay); 
            string thisStartTimeStr = string.Format("{0:D2}{1:D2}", this.DisplayStartH, this.DisplayStartM);
            string thisEndTimeStr   = string.Format("{0:D2}{1:D2}", this.DisplayEndH, this.DisplayEndM);

            string newSchStartDateStr = string.Format("{0}{1:D2}{2:D2}", newSchCls.PeriodStartYear, 
                newSchCls.PeriodStartMonth, newSchCls.PeriodStartDay);
            string newSchEndDateStr = string.Format("{0}{1:D2}{2:D2}", newSchCls.PeriodEndYear, 
                newSchCls.PeriodEndMonth, newSchCls.PeriodEndDay);
            string newSchStartTimeStr = string.Format("{0:D2}{1:D2}", newSchCls.DisplayStartH, newSchCls.DisplayStartM);
            string newSchEndTimeStr = string.Format("{0:D2}{1:D2}", newSchCls.DisplayEndH, newSchCls.DisplayEndM);

            int thisStartDateInteger = Int32.Parse(thisStartDateStr);
            int thisEndDateInteger = Int32.Parse(thisEndDateStr);

            int thisStartTimeInteger = Int32.Parse(thisStartTimeStr);
            int thisEndTimeInteger = Int32.Parse(thisEndTimeStr);


            int newSchClsStartDateInteger = Int32.Parse(newSchStartDateStr);
            int newSchClsEndDateInteger = Int32.Parse(newSchEndDateStr);

            int newSchClsStartTimeInteger = Int32.Parse(newSchStartTimeStr);
            int newSchClsEndTimeInteger = Int32.Parse(newSchEndTimeStr);

            // 둘다 기간설정이 되어 있는 조건외의 조건은 같다. (어차피 기간설정안된 한개가 매주 돌아갈테니..)
            if (this.IsPeriodEnable == true && newSchCls.IsPeriodEnable == true)
            {
                // 기간이 겹치는가?  
                if (CheckIsDateOverlapped(thisStartDateInteger, thisEndDateInteger, newSchClsStartDateInteger, newSchClsEndDateInteger) == true)
                {
                    // 기간이 겹치면 요일별 시간이 겹치는지 확인한다.
                    if (IsCheckOverlappingTimeOnDayofWeek(newSchCls, thisStartTimeInteger, thisEndTimeInteger,
                        newSchClsStartTimeInteger, newSchClsEndTimeInteger) == true)
                    {
                        IsSamePeriodExist = true;
                    }

                }
                else
                {
                    IsSamePeriodExist = false;
                }
            }
            else 
            {
                if (IsCheckOverlappingTimeOnDayofWeek(newSchCls, thisStartTimeInteger, thisEndTimeInteger,
                    newSchClsStartTimeInteger, newSchClsEndTimeInteger) == true)
                {
                    IsSamePeriodExist = true;
                }
            }
           
            //else if (this.IsPeriodEnable == false && newSchCls.IsPeriodEnable == true)  // 하나라도 기간설정이 안되어있으면
            //else if (this.IsPeriodEnable == true && newSchCls.IsPeriodEnable == false)
            //if (this.IsPeriodEnable == false && newSchCls.IsPeriodEnable == false)
          

            return IsSamePeriodExist;
        }


        public bool CheckIsDateOverlapped(int thisStartDate, int thisEndDate, int newSchStartDate, int newSchEndDate)
        {
            List<int> tmpIntegetList = new List<int>();
            tmpIntegetList.Clear();

            if (tmpIntegetList.Contains(thisStartDate) == false)
                tmpIntegetList.Add(thisStartDate);

            if (tmpIntegetList.Contains(thisEndDate) == false)
                tmpIntegetList.Add(thisEndDate);

            if (tmpIntegetList.Contains(newSchStartDate) == false)
                tmpIntegetList.Add(newSchStartDate);

            if (tmpIntegetList.Contains(newSchEndDate) == false)
                tmpIntegetList.Add(newSchEndDate);


            if (tmpIntegetList.Count != 4)  // 하나라도 겹치는게 있으면 시간이 겹친다고 판단한다.
            {
                return true;
            }


            tmpIntegetList.Sort();


            /////////////////////////////////////////////////////////////////////////////////////////////
            //  시간겹치는걸 판단하는것은 결국은 순서다. A1, A2, B1, B2 아니면 B1, B2, A1, A2    2는 절대 1앞에 올수 없다. 

            if (tmpIntegetList[0] == thisStartDate &&
                tmpIntegetList[1] == thisEndDate &&
                tmpIntegetList[2] == newSchStartDate &&
                tmpIntegetList[3] == newSchEndDate)
            {
                return false;
            }
            else if (tmpIntegetList[0] == thisStartDate &&
                tmpIntegetList[1] == thisEndDate &&
                tmpIntegetList[2] == newSchStartDate &&
                tmpIntegetList[3] == newSchEndDate)
            {
                return false;
            }
            else
            {
                return true;
            }           
        }


        public bool IsCheckOverlappingTimeOnDayofWeek(SpecialScheduleInfoClass newSchCls,
            int thisStartTime, int thisEndTime, int newSchStartTime, int newSchEndTime)
        {
            bool IsSamePeriodExist = false;

            //////////// SUN ////////////////////////////
            if (this.DayOfWeek1 == true && newSchCls.DayOfWeek1 == true)
            {
                if (CheckIsTimeOverlapped(thisStartTime, thisEndTime, newSchStartTime, newSchEndTime) == true)
                {
                    IsSamePeriodExist = true;
                }
            }

            //////////// MON ////////////////////////////
            if (this.DayOfWeek2 == true && newSchCls.DayOfWeek2 == true)
            {
                if (CheckIsTimeOverlapped(thisStartTime, thisEndTime, newSchStartTime, newSchEndTime) == true)
                {
                    IsSamePeriodExist = true;
                }
            }

            //////////// TUE ////////////////////////////
            if (this.DayOfWeek3 == true && newSchCls.DayOfWeek3 == true)
            {
                if (CheckIsTimeOverlapped(thisStartTime, thisEndTime, newSchStartTime, newSchEndTime) == true)
                {
                    IsSamePeriodExist = true;
                }
            }

            //////////// WED ////////////////////////////
            if (this.DayOfWeek4 == true && newSchCls.DayOfWeek4 == true)
            {
                if (CheckIsTimeOverlapped(thisStartTime, thisEndTime, newSchStartTime, newSchEndTime) == true)
                {
                    IsSamePeriodExist = true;
                }
            }

            //////////// THU ////////////////////////////
            if (this.DayOfWeek5 == true && newSchCls.DayOfWeek5 == true)
            {
                if (CheckIsTimeOverlapped(thisStartTime, thisEndTime, newSchStartTime, newSchEndTime) == true)
                {
                    IsSamePeriodExist = true;
                }
            }

            //////////// FRI ////////////////////////////
            if (this.DayOfWeek6 == true && newSchCls.DayOfWeek6 == true)
            {
                if (CheckIsTimeOverlapped(thisStartTime, thisEndTime, newSchStartTime, newSchEndTime) == true)
                {
                    IsSamePeriodExist = true;
                }
            }

            //////////// SAT ////////////////////////////
            if (this.DayOfWeek7 == true && newSchCls.DayOfWeek7 == true)
            {
                if (CheckIsTimeOverlapped(thisStartTime, thisEndTime, newSchStartTime, newSchEndTime) == true)
                {
                    IsSamePeriodExist = true;
                }
            }

            return IsSamePeriodExist;
        }

        public bool CheckIsTimeOverlapped(int thisStartTime, int thisEndTime, int newSchStartTime, int newSchEndTime)
        {
            int thisStartMinutes = ConvertToMinutes(thisStartTime);
            int thisEndMinutes = ConvertToMinutes(thisEndTime);
            int newStartMinutes = ConvertToMinutes(newSchStartTime);
            int newEndMinutes = ConvertToMinutes(newSchEndTime);

            if (thisStartMinutes < 0 || thisEndMinutes < 0 || newStartMinutes < 0 || newEndMinutes < 0)
                return true;

            List<int[]> thisRanges = BuildTimeRanges(thisStartMinutes, thisEndMinutes);
            List<int[]> newRanges = BuildTimeRanges(newStartMinutes, newEndMinutes);

            foreach (int[] thisRange in thisRanges)
            {
                foreach (int[] newRange in newRanges)
                {
                    if (thisRange[0] <= newRange[1] && newRange[0] <= thisRange[1])
                        return true;
                }
            }

            return false;
        }

        private static int ConvertToMinutes(int timeValue)
        {
            if (timeValue < 0)
                return -1;

            int hour = timeValue / 100;
            int minute = timeValue % 100;

            if (hour < 0 || hour > 23 || minute < 0 || minute > 59)
                return -1;

            return (hour * 60) + minute;
        }

        private static List<int[]> BuildTimeRanges(int startMinutes, int endMinutes)
        {
            List<int[]> ranges = new List<int[]>();

            if (startMinutes == endMinutes)
            {
                ranges.Add(new[] { 0, 24 * 60 });
                return ranges;
            }

            int normalizedEnd = endMinutes;
            if (endMinutes < startMinutes)
                normalizedEnd = endMinutes + (24 * 60);

            ranges.Add(new[] { startMinutes, normalizedEnd });
            return ranges;
        }
    }

  
}
