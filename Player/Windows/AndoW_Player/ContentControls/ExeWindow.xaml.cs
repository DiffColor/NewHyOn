using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace HyOnPlayer
{
    /// <summary>
    /// ExeWindow.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class ExeWindow : Window
    {
        public ExeWindow()
        {
            InitializeComponent();
        }
        
        public ExeWindow(string exeName, string args, int offsetX = 0, int offsetY = 0, int marginW = 0, int marginH = 0, double scaleX = 1, double scaleY = 1)
        {
            InitializeComponent();

            exeCtr.ExeName = exeName;
            exeCtr.Args = args;

            exeCtr.MarginH = 22;

            this.Unloaded += new RoutedEventHandler((s, e) => { exeCtr.Dispose(); });
        }

        private void Window_LocationChanged(object sender, EventArgs e)
        {
            exeCtr.UpdatePosition(this.Left, this.Top);
        }
    }
}
