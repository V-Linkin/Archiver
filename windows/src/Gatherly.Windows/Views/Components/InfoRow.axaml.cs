using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Gatherly.Windows.Views.Components;

public partial class InfoRow : UserControl
{
    public static readonly StyledProperty<string> IconProperty =
        AvaloniaProperty.Register<InfoRow, string>(nameof(Icon), "");

    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<InfoRow, string>(nameof(Label), "");

    public static readonly StyledProperty<string> ValueProperty =
        AvaloniaProperty.Register<InfoRow, string>(nameof(Value), "");

    public static readonly StyledProperty<bool> ShowSeparatorProperty =
        AvaloniaProperty.Register<InfoRow, bool>(nameof(ShowSeparator), true);

    public string Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    public string Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public string Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public bool ShowSeparator
    {
        get => GetValue(ShowSeparatorProperty);
        set => SetValue(ShowSeparatorProperty, value);
    }

    static InfoRow()
    {
        IconProperty.Changed.AddClassHandler<InfoRow>((x, e) => x.UpdateIcon());
        LabelProperty.Changed.AddClassHandler<InfoRow>((x, e) => x.UpdateLabel());
        ValueProperty.Changed.AddClassHandler<InfoRow>((x, e) => x.UpdateValue());
        ShowSeparatorProperty.Changed.AddClassHandler<InfoRow>((x, e) => x.UpdateSeparator());
    }

    public InfoRow()
    {
        InitializeComponent();
    }

    private void UpdateIcon()
    {
        IconBlock.Text = Icon;
        IconBlock.IsVisible = !string.IsNullOrEmpty(Icon);
    }

    private void UpdateLabel()
    {
        LabelBlock.Text = Label;
        LabelBlock.IsVisible = !string.IsNullOrEmpty(Label);
    }

    private void UpdateValue()
    {
        ValueBlock.Text = Value;
    }

    private void UpdateSeparator()
    {
        if (Content is Border border)
            border.BorderThickness = new Thickness(0, 0, 0, ShowSeparator ? 1 : 0);
    }
}
