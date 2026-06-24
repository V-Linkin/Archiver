using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.Input;
using Gatherly.Windows.Services;
using Gatherly.Windows.ViewModels;

namespace Gatherly.Windows.Views;

public partial class ItemDetailView : UserControl
{
    private readonly IExternalLinkService _externalLinkService = new ExternalLinkService();

    public ItemDetailView()
    {
        InitializeComponent();
    }

    /// <summary>
    /// 原始链接点击事件
    /// </summary>
    private void OriginalUrl_PointerPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm && vm.SelectedItem?.OriginalUrl != null)
        {
            _externalLinkService.Open(vm.SelectedItem.OriginalUrl);
            e.Handled = true;
        }
    }

    /// <summary>
    /// 正文链接点击事件
    /// </summary>
    private void BodyLink_PointerPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
    {
        if (sender is TextBlock textBlock && textBlock.Tag is string url)
        {
            _externalLinkService.Open(url);
            e.Handled = true;
        }
    }

    /// <summary>
    /// 视频预览卡片点击 — 播放视频
    /// </summary>
    private void VideoCard_PointerPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            vm.PlayVideoCardCommand.Execute(null);
            e.Handled = true;
        }
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
