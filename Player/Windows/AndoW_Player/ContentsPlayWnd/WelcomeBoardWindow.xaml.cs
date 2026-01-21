using System;
using System.Windows;
using System.Windows.Interop;
using System.Diagnostics;
using System.IO;
using HyOnPlayer.DataManager;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using TurtleTools;

namespace HyOnPlayer
{
    /// <summary>
    /// WelcomeBoardWindow.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class WelcomeBoardWindow : Window
    {
        public ElementInfoClass g_ElementInfoClass = new ElementInfoClass();
        public TextInfoManager g_TextInfoManager = new TextInfoManager();
        public TextInfoClass g_TextInfoClass = new TextInfoClass();
        
        public WelcomeBoardWindow()
        {
            InitializeComponent();
        }
                
        public void UpdateTextInfoClsFromPage(ElementInfoClass tmpCls, string pageName)
        {
            g_ElementInfoClass.CopyData(tmpCls);
            g_TextInfoManager.LoadData(pageName, tmpCls.EIF_Name);
            this.g_TextInfoClass.CopyData(g_TextInfoManager.g_DataClassList[0]);

            //string imgPath = FNDTools.GetWelcomeBgPath(pageName, g_TextInfoClass.CIF_DataImageFileName);
            //DisplayImage(imgPath);
        }

        public void DisplayImage(string imgPath)
        {
            if (File.Exists(imgPath))
            {
                try
                {
                    BitmapImage bi = new BitmapImage();

                    bi.BeginInit();

                    bi.DecodePixelWidth = (int)this.ActualWidth;
                    bi.DecodePixelHeight = (int)this.ActualHeight;
                    bi.CacheOption = BitmapCacheOption.OnLoad;
                    bi.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;

                    bi.UriSource = new Uri(imgPath);
                    bi.EndInit();
                    bi.Freeze();

                    BG.Source = bi;

                    if (!BG.IsVisible) BG.Visibility = Visibility.Visible;
                }
                catch (Exception ex)
                {
                    Logger.WriteErrorLog(string.Format("@DisplayImage __Player. {0}", ex.ToString()), Logger.GetLogFileName());
                }
            }
        }

        private void Window_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (!(bool)e.NewValue)
            {
                BG.Visibility = Visibility.Hidden;
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            LayoutRoot.UpdateLayout();
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }
    }
}
