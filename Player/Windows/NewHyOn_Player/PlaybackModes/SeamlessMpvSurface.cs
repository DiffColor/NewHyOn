using System;
using System.Linq;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace NewHyOnPlayer.PlaybackModes
{
    internal sealed class SeamlessMpvSurface : Border
    {
        private const int ControlledImageDurationSeconds = 86400;
        private readonly MPVLibControl player;
        private bool muted = true;

        public event Action MediaLoaded;

        public SeamlessMpvSurface()
        {
            Background = Brushes.Black;
            SnapsToDevicePixels = true;
            ClipToBounds = true;
            Visibility = Visibility.Hidden;
            Opacity = 0.0;

            player = new MPVLibControl
            {
                Stretch = Stretch.Fill,
                Volume = 80,
                Muted = true,
                Loop = false,
                AutoPlay = false,
                Visibility = Visibility.Visible
            };

            player.FileLoadedEvent += Player_FileLoadedEvent;
            Child = player;
        }

        public bool IsMediaLoaded
        {
            get { return player.IsMediaLoaded(); }
        }

        public bool IsPlaying
        {
            get { return player.IsPlaying(); }
        }

        public int PlaylistIndex
        {
            get { return player.PlaylistIndex; }
        }

        public void Configure(bool muted)
        {
            this.muted = muted;
            player.Muted = muted;
        }

        public void SetLooping(bool shouldLoop)
        {
            player.Loop = shouldLoop;
        }

        public bool LoadPlaylist(SeamlessContentItem[] items, int startIndex, bool autoPlay)
        {
            if (items == null || items.Length == 0)
            {
                Stop();
                return false;
            }

            int safeIndex = NormalizeIndex(items.Length, startIndex);
            ApplyPlaybackOptions(items[safeIndex], items.Length);
            player.LoadPlaylist(items.Select(x => x.FilePath).ToArray(), autoPlay);

            if (safeIndex > 0)
            {
                return player.SetPlaylistIndex(safeIndex, autoPlay);
            }

            if (!autoPlay)
            {
                player.Pause();
            }

            return true;
        }

        public bool SwitchToIndex(SeamlessContentItem[] items, int index, bool autoPlay)
        {
            if (items == null || items.Length == 0)
            {
                Stop();
                return false;
            }

            int safeIndex = NormalizeIndex(items.Length, index);
            ApplyPlaybackOptions(items[safeIndex], items.Length);
            return player.SetPlaylistIndex(safeIndex, autoPlay);
        }

        public void Load(SeamlessContentItem item, bool autoPlay)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.FilePath))
            {
                Stop();
                return;
            }

            player.AutoPlay = autoPlay;
            player.Muted = muted;
            player.Loop = item.ShouldLoop;
            if (item.ContentType == NewHyOnPlayer.ContentType.Image)
            {
                player.ImageDuration = Math.Max(1, item.DurationSeconds).ToString(CultureInfo.InvariantCulture);
            }
            player.Load(item.FilePath, true);
            if (!autoPlay)
            {
                player.Pause();
            }
        }

        public void ShowSurface()
        {
            Visibility = Visibility.Visible;
            Opacity = 1.0;
        }

        public void HideSurface()
        {
            Opacity = 0.0;
            Visibility = Visibility.Hidden;
        }

        public void Play()
        {
            player.Play();
        }

        public void Pause()
        {
            player.Pause();
        }

        public bool SeekToStart()
        {
            return player.TrySeek(TimeSpan.Zero);
        }

        public void Stop()
        {
            try
            {
                player.Stop();
            }
            catch
            {
            }

            HideSurface();
        }

        private void ApplyPlaybackOptions(SeamlessContentItem item, int itemCount)
        {
            player.AutoPlay = false;
            player.Muted = muted;
            player.Loop = item != null && item.ShouldLoop;
            player.LoopPlaylist = false;

            if (item != null && item.ContentType == NewHyOnPlayer.ContentType.Image)
            {
                player.ImageDuration = ControlledImageDurationSeconds.ToString(CultureInfo.InvariantCulture);
            }
        }

        private static int NormalizeIndex(int itemCount, int index)
        {
            if (itemCount <= 0)
            {
                return 0;
            }

            if (index < 0)
            {
                return 0;
            }

            if (index >= itemCount)
            {
                return itemCount - 1;
            }

            return index;
        }

        private void Player_FileLoadedEvent()
        {
            MediaLoaded?.Invoke();
        }
    }
}
