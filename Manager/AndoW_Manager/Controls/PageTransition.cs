using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace WpfPageTransitions
{
    public enum PageTransitionType
    {
        Fade,
        Slide,
        SlideAndFade,
        Grow,
        GrowAndFade,
        Flip,
        FlipAndFade,
        Spin,
        SpinAndFade
    }

    /// <summary>
    /// Lightweight PageTransition control that cross-fades between contents.
    /// </summary>
    public class PageTransition : Grid
    {
        public static readonly DependencyProperty TransitionDurationProperty =
            DependencyProperty.Register(
                nameof(TransitionDuration),
                typeof(TimeSpan),
                typeof(PageTransition),
                new PropertyMetadata(TimeSpan.FromMilliseconds(120)));

        public static readonly DependencyProperty TransitionTypeProperty =
            DependencyProperty.Register(
                nameof(TransitionType),
                typeof(PageTransitionType),
                typeof(PageTransition),
                new PropertyMetadata(PageTransitionType.Fade));

        private readonly ContentPresenter _currentPresenter;
        private readonly ContentPresenter _previousPresenter;
        private bool _isLoaded;

        public TimeSpan TransitionDuration
        {
            get => (TimeSpan)GetValue(TransitionDurationProperty);
            set => SetValue(TransitionDurationProperty, value);
        }

        public PageTransitionType TransitionType
        {
            get => (PageTransitionType)GetValue(TransitionTypeProperty);
            set => SetValue(TransitionTypeProperty, value);
        }

        public PageTransition()
        {
            ClipToBounds = true;

            _previousPresenter = new ContentPresenter
            {
                Opacity = 0
            };

            _currentPresenter = new ContentPresenter
            {
                Opacity = 1
            };

            Children.Add(_previousPresenter);
            Children.Add(_currentPresenter);

            Loaded += (_, __) => _isLoaded = true;
            Unloaded += (_, __) => _isLoaded = false;
        }

        public void ShowPage(object content)
        {
            if (content == null)
            {
                return;
            }

            if (!_isLoaded || _currentPresenter.Content == null)
            {
                _currentPresenter.Content = content;
                _currentPresenter.Opacity = 1;
                _previousPresenter.Content = null;
                return;
            }

            _previousPresenter.Content = _currentPresenter.Content;
            _previousPresenter.Opacity = 1;

            _currentPresenter.Content = content;
            _currentPresenter.Opacity = 0;

            var fadeIn = CreateAnimation(0, 1);
            var fadeOut = CreateAnimation(1, 0);
            fadeOut.Completed += (_, __) => _previousPresenter.Content = null;

            _currentPresenter.BeginAnimation(OpacityProperty, fadeIn);
            _previousPresenter.BeginAnimation(OpacityProperty, fadeOut);
        }

        private DoubleAnimation CreateAnimation(double from, double to)
        {
            var duration = TransitionDuration;
            if (duration <= TimeSpan.Zero)
            {
                duration = TimeSpan.FromMilliseconds(10);
            }

            return new DoubleAnimation
            {
                From = from,
                To = to,
                Duration = new Duration(duration),
                EasingFunction = new QuadraticEase()
            };
        }
    }
}
