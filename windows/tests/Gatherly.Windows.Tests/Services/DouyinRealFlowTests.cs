using Gatherly.Windows.Models.Enums;
using Gatherly.Windows.Services.Parsers;
using Gatherly.Windows.Services.Url;
using Xunit;
using Xunit.Abstractions;

namespace Gatherly.Windows.Tests.Services;

public class DouyinRealFlowTests
{
    private static readonly HttpClient SharedHttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(15),
        DefaultRequestHeaders =
        {
            { "User-Agent", "Mozilla/5.0 (iPhone; CPU iPhone OS 16_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/16.0 Mobile/15E148 Safari/604.1" },
            { "Referer", "https://www.douyin.com/" }
        }
    };
    private readonly ITestOutputHelper _output;

    public DouyinRealFlowTests(ITestOutputHelper output) { _output = output; }

    [Fact]
    public async Task ParseAsync_RealVideoNote_ShouldExtractContent()
    {
        var url = "https://v.douyin.com/-baq4Gfb7Mk/";
        var parser = new DouyinParser();
        var request = new ParseRequest
        {
            Url = url,
            NormalizedUrl = UrlNormalizer.Normalize(url, Platform.douyin),
            Platform = Platform.douyin,
            PlatformContentId = UrlNormalizer.ExtractContentId(url, Platform.douyin)
        };

        _output.WriteLine($"Calling ParseAsync with URL: {url}");
        var result = await parser.ParseAsync(request);

        _output.WriteLine($"Status: {result.Status}");
        _output.WriteLine($"ErrorMessage: {result.ErrorMessage}");

        if (result.Content != null)
        {
            _output.WriteLine($"Title: [{result.Content.Title}]");
            _output.WriteLine($"Body: [{result.Content.Body?.Substring(0, Math.Min(100, result.Content.Body?.Length ?? 0))}]");
            _output.WriteLine($"Author: [{result.Content.Author}]");
            _output.WriteLine($"CoverUrl: [{result.Content.CoverUrl}]");
            _output.WriteLine($"ImageUrls: {result.Content.ImageUrls.Count}");
            _output.WriteLine($"VideoUrl: [{result.Content.VideoUrl}]");
        }

        // Should succeed with content
        Assert.Equal(ParseStatus.Success, result.Status);
        Assert.NotNull(result.Content);
    }

    [Fact]
    public async Task ParseAsync_RealImageNote_ShouldExtractContent()
    {
        var url = "https://v.douyin.com/i92QTmubd7Q/";
        var parser = new DouyinParser();
        var request = new ParseRequest
        {
            Url = url,
            NormalizedUrl = UrlNormalizer.Normalize(url, Platform.douyin),
            Platform = Platform.douyin,
            PlatformContentId = UrlNormalizer.ExtractContentId(url, Platform.douyin)
        };

        _output.WriteLine($"Calling ParseAsync with URL: {url}");
        var result = await parser.ParseAsync(request);

        _output.WriteLine($"Status: {result.Status}");
        _output.WriteLine($"ErrorMessage: {result.ErrorMessage}");

        if (result.Content != null)
        {
            _output.WriteLine($"Title: [{result.Content.Title}]");
            _output.WriteLine($"Body: [{result.Content.Body?.Substring(0, Math.Min(100, result.Content.Body?.Length ?? 0))}]");
            _output.WriteLine($"Author: [{result.Content.Author}]");
            _output.WriteLine($"ImageUrls: {result.Content.ImageUrls.Count}");
        }

        Assert.Equal(ParseStatus.Success, result.Status);
        Assert.NotNull(result.Content);
    }

    [Fact]
    public async Task FetchAndParse_FullDiagnostic()
    {
        // 1. Fetch HTML directly
        var url = "https://v.douyin.com/-baq4Gfb7Mk/";
        _output.WriteLine($"=== Fetching {url} ===");
        var response = await SharedHttpClient.GetAsync(url);
        var html = await response.Content.ReadAsStringAsync();
        _output.WriteLine($"HTML length: {html.Length}");
        _output.WriteLine($"Has _ROUTER_DATA: {html.Contains("window._ROUTER_DATA")}");

        // 2. Test regex
        var routerMatch = DouyinParser.RouterDataPatternForTest(html);
        _output.WriteLine($"Regex match: {routerMatch.Success}");

        if (routerMatch.Success)
        {
            var jsonStr = routerMatch.Groups[1].Value.Trim();
            if (jsonStr.EndsWith(";"))
                jsonStr = jsonStr.Substring(0, jsonStr.Length - 1);
            _output.WriteLine($"JSON length: {jsonStr.Length}");

            // 3. Parse JSON
            var doc = System.Text.Json.JsonDocument.Parse(jsonStr);
            var root = doc.RootElement;

            if (root.TryGetProperty("loaderData", out var loaderData))
            {
                _output.WriteLine("loaderData keys:");
                foreach (var prop in loaderData.EnumerateObject())
                    _output.WriteLine($"  {prop.Name} ({prop.Value.ValueKind})");
            }
        }

        // 4. Now call ParseAsync and compare
        _output.WriteLine("\n=== Calling ParseAsync ===");
        var parser = new DouyinParser();
        var request = new ParseRequest
        {
            Url = url,
            NormalizedUrl = UrlNormalizer.Normalize(url, Platform.douyin),
            Platform = Platform.douyin,
            PlatformContentId = UrlNormalizer.ExtractContentId(url, Platform.douyin)
        };
        var result = await parser.ParseAsync(request);
        _output.WriteLine($"ParseAsync Status: {result.Status}");
        _output.WriteLine($"ParseAsync Title: [{result.Content?.Title}]");
        _output.WriteLine($"ParseAsync Author: [{result.Content?.Author}]");

        Assert.Equal(ParseStatus.Success, result.Status);
        Assert.NotNull(result.Content);
    }
}
