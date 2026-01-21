using System;
using System.Collections.Generic;
using System.Linq;
using LiteDB;
using Newtonsoft.Json;

namespace AndoW.Shared
{
    public class PageListInfoClass
    {
        [BsonId]
        [BsonField("id")]
        [JsonProperty("id")]
        public string Id { get; set; }

        public string PLI_PageListName { get; set; } = string.Empty;
        public string PLI_CreateTimeStr { get; set; } = string.Empty;
        public string PLI_PageDirection { get; set; } = string.Empty;
        public List<string> PLI_Pages { get; set; } = new List<string>();

        public PageListInfoClass()
        {
            PLI_CreateTimeStr = DateTime.Now.ToShortDateString();
        }

        public void CopyData(PageListInfoClass paramCls)
        {
            if (paramCls == null) return;

            PLI_PageListName = paramCls.PLI_PageListName;
            PLI_CreateTimeStr = paramCls.PLI_CreateTimeStr;
            PLI_PageDirection = paramCls.PLI_PageDirection;
            PLI_Pages = paramCls.PLI_Pages?.ToList() ?? new List<string>();
            Id = paramCls.Id;
        }
    }
}
