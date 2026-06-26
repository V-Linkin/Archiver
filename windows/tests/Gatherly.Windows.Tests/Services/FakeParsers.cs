using Gatherly.Windows.Models;
using Gatherly.Windows.Models.Enums;
using Gatherly.Windows.Services.Parsers;

namespace Gatherly.Windows.Tests.Services;

public class FakeContentParser : IContentParser
{
    public bool CanParse(Platform platform, string url) => true;
    public Platform Platform { get; set; } = Platform.youtube;
    public string Title { get; set; } = "Fake Title";
    public string Author { get; set; } = "Fake Author";
    public string Body { get; set; } = "Fake body content";
    public string? CoverUrl { get; set; } = null;
    public string? VideoUrl { get; set; } = null;
    public List<string> ImageUrls { get; set; } = new();
    public string? PlatformContentId { get; set; } = "fake-id-123";

    public bool ShouldFail { get; set; }
    public string? FailureReason { get; set; }
    public bool ShouldThrowNotImplemented { get; set; }

    public Task<ParseResult> ParseAsync(ParseRequest request, CancellationToken cancellationToken = default)
    {
        if (ShouldThrowNotImplemented)
            return Task.FromResult(new ParseResult { Status = ParseStatus.NotImplemented });

        if (ShouldFail)
            return Task.FromResult(new ParseResult
            {
                Status = ParseStatus.Failed,
                ErrorMessage = FailureReason ?? "Fake parse failure"
            });

        // Use URL-derived fields if not explicitly set
        var title = Title;
        var author = Author;
        if (!string.IsNullOrEmpty(request.Url))
        {
            var uri = new Uri(request.Url);
            var path = uri.AbsolutePath.TrimStart('/').Replace("-", " ");
            
            // For GitHub-style URLs, extract owner as author
            if (Platform == Platform.github && path.Contains('/'))
            {
                author = path.Split('/')[0];
                title = path;
            }
            
            if (title == "Fake Title" || string.IsNullOrEmpty(title))
            {
                title = string.IsNullOrEmpty(path) ? "Fake Title" : path;
            }
        }

        return Task.FromResult(new ParseResult
        {
            Status = ParseStatus.Success,
            Content = new ParsedContent
            {
                Title = title,
                Author = author,
                Body = Body,
                CoverUrl = CoverUrl,
                VideoUrl = VideoUrl,
                ImageUrls = ImageUrls,
                PlatformContentId = PlatformContentId,
                OriginalUrl = request.Url,
                NormalizedUrl = request.NormalizedUrl
            }
        });
    }

    public Task<List<MediaAsset>> DownloadMedia(ParsedContent content, Guid itemId, string mediaDir, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new List<MediaAsset>());
    }
}

public class FakePlatformRouter : PlatformRouter
{
    private readonly Dictionary<Platform, FakeContentParser> _parsers = new();

    public void RegisterParser(Platform platform, FakeContentParser parser)
    {
        _parsers[platform] = parser;
    }

    public override IContentParser GetParser(Platform platform, string url)
    {
        if (_parsers.TryGetValue(platform, out var fake))
            return fake;
        return base.GetParser(platform, url);
    }
}
