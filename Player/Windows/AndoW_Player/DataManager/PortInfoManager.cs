using AndoW.LiteDb;
using LiteDB;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using TurtleTools;

namespace HyOnPlayer
{
    public class PortInfoManager
    {
        private readonly PortInfoRepository repository = new PortInfoRepository();
        public List<PortInfoClass> g_DataClassList = new List<PortInfoClass>();

        public PortInfoManager()
        {
            try
            {
                LoadData();
            }
            catch (Exception ex)
            {
                Logger.WriteLog(ex.ToString(), Logger.GetLogFileName());
            }
        }

        public void LoadData()
        {
            g_DataClassList.Clear();

            List<PortInfoClass> stored = repository.LoadAll();
            if (stored.Count > 0)
            {
                g_DataClassList.AddRange(stored);
            }

            if (g_DataClassList.Count == 0)
            {
                PortInfoClass tmpCls = new PortInfoClass();
                AddDataInfo(tmpCls);
                return;
            }

            bool needsSave = false;
            foreach (var item in g_DataClassList)
            {
                if (item.AIF_SYNC <= 0)
                {
                    item.AIF_SYNC = NetworkTools.SYNC_PORT;
                    needsSave = true;
                }
            }

            if (needsSave)
            {
                SaveData();
            }
        }

        public void AddDataInfo(PortInfoClass paramCls)
        {
            PortInfoClass tmpCls = new PortInfoClass();
            tmpCls.CopyData(paramCls);

            g_DataClassList.Clear();
            g_DataClassList.Add(tmpCls);
            SaveData();
        }

        public void SaveData()
        {
            if (g_DataClassList.Count == 0)
            {
                return;
            }

            foreach (var item in g_DataClassList)
            {
                item.Id = 0;
            }
            repository.ReplaceAll(g_DataClassList);
        }

        private class PortInfoRepository : LiteDbRepository<PortInfoClass>
        {
            public PortInfoRepository()
                : base("PortInfoManager", "Id")
            {
            }
        }
    }

    public class PortInfoClass
    {
        [BsonId]
        [BsonField("id")]
        [JsonProperty("id")]
        public int Id { get; set; } = 0; // 한개의 데이터만 저장하기 위한 필드

        public int AIF_AgentSVCPort { get; set; } = NetworkTools.AGENT_PORT;
        public int AIF_OperaterSVCPort { get; set; } = NetworkTools.OPERATOR_PORT;
        public int AIF_FTP { get; set; } = NetworkTools.FTP_PORT;
        public int AIF_FTP_PasvMinPort { get; set; } = NetworkTools.FTP_PASV_MIN_PORT;
        public int AIF_FTP_PasvMaxPort { get; set; } = NetworkTools.FTP_PASV_MAX_PORT;
        public int AIF_SYNC { get; set; } = NetworkTools.SYNC_PORT;

        public void CopyData(PortInfoClass paramCls)
        {
            AIF_AgentSVCPort = paramCls.AIF_AgentSVCPort;
            AIF_OperaterSVCPort = paramCls.AIF_OperaterSVCPort;
            AIF_FTP = paramCls.AIF_FTP;
            AIF_FTP_PasvMinPort = paramCls.AIF_FTP_PasvMinPort;
            AIF_FTP_PasvMaxPort = paramCls.AIF_FTP_PasvMaxPort;
            AIF_SYNC = paramCls.AIF_SYNC;
        }

        public void CleanDataField()
        {
            AIF_AgentSVCPort = NetworkTools.AGENT_PORT;
            AIF_OperaterSVCPort = NetworkTools.OPERATOR_PORT;
            AIF_FTP = NetworkTools.FTP_PORT;
            AIF_FTP_PasvMinPort = NetworkTools.FTP_PASV_MIN_PORT;
            AIF_FTP_PasvMaxPort = NetworkTools.FTP_PASV_MAX_PORT;
            AIF_SYNC = NetworkTools.SYNC_PORT;
        }
    }
}
