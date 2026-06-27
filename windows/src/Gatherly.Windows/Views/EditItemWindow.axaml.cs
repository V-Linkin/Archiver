using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Gatherly.Windows.Database;
using Gatherly.Windows.Models;
using Gatherly.Windows.Models.Enums;
using Gatherly.Windows.Services;
using Gatherly.Windows.ViewModels;

namespace Gatherly.Windows.Views;

public partial class EditItemWindow : Window
{
    private readonly Item _item;
    private readonly string _initialTitle;
    private readonly string _initialBody;
    private readonly string _initialAuthor;
    private readonly string _initialRemark;
    private bool _forceClose;

    private readonly List<MediaAsset> _existingAssets = new();
    private readonly HashSet<Guid> _removedAssetIds = new();
    private readonly List<(string TempPath, string FileName)> _newImages = new();
    private readonly List<(string TempPath, string FileName)> _newVideos = new();

    public EditItemResult? Result { get; private set; }

    public EditItemWindow(Item item, List<MediaAsset> existingAssets)
    {
        _item = item;
        _existingAssets.AddRange(existingAssets);
        InitializeComponent();

        TitleBox.Text = item.Title ?? "";
        BodyBox.Text = item.Body ?? "";
        AuthorBox.Text = item.Author ?? "";
        RemarkBox.Text = item.Remark ?? "";

        _initialTitle = TitleBox.Text;
        _initialBody = BodyBox.Text;
        _initialAuthor = AuthorBox.Text;
        _initialRemark = RemarkBox.Text;

        RefreshMediaDisplay();
    }

    private bool HasChanges =>
        TitleBox.Text != _initialTitle ||
        BodyBox.Text != _initialBody ||
        AuthorBox.Text != _initialAuthor ||
        RemarkBox.Text != _initialRemark ||
        _newImages.Count > 0 ||
        _newVideos.Count > 0 ||
        _removedAssetIds.Count > 0;

    private void RefreshMediaDisplay()
    {
        ImageWrapPanel.Children.Clear();

        var visibleExisting = _existingAssets
            .Where(a => (a.Type == MediaType.image || a.Type == MediaType.cover) && !_removedAssetIds.Contains(a.Id))
            .ToList();
        foreach (var asset in visibleExisting)
        {
            var fullPath = MediaPathHelper.ResolveFullPath(asset.LocalPath!);
            if (!File.Exists(fullPath)) continue;
            var idx = visibleExisting.IndexOf(asset);
            ImageWrapPanel.Children.Add(CreateImageTile(fullPath, asset.Id, isExisting: true, index: idx, allPaths: null));
        }

        var newPaths = _newImages.Where(n => File.Exists(n.TempPath)).Select(n => n.TempPath).ToList();
        for (int i = 0; i < newPaths.Count; i++)
        {
            ImageWrapPanel.Children.Add(CreateImageTile(newPaths[i], Guid.Empty, isExisting: false, index: visibleExisting.Count + i, allPaths: null));
        }

        NoImagesText.IsVisible = ImageWrapPanel.Children.Count == 0;

        VideoStackPanel.Children.Clear();

        foreach (var asset in _existingAssets.Where(a => a.Type == MediaType.video && !_removedAssetIds.Contains(a.Id)))
        {
            VideoStackPanel.Children.Add(CreateVideoRow(asset.FileName, asset.Id));
        }
        foreach (var vid in _newVideos)
        {
            VideoStackPanel.Children.Add(CreateVideoRow(vid.FileName, Guid.Empty, vid.TempPath));
        }

        NoVideosText.IsVisible = VideoStackPanel.Children.Count == 0;
    }

