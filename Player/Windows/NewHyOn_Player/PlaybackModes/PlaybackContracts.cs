using System.Collections.Generic;
using SharedContentsInfoClass = AndoW.Shared.ContentsInfoClass;
using SharedPageInfoClass = AndoW.Shared.PageInfoClass;

namespace NewHyOnPlayer.PlaybackModes
{
    internal enum SeamlessLayoutState
    {
        Idle,
        Preparing,
        Ready,
        Active
    }

    internal enum SeamlessSlotState
    {
        Idle,
        Preparing,
        Ready,
        Active,
        Error
    }

    internal sealed class SeamlessContentItem
    {
        public SharedContentsInfoClass Source { get; set; }
        public string FilePath { get; set; }
        public NewHyOnPlayer.ContentType ContentType { get; set; }
        public int DurationSeconds { get; set; }
        public long ActualDurationSeconds { get; set; }
        public bool ShouldLoop { get; set; }
        public bool TransitionByTimer { get; set; }
        public int LoopDisableAfterEndCount { get; set; }
        public int TransitionEndEventCount { get; set; }

        public bool IsVideo
        {
            get { return ContentType == NewHyOnPlayer.ContentType.Video; }
        }
    }

    internal sealed class SeamlessSlotPlan
    {
        public string ElementName { get; set; }
        public bool IsMuted { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public double Left { get; set; }
        public double Top { get; set; }
        public int ZIndex { get; set; }
        public List<SeamlessContentItem> Items { get; set; }

        public SeamlessSlotPlan()
        {
            Items = new List<SeamlessContentItem>();
        }

        public bool HasPlayableItems
        {
            get { return Items != null && Items.Count > 0; }
        }
    }

    internal sealed class SeamlessPagePlan
    {
        public string PlaylistName { get; set; }
        public string PageName { get; set; }
        public double CanvasWidth { get; set; }
        public double CanvasHeight { get; set; }
        public int DurationSeconds { get; set; }
        public List<SeamlessSlotPlan> Slots { get; set; }

        public SeamlessPagePlan()
        {
            Slots = new List<SeamlessSlotPlan>();
        }

        public string PlanKey
        {
            get { return string.Concat(PlaylistName ?? string.Empty, "|", PageName ?? string.Empty); }
        }
    }

    internal sealed class SeamlessSyncStatus
    {
        public string ElementName { get; set; }
        public string CurrentContentName { get; set; }
        public string NextContentName { get; set; }
        public int CurrentIndex { get; set; }
        public int NextIndex { get; set; }
        public long ElapsedSeconds { get; set; }
        public long DurationSeconds { get; set; }
        public bool IsVisible { get; set; }
    }

    internal sealed class SeamlessPlaybackPulse
    {
        public SeamlessSyncStatus Status { get; set; }
        public bool IsSecondTick { get; set; }
        public bool IsContentBoundary { get; set; }
    }

    internal sealed class SeamlessPagePlaybackRequest
    {
        public SharedPageInfoClass CurrentPage { get; set; }
        public SharedPageInfoClass NextPage { get; set; }
        public string PlaylistName { get; set; }
    }

    internal sealed class PlaybackDebugItem
    {
        public string ElementName { get; set; }
        public string CurrentContentName { get; set; }
        public string NextContentName { get; set; }
        public long ElapsedSeconds { get; set; }
        public long DurationSeconds { get; set; }
        public bool IsVisible { get; set; }
        public string LayoutName { get; set; }
        public int SlotIndex { get; set; }
        public SeamlessLayoutState LayoutState { get; set; }
        public SeamlessSlotState SlotState { get; set; }
    }
}
