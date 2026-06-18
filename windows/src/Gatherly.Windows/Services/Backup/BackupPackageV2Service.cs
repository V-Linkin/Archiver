using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using Gatherly.Windows.Database;
using Gatherly.Windows.Models.Enums;
using Microsoft.Data.Sqlite;

namespace Gatherly.Windows.Services.Backup;

public class BackupPackageV2Service
{
    private readonly SqliteConnection _connection;
    private readonly CustomPlatformRepository _customPlatformRepo;
    private readonly SystemPlatformDisplayNames _displayNames;
    private readonly SystemPlatformCustomMap _customMap;
    private readonly string _dataDirectory;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private static readonly string[] SystemPlatformRawValues =
    {
        "douyin", "xiaohongshu", "coolapk", "bilibili", "github",
        "youtube", "x", "weibo", "zhihu", "douban"
    };

    public BackupPackageV2Service(
        SqliteConnection connection,
        CustomPlatformRepository customPlatformRepo,
        SystemPlatformDisplayNames displayNames,
        SystemPlatformCustomMap customMap,
        string? dataDirectory = null)
    {
        _connection = connection;
        _customPlatformRepo = customPlatformRepo;
        _displayNames = displayNames;
        _customMap = customMap;
        _dataDirectory = dataDirectory ?? DatabasePaths.DataDirectory;
    }

