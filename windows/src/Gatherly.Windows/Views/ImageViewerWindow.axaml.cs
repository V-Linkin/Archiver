using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Gatherly.Windows.ViewModels;

namespace Gatherly.Windows.Views;

/// <summary>
/// 图片查看器窗口 — 支持多图上一张/下一张
/// </summary>
public partial class ImageViewerWindow : Window
{
    private readonly List<MediaAssetDisplay> _images;
    private int _currentIndex;

    public ImageViewerWindow()
    {
        InitializeComponent();
        _images = new List<MediaAssetDisplay>();
        KeyDown += OnKeyDown;
    }

    /// <summary>
    /// 打开图片查看器
    /// </summary>
    public static void Open(Window owner, List<MediaAssetDisplay> images, int startIndex = 0)
    {
        if (images.Count == 0) return;

        var viewer = new ImageViewerWindow();
        viewer._images.Clear();
        viewer._images.AddRange(images);
        viewer._currentIndex = Math.Clamp(startIndex, 0, images.Count - 1);
        viewer.LoadCurrentImage();
        viewer.ShowDialog(owner);
    }

    private void LoadCurrentImage()
    {
        if (_images.Count == 0) return;

        var current = _images[_currentIndex];

        // Update navigation visibility
        PrevButton.IsVisible = _images.Count > 1;
        NextButton.IsVisible = _images.Count > 1;

        // Update page info
        PageInfo.Text = $"{_currentIndex + 1} / {_images.Count}";
        FileNameText.Text = current.FileName;

        // Load image
        if (current.FileExists && current.FullPath != null)
        {
            try
            {
                MainImage.Source = new Bitmap(current.FullPath);
                MainImage.IsVisible = true;
                ErrorText.IsVisible = false;
            }
            catch
            {
                MainImage.IsVisible = false;
                ErrorText.Text = "图片加载失败";
                ErrorText.IsVisible = true;
            }
        }
        else
        {
            MainImage.IsVisible = false;
            ErrorText.Text = "图片文件不存在";
            ErrorText.IsVisible = true;
        }
    }

    private void Prev_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_images.Count <= 1) return;
        _currentIndex = (_currentIndex - 1 + _images.Count) % _images.Count;
        LoadCurrentImage();
    }

    private void Next_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_images.Count <= 1) return;
        _currentIndex = (_currentIndex + 1) % _images.Count;
        LoadCurrentImage();
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Left:
                Prev_Click(this, new Avalonia.Interactivity.RoutedEventArgs());
                e.Handled = true;
                break;
            case Key.Right:
                Next_Click(this, new Avalonia.Interactivity.RoutedEventArgs());
                e.Handled = true;
                break;
            case Key.Escape:
                Close();
                e.Handled = true;
                break;
        }
    }
}
