using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TurtleTools;
using SharedContentPeriodPayload = AndoW.Shared.ContentPeriodPayload;
using SharedContentsInfoClass = AndoW.Shared.ContentsInfoClass;
using SharedElementInfoClass = AndoW.Shared.ElementInfoClass;
using SharedPageInfoClass = AndoW.Shared.PageInfoClass;

namespace NewHyOnPlayer.PlaybackModes
{
    internal sealed class PlaybackCoordinator
    {
        private readonly MainWindow owner;

        public PlaybackCoordinator(MainWindow owner)
        {
            this.owner = owner;
        }

        public Task<SeamlessPagePlan> BuildPagePlanAsync(SharedPageInfoClass page, string playlistName)
        {
            return Task.Run(() => BuildPagePlan(page, playlistName));
        }

        public SeamlessPagePlan BuildPagePlan(SharedPageInfoClass page, string playlistName)
        {
            if (page == null)
            {
                return null;
            }

            SeamlessPagePlan plan = new SeamlessPagePlan();
            plan.PlaylistName = playlistName ?? string.Empty;
            plan.PageName = page.PIC_PageName ?? string.Empty;
            plan.CanvasWidth = page.PIC_CanvasWidth > 0 ? page.PIC_CanvasWidth : 1920;
            plan.CanvasHeight = page.PIC_CanvasHeight > 0 ? page.PIC_CanvasHeight : 1080;
            plan.DurationSeconds = Math.Max(1, (page.PIC_PlaytimeHour * 3600) + (page.PIC_PlaytimeMinute * 60) + page.PIC_PlaytimeSecond);

            List<SharedElementInfoClass> playableElements = new List<SharedElementInfoClass>();
            if (page.PIC_Elements != null)
            {
                foreach (SharedElementInfoClass element in page.PIC_Elements.OrderBy(x => x.EIF_ZIndex))
                {
                    if (!TryParseDisplayType(element, out DisplayType displayType))
                    {
                        continue;
                    }

                    if (displayType != DisplayType.Media)
                    {
                        continue;
                    }

                    playableElements.Add(element);
                    if (playableElements.Count == 6)
                    {
                        break;
                    }
                }
            }

            foreach (SharedElementInfoClass element in playableElements)
            {
                SeamlessSlotPlan slotPlan = new SeamlessSlotPlan();
                slotPlan.ElementName = element.EIF_Name ?? string.Empty;
                slotPlan.IsMuted = element.EIF_IsMuted;
                slotPlan.Width = Math.Max(0, element.EIF_Width);
                slotPlan.Height = Math.Max(0, element.EIF_Height);
                slotPlan.Left = element.EIF_PosLeft;
                slotPlan.Top = element.EIF_PosTop;
                slotPlan.ZIndex = element.EIF_ZIndex;

                if (element.EIF_ContentsInfoClassList != null)
                {
                    foreach (SharedContentsInfoClass content in element.EIF_ContentsInfoClassList)
                    {
                        SeamlessContentItem item = BuildContentItem(content);
                        if (item != null)
                        {
                            slotPlan.Items.Add(item);
                        }
                    }
                }

                plan.Slots.Add(slotPlan);
            }

            while (plan.Slots.Count < 6)
            {
                plan.Slots.Add(new SeamlessSlotPlan());
            }

            if (page.PIC_Elements != null && page.PIC_Elements.Count > 6)
            {
                Logger.WriteLog($"페이지({page.PIC_PageName})의 Media 요소가 6개를 초과하여 앞 6개만 사용합니다.", Logger.GetLogFileName());
            }

            return plan;
        }

