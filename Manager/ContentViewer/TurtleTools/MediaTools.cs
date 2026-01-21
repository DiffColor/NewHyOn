using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using MediaInfo.DotNetWrapper.Enumerations;
using MediaInfo.DotNetWrapper;

namespace TurtleTools
{
    class MediaTools
    {

        public static ImageCodecInfo GetEncoderInfo(ImageFormat format)
        {
            return ImageCodecInfo.GetImageDecoders().SingleOrDefault(c => c.FormatID == format.Guid);
        }

        private static ImageCodecInfo GetEncoder(ImageFormat format)
        {
            ImageCodecInfo[] codecs = ImageCodecInfo.GetImageDecoders();

            foreach (ImageCodecInfo codec in codecs)
            {
                if (codec.FormatID == format.Guid)
                {
                    return codec;
                }
            }
            return null;
        }


        /*
         *  Metadata (MediaInfo)
         */
        public static Rotation GetImageRotationFromExif(string imgpath)
        {
            int angle = GetImageRotateAngleFromMediaInfo(imgpath);
            return ConvertAngleToRotation(angle);
        }

        public static RotateFlipType GetImageRotateFlipTypeFromExif(string imgpath)
        {
            int angle = GetImageRotateAngleFromMediaInfo(imgpath);
            return ConvertAngleToRotateFlipType(angle);
        }

        public static int GetImageRotateAngleFromExif(string imgpath)
        {
            return GetImageRotateAngleFromMediaInfo(imgpath);
        }

        private static int GetImageRotateAngleFromMediaInfo(string mediaPath)
        {
            try
            {
                var mediaInfo = new MediaInfo.DotNetWrapper.MediaInfo();
                if (mediaInfo.Open(mediaPath) == 0)
                {
                    return 0;
                }

                int angle = ExtractMediaInfoAngle(mediaInfo, StreamKind.Image);
                if (angle == 0)
                {
                    angle = ExtractMediaInfoAngle(mediaInfo, StreamKind.Video);
                }

                mediaInfo.Close();
                return angle;
            }
            catch
            {
            }

            return 0;
        }

        private static int ExtractMediaInfoAngle(MediaInfo.DotNetWrapper.MediaInfo mediaInfo, StreamKind streamKind)
        {
            string rotationVal = mediaInfo.Get(streamKind, 0, "Rotation");
            int angle = ParseRotationAngle(rotationVal);
            if (angle != 0)
            {
                return angle;
            }

            string orientationVal = mediaInfo.Get(streamKind, 0, "Orientation");
            angle = ParseRotationAngle(orientationVal);
            if (angle != 0)
            {
                return angle;
            }

            int orientationCode = ParseOrientationCode(orientationVal);
            if (orientationCode > 0)
            {
                return GetImageRotateAngle(orientationCode);
            }

            return 0;
        }
        
        public static RotateFlipType ConvertAngleToRotateFlipType(int angle)
        {
            RotateFlipType ret = RotateFlipType.RotateNoneFlipNone;

            switch (NormalizeRightAngle(angle))
            {
                case 0:
                    ret = RotateFlipType.RotateNoneFlipNone;
                    break;
                case 90:
                    ret = RotateFlipType.Rotate90FlipNone;
                    break;
                case 180:
                    ret = RotateFlipType.Rotate180FlipNone;
                    break;
                case 270:
                    ret = RotateFlipType.Rotate270FlipNone;
                    break;
            }

            return ret;
        }

        private static Rotation ConvertAngleToRotation(int angle)
        {
            switch (NormalizeRightAngle(angle))
            {
                case 90:
                    return Rotation.Rotate90;
                case 180:
                    return Rotation.Rotate180;
                case 270:
                    return Rotation.Rotate270;
                default:
                    return Rotation.Rotate0;
            }
        }

        private static int ParseRotationAngle(string raw)
        {
            double angle;
            if (TryParseAngle(raw, out angle))
            {
                return NormalizeRightAngle(angle);
            }

            return 0;
        }

        private static int ParseOrientationCode(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return 0;
            }

            string number = ExtractFirstNumber(raw);
            int orientationCode;
            if (int.TryParse(number, NumberStyles.Integer, CultureInfo.InvariantCulture, out orientationCode))
            {
                return orientationCode;
            }

            return 0;
        }

        private static bool TryParseAngle(string raw, out double angle)
        {
            angle = 0;

            if (string.IsNullOrWhiteSpace(raw))
            {
                return false;
            }

            Match match = Regex.Match(raw, @"-?\d+(\.\d+)?");
            if (match.Success == false)
            {
                return false;
            }

            return double.TryParse(match.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out angle);
        }

        private static string ExtractFirstNumber(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return string.Empty;
            }

