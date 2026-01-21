using LiteDB;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;

namespace AndoW.Shared
{
    public class PageInfoClass
    {
        [BsonId]
        [BsonField("id")]
        [JsonProperty("id")]
        public string PIC_GUID { get; set; } = Guid.NewGuid().ToString();

        public string PIC_PageName { get; set; } = string.Empty;
        public int PIC_PlaytimeHour { get; set; }
        public int PIC_PlaytimeMinute { get; set; }
        public int PIC_PlaytimeSecond { get; set; } = 10;
        public int PIC_Volume { get; set; }
        public bool PIC_IsLandscape { get; set; } = true;
        public int PIC_Rows { get; set; } = 1;
        public int PIC_Columns { get; set; } = 1;
        public double PIC_CanvasWidth { get; set; } = 1920;
        public double PIC_CanvasHeight { get; set; } = 1080;
        public bool PIC_NeedGuide { get; set; } = true;
        public string PIC_Thumb { get; set; } = string.Empty;
        public List<ElementInfoClass> PIC_Elements { get; set; } = new List<ElementInfoClass>();

        public void CopyData(PageInfoClass paramCls)
        {
            if (paramCls == null) return;

            PIC_PageName = paramCls.PIC_PageName;
            PIC_PlaytimeHour = paramCls.PIC_PlaytimeHour;
            PIC_PlaytimeMinute = paramCls.PIC_PlaytimeMinute;
            PIC_PlaytimeSecond = paramCls.PIC_PlaytimeSecond;
            PIC_Volume = paramCls.PIC_Volume;
            PIC_GUID = paramCls.PIC_GUID;
            PIC_IsLandscape = paramCls.PIC_IsLandscape;
            PIC_Rows = paramCls.PIC_Rows > 0 ? paramCls.PIC_Rows : 1;
            PIC_Columns = paramCls.PIC_Columns > 0 ? paramCls.PIC_Columns : 1;
            PIC_CanvasWidth = paramCls.PIC_CanvasWidth > 0 ? paramCls.PIC_CanvasWidth : 1920;
            PIC_CanvasHeight = paramCls.PIC_CanvasHeight > 0 ? paramCls.PIC_CanvasHeight : 1080;
            PIC_NeedGuide = paramCls.PIC_NeedGuide;
            PIC_Thumb = paramCls.PIC_Thumb;

            PIC_Elements = new List<ElementInfoClass>();
            if (paramCls.PIC_Elements != null && paramCls.PIC_Elements.Count > 0)
            {
                foreach (ElementInfoClass element in paramCls.PIC_Elements)
                {
                    ElementInfoClass cloned = new ElementInfoClass();
                    cloned.CopyData(element);
                    PIC_Elements.Add(cloned);
                }
            }
        }
    }

    public class ElementInfoClass
    {
        public string EIF_Name { get; set; } = string.Empty;
        public string EIF_Type { get; set; } = string.Empty;
        public int EIF_RowVal { get; set; }
        public int EIF_ColVal { get; set; }
        public int EIF_RowSpanVal { get; set; }
        public int EIF_ColSpanVal { get; set; }
        public double EIF_Width { get; set; }
        public double EIF_Height { get; set; }
        public double EIF_PosTop { get; set; }
        public double EIF_PosLeft { get; set; }
        public int EIF_ZIndex { get; set; }
        public string EIF_DataFileName { get; set; } = string.Empty;
        public string EIF_DataFileFullPath { get; set; } = string.Empty;
        public List<ContentsInfoClass> EIF_ContentsInfoClassList { get; set; } = new List<ContentsInfoClass>();

        public void CopyData(ElementInfoClass tmpData)
        {
            if (tmpData == null) return;

            EIF_Name = tmpData.EIF_Name;
            EIF_Type = tmpData.EIF_Type;
            EIF_RowVal = tmpData.EIF_RowVal;
            EIF_ColVal = tmpData.EIF_ColVal;
            EIF_RowSpanVal = tmpData.EIF_RowSpanVal;
            EIF_ColSpanVal = tmpData.EIF_ColSpanVal;
            EIF_Width = tmpData.EIF_Width;
            EIF_Height = tmpData.EIF_Height;
            EIF_PosTop = tmpData.EIF_PosTop;
            EIF_PosLeft = tmpData.EIF_PosLeft;
            EIF_ZIndex = tmpData.EIF_ZIndex;
            EIF_DataFileName = tmpData.EIF_DataFileName;
            EIF_DataFileFullPath = tmpData.EIF_DataFileFullPath;

            EIF_ContentsInfoClassList = new List<ContentsInfoClass>();
            if (tmpData.EIF_ContentsInfoClassList?.Count > 0)
            {
                foreach (ContentsInfoClass item in tmpData.EIF_ContentsInfoClassList)
                {
                    ContentsInfoClass tmpNewCls = new ContentsInfoClass();
                    tmpNewCls.CopyData(item);
                    EIF_ContentsInfoClassList.Add(tmpNewCls);
                }
            }
        }
    }

    public class ContentsInfoClass
    {
        public string CIF_FileName { get; set; } = string.Empty;
        private string fileFullPath = string.Empty;

        [JsonProperty("CIF_FileFullPath")]
        public string CIF_FileFullPath
        {
            get => fileFullPath;
            set
            {
                fileFullPath = value;
                RefreshRelativePath();
            }
        }

        private void RefreshRelativePath()
        {
            CIF_RelativePath = fileFullPath.Replace(AppDomain.CurrentDomain.BaseDirectory, string.Empty).TrimStart('\\');
        }

        public string CIF_RelativePath { get; set; } = string.Empty;
        public string CIF_StrGUID { get; set; } = string.Empty;
        public string CIF_PlayMinute { get; set; } = "00";
        public string CIF_PlaySec { get; set; } = "10";
        public string CIF_ContentType { get; set; } = string.Empty;
        public bool CIF_ValidTime { get; set; } = true;
        public bool CIF_FileExist { get; set; } = true;
        public int CIF_ScrollTextSpeedSec { get; set; } = 10;
        public string CIF_ReservedData1 { get; set; } = string.Empty;
        public string CIF_ReservedData2 { get; set; } = string.Empty;
        public long CIF_FileSize { get; set; }
        public string CIF_FileHash { get; set; } = string.Empty;

        public ContentsInfoClass()
        {
            CIF_StrGUID = Guid.NewGuid().ToString();
        }

        public void CopyData(ContentsInfoClass tmpData)
        {
            if (tmpData == null) return;

            CIF_FileName = tmpData.CIF_FileName;
            CIF_FileFullPath = tmpData.CIF_FileFullPath;
            CIF_RelativePath = tmpData.CIF_RelativePath;
            CIF_StrGUID = tmpData.CIF_StrGUID;
            CIF_PlayMinute = tmpData.CIF_PlayMinute;
            CIF_PlaySec = tmpData.CIF_PlaySec;
            CIF_ContentType = tmpData.CIF_ContentType;
            CIF_ValidTime = !(tmpData.CIF_PlayMinute == "00" && tmpData.CIF_PlaySec == "00");
            CIF_FileExist = File.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Contents", tmpData.CIF_FileName));
            CIF_ScrollTextSpeedSec = tmpData.CIF_ScrollTextSpeedSec;
            CIF_ReservedData1 = tmpData.CIF_ReservedData1;
            CIF_ReservedData2 = tmpData.CIF_ReservedData2;
            CIF_FileSize = tmpData.CIF_FileSize;
            CIF_FileHash = tmpData.CIF_FileHash;
        }
    }
}
