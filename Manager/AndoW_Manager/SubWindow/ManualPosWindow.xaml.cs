using System;
using System.Windows;
using System.Windows.Controls;
using TurtleTools;

namespace AndoW_Manager
{
    /// <summary>
    /// ManualPosWindow.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class ManualPosWindow : Window
    {
        public DisplayElementForEditor g_Parent1 = null;
        public WelcomeBoardForEditor g_Parent2 = null;

        public ManualPosWindow(DisplayElementForEditor parent1, WelcomeBoardForEditor parent2=null)
        {
            InitializeComponent();
            g_Parent1 = parent1;
            g_Parent2 = parent2;
        }


        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            DisplayData();
        }

        public void DisplayData()
        {
            if (g_Parent2 == null)
            {
                XPos.Text = Canvas.GetLeft(g_Parent1).ToString("0.##");
                YPos.Text = Canvas.GetTop(g_Parent1).ToString("0.##");
                ObjWidth.Text = g_Parent1.Width.ToString("0.##");
                ObjHeight.Text = g_Parent1.Height.ToString("0.##");
            }
            else
            {
                XPos.Text = Canvas.GetLeft(g_Parent2).ToString("0.##");
                YPos.Text = Canvas.GetTop(g_Parent2).ToString("0.##");
                ObjWidth.Text = g_Parent2.Width.ToString("0.##");
                ObjHeight.Text = g_Parent2.Height.ToString("0.##");
            }
        }

        private void BtnWin_close_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            this.DialogResult = true;
            this.Close();
        }

        private void BtnWin_drag_PreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            this.DragMove();
        }

        private void SaveBtn_Click(object sender, RoutedEventArgs e)
        {
            if (g_Parent2 == null)
            {
                this.g_Parent1.SetManualPosData(double.Parse(XPos.Text), double.Parse(YPos.Text), double.Parse(ObjWidth.Text), double.Parse(ObjHeight.Text));
            }
            else
            {
                this.g_Parent2.SetManualPosData(double.Parse(XPos.Text), double.Parse(YPos.Text), double.Parse(ObjWidth.Text), double.Parse(ObjHeight.Text));
            }

            this.DialogResult = true;
            this.Close();
        }

        private void CancelBtn_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }

        string oldvalue;
        private void TBox_GotFocus(object sender, RoutedEventArgs e)
        {
            oldvalue = ((TextBox)sender).Text;
        }

        private void TBox_LostFocus(object sender, RoutedEventArgs e)
        {
            TextBox tb = ((TextBox)sender);

            try
            {
                tb.Text = double.Parse(tb.Text).ToString("0.##");
            }
            catch (Exception ex)
            {
                MessageTools.ShowMessageBox("잘못된 값을 입력하였습니다.", "확인");
                tb.Text = oldvalue;
            }
        }
    }

}
