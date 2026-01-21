using System;
using System.Collections.Generic;
using System.IO;
using System.Data;
using System.Windows;
using TurtleTools;
using Newtonsoft.Json;

namespace AndoW_Manager
{
    public class ElementInfoControlClass
    {
        public List<ElementInfoClass> g_ElementInfoClassList = new List<ElementInfoClass>();
        public List<CopyFileInfo> g_CopyFileList = new List<CopyFileInfo>();


        public PageInfoClass g_PageInfoClass = new PageInfoClass();

        public ElementInfoControlClass()
        {
        }

        public void LoadData_ElementInfo(string pageName)
        {
            g_ElementInfoClassList.Clear();
            g_CopyFileList.Clear();

            if (string.IsNullOrWhiteSpace(pageName))
            {
                g_PageInfoClass = new PageInfoClass();
                return;
            }

            try
            {
                PageInfoClass definition = DataShop.Instance.g_PageInfoManager.GetPageDefinition(pageName);
                if (definition == null)
                {
                    g_PageInfoClass = new PageInfoClass();
                    return;
                }

                g_PageInfoClass = new PageInfoClass();
                g_PageInfoClass.CopyData(definition);

                List<ElementInfoClass> elements = CloneElementList(definition.PIC_Elements);
                if (elements.Count > 0)
                {
                    foreach (ElementInfoClass element in elements)
                    {
                        if (element == null || element.EIF_ContentsInfoClassList == null)
                        {
                            continue;
                        }

                        foreach (ContentsInfoClass content in element.EIF_ContentsInfoClassList)
                        {
                            if (content == null)
                            {
                                continue;
                            }

                            if (string.IsNullOrEmpty(content.CIF_FileFullPath))
                            {
                                content.CIF_FileFullPath = FNDTools.GetTargetContentsFilePath(content.CIF_FileName);
                            }
                        }
                    }
                }

                g_ElementInfoClassList.AddRange(elements);
            }
            catch (Exception ex)
            {
                Logger.WriteErrorLog(ex.ToString(), Logger.GetLogFileName());
            }
        }

