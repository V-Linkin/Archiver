using System;
using System.IO;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;

namespace Gatherly.Windows.Database;

/// <summary>
/// 执行 shared/db/migrations/ 中的 SQL migration 文件
/// 按文件名顺序执行，依赖 SQL 中的 IF NOT EXISTS 保证幂等
/// </summary>
public static class MigrationRunner
{
    /// <summary>
    /// 已知的 migration 文件，按执行顺序排列
    /// </summary>
    private static readonly string[] MigrationFiles = new[]
    {
        "v1_create_tables.sql",
        "v2_fts.sql",
        "v3_import_tasks_updated_at.sql"
    };

    /// <summary>
    /// 执行所有 migration
    /// </summary>
    public static void RunAll(SqliteConnection connection)
    {
        foreach (var fileName in MigrationFiles)
        {
            RunMigration(connection, fileName);
        }
    }

    /// <summary>
    /// 执行指定 migration 文件
    /// </summary>
    public static void RunMigration(SqliteConnection connection, string fileName)
    {
        var migrationPath = Path.Combine(DatabasePaths.MigrationsDirectory, fileName);
        
        if (!File.Exists(migrationPath))
        {
            throw new FileNotFoundException(
                $"Migration file not found: {migrationPath}. " +
                $"Ensure shared/db/migrations/*.sql are copied to output directory.");
        }

        var sql = File.ReadAllText(migrationPath);
        
        // Split SQL into individual statements and execute each separately
        // This ensures that if one statement fails (e.g., duplicate column),
        // subsequent statements can still execute
        var statements = SplitSqlStatements(sql);
        
        foreach (var statement in statements)
        {
            if (string.IsNullOrWhiteSpace(statement))
                continue;
                
            try
            {
                using var command = connection.CreateCommand();
                command.CommandText = statement;
                command.ExecuteNonQuery();
            }
            catch (SqliteException ex) when (ex.Message.Contains("duplicate column"))
            {
                // Column already exists, safe to ignore this specific statement
                // Continue executing subsequent statements (UPDATE, CREATE INDEX)
            }
        }
    }

    /// <summary>
    /// Split SQL file into individual statements
    /// </summary>
    private static string[] SplitSqlStatements(string sql)
    {
        // Remove SQL comments
        var cleaned = Regex.Replace(sql, @"--[^\r\n]*", "");
        
        // Split by semicolons
        var statements = cleaned.Split(';', StringSplitOptions.RemoveEmptyEntries);
        
        return statements;
    }
}
