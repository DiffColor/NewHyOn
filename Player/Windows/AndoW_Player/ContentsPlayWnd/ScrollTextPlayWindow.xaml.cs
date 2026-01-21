using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using TurtleTools;
using SharedContentsInfoClass = AndoW.Shared.ContentsInfoClass;

namespace HyOnPlayer
{
    /// <summary>
    /// ScrollTextPlayWindow.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class ScrollTextPlayWindow : Window
    {
        public enum MarqueeType
        {
            LeftToRight,
            RightToLeft,
            TopToBottom,
            BottomToTop
        }

        //////////////////////////////////////////////
        // Common
        public string elementName = string.Empty;
        public string elementTypeName = string.Empty;
        public double width = 0;
        public double height = 0;
        public double posLeft = 0;
        public double posTop = 0;
        public double rotationAngle = 0;
        public int zindex = 0;

        //////////////////////////////////////////////
        // ScrollText
        public string scrollText = "This Text is Scroll.";

        public string ScrollTextfontName = "Tahoma";
        public double ScrollTextfontSize = 30;
        public bool ScrollTextIsBold = false;
        public bool ScrollTextIsItalic = false;
        public bool ScrollTextIsUnderLine = false;
        public bool ScrollTextIsNormal = true;

        public int scrollDirectionType = 2;
        public int scrollTextBgType = 1;

        public string ScrollTextfontColor = "#FFFFFFFF";
        public string ScrollTextbackgroundColor = "#FF000000";
        public bool ScrollTextIsBackGroundNull = true;

        MarqueeType _marqueeType;
        public double g_ActualTextWidth = 0;

        public ElementInfoClass g_ElementInfoClass = new ElementInfoClass();

        int g_CurrentScrollIdx = 0;

        DoubleAnimation AnimationForL2R = new DoubleAnimation();
        DoubleAnimation AnimationForR2L = new DoubleAnimation();
        DoubleAnimation AnimationForB2T = new DoubleAnimation();
        DoubleAnimation AnimationForT2B = new DoubleAnimation();

        public MarqueeType Type
        {
            get { return _marqueeType; }
            set { _marqueeType = value; }
        }

        public String MarqueeContent
        {
            set { MarqueeTBlock.Text = value; }
        }

        public double _marqueeTimeInSeconds;

        public double MarqueeTimeInSeconds
        {
            get { return _marqueeTimeInSeconds; }
            set { _marqueeTimeInSeconds = value; }
        }
     
        public ScrollTextPlayWindow()
        {
            InitializeComponent();

            InitAnimationEventHandler();
            InitializeThisElement();
        }

        public void InitAnimationEventHandler()
        {
            AnimationForL2R.Completed += new EventHandler(AnimationForL2R_Completed);
            AnimationForR2L.Completed += new EventHandler(AnimationForL2R_Completed);
            AnimationForB2T.Completed += new EventHandler(AnimationForL2R_Completed);
            AnimationForT2B.Completed += new EventHandler(AnimationForL2R_Completed);
        }

        void RemoveHandlers()
        {
            AnimationForL2R.Completed -= AnimationForL2R_Completed;
            AnimationForR2L.Completed -= AnimationForL2R_Completed;
            AnimationForB2T.Completed -= AnimationForL2R_Completed;
            AnimationForT2B.Completed -= AnimationForL2R_Completed;
        }

        void AnimationForL2R_Completed(object sender, EventArgs e)
        {
            StartScrollText();
        }

        public void InitializeThisElement()
        {
            Type = MarqueeType.RightToLeft;
        }

        void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            CanvMain.Height = this.ActualHeight;
            CanvMain.Width = this.ActualWidth;
            g_CurrentScrollIdx = 0;
            StartScrollText();
        }

        public void UpdateScrollTextList(ElementInfoClass paramCls, double paramScaledHeight)
        {

            this.g_ElementInfoClass.CopyData(paramCls);

            if (g_ElementInfoClass.EIF_ContentsInfoClassList.Count > 0)
            {
                if (ColorTools.IsHexColorString(g_ElementInfoClass.EIF_ContentsInfoClassList[0].CIF_PlayMinute))
                {
                    ScrollTextfontColor = g_ElementInfoClass.EIF_ContentsInfoClassList[0].CIF_PlayMinute;
                }

                if (g_ElementInfoClass.EIF_ContentsInfoClassList[0].CIF_ContentType != string.Empty)
                {
                    ScrollTextfontName = g_ElementInfoClass.EIF_ContentsInfoClassList[0].CIF_ContentType;
                }

                if (ColorTools.IsHexColorString(g_ElementInfoClass.EIF_ContentsInfoClassList[0].CIF_PlaySec))
                {
                    ScrollTextbackgroundColor = g_ElementInfoClass.EIF_ContentsInfoClassList[0].CIF_PlaySec;
                }

                FontFamily fontFamily = new FontFamily(ScrollTextfontName);
                double fontHeight = 0.0;

                for (int i = 15; i < 1000; i++)
                {
                    fontHeight = Math.Ceiling(i * fontFamily.LineSpacing);
                    if (paramScaledHeight < fontHeight)
                    {
                        fontHeight = i - 3;
                        break;
                    }
                }

                ScrollTextfontSize = fontHeight;
                MarqueeTBlock.FontSize = fontHeight;
                //CanvMain.Margin = new Thickness(0, fontHeight / 12, 0, 0);    // set textmargin on canvas
            }

            g_CurrentScrollIdx = 0;
            StopAnimation();

            _marqueeTimeInSeconds = 10;

            MarqueeTBlock.Foreground = ColorTools.GetSolidColorBrushByHexString(ScrollTextfontColor);
            ContentGrid.Background = ColorTools.GetSolidColorBrushByHexString(ScrollTextbackgroundColor);
            MarqueeTBlock.FontFamily = new FontFamily(ScrollTextfontName);

            StartScrollText();
        }