        private SeamlessContentItem BuildContentItem(SharedContentsInfoClass content)
        {
            if (content == null || string.IsNullOrWhiteSpace(content.CIF_FileName))
            {
                return null;
            }

            if (content.CIF_PlayMinute == "00" && content.CIF_PlaySec == "00")
            {
                return null;
            }

            if (owner != null && !string.IsNullOrWhiteSpace(content.CIF_StrGUID))
            {
                SharedContentPeriodPayload period;
                if (owner.TryGetContentPeriod(content.CIF_StrGUID, out period) && period != null)
                {
                    DateTime start;
                    DateTime end;
                    if (!DateTime.TryParse(period.StartDate, out start))
                    {
                        start = DateTime.MinValue;
                    }
                    if (!DateTime.TryParse(period.EndDate, out end))
                    {
                        end = DateTime.MaxValue;
                    }

                    DateTime today = DateTime.Today;
                    if (today < start.Date || today > end.Date)
                    {
                        return null;
                    }
                }
            }

            NewHyOnPlayer.ContentType contentType;
            if (!Enum.TryParse(content.CIF_ContentType, true, out contentType))
            {
                return null;
            }

            string filePath = FNDTools.GetContentsFilePath(content.CIF_FileName);
            if (!File.Exists(filePath))
            {
                return null;
            }

            try
            {
                FileInfo info = new FileInfo(filePath);
                if (!info.Exists || info.Length == 0)
                {
                    return null;
                }
            }
            catch
            {
                return null;
            }

            int minute = 0;
            int second = 0;
            int.TryParse(content.CIF_PlayMinute, out minute);
            int.TryParse(content.CIF_PlaySec, out second);
            int duration = Math.Max(1, (minute * 60) + second);

            long actualDuration = duration;
            if (contentType == NewHyOnPlayer.ContentType.Video)
            {
                try
                {
                    actualDuration = Math.Max(1, (long)MediaTools.GetVideoDuration(filePath).TotalSeconds);
                }
                catch (Exception ex)
                {
                    Logger.WriteErrorLog($"영상 길이 확인 실패: {filePath}, {ex}", Logger.GetLogFileName());
                    actualDuration = duration;
                }
            }

            bool shouldLoop = false;
            bool transitionByTimer = true;
            int loopDisableAfterEndCount = 0;
            int transitionEndEventCount = 0;

            if (contentType == NewHyOnPlayer.ContentType.Video)
            {
                long safeActualDuration = Math.Max(1, actualDuration);
                long remainder = duration % safeActualDuration;

                if (duration < safeActualDuration)
                {
                    shouldLoop = false;
                    transitionByTimer = true;
                }
                else if (duration == safeActualDuration)
                {
                    shouldLoop = false;
                    transitionByTimer = false;
                    transitionEndEventCount = 1;
                }
                else
                {
                    shouldLoop = true;
                    transitionByTimer = remainder != 0;

                    int fullPlaybackCount = (int)(duration / safeActualDuration);
                    if (remainder == 0)
                    {
                        loopDisableAfterEndCount = Math.Max(0, fullPlaybackCount - 1);
                        transitionEndEventCount = Math.Max(1, fullPlaybackCount);
                    }
                    else
                    {
                        loopDisableAfterEndCount = Math.Max(1, fullPlaybackCount);
                    }
                }
            }

            return new SeamlessContentItem
            {
                Source = content,
                FilePath = filePath,
                ContentType = contentType,
                DurationSeconds = duration,
                ActualDurationSeconds = actualDuration,
                ShouldLoop = shouldLoop,
                TransitionByTimer = transitionByTimer,
                LoopDisableAfterEndCount = loopDisableAfterEndCount,
                TransitionEndEventCount = transitionEndEventCount
            };
        }

        private static bool TryParseDisplayType(SharedElementInfoClass element, out DisplayType displayType)
        {
            displayType = DisplayType.None;
            if (element == null || string.IsNullOrWhiteSpace(element.EIF_Type))
            {
                return false;
            }

            return Enum.TryParse(element.EIF_Type, true, out displayType)
                && displayType != DisplayType.ScrollText
                && displayType != DisplayType.WelcomeBoard;
        }
    }
}
