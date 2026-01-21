using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using TurtleTools;

namespace AndoW_Manager
{
    public class TextInfoManager : RethinkDbManagerBase<TextInfoClass>
    {
        public List<TextInfoClass> g_DataClassList = new List<TextInfoClass>();

        public TextInfoManager()
            : base(RethinkDbConfigurator.GetDataDatabaseName(), nameof(TextInfoManager), "id")
        {
        }

        public void LoadTextInfo(string paramPageName, string dataFileName)
        {
            var results = Find(x => x.CIF_PageName == paramPageName && x.CIF_DataFileName == dataFileName);
            if (results == null || results.Count == 0)
            {
                g_DataClassList = new List<TextInfoClass>
                {
                    CreateDefaultTextInfo(paramPageName, dataFileName)
                };
                return;
            }

            g_DataClassList = results;
        }

        public void AddDataInfo(TextInfoClass paramCls, string paramPageName, string dataFileName)
        {
            if (paramCls == null)
            {
                return;
            }

            TextInfoClass tmpCls = new TextInfoClass();
            tmpCls.CopyData(paramCls);
            tmpCls.CIF_PageName = paramPageName;
            tmpCls.CIF_DataFileName = dataFileName;

            TextInfoClass existing = GetExistingRecord(paramPageName, dataFileName);
            if (existing != null)
            {
                tmpCls.CIF_Id = existing.CIF_Id;
            }

            Upsert(tmpCls);
            g_DataClassList = new List<TextInfoClass> { tmpCls };
        }

        private TextInfoClass GetExistingRecord(string pageName, string dataFileName)
        {
            return FindOne(x => x.CIF_PageName == pageName && x.CIF_DataFileName == dataFileName);
        }

        private static TextInfoClass CreateDefaultTextInfo(string pageName, string dataFileName)
        {
            return new TextInfoClass
            {
                CIF_PageName = pageName,
                CIF_DataFileName = dataFileName
            };
        }

    }

    public class TextInfoClass
    {
        [JsonProperty("id")]
        public string CIF_Id = Guid.NewGuid().ToString();
        [JsonIgnore]
        public string Id
        {
            get { return CIF_Id; }
            set { CIF_Id = value; }
        }
        public string CIF_PageName = string.Empty;
        public string CIF_DataFileName = string.Empty;
        public string CIF_TextContent = "";
        public string CIF_FontName = "Malgun Gothic";
        public string CIF_FontColor = "#FFCBCBCB";
        public double CIF_FontSize = 76;
        public bool CIF_IsBold = false;
        public bool CIF_IsItalic = false;
        public string CIF_BGColor = "#FF000000";
        public string CIF_BGImageFileName = string.Empty;
        public string CIF_BGImageFileFullPath = string.Empty;
        public bool CIF_IsBGImageExist= false;
        public int CIF_FontColorIndex = 0;
        public int CIF_BGColorIndex = 7;
        public string CIF_DataImageFileName = string.Empty;

        public TextInfoClass()
        {
            this.CIF_Id = Guid.NewGuid().ToString();
            this.CIF_DataImageFileName = string.Format("{0}.png", Guid.NewGuid().ToString());
        }

        public void CopyData(TextInfoClass paramCls)
        {
            this.CIF_Id = paramCls.CIF_Id;
            this.CIF_PageName = paramCls.CIF_PageName;
            this.CIF_DataFileName = paramCls.CIF_DataFileName;
            this.CIF_TextContent = paramCls.CIF_TextContent;
            this.CIF_FontName = paramCls.CIF_FontName;
            this.CIF_FontColor = paramCls.CIF_FontColor;
            this.CIF_FontSize = paramCls.CIF_FontSize;
            this.CIF_IsBold = paramCls.CIF_IsBold;
            this.CIF_IsItalic = paramCls.CIF_IsItalic;
            this.CIF_BGColor = paramCls.CIF_BGColor;
            this.CIF_BGImageFileName = paramCls.CIF_BGImageFileName;
            this.CIF_BGImageFileFullPath = paramCls.CIF_BGImageFileFullPath;
            this.CIF_IsBGImageExist = paramCls.CIF_IsBGImageExist;
            this.CIF_FontColorIndex = paramCls.CIF_FontColorIndex;
            this.CIF_BGColorIndex = paramCls.CIF_BGColorIndex;
            this.CIF_DataImageFileName = paramCls.CIF_DataImageFileName;
        }
    }
}
