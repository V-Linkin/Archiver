namespace Gatherly.Windows.Database;

/// <summary>
/// 数据目录迁移计划（dry-run 模式，只生成计划不执行）
/// </summary>
public class DataDirectoryMigrationPlan
{
    public string CurrentDirectory { get; set; } = "";
    public string TargetDirectory { get; set; } = "";
    public bool IsSameDirectory { get; set; }
    public bool TargetExists { get; set; }
    public bool TargetEmpty { get; set; }
    public bool TargetWritable { get; set; }
    public long DbFileSize { get; set; }
    public int MediaFileCount { get; set; }
    public long MediaTotalSize { get; set; }
    public int LogosFileCount { get; set; }
    public long LogosTotalSize { get; set; }
    public int EstimatedFileCount { get; set; }
    public long EstimatedTotalSize { get; set; }
    public bool NeedsRestart { get; set; }
    public bool IsMigratable { get; set; }
    public List<string> Warnings { get; set; } = new();
    public List<string> Errors { get; set; } = new();

    public static DataDirectoryMigrationPlan Generate(string targetDir)
    {
        var plan = new DataDirectoryMigrationPlan
        {
            CurrentDirectory = DataDirectoryConfig.DataDirectory,
            TargetDirectory = targetDir
        };

        plan.IsSameDirectory = string.Equals(
            Path.GetFullPath(plan.CurrentDirectory),
            Path.GetFullPath(plan.TargetDirectory),
            StringComparison.OrdinalIgnoreCase);

        if (plan.IsSameDirectory)
        {
            plan.Errors.Add("目标目录与当前目录相同，无需迁移");
            plan.IsMigratable = false;
            return plan;
        }

        plan.TargetExists = Directory.Exists(targetDir);
        plan.TargetEmpty = !plan.TargetExists || !Directory.EnumerateFileSystemEntries(targetDir).Any();

        // Check writable
        try
        {
            var testDir = plan.TargetExists ? targetDir : Path.GetDirectoryName(targetDir)!;
            Directory.CreateDirectory(testDir);
            var testFile = Path.Combine(testDir, ".write_test");
            File.WriteAllText(testFile, "test");
            File.Delete(testFile);
            plan.TargetWritable = true;
        }
        catch { plan.TargetWritable = false; plan.Errors.Add("目标目录不可写"); }

        if (!plan.TargetExists && !plan.TargetWritable)
        {
            plan.Errors.Add("目标目录不存在且无法创建");
            plan.IsMigratable = false;
            return plan;
        }

        if (plan.TargetExists && !plan.TargetEmpty)
        {
            plan.Warnings.Add("目标目录非空，可能与现有文件冲突");
        }

        // Scan current directory
        var currentDir = plan.CurrentDirectory;

        // DB file
        var dbFile = Path.Combine(currentDir, "Gatherly.db");
        plan.DbFileSize = File.Exists(dbFile) ? new FileInfo(dbFile).Length : 0;

        // Media files
        var mediaDir = Path.Combine(currentDir, "media");
        if (Directory.Exists(mediaDir))
        {
            var mediaFiles = new DirectoryInfo(mediaDir).EnumerateFiles("*", SearchOption.AllDirectories).ToList();
            plan.MediaFileCount = mediaFiles.Count;
            plan.MediaTotalSize = mediaFiles.Sum(f => f.Length);
        }

        // Logos
        var logosDir = Path.Combine(currentDir, "platform_logos");
        if (Directory.Exists(logosDir))
        {
            var logoFiles = new DirectoryInfo(logosDir).EnumerateFiles("*", SearchOption.AllDirectories).ToList();
            plan.LogosFileCount = logoFiles.Count;
            plan.LogosTotalSize = logoFiles.Sum(f => f.Length);
        }

        plan.EstimatedFileCount = 1 + plan.MediaFileCount + plan.LogosFileCount; // +1 for DB
        plan.EstimatedTotalSize = plan.DbFileSize + plan.MediaTotalSize + plan.LogosTotalSize;
        plan.NeedsRestart = true;

        plan.IsMigratable = plan.Errors.Count == 0;
        return plan;
    }
}
