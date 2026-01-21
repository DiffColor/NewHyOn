using System;
using System.IO;

namespace HyOnPlayer
{
    class FNDTools
    {
        #region Root Directory Pathes

        public static string GetContentsRootDirPath()
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Contents");
            CreateOrPass(path);
            return path;
        }

        public static string GetLogRootDirPath()
        {
            string path = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "LogData");
            CreateOrPass(path);
            return path;
        }

        public static string GetTempRootDirPath()
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Temp");
            CreateOrPass(path);
            return path;
        }

        public static string GetUpdateRootDirPath()
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Update");
            CreateOrPass(path);
            return path;
        }

        #endregion


        #region Sub Directory Pathes

        public static string GetLogSubDirPath(string yyyymm)
        {
            string path = Path.Combine(GetLogRootDirPath(), yyyymm);
            CreateOrPass(path);
            return path;
        }

        public static string GetLogSubDirPath(string year, string month)
        {
            return string.Format("{0}\\{1}{2}", GetLogRootDirPath(), year, month);
        }

        public static string GetPlayerLogRootDirPath()
        {
            string path = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "PlayerLogData");
            CreateOrPass(path);
            return path;
        }

        public static string GetPlayerLogDirByPlayerName(string pname)
        {
            string path = System.IO.Path.Combine(GetPlayerLogRootDirPath(), pname);
            CreateOrPass(path);
            return path;
        }

        public static string GetPlayerLogDirBySubFolder(string pname, string subfolder)
        {
            string path = System.IO.Path.Combine(GetPlayerLogDirByPlayerName(pname), subfolder);
            CreateOrPass(path);
            return path;
        }

        private static string GetTempContentsFolder()
        {
            string path = Path.Combine(FNDTools.GetTempRootDirPath(), "Contents");
            CreateOrPass(path);
            return path;
        }

        #endregion


        #region File Pathes

        public static string GetLogDataFilePath(string folderpath, string paramLogfileName)
        {
            return Path.Combine(GetLogSubDirPath(folderpath), paramLogfileName);
        }

        public static string GetContentsFilePath(string fname)
        {
            return Path.Combine(GetContentsRootDirPath(), fname);
        }

        public static string GetUSBRootFilePath(string drive, string filename)
        {
            return string.Format("{0}\\{1}", drive, filename);
        }

        public static string GetPlayersLogDataFilePath(string pname, string subfolder, string filename)
        {
            return Path.Combine(GetPlayerLogDirBySubFolder(pname, subfolder), filename);
        }

        public static string GetXmlFilename(string path, string filename)
        {
            string xmlname = filename;

            if (string.IsNullOrEmpty(Path.GetExtension(xmlname)))
                xmlname = string.Format("{0}.xml", filename);

            return Path.Combine(path, xmlname);
        }

        public static string GetTempFilePath(string filename)
        {
            return Path.Combine(GetTempRootDirPath(), filename);
        }

        public static string GetTempContentFilePath(string fname)
        {
            return Path.Combine(GetTempContentsFolder(), fname);
        }

        #endregion


        #region Exe File Pathes

        public static string GetPlayerProcName()
        {
            return "HyOn Player";
        }
        public static string GetAgentProcName()
        {
            return "HyOn Agent";
        }
        public static string GetPCSProcName()
        {
            return "PCScheduler";
        }
        public static string GetManagerProcName()
        {
            return "HyonMessageServer";
        }

        public static string GetPlayerExeFilePath()
        {
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, string.Format("{0}.exe", GetPlayerProcName()));
        }

        public static string GetAgentExeFilePath()
        {
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, string.Format("{0}.exe", GetAgentProcName()));
        }

        public static string GetManagerExeFilePath()
        {
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, string.Format("{0}.exe", GetManagerProcName()));
        }

        public static string GetPCSchedulerExeFilePath()
        {
            return System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, string.Format("{0}.exe", GetPCSProcName()));
        }


        public static string GetUpdatedPlayerExeFilePath()
        {
            return Path.Combine(GetUpdateRootDirPath(), string.Format("{0}.exe", GetPlayerProcName()));
        }

        #endregion


        public static void CreateOrPass(string path)
        {
            try
            {
                if (Directory.Exists(path))
                    return;

                Directory.CreateDirectory(path);
            }
            catch (Exception ex) { }
        }
    }
}
