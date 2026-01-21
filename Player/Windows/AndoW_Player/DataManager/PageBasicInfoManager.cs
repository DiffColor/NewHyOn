using AndoW.Shared;

namespace HyOnPlayer
{
    public class PageBasicInfoManager
    {
        public PageBasicInfoClass g_DataClass = new PageBasicInfoClass();

        public void LoadFromPage(PageInfoClass page)
        {
            if (page == null)
            {
                g_DataClass = new PageBasicInfoClass();
                return;
            }

            g_DataClass.PBI_PageDirection = page.PIC_IsLandscape ? DeviceOrientation.Landscape.ToString() : DeviceOrientation.Portrait.ToString();
            g_DataClass.PBI_PlaytimeMinute = page.PIC_PlaytimeMinute;
            g_DataClass.PBI_PlaytimeSecond = page.PIC_PlaytimeSecond;
            g_DataClass.PBI_Rows = page.PIC_Rows;
            g_DataClass.PBI_Columns = page.PIC_Columns;
            g_DataClass.PBI_CanvasWidth = page.PIC_CanvasWidth;
            g_DataClass.PBI_CanvasHeight = page.PIC_CanvasHeight;
            g_DataClass.PBI_NeedGuide = page.PIC_NeedGuide;
        }
    }

    public class PageBasicInfoClass
    {
        public string PBI_PageDirection = DeviceOrientation.Landscape.ToString();
        public int PBI_PlaytimeMinute = 0;
        public int PBI_PlaytimeSecond = 10;
        public int PBI_Rows = 1;
        public int PBI_Columns = 1;
        public double PBI_CanvasWidth = 1920;
        public double PBI_CanvasHeight = 1080;
        public bool PBI_NeedGuide = true;
    }
}
