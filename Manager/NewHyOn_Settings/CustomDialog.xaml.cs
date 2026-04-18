using NewHyOn.Settings.Wpf.Models;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace NewHyOn.Settings.Wpf;

public partial class CustomDialog : Window
{
    private static readonly Brush DefaultSubtitleBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#177E89"));
    private static readonly Brush NoticeSubtitleBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#C2410C"));

    public CustomDialogResult Result { get; private set; }

    public CustomDialog(string title, string message, string? subtitle = null, string primaryButtonText = "확인", string? secondaryButtonText = null)
    {
        InitializeComponent();
        DialogTitleTextBlock.Text = title;
        bool hasCustomSubtitle = string.IsNullOrWhiteSpace(subtitle) == false;
        DialogSubtitleTextBlock.Text = hasCustomSubtitle ? subtitle : "NewHyOn Settings";
        DialogSubtitleTextBlock.Foreground = hasCustomSubtitle ? NoticeSubtitleBrush : DefaultSubtitleBrush;
        DialogMessageTextBlock.Text = message;
        PrimaryActionButton.Content = string.IsNullOrWhiteSpace(primaryButtonText) ? "확인" : primaryButtonText;

        if (string.IsNullOrWhiteSpace(secondaryButtonText))
        {
            SecondaryActionButton.Visibility = Visibility.Collapsed;
        }
        else
        {
            SecondaryActionButton.Visibility = Visibility.Visible;
            SecondaryActionButton.Content = secondaryButtonText;
        }

    }

    public static void Show(Window owner, string title, string message, string? subtitle = null)
    {
        var dialog = new CustomDialog(title, message, subtitle)
        {
            Owner = owner
        };

        ToggleOwnerOverlay(owner, true);
        try
        {
            dialog.ShowDialog();
        }
        finally
        {
            ToggleOwnerOverlay(owner, false);
        }
    }

    public static CustomDialogResult ShowChoice(
        Window owner,
        string title,
        string message,
        string? subtitle,
        string primaryButtonText,
        string secondaryButtonText)
    {
        var dialog = new CustomDialog(title, message, subtitle, primaryButtonText, secondaryButtonText)
        {
            Owner = owner
        };

        ToggleOwnerOverlay(owner, true);
        try
        {
            dialog.ShowDialog();
            return dialog.Result;
        }
        finally
        {
            ToggleOwnerOverlay(owner, false);
        }
    }

    private void TitleBar_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void PrimaryButton_OnClick(object sender, RoutedEventArgs e)
    {
        Result = CustomDialogResult.Primary;
        Close();
    }

    private void DecorationButton_OnClick(object sender, RoutedEventArgs e)
    {
    }

    private void SecondaryButton_OnClick(object sender, RoutedEventArgs e)
    {
        Result = CustomDialogResult.Secondary;
        Close();
    }

    private void CloseButton_OnClick(object sender, RoutedEventArgs e)
    {
        Result = CustomDialogResult.None;
        Close();
    }

    private static void ToggleOwnerOverlay(Window owner, bool isVisible)
    {
        if (owner.FindName("DialogOverlay") is UIElement overlay)
        {
            overlay.Visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;
        }
    }
}
