using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using TurtleTools;
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;
using FontFamily = System.Windows.Media.FontFamily;
using FontStyle = System.Windows.FontStyle;
using Pen = System.Windows.Media.Pen;
using Point = System.Windows.Point;


namespace AndoW_Manager
{
    /// <summary>
    /// Page1.xaml에 대한 상호 작용 논리
    /// </summary>
    /// 

    public partial class Page1 : UserControl
    {
        public string g_CurrentSelectedObjName = string.Empty;
        Control g_CurrentSelectedElement = null;
        public bool g_IsSelecteControlResizing = false;
        public bool g_IsSelecteControlMoving = false;
        double oldXPos = 0;
        double oldYPos = 0;

        public System.Windows.Point g_ElementStartPos = new System.Windows.Point();

        //////////////////////////////////////////////////////////////////
        // 에디터용
        public List<DisplayElementForEditor> g_DspElmtList = new List<DisplayElementForEditor>();
        public List<ScrollTextForEditor> g_ScrollTextForEditorList = new List<ScrollTextForEditor>();
        public List<WelcomeBoardForEditor> g_WelcomBoardForEditorList = new List<WelcomeBoardForEditor>();
        private const string DefaultScrollTextForeground = "#FFFFFFFF";
        private const string DefaultScrollTextBackground = "#FF222222";
        private const string DefaultScrollTextFontFamily = "Malgun Gothic";
        private const double BaseLandscapeWidth = 1920d;
        private const double BaseLandscapeHeight = 1080d;
        private const double BasePortraitWidth = 1080d;
        private const double BasePortraitHeight = 1920d;
        private bool _suppressFontSettingChanges = false;
        private readonly EditFontInfoClass _lastScrollTextFontStyle = new EditFontInfoClass();


        //////////////////////////////////////////////////////////////////
        // 미리보기용
        //public List<VideoImageElement> g_VideoImageElementList = new List<VideoImageElement>();
        //public List<ScrollTextElement> g_ScrollTextElementList = new List<ScrollTextElement>();
        //public List<WelcomeBoardForEditor> g_TextElementForPreviewList = new List<WelcomeBoardForEditor>();
        
        public List<string> g_ChildNameListForZidx = new List<string>();

        public ElementInfoClass g_SelectedCurElement = new ElementInfoClass();

        public PageInfoClass g_CurrentPageInfo = new PageInfoClass();

        public List<string> videoExtentionSetList = new List<string>();
        public List<string> imageExtentionSetList = new List<string>();

        public ContentsInfoClass g_CurSelContentsInfoClass = new ContentsInfoClass();

        //public string g_CurSelListType = string.Empty;
        public DisplayType g_CurSelListType = DisplayType.None;

        public static Page1 Instance { get; set; }

        private const string PageNamePlaceholderText = "화면구성 이름을 작성하세요.";
        private readonly Brush _pageNamePlaceholderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFB3B3E0"));
        private readonly Brush _pageNameNormalBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF101098"));

        public Page1()
        {
            InitializeComponent();

            Instance = this;

            ScaleDesignerCanvas();
            InitEventHandler();

            HideAllListGrid();
            InitExtentionSet();

            NeedGuideCheckBox.IsChecked = g_CurrentPageInfo.PIC_NeedGuide;
            //this.GuideGrid.ShowGridLines = false;

            FontComboOnTextElem1.ItemsSource = Fonts.SystemFontFamilies;
            InitComboBoxes();
            ApplyDefaultWelcomeFontSettings();
            ApplyDefaultResolution();

            //ResolutionStackPanel.Visibility = System.Windows.Visibility.Hidden;
            //BTN0DO_Copy16.Visibility = System.Windows.Visibility.Hidden;

            BTN0DO_Copy21.Visibility = Visibility.Collapsed;
            BTN0DO_Copy22.Visibility = Visibility.Collapsed;

            InitPageNamePlaceholder();
        }

        public void ChangeLandOrPortTypeTextColor(bool isPortrait)
        {
            if (isPortrait)
            {
                TextAngleGrade22.Foreground = new SolidColorBrush(Colors.Gray);
                TextAngleGrade23.Foreground = new SolidColorBrush(Colors.Yellow);
            }
            else
            {
                TextAngleGrade22.Foreground = new SolidColorBrush(Colors.Yellow);
                TextAngleGrade23.Foreground = new SolidColorBrush(Colors.Gray);
            }
        }


        public void InitComboBoxes()
        {
            for (int i = 20; i < 1200; i++)
            {
                FontComboOnTextElem2.Items.Add(i);
            }

            ScrollSpeedComboBox.Items.Clear();  // 자막속도 관련 콤보박스
            for (int i = 1; i < 31; i++)
            {
                ScrollSpeedComboBox.Items.Add(i);
            }
            ScrollSpeedComboBox.SelectedIndex = 9;


        }

        private void ApplyDefaultWelcomeFontSettings()
        {
            ServerSettings settings = DataShop.Instance?.g_ServerSettingsManager?.sData;
            if (settings == null)
            {
                return;
            }

            _suppressFontSettingChanges = true;
            try
            {
                if (!string.IsNullOrWhiteSpace(settings.DefaultWelcomeFontFamily))
                {
                    g_TextInfoClass.CIF_FontName = settings.DefaultWelcomeFontFamily;
                    FontFamily family = FontComboOnTextElem1.Items
                        .OfType<FontFamily>()
                        .FirstOrDefault(f => string.Equals(f.Source, settings.DefaultWelcomeFontFamily, StringComparison.OrdinalIgnoreCase));
                    if (family != null)
                    {
                        FontComboOnTextElem1.SelectedItem = family;
                    }
                    else if (FontComboOnTextElem1.Items.Count > 0 && FontComboOnTextElem1.SelectedItem == null)
                    {
                        FontComboOnTextElem1.SelectedIndex = 0;
                    }
                }

                double size = settings.DefaultWelcomeFontSize;
                if (size < 20)
                {
                    size = 20;
                }
                else if (size > 1199)
                {
                    size = 1199;
                }
                g_TextInfoClass.CIF_FontSize = (int)Math.Round(size, MidpointRounding.AwayFromZero);
                int roundedSize = (int)g_TextInfoClass.CIF_FontSize;
                if (!FontComboOnTextElem2.Items.Contains(roundedSize))
                {
                    FontComboOnTextElem2.Items.Add(roundedSize);
                }
                FontComboOnTextElem2.SelectedItem = roundedSize;

                if (!string.IsNullOrWhiteSpace(settings.DefaultWelcomeFontColor))
                {
                    g_TextInfoClass.CIF_FontColor = settings.DefaultWelcomeFontColor;
                    fontColorCombo.SelectedColor = ColorTools.GetSolidBrushByColorString(settings.DefaultWelcomeFontColor);
                }
                int fontColorCount = fontColorCombo.superCombo.Items.Count;
                if (fontColorCount > 0 && settings.DefaultWelcomeFontColorIndex >= 0)
                {
                    int safeColorIndex = Math.Min(settings.DefaultWelcomeFontColorIndex, fontColorCount - 1);
                    fontColorCombo.SelectedIndex = safeColorIndex;
                    g_TextInfoClass.CIF_FontColorIndex = safeColorIndex;
                }

                if (!string.IsNullOrWhiteSpace(settings.DefaultWelcomeBackgroundColor))
                {
                    g_TextInfoClass.CIF_BGColor = settings.DefaultWelcomeBackgroundColor;
                    BGColorCombo.SelectedColor = ColorTools.GetSolidBrushByColorString(settings.DefaultWelcomeBackgroundColor);
                }
                int bgColorCount = BGColorCombo.superCombo.Items.Count;
                if (bgColorCount > 0 && settings.DefaultWelcomeBackgroundColorIndex >= 0)
                {
                    int safeBgIndex = Math.Min(settings.DefaultWelcomeBackgroundColorIndex, bgColorCount - 1);
                    BGColorCombo.SelectedIndex = safeBgIndex;
                    g_TextInfoClass.CIF_BGColorIndex = safeBgIndex;
                }
            }
            catch
            {
                // ignore invalid persisted values and fall back to defaults
            }
            finally
            {
                _suppressFontSettingChanges = false;
            }
        }

        private void PersistWelcomeFontSettings()
        {
            if (_suppressFontSettingChanges)
            {
                return;
            }

            ServerSettings settings = DataShop.Instance?.g_ServerSettingsManager?.sData;
            if (settings == null)
            {
                return;
            }

            settings.DefaultWelcomeFontFamily = g_TextInfoClass.CIF_FontName;
            settings.DefaultWelcomeFontSize = g_TextInfoClass.CIF_FontSize;
            settings.DefaultWelcomeFontColor = g_TextInfoClass.CIF_FontColor;
            settings.DefaultWelcomeBackgroundColor = g_TextInfoClass.CIF_BGColor;
            settings.DefaultWelcomeFontColorIndex = g_TextInfoClass.CIF_FontColorIndex;
            settings.DefaultWelcomeBackgroundColorIndex = g_TextInfoClass.CIF_BGColorIndex;

            DataShop.Instance.g_ServerSettingsManager.SaveData(settings);
        }

        private void ApplyDefaultResolution()
        {
            ServerSettings settings = DataShop.Instance?.g_ServerSettingsManager?.sData;
            ResolutionSelection defaultRes = new ResolutionSelection(settings);
            ApplyResolutionToUI(defaultRes.Orientation, defaultRes.Row, defaultRes.Column, defaultRes.WidthPixels, defaultRes.HeightPixels);
        }

        private void ApplyResolutionToUI(DeviceOrientation orientation, int rows, int columns, double widthPixels, double heightPixels)
        {
            rows = Math.Max(1, rows);
            columns = Math.Max(1, columns);
            widthPixels = Math.Max(1, widthPixels);
            heightPixels = Math.Max(1, heightPixels);

            g_CurrentPageInfo.PIC_IsLandscape = orientation == DeviceOrientation.Landscape;
            g_CurrentPageInfo.PIC_Rows = rows;
            g_CurrentPageInfo.PIC_Columns = columns;
            g_CurrentPageInfo.PIC_CanvasWidth = widthPixels;
            g_CurrentPageInfo.PIC_CanvasHeight = heightPixels;

            if (MainWindow.Instance != null)
            {
                MainWindow.Instance.g_static_Width = (int)Math.Round(widthPixels);
                MainWindow.Instance.g_static_Height = (int)Math.Round(heightPixels);
                UpdateOrientationScale(rows, columns);
                MainWindow.Instance.isPortraitEditor = orientation == DeviceOrientation.Portrait;
            }

            ChangePortraitOrLandscape(orientation == DeviceOrientation.Portrait);
        }

        private (double width, double height) ComputeCanvasSize(DeviceOrientation orientation, int rows, int columns)
        {
            rows = Math.Max(1, rows);
            columns = Math.Max(1, columns);

            double baseWidth = orientation == DeviceOrientation.Portrait ? BasePortraitWidth : BaseLandscapeWidth;
            double baseHeight = orientation == DeviceOrientation.Portrait ? BasePortraitHeight : BaseLandscapeHeight;

            return (columns * baseWidth, rows * baseHeight);
        }

        private void UpdateOrientationScale(int rows, int columns)
        {
            rows = Math.Max(1, rows);
            columns = Math.Max(1, columns);

            double landW = Math.Max(1, columns * BaseLandscapeWidth);
            double landH = Math.Max(1, rows * BaseLandscapeHeight);
            double portW = Math.Max(1, columns * BasePortraitWidth);
            double portH = Math.Max(1, rows * BasePortraitHeight);

            MainWindow.Instance.g_wLandScale = landW / portW;
            MainWindow.Instance.g_hLandScale = landH / portH;
            MainWindow.Instance.g_wPortScale = portW / landW;
            MainWindow.Instance.g_hPortScale = portH / landH;
        }

        /*
        public void SelectedContentInfo(ContentsInfoClass paramCls, string paramType)
        {
            g_CurSelContentsInfoClass.CopyData(paramCls);

            g_CurSelListType = paramType;

            if (paramType == "Display")
            {
                
            }
            else if (paramType == "ScrollText")
            {
                TextBoxNewPlayerName.Text = paramCls.CIF_FileName;
                cavasWidthCombo1.SelectedItem = paramCls.CIF_ScrollTextSpeedSec;
            }
        }
     */

        public void SelectedContentInfo(ContentsInfoClass paramCls, DisplayType dType)
        {
            g_CurSelContentsInfoClass.CopyData(paramCls);

            g_CurSelListType = dType;

            switch (dType)
            {
                case DisplayType.Media:
                case DisplayType.HDTV:
                case DisplayType.IPTV:
                    break;

                case DisplayType.ScrollText:
                    ScrollTextTBox.Text = paramCls.CIF_FileName;
                    ScrollSpeedComboBox.SelectedItem = paramCls.CIF_ScrollTextSpeedSec;
                    break;
            }

            SwitchSelectedMediaContent(paramCls);
        }


        private void SwitchSelectedMediaContent(ContentsInfoClass paramCls)
        {
            if (MediaListGrid.IsVisible)
            {
                foreach (ContentInfoElement cie in MediaListBox.Items)
                {
                    if (cie.g_ContentsInfoClass.CIF_StrGUID.Equals(paramCls.CIF_StrGUID))
                    {
                        cie.Selected = true;
                        continue;
                    }
                    cie.Selected = false;
                }
                return;
            }

            //if (HDTVChannelsGrid.IsVisible)
            //{
            //    foreach (ContentInfoElement cie in HDTVChannelStackPannel.Children)
            //    {
            //        if (cie.g_ContentsInfoClass.CIF_StrGUID.Equals(paramCls.CIF_StrGUID))
            //        {
            //            cie.Selected = true;
            //            continue;
            //        }
            //        cie.Selected = false;
            //    }
            //    return;
            //}

            if (ScrollListGrid.IsVisible)
            {
                foreach (ScrollTextInfoElement ste in ScrollTextStackPanel.Children)
                {
                    if (ste.g_ContentsInfoClass.CIF_StrGUID.Equals(paramCls.CIF_StrGUID))
                    {
                        ste.Selected = true;
                        continue;
                    }
                    ste.Selected = false;
                }
                return;
            }
        }

        public void InitExtentionSet()
        {
            videoExtentionSetList.Clear();
            imageExtentionSetList.Clear();

            ///-----------Video 확장자 설정---------------------///
            videoExtentionSetList.Add(".avi");
            videoExtentionSetList.Add(".mp4");
            videoExtentionSetList.Add(".3gp");
            videoExtentionSetList.Add(".mov");
            videoExtentionSetList.Add(".mpg");
            videoExtentionSetList.Add(".mpeg");
            videoExtentionSetList.Add(".m2ts");
            videoExtentionSetList.Add(".ts");
            videoExtentionSetList.Add(".wmv");
            videoExtentionSetList.Add(".asf");

            ///-----------Image 확장자 설정---------------------///
            imageExtentionSetList.Add(".jpg");
            imageExtentionSetList.Add(".jpeg");
            imageExtentionSetList.Add(".bmp");
            imageExtentionSetList.Add(".png");
            imageExtentionSetList.Add(".gif");
        }

        public void ScaleDesignerCanvas()
        {
            /*
            //ScaleTransform scale = new ScaleTransform(0.382, 0.395);
            ScaleTransform scale = new ScaleTransform(0.401, 0.416);
            DesignerCanvas.RenderTransform = scale;  //  <--- 큰거
            
            //scale = new ScaleTransform(0.1835, 0.198);
            scale = new ScaleTransform(0.163, 0.175);
             * 
            */
        }

        double g_FitscaleValueX = 0;
        double g_FitscaleValueY = 0;

        public void AdjustCanvasSize()
        {
            GuideBorder.UpdateLayout();

            double canvasW = Math.Max(1, DesignerCanvas.Width);
            double canvasH = Math.Max(1, DesignerCanvas.Height);

            g_FitscaleValueX = GuideBorder.ActualWidth / canvasW;
            g_FitscaleValueY = GuideBorder.ActualHeight / canvasH;

            ScaleTransform scale = new ScaleTransform(g_FitscaleValueX, g_FitscaleValueY);
            DesignerCanvas.RenderTransform = scale;
            GuideGrid.RenderTransform = scale;
            ScreenGuideGrid.RenderTransform = scale;
            ResolutionGuideGrid.RenderTransform = scale;

            /*
            if (DesignerCanvas.ActualWidth > DesignerCanvas.ActualHeight)
            {
                if (g_FitscaleValueX > g_FitscaleValueY)
                {
                    ScaleTransform scale = new ScaleTransform(g_FitscaleValueX, g_FitscaleValueY);
                    DesignerCanvas.RenderTransform = scale;
                    
                }
                else
                {
                    ScaleTransform scale = new ScaleTransform(g_FitscaleValueX, g_FitscaleValueX);
                    DesignerCanvas.RenderTransform = scale;
                }

            
            }
            else
            {
                if (g_FitscaleValueX < g_FitscaleValueY)
                {
                    ScaleTransform scale = new ScaleTransform(g_FitscaleValueX, g_FitscaleValueY);
                    DesignerCanvas.RenderTransform = scale;
                }
                else
                {
                    ScaleTransform scale = new ScaleTransform(g_FitscaleValueY, g_FitscaleValueY);
                    DesignerCanvas.RenderTransform = scale;
                  
                }
            }
            */
        }

        double g_FitscaleValueXForPreview = 0;
        double g_FitscaleValueYForPreview = 0;

        //public void AdjustCanvasSize()
        //{
        //    g_FitscaleValueX = GuideBorder.ActualWidth / DesignerCanvas.Width;
        //    g_FitscaleValueY = GuideBorder.ActualHeight / DesignerCanvas.Height;

        //    if (Math.Abs(DesignerCanvas.ActualWidth - DesignerCanvas.ActualHeight) < 500)
        //    {
        //        if (DesignerCanvas.ActualWidth < 1000)
        //        {
        //            if (DesignerCanvas.ActualWidth > DesignerCanvas.ActualHeight)
        //            {
        //                if (DesignerCanvas.ActualHeight < 1000)
        //                {
        //                    ScaleTransform scale = new ScaleTransform(g_FitscaleValueX, g_FitscaleValueY);
        //                    DesignerCanvas.RenderTransform = scale;
        //                }
        //                else
        //                {
        //                    ScaleTransform scale = new ScaleTransform(g_FitscaleValueX, g_FitscaleValueX);
        //                    DesignerCanvas.RenderTransform = scale;
        //                }
        //            }
        //            else
        //            {
        //                ScaleTransform scale = new ScaleTransform(g_FitscaleValueY, g_FitscaleValueY);
        //                DesignerCanvas.RenderTransform = scale;
        //            }
        //        }
        //        else
        //        {
        //            ScaleTransform scale = new ScaleTransform(g_FitscaleValueX, g_FitscaleValueY);
        //            DesignerCanvas.RenderTransform = scale;
        //        }
        //    }
        //    else if (DesignerCanvas.ActualWidth > DesignerCanvas.ActualHeight)
        //    {
        //        ScaleTransform scale = new ScaleTransform(g_FitscaleValueX, g_FitscaleValueX);
        //        DesignerCanvas.RenderTransform = scale;
        //    }
        //    else
        //    {
        //        ScaleTransform scale = new ScaleTransform(g_FitscaleValueY, g_FitscaleValueY);
        //        DesignerCanvas.RenderTransform = scale;
        //    }

        
        //}

        public void HideAllListGrid()
        {
            MediaListGrid.Visibility = Visibility.Hidden;

            ScrollListGrid.Visibility = Visibility.Hidden;
            ScrollTextTBox.Text = string.Empty;

            WelcomeListGrid.Visibility = System.Windows.Visibility.Hidden;
        }

