using System.Windows;
using System.Windows.Input;

namespace TurtleTools
{
    public partial class DbLoadingWindow : Window
    {
        public DbLoadingWindow()
        {
            InitializeComponent();
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }
    }
}
