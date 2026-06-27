using System.ComponentModel;
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
    private MainWindowViewModel? _subscribedVm;

    public ItemDetailView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_subscribedVm != null)
            _subscribedVm.PropertyChanged -= OnViewModelPropertyChanged;

        if (DataContext is MainWindowViewModel vm)
        {
            _subscribedVm = vm;
            _subscribedVm.PropertyChanged += OnViewModelPropertyChanged;
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainWindowViewModel.SelectedItem))
            ResetScrollToTop();
    }

    private void ResetScrollToTop()
    {
        if (DetailScrollViewer == null) return;
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            DetailScrollViewer.Offset = new Avalonia.Vector(0, 0);
        });
    }

    protected override void OnDetachedFromVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        if (_subscribedVm != null)
        {
            _subscribedVm.PropertyChanged -= OnViewModelPropertyChanged;
            _subscribedVm = null;
        }
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

    private async void EditItem_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm || vm.SelectedItem == null) return;

        var window = new EditItemWindow(vm.SelectedItem);
        if (TopLevel.GetTopLevel(this) is Window owner)
            await window.ShowDialog(owner);

        if (window.Result?.Success == true)
        {
            try
            {
                var itemRepo = new Gatherly.Windows.Database.ItemRepository(vm.MainConnection);
                var fresh = await itemRepo.GetByIdAsync(vm.SelectedItem.Id);
                if (fresh != null)
                {
                    fresh.Title = window.Result.Title;
                    fresh.Body = window.Result.Body;
                    fresh.Author = window.Result.Author;
                    fresh.Remark = window.Result.Remark;
                    fresh.ModifyDate = DateTimeOffset.UtcNow;
                    await itemRepo.UpdateAsync(fresh);

                    var reloaded = await itemRepo.GetByIdAsync(vm.SelectedItem.Id);
                    if (reloaded != null)
                    {
                        vm.SelectedItem = null;
                        vm.SelectedItem = reloaded;

                        if (vm.PreviousSection == "Home")
                        {
                            await vm.Home.LoadCommand.ExecuteAsync(null);
                        }
                        else if (vm.PreviousSection == "Search")
                        {
                            if (!string.IsNullOrWhiteSpace(vm.Search.Query))
                                await vm.Search.SearchCommand.ExecuteAsync(null);
                        }
                        else
                        {
                            await vm.ContentList.ReloadCurrentContentAsync();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[EditSave] Error: {ex.Message}");
            }
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