            Match match = Regex.Match(raw, @"-?\d+");
            return match.Success ? match.Value : string.Empty;
        }

        private static int ParseIntValue(string raw)
        {
            int value;
            if (int.TryParse(ExtractFirstNumber(raw), NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
            {
                return value;
            }

            return 0;
        }

        private static int NormalizeRightAngle(double angle)
        {
            if (double.IsNaN(angle) || double.IsInfinity(angle))
            {
                return 0;
            }

            int normalized = (int)Math.Round(angle);
            normalized %= 360;
            if (normalized < 0)
            {
                normalized += 360;
            }

            int[] candidates = new[] { 0, 90, 180, 270 };
            return candidates.OrderBy(a => Math.Abs(a - normalized)).First();
        }

        private static int GetImageRotateAngle(int exifInt)
        {
            int ret = 0;

            switch (exifInt)
            {
                case 1:
                    ret = 0;
                    break;
                case 2:
                    break;
                case 3:
                    ret = 180;
                    break;
                case 4:
                    break;
                case 5:
                    break;
                case 6:
                    ret = 90;
                    break;
                case 7:
                    break;
                case 8:
                    ret = 270;
                    break;
            }

            return ret;
        }


        /*
         * MediaInfo
         */

        public static int GetVideoRotateAngle(string videopath)
        {
            try
            {
                var mediaInfo = new MediaInfo.DotNetWrapper.MediaInfo();
                if (mediaInfo.Open(videopath) == 0)
                {
                    return 0;
                }

                int angle = ExtractMediaInfoAngle(mediaInfo, StreamKind.Video);
                mediaInfo.Close();
                return angle;
            }
            catch
            {
            }

            return 0;
        }

        public static System.Drawing.Size GetVideoSize(string fpath)
        {
            try
            {
                var mediaInfo = new MediaInfo.DotNetWrapper.MediaInfo();
                if (mediaInfo.Open(fpath) == 0)
                {
                    return new System.Drawing.Size(0, 0);
                }

                int width = ParseIntValue(mediaInfo.Get(StreamKind.Video, 0, "Width"));
                int height = ParseIntValue(mediaInfo.Get(StreamKind.Video, 0, "Height"));

                if (width <= 0 || height <= 0)
                {
                    width = ParseIntValue(mediaInfo.Get(StreamKind.General, 0, "Width"));
                    height = ParseIntValue(mediaInfo.Get(StreamKind.General, 0, "Height"));
                }

                mediaInfo.Close();

                return new System.Drawing.Size(Math.Max(width, 0), Math.Max(height, 0));
            }
            catch
            {
            }

            return new System.Drawing.Size(0, 0);
        }

        public static TimeSpan GetVideoDuration(string fpath)
        {
            try
            {
                TimeSpan _ts = TimeSpan.Zero;
                var mediaInfo = new MediaInfo.DotNetWrapper.MediaInfo();
                if (mediaInfo.Open(fpath) == 0)
                {
                    return TimeSpan.Zero;
                }

                ulong durationMs = 0;
                ulong.TryParse(ExtractFirstNumber(mediaInfo.Get(StreamKind.General, 0, "Duration")), NumberStyles.Integer, CultureInfo.InvariantCulture, out durationMs);
                _ts = TimeSpan.FromMilliseconds(durationMs);
                mediaInfo.Close();
                return _ts;
            }
            catch (Exception e)
            {
            }

            return TimeSpan.Zero;
        }

        public static BitmapSource GetVideoThumb(string fpath, float time = 1)
        {
            if (File.Exists(fpath) == false)
            {
                return null;
            }

            try
            {
                System.Drawing.Size videoSize = GetVideoSize(fpath);
                BitmapSource capturedFrame = null;
                MediaPlayer player = new MediaPlayer();
                DispatcherFrame frameWaiter = new DispatcherFrame();

                EventHandler openedHandler = null;
                EventHandler<ExceptionEventArgs> failedHandler = null;

                DispatcherTimer timeoutTimer = new DispatcherTimer(TimeSpan.FromSeconds(3), DispatcherPriority.Background, (s, e) =>
                {
                    ((DispatcherTimer)s).Stop();
                    frameWaiter.Continue = false;
                }, Dispatcher.CurrentDispatcher);

                openedHandler = (s, e) =>
                {
                    player.ScrubbingEnabled = true;
                    if (time > 0)
                    {
                        player.Position = TimeSpan.FromSeconds(time);
                    }

                    player.Play();
                    player.Pause();

                    int renderWidth = player.NaturalVideoWidth > 0 ? player.NaturalVideoWidth : videoSize.Width;
                    int renderHeight = player.NaturalVideoHeight > 0 ? player.NaturalVideoHeight : videoSize.Height;

                    renderWidth = renderWidth > 0 ? renderWidth : 320;
                    renderHeight = renderHeight > 0 ? renderHeight : 180;

                    DrawingVisual drawingVisual = new DrawingVisual();
                    using (DrawingContext drawingContext = drawingVisual.RenderOpen())
                    {
                        drawingContext.DrawVideo(player, new Rect(0, 0, renderWidth, renderHeight));
                    }

                    RenderTargetBitmap targetBitmap = new RenderTargetBitmap(renderWidth, renderHeight, 96, 96, PixelFormats.Pbgra32);
                    targetBitmap.Render(drawingVisual);
                    targetBitmap.Freeze();

                    capturedFrame = targetBitmap;
                    frameWaiter.Continue = false;
                };

                failedHandler = (s, e) =>
                {
                    capturedFrame = null;
                    frameWaiter.Continue = false;
                };

                player.MediaOpened += openedHandler;
                player.MediaFailed += failedHandler;
                player.Open(new Uri(fpath, UriKind.RelativeOrAbsolute));

                timeoutTimer.Start();
                Dispatcher.PushFrame(frameWaiter);
                timeoutTimer.Stop();

                player.MediaOpened -= openedHandler;
                player.MediaFailed -= failedHandler;
                player.Close();

                return capturedFrame;
            }
            catch
            {
            }

            return null;
        }

        /*
         * MediaType
         */
        static List<string> VideoTypeExtentions = new List<string> { "mov", "flv", "avi", "mp4", "wmv", "mpeg", "mpg", "mkv", "ts", "asf", "m2ts" };
        static List<string> ImageTypeExtentions = new List<string> { "jpg", "jpeg", "png", "gif", "bmp" };
        static List<string> PPTTypeExtentions = new List<string> { "ppt", "pptx", "pps" };

        public static bool CheckIsVideoFile(string fname)
        {
            return VideoTypeExtentions.Contains(Path.GetExtension(fname).TrimStart('.'));
        }

        public static bool CheckIsImageFile(string fname)
        {
            return ImageTypeExtentions.Contains(Path.GetExtension(fname).TrimStart('.'));
        }

        public static bool CheckIsFlashFile(string fname)
        {
            return Path.GetExtension(fname).TrimStart('.').Equals("swf", StringComparison.CurrentCultureIgnoreCase);
        }

        public static bool CheckIsPPTFile(string fname)
        {
            return PPTTypeExtentions.Contains(Path.GetExtension(fname).TrimStart('.'));
        }

        #region Image Header Reader
        const string errorMessage = "Could not recognise image format.";

        private static Dictionary<byte[], Func<BinaryReader, System.Drawing.Size>> imageFormatDecoders = new Dictionary<byte[], Func<BinaryReader, System.Drawing.Size>>()
        {
            { new byte[] { 0x42, 0x4D }, DecodeBitmap },
            { new byte[] { 0x47, 0x49, 0x46, 0x38, 0x37, 0x61 }, DecodeGif },
            { new byte[] { 0x47, 0x49, 0x46, 0x38, 0x39, 0x61 }, DecodeGif },
            { new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }, DecodePng },
            { new byte[] { 0xff, 0xd8 }, DecodeJfif },
        };

        /// <summary>        
        /// Gets the dimensions of an image.        
        /// </summary>        
        /// <param name="fpath">The path of the image to get the dimensions of.</param>        
        /// <returns>The dimensions of the specified image.</returns>        
        /// <exception cref="ArgumentException">The image was of an unrecognised format.</exception>        
        public static System.Drawing.Size GetDimensions(string fpath)
        {
            try
            {
                using (BinaryReader binaryReader = new BinaryReader(File.OpenRead(fpath)))
                {
                    return GetDimensions(binaryReader);
                }
            }
            catch (Exception)
            {
                using (Bitmap b = new Bitmap(fpath))
                {
                    return b.Size;
                }
            }
        }

        /// <summary>        
        /// Gets the dimensions of an image.        
        /// </summary>        
        /// <param name="path">The path of the image to get the dimensions of.</param>        
        /// <returns>The dimensions of the specified image.</returns>        
        /// <exception cref="ArgumentException">The image was of an unrecognised format.</exception>            
        public static System.Drawing.Size GetDimensions(BinaryReader binaryReader)
        {
            int maxMagicBytesLength = imageFormatDecoders.Keys.OrderByDescending(x => x.Length).First().Length;
            byte[] magicBytes = new byte[maxMagicBytesLength];
            for (int i = 0; i < maxMagicBytesLength; i += 1)
            {
                magicBytes[i] = binaryReader.ReadByte();
                foreach (var kvPair in imageFormatDecoders)
                {
                    if (StartsWith(magicBytes, kvPair.Key))
                    {
                        return kvPair.Value(binaryReader);
                    }
                }
            }

            return new System.Drawing.Size(0, 0);
        }

        private static bool StartsWith(byte[] thisBytes, byte[] thatBytes)
        {
            for (int i = 0; i < thatBytes.Length; i += 1)
            {
                if (thisBytes[i] != thatBytes[i])
                {
                    return false;
                }
            }

            return true;
        }

        private static short ReadLittleEndianInt16(BinaryReader binaryReader)
        {
            byte[] bytes = new byte[sizeof(short)];

            for (int i = 0; i < sizeof(short); i += 1)
            {
                bytes[sizeof(short) - 1 - i] = binaryReader.ReadByte();
            }
            return BitConverter.ToInt16(bytes, 0);
        }

        private static ushort ReadLittleEndianUInt16(BinaryReader binaryReader)
        {
            byte[] bytes = new byte[sizeof(ushort)];

            for (int i = 0; i < sizeof(ushort); i += 1)
            {
                bytes[sizeof(ushort) - 1 - i] = binaryReader.ReadByte();
            }
            return BitConverter.ToUInt16(bytes, 0);
        }

        private static int ReadLittleEndianInt32(BinaryReader binaryReader)
        {
            byte[] bytes = new byte[sizeof(int)];
            for (int i = 0; i < sizeof(int); i += 1)
            {
                bytes[sizeof(int) - 1 - i] = binaryReader.ReadByte();
            }
            return BitConverter.ToInt32(bytes, 0);
        }

        private static System.Drawing.Size DecodeBitmap(BinaryReader binaryReader)
        {
            binaryReader.ReadBytes(16);
            int width = binaryReader.ReadInt32();
            int height = binaryReader.ReadInt32();
            return new System.Drawing.Size(width, height);
        }

        private static System.Drawing.Size DecodeGif(BinaryReader binaryReader)
        {
            int width = binaryReader.ReadInt16();
            int height = binaryReader.ReadInt16();
            return new System.Drawing.Size(width, height);
        }

        private static System.Drawing.Size DecodePng(BinaryReader binaryReader)
        {
            binaryReader.ReadBytes(8);
            int width = ReadLittleEndianInt32(binaryReader);
            int height = ReadLittleEndianInt32(binaryReader);
            return new System.Drawing.Size(width, height);
        }

        private static System.Drawing.Size DecodeJfif(BinaryReader binaryReader)
        {
            while (binaryReader.ReadByte() == 0xff)
            {
                byte marker = binaryReader.ReadByte();
                short chunkLength = ReadLittleEndianInt16(binaryReader);
                if (marker == 0xc0)
                {
                    binaryReader.ReadByte();
                    int height = ReadLittleEndianInt16(binaryReader);
                    int width = ReadLittleEndianInt16(binaryReader);
                    return new System.Drawing.Size(width, height);
                }

                if (chunkLength < 0)
                {
                    ushort uchunkLength = (ushort)chunkLength;
                    binaryReader.ReadBytes(uchunkLength - 2);
                }
                else
                {
                    binaryReader.ReadBytes(chunkLength - 2);
                }
            }

            throw new ArgumentException(errorMessage);
        }
        #endregion


        public static System.Windows.Media.Brush CreateBrushFromImgPath(string fpath)
        {
            ImageBrush b = null;

            using (Bitmap bmp = LoadBitmap(fpath))
            {
                b = new ImageBrush(GetBitmapSourceFromHBitmap(bmp));
            }

            return b;
        }

        public static System.Drawing.Bitmap GetBitmapFromBitmapSource(BitmapSource bitmapSource)
        {
            System.Drawing.Bitmap bitmap;

            using (MemoryStream memoryStream = new MemoryStream())
            {
                BitmapEncoder bitmapEncoder = new BmpBitmapEncoder();
                bitmapEncoder.Frames.Add(BitmapFrame.Create(bitmapSource));
                bitmapEncoder.Save(memoryStream);

                bitmap = new System.Drawing.Bitmap(memoryStream);
            }

            return bitmap;
        }

        public static Bitmap LoadBitmap(string fpath)
        {
            using (FileStream stream = new FileStream(fpath, FileMode.Open, FileAccess.Read))
            using (BinaryReader reader = new BinaryReader(stream))
            {
                var memoryStream = new MemoryStream(reader.ReadBytes((int)stream.Length));
                return new Bitmap(memoryStream);
            }
        }

        public static ImageSource GetImageSourceFromFile(string fpath, double longside = 0.0f)
        {
            BitmapImage bi = null;

            try
            {
                bi = new BitmapImage();

                bi.BeginInit();

                bi.Rotation = GetImageRotationFromExif(fpath);
                bi.CacheOption = BitmapCacheOption.OnLoad;
                bi.CreateOptions = BitmapCreateOptions.IgnoreImageCache;

                bi.UriSource = new Uri(fpath, UriKind.RelativeOrAbsolute);

                if (longside > 0.0f)
                {
                    System.Drawing.Size sz = GetDimensions(fpath);
                    double longPixel = sz.Width > sz.Height ? sz.Width : sz.Height;

                    double scale = (float)(longside / longPixel);
                    bi.DecodePixelWidth = (int)(sz.Width * scale);
                    bi.DecodePixelHeight = (int)(sz.Height * scale);
                }

                bi.EndInit();
                bi.Freeze();
            }
            catch (Exception ex)
            {
            }

            return bi;
        }

        public static BitmapSource GetBitmapSourceFromFile(string fpath, double longside = 0.0f)
        {
            return (BitmapSource)GetImageSourceFromFile(fpath, longside);
        }

        public static void DisplayImage(System.Windows.Controls.Image imgCtrl, string fpath, bool fitting = true, int width = -1, int height = -1)
        {
            if (File.Exists(fpath) == false)
                return;

            try
            {
                BitmapImage bi = new BitmapImage();

                bi.BeginInit();

                bi.Rotation = GetImageRotationFromExif(fpath);
                bi.CacheOption = BitmapCacheOption.OnLoad;
                bi.CreateOptions = BitmapCreateOptions.IgnoreImageCache;

                if (fitting)
                {
                    double wPixel = imgCtrl.Width;
                    double hPixel = imgCtrl.Height;

                    if (double.IsNaN(wPixel) || double.IsNaN(hPixel))
                    {
                        wPixel = imgCtrl.ActualWidth;
                        hPixel = imgCtrl.ActualHeight;
                    }

                    using (var stream = File.OpenRead(fpath))
                    {
                        var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.IgnoreImageCache, BitmapCacheOption.Default);
                        var bmp_width = decoder.Frames[0].PixelWidth;
                        var bmp_height = decoder.Frames[0].PixelHeight;

                        double percentWidth = wPixel / bmp_width;
                        double percentHeight = hPixel / bmp_height;

                        double percent = percentHeight < percentWidth ? percentHeight : percentWidth;

                        wPixel = bmp_width * percent;
                        hPixel = bmp_height * percent;
                    }

                    bi.DecodePixelWidth = (int)wPixel;
                    bi.DecodePixelHeight = (int)hPixel;
                }
                else
                {
                    if (width > -1)
                        bi.DecodePixelWidth = width;

                    if (height > -1)
                        bi.DecodePixelHeight = height;
                }

                bi.UriSource = new Uri(fpath, UriKind.RelativeOrAbsolute);

                bi.EndInit();
                bi.Freeze();

                imgCtrl.Source = bi;
            }
            catch (Exception ex)
            {
            }
        }

        public static void DisplayImageStream(System.Windows.Controls.Image imgCtrl, string fpath)
        {
            using (var fs = File.OpenRead(fpath))
            using (var image = System.Drawing.Image.FromStream(fs))
                imgCtrl.Source = GetBitmapSourceFromHBitmap((Bitmap)image);
        }

        public static void DisplayCopiedImage(System.Windows.Controls.Image imgCtrl, string fpath, bool fitting = true, int width = -1, int height = -1)
        {
            using (FileStream stream = new FileStream(fpath, FileMode.Open, FileAccess.Read))
            {
                BitmapImage bi = new BitmapImage();

                bi.BeginInit();

                bi.Rotation = GetImageRotationFromExif(fpath);
                bi.CacheOption = BitmapCacheOption.OnLoad;
                bi.StreamSource = stream;

                if (fitting)
                {
                    double wPixel = imgCtrl.Width;
                    double hPixel = imgCtrl.Height;

                    if (double.IsNaN(wPixel) || double.IsNaN(hPixel))
                    {
                        wPixel = imgCtrl.ActualWidth;
                        hPixel = imgCtrl.ActualHeight;
                    }

                    var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.IgnoreImageCache, BitmapCacheOption.Default);
                    var bmp_width = decoder.Frames[0].PixelWidth;
                    var bmp_height = decoder.Frames[0].PixelHeight;

                    double percentWidth = wPixel / bmp_width;
                    double percentHeight = hPixel / bmp_height;

                    double percent = percentHeight < percentWidth ? percentHeight : percentWidth;

                    wPixel = bmp_width * percent;
                    hPixel = bmp_height * percent;

                    bi.DecodePixelWidth = (int)wPixel;
                    bi.DecodePixelHeight = (int)hPixel;
                }
                else
                {
                    if (width > -1)
                        bi.DecodePixelWidth = width;

                    if (height > -1)
                        bi.DecodePixelHeight = height;
                }

                bi.UriSource = new Uri(fpath, UriKind.RelativeOrAbsolute);

                bi.EndInit();
                bi.Freeze();

                BitmapSource prgbaSource = new FormatConvertedBitmap(bi, PixelFormats.Pbgra32, null, 0);
                WriteableBitmap bmp = new WriteableBitmap(prgbaSource);
                int w = bmp.PixelWidth;
                int h = bmp.PixelHeight;
                int[] pixelData = new int[w * h];
                //int widthInBytes = 4 * w;
                int widthInBytes = bmp.PixelWidth * (bmp.Format.BitsPerPixel / 8); //equals 4*w
                bmp.CopyPixels(pixelData, widthInBytes, 0);
                bmp.WritePixels(new Int32Rect(0, 0, w, h), pixelData, widthInBytes, 0);
                bi = null;

                imgCtrl.Source = (BitmapSource)bmp;
            }
        }

        /// <summary>
        /// Creates a new ImageSource with the specified width/height
        /// </summary>
        /// <param name="source">Source image to resize</param>
        /// <param name="width">Width of resized image</param>
        /// <param name="height">Height of resized image</param>
        /// <returns>Resized image</returns>
        ImageSource CreateResizedImage(ImageSource source, int width, int height)
        {
            // Target Rect for the resize operation
            Rect rect = new Rect(0, 0, width, height);

            // Create a DrawingVisual/Context to render with
            DrawingVisual drawingVisual = new DrawingVisual();
            using (DrawingContext drawingContext = drawingVisual.RenderOpen())
            {
                drawingContext.DrawImage(source, rect);
            }

            // Use RenderTargetBitmap to resize the original image
            RenderTargetBitmap resizedImage = new RenderTargetBitmap(
                (int)rect.Width, (int)rect.Height,  // Resized dimensions
                96, 96,                             // Default DPI values
                PixelFormats.Default);              // Default pixel format
            resizedImage.Render(drawingVisual);

            // Return the resized image
            return resizedImage;
        }

        //public static void DisplayImageByURL(System.Windows.Controls.Image imgCtrl, string url)
        //{
        //    try
        //    {
        //        BitmapImage bi = new BitmapImage();

        //        bi.BeginInit();

        //        bi.CacheOption = BitmapCacheOption.OnLoad;
        //        bi.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
        //        bi.UriSource = new Uri(FileTools.EncodeURLString(url), UriKind.RelativeOrAbsolute);
        //        bi.EndInit();
        //        bi.Freeze();

        //        imgCtrl.Source = bi;
        //    }
        //    catch (Exception ex)
        //    {
        //    }
        //}


        public static void DisplayImage(System.Windows.Shapes.Rectangle rect, string fpath, bool fitting = true, int width = -1, int height = -1)
        {
            if (File.Exists(fpath) == false)
                return;

            try
            {
                BitmapImage bi = new BitmapImage();
                bi.BeginInit();

                bi.Rotation = GetImageRotationFromExif(fpath);
                bi.CacheOption = BitmapCacheOption.OnLoad;
                //bi.CreateOptions = BitmapCreateOptions.IgnoreImageCache;

                bi.StreamSource = new FileStream(fpath, FileMode.Open, FileAccess.Read);

                ImageBrush myBrush = new ImageBrush();

                if (fitting)
                {
                    double wPixel = rect.Width;
                    double hPixel = rect.Height;

                    if (double.IsNaN(wPixel) || double.IsNaN(hPixel))
                    {
                        wPixel = rect.ActualWidth;
                        hPixel = rect.ActualHeight;
                    }

                    using (var stream = File.OpenRead(fpath))
                    {
                        var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.IgnoreImageCache, BitmapCacheOption.Default);
                        var bmp_width = decoder.Frames[0].PixelWidth;
                        var bmp_height = decoder.Frames[0].PixelHeight;

                        double percentWidth = wPixel / bmp_width;
                        double percentHeight = hPixel / bmp_height;

                        double percent = percentHeight < percentWidth ? percentHeight : percentWidth;

                        wPixel = bmp_width * percent;
                        hPixel = bmp_height * percent;
                    }

                    bi.DecodePixelWidth = (int)wPixel;
                    bi.DecodePixelHeight = (int)hPixel;
                }
                else
                {
                    if (width > -1)
                        bi.DecodePixelWidth = width;

                    if (height > -1)
                        bi.DecodePixelHeight = height;
                }

                bi.EndInit();
                bi.Freeze();

                myBrush.ImageSource = bi;

                rect.Fill = myBrush;

                bi.StreamSource.Dispose();
                myBrush = null;
                bi = null;

                GC.Collect();
                GC.WaitForPendingFinalizers();
            }
            catch (Exception ex)
            {
            }
        }

        public static void DisplayImage(System.Windows.Shapes.Rectangle rect, Uri uri, bool fitting = true, int width = -1, int height = -1)
        {
            try
            {
                BitmapImage bi = new BitmapImage();
                bi.BeginInit();
                bi.UriSource = uri;

                bi.Rotation = GetImageRotationFromExif(uri.AbsolutePath);
                bi.CacheOption = BitmapCacheOption.OnLoad;
                bi.CreateOptions = BitmapCreateOptions.IgnoreImageCache;

                ImageBrush myBrush = new ImageBrush();

                if (fitting)
                {
                    double wPixel = rect.Width;
                    double hPixel = rect.Height;

                    if (double.IsNaN(wPixel) || double.IsNaN(hPixel))
                    {
                        wPixel = rect.ActualWidth;
                        hPixel = rect.ActualHeight;
                    }

                    using (var stream = File.OpenRead(uri.AbsolutePath))
                    {
                        var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.IgnoreImageCache, BitmapCacheOption.Default);
                        var bmp_width = decoder.Frames[0].PixelWidth;
                        var bmp_height = decoder.Frames[0].PixelHeight;

                        double percentWidth = wPixel / bmp_width;
                        double percentHeight = hPixel / bmp_height;

                        double percent = percentHeight < percentWidth ? percentHeight : percentWidth;

                        wPixel = bmp_width * percent;
                        hPixel = bmp_height * percent;
                    }

                    bi.DecodePixelWidth = (int)wPixel;
                    bi.DecodePixelHeight = (int)hPixel;
                }
                else
                {
                    if (width > -1)
                        bi.DecodePixelWidth = width;

                    if (height > -1)
                        bi.DecodePixelHeight = height;
                }

                bi.EndInit();
                bi.Freeze();

                myBrush.ImageSource = bi;

                rect.Fill = myBrush;

                bi.StreamSource.Dispose();
                myBrush = null;
                bi = null;

                GC.Collect();
                GC.WaitForPendingFinalizers();
            }
            catch (Exception ex)
            {
            }
        }

        public static void DisplayImage(Grid grid, string fpath, bool fitting = true, int width = -1, int height = -1)
        {
            if (File.Exists(fpath) == false)
                return;

            try
            {
                BitmapImage bi = new BitmapImage();
                bi.BeginInit();

                bi.Rotation = GetImageRotationFromExif(fpath);
                bi.CacheOption = BitmapCacheOption.OnLoad;
                //bi.CreateOptions = BitmapCreateOptions.IgnoreImageCache;

                bi.StreamSource = new FileStream(fpath, FileMode.Open, FileAccess.Read);

                ImageBrush myBrush = new ImageBrush();

                if (fitting)
                {
                    double wPixel = grid.Width;
                    double hPixel = grid.Height;

                    if (double.IsNaN(wPixel) || double.IsNaN(hPixel))
                    {
                        wPixel = grid.ActualWidth;
                        hPixel = grid.ActualHeight;
                    }

                    using (var stream = File.OpenRead(fpath))
                    {
                        var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.IgnoreImageCache, BitmapCacheOption.Default);
                        var bmp_width = decoder.Frames[0].PixelWidth;
                        var bmp_height = decoder.Frames[0].PixelHeight;

                        double percentWidth = wPixel / bmp_width;
                        double percentHeight = hPixel / bmp_height;

                        double percent = percentHeight < percentWidth ? percentHeight : percentWidth;

                        wPixel = bmp_width * percent;
                        hPixel = bmp_height * percent;
                    }

                    bi.DecodePixelWidth = (int)wPixel;
                    bi.DecodePixelHeight = (int)hPixel;
                }
                else
                {
                    if (width > -1)
                        bi.DecodePixelWidth = width;

                    if (height > -1)
                        bi.DecodePixelHeight = height;
                }

                bi.EndInit();
                bi.Freeze();

                myBrush.ImageSource = bi;

                grid.Background = myBrush;

                bi.StreamSource.Dispose();
                myBrush = null;
                bi = null;

                GC.Collect();
                GC.WaitForPendingFinalizers();
            }
            catch (Exception ex)
            {
            }
        }

        public static void ResizeImageFile(string sourcePath, string targetPath, ImageFormat codec, double longside = 0.0f, long quality = 100, int rotation = 0)
        {
            try
            {
                using (var fs = File.OpenRead(sourcePath))
                using (var image = System.Drawing.Image.FromStream(fs))
                {
                    using (var resized = ResizeBitmap(image, longside, rotation))
                    {
                        if (File.Exists(targetPath))
                            File.Delete(targetPath);

                        ImageCodecInfo jpgEncoder = GetEncoder(codec);
                        System.Drawing.Imaging.Encoder encoder = System.Drawing.Imaging.Encoder.Quality;
                        EncoderParameters encoderParameters = new EncoderParameters(1);
                        EncoderParameter encoderParameter = new EncoderParameter(encoder, quality);
                        encoderParameters.Param[0] = encoderParameter;

                        resized.Save(targetPath, jpgEncoder, encoderParameters);
                    }
                }
            }
            catch (Exception e)
            {
            }
        }

        public void DisplayBase64Image(System.Windows.Controls.Image imgCtrl, string base64data)
        {
            byte[] binaryData = Convert.FromBase64String(base64data);

            BitmapImage bi = new BitmapImage();

            bi.BeginInit();

            bi.CacheOption = BitmapCacheOption.OnLoad;
            //bi.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
            bi.StreamSource = new MemoryStream(binaryData);
            bi.EndInit();
            bi.Freeze();

            imgCtrl.Source = bi;
        }

        public static void DisplayBase64Image(System.Windows.Shapes.Rectangle rect, string base64data)
        {
            try
            {
                byte[] binaryData = Convert.FromBase64String(base64data);

                BitmapImage bi = new BitmapImage();
                bi.BeginInit();

                bi.CacheOption = BitmapCacheOption.OnLoad;
                //bi.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
                bi.StreamSource = new MemoryStream(binaryData);

                ImageBrush myBrush = new ImageBrush();

                bi.EndInit();
                bi.Freeze();

                myBrush.ImageSource = bi;

                rect.Fill = myBrush;

                bi.StreamSource.Dispose();
                myBrush = null;
                bi = null;

                GC.Collect();
                GC.WaitForPendingFinalizers();
            }
            catch (Exception ex)
            {
            }
        }

        #region GDI+

        public enum Dimensions
        {
            Width,
            Height
        }

        public enum AnchorPosition
        {
            Top,
            Center,
            Bottom,
            Left,
            Right
        }

        [DllImport("gdi32.dll")]
        public static extern IntPtr DeleteObject(IntPtr hDc);

        static Bitmap ResizeBitmap(System.Drawing.Image image, double longside = 0.0f, int rotation = 0)
        {
            if (image == null) throw new Exception();

            float scale = 1.0f;

            if (longside > 0.0f)
            {
                int longPixel = image.Width > image.Height ? image.Width : image.Height;
                scale = (float)(longside / longPixel);
            }

            var width = (int)(image.Width * scale);
            var height = (int)(image.Height * scale);

            var bmp = new Bitmap(width, height);
            bmp.SetResolution(image.HorizontalResolution, image.VerticalResolution);  // maintain image resolution

            bool isTopDown = false;

            bool isTopSide = false;
            var trfWidth = width;
            var trfHeight = height;

            if (rotation == 90 || rotation == 270)
            {
                isTopSide = true;
                trfWidth = height;
                trfHeight = width;
                bmp.RotateFlip(ConvertAngleToRotateFlipType(rotation));
            }
            else if (rotation == 180)
            {
                isTopDown = true;
                bmp.RotateFlip(ConvertAngleToRotateFlipType(rotation));
            }

            using (var g = Graphics.FromImage(bmp))
            {
                //g.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceOver;
                //g.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
                //g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                //g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;

                if (isTopSide)
                {
                    //move rotation point to center of image
                    g.TranslateTransform((float)trfWidth / 2, (float)trfHeight / 2);
                    //rotate
                    g.RotateTransform(rotation);
                    //move image back
                    g.TranslateTransform(-(float)width / 2, -(float)height / 2);
                }
                else if (isTopDown)
                {
                    g.RotateTransform(rotation);
                }

                g.DrawImage(image, new Rectangle(0, 0, width, height));
                g.Save();
            }

            return bmp;
        }

        static Bitmap ResizeBitmap(System.Drawing.Image source, int width, int height)
        {
            if (source == null) throw new Exception();

            var bmp = new Bitmap(width, height);
            bmp.SetResolution(source.HorizontalResolution, source.VerticalResolution);  // maintain image resolution

            var trfWidth = width;
            var trfHeight = height;

            using (var g = Graphics.FromImage(bmp))
            {
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                g.DrawImage(source, new Rectangle(0, 0, width, height));
                g.Save();
            }

            return bmp;
        }

        public static System.Drawing.Image ResizeImage(System.Drawing.Image image, System.Drawing.Size size, bool preserveAspectRatio = true)
        {
            int newWidth;
            int newHeight;
            if (preserveAspectRatio)
            {
                int originalWidth = image.Width;
                int originalHeight = image.Height;
                float percentWidth = (float)size.Width / (float)originalWidth;
                float percentHeight = (float)size.Height / (float)originalHeight;
                float percent = percentHeight < percentWidth ? percentHeight : percentWidth;
                newWidth = (int)(originalWidth * percent);
                newHeight = (int)(originalHeight * percent);
            }
            else
            {
                newWidth = size.Width;
                newHeight = size.Height;
            }
            System.Drawing.Image newImage = new Bitmap(newWidth, newHeight);
            using (Graphics graphicsHandle = Graphics.FromImage(newImage))
            {
                graphicsHandle.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                graphicsHandle.DrawImage(image, 0, 0, newWidth, newHeight);
            }
            return newImage;
        }

        public static System.Drawing.Bitmap ResizeImage(System.Drawing.Image image, int width, int height)
        {
            Bitmap result = new Bitmap(width, height);

            using (Graphics graphics = Graphics.FromImage(result))
            {
                graphics.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
                graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                graphics.DrawImage(image, 0, 0, result.Width, result.Height);
            }

            return result;
        }

        public static void SaveImage(Bitmap image, string filePath, ImageFormat codec, long quality = 100L, int maxWidth = 0, int maxHeight = 0)
        {
            int originalWidth = image.Width;
            int originalHeight = image.Height;

            int newWidth, newHeight;
            float ratioX, ratioY, ratio;

            if (maxWidth > 0 && maxHeight > 0)
            {
                ratioX = (float)maxWidth / (float)originalWidth;
                ratioY = (float)maxHeight / (float)originalHeight;
                ratio = Math.Min(ratioX, ratioY);

                newWidth = (int)(originalWidth * ratio);
                newHeight = (int)(originalHeight * ratio);
            }
            else
            {
                newWidth = originalWidth;
                newHeight = originalHeight;
            }

            Bitmap newImage = new Bitmap(newWidth, newHeight, System.Drawing.Imaging.PixelFormat.Format24bppRgb);

            using (Graphics graphics = Graphics.FromImage(newImage))
            {
                graphics.CompositingQuality = CompositingQuality.HighQuality;
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.SmoothingMode = SmoothingMode.HighQuality;
                graphics.DrawImage(image, 0, 0, newWidth, newHeight);
            }

            ImageCodecInfo imageCodecInfo = GetEncoderInfo(codec);

            Encoder encoder = Encoder.Quality;

            EncoderParameters encoderParameters = new EncoderParameters(1);

            EncoderParameter encoderParameter = new EncoderParameter(encoder, quality);
            encoderParameters.Param[0] = encoderParameter;
            newImage.Save(filePath, imageCodecInfo, encoderParameters);
        }


        public static BitmapSource GetBitmapSourceFromHBitmap(System.Drawing.Bitmap bitmap, bool hasAlpha = true)
        {
            if (bitmap == null)
                return null;

            BitmapSource bitmapSource;
            IntPtr hBitmap;

            if (hasAlpha)
                hBitmap = bitmap.GetHbitmap(System.Drawing.Color.FromArgb(0, 0, 0, 0));
            else
                hBitmap = bitmap.GetHbitmap();

            BitmapSizeOptions sizeOptions = BitmapSizeOptions.FromEmptyOptions();
            bitmapSource = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(hBitmap, IntPtr.Zero, Int32Rect.Empty, sizeOptions);
            bitmapSource.Freeze();

            DeleteObject(hBitmap);
            GC.Collect();

            return bitmapSource;
        }

        public static BitmapSource GetBitmapSourceFromDrawingImage(System.Drawing.Image image)
        {
            return GetBitmapSourceFromHBitmap(new Bitmap(image));
        }

        public static System.Drawing.Image ScaledDrawingImageByPercent(string fpath, int Percent)
        {
            Bitmap bmPhoto;

            using (var fs = File.OpenRead(fpath))
            using (var image = System.Drawing.Image.FromStream(fs))
            {
                float nPercent = ((float)Percent / 100);

                int sourceWidth = image.Width;
                int sourceHeight = image.Height;
                int sourceX = 0;
                int sourceY = 0;

                int destX = 0;
                int destY = 0;
                int destWidth = (int)(sourceWidth * nPercent);
                int destHeight = (int)(sourceHeight * nPercent);

                bmPhoto = new Bitmap(destWidth, destHeight, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
                bmPhoto.SetResolution(image.HorizontalResolution, image.VerticalResolution);

                using (Graphics grPhoto = Graphics.FromImage(bmPhoto))
                {
                    grPhoto.InterpolationMode = InterpolationMode.HighQualityBicubic;

                    grPhoto.DrawImage(image,
                        new Rectangle(destX, destY, destWidth, destHeight),
                        new Rectangle(sourceX, sourceY, sourceWidth, sourceHeight),
                        GraphicsUnit.Pixel);
                }
            }

            return bmPhoto;
        }

        public static BitmapSource ScaledBitmapSourceByPercent(string fpath, int Percent)
        {
            return GetBitmapSourceFromHBitmap(new Bitmap(ScaledDrawingImageByPercent(fpath, Percent)));
        }

        public static System.Drawing.Image ConstrainProportionsDrawingImage(string fpath, int Size, Dimensions Dimension)
        {
            Bitmap bmPhoto;

            using (var fs = File.OpenRead(fpath))
            using (var image = System.Drawing.Image.FromStream(fs))
            {
                int sourceWidth = image.Width;
                int sourceHeight = image.Height;
                int sourceX = 0;
                int sourceY = 0;
                int destX = 0;
                int destY = 0;
                float nPercent = 0;

                switch (Dimension)
                {
                    case Dimensions.Width:
                        nPercent = ((float)Size / (float)sourceWidth);
                        break;
                    default:
                        nPercent = ((float)Size / (float)sourceHeight);
                        break;
                }

                int destWidth = (int)(sourceWidth * nPercent);
                int destHeight = (int)(sourceHeight * nPercent);

                bmPhoto = new Bitmap(destWidth, destHeight, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
                bmPhoto.SetResolution(image.HorizontalResolution, image.VerticalResolution);

                using (Graphics grPhoto = Graphics.FromImage(bmPhoto))
                {
                    grPhoto.InterpolationMode = InterpolationMode.HighQualityBicubic;

                    grPhoto.DrawImage(image,
                    new Rectangle(destX, destY, destWidth, destHeight),
                    new Rectangle(sourceX, sourceY, sourceWidth, sourceHeight),
                    GraphicsUnit.Pixel);
                }
            }

            return bmPhoto;
        }

        public static BitmapSource ConstrainProportionsBitmapSource(string fpath, int Size, Dimensions Dimension)
        {
            return GetBitmapSourceFromHBitmap(new Bitmap(ConstrainProportionsDrawingImage(fpath, Size, Dimension)));
        }

        public static System.Drawing.Image FixedSizeDrawingImage(System.Drawing.Image image, int Width, int Height)
        {
            int sourceWidth = image.Width;
            int sourceHeight = image.Height;
            int sourceX = 0;
            int sourceY = 0;
            int destX = 0;
            int destY = 0;

            float nPercent = 0;
            float nPercentW = 0;
            float nPercentH = 0;

            nPercentW = ((float)Width / (float)sourceWidth);
            nPercentH = ((float)Height / (float)sourceHeight);

            if (nPercentH < nPercentW)
            {
                nPercent = nPercentH;
                destX = (int)((Width - (sourceWidth * nPercent)) / 2);
            }
            else
            {
                nPercent = nPercentW;
                destY = (int)((Height - (sourceHeight * nPercent)) / 2);
            }

            int destWidth = (int)(sourceWidth * nPercent);
            int destHeight = (int)(sourceHeight * nPercent);

            Bitmap bmPhoto = new Bitmap(Width, Height, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
            bmPhoto.SetResolution(image.HorizontalResolution, image.VerticalResolution);

            using (Graphics grPhoto = Graphics.FromImage(bmPhoto))
            {
                grPhoto.Clear(System.Drawing.Color.Black);
                grPhoto.InterpolationMode = InterpolationMode.HighQualityBicubic;

                grPhoto.DrawImage(image,
                    new Rectangle(destX, destY, destWidth, destHeight),
                    new Rectangle(sourceX, sourceY, sourceWidth, sourceHeight),
                    GraphicsUnit.Pixel);
            }

            return bmPhoto;
        }

        public static System.Drawing.Image FixedSizeDrawingImage(string fpath, int Width, int Height)
        {
            System.Drawing.Image _img;

            using (var fs = File.OpenRead(fpath))
            using (var image = System.Drawing.Image.FromStream(fs))
            {
                _img = FixedSizeDrawingImage(image, Width, Height);
            }

            return _img;
        }

        public static BitmapSource FixedSizeBitmapSource(string fpath, int Width, int Height)
        {
            return GetBitmapSourceFromHBitmap(new Bitmap(FixedSizeDrawingImage(fpath, Width, Height)));
        }

        public static BitmapSource FixedSizeBitmapSourceByURL(string url, int Width, int Height)
        {
            return GetBitmapSourceFromHBitmap(new Bitmap(FixedSizeDrawingImage(GetDrawingImageByURL(url), Width, Height)));
        }

        public static System.Drawing.Image GetDrawingImageByURL(string url)
        {
            if (IsValidUrlContent(url) == false)
                return null;

            System.Drawing.Image image = null;

            using (WebClient wc = new WebClient())
            {
                byte[] bytes = wc.DownloadData(url);
                using (MemoryStream ms = new MemoryStream(bytes))
                    image = System.Drawing.Image.FromStream(ms);
            }

            return image;
        }

        public static bool IsValidUrlContent(string url)
        {
            bool try1 = false, try2 = false;
            Uri uriResult;

            try1 = Uri.TryCreate(url, UriKind.Absolute, out uriResult);

            if (uriResult == null)
                return false;

            try2 = (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps);

            return try1 && try2;
        }

        public static System.Drawing.Image CropDrawingImage(string fpath, int Width, int Height, AnchorPosition Anchor)
        {
            Bitmap bmPhoto;

            using (var fs = File.OpenRead(fpath))
            using (var image = System.Drawing.Image.FromStream(fs))
            {
                int sourceWidth = image.Width;
                int sourceHeight = image.Height;
                int sourceX = 0;
                int sourceY = 0;
                int destX = 0;
                int destY = 0;

                float nPercent = 0;
                float nPercentW = 0;
                float nPercentH = 0;

                nPercentW = ((float)Width / (float)sourceWidth);
                nPercentH = ((float)Height / (float)sourceHeight);

                if (nPercentH < nPercentW)
                {
                    nPercent = nPercentW;
                    switch (Anchor)
                    {
                        case AnchorPosition.Top:
                            destY = 0;
                            break;
                        case AnchorPosition.Bottom:
                            destY = (int)(Height - (sourceHeight * nPercent));
                            break;
                        default:
                            destY = (int)((Height - (sourceHeight * nPercent)) / 2);
                            break;
                    }
                }
                else
                {
                    nPercent = nPercentH;
                    switch (Anchor)
                    {
                        case AnchorPosition.Left:
                            destX = 0;
                            break;
                        case AnchorPosition.Right:
                            destX = (int)(Width - (sourceWidth * nPercent));
                            break;
                        default:
                            destX = (int)((Width - (sourceWidth * nPercent)) / 2);
                            break;
                    }
                }

                int destWidth = (int)(sourceWidth * nPercent);
                int destHeight = (int)(sourceHeight * nPercent);

                bmPhoto = new Bitmap(Width, Height, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
                bmPhoto.SetResolution(image.HorizontalResolution, image.VerticalResolution);

                using (Graphics grPhoto = Graphics.FromImage(bmPhoto))
                {
                    grPhoto.InterpolationMode = InterpolationMode.HighQualityBicubic;

                    grPhoto.DrawImage(image,
                        new Rectangle(destX, destY, destWidth, destHeight),
                        new Rectangle(sourceX, sourceY, sourceWidth, sourceHeight),
                        GraphicsUnit.Pixel);
                }
            }

            return bmPhoto;
        }

        public static BitmapSource CropBitmapSource(FrameworkElement fe, int width, int height, int x = 0, int y = 0)
        {
            try
            {
                if (fe == null)
                    return null;

                return new CroppedBitmap(GetBitmapSource(fe), new Int32Rect(x, y, width, height));

            }
            catch (Exception e) { }

            return null;
        }

        public static BitmapSource CropBitmapSource(BitmapSource src, int width, int height, int x = 0, int y = 0)
        {
            try
            {
                if (src == null)
                    return null;

                return new CroppedBitmap(src, new Int32Rect(x, y, width, height));

            }
            catch (Exception e) { }

            return null;
        }

        public static BitmapSource CropBitmapSource(string fpath, int width, int height, int x = 0, int y = 0)
        {
            BitmapImage src = new BitmapImage();
            src.BeginInit();
            src.UriSource = new Uri(fpath, UriKind.Relative);
            src.CacheOption = BitmapCacheOption.OnLoad;
            src.EndInit();

            return new CroppedBitmap(src, new Int32Rect(x, y, width, height));
        }

        public static BitmapSource CropBitmapSource(string fpath, int Width, int Height, AnchorPosition Anchor)
        {
            return GetBitmapSourceFromHBitmap(new Bitmap(CropDrawingImage(fpath, Width, Height, Anchor)));
        }

        public static bool IsValidImage(string fpath)
        {
            try
            {
                using (FileStream fs = new FileStream(fpath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    using (StreamReader sr = new StreamReader(fs))
                    {
                        return IsValidImage(fs);
                    }
                }
            }
            catch (Exception ex)
            {
                if (ex is IOException)
                    throw;
                return false;
            }
        }
        public static bool IsValidImage(Stream stream)
        {
            try
            {
                try
                {
                    stream.Position = 0;
                    stream.Seek(0, SeekOrigin.Begin);
                }
                catch { }
                using (System.Drawing.Image img = System.Drawing.Image.FromStream(stream))
                {
                    img.Dispose();
                    return true;
                }
            }
            catch
            {
                return false;
            }
        }

        #endregion


        #region ScreenCapture
        private static BitmapSource CopyScreen()
        {
            using (var screenBmp = new Bitmap(
                (int)SystemParameters.PrimaryScreenWidth,
                (int)SystemParameters.PrimaryScreenHeight,
                System.Drawing.Imaging.PixelFormat.Format32bppArgb))
            {
                using (var bmpGraphics = Graphics.FromImage(screenBmp))
                {
                    bmpGraphics.CopyFromScreen(0, 0, 0, 0, screenBmp.Size);
                    return Imaging.CreateBitmapSourceFromHBitmap(
                        screenBmp.GetHbitmap(),
                        IntPtr.Zero,
                        Int32Rect.Empty,
                        BitmapSizeOptions.FromEmptyOptions());
                }
            }
        }

        public void SaveScreenShotToFile(Window parentWnd, string targetPath, int marginTop, int marginLeft, int width, int height, int level)
        {
            FileStream stream = new FileStream(targetPath, FileMode.Create);
            try
            {
                JpegBitmapEncoder encoder = new JpegBitmapEncoder();
                encoder.FlipVertical = false;
                encoder.QualityLevel = level;

                BitmapSource tempSource = CopyScreen();

                int topVal = (int)parentWnd.Top + marginTop;
                int leftVal = (int)(parentWnd.Left + parentWnd.ActualWidth) - marginLeft;
                CroppedBitmap cb = new CroppedBitmap(tempSource, new Int32Rect(leftVal, topVal, width, height));

                encoder.Frames.Add(BitmapFrame.Create(cb));
                encoder.Save(stream);

                stream.Close();
            }
            catch (Exception ex)
            {
                if (stream != null)
                {
                    stream.Close();
                }

            }
        }

        public static RenderTargetBitmap ConvertExactBitmapImage(FrameworkElement element)
        {
            DrawingVisual drawingVisual = new DrawingVisual();
            DrawingContext drawingContext = drawingVisual.RenderOpen();

            drawingContext.DrawRectangle(
                new VisualBrush(element), null,
                new Rect(new System.Windows.Point(0, 0), new System.Windows.Point(element.Width, element.Height)));

            drawingContext.Close();

            RenderTargetBitmap target = new RenderTargetBitmap((int)element.Width, (int)element.Height,
                                                                                    96, 96, System.Windows.Media.PixelFormats.Pbgra32);

            target.Render(drawingVisual);

            return target;
        }

        public static RenderTargetBitmap ConvertBitmapImage(FrameworkElement element)
        {
            DrawingVisual drawingVisual = new DrawingVisual();
            DrawingContext drawingContext = drawingVisual.RenderOpen();

            drawingContext.DrawRectangle(
                new VisualBrush(element), null,
                new Rect(new System.Windows.Point(0, 0), new System.Windows.Point(element.ActualWidth, element.ActualHeight)));

            drawingContext.Close();

            RenderTargetBitmap target = new RenderTargetBitmap((int)element.ActualWidth, (int)element.ActualHeight,
                                                                                    96, 96, System.Windows.Media.PixelFormats.Pbgra32);

            target.Render(drawingVisual);

            return target;
        }

        public static bool ConvertAndSavePngImage(FrameworkElement element, string fpath, bool actual = true)
        {
            if (actual)
                return SaveToPngFile(ConvertBitmapImage(element), fpath);
            else
                return SaveToPngFile(ConvertExactBitmapImage(element), fpath);
        }

        public static bool ConvertAndSaveBmpImage(FrameworkElement element, string fpath, bool actual = true)
        {
            if (actual)
                return SaveToBmpFile(ConvertBitmapImage(element), fpath);
            else
                return SaveToBmpFile(ConvertExactBitmapImage(element), fpath);
        }

        public static bool ConvertAndSaveJpgImage(FrameworkElement element, string fpath, bool actual = true)
        {
            if (actual)
                return SaveToJpgFile(ConvertBitmapImage(element), fpath);
            else
                return SaveToJpgFile(ConvertExactBitmapImage(element), fpath);
        }

        public static bool SaveToPngFile(RenderTargetBitmap bmp, string fpath)
        {
            try
            {
                var enc = new PngBitmapEncoder();
                enc.Frames.Add(BitmapFrame.Create(bmp));

                using (var fp = System.IO.File.Create(fpath))
                {
                    enc.Save(fp);
                }
            }
            catch (Exception exc) { return false; }

            return true;
        }

        public static bool SaveToBmpFile(RenderTargetBitmap bmp, string fpath)
        {
            try
            {
                var enc = new BmpBitmapEncoder();
                enc.Frames.Add(BitmapFrame.Create(bmp));

                using (var fp = System.IO.File.Create(fpath))
                {
                    enc.Save(fp);
                }
            }
            catch (Exception exc) { return false; }

            return true;
        }

        /// <summary>
        /// PixelFormat이 Gray8 (8bit 그레이 스케일)의 BitmapSource를 흑백 값으로 변환하여
        /// 1bit 비트 맵 이미지 파일로 저장
        /// </ summary>
        /// <param name = "source"> PixelFormat이 Gray8의 BitmapSource </ param>
        /// <param name = "threshold"> 임계 값 </ param>
        public static void SaveToBlackWhiteBmpFile(Bitmap bitmap, string fpath, byte threshold = 128)
        {
            BitmapSource source = GetGrayBitmapSource(bitmap);

            int w = source.PixelWidth;
            int h = source.PixelHeight;
            int stride = w; // 1 픽셀 행의 byte 수를 지정 Gray8는 1 픽셀 8bit이므로 w * 8 / 8 = w
            byte[] pixels = new byte[h * stride];
            source.CopyPixels(pixels, stride, 0);
            // 임계 값에서 흑백 낸다
            for (int i = 0; i < pixels.Length; ++i)
            {
                if (pixels[i] < threshold)
                {
                    pixels[i] = 0;
                }
                else
                {
                    pixels[i] = 255;
                }
            }

            // BitmapSource 만들고 여기에서 PixelFormat을 BlackWhite하면 이미지가 무너 지므로 그대로 작성
            BitmapSource newBitmap = BitmapSource.Create(w, h, source.DpiX, source.DpiY, source.Format, null, pixels, stride);

            // PixelFormat을 BlackWhite로 변환			
            FormatConvertedBitmap convertedBitmap = new FormatConvertedBitmap(newBitmap, PixelFormats.BlackWhite, null, 0);

            // 파일에 저장
            // 1 비트 bmp 파일 작성
            using (var fs = new FileStream(fpath, FileMode.Create, FileAccess.Write))
            {
                var encoder = new BmpBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(convertedBitmap));
                encoder.Save(fs);
            }
        }

        public static void SaveToBlackWhiteBmpFile(FrameworkElement fe, string fpath, byte threshold = 128)
        {
            BitmapSource source = GetGrayBitmapSource(fe);

            int w = source.PixelWidth;
            int h = source.PixelHeight;
            int stride = w; // 1 픽셀 행의 byte 수를 지정 Gray8는 1 픽셀 8bit이므로 w * 8 / 8 = w
            byte[] pixels = new byte[h * stride];
            source.CopyPixels(pixels, stride, 0);
            // 임계 값에서 흑백 낸다
            for (int i = 0; i < pixels.Length; ++i)
            {
                if (pixels[i] < threshold)
                {
                    pixels[i] = 0;
                }
                else
                {
                    pixels[i] = 255;
                }
            }

            // BitmapSource 만들고 여기에서 PixelFormat을 BlackWhite하면 이미지가 무너 지므로 그대로 작성
            BitmapSource newBitmap = BitmapSource.Create(w, h, source.DpiX, source.DpiY, source.Format, null, pixels, stride);

            // PixelFormat을 BlackWhite로 변환			
            FormatConvertedBitmap convertedBitmap = new FormatConvertedBitmap(newBitmap, PixelFormats.BlackWhite, null, 0);

            // 파일에 저장
            // 1 비트 bmp 파일 작성
            using (var fs = new FileStream(fpath, FileMode.Create, FileAccess.Write))
            {
                var encoder = new BmpBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(convertedBitmap));
                encoder.Save(fs);
            }
        }

        public static BitmapSource GetGrayBitmapSource(Bitmap bitmap)
        {
            BitmapSource source = GetBitmapSourceFromHBitmap(bitmap);

            System.Windows.Media.PixelFormat pf = PixelFormats.Gray8;
            var convertedBitmap = new FormatConvertedBitmap(source, PixelFormats.Gray8, null, 0);
            int w = convertedBitmap.PixelWidth;
            int h = convertedBitmap.PixelHeight;
            int stride = (w * pf.BitsPerPixel + 7) / 8;
            byte[] pixels = new byte[h * stride];
            convertedBitmap.CopyPixels(pixels, stride, 0);
            source = BitmapSource.Create(
                w, h, source.DpiX, source.DpiY,
                convertedBitmap.Format,
                convertedBitmap.Palette, pixels, stride);
            return source;
        }

        public static BitmapSource GetGrayBitmapSource(FrameworkElement fe)
        {
            BitmapSource source = ConvertBitmapImage(fe);

            System.Windows.Media.PixelFormat pf = PixelFormats.Gray8;
            var convertedBitmap = new FormatConvertedBitmap(source, PixelFormats.Gray8, null, 0);
            int w = convertedBitmap.PixelWidth;
            int h = convertedBitmap.PixelHeight;
            int stride = (w * pf.BitsPerPixel + 7) / 8;
            byte[] pixels = new byte[h * stride];
            convertedBitmap.CopyPixels(pixels, stride, 0);
            source = BitmapSource.Create(
                w, h, source.DpiX, source.DpiY,
                convertedBitmap.Format,
                convertedBitmap.Palette, pixels, stride);
            return source;
        }

        public static BitmapSource GetBitmapSource(FrameworkElement fe)
        {
            var target = new RenderTargetBitmap((int)(fe.RenderSize.Width), (int)(fe.RenderSize.Height), 96, 96, PixelFormats.Pbgra32);
            var brush = new VisualBrush(fe);

            var visual = new DrawingVisual();
            var drawingContext = visual.RenderOpen();


            drawingContext.DrawRectangle(brush, null, new Rect(new System.Windows.Point(0, 0),
                new System.Windows.Point(fe.RenderSize.Width, fe.RenderSize.Height)));

            drawingContext.PushOpacityMask(brush);

            drawingContext.Close();

            target.Render(visual);

            return target;
        }

        public static bool SaveToJpgFile(RenderTargetBitmap bmp, string fpath)
        {
            try
            {
                var enc = new JpegBitmapEncoder();
                enc.Frames.Add(BitmapFrame.Create(bmp));

                using (var fp = System.IO.File.Create(fpath))
                {
                    enc.Save(fp);
                }
            }
            catch (Exception exc) { return false; }

            return true;
        }

        public static void ConvertAndSaveImage(FrameworkElement fe, string fpath, ImageFormat codec, int width = 0, int height = 0, long quality = 100L)
        {
            SaveImage(RenderTargetBitmap2Bitmap(ConvertBitmapImage(fe)), fpath, codec, quality, width, height);
        }

        public static Bitmap RenderTargetBitmap2Bitmap(RenderTargetBitmap rtb)
        {
            Bitmap bitmap = null;

            try
            {
                using (MemoryStream stream = new MemoryStream())
                {
                    BitmapEncoder encoder = new BmpBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(rtb));
                    encoder.Save(stream);

                    bitmap = new Bitmap(stream);
                }
            }
            catch (Exception e) { }

            return bitmap;
        }

        public static void ReduceImageSize(string sourcePath, string targetPath, ImageFormat codec, long quality = 100L)
        {
            try
            {
                using (var fs = File.OpenRead(sourcePath))
                {
                    using (var image = System.Drawing.Image.FromStream(fs))
                    {
                        using (var resized = ResizeBitmap(image))
                        {
                            if (File.Exists(targetPath))
                                File.Delete(targetPath);

                            Encoder _enc = Encoder.Quality;
                            EncoderParameters _params = new EncoderParameters(1);
                            EncoderParameter _param = new EncoderParameter(_enc, quality);
                            _params.Param[0] = _param;

                            resized.Save(targetPath, GetEncoder(codec), _params);
                        }
                    }
                }
            }
            catch (Exception e)
            {
            }
        }
        #endregion

        public static BitmapImage CreateBitmapFromBytes(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0)
                return null;

            try
            {
                using (var imageStream = new MemoryStream(bytes))
                {
                    BitmapImage bmpImg = new BitmapImage();
                    bmpImg.BeginInit();
                    bmpImg.CacheOption = BitmapCacheOption.OnLoad;
                    bmpImg.StreamSource = imageStream;
                    bmpImg.EndInit();
                    bmpImg.Freeze();
                    return bmpImg;
                }
            }
            catch { }

            return null;
        }

        public static BitmapImage CreateBitmapFromBase64(string base64)
        {
            if (string.IsNullOrWhiteSpace(base64))
                return null;

            byte[] _bt = Convert.FromBase64String(base64);
            return CreateBitmapFromBytes(_bt);
        }

    }
}
