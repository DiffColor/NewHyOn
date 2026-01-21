using System;
using System.IO;

namespace PageViewer
{
    public class FNDTools
    {
        #region Root Directory Pathes

        public static string GetDataDirPath()
        {
            string path = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data");
            CreateOrPass(path);
            return path;
        }

        public static string GetContentsRootDirPath()
        {
            string path = Path.Combine(GetDataDirPath(), "Contents");
            CreateOrPass(path);
            return path;
        }

        public static string GetPagesRootDirPath()
        {
            string path = Path.Combine(GetDataDirPath(), "Pages");
            CreateOrPass(path);
            return path;
        }

        public static string GetContentsFilePath(string paramFileName)
        {
            return Path.Combine(GetContentsRootDirPath(), paramFileName);
        }

        #endregion


        #region Sub Directory Pathes

        #endregion


        #region File Pathes

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
        #endregion


        #region Exe File Pathes

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