    private Border CreateImageTile(string fullPath, Guid assetId, bool isExisting, int index, List<string>? allPaths)
    {
        var grid = new Grid { Width = 100, Height = 100, Margin = new Avalonia.Thickness(0, 0, 6, 6) };

        var border = new Border
        {
            ClipToBounds = true,
            CornerRadius = new Avalonia.CornerRadius(6)
        };
        var img = new Image
        {
            Source = new Avalonia.Media.Imaging.Bitmap(fullPath),
            Stretch = Avalonia.Media.Stretch.UniformToFill
        };

        var tapHandler = new EventHandler<RoutedEventArgs>((_, _) => OpenImageViewer(fullPath));
        img.Tapped += (s, e) => OpenImageViewer(fullPath);
        border.Cursor = new Cursor(StandardCursorType.Hand);
        border.PointerPressed += (s, e) =>
        {
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
                OpenImageViewer(fullPath);
        };

        border.Child = img;

        // Remove button — xmark.circle.fill style (red circle with white X)
        var removeBtn = new Button
        {
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top,
            Margin = new Avalonia.Thickness(0, -4, -4, 0),
            Padding = new Avalonia.Thickness(0),
            Tag = assetId,
            Width = 20, Height = 20,
            Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#D32F2F")),
            CornerRadius = new Avalonia.CornerRadius(10),
            BorderThickness = new Avalonia.Thickness(0)
        };
        var xIcon = new TextBlock
        {
            Text = "✕",
            FontSize = 12,
            FontWeight = Avalonia.Media.FontWeight.Bold,
            Foreground = Avalonia.Media.Brushes.White,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
        };
        removeBtn.Content = xIcon;

        if (isExisting)
            removeBtn.Click += RemoveExistingImage_Click;
        else
        {
            removeBtn.Tag = fullPath;
            removeBtn.Click += RemoveNewImage_Click;
        }

        grid.Children.Add(border);
        grid.Children.Add(removeBtn);

        return new Border { Child = grid, Background = Avalonia.Media.Brushes.Transparent };
    }

    private void OpenImageViewer(string fullPath)
    {
        try
        {
            var displays = new List<MediaAssetDisplay>();

            var existingVisible = _existingAssets
                .Where(a => (a.Type == MediaType.image || a.Type == MediaType.cover) && !_removedAssetIds.Contains(a.Id))
                .ToList();
            foreach (var a in existingVisible)
            {
                var p = MediaPathHelper.ResolveFullPath(a.LocalPath!);
                if (File.Exists(p))
                    displays.Add(new MediaAssetDisplay { FileName = a.FileName, FullPath = p, FileExists = true, FileSize = a.FileSize });
            }
            foreach (var n in _newImages)
            {
                if (File.Exists(n.TempPath))
                    displays.Add(new MediaAssetDisplay { FileName = n.FileName, FullPath = n.TempPath, FileExists = true, FileSize = new FileInfo(n.TempPath).Length });
            }

            var index = displays.FindIndex(d => d.FullPath == fullPath);
            if (index < 0) index = 0;

            if (displays.Count > 0)
                ImageViewerWindow.Open(TopLevel.GetTopLevel(this) as Window, displays, index);
        }
        catch { }
    }

    private Border CreateVideoRow(string fileName, Guid assetId, string? tempPath = null)
    {
        var textBlock = new TextBlock
        {
            Text = $"🎬 {fileName}",
            FontSize = 12, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
        };
        var removeBtn = new Button
        {
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
            Margin = new Avalonia.Thickness(8, 0, 0, 0),
            Padding = new Avalonia.Thickness(4),
            Tag = assetId != Guid.Empty ? (object)assetId : (tempPath ?? ""),
            Content = new TextBlock
            {
                Text = "✕",
                FontSize = 12,
                FontWeight = Avalonia.Media.FontWeight.Bold,
                Foreground = Avalonia.Media.Brushes.Red
            }
        };
        removeBtn.Click += RemoveVideo_Click;

        var panel = new DockPanel
        {
            Margin = new Avalonia.Thickness(0, 0, 0, 6),
            Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#F5F5F5"))
        };
        DockPanel.SetDock(removeBtn, Dock.Right);
        panel.Children.Add(removeBtn);
        panel.Children.Add(textBlock);

        return new Border
        {
            CornerRadius = new Avalonia.CornerRadius(6),
            Padding = new Avalonia.Thickness(10, 8),
            Child = panel
        };
    }

