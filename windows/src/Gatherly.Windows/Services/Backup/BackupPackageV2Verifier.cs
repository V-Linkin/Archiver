using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace Gatherly.Windows.Services.Backup;

public enum BackupVerificationError
{
    InvalidZip,
    MissingManifest,
    InvalidManifestJson,
    ManifestSchemaInvalid,
    UnsupportedFormatVersion,
    UnsafePackagePath,
    DuplicateZipEntry,
    MissingManifestFile,
    UnexpectedBusinessFile,
    SizeMismatch,
    Sha256Mismatch,
    MissingDatabase,
    DatabaseOpenFailed,
    DatabaseIntegrityFailed,
    DatabaseForeignKeyFailed,
    DatabaseUserVersionMismatch,
    DatabaseSchemaInvalid,
    Cancelled
}

public sealed class BackupVerificationErrorInfo
{
    public BackupVerificationError Code { get; init; }
    public string Message { get; init; } = string.Empty;
}

public sealed class BackupVerificationWarning
{
    public string Code { get; init; } = "";
    public string Message { get; init; } = "";
}

public sealed class BackupVerificationResult
{
    public bool IsValid { get; init; }
    public bool IsCancelled { get; init; }
    public List<BackupVerificationErrorInfo> Errors { get; init; } = new();
    public List<BackupVerificationWarning> Warnings { get; init; } = new();
    public int FormatVersion { get; init; }
    public string? CreatedAt { get; init; }
    public string? AppVersion { get; init; }
    public int DatabaseUserVersion { get; init; }
    public int DatabaseSchemaVersion { get; init; }
    public int TotalFileEntries { get; init; }
    public int ValidatedFileEntries { get; init; }
    public long ValidatedTotalBytes { get; init; }
    public bool DatabaseIntegrityOk { get; init; }
    public bool DatabaseForeignKeyOk { get; init; }
    public bool DatabaseUserVersionOk { get; init; }
    public bool DatabaseTablesOk { get; init; }

    public static BackupVerificationResult Cancelled() => new()
    {
        IsValid = false,
        IsCancelled = true,
        Errors = new() { new() { Code = BackupVerificationError.Cancelled, Message = "验证已取消" } }
    };
}

public static class BackupPackageV2Verifier
{
    private const int BufferSize = 81920;
    private const int MaxManifestSize = 1024 * 1024;
    private static readonly HashSet<string> RequiredTables = new() { "items", "media_assets", "folders", "trash_records", "import_tasks", "custom_platforms" };
    private static readonly HashSet<string> RequiredItemColumns = new() { "id", "title", "body", "original_url", "platform", "platform_content_id", "normalized_url", "author", "author_id", "publish_date", "import_date", "modify_date", "content_status", "archive_status", "media_status", "custom_platform_id", "folder_id" };
    private static readonly HashSet<string> RequiredMediaColumns = new() { "id", "item_id", "type", "file_name", "download_status", "created_at" };
    private static readonly HashSet<string> RequiredFolderColumns = new() { "id", "name", "platform", "created_at", "sort_order" };
    private static readonly HashSet<string> RequiredTrashColumns = new() { "id", "item_id", "deleted_at", "auto_delete_at", "original_archive_status" };
    private static readonly HashSet<string> RequiredImportTaskColumns = new() { "id", "original_url", "normalized_url", "status", "created_at" };
    private static readonly HashSet<string> RequiredCustomPlatformColumns = new() { "id", "name", "created_at", "sort_order" };

