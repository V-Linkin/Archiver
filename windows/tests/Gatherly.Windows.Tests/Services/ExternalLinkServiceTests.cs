using Gatherly.Windows.Services;
using Xunit;

namespace Gatherly.Windows.Tests.Services;

public class ExternalLinkServiceTests
{
    private readonly ExternalLinkService _service = new();

    [Fact]
    public void Open_HttpsUrl_ReturnsSuccess()
    {
        var result = _service.Open("https://example.com");
        Assert.Equal(OpenExternalLinkResult.Success, result);
    }

    [Fact]
    public void Open_HttpUrl_ReturnsSuccess()
    {
        var result = _service.Open("http://example.com");
        Assert.Equal(OpenExternalLinkResult.Success, result);
    }

    [Fact]
    public void Open_JavascriptScheme_ReturnsUnsupported()
    {
        var result = _service.Open("javascript:alert('xss')");
        Assert.Equal(OpenExternalLinkResult.UnsupportedScheme, result);
    }

    [Fact]
    public void Open_FileScheme_ReturnsUnsupported()
    {
        var result = _service.Open("file:///C:/test.txt");
        Assert.Equal(OpenExternalLinkResult.UnsupportedScheme, result);
    }

    [Fact]
    public void Open_DataScheme_ReturnsUnsupported()
    {
        var result = _service.Open("data:text/html,<h1>test</h1>");
        Assert.Equal(OpenExternalLinkResult.UnsupportedScheme, result);
    }

    [Fact]
    public void Open_RelativeUrl_ReturnsInvalid()
    {
        var result = _service.Open("/path/to/page");
        Assert.Equal(OpenExternalLinkResult.InvalidUrl, result);
    }

    [Fact]
    public void Open_InvalidUrl_ReturnsInvalid()
    {
        var result = _service.Open("not a url");
        Assert.Equal(OpenExternalLinkResult.InvalidUrl, result);
    }

    [Fact]
    public void Open_Null_ReturnsInvalid()
    {
        var result = _service.Open(null);
        Assert.Equal(OpenExternalLinkResult.InvalidUrl, result);
    }

    [Fact]
    public void Open_EmptyString_ReturnsInvalid()
    {
        var result = _service.Open("");
        Assert.Equal(OpenExternalLinkResult.InvalidUrl, result);
    }

    [Fact]
    public void Open_WhitespaceString_ReturnsInvalid()
    {
        var result = _service.Open("   ");
        Assert.Equal(OpenExternalLinkResult.InvalidUrl, result);
    }
}
