using System;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using TransitionEffects;
using TurtleTools;

namespace TurtleMediaControl
{
    public enum ASPECT_RATIO_TYPES
    {
        Maintain,
        DependOnOwner 
    }

    public enum TRANSITION_EFFECTS
    {
        None = -2,
        Random,
        Shrink,
        Blinds,
        CloudReveal,
        RandomCircleReveal,
        Fade,
        Wave,
        RadialWigle,
        Blood,
        CircleStretch,
        Disolve,
        DropFade,
        RotateCrumble,
        Water,
        RadialBlur,
        CircularBlur,
        Pixelate,
        PixelateIn,
        PixelateOut,
        SwirGrid1,
        SwirGrid2,
        SmoothSwirlGrid1,
        SmoothSwirlGrid2,
        SmoothSwirlGrid3,
        SmoothSwirlGrid4,
        MostBright,
        LeastBright,
        Saturate,
        BandedSwir1,
        BandedSwir2, 
        BandedSwir3,
        CircleReveal1,
        CircleReveal2,
        CircleReveal3,
        LineReveal1,
        LineReveal2,
        LineReveal3,
        LineReveal4,
        LineReveal5,
        LineReveal6,
        LineReveal7,
        LineReveal8,
        Ripple1,
        Ripple2,
        Ripple3,
        Ripple4,
        Ripple5,
        SlideIn1,
        SlideIn2,
        SlideIn3,
        SlideIn4,
        Swirl1,
        Swirl2,
        Swirl3,
        Swirl4
    }