    private static void CheckColumns(Microsoft.Data.Sqlite.SqliteConnection conn, string table, HashSet<string> required, DbVerificationResult dbResult)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"PRAGMA table_info({table})";
        using var reader = cmd.ExecuteReader();
        var actualColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        while (reader.Read()) actualColumns.Add(reader.GetString(1));
        foreach (var col in required)
        {
            if (!actualColumns.Contains(col))
            {
                dbResult.TablesOk = false;
                dbResult.MissingColumns.Add($"{table}.{col}");
            }
        }
    }

    public static async Task<BackupVerificationResult> VerifyAsync(string zipPath, int expectedUserVersion, CancellationToken ct = default)
    {
        var errors = new List<BackupVerificationErrorInfo>();
        var warnings = new List<BackupVerificationWarning>();

        if (!File.Exists(zipPath))
            return Fail(BackupVerificationError.InvalidZip, "备份文件不存在");

        ZipArchive archive;
        try { archive = ZipFile.OpenRead(zipPath); }
        catch (InvalidDataException) { return Fail(BackupVerificationError.InvalidZip, "ZIP 文件损坏"); }
        catch (Exception ex) { return Fail(BackupVerificationError.InvalidZip, $"无法打开 ZIP: {ex.Message}"); }

        try
        {
            return await VerifyArchiveAsync(archive, expectedUserVersion, errors, warnings, ct);
        }
        catch (OperationCanceledException)
        {
            return BackupVerificationResult.Cancelled();
        }
    }

    private static async Task<BackupVerificationResult> VerifyArchiveAsync(ZipArchive archive, int expectedUserVersion, List<BackupVerificationErrorInfo> errors, List<BackupVerificationWarning> warnings, CancellationToken ct)
    {
        // Step 1: Collect and validate entries
        var entryNames = archive.Entries
            .Where(e => !string.IsNullOrEmpty(e.Name))
            .Select(e => e.FullName.Replace('\\', '/'))
            .ToList();

        var seenPaths = new HashSet<string>(StringComparer.Ordinal);
        foreach (var path in entryNames)
        {
            if (!seenPaths.Add(path))
                return Fail(BackupVerificationError.DuplicateZipEntry, $"重复 ZIP 条目: {path}");

            if (path.Contains('\\')) return Fail(BackupVerificationError.UnsafePackagePath, $"路径含反斜杠: {path}");
            if (Path.IsPathRooted(path) || path.StartsWith('/')) return Fail(BackupVerificationError.UnsafePackagePath, $"路径为绝对路径: {path}");
            if (path.Contains(':')) return Fail(BackupVerificationError.UnsafePackagePath, $"路径含盘符: {path}");
            if (path.Contains("..")) return Fail(BackupVerificationError.UnsafePackagePath, $"路径含穿越: {path}");
        }

        ct.ThrowIfCancellationRequested();

        // Step 2: Read manifest
        var manifestEntry = archive.GetEntry("manifest.json");
        if (manifestEntry == null) return Fail(BackupVerificationError.MissingManifest, "缺少 manifest.json");
        if (manifestEntry.Length > MaxManifestSize) return Fail(BackupVerificationError.InvalidManifestJson, "manifest.json 超过 1 MiB");

        string manifestJson;
        try { using var s = manifestEntry.Open(); using var r = new StreamReader(s); manifestJson = await r.ReadToEndAsync(ct); }
        catch (Exception ex) { return Fail(BackupVerificationError.InvalidManifestJson, $"读取 manifest.json 失败: {ex.Message}"); }

        BackupPackageV2Manifest manifest;
        try { manifest = JsonSerializer.Deserialize<BackupPackageV2Manifest>(manifestJson)!; }
        catch { return Fail(BackupVerificationError.InvalidManifestJson, "manifest.json 解析失败"); }

        ct.ThrowIfCancellationRequested();

        // Step 3: Schema
        var (schemaValid, schemaError) = BackupManifestSchemaValidator.Validate(manifestJson);
        if (!schemaValid) return Fail(BackupVerificationError.ManifestSchemaInvalid, $"Schema 验证失败: {schemaError}");

        // Step 4: Format version
        if (manifest.FormatVersion != 2) return Fail(BackupVerificationError.UnsupportedFormatVersion, $"不支持的格式版本: {manifest.FormatVersion}");

        // Step 5: Check all manifest files exist and no unexpected business files
        var managedPaths = manifest.Files.Select(f => f.Path).ToHashSet(StringComparer.Ordinal);
        managedPaths.Add("manifest.json");

        foreach (var path in entryNames)
        {
            if (!managedPaths.Contains(path))
            {
                if (path.EndsWith(".db-wal") || path.EndsWith(".db-shm"))
                    warnings.Add(new() { Code = "WalFile", Message = $"ZIP 包含 WAL/SHM 文件: {path}" });
                else
                    return Fail(BackupVerificationError.UnexpectedBusinessFile, $"ZIP 中存在未管理的业务文件: {path}");
            }
        }

        foreach (var f in manifest.Files)
        {
            if (archive.GetEntry(f.Path) == null)
                return Fail(BackupVerificationError.MissingManifestFile, $"Manifest 记录的文件不在 ZIP 中: {f.Path}");
        }

        ct.ThrowIfCancellationRequested();

        // Step 6: Size + SHA-256
        int validFiles = 0;
        long validBytes = 0;
        foreach (var f in manifest.Files)
        {
            ct.ThrowIfCancellationRequested();
            var entry = archive.GetEntry(f.Path)!;
            using var stream = entry.Open();
            using var sha = SHA256.Create();
            var hash = Convert.ToHexString(await sha.ComputeHashAsync(stream, ct)).ToLowerInvariant();
            var actualSize = entry.Length;

            if (actualSize != f.Size) errors.Add(new() { Code = BackupVerificationError.SizeMismatch, Message = $"size 不匹配: {f.Path} manifest={f.Size} actual={actualSize}" });
            if (hash != f.Sha256.ToLowerInvariant()) errors.Add(new() { Code = BackupVerificationError.Sha256Mismatch, Message = $"SHA-256 不匹配: {f.Path}" });

            validFiles++;
            validBytes += actualSize;
        }

        ct.ThrowIfCancellationRequested();

        // Step 7: Database verification
        bool dbIntegrity = false, dbForeignKey = false, dbUserVersion = false, dbTables = false;
        var dbEntry = archive.GetEntry("database/archiver.db");
        if (dbEntry == null)
        {
            errors.Add(new() { Code = BackupVerificationError.MissingDatabase, Message = "缺少 database/archiver.db" });
        }
        else
        {
            var dbPath = Path.Combine(Path.GetTempPath(), $"gly_verify_{Guid.NewGuid():N}.db");
            try
            {
                await ExtractDatabaseEntryAsync(dbEntry, dbPath, ct);
                var dbResult = await VerifyDatabaseAsync(dbPath, expectedUserVersion, ct);
                dbIntegrity = dbResult.IntegrityOk;
                dbForeignKey = dbResult.ForeignKeyOk;
                dbUserVersion = dbResult.UserVersionOk;
                dbTables = dbResult.TablesOk;

                if (!dbIntegrity) errors.Add(new() { Code = BackupVerificationError.DatabaseIntegrityFailed, Message = "数据库完整性检查失败" });
                if (!dbForeignKey) errors.Add(new() { Code = BackupVerificationError.DatabaseForeignKeyFailed, Message = "数据库外键检查发现违规" });
                if (!dbUserVersion) errors.Add(new() { Code = BackupVerificationError.DatabaseUserVersionMismatch, Message = $"user_version 不匹配: manifest={manifest.DatabaseUserVersion} actual={dbResult.ActualUserVersion}" });
                if (!dbResult.TablesOk)
                {
                    if (dbResult.MissingTables.Count > 0) errors.Add(new() { Code = BackupVerificationError.DatabaseSchemaInvalid, Message = $"缺少必要表: {string.Join(", ", dbResult.MissingTables)}" });
                    if (dbResult.MissingColumns.Count > 0) errors.Add(new() { Code = BackupVerificationError.DatabaseSchemaInvalid, Message = $"缺少必要字段: {string.Join(", ", dbResult.MissingColumns)}" });
                }
            }
            catch (Exception ex)
            {
                errors.Add(new() { Code = BackupVerificationError.DatabaseOpenFailed, Message = $"数据库验证失败: {ex.Message}" });
            }
            finally
            {
                try { File.Delete(dbPath); } catch { }
            }
        }

        return new BackupVerificationResult
        {
            IsValid = errors.Count == 0,
            Errors = errors,
            Warnings = warnings,
            FormatVersion = manifest.FormatVersion,
            CreatedAt = manifest.CreatedAt,
            AppVersion = manifest.AppVersion,
            DatabaseUserVersion = manifest.DatabaseUserVersion,
            DatabaseSchemaVersion = manifest.DatabaseSchemaVersion,
            TotalFileEntries = manifest.Files.Count,
            ValidatedFileEntries = validFiles,
            ValidatedTotalBytes = validBytes,
            DatabaseIntegrityOk = dbIntegrity,
            DatabaseForeignKeyOk = dbForeignKey,
            DatabaseUserVersionOk = dbUserVersion,
            DatabaseTablesOk = dbTables
        };
    }

    private static async Task ExtractDatabaseEntryAsync(ZipArchiveEntry entry, string targetPath, CancellationToken ct)
    {
        using var entryStream = entry.Open();
        await using var fileStream = new FileStream(targetPath, FileMode.Create, FileAccess.Write, FileShare.None, BufferSize, true);
        await entryStream.CopyToAsync(fileStream, ct);
    }

    private static async Task<DbVerificationResult> VerifyDatabaseAsync(string dbPath, int expectedUserVersion, CancellationToken ct)
    {
        var result = new DbVerificationResult();

        await using var conn = new SqliteConnection($"Data Source={dbPath};");
        await conn.OpenAsync(ct);

        // integrity_check
        using (var cmd = conn.CreateCommand()) { cmd.CommandText = "PRAGMA integrity_check"; result.IntegrityOk = cmd.ExecuteScalar()?.ToString() == "ok"; }

        // foreign_key_check (must enable FK first)
        using (var cmd = conn.CreateCommand()) { cmd.CommandText = "PRAGMA foreign_keys = ON"; await cmd.ExecuteNonQueryAsync(ct); }
        using (var cmd = conn.CreateCommand()) { await using var reader = await cmd.ExecuteReaderAsync(ct); while (await reader.ReadAsync()) { result.ForeignKeyOk = false; break; } }

        // user_version
        using (var cmd = conn.CreateCommand()) { cmd.CommandText = "PRAGMA user_version"; result.ActualUserVersion = Convert.ToInt32(cmd.ExecuteScalar()); result.UserVersionOk = result.ActualUserVersion == expectedUserVersion; }

        // Tables
        foreach (var t in new[] { "items", "media_assets", "folders", "trash_records", "import_tasks", "custom_platforms" })
        {
            using var cmd = conn.CreateCommand(); cmd.CommandText = $"SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='{t}'"; if (Convert.ToInt64(cmd.ExecuteScalar()) == 0) { result.TablesOk = false; result.MissingTables.Add(t); }
        }

        // Columns
        if (result.TablesOk)
        {
            CheckColumns(conn, "items", RequiredItemColumns, result);
            CheckColumns(conn, "media_assets", RequiredMediaColumns, result);
            CheckColumns(conn, "folders", RequiredFolderColumns, result);
            CheckColumns(conn, "trash_records", RequiredTrashColumns, result);
            CheckColumns(conn, "import_tasks", RequiredImportTaskColumns, result);
            CheckColumns(conn, "custom_platforms", RequiredCustomPlatformColumns, result);
        }

        await conn.CloseAsync();
        return result;
    }

    private static BackupVerificationResult Fail(BackupVerificationError code, string message) => new()
    {
        IsValid = false,
        Errors = new() { new() { Code = code, Message = message } }
    };

    private sealed class DbVerificationResult
    {
        public bool IntegrityOk { get; set; }
        public bool ForeignKeyOk { get; set; } = true;
        public bool UserVersionOk { get; set; }
        public bool TablesOk { get; set; } = true;
        public int ActualUserVersion { get; set; }
        public List<string> MissingTables { get; set; } = new();
        public List<string> MissingColumns { get; set; } = new();
    }
}
