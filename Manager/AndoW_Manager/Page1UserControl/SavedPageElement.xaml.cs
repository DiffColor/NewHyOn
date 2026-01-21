using System;
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
    /// SavedPageElement.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class SavedPageElement : UserControl
    {
        public bool g_IsSelected = false;

        public string g_PreviewThumbBase64 = string.Empty;

        private bool _isPreviewLoaded;
        private bool _isPreviewLoading;
        private readonly ToolTip _previewToolTip = new ToolTip();
        private CancellationTokenSource _previewCts;

        public SavedPageElement()
        {
            InitializeComponent();
            InitEventHandler();
            SelectBorder.Visibility = System.Windows.Visibility.Hidden;

            ToolTipService.SetInitialShowDelay(this, 0);
            ToolTipService.SetShowDuration(this, int.MaxValue);
            ToolTipService.SetIsEnabled(this, false);
            _previewToolTip.PlacementTarget = this;
            _previewToolTip.Content = "미리보기 준비중...";
            this.ToolTip = _previewToolTip;
        }

        public void InitEventHandler()
        {
            this.PreviewMouseLeftButtonDown += new MouseButtonEventHandler(PageListElement_PreviewMouseLeftButtonDown);
            this.PreviewMouseMove += new MouseEventHandler(PageListElement_PreviewMouseMove);
            this.MouseLeave += new MouseEventHandler(PageListElement_MouseLeave);
            this.PreviewMouseDoubleClick += SavedPageElement_PreviewMouseDoubleClick;

            BorderBTN_Copy.Click += BorderBTN_Copy_Click;

            MC_DeletePage.Click += MC_DeletePage_Click;
            MC_CopyToUSB.Click += MC_CopyToUSB_Click;
            DisplayCotextMenu.Opened += DisplayCotextMenu_Opened;

            this.Loaded += SavedPageElement_Loaded;
            this.MouseEnter += SavedPageElement_MouseEnter;
            this.Unloaded += SavedPageElement_Unloaded;
        }

        void SavedPageElement_Loaded(object sender, RoutedEventArgs e)
        {
        }

        private void SavedPageElement_MouseEnter(object sender, MouseEventArgs e)
        {
            BeginPreviewLoadIfNeeded();
        }

        private void SavedPageElement_Unloaded(object sender, RoutedEventArgs e)
        {
            CancelPreviewLoading(clearContent: true);
        }

        private void BeginPreviewLoadIfNeeded()
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

        private static FrameworkElement BuildPreviewElement(BitmapSource source)
        {
            double maxEdge = 300d;
            double width = source.PixelWidth;
            double height = source.PixelHeight;
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

            ImageBrush brush = new ImageBrush(source);
            brush.Freeze();

            return new Rectangle
            {
                Width = targetWidth,
                Height = targetHeight,
                Fill = brush
            };
        }

        private BitmapSource LoadPreviewImageFromThumb()
        {
            if (string.IsNullOrEmpty(g_PreviewThumbBase64))
            {
                return null;
            }

            return MediaTools.CreateBitmapFromBase64(g_PreviewThumbBase64);
        }

        void MC_DeletePage_Click(object sender, RoutedEventArgs e)
        {
            string pageName = pageNameTextBlock.Text;
            DataShop.Instance.g_PageInfoManager.DeletePagesByPageName(pageName);
            Page2.Instance.RemovePageAndList(pageName);
            MainWindow.Instance.RefreshSavedPageList();
        }

        private void MC_CopyToUSB_Click(object sender, RoutedEventArgs e)
        {
            if (Page2.Instance == null)
            {
                return;
            }

            string pageName = pageNameTextBlock.Text;
            Page2.Instance.ExportPlaylistToUsb(pageName);
        }

        void BorderBTN_Copy_Click(object sender, RoutedEventArgs e)
        {
            if (MessageTools.ShowMessageBox(string.Format("페이지 [{0}]을(를) 로드하시겠습니까?", pageNameTextBlock.Text), "예", "아니오") == true)
            {
                Page1.Instance.LoadSelectedPage(pageNameTextBlock.Text);
                Page1.Instance.HideDepartuerAddPanel();
            }
        }

        private void DisplayCotextMenu_Opened(object sender, RoutedEventArgs e)
        {
            UpdateUsbMenuVisibility();
        }

        private void UpdateUsbMenuVisibility()
        {
            bool hasUsb = Page2.Instance != null && Page2.Instance.HasAvailableUsb();
            Visibility visibility = hasUsb ? Visibility.Visible : Visibility.Collapsed;

            MC_CopyToUSB.Visibility = visibility;
            SepUSB.Visibility = visibility;
        }

        void SavedPageElement_PreviewMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (MessageTools.ShowMessageBox(string.Format("페이지 [{0}]을(를) 로드하시겠습니까?", pageNameTextBlock.Text), "예", "아니오") == true)
            {
                Page1.Instance.LoadSelectedPage(pageNameTextBlock.Text);
                Page1.Instance.LockCheckBox.IsChecked = true;
                Page1.Instance.HideDepartuerAddPanel();
            }
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
            // Page1.Instance.DisplaySelectedPagePreviewImage(pageNameTextBlock.Text, g_PreviewImgFilePath);

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
