using System;
using LiteDB;
using Newtonsoft.Json;

namespace AndoW.Shared
{
    public class TextInfoClass
    {
        [BsonId]
        [BsonField("id")]
        [JsonProperty("id")]
        public string CIF_Id { get; set; }

        public string CIF_PageName { get; set; } = string.Empty;
        public string CIF_DataFileName { get; set; } = string.Empty;
        public string CIF_TextContent { get; set; } = string.Empty;
        public string CIF_FontName { get; set; } = "Tahoma";
        public string CIF_FontColor { get; set; } = "#FFCBCBCB";
        public double CIF_FontSize { get; set; } = 76;
        public bool CIF_IsBold { get; set; }
        public bool CIF_IsItalic { get; set; }
        public string CIF_BGColor { get; set; } = "#FF000000";
        public string CIF_BGImageFileName { get; set; } = string.Empty;
        public string CIF_BGImageFileFullPath { get; set; } = string.Empty;
        public bool CIF_IsBGImageExist { get; set; }
        public int CIF_FontColorIndex { get; set; }
        public int CIF_BGColorIndex { get; set; } = 7;
        public string CIF_DataImageFileName { get; set; } = string.Empty;

        public TextInfoClass()
        {
        }

        public void CopyData(TextInfoClass paramCls)
        {
            if (paramCls == null) return;

            CIF_Id = paramCls.CIF_Id;
            CIF_PageName = paramCls.CIF_PageName;
            CIF_DataFileName = paramCls.CIF_DataFileName;
            CIF_TextContent = paramCls.CIF_TextContent;
            CIF_FontName = paramCls.CIF_FontName;
            CIF_FontColor = paramCls.CIF_FontColor;
            CIF_FontSize = paramCls.CIF_FontSize;
            CIF_IsBold = paramCls.CIF_IsBold;
            CIF_IsItalic = paramCls.CIF_IsItalic;
            CIF_BGColor = paramCls.CIF_BGColor;
            CIF_BGImageFileName = paramCls.CIF_BGImageFileName;
            CIF_BGImageFileFullPath = paramCls.CIF_BGImageFileFullPath;
            CIF_IsBGImageExist = paramCls.CIF_IsBGImageExist;
            CIF_FontColorIndex = paramCls.CIF_FontColorIndex;
            CIF_BGColorIndex = paramCls.CIF_BGColorIndex;
            CIF_DataImageFileName = paramCls.CIF_DataImageFileName;
        }
    }
}
