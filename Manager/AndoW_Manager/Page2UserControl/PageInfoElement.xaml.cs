using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using TurtleTools;

namespace AndoW_Manager
{
    /// <summary>
    /// ContentInfoElement.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class PageInfoElement : UserControl
    {
        public string noticeTitle = string.Empty;
        public string noticeImageName = string.Empty;

        public PageInfoClass g_PageInfoClass = new PageInfoClass();
        int g_Idx = 0;
        bool g_IsSelected = false;

        public string g_PreviewThumbBase64 = string.Empty;

        private bool _isPreviewLoaded;
        private bool _isPreviewLoading;
        private readonly ToolTip _previewToolTip = new ToolTip();
        private CancellationTokenSource _previewCts;

        public bool Selected
        {
            get { return g_IsSelected; }
            set
            {
                g_IsSelected = (bool)value;
                if (g_IsSelected)
                {
                    SelectBorder.Visibility = Visibility.Visible;
                    SelectBorder.BorderBrush = ColorTools.GetSolidBrushByColorString("#FF0DB8CE");
                }
                else
                {
                    SelectBorder.Visibility = Visibility.Hidden;
                    SelectBorder.BorderBrush = new SolidColorBrush(Colors.WhiteSmoke);
                }
            }
        }

        public PageInfoElement()
        {
            InitializeComponent();
            InitEventHandler();
            SelectBorder.Visibility = System.Windows.Visibility.Hidden;
            EditPlayTimeGrid.Visibility = System.Windows.Visibility.Hidden;
            InitComboBoxes();

            ToolTipService.SetInitialShowDelay(this, 0);
            ToolTipService.SetShowDuration(this, int.MaxValue);
            ToolTipService.SetIsEnabled(this, false);
            _previewToolTip.PlacementTarget = this;
            _previewToolTip.Content = "미리보기 준비중...";
            this.ToolTip = _previewToolTip;
        }

        public void InitComboBoxes()
        {
            scrollSpeedComboBox_Copy1.SelectedIndex = 0;
            scrollSpeedComboBox_Copy.SelectedIndex = 0;
            scrollSpeedComboBox_Copy2.SelectedIndex = 0;
        }

        public void InitEventHandler()
        {
            this.PreviewMouseLeftButtonDown += new MouseButtonEventHandler(PageListElement_PreviewMouseLeftButtonDown);
            this.PreviewMouseMove += new MouseEventHandler(PageListElement_PreviewMouseMove);
            this.MouseLeave += new MouseEventHandler(PageListElement_MouseLeave);

            this.BTN0DO_Copy3.Click += BTN0DO_Copy3_Click;  // 플레이 시간편집
            BTN0DO_Copy1.Click += BTN0DO_Copy1_Click;  // 플레이 시간저장

            ExitTextBlock.PreviewMouseMove += new MouseEventHandler(ExitTextBlock_PreviewMouseMove);
            ExitTextBlock.MouseLeave += new MouseEventHandler(ExitTextBlock_MouseLeave);
            ExitTextBlock.PreviewMouseLeftButtonUp += new MouseButtonEventHandler(ExitTextBlock_PreviewMouseLeftButtonUp);

            MC_CopyPage.Click += MC_CopyPage_Click;
            this.MouseEnter += PageInfoElement_MouseEnter;
            this.Unloaded += PageInfoElement_Unloaded;
        }

        void MC_CopyPage_Click(object sender, RoutedEventArgs e)
        {
            Page2.Instance.AddPageToPageList(this.g_PageInfoClass.PIC_PageName);
        }
        
        public void UpdateDataInfo(PageInfoClass paramCls, int paramIdx)
        {
            g_PageInfoClass.CopyData(paramCls);
            g_Idx = paramIdx;
            ResetPreviewCache();
            DisplayDataInfo();
        }

        public void DisplayDataInfo()
        {
            TextBlockPageName.Text = g_PageInfoClass.PIC_PageName;
            TextBlockPageName_Copy2.Text = string.Format("{0:D2}:{1:D2}:{2:D2}", g_PageInfoClass.PIC_PlaytimeHour,
                g_PageInfoClass.PIC_PlaytimeMinute,
                g_PageInfoClass.PIC_PlaytimeSecond);

            scrollSpeedComboBox_Copy1.SelectedItem = string.Format("{0:D2}", g_PageInfoClass.PIC_PlaytimeHour);
            scrollSpeedComboBox_Copy.SelectedItem = string.Format("{0:D2}", g_PageInfoClass.PIC_PlaytimeMinute);
            scrollSpeedComboBox_Copy2.SelectedItem = string.Format("{0:D2}", g_PageInfoClass.PIC_PlaytimeSecond);
        }


        void BTN0DO_Copy1_Click(object sender, RoutedEventArgs e)
        {
            if (  scrollSpeedComboBox_Copy1.SelectedIndex == 0 && 
                scrollSpeedComboBox_Copy.SelectedIndex == 0 && 
                scrollSpeedComboBox_Copy2.SelectedIndex == 0)
            {
                MessageTools.ShowMessageBox("재생시간을 [00:00:00]으로 설정 할 수 없습니다.", "확인");
                return;
            }

            g_PageInfoClass.PIC_PlaytimeHour = Int32.Parse(scrollSpeedComboBox_Copy1.SelectedItem.ToString());
            g_PageInfoClass.PIC_PlaytimeMinute = Int32.Parse(scrollSpeedComboBox_Copy.SelectedItem.ToString());
            g_PageInfoClass.PIC_PlaytimeSecond = Int32.Parse(scrollSpeedComboBox_Copy2.SelectedItem.ToString());


            DisplayTimeGrid.Visibility = System.Windows.Visibility.Visible;
            EditPlayTimeGrid.Visibility = System.Windows.Visibility.Hidden;

            DisplayDataInfo();

            Page2.Instance.EditPageInfoOfPageList(g_PageInfoClass, this.g_Idx);
        }

        void BTN0DO_Copy3_Click(object sender, RoutedEventArgs e)
        {
            DisplayTimeGrid.Visibility = System.Windows.Visibility.Hidden;
            EditPlayTimeGrid.Visibility = System.Windows.Visibility.Visible;
        }

        void ExitTextBlock_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            Page2.Instance.DeletePageInfoOfPageList(g_PageInfoClass);
        }

