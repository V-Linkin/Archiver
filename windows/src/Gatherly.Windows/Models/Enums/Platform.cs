namespace Gatherly.Windows.Models.Enums;

/// <summary>
/// 内容来源平台 — rawValue 与 shared/model/enums.json 完全一致
/// 数据库存储为 TEXT string
/// </summary>
public enum Platform
{
    douyin,
    xiaohongshu,
    coolapk,
    bilibili,
    github,
    youtube,
    x,
    weibo,
    zhihu,
    douban,
    custom
}

public static class PlatformExtensions
{
    public static string ToRawValue(this Platform p) => p.ToString();

    public static Platform FromRawValue(string value) =>
        Enum.TryParse<Platform>(value, true, out var result) ? result : Platform.custom;

    public static string GetDisplayName(this Platform p) => p switch
    {
        Platform.douyin => "抖音",
        Platform.xiaohongshu => "小红书",
        Platform.coolapk => "酷安",
        Platform.bilibili => "B站",
        Platform.github => "GitHub",
        Platform.youtube => "YouTube",
        Platform.x => "X",
        Platform.weibo => "微博",
        Platform.zhihu => "知乎",
        Platform.douban => "豆瓣",
        Platform.custom => "自定义",
        _ => p.ToString()
    };
}
