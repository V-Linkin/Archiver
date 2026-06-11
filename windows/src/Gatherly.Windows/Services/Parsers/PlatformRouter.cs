using Gatherly.Windows.Models.Enums;

namespace Gatherly.Windows.Services.Parsers;

/// <summary>
/// 平台路由器 — 根据平台返回对应 Parser
/// 当前所有平台返回 NotImplementedParser，后续 Phase 7D 逐个接入真实 Parser
/// </summary>
public class PlatformRouter
{
    private readonly NotImplementedParser _notImplemented = new();

    public IContentParser GetParser(Platform platform, string url)
    {
        // Phase 7D 开始逐个接入真实 Parser
        // if (platform == Platform.github) return new GitHubParser();
        // if (platform == Platform.bilibili) return new BilibiliParser();

        return _notImplemented;
    }
}
