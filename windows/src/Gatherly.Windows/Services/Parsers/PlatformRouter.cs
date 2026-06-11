using Gatherly.Windows.Models.Enums;

namespace Gatherly.Windows.Services.Parsers;

/// <summary>
/// 平台路由器 — 根据平台返回对应 Parser
/// 当前只有 GitHub 有真实 Parser，其它平台返回 NotImplementedParser
/// </summary>
public class PlatformRouter
{
    private readonly NotImplementedParser _notImplemented = new();
    private readonly GitHubParser _github = new();

    public IContentParser GetParser(Platform platform, string url)
    {
        if (platform == Platform.github)
            return _github;

        return _notImplemented;
    }
}
