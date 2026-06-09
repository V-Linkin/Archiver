using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using CommunityToolkit.Mvvm.Input;
using Gatherly.Windows.ViewModels;

namespace Gatherly.Windows.Views;

public partial class ItemDetailView : UserControl
{
    public ItemDetailView()
    {
        InitializeComponent();
    }

    private async void TrashConfirm_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm || vm.SelectedItem == null) return;

        var title = vm.SelectedItem.Title ?? "未命名内容";

        Window? dialog = null;

        var cancelBtn = new Button
        {
            Content = "取消",
            Padding = new Avalonia.Thickness(16, 6),
            Command = new RelayCommand(() => dialog?.Close())
        };

        var confirmBtn = new Button
        {
            Content = "移入回收站",
            Padding = new Avalonia.Thickness(16, 6),
            Foreground = new SolidColorBrush(Color.Parse("#D32F2F")),
            Command = new RelayCommand(async () =>
            {
                dialog?.Close();
                await vm.TrashSelectedItemCommand.ExecuteAsync(null);
            })
        };

        dialog = new Window
        {
            Title = "确认删除",
            Width = 360,
            Height = 160,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            Content = new StackPanel
            {
                Margin = new Avalonia.Thickness(20),
                Spacing = 16,
                Children =
                {
                    new TextBlock
                    {
                        Text = $"确定将「{title}」移入回收站？",
                        TextWrapping = TextWrapping.Wrap,
                        FontSize = 14
                    },
                    new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        HorizontalAlignment = HorizontalAlignment.Right,
                        Spacing = 10,
                        Children = { cancelBtn, confirmBtn }
                    }
                }
            }
        };

        if (TopLevel.GetTopLevel(this) is Window owner)
        {
            await dialog.ShowDialog(owner);
        }
    }
}
