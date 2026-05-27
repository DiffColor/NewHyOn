using Mpv.NET.API;
using Mpv.NET.Player;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace NewHyOnPlayer
{
    /// <summary>
    /// MPVLibControl.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class MPVLibControl : UserControl
    {
        public delegate void FileLoaded();
        public event FileLoaded FileLoadedEvent;

        public delegate void FileEnded();
        public event FileEnded FileEndedEvent;

        private MpvPlayer sPlayer;
        private readonly object mpvLock = new object();
        private bool isDisposed;

        public bool IsMediaLoaded()
        {
            return TryWithPlayer(player => player.IsMediaLoaded, false);
        }

        public bool IsPlaying()
        {
            return TryWithPlayer(player => player.IsPlaying, false);
        }

        bool sAutoPlay = false;
        public bool AutoPlay
        {
            set
            {
                TryWithPlayer(player =>
                {
                    player.AutoPlay = value;
                    return true;
                }, false);
                sAutoPlay = value;
            }

            get
            {
                return sAutoPlay;
            }
        }

        public Stretch Stretch
        {
            set
            {
                SetValue(StretchProperty, value);
            }

            get
            {
                return (Stretch)GetValue(StretchProperty);
            }
        }

        public static readonly DependencyProperty StretchProperty = DependencyProperty.Register
            (
                "Stretch",
                typeof(Stretch),
                typeof(MPVLibControl),
                new PropertyMetadata(Stretch.Uniform, OnStretchChanged)
            );

        private static void OnStretchChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            MPVLibControl mpv = d as MPVLibControl;
            mpv?.StretchChanged((Stretch)e.NewValue);
        }

        private void StretchChanged(Stretch value)
        {
            TryWithPlayer(player =>
            {
                switch (value)
                {
                    case Stretch.Fill:
                        player.API.SetPropertyString("keepaspect", "no");
                        break;

                    case Stretch.UniformToFill:
                        player.API.SetPropertyString("panscan", "1.0");
                        break;

                    case Stretch.None:
                    case Stretch.Uniform:
                    default:
                        player.API.SetPropertyString("keepaspect", "yes");
                        break;
                }

                return true;
            }, false);
        }

        public double SpeedRate
        {
            set
            {
                SetValue(SpeedRateProperty, value);
            }

            get
            {
                return (double)GetValue(SpeedRateProperty);
            }
        }

        public static readonly DependencyProperty SpeedRateProperty = DependencyProperty.Register
            (
                "SpeedRate", typeof(double),
                typeof(MPVLibControl),
                new PropertyMetadata(1.0, OnSpeedRateChanged)
            );

        private static void OnSpeedRateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            MPVLibControl mpv = d as MPVLibControl;
            mpv?.SpeedRateChanged((double)e.NewValue);
        }

        private void SpeedRateChanged(double value)
        {
            TryWithPlayer(player =>
            {
                player.Speed = value;
                return true;
            }, false);
        }

        public bool Loop
        {
            set
            {
                SetValue(LoopProperty, value);
            }

            get
            {
                return (bool)GetValue(LoopProperty);
            }
        }

        public static readonly DependencyProperty LoopProperty = DependencyProperty.Register
            (
                "Loop", typeof(bool),
                typeof(MPVLibControl),
                new PropertyMetadata(true, OnLoopChanged)
            );

        private static void OnLoopChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            MPVLibControl mpv = d as MPVLibControl;
            mpv?.LoopChanged((bool)e.NewValue);
        }

        private void LoopChanged(bool value)
        {
            TryWithPlayer(player =>
            {
                player.Loop = value;
                return true;
            }, false);
        }

        public int Volume
        {
            set
            {
                SetValue(VolumeProperty, value);
            }

            get
            {
                return (int)GetValue(VolumeProperty);
            }
        }

        public static readonly DependencyProperty VolumeProperty = DependencyProperty.Register
            (
                "Volume",
                typeof(int),
                typeof(MPVLibControl),
                new PropertyMetadata(50, OnVolumeChanged)
            );

        private static void OnVolumeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            MPVLibControl mpv = d as MPVLibControl;
            mpv?.VolumeChanged((int)e.NewValue);
        }

        private void VolumeChanged(int value)
        {
            TryWithPlayer(player =>
            {
                player.Volume = value;
                return true;
            }, false);
        }

        public bool Muted
        {
            set
            {
                SetValue(MutedProperty, value);
            }

            get
            {
                return (bool)GetValue(MutedProperty);
            }
        }

        public static readonly DependencyProperty MutedProperty = DependencyProperty.Register
            (
                "Muted",
                typeof(bool),
                typeof(MPVLibControl),
                new PropertyMetadata(true, OnMutedChanged)
            );

        private static void OnMutedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            MPVLibControl mpv = d as MPVLibControl;
            mpv?.MutedChanged((bool)e.NewValue);
        }

        private void MutedChanged(bool value)
        {
            TryWithPlayer(player =>
            {
                player.API.SetPropertyString("mute", value ? "yes" : "no");
                return true;
            }, false);
        }

        public TimeSpan Position
        {
            get
            {
                return TryWithPlayer(player => player.IsMediaLoaded ? player.Position : TimeSpan.Zero, TimeSpan.Zero);
            }
            set
            {
                TryWithPlayer(player => player.TrySeek(value), false);
            }
        }

        public bool TrySeek(TimeSpan position, bool relative = false)
        {
            return TryWithPlayer(player => player.TrySeek(position, relative), false);
        }

        public string ImageDuration
        {
            get
            {
                return TryWithPlayer(player => player.API.GetPropertyString("image-display-duration"), string.Empty);
            }
            set
            {
                TryWithPlayer(player =>
                {
                    player.API.SetPropertyString("image-display-duration", value);
                    return true;
                }, false);
            }
        }

        public MPVLibControl()
        {
            InitializeComponent();
            sPlayer = new MpvPlayer(PlayerHost.Handle);
            sPlayer.KeepOpen = KeepOpen.Always;
            sPlayer.MediaLoaded += Player_MediaLoaded;
            sPlayer.MediaFinished += Player_MediaFinished;
            MutedChanged(Muted);
        }

        private void Player_MediaLoaded(object sender, EventArgs e)
        {
            FileLoadedEvent?.Invoke();
        }

        private void Player_MediaFinished(object sender, EventArgs e)
        {
            FileEndedEvent?.Invoke();
        }

        public void Load(string fpath, bool append = false)
        {
            if (string.IsNullOrWhiteSpace(fpath))
            {
                return;
            }

            TryWithPlayer(player =>
            {
                Visibility = Visibility.Visible;
                player.Load(fpath, !append);
                return true;
            }, false);
        }

        public void AppendFiles(string[] pathes, bool append = false)
        {
            if (pathes == null || pathes.Length == 0)
            {
                return;
            }

            TryWithPlayer(player =>
            {
                player.LoadPlaylist(pathes, !append);
                return true;
            }, false);
        }

        public int PlaylistEntryCount
        {
            get
            {
                return TryWithPlayer(player => player.PlaylistEntryCount, 0);
            }
        }

        public int PlaylistIndex
        {
            get
            {
                return TryWithPlayer(player => player.PlaylistIndex, -1);
            }
        }

        public bool LoopPlaylist
        {
            get
            {
                return TryWithPlayer(player => player.LoopPlaylist, false);
            }
            set
            {
                TryWithPlayer(player =>
                {
                    player.LoopPlaylist = value;
                    return true;
                }, false);
            }
        }

        public void LoadPlaylist(string[] pathes, bool autoPlay)
        {
            if (pathes == null || pathes.Length == 0)
            {
                return;
            }

            TryWithPlayer(player =>
            {
                Visibility = Visibility.Visible;
                player.AutoPlay = autoPlay;
                sAutoPlay = autoPlay;
                player.LoadPlaylist(pathes, true);
                return true;
            }, false);
        }

        void Dispose()
        {
            MpvPlayer playerToDispose = null;

            lock (mpvLock)
            {
                if (isDisposed)
                {
                    return;
                }

                isDisposed = true;
                playerToDispose = sPlayer;
                sPlayer = null;
            }

            if (playerToDispose == null)
            {
                return;
            }

            try
            {
                playerToDispose.MediaLoaded -= Player_MediaLoaded;
                playerToDispose.MediaFinished -= Player_MediaFinished;
                playerToDispose.Dispose();
            }
            catch (ObjectDisposedException)
            {
            }
        }

        public void Play()
        {
            TryWithPlayer(player =>
            {
                Visibility = Visibility.Visible;
                player.Resume();
                return true;
            }, false);
        }

        public void Pause(bool hide = false)
        {
            bool paused = TryWithPlayer(player =>
            {
                player.Pause();
                return true;
            }, false);

            if (!paused)
            {
                return;
            }

            Visibility = hide ? Visibility.Collapsed : Visibility.Visible;
        }

        public void Stop()
        {
            bool stopped = TryWithPlayer(player =>
            {
                player.KeepOpen = KeepOpen.No;
                player.Stop();
                player.KeepOpen = KeepOpen.Always;
                return true;
            }, false);

            if (stopped)
            {
                Visibility = Visibility.Collapsed;
            }
        }

        public void Close()
        {
            TryWithPlayer(player =>
            {
                player.Stop();
                return true;
            }, false);
        }

        public void LoadVideoByIndex(int index, bool preload = false)
        {
            if (index < 0)
            {
                return;
            }

            bool autoPlay = AutoPlay;
            bool loaded = TryWithPlayer(player =>
            {
                player.API.SetPropertyLong("playlist-pos", index);
                if (autoPlay)
                {
                    player.Resume();
                }
                else
                {
                    player.Pause();
                }

                return true;
            }, false);

            if (!loaded || autoPlay)
            {
                return;
            }

            Visibility = preload ? Visibility.Collapsed : Visibility.Visible;
        }

        public bool SetPlaylistIndex(int index, bool autoPlay)
        {
            if (index < 0)
            {
                return false;
            }

            return TryWithPlayer(player =>
            {
                Visibility = Visibility.Visible;
                player.AutoPlay = autoPlay;
                sAutoPlay = autoPlay;
                player.API.SetPropertyLong("playlist-pos", index);
                if (autoPlay)
                {
                    player.Resume();
                }
                else
                {
                    player.Pause();
                }

                return true;
            }, false);
        }

        public bool PlaylistNext()
        {
            return TryWithPlayer(player => player.PlaylistNext(), false);
        }

        public bool PlaylistPrevious()
        {
            return TryWithPlayer(player => player.PlaylistPrevious(), false);
        }

        public bool PlaylistRemove()
        {
            return TryWithPlayer(player => player.PlaylistRemove(), false);
        }

        public bool PlaylistRemove(int index)
        {
            return TryWithPlayer(player => player.PlaylistRemove(index), false);
        }

        public bool PlaylistMove(int oldIndex, int newIndex)
        {
            return TryWithPlayer(player => player.PlaylistMove(oldIndex, newIndex), false);
        }

        public void PlaylistClear()
        {
            TryWithPlayer(player =>
            {
                player.PlaylistClear();
                return true;
            }, false);
        }

        private TResult TryWithPlayer<TResult>(Func<MpvPlayer, TResult> action, TResult fallback)
        {
            if (action == null)
            {
                return fallback;
            }

            lock (mpvLock)
            {
                MpvPlayer player = sPlayer;
                if (isDisposed || player == null)
                {
                    return fallback;
                }

                try
                {
                    return action(player);
                }
                catch (ObjectDisposedException)
                {
                    isDisposed = true;
                    sPlayer = null;
                    return fallback;
                }
                catch (MpvAPIException)
                {
                    return fallback;
                }
            }
        }

        private void UserControl_Unloaded(object sender, RoutedEventArgs e)
        {
            Dispose();
        }
    }
}
