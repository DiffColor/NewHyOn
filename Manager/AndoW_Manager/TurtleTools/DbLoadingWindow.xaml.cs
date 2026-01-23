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

        public void SetStatus(string title, string message)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(() => SetStatus(title, message));
                return;
            }

            if (!string.IsNullOrWhiteSpace(title) && TitleTextBlock != null)
            {
                TitleTextBlock.Text = title;
            }

            if (!string.IsNullOrWhiteSpace(message) && MessageTextBlock != null)
            {
                MessageTextBlock.Text = message;
            }
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
