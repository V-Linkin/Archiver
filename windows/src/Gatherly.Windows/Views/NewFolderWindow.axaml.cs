using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Gatherly.Windows.Views;

public partial class NewFolderWindow : Window
{
    public string? Result { get; private set; }

    public NewFolderWindow(string title = "新建文件夹")
    {
        InitializeComponent();
        TitleText.Text = title;
        CreateButton.IsEnabled = false;
        FolderNameBox.TextChanged += (_, _) =>
        {
            CreateButton.IsEnabled = !string.IsNullOrWhiteSpace(FolderNameBox.Text);
        };
    }

    private void CancelButton_Click(object? sender, RoutedEventArgs e)
    {
        Result = null;
        Close();
    }

    private void CreateButton_Click(object? sender, RoutedEventArgs e)
    {
        var name = FolderNameBox.Text?.Trim();
        if (string.IsNullOrWhiteSpace(name)) return;
        Result = name;
        Close();
    }
}
