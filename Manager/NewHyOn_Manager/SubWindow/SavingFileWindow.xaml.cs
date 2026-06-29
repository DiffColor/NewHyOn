using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using TurtleTools;

namespace AndoW_Manager
{
    /// <summary>
    /// SavingFileWindow.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class SavingFileWindow : Window
    {
        private readonly List<CopyFileInfo> g_CopyFileList = new List<CopyFileInfo>();
        private readonly Action g_PostCopyAction;
        private readonly bool g_EnableFtpUpload;
        private readonly bool g_EnableFtpDownloadMissing;
        private readonly System.Timers.Timer startCopyTimer = new System.Timers.Timer();
        private readonly CancellationTokenSource uploadCancellation = new CancellationTokenSource();
        private readonly List<string> copyErrors = new List<string>();

        private sealed class UploadJob
        {
            public string LocalPath { get; set; }
            public string RemoteRelativePath { get; set; }
            public string DisplayName { get; set; }
            public long SizeBytes { get; set; }
            public bool ForceUpload { get; set; }
        }

        public SavingFileWindow()
        {
            InitializeComponent();
        }

        public SavingFileWindow(List<CopyFileInfo> copyFileList,
            Action postCopyAction = null,
            bool enableFtpUpload = false,
            bool enableFtpDownloadMissing = false)
        {
            InitializeComponent();
            g_PostCopyAction = postCopyAction;
            g_EnableFtpUpload = enableFtpUpload;
            g_EnableFtpDownloadMissing = enableFtpDownloadMissing;
            InitCopyFileList(copyFileList);
            InitializeUiState();
            InitTimer();
        }

        private void InitializeUiState()
        {
            if (UploadSection != null)
            {
                UploadSection.Visibility = g_EnableFtpUpload ? Visibility.Visible : Visibility.Collapsed;
            }

            string subtitle = g_EnableFtpUpload
                ? "로컬 보존과 서버 전송을 함께 진행합니다."
                : "파일 복사를 진행합니다.";

            if (WindowTitleText != null)
            {
                WindowTitleText.Text = g_EnableFtpUpload ? "콘텐츠 저장" : "파일 저장";
            }

            string title = g_EnableFtpUpload ? "화면구성을 저장 중입니다." : "파일을 저장 중입니다.";
            SetStage(title, subtitle);
            UpdateCopyProgress(0, Math.Max(0, g_CopyFileList.Count), string.Empty);
            UpdateUploadProgress(0, 0, string.Empty, 0, 0);
        }

        public void InitTimer()
        {
            startCopyTimer.Interval = 200;
            startCopyTimer.Elapsed += new System.Timers.ElapsedEventHandler(startCopyTimer_Elapsed);
            startCopyTimer.Start();
        }

        private void startCopyTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            startCopyTimer.Stop();
            _ = RunCopyAndUploadAsync();
        }

        private async Task RunCopyAndUploadAsync()
        {
            bool succeeded = false;
            try
            {
                await CopyFilesWithProgressAsync();

                if (copyErrors.Count > 0)
                {
                    Dispatcher.Invoke(() =>
                    {
                        MessageTools.ShowMessageBox($"파일 {copyErrors.Count}건을 USB에 저장하지 못했습니다.\r\n로그를 확인해주세요.", "확인");
                    });
                    return;
                }

                Dispatcher.Invoke(() =>
                {
                    try
                    {
                        g_PostCopyAction?.Invoke();
                    }
                    catch { }
                });

                if (g_EnableFtpUpload)
                {
                    await UploadFilesAsync();
                }

                succeeded = true;
            }
            catch (Exception ex)
            {
                Logger.WriteErrorLog(ex.ToString(), Logger.GetLogFileName());
                return;
            }
            finally
            {
                Dispatcher.Invoke(() =>
                {
                    if (!SetDialogResult(succeeded))
                    {
                        closeThisWindow();
                    }
                });
            }
        }

