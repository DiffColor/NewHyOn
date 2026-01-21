using System;
using System.Collections.Generic;
using AndoW.Shared;
using HyOnPlayer;

namespace HyOnPlayer.DataManager
{
    public class TextInfoManager
    {
        private readonly TextRepository repository = new TextRepository();

        public List<TextInfoClass> g_DataClassList = new List<TextInfoClass>();

        public TextInfoManager()
        {
        }

        public void LoadData(string paramPageName, string dataFileName)
        {
            var results = repository.Find(x => x.CIF_PageName == paramPageName && x.CIF_DataFileName == dataFileName);
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

        private static TextInfoClass CreateDefaultTextInfo(string pageName, string dataFileName)
        {
            return new TextInfoClass
            {
                CIF_PageName = pageName,
                CIF_DataFileName = dataFileName
            };
        }

    }
}