    public async Task<BackupV2Result> CreateBackupAsync(string destinationPath, bool allowOverwrite = false, IProgress<BackupProgress>? progress = null, CancellationToken ct = default)
    {
        // Target file safety
        if (File.Exists(destinationPath) && !allowOverwrite)
            return BackupV2Result.Fail("目标文件已存在，且未允许覆盖。");

        var stagingDir = Path.Combine(Path.GetTempPath(), $"backup_v2_{Guid.NewGuid():N}");
        var tempZip = destinationPath + $".tmp-{Guid.NewGuid():N}";
        var progressState = new BackupProgress();
        var warnings = new List<string>();
        string? backupOfOriginal = null;

        try
        {
            Directory.CreateDirectory(stagingDir);
            var settingsDir = Path.Combine(stagingDir, "settings");
            var mediaDir = Path.Combine(stagingDir, "media");
            var logosDir = Path.Combine(stagingDir, "platform_logos");
            var dbDir = Path.Combine(stagingDir, "database");
            Directory.CreateDirectory(dbDir);
            Directory.CreateDirectory(settingsDir);

            // 1. Check pending mappings
            Report(progress, progressState, BackupProgressStage.CheckingPending);
            var pendingResult = await CheckPendingMappingsAsync();
            if (!pendingResult.success)
                return BackupV2Result.Fail($"pending映射恢复失败: {pendingResult.error}");

            // 2. Database snapshot
            Report(progress, progressState, BackupProgressStage.SnapshottingDatabase);
            var snapshotPath = Path.Combine(dbDir, "archiver.db");
            var snapshotResult = await CreateDatabaseSnapshotAsync(snapshotPath);
            if (!snapshotResult.success)
                return BackupV2Result.Fail(snapshotResult.error!);

            // 3. Export settings
            Report(progress, progressState, BackupProgressStage.ExportingSettings);
            var displayNamesJson = Path.Combine(settingsDir, "platform_display_names.json");
            var mappingsJson = Path.Combine(settingsDir, "system_platform_mappings.json");
            ExportDisplayNames(displayNamesJson);
            ExportMappings(mappingsJson, warnings);

            // 4. Collect media & logos (with collision tracking)
            Report(progress, progressState, BackupProgressStage.CollectingFiles, "收集媒体文件...");
            var collisionResolver = new CollisionResolver();
            var mediaFiles = await CollectMediaFilesAsync(snapshotPath, mediaDir, collisionResolver);
            var logoFiles = await CollectLogoFilesAsync(snapshotPath, logosDir, collisionResolver);

            // 5. Re-verify snapshot DB after collision updates
            var reverify = await VerifySnapshotAsync(snapshotPath);
            if (!reverify.success)
                return BackupV2Result.Fail(reverify.error!);

            // 6. Hash all files
            Report(progress, progressState, BackupProgressStage.HashingFiles);
            var allFiles = new List<StagedFile>();
            allFiles.Add(new("database/archiver.db", snapshotPath, "database", true));
            allFiles.Add(new("settings/platform_display_names.json", displayNamesJson, "settings", true));
            allFiles.Add(new("settings/system_platform_mappings.json", mappingsJson, "settings", true));
            foreach (var f in mediaFiles) allFiles.Add(new(f.packagePath, f.fullPath, "media", true));
            foreach (var f in logoFiles) allFiles.Add(new(f.packagePath, f.fullPath, "platform_logos", true));

            var fileEntries = new List<BackupFileEntry>();
            int processed = 0;
            foreach (var f in allFiles)
            {
                ct.ThrowIfCancellationRequested();
                var hash = await ComputeSha256Async(f.fullPath, ct);
                var size = new FileInfo(f.fullPath).Length;
                fileEntries.Add(new BackupFileEntry { Path = f.packagePath, Type = f.type, Size = size, Sha256 = hash, Required = f.required });
                processed++;
                Report(progress, progressState, progressState.Stage, $"Hash: {Path.GetFileName(f.packagePath)}", totalFiles: allFiles.Count, processedFiles: processed);
            }

            // 7. Build manifest
            var counts = await GetCountsAsync(snapshotPath);
            var manifest = new BackupPackageV2Manifest
            {
                CreatedAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                AppVersion = GetAppVersion(),
                DatabaseSchemaVersion = GetDbUserVersion(snapshotPath),
                DatabaseUserVersion = GetDbUserVersion(snapshotPath),
                Counts = counts,
                Files = fileEntries.OrderBy(f => f.Path, StringComparer.Ordinal).ToList(),
                Features = new BackupFeatures
                {
                    HasDatabase = true,
                    HasMedia = mediaFiles.Count > 0,
                    HasPlatformLogos = logoFiles.Count > 0,
                    HasPlatformDisplayNames = true,
                    HasSystemPlatformMappings = true,
                    HasTrash = counts.TrashRecords > 0,
                    HasFTS = true,
                    HasFolders = counts.Folders > 0
                },
                Warnings = warnings
            };
            File.WriteAllText(Path.Combine(stagingDir, "manifest.json"), JsonSerializer.Serialize(manifest, JsonOptions));

            // 8. Create ZIP
            Report(progress, progressState, BackupProgressStage.CreatingArchive);
            if (File.Exists(tempZip)) File.Delete(tempZip);
            CreateZipExplicit(stagingDir, tempZip, ct);

            // 9. Verify ZIP
            Report(progress, progressState, BackupProgressStage.VerifyingArchive);
            var verifyResult = VerifyArchive(tempZip, manifest);
            if (!verifyResult.success)
            {
                File.Delete(tempZip);
                return BackupV2Result.Fail(verifyResult.error!);
            }

            // 10. Safe replace
            Report(progress, progressState, BackupProgressStage.Finalizing);
            if (File.Exists(destinationPath))
            {
                backupOfOriginal = destinationPath + ".bak-" + Guid.NewGuid().ToString("N");
                File.Copy(destinationPath, backupOfOriginal, true);
                try
                {
                    File.Move(tempZip, destinationPath, overwrite: true);
                    File.Delete(backupOfOriginal);
                }
                catch
                {
                    try { if (File.Exists(backupOfOriginal)) File.Move(backupOfOriginal, destinationPath); } catch { }
                    return BackupV2Result.Fail("替换目标文件失败，原文件已保留。");
                }
            }
            else
            {
                File.Move(tempZip, destinationPath);
            }

            Report(progress, progressState, BackupProgressStage.Completed, "备份完成");
            return BackupV2Result.Ok(destinationPath);
        }
        catch (OperationCanceledException)
        {
            Report(progress, progressState, BackupProgressStage.Cancelled);
            return BackupV2Result.Cancelled();
        }
        catch (Exception ex)
        {
            return BackupV2Result.Fail($"备份创建失败: {ex.Message}");
        }
        finally
        {
            try { if (File.Exists(tempZip)) File.Delete(tempZip); } catch { }
            try { if (File.Exists(backupOfOriginal)) File.Delete(backupOfOriginal); } catch { }
            try { if (Directory.Exists(stagingDir)) Directory.Delete(stagingDir, true); } catch { }
        }
    }

