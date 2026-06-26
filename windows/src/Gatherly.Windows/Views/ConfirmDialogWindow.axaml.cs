using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Gatherly.Windows.Views;

public partial class ConfirmDialogWindow : Window
{
    public bool? Result { get; private set; }

    public ConfirmDialogWindow(string title, string message)
    {
        InitializeComponent();
        TitleText.Text = title;
        MessageText.Text = message;
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