        private async Task CopyFilesWithProgressAsync()
        {
            var copyItems = g_CopyFileList
                .Where(item => item != null && item.CFI_RequireCopy)
                .ToList();

            int total = copyItems.Count;
            if (total == 0)
            {
                UpdateCopyProgress(0, 0, string.Empty);
                return;
            }

            SetStage(g_EnableFtpDownloadMissing ? "콘텐츠 확인 중입니다." : "로컬 복사 중입니다.",
                g_EnableFtpDownloadMissing ? "로컬에 없는 콘텐츠는 서버에서 받은 뒤 저장합니다." : "콘텐츠 파일을 매니저에 보존합니다.");

            int completed = 0;
            foreach (CopyFileInfo item in copyItems)
            {
                string displayName = item?.CFI_FileName ?? string.Empty;
                UpdateCopyProgress(completed, total, displayName);

                if (item == null)
                {
                    completed++;
                    continue;
                }

                if (string.IsNullOrEmpty(item.CFI_FileSourceFullPath) && string.IsNullOrEmpty(item.CFI_FileName))
                {
                    completed++;
                    continue;
                }

                try
                {
                    string source = await ResolveCopySourceAsync(item);
                    string target = item.CFI_TargetFileName;

                    if (string.IsNullOrWhiteSpace(source) || !File.Exists(source))
                    {
                        copyErrors.Add(displayName);
                        Logger.WriteErrorLog($"USB export source missing: {displayName}", Logger.GetLogFileName());
                        continue;
                    }

                    if (File.Exists(source))
                    {
                        if (source.Equals(target, StringComparison.CurrentCultureIgnoreCase))
                        {
                            if (HasSameFileLength(source, target))
                            {
                                completed++;
                                continue;
                            }
                        }
                    }
                    if (!FileTools.CopyFile(source, target))
                    {
                        copyErrors.Add(displayName);
                        Logger.WriteErrorLog($"USB export copy failed: {displayName} / {source} -> {target}", Logger.GetLogFileName());
                    }
                }
                catch (Exception ex)
                {
                    copyErrors.Add(displayName);
                    Logger.WriteErrorLog(ex.ToString(), Logger.GetLogFileName());
                }
                finally
                {
                    completed++;
                    UpdateCopyProgress(completed, total, displayName);
                }
            }
        }

        private async Task<string> ResolveCopySourceAsync(CopyFileInfo item)
        {
            if (item == null)
            {
                return string.Empty;
            }

            string source = item.CFI_FileSourceFullPath;
            if (!string.IsNullOrWhiteSpace(source) && File.Exists(source))
            {
                return source;
            }

            string localContentPath = string.IsNullOrWhiteSpace(item.CFI_FileName)
                ? string.Empty
                : FNDTools.GetTargetContentsFilePath(item.CFI_FileName);
            if (!string.IsNullOrWhiteSpace(localContentPath) && File.Exists(localContentPath))
            {
                return localContentPath;
            }

            if (!g_EnableFtpDownloadMissing || string.IsNullOrWhiteSpace(item.CFI_FileName))
            {
                return source;
            }

            string remoteRelativePath = FtpTransferTools.BuildContentsRelativePath(item.CFI_FileName);
            if (string.IsNullOrWhiteSpace(remoteRelativePath) || string.IsNullOrWhiteSpace(localContentPath))
            {
                return source;
            }

            SetStage("콘텐츠 다운로드 중입니다.", item.CFI_FileName);
            string downloadError = await FtpTransferTools.DownloadFileAsync(remoteRelativePath, localContentPath, uploadCancellation.Token);
            if (!string.IsNullOrWhiteSpace(downloadError))
            {
                Logger.WriteErrorLog($"USB export download failed: {item.CFI_FileName} / {downloadError}", Logger.GetLogFileName());
                return source;
            }

            return localContentPath;
        }

