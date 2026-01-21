using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using TurtleTools;

namespace AndoW_Manager
{
    /// <summary>
    /// ScrollTextElement4.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class ScrollTextElement4 : UserControl
    {
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
        public double scrollTime = 10;

        public string ScrollTextfontName = "Malgun Gothic";
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
        //
        ///////////////////////////////////////////////////////////////////

        public MarqueeType _marqueeType;
        public double g_ActualTextWidth = 0;

        PagePreviewWindow parentPage = null;

        public ElementInfoClass g_ElementInfoClass = new ElementInfoClass();

        int g_CurrentScrollIdx = 0;

        DoubleAnimation AnimationForL2R = new DoubleAnimation();
        DoubleAnimation AnimationForR2L = new DoubleAnimation();
        DoubleAnimation AnimationForB2T = new DoubleAnimation();
        DoubleAnimation AnimationForT2B = new DoubleAnimation();

        public MarqueeType MarqueeType
        {
            get { return _marqueeType; }
            set { _marqueeType = value; }
        }       

        public String MarqueeContent
        {
            set { tbmarquee.Text = value; }
            //set { tbmarquee.Content = value; }
        }

        public double _marqueeTimeInSeconds;

        public double MarqueeTimeInSeconds
        {
            get { return _marqueeTimeInSeconds; }
            set { _marqueeTimeInSeconds = value; }
        }

        public ScrollTextElement4()
        {
            InitializeComponent();
            canMain.Height = this.Height;
            canMain.Width = this.Width;
            InitAnimationEventHandler();
            InitializeThisElement();
        }

        public ScrollTextElement4(double height, double width)
        {
            InitializeComponent();
            canMain.Height = height;
            canMain.Width = width;
            InitAnimationEventHandler();
            InitializeThisElement();
        }

        public ScrollTextElement4(PagePreviewWindow page)
        {
            InitializeComponent();
            parentPage = page;
            canMain.Height = this.Height;
            canMain.Width = this.Width;
            InitAnimationEventHandler();
            InitializeThisElement();
        }

        public ScrollTextElement4(double height, double width, PagePreviewWindow page)
        {
            InitializeComponent();
            parentPage = page;
            canMain.Height = height;
            canMain.Width = width;
            InitAnimationEventHandler();
            InitializeThisElement();

        }

        public void InitAnimationEventHandler()
        {
            AnimationForL2R.Completed += new EventHandler(AnimationForL2R_Completed);
            AnimationForR2L.Completed += new EventHandler(AnimationForL2R_Completed);
            AnimationForB2T.Completed += new EventHandler(AnimationForL2R_Completed);
            AnimationForT2B.Completed += new EventHandler(AnimationForL2R_Completed);

            //ScrollTextInfoClass tmeInfo = new ScrollTextInfoClass();
            //tmeInfo.scrollTextStr = "Hello, This is ScrollText.";
            //tmeInfo.scrollIndex = 1;
            //g_ScrollTextInfoList.Add(tmeInfo);

            //tmeInfo = new ScrollTextInfoClass();
            //tmeInfo.scrollTextStr = "Thank you For Using ShopPD!";
            //tmeInfo.scrollIndex = 2;
            //g_ScrollTextInfoList.Add(tmeInfo);
        }

        void AnimationForL2R_Completed(object sender, EventArgs e)
        {  
            StartScrollText();
        }


        public void InitializeThisElement()
        { 
            this.Loaded += new RoutedEventHandler(MarqueeText_Loaded);
            //this.SizeChanged += new SizeChangedEventHandler(MarqueeText_SizeChanged);

            _marqueeType = MarqueeType.RightToLeft;
            this.canMain.PreviewMouseLeftButtonDown += new MouseButtonEventHandler(FlashAndWebElement_PreviewMouseLeftButtonDown);
            MoveTopRectangle.PreviewMouseLeftButtonDown += new MouseButtonEventHandler(MoveTopRectangle_PreviewMouseLeftButtonDown);
            MoveBottomRectangle.PreviewMouseLeftButtonDown += new MouseButtonEventHandler(MoveBottomRectangle_PreviewMouseLeftButtonDown);
            this.ResizeRectangle.PreviewMouseLeftButtonDown += new MouseButtonEventHandler(ResizeRectangle_PreviewMouseLeftButtonDown);
            ExitRectangle.PreviewMouseDown += new MouseButtonEventHandler(ExitRectangle_PreviewMouseDown);

            //MenuBringToFront.Click += new RoutedEventHandler(MenuBringToFront_Click);
            //MenuBringToBack.Click += new RoutedEventHandler(MenuBringToBack_Click);

            exitPath1.PreviewMouseDown += new MouseButtonEventHandler(ExitRectangle_PreviewMouseDown);
            exitPath2.PreviewMouseDown += new MouseButtonEventHandler(ExitRectangle_PreviewMouseDown);
            //MenuBringToFrontOneStep.Click += new RoutedEventHandler(MenuBringToFrontOneStep_Click);
            //MenuBringToBackOneStep.Click += new RoutedEventHandler(MenuBringToBackOneStep_Click);
            //ElementCopy.Click += new RoutedEventHandler(ElementCopy_Click);
        }

        void ElementCopy_Click(object sender, RoutedEventArgs e)
        {   
            //parentPage.StartCopyElement(this);
        }

        void MenuBringToBackOneStep_Click(object sender, RoutedEventArgs e)
        {   
            //parentPage.BringObjectToBackByNameOneStep(this.Name);
        }

        void MenuBringToFrontOneStep_Click(object sender, RoutedEventArgs e)
        {
            //parentPage.BringObjectToFrontByNameOneStep(this.Name);
        }

        void MenuBringToBack_Click(object sender, RoutedEventArgs e)
        {
            //parentPage.BringObjectToBackByName(this.Name);
        }

        void MenuBringToFront_Click(object sender, RoutedEventArgs e)
        {
            //parentPage.BringObjectToFrontByName(this.Name);
        }


        void ExitRectangle_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {   
            string str = "이 객체를 삭제하시겠습니까?";

            MessageBoxResult result =
                  MessageBox.Show(
                  str,
                  "MenuBoard BADOU Ver",
                  MessageBoxButton.YesNo,
                  MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                DeleteThisObjectByName(this.Name);
            }
        }

        public void DeleteThisObjectByName(string objName)
        {
            //parentPage.DeleteObjectByName(objName);
        }

        void MoveBottomRectangle_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            //parentPage.SetCurrentSelectedElementMovable(true, currentSelectedControlMoveStartPosition.Bottom, e.GetPosition(this));
        }

