using Gatherly.Windows.Services;
using Xunit;

namespace Gatherly.Windows.Tests.Services;

/// <summary>
/// Fake 外部进程启动器 — 不打开真实浏览器
/// </summary>
public sealed class FakeExternalProcessLauncher : IExternalProcessLauncher
{
    public List<Uri> OpenedUris { get; } = [];
    public Exception? ExceptionToThrow { get; set; }

    public void Open(Uri uri)
    {
        if (ExceptionToThrow is not null)
            throw ExceptionToThrow;

        OpenedUris.Add(uri);
    }
}

public class ExternalLinkServiceTests
{
    private readonly FakeExternalProcessLauncher _launcher = new();
    private readonly ExternalLinkService _service;

    public ExternalLinkServiceTests()
    {
        _service = new ExternalLinkService(_launcher);
    }

    [Fact]
    public void Open_HttpsUrl_CallsLauncherOnce()
    {
        var result = _service.Open("https://example.com");
        Assert.Equal(OpenExternalLinkResult.Success, result);
        Assert.Single(_launcher.OpenedUris);
        Assert.Equal("https://example.com/", _launcher.OpenedUris[0].AbsoluteUri);
    }

    [Fact]
    public void Open_HttpUrl_CallsLauncherOnce()
    {
        var result = _service.Open("http://example.com");
        Assert.Equal(OpenExternalLinkResult.Success, result);
        Assert.Single(_launcher.OpenedUris);
        Assert.Equal("http://example.com/", _launcher.OpenedUris[0].AbsoluteUri);
    }

    [Fact]
    public void Open_JavascriptScheme_DoesNotCallLauncher()
    {
        var result = _service.Open("javascript:alert('xss')");
        Assert.Equal(OpenExternalLinkResult.UnsupportedScheme, result);
        Assert.Empty(_launcher.OpenedUris);
    }

    [Fact]
    public void Open_FileScheme_DoesNotCallLauncher()
    {
        var result = _service.Open("file:///C:/test.txt");
        Assert.Equal(OpenExternalLinkResult.UnsupportedScheme, result);
        Assert.Empty(_launcher.OpenedUris);
    }

    [Fact]
    public void Open_DataScheme_DoesNotCallLauncher()
    {
        var result = _service.Open("data:text/html,<h1>test</h1>");
        Assert.Equal(OpenExternalLinkResult.UnsupportedScheme, result);
        Assert.Empty(_launcher.OpenedUris);
    }

    [Fact]
    public void Open_FtpScheme_DoesNotCallLauncher()
    {
        var result = _service.Open("ftp://example.com/file.txt");
        Assert.Equal(OpenExternalLinkResult.UnsupportedScheme, result);
        Assert.Empty(_launcher.OpenedUris);
    }

    [Fact]
    public void Open_RelativeUrl_DoesNotCallLauncher()
    {
        var result = _service.Open("/path/to/page");
        Assert.Equal(OpenExternalLinkResult.InvalidUrl, result);
        Assert.Empty(_launcher.OpenedUris);
    }

    [Fact]
    public void Open_InvalidUrl_DoesNotCallLauncher()
    {
        var result = _service.Open("not a url");
        Assert.Equal(OpenExternalLinkResult.InvalidUrl, result);
        Assert.Empty(_launcher.OpenedUris);
    }

    [Fact]
    public void Open_Null_DoesNotCallLauncher()
    {
        var result = _service.Open(null);
        Assert.Equal(OpenExternalLinkResult.InvalidUrl, result);
        Assert.Empty(_launcher.OpenedUris);
    }

    [Fact]
    public void Open_EmptyString_DoesNotCallLauncher()
    {
        var result = _service.Open("");
        Assert.Equal(OpenExternalLinkResult.InvalidUrl, result);
        Assert.Empty(_launcher.OpenedUris);
    }

    [Fact]
    public void Open_WhitespaceString_DoesNotCallLauncher()
    {
        var result = _service.Open("   ");
        Assert.Equal(OpenExternalLinkResult.InvalidUrl, result);
        Assert.Empty(_launcher.OpenedUris);
    }

    [Fact]
    public void Open_LauncherThrows_ReturnsFailed()
    {
        _launcher.ExceptionToThrow = new InvalidOperationException("test");
        var result = _service.Open("https://example.com");
        Assert.Equal(OpenExternalLinkResult.Failed, result);
        Assert.Empty(_launcher.OpenedUris);
    }

    [Fact]
    public void Open_SingleClick_CallsLauncherOnce()
    {
        _service.Open("https://example.com");
        _service.Open("https://example.com");
        Assert.Equal(2, _launcher.OpenedUris.Count);
    }
}
