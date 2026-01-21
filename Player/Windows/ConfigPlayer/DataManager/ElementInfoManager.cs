using System.Collections.Generic;
using AndoW.Shared;

namespace ConfigPlayer
{
    public class ElementInfoManager
    {
        public List<ElementInfoClass> g_ElementInfoClassList = new List<ElementInfoClass>();
        public string g_CurrentPageName = string.Empty;

        public ElementInfoManager()
        {
        }

        public void LoadData(string pageName)
        {
            g_CurrentPageName = pageName;
            // PageInfoManager가 이미 LiteDB에서 요소를 포함한 PageInfoClass를 로드하므로,
            // 여기서는 PageInfoManager의 결과를 그대로 받아 사용하도록 외부에서 설정해준다.
            // (XML 로딩 대체)
        }
    }
}
