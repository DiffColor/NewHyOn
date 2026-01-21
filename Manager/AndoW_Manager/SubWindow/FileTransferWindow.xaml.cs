using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using HyonManager.Pages;
using HyonManager.SubWindow;
using HyonManager.DataClass;
using System.Windows.Threading;
using System.IO;

namespace HyonManager.SubWindow
{
    /// <summary>
    /// NewPlayerWindow.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class FileTransferWindow : Window
    {
        MainWindow g_ParentPage = null;

        public IFileRepositoryService pipeProxy = null;
        List<CopyFileInfo> g_CopyFileList = new List<CopyFileInfo>();
        public bool g_IsPlayerTypeLand = true;

        DispatcherTimer fileTransferTimer = new DispatcherTimer();

        public FileTransferWindow(MainWindow paramPage, List<CopyFileInfo> paramList, IFileRepositoryService paramProxy)
        {
            InitializeComponent();
            g_ParentPage = paramPage;
            InitEventHandler();
            pipeProxy = paramProxy;

            g_CopyFileList.Clear();
            if (paramList.Count > 0)
            {
                foreach (CopyFileInfo item in paramList)
                {
                    CopyFileInfo tmpCls = new CopyFileInfo();
                    tmpCls.CopyData(item);
                    g_CopyFileList.Add(tmpCls);
                }
            }

            ShowClockSwf();

            this.Loaded += FileTransferWindow_Loaded;
        }

        public void ShowClockSwf()
        {
            string filePath = UtilityClass.GetClockFlashFilePath();
            string htmlCode = string.Format("<!-- saved from url=(0014)about:internet -->" +
                                            "<html xmlns=\"http://www.w3.org/1999/xhtml\" xml:lang=\"en\" lang=\"en\">" +
                                            "<embed src=\"file:///{0}\" quality=\"high\" scale=\"noborder\" bgcolor=\"#000000\" name=\"Video\" align=\"middle\" width=\"100%\" height=\"100%\" allowfullscreen=\"true\" SCALE=\"exactfit\" allowScriptAccess=\"sameDomain\" type=\"application/x-shockwave-flash\" /></html>", filePath);

            WebBrowser1.NavigateToString(htmlCode);
        }

        void FileTransferWindow_Loaded(object sender, RoutedEventArgs e)
        {

            fileTransferTimer.Interval = new TimeSpan(0, 0, 2);
            fileTransferTimer.Tick += fileTransferTimer_Tick;
            fileTransferTimer.Start();

        }

        void fileTransferTimer_Tick(object sender, EventArgs e)
        {
            fileTransferTimer.Stop();
            TransferFileToPlayer();
            this.Close();
        }

        public void TransferFileToPlayer()
        {
            if (g_CopyFileList.Count > 0)
            {
                foreach (CopyFileInfo item in g_CopyFileList)
                {
                    if (new FileInfo(item.CFI_FileSourceFullPath).Exists == true)
                    {
                        Stream uploadStream = new FileStream(item.CFI_FileSourceFullPath, FileMode.Open);
                        FileUploadMessage tmpFileMsg = new FileUploadMessage();
                        tmpFileMsg.pageName = item.CFI_PageName;
                        tmpFileMsg.fileName = item.CFI_FileName;
                        tmpFileMsg.DataStream = uploadStream;

                        if (pipeProxy != null)
                        {
                            string fileExtension = new System.IO.FileInfo(item.CFI_FileSourceFullPath).Extension.ToLowerInvariant();

                            if (fileExtension == ".xml")  // 데이터 파일은 무조건 전송하게한다.
                            {
                                pipeProxy.PutFile(tmpFileMsg);
                            }
                            else
                            {
                                if (pipeProxy.CheckIsExistFile(item.CFI_FileName, item.CFI_PageName) == false)
                                {
                                    pipeProxy.PutFile(tmpFileMsg);
                                }
                            }
                        }

                        uploadStream.Close();
                    }
                }
            }
        }

        public void InitEventHandler()
        {

        }

        void PageNavigator2_Click(object sender, RoutedEventArgs e)
        {
            //this.Close();
        }
            

        private void BtnWin_close_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            this.Close();
        }

        private void BtnWin_drag_PreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            this.DragMove();
        }
    }
}
