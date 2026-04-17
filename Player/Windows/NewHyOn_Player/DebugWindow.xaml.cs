using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Threading;
using NewHyOnPlayer.DataManager;
using NewHyOnPlayer.PlaybackModes;

namespace NewHyOnPlayer
{
    public partial class DebugWindow : Window
    {
        private readonly MainWindow owner;
        private readonly DispatcherTimer timer;

        public DebugWindow(MainWindow owner)
        {
            InitializeComponent();
            this.owner = owner;
            timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            timer.Tick += (s, e) => RefreshInfo();
            Loaded += (s, e) => RefreshInfo();
            Unloaded += (s, e) => timer.Stop();
        }

        public void Start()
        {
            RefreshInfo();
            timer.Start();
        }

        private void RefreshInfo()
        {
            if (owner == null)
            {
                return;
            }

            PlaybackText.Text = BuildPlaybackText();
            ContentText.Text = BuildContentText();

            var queues = LoadQueues(out QueueSummary activeQueue);
            NextText.Text = BuildNextText(activeQueue?.PlaylistId);
            CommandText.Text = BuildCommandText(activeQueue);
            QueueList.ItemsSource = queues;
        }

        private string BuildPlaybackText()
        {
            var player = owner.g_PlayerInfoManager?.g_PlayerInfo;
            var sb = new StringBuilder();

            sb.AppendLine($"페이지리스트: {ValueOrDash(player?.PIF_CurrentPlayList)}");
            sb.AppendLine($"현재 페이지: {ValueOrDash(owner.CurrentPageName)}");
            sb.AppendLine($"페이지 재생: {owner.CurrentPageElapsedSeconds}/{owner.CurrentPageDurationSeconds}초");

            return sb.ToString().TrimEnd();
        }

        private string BuildContentText()
        {
            var sb = new StringBuilder();
            var list = owner.GetPlaybackDebugItems() ?? new List<PlaybackDebugItem>();
            int index = 1;

            foreach (var item in list)
            {
                if (item == null || string.IsNullOrWhiteSpace(item.ElementName))
                {
                    continue;
                }
                string elementName = ValueOrDash(item.ElementName);
                string currentName = ValueOrDash(item.CurrentContentName);
                string nextName = ValueOrDash(item.NextContentName);
                string visibleState = item.IsVisible ? "Visible" : "Hidden";

                sb.AppendLine($"[{index}] {elementName} / {item.LayoutName} / Slot {item.SlotIndex} ({visibleState})");
                sb.AppendLine($" - 상태: Layout={item.LayoutState}, Slot={item.SlotState}");
                sb.AppendLine($" - 현재 컨텐츠: {currentName} ({item.ElapsedSeconds}/{item.DurationSeconds}초)");
                sb.AppendLine($" - 다음 컨텐츠: {nextName}");
                sb.AppendLine();
                index++;
            }

            if (sb.Length == 0)
            {
                sb.Append("재생 중인 컨텐츠가 없습니다.");
            }

            return sb.ToString().TrimEnd();
        }

        private string BuildNextText(string nextPlaylistFromQueue)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"다음 페이지: {ValueOrDash(owner.NextPageName)}");

            string nextPlaylist = string.IsNullOrWhiteSpace(nextPlaylistFromQueue)
                ? owner.g_PlayerInfoManager?.g_PlayerInfo?.PIF_CurrentPlayList
                : nextPlaylistFromQueue;

            sb.AppendLine($"다음 페이지리스트(큐 포함): {ValueOrDash(nextPlaylist)}");

            return sb.ToString().TrimEnd();
        }

        private string BuildCommandText(QueueSummary activeQueue)
        {
            var sb = new StringBuilder();
            var cmdService = owner.CommandService;
            string cmd = ValueOrDash(cmdService?.CurrentCommand);
            bool running = cmdService != null && cmdService.IsHandlingCommand;

            sb.AppendLine($"커맨드 상태: {(running ? "실행중" : "대기")}");
            sb.AppendLine($"최근 커맨드: {cmd}");

            if (activeQueue != null)
            {
                sb.AppendLine($"업데이트 상태: {activeQueue.Status} ({ValueOrDash(activeQueue.PlaylistId)})");
                sb.AppendLine($"다운로드: {activeQueue.Download:P0} / 검증: {activeQueue.Validate:P0}");
                if (!string.IsNullOrWhiteSpace(activeQueue.LastError))
                {
                    sb.AppendLine($"오류: {activeQueue.LastError}");
                }
            }
            else
            {
                sb.Append("업데이트 큐 없음");
            }

            return sb.ToString().TrimEnd();
        }

        private List<QueueView> LoadQueues(out QueueSummary activeQueue)
        {
            var items = new List<QueueView>();
            activeQueue = null;

            try
            {
                using (var repo = new UpdateQueueRepository())
                {
                    var queues = repo.LoadAll()
                        .OrderByDescending(q => q.CreatedTicks)
                        .ToList();

                    foreach (var q in queues)
                    {
                        if (activeQueue == null && IsActiveStatus(q.Status))
                        {
                            activeQueue = new QueueSummary
                            {
                                Id = q.Id,
                                PlaylistId = q.PlaylistId,
                                Status = q.Status,
                                Download = q.DownloadProgress,
                                Validate = q.ValidateProgress,
                                LastError = q.LastError
                            };
                        }

                        items.Add(new QueueView
                        {
                            Title = $"{q.Status} | {ValueOrDash(q.PlaylistId)}",
                            Detail = BuildQueueDetail(q)
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                items.Add(new QueueView
                {
                    Title = "큐 로드 실패",
                    Detail = ex.Message
                });
            }

            return items;
        }

        private static string BuildQueueDetail(UpdateQueue queue)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"ID: {queue.Id}");
            sb.AppendLine($"플레이리스트: {ValueOrDash(queue.PlaylistId)}");
            sb.AppendLine($"다운로드: {queue.DownloadProgress:P0}, 검증: {queue.ValidateProgress:P0}");
            sb.AppendLine($"상태: {queue.Status} (재시도 {queue.RetryCount})");
            if (!string.IsNullOrWhiteSpace(queue.LastError))
            {
                sb.AppendLine($"오류: {queue.LastError}");
            }

            return sb.ToString().TrimEnd();
        }

        private static bool IsActiveStatus(string status)
        {
            return !string.Equals(status, UpdateQueueStatus.Done, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(status, UpdateQueueStatus.Failed, StringComparison.OrdinalIgnoreCase);
        }

        private static string ValueOrDash(string text)
        {
            return string.IsNullOrWhiteSpace(text) ? "-" : text;
        }

        private sealed class QueueView
        {
            public string Title { get; set; }
            public string Detail { get; set; }
        }

        private sealed class QueueSummary
        {
            public string Id { get; set; }
            public string PlaylistId { get; set; }
            public string Status { get; set; }
            public double Download { get; set; }
            public double Validate { get; set; }
            public string LastError { get; set; }
        }
    }
}
