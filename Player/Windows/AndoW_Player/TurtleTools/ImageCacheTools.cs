using AndoW.LiteDb;
using LiteDB;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;

namespace TurtleTools
{
    public enum ImageOrientation
    {
        Landscape = 0,
        Portrait = 1
    }

    public class ImageCacheTools
    {
        public static string CheckForResizing(ImageList list, string folder, string sourcepath, string targetpath, double longside, long maxbyte, Int64 quality)
        {
            ImageFolderQuickCheck.QuickCheck(list, folder);

            ImageFileAttributes ifa = list.NeedToResizeBySizeByte(sourcepath, maxbyte);
               
            if(ifa == null) return sourcepath;

            try
            {
                FileInfo fi = new FileInfo(targetpath);
                if (fi.Exists)
                {
                    if (fi.LastWriteTime.ToFileTime() > ifa.FileTime)
                        return targetpath;
                }

                MediaTools.ResizeImageFile(sourcepath, targetpath, ImageFormat.Jpeg, longside, quality, MediaTools.GetImageRotateAngleFromExif(sourcepath));
            }
            catch (Exception e)
            {
                targetpath = sourcepath;
            }
            return targetpath;
        }

        public static void CachingImages(ImageList list, string folder, string optfolder, double longside, long maxbyte, Int64 quality)
        {
            ImageFolderQuickCheck.QuickCheck(list, folder);

            List<ImageFileAttributes> ifalist = list.NeedToResizeBySizeByteList(maxbyte);

            foreach (ImageFileAttributes ifa in ifalist)
            {
                if (ifa == null) continue;

                string filename = Path.GetFileName(ifa.Path);
                string targetpath = Path.Combine(optfolder, filename);
                try
                {
                    FileInfo fi = new FileInfo(targetpath);
                    if (fi.Exists)
                    {
                        if (fi.LastWriteTime.ToFileTime() > ifa.FileTime)
                            continue;
                    }

                    MediaTools.ResizeImageFile(ifa.Path, targetpath, ImageFormat.Jpeg, longside, quality, MediaTools.GetImageRotateAngleFromExif(ifa.Path));
                }
                catch (Exception e) { continue; }
            }
        }
    }

    public class ImageFolderQuickCheck
    {
        public static void QuickCheck(ImageList images, string imagesFolder)
        {
            Directory.CreateDirectory(imagesFolder);

            List<string> sourceImageList = new List<string>(Directory.GetFiles(imagesFolder, "*.*", SearchOption.TopDirectoryOnly).Where(s => s.EndsWith(".png") || s.EndsWith(".jpg") || s.EndsWith(".jpeg") || s.EndsWith(".bmp") || s.EndsWith(".gif")));
            List<int> removeIndex = new List<int>();

            bool hasChanged = false;

            sourceImageList.Sort(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < images.Count; i++)
            {
                ImageFileAttributes imageAttributes = images[i];

                int sourceIndex = sourceImageList.BinarySearch(imageAttributes.Path, StringComparer.OrdinalIgnoreCase);

                if (sourceIndex < 0)
                {
                    //image is not longer in main folder
                    removeIndex.Add(i);
                }
                else
                {
                    if (images.Where(f => f.Path.Equals(imageAttributes.Path, StringComparison.CurrentCultureIgnoreCase)
                                        && (f.IsModified(new FileInfo(imageAttributes.Path).LastWriteTime.ToFileTime()))).FirstOrDefault() == null)
                    {
                        //item exists, remove it from the initial list
                        sourceImageList.RemoveAt(sourceIndex);
                    }
                    else
                    {
                        removeIndex.Add(i);
                    }
                }
            }

            //we must remove from the end of the images list backwards otherwise we screw the index positions
            removeIndex.Reverse();

            for (int i = 0; i < removeIndex.Count; i++)
            {
                ImageFileAttributes imageAttributes = images[removeIndex[i]];

                // Remove image from cache
                hasChanged = true;
                images.RemoveAt(removeIndex[i]);
            }

            //now add new images into the cache
            for (int i = 0; i < sourceImageList.Count; i++)
            {
                string imagePath = sourceImageList[i];

                // Add image to cache
                hasChanged = true;

                Size originalSize = ImageHeader.GetDimensions(imagePath);
                try
                {
                    FileInfo fi = new FileInfo(imagePath);
                    images.Add(new ImageFileAttributes(imagePath, originalSize, fi.Length, fi.LastWriteTime.ToFileTime()));
                }
                catch (Exception e) { continue; }
            }

            if (hasChanged)
            {
                images.Save();
            }
        }
    }

    public class ImageList : List<ImageFileAttributes>
    {
        private readonly string legacyFilePath;
        private readonly object syncRoot = new object();
        private readonly ImageCacheRepository repository = new ImageCacheRepository();

        public ImageList(string legacyFilePath)
            : base()
        {
            this.legacyFilePath = legacyFilePath;
            LoadData();
        }

        public void LoadData()
        {
            lock (syncRoot)
            {
                var documents = repository.LoadAll();

                Clear();
                AddRange(documents.Select(ToAttributes).Where(a => a != null));
            }
        }

        public ImageFileAttributes NeedToResizeBySize(string imagepath, double maxwidth, double maxheight)
        {
            ImageFileAttributes ifa = this.Where(i => i.Path.Equals(imagepath, StringComparison.CurrentCultureIgnoreCase)
                                        && (i.Size.Width > maxwidth && i.Size.Height > maxheight)).FirstOrDefault();
            return ifa;
        }