        public void InitEventHandler()
        {
            //this.cavasWidthCombo.SelectedItem = ILYCODEDataShop.Instance.g_ApplicationInfoManager.g_DataClassList[0].AIF_CanvasWidth;
            //this.cavasHeightCombo.SelectedItem = ILYCODEDataShop.Instance.g_ApplicationInfoManager.g_DataClassList[0].AIF_CanvasHeight;
            BTN0DO_Copy19.Click += BTN0DO_Copy19_Click;
            BTN0DO_Copy20.Click += BTN0DO_Copy20_Click;

            BTN0DO_Copy18.Click += BTN0DO_Copy18_Click;   // 웰컴보드 텍스 입력



            /*
            cavasWidthCombo.SelectionChanged += cavasWidthCombo_SelectionChanged;
            cavasHeightCombo.SelectionChanged += cavasHeightCombo_SelectionChanged;
             */ 

            FontComboOnTextElem2.SelectionChanged += FontComboOnTextElem2_SelectionChanged;

            BTN0DO_Copy.Click += BTN0DO_Copy_Click;    // 화면추가
            BTN0DO_Copy1.Click += BTN0DO_Copy1_Click;  // 자막추가
            BTN0DO_Copy16.Click += BTN0DO_Copy16_Click;  // 웰컴보드추가
            BTN0DO_Copy2.Click += BTN0DO_Copy2_Click;  // 화면정렬
            BTN0DO_Copy3.Click += BTN0DO_Copy3_Click;  // 화면 초기화
            BTN0DO_Copy4.Click += BTN0DO_Copy4_Click;  // 페이지저장

            BTN0DO_Copy5.Click += BTN0DO_Copy5_Click;  // 미리보기 재생

            BTN0DO_Copy7.Click += BTN0DO_Copy7_Click;   // 디스플레이 컨텐츠리스트 Shift Up
            BTN0DO_Copy8.Click += BTN0DO_Copy8_Click;   // 디스플레이 컨텐츠리스트 Shift Down
            BTN0DO_Copy9.Click += BTN0DO_Copy9_Click;   // 디스플레이 컨텐츠 목록 추가
            //BTN0DO_Copy23.Click += BTN0DO_Copy23_Click;  // 기존파일에서 추가

            BTN0DO_Copy10.Click += BTN0DO_Copy10_Click; // 자막수정   
            BTN0DO_Copy11.Click += BTN0DO_Copy11_Click; // 자막목록추가
            BTN0DO_Copy12.Click += BTN0DO_Copy12_Click; // 자막 컨텐츠리스트 Shift Up
            BTN0DO_Copy13.Click += BTN0DO_Copy13_Click; // 자막 컨텐츠리스트 Shift Down

            BTN0DO_Copy14.Click += BTN0DO_Copy14_Click; // 자막 폰트 설정

            this.DesignerCanvas.PreviewMouseMove += new MouseEventHandler(DesignerCanvas_PreviewMouseMove);
            this.DesignerCanvas.PreviewMouseLeftButtonDown += new MouseButtonEventHandler(DesignerCanvas_PreviewMouseLeftButtonDown);
            this.DesignerCanvas.PreviewMouseLeftButtonUp += new MouseButtonEventHandler(DesignerCanvas_PreviewMouseLeftButtonUp);


            NeedGuideCheckBox.Checked += NeedGuideCheckBox_Checked;
            NeedGuideCheckBox.Unchecked += NeedGuideCheckBox_Unchecked;

            this.Loaded += Page1_Loaded;

            fontColorCombo.superCombo.SelectionChanged += superCombo_SelectionChanged;
            BGColorCombo.superCombo.SelectionChanged +=superCombo_SelectionChanged1;

            BTN0DO_Copy17.Click += BTN0DO_Copy17_Click;  // 글씨 배경이미지 찾기
            FontComboOnTextElem1.SelectionChanged += FontComboOnTextElem1_SelectionChanged;

            this.SizeChanged += Page1_SizeChanged;

            BTN0DO_Copy21.Click += BTN0DO_Copy21_Click;        // 가로형 버튼
            BTN0DO_Copy22.Click += BTN0DO_Copy22_Click;        // 세로형 버튼

            BTN0DO_Copy26.Click += BTN0DO_Copy26_Click;

            MediaListBox.PreviewMouseWheel += MediaListBox_PreviewMouseWheel;
        }

        void BTN0DO_Copy26_Click(object sender, RoutedEventArgs e)
        {
            DepartuerAddGrid_Copy1.Visibility = Visibility.Visible;
            InitPageNamePlaceholder();
            RefreshSavedPageList();
        }
        
        void BTN0DO_Copy22_Click(object sender, RoutedEventArgs e)  // 세로형 버튼
        {
            // 사용 중지: 더 이상 가로/세로 전환 버튼은 동작하지 않음.
            return;
        }

        public void ChangePortraitOrLandscape(bool isPortrait)
        {
            bool changed = false;
            double targetWidth = g_CurrentPageInfo.PIC_CanvasWidth;
            double targetHeight = g_CurrentPageInfo.PIC_CanvasHeight;

            if (Math.Abs(DesignerCanvas.Width - targetWidth) > 0.1 || Math.Abs(DesignerCanvas.Height - targetHeight) > 0.1)
            {
                changed = true;
            }

            if (isPortrait)   //세로
            {
                //Grid.SetColumn(CanvasContainGrid, 0);
                //Grid.SetRow(CanvasContainGrid, 0);
                //Grid.SetRowSpan(CanvasContainGrid, 2);
                //Grid.SetColumnSpan(CanvasContainGrid, 2);

                //Grid.SetColumn(DepartuerAddGrid_Copy1, 1);
                //Grid.SetRow(DepartuerAddGrid_Copy1, 0);
                //Grid.SetRowSpan(DepartuerAddGrid_Copy1, 2);
                //Grid.SetColumnSpan(DepartuerAddGrid_Copy1, 1);

                //DepartuerAddGrid_Copy1.Margin = new Thickness(0, 18, 18, 23);

                GuideBorder1.Margin = new Thickness(7, 3, 17, 15);
                GuideBorder.Margin = new Thickness(7, 3, 19, 15);
                //DepartuerAddGrid_Copy1

                if (MainWindow.Instance.Width < 1280)
                {
                    //BTN0DO_Copy.Width = 50;
                    //BTN0DO_Copy1.Width = 50;
                    //BTN0DO_Copy16.Width = 50;
                    //BTN0DO_Copy2.Width = 50;
                    //BTN0DO_Copy3.Width = 50;
                    //ResSettingsBtn.Width = 50;

                    TextAngleGrade3.Visibility = Visibility.Collapsed;
                    TextAngleGrade1.Visibility = Visibility.Collapsed;
                    TextAngleGrade17.Visibility = Visibility.Collapsed;
                    TextAngleGrade2.Visibility = Visibility.Collapsed;
                    TextAngleGrade4.Visibility = Visibility.Collapsed;
                    ResBtnText.Visibility = Visibility.Collapsed;

                    foreach (SavedPageElement spe in ContentsElementsStackPannel2.Children)
                    {
                        spe.Width = 242;
                    }
                }
                else
                {
                    //BTN0DO_Copy.Width = 75;
                    //BTN0DO_Copy1.Width = 55;
                    //BTN0DO_Copy16.Width = 110;
                    //BTN0DO_Copy2.Width = 55;
                    //BTN0DO_Copy3.Width = 55;
                    //ResSettingsBtn.Width = 100;

                    TextAngleGrade3.Visibility = Visibility.Visible;
                    TextAngleGrade1.Visibility = Visibility.Visible;
                    TextAngleGrade17.Visibility = Visibility.Visible;
                    TextAngleGrade2.Visibility = Visibility.Visible;
                    TextAngleGrade4.Visibility = Visibility.Visible;
                    ResBtnText.Visibility = Visibility.Visible;

                    foreach (SavedPageElement spe in ContentsElementsStackPannel2.Children)
                    {
                        spe.Width = 222;
                    }
                }
            }
            else   //가로
            {

                Grid.SetColumn(CanvasContainGrid, 0);
                Grid.SetRow(CanvasContainGrid, 0);
                Grid.SetRowSpan(CanvasContainGrid, 1);
                Grid.SetColumnSpan(CanvasContainGrid, 2);

                //Grid.SetColumn(DepartuerAddGrid_Copy1, 0);
                //Grid.SetRow(DepartuerAddGrid_Copy1, 1);
                //Grid.SetRowSpan(DepartuerAddGrid_Copy1, 1);
                //Grid.SetColumnSpan(DepartuerAddGrid_Copy1, 2);

                //DepartuerAddGrid_Copy1.Margin = new Thickness(8, 0, 8, 8);

                GuideBorder1.Margin = new Thickness(8, 3, 8, 10);
                GuideBorder.Margin = new Thickness(8, 3, 8, 10);

                //BTN0DO_Copy.Width = 75;
                //BTN0DO_Copy1.Width = 55;
                //BTN0DO_Copy16.Width = 110;
                //BTN0DO_Copy2.Width = 55;
                //BTN0DO_Copy3.Width = 55;

                TextAngleGrade3.Visibility = Visibility.Visible;
                TextAngleGrade1.Visibility = Visibility.Visible;
                TextAngleGrade17.Visibility = Visibility.Visible;
                TextAngleGrade2.Visibility = Visibility.Visible;
                TextAngleGrade4.Visibility = Visibility.Visible;

                foreach (SavedPageElement spe in ContentsElementsStackPannel2.Children)
                {
                    spe.Width = 267;
                }
            }

            DesignerCanvas.Width = GuideGrid.Width = targetWidth;
            DesignerCanvas.Height = GuideGrid.Height = targetHeight;
            DesignGrid.Width = targetWidth;
            DesignGrid.Height = targetHeight;

            AdjustCanvasSize();
            TransformElements(changed, isPortrait);
            DesignerCanvas.UpdateLayout();
            GuideGrid.UpdateLayout();
            ScreenGuideGrid.UpdateLayout();
            RefreshGuides(g_CurrentPageInfo.PIC_Rows, g_CurrentPageInfo.PIC_Columns);

            ChangeLandOrPortTypeTextColor(isPortrait);   // 가로화면, 세로화면 글씨바꾸기

            g_CurrentPageInfo.PIC_IsLandscape = !isPortrait;
            g_CurrentPageInfo.PIC_CanvasWidth = targetWidth;
            g_CurrentPageInfo.PIC_CanvasHeight = targetHeight;
            if (MainWindow.Instance != null)
            {
                MainWindow.Instance.isPortraitEditor = isPortrait;
                MainWindow.Instance.g_static_Width = (int)Math.Round(targetWidth);
                MainWindow.Instance.g_static_Height = (int)Math.Round(targetHeight);
            }
        }

        public void TransformElements(bool changed, bool isPortrait)
        {
            if (!changed) return;

            double wScale = 1.0;
            double hScale = 1.0;

            if (isPortrait)
            {
                wScale = MainWindow.Instance.g_wPortScale;
                hScale = MainWindow.Instance.g_hPortScale;
            }
            else
            {
                wScale = MainWindow.Instance.g_wLandScale;
                hScale = MainWindow.Instance.g_hLandScale;
            }


            foreach (DisplayElementForEditor defe in g_DspElmtList)
            {
                WindowTools.ConvertInScaledUserCtrl(defe, wScale, hScale);
                defe.UpdateElementLandSizeAndPos();
            }

            foreach (ScrollTextForEditor stfe in g_ScrollTextForEditorList)
            {
                WindowTools.ConvertInScaledUserCtrl(stfe, wScale, hScale);
                stfe.UpdateElementLandSizeAndPos();
            }

            foreach (WelcomeBoardForEditor wbfe in g_WelcomBoardForEditorList)
            {
                WindowTools.ConvertInScaledUserCtrl(wbfe, wScale, hScale);
                wbfe.UpdateElementLandSizeAndPos();
            }
        }



        void BTN0DO_Copy21_Click(object sender, RoutedEventArgs e)  // 가로형 버튼
        {
            // 사용 중지: 더 이상 가로/세로 전환 버튼은 동작하지 않음.
            return;
        }

        void Page1_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            AdjustCanvasSize(); 
        }

        void BTN0DO_Copy23_Click(object sender, RoutedEventArgs e)
        {
            SelectAlreadyExistFileWindow tmpWindow = new SelectAlreadyExistFileWindow();
            tmpWindow.Show();

            MediaListScrollViewer.ScrollToBottom();
        }

        void BTN0DO_Copy20_Click(object sender, RoutedEventArgs e)
        {
            if (this.g_TextInfoClass.CIF_IsItalic == true)
            {
                this.g_TextInfoClass.CIF_IsItalic = false;
                TextAngleGrade21.Foreground = new SolidColorBrush(Colors.Gray);
            }
            else
            {
                this.g_TextInfoClass.CIF_IsItalic = true;
                TextAngleGrade21.Foreground = new SolidColorBrush(Colors.YellowGreen);
            }

            UpdateTextInfoClassToElement(this.g_TextInfoClass);
        }

        void BTN0DO_Copy19_Click(object sender, RoutedEventArgs e)
        {
            if (this.g_TextInfoClass.CIF_IsBold == true)
            {
                this.g_TextInfoClass.CIF_IsBold = false;
                TextAngleGrade20.Foreground = new SolidColorBrush(Colors.Gray);
            }
            else
            {
                this.g_TextInfoClass.CIF_IsBold = true;
                TextAngleGrade20.Foreground = new SolidColorBrush(Colors.YellowGreen);
            }

            UpdateTextInfoClassToElement(this.g_TextInfoClass);
        }

        void BTN0DO_Copy18_Click(object sender, RoutedEventArgs e)
        {
            this.g_TextInfoClass.CIF_TextContent = TextBoxNewPlayerName1.Text;

            UpdateTextInfoClassToElement(this.g_TextInfoClass);
        }

        void BTN0DO_Copy17_Click(object sender, RoutedEventArgs e)
        {
            FindContentFileForTextElement();

        }

        public void FindContentFileForTextElement()
        {
            Microsoft.Win32.OpenFileDialog openFileDialog = new Microsoft.Win32.OpenFileDialog();
            openFileDialog.Multiselect = false;
            openFileDialog.Filter = "Image Files  (*.jpg, *.png, *.bmp, *.gif, *.jpeg)|*.jpg*;*.png*;*.bmp*;*.gif*;*.jpeg*"; 
            if ((bool)openFileDialog.ShowDialog())
            {
                try
                {
                    string fileNameFullPath = openFileDialog.FileName;
                    string fileName = System.IO.Path.GetFileName(openFileDialog.FileName);

                    this.g_TextInfoClass.CIF_BGImageFileFullPath = fileNameFullPath;
                    this.g_TextInfoClass.CIF_BGImageFileName = fileName;
                    this.g_TextInfoClass.CIF_IsBGImageExist = true;

                    TextBoxNewPlayerName2.Text = fileName;

                    this.UpdateTextInfoClassToElement(this.g_TextInfoClass);
                  
                }
                catch (Exception ex)
                {

                }
            }
        }


        void FontComboOnTextElem1_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (FontComboOnTextElem1.SelectedItem == null)
            {
                return;
            }

            this.g_TextInfoClass.CIF_FontName = FontComboOnTextElem1.SelectedItem.ToString();

