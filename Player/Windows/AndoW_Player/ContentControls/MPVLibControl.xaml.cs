using Mpv.NET.Player;
using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace HyOnPlayer
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

        MpvPlayer sPlayer;

        private readonly object mpvLock = new object();

        public bool IsMediaLoaded()
        {
            if (sPlayer != null)
                return sPlayer.IsMediaLoaded;

            return false;
        }
        public bool IsPlaying()
        {
            if (sPlayer != null)
                return sPlayer.IsPlaying;

            return false;
        }

        bool sAutoPlay = false;
        public bool AutoPlay
        {
            set
            {
                //SetValue(AutoPlayProperty, value);
                if (sPlayer != null)
                {
                    sPlayer.AutoPlay = value;
                }
                sAutoPlay = value;
            }

            get
            {
                //return (bool)GetValue(AutoPlayProperty);
                return sAutoPlay;
            }
        }

        //public static readonly DependencyProperty AutoPlayProperty = DependencyProperty.Register
        //   (
        //       "AutoPlay",
        //       typeof(bool),
        //       typeof(MPVLibControl),
        //       new PropertyMetadata(true, OnAutoPlayChanged)
        //   );

        //private static void OnAutoPlayChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        //{
        //    MPVLibControl mpv = d as MPVLibControl;
        //    mpv.AutoPlayChanged((bool)e.NewValue);
        //}

        //private void AutoPlayChanged(bool value)
        //{
        //    if (sPlayer != null)
        //    {
        //        sPlayer.AutoPlay = value;
        //    }
        //}

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
            mpv.StretchChanged((Stretch)e.NewValue);
        }

        private void StretchChanged(Stretch value)
        {
            if (sPlayer != null)
            {
                lock (mpvLock)
                {
                    switch (value)
                    {
                        case Stretch.Fill:
                            sPlayer.API.SetPropertyString("keepaspect", "no");
                            break;

                        case Stretch.UniformToFill:
                            sPlayer.API.SetPropertyString("panscan", "1.0");
                            break;

                        case Stretch.None:
                        case Stretch.Uniform:
                        default:
                            sPlayer.API.SetPropertyString("keepaspect", "yes");
                            return;
                    }
                }
            }
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
            mpv.SpeedRateChanged((double)e.NewValue);
        }

        private void SpeedRateChanged(double value)
        {
            if (sPlayer != null)
            {
                sPlayer.Speed = value;
            }
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
            mpv.LoopChanged((bool)e.NewValue);
        }

        private void LoopChanged(bool value)
        {
            if (sPlayer != null)
            {
                sPlayer.Loop = value;
            }
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
            mpv.VolumeChanged((int)e.NewValue);
        }

        private void VolumeChanged(int value)
        {
            if (sPlayer != null)
            {
                sPlayer.Volume = value;
            }
        }

        public TimeSpan Position
        {
            get
            {
                if (IsMediaLoaded())
                    return sPlayer.Position;
                else
                    return TimeSpan.Zero;
            }
            set
            {
                sPlayer.Position = value;
            }
        }

        public string ImageDuration
        {
            get
            {
                lock (mpvLock)
                {
                    return sPlayer.API.GetPropertyString("image-display-duration");
                }
            }
            set
            {
                lock (mpvLock)
                {
                    sPlayer.API.SetPropertyString("image-display-duration", value);
                }
            }
        }

        public MPVLibControl()
        {
            InitializeComponent();
            sPlayer = new MpvPlayer(PlayerHost.Handle);
            sPlayer.KeepOpen = KeepOpen.Always;
            sPlayer.MediaLoaded += Player_MediaLoaded;
            sPlayer.MediaFinished += Player_MediaFinished;
        }

        private void Player_MediaLoaded(object sender, System.EventArgs e)
        {
            if (FileLoadedEvent != null)
                FileLoadedEvent();
        }

        private void Player_MediaFinished(object sender, System.EventArgs e)
        {
            if (FileEndedEvent != null)
                FileEndedEvent();
        }

        public void Load(string fpath, bool append = false)
        {
            sPlayer.Load(fpath, !append);
        }

        public void AppendFiles(string[] pathes, bool append = false)
        {
            sPlayer.LoadPlaylist(pathes, !append);
        }

        void Dispose()
        {
            if (sPlayer != null)
            {
                sPlayer.Dispose();
            }
        }

        public void Play()
        {
            this.Visibility = Visibility.Visible;
            sPlayer.Resume();
        }

        public void Pause(bool hide = false)
        {
            sPlayer.Pause();

            if (hide)
                this.Visibility = Visibility.Collapsed;
        }

        public void Stop()
        {
            Pause(true);
            Position = new TimeSpan(0);
        }

        public void Close()
        {
            sPlayer.Stop();
        }

        public void LoadVideoByIndex(int index, bool preload = false)
        {
            sPlayer.API.SetPropertyLong("playlist-pos", index);
            if (AutoPlay)
                sPlayer.Resume();
            else
                Pause(preload);
        }

        public bool PlaylistNext()
        {
            return sPlayer.PlaylistNext();
        }

        public bool PlaylistPrevious()
        {
            return sPlayer.PlaylistPrevious();
        }

        public bool PlaylistRemove()
        {
            return sPlayer.PlaylistRemove();
        }

        public bool PlaylistRemove(int index)
        {
            return sPlayer.PlaylistRemove(index);
        }

        public bool PlaylistMove(int oldIndex, int newIndex)
        {
            return sPlayer.PlaylistMove(oldIndex, newIndex);
        }

        public void PlaylistClear()
        {
            sPlayer.PlaylistClear();
        }

        private void UserControl_Unloaded(object sender, RoutedEventArgs e)
        {
            Dispose();
        }
    }
}