    private void CreateZipExplicit(string sourceDir, string zipPath, CancellationToken ct)
    {
        using var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create);
        foreach (var fullPath in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
        {
            ct.ThrowIfCancellationRequested();
            var relPath = Path.GetRelativePath(sourceDir, fullPath).Replace('\\', '/');
            var normalizedPath = BackupPathResolver.NormalizePackagePath(relPath);
            archive.CreateEntryFromFile(fullPath, normalizedPath, CompressionLevel.Optimal);
        }
        archive.Dispose();
    }

    private (bool success, string? error) VerifyArchive(string zipPath, BackupPackageV2Manifest manifest)
    {
        try
        {
            using var archive = ZipFile.OpenRead(zipPath);
            var zipEntries = archive.Entries
                .Where(e => !string.IsNullOrEmpty(e.Name))
                .Select(e => e.FullName.Replace('\\', '/'))
                .ToHashSet(StringComparer.Ordinal);

            if (manifest.FormatVersion != 2) return (false, "manifest formatVersion != 2");

            foreach (var f in manifest.Files)
            {
                if (!zipEntries.Contains(f.Path))
                    return (false, $"manifest记录的文件不在ZIP中: {f.Path}");
            }

            foreach (var entry in zipEntries)
            {
                if (!manifest.Files.Any(f => f.Path == entry) && entry != "manifest.json")
                    return (false, $"ZIP中存在未记录的业务文件: {entry}");
            }

            if (zipEntries.Any(e => e.EndsWith(".db-wal") || e.EndsWith(".db-shm")))
                return (false, "ZIP中包含数据库WAL/SHM文件");

            foreach (var entry in zipEntries)
            {
                if (entry.Contains('\\')) return (false, $"ZIP条目含反斜杠: {entry}");
                if (Path.IsPathRooted(entry) || entry.StartsWith('/')) return (false, $"ZIP条目为绝对路径: {entry}");
                if (entry.Contains("..")) return (false, $"ZIP条目含路径穿越: {entry}");
            }

            foreach (var f in manifest.Files)
            {
                var entry = archive.GetEntry(f.Path);
                if (entry == null) return (false, $"无法打开ZIP条目: {f.Path}");
                if (entry.Length != f.Size) return (false, $"文件大小不匹配: {f.Path} manifest={f.Size} actual={entry.Length}");
            }

            return (true, null);
        }
        catch (Exception ex)
        {
            return (false, $"ZIP验证失败: {ex.Message}");
        }
    }

    private async Task<(bool success, string? error)> CheckPendingMappingsAsync()
    {
        var pendingPath = Path.Combine(_dataDirectory, "system_platform_custom_map.pending.json");
        if (!File.Exists(pendingPath)) return (true, null);
        try
        {
            var json = await File.ReadAllTextAsync(pendingPath);
            var pending = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
            if (pending == null || pending.Count == 0) return (true, null);
            foreach (var (rawValue, idStr) in pending)
            {
                if (!Guid.TryParse(idStr, out var id)) return (false, $"pending映射 {rawValue} 的UUID无效");
                var cp = await _customPlatformRepo.GetByIdAsync(id);
                if (cp == null) return (false, $"pending映射 {rawValue} 指向不存在的CustomPlatform {id}");
            }
            foreach (var (rawValue, idStr) in pending)
            {
                if (Guid.TryParse(idStr, out var id))
                    _customMap.SetMapping(rawValue, id);
            }
            File.Delete(pendingPath);
            return (true, null);
        }
        catch (Exception ex) { return (false, $"pending恢复失败: {ex.Message}"); }
    }

