using System;
using System.IO;

namespace Gatherly.Windows.Database;

/// <summary>
/// 跨平台数据库路径管理
/// </summary>
public static class DatabasePaths
{
    /// <summary>
    /// 获取数据目录路径
    /// Windows: %LOCALAPPDATA%/Gatherly/
    /// macOS: ~/Library/Application Support/Gatherly/
    /// </summary>
    public static string DataDirectory
    {
        get
        {
            var baseDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(baseDir, "Gatherly");
        }
    }

    /// <summary>
    /// 数据库文件完整路径
    /// </summary>
    public static string DatabaseFile => Path.Combine(DataDirectory, "Gatherly.db");

    /// <summary>
    /// SQL migration 文件所在目录
    /// 优先查找输出目录，回退到仓库根目录的 shared/db/migrations/
    /// </summary>
    public static string MigrationsDirectory
    {
        get
        {
            // 优先：输出目录下的 Database/Migrations/
            var appDir = AppDomain.CurrentDomain.BaseDirectory;
            var outputMigrations = Path.Combine(appDir, "Database", "Migrations");
            if (Directory.Exists(outputMigrations) && Directory.GetFiles(outputMigrations, "*.sql").Length > 0)
            {
                return outputMigrations;
            }

            // 回退：从当前目录向上查找仓库根目录的 shared/db/migrations/
            var dir = appDir;
            for (int i = 0; i < 10; i++)
            {
                dir = Path.GetDirectoryName(dir);
                if (dir == null) break;
                
                var sharedMigrations = Path.Combine(dir, "shared", "db", "migrations");
                if (Directory.Exists(sharedMigrations) && Directory.GetFiles(sharedMigrations, "*.sql").Length > 0)
                {
                    return sharedMigrations;
                }
            }

            return outputMigrations; // 返回默认路径，让 MigrationRunner 报出清晰错误
        }
    }

    /// <summary>
    /// 确保数据目录存在
    /// </summary>
    public static void EnsureDataDirectory()
    {
        Directory.CreateDirectory(DataDirectory);
    }
}
