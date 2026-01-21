using System;
using System.IO;

namespace ConfigPlayer
{
    public enum ContentType { None, Video, Image, Browser, Flash, PPT, HDTV, IPTV, WebSiteURL, PDF }

    class FNDTools
    {
        #region Root Directory Pathes

        public static string GetDataRootDirPath()
        {
            string path = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data");
            CreateOrPass(path);
            return path;
        }

        public static string GetPagesRootDirPath()
        {
            string path = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Pages");
            CreateOrPass(path);
            return path;
        }

        public static string GetContentsRootDirPath()
        {
            string path = Path.Combine(GetPagesRootDirPath(), "Contents");
            CreateOrPass(path);
            return path;
        }

        public static string GetLogRootDirPath()
        {
            string path = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "LogData");
            CreateOrPass(path);
            return path;
        }

        public static string GetPlayerInfoRootDirPath()
        {
            string path = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "PlayerInfo");
            CreateOrPass(path);
            return path;
        }

        public static string GetPlayListRootDirPath()
        {
            string path = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "PlayList");
            CreateOrPass(path);
            return path;
        }

        public static string GetPageListRootDirPath()
        {
            string path = Path.Combine(GetDataRootDirPath(), "PageList");
            CreateOrPass(path);
            return path;
        }
        public static string GetTempRootDirPath()
        {
            string path = Path.Combine(GetContentsRootDirPath(), "Temp");
            CreateOrPass(path);
            return path;
        }

        public static string GetDownloadsRootDirPath()
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Downloads");
            CreateOrPass(path);
            return path;
        }

        public static string GetEmergencyRootDirPath()
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Emergency");
            CreateOrPass(path);
            return path;
        }

        public static string GetUpdateRootDirPath()
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Update");
            CreateOrPass(path);
            return path;
        }

        public static string GetShopPDDataRootDirPath()
        {
            string dataPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ShopPDData");
            CreateOrPass(dataPath);
            return dataPath;
        }

        public static string GetCefCacheRootDirPath()
        {
            string dataPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CefCache");
            CreateOrPass(dataPath);
            return dataPath;
        }
        #endregion


        #region Sub Directory Pathes

        public static string GetActionTypeDirPath(string actionType)
        {
            string path = string.Empty;

            switch (actionType)
            {
                case "FileDownload":
                    path = GetDownloadsRootDirPath();
                    break;

                case "EmergencyDisplay":
                    path = GetEmergencyRootDirPath();
                    break;

                case "Update":
                    path = GetUpdateRootDirPath();
                    break;

                default:
                    break;
            }

            return path;
        }

        public static string GetSpecialScheduleRootDirPath()
        {
            string path = Path.Combine(GetDataRootDirPath(), "SpecialSchedule");
            CreateOrPass(path);
            return path;
        }

        public static string GetPageDirPathByPageName(string pageName)
        {
            string path = Path.Combine(GetPagesRootDirPath(), pageName);
            CreateOrPass(path);
            return path;
        }

        public static string GetPlayerInfoDirPath(string playeName)
        {
            string path = Path.Combine(GetPlayerInfoRootDirPath(), playeName);
            CreateOrPass(path);
            return path;
        }

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

        public static string GetShopPDSavedPageDirPath()
        {
            string dataPath = System.IO.Path.Combine(GetShopPDDataRootDirPath(), "SavedPages");
            CreateOrPass(dataPath);
            return dataPath;
        }


        public static string GetShopPDPreviewImageDirPath()
        {
            string path = string.Format("{0}\\SavedPreviewImages", GetShopPDDataRootDirPath());
            CreateOrPass(path);
            return path;
        }
        public static string GetHyOnUSBRootPath(string usbname)
        {
            string path = string.Format(@"{0}:\{1}", usbname, @"MultiHyOn USB");
            CreateOrPass(path);
            return path;
        }

        public static string GetUSBDataPath(string usbname)
        {
            //string path = Path.Combine(GetHyOnUSBRootPath(usbname), "Data");
            string path = Path.Combine(GetUSBPagesPath(usbname), "TurtleData");
            CreateOrPass(path);
            return path;

        }

        public static string GetUSBPagesPath(string usbname)
        {
            string path = Path.Combine(GetHyOnUSBRootPath(usbname), "Pages");
            CreateOrPass(path);
            return path;

        }

        public static string GetUSBContentRootPath(string usbname)
        {
            string path = Path.Combine(GetUSBPagesPath(usbname), "Contents");
            CreateOrPass(path);
            return path;
        }

        public static string GetUSBPageListPath(string usbname)
        {
            string path = Path.Combine(GetUSBDataPath(usbname), "PageList");
            CreateOrPass(path);
            return path;
        }


        public static string GetWeeklyInfoRootDirPath()
        {
            string path = Path.Combine(GetDataRootDirPath(), "WeeklyInfo");
            CreateOrPass(path);
            return path;
        }

        public static string GetPlayerConfignInfoDirPath()
        {
            string path = System.IO.Path.Combine(GetDataRootDirPath(), "PlayerConfiguration");
            CreateOrPass(path);
            return path;
        }

        public static string GetWelcomeBoardImagePathByFileName(string paramFileName)
        {
            return Path.Combine(GetShopPDSavedPageDirPath(), paramFileName);
        }

        public static string GetWelcomePageDirPathByPageName(string pageName)
        {
            return System.IO.Path.Combine(GetShopPDSavedPageDirPath(), pageName);
        }

        public static string GetOptimizedImageDirPath()
        {
            string path = Path.Combine(GetContentsRootDirPath(), "Optimized");
            CreateOrPass(path);
            return path;
        }
        public static string GetActionTypeDirPath(string paramFileName, string actionType)
        {
            string filePath = string.Empty;

            switch (actionType)
            {
                case "FileDownload":
                    filePath = string.Format("{0}\\{1}", GetDownloadsRootDirPath(), paramFileName);
                    break;

                case "EmergencyDisplay":
                    filePath = string.Format("{0}\\{1}", GetEmergencyRootDirPath(), paramFileName);
                    break;

                case "Update":
                    filePath = string.Format("{0}\\{1}", GetUpdateRootDirPath(), paramFileName);
                    break;

                default:
                    break;
            }

            return filePath;
        }
        #endregion


        #region File Pathes

        public static string GetPortInfoFilePath()
        {
            return Path.Combine(GetDataRootDirPath(), "PortInfo.xml");
        }
        public static string GetScheduleInfoFilePath()
        {
            return Path.Combine(GetDataRootDirPath(), "ScheduleInfo.xml");
        }

        public static string GetContentsListInfoFileFullPath(string pageName, string displayElementName)
        {
            return string.Format("{0}\\{1}.xml", GetPageDirPathByPageName(pageName), displayElementName);
        }

        public static string GetLogDataFilePath(string folderpath, string paramLogfileName)
        {
            return Path.Combine(GetLogSubDirPath(folderpath), paramLogfileName);
        }

        public static string GetTimeTableInfoFilePath()
        {
            return Path.Combine(GetDataRootDirPath(), "TimeTableInfo.xml");
        }

        public static string GetPlayerConfigInfoFilePath()
        {
            return Path.Combine(GetDataRootDirPath(), "PlayerConfiguration.xml");
        }

        public static string GetContentsListFilePathOfElement(string pageName, string elementName)
        {
            return string.Format("{0}\\{1}.xml", GetPageDirPathByPageName(pageName), elementName);
        }

        public static string GetTargetContentsFilePath(string paramFileName)
        {
            return Path.Combine(GetContentsRootDirPath(), paramFileName);
        }

        public static string GetContentSrcFileFullPath(string pageName, string contentFileName)
        {
            return Path.Combine(GetPageDirPathByPageName(pageName), contentFileName);
        }

        public static string GetEmergencyDataFilePath()
        {
            return Path.Combine(GetEmergencyRootDirPath(), "EmergencyFileName.xml");
        }

        public static string GetZorderInfoFilePathByPageName(string pageName)
        {
            return Path.Combine(GetPageDirPathByPageName(pageName), "PageZorderInfo.xml");
        }

        public static string GetPageEtcInfoFilePathByPageName(string pageName)
        {
            return string.Format("{0}\\{1}\\PageEtcInfo.xml", GetPagesRootDirPath(), pageName);
        }

        public static string GetElementInfoFilePathByPageName(string pageName)
        {
            return string.Format("{0}\\{1}\\{2}.xml", GetPagesRootDirPath(), pageName, pageName);
        }

        public static string GetPlayListFileFullPathToSave(string playListFileName)
        {
            return string.Format("{0}\\{1}", GetPlayListRootDirPath(), playListFileName);
        }

        public static string GetPlayListFileFullPathForLoad(string playListName)
        {
            return string.Format("{0}\\{1}.xml", GetPlayListRootDirPath(), playListName);
        }

        public static string GetPlayerInfoDataFileFullPath()
        {
            return Path.Combine(GetPlayerInfoRootDirPath(), "Local_Default_Player.xml");
        }

        public static string GetPlayerInfoSchDataFileFullPath(string dataFileName)
        {
            return string.Format("{0}\\{1}", GetPlayerInfoRootDirPath(), dataFileName);
        }

        public static string GetPlayerWPSDataFileFullPath(string playerName)
        {
            return string.Format("{0}\\{1}_WeeklySch.xml", GetPlayerInfoRootDirPath(), playerName);
        }

        public static string GetPlayerMPSDataFileFullPath(string playerName)
        {
            return string.Format("{0}\\{1}_MonthlySch.xml", GetPlayerInfoRootDirPath(), playerName);
        }

        public static string GetUSBRootFilePath(string drive, string filename)
        {
            return string.Format("{0}\\{1}", drive, filename);
        }

        public static string GetApplicationInfoFilePath()
        {
            return string.Format("{0}\\ApplicationInfo.xml", GetDataRootDirPath());
        }


        public static string GetResolutionInfoFilePath()
        {
            return string.Format("{0}\\ResolutionInfo.xml", GetDataRootDirPath());
        }

        public static string GetPlayerInfoDataFileFullPath(string playeName)
        {
            return string.Format("{0}\\{1}\\{2}.xml", GetPlayerInfoRootDirPath(), playeName, playeName);
        }

        public static string GetTTPlayerInformationFilePath()
        {
            return string.Format("{0}\\TTPlayerInformation.xml", GetDataRootDirPath());
        }

        public static string GetPageBasicInfoFilePathByPageName(string pageName)
        {
            return string.Format("{0}\\{1}\\PageBasicInfo.xml", GetPagesRootDirPath(), pageName);
        }

        public static string GetPageListFilePath(string paramPageName)
        {
            return string.Format("{0}\\{1}.xml", GetPageListRootDirPath(), paramPageName);
        }

        public static string GetPageListInfoFilePath()
        {
            return Path.Combine(GetDataRootDirPath(), "PageListInfo.xml");
        }

        public static string GetPlayerInfoFilePath()
        {
            return Path.Combine(GetDataRootDirPath(), "PlayerInfo.xml");
        }

        public static string GetPlayersLogDataFilePath(string pname, string subfolder, string filename)
        {
            return Path.Combine(GetPlayerLogDirBySubFolder(pname, subfolder), filename);
        }

        public static string GetPreviewImageFilePathByPageName(string pageName)
        {
            return string.Format("{0}\\{1}.png", GetPagesRootDirPath(), pageName);
        }

        public static string GetSpecialScheduleFilePathByPlayerName(string pname)
        {
            return string.Format("{0}\\{1}.xml", GetSpecialScheduleRootDirPath(), pname);
        }

        public static string GetTTManagerFunctionInfoFilePath()
        {
            return Path.Combine(GetDataRootDirPath(), "TTManagerFunctionInfo.xml");
        }

        public static string GetXmlFilename(string path, string filename)
        {
            string xmlname = filename;

            if (string.IsNullOrEmpty(Path.GetExtension(xmlname)))
                xmlname = string.Format("{0}.xml", filename);

            return Path.Combine(path, xmlname);
        }

        public static string GetUserInfoFilePath()
        {
            return Path.Combine(GetDataRootDirPath(), "UserInfo.xml");
        }

        public static string GetWeeklyInfoFilePathByPlayerName(string paramPlayerName)
        {
            return string.Format("{0}\\{1}.xml", GetWeeklyInfoRootDirPath(), paramPlayerName);
        }

        public static string GetPlayerConfigInfoFilePath(string paramPlayerName)
        {
            return string.Format("{0}\\{1}.xml", GetPlayerConfignInfoDirPath(), paramPlayerName);
        }

        public static string GetWelcomePageImgPathByPageName(string pageName)
        {
            return string.Format("{0}\\{1}.png", GetWelcomePageDirPathByPageName(pageName), pageName);
        }

        public static string GetDefaultPlaylistNameFilePath()
        {
            return Path.Combine(GetDataRootDirPath(), "DefaultPlaylistName.txt");
        }

        public static string GetTempFilePath(string filename)
        {
            return Path.Combine(GetTempRootDirPath(), filename);
        }

        public static string GetDeviceInfoFilePath()
        {
            return Path.Combine(GetDataRootDirPath(), "DeviceInfo.xml");
        }

        public static string GetEmergencyScrollTextDataFilePath()
        {
            return Path.Combine(GetDataRootDirPath(), "EmergencyScrollText.txt");
        }

        public static string GetTempImageCacheFilePath()
        {
            return Path.Combine(GetContentsRootDirPath(), "ImageCache.xml");
        }

        public static string GetWelcomeBgPath(string pageName, string filename)
        {
            return System.IO.Path.Combine(GetPageDirPathByPageName(pageName), filename);
        }

        public static string GetOptimizedImageFilePath(string filname)
        {
            return Path.Combine(GetOptimizedImageDirPath(), filname);
        }

        public static string GetEmergencyImagePath(string FileName)
        {
            return Path.Combine(GetEmergencyRootDirPath(), FileName);
        }

        public static string GetAuthInfoFilePath()
        {
            return Path.Combine(GetDataRootDirPath(), "AuthInfo.xml");
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
        public static string GetShopPDProcName()
        {
            return "ShopPDTest";
        }
        public static string GetPCSProcName()
        {
            return "PCScheduler";
        }
        public static string GetEmergScrollProcName()
        {
            return "EmergencyScrollText";
        }
        public static string GetManagerProcName()
        {
            return "HyonMessageServer";
        }
        public static string GetPPTViewerProcName()
        {
            return "PPTVIEW";
        }


        public static string GetVNCServerExeFilePath()
        {
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "winvnc.exe");
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

        public static string GetVNCViewerFilePath()
        {
            return System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "vncviewer.exe");
        }

        public static string GetWelcomeBoardExeFilePath()
        {
            return System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, string.Format("{0}.exe", GetShopPDProcName()));
        }

        public static string GetEmergencyScrollTextExeFilePath()
        {
            return System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, string.Format("{0}.exe", GetEmergScrollProcName()));
        }

        public static string GetPCSchedulerExeFilePath()
        {
            return System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, string.Format("{0}.exe", GetPCSProcName()));
        }


        public static string GetUpdatedPlayerExeFilePath()
        {
            return Path.Combine(GetUpdateRootDirPath(), string.Format("{0}.exe", GetPlayerProcName()));
        }

        public static string GetPPTViewerPath()
        {
            // Viewer 2007 in 64bit Windows:
            string path = "C:\\Program Files (x86)\\Microsoft Office\\Office12\\PPTVIEW.EXE";
            if (File.Exists(path))
                return path;

            // Viewer 2007 in 32bit Windows:
            path = "C:\\Program Files\\Microsoft Office\\Office12\\PPTVIEW.EXE";
            if (File.Exists(path))
                return path;

            // Viewer 2010 in 64bit Windows:
            path = "C:\\Program Files (x86)\\Microsoft Office\\Office14\\PPTVIEW.EXE";
            if (File.Exists(path))
                return path;

            // Viewer 2010 in 32bit Windows:
            path = "C:\\Program Files\\Microsoft Office\\Office14\\PPTVIEW.EXE";
            if (File.Exists(path))
                return path;

            // Give them the opportunity to place it in the same folder as the app
            path = Directory.GetCurrentDirectory() + "\\pptview\\PPTVIEW.EXE";
            if (File.Exists(path))
                return path;

            return null;
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
