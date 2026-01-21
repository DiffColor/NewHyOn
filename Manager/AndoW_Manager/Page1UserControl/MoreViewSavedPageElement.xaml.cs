using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using TurtleTools;

namespace AndoW_Manager
{
    /// <summary>
    /// MoreViewSavedPageElement.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class MoreViewSavedPageElement : UserControl
    {
        public bool g_IsSelected = false;

        public string g_PreviewThumbBase64 = string.Empty;

        private bool _isPreviewLoaded;
        private bool _isPreviewLoading;
        private readonly ToolTip _previewToolTip = new ToolTip();

        public MoreViewSavedPageWindow g_ParentWnd = null;

        public MoreViewSavedPageElement(MoreViewSavedPageWindow paramWnd)
        {
            InitializeComponent();
            g_ParentWnd = paramWnd;
            InitEventHandler();
            SelectBorder.Visibility = System.Windows.Visibility.Hidden;
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
            this.MouseEnter += MoreViewSavedPageElement_MouseEnter;

            ToolTipService.SetInitialShowDelay(this, 0);
            ToolTipService.SetShowDuration(this, int.MaxValue);
            _previewToolTip.Content = "미리보기 준비중...";
            this.ToolTip = _previewToolTip;
        }

        void SavedPageElement_Loaded(object sender, RoutedEventArgs e)
        {

            BeginPreviewLoadIfNeeded();
        }

        private void MoreViewSavedPageElement_MouseEnter(object sender, MouseEventArgs e)
        {
            _previewToolTip.IsOpen = true;
            _previewToolTip.Visibility = Visibility.Visible;
            BeginPreviewLoadIfNeeded();
        }

        private void BeginPreviewLoadIfNeeded()
        {
            if (_isPreviewLoaded || _isPreviewLoading)
            {
                return;
            }

            _isPreviewLoading = true;
            _previewToolTip.Content = "미리보기 준비중...";

            var scheduler = TaskScheduler.FromCurrentSynchronizationContext();
            Task.Run(() =>
            {
                return MediaTools.CreateBitmapFromBase64(g_PreviewThumbBase64);
            }).ContinueWith(t =>
            {
                _isPreviewLoading = false;

                BitmapSource bitmap = t.Result;
                if (bitmap == null)
                {
                    _previewToolTip.Content = "미리보기를 불러오지 못했습니다.";
                    return;
                }

                FrameworkElement preview = BuildPreviewElement(bitmap);
                _previewToolTip.Content = preview;
                _previewToolTip.IsOpen = true;
                _isPreviewLoaded = true;
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

        void MC_DeletePage_Click(object sender, RoutedEventArgs e)
        {
            // Pages 폴더에 폴더와 Preview 이미지를 지운다.
            string currentPageName = pageNameTextBlock.Text;

            DataShop.Instance.g_PageInfoManager.DeletePagesByPageName(currentPageName);
            Page2.Instance.RemovePageAndList(currentPageName);

            Page2.Instance.ClearIncludedPageList();

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
                 this.g_ParentWnd.MoreViewSavedPageWindowClose();
            }
            else
            {

            }
        }

        void SavedPageElement_PreviewMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (MessageTools.ShowMessageBox(string.Format("페이지 [{0}]을(를) 로드하시겠습니까?", pageNameTextBlock.Text), "예", "아니오") == true)
            {
                Page1.Instance.LoadSelectedPage(pageNameTextBlock.Text);
                this.g_ParentWnd.MoreViewSavedPageWindowClose();
            }
            else
            {

            }
        }

        private void DisplayCotextMenu_Opened(object sender, RoutedEventArgs e)
        {
            UpdateUsbMenuVisibility();
        }

        void PageListElement_MouseLeave(object sender, MouseEventArgs e)
        {
            if (g_IsSelected == false)
            {
                SelectBorder.Visibility = System.Windows.Visibility.Hidden;
 
            }

            _previewToolTip.IsOpen = false;
            _previewToolTip.Content = null;
            _previewToolTip.Visibility = Visibility.Collapsed;
            _isPreviewLoaded = false;
            _isPreviewLoading = false;
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

        private void UpdateUsbMenuVisibility()
        {
            bool hasUsb = Page2.Instance != null && Page2.Instance.HasAvailableUsb();
            Visibility visibility = hasUsb ? Visibility.Visible : Visibility.Collapsed;

            MC_CopyToUSB.Visibility = visibility;
            SepUSB.Visibility = visibility;
        }
    }
}
