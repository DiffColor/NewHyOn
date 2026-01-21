using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

using System.IO;
using TurtleTools;


namespace PageViewer
{
    /// <summary>
    /// TextElementForEditor4.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class TextImageControl : UserControl
    {
        public TextImageControl()
        {
            InitializeComponent();
        }

        public void SetData(List<PreviewData> data)
        {
            TextDisplayName.Foreground = ColorTools.GetSolidColorBrushByHexString(data[0].FontColor);
            TextDisplayName.FontFamily = new FontFamily(data[0].FontName);

            if (string.IsNullOrEmpty(data[0].FilePath))
            {
                BackgroundImg.Visibility = Visibility.Hidden;
                ContentGrid.Background = ColorTools.GetSolidColorBrushByHexString(data[0].BGColor);
            }
            else
            {
                string _fpath = data[0].FilePath;
                if (File.Exists(_fpath) == false)
                    _fpath = FNDTools.GetContentsFilePath(Path.GetFileName(_fpath));

                MediaTools.DisplayImage(BackgroundImg, _fpath);
                BackgroundImg.Visibility = Visibility.Visible;
            }

            TextDisplayName.FontSize = data[0].FontSize;
            TextDisplayName.Text = data[0].TextContent;
            TextDisplayName.FontWeight = data[0].IsBold ? FontWeights.Bold : FontWeights.Normal;
            TextDisplayName.FontStyle = data[0].IsItalic ? FontStyles.Italic : FontStyles.Normal;
        }
    }
}
