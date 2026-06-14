namespace Gatherly.Windows.Services;

/// <summary>
/// 正文片段 — 普通文本或链接
/// </summary>
public sealed record ContentSegment(
    string Text,
    bool IsLink,
    string? Url = null);
