using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Threading;
using System.Threading.Tasks;

using TurtleTools;

namespace AndoW_Manager
{
    /// <summary>
    /// SavedPageElement.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class SavedPageElement2 : UserControl
    {   
        public bool g_IsSelected = false;
        public string g_PreviewThumbBase64 = string.Empty;
        private CancellationTokenSource _previewLoadCts;

        public SavedPageElement2()
        {
            InitializeComponent();
            InitEventHandler();
            SelectBorder.Visibility = System.Windows.Visibility.Hidden;
            Loaded += SavedPageElement_Loaded;
            Unloaded += SavedPageElement_Unloaded;
        }

        public void InitEventHandler()
        {
            this.PreviewMouseMove += new MouseEventHandler(PageListElement_PreviewMouseMove);
            this.MouseLeave += new MouseEventHandler(PageListElement_MouseLeave);
            this.PreviewMouseDoubleClick += SavedPageElement_PreviewMouseDoubleClick;

            MC_DeletePage.Click += MC_DeletePage_Click;
            BorderBTN_Copy.Click += BorderBTN_Copy_Click;
            MC_CopyToUSB.Click += MC_CopyToUSB_Click;
            DisplayCotextMenu.Opened += DisplayCotextMenu_Opened;
        }

        void MC_DeletePage_Click(object sender, RoutedEventArgs e)
        {
            string pageName = pageNameTextBlock.Text;
            DataShop.Instance.g_PageInfoManager.DeletePagesByPageName(pageName);
            Page2.Instance.RemovePageAndList(pageName);
            MainWindow.Instance.RefreshSavedPageList();
        }

        void MC_CopyToUSB_Click(object sender, RoutedEventArgs e)
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
            Page2.Instance.AddPageToPageList(pageNameTextBlock.Text);
        }

        void SavedPageElement_PreviewMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            Page2.Instance.AddPageToPageList(pageNameTextBlock.Text);
        }

        void DisplayCotextMenu_Opened(object sender, RoutedEventArgs e)
        {
            UpdateUsbMenuVisibility();
        }

        void PageListElement_MouseLeave(object sender, MouseEventArgs e)
        {
            if (g_IsSelected == false)
            {
                SelectBorder.Visibility = System.Windows.Visibility.Hidden;
              
            }
        }

        void PageListElement_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (g_IsSelected == false)
            {
                SelectBorder.Visibility = System.Windows.Visibility.Visible;
            }
        }

        public BitmapSource LoadPreviewImage()
        {
            return MediaTools.CreateBitmapFromBase64(g_PreviewThumbBase64);
        }

        private async void SavedPageElement_Loaded(object sender, RoutedEventArgs e)
        {
            if (pagePreviewImge.Source != null || string.IsNullOrWhiteSpace(g_PreviewThumbBase64))
            {
                return;
            }

            CancelPreviewLoad();
            var previewLoadCts = new CancellationTokenSource();
            _previewLoadCts = previewLoadCts;
            CancellationToken cancellationToken = previewLoadCts.Token;
            string previewThumbBase64 = g_PreviewThumbBase64;

            try
            {
                BitmapSource preview = await Task.Run(
                    () => MediaTools.CreateBitmapFromBase64(previewThumbBase64),
                    cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();

                if (IsLoaded && preview != null)
                {
                    pagePreviewImge.Source = preview;
                }
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                if (ReferenceEquals(_previewLoadCts, previewLoadCts))
                {
                    _previewLoadCts = null;
                }
                previewLoadCts.Dispose();
            }
        }

        private void SavedPageElement_Unloaded(object sender, RoutedEventArgs e)
        {
            CancelPreviewLoad();
        }

        private void CancelPreviewLoad()
        {
            CancellationTokenSource previewLoadCts = _previewLoadCts;
            if (previewLoadCts == null)
            {
                return;
            }

            _previewLoadCts = null;
            try
            {
                if (previewLoadCts.IsCancellationRequested == false)
                {
                    previewLoadCts.Cancel();
                }
            }
            catch (ObjectDisposedException)
            {
            }
        }

        void UpdateUsbMenuVisibility()
        {
            bool hasUsb = Page2.Instance != null && Page2.Instance.HasAvailableUsb();
            Visibility visibility = hasUsb ? Visibility.Visible : Visibility.Collapsed;

            MC_CopyToUSB.Visibility = visibility;
            SepUSB.Visibility = visibility;
        }
    }
}
