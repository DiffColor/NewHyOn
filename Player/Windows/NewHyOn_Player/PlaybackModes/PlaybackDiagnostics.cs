using System.Collections.Generic;

namespace NewHyOnPlayer.PlaybackModes
{
    internal static class PlaybackDiagnostics
    {
        public static List<PlaybackDebugItem> Merge(List<PlaybackDebugItem> activeItems, List<PlaybackDebugItem> standbyItems)
        {
            List<PlaybackDebugItem> items = new List<PlaybackDebugItem>();
            if (activeItems != null)
            {
                items.AddRange(activeItems);
            }
            if (standbyItems != null)
            {
                items.AddRange(standbyItems);
            }
            return items;
        }
    }
}
