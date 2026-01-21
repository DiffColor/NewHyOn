using System.Windows;

namespace AndoW_Manager
{
    /// <summary>
    /// SavePreviewImageWindow.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class SavePreviewImageWindow : Window
    {
        string g_PageName = null;

        System.Timers.Timer saveTimer = new System.Timers.Timer();

        public SavePreviewImageWindow(string pageName)
        {
            InitializeComponent();
            g_PageName = pageName;
            InitEventHandler();
        }

        void saveTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            //saveTimer.Stop();

            
            //this.Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.Normal, (Action)(() =>
            //{
            //    if (ILYCODEDataShop.Instance.g_ApplicationInfoManager.g_DataClassList[0].AIF_IsUseScreenShot == true)
            //    {
            //        this.g_ParentPage.SaveScreenShotToFile(g_PageName);
            //    }
            //    else
            //    {
            //        g_ParentPage.SavePagePreviewImage(g_PageName);
            //    }

            //    g_ParentPage.StopPreview();
            //    this.Close();
            //}));    

           
        }

        public void InitEventHandler()
        {
            this.Loaded += SavePreviewImageWindow_Loaded;
        }

        void SavePreviewImageWindow_Loaded(object sender, RoutedEventArgs e)
        {      
            Page1.Instance.ShowPreview();        

            saveTimer.Interval = 5000;
            saveTimer.Elapsed += saveTimer_Elapsed;
            saveTimer.Start();
        }


        private void BtnWin_close_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            this.Close();
        }

        private void BtnWin_drag_PreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            this.DragMove();
        }

        //public BitmapSource SaveScreenShot()
        //{
        //    using (var screenBmp = new Bitmap(
        //  (int)SystemParameters.PrimaryScreenWidth,
        //  (int)SystemParameters.PrimaryScreenHeight,
        //  System.Drawing.Imaging.PixelFormat.Format32bppArgb))
        //    {
        //        using (var bmpGraphics = Graphics.FromImage(screenBmp))
        //        {
        //            bmpGraphics.CopyFromScreen(0, 0, 0, 0, screenBmp.Size);
        //            return Imaging.CreateBitmapSourceFromHBitmap(
        //                screenBmp.GetHbitmap(),
        //                IntPtr.Zero,
        //                Int32Rect.Empty,
        //                BitmapSizeOptions.FromEmptyOptions());
        //        }
        //    }
        //}

    }
}