        public void InitElementInfoListFromDataTable(string pageName)
        {
            try
            {
                g_ElementInfoClassList.Clear();
                g_CopyFileList.Clear();

                if (string.IsNullOrWhiteSpace(pageName))
                {
                    g_PageInfoClass = new PageInfoClass();
                    return;
                }

                PageInfoClass definition = DataShop.Instance.g_PageInfoManager.GetPageDefinition(pageName);
                if (definition == null)
                {
                    g_PageInfoClass = new PageInfoClass();
                    return;
                }

                g_PageInfoClass = new PageInfoClass();
                g_PageInfoClass.CopyData(definition);

                if (definition.PIC_Elements == null || definition.PIC_Elements.Count == 0)
                {
                    return;
                }

                foreach (ElementInfoClass element in definition.PIC_Elements)
                {
                    if (element == null)
                    {
                        continue;
                    }

                    ElementInfoClass clone = new ElementInfoClass();
                    clone.CopyData(element);
                    NormalizeContentInfo(clone);
                    g_ElementInfoClassList.Add(clone);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
        }

        private static void NormalizeContentInfo(ElementInfoClass element)
        {
            if (element?.EIF_ContentsInfoClassList == null)
            {
                return;
            }

            foreach (ContentsInfoClass content in element.EIF_ContentsInfoClassList)
            {
                if (content == null)
                {
                    continue;
                }

                if (string.IsNullOrEmpty(content.CIF_FileFullPath) && string.IsNullOrEmpty(content.CIF_FileName) == false)
                {
                    content.CIF_FileFullPath = FNDTools.GetTargetContentsFilePath(content.CIF_FileName);
                }
            }
        }

        public static List<ElementInfoClass> CloneElementList(IEnumerable<ElementInfoClass> source)
        {
            List<ElementInfoClass> cloneList = new List<ElementInfoClass>();
            if (source == null)
            {
                return cloneList;
            }

            foreach (ElementInfoClass element in source)
            {
                if (element == null)
                {
                    continue;
                }

                ElementInfoClass cloned = new ElementInfoClass();
                cloned.CopyData(element);
                cloneList.Add(cloned);
            }

            return cloneList;
        }

        private void TransformToLandscapeData()
        {
            foreach (ElementInfoClass eic in g_ElementInfoClassList)
            {
                eic.EIF_Width *= MainWindow.Instance.g_wLandScale;
                eic.EIF_Height *= MainWindow.Instance.g_hLandScale;
                eic.EIF_PosLeft *= MainWindow.Instance.g_wLandScale;
                eic.EIF_PosTop *= MainWindow.Instance.g_hLandScale;
            }
        }

    }

    public class ContentsInfoClass
    {
        public string CIF_FileName = string.Empty;             // ScrollText에서 자막내용
        public string CIF_FileFullPath = string.Empty;
        public string CIF_RelativePath = string.Empty;
        public string CIF_StrGUID = string.Empty;
        public string CIF_PlayMinute = "00";                   // ScrollText에서 ForeGround Color
        public string CIF_PlaySec = "10";                      // ScrollText에서 BackGournd Color
        public string CIF_ContentType = "Malgun Gothic";          // ScrollText에서 FontName
        public bool CIF_ValidTime = true;
        public bool CIF_FileExist = true;

        public int CIF_ScrollTextSpeedSec = 10;               // 자막속도
        public long CIF_FileSize = 0;
        public string CIF_FileHash = string.Empty;


        public ContentsInfoClass()
        {
            CIF_StrGUID = Guid.NewGuid().ToString();
        }

        public void CopyData(ContentsInfoClass tmpData)
        {
            this.CIF_FileName = tmpData.CIF_FileName;
            this.CIF_FileFullPath = tmpData.CIF_FileFullPath;
            this.CIF_RelativePath = tmpData.CIF_RelativePath;
            this.CIF_StrGUID = tmpData.CIF_StrGUID;
            this.CIF_PlayMinute = tmpData.CIF_PlayMinute;
            this.CIF_PlaySec = tmpData.CIF_PlaySec;
            this.CIF_ContentType = tmpData.CIF_ContentType;
            this.CIF_ValidTime = tmpData.CIF_ValidTime;
            this.CIF_FileExist = tmpData.CIF_FileExist;
            this.CIF_FileSize = tmpData.CIF_FileSize;
            this.CIF_FileHash = tmpData.CIF_FileHash;

            if (this.CIF_PlayMinute == "00" && this.CIF_PlaySec == "00")
            {
                this.CIF_ValidTime = false;
            }
            else
            {
                this.CIF_ValidTime = true;
            }

            CIF_RelativePath = $"Contents/{this.CIF_FileName}";

            if (this.CIF_FileFullPath != string.Empty)
            {
                try
                {
                    if (this.CIF_ContentType != ContentType.WebSiteURL.ToString())  // 컨텐트타입이 WebSiteURL이 아닐때만 파일 존재유무확인
                    {
                        if (File.Exists(this.CIF_FileFullPath) == false)
                        {
                            this.CIF_FileExist = false;
                            this.CIF_FileSize = 0;
                            this.CIF_FileHash = string.Empty;
                        }
                        else
                        {
                            this.CIF_FileExist = true;
                            UpdateFileMetadata(this);
                        }
                    }
                }
                catch (Exception ex)
                {
                    this.CIF_FileExist = false;
                    this.CIF_FileSize = 0;
                    this.CIF_FileHash = string.Empty;
                }
            }

            this.CIF_ScrollTextSpeedSec = tmpData.CIF_ScrollTextSpeedSec;
    

        }

        public void CopyDataWithOutGUID(ContentsInfoClass tmpData)
        {
            this.CIF_FileName = tmpData.CIF_FileName;
            this.CIF_FileFullPath = tmpData.CIF_FileFullPath;
            this.CIF_PlayMinute = tmpData.CIF_PlayMinute;
            this.CIF_PlaySec = tmpData.CIF_PlaySec;
            this.CIF_ContentType = tmpData.CIF_ContentType;
            this.CIF_ValidTime = tmpData.CIF_ValidTime;
            this.CIF_FileExist = tmpData.CIF_FileExist;
            this.CIF_FileSize = tmpData.CIF_FileSize;
            this.CIF_FileHash = tmpData.CIF_FileHash;

            if (this.CIF_PlayMinute == "00" && this.CIF_PlaySec == "00")
            {
                this.CIF_ValidTime = false;
            }
            else
            {
                this.CIF_ValidTime = true;
            }

            if (this.CIF_FileFullPath != string.Empty)
            {
                try
                {
                    if (File.Exists(this.CIF_FileFullPath) == false)
                    {
                        this.CIF_FileExist = false;
                        this.CIF_FileSize = 0;
                        this.CIF_FileHash = string.Empty;
                    }
                    else
                    {
                        this.CIF_FileExist = true;
                        UpdateFileMetadata(this);
                    }
                }
                catch (Exception ex)
                {
                    this.CIF_FileExist = false;
                    this.CIF_FileSize = 0;
                    this.CIF_FileHash = string.Empty;
                }
            }

            this.CIF_ScrollTextSpeedSec = tmpData.CIF_ScrollTextSpeedSec;
        }

        public bool CheckIsSameData(ContentsInfoClass tmpData)
        {
            bool IsSame = false;

            if (this.CIF_FileName == tmpData.CIF_FileName && this.CIF_FileFullPath == tmpData.CIF_FileFullPath &&
                 this.CIF_StrGUID == tmpData.CIF_StrGUID)
            {
                IsSame = true;
            }
            return IsSame;
        }

        private static void UpdateFileMetadata(ContentsInfoClass target)
        {
            try
            {
                if (target == null || string.IsNullOrEmpty(target.CIF_FileFullPath))
                {
                    return;
                }
                FileInfo info = new FileInfo(target.CIF_FileFullPath);
                if (!info.Exists)
                {
                    target.CIF_FileSize = 0;
                    target.CIF_FileHash = string.Empty;
                    return;
                }
                target.CIF_FileSize = info.Length;
                target.CIF_FileHash = XXHash64.ComputePartialSignature(target.CIF_FileFullPath);
            }
            catch
            {
                target.CIF_FileSize = 0;
                target.CIF_FileHash = string.Empty;
            }
        }
    }

    public class ElementInfoClass
    {
        public string EIF_Name = string.Empty;
        public string EIF_Type = string.Empty;
        public int EIF_RowVal = 0;
        public int EIF_ColVal = 0;
        public int EIF_RowSpanVal = 0;
        public int EIF_ColSpanVal = 0;
        public double EIF_Width = 0;
        public double EIF_Height = 0;
        public double EIF_PosTop = 0;
        public double EIF_PosLeft = 0;
        public int EIF_ZIndex = 0;
        public string EIF_DataFileName = string.Empty;
        public string EIF_DataFileFullPath = string.Empty;
        public List<ContentsInfoClass> EIF_ContentsInfoClassList = new List<ContentsInfoClass>();

        public void CopyData(ElementInfoClass tmpData)
        {
            this.EIF_Name = tmpData.EIF_Name;
            this.EIF_Type = tmpData.EIF_Type;
            this.EIF_RowVal = tmpData.EIF_RowVal;
            this.EIF_ColVal = tmpData.EIF_ColVal;
            this.EIF_RowSpanVal = tmpData.EIF_RowSpanVal;
            this.EIF_ColSpanVal = tmpData.EIF_ColSpanVal;
            this.EIF_Width = tmpData.EIF_Width;
            this.EIF_Height = tmpData.EIF_Height;
            this.EIF_PosTop = tmpData.EIF_PosTop;
            this.EIF_PosLeft = tmpData.EIF_PosLeft;
            this.EIF_ZIndex = tmpData.EIF_ZIndex;
            this.EIF_DataFileName = tmpData.EIF_DataFileName;
            this.EIF_DataFileFullPath = tmpData.EIF_DataFileFullPath;

            this.EIF_ContentsInfoClassList.Clear();

            if (tmpData.EIF_ContentsInfoClassList.Count > 0)
            {
                foreach (ContentsInfoClass item in tmpData.EIF_ContentsInfoClassList)
                {
                    ContentsInfoClass tmpCls = new ContentsInfoClass();
                    tmpCls.CopyData(item);
                    this.EIF_ContentsInfoClassList.Add(tmpCls);
                }
            }
        }

    }

    public class PreviewCanvas
    {
        public string Direction { get; set; }
        public int Rows { get; set; }
        public int Columns { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public bool FillContent { get; set; }

        public PreviewCanvas()
        {
            Direction = DeviceOrientation.Landscape.ToString();
            Rows = Columns = 1;
            Width = 1280;
            Height = 720;
            FillContent = false;
        }
    }

    public class PreviewElement
    {
        public string ElementType { get; set; }
        public int PosX { get; set; }
        public int PosY { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public int Index { get; set; }

        public List<PreviewData> DataList { get; set; }

        public PreviewElement()
        {
            ElementType = DisplayType.None.ToString();
            PosX = PosY = Width = Height = 0;
            DataList = new List<PreviewData>();
            Index = 0;
        }
    }

    public class PreviewData
    {
        public string DataType { get; set; }
        public string FilePath { get; set; }
        public int Playtime { get; set; }
        public string TextContent { get; set; }
        public string FontName { get; set; }
        public string FontColor { get; set; }
        public double FontSize { get; set; }
        public bool IsBold { get; set; }
        public bool IsItalic { get; set; }
        public string BGColor { get; set; }

        public PreviewData()
        {
            DataType = ContentType.None.ToString();
            FilePath = string.Empty;
            Playtime = 0;
        }
    }

    public class CopyFileInfo
    {
        public string CFI_FileName = string.Empty;
        public string CFI_TargetFileName = string.Empty;
        public string CFI_FileSourceFullPath = string.Empty;
        public string CFI_PageName = string.Empty;

        public void CopyData(CopyFileInfo tmpData)
        {
            this.CFI_FileName = tmpData.CFI_FileName;
            this.CFI_TargetFileName = tmpData.CFI_TargetFileName;
            this.CFI_FileSourceFullPath = tmpData.CFI_FileSourceFullPath;
            this.CFI_PageName = tmpData.CFI_PageName;
        }
    }

    public class DataTableForAndroid
    {
        public DataTable g_UpdateFileInfo = new DataTable("TB_UpdateFileInfo");

        public DataTableForAndroid()
        {
            InitDataTable();
        }

        public void InitDataTable()
        {
            g_UpdateFileInfo.Columns.Add("FIT_FileName", typeof(string));
            g_UpdateFileInfo.Columns.Add("FIT_RelativePath", typeof(string));
        }
    }

    public class UploadContentsInfoClass
    {
        public DataTable dt = new DataTable("TB_FileInfo");

        public UploadContentsInfoClass()
        {
            Initialize();
        }

        private void Initialize()
        {
            dt.Columns.Add("Name", typeof(string));
            dt.Columns.Add("Length", typeof(string));
            dt.Columns.Add("LastWriteTime", typeof(string));
        }
    }

    public class EditFontInfoClass
    {
        public string EFT_ForeGoundColor = "#FFFFFFFF";
        public string EFT_BackGoundColor = "#FF222222";
        public string EFT_FontName = "Malgun Gothic";

        public void CopyData(EditFontInfoClass paramCls)
        {
            this.EFT_ForeGoundColor = paramCls.EFT_ForeGoundColor;
            this.EFT_BackGoundColor = paramCls.EFT_BackGoundColor;
            this.EFT_FontName = paramCls.EFT_FontName;
        }
    }

    public class ElementPosClass
    {
        public double Left { get; set; }
        public double Top { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
    }
}
