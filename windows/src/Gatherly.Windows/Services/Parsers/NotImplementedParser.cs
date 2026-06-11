using Gatherly.Windows.Models.Enums;

namespace Gatherly.Windows.Services.Parsers;

/// <summary>
/// 未实现的解析器占位 — 当前所有平台统一返回 NotImplemented
/// </summary>
public class NotImplementedParser : IContentParser
{
    public bool CanParse(Platform platform, string url) => true;

    public Task<ParseResult> ParseAsync(ParseRequest request, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(ParseResult.NotImpl);
    }
}