    private async Task<(bool success, string? error)> CreateDatabaseSnapshotAsync(string targetPath)
    {
        try
        {
            var escapedPath = targetPath.Replace("'", "''");
            await using (var cmd = _connection.CreateCommand())
            {
                cmd.CommandText = $"VACUUM INTO '{escapedPath}'";
                await cmd.ExecuteNonQueryAsync();
            }
        }
        catch (Exception ex) { return (false, $"数据库快照失败: {ex.Message}"); }

        using var verifyConn = new SqliteConnection($"Data Source={targetPath};Mode=ReadOnly");
        await verifyConn.OpenAsync();
        using (var cmd = verifyConn.CreateCommand())
        {
            cmd.CommandText = "PRAGMA integrity_check";
            if (cmd.ExecuteScalar()?.ToString() != "ok") return (false, "数据库完整性检查失败");
        }
        using (var cmd = verifyConn.CreateCommand())
        {
            cmd.CommandText = "PRAGMA foreign_key_check";
            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync()) return (false, "数据库外键检查发现违规");
        }
        return (true, null);
    }

    private async Task<(bool success, string? error)> VerifySnapshotAsync(string snapshotPath)
    {
        using var verifyConn = new SqliteConnection($"Data Source={snapshotPath};Mode=ReadOnly");
        await verifyConn.OpenAsync();
        using (var cmd = verifyConn.CreateCommand())
        {
            cmd.CommandText = "PRAGMA integrity_check";
            if (cmd.ExecuteScalar()?.ToString() != "ok") return (false, "碰撞后数据库完整性检查失败");
        }
        using (var cmd = verifyConn.CreateCommand())
        {
            cmd.CommandText = "PRAGMA foreign_key_check";
            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync()) return (false, "碰撞后数据库外键检查发现违规");
        }
        return (true, null);
    }

    private void ExportDisplayNames(string filePath)
    {
        var names = new List<Dictionary<string, string>>();
        foreach (var raw in SystemPlatformRawValues)
        {
            var platform = PlatformExtensions.FromRawValue(raw);
            if (platform == Platform.custom) continue;
            var name = _displayNames.GetDisplayName(platform);
            if (name != platform.GetDisplayName())
                names.Add(new Dictionary<string, string> { ["platformRawValue"] = raw, ["displayName"] = name });
        }
        File.WriteAllText(filePath, JsonSerializer.Serialize(new Dictionary<string, object> { ["platformDisplayNames"] = names }, JsonOptions));
    }

    private void ExportMappings(string filePath, List<string> warnings)
    {
        var mappings = new List<Dictionary<string, string>>();
        foreach (var raw in SystemPlatformRawValues)
        {
            var id = _customMap.GetCustomPlatformId(raw);
            if (id.HasValue)
                mappings.Add(new Dictionary<string, string> { ["platformRawValue"] = raw, ["customPlatformId"] = id.Value.ToString("D") });
        }
        File.WriteAllText(filePath, JsonSerializer.Serialize(new Dictionary<string, object> { ["systemPlatformMappings"] = mappings }, JsonOptions));
    }

    private async Task<List<StagedMediaFile>> CollectMediaFilesAsync(string snapshotPath, string mediaDir, CollisionResolver collisionResolver)
    {
        var result = new List<StagedMediaFile>();
        var updates = new List<(string id, string newPath)>();

        using (var cmd = _connection.CreateCommand())
        {
            cmd.CommandText = "SELECT id, local_path FROM media_assets WHERE local_path IS NOT NULL";
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var id = reader.GetString(0);
                var localPath = reader.GetString(1);
                var fullPath = Path.Combine(_dataDirectory, "media", localPath.Replace('/', '\\'));

                if (!File.Exists(fullPath))
                    throw new BackupV2Exception($"媒体文件缺失: {localPath}", BackupErrorKind.MissingRequiredMedia);

                var pkgPath = "media/" + localPath.Replace('\\', '/');
                var finalPkgPath = collisionResolver.Resolve(pkgPath, fullPath);
                if (finalPkgPath != pkgPath)
                    updates.Add((id, "media/" + finalPkgPath["media/".Length..]));

                var mediaRelativePath = finalPkgPath["media/".Length..];
                var destPath = Path.Combine(mediaDir, mediaRelativePath.Replace('/', '\\'));
                var destDir = Path.GetDirectoryName(destPath);
                if (destDir != null && !Directory.Exists(destDir))
                    Directory.CreateDirectory(destDir);
                File.Copy(fullPath, destPath, true);
                result.Add(new StagedMediaFile(finalPkgPath, destPath));
            }
        }

        if (updates.Count > 0)
        {
            using var updateConn = new SqliteConnection($"Data Source={snapshotPath};Mode=ReadWrite");
            await updateConn.OpenAsync();
            foreach (var (id, newPath) in updates)
            {
                using var updateCmd = updateConn.CreateCommand();
                updateCmd.CommandText = "UPDATE media_assets SET local_path=$p WHERE id=$id";
                updateCmd.Parameters.AddWithValue("$p", newPath);
                updateCmd.Parameters.AddWithValue("$id", id);
                await updateCmd.ExecuteNonQueryAsync();
            }
        }

        return result;
    }

    private async Task<List<StagedMediaFile>> CollectLogoFilesAsync(string snapshotPath, string logosDir, CollisionResolver collisionResolver)
    {
        var result = new List<StagedMediaFile>();
        var updates = new List<(string id, string newPath)>();

        using (var cmd = _connection.CreateCommand())
        {
            cmd.CommandText = "SELECT id, logo_path FROM custom_platforms WHERE logo_path IS NOT NULL AND logo_path != ''";
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var id = reader.GetString(0);
                var logoPath = reader.GetString(1);
                var fullPath = Path.Combine(_dataDirectory, "platform_logos", logoPath);

                if (!File.Exists(fullPath))
                    throw new BackupV2Exception($"平台图标缺失: {logoPath}", BackupErrorKind.MissingRequiredLogo);

                var pkgPath = "platform_logos/" + logoPath;
                var finalPkgPath = collisionResolver.Resolve(pkgPath, fullPath);
                if (finalPkgPath != pkgPath)
                    updates.Add((id, finalPkgPath["platform_logos/".Length..]));

                var logoRelativePath = finalPkgPath["platform_logos/".Length..];
                var destPath = Path.Combine(logosDir, logoRelativePath.Replace('/', '\\'));
                File.Copy(fullPath, destPath, true);
                result.Add(new StagedMediaFile(finalPkgPath, destPath));
            }
        }

        if (updates.Count > 0)
        {
            using var updateConn = new SqliteConnection($"Data Source={snapshotPath};Mode=ReadWrite");
            await updateConn.OpenAsync();
            foreach (var (id, newPath) in updates)
            {
                using var updateCmd = updateConn.CreateCommand();
                updateCmd.CommandText = "UPDATE custom_platforms SET logo_path=$p WHERE id=$id";
                updateCmd.Parameters.AddWithValue("$p", newPath);
                updateCmd.Parameters.AddWithValue("$id", id);
                await updateCmd.ExecuteNonQueryAsync();
            }
        }

        return result;
    }

    private async Task<BackupCounts> GetCountsAsync(string snapshotPath)
    {
        using var conn = new SqliteConnection($"Data Source={snapshotPath};Mode=ReadOnly");
        await conn.OpenAsync();
        return new BackupCounts
        {
            Items = await Q(conn, "SELECT COUNT(*) FROM items"),
            MediaAssets = await Q(conn, "SELECT COUNT(*) FROM media_assets"),
            Folders = await Q(conn, "SELECT COUNT(*) FROM folders"),
            CustomPlatforms = await Q(conn, "SELECT COUNT(*) FROM custom_platforms"),
            ImportTasks = await Q(conn, "SELECT COUNT(*) FROM import_tasks"),
            TrashRecords = await Q(conn, "SELECT COUNT(*) FROM trash_records")
        };
    }

    private static async Task<int> Q(SqliteConnection c, string sql)
    {
        using var cmd = c.CreateCommand(); cmd.CommandText = sql;
        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }

    private static int GetDbUserVersion(string p)
    {
        using var c = new SqliteConnection($"Data Source={p};Mode=ReadOnly"); c.Open();
        using var cmd = c.CreateCommand(); cmd.CommandText = "PRAGMA user_version";
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    private static string GetAppVersion() =>
        System.Reflection.Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "1.0.0";

    private static async Task<string> ComputeSha256Async(string filePath, CancellationToken ct)
    {
        using var sha = SHA256.Create();
        await using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, true);
        return Convert.ToHexString(await sha.ComputeHashAsync(stream, ct)).ToLowerInvariant();
    }

    private static void Report(IProgress<BackupProgress>? p, BackupProgress s, BackupProgressStage st, string? m = null, int totalFiles = 0, int processedFiles = 0)
    {
        s.Stage = st;
        if (m != null) s.Message = m;
        if (totalFiles > 0) s.TotalFiles = totalFiles;
        if (processedFiles > 0) s.ProcessedFiles = processedFiles;
        p?.Report(s);
    }
}