    private void RemoveExistingImage_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is Guid assetId)
        {
            _removedAssetIds.Add(assetId);
            RefreshMediaDisplay();
        }
    }

    private void RemoveNewImage_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string path)
        {
            var idx = _newImages.FindIndex(n => n.TempPath == path);
            if (idx >= 0)
            {
                try { File.Delete(_newImages[idx].TempPath); } catch { }
                _newImages.RemoveAt(idx);
            }
            RefreshMediaDisplay();
        }
    }

    private void RemoveVideo_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn)
        {
            if (btn.Tag is Guid assetId && assetId != Guid.Empty)
            {
                _removedAssetIds.Add(assetId);
            }
            else if (btn.Tag is string tempPath && !string.IsNullOrEmpty(tempPath))
            {
                var idx = _newVideos.FindIndex(v => v.TempPath == tempPath);
                if (idx >= 0)
                {
                    try { File.Delete(_newVideos[idx].TempPath); } catch { }
                    _newVideos.RemoveAt(idx);
                }
            }
            RefreshMediaDisplay();
        }
    }

    private async void AddImage_Click(object? sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "选择图片",
            AllowMultiple = true,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("图片") { Patterns = new[] { "*.jpg", "*.jpeg", "*.png", "*.gif", "*.webp", "*.bmp" } }
            }
        });

        if (files.Count == 0) return;

        var tempDir = Path.Combine(Path.GetTempPath(), "GatherlyEdit_" + _item.Id.ToString("N")[..8]);
        Directory.CreateDirectory(tempDir);

        foreach (var file in files)
        {
            var sourcePath = file.TryGetLocalPath();
            if (string.IsNullOrEmpty(sourcePath)) continue;

            var ext = Path.GetExtension(sourcePath);
            var tempPath = Path.Combine(tempDir, $"img_{Guid.NewGuid():N}{ext}");
            File.Copy(sourcePath, tempPath, true);
            _newImages.Add((tempPath, Path.GetFileName(sourcePath)));
        }

        RefreshMediaDisplay();
    }

    private async void AddVideo_Click(object? sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "选择视频",
            AllowMultiple = true,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("视频") { Patterns = new[] { "*.mp4", "*.mov", "*.m4v", "*.avi", "*.mkv", "*.webm" } }
            }
        });

        if (files.Count == 0) return;

        var tempDir = Path.Combine(Path.GetTempPath(), "GatherlyEdit_" + _item.Id.ToString("N")[..8]);
        Directory.CreateDirectory(tempDir);

        foreach (var file in files)
        {
            var sourcePath = file.TryGetLocalPath();
            if (string.IsNullOrEmpty(sourcePath)) continue;

            var ext = Path.GetExtension(sourcePath);
            var tempPath = Path.Combine(tempDir, $"vid_{Guid.NewGuid():N}{ext}");
            File.Copy(sourcePath, tempPath, true);
            _newVideos.Add((tempPath, Path.GetFileName(sourcePath)));
        }

        RefreshMediaDisplay();
    }

    private async void Window_Closing(object? sender, WindowClosingEventArgs e)
    {
        if (_forceClose || !HasChanges) return;
        e.Cancel = true;

        var dialog = new ConfirmDialogWindow(
            "放弃修改？",
            "当前内容有未保存的修改，放弃后将丢失这些更改。",
            "继续编辑", "放弃修改", isDangerConfirm: false);
        if (TopLevel.GetTopLevel(this) is Window owner)
            await dialog.ShowDialog(owner);

        if (dialog.Result == true)
        {
            _forceClose = true;
            CleanupTempFiles();
            Close();
        }
    }

    private async void CancelButton_Click(object? sender, RoutedEventArgs e)
    {
        if (HasChanges)
        {
            var dialog = new ConfirmDialogWindow(
                "放弃修改？",
                "当前内容有未保存的修改，放弃后将丢失这些更改。",
                "继续编辑", "放弃修改", isDangerConfirm: false);
            if (TopLevel.GetTopLevel(this) is Window owner)
                await dialog.ShowDialog(owner);

            if (dialog.Result == true)
            {
                _forceClose = true;
                CleanupTempFiles();
                Close();
            }
        }
        else
        {
            Close();
        }
    }

    private void SaveButton_Click(object? sender, RoutedEventArgs e)
    {
        Result = new EditItemResult
        {
            Success = true,
            Title = string.IsNullOrWhiteSpace(TitleBox.Text) ? null : TitleBox.Text,
            Body = string.IsNullOrWhiteSpace(BodyBox.Text) ? null : BodyBox.Text,
            Author = string.IsNullOrWhiteSpace(AuthorBox.Text) ? null : AuthorBox.Text,
            Remark = string.IsNullOrWhiteSpace(RemarkBox.Text) ? null : RemarkBox.Text,
            NewImages = _newImages.ToList(),
            NewVideos = _newVideos.ToList(),
            RemovedAssetIds = _removedAssetIds.ToList(),
        };
        _forceClose = true;
        Close();
    }

    private void CleanupTempFiles()
    {
        foreach (var img in _newImages)
        {
            try { if (File.Exists(img.TempPath)) File.Delete(img.TempPath); } catch { }
        }
        foreach (var vid in _newVideos)
        {
            try { if (File.Exists(vid.TempPath)) File.Delete(vid.TempPath); } catch { }
        }
        _newImages.Clear();
        _newVideos.Clear();
    }
}

public class EditItemResult
{
    public bool Success { get; set; }
    public string? Title { get; set; }
    public string? Body { get; set; }
    public string? Author { get; set; }
    public string? Remark { get; set; }
    public List<(string TempPath, string FileName)> NewImages { get; set; } = new();
    public List<(string TempPath, string FileName)> NewVideos { get; set; } = new();
    public List<Guid> RemovedAssetIds { get; set; } = new();
}
