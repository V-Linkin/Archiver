namespace Gatherly.Windows.Services;

/// <summary>
/// 媒体恢复服务 — 从备份目录复制 media/ 和 platform_logos/ 到目标数据目录
/// </summary>
public class MediaRestoreService
{
    /// <summary>
    /// 恢复 media/ 目录
    /// </summary>
    public void RestoreMedia(string sourceDir, string targetDataDir)
    {
        var sourceMedia = Path.Combine(sourceDir, "media");
        var targetMedia = Path.Combine(targetDataDir, "media");

        if (!Directory.Exists(sourceMedia)) return;

        CopyDirectoryIfNotExists(sourceMedia, targetMedia);
    }

    /// <summary>
    /// 恢复 platform_logos/ 目录
    /// </summary>
    public void RestorePlatformLogos(string sourceDir, string targetDataDir)
    {
        var sourceLogos = Path.Combine(sourceDir, "platform_logos");
        var targetLogos = Path.Combine(targetDataDir, "platform_logos");

        if (!Directory.Exists(sourceLogos)) return;

        CopyDirectoryIfNotExists(sourceLogos, targetLogos);
    }

    private void CopyDirectoryIfNotExists(string source, string target)
    {
        if (!Directory.Exists(target))
        {
            Directory.CreateDirectory(target);
        }

        foreach (var file in Directory.GetFiles(source))
        {
            var fileName = Path.GetFileName(file);
            var destFile = Path.Combine(target, fileName);

            if (!File.Exists(destFile))
            {
                File.Copy(file, destFile);
            }
        }

        foreach (var dir in Directory.GetDirectories(source))
        {
            var dirName = Path.GetFileName(dir);
            var destDir = Path.Combine(target, dirName);

            CopyDirectoryIfNotExists(dir, destDir);
        }
    }
}
