using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using TurtleTools;

namespace PageViewer
{
    /// <summary>
    /// ScrollTextControl.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class ScrollTextControl : UserControl
    {
        public enum MarqueeType
        {
            LeftToRight,
            RightToLeft,
            TopToBottom,
            BottomToTop
        }

        List<PreviewData> mData = new List<PreviewData>();

        int mCurrentScrollIdx = 0;

        DoubleAnimation AnimationForL2R = new DoubleAnimation();
        DoubleAnimation AnimationForR2L = new DoubleAnimation();
        DoubleAnimation AnimationForB2T = new DoubleAnimation();
        DoubleAnimation AnimationForT2B = new DoubleAnimation();

        public MarqueeType Marquee { get; set; }

        public double _marqueeTimeInSeconds;

        public double MarqueeTimeInSeconds
        {
            get { return _marqueeTimeInSeconds; }
            set { _marqueeTimeInSeconds = value; }
        }

        public ScrollTextControl()
        {
            InitializeComponent();
        }

        public void SetData(List<PreviewData> data)
        {
            mData = data;
            Marquee = MarqueeType.RightToLeft;
            InitAnimationEventHandler();
            InitData();
        }


        public void InitData()
        {
            if (mData.Count < 1)
                return;

            FontFamily fontFamily = new FontFamily(mData[0].FontName);
            double fontHeight = 0.0;

            for (int i = 15; i < 1000; i++)
            {
                fontHeight = Math.Ceiling(i * fontFamily.LineSpacing);
                if (this.Height < fontHeight)
                {
                    fontHeight = i - 3;
                    break;
                }
            }

            TextTBlock.FontSize = fontHeight;

            ContentCanvas.Width = this.Width;
            ContentCanvas.Height = this.Height;

            mCurrentScrollIdx = 0;    
            StopAnimation();

            _marqueeTimeInSeconds = 10;

            TextTBlock.Foreground = ColorTools.GetSolidColorBrushByHexString(string.IsNullOrEmpty(mData[0].FontColor) ? "#FFFFFFFF" : mData[0].FontColor);
            ContentCanvas.Background = ColorTools.GetSolidColorBrushByHexString(string.IsNullOrEmpty(mData[0].BGColor) ? "#FF000000" : mData[0].BGColor);
            TextTBlock.FontFamily = new FontFamily(mData[0].FontName);

            StartScrollText();
        }

        public void InitAnimationEventHandler()
        {
            AnimationForL2R.Completed += new EventHandler(AnimationForL2R_Completed);
            AnimationForR2L.Completed += new EventHandler(AnimationForL2R_Completed);
            AnimationForB2T.Completed += new EventHandler(AnimationForL2R_Completed);
            AnimationForT2B.Completed += new EventHandler(AnimationForL2R_Completed);
        }

        void AnimationForL2R_Completed(object sender, EventArgs e)
        {  
            StartScrollText();
        }
        
        public void StartMarqueeingForScrollTextList()
        {
            _marqueeTimeInSeconds = mData[mCurrentScrollIdx].Playtime;
            TextTBlock.Text = mData[mCurrentScrollIdx].TextContent;

            TextTBlock.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

            StopAnimation();

            switch (Marquee)
            {
                case MarqueeType.LeftToRight:
                    LeftToRightMarquee();
                    break;
                case MarqueeType.RightToLeft:
                    RightToLeftMarquee();
                    break;
                case MarqueeType.TopToBottom:
                    TopToBottomMarquee();
                    break;
                case MarqueeType.BottomToTop:
                    BottomToTopMarquee();
                    break;
                default:
                    break;
            }
        }

        public void StartMarqueeing(MarqueeType marqueeType)
        {
            if (marqueeType == MarqueeType.LeftToRight)
            {
                LeftToRightMarquee();
            }
            else if (marqueeType == MarqueeType.RightToLeft)
            {
                RightToLeftMarquee();
            }
            else if (marqueeType == MarqueeType.TopToBottom)
            {
                TopToBottomMarquee();
            }
            else if (marqueeType == MarqueeType.BottomToTop)
            {
                BottomToTopMarquee();
            }        
        }

        public void StopAnimation()
        { 
            DoubleAnimation doubleAnimation = new DoubleAnimation();

            doubleAnimation.FillBehavior = FillBehavior.HoldEnd;

            switch (Marquee)
            {
                case MarqueeType.LeftToRight:
                    TextTBlock.BeginAnimation(Canvas.LeftProperty, null);
                    break;
                case MarqueeType.RightToLeft:
                    TextTBlock.BeginAnimation(Canvas.RightProperty, null);
                    break;
                case MarqueeType.TopToBottom:
                    TextTBlock.BeginAnimation(Canvas.TopProperty, null);
                    break;
                case MarqueeType.BottomToTop:
                    TextTBlock.BeginAnimation(Canvas.BottomProperty, null);
                    break;
                default:
                    break;
            }         
        }

        public void StartScrollText()
        {
            if (mData.Count < 1)
                return;

            if (mCurrentScrollIdx > (mData.Count - 1))
                mCurrentScrollIdx = 0;

            TextTBlock.Text = mData[mCurrentScrollIdx].TextContent;
            StartMarqueeingForScrollTextList();
            mCurrentScrollIdx++;
        }

        public void LeftToRightMarquee()
        {
            Marquee = MarqueeType.LeftToRight;

            double height = ContentCanvas.Height - TextTBlock.DesiredSize.Height;
            TextTBlock.Margin = new Thickness(0, height / 2, 0, 0);
            AnimationForR2L.From = -TextTBlock.DesiredSize.Width;
            AnimationForR2L.To = ContentCanvas.Width;

            double st = (ContentCanvas.Width + TextTBlock.DesiredSize.Width) / (MarqueeTimeInSeconds * 5);
            AnimationForR2L.Duration = new Duration(TimeSpan.FromSeconds(st));

            TextTBlock.BeginAnimation(Canvas.LeftProperty, AnimationForL2R);
        }

        public void RightToLeftMarquee()
        {
            Marquee = MarqueeType.RightToLeft;

            double height = ContentCanvas.Height - TextTBlock.DesiredSize.Height;
            TextTBlock.Margin = new Thickness(0, height / 2, 0, 0);
            AnimationForR2L.From = -TextTBlock.DesiredSize.Width;
            AnimationForR2L.To = ContentCanvas.Width;

            double st = (ContentCanvas.Width + TextTBlock.DesiredSize.Width) / (MarqueeTimeInSeconds * 5);
            AnimationForR2L.Duration = new Duration(TimeSpan.FromSeconds(st));

            TextTBlock.BeginAnimation(Canvas.RightProperty, AnimationForR2L);
        }

        public void TopToBottomMarquee()
        {
            Marquee = MarqueeType.TopToBottom;

            double width = ContentCanvas.Width - TextTBlock.DesiredSize.Width;
            TextTBlock.Margin = new Thickness(width / 2, 0, 0, 0);
            AnimationForT2B.From = -TextTBlock.DesiredSize.Height;
            AnimationForT2B.To = ContentCanvas.Height;

            double st = (ContentCanvas.Height + TextTBlock.DesiredSize.Height) / (MarqueeTimeInSeconds * 5);
            AnimationForT2B.Duration = new Duration(TimeSpan.FromSeconds(st));

            TextTBlock.BeginAnimation(Canvas.TopProperty, AnimationForT2B);
        }

        public void BottomToTopMarquee()
        {
            Marquee = MarqueeType.BottomToTop;

            double width = ContentCanvas.Width - TextTBlock.DesiredSize.Width;
            TextTBlock.Margin = new Thickness(width / 2, 0, 0, 0);
            AnimationForB2T.From = -TextTBlock.DesiredSize.Height;
            AnimationForB2T.To = ContentCanvas.Height;

            double st = (ContentCanvas.Height + TextTBlock.DesiredSize.Height) / (MarqueeTimeInSeconds * 5);
            AnimationForB2T.Duration = new Duration(TimeSpan.FromSeconds(st));

            TextTBlock.BeginAnimation(Canvas.BottomProperty, AnimationForB2T);
        }
    }

}
