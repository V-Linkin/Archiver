using Gatherly.Windows.Database;
using Gatherly.Windows.Models;
using Gatherly.Windows.Models.Enums;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Gatherly.Windows.Tests;

public class ModelMappingTests
{
    [Fact]
    public void Platform_RawValue_MatchesContract()
    {
        Assert.Equal("douyin", Platform.douyin.ToRawValue());
        Assert.Equal("xiaohongshu", Platform.xiaohongshu.ToRawValue());
        Assert.Equal("coolapk", Platform.coolapk.ToRawValue());
        Assert.Equal("bilibili", Platform.bilibili.ToRawValue());
        Assert.Equal("github", Platform.github.ToRawValue());
        Assert.Equal("youtube", Platform.youtube.ToRawValue());
        Assert.Equal("x", Platform.x.ToRawValue());
        Assert.Equal("weibo", Platform.weibo.ToRawValue());
        Assert.Equal("zhihu", Platform.zhihu.ToRawValue());
        Assert.Equal("douban", Platform.douban.ToRawValue());
        Assert.Equal("custom", Platform.custom.ToRawValue());
    }

    [Fact]
    public void Platform_FromRawValue_RoundTrips()
    {
        foreach (var platform in Enum.GetValues<Platform>())
        {
            var raw = platform.ToRawValue();
            var parsed = PlatformExtensions.FromRawValue(raw);
            Assert.Equal(platform, parsed);
        }
    }

    [Fact]
    public void ArchiveStatus_RawValue_MatchesContract()
    {
        Assert.Equal("favorite", ArchiveStatus.favorite.ToRawValue());
        Assert.Equal("inspiration", ArchiveStatus.inspiration.ToRawValue());
        Assert.Equal("pending", ArchiveStatus.pending.ToRawValue());
        Assert.Equal("archived", ArchiveStatus.archived.ToRawValue());
    }

    [Fact]
    public void DateTimeOffset_UnixConversion()
    {
        var now = DateTimeOffset.UtcNow;
        var unixSeconds = now.ToUnixTimeSeconds();
        var restored = DateTimeOffset.FromUnixTimeSeconds(unixSeconds);
        Assert.Equal(unixSeconds, restored.ToUnixTimeSeconds());
    }
}