        void MoveTopRectangle_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            //parentPage.SetCurrentSelectedElementMovable(true, currentSelectedControlMoveStartPosition.Top, e.GetPosition(this));
        }

        void ResizeRectangle_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            //parentPage.SetCurrentSelectedElementResizable(true);
        }

        void FlashAndWebElement_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            ChooseThisElement();
        }

        public void ChooseThisElement()
        {
          
            if (MoveAndResizeGrid.Visibility == Visibility.Hidden)
            {
                //parentPage.SetCurrentSelectedObjectName(this.Name);
                MoveAndResizeGrid.Visibility = Visibility.Visible;

                //parentPage.SetCurrentSelectedElement(this);
                //parentPage.ShowPropertySet(CurrentSelectedObjectType.ScrollText);
                //parentPage.SetCurrentSelectedObjectType(CurrentSelectedObjectType.ScrollText);
                //parentPage.ReleaseSelectedObjectExceptSelectedObject();

            }
            else
            {
                MoveAndResizeGrid.Visibility = Visibility.Hidden;

                //parentPage.ShowPropertySet(CurrentSelectedObjectType.NoneSelected);
            }
        }

        public void ShowMoveAndResizGrid()
        {
            if (MoveAndResizeGrid.Visibility == Visibility.Hidden)
            {
                MoveAndResizeGrid.Visibility = Visibility.Visible;
            }
            else
            {
                MoveAndResizeGrid.Visibility = Visibility.Hidden;
            }
        }

        public void SetFreeThisElementFromSelecting()
        {
            MoveAndResizeGrid.Visibility = Visibility.Hidden;
        }


        void MenusetElement_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            //parentPage.ShowPropertySet(CurrentSelectedObjectType.ScrollText);
            //parentPage.SetCurrentSelectedObjectType(CurrentSelectedObjectType.ScrollText);

            //parentPage.SetCurrentSelectedObjectName(this.Name);
            //parentPage.ReleaseSelectedObject(); 
        }

        void MarqueeText_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            canMain.Height = this.ActualHeight;
            canMain.Width = this.ActualWidth;
          
            //this.UpdateLayout();
            //this.tbmarquee.UpdateLayout();
          
            //StopAnimation();
            //StartMarqueeing(_marqueeType);

            g_CurrentScrollIdx = 0;
            StartScrollText();
        }

        void MarqueeText_Loaded(object sender, RoutedEventArgs e)
        {
            //StartMarqueeing(_marqueeType);
        }

        //public float GetFontHeight()
        //{
        //    float fontHeight = 10;
        //    SizeF totalWidthOfStr;
        //    if (g_SubTitleList.Count > 0)
        //    {
        //        Graphics g = CreateGraphics();

        //        for (int i = 10; i < 100; i++)
        //        {
        //            Font fontForText = new Font("Consolas", i);
        //            totalWidthOfStr = g.MeasureString(g_SubTitleList[0], fontForText);

        //        
        //        }
        //    }

        //    return fontHeight;
        //}

        /*
           
         */

        public void UpdateScrollTextList(ElementInfoClass paramCls)
        {

            this.g_ElementInfoClass.CopyData(paramCls);

            if (g_ElementInfoClass.EIF_ContentsInfoClassList.Count > 0)
            {
                if (g_ElementInfoClass.EIF_ContentsInfoClassList[0].CIF_PlayMinute != string.Empty &&
                    g_ElementInfoClass.EIF_ContentsInfoClassList[0].CIF_PlayMinute != "00")
                {
                    ScrollTextfontColor = g_ElementInfoClass.EIF_ContentsInfoClassList[0].CIF_PlayMinute;
                }

                if (g_ElementInfoClass.EIF_ContentsInfoClassList[0].CIF_ContentType != string.Empty)
                {
                    ScrollTextfontName = g_ElementInfoClass.EIF_ContentsInfoClassList[0].CIF_ContentType;
                }

                if (g_ElementInfoClass.EIF_ContentsInfoClassList[0].CIF_PlaySec != string.Empty &&
                    g_ElementInfoClass.EIF_ContentsInfoClassList[0].CIF_PlaySec != "10")
                {
                    ScrollTextbackgroundColor = g_ElementInfoClass.EIF_ContentsInfoClassList[0].CIF_PlaySec;
                }

                FontFamily fontFamily = new FontFamily(ScrollTextfontName);
                double fontHeight = 0.0;

                for (int i = 15; i < 1000; i++)
                {
                    fontHeight = Math.Ceiling(i * fontFamily.LineSpacing);
                    if (g_ElementInfoClass.EIF_Height < fontHeight)
                    {
                        fontHeight = i - 3;
                        break;
                    }
                }

                ScrollTextfontSize = fontHeight;
                tbmarquee.FontSize = fontHeight;

            }
          
            g_CurrentScrollIdx = 0;    
            StopAnimation();


            _marqueeTimeInSeconds = 10;
            //ScrollTextfontSize = 60;
            //tbmarquee.FontSize = 60;

            tbmarquee.Foreground = ColorTools.GetSolidBrushByColorString(ScrollTextfontColor);
            //tbmarquee.Background = ColorTools.GetSolidBrushByColorString(ScrollTextbackgroundColor);
            ContentGrid.Background = ColorTools.GetSolidBrushByColorString(ScrollTextbackgroundColor);
            tbmarquee.FontFamily = new FontFamily(ScrollTextfontName);

            StartScrollText();
        }

        //public void StartMarqueeing2()
        //{
        //    this.UpdateLayout();
        //    this.tbmarquee.UpdateLayout();
        //    StopAnimation();
        //    switch (_marqueeType)
        //    {
        //        case MarqueeType.LeftToRight:
        //            LeftToRightMarquee();
        //            break;
        //        case MarqueeType.RightToLeft:
        //            RightToLeftMarquee();
        //            break;
        //        case MarqueeType.TopToBottom:
        //            TopToBottomMarquee();
        //            break;
        //        case MarqueeType.BottomToTop:
        //            BottomToTopMarquee();
        //            break;
        //        default:
        //            break;
        //    }          
        //}

        //public void StartMarqueeingForScrollTextList()
        //{   
        //    this.UpdateLayout();
        //    this.tbmarquee.UpdateLayout();
        //    StopAnimation();
        //    switch (_marqueeType)
        //    {
        //        case MarqueeType.LeftToRight:
        //            LeftToRightMarquee();
        //            break;
        //        case MarqueeType.RightToLeft:
        //            RightToLeftMarquee();
        //            break;
        //        case MarqueeType.TopToBottom:
        //            TopToBottomMarquee();
        //            break;
        //        case MarqueeType.BottomToTop:
        //            BottomToTopMarquee();
        //            break;
        //        default:
        //            break;
        //    }
        //}

        public void StartMarqueeingForScrollTextList(ContentsInfoClass paramCls)
        {
            _marqueeTimeInSeconds = paramCls.CIF_ScrollTextSpeedSec;
            MarqueeContent = paramCls.CIF_FileName;
            this.UpdateLayout();
            this.tbmarquee.UpdateLayout();
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

        public void ChangeScrollTime(double scrollTime)
        {
            g_CurrentScrollIdx = 0;
            StartScrollText();

            //this.UpdateLayout();
            //this.tbmarquee.UpdateLayout();
            //StopAnimation();
            //switch (_marqueeType)
            //{
            //    case MarqueeType.LeftToRight:
            //        LeftToRightMarquee();
            //        break;
            //    case MarqueeType.RightToLeft:
            //        RightToLeftMarquee();
            //        break;
            //    case MarqueeType.TopToBottom:
            //        TopToBottomMarquee();
            //        break;
            //    case MarqueeType.BottomToTop:
            //        BottomToTopMarquee();
            //        break;
            //    default:
            //        break;
            //}
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
            
            switch (_marqueeType)
            {
                case MarqueeType.LeftToRight:
                    tbmarquee.BeginAnimation(Canvas.LeftProperty, null);
                    break;
                case MarqueeType.RightToLeft:
                    tbmarquee.BeginAnimation(Canvas.RightProperty, null);
                    break;
                case MarqueeType.TopToBottom:
                    tbmarquee.BeginAnimation(Canvas.TopProperty, null);
                    break;
                case MarqueeType.BottomToTop:
                    tbmarquee.BeginAnimation(Canvas.BottomProperty, null);
                    break;
                default:
                    break;
            }         
             /*  */ 
        }

        public void LeftToRightMarquee()
        {
            _marqueeType = MarqueeType.LeftToRight;

            double height = canMain.ActualHeight - tbmarquee.ActualHeight;
            tbmarquee.Margin = new Thickness(0, height / 2, 0, 0);
            AnimationForL2R.From = -tbmarquee.ActualWidth;
            AnimationForL2R.To = canMain.ActualWidth;

            double st = (canMain.ActualWidth + tbmarquee.ActualWidth) / (MarqueeTimeInSeconds * 14);
            AnimationForR2L.Duration = new Duration(TimeSpan.FromSeconds(st));

            tbmarquee.BeginAnimation(Canvas.LeftProperty, AnimationForL2R);
        }


        public void RightToLeftMarquee()
        {
            _marqueeType = MarqueeType.RightToLeft;

            double height = canMain.ActualHeight - tbmarquee.ActualHeight;
            tbmarquee.Margin = new Thickness(0, height / 2, 0, 0);
            AnimationForR2L.From = -tbmarquee.ActualWidth;
            AnimationForR2L.To = canMain.ActualWidth;

            double st = (canMain.ActualWidth + tbmarquee.ActualWidth) / (MarqueeTimeInSeconds * 14);
            AnimationForR2L.Duration = new Duration(TimeSpan.FromSeconds(st));

            tbmarquee.BeginAnimation(Canvas.RightProperty, AnimationForR2L);
        }

        public void StartScrollText()
        {
            if (this.g_ElementInfoClass.EIF_ContentsInfoClassList.Count > 0)
            {
                if (g_CurrentScrollIdx > (g_ElementInfoClass.EIF_ContentsInfoClassList.Count - 1))
                {
                    g_CurrentScrollIdx = 0;
                }

                tbmarquee.Text = this.g_ElementInfoClass.EIF_ContentsInfoClassList[g_CurrentScrollIdx].CIF_FileName;
                //StartMarqueeingForScrollTextList(this.g_ElementInfoClass.EIF_ContentsInfoClassList[g_CurrentScrollIdx].CIF_FileName);
                StartMarqueeingForScrollTextList(this.g_ElementInfoClass.EIF_ContentsInfoClassList[g_CurrentScrollIdx]);
                g_CurrentScrollIdx++;
            }
        }

        public void TopToBottomMarquee()
        {
            _marqueeType = MarqueeType.TopToBottom;

            double width = canMain.ActualWidth - tbmarquee.ActualWidth;
            tbmarquee.Margin = new Thickness(width / 2, 0, 0, 0);
            AnimationForT2B.From = -tbmarquee.ActualHeight;
            AnimationForT2B.To = canMain.ActualHeight;
            AnimationForT2B.Duration = new Duration(TimeSpan.FromSeconds(_marqueeTimeInSeconds));
            tbmarquee.BeginAnimation(Canvas.TopProperty, AnimationForT2B);
        }

        public void BottomToTopMarquee()
        {
            _marqueeType = MarqueeType.BottomToTop;

            double width = canMain.ActualWidth - tbmarquee.ActualWidth;
            tbmarquee.Margin = new Thickness(width / 2, 0, 0, 0);
            AnimationForB2T.From = -tbmarquee.ActualHeight;
            AnimationForB2T.To = canMain.ActualHeight;
            AnimationForB2T.Duration = new Duration(TimeSpan.FromSeconds(_marqueeTimeInSeconds));
            tbmarquee.BeginAnimation(Canvas.BottomProperty, AnimationForB2T);
        }
    }

}