    /// <summary>
    /// DirectShowControl.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class TransImageControl : UserControl
    {

        #region Transition Effect
        private TransitionEffect[] transitionEffect = new TransitionEffect[]
        {
            new ShrinkTransitionEffect(), 
            new BlindsTransitionEffect(),
            new CloudRevealTransitionEffect(),
            new RandomCircleRevealTransitionEffect(),
            new FadeTransitionEffect(),
            new WaveTransitionEffect(),
            new RadialWiggleTransitionEffect(),
            new BloodTransitionEffect(),
            new CircleStretchTransitionEffect(),
            new DisolveTransitionEffect(),
            new DropFadeTransitionEffect(),   
            new RotateCrumbleTransitionEffect(),
            new WaterTransitionEffect(),
            new RadialBlurTransitionEffect(),
            new CircularBlurTransitionEffect(),
            new PixelateTransitionEffect(),
            new PixelateInTransitionEffect(),
            new PixelateOutTransitionEffect(),
            new SwirlGridTransitionEffect(Math.PI * 4), 
            new SwirlGridTransitionEffect(Math.PI * 16),
            new SmoothSwirlGridTransitionEffect(Math.PI * 4),
            new SmoothSwirlGridTransitionEffect(Math.PI * 16),
            new SmoothSwirlGridTransitionEffect(-Math.PI * 8),
            new SmoothSwirlGridTransitionEffect(-Math.PI * 6),
            new MostBrightTransitionEffect(),
            new LeastBrightTransitionEffect(),
            new SaturateTransitionEffect(),
            new BandedSwirlTransitionEffect(Math.PI / 5.0, 50.0),
            new BandedSwirlTransitionEffect(Math.PI, 10.0),
            new BandedSwirlTransitionEffect(-Math.PI, 10.0),
            new CircleRevealTransitionEffect { FuzzyAmount= 0.0} , 
            new CircleRevealTransitionEffect { FuzzyAmount= 0.1},
            new CircleRevealTransitionEffect { FuzzyAmount=0.5},
            new LineRevealTransitionEffect{LineOrigin= new Vector(-0.2, -0.2), LineNormal = new Vector(1, 0), LineOffset= new Vector(1.4, 0), FuzzyAmount = 0.2},
            new LineRevealTransitionEffect{LineOrigin= new Vector(1.2, -0.2), LineNormal = new Vector(-1, 0), LineOffset= new Vector(-1.4, 0), FuzzyAmount = 0.2},
            new LineRevealTransitionEffect{LineOrigin= new Vector(-.2, -0.2), LineNormal = new Vector(0, 1), LineOffset= new Vector(0, 1.4), FuzzyAmount = 0.2},
            new LineRevealTransitionEffect{LineOrigin= new Vector(-0.2, 1.2), LineNormal = new Vector(0, -1), LineOffset= new Vector(0, -1.4), FuzzyAmount = 0.2},
            new LineRevealTransitionEffect{LineOrigin= new Vector(-0.2, -0.2), LineNormal = new Vector(1, 1), LineOffset= new Vector(1.4, 1.4), FuzzyAmount = 0.2},
            new LineRevealTransitionEffect{LineOrigin= new Vector(1.2, 1.2), LineNormal = new Vector(-1, -1), LineOffset= new Vector(-1.4, -1.4), FuzzyAmount = 0.2},
            new LineRevealTransitionEffect{LineOrigin= new Vector(1.2, -0.2), LineNormal = new Vector(-1, 1), LineOffset= new Vector(-1.4, 1.4), FuzzyAmount = 0.2},
            new LineRevealTransitionEffect{LineOrigin= new Vector(-0.2, 1.2), LineNormal = new Vector(1, -1), LineOffset= new Vector(1.4, -1.4), FuzzyAmount = 0.2},
            new RippleTransitionEffect(),
            new RippleTransitionEffect(),
            new RippleTransitionEffect(),
            new RippleTransitionEffect(),
            new RippleTransitionEffect(),
            new SlideInTransitionEffect{ SlideAmount= new Vector(1, 0)},
            new SlideInTransitionEffect{ SlideAmount=new Vector(0, 1)},
            new SlideInTransitionEffect{ SlideAmount=new Vector(-1, 0)},
            new SlideInTransitionEffect{ SlideAmount=new Vector(0, -1)},
            new SwirlTransitionEffect(Math.PI * 4),
            new SwirlTransitionEffect(-Math.PI * 4),
            new SwirlTransitionEffect(Math.PI * 4),
            new SwirlTransitionEffect(-Math.PI * 4)
        };
        #endregion

        BitmapSource oldBitmapSource;
        BitmapSource currentBitmapSource;

        #region dependency properties

        public string DirectSource
        {
            set
            {
                SetValue(DirectSourceProperty, value);
            }

            get
            {
                return (string)GetValue(DirectSourceProperty);
            }
        }

        public string Source
        {
            set
            {
                SetValue(SourceProperty, value);
            }

            get
            {
                return (string)GetValue(SourceProperty);
            }
        }

        public string ChangeNowSource
        {
            set
            {
                SetValue(ChangeNowSourceProperty, value);
            }

            get
            {
                return (string)GetValue(ChangeNowSourceProperty);
            }
        }
        
        public ASPECT_RATIO_TYPES AspectRatioMode
        {
            set
            {
                SetValue(AspectRatioModeProperty, value);
            }

            get
            {
                return (ASPECT_RATIO_TYPES)GetValue(AspectRatioModeProperty);
            }
        }

        public TRANSITION_EFFECTS ChangeEffect
        {
            set
            {
                SetValue(ChangeEffectProperty, value);
            }

            get
            {
                return (TRANSITION_EFFECTS)GetValue(ChangeEffectProperty);
            }
        }

        //public double DirectionDegree
        //{
        //    set
        //    {
        //        SetValue(DirectionDegreeProperty, value);
        //    }

        //    get
        //    {
        //        return (double)GetValue(DirectionDegreeProperty);
        //    }
        //}

        public double Duration
        {
            set
            {
                SetValue(DurationProperty, value);
            }

            get
            {
                return (double)GetValue(DurationProperty);
            }
        }

        public double AccelRatio
        {
            set
            {
                if (value >= 1.0)
                    value = 0.9;
                SetValue(AccelRatioProperty, value);
            }

            get
            {
                return (double)GetValue(AccelRatioProperty);
            }
        }

        //public double DeaccelRatio
        //{
        //    set
        //    {
        //        SetValue(DeaccelRatioProperty, value);
        //    }

        //    get
        //    {
        //        return (double)GetValue(DeaccelRatioProperty);
        //    }
        //}

        //public double Delay
        //{
        //    set
        //    {
        //        SetValue(DelayProperty, value);
        //    }

        //    get
        //    {
        //        return (double)GetValue(DelayProperty);
        //    }
        //}

        public static readonly DependencyProperty DirectSourceProperty = DependencyProperty.Register
            (
                "DirectSource",
                typeof(string),
                typeof(TransImageControl),
                new PropertyMetadata(null, OnDirectSourceChanged)
            );

        public static readonly DependencyProperty SourceProperty = DependencyProperty.Register
            (
                "Source",
                typeof(string),
                typeof(TransImageControl),
                new PropertyMetadata(null, OnSourceChanged)
            );

        public static readonly DependencyProperty ChangeNowSourceProperty = DependencyProperty.Register
            (
                "ChangeNowSource",
                typeof(string),
                typeof(TransImageControl),
                new PropertyMetadata(null, OnChangeNowSourceChanged)
            );

        public static readonly DependencyProperty AspectRatioModeProperty = DependencyProperty.Register
            (
                "AspectRatioMode",
                typeof(ASPECT_RATIO_TYPES),
                typeof(TransImageControl),
                new PropertyMetadata(ASPECT_RATIO_TYPES.Maintain, OnAspectRatioModeChanged)
            );

        public static readonly DependencyProperty ChangeEffectProperty = DependencyProperty.Register
            (
                "ChangeEffect",
                typeof(TRANSITION_EFFECTS),
                typeof(TransImageControl),
                new PropertyMetadata(TRANSITION_EFFECTS.None, OnChangeEffectChanged)
            );

        //public static readonly DependencyProperty DirectionDegreeProperty = DependencyProperty.Register
        //    (
        //        "DirectionDegree", 
        //        typeof(double), 
        //        typeof(TransitionImage),
        //        new PropertyMetadata(0.0, OnDirectionDegreeChanged)
        //    );

        public static readonly DependencyProperty DurationProperty = DependencyProperty.Register
            (
                "Duration", typeof(double),
                typeof(TransImageControl),
                new PropertyMetadata(1.0, OnDurationChanged)
            );

        public static readonly DependencyProperty AccelRatioProperty = DependencyProperty.Register
            (
                "AccelRatio", typeof(double),
                typeof(TransImageControl),
                new PropertyMetadata(0.5, OnAccelRatioChanged)
            );

        //public static readonly DependencyProperty DeaccelRatioProperty = DependencyProperty.Register
        //    (
        //        "DeaccelRatio", typeof(double),
        //        typeof(TransitionImage),
        //        new PropertyMetadata(0.5, OnDeaccelRatioChanged)
        //    );

        //public static readonly DependencyProperty DelayProperty = DependencyProperty.Register
        //    (
        //        "Delay", typeof(double), 
        //        typeof(TransitionImage),
        //        new PropertyMetadata(0.0, OnDelayChanged)
        //    );

        private static void OnDirectSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            TransImageControl dsp = d as TransImageControl;
            dsp.DirectSourceChanged((string)e.NewValue);
        }