        public ImageFileAttributes NeedToResizeBySizeByte(string imagepath, long maxByte)
        {
            ImageFileAttributes ifa = this.Where(i => i.Path.Equals(imagepath, StringComparison.CurrentCultureIgnoreCase)
                                        && (i.FileSize > maxByte)).FirstOrDefault();
            return ifa;
        }

        public List<ImageFileAttributes> NeedToResizeBySizeByteList(long maxByte)
        {
            return this.Where(i => i.FileSize > maxByte).ToList();
        }
        
        public void Save()
        {
            lock (syncRoot)
            {
                repository.ReplaceAll(this.Select(ToDocument).Where(d => d != null));
            }
        }

        public List<ImageFileAttributes> Clone()
        {
            lock (syncRoot)
            {
                return new List<ImageFileAttributes>(this);
            }
        }

        private static ImageFileAttributes ToAttributes(ImageCacheDocument document)
        {
            if (document == null || string.IsNullOrWhiteSpace(document.Path))
            {
                return null;
            }

            return new ImageFileAttributes(
                document.Path,
                new Size(document.Width, document.Height),
                document.FileSize,
                document.FileTime);
        }

        private static ImageCacheDocument ToDocument(ImageFileAttributes attributes)
        {
            if (attributes == null || string.IsNullOrWhiteSpace(attributes.Path))
            {
                return null;
            }

            return new ImageCacheDocument
            {
                Path = attributes.Path,
                Width = attributes.Size.Width,
                Height = attributes.Size.Height,
                FileSize = attributes.FileSize,
                FileTime = attributes.FileTime
            };
        }
    }

    public class ImageHeader
    {
        const string errorMessage = "Could not recognise image format.";

        private static Dictionary<byte[], Func<BinaryReader, Size>> imageFormatDecoders = new Dictionary<byte[], Func<BinaryReader, Size>>()
        { 
            { new byte[] { 0x42, 0x4D }, DecodeBitmap }, 
            { new byte[] { 0x47, 0x49, 0x46, 0x38, 0x37, 0x61 }, DecodeGif }, 
            { new byte[] { 0x47, 0x49, 0x46, 0x38, 0x39, 0x61 }, DecodeGif }, 
            { new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }, DecodePng },
            { new byte[] { 0xff, 0xd8 }, DecodeJfif }, 
        };

        /// Gets the dimensions of an image.        
        public static Size GetDimensions(string path)
        {
            try
            {
                using (BinaryReader binaryReader = new BinaryReader(File.OpenRead(path)))
                {
                    try
                    {
                        return GetDimensions(binaryReader);
                    }
                    catch (ArgumentException e)
                    {
                        string newMessage = string.Format("{0} file: '{1}' ", errorMessage, path);
                        throw new ArgumentException(newMessage, "path", e);
                    }
                }
            }
            catch (ArgumentException)
            {
                //do it the old fashioned way
                using (Bitmap b = new Bitmap(path))
                {
                    return b.Size;
                }
            }
        }
   
        /// Gets the dimensions of an image.               
        public static Size GetDimensions(BinaryReader binaryReader)
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

            throw new ArgumentException(errorMessage, "binaryReader");
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

        private static Size DecodeBitmap(BinaryReader binaryReader)
        {
            binaryReader.ReadBytes(16);
            int width = binaryReader.ReadInt32();
            int height = binaryReader.ReadInt32();
            return new Size(width, height);
        }

        private static Size DecodeGif(BinaryReader binaryReader)
        {
            int width = binaryReader.ReadInt16();
            int height = binaryReader.ReadInt16();
            return new Size(width, height);
        }

        private static Size DecodePng(BinaryReader binaryReader)
        {
            binaryReader.ReadBytes(8);
            int width = ReadLittleEndianInt32(binaryReader);
            int height = ReadLittleEndianInt32(binaryReader);
            return new Size(width, height);
        }

        private static Size DecodeJfif(BinaryReader binaryReader)
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
                    return new Size(width, height);
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
    }

    public class ImageFileAttributes
    {
        public ImageFileAttributes(string path, Size size, long filesize, long filetime = 0)
        {
            this.Path = path;
            this.Size = size;
            this.FileSize = filesize;
            if (filetime > 0)
            {
                this.FileTime = filetime;
            }
        }

        public string Path { get; private set; }
        public Size Size { get; private set; }
        public long FileSize { get; private set; }
        public long FileTime { get; private set; }

        public string DirectoryName
        {
            get { return System.IO.Path.GetDirectoryName(this.Path); }
        }

        public ImageOrientation Orientation
        {
            get { return this.Size.Height > this.Size.Width ? ImageOrientation.Portrait : ImageOrientation.Landscape; }
        }

        public bool IsModified(long filetime)
        {
            return this.FileTime <= 0 || this.FileTime != filetime;
        }
    }


    class ImageCacheDocument
    {
        [BsonId]
        public string Path { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public long FileSize { get; set; }
        public long FileTime { get; set; }
    }

    class ImageCacheRepository : LiteDbRepository<ImageCacheDocument>
    {
        public ImageCacheRepository() : base("ImageCacheTools", "Path") { }
    }
}
