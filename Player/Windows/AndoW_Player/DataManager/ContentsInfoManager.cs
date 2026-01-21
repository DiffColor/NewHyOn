using System.Collections.Generic;
using AndoW.Shared;
using SharedContentsInfoClass = AndoW.Shared.ContentsInfoClass;

namespace HyOnPlayer
{
    public class ContentsInfoManager
    {
        public List<SharedContentsInfoClass> g_ContentsInfoClassList = new List<SharedContentsInfoClass>();
        public string g_CurrentPageName = string.Empty;

        public ContentsInfoManager()
        {
        }

        public void LoadData(string pageName, string elementName)
        {
            g_CurrentPageName = pageName;
            // LiteDB에서 PageInfo 로드 시 콘텐츠 포함되므로 별도 XML 로딩 없음
        }

        public void AddDataInfo(SharedContentsInfoClass paramCls)
        {
            SharedContentsInfoClass tmpCls = new SharedContentsInfoClass();
            tmpCls.CopyData(paramCls);
            g_ContentsInfoClassList.Add(tmpCls);
        }
    }
}