        public void StartMarqueeingForScrollTextList(SharedContentsInfoClass paramCls)
        {
            _marqueeTimeInSeconds = paramCls.CIF_ScrollTextSpeedSec;
            MarqueeContent = paramCls.CIF_FileName;
            this.UpdateLayout();
            StopAnimation();
            switch (_marqueeType)
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

        public void StopAnimation()
        {
            DoubleAnimation doubleAnimation = new DoubleAnimation();

            doubleAnimation.FillBehavior = FillBehavior.HoldEnd;

            switch (Type)
            {
                case MarqueeType.LeftToRight:
                    MarqueeTBlock.BeginAnimation(Canvas.LeftProperty, null);
                    break;
                case MarqueeType.RightToLeft:
                    MarqueeTBlock.BeginAnimation(Canvas.RightProperty, null);
                    break;
                case MarqueeType.TopToBottom:
                    MarqueeTBlock.BeginAnimation(Canvas.TopProperty, null);
                    break;
                case MarqueeType.BottomToTop:
                    MarqueeTBlock.BeginAnimation(Canvas.BottomProperty, null);
                    break;
                default:
                    break;
            }
        }

        public void StartScrollText()
        {
            if (this.g_ElementInfoClass.EIF_ContentsInfoClassList.Count > 0)
            {
                if (g_CurrentScrollIdx > (g_ElementInfoClass.EIF_ContentsInfoClassList.Count - 1))
                {
                    g_CurrentScrollIdx = 0;
                }

                MarqueeTBlock.FontFamily = new FontFamily(g_ElementInfoClass.EIF_ContentsInfoClassList[g_CurrentScrollIdx].CIF_ContentType);

                MarqueeTBlock.Text = this.g_ElementInfoClass.EIF_ContentsInfoClassList[g_CurrentScrollIdx].CIF_FileName;
                StartMarqueeingForScrollTextList(this.g_ElementInfoClass.EIF_ContentsInfoClassList[g_CurrentScrollIdx]);
                g_CurrentScrollIdx++;
            }
        }

        public void LeftToRightMarquee()
        {
            Type = MarqueeType.LeftToRight;

            double height = CanvMain.ActualHeight - MarqueeTBlock.ActualHeight;
            MarqueeTBlock.Margin = new Thickness(0, height / 2, 0, 0);
            AnimationForL2R.From = -MarqueeTBlock.ActualWidth;
            AnimationForL2R.To = CanvMain.ActualWidth;

            double st = (CanvMain.ActualWidth + MarqueeTBlock.ActualWidth) / (MarqueeTimeInSeconds * 14);
            AnimationForR2L.Duration = new Duration(TimeSpan.FromSeconds(st));

            MarqueeTBlock.BeginAnimation(Canvas.LeftProperty, AnimationForL2R);
        }

        public void RightToLeftMarquee()
        {
            Type = MarqueeType.RightToLeft;

            double height = CanvMain.ActualHeight - MarqueeTBlock.ActualHeight;
            MarqueeTBlock.Margin = new Thickness(0, height / 2, 0, 0);
            AnimationForR2L.From = -MarqueeTBlock.ActualWidth;
            AnimationForR2L.To = CanvMain.ActualWidth;

            double st = (CanvMain.ActualWidth + MarqueeTBlock.ActualWidth) / (MarqueeTimeInSeconds * 14);
            AnimationForR2L.Duration = new Duration(TimeSpan.FromSeconds(st));

            MarqueeTBlock.BeginAnimation(Canvas.RightProperty, AnimationForR2L);
        }

        public void TopToBottomMarquee()
        {
            Type = MarqueeType.TopToBottom;

            double width = CanvMain.ActualWidth - MarqueeTBlock.ActualWidth;
            MarqueeTBlock.Margin = new Thickness(width / 2, 0, 0, 0);
            AnimationForT2B.From = -MarqueeTBlock.ActualHeight;
            AnimationForT2B.To = CanvMain.ActualHeight;

            double st = (CanvMain.ActualHeight + MarqueeTBlock.ActualHeight) / (MarqueeTimeInSeconds * 14);
            AnimationForT2B.Duration = new Duration(TimeSpan.FromSeconds(st));

            MarqueeTBlock.BeginAnimation(Canvas.TopProperty, AnimationForT2B);
        }

        public void BottomToTopMarquee()
        {
            Type = MarqueeType.BottomToTop;

            double width = CanvMain.ActualWidth - MarqueeTBlock.ActualWidth;
            MarqueeTBlock.Margin = new Thickness(width / 2, 0, 0, 0);
            AnimationForB2T.From = -MarqueeTBlock.ActualHeight;
            AnimationForB2T.To = CanvMain.ActualHeight;

            double st = (CanvMain.ActualHeight + MarqueeTBlock.ActualHeight) / (MarqueeTimeInSeconds * 14);
            AnimationForB2T.Duration = new Duration(TimeSpan.FromSeconds(st));
            
            MarqueeTBlock.BeginAnimation(Canvas.BottomProperty, AnimationForB2T);
        }
      
        void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            StopAnimation();
            RemoveHandlers();
        }
    }
}
