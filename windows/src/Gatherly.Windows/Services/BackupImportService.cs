using System.IO.Compression;
using System.Text.Json;

namespace Gatherly.Windows.Services;

/// <summary>
/// 备份导入服务 — 从 macOS zip 备份包恢复数据
/// 本阶段仅支持恢复到空数据库
/// </summary>
public class BackupImportService
{
    private readonly DatabaseMergeService _mergeService;
    private readonly MediaRestoreService _mediaService;

    public BackupImportService()
    {
        _mergeService = new DatabaseMergeService();
        _mediaService = new MediaRestoreService();
    }

    /// <summary>
    /// 从 zip 备份包恢复数据到目标位置
    /// </summary>
    /// <param name="backupZipPath">zip 备份包路径</param>
    /// <param name="targetDatabasePath">目标数据库文件路径</param>
    /// <param name="targetDataDirectory">目标数据目录（media/platform_logos 的父目录）</param>
    public async Task ImportBackupAsync(string backupZipPath, string targetDatabasePath, string targetDataDirectory)
    {
        // 1. 验证 zip 文件存在
        if (!File.Exists(backupZipPath))
        {
            throw new FileNotFoundException("备份文件不存在", backupZipPath);
        }

        // 2. 解压到临时目录
        var tempDir = Path.Combine(Path.GetTempPath(), $"archiver_restore_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            ZipFile.ExtractToDirectory(backupZipPath, tempDir);

            // 3. 定位备份根目录（兼容 macOS archiver_backup_{UUID}/ 子目录）
            var backupRoot = LocateBackupRoot(tempDir);
            var dbFile = Path.Combine(backupRoot, "archiver.db");

            // 4. 可选读取 backup_info.json
            ReadBackupInfo(backupRoot);

            // 5. 合并数据库
            await _mergeService.MergeAsync(dbFile, targetDatabasePath);

            // 6. 恢复 media/
            _mediaService.RestoreMedia(backupRoot, targetDataDirectory);

            // 7. 恢复 platform_logos/
            _mediaService.RestorePlatformLogos(backupRoot, targetDataDirectory);
        }
        finally
        {
            // 8. 清理临时目录
            try
            {
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, true);
                }
            }
            catch
            {
                // 清理失败不影响主流程
            }
        }
    }

    private void ReadBackupInfo(string tempDir)
    {
        var infoFile = Path.Combine(tempDir, "backup_info.json");
        if (!File.Exists(infoFile)) return;

        try
        {
            var json = File.ReadAllText(infoFile);
            var doc = JsonDocument.Parse(json);
            // backup_info.json 信息可用于日志或验证，当前不做进一步处理
        }
        catch
        {
            // backup_info.json 解析失败不影响恢复
        }
    }

    /// <summary>
    /// 定位备份根目录。
    /// 支持两种 zip 结构：
    /// 1. 根目录直接有 archiver.db（旧格式/测试格式）
    /// 2. 根目录下 archiver_backup_{UUID}/ 子目录内有 archiver.db（macOS 真实导出格式）
    /// </summary>
    private static string LocateBackupRoot(string extractedRoot)
    {
        // 1. 根目录直接有 archiver.db
        if (File.Exists(Path.Combine(extractedRoot, "archiver.db")))
        {
            return extractedRoot;
        }

        // 2. 检查一级子目录，优先 archiver_backup_* 前缀
        var subDirs = Directory.GetDirectories(extractedRoot);
        string? backupDir = null;
        int matchCount = 0;

        foreach (var dir in subDirs)
        {
            var dirName = Path.GetFileName(dir);
            if (dirName.StartsWith("archiver_backup_", StringComparison.OrdinalIgnoreCase)
                && File.Exists(Path.Combine(dir, "archiver.db")))
            {
                backupDir = dir;
                matchCount++;
            }
        }

        // 找到 archiver_backup_* 且只有一匹配，直接返回
        if (backupDir != null && matchCount == 1)
        {
            return backupDir;
        }

        // 多个 archiver_backup_* 匹配时取第一个（实际几乎不会发生）
        if (backupDir != null)
        {
            return backupDir;
        }

        // 3. 无 archiver_backup_* 前缀时，遍历所有一级子目录
        foreach (var dir in subDirs)
        {
            // 跳过 __MACOSX 等系统目录
            if (Path.GetFileName(dir).StartsWith("__", StringComparison.Ordinal))
                continue;

            if (File.Exists(Path.Combine(dir, "archiver.db")))
            {
                return dir;
            }
        }

        // 4. 找不到
        throw new InvalidOperationException("备份中缺少数据库文件 archiver.db");
    }
}
