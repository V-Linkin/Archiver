using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Gatherly.Windows.Views;

public partial class ConfirmDialogWindow : Window
{
    public bool? Result { get; private set; }

    public ConfirmDialogWindow(string title, string message,
        string cancelText = "取消", string confirmText = "确认删除",
        bool isDangerConfirm = true)
    {
        InitializeComponent();
        TitleText.Text = title;
        MessageText.Text = message;
        CancelButton.Content = cancelText;
        ConfirmButton.Content = confirmText;
        if (isDangerConfirm)
        {
            ConfirmButton.Foreground = new Avalonia.Media.SolidColorBrush(
                Avalonia.Media.Color.Parse("#D32F2F"));
        }
        else
        {
            ConfirmButton.Foreground = new Avalonia.Media.SolidColorBrush(
                Avalonia.Media.Color.Parse("#333333"));
        }
    }

    private void ConfirmButton_Click(object? sender, RoutedEventArgs e)
    {
        Result = true;
        Close();
    }

    private void CancelButton_Click(object? sender, RoutedEventArgs e)
    {
        Result = false;
        Close();
    }
}
