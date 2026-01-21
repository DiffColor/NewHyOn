using System.Collections.Generic;
using AndoW.Shared;
using SharedContentsInfoClass = AndoW.Shared.ContentsInfoClass;

namespace ConfigPlayer
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
            // LiteDB 기반으로 PageInfoManager에서 콘텐츠가 포함된 ElementInfoClass를 받아 사용
        }

        public void AddDataInfo(SharedContentsInfoClass paramCls)
        {
            SharedContentsInfoClass tmpCls = new SharedContentsInfoClass();
            tmpCls.CopyData(paramCls);
            g_ContentsInfoClassList.Add(tmpCls);
        }
    }
}
