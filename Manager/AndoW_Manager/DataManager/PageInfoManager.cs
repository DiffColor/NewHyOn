using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TurtleTools;


namespace AndoW_Manager
{
    public class PageInfoManager : RethinkDbManagerBase<PageInfoClass>
    {
        public List<PageInfoClass> g_PageInfoClassList = new List<PageInfoClass>();

        public string g_PageListName = string.Empty;

        public PageInfoManager()
            : base(RethinkDbConfigurator.GetDataDatabaseName(), nameof(PageInfoManager), "id")
        {
        }

        public void LoadPagesForList(string pageListName)
        {
            g_PageListName = pageListName;

            LoadPageList(pageListName);
        }

        private void LoadPageList(string pageListName)
        {
            g_PageInfoClassList.Clear();

            var pageList = DataShop.Instance.g_PageListInfoManager.GetPageListByName(pageListName);
            if (pageList == null || pageList.PLI_Pages == null || pageList.PLI_Pages.Count == 0)
            {
                return;
            }

            var orderedIds = pageList.PLI_Pages.ToList();
            var storedPages = Find(x => orderedIds.Contains(x.PIC_GUID))
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

        public void ShiftDownDataInfo(PageInfoClass paramCls, string paramPageListName)
        {
            int idx = 0;
            foreach (PageInfoClass item in g_PageInfoClassList)
            {
                if (item.PIC_GUID == paramCls.PIC_GUID)
                {
                    break;
                }
                idx++;
            }


            if (idx == (g_PageInfoClassList.Count - 1))
            {
                return;
            }
            else
            {
                if (idx < (g_PageInfoClassList.Count - 1))
                {
                    g_PageInfoClassList.RemoveAt(idx);
                    idx++;

                    PageInfoClass tmpCls = new PageInfoClass();
                    tmpCls.CopyData(paramCls);

                    g_PageInfoClassList.Insert(idx, tmpCls);
                    SavePageList(paramPageListName);
                }
            }

        }

        public void ShiftUpDataInfo(PageInfoClass paramCls, string paramPageListName)
        {
            int idx = 0;
            foreach (PageInfoClass item in g_PageInfoClassList)
            {
                if (item.PIC_GUID == paramCls.PIC_GUID)
                {
                    break;
                }
                idx++;
            }


            if (idx == 0)
            {
                return;
            }
            else
            {
                if (idx > 0)
                {
                    g_PageInfoClassList.RemoveAt(idx);
                    idx--;

                    PageInfoClass tmpCls = new PageInfoClass();
                    tmpCls.CopyData(paramCls);

                    g_PageInfoClassList.Insert(idx, tmpCls);
                    SavePageList(paramPageListName);
                }
            }

        }

        public bool CheckExistSamename(string paramName)
        {
            bool IsSameExist = false;
            foreach (PageInfoClass item in g_PageInfoClassList)
            {
                if (item.PIC_PageName == paramName)
                {
                    IsSameExist = true;
                    break;
                }
               
            }

            return IsSameExist;
        }

        public void AddPageInfoClass(PageInfoClass paramCls, string paramPageListName)
        {
            bool needToRefresh = false;

            LoadPagesForList(paramPageListName);

            if (g_PageInfoClassList.Count > 0)
            {
                if (g_PageInfoClassList[0].PIC_IsLandscape != paramCls.PIC_IsLandscape)
                {
                    string msg = "세로형";
                    if (g_PageInfoClassList[0].PIC_IsLandscape)
                        msg = "가로형";

                    MessageTools.ShowMessageBox(string.Format("{0} 리스트입니다. {0} 페이지를 추가해주세요.", msg), "확인");
                    return;
                }
            }
            else
                needToRefresh = true;

            PageInfoClass tmpCls = new PageInfoClass();
            tmpCls.CopyData(paramCls);

            g_PageInfoClassList.Add(tmpCls);

            SavePageList(paramPageListName);

            if (needToRefresh)
                Page3.Instance.UpdatePListComboForPlayer();
        }

        public void EditDeviceInfoClass(string pageListName, PageInfoClass newCls, int paramIdx)
        {
            int idx = paramIdx - 1;

            if (idx > -1 && idx < g_PageInfoClassList.Count)
            {

                g_PageInfoClassList.RemoveAt(idx);

                PageInfoClass tmpCls = new PageInfoClass();
                tmpCls.CopyData(newCls);
                g_PageInfoClassList.Insert(idx, tmpCls);
                SavePageList(pageListName);
            }
        }

        public void DeletePageByPageName(string PageName)
        {

            int idx = 0;

            foreach (PageInfoClass item in g_PageInfoClassList)
            {
                if (item.PIC_PageName == PageName)
                {
                    break;
                }
                idx++;
            }

            g_PageInfoClassList.RemoveAt(idx);
            SavePageList(g_PageListName);
        }

        public void DeletePageInfoClass(string pageListName, PageInfoClass newCls)
        {
            int idx = 0;

            foreach (PageInfoClass item in g_PageInfoClassList)
            {
                if (item.PIC_PageName == newCls.PIC_PageName)
                {
                    break;
                }
                idx++;

            }

            if (idx < g_PageInfoClassList.Count)
            {
                g_PageInfoClassList.RemoveAt(idx);
                SavePageList(pageListName);
            }
            else
            {
                Page2.Instance.RefreshPageListOfSelectedPageList();
            }
        }

        public void SavePageList(string pageListName)
        {
            var pageList = DataShop.Instance.g_PageListInfoManager.GetOrCreatePageList(pageListName);

            var existingIds = pageList.PLI_Pages?.ToList() ?? new List<string>();

            if (existingIds.Count > 0)
            {
                DeleteMany(x => existingIds.Contains(x.PIC_GUID));
            }

            if (g_PageInfoClassList.Count > 0)
            {
                InsertMany(g_PageInfoClassList);
            }

            pageList.PLI_Pages = g_PageInfoClassList.Select(x => x.PIC_GUID).ToList();

            if (g_PageInfoClassList.Count > 0)
            {
                string direction = g_PageInfoClassList[0].PIC_IsLandscape
                    ? DeviceOrientation.Landscape.ToString()
                    : DeviceOrientation.Portrait.ToString();

                pageList.PLI_PageDirection = direction;
            }

            DataShop.Instance.g_PageListInfoManager.SavePageList(pageList);
            LoadPageList(pageListName);
        }

        public void DeletePagesByPageName(string pageName)
        {
            if (string.IsNullOrWhiteSpace(pageName))
            {
                return;
            }

            var target = FindOne(x => x.PIC_PageName.Equals(pageName, StringComparison.CurrentCultureIgnoreCase));
            if (target == null)
            {
                return;
            }

            DeleteMany(x => x.PIC_GUID == target.PIC_GUID);

            var pageLists = DataShop.Instance.g_PageListInfoManager.g_PageListInfoClassList;
            if (pageLists == null)
            {
                return;
            }

            foreach (PageListInfoClass list in pageLists)
            {
                if (list.PLI_Pages != null && list.PLI_Pages.Remove(target.PIC_GUID))
                {
                    DataShop.Instance.g_PageListInfoManager.SavePageList(list);
                }
            }
        }

        public PageInfoClass SavePageDefinition(string pageName, PageInfoClass definition)
        {
            if (definition == null || string.IsNullOrWhiteSpace(pageName))
            {
                return null;
            }

            var existing = FindOne(x => x.PIC_PageName.Equals(pageName, StringComparison.CurrentCultureIgnoreCase));

            PageInfoClass clone = new PageInfoClass();
            clone.CopyData(definition);
            clone.PIC_PageName = pageName;
            if (existing != null)
            {
                clone.PIC_GUID = existing.PIC_GUID;
            }
            else if (string.IsNullOrWhiteSpace(clone.PIC_GUID))
            {
                clone.PIC_GUID = Guid.NewGuid().ToString();
            }

            NormalizeRelativePaths(clone);
            Upsert(clone);
            return clone;
        }

        public PageInfoClass GetPageDefinition(string pageName)
        {
            if (string.IsNullOrWhiteSpace(pageName))
            {
                return null;
            }

            PageInfoClass stored = FindOne(x => x.PIC_PageName.Equals(pageName, StringComparison.CurrentCultureIgnoreCase));
            if (stored == null)
            {
                return null;
            }

            PageInfoClass clone = new PageInfoClass();
            clone.CopyData(stored);
            NormalizeRelativePaths(clone);
            return clone;
        }

        public List<PageInfoClass> GetAllSavedPages()
        {
            var documents = LoadAllDocuments();
            if (documents == null || documents.Count == 0)
            {
                return new List<PageInfoClass>();
            }

            var unique = new Dictionary<string, PageInfoClass>(StringComparer.CurrentCultureIgnoreCase);

            foreach (PageInfoClass page in documents)
            {
                if (page == null || string.IsNullOrWhiteSpace(page.PIC_PageName))
                {
                    continue;
                }

                if (unique.TryGetValue(page.PIC_PageName, out PageInfoClass existing))
                {
                    if (string.IsNullOrEmpty(existing.PIC_Thumb) && string.IsNullOrEmpty(page.PIC_Thumb) == false)
                    {
                        unique[page.PIC_PageName] = page;
                    }
                }
                else
                {
                    unique[page.PIC_PageName] = page;
                }
            }

            foreach (PageInfoClass page in unique.Values)
            {
                NormalizeRelativePaths(page);
            }

            return unique.Values
                .OrderBy(x => x.PIC_PageName, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }

        private void NormalizeRelativePaths(PageInfoClass page)
        {
            if (page == null || page.PIC_Elements == null)
            {
                return;
            }
            foreach (ElementInfoClass element in page.PIC_Elements)
            {
                if (element?.EIF_ContentsInfoClassList == null)
                {
                    continue;
                }
                foreach (ContentsInfoClass content in element.EIF_ContentsInfoClassList)
                {
                    content.CIF_RelativePath = $"Contents/{content.CIF_FileName}";
                }
            }
        }
    }

    public class PageInfoClass
    {
        [JsonProperty("id")]
        public string PIC_GUID;
        [JsonIgnore]
        public string Id
        {
            get => PIC_GUID;
            set => PIC_GUID = value;
        }
        public string PIC_PageName = string.Empty;
        public int PIC_PlaytimeHour = 0;
        public int PIC_PlaytimeMinute = 0;
        public int PIC_PlaytimeSecond = 10;
        public int PIC_Volume = 0;
        public bool PIC_IsLandscape = true;
        public int PIC_Rows = 1;
        public int PIC_Columns = 1;
        public double PIC_CanvasWidth = 1920;
        public double PIC_CanvasHeight = 1080;
        public bool PIC_NeedGuide = true;
        public string PIC_Thumb = string.Empty;
        public List<ElementInfoClass> PIC_Elements = new List<ElementInfoClass>();

        public PageInfoClass()
        {
            this.PIC_GUID = Guid.NewGuid().ToString();
        }

        public void CopyData(PageInfoClass paramCls)
        {
            this.PIC_PageName = paramCls.PIC_PageName;
            this.PIC_PlaytimeHour = paramCls.PIC_PlaytimeHour;
            this.PIC_PlaytimeMinute = paramCls.PIC_PlaytimeMinute;
            this.PIC_PlaytimeSecond = paramCls.PIC_PlaytimeSecond;
            this.PIC_Volume = paramCls.PIC_Volume;
            this.PIC_GUID = paramCls.PIC_GUID;
            this.PIC_IsLandscape = paramCls.PIC_IsLandscape;
            this.PIC_Rows = paramCls.PIC_Rows > 0 ? paramCls.PIC_Rows : 1;
            this.PIC_Columns = paramCls.PIC_Columns > 0 ? paramCls.PIC_Columns : 1;
            this.PIC_CanvasWidth = paramCls.PIC_CanvasWidth > 0 ? paramCls.PIC_CanvasWidth : 1920;
            this.PIC_CanvasHeight = paramCls.PIC_CanvasHeight > 0 ? paramCls.PIC_CanvasHeight : 1080;
            this.PIC_NeedGuide = paramCls.PIC_NeedGuide;
            this.PIC_Thumb = paramCls.PIC_Thumb;

            this.PIC_Elements = new List<ElementInfoClass>();
            if (paramCls.PIC_Elements != null && paramCls.PIC_Elements.Count > 0)
            {
                foreach (ElementInfoClass element in paramCls.PIC_Elements)
                {
                    ElementInfoClass cloned = new ElementInfoClass();
                    cloned.CopyData(element);
                    this.PIC_Elements.Add(cloned);
                }
            }
        }

        public void CleanDataField()
        {
            this.PIC_PageName = string.Empty;
            this.PIC_PlaytimeHour = 0;
            this.PIC_PlaytimeMinute = 0;
            this.PIC_PlaytimeSecond = 0;
            this.PIC_Volume = 0;
            this.PIC_GUID = string.Empty;
            this.PIC_IsLandscape = true;
            this.PIC_Rows = 1;
            this.PIC_Columns = 1;
            this.PIC_CanvasWidth = 1920;
            this.PIC_CanvasHeight = 1080;
            this.PIC_NeedGuide = true;
            this.PIC_Thumb = string.Empty;
            this.PIC_Elements.Clear();
        }
    }
}