internal sealed record StagedFile(string packagePath, string fullPath, string type, bool required);
internal sealed record StagedMediaFile(string packagePath, string fullPath);

public enum BackupErrorKind
{
    MissingRequiredMedia,
    MissingRequiredLogo,
    DatabaseSnapshotFailed,
    DatabaseIntegrityFailed,
    ForeignKeyCheckFailed,
    ArchiveVerificationFailed,
    DestinationAlreadyExists,
    DestinationReplaceFailed,
    PendingMappingRecoveryFailed,
    Cancelled
}

public class BackupV2Exception : Exception
{
    public BackupErrorKind Kind { get; }
    public BackupV2Exception(string message, BackupErrorKind kind) : base(message) { Kind = kind; }
}

public class CollisionResolver
{
    private readonly Dictionary<string, string> _usedPaths = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _fileHashes = new();

    public string Resolve(string packagePath, string fullPath)
    {
        var equivalenceKey = BackupPathResolver.GetEquivalenceKey(packagePath);
        var hash = ComputeHashFast(fullPath);

        if (_usedPaths.TryGetValue(equivalenceKey, out var existingPath))
        {
            var existingHash = _fileHashes[equivalenceKey];
            if (hash == existingHash)
                return existingPath;

            var suffix = hash[..Math.Min(12, hash.Length)];
            var ext = Path.GetExtension(packagePath);
            var nameOnly = Path.GetFileNameWithoutExtension(packagePath);
            var dir = Path.GetDirectoryName(packagePath)?.Replace('\\', '/') ?? "";
            var renamed = string.IsNullOrEmpty(dir) ? $"{nameOnly}_{suffix}{ext}" : $"{dir}/{nameOnly}_{suffix}{ext}";
            _usedPaths[equivalenceKey] = renamed;
            _fileHashes[equivalenceKey] = hash;
            return renamed;
        }

        _usedPaths[equivalenceKey] = packagePath;
        _fileHashes[equivalenceKey] = hash;
        return packagePath;
    }

    private static string ComputeHashFast(string fullPath)
    {
        using var sha = SHA256.Create();
        using var stream = File.OpenRead(fullPath);
        return Convert.ToHexString(sha.ComputeHash(stream)).ToLowerInvariant();
    }
}