        private async Task UploadFilesAsync()
        {
            List<UploadJob> uploadJobs = BuildUploadJobs();
            if (uploadJobs.Count == 0)
            {
                UpdateUploadProgress(0, 0, string.Empty, 0, 0);
                return;
            }

            if (!FtpTransferTools.TryTestConnection(out string error))
            {
                Logger.WriteErrorLog($"FTP connection failed: {error}", Logger.GetLogFileName());
                Dispatcher.Invoke(() =>
                {
                    MessageTools.ShowMessageBox("전송 서버에 연결할 수 없습니다.\r\n설정을 확인해주세요.", "확인");
                });
                return;
            }

            SetStage("서버 전송 중입니다.", "플레이어용 콘텐츠를 서버로 전송합니다.");

            Dictionary<string, HashSet<string>> remoteNamesByDir = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

            long totalBytes = uploadJobs.Sum(x => x.SizeBytes);
            long uploadedBytes = 0;
            int completed = 0;
            List<string> errors = new List<string>();

            foreach (UploadJob job in uploadJobs)
            {
                string remoteName = Path.GetFileName(job.RemoteRelativePath);
                string remoteDir = GetRemoteRelativeDir(job.RemoteRelativePath);
                if (!remoteNamesByDir.TryGetValue(remoteDir, out HashSet<string> remoteNames))
                {
                    remoteNames = await FtpTransferTools.GetRemoteFileNameSetAsync(remoteDir, uploadCancellation.Token)
                        ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    remoteNamesByDir[remoteDir] = remoteNames;
                }

                if (!job.ForceUpload && !string.IsNullOrWhiteSpace(remoteName) && remoteNames.Contains(remoteName))
                {
                    uploadedBytes += job.SizeBytes;
                    completed++;
                    UpdateUploadProgress(completed, uploadJobs.Count, job.DisplayName, uploadedBytes, totalBytes);
                    continue;
                }

                UpdateUploadProgress(completed, uploadJobs.Count, job.DisplayName, uploadedBytes, totalBytes);

                long jobBase = uploadedBytes;
                var progress = new Progress<TransferProgress>(p =>
                {
                    long current = p?.TransferredBytes ?? 0;
                    long overall = jobBase + current;
                    UpdateUploadProgress(completed, uploadJobs.Count, job.DisplayName, overall, totalBytes);
                });

                string uploadError = await FtpTransferTools.UploadFileAsync(job.LocalPath, job.RemoteRelativePath, progress, uploadCancellation.Token);
                if (!string.IsNullOrWhiteSpace(uploadError))
                {
                    errors.Add(job.DisplayName);
                    Logger.WriteErrorLog($"FTP upload failed: {job.DisplayName} / {uploadError}", Logger.GetLogFileName());
                }
                else if (!string.IsNullOrWhiteSpace(remoteName))
                {
                    remoteNames.Add(remoteName);
                }

                uploadedBytes += job.SizeBytes;
                completed++;
                UpdateUploadProgress(completed, uploadJobs.Count, job.DisplayName, uploadedBytes, totalBytes);
            }

            if (errors.Count > 0)
            {
                Dispatcher.Invoke(() =>
                {
                    MessageTools.ShowMessageBox($"서버 전송 중 {errors.Count}건 오류가 발생했습니다.\r\n로그를 확인해주세요.", "확인");
                });
            }
        }

        private List<UploadJob> BuildUploadJobs()
        {
            var jobs = new List<UploadJob>();
            var dedupe = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (CopyFileInfo item in g_CopyFileList)
            {
                if (item == null)
                {
                    continue;
                }

                string localPath = string.IsNullOrWhiteSpace(item.CFI_TargetFileName)
                    ? item.CFI_FileSourceFullPath
                    : item.CFI_TargetFileName;

                if (string.IsNullOrWhiteSpace(localPath) || !File.Exists(localPath))
                {
                    if (!string.IsNullOrWhiteSpace(item.CFI_FileSourceFullPath) && File.Exists(item.CFI_FileSourceFullPath))
                    {
                        localPath = item.CFI_FileSourceFullPath;
                    }
                    else if (!string.IsNullOrWhiteSpace(item.CFI_FileName))
                    {
                        string fallback = FNDTools.GetTargetContentsFilePath(item.CFI_FileName);
                        if (File.Exists(fallback))
                        {
                            localPath = fallback;
                        }
                    }
                }

                if (string.IsNullOrWhiteSpace(localPath) || !File.Exists(localPath))
                {
                    continue;
                }

                string fileName = string.IsNullOrWhiteSpace(item.CFI_FileName)
                    ? Path.GetFileName(localPath)
                    : item.CFI_FileName;

                if (string.IsNullOrWhiteSpace(fileName))
                {
                    continue;
                }

                string remoteRelativePath = string.IsNullOrWhiteSpace(item.CFI_RemoteRelativePath)
                    ? BuildRemoteRelativePath(fileName)
                    : BuildRemoteRelativePath(item.CFI_RemoteRelativePath);
                if (!dedupe.Add(remoteRelativePath))
                {
                    continue;
                }

                long size = 0;
                try
                {
                    size = new FileInfo(localPath).Length;
                }
                catch { }

                jobs.Add(new UploadJob
                {
                    LocalPath = localPath,
                    RemoteRelativePath = remoteRelativePath,
                    DisplayName = fileName,
                    SizeBytes = size,
                    ForceUpload = item.CFI_ForceFtpUpload
                });
            }

            return jobs;
        }