        private void DirectSourceChanged(string source)
        {
            if (string.IsNullOrWhiteSpace(source))
                return;

            if(Path.GetExtension(source).Equals(".png", StringComparison.CurrentCultureIgnoreCase))
                RootGrid.Background = new SolidColorBrush(Colors.Transparent);
            else
                RootGrid.Background = new SolidColorBrush(Colors.Black);

            currentChild.Stretch = AspectRatioMode == ASPECT_RATIO_TYPES.Maintain ? Stretch.Uniform : Stretch.Fill;

            if (MediaTools.IsValidUrlContent(source))
                MediaTools.DisplayImage(currentChild, source);
            else
                currentChild.Source = MediaTools.GetBitmapSourceFromFile(DirectSource, longside);

            // DirectSource 경로에서도 현재 비트맵 상태를 갱신해 다음 전환에서 이전 이미지가 남지 않도록 한다.
            currentBitmapSource = currentChild.Source as BitmapSource;
            currentPath = source;
        }
        
        private static void OnSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            TransImageControl dsp = d as TransImageControl;
            dsp.SourceChanged((string)e.NewValue);
        }

        private void SourceChanged(string source)
        {
            if (File.Exists(source) == false) 
                return;

            RootGrid.Background = new SolidColorBrush(Colors.Black);
            currentChild.Stretch = Stretch.Fill;

            ASPECT_RATIO_TYPES type = AspectRatioMode;
            
            if (currentBitmapSource == null)
            {
                SetCurrentSource(source, type);
                NextWithEffect();
                return;
            }

            oldBitmapSource = null;
            oldBitmapSource = currentBitmapSource;

            currentBitmapSource = null;

            ThreadPool.QueueUserWorkItem(new WaitCallback(delegate(object state)
            {
                SetCurrentSource(source, type);
            }));
        }

        void SetCurrentSource(string fpath, ASPECT_RATIO_TYPES type)
        {
            if (type == ASPECT_RATIO_TYPES.Maintain)
                currentBitmapSource = MediaTools.FixedSizeBitmapSource(fpath, (int)this.ActualWidth, (int)this.ActualHeight);
            else
                currentBitmapSource = MediaTools.GetBitmapSourceFromFile(fpath, longside);
        }

        void SetCurrentSource(BitmapSource source, ASPECT_RATIO_TYPES type)
        {
            if (type == ASPECT_RATIO_TYPES.Maintain)
                currentBitmapSource = MediaTools.FixedSizeBitmapSource(source, (int)this.ActualWidth, (int)this.ActualHeight);
            else
                currentBitmapSource = source;
        }

        private static void OnChangeNowSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            TransImageControl dsp = d as TransImageControl;
            dsp.ChangeNowSourceChanged((string)e.NewValue);
        }

        private void ChangeNowSourceChanged(string source)
        {
            if (File.Exists(source) == false)
                return;

            RootGrid.Background = new SolidColorBrush(Colors.Black);
            currentChild.Stretch = Stretch.Fill;

            ASPECT_RATIO_TYPES type = AspectRatioMode;

            if (currentBitmapSource == null)
            {
                SetCurrentSource(source, type);
                NextWithEffect();
                return;
            }

            oldBitmapSource = null;
            oldBitmapSource = currentBitmapSource;

            currentBitmapSource = null;

            ThreadPool.QueueUserWorkItem(new WaitCallback(delegate(object state)
            {
                SetCurrentSource(source, type);
                Dispatcher.BeginInvoke(DispatcherPriority.Normal,
                           new Action(delegate()
                           {
                               NextWithEffect();
                           }));
            }));
        }

        public void DirectChangeNowSource(BitmapSource source)
        {
            if (source == null)
                return;

            RootGrid.Background = new SolidColorBrush(Colors.Black);
            currentChild.Stretch = Stretch.Fill;

            ASPECT_RATIO_TYPES type = AspectRatioMode;

            if (currentBitmapSource == null)
            {
                SetCurrentSource(source, type);
                NextWithEffect();
                return;
            }

            oldBitmapSource = null;
            oldBitmapSource = currentBitmapSource;

            currentBitmapSource = null;

            ThreadPool.QueueUserWorkItem(new WaitCallback(delegate (object state)
            {
                SetCurrentSource(source, type);
                Dispatcher.BeginInvoke(DispatcherPriority.Normal,
                           new Action(delegate ()
                           {
                               NextWithEffect();
                           }));
            }));
        }

        private static void OnAspectRatioModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            TransImageControl dsp = d as TransImageControl;
            dsp.AspectRatioModeChanged((ASPECT_RATIO_TYPES)e.NewValue);
        }
        protected virtual void AspectRatioModeChanged(ASPECT_RATIO_TYPES aspectRatioType)
        {
            AspectRatioMode = aspectRatioType;
        }

        private static void OnChangeEffectChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            TransImageControl dsp = d as TransImageControl;
            dsp.ChangeEffectChanged((TRANSITION_EFFECTS)e.NewValue);
        }
        protected virtual void ChangeEffectChanged(TRANSITION_EFFECTS effect)
        {
            ChangeEffect = effect;
        }

        //private static void OnDirectionDegreeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        //{
        //    TransitionImage dsp = d as TransitionImage;
        //    dsp.DirectionDegreeChanged((double)e.NewValue);
        //}
        //protected virtual void DirectionDegreeChanged(double loadedBehaviorType)
        //{
        //    DirectionDegree = loadedBehaviorType;
        //}


        private static void OnDurationChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            TransImageControl dsp = d as TransImageControl;
            dsp.DurationChanged((double)e.NewValue);
        }

        protected virtual void DurationChanged(double duration)
        {
            Duration = duration;
        }

        private static void OnAccelRatioChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            TransImageControl dsp = d as TransImageControl;
            dsp.AccelRatioChanged((double)e.NewValue);
        }

        protected virtual void AccelRatioChanged(double accelratio)
        {
            AccelRatio = accelratio;
        }

        //private static void OnDeaccelRatioChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        //{
        //    TransitionImage dsp = d as TransitionImage;
        //    dsp.DeaccelRatioChanged((double)e.NewValue);
        //}

        //protected virtual void DeaccelRatioChanged(double deaccelratio)
        //{
        //    DeaccelRatio = deaccelratio;
        //}

        //private static void OnDelayChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        //{
        //    TransitionImage dsp = d as TransitionImage;
        //    dsp.DelayChanged((double)e.NewValue);
        //}

        //protected virtual void DelayChanged(double delay)
        //{
        //    Delay = delay;
        //}
        #endregion


        Random random = new Random(Guid.NewGuid().GetHashCode());
        string currentPath = null;

        public TransImageControl()
        {
            InitializeComponent();
        }

        public void NextWithEffect() {

            if (currentBitmapSource == null) { 
                return;
            }

            if (oldBitmapSource != null)
            {
                if (oldChild.Source == oldBitmapSource)
                    if (oldBitmapSource != currentBitmapSource)
                        oldBitmapSource = currentBitmapSource;
            }

            oldChild.Source = oldBitmapSource;
            currentChild.Source = currentBitmapSource;

            DoubleAnimation da = new DoubleAnimation(0.0, 1.0, new Duration(TimeSpan.FromSeconds(Duration)), FillBehavior.HoldEnd);
            da.AccelerationRatio = AccelRatio;
            da.DecelerationRatio = 1.0 - AccelRatio;
            da.Completed += new EventHandler(this.TransitionCompleted);

            VisualBrush vb = new VisualBrush(this);
            vb.Viewbox = new Rect(0, 0, this.ActualWidth, this.ActualHeight);
            vb.ViewboxUnits = BrushMappingMode.Absolute;

            TransitionEffect effect = null;

            if (ChangeEffect != TRANSITION_EFFECTS.None)
            {
                Array values = Enum.GetValues(typeof(TRANSITION_EFFECTS));

                if(ChangeEffect == TRANSITION_EFFECTS.Random)
                    effect = transitionEffect[random.Next(values.Length - 3)];
                else 
                    effect = transitionEffect[Array.IndexOf(values, ChangeEffect)];

                effect.BeginAnimation(TransitionEffect.ProgressProperty, da);
                effect.OldImage = vb;
            }

            this.currentChild.Effect = effect;

            currentPath = Source;
        }

        private void TransitionCompleted(object sender, EventArgs e)
        {
            currentChild.Effect = null;
            ReleaseMemory();
        }

        private void TransitionImage_Unloaded(object sender, RoutedEventArgs e)
        {
            Init();
        }

        double longside = 0.0;
        private void TransitionImage_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            longside = e.NewSize.Width > e.NewSize.Height ? e.NewSize.Width : e.NewSize.Height;

            if (string.IsNullOrEmpty(DirectSource) == false)
            {
                if (MediaTools.IsValidUrlContent(DirectSource))
                    MediaTools.DisplayImageByURL(currentChild, DirectSource);
                else
                    currentChild.Source = MediaTools.GetBitmapSourceFromFile(DirectSource, longside);

                return;
            }

            if (string.IsNullOrEmpty(currentPath))
                return;
            
            string fpath = Source;

            ASPECT_RATIO_TYPES type = AspectRatioMode;

            if (type == ASPECT_RATIO_TYPES.Maintain)
                currentChild.Source = oldBitmapSource = MediaTools.FixedSizeBitmapSource(currentPath, (int)e.NewSize.Width, (int)e.NewSize.Height);
            else
                currentChild.Source = oldBitmapSource = MediaTools.GetBitmapSourceFromFile(currentPath, longside);

            if (currentPath != Source)
            {
                ThreadPool.QueueUserWorkItem(new WaitCallback(delegate(object state)
                {
                    SetCurrentSource(fpath, type);
                }));
            }
        }

        void ReleaseMemory()
        {
            RootGrid.UpdateLayout();
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

        public void Init()
        {
            currentBitmapSource = null;
            oldBitmapSource = null;

            currentChild.Source = null;
            oldChild.Source = null;

            ReleaseMemory();
        }
    }
}
