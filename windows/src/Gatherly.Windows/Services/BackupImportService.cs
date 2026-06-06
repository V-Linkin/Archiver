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

            // 3. 验证 archiver.db 存在
            var dbFile = Path.Combine(tempDir, "archiver.db");
            if (!File.Exists(dbFile))
            {
                throw new InvalidOperationException("备份中缺少数据库文件 archiver.db");
            }

            // 4. 可选读取 backup_info.json
            ReadBackupInfo(tempDir);

            // 5. 合并数据库
            await _mergeService.MergeAsync(dbFile, targetDatabasePath);

            // 6. 恢复 media/
            _mediaService.RestoreMedia(tempDir, targetDataDirectory);

            // 7. 恢复 platform_logos/
            _mediaService.RestorePlatformLogos(tempDir, targetDataDirectory);
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
}
