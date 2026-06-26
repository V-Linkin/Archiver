using Gatherly.Windows.Models.Enums;
using Gatherly.Windows.Services;
using Xunit;

namespace Gatherly.Windows.Tests.Services;

public class SystemPlatformDisplayNamesTests : IDisposable
{
    private readonly string _testDir;
    private readonly SystemPlatformDisplayNames _service;

    public SystemPlatformDisplayNamesTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "Gatherly.Tests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDir);
        _service = new SystemPlatformDisplayNames(_testDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
            Directory.Delete(_testDir, true);
    }

    [Fact]
    public void GetDisplayName_FileNotExist_ReturnsDefault()
    {
        Assert.Equal("YouTube", _service.GetDisplayName(Platform.youtube));
    }

    [Fact]
    public void SetDisplayName_GetDisplayName_ReturnsNewName()
    {
        _service.SetDisplayName(Platform.youtube, "我的影片");
        Assert.Equal("我的影片", _service.GetDisplayName(Platform.youtube));
    }

    [Fact]
    public void SetDisplayName_PersistsAcrossInstances()
    {
        _service.SetDisplayName(Platform.youtube, "我的影片");
        var service2 = new SystemPlatformDisplayNames(_testDir);
        Assert.Equal("我的影片", service2.GetDisplayName(Platform.youtube));
    }

    [Fact]
    public void SetDisplayName_DoesNotAffectOtherPlatforms()
    {
        _service.SetDisplayName(Platform.youtube, "我的影片");
        Assert.Equal("B站", _service.GetDisplayName(Platform.bilibili));
        Assert.Equal("GitHub", _service.GetDisplayName(Platform.github));
    }

    [Fact]
    public void ResetDisplayName_RestoresDefault()
    {
        _service.SetDisplayName(Platform.youtube, "我的影片");
        _service.ResetDisplayName(Platform.youtube);
        Assert.Equal("YouTube", _service.GetDisplayName(Platform.youtube));
    }

    [Fact]
    public void SetDisplayName_TrimsWhitespace()
    {
        _service.SetDisplayName(Platform.youtube, "  我的影片  ");
        Assert.Equal("我的影片", _service.GetDisplayName(Platform.youtube));
    }

    [Fact]
    public void SetDisplayName_EmptyString_Throws()
    {
        Assert.Throws<ArgumentException>(() => _service.SetDisplayName(Platform.youtube, ""));
        Assert.Throws<ArgumentException>(() => _service.SetDisplayName(Platform.youtube, "   "));
    }

    [Fact]
    public void SetDisplayName_CustomPlatform_Throws()
    {
        Assert.Throws<ArgumentException>(() => _service.SetDisplayName(Platform.custom, "测试"));
    }

    [Fact]
    public void ResetDisplayName_CustomPlatform_Throws()
    {
        Assert.Throws<ArgumentException>(() => _service.ResetDisplayName(Platform.custom));
    }

    [Fact]
    public void InvalidJson_GetDisplayName_ReturnsDefault()
    {
        File.WriteAllText(Path.Combine(_testDir, "platform_display_names.json"), "not json!!!");
        var service = new SystemPlatformDisplayNames(_testDir);
        Assert.Equal("YouTube", service.GetDisplayName(Platform.youtube));
    }

    [Fact]
    public void InvalidJson_ThenSet_RecoversToFile()
    {
        File.WriteAllText(Path.Combine(_testDir, "platform_display_names.json"), "not json!!!");
        var service = new SystemPlatformDisplayNames(_testDir);
        service.SetDisplayName(Platform.youtube, "我的影片");
        var service2 = new SystemPlatformDisplayNames(_testDir);
        Assert.Equal("我的影片", service2.GetDisplayName(Platform.youtube));
    }

    [Fact]
    public void UnknownJsonKey_IsIgnored()
    {
        File.WriteAllText(Path.Combine(_testDir, "platform_display_names.json"), "{\"unknown\":\"test\",\"youtube\":\"我的影片\"}");
        var service = new SystemPlatformDisplayNames(_testDir);
        Assert.Equal("我的影片", service.GetDisplayName(Platform.youtube));
    }

    [Fact]
    public void SetDisplayName_CreatesDirectoryAndFile()
    {
        var subDir = Path.Combine(_testDir, "sub", "dir");
        var service = new SystemPlatformDisplayNames(subDir);
        service.SetDisplayName(Platform.youtube, "我的影片");
        Assert.True(File.Exists(Path.Combine(subDir, "platform_display_names.json")));
    }

    [Fact]
    public void JsonKey_UsesRawValue()
    {
        _service.SetDisplayName(Platform.youtube, "我的影片");
        var json = File.ReadAllText(Path.Combine(_testDir, "platform_display_names.json"));
        Assert.Contains("\"youtube\"", json);
    }

    [Fact]
    public void ConcurrentSetDifferentPlatforms_NoDataLoss()
    {
        var tasks = new List<Task>();
        for (int i = 0; i < 10; i++)
        {
            tasks.Add(Task.Run(() => _service.SetDisplayName(Platform.youtube, $"YouTube_{i}")));
            tasks.Add(Task.Run(() => _service.SetDisplayName(Platform.bilibili, $"Bilibili_{i}")));
            tasks.Add(Task.Run(() => _service.SetDisplayName(Platform.github, $"GitHub_{i}")));
        }
        Task.WaitAll(tasks.ToArray());
        var lastYoutube = _service.GetDisplayName(Platform.youtube);
        Assert.StartsWith("YouTube_", lastYoutube);
    }

    [Fact]
    public void ResetOne_Platform_DoesNotAffectOthers()
    {
        _service.SetDisplayName(Platform.youtube, "我的影片");
        _service.SetDisplayName(Platform.bilibili, "哔哩");
        _service.ResetDisplayName(Platform.youtube);
        Assert.Equal("YouTube", _service.GetDisplayName(Platform.youtube));
        Assert.Equal("哔哩", _service.GetDisplayName(Platform.bilibili));
    }
}
