using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.ComponentModel;
using TurtleTools;

namespace AndoW_Manager
{
    /// <summary>
    /// Interaction logic for UserControl1.xaml
    /// </summary>
    public partial class FlatButton1 : UserControl
    {

        bool g_IsSelected = false;
        public bool Selected
        {
            get { return g_IsSelected; }
        }

        public FlatButton1()
        {
            InitializeComponent();
            InitEventHandler();
            SelectedBorder.Visibility = System.Windows.Visibility.Hidden;
        }

        public void InitEventHandler()
        {
            this.PreviewMouseMove += new MouseEventHandler(PageListElement_PreviewMouseMove);
            this.MouseLeave += new MouseEventHandler(PageListElement_MouseLeave);
        }

        public void ShowAndHideSelectedBorder(bool IsShow)
        {
            if (IsShow == true)
            {
                this.SelectedBorder.Visibility = System.Windows.Visibility.Visible;
                this.BtnDisplayNameTextBlk.Foreground = ColorTools.GetSolidBrushByColorString("#FFFFFFFF");
                BtnDisplayNameTextBlk_Copy.Foreground = ColorTools.GetSolidBrushByColorString("#FFFFFFFF");
                // this.DspRect.Fill = ColorTools.GetSolidBrushByColorString("#FF005F74");
                this.DspRect.Visibility = System.Windows.Visibility.Visible;
                PageIcon01.Fill = ColorTools.GetSolidBrushByColorString("#FF00FF84");
                PageIcon03.Fill = ColorTools.GetSolidBrushByColorString("#FF00FF84");
                PageIcon04.Fill = ColorTools.GetSolidBrushByColorString("#FF00FF84");
            }
            else
            {
                this.SelectedBorder.Visibility = System.Windows.Visibility.Hidden;
                this.BtnDisplayNameTextBlk.Foreground = ColorTools.GetSolidBrushByColorString("#66FFFFFF");
                BtnDisplayNameTextBlk_Copy.Foreground = ColorTools.GetSolidBrushByColorString("#66FFFFFF");
                // this.DspRect.Fill = ColorTools.GetSolidBrushByColorString("#FF666666");
                this.DspRect.Visibility = System.Windows.Visibility.Hidden;
                PageIcon01.Fill = ColorTools.GetSolidBrushByColorString("#66FFFFFF");
                PageIcon03.Fill = ColorTools.GetSolidBrushByColorString("#66FFFFFF");
                PageIcon04.Fill = ColorTools.GetSolidBrushByColorString("#66FFFFFF");
            }

            g_IsSelected = IsShow;
        }

        public void ShowIcon01()
        {
            PageIcon01.Visibility = System.Windows.Visibility.Visible;
            PageIcon03.Visibility = System.Windows.Visibility.Hidden;
        }

        public void ShowIcon03()
        {
            PageIcon01.Visibility = System.Windows.Visibility.Hidden;
            PageIcon03.Visibility = System.Windows.Visibility.Visible;
        }

        public void ShowIcon04()
        {
            PageIcon01.Visibility = System.Windows.Visibility.Hidden;
            PageIcon03.Visibility = System.Windows.Visibility.Hidden;
            PageIcon04.Visibility = System.Windows.Visibility.Visible;
        }

        public void DisBTNName(string paramStr1, string paramStr2)
        {
            BtnDisplayNameTextBlk.Text = paramStr1;
            BtnDisplayNameTextBlk_Copy.Text = paramStr2;
        }

        void PageListElement_MouseLeave(object sender, MouseEventArgs e)
        {
            if (g_IsSelected == false)
            {
                this.BtnDisplayNameTextBlk.Foreground = ColorTools.GetSolidBrushByColorString("#66FFFFFF");
                BtnDisplayNameTextBlk_Copy.Foreground = ColorTools.GetSolidBrushByColorString("#66FFFFFF");
            }
        }

        void PageListElement_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (g_IsSelected == false)
            {
                this.BtnDisplayNameTextBlk.Foreground = ColorTools.GetSolidBrushByColorString("#66FFFFFF");
                BtnDisplayNameTextBlk_Copy.Foreground = ColorTools.GetSolidBrushByColorString("#66FFFFFF");
            }
        }

    }
}
