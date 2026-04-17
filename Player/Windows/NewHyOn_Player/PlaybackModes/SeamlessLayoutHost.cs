using System.Collections.Generic;
using System.Windows.Controls;
using System.Windows.Media;

namespace NewHyOnPlayer.PlaybackModes
{
    internal sealed class SeamlessLayoutHost : Canvas
    {
        public SeamlessLayoutHost()
        {
            Background = Brushes.Black;
            ClipToBounds = true;
            Visibility = System.Windows.Visibility.Hidden;
            Opacity = 0.0;
            IsHitTestVisible = false;
        }

        public void SetCanvasSize(double width, double height)
        {
            Width = width;
            Height = height;
        }

        public void AttachSurfaces(IEnumerable<SeamlessContentSlot> slots)
        {
            Children.Clear();
            if (slots == null)
            {
                return;
            }

            foreach (SeamlessContentSlot slot in slots)
            {
                if (slot == null)
                {
                    continue;
                }

                Children.Add(slot.View);
            }
        }
    }
}
