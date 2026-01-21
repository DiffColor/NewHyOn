using System;
using System.Collections.Generic;
using System.Windows;
using System.Threading;
using System.IO;
using TurtleTools;

namespace AndoW_Manager
{
    /// <summary>
    /// SavingFileWindow.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class SavingFileWindow : Window
    { 
         List<CopyFileInfo> g_CopyFileList = new List<CopyFileInfo>();
        private readonly Action g_PostCopyAction;

        System.Timers.Timer startCopyTimer = new System.Timers.Timer();

        private delegate void updateCopyedCount(int idx);
        private delegate void CloseThisWindowDelegate();
        private delegate void ChangePictureBoxColorD();

        bool g_IsToggleColor = false;

        //Page1 g_ParentPage = null;
        //string currentSavingPageName = string.Empty;
        public SavingFileWindow()
        {
            InitializeComponent();
        }

        public SavingFileWindow(List<CopyFileInfo> copyFileList, Action postCopyAction = null)
        {
            InitializeComponent();
            //g_ParentPage = paramPage;
            //currentSavingPageName = pageName;
            g_PostCopyAction = postCopyAction;
            InitCopyFileList(copyFileList);

            //label1.Text = string.Format("1 / {0}", g_CopyFileList.Count);

            InitTimer();
        }

        public void InitTimer()
        {
            startCopyTimer.Interval = 500;
            startCopyTimer.Elapsed += new System.Timers.ElapsedEventHandler(startCopyTimer_Elapsed);
            startCopyTimer.Start();
        }


        void startCopyTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            startCopyTimer.Stop();

            //Thread th = new Thread(new ThreadStart(FileCopyToFolder));
            //th.Start();
            FileCopyToFolder();
        }

        public bool HasSameFileLength(string fpath1, string fpath2)
        {
            FileInfo _finfo1 = new FileInfo(fpath1);
            FileInfo _finfo2 = new FileInfo(fpath2);
            if (_finfo2.Exists == false)
                return false;

            return _finfo1.Length == _finfo2.Length;
        }

        public void FileCopyToFolder()
        {
            foreach (CopyFileInfo item in g_CopyFileList)
            {
                if (string.IsNullOrEmpty(item.CFI_FileSourceFullPath) && string.IsNullOrEmpty(item.CFI_FileName))
                    continue;

                try
                {
                    string _source = item.CFI_FileSourceFullPath;
                    string _target = item.CFI_TargetFileName;
                    if (File.Exists(_source))
                    {
                        if (_source.Equals(_target, StringComparison.CurrentCultureIgnoreCase))
                            if (HasSameFileLength(_source, _target))
                                continue;
                    }
                    else if (File.Exists(FNDTools.GetTargetContentsFilePath(item.CFI_FileName)))
                    {
                        _source = FNDTools.GetTargetContentsFilePath(item.CFI_FileName);
                        if (HasSameFileLength(_source, _target))
                            continue;
                    }

                    FileTools.CopyFile(_source, _target);
                }
                catch (Exception ex)
                {
                    continue;
                }               
            }

            startCopyTimer.Stop();
            //BeginInvoke(new CloseThisWindowDelegate(this.closeThisWindow));

            //g_ParentPage.SavePagePreviewImage();
            this.Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.Normal, (Action)(() =>
            {
                try
                {
                    g_PostCopyAction?.Invoke();
                }
                catch { }

                closeThisWindow();
            }));        
        }
        

        public void closeThisWindow()
        {
            this.Close();
        }

        public void InitCopyFileList(List<CopyFileInfo> copyFileList)
        {
            if (copyFileList.Count > 0)
            {
                g_CopyFileList.Clear();

                foreach (CopyFileInfo item in copyFileList)
                {
                    CopyFileInfo tempInfo = new CopyFileInfo();
                    tempInfo.CopyData(item);

                    g_CopyFileList.Add(tempInfo);
                }
            }
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
