using Avalonia.Controls;
using Avalonia.Interactivity;
using Gatherly.Windows.ViewModels;

namespace Gatherly.Windows.Views;

public partial class ExportPickerWindow : Window
{
    private string _selection = "mediaOnly";

    public int MediaAssetCount { get; set; }
    public int BodyImageCount { get; set; }
    public bool HasBodyImages { get; set; }

    public string ExportSelection => _selection;

    public ExportPickerWindow()
    {
        InitializeComponent();
    }

    public void SetupOptions()
    {
        var hasMedia = MediaAssetCount > 0;
        var hasBody = HasBodyImages && BodyImageCount > 0;
        var hasBoth = hasMedia && hasBody;

        MediaOption.IsVisible = hasMedia;
        MediaDetail.Text = $"{MediaAssetCount} 个文件（图片/视频）";

        BodyOption.IsVisible = hasBody;
        BodyDetail.Text = $"{BodyImageCount} 个文件";

        AllOption.IsVisible = hasBoth;
        AllDetail.Text = $"{MediaAssetCount + BodyImageCount} 个文件";

        if (hasMedia)
            _selection = "mediaOnly";
        else if (hasBody)
            _selection = "bodyImagesOnly";

        UpdateSelection();
    }

    private void UpdateSelection()
    {
        MediaCheckIcon.Text = _selection == "mediaOnly" ? "●" : "○";
        MediaCheckIcon.Foreground = _selection == "mediaOnly"
            ? new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#007AFF"))
            : new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#888888"));

        BodyCheckIcon.Text = _selection == "bodyImagesOnly" ? "●" : "○";
        BodyCheckIcon.Foreground = _selection == "bodyImagesOnly"
            ? new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#007AFF"))
            : new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#888888"));

        AllCheckIcon.Text = _selection == "all" ? "●" : "○";
        AllCheckIcon.Foreground = _selection == "all"
            ? new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#007AFF"))
            : new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#888888"));
    }

    private void Option_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string tag)
        {
            _selection = tag;
            UpdateSelection();
        }
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    private void Export_Click(object? sender, RoutedEventArgs e)
    {
        Close(_selection);
    }
}
