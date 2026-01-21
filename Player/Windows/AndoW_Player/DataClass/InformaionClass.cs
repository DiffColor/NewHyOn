using System.Data;


namespace HyOnPlayer
{
    public enum WindowType { contentsPlayWindow, scrollTextWindow, welcomeBoardWindow}

    public class WindowZIdxForTTClass
    {
        public WindowType AI_WindowType = WindowType.contentsPlayWindow;
        public int AI_WindowIndex = 0;
        public int AI_Zorder = 0;

        public void CopyData(WindowZIdxForTTClass tmpData)
        {
            this.AI_WindowType = tmpData.AI_WindowType;
            this.AI_WindowIndex = tmpData.AI_WindowIndex;
            this.AI_Zorder = tmpData.AI_Zorder;
        }
    }


    public class WindowIdxForZorderClass
    {
        public int AI_WindowIndex = 0;
        public int AI_Zorder = 0;

        public void CopyData(WindowIdxForZorderClass tmpData)
        {
            this.AI_WindowIndex = tmpData.AI_WindowIndex;
            this.AI_Zorder = tmpData.AI_Zorder;
        }
    }

   

    public class DataTableForParam
    {
        public DataTable g_DtUpdateFileInfo = new DataTable("TB_DtUpdateFileInfo");

        public DataTableForParam()
        {
            InitDataTable();
        }

        public void InitDataTable()
        {
            g_DtUpdateFileInfo.Columns.Add("FIT_FileName", typeof(string));
            g_DtUpdateFileInfo.Columns.Add("FIT_SrcFileFullPath", typeof(string));
            g_DtUpdateFileInfo.Columns.Add("FIT_PageName", typeof(string));
        }
    }
     

    public class CopyFileInfo
    {
        public string fileName = string.Empty;
        public string pageName = string.Empty;
        public string targetFileName = string.Empty;
        public string fileSourceFullPath = string.Empty;
        public string fileCategory = string.Empty;

        public void CopyData(CopyFileInfo tmpData)
        {
            this.fileName = tmpData.fileName;
            this.pageName = tmpData.pageName;
            this.targetFileName = tmpData.targetFileName;
            this.fileSourceFullPath = tmpData.fileSourceFullPath;
            this.fileCategory = tmpData.fileCategory;
        }
    }

    public class FileCopyInfoFailedClass
    {
        public string FCI_RemotePath = string.Empty;
        public string FCI_LocalPath = string.Empty;
        public string FCI_FileName = string.Empty;

        public FileCopyInfoFailedClass(string rPath, string lPath, string fName)
        {
            this.FCI_RemotePath = rPath;
            this.FCI_LocalPath = lPath;
            this.FCI_FileName = fName;
        }

        public void CopyData(FileCopyInfoFailedClass tmpData)
        {
            this.FCI_RemotePath = tmpData.FCI_RemotePath;
            this.FCI_LocalPath = tmpData.FCI_LocalPath;
            this.FCI_FileName = tmpData.FCI_FileName;
        }
    }


    public class SubTitleInfoClass
    {
        public string subTitleStr = string.Empty;
        public string ResverveData1 = string.Empty;
        public string ResverveData2 = string.Empty;
        public int subIdx = 0;

        public void CopyData(SubTitleInfoClass tmpData)
        {
            this.subTitleStr = tmpData.subTitleStr;
            this.ResverveData1 = tmpData.ResverveData1;
            this.ResverveData2 = tmpData.ResverveData2;
            this.subIdx = tmpData.subIdx;         
        }

        public bool CheckIsSameData(SubTitleInfoClass tmpData)
        {
            bool IsSame = false;

            if (this.subTitleStr == tmpData.subTitleStr &&
            this.ResverveData1 == tmpData.ResverveData1 &&
            this.ResverveData2 == tmpData.ResverveData2 )
            {
                IsSame = true;
            }
            return IsSame;
        }
    }

    public class EditFontInfoClass
    {
        public string EFT_ForeGoundColor = string.Empty;
        public string EFT_BackGoundColor = string.Empty;
        public string EFT_FontName = string.Empty;

        public void CopyData(EditFontInfoClass paramCls)
        {
            this.EFT_ForeGoundColor = paramCls.EFT_ForeGoundColor;
            this.EFT_BackGoundColor = paramCls.EFT_BackGoundColor;
            this.EFT_FontName = paramCls.EFT_FontName;
        }
    }

    public class SavedContentsInfo
    {
        public string SCI_ContentsName = string.Empty;
        public int SCI_Index = 0;

        public void CopyData(SavedContentsInfo tmpData)
        {
            this.SCI_ContentsName = tmpData.SCI_ContentsName;
            this.SCI_Index = tmpData.SCI_Index;
        }
    }

   
}
