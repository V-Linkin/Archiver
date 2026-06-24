using Gatherly.Windows.Models.Enums;

namespace Gatherly.Windows.Services.Parsers;

/// <summary>
/// 平台路由器 — 根据平台返回对应 Parser
/// 当前 GitHub + Bilibili + YouTube + Douyin 有真实 Parser，其它平台返回 NotImplementedParser
/// </summary>
public class PlatformRouter
{
    private readonly NotImplementedParser _notImplemented = new();
    private readonly GitHubParser _github = new();
    private readonly BilibiliParser _bilibili = new();
    private readonly YouTubeParser _youtube = new();
    private readonly DouyinParser _douyin = new();
    private readonly XiaohongshuParser _xiaohongshu = new();
    private readonly CoolapkParser _coolapk = new();
    private readonly WeiboParser _weibo = new();

    public virtual IContentParser GetParser(Platform platform, string url)
    {
        if (platform == Platform.github)
            return _github;
        if (platform == Platform.bilibili)
            return _bilibili;
        if (platform == Platform.youtube)
            return _youtube;
        if (platform == Platform.douyin)
            return _douyin;
        if (platform == Platform.xiaohongshu)
            return _xiaohongshu;
        if (platform == Platform.coolapk)
            return _coolapk;
        if (platform == Platform.weibo)
            return _weibo;

        return _notImplemented;
    }
}