        private static string GetRemoteRelativeDir(string remoteRelativePath)
        {
            string normalized = string.IsNullOrWhiteSpace(remoteRelativePath)
                ? string.Empty
                : remoteRelativePath.Replace("\\", "/").Trim('/');
            int slashIndex = normalized.LastIndexOf('/');
            if (slashIndex <= 0)
            {
                return "Contents";
            }
            return normalized.Substring(0, slashIndex);
        }

        private static string BuildRemoteRelativePath(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return string.Empty;
            }

            string normalized = fileName.Replace("\\", "/").TrimStart('/');
            if (normalized.StartsWith("Contents/tizen/", StringComparison.OrdinalIgnoreCase))
            {
                return normalized;
            }
            if (normalized.StartsWith("Contents/", StringComparison.OrdinalIgnoreCase))
            {
                return normalized;
            }

            return $"Contents/{normalized}";
        }

        public bool HasSameFileLength(string fpath1, string fpath2)
        {
            FileInfo finfo1 = new FileInfo(fpath1);
            FileInfo finfo2 = new FileInfo(fpath2);
            if (!finfo2.Exists)
                return false;

            return finfo1.Length == finfo2.Length;
        }

        public void closeThisWindow()
        {
            Close();
        }

        private bool SetDialogResult(bool result)
        {
            try
            {
                DialogResult = result;
                return true;
            }
            catch
            {
                return false;
            }
        }

        public void InitCopyFileList(List<CopyFileInfo> copyFileList)
        {
            g_CopyFileList.Clear();

            if (copyFileList == null || copyFileList.Count == 0)
            {
                return;
            }

            foreach (CopyFileInfo item in copyFileList)
            {
                CopyFileInfo tempInfo = new CopyFileInfo();
                tempInfo.CopyData(item);
                g_CopyFileList.Add(tempInfo);
            }
        }

        private void UpdateCopyProgress(int completed, int total, string fileName)
        {
            double percent = total > 0 ? (double)completed / total * 100d : 100d;
            Dispatcher.Invoke(() =>
            {
                CopyProgressBar.Value = Math.Max(0, Math.Min(100, percent));
                CopyProgressText.Text = total > 0 ? $"{completed} / {total} ({percent:0}%)" : "0 / 0";
                if (!string.IsNullOrWhiteSpace(fileName))
                {
                    CurrentFileText.Text = $"복사 중: {fileName}";
                }
            });
        }

        private void UpdateUploadProgress(int completed, int total, string fileName, long uploadedBytes, long totalBytes)
        {
            double percent = totalBytes > 0 ? (double)uploadedBytes / totalBytes * 100d : (total > 0 ? (double)completed / total * 100d : 0d);
            Dispatcher.Invoke(() =>
            {
                UploadProgressBar.Value = Math.Max(0, Math.Min(100, percent));
                UploadProgressText.Text = total > 0 ? $"{completed} / {total} ({percent:0}%)" : "0 / 0";
                if (!string.IsNullOrWhiteSpace(fileName))
                {
                    CurrentFileText.Text = $"업로드 중: {fileName}";
                }
            });
        }

        private void SetStage(string title, string subtitle)
        {
            Dispatcher.Invoke(() =>
            {
                MainStatusText.Text = title ?? string.Empty;
                SubStatusText.Text = subtitle ?? string.Empty;
            });
        }

        private void BtnWin_close_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            Close();
        }

        private void BtnWin_drag_PreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            DragMove();
        }
    }
}
