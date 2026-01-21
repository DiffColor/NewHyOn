using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using TurtleTools;

namespace AndoW_Manager
{
    public class PageListInfoManager : RethinkDbManagerBase<PageListInfoClass>
    {
        public List<PageListInfoClass> g_PageListInfoClassList = new List<PageListInfoClass>();

        public PageListInfoManager()
            : base(RethinkDbConfigurator.GetDataDatabaseName(), nameof(PageListInfoManager), "id")
        {
            LoadDataFromDatabase();
        }

        public void LoadDataFromDatabase()
        {
            g_PageListInfoClassList = LoadAllDocuments();
        }

        public PageListInfoClass GetPageListByName(string pageListName)
        {
            if (string.IsNullOrWhiteSpace(pageListName))
            {
                return null;
            }

            return g_PageListInfoClassList.FirstOrDefault(x =>
                x.PLI_PageListName.Equals(pageListName, StringComparison.CurrentCultureIgnoreCase));
        }

        public PageListInfoClass GetOrCreatePageList(string pageListName)
        {
            var pageList = GetPageListByName(pageListName);
            if (pageList != null)
            {
                return pageList;
            }

            pageList = new PageListInfoClass
            {
                PLI_PageListName = pageListName
            };

            EnsureListHasId(pageList);
            g_PageListInfoClassList.Add(pageList);
            SavePageList(pageList);
            return pageList;
        }

        public bool CheckExistSamename(string paramName)
        {
            foreach (PageListInfoClass item in g_PageListInfoClassList)
            {
                if (item.PLI_PageListName == paramName)
                {
                    return true;
                }
            }

            return false;
        }

        public void AddPageListInfoClass(PageListInfoClass paramCls)
        {
            PageListInfoClass tmpCls = new PageListInfoClass();
            tmpCls.CopyData(paramCls);

            g_PageListInfoClassList.Add(tmpCls);
            SavePageList(tmpCls);
        }

        public void AddPageToPlaylist(string pageListName, string pageId)
        {
            if (string.IsNullOrWhiteSpace(pageListName) || string.IsNullOrWhiteSpace(pageId))
            {
                return;
            }

            var list = GetOrCreatePageList(pageListName);
            if (list.PLI_Pages == null)
            {
                list.PLI_Pages = new List<string>();
            }

            if (!list.PLI_Pages.Contains(pageId))
            {
                list.PLI_Pages.Add(pageId);
                SavePageList(list);
            }
        }

        public void RemovePageFromPlaylist(string pageListName, string pageId)
        {
            var list = GetPageListByName(pageListName);
            if (list == null || list.PLI_Pages == null)
            {
                return;
            }

            if (list.PLI_Pages.Remove(pageId))
            {
                SavePageList(list);
            }
        }

        public void AddPageListInfoClassByOperator(PageListInfoClass paramCls)
        {
            PageListInfoClass tmpCls = new PageListInfoClass();
            tmpCls.CopyData(paramCls);

            DeletePageListInfoByName(tmpCls.PLI_PageListName);

            g_PageListInfoClassList.Add(tmpCls);
            SavePageList(tmpCls);
        }

        public void EditPageListDirection(string listname, string direction)
        {
            foreach (PageListInfoClass item in g_PageListInfoClassList)
            {
                if (item.PLI_PageListName == listname)
                {
                    item.PLI_PageDirection = direction;
                    SavePageList(item);
                    break;
                }
            }
        }

        public void DeletePageListInfoByName(string playListName)
        {
            int idx = 0;

            foreach (PageListInfoClass item in g_PageListInfoClassList)
            {
                if (item.PLI_PageListName == playListName)
                {
                    break;
                }
                idx++;

            }

            if (g_PageListInfoClassList.Count > idx)
            {
                PageListInfoClass removed = g_PageListInfoClassList[idx];
                g_PageListInfoClassList.RemoveAt(idx);
                RemovePageListFromDatabase(removed);
                LoadDataFromDatabase();
            }
        }

        public void DeletePageListInfo(PageListInfoClass paramCls)
        {
            int idx = 0;

            foreach (PageListInfoClass item in g_PageListInfoClassList)
            {
                if (item.PLI_PageListName == paramCls.PLI_PageListName)
                {
                    break;
                }
                idx++;

            }

            if (g_PageListInfoClassList.Count > idx)
            {
                PageListInfoClass removed = g_PageListInfoClassList[idx];
                g_PageListInfoClassList.RemoveAt(idx);
                RemovePageListFromDatabase(removed);
            }
        }

        public void SavePageList(PageListInfoClass pageList)
        {
            if (pageList == null)
            {
                return;
            }

            EnsureListHasId(pageList);
            Upsert(pageList);
        }

        public string GetPageDirection(string listname)
        {
            foreach (PageListInfoClass tempClass in g_PageListInfoClassList)
            {
                if (tempClass.PLI_PageListName == listname)
                {
                    return tempClass.PLI_PageDirection;
                }
            }

            return null;
        }

        private void EnsureListHasId(PageListInfoClass pageList)
        {
            if (pageList == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(pageList.Id))
            {
                pageList.Id = Guid.NewGuid().ToString();
            }
        }

        private void RemovePageListFromDatabase(PageListInfoClass pageList)
        {
            if (pageList == null)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(pageList.Id))
            {
                DeleteById(pageList.Id);
                return;
            }

            DeleteMany(x => x.PLI_PageListName.Equals(pageList.PLI_PageListName, StringComparison.CurrentCultureIgnoreCase));
        }
    }

    public class PageListInfoClass
    {
        [JsonProperty("id")]
        public string Id { get; set; }
        public string PLI_PageListName = string.Empty;
        public string PLI_CreateTimeStr = string.Empty;
        public string PLI_PageDirection = string.Empty;
        public List<string> PLI_Pages = new List<string>();

        public PageListInfoClass()
        {
            PLI_CreateTimeStr = DateTime.Now.ToShortDateString();
        }

        public void CopyData(PageListInfoClass paramCls)
        {
            this.PLI_PageListName = paramCls.PLI_PageListName;
            this.PLI_CreateTimeStr = paramCls.PLI_CreateTimeStr;
            this.PLI_PageDirection = paramCls.PLI_PageDirection;
            this.PLI_Pages = paramCls.PLI_Pages?.ToList() ?? new List<string>();
        }

        public void CleanDataField()
        {
            this.PLI_PageListName = string.Empty;
            this.PLI_CreateTimeStr = string.Empty;
            this.PLI_PageDirection = string.Empty;
            this.PLI_Pages.Clear();
        }
    }
}