        void ExitTextBlock_MouseLeave(object sender, MouseEventArgs e)
        {
            ExitTextBlock.Foreground = new SolidColorBrush(Colors.Gray);
        }

        void ExitTextBlock_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            ExitTextBlock.Foreground = new SolidColorBrush(Colors.White);
        }

        void PageListElement_MouseLeave(object sender, MouseEventArgs e)
        {
            if (g_IsSelected == false)
            {
                SelectBorder.Visibility = System.Windows.Visibility.Hidden;
            }

            CancelPreviewLoading(clearContent: true);
        }

        void PageListElement_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (g_IsSelected == false)
            {
                SelectBorder.Visibility = System.Windows.Visibility.Visible;
            }
        }

        void PageListElement_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            Page2.Instance.SelectPageInfoOfPageList(g_PageInfoClass);    
        }

        private void PageInfoElement_MouseEnter(object sender, MouseEventArgs e)
        {
            ShowPreviewToolTip();
        }

        private void PageInfoElement_Unloaded(object sender, RoutedEventArgs e)
        {
            CancelPreviewLoading(clearContent: true);
        }

        private void ShowPreviewToolTip()
        {
            if (_isPreviewLoaded || _isPreviewLoading)
            {
                return;
            }

            CancelPreviewLoading(clearContent: false);
            _isPreviewLoading = true;
            _previewCts = new CancellationTokenSource();
            CancellationToken token = _previewCts.Token;
            ShowToolTipContent("미리보기 준비중...");

            var scheduler = TaskScheduler.FromCurrentSynchronizationContext();
            Task.Run(() =>
            {
                token.ThrowIfCancellationRequested();
                BitmapSource bitmap = LoadPreviewImageFromThumb();
                return bitmap;
            }, token).ContinueWith(t =>
            {
                _isPreviewLoading = false;
                if (token.IsCancellationRequested || t.IsCanceled)
                {
                    return;
                }

                _previewToolTip.Visibility = Visibility.Visible;

                BitmapSource bitmap = null;
                if (t.IsFaulted)
                {
                    Logger.WriteErrorLog($"미리보기 로드 실패: {t.Exception?.GetBaseException().Message}", Logger.GetLogFileName());
                }
                else
                {
                    bitmap = t.Result;
                }

                if (bitmap == null)
                {
                    ShowToolTipContent("미리보기를 불러오지 못했습니다.");
                    return;
                }

                FrameworkElement previewElement = BuildPreviewElement(bitmap);
                bool displayed = ShowToolTipContent(previewElement);
                _isPreviewLoaded = displayed;
            }, scheduler);
        }

        private BitmapSource LoadPreviewImageFromThumb()
        {
            if (string.IsNullOrEmpty(g_PreviewThumbBase64))
            {
                return null;
            }

            return MediaTools.CreateBitmapFromBase64(g_PreviewThumbBase64);
        }

        private static FrameworkElement BuildPreviewElement(BitmapSource bitmap)
        {
            if (bitmap == null)
            {
                return null;
            }

            double maxEdge = 300d;
            double width = bitmap.PixelWidth;
            double height = bitmap.PixelHeight;
            if (width <= 0 || height <= 0)
            {
                width = maxEdge;
                height = maxEdge * 0.66;
            }

            double scale = maxEdge / Math.Max(width, height);
            if (scale > 1d)
            {
                scale = 1d;
            }

            double targetWidth = Math.Max(50, width * scale);
            double targetHeight = Math.Max(50, height * scale);

            ImageBrush brush = new ImageBrush(bitmap);
            brush.Freeze();

            return new Rectangle
            {
                Width = targetWidth,
                Height = targetHeight,
                Fill = brush
            };
        }

        private void ResetPreviewCache()
        {
            CancelPreviewLoading(clearContent: true);
            _previewToolTip.Content = "미리보기 준비중...";
        }

        private void CancelPreviewLoading(bool clearContent)
        {
            if (_previewCts != null)
            {
                try
                {
                    if (!_previewCts.IsCancellationRequested)
                    {
                        _previewCts.Cancel();
                    }
                }
                catch (ObjectDisposedException)
                {
                }
                _previewCts.Dispose();
                _previewCts = null;
            }

            HidePreviewToolTip();
            _isPreviewLoading = false;
            if (clearContent)
            {
                _isPreviewLoaded = false;
                _previewToolTip.Content = null;
            }
        }

        private bool ShowToolTipContent(object content)
        {
            if (!IsMouseOver)
            {
                return false;
            }

            _previewToolTip.Content = content;
            _previewToolTip.Visibility = Visibility.Visible;
            _previewToolTip.IsOpen = true;
            return true;
        }

        private void HidePreviewToolTip()
        {
            _previewToolTip.IsOpen = false;
            _previewToolTip.Visibility = Visibility.Collapsed;
        }
    }
}
