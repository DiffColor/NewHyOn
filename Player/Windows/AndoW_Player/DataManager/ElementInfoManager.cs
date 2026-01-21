using System.Collections.Generic;
using AndoW.Shared;

namespace HyOnPlayer
{
    public class ElementInfoManager
    {
        public List<ElementInfoClass> g_ElementInfoClassList = new List<ElementInfoClass>();
        public ContentsInfoManager g_ContentsInfoManager = new ContentsInfoManager();
        public string g_CurrentPageName = string.Empty;

        public ElementInfoManager()
        {
        }

        public void LoadData(string pageName)
        {
            g_CurrentPageName = pageName;
            // LiteDB 기반 PageInfoManager에서 요소를 로드하므로 별도 XML 처리 없음
        }
    }
}
