namespace Gatherly.Windows.Services.Parsers;

/// <summary>
/// 内容解析器接口
/// </summary>
public interface IContentParser
{
    bool CanParse(Models.Enums.Platform platform, string url);
    Task<ParseResult> ParseAsync(ParseRequest request, CancellationToken cancellationToken = default);
}
