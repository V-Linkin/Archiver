using System.Diagnostics;

namespace Gatherly.Windows.Services;

/// <summary>
/// 外部链接打开结果
/// </summary>
public enum OpenExternalLinkResult
{
    Success,
    InvalidUrl,
    UnsupportedScheme,
    Failed
}

/// <summary>
/// 外部进程启动器接口 — 用于测试隔离
/// </summary>
public interface IExternalProcessLauncher
{
    void Open(Uri uri);
}

/// <summary>
/// 系统默认浏览器启动器
/// </summary>
public sealed class SystemExternalProcessLauncher : IExternalProcessLauncher
{
    public void Open(Uri uri)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = uri.AbsoluteUri,
            UseShellExecute = true
        });
    }
}

/// <summary>
/// 外部链接服务 — 使用系统默认浏览器打开 URL
/// </summary>
public interface IExternalLinkService
{
    OpenExternalLinkResult Open(string? url);
}

/// <summary>
/// 外部链接服务实现
/// </summary>
public sealed class ExternalLinkService : IExternalLinkService
{
    private readonly IExternalProcessLauncher _launcher;

    public ExternalLinkService()
        : this(new SystemExternalProcessLauncher())
    {
    }

    public ExternalLinkService(IExternalProcessLauncher launcher)
    {
        _launcher = launcher;
    }

    public OpenExternalLinkResult Open(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return OpenExternalLinkResult.InvalidUrl;

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return OpenExternalLinkResult.InvalidUrl;

        // 只允许 http/https
        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            return OpenExternalLinkResult.UnsupportedScheme;

        try
        {
            _launcher.Open(uri);
            return OpenExternalLinkResult.Success;
        }
        catch
        {
            return OpenExternalLinkResult.Failed;
        }
    }
}
