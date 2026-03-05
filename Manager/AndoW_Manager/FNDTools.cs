using System;
using System.IO;

namespace AndoW_Manager
{
    class FNDTools
    {
        #region Root Directory Pathes

        public static string GetContentsDirPath()
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Contents");
            CreateOrPass(path);
            return path;
        }

        public static string GetDataDirPath()
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data");
            CreateOrPass(path);
            return path;
        }

        public static string GetFontsTargetFolderPath()
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Fonts");
            CreateOrPass(path);
            return path;
        }

        public static string GetUpgradeAPKFTPDirPath()
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "UpgradeAPK");
            CreateOrPass(path);
            return path;
        }

        public static string GetLogRootDirPath()
        {
            string path = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "LogData");
            CreateOrPass(path);
            return path;
        }

        public static string GetPlayerLogRootDirPath()
        {
            string path = System.IO.Path.Combine(GetDataDirPath(), "PlayerLogData");
            CreateOrPass(path);
            return path;
        }

        public static string GetCefCacheRootDirPath()
        {
            string dataPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CefCache");
            CreateOrPass(dataPath);
            return dataPath;
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

        public static string GetUSBRootPath(string usbname)
        {
            string path = string.Format(@"{0}:\{1}", usbname, @"AndoW_USB");
            CreateOrPass(path);
            return path;
        }
                
        public static string GetUSBContentPath(string usbname)
        {
            string path = Path.Combine(GetUSBRootPath(usbname), "Contents");
            CreateOrPass(path);
            return path;
        }

        public static string GetUSBAuthKeyPath(string usbname)
        {
            return Path.Combine(GetUSBRootPath(usbname), "AuthKeys");
        }

        public static string GetOptimizedImageDirPath()
        {
            string path = Path.Combine(GetContentsDirPath(), "Optimized");
            CreateOrPass(path);
            return path;
        }

        public static string GetGroupLogosDirPath()
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "GroupLogos");
            CreateOrPass(path);
            return path;
        }

        public static string GetRustDeskFilePath()
        {
            return System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "rustdesk.exe");
        }
        #endregion


        #region File Pathes

        public static string GetLogDataFilePath(string folderpath, string paramLogfileName)
        {
            return Path.Combine(GetLogSubDirPath(folderpath), paramLogfileName);
        }

        public static string GetTargetContentsFilePath(string paramFileName)
        {
            return Path.Combine(GetContentsDirPath(), paramFileName);
        }

        public static string GetContentFilePath(ContentsInfoClass content)
        {
            if (content == null)
            {
                return string.Empty;
            }

            if (string.IsNullOrWhiteSpace(content.CIF_FileName) == false)
            {
                string candidate = GetTargetContentsFilePath(content.CIF_FileName);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }

            if (string.IsNullOrWhiteSpace(content.CIF_FileFullPath) == false && File.Exists(content.CIF_FileFullPath))
            {
                return content.CIF_FileFullPath;
            }

            return string.Empty;
        }

        public static string GetWelcomeBoardBackgroundPath(TextInfoClass textInfo)
        {
            if (textInfo == null)
            {
                return string.Empty;
            }

            if (string.IsNullOrWhiteSpace(textInfo.CIF_BGImageFileName) == false)
            {
                string candidate = GetTargetContentsFilePath(textInfo.CIF_BGImageFileName);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }

            if (string.IsNullOrWhiteSpace(textInfo.CIF_BGImageFileFullPath) == false && File.Exists(textInfo.CIF_BGImageFileFullPath))
            {
                return textInfo.CIF_BGImageFileFullPath;
            }

            return string.Empty;
        }

        public static string GetPlayersLogDataFilePath(string pname, string subfolder, string filename)
        {
            return Path.Combine(GetPlayerLogDirBySubFolder(pname, subfolder), filename);
        }

        public static string GetAuthKeyPath()
        {
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "AuthKeys");
        }

        public static string GetPreviewCanvasFilePath()
        {
            return Path.Combine(GetDataDirPath(), "PreviewCanvas.xml");
        }

        public static string GetPreviewDataFilePath()
        {
            return Path.Combine(GetDataDirPath(), "PreviewData.xml");
        }

        public static string GetPreviewThumbFilePath()
        {
            return Path.Combine(GetDataDirPath(), "thumb.png");
        }

        public static string GetLocalDbPath()
        {
            return Path.Combine(GetDataDirPath(), "local.db");
        }
        #endregion


        #region Exe File Pathes

        public static string GetManagerProcName()
        {
            return "AndoW Manager";
        }

        public static string GetHyOnManagerProcName()
        {
            return "HyonMessageServer";
        }

        public static string GetContentViewerProcName()
        {
            return "ContentViewer";
        }

        public static string GetPageViewerProcName()
        {
            return "PageViewer";
        }

        public static string GetManagerExeFilePath()
        {
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, string.Format("{0}.exe", GetManagerProcName()));
        }

        public static string GetContentViewerPath()
        {
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"{GetContentViewerProcName()}.exe");
        }

        public static string GetPageViewerPath()
        {
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"{GetPageViewerProcName()}.exe");
        }

        public static string GetUpgradeAPKFTPFilePath()
        {
            return Path.Combine(GetUpgradeAPKFTPDirPath(), "AndoW_Player.apk");
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
