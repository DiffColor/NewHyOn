using AndoW.LiteDb;
using System;
using System.Collections.Generic;
using System.Linq;
using SharedContentsInfoClass = AndoW.Shared.ContentsInfoClass;
using SharedElementInfoClass = AndoW.Shared.ElementInfoClass;

namespace ConfigPlayer
{
    public class PageInfoManager
    {
        private readonly PageRepository pageRepository = new PageRepository();
        private readonly PageListRepository pageListRepository = new PageListRepository();

        public List<PageInfoClass> g_PageInfoClassList = new List<PageInfoClass>();
        public string g_PageListName = string.Empty;

        public PageInfoManager()
        {
        }

        public bool LoadData(string paramPageListName)
        {
            g_PageListName = paramPageListName;
            LoadPageList(paramPageListName);
            return g_PageInfoClassList.Count > 0;
        }

        private void LoadPageList(string pageListName)
        {
            g_PageInfoClassList.Clear();

            var pageList = pageListRepository.FindOne(x => x.PLI_PageListName.Equals(pageListName, StringComparison.CurrentCultureIgnoreCase));
            if (pageList == null || pageList.PLI_Pages == null || pageList.PLI_Pages.Count == 0)
            {
                return;
            }

            var orderedIds = pageList.PLI_Pages.ToList();
            var storedPages = pageRepository.Find(x => orderedIds.Contains(x.PIC_GUID))
                .ToDictionary(x => x.PIC_GUID, x => x);

            foreach (string pageId in orderedIds)
            {
                if (storedPages.TryGetValue(pageId, out var page))
                {
                    PageInfoClass clone = new PageInfoClass();
                    clone.CopyData(page);
                    g_PageInfoClassList.Add(clone);
                }
            }
        }

        private class PageRepository : LiteDbRepository<PageInfoClass>
        {
            public PageRepository() : base("PageInfoManager", "PIC_GUID") { }
        }

        private class PageListRepository : LiteDbRepository<PageListInfoClass>
        {
            public PageListRepository() : base("PageListInfoManager", "id") { }
        }
    }
}
