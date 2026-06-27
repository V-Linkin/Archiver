using System.ComponentModel;
using System.Runtime.InteropServices;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.Input;
using Gatherly.Windows.Models;
using Gatherly.Windows.Models.Enums;
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

    private async void MoveItem_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm || vm.SelectedItem == null) return;
        vm.HandleMoveToFolder(vm.SelectedItem);
    }

    private async void ExportItem_Click(object? sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        Window? info = null;
        var okBtn = new Button
        {
            Content = "确定",
            Padding = new Avalonia.Thickness(16, 6),
            Command = new CommunityToolkit.Mvvm.Input.RelayCommand(() => info?.Close())
        };
        okBtn.Classes.Add("PrimaryButton");

        info = new Window
        {
            Title = "提示",
            Width = 320,
            Height = 140,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            Content = new StackPanel
            {
                Margin = new Avalonia.Thickness(20),
                Spacing = 16,
                Children =
                {
                    new TextBlock { Text = "导出功能将在后续阶段实现", FontSize = 14, TextWrapping = TextWrapping.Wrap },
                    new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        HorizontalAlignment = HorizontalAlignment.Right,
                        Children = { okBtn }
                    }
                }
            }
        };
        await info.ShowDialog(topLevel as Window);
    }

    private async void EditItem_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm || vm.SelectedItem == null) return;

        var mediaRepo = new Gatherly.Windows.Database.MediaRepository(vm.MainConnection);
        var existingAssets = await mediaRepo.GetByItemIdAsync(vm.SelectedItem.Id);

        var window = new EditItemWindow(vm.SelectedItem, existingAssets);
        if (TopLevel.GetTopLevel(this) is Window owner)
            await window.ShowDialog(owner);

        if (window.Result?.Success == true)
        {
            try
            {
                var itemRepo = new Gatherly.Windows.Database.ItemRepository(vm.MainConnection);
                var fresh = await itemRepo.GetByIdAsync(vm.SelectedItem.Id);
                if (fresh == null) return;

                fresh.Title = window.Result.Title;
                fresh.Body = window.Result.Body;
                fresh.Author = window.Result.Author;
                fresh.Remark = window.Result.Remark;
                fresh.ModifyDate = DateTimeOffset.UtcNow;
                await itemRepo.UpdateAsync(fresh);

                var itemId = fresh.Id;
                var itemMediaDir = Path.Combine(Gatherly.Windows.Database.DatabasePaths.DataDirectory, "media", itemId.ToString("D"));
                Directory.CreateDirectory(itemMediaDir);

                // Copy new images
                var existingImageCount = existingAssets
                    .Count(a => (a.Type == MediaType.image || a.Type == MediaType.cover) && !window.Result.RemovedAssetIds.Contains(a.Id));

                var copiedImageAssets = new List<MediaAsset>();
                for (int i = 0; i < window.Result.NewImages.Count; i++)
                {
                    var (tempPath, originalName) = window.Result.NewImages[i];
                    var ext = Path.GetExtension(originalName);
                    var fileName = $"image_{existingImageCount + i + 1}{ext}";
                    var dest = Path.Combine(itemMediaDir, fileName);
                    try
                    {
                        File.Copy(tempPath, dest, true);
                        var fileInfo = new FileInfo(dest);
                        var asset = new MediaAsset
                        {
                            Id = Guid.NewGuid(),
                            ItemId = itemId,
                            Type = MediaType.image,
                            LocalPath = $"{itemId.ToString("D")}/{fileName}",
                            FileName = fileName,
                            FileSize = fileInfo.Length,
                            DownloadStatus = DownloadStatus.completed,
                            CreatedAt = DateTimeOffset.UtcNow
                        };
                        await mediaRepo.InsertAsync(asset);
                        copiedImageAssets.Add(asset);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[EditSave] Image copy failed: {ex.Message}");
                        try { if (File.Exists(dest)) File.Delete(dest); } catch { }
                        return;
                    }
                }

                // Copy new videos
                var existingVideoCount = existingAssets
                    .Count(a => a.Type == MediaType.video && !window.Result.RemovedAssetIds.Contains(a.Id));

                for (int i = 0; i < window.Result.NewVideos.Count; i++)
                {
                    var (tempPath, originalName) = window.Result.NewVideos[i];
                    var ext = Path.GetExtension(originalName);
                    if (string.IsNullOrEmpty(ext)) ext = ".mp4";
                    var fileName = $"video_{existingVideoCount + i + 1}{ext}";
                    var dest = Path.Combine(itemMediaDir, fileName);
                    try
                    {
                        File.Copy(tempPath, dest, true);
                        var fileInfo = new FileInfo(dest);
                        var asset = new MediaAsset
                        {
                            Id = Guid.NewGuid(),
                            ItemId = itemId,
                            Type = MediaType.video,
                            LocalPath = $"{itemId.ToString("D")}/{fileName}",
                            FileName = fileName,
                            FileSize = fileInfo.Length,
                            DownloadStatus = DownloadStatus.completed,
                            CreatedAt = DateTimeOffset.UtcNow
                        };
                        await mediaRepo.InsertAsync(asset);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[EditSave] Video copy failed: {ex.Message}");
                        try { if (File.Exists(dest)) File.Delete(dest); } catch { }
                        return;
                    }
                }

                // Delete removed assets
                foreach (var assetId in window.Result.RemovedAssetIds)
                {
                    var asset = existingAssets.FirstOrDefault(a => a.Id == assetId);
                    if (asset != null && !string.IsNullOrEmpty(asset.LocalPath))
                    {
                        var fullPath = Gatherly.Windows.Services.MediaPathHelper.ResolveFullPath(asset.LocalPath);
                        try { if (File.Exists(fullPath)) File.Delete(fullPath); } catch { }
                    }
                    await mediaRepo.DeleteAsync(assetId);
                }

                // Update cover
                var allImages = await mediaRepo.GetByItemIdAsync(itemId);
                var firstImage = allImages
                    .Where(a => a.Type == MediaType.image || a.Type == MediaType.cover)
                    .OrderBy(a => a.CreatedAt)
                    .FirstOrDefault();

                var coverFresh = await itemRepo.GetByIdAsync(itemId);
                if (coverFresh != null)
                {
                    coverFresh.CoverAssetId = firstImage?.Id;
                    coverFresh.ModifyDate = DateTimeOffset.UtcNow;
                    await itemRepo.UpdateAsync(coverFresh);
                }

                // Reload and refresh
                var reloaded = await itemRepo.GetByIdAsync(itemId);
                if (reloaded != null)
                {
                    vm.SelectedItem = null;
                    vm.SelectedItem = reloaded;

                    if (vm.PreviousSection == "Home")
                        await vm.Home.LoadCommand.ExecuteAsync(null);
                    else if (vm.PreviousSection == "Search")
                    {
                        if (!string.IsNullOrWhiteSpace(vm.Search.Query))
                            await vm.Search.SearchCommand.ExecuteAsync(null);
                    }
                    else
                        await vm.ContentList.ReloadCurrentContentAsync();
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

        var dialog = new ConfirmDialogWindow(
            "移入回收站",
            "确定将此内容移入回收站？",
            "取消", "删除", isDangerConfirm: true);

        if (TopLevel.GetTopLevel(this) is Window owner)
            await dialog.ShowDialog(owner);

        if (dialog.Result == true)
        {
            await vm.TrashSelectedItemCommand.ExecuteAsync(null);
        }
    }

    [DllImport("user32.dll")]
    private static extern bool OpenClipboard(IntPtr hWndNewOwner);

    [DllImport("user32.dll")]
    private static extern bool CloseClipboard();

    [DllImport("user32.dll")]
    private static extern bool EmptyClipboard();

    [DllImport("user32.dll")]
    private static extern IntPtr SetClipboardData(uint uFormat, IntPtr hMem);

    [DllImport("user32.dll")]
    private static extern uint RegisterClipboardFormat(string format);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GlobalAlloc(uint uFlags, UIntPtr dwBytes);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GlobalLock(IntPtr hMem);

    [DllImport("kernel32.dll")]
    private static extern bool GlobalUnlock(IntPtr hMem);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GlobalFree(IntPtr hMem);

    private const uint GMEM_MOVEABLE = 0x0002;
    private const uint CF_DIB = 8;

    private void ImageCopy_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { Tag: string fullPath } || string.IsNullOrEmpty(fullPath)) return;
        if (!File.Exists(fullPath)) return;

        try
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null) return;

            using var bitmap = new Avalonia.Media.Imaging.Bitmap(fullPath);
            int width = bitmap.PixelSize.Width;
            int height = bitmap.PixelSize.Height;
            int stride = width * 4;
            var pixels = new byte[stride * height];
            var handle = System.Runtime.InteropServices.GCHandle.Alloc(pixels, System.Runtime.InteropServices.GCHandleType.Pinned);
            try
            {
                bitmap.CopyPixels(new Avalonia.PixelRect(0, 0, width, height), handle.AddrOfPinnedObject(), pixels.Length, stride);
            }
            finally
            {
                handle.Free();
            }

            int headerSize = 40;
            int dibSize = headerSize + pixels.Length;
            IntPtr hGlobal = GlobalAlloc(GMEM_MOVEABLE, (UIntPtr)dibSize);
            if (hGlobal == IntPtr.Zero) return;

            IntPtr ptr = GlobalLock(hGlobal);

            var headerBytes = new byte[headerSize];
            BitConverter.TryWriteBytes(headerBytes.AsSpan(0, 4), (uint)headerSize);
            BitConverter.TryWriteBytes(headerBytes.AsSpan(4, 4), (uint)width);
            BitConverter.TryWriteBytes(headerBytes.AsSpan(8, 4), (uint)(-height));
            BitConverter.TryWriteBytes(headerBytes.AsSpan(12, 2), (ushort)1);
            BitConverter.TryWriteBytes(headerBytes.AsSpan(14, 2), (ushort)32);
            BitConverter.TryWriteBytes(headerBytes.AsSpan(16, 4), (uint)0);
            BitConverter.TryWriteBytes(headerBytes.AsSpan(20, 4), (uint)pixels.Length);
            BitConverter.TryWriteBytes(headerBytes.AsSpan(24, 4), (int)0);
            BitConverter.TryWriteBytes(headerBytes.AsSpan(28, 4), (int)0);
            BitConverter.TryWriteBytes(headerBytes.AsSpan(32, 4), (uint)0);
            BitConverter.TryWriteBytes(headerBytes.AsSpan(36, 4), (uint)0);

            Marshal.Copy(headerBytes, 0, ptr, headerSize);
            Marshal.Copy(pixels, 0, ptr + headerSize, pixels.Length);
            GlobalUnlock(hGlobal);

            uint pngFormat = RegisterClipboardFormat("PNG");
            IntPtr hPng = IntPtr.Zero;
            byte[]? pngBytes = null;
            if (pngFormat != 0)
            {
                using var ms = new MemoryStream();
                bitmap.Save(ms);
                pngBytes = ms.ToArray();
                hPng = GlobalAlloc(GMEM_MOVEABLE, (UIntPtr)pngBytes.Length);
                if (hPng != IntPtr.Zero)
                {
                    IntPtr pngPtr = GlobalLock(hPng);
                    Marshal.Copy(pngBytes, 0, pngPtr, pngBytes.Length);
                    GlobalUnlock(hPng);
                }
            }

            var topLevelHandle = topLevel.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
            if (OpenClipboard(topLevelHandle))
            {
                EmptyClipboard();
                SetClipboardData(CF_DIB, hGlobal);
                if (hPng != IntPtr.Zero)
                    SetClipboardData(pngFormat, hPng);
                CloseClipboard();
            }
            else
            {
                GlobalFree(hGlobal);
                if (hPng != IntPtr.Zero) GlobalFree(hPng);
            }
        }
        catch { }
    }

    private async void ImageSaveAs_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { Tag: string fullPath } || string.IsNullOrEmpty(fullPath)) return;
        if (!File.Exists(fullPath)) return;

        try
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null) return;

            var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "另存为",
                SuggestedFileName = Path.GetFileName(fullPath),
                FileTypeChoices = new[]
                {
                    new FilePickerFileType("图片") { Patterns = new[] { "*.jpg", "*.jpeg", "*.png", "*.gif", "*.webp", "*.bmp" } }
                }
            });

            if (file != null)
            {
                var destPath = file.TryGetLocalPath();
                if (!string.IsNullOrEmpty(destPath))
                    File.Copy(fullPath, destPath, true);
            }
        }
        catch { }
    }
}
