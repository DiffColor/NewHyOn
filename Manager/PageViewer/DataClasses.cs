using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PageViewer
{
    public enum DisplayType { None, Media, HDTV, IPTV, ScrollText, WelcomeBoard }
    public enum ContentType { None, Video, Image, Browser, Flash, PPT, HDTV, IPTV, WebSiteURL, PDF }
    public enum DeviceOrientation { Landscape, Portrait }

    public class PreviewCanvas
    {
        public string Direction { get; set; }
        public int Rows { get; set; }
        public int Columns { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public bool FillContent { get; set; }

        public PreviewCanvas()
        {
            Direction = DeviceOrientation.Landscape.ToString();
            Rows = Columns = 1;
            Width = 1280;
            Height = 720;
            FillContent = false;
        }
    }

    public class PreviewElement
    {
        public string ElementType { get; set; }
        public int PosX { get; set; }
        public int PosY { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public int Index { get; set; }

        public List<PreviewData> DataList { get; set; }

        public PreviewElement()
        {
            ElementType = DisplayType.None.ToString();
            PosX = PosY = Width = Height = Index = 0;
            DataList = new List<PreviewData>();
        }
    }

    public class PreviewData
    {
        public string DataType { get; set; }
        public string FilePath { get; set; }
        public int Playtime { get; set; }
        public string TextContent { get; set; }
        public string FontName { get; set; }
        public string FontColor { get; set; }
        public double FontSize { get; set; }
        public bool IsBold { get; set; }
        public bool IsItalic { get; set; }
        public string BGColor { get; set; }

        public PreviewData()
        {
            DataType = ContentType.None.ToString();
            FilePath = string.Empty;
            Playtime = 0;
        }
    }
}