            UpdateTextInfoClassToElement(this.g_TextInfoClass);
            PersistWelcomeFontSettings();
        }

        void FontComboOnTextElem2_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (FontComboOnTextElem2.SelectedItem == null)
            {
                return;
            }

            this.g_TextInfoClass.CIF_FontSize = Convert.ToDouble(FontComboOnTextElem2.SelectedItem.ToString());

            UpdateTextInfoClassToElement(this.g_TextInfoClass);
            PersistWelcomeFontSettings();
        }



        bool g_IsFontColorFirstChange = true;
        void superCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (g_IsFontColorFirstChange == true)
            {
                g_IsFontColorFirstChange = false;
            }
            else
            {
                if (!(fontColorCombo.SelectedColor is SolidColorBrush brush))
                {
                    return;
                }

                System.Windows.Media.Color c2 = brush.Color;
                this.g_TextInfoClass.CIF_FontColorIndex = fontColorCombo.superCombo.SelectedIndex;
                this.g_TextInfoClass.CIF_FontColor = c2.ToString();

                UpdateTextInfoClassToElement(this.g_TextInfoClass);
                PersistWelcomeFontSettings();
            }
        }

        bool g_IsFontColorFirstChange1 = true;
        void superCombo_SelectionChanged1(object sender, SelectionChangedEventArgs e)
        {
            if (g_IsFontColorFirstChange1 == true)
            {
                g_IsFontColorFirstChange1 = false;
            }
            else
            {
                if (!(BGColorCombo.SelectedColor is SolidColorBrush brush))
                {
                    return;
                }

                System.Windows.Media.Color c2 = brush.Color;
                this.g_TextInfoClass.CIF_IsBGImageExist = false;
                this.g_TextInfoClass.CIF_BGImageFileName = string.Empty;
                this.g_TextInfoClass.CIF_BGImageFileFullPath = string.Empty;
                this.g_TextInfoClass.CIF_BGColorIndex = BGColorCombo.superCombo.SelectedIndex;
                this.g_TextInfoClass.CIF_BGColor = c2.ToString();

                UpdateTextInfoClassToElement(this.g_TextInfoClass);
                PersistWelcomeFontSettings();
            }
        }

        void BTN0DO_Copy16_Click(object sender, RoutedEventArgs e)  // 웰컴보드 추가
        {
            //AddTextElement();
            AddWelcomeBoardElement();
            LockCheckBox.IsChecked = false;
        }

        public void AddWelcomeBoardElement()
        {
            double unitWidth = this.GuideGrid.ColumnDefinitions[0].ActualWidth;
            double unitHeight = this.GuideGrid.RowDefinitions[0].ActualHeight;

            double elementWidth = unitWidth * 16;
            double elementHeight = unitHeight * 10;

            if (g_WelcomBoardForEditorList.Count > 3)
            {
                MessageTools.ShowMessageBox("더이상 추가 할 수 없습니다.", "확인");
                return;
            }

            System.Windows.Point position = new System.Windows.Point(100, 100);
            string elementNameGuidStr = string.Empty;
            string elementName = string.Empty;

            elementNameGuidStr = Guid.NewGuid().ToString();
            elementName = GetElementNameByDateTime();
            WelcomeBoardForEditor dspElement = new WelcomeBoardForEditor();

            dspElement.Width = elementWidth;
            dspElement.Height = elementHeight;
            dspElement.Name = elementName;

            Canvas.SetLeft(dspElement, position.X);
            Canvas.SetTop(dspElement, position.Y);
            DesignerCanvas.Children.Add(dspElement);

            dspElement.g_TextInfoClass.CIF_FontColor = fontColorCombo.SelectedColor.ToString();
            dspElement.g_TextInfoClass.CIF_BGColor = BGColorCombo.SelectedColor.ToString();
            dspElement.g_TextInfoClass.CIF_FontName = g_TextInfoClass.CIF_FontName;
            dspElement.g_TextInfoClass.CIF_FontSize = g_TextInfoClass.CIF_FontSize;
            dspElement.g_TextInfoClass.CIF_FontColorIndex = g_TextInfoClass.CIF_FontColorIndex;
            dspElement.g_TextInfoClass.CIF_BGColorIndex = g_TextInfoClass.CIF_BGColorIndex;
            dspElement.g_TextInfoClass.CIF_IsBold = g_TextInfoClass.CIF_IsBold;
            dspElement.g_TextInfoClass.CIF_IsItalic = g_TextInfoClass.CIF_IsItalic;
            dspElement.LayoutRoot.Background = BGColorCombo.SelectedColor;
            dspElement.UpdateTextInfoClsFromPage(dspElement.g_TextInfoClass);

            ElementInfoClass tmpcls = new ElementInfoClass();
            double leftVal = (double)dspElement.GetValue(Canvas.LeftProperty);
            double topVal = (double)dspElement.GetValue(Canvas.TopProperty);
            tmpcls.EIF_Name = elementName;
            tmpcls.EIF_Type = DisplayType.WelcomeBoard.ToString();

            tmpcls.EIF_RowSpanVal = 10;
            tmpcls.EIF_ColSpanVal = 16;

            tmpcls.EIF_Width = elementWidth;
            tmpcls.EIF_Height = elementHeight;
            tmpcls.EIF_PosTop = topVal;
            tmpcls.EIF_PosLeft = leftVal;
            tmpcls.EIF_DataFileName = string.Format("{0}.xml", elementName);

            dspElement.UpdateElemenetnInfoCls(tmpcls);
            dspElement.UpdateElementSizeAndPosThroughGridBase();

            g_ChildNameListForZidx.Add(elementName);
            g_WelcomBoardForEditorList.Add(dspElement);
            RenameDisplayElementText();

            ReOrderingZorder();
        }

        void BTN0DO_Copy15_Click(object sender, RoutedEventArgs e)
        {
            int idx = 0;
            if (g_CurrentSelectedObjName != string.Empty)
            {
                
                foreach (DisplayElementForEditor item in g_DspElmtList)
                {
                    if (item.g_ElementInfoClass.EIF_Name == g_CurrentSelectedObjName)
                    {
                       // item.AddContentInfoCls(temInfoClass);
                        break;
                    }
                    idx++;
                }

            }

            if (g_DspElmtList.Count > idx)
            {
                EditContentsListWindow tmpWindow = new EditContentsListWindow(g_DspElmtList[idx].g_ElementInfoClass.EIF_ContentsInfoClassList);
                tmpWindow.ShowDialog();
            }
          
        }

        void Page1_Loaded(object sender, RoutedEventArgs e)
        {
            NeedGuideCheckBox.IsChecked = g_CurrentPageInfo.PIC_NeedGuide;

            //if (ILYCODEDataShop.Instance.g_ApplicationInfoManager.g_DataClassList[0].AIF_CanvasWidth > ILYCODEDataShop.Instance.g_ApplicationInfoManager.g_DataClassList[0].AIF_CanvasHeight)
            //{
            //    ChangePortraitAndLandscape(true);
            //}
            //else
            //{
            //    ChangePortraitAndLandscape(false);
            //}

            MainWindow.Instance.isPortraitEditor = !g_CurrentPageInfo.PIC_IsLandscape;
            ChangePortraitOrLandscape(MainWindow.Instance.isPortraitEditor);
        }

        void BTN0DO_Copy14_Click(object sender, RoutedEventArgs e)
        {
            if (g_CurrentSelectedElement is ScrollTextForEditor)
            {
                EditFontInfoClass efic = new EditFontInfoClass();
                ScrollTextForEditor stfe = (ScrollTextForEditor)g_CurrentSelectedElement;
                efic.CopyData(stfe.g_EditFontInfoClass);

                if (ScrollTextStackPanel.Children.Count > 0)
                {
                    ScrollTextInfoElement stie = (ScrollTextInfoElement)ScrollTextStackPanel.Children[0];
                    if (string.IsNullOrWhiteSpace(efic.EFT_FontName))
                    {
                        efic.EFT_FontName = stie.g_ContentsInfoClass.CIF_ContentType;
                    }
                    if (string.IsNullOrWhiteSpace(efic.EFT_BackGoundColor))
                    {
                        efic.EFT_BackGoundColor = stie.g_ContentsInfoClass.CIF_PlaySec;
                    }
                    if (string.IsNullOrWhiteSpace(efic.EFT_ForeGoundColor))
                    {
                        efic.EFT_ForeGoundColor = stie.g_ContentsInfoClass.CIF_PlayMinute;
                    }
                }

                FontStyleEditWindow wnd = new FontStyleEditWindow(efic);
                wnd.ShowDialog();
            }
        }

        void BTN0DO_Copy13_Click(object sender, RoutedEventArgs e)
        {

            if (g_CurSelContentsInfoClass.CIF_FileName == string.Empty)
            {
                return;
            }

            int idx = 0;
            foreach (ContentsInfoClass item in this.g_SelectedCurElement.EIF_ContentsInfoClassList)
            {
                if (item.CIF_StrGUID == g_CurSelContentsInfoClass.CIF_StrGUID)
                {
                    break;
                }
                idx++;
            }

            ////////////////
            if (idx == (this.g_SelectedCurElement.EIF_ContentsInfoClassList.Count - 1))
            {
                return;
            }
            else
            {
                if (idx < (this.g_SelectedCurElement.EIF_ContentsInfoClassList.Count - 1))
                {
                    this.g_SelectedCurElement.EIF_ContentsInfoClassList.RemoveAt(idx);
                    idx++;

                    ContentsInfoClass tmpCls = new ContentsInfoClass();
                    tmpCls.CopyData(g_CurSelContentsInfoClass);

                    this.g_SelectedCurElement.EIF_ContentsInfoClassList.Insert(idx, tmpCls);

                    foreach (ScrollTextForEditor item in g_ScrollTextForEditorList)
                    {
                        if (item.g_ElementInfoClass.EIF_Name == g_SelectedCurElement.EIF_Name)
                        {
                            item.UpdateElemenetnInfoCls(g_SelectedCurElement);
                        }
                    }
                }
            }

            RefreshScrollTextInfoList();
        }

        void BTN0DO_Copy12_Click(object sender, RoutedEventArgs e)
        {

            if (g_CurSelContentsInfoClass.CIF_FileName == string.Empty)
            {
                return;
            }

            int idx = 0;
            foreach (ContentsInfoClass item in this.g_SelectedCurElement.EIF_ContentsInfoClassList)
            {
                if (item.CIF_StrGUID == g_CurSelContentsInfoClass.CIF_StrGUID)
                {
                    break;
                }
                idx++;
            }

            if (idx == 0)
            {
                return;
            }
            else
            {
                if (idx > 0)
                {
                    this.g_SelectedCurElement.EIF_ContentsInfoClassList.RemoveAt(idx);
                    idx--;

                    ContentsInfoClass tmpCls = new ContentsInfoClass();
                    tmpCls.CopyData(g_CurSelContentsInfoClass);

                    this.g_SelectedCurElement.EIF_ContentsInfoClassList.Insert(idx, tmpCls);

                    foreach (ScrollTextForEditor item in g_ScrollTextForEditorList)
                    {
                        if (item.g_ElementInfoClass.EIF_Name == g_SelectedCurElement.EIF_Name)
                        {
                            item.UpdateElemenetnInfoCls(g_SelectedCurElement);
                        }
                    }
                }
            }

            RefreshScrollTextInfoList();
        }

        void BTN0DO_Copy11_Click(object sender, RoutedEventArgs e)
        {
            AddScrollTextToList();
            ScrollTextTBox.Text = string.Empty;
            ContentsListScrollViewer3.ScrollToBottom();
        }


        void BTN0DO_Copy10_Click(object sender, RoutedEventArgs e)
        {
            //if (g_CurSelListType == "ScrollText")
            //{
            //    if (TextBoxNewPlayerName.Text != string.Empty)
            //    {
            //        foreach (ScrollTextForEditor item in g_ScrollTextForEditorList)
            //        {
            //            if (item.g_ElementInfoClass.EIF_Name == g_CurrentSelectedObjName)
            //            {
            //                item.EditScrollText(g_CurSelContentsInfoClass.CIF_StrGUID, TextBoxNewPlayerName.Text, (int)cavasWidthCombo1.SelectedItem);
            //                break;
            //            }
            //        }
            //    }
            //    else
            //    {
            //        MessageTools.ShowMessageBox("자막 내용을 입력해주세요.", "확인");
            //    }
            //}


            if (g_CurSelListType == DisplayType.ScrollText)
            {
                if (ScrollTextTBox.Text != string.Empty)
                {
                    foreach (ScrollTextForEditor item in g_ScrollTextForEditorList)
                    {
                        if (item.g_ElementInfoClass.EIF_Name == g_CurrentSelectedObjName)
                        {
                            item.EditScrollText(g_CurSelContentsInfoClass.CIF_StrGUID, ScrollTextTBox.Text, (int)ScrollSpeedComboBox.SelectedItem);
                            break;
                        }
                    }
                }
                else
                {
                    MessageTools.ShowMessageBox("자막 내용을 입력해주세요.", "확인");
                }
            }
        }

        void BTN0DO_Copy9_Click(object sender, RoutedEventArgs e)
        {
           AddContentsToList();
           MediaListScrollViewer.ScrollToBottom();
        }

        void BTN0DO_Copy8_Click(object sender, RoutedEventArgs e)
        {
            if (g_CurSelContentsInfoClass.CIF_FileName == string.Empty)
            {
                return;
            }

            int idx = 0;
            foreach (ContentsInfoClass item in this.g_SelectedCurElement.EIF_ContentsInfoClassList)
            {
                if (item.CIF_StrGUID == g_CurSelContentsInfoClass.CIF_StrGUID)
                {
                    break;
                }
                idx++;
            }

            ////////////////
            if (idx == (this.g_SelectedCurElement.EIF_ContentsInfoClassList.Count - 1))
            {
                return;
            }
            else
            {
                if (idx < (this.g_SelectedCurElement.EIF_ContentsInfoClassList.Count - 1))
                {
                    this.g_SelectedCurElement.EIF_ContentsInfoClassList.RemoveAt(idx);
                    idx++;

                    ContentsInfoClass tmpCls = new ContentsInfoClass();
                    tmpCls.CopyData(g_CurSelContentsInfoClass);

                    this.g_SelectedCurElement.EIF_ContentsInfoClassList.Insert(idx, tmpCls);

                    foreach (DisplayElementForEditor item in g_DspElmtList)
                    {
                        if (item.g_ElementInfoClass.EIF_Name == g_SelectedCurElement.EIF_Name)
                        {
                            item.UpdateElemenetnInfoCls(g_SelectedCurElement);
                        }
                    }
                }
            }

            RefreshContentInfoList();
        }

        void BTN0DO_Copy7_Click(object sender, RoutedEventArgs e)   // 디스플레이 컨텐츠리스트 Shift Up
        {
            if (g_CurSelContentsInfoClass.CIF_FileName == string.Empty)
            {
                return;
            }

            int idx = 0;
            foreach (ContentsInfoClass item in this.g_SelectedCurElement.EIF_ContentsInfoClassList)
            {
                if (item.CIF_StrGUID == g_CurSelContentsInfoClass.CIF_StrGUID)
                {
                    break;
                }
                idx++;
            }

            if (idx == 0)
            {
                return;
            }
            else
            {
                if (idx > 0)
                {
                    this.g_SelectedCurElement.EIF_ContentsInfoClassList.RemoveAt(idx);
                    idx--;

                    ContentsInfoClass tmpCls = new ContentsInfoClass();
                    tmpCls.CopyData(g_CurSelContentsInfoClass);

                    this.g_SelectedCurElement.EIF_ContentsInfoClassList.Insert(idx, tmpCls);

                    foreach (DisplayElementForEditor item in g_DspElmtList)
                    {
                        if (item.g_ElementInfoClass.EIF_Name == g_SelectedCurElement.EIF_Name)
                        {
                            item.UpdateElemenetnInfoCls(g_SelectedCurElement);
                        }
                    }
                }
            }

            RefreshContentInfoList();
        }

        void BTN0DO_Copy5_Click(object sender, RoutedEventArgs e)
        {
            StartPreviewPlay();
        }

        void BTN0DO_Copy4_Click(object sender, RoutedEventArgs e)   // 현재 작업중인 페이지저장
        {
            SaveCurrentPage();
        }

        void BTN0DO_Copy3_Click(object sender, RoutedEventArgs e)
        {
            if (MessageTools.ShowMessageBox("화면을 초기화하시겠습니까?", "예", "아니오") == true)
            {
                HideAllListGrid();
                SetPageNamePlaceholder();
                ClearAllChildDataOfCanvas();

                MediaListBox.Items.Clear();

                LockCheckBox.IsChecked = false;
                BTN0DO_Copy2.IsEnabled = true;
            }
        }

        void BTN0DO_Copy2_Click(object sender, RoutedEventArgs e)
        {
            LockCheckBox.IsChecked = false;
            LineUpDisplayElementBase();
            BringToFrontAfterLineUp();
        }

        void BringToFrontAfterLineUp()
        {
            Dictionary<int, string> dics = new Dictionary<int, string>();

            foreach (WelcomeBoardForEditor item in g_WelcomBoardForEditorList)
            {
                dics.Add(item.g_ElementInfoClass.EIF_ZIndex, item.g_ElementInfoClass.EIF_Name);
            }

            foreach (ScrollTextForEditor item in g_ScrollTextForEditorList)
            {
                dics.Add(item.g_ElementInfoClass.EIF_ZIndex, item.g_ElementInfoClass.EIF_Name);
            }

            var list = dics.Keys.ToList();
            list.Sort();

            foreach (int key in list)
            {
                string name = dics[key];
                g_ChildNameListForZidx.Remove(name);
                g_ChildNameListForZidx.Add(name);
            }

            ReOrderingZorder();
        }

        void BTN0DO_Copy1_Click(object sender, RoutedEventArgs e)
        {
            AddScrollTextElement();
            LockCheckBox.IsChecked = false;
        }

        void BTN0DO_Copy_Click(object sender, RoutedEventArgs e)  // Display 객체 추가
        {
           // AddDiplayElement();
            AddDiplayElement(DisplayType.Media);
            LockCheckBox.IsChecked = false;
        }

        void NeedGuideCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            g_CurrentPageInfo.PIC_NeedGuide = false;
            BTN0DO_Copy2.IsEnabled = false;
            RefreshGuides(g_CurrentPageInfo.PIC_Rows, g_CurrentPageInfo.PIC_Columns);

            if (ScreenGuideGrid != null)
            {
                ScreenGuideGrid.GridLineBrush = ColorTools.GetSolidBrushByColorString("#FFFFFFFF");
            }
            if (ResolutionGuideGrid != null)
            {
                ResolutionGuideGrid.GridLineBrush = ColorTools.GetSolidBrushByColorString("#FFFFFFFF");
            }

            //if (NeedGuideCheckBox.IsChecked == true)
            //{
            //    GuideGrid.ShowGridLines = true;
            //}
            //else
            //{
            //    GuideGrid.ShowGridLines = false;
            //}
        }

        void NeedGuideCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            g_CurrentPageInfo.PIC_NeedGuide = true;
            BTN0DO_Copy2.IsEnabled = true;

            ////if (NeedGuideCheckBox.IsChecked == true)
            ////{
            ////    GuideGrid.ShowGridLines = true;
            ////}
            ////else
            ////{
            ////    GuideGrid.ShowGridLines = false;
            ////}

            RefreshGuides(g_CurrentPageInfo.PIC_Rows, g_CurrentPageInfo.PIC_Columns);

            if (ScreenGuideGrid != null)
            {
                ScreenGuideGrid.GridLineBrush = ColorTools.GetSolidBrushByColorString("#3FC1CAD8");
            }
        }


        public void ClearAllChildDataOfCanvas()
        {
            g_ChildNameListForZidx.Clear();
            g_DspElmtList.Clear();
            g_ScrollTextForEditorList.Clear();
            g_WelcomBoardForEditorList.Clear();

            Grid tmpGrid = new Grid();

            foreach (UIElement item in this.DesignerCanvas.Children)
            {
                if (item is Grid)
                {
                    tmpGrid = (Grid)item;
                    break;
                }
            }
            this.DesignerCanvas.Children.Clear();
            this.DesignerCanvas.Children.Add(tmpGrid);
        }

        public void LoadSelectedPage(string pageName)
        {
            ClearAllChildDataOfCanvas();

            MediaListBox.Items.Clear();
            //ListBoxForContentsList.Items.Clear();
            //HDTVChannelStackPannel.Children.Clear();
            ScrollTextStackPanel.Children.Clear();
            //ListBoxForScrollTest.Items.Clear();
            PageInfoClass definition = DataShop.Instance.g_PageInfoManager.GetPageDefinition(pageName);
            if (definition == null)
            {
                MessageTools.ShowMessageBox("저장된 화면구성을 찾을 수 없습니다.", "확인");
                return;
            }

            g_CurrentPageInfo = new PageInfoClass();
            g_CurrentPageInfo.CopyData(definition);

            NeedGuideCheckBox.IsChecked = g_CurrentPageInfo.PIC_NeedGuide;

            if (g_CurrentPageInfo.PIC_Rows <= 0) g_CurrentPageInfo.PIC_Rows = 1;
            if (g_CurrentPageInfo.PIC_Columns <= 0) g_CurrentPageInfo.PIC_Columns = 1;

            if (g_CurrentPageInfo.PIC_CanvasWidth <= 0 || g_CurrentPageInfo.PIC_CanvasHeight <= 0)
            {
                var canvasSize = ComputeCanvasSize(g_CurrentPageInfo.PIC_IsLandscape ? DeviceOrientation.Landscape : DeviceOrientation.Portrait, g_CurrentPageInfo.PIC_Rows, g_CurrentPageInfo.PIC_Columns);
                g_CurrentPageInfo.PIC_CanvasWidth = canvasSize.width;
                g_CurrentPageInfo.PIC_CanvasHeight = canvasSize.height;
            }

            ApplyResolutionToUI(g_CurrentPageInfo.PIC_IsLandscape ? DeviceOrientation.Landscape : DeviceOrientation.Portrait,
                g_CurrentPageInfo.PIC_Rows, g_CurrentPageInfo.PIC_Columns, g_CurrentPageInfo.PIC_CanvasWidth, g_CurrentPageInfo.PIC_CanvasHeight);

            List<ElementInfoClass> elements = ElementInfoControlClass.CloneElementList(definition.PIC_Elements);
            if (elements.Count > 0)
            {
                foreach (ElementInfoClass item in elements)
                {
                    switch ((DisplayType)Enum.Parse(typeof(DisplayType), item.EIF_Type))
                    {
                        case DisplayType.Media:
                        case DisplayType.HDTV:
                        case DisplayType.IPTV:
                            if (item.EIF_ContentsInfoClassList.Count > 0)
                            {
                                foreach (ContentsInfoClass contentInfo in item.EIF_ContentsInfoClassList)
                                {
                                    if (string.IsNullOrEmpty(contentInfo.CIF_FileFullPath))
                                    {
                                        contentInfo.CIF_FileFullPath = FNDTools.GetTargetContentsFilePath(contentInfo.CIF_FileName);
                                    }

                                    EnsureContentFileInContents(contentInfo);
                                }
                            }

                            LoadDisplayElementFromData(item, false);
                            break;

                        case DisplayType.ScrollText:
                            LoadScrollTextElementFromData(item);
                            break;

                        case DisplayType.WelcomeBoard:
                            LoadTextElementFromData(item, pageName);
                            break;
                    }
                }

                RenameDisplayElementText();

                TextBoxNewPlayerName_Copy.Text = pageName;
                SetPageNameNormalForeground();
                ReOrederingZorderForLoadingPage();

                HideAllListGrid();
            }
        }

        private void EnsureContentFileInContents(ContentsInfoClass content)
        {
            if (content == null)
            {
                return;
            }

            string fileName = content.CIF_FileName;
            if (string.IsNullOrWhiteSpace(fileName) && string.IsNullOrWhiteSpace(content.CIF_FileFullPath) == false)
            {
                fileName = Path.GetFileName(content.CIF_FileFullPath);
                content.CIF_FileName = fileName;
            }

            if (string.IsNullOrWhiteSpace(fileName))
            {
                return;
            }

            string targetPath = FNDTools.GetTargetContentsFilePath(fileName);
            if (File.Exists(targetPath))
            {
                return;
            }

            string sourcePath = content.CIF_FileFullPath;
            if (string.IsNullOrWhiteSpace(sourcePath) || File.Exists(sourcePath) == false)
            {
                return;
            }

            try
            {
                string targetDir = Path.GetDirectoryName(targetPath);
                if (string.IsNullOrWhiteSpace(targetDir) == false)
                {
                    Directory.CreateDirectory(targetDir);
                }
                FileTools.CopyFile(sourcePath, targetPath);
            }
            catch (Exception ex)
            {
                Logger.WriteErrorLog(ex.ToString(), Logger.GetLogFileName());
            }
        }

        public void LoadDisplayElementFromData(ElementInfoClass paramCls, bool isHDTV)
        {
            DisplayElementForEditor dspElement = new DisplayElementForEditor();

            dspElement.Width = paramCls.EIF_Width;
            dspElement.Height = paramCls.EIF_Height;
            dspElement.Name = paramCls.EIF_Name;

            Canvas.SetLeft(dspElement, paramCls.EIF_PosLeft);
            Canvas.SetTop(dspElement, paramCls.EIF_PosTop);

            if (MainWindow.Instance.isPortraitEditor)
            {
                dspElement.Width *= MainWindow.Instance.g_wPortScale;
                dspElement.Height *= MainWindow.Instance.g_hPortScale;
                Canvas.SetLeft(dspElement, paramCls.EIF_PosLeft * MainWindow.Instance.g_wPortScale);
                Canvas.SetTop(dspElement, paramCls.EIF_PosTop * MainWindow.Instance.g_hPortScale);
            }

            DesignerCanvas.Children.Add(dspElement);

            dspElement.UpdateElemenetnInfoCls(paramCls);

            g_DspElmtList.Add(dspElement);
        }


        public int GetPageTotalRunningTime()
        { 
            // 페이지에 Display객체가 하나이상일 경우 각 Display객체에 할당된 컨텐츠 런닝타임의 총합이 제일 큰 Display객체의
            // 런닝타임이 이페이지의 런닝타임이 된다.
            int timeFinalTotal = 0;
            int timeMidTotal = 0;
            if (g_DspElmtList.Count > 0)
            {
                for (int i = 0; i < g_DspElmtList.Count; i++)
                {
                    timeMidTotal = 0;
                    if (g_DspElmtList[i].g_ElementInfoClass.EIF_ContentsInfoClassList.Count > 0)
                    {

                        foreach (ContentsInfoClass item in g_DspElmtList[i].g_ElementInfoClass.EIF_ContentsInfoClassList)
                        {
                            //int timeVal = Int32.Parse(string.Format("{0}{1}", item.CIF_PlayMinute, item.CIF_PlaySec));

                            int timeVal = (Int32.Parse(item.CIF_PlayMinute) * 60) + Int32.Parse(item.CIF_PlaySec);

                            timeMidTotal = timeMidTotal + timeVal;
                        }
                    }

                    if (timeFinalTotal < timeMidTotal)
                    {
                        timeFinalTotal = timeMidTotal;
                    }
                }
            }
            else  // 컨텐츠 없이 웰컴보드만 띄울수도 있기때문에 시간을 설정해주어야한다. 안그러면 플레이어에서 오류난다.
            {
                timeFinalTotal = 10;
            }

        
            return timeFinalTotal;
        }

        public void SaveCurrentPage()
        {
            try
            {
                if (g_DspElmtList.Count > 0)
                {
                    foreach (DisplayElementForEditor item in g_DspElmtList)
                    {
                        if(item.g_ElementInfoClass.EIF_ContentsInfoClassList.Count < 1)
                        {
                            MessageTools.ShowMessageBox(string.Format("{0}에 할당된 컨텐츠가 없습니다.", item.TextDisplayName.Text), "확인");
                            return;
                        }
                    }
                }

                if(g_ScrollTextForEditorList.Count > 0)
                {
                    foreach (ScrollTextForEditor item in g_ScrollTextForEditorList)
                    {
                        if (item.g_ElementInfoClass.EIF_ContentsInfoClassList.Count < 1)
                        {
                            MessageTools.ShowMessageBox(string.Format("{0}에 할당된 컨텐츠가 없습니다.", item.TextDisplayName.Text), "확인");
                            return;
                        }
                    }
                }

                if (g_DspElmtList.Count == 0 && g_ScrollTextForEditorList.Count == 0
                     && this.g_WelcomBoardForEditorList.Count == 0)
                {
                    MessageTools.ShowMessageBox("빈페이지는 저장할수 없습니다.", "확인");
                    return;
                }

                TextBoxNewPlayerName_Copy.Text = TextBoxNewPlayerName_Copy.Text.Trim();

                if (FileTools.HasWrongPathCharacter(TextBoxNewPlayerName_Copy.Text) || TextBoxNewPlayerName_Copy.Text.Contains(","))
                {
                    MessageTools.ShowMessageBox("페이지 이름에 (,) 또는 사용할 수 없는 특수문자가 포함되어있습니다.", "확인");
                    return;
                }

                if (string.IsNullOrEmpty(TextBoxNewPlayerName_Copy.Text) ||
                    TextBoxNewPlayerName_Copy.Text.Equals("DownLoads", StringComparison.CurrentCultureIgnoreCase) ||
                    TextBoxNewPlayerName_Copy.Text.Equals("Contents", StringComparison.CurrentCultureIgnoreCase) ||
                    TextBoxNewPlayerName_Copy.Text.Equals("Update", StringComparison.CurrentCultureIgnoreCase))
                {
                    MessageTools.ShowMessageBox("사용할수 없는 페이지 이름입니다.", "확인");
                    return;
                }

                string savedPageName = TextBoxNewPlayerName_Copy.Text;
                bool saveCompleted = false;
                bool pageExists = DataShop.Instance.g_PageListInfoManager.CheckExistSamename(savedPageName);

                /////////////////////////////////////////////////
                // 여기서 해당페이지의 총 플레이타임을 정한다.
                int runningTimeVal = GetPageTotalRunningTime();
                g_CurrentPageInfo.PIC_PlaytimeMinute = runningTimeVal / 60;
                g_CurrentPageInfo.PIC_PlaytimeSecond = runningTimeVal % 60;

                g_CurrentPageInfo.PIC_CanvasWidth = DesignerCanvas.Width;
                g_CurrentPageInfo.PIC_CanvasHeight = DesignerCanvas.Height;
                g_CurrentPageInfo.PIC_Rows = Math.Max(1, g_CurrentPageInfo.PIC_Rows);
                g_CurrentPageInfo.PIC_Columns = Math.Max(1, g_CurrentPageInfo.PIC_Columns);
                g_CurrentPageInfo.PIC_IsLandscape = !MainWindow.Instance.isPortraitEditor;

                //
                //////////////////////////////////////////////////
                if (pageExists)
                {
                    if (MessageTools.ShowMessageBox("같은 이름의 페이지가 존재합니다. 덮어쓰시겠습니까?", "예", "아니오") == false)
                    {
                        return;
                    }
                }

                List<ElementInfoClass> elementSnapshot = null;
                string thumbData = null;
                Action generateAssets = () =>
                {
                    elementSnapshot = BuildElementSnapshot(savedPageName);
                    thumbData = CaptureCompressedThumbnail(savedPageName, elementSnapshot);
                };

                List<CopyFileInfo> copyJobs = PrepareCopyFileJobs(savedPageName);
                if (copyJobs.Count > 0)
                {
                    SavingFileWindow copyWindow = new SavingFileWindow(copyJobs, generateAssets);
                    copyWindow.ShowDialog();
                }
                else
                {
                    generateAssets();
                }

                if (elementSnapshot == null)
                {
                    elementSnapshot = BuildElementSnapshot(savedPageName);
                }
                if (thumbData == null)
                {
                    thumbData = CaptureCompressedThumbnail(savedPageName, elementSnapshot);
                }
                PageInfoClass pageDefinition = CreatePageDefinition(savedPageName, elementSnapshot, thumbData);

                var savedDefinition = DataShop.Instance.g_PageInfoManager.SavePageDefinition(savedPageName, pageDefinition);
                Page2.Instance.SaveDefaultPlaylist(savedPageName, savedDefinition);
                saveCompleted = true;

                if (saveCompleted)
                {
                    PersistPageLayoutToPlaylists(savedPageName, elementSnapshot, thumbData);
                    Page3.Instance?.UpdatePlayListForPlayer();
                    MessageTools.ShowMessageBox(string.Format("{0}을(를) 저장했습니다.", savedPageName), "확인");
                    MainWindow.Instance.RefreshSavedPageList();
                }
            }
            catch (Exception ex)
            {
                Logger.WriteErrorLog(ex.ToString(), Logger.GetLogFileName());
            }
        }
        public void SavePagePreviewImage(string pageName)
        {
            /*
            ScaleTransform originScale = (ScaleTransform)DesignerCanvas1.RenderTransform;

            ScaleTransform scale = new ScaleTransform(1, 1);
            DesignerCanvas1.RenderTransform = scale;

            ExportToPng(FNDTools.GetPreviewImageFilePathByPageName(pageName), this.DesignerCanvas1);

         
            ///////////////////////////////////////////////////
            // 웰컴보드는 이미지를 저장해야한다.  <--- 이거 중요
            if (g_TextElementForPreviewList.Count > 0)
            {
                foreach (WelcomeBoardForEditor item in g_TextElementForPreviewList)
                {
                    Canvas.SetLeft(item, 0);
                    Canvas.SetTop(item, 0);

                }

                foreach (WelcomeBoardForEditor item in g_TextElementForPreviewList)
                {
                    string filePath = string.Format("{0}\\{1}",
                    FNDTools.GetPageFolderPathByPageName(pageName), item.g_TextInfoClass.CIF_DataImageFileName);
                    ExportToPngOfControl2(filePath, item);
                }
            }
            //
            ///////////////////////////////////////////////////

            AdjustCanvasSize();
            */
        }

        private List<CopyFileInfo> PrepareCopyFileJobs(string pageName)
        {
            List<CopyFileInfo> jobs = new List<CopyFileInfo>();
            HashSet<string> jobKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            AppendMediaCopyJobs(jobs, jobKeys, pageName);
            AppendWelcomeBoardCopyJobs(jobs, jobKeys, pageName);

            return jobs;
        }

        private void AppendMediaCopyJobs(List<CopyFileInfo> jobs, HashSet<string> jobKeys, string pageName)
        {
            foreach (DisplayElementForEditor display in g_DspElmtList)
            {
                if (display?.g_ElementInfoClass?.EIF_ContentsInfoClassList == null)
                {
                    continue;
                }

                foreach (ContentsInfoClass content in display.g_ElementInfoClass.EIF_ContentsInfoClassList)
                {
                    string sourcePath = string.IsNullOrWhiteSpace(content.CIF_FileFullPath)
                        ? ResolveContentFilePath(content)
                        : content.CIF_FileFullPath;
                    if (string.IsNullOrWhiteSpace(sourcePath) || File.Exists(sourcePath) == false)
                    {
                        continue;
                    }

                    string fileName = !string.IsNullOrWhiteSpace(content.CIF_FileName)
                        ? content.CIF_FileName
                        : Path.GetFileName(sourcePath);

                    if (string.IsNullOrWhiteSpace(fileName))
                    {
                        fileName = $"{Guid.NewGuid():N}{Path.GetExtension(sourcePath)}";
                    }

                    string destinationPath = FNDTools.GetTargetContentsFilePath(fileName);

                    if (sourcePath.Equals(destinationPath, StringComparison.OrdinalIgnoreCase) == false)
                    {
                        string jobKey = $"{sourcePath}|{destinationPath}";
                        if (jobKeys.Add(jobKey))
                        {
                            jobs.Add(new CopyFileInfo
                            {
                                CFI_FileName = fileName,
                                CFI_FileSourceFullPath = sourcePath,
                                CFI_TargetFileName = destinationPath,
                                CFI_PageName = pageName
                            });
                        }
                    }

                    content.CIF_FileName = fileName;
                }
            }
        }

        private void AppendWelcomeBoardCopyJobs(List<CopyFileInfo> jobs, HashSet<string> jobKeys, string pageName)
        {
            foreach (WelcomeBoardForEditor welcome in g_WelcomBoardForEditorList)
            {
                TextInfoClass textInfo = welcome?.g_TextInfoClass;
                if (textInfo?.CIF_IsBGImageExist != true)
                {
                    continue;
                }

                string sourcePath = ResolveWelcomeBoardBackgroundPath(textInfo);
                if (string.IsNullOrWhiteSpace(sourcePath) || File.Exists(sourcePath) == false)
                {
                    textInfo.CIF_IsBGImageExist = false;
                    textInfo.CIF_BGImageFileFullPath = string.Empty;
                    continue;
                }

                string fileName = !string.IsNullOrWhiteSpace(textInfo.CIF_BGImageFileName)
                    ? textInfo.CIF_BGImageFileName
                    : Path.GetFileName(sourcePath);

                if (string.IsNullOrWhiteSpace(fileName))
                {
                    fileName = $"{Guid.NewGuid():N}{Path.GetExtension(sourcePath)}";
                }

                string destinationPath = FNDTools.GetTargetContentsFilePath(fileName);

                if (sourcePath.Equals(destinationPath, StringComparison.OrdinalIgnoreCase) == false)
                {
                    string jobKey = $"{sourcePath}|{destinationPath}";
                    if (jobKeys.Add(jobKey))
                    {
                        jobs.Add(new CopyFileInfo
                        {
                            CFI_FileName = fileName,
                            CFI_FileSourceFullPath = sourcePath,
                            CFI_TargetFileName = destinationPath,
                            CFI_PageName = pageName
                        });
                    }
                }

                textInfo.CIF_BGImageFileName = fileName;
                textInfo.CIF_IsBGImageExist = true;
            }
        }

        private List<ElementInfoClass> BuildElementSnapshot(string pageName)
        {
            List<ElementInfoClass> elements = new List<ElementInfoClass>();

            foreach (DisplayElementForEditor display in g_DspElmtList)
            {
                if (display?.g_ElementInfoClass == null)
                {
                    continue;
                }

                ElementInfoClass clone = new ElementInfoClass();
                clone.CopyData(display.g_ElementInfoClass);
                elements.Add(clone);
            }

            foreach (ScrollTextForEditor scroll in g_ScrollTextForEditorList)
            {
                if (scroll?.g_ElementInfoClass == null)
                {
                    continue;
                }

                ElementInfoClass clone = new ElementInfoClass();
                clone.CopyData(scroll.g_ElementInfoClass);
                elements.Add(clone);
            }

            foreach (WelcomeBoardForEditor welcome in g_WelcomBoardForEditorList)
            {
                if (welcome?.g_ElementInfoClass == null)
                {
                    continue;
                }

                ElementInfoClass clone = new ElementInfoClass();
                clone.CopyData(welcome.g_ElementInfoClass);
                elements.Add(clone);

                if (welcome.g_TextInfoClass != null)
                {
                    DataShop.Instance.g_TextInfoManager.AddDataInfo(welcome.g_TextInfoClass, pageName, clone.EIF_Name);
                }
            }

            return elements;
        }

        private PageInfoClass CreatePageDefinition(string pageName, List<ElementInfoClass> elementSnapshot, string thumbData)
        {
            var definition = new PageInfoClass
            {
                PIC_PageName = pageName,
                PIC_PlaytimeMinute = g_CurrentPageInfo.PIC_PlaytimeMinute,
                PIC_PlaytimeSecond = g_CurrentPageInfo.PIC_PlaytimeSecond,
                PIC_Rows = g_CurrentPageInfo.PIC_Rows,
                PIC_Columns = g_CurrentPageInfo.PIC_Columns,
                PIC_IsLandscape = g_CurrentPageInfo.PIC_IsLandscape,
                PIC_CanvasWidth = g_CurrentPageInfo.PIC_CanvasWidth,
                PIC_CanvasHeight = g_CurrentPageInfo.PIC_CanvasHeight,
                PIC_NeedGuide = g_CurrentPageInfo.PIC_NeedGuide,
                PIC_Thumb = thumbData
            };

            definition.PIC_Elements = ElementInfoControlClass.CloneElementList(elementSnapshot ?? new List<ElementInfoClass>());
            return definition;
        }

        private void PersistPageLayoutToPlaylists(string pageName, List<ElementInfoClass> elementSnapshot, string thumbData)
        {
            if (string.IsNullOrEmpty(pageName))
            {
                return;
            }

            if (elementSnapshot == null)
            {
                elementSnapshot = new List<ElementInfoClass>();
            }

            if (string.IsNullOrEmpty(thumbData))
            {
                thumbData = CaptureCompressedThumbnail(pageName, elementSnapshot);
            }

            bool isLandscape = g_CurrentPageInfo.PIC_IsLandscape;

            foreach (PageListInfoClass item in DataShop.Instance.g_PageListInfoManager.g_PageListInfoClassList)
            {
                DataShop.Instance.g_PageInfoManager.LoadPagesForList(item.PLI_PageListName);

                bool requiresSave = false;
                foreach (PageInfoClass pageInfo in DataShop.Instance.g_PageInfoManager.g_PageInfoClassList)
                {
                    if (!pageInfo.PIC_PageName.Equals(pageName, StringComparison.CurrentCultureIgnoreCase))
                    {
                        continue;
                    }

                    pageInfo.PIC_PlaytimeMinute = g_CurrentPageInfo.PIC_PlaytimeMinute;
                    pageInfo.PIC_PlaytimeSecond = g_CurrentPageInfo.PIC_PlaytimeSecond;
                    pageInfo.PIC_Rows = g_CurrentPageInfo.PIC_Rows;
                    pageInfo.PIC_Columns = g_CurrentPageInfo.PIC_Columns;
                    pageInfo.PIC_IsLandscape = isLandscape;
                    pageInfo.PIC_CanvasWidth = g_CurrentPageInfo.PIC_CanvasWidth;
                    pageInfo.PIC_CanvasHeight = g_CurrentPageInfo.PIC_CanvasHeight;
                    pageInfo.PIC_NeedGuide = g_CurrentPageInfo.PIC_NeedGuide;
                    pageInfo.PIC_Elements = ElementInfoControlClass.CloneElementList(elementSnapshot);
                    pageInfo.PIC_Thumb = thumbData;
                    requiresSave = true;
                }

                if (requiresSave)
                {
                    DataShop.Instance.g_PageInfoManager.SavePageList(item.PLI_PageListName);
                }
            }
        }

        private string CaptureCompressedThumbnail(string pageName, List<ElementInfoClass> elements)
        {
            try
            {
                double sourceWidth = Math.Max(1, g_CurrentPageInfo.PIC_CanvasWidth);
                double sourceHeight = Math.Max(1, g_CurrentPageInfo.PIC_CanvasHeight);

                bool isLandscape = g_CurrentPageInfo.PIC_IsLandscape;

                const double longEdge = 260d;
                const double shortEdge = 146d;

                double thumbWidth = isLandscape ? longEdge : shortEdge;
                double thumbHeight = isLandscape ? shortEdge : longEdge;

                double scale = Math.Min(thumbWidth / sourceWidth, thumbHeight / sourceHeight);
                if (scale > 1d)
                {
                    scale = 1d;
                }

                double contentWidth = Math.Max(1, sourceWidth * scale);
                double contentHeight = Math.Max(1, sourceHeight * scale);

                double offsetX = (thumbWidth - contentWidth) / 2d;
                double offsetY = (thumbHeight - contentHeight) / 2d;


                List<ElementInfoClass> previewElements = elements;
                if (!isLandscape && elements != null && elements.Count > 0)
                {
                    previewElements = new List<ElementInfoClass>(elements.Count);
                    foreach (ElementInfoClass element in elements)
                    {
                        ElementInfoClass previewElement = CreatePortraitPreviewElement(element);
                        if (previewElement != null)
                        {
                            previewElements.Add(previewElement);
                        }
                    }
                }

                DrawingVisual visual = new DrawingVisual();
                using (DrawingContext drawingContext = visual.RenderOpen())
                {
                    SolidColorBrush canvasBrush = new SolidColorBrush(Color.FromRgb(0x12, 0x12, 0x12));
                    canvasBrush.Freeze();
                    drawingContext.DrawRectangle(canvasBrush, null, new Rect(0, 0, thumbWidth, thumbHeight));

                    if (previewElements != null && previewElements.Count > 0)
                    {
                        foreach (ElementInfoClass element in previewElements.OrderBy(x => x.EIF_ZIndex))
                        {
                            Rect destination = new Rect(
                                offsetX + (element.EIF_PosLeft * scale),
                                offsetY + (element.EIF_PosTop * scale),
                                Math.Max(1, element.EIF_Width * scale),
                                Math.Max(1, element.EIF_Height * scale));

                            DrawElementPreview(drawingContext, element, destination, pageName);
                        }
                    }
                }

                RenderTargetBitmap renderBitmap = new RenderTargetBitmap(
                    Math.Max(1, (int)Math.Ceiling(thumbWidth)),
                    Math.Max(1, (int)Math.Ceiling(thumbHeight)),
                    96d,
                    96d,
                    PixelFormats.Pbgra32);
                renderBitmap.Render(visual);

                JpegBitmapEncoder encoder = new JpegBitmapEncoder
                {
                    QualityLevel = 85
                };
                encoder.Frames.Add(BitmapFrame.Create(renderBitmap));

                using (var stream = new MemoryStream())
                {
                    encoder.Save(stream);
                    return Convert.ToBase64String(stream.ToArray());
                }
            }
            catch (Exception ex)
            {
                Logger.WriteErrorLog(ex.ToString(), Logger.GetLogFileName());
                return string.Empty;
            }
        }

        private ElementInfoClass CreatePortraitPreviewElement(ElementInfoClass source)
        {
            if (source == null)
            {
                return null;
            }

            ElementInfoClass clone = new ElementInfoClass();
            clone.CopyData(source);

            double wScale = MainWindow.Instance?.g_wPortScale ?? 0;
            double hScale = MainWindow.Instance?.g_hPortScale ?? 0;

            if (wScale <= 0 || hScale <= 0)
            {
                wScale = BasePortraitWidth / BaseLandscapeWidth;
                hScale = BasePortraitHeight / BaseLandscapeHeight;
            }

            clone.EIF_PosLeft *= wScale;
            clone.EIF_PosTop *= hScale;
            clone.EIF_Width *= wScale;
            clone.EIF_Height *= hScale;

            return clone;
        }

        private void DrawElementPreview(DrawingContext drawingContext, ElementInfoClass element, Rect destination, string pageName)
        {
            if (destination.Width <= 0 || destination.Height <= 0 || element == null)
            {
                return;
            }

            DisplayType displayType;
            if (Enum.TryParse(element.EIF_Type, out displayType) == false)
            {
                displayType = DisplayType.Media;
            }

            switch (displayType)
            {
                case DisplayType.Media:
                case DisplayType.HDTV:
                case DisplayType.IPTV:
                    BitmapSource mediaSource = BuildMediaPreview(element);
                    if (mediaSource != null)
                    {
                        drawingContext.DrawImage(mediaSource, destination);
                    }
                    else
                    {
                        DrawPlaceholder(drawingContext, destination, displayType.ToString());
                    }
                    break;

                case DisplayType.ScrollText:
                    DrawScrollTextPreview(drawingContext, element, destination);
                    break;

                case DisplayType.WelcomeBoard:
                    DrawWelcomeBoardPreview(drawingContext, element, destination, pageName);
                    break;

                default:
                    DrawPlaceholder(drawingContext, destination, element.EIF_Type);
                    break;
            }
        }

        private BitmapSource BuildMediaPreview(ElementInfoClass element)
        {
            if (element.EIF_ContentsInfoClassList == null || element.EIF_ContentsInfoClassList.Count == 0)
            {
                return null;
            }

            foreach (ContentsInfoClass content in element.EIF_ContentsInfoClassList)
            {
                string filePath = ResolveContentFilePath(content);
                if (string.IsNullOrEmpty(filePath))
                {
                    continue;
                }

                try
                {
                    if (MediaTools.CheckIsImageFile(filePath))
                        return MediaTools.GetBitmapSourceFromFile(filePath);

                    if (MediaTools.CheckIsVideoFile(filePath))
                    {
                        BitmapSource videoThumb = MediaTools.GetVideoThumb(filePath);
                        videoThumb?.Freeze();
                        return videoThumb;
                    }
                }
                catch (Exception ex)
                {
                    Logger.WriteErrorLog(ex.ToString(), Logger.GetLogFileName());
                }
            }

            return null;
        }

        private static string ResolveContentFilePath(ContentsInfoClass content)
        {
            return FNDTools.GetContentFilePath(content);
        }

        private void DrawScrollTextPreview(DrawingContext drawingContext, ElementInfoClass element, Rect destination)
        {
            ContentsInfoClass content = element.EIF_ContentsInfoClassList?.FirstOrDefault();
            string text = content != null ? content.CIF_FileName : string.Empty;
            if (string.IsNullOrWhiteSpace(text))
            {
                text = "SCROLL TEXT";
            }

            Color foreground = ParseColorOrDefault(content?.CIF_PlayMinute, Colors.White);
            Color background = ParseColorOrDefault(content?.CIF_PlaySec, Color.FromRgb(0x22, 0x22, 0x22));

            SolidColorBrush bgBrush = new SolidColorBrush(background);
            bgBrush.Freeze();
            SolidColorBrush fgBrush = new SolidColorBrush(foreground);
            fgBrush.Freeze();

            drawingContext.DrawRectangle(bgBrush, null, destination);

            Typeface typeface = new Typeface(content != null ? content.CIF_ContentType : "Malgun Gothic");
            double fontSize = Math.Max(12, destination.Height * 0.4);

            FormattedText formattedText = new FormattedText(
                text,
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                typeface,
                fontSize,
                fgBrush);
            formattedText.MaxTextWidth = Math.Max(1, destination.Width - 8);
            formattedText.MaxTextHeight = Math.Max(1, destination.Height - 4);

            double textX = destination.X + (destination.Width - formattedText.Width) / 2;
            double textY = destination.Y + (destination.Height - formattedText.Height) / 2;
            drawingContext.DrawText(formattedText, new Point(textX, textY));
        }

        private void DrawWelcomeBoardPreview(DrawingContext drawingContext, ElementInfoClass element, Rect destination, string pageName)
        {
            TextInfoClass textInfo = null;
            var welcomeBoard = g_WelcomBoardForEditorList.FirstOrDefault(x => x.g_ElementInfoClass.EIF_Name == element.EIF_Name);
            if (welcomeBoard != null)
            {
                textInfo = welcomeBoard.g_TextInfoClass;
            }
            else if (string.IsNullOrEmpty(pageName) == false)
            {
                try
                {
                    TextInfoManager infoManager = new TextInfoManager();
                    infoManager.LoadTextInfo(pageName, element.EIF_Name);
                    textInfo = infoManager.g_DataClassList.FirstOrDefault()
                        ?? new TextInfoClass { CIF_PageName = pageName, CIF_DataFileName = element.EIF_Name };
                }
                catch (Exception ex)
                {
                    Logger.WriteErrorLog(ex.ToString(), Logger.GetLogFileName());
                }
            }

            string text = textInfo != null ? textInfo.CIF_TextContent : string.Empty;
            if (string.IsNullOrWhiteSpace(text))
            {
                text = "WELCOME";
            }

            Color foreground = ParseColorOrDefault(textInfo?.CIF_FontColor, Colors.White);
            Color background = ParseColorOrDefault(textInfo?.CIF_BGColor, Color.FromRgb(0x33, 0x33, 0x33));

            Brush bgBrush = BuildWelcomeBoardBackgroundBrush(textInfo, background);
            SolidColorBrush fgBrush = new SolidColorBrush(foreground);
            fgBrush.Freeze();

            drawingContext.DrawRectangle(bgBrush, null, destination);

            Typeface typeface = BuildWelcomeBoardTypeface(textInfo);
            double fontSize = CalculateWelcomeBoardFontSize(textInfo, element, destination);
            FormattedText formattedText = new FormattedText(
                text,
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                typeface,
                fontSize,
                fgBrush);
            formattedText.MaxTextWidth = Math.Max(1, destination.Width - 12);
            formattedText.MaxTextHeight = Math.Max(1, destination.Height - 12);

            double textX = destination.X + (destination.Width - formattedText.Width) / 2;
            double textY = destination.Y + (destination.Height - formattedText.Height) / 2;
            drawingContext.DrawText(formattedText, new Point(textX, textY));
        }

        [Obsolete]
        private void DrawPlaceholder(DrawingContext drawingContext, Rect destination, string label)
        {
            SolidColorBrush bgBrush = new SolidColorBrush(Color.FromRgb(0x1F, 0x1F, 0x1F));
            bgBrush.Freeze();
            SolidColorBrush borderBrush = new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55));
            borderBrush.Freeze();
            Pen borderPen = new Pen(borderBrush, Math.Max(1, Math.Min(destination.Width, destination.Height) * 0.02));
            borderPen.Freeze();

            drawingContext.DrawRectangle(bgBrush, borderPen, destination);

            if (string.IsNullOrWhiteSpace(label))
            {
                return;
            }

            SolidColorBrush textBrush = new SolidColorBrush(Color.FromRgb(0xCC, 0xCC, 0xCC));
            textBrush.Freeze();

            FormattedText formattedText = new FormattedText(
                label,
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface("Segoe UI"),
                Math.Max(12, destination.Height * 0.3),
                textBrush);
            formattedText.MaxTextWidth = Math.Max(1, destination.Width - 8);
            formattedText.MaxTextHeight = Math.Max(1, destination.Height - 8);

            double textX = destination.X + (destination.Width - formattedText.Width) / 2;
            double textY = destination.Y + (destination.Height - formattedText.Height) / 2;
            drawingContext.DrawText(formattedText, new Point(textX, textY));
        }

        private Brush BuildWelcomeBoardBackgroundBrush(TextInfoClass textInfo, Color fallbackColor)
        {
            if (textInfo?.CIF_IsBGImageExist == true)
            {
                string filePath = ResolveWelcomeBoardBackgroundPath(textInfo);
                if (File.Exists(filePath))
                    return MediaTools.CreateBrushFromImgPath(filePath);
            }

            SolidColorBrush fallbackBrush = new SolidColorBrush(fallbackColor);
            fallbackBrush.Freeze();
            return fallbackBrush;
        }

        private static string ResolveWelcomeBoardBackgroundPath(TextInfoClass textInfo)
        {
            return FNDTools.GetWelcomeBoardBackgroundPath(textInfo);
        }

        private static Typeface BuildWelcomeBoardTypeface(TextInfoClass textInfo)
        {
            FontFamily family;
            try
            {
                family = new FontFamily(textInfo?.CIF_FontName ?? "Malgun Gothic");
            }
            catch
            {
                family = new FontFamily("Malgun Gothic");
            }
            FontStyle style = (textInfo?.CIF_IsItalic ?? false) ? FontStyles.Italic : FontStyles.Normal;
            FontWeight weight = (textInfo?.CIF_IsBold ?? false) ? FontWeights.Bold : FontWeights.Normal;
            return new Typeface(family, style, weight, FontStretches.Normal);
        }

        private static double CalculateWelcomeBoardFontSize(TextInfoClass textInfo, ElementInfoClass element, Rect destination)
        {
            double fallback = Math.Max(16, destination.Height * 0.35);
            if (textInfo == null || element == null || element.EIF_Height <= 0)
            {
                return fallback;
            }

            double scale = destination.Height / element.EIF_Height;
            if (double.IsNaN(scale) || double.IsInfinity(scale) || scale <= 0)
            {
                scale = 1d;
            }

            double requestedSize = textInfo.CIF_FontSize * scale;
            if (requestedSize <= 0)
            {
                return fallback;
            }

            return Math.Max(12, requestedSize);
        }

        private static Color ParseColorOrDefault(string colorCode, Color fallback)
        {
            if (string.IsNullOrWhiteSpace(colorCode) == false)
            {
                try
                {
                    return (Color)ColorConverter.ConvertFromString(colorCode);
                }
                catch (Exception)
                {
                }
            }

            return fallback;
        }

        public void RefreshSavedPageList()
        {
            List<PageInfoClass> savedPages = DataShop.Instance.g_PageInfoManager.GetAllSavedPages();
            ContentsElementsStackPannel2.Children.Clear();

            if (savedPages.Count == 0)
            {
                ContentsListScrollViewer2.ScrollToBottom();
                return;
            }

            int pageIdx = 1;
            foreach (PageInfoClass pageInfo in savedPages)
            {
                string pageName = pageInfo.PIC_PageName;

                SavedPageElement tmpElement = new SavedPageElement();
                tmpElement.g_PreviewThumbBase64 = pageInfo.PIC_Thumb;
                tmpElement.Width = 267;

                if (MainWindow.Instance.isPortraitEditor)
                {
                    if (MainWindow.Instance.Width < 1280)
                    {
                        tmpElement.Width = 242;
                    }
                    else
                        tmpElement.Width = 222;
                }

                tmpElement.Height = 25;
                tmpElement.Margin = new Thickness(4, 3, 0, 0);

                tmpElement.pageNameTextBlock.Text = pageName;
                tmpElement.pageNameTextBlock_Copy.Text = pageIdx.ToString();
                ContentsElementsStackPannel2.Children.Add(tmpElement);

                pageIdx++;
            }

            ContentsListScrollViewer2.ScrollToBottom();
        }

        public void HideDepartuerAddPanel()
        {
            DepartuerAddGrid_Copy1.Visibility = Visibility.Hidden;
        }

        private void DepartuerClose_Click(object sender, RoutedEventArgs e)
        {
            HideDepartuerAddPanel();
        }

        private void InitPageNamePlaceholder()
        {
            if (TextBoxNewPlayerName_Copy == null)
                return;

            if (string.IsNullOrWhiteSpace(TextBoxNewPlayerName_Copy.Text) ||
                TextBoxNewPlayerName_Copy.Text.Equals(PageNamePlaceholderText, StringComparison.CurrentCulture))
            {
                SetPageNamePlaceholder();
            }
            else
            {
                SetPageNameNormalForeground();
            }
        }

        private void SetPageNamePlaceholder()
        {
            TextBoxNewPlayerName_Copy.Text = PageNamePlaceholderText;
            TextBoxNewPlayerName_Copy.Foreground = _pageNamePlaceholderBrush;
        }

        private void SetPageNameNormalForeground()
        {
            TextBoxNewPlayerName_Copy.Foreground = _pageNameNormalBrush;
        }

        private void TextBoxNewPlayerName_Copy_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            if (TextBoxNewPlayerName_Copy.Text.Equals(PageNamePlaceholderText, StringComparison.CurrentCulture))
            {
                TextBoxNewPlayerName_Copy.Text = string.Empty;
            }

            SetPageNameNormalForeground();
        }

        private void TextBoxNewPlayerName_Copy_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TextBoxNewPlayerName_Copy.Text))
            {
                SetPageNamePlaceholder();
            }
            else
            {
                SetPageNameNormalForeground();
            }
        }
        
        public void StartPreviewPlay()
        {
            if (g_DspElmtList.Count == 0 && g_ScrollTextForEditorList.Count == 0
                 && this.g_WelcomBoardForEditorList.Count == 0)
            {
                MessageTools.ShowMessageBox("미리보기를 재생할 객체가 없습니다.", "확인");
                return;
            }

            if (MessageTools.ShowMessageBox("미리보기를 재생하시겠습니까?", "예", "아니오") == true)
            {
                SavePreviewFiles();

                string viewerPath = FNDTools.GetPageViewerPath();
                if (File.Exists(viewerPath))
                {
                    string previewArgs = g_CurrentPageInfo.PIC_IsLandscape ? "1280 720" : "720 1280";
                    ProcessTools.LaunchProcess(viewerPath, false, previewArgs);
                }
                else
                {
                    ShowPreview();
                }
            }
        }

        public void ShowPreview()
        {
            PagePreviewWindow pagePreviewWnd = new PagePreviewWindow(false, string.Empty);
            pagePreviewWnd.ShowDialog();

            /*
            if (g_DspElmtList.Count > 0)
            {
                foreach (DisplayElementForEditor item in g_DspElmtList)
                {
                    CreateVideoAndImageForPreview(item.g_ElementInfoClass);
                }
            }

            if (g_ScrollTextForEditorList.Count > 0)
            {
                foreach (ScrollTextForEditor item in g_ScrollTextForEditorList)
                {
                    CreateScrollTextForPreview(item.g_ElementInfoClass);
                }
            }

            if (this.g_WelcomBoardForEditorList.Count > 0)
            {
                foreach (WelcomeBoardForEditor item in g_WelcomBoardForEditorList)
                {
                    CreateTextElementForPreview(item.g_ElementInfoClass, item.g_TextInfoClass);
                }
            }
            */
        }

        void SavePreviewFiles()
        {
            PreviewCanvas canvas = new PreviewCanvas();
            canvas.Direction = g_CurrentPageInfo.PIC_IsLandscape ? DeviceOrientation.Landscape.ToString() : DeviceOrientation.Portrait.ToString();
            canvas.Rows = Math.Max(1, g_CurrentPageInfo.PIC_Rows);
            canvas.Columns = Math.Max(1, g_CurrentPageInfo.PIC_Columns);
            canvas.Width = Math.Max(1, g_CurrentPageInfo.PIC_CanvasWidth);
            canvas.Height = Math.Max(1, g_CurrentPageInfo.PIC_CanvasHeight);
            canvas.FillContent = !DataShop.Instance.g_ServerSettingsManager.sData.PreserveAspectRatio;

            XmlTools.WriteXml(FNDTools.GetPreviewCanvasFilePath(), canvas);

            List<PreviewElement> elements = new List<PreviewElement>();

            foreach (DisplayElementForEditor item in g_DspElmtList)
            {
                PreviewElement element = new PreviewElement();

                element.Index = item.g_ElementInfoClass.EIF_ZIndex;
                element.ElementType = item.g_ElementInfoClass.EIF_Type;
                element.PosX = Convert.ToInt32(item.g_ElementInfoClass.EIF_PosLeft);
                element.PosY = Convert.ToInt32(item.g_ElementInfoClass.EIF_PosTop);
                element.Width = Convert.ToInt32(item.g_ElementInfoClass.EIF_Width);
                element.Height = Convert.ToInt32(item.g_ElementInfoClass.EIF_Height);

                foreach (ContentsInfoClass cic in item.g_ElementInfoClass.EIF_ContentsInfoClassList)
                {
                    PreviewData data = new PreviewData();
                    data.DataType = cic.CIF_ContentType;
                    data.FilePath = cic.CIF_ContentType == ContentType.WebSiteURL.ToString()
                        ? cic.CIF_FileName
                        : FNDTools.GetContentFilePath(cic);
                    data.Playtime = Convert.ToInt32(cic.CIF_PlayMinute) * 60 + Convert.ToInt32(cic.CIF_PlaySec);

                    element.DataList.Add(data);
                }

                elements.Add(element);
            }

            foreach (ScrollTextForEditor item in g_ScrollTextForEditorList)
            {
                PreviewElement element = new PreviewElement();

                element.Index = item.g_ElementInfoClass.EIF_ZIndex;
                element.ElementType = item.g_ElementInfoClass.EIF_Type;
                element.PosX = Convert.ToInt32(item.g_ElementInfoClass.EIF_PosLeft);
                element.PosY = Convert.ToInt32(item.g_ElementInfoClass.EIF_PosTop);
                element.Width = Convert.ToInt32(item.g_ElementInfoClass.EIF_Width);
                element.Height = Convert.ToInt32(item.g_ElementInfoClass.EIF_Height);

                string fontcolor = string.IsNullOrWhiteSpace(item.g_EditFontInfoClass.EFT_ForeGoundColor)
                    ? DefaultScrollTextForeground
                    : item.g_EditFontInfoClass.EFT_ForeGoundColor;
                string bgcolor = string.IsNullOrWhiteSpace(item.g_EditFontInfoClass.EFT_BackGoundColor)
                    ? DefaultScrollTextBackground
                    : item.g_EditFontInfoClass.EFT_BackGoundColor;
                string fontname = string.IsNullOrWhiteSpace(item.g_EditFontInfoClass.EFT_FontName)
                    ? DefaultScrollTextFontFamily
                    : item.g_EditFontInfoClass.EFT_FontName;

                foreach (ContentsInfoClass cic in item.g_ElementInfoClass.EIF_ContentsInfoClassList)
                {
                    PreviewData data = new PreviewData();
                    data.TextContent = cic.CIF_FileName;
                    data.DataType = DisplayType.ScrollText.ToString();
                    data.Playtime = Convert.ToInt32(cic.CIF_ScrollTextSpeedSec);
                    data.FontColor = fontcolor;
                    data.BGColor = bgcolor;
                    data.FontName = fontname;

                    element.DataList.Add(data);
                }

                elements.Add(element);
            }

            foreach (WelcomeBoardForEditor item in g_WelcomBoardForEditorList)
            {
                PreviewElement element = new PreviewElement();

                element.Index = item.g_ElementInfoClass.EIF_ZIndex;
                element.ElementType = item.g_ElementInfoClass.EIF_Type;
                element.PosX = Convert.ToInt32(item.g_ElementInfoClass.EIF_PosLeft);
                element.PosY = Convert.ToInt32(item.g_ElementInfoClass.EIF_PosTop);
                element.Width = Convert.ToInt32(item.g_ElementInfoClass.EIF_Width);
                element.Height = Convert.ToInt32(item.g_ElementInfoClass.EIF_Height);

                TextInfoClass tic = item.g_TextInfoClass;

                PreviewData data = new PreviewData();
                data.DataType = DisplayType.WelcomeBoard.ToString();
                data.TextContent = tic.CIF_TextContent;
                data.FontName = tic.CIF_FontName;
                data.FontColor = tic.CIF_FontColor;
                data.FontSize = tic.CIF_FontSize;
                data.IsBold = tic.CIF_IsBold;
                data.IsItalic = tic.CIF_IsItalic;
                data.BGColor = tic.CIF_BGColor;
                data.FilePath = FNDTools.GetWelcomeBoardBackgroundPath(tic);

                element.DataList.Add(data);

                elements.Add(element);
            }

            XmlTools.WriteXml(FNDTools.GetPreviewDataFilePath(), elements);
        }

        public void AddScrollTextToList()
        {
            if (ScrollTextTBox.Text == string.Empty)
            {
                MessageTools.ShowMessageBox("자막내용을 입력해주세요.", "확인");               
            }
            else
            {
                ContentsInfoClass temInfoClass = new ContentsInfoClass();
                temInfoClass.CIF_FileName = ScrollTextTBox.Text;
                temInfoClass.CIF_ScrollTextSpeedSec = (int)ScrollSpeedComboBox.SelectedValue;

                ScrollTextForEditor selectedScroll = g_CurrentSelectedElement as ScrollTextForEditor;
                EditFontInfoClass fontInfo = selectedScroll?.g_EditFontInfoClass;

                string foreground = fontInfo != null && string.IsNullOrWhiteSpace(fontInfo.EFT_ForeGoundColor) == false
                    ? fontInfo.EFT_ForeGoundColor
                    : DefaultScrollTextForeground;
                string background = fontInfo != null && string.IsNullOrWhiteSpace(fontInfo.EFT_BackGoundColor) == false
                    ? fontInfo.EFT_BackGoundColor
                    : DefaultScrollTextBackground;
                string fontName = fontInfo != null && string.IsNullOrWhiteSpace(fontInfo.EFT_FontName) == false
                    ? fontInfo.EFT_FontName
                    : DefaultScrollTextFontFamily;

                temInfoClass.CIF_PlayMinute = foreground;
                temInfoClass.CIF_PlaySec = background;
                temInfoClass.CIF_ContentType = fontName;

                UpdateScrollTextListToSelectedElement(temInfoClass);
            }
        }

        public void ChangeFontStyle(EditFontInfoClass paramCls)
        {
            if (this.g_ScrollTextForEditorList.Count > 0)
            {
                if (g_CurrentSelectedElement is ScrollTextForEditor)
                {
                    ScrollTextForEditor tmpControl = (ScrollTextForEditor)g_CurrentSelectedElement;

                    foreach (ScrollTextForEditor stfe in g_ScrollTextForEditorList)
                    {
                        if (tmpControl.g_ElementInfoClass.EIF_Name.Equals(stfe.g_ElementInfoClass.EIF_Name))
                        {
                            stfe.UpdateFontStyle(paramCls);
                            RememberScrollFontStyle(stfe.g_EditFontInfoClass);
                            break;
                        }
                    }
                }
            }
        }

        private void ApplyLatestScrollFontStyle(ScrollTextForEditor element)
        {
            if (element == null)
            {
                return;
            }

            element.UpdateFontStyle(_lastScrollTextFontStyle);
        }

        private void RememberScrollFontStyle(EditFontInfoClass style)
        {
            if (style == null)
            {
                return;
            }

            _lastScrollTextFontStyle.EFT_ForeGoundColor = string.IsNullOrWhiteSpace(style.EFT_ForeGoundColor)
                ? DefaultScrollTextForeground
                : style.EFT_ForeGoundColor;
            _lastScrollTextFontStyle.EFT_BackGoundColor = string.IsNullOrWhiteSpace(style.EFT_BackGoundColor)
                ? DefaultScrollTextBackground
                : style.EFT_BackGoundColor;
            _lastScrollTextFontStyle.EFT_FontName = string.IsNullOrWhiteSpace(style.EFT_FontName)
                ? DefaultScrollTextFontFamily
                : style.EFT_FontName;
        }

        public ContentType GetContentTypeHyon(string filePath)
        {
            ContentType type = ContentType.None;
            string fileExtension = new System.IO.FileInfo(filePath).Extension.ToLowerInvariant();

            if (videoExtentionSetList.Contains(fileExtension))
            {
                type = ContentType.Video;
            }
            else if (imageExtentionSetList.Contains(fileExtension))
            {
                type = ContentType.Image;
            }

            return type;
        }

        public void AddContentsToList()
        {
            Microsoft.Win32.OpenFileDialog openFileDialog = new Microsoft.Win32.OpenFileDialog();

            openFileDialog.Filter = "Media Files|*.jpg;*.png;*.bmp;*.gif;*.jpeg;*.avi;*.mp4;*.3gp;*.mov;*.mpg;*.mpeg;*.m2ts;*.ts;*.wmv;*.asf;";
            openFileDialog.FilterIndex = 1;     // FilterIndex는 1부터 시작
            openFileDialog.RestoreDirectory = true;

            openFileDialog.Multiselect = true;

            List<ContentsInfoClass> _cicList = new List<ContentsInfoClass>();
            List<string> NotValidContentList = new List<string>();
            NotValidContentList.Clear();

            try
            {
                if ((bool)openFileDialog.ShowDialog())
                {
                    foreach (string item in openFileDialog.FileNames)
                    {
                        if (CheckValidContentsType(item) == true)
                        {
                            ContentsInfoClass temInfoClass = new ContentsInfoClass();
                            temInfoClass.CIF_FileName = System.IO.Path.GetFileName(item);
                            temInfoClass.CIF_FileFullPath = item;
                            ContentType type = GetContentTypeHyon(item);
                            temInfoClass.CIF_ContentType = type.ToString();

                            try
                            {
                                switch (type)
                                {
                                    case ContentType.Video:

                                        TimeSpan _ts = MediaTools.GetVideoDuration(item);

                                        temInfoClass.CIF_PlayMinute = string.Format("{0:D2}", _ts.Minutes);
                                        temInfoClass.CIF_PlaySec = string.Format("{0:D2}", _ts.Seconds);
                                        break;
                                }
                            }
                            catch (Exception ex)
                            {
                                MessageTools.ShowMessageBox("영상은 목록에 추가했지만, 코덱이 맞지 않아 Running Time이 기본으로 설정됩니다.", "확인");
                                temInfoClass.CIF_PlayMinute = "00";
                                temInfoClass.CIF_PlaySec = "10";
                            }

                            _cicList.Add(temInfoClass);
                        }
                        else
                        {
                            NotValidContentList.Add(System.IO.Path.GetFileName(item));
                        }
                    }

                    AddContentsToSelectedElement(_cicList);

                    // 적합한 컨텐츠파일이 아닌것들은 안들어갔다고 Noti해준다.
                    if (NotValidContentList.Count > 0)
                    {
                        string msgStr = string.Empty;
                        foreach (string item in NotValidContentList)
                        {
                            msgStr = msgStr + item + ",";
                        }

                        msgStr = msgStr.Remove(msgStr.Length - 1);
                        MessageTools.ShowMessageBox(string.Format("다음 파일은 등록할 수 없습니다. \n {0}", msgStr));
                    }

                }
            }
            catch (System.Exception ex)
            {
                MessageTools.ShowMessageBox("File Open Error : " + ex.Message);
            }
        }

        public bool CheckValidContentsType(string filePath)
        {
            string strContentType = string.Empty;
            string fileExtension = new System.IO.FileInfo(filePath).Extension.ToLowerInvariant();

            if (videoExtentionSetList.Contains(fileExtension) ||
                imageExtentionSetList.Contains(fileExtension))
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        public void UpdateScrollTextListToSelectedElement(ContentsInfoClass temInfoClass)
        {
            if (g_CurrentSelectedObjName != string.Empty)
            {
                foreach (ScrollTextForEditor item in g_ScrollTextForEditorList)
                {
                    if (item.g_ElementInfoClass.EIF_Name == g_CurrentSelectedObjName)
                    {
                        item.AddContentInfoCls(temInfoClass);
                        break;
                    }

                }

            }
        }

        public void UpdateTextInfoClassToElement(TextInfoClass paramCls)
        {
            if (g_CurrentSelectedObjName != string.Empty)
            {
                foreach (WelcomeBoardForEditor item in g_WelcomBoardForEditorList)
                {
                    if (item.g_ElementInfoClass.EIF_Name == g_CurrentSelectedObjName)
                    {
                        item.UpdateTextInfoClsFromPage(paramCls);
                        break;
                    }

                }

            }
        }

        public void UpdateContentsListToSelectedElement(ContentsInfoClass temInfoClass)
        {
            if (g_CurrentSelectedObjName != string.Empty)
            {
                foreach (DisplayElementForEditor item in g_DspElmtList)
	            {
                    if (item.g_ElementInfoClass.EIF_Name == g_CurrentSelectedObjName)
                    {
                        item.AddContentInfoCls(temInfoClass);
                        break;  
                    }
	            }
            }
        }

        public void AddContentsToSelectedElement(List<ContentsInfoClass> clslist)
        {
            if (g_CurrentSelectedObjName != string.Empty)
            {
                foreach (DisplayElementForEditor item in g_DspElmtList)
                {
                    if (item.g_ElementInfoClass.EIF_Name == g_CurrentSelectedObjName)
                    {
                        foreach(ContentsInfoClass temInfoClass in clslist)
                            item.AddContentInfoCls(temInfoClass);

                        SetCurrentSelectedObjectName(item.Name, item.TextDisplayName.Text, item.g_ElementInfoClass);
                        break;
                    }
                }
            }
        }

        public void UpdateContentsListByEditWindow(List<ContentsInfoClass> paramList)
        {
            if (g_CurrentSelectedObjName != string.Empty)
            {
                foreach (DisplayElementForEditor item in g_DspElmtList)
                {
                    if (item.g_ElementInfoClass.EIF_Name == g_CurrentSelectedObjName)
                    {
                        item.RefreshContentsInfoList(paramList);
                        break;
                    }
                }

            }
        }

        public void RefreshScrollTextInfoList()
        {
            ScrollTextStackPanel.Children.Clear();

            if (this.g_SelectedCurElement.EIF_ContentsInfoClassList.Count > 0)
            {
                int idx = 1;
                foreach (ContentsInfoClass item in this.g_SelectedCurElement.EIF_ContentsInfoClassList)
                {
                    ScrollTextInfoElement tmpElement = new ScrollTextInfoElement(item);
                    tmpElement.TextBlockPageName.Text = item.CIF_FileName;
                    tmpElement.TextBlockOrderingNumber.Text = string.Format("{0:D2}", idx);
                    tmpElement.Margin = new Thickness(5, 2, 5, 0);
                    ScrollTextStackPanel.Children.Add(tmpElement);
                    idx++;
                }
            }
            SelectedContentInfo(g_CurSelContentsInfoClass, g_CurSelListType);
        }

        public void DeleteScrollTextList(ContentsInfoClass paramcls)
        {
            int idx = 0;
            foreach (ContentsInfoClass item in this.g_SelectedCurElement.EIF_ContentsInfoClassList)
            {
                if (item.CIF_StrGUID == paramcls.CIF_StrGUID)
                {
                    break;
                }
                idx++;
            }

            if (this.g_SelectedCurElement.EIF_ContentsInfoClassList.Count > 0)
            {
                if (idx == 0 || idx < this.g_SelectedCurElement.EIF_ContentsInfoClassList.Count)
                {
                    this.g_SelectedCurElement.EIF_ContentsInfoClassList.RemoveAt(idx);

                    foreach (ScrollTextForEditor item in g_ScrollTextForEditorList)
                    {
                        if (item.g_ElementInfoClass.EIF_Name == this.g_SelectedCurElement.EIF_Name)
                        {
                            item.g_ElementInfoClass.EIF_ContentsInfoClassList.RemoveAt(idx);
                            item.DisplayContentsListCount();
                            break;
                        }

                    }


                }
            }


            RefreshScrollTextInfoList();
        }

        public void DeleteContentsList(ContentsInfoClass paramcls)
        {
            int idx = 0;
            foreach (ContentsInfoClass item in this.g_SelectedCurElement.EIF_ContentsInfoClassList)
            {
                if (item.CIF_StrGUID == paramcls.CIF_StrGUID)
                {
                    break;
                }
                idx++;
            }

            if (this.g_SelectedCurElement.EIF_ContentsInfoClassList.Count > 0)
            {

                if (idx == 0 || idx < this.g_SelectedCurElement.EIF_ContentsInfoClassList.Count)
                {
                    this.g_SelectedCurElement.EIF_ContentsInfoClassList.RemoveAt(idx);

                    foreach (DisplayElementForEditor item in g_DspElmtList)
                    {
                        if (item.g_ElementInfoClass.EIF_Name == this.g_SelectedCurElement.EIF_Name)
                        {
                            item.g_ElementInfoClass.EIF_ContentsInfoClassList.RemoveAt(idx);
                            item.DisplayContentsListCount();
                            break;
                        }                        
                    }
                }
            }
           

            RefreshContentInfoList();
        }

        public void RefreshContentInfoList()
        {
            MediaListBox.Items.Clear();
            MediaListBox.UpdateLayout();

            if (this.g_SelectedCurElement.EIF_ContentsInfoClassList.Count > 0)
            {
                int idx = 1;
                foreach (ContentsInfoClass item in this.g_SelectedCurElement.EIF_ContentsInfoClassList)
                {
                    ContentInfoElement tmpElement = new ContentInfoElement(item);
                    tmpElement.Width = 360;
                    tmpElement.Height = 30;
                    tmpElement.TextBlockOrderingNumber.Text = string.Format("{0:D2}", idx);
                    tmpElement.Margin = new Thickness();
                    MediaListBox.Items.Add(tmpElement);
                    idx++;
                }
            }

            SelectedContentInfo(g_CurSelContentsInfoClass, g_CurSelListType);

            // 배경 아이콘 표시/숨김
            if (ContentsBgIcon != null)
            {
                ContentsBgIcon.Visibility =
                    MediaListBox.Items.Count == 0 ? Visibility.Visible : Visibility.Hidden;
            }
        }

        public void EditContentsPlayTime(ContentsInfoClass paramCls)
        {
            foreach (DisplayElementForEditor item in g_DspElmtList)
            {
                if (item.Name == this.g_CurrentSelectedObjName)
                {
                    foreach (ContentsInfoClass tempInfo in item.g_ElementInfoClass.EIF_ContentsInfoClassList)
                    {
                        if (tempInfo.CIF_StrGUID == paramCls.CIF_StrGUID)
                        {
                            tempInfo.CopyData(paramCls);
                            break;
                        }
                    }

                    break;
                }
            }
        }

     
        public void LineUpDisplayElementBase()
        {
            int childCnt = g_DspElmtList.Count;

            if (childCnt == 0)
            {
                return;
            }

            if (childCnt != lastchcnt)
                lineidx = 1;

            lastchcnt = childCnt;

            switch (childCnt)
            {
                case 1:
                    LineUpDisplay(1, 1);
                    lineidx = 1;
                    break;

                case 2:
                    switch (lineidx)
                    {
                        case 1:
                            LineUpDisplay(2, 1);
                            break;
                        case 2:
                            LineUpDisplay(1, 2);
                            lineidx = 0;
                            break;
                    }
                    break;
                case 3:
                    switch (lineidx)
                    {
                        case 1:
                            LineUpDisplay(3, 1);
                            break;
                        case 2:
                            LineUpDisplay(1, 3);
                            lineidx = 0;
                            break;
                    }
                    break;
                case 4:
                    switch (lineidx)
                    {
                        case 1:
                            LineUpDisplay(4, 1);
                            break;
                        case 2:
                            LineUpDisplay(1, 4);
                            break;
                        case 3:
                            LineUpDisplay(2, 2);
                            lineidx = 0;
                            break;
                    }
                    break;
                case 5:
                    switch (lineidx)
                    {
                        case 1:
                            LineUpDisplay(5, 1);
                            break;
                        case 2:
                            LineUpDisplay(1, 5);
                            lineidx = 0;
                            break;
                    }
                    break;
                case 6:
                    switch (lineidx)
                    {
                        case 1:
                            LineUpDisplay(6, 1);
                            break;
                        case 2:
                            LineUpDisplay(1, 6);
                            break;
                        case 3:
                            LineUpDisplay(3, 2);
                            break;
                        case 4:
                            LineUpDisplay(2, 3);
                            lineidx = 0;
                            break;
                    }
                    break;
                case 7:
                    switch (lineidx)
                    {
                        case 1:
                            LineUpDisplay(7, 1);
                            break;
                        case 2:
                            LineUpDisplay(1, 7);
                            lineidx = 0;
                            break;
                    }
                    break;
                case 8:
                    switch (lineidx)
                    {
                        case 1:
                            LineUpDisplay(8, 1);
                            break;
                        case 2:
                            LineUpDisplay(1, 8);
                            break;
                        case 3:
                            LineUpDisplay(4, 2);
                            break;
                        case 4:
                            LineUpDisplay(2, 4);
                            lineidx = 0;
                            break;
                    }
                    break;
                case 9:
                    switch (lineidx)
                    {
                        case 1:
                            LineUpDisplay(9, 1);
                            break;
                        case 2:
                            LineUpDisplay(1, 9);
                            break;
                        case 3:
                            LineUpDisplay(3, 3);
                            lineidx = 0;
                            break;
                    }
                    break;
                case 10:
                    switch (lineidx)
                    {
                        case 1:
                            LineUpDisplay(10, 1);
                            break;
                        case 2:
                            LineUpDisplay(1, 10);
                            break;
                        case 3:
                            LineUpDisplay(5, 2);
                            break;
                        case 4:
                            LineUpDisplay(2, 5);
                            lineidx = 0;
                            break;
                    }
                    break;
                default:
                    break;
            }

            foreach (DisplayElementForEditor item in g_DspElmtList)
            {
                item.UpdateElementLandSizeAndPos();
            }

            lineidx++;
        }

        int lastchcnt = 0;
        int lineidx = 1;

        public void LineUpDisplay(int column, int row)
        {
            int _count = g_DspElmtList.Count;
            double canvasW = Math.Max(1, DesignerCanvas.Width);
            double canvasH = Math.Max(1, DesignerCanvas.Height);

            double _columnW = canvasW / column;
            double _rowH = canvasH / row;

            double top = 0;
            double left = 0;

            int itemsPerRow = Math.Max(1, _count / row);

            for (int i = 0; i < _count; i++)
            {
                if (i != 0 && i % itemsPerRow == 0)
                {
                    top += _rowH;
                    left = 0;
                }

                g_DspElmtList[i].Width = _columnW;
                g_DspElmtList[i].Height = _rowH;
                Canvas.SetTop(g_DspElmtList[i], top);
                Canvas.SetLeft(g_DspElmtList[i], left);

                g_DspElmtList[i].g_ElementInfoClass.EIF_RowVal = 0;
                g_DspElmtList[i].g_ElementInfoClass.EIF_ColVal = 0;
                g_DspElmtList[i].g_ElementInfoClass.EIF_RowSpanVal = 0;
                g_DspElmtList[i].g_ElementInfoClass.EIF_ColSpanVal = 0;

                left += _columnW;
            }
        }

        public void BringToBackOneStepByElementName(string paramName)
        {
            if (g_ChildNameListForZidx.Contains(paramName) == true)
            {
                int idx = g_ChildNameListForZidx.IndexOf(paramName);

                if (idx == 0)
                {

                }
                else
                {
                    g_ChildNameListForZidx.Remove(paramName);
                    idx--;
                    g_ChildNameListForZidx.Insert(idx, paramName);

                    ReOrderingZorder();
                }
            }
        }


        public void BringToFrontOneStepByElementName(string paramName)
        {
            if (g_ChildNameListForZidx.Contains(paramName) == true)
            {
                int idx = g_ChildNameListForZidx.IndexOf(paramName);

                if (idx == (g_ChildNameListForZidx.Count - 1))
                {

                }
                else
                {
                    g_ChildNameListForZidx.Remove(paramName);
                    idx++;
                    g_ChildNameListForZidx.Insert(idx, paramName);

                    ReOrderingZorder();
                }
            }
        }

        public void BringToBackByElementName(string paramName)
        {
            if (g_ChildNameListForZidx.Contains(paramName) == true)
            {
                g_ChildNameListForZidx.Remove(paramName);
                g_ChildNameListForZidx.Insert(0, paramName);

                ReOrderingZorder();
            }
        }

        public void BringToFrontByElementName(string paramName)
        {
            if (g_ChildNameListForZidx.Contains(paramName) == true)
            {
                g_ChildNameListForZidx.Remove(paramName);
                g_ChildNameListForZidx.Add(paramName);

                ReOrderingZorder();
            }
        }
        
        public void ReOrederingZorderForLoadingPage()
        {
            List<int> zorderArray = new List<int>();

            g_ChildNameListForZidx.Clear();
            foreach (DisplayElementForEditor item in g_DspElmtList)
            {
                zorderArray.Add(item.g_ElementInfoClass.EIF_ZIndex);
            }

            foreach (ScrollTextForEditor item in g_ScrollTextForEditorList)
            {
                zorderArray.Add(item.g_ElementInfoClass.EIF_ZIndex);
            }

            foreach (WelcomeBoardForEditor item in g_WelcomBoardForEditorList)
            {
                zorderArray.Add(item.g_ElementInfoClass.EIF_ZIndex);
            }

            zorderArray.Sort();

            int idxForZorder = 0;
            
            foreach (int zorderVal in zorderArray)
            {
                foreach (DisplayElementForEditor item in g_DspElmtList)
                {
                    if (item.g_ElementInfoClass.EIF_ZIndex == zorderVal)
                    {
                        Canvas.SetZIndex(item, idxForZorder);
                        item.UpdateZidxInfo(idxForZorder);
                        g_ChildNameListForZidx.Add(item.g_ElementInfoClass.EIF_Name);
                        break;
                    }
                }
              
                foreach (ScrollTextForEditor item in g_ScrollTextForEditorList)
                {
                    if (item.g_ElementInfoClass.EIF_ZIndex == zorderVal)
                    {
                        Canvas.SetZIndex(item, idxForZorder);
                        item.UpdateZidxInfo(idxForZorder);
                        g_ChildNameListForZidx.Add(item.g_ElementInfoClass.EIF_Name);
                        break;
                    }
                }

                foreach (WelcomeBoardForEditor item in g_WelcomBoardForEditorList)
                {
                    if (item.g_ElementInfoClass.EIF_ZIndex == zorderVal)
                    {
                        Canvas.SetZIndex(item, idxForZorder);
                        item.UpdateZidxInfo(idxForZorder);
                        g_ChildNameListForZidx.Add(item.g_ElementInfoClass.EIF_Name);
                        break;
                    }
                }

                idxForZorder++;
                
            }

            //ReOrederingZorder();
        }


        public void ReOrderingZorder()
        {
            int idxForZorder = 0;
            
            foreach (string childName in g_ChildNameListForZidx)
            {
                foreach (DisplayElementForEditor item in g_DspElmtList)
                {
                    if (item.Name == childName)
                    {
                        Canvas.SetZIndex(item, idxForZorder);
                        item.UpdateZidxInfo(idxForZorder);
                        break;
                    }
                }

                foreach (ScrollTextForEditor item in g_ScrollTextForEditorList)
                {
                    if (item.Name == childName)
                    {
                        Canvas.SetZIndex(item, idxForZorder);
                        item.UpdateZidxInfo(idxForZorder);
                        break;
                    }
                }

                foreach (WelcomeBoardForEditor item in this.g_WelcomBoardForEditorList)
                {
                    if (item.Name == childName)
                    {
                        Canvas.SetZIndex(item, idxForZorder);
                        item.UpdateZidxInfo(idxForZorder);
                        break;
                    }
                }

                idxForZorder++;
            }
        }

      
        void DesignerCanvas_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            base.OnMouseDown(e);

            if (e.Source == DesignerCanvas)
            {
                //ShowPropertySet(ElementType.None);
                g_CurrentSelectedElement = null;
                HideAllListGrid();

                ReleaseSelectedObject();
            }
        }

        public void DeleteObjectByName(string objName)
        {
            int idx = 0;

            foreach (UIElement item in DesignerCanvas.Children)
            {
                if (item is DisplayElementForEditor)
                {
                    DisplayElementForEditor element2 = (DisplayElementForEditor)item;

                    if (element2.Name == objName)
                    {
                        break;
                    }
                }
                else if (item is ScrollTextForEditor)
                {
                    ScrollTextForEditor element11 = (ScrollTextForEditor)item;

                    if (element11.Name == objName)
                    {
                        break;
                    }
                }
                else if (item is WelcomeBoardForEditor)
                {
                    WelcomeBoardForEditor element11 = (WelcomeBoardForEditor)item;

                    if (element11.Name == objName)
                    {
                        break;
                    }
                }


                idx++;
            }



            DesignerCanvas.Children.RemoveAt(idx);

            if (g_ChildNameListForZidx.Contains(objName) == true)
            {
                g_ChildNameListForZidx.Remove(objName);
            }

            if (g_DspElmtList.Count > 0)
            {
                idx = 0;
                foreach (DisplayElementForEditor item in g_DspElmtList)
                {
                    if (item.Name == objName)
                    {
                        break;
                    }
                    idx++;
                }

                if (idx < g_DspElmtList.Count)
                {
                    g_DspElmtList.RemoveAt(idx);      
                }
                
            }

            if (g_ScrollTextForEditorList.Count > 0)
            {
                idx = 0;
                foreach (ScrollTextForEditor item in g_ScrollTextForEditorList)
                {
                    if (item.Name == objName)
                    {
                        break;
                    }
                    idx++;
                }

                if (idx < g_ScrollTextForEditorList.Count)
                {
                    g_ScrollTextForEditorList.RemoveAt(idx);
                }
            }

            if (this.g_WelcomBoardForEditorList.Count > 0)
            {
                idx = 0;
                foreach (WelcomeBoardForEditor item in g_WelcomBoardForEditorList)
                {
                    if (item.Name == objName)
                    {
                        break;
                    }
                    idx++;
                }

                if (idx < g_WelcomBoardForEditorList.Count)
                {
                    g_WelcomBoardForEditorList.RemoveAt(idx);
                }
            }       

            RenameDisplayElementText();
            HideAllListGrid();
        }

        void DesignerCanvas_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (LockCheckBox.IsChecked == true)
            {
                g_IsSelecteControlResizing = false;
                g_IsSelecteControlMoving = false;
                return;
            }

            //if (g_IsSelecteControlResizing == true)
            //{
            if (g_CurrentSelectedElement is DisplayElementForEditor)
            {
                if (g_DspElmtList.Count > 0)
                {
                    foreach (DisplayElementForEditor item in g_DspElmtList)
                    {
                        if (item.Name == g_CurrentSelectedObjName)
                        {
                            if (NeedGuideCheckBox.IsChecked == true)
                            {
                                item.UpdateElementSizeAndPosThroughGridBase();
                            }
                            else
                            {
                                item.UpdateElementSizeAndPosNoGridBase();
                            }
                         
                            break;
                        }
                    }
                }
            }
            else if (g_CurrentSelectedElement is ScrollTextForEditor)
            {
                if (g_ScrollTextForEditorList.Count > 0)
                {
                    foreach (ScrollTextForEditor item in g_ScrollTextForEditorList)
                    {
                        if (item.Name == g_CurrentSelectedObjName)
                        {
                            if (NeedGuideCheckBox.IsChecked == true)
                            {
                                item.UpdateElementSizeAndPosThroughGridBase();
                            }
                            else
                            {
                                item.UpdateElementSizeAndPosNoGridBase();
                            }
                            break;
                        }
                    }
                }
            }
            else if (g_CurrentSelectedElement is WelcomeBoardForEditor)
            {
                if (this.g_WelcomBoardForEditorList.Count > 0)
                {
                    foreach (WelcomeBoardForEditor item in g_WelcomBoardForEditorList)
                    {
                        if (item.Name == g_CurrentSelectedObjName)
                        {
                            if (NeedGuideCheckBox.IsChecked == true)
                            {
                                item.UpdateElementSizeAndPosThroughGridBase();
                            }
                            else
                            {
                                item.UpdateElementSizeAndPosNoGridBase();
                            }
                            break;
                        }
                    }
                }
            }
            //}

            g_IsSelecteControlResizing = false;
            g_IsSelecteControlMoving = false;
        }

        ////////////////////////////////////////////////////////////////////
        //  객체 Position 및 Size 관련 잘 되던 이전 루틴임~ 지우면 안되는겨!!!
        /*
        void DesignerCanvas_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            //if (g_IsSelecteControlResizing == true)
            //{
                if (g_CurrentSelectedElement is DisplayElementForEditor)
                {
                    if (g_DspElmtList.Count > 0)
                    {
                        foreach (DisplayElementForEditor item in g_DspElmtList)
                        {
                            if (item.Name == g_CurrentSelectedObjName)
                            {
                              //  item.UpdateControlSize(g_CurrentSelectedElement.ActualWidth, g_CurrentSelectedElement.ActualHeight);
                                item.UpdateElementSizeAndPosThroughGridBase();
                                break;                                
                            }
                        }
                    }
                }
                else if(g_CurrentSelectedElement is ScrollTextForEditor)
                {
                    if (g_ScrollTextForEditorList.Count > 0)
                    {
                        foreach (ScrollTextForEditor item in g_ScrollTextForEditorList)
                        {
                            if (item.Name == g_CurrentSelectedObjName)
                            {
                                //item.UpdateControlSize(g_CurrentSelectedElement.ActualWidth, g_CurrentSelectedElement.ActualHeight);
                                item.UpdateElementSizeAndPosThroughGridBase();
                                break;
                            }
                        }
                    }
                }
                else if (g_CurrentSelectedElement is WelcomeBoardForEditor)
                {
                    if (this.g_WelcomBoardForEditorList.Count > 0)
                    {
                        foreach (WelcomeBoardForEditor item in g_WelcomBoardForEditorList)
                        {
                            if (item.Name == g_CurrentSelectedObjName)
                            {
                                //item.UpdateControlSize(g_CurrentSelectedElement.ActualWidth, g_CurrentSelectedElement.ActualHeight);
                                item.UpdateElementSizeAndPosThroughGridBase();
                                break;
                            }
                        }
                    }
                }                
            //}

            g_IsSelecteControlResizing = false;
            g_IsSelecteControlMoving = false;
        }
        */
        public void ReleaseSelectedObjectExceptSelectedObject()
        {
            foreach (UIElement item in DesignerCanvas.Children)
            {              
                if (item is DisplayElementForEditor)
                {
                    DisplayElementForEditor element2 = (DisplayElementForEditor)item;
                    element2.SetFreeThisElementFromSelecting();

                    if (element2.Name != g_CurrentSelectedObjName)
                    {
                        element2.SetFreeThisElementFromSelecting();
                    }
                }
                else if (item is ScrollTextForEditor)
                {
                    ScrollTextForEditor element11 = (ScrollTextForEditor)item;
                    element11.SetFreeThisElementFromSelecting();


                    if (element11.Name != g_CurrentSelectedObjName)
                    {
                        element11.SetFreeThisElementFromSelecting();
                    }
                }
                else if (item is WelcomeBoardForEditor)
                {
                    WelcomeBoardForEditor element11 = (WelcomeBoardForEditor)item;
                    element11.SetFreeThisElementFromSelecting();


                    if (element11.Name != g_CurrentSelectedObjName)
                    {
                        element11.SetFreeThisElementFromSelecting();
                    }
                }
            }
        }

        public TextInfoClass g_TextInfoClass = new TextInfoClass();
        public void RefreshTextElementProperty(TextInfoClass paramCls)
        {
            g_TextInfoClass.CopyData(paramCls);

            _suppressFontSettingChanges = true;
            try
            {
                fontColorCombo.SelectedIndex = g_TextInfoClass.CIF_FontColorIndex;
                BGColorCombo.SelectedIndex = g_TextInfoClass.CIF_BGColorIndex;

                if (g_TextInfoClass.CIF_IsBGImageExist == false)
                {
                    BGColorCombo.SelectedIndex = g_TextInfoClass.CIF_BGColorIndex;
                }

                FontComboOnTextElem1.SelectedItem = new System.Windows.Media.FontFamily(g_TextInfoClass.CIF_FontName);
                FontComboOnTextElem2.SelectedItem = (int)this.g_TextInfoClass.CIF_FontSize;
            }
            finally
            {
                _suppressFontSettingChanges = false;
            }

            TextBoxNewPlayerName1.Text = g_TextInfoClass.CIF_TextContent;
            TextBoxNewPlayerName2.Text = g_TextInfoClass.CIF_BGImageFileName;

            if (this.g_TextInfoClass.CIF_IsBold == true)
            {
                TextAngleGrade20.Foreground = new SolidColorBrush(Colors.YellowGreen);
            }
            else
            {
                TextAngleGrade20.Foreground = new SolidColorBrush(Colors.Gray);
              
            }

            if (this.g_TextInfoClass.CIF_IsItalic == true)
            {
                TextAngleGrade21.Foreground = new SolidColorBrush(Colors.YellowGreen);                
            }
            else
            {
                TextAngleGrade21.Foreground = new SolidColorBrush(Colors.Gray);
            }

        }

        public void SetCurrentSelectedElement(Control uiElement)
        {
            g_CurrentSelectedElement = uiElement;
            HideAllListGrid();
            if (g_CurrentSelectedElement is DisplayElementForEditor)
            {
                MediaListGrid.Visibility = Visibility.Visible;  
            }
            else if (g_CurrentSelectedElement is ScrollTextForEditor stfe)
            {
                ScrollListGrid.Visibility = Visibility.Visible;
                RememberScrollFontStyle(stfe.g_EditFontInfoClass);
            }
            else if (g_CurrentSelectedElement is WelcomeBoardForEditor)
            {
                WelcomeListGrid.Visibility = Visibility.Visible;
            }
        }

        //public void SetCurrentSelectedObjectName(string ctrlName, string dspName, ElementInfoClass paramCls)
        //{
        //    g_CurrentSelectedObjName = ctrlName;
        //    SelectedDspText.Text = string.Format("- {0}", dspName);
        //    SelectedDspText_Copy.Text = string.Format("- {0}", dspName);

        //    g_SelectedCurElement.CopyData(paramCls);

        //    if (paramCls.EIF_Type == "Display")
        //    {
        //        RefreshContentInfoList();
        //    }
        //    else if (paramCls.EIF_Type == "ScrollText")
        //    {
        //        RefreshScrollTextInfoList();                
        //    }
        //}

        public void SetCurrentSelectedObjectName(string ctrlName, string dspName, ElementInfoClass paramCls)
        {
            g_CurrentSelectedObjName = ctrlName;
            SelectedDspText.Text = string.Format("- {0}", dspName);
            SelectedDspText_Copy.Text = string.Format("- {0}", dspName);

            g_SelectedCurElement.CopyData(paramCls);

            switch ((DisplayType)Enum.Parse(typeof(DisplayType), paramCls.EIF_Type))
            {
                case DisplayType.Media:
                case DisplayType.HDTV:
                    RefreshContentInfoList();
                    break;

                case DisplayType.ScrollText:
                    RefreshScrollTextInfoList();
                    break;

                case DisplayType.WelcomeBoard:
                case DisplayType.IPTV:
                    break;
            }
        }
        public void SetCurrentSelectedElementMovable(bool isMovable, System.Windows.Point pos)
        {
            if (LockCheckBox.IsChecked == true)
            {
                g_IsSelecteControlMoving = false;
                return;
            }

            g_IsSelecteControlMoving = isMovable;
            //startPosType = startPos;
            g_ElementStartPos = pos;
        }

        public void ReleaseSelectedObject()
        {
            foreach (UIElement item in DesignerCanvas.Children)
            {
                if (item is DisplayElementForEditor)
                {
                    DisplayElementForEditor element2 = (DisplayElementForEditor)item;
                    element2.SetFreeThisElementFromSelecting();
                }
                else if (item is ScrollTextForEditor)
                {
                    ScrollTextForEditor element11 = (ScrollTextForEditor)item;
                    element11.SetFreeThisElementFromSelecting();
                }
            }

            g_IsSelecteControlResizing = false;
            g_IsSelecteControlMoving = false;
        }

        void DesignerCanvas_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            System.Windows.Point curPos = new System.Windows.Point();

            if ((bool)LockCheckBox.IsChecked)
                return;

            if (g_IsSelecteControlResizing == true)
            {
                curPos = e.GetPosition(g_CurrentSelectedElement);

                double minFixedWidth = 20;
                double minFixedHeight = 20;

                if (g_CurrentSelectedElement != null)
                {
                    oldXPos = g_CurrentSelectedElement.ActualWidth;
                    oldYPos = g_CurrentSelectedElement.ActualHeight + 15;
                }

                if (e.LeftButton == MouseButtonState.Pressed)
                {
                    double gapWithOldCurrentX = 0;
                    double gapWithOldCurrentY = 0;
                    double wndWidth = 0;
                    double wndHeight = 0;

                    ///// 컨트롤의 Width 설정 //////////////
                    if (oldXPos > curPos.X)
                    {
                        gapWithOldCurrentX = oldXPos - curPos.X;
                        wndWidth = g_CurrentSelectedElement.ActualWidth - gapWithOldCurrentX;
                    }
                    else
                    {
                        gapWithOldCurrentX = curPos.X - oldXPos;
                        wndWidth = g_CurrentSelectedElement.ActualWidth + gapWithOldCurrentX;
                    }

                    ///// 컨트롤의 Height 설정 //////////////
                    if (oldYPos > curPos.Y)
                    {
                        gapWithOldCurrentY = oldYPos - curPos.Y;
                        wndHeight = g_CurrentSelectedElement.ActualHeight - gapWithOldCurrentY;
                    }
                    else
                    {
                        gapWithOldCurrentY = curPos.Y - oldYPos;
                        wndHeight = g_CurrentSelectedElement.ActualHeight + gapWithOldCurrentY;
                    }

                    if (wndWidth > minFixedWidth && wndHeight > minFixedHeight)
                    {
                        g_CurrentSelectedElement.Width = wndWidth;
                        g_CurrentSelectedElement.Height = wndHeight;
                    }

                    //UpdateSelecedElementSizeToPropertySet(wndWidth, wndHeight);

                    oldXPos = curPos.X;
                    oldYPos = curPos.Y;
                }
            }
            else if (g_IsSelecteControlMoving == true)
            {
                curPos = e.GetPosition(this.DesignerCanvas);

                oldXPos = curPos.X;
                oldYPos = curPos.Y;

                if (e.LeftButton == MouseButtonState.Pressed)
                {
                    double positionTop = 0;
                    double positionLeft = 0;
                    positionTop = curPos.Y - g_ElementStartPos.Y;
                    positionLeft = curPos.X  - g_ElementStartPos.X; ;

                    if (g_CurrentSelectedElement != null)
                    {
                        Canvas.SetTop(g_CurrentSelectedElement, positionTop);
                        Canvas.SetLeft(g_CurrentSelectedElement, positionLeft);
                    }
                }
            }
        }
        
        public string GetElementNameByDateTime()
        {
            DateTime curTime = DateTime.Now;
            return string.Format("YTYPE_A34{0}{1}{2}_TYP_{3}{4}_COM3_{5}{6}_TAGRE", curTime.Year,
                                                              curTime.Month,
                                                              curTime.Day,
                                                              curTime.Hour,
                                                              curTime.Minute,
                                                              curTime.Second,
                                                              curTime.Millisecond);
        }

        //public void LoadDisplayElementFromData(ElementInfoClass paramCls)
        //{
        //    DisplayElementForEditor dspElement = new DisplayElementForEditor(this);

        //    dspElement.Width = paramCls.EIF_Width;
        //    dspElement.Height = paramCls.EIF_Height;
        //    dspElement.Name = paramCls.EIF_Name;

        //    Canvas.SetLeft(dspElement, paramCls.EIF_PosLeft);
        //    Canvas.SetTop(dspElement, paramCls.EIF_PosTop);
        //    DesignerCanvas.Children.Add(dspElement);

        //    dspElement.UpdateElemenetnInfoCls(paramCls);

        //    //g_ChildNameListForZidx.Add(paramCls.EIF_Name);
        //    g_DspElmtList.Add(dspElement);

        //    dspElement.UpdateElementSizeAndPosByElementInfoClass();

        //}

        public void LoadScrollTextElementFromData(ElementInfoClass paramCls)
        {
            ScrollTextForEditor dspElement = new ScrollTextForEditor();

            dspElement.Width = paramCls.EIF_Width;
            dspElement.Height = paramCls.EIF_Height;
            dspElement.Name = paramCls.EIF_Name;

            Canvas.SetLeft(dspElement, paramCls.EIF_PosLeft);
            Canvas.SetTop(dspElement, paramCls.EIF_PosTop);
            DesignerCanvas.Children.Add(dspElement);

            dspElement.UpdateElemenetnInfoCls(paramCls);

           // g_ChildNameListForZidx.Add(paramCls.EIF_Name);
            g_ScrollTextForEditorList.Add(dspElement);

            dspElement.UpdateElementSizeAndPosByElementInfoClass();
        }


        public void LoadTextElementFromData(ElementInfoClass paramCls, string paramPageName)
        {
            WelcomeBoardForEditor dspElement = new WelcomeBoardForEditor();

            dspElement.Width = paramCls.EIF_Width;
            dspElement.Height = paramCls.EIF_Height;
            dspElement.Name = paramCls.EIF_Name;

            Canvas.SetLeft(dspElement, paramCls.EIF_PosLeft);
            Canvas.SetTop(dspElement, paramCls.EIF_PosTop);
            DesignerCanvas.Children.Add(dspElement);

            dspElement.UpdateElemenetnInfoCls(paramCls);

            // g_ChildNameListForZidx.Add(paramCls.EIF_Name);
            this.g_WelcomBoardForEditorList.Add(dspElement);

            dspElement.UpdateElementSizeAndPosByElementInfoClass();

            DataShop.Instance.g_TextInfoManager.LoadTextInfo(paramPageName, paramCls.EIF_Name);
            TextInfoClass textInfo = DataShop.Instance.g_TextInfoManager.g_DataClassList.FirstOrDefault()
                ?? new TextInfoClass { CIF_PageName = paramPageName, CIF_DataFileName = paramCls.EIF_Name };

            EnsureWelcomeBackgroundFileInContents(textInfo);

            string backgroundPath = ResolveWelcomeBoardBackgroundPath(textInfo);
            textInfo.CIF_IsBGImageExist = string.IsNullOrWhiteSpace(backgroundPath) == false;

            dspElement.UpdateTextInfoClsFromPage(textInfo);

        }

        private void EnsureWelcomeBackgroundFileInContents(TextInfoClass textInfo)
        {
            if (textInfo == null || textInfo.CIF_IsBGImageExist == false)
            {
                return;
            }

            string fileName = textInfo.CIF_BGImageFileName;
            if (string.IsNullOrWhiteSpace(fileName) && string.IsNullOrWhiteSpace(textInfo.CIF_BGImageFileFullPath) == false)
            {
                fileName = Path.GetFileName(textInfo.CIF_BGImageFileFullPath);
                textInfo.CIF_BGImageFileName = fileName;
            }

            if (string.IsNullOrWhiteSpace(fileName))
            {
                return;
            }

            string targetPath = FNDTools.GetTargetContentsFilePath(fileName);
            if (File.Exists(targetPath))
            {
                return;
            }

            string sourcePath = ResolveWelcomeBoardBackgroundPath(textInfo);
            if (string.IsNullOrWhiteSpace(sourcePath) || File.Exists(sourcePath) == false)
            {
                textInfo.CIF_IsBGImageExist = false;
                return;
            }

            try
            {
                string targetDir = Path.GetDirectoryName(targetPath);
                if (string.IsNullOrWhiteSpace(targetDir) == false)
                {
                    Directory.CreateDirectory(targetDir);
                }
                FileTools.CopyFile(sourcePath, targetPath);
                textInfo.CIF_IsBGImageExist = true;
            }
            catch (Exception ex)
            {
                Logger.WriteErrorLog(ex.ToString(), Logger.GetLogFileName());
            }
        }

        public void AddTextElement()
        {
            double unitWidth = this.GuideGrid.ColumnDefinitions[0].ActualWidth;
            double unitHeight = this.GuideGrid.RowDefinitions[0].ActualHeight;

            double elementWidth = unitWidth * 16;
            double elementHeight = unitHeight * 10;

            if (g_WelcomBoardForEditorList.Count > 3)
            {
                MessageTools.ShowMessageBox("더이상 추가 할 수 없습니다.", "확인");
                return;
            }

            System.Windows.Point position = new System.Windows.Point(100, 100);
            string elementNameGuidStr = string.Empty;
            string elementName = string.Empty;

            elementNameGuidStr = Guid.NewGuid().ToString();
            elementName = GetElementNameByDateTime();
            WelcomeBoardForEditor dspElement = new WelcomeBoardForEditor();

            dspElement.Width = elementWidth;
            dspElement.Height = elementHeight;
            dspElement.Name = elementName;

            Canvas.SetLeft(dspElement, position.X);
            Canvas.SetTop(dspElement, position.Y);
            DesignerCanvas.Children.Add(dspElement);

            ElementInfoClass tmpcls = new ElementInfoClass();
            double leftVal = (double)dspElement.GetValue(Canvas.LeftProperty);
            double topVal = (double)dspElement.GetValue(Canvas.TopProperty);
            tmpcls.EIF_Name = elementName;
            tmpcls.EIF_Type = "TextElement";

            tmpcls.EIF_RowSpanVal = 10;
            tmpcls.EIF_ColSpanVal = 16;

            tmpcls.EIF_Width = elementWidth;
            tmpcls.EIF_Height = elementHeight;
            tmpcls.EIF_PosTop = topVal;
            tmpcls.EIF_PosLeft = leftVal;
            tmpcls.EIF_DataFileName = string.Format("{0}.xml", elementName);

            dspElement.UpdateElemenetnInfoCls(tmpcls);
            dspElement.UpdateElementSizeAndPosThroughGridBase();

            g_ChildNameListForZidx.Add(elementName);
            g_WelcomBoardForEditorList.Add(dspElement);
            RenameDisplayElementText();

            ReOrderingZorder();
        }

        //public void AddDiplayElement()
        //{
        //    double unitWidth = this.GuideGrid.ColumnDefinitions[0].ActualWidth;
        //    double unitHeight = this.GuideGrid.RowDefinitions[0].ActualHeight;

        //    double elementWidth = unitWidth * 10;
        //    double elementHeight = unitHeight * 10;

        //    if (g_DspElmtList.Count > 3)
        //    {
        //        MessageTools.ShowMessageBox("화면을 더이상 추가 할 수 없습니다.", "확인");
        //        return;
        //    }

        //    System.Windows.Point position = new System.Windows.Point(100, 100);
        //    string elementNameGuidStr = string.Empty;
        //    string elementName = string.Empty;

        //    elementNameGuidStr = Guid.NewGuid().ToString();
        //    elementName = GetElementNameByDateTime();
        //    DisplayElementForEditor dspElement = new DisplayElementForEditor(this);

        //    dspElement.Width = elementWidth;
        //    dspElement.Height = elementHeight;
        //    dspElement.Name = elementName;

        //    Canvas.SetLeft(dspElement, position.X);
        //    Canvas.SetTop(dspElement, position.Y);
        //    DesignerCanvas.Children.Add(dspElement);

        //    ElementInfoClass tmpcls = new ElementInfoClass();
        //    double leftVal = (double)dspElement.GetValue(Canvas.LeftProperty);
        //    double topVal = (double)dspElement.GetValue(Canvas.TopProperty);
        //    tmpcls.EIF_Name = elementName;
        //    tmpcls.EIF_Type = "Display";

        //    tmpcls.EIF_RowSpanVal = 10;
        //    tmpcls.EIF_ColSpanVal = 10;

        //    tmpcls.EIF_Width = elementWidth;
        //    tmpcls.EIF_Height = elementHeight;
        //    tmpcls.EIF_PosTop = topVal;
        //    tmpcls.EIF_PosLeft = leftVal;
        //    tmpcls.EIF_DataFileName = string.Format("{0}.xml", elementName);

        //    dspElement.UpdateElemenetnInfoCls(tmpcls);
        //    dspElement.UpdateElementSizeAndPosThroughGridBase();

        //    g_ChildNameListForZidx.Add(elementName);
        //    g_DspElmtList.Add(dspElement);
        //    RenameDisplayElementText();

        //    ReOrederingZorder();
        //}

        public void AddDiplayElement(DisplayType type)
        {
            if(CheckFullScreenDisplay())
            {
                MessageTools.ShowMessageBox("미디어 객체는 겹칠 수 없습니다.", "확인");
                return;
            }

            double unitWidth = this.GuideGrid.ColumnDefinitions[0].ActualWidth;
            double unitHeight = this.GuideGrid.RowDefinitions[0].ActualHeight;

            double elementWidth = unitWidth * 10;
            double elementHeight = unitHeight * 10;

            if (g_DspElmtList.Count > 3)
            {
                MessageTools.ShowMessageBox("더이상 추가 할 수 없습니다.", "확인");
                return;
            }

            System.Windows.Point position = new System.Windows.Point(100, 100);
            string elementNameGuidStr = string.Empty;
            string elementName = string.Empty;

            elementNameGuidStr = Guid.NewGuid().ToString();
            elementName = GetElementNameByDateTime();
            DisplayElementForEditor dspElement = new DisplayElementForEditor();
            dspElement.Width = elementWidth;
            dspElement.Height = elementHeight;
            dspElement.Name = elementName;

            Canvas.SetLeft(dspElement, position.X);
            Canvas.SetTop(dspElement, position.Y);
            DesignerCanvas.Children.Add(dspElement);

            ElementInfoClass tmpcls = new ElementInfoClass();
            double leftVal = (double)dspElement.GetValue(Canvas.LeftProperty);
            double topVal = (double)dspElement.GetValue(Canvas.TopProperty);
            tmpcls.EIF_Name = elementName;

            if (type.Equals(DisplayType.HDTV))
            {
                tmpcls.EIF_Type = DisplayType.HDTV.ToString();
            }
            else if (type.Equals(DisplayType.IPTV))
            {
                tmpcls.EIF_Type = DisplayType.IPTV.ToString();
            }
            else
            {
                tmpcls.EIF_Type = DisplayType.Media.ToString();
            }

            tmpcls.EIF_RowSpanVal = 10;
            tmpcls.EIF_ColSpanVal = 10;

            tmpcls.EIF_Width = elementWidth;
            tmpcls.EIF_Height = elementHeight;
            tmpcls.EIF_PosTop = topVal;
            tmpcls.EIF_PosLeft = leftVal;

            tmpcls.EIF_DataFileName = string.Format("{0}.xml", elementName);

            dspElement.UpdateElemenetnInfoCls(tmpcls);
            dspElement.UpdateElementSizeAndPosThroughGridBase();

            g_ChildNameListForZidx.Add(elementName);
            g_DspElmtList.Add(dspElement);
            RenameDisplayElementText();

            ReOrderingZorder();

            dspElement.ChooseThisElement();
        }

        public bool CheckFullScreenDisplay()
        {
            double canvasW = Math.Max(1, DesignerCanvas.Width);
            double canvasH = Math.Max(1, DesignerCanvas.Height);

            foreach(DisplayElementForEditor defe in g_DspElmtList)
            {
                if (defe.g_ElementInfoClass.EIF_Width >= canvasW && defe.g_ElementInfoClass.EIF_Height >= canvasH)
                    return true;
            }

            return false;
        }

        public void AddScrollTextElement()
        {
            double unitWidth = this.GuideGrid.ColumnDefinitions[0].ActualWidth;
            double unitHeight = this.GuideGrid.RowDefinitions[0].ActualHeight;

            double elementWidth = unitWidth * 18;
            double elementHeight = unitHeight * 2;

            if (g_ScrollTextForEditorList.Count > 3)
            {
                MessageTools.ShowMessageBox("더이상 자막을 추가 할 수 없습니다.", "확인");
                return;
            }

            System.Windows.Point position = new System.Windows.Point(200, 200);
            string elementNameGuidStr = string.Empty;
            string elementName = string.Empty;

            elementNameGuidStr = Guid.NewGuid().ToString();

            DateTime curTime = DateTime.Now;

            elementName = GetElementNameByDateTime();

            ScrollTextForEditor dspElement = new ScrollTextForEditor();

            dspElement.Width = elementWidth;
            dspElement.Height = elementHeight;

            dspElement.Name = elementName;

            Canvas.SetLeft(dspElement, position.X);
            Canvas.SetTop(dspElement, position.Y);
            DesignerCanvas.Children.Add(dspElement);

            ElementInfoClass tmpcls = new ElementInfoClass();
            double leftVal = (double)dspElement.GetValue(Canvas.LeftProperty);
            double topVal = (double)dspElement.GetValue(Canvas.TopProperty);
            tmpcls.EIF_Name = elementName;
            tmpcls.EIF_Type = "ScrollText";

            tmpcls.EIF_RowSpanVal = 2;
            tmpcls.EIF_ColSpanVal = 15;

            tmpcls.EIF_Width = elementWidth;
            tmpcls.EIF_Height = elementHeight;
            tmpcls.EIF_PosTop = topVal;
            tmpcls.EIF_PosLeft = leftVal;
            tmpcls.EIF_DataFileName = string.Format("{0}.xml", elementName);

            dspElement.UpdateElemenetnInfoCls(tmpcls);
            dspElement.UpdateElementSizeAndPosThroughGridBase();
            ApplyLatestScrollFontStyle(dspElement);

            g_ChildNameListForZidx.Add(elementName);
            g_ScrollTextForEditorList.Add(dspElement);

            RenameDisplayElementText();
            ReOrderingZorder();
        }
        /*
        public void AddScrollTextElement()
        {
            double unitWidth = this.GuideGrid.ColumnDefinitions[0].ActualWidth;
            double unitHeight = this.GuideGrid.RowDefinitions[0].ActualHeight;

            double elementWidth = unitWidth * 15;
            double elementHeight = unitHeight * 2;

            if (g_ScrollTextForEditorList.Count > 3)
            {
                MessageTools.ShowMessageBox("더이상 자막을 추가 할 수 없습니다.", "확인");
                return;
            }

            System.Windows.Point position = new System.Windows.Point(200, 200);
            string elementNameGuidStr = string.Empty;
            string elementName = string.Empty;

            elementNameGuidStr = Guid.NewGuid().ToString();

            DateTime curTime = DateTime.Now;

            elementName = GetElementNameByDateTime();

            ScrollTextForEditor dspElement = new ScrollTextForEditor(this);

            dspElement.Width = elementWidth;
            dspElement.Height = elementHeight;

            dspElement.Name = elementName;

            Canvas.SetLeft(dspElement, position.X);
            Canvas.SetTop(dspElement, position.Y);
            DesignerCanvas.Children.Add(dspElement);

            ElementInfoClass tmpcls = new ElementInfoClass();
            double leftVal = (double)dspElement.GetValue(Canvas.LeftProperty);
            double topVal = (double)dspElement.GetValue(Canvas.TopProperty);
            tmpcls.EIF_Name = elementName;
            tmpcls.EIF_Type = "ScrollText";

            tmpcls.EIF_RowSpanVal = 2;
            tmpcls.EIF_ColSpanVal = 15;

            tmpcls.EIF_Width = elementWidth;
            tmpcls.EIF_Height = elementHeight;
            tmpcls.EIF_PosTop = topVal;
            tmpcls.EIF_PosLeft = leftVal;
            tmpcls.EIF_DataFileName = string.Format("{0}.xml", elementName);

            dspElement.UpdateElemenetnInfoCls(tmpcls);
            dspElement.UpdateElementSizeAndPosThroughGridBase();

            g_ChildNameListForZidx.Add(elementName);
            g_ScrollTextForEditorList.Add(dspElement);
            
            //ReArrangeElementNameList();
            RenameDisplayElementText();
            ReOrederingZorder();
        }
        */

        public void RenameDisplayElementText()
        {
            if (g_DspElmtList.Count > 0)
            {
                int idx = 1;
                foreach (DisplayElementForEditor item in g_DspElmtList)
                {
                    item.TextDisplayName.Text = string.Format("Display #{0}", idx);
                    idx++;
                }
                
            }

            if (g_ScrollTextForEditorList.Count > 0)
            {
                int idx = 1;
                foreach (ScrollTextForEditor item in g_ScrollTextForEditorList)
                {
                    item.TextDisplayName.Text = string.Format("ScrollText #{0}", idx);
                    idx++;
                }

            }

            if (this.g_WelcomBoardForEditorList.Count > 0)
            {
                int idx = 1;
                foreach (WelcomeBoardForEditor item in g_WelcomBoardForEditorList)
                {
                    item.TextDisplayName.Text = item.g_TextInfoClass.CIF_TextContent;
                    idx++;
                }
            }
        }
        
        internal void SetScrollSpeedComboItem(string value)
        {
            LogicTools.SelectItemByName(ScrollSpeedComboBox, value);
        }

        public void EditScrollTextListToSelectedElement(ContentsInfoClass temInfoClass)
        {
            if (g_CurrentSelectedObjName != string.Empty)
            {
                foreach (ScrollTextForEditor item in g_ScrollTextForEditorList)
                {
                    if (item.g_ElementInfoClass.EIF_Name == g_CurrentSelectedObjName)
                    {
                        item.EditContentInfoCls(temInfoClass);
                        ScrollTextTBox.Text = temInfoClass.CIF_FileName;
                        SetScrollSpeedComboItem(temInfoClass.CIF_ScrollTextSpeedSec.ToString());
                        break;
                    }

                }

            }
        }


        ContentsInfoBatchUpdateWindow editwindow = new ContentsInfoBatchUpdateWindow();
        private void BatchBtn_Click(object sender, RoutedEventArgs e)
        {
            editwindow.ShowDialog();
        }


        public bool CheckStackObjects(double left, double top, double right, double bottom, string guid)
        {
            bool result = false;

            foreach (DisplayElementForEditor defe in g_DspElmtList)
            {
                if (guid == defe.g_ElementInfoClass.EIF_Name)
                    continue;

                double _left = Canvas.GetLeft(defe);
                double _top = Canvas.GetTop(defe);

                Rect _r1 = new Rect(left, top, right, bottom);
                Rect _r2 = new Rect(_left+1, _top+1, defe.ActualWidth-2, defe.ActualHeight-2);

                if (_r1.IntersectsWith(_r2))
                {
                    result = true;
                    break;
                }
            }

            return result;
        }

        private void ResSettingsBtn_Click(object sender, RoutedEventArgs e)
        {
            ClearCanvas();

            ResWindow rwin = new ResWindow(g_CurrentPageInfo);
            if (rwin.ShowDialog() == true && rwin.ResInfo != null)
            {
                var res = rwin.ResInfo;
                ApplyResolutionToUI(res.Orientation, res.Row, res.Column, res.WidthPixels, res.HeightPixels);

                ServerSettings settings = DataShop.Instance?.g_ServerSettingsManager?.sData;
                if (settings != null)
                {
                    settings.DefaultResolutionOrientation = res.Orientation;
                    settings.DefaultResolutionRows = res.Row;
                    settings.DefaultResolutionColumns = res.Column;
                    settings.DefaultResolutionWidthPixels = res.WidthPixels;
                    settings.DefaultResolutionHeightPixels = res.HeightPixels;
                    DataShop.Instance.g_ServerSettingsManager.SaveData(settings);
                }
            }
        }

        void ClearCanvas()
        {
            HideAllListGrid();

            //PagenameTBox.Text = string.Empty;
            //ClearAllChildDataOfCanvas();

            //MediaListBox.Items.Clear();
        }

        private void DesignGridSizeChanged(object sender, SizeChangedEventArgs e)
        {
            RefreshGuides(g_CurrentPageInfo.PIC_Rows, g_CurrentPageInfo.PIC_Columns);
        }

        private void RefreshGuides(int newRowCnt, int newColCnt)
        {
            SetGuide(newRowCnt, newColCnt);
            SetScreenGuide(newRowCnt, newColCnt);
        }

        private static int GetAspectUnitCount(double width, double height, out int colCount, out int rowCount)
        {
            colCount = 0;
            rowCount = 0;

            int iw = (int)Math.Round(Math.Max(1.0, width));
            int ih = (int)Math.Round(Math.Max(1.0, height));

            int a = iw;
            int b = ih;
            while (b != 0)
            {
                int t = a % b;
                a = b;
                b = t;
            }

            int gcd = Math.Max(1, a);

            int baseW = Math.Max(1, iw / gcd);
            int baseH = Math.Max(1, ih / gcd);

            colCount = baseW * 6;
            rowCount = baseH * 6;

            return gcd;
        }

        public void SetGuide(int newRowCnt, int newColCnt, bool isGridSize = true)
        {
            GuideGrid.RowDefinitions.Clear();
            GuideGrid.ColumnDefinitions.Clear();
            GuideGrid.GridLineThickness = 0.7;

            if (DesignerCanvas.ActualHeight <= 0 || DesignerCanvas.ActualWidth <= 0)
                return;

            GetAspectUnitCount(DesignerCanvas.ActualWidth, DesignerCanvas.ActualHeight, out int colCount, out int rowCount);

            double g_unitRowHeight = DesignerCanvas.ActualHeight / rowCount;
            double g_unitColWidth = DesignerCanvas.ActualWidth / colCount;

            for (int i = 0; i < colCount; i++)
            {
                ColumnDefinition _col = new ColumnDefinition();
                _col.Width = new GridLength(g_unitColWidth, GridUnitType.Star);
                GuideGrid.ColumnDefinitions.Add(_col);
            }

            for (int i = 0; i < rowCount; i++)
            {
                RowDefinition _row = new RowDefinition();
                _row.Height = new GridLength(g_unitRowHeight, GridUnitType.Star);
                GuideGrid.RowDefinitions.Add(_row);
            }
        }

        public void SetScreenGuide(int newRowCnt, int newColCnt)
        {
            ScreenGuideGrid.RowDefinitions.Clear();
            ScreenGuideGrid.ColumnDefinitions.Clear();
            ScreenGuideGrid.GridLineThickness = 2;

            if (ScreenGuideGrid.ActualHeight <= 0 || ScreenGuideGrid.ActualWidth <= 0)
                return;

            GetAspectUnitCount(ScreenGuideGrid.ActualWidth, ScreenGuideGrid.ActualHeight, out int colCount, out int rowCount);

            double g_unitRowHeight = ScreenGuideGrid.ActualHeight / rowCount;
            double g_unitColWidth = ScreenGuideGrid.ActualWidth / colCount;

            for (int i = 0; i < colCount; i++)
            {
                ColumnDefinition _col = new ColumnDefinition();
                _col.Width = new GridLength(g_unitColWidth, GridUnitType.Star);
                ScreenGuideGrid.ColumnDefinitions.Add(_col);
            }

            for (int i = 0; i < rowCount; i++)
            {
                RowDefinition _row = new RowDefinition();
                _row.Height = new GridLength(g_unitRowHeight, GridUnitType.Star);
                ScreenGuideGrid.RowDefinitions.Add(_row);
            }
        }

        ContentInfoElement leadCtrl;
        private void MediaListBox_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (e.Handled)
            {
                return;
            }

            if (MediaListScrollViewer == null)
            {
                return;
            }

            e.Handled = true;
            var eventArgs = new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta)
            {
                RoutedEvent = UIElement.MouseWheelEvent,
                Source = sender
            };

            MediaListScrollViewer.RaiseEvent(eventArgs);
        }

        private void MediaListBox_LayoutUpdated(object sender, EventArgs e)
        {
            if (leadCtrl != null)
            {
                Point pt = leadCtrl.TranslatePoint(new Point(0, 0), MediaListBox);
                if (Point.Equals(org_pt, pt) == false)
                {
                    List<ContentsInfoClass> datalist = new List<ContentsInfoClass>();

                    int idx = 1;
                    foreach (ContentInfoElement cie in MediaListBox.Items)
                    {
                        cie.TextBlockOrderingNumber.Text = string.Format("{0:D2}", idx);
                        idx++;

                        datalist.Add(cie.g_ContentsInfoClass);
                    }
                    org_pt = pt;

                    this.g_SelectedCurElement.EIF_ContentsInfoClassList = datalist;
                    ((DisplayElementForEditor)this.g_CurrentSelectedElement).UpdateElemenetnInfoCls(this.g_SelectedCurElement);
                }
                leadCtrl = null;
            }
        }

        Point org_pt;
        private void MediaListBox_Drop(object sender, DragEventArgs e)
        {
            leadCtrl = MediaListBox.SelectedItem as ContentInfoElement;

            if (leadCtrl != null)
                org_pt = leadCtrl.TranslatePoint(new Point(0, 0), MediaListBox);
        }
    }
}