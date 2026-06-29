using System.Text.RegularExpressions;
using Avalonia.Controls;
using Avalonia.Media;

namespace Gatherly.Windows.Views.Components;

/// <summary>
/// 轻量 inline-only Markdown 渲染组件 — 对齐 macOS MarkdownView.swift
/// 只支持：粗体、斜体、行内代码、链接。其他语法原样显示。
/// </summary>
public partial class MarkdownTextBlock : UserControl
{
    private static readonly Regex InlinePattern = new(
        @"(\*\*(.+?)\*\*)|(\*(.+?)\*)|(`(.+?)`)|(\[([^\]]+)\]\(([^)]+)\))",
        RegexOptions.Compiled);

    public MarkdownTextBlock()
    {
        var panel = new StackPanel { Spacing = 4 };
        Content = panel;
    }

    public void RenderMarkdown(string? text)
    {
        if (Content is not StackPanel panel) return;
        panel.Children.Clear();

        if (string.IsNullOrWhiteSpace(text))
        {
            var empty = new TextBlock
            {
                Text = "暂无正文内容",
                FontSize = 14,
                Foreground = new SolidColorBrush(Color.Parse("#999999"))
            };
            panel.Children.Add(empty);
            return;
        }

        var lines = text.Split('\n');
        foreach (var line in lines)
        {
            var linePanel = new StackPanel
            {
                Orientation = Avalonia.Layout.Orientation.Horizontal,
                Spacing = 0
            };

            ParseInline(line, linePanel);

            if (linePanel.Children.Count == 0)
                linePanel.Children.Add(new TextBlock { Text = " ", FontSize = 14 });

            panel.Children.Add(linePanel);
        }
    }

    private static void ParseInline(string text, StackPanel container)
    {
        var lastIndex = 0;

        foreach (Match match in InlinePattern.Matches(text))
        {
            if (match.Index > lastIndex)
            {
                var before = text[lastIndex..match.Index];
                AddTextRun(container, before, false, false, false);
            }

            if (match.Groups[1].Success)
            {
                AddTextRun(container, match.Groups[2].Value, true, false, false);
            }
            else if (match.Groups[3].Success)
            {
                AddTextRun(container, match.Groups[4].Value, false, true, false);
            }
            else if (match.Groups[5].Success)
            {
                AddCodeRun(container, match.Groups[6].Value);
            }
            else if (match.Groups[7].Success)
            {
                AddLinkRun(container, match.Groups[8].Value, match.Groups[9].Value);
            }

            lastIndex = match.Index + match.Length;
        }

        if (lastIndex < text.Length)
        {
            var remaining = text[lastIndex..];
            AddTextRun(container, remaining, false, false, false);
        }
    }

    private static void AddTextRun(StackPanel container, string text, bool bold, bool italic, bool code)
    {
        var tb = new TextBlock
        {
            Text = text,
            FontSize = 14,
            Foreground = new SolidColorBrush(Color.Parse("#333333"))
        };

        var weight = FontWeight.Normal;
        var style = FontStyle.Normal;

        if (bold) weight = FontWeight.Bold;
        if (italic) style = FontStyle.Italic;

        tb.FontWeight = weight;
        tb.FontStyle = style;

        container.Children.Add(tb);
    }

    private static void AddCodeRun(StackPanel container, string code)
    {
        var border = new Border
        {
            Background = new SolidColorBrush(Color.Parse("#F0F0F0")),
            CornerRadius = new Avalonia.CornerRadius(4),
            Padding = new Avalonia.Thickness(6, 2),
            Margin = new Avalonia.Thickness(2, 0),
            Child = new TextBlock
            {
                Text = code,
                FontSize = 13,
                FontFamily = new FontFamily("Consolas, Courier New, monospace"),
                Foreground = new SolidColorBrush(Color.Parse("#C7254E"))
            }
        };

        container.Children.Add(border);
    }

    private static void AddLinkRun(StackPanel container, string displayText, string url)
    {
        if (!url.StartsWith("http://") && !url.StartsWith("https://"))
        {
            AddTextRun(container, displayText, false, false, false);
            return;
        }

        var tb = new TextBlock
        {
            Text = displayText,
            FontSize = 14,
            Foreground = new SolidColorBrush(Color.Parse("#0066CC")),
            TextDecorations = TextDecorations.Underline,
            Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand)
        };

        tb.PointerPressed += (_, e) =>
        {
            if (e.GetCurrentPoint(tb).Properties.IsLeftButtonPressed)
            {
                try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true }); }
                catch { }
            }
        };

        container.Children.Add(tb);
    }
}
