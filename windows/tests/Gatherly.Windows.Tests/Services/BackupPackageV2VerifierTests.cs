using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using Gatherly.Windows.Services.Backup;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Gatherly.Windows.Tests.Services;

[Collection("Sequential")]
public class BackupPackageV2VerifierTests : IDisposable
{
    private readonly string _testDir;

    public BackupPackageV2VerifierTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"gly_vt_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDir);
        var src = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "shared", "import-export", "backup-package-v2.schema.json");
        if (!File.Exists(src)) src = Path.Combine(AppContext.BaseDirectory, "backup-package-v2.schema.json");
        if (File.Exists(src)) BackupManifestSchemaValidator.ResetSchemaCache(src);
    }

    public void Dispose()
    {
        BackupManifestSchemaValidator.ResetSchemaCache();
        try { Directory.Delete(_testDir, true); } catch { }
    }

    private static (string path, byte[] bytes) CreateDbFile(string sql, int userVersion = 3)
    {
        var path = Path.Combine(Path.GetTempPath(), $"gly_db_{Guid.NewGuid():N}.db");
        var conn = new SqliteConnection($"Data Source={path};");
        try { conn.Open(); using (var cmd = conn.CreateCommand()) { cmd.CommandText = sql; cmd.ExecuteNonQuery(); } using (var cmd = conn.CreateCommand()) { cmd.CommandText = $"PRAGMA user_version = {userVersion}"; cmd.ExecuteNonQuery(); } }
        finally { conn.Close(); conn.Dispose(); }
        SqliteConnection.ClearAllPools();
        for (int i = 0; i < 5; i++) { try { return (path, File.ReadAllBytes(path)); } catch (IOException) { Thread.Sleep(10); } }
        throw new IOException($"Failed to read {path}");
    }

    private static void DeleteDbFile(string path) { File.Delete(path); try { File.Delete(path + "-wal"); } catch { } try { File.Delete(path + "-shm"); } catch { } }

    private static string HexHash(byte[] bytes) => Convert.ToHexString(bytes).ToLowerInvariant();

    private static void WriteManifest(ZipArchive a, BackupPackageV2Manifest m)
    {
        var b = System.Text.Encoding.UTF8.GetBytes(JsonSerializer.Serialize(m));
        var e = a.CreateEntry("manifest.json"); var s = e.Open(); s.Write(b, 0, b.Length); s.Close();
    }

    private static void WriteRaw(ZipArchive a, string name, byte[] data)
    {
        var e = a.CreateEntry(name); var s = e.Open(); s.Write(data, 0, data.Length); s.Close();
    }

    private static BackupPackageV2Manifest MakeManifest(List<BackupFileEntry>? files = null, int userVersion = 3, string? dbHash = null, long dbSize = 0)
    {
        var fe = files ?? (dbHash != null ? new List<BackupFileEntry> { new() { Path = "database/archiver.db", Type = "database", Size = dbSize, Sha256 = dbHash, Required = true } } : new());
        return new() { FormatVersion = 2, CreatedAt = "2026-01-01T00:00:00Z", AppVersion = "1.0.0", SourceApp = "Gatherly", SourceOS = "windows", DatabaseSchemaVersion = 0, DatabaseUserVersion = userVersion, Counts = new(), Files = fe, Features = new() { HasDatabase = true }, Warnings = new() };
    }

    private string ValidZipPath(int userVersion = 3)
    {
        var dbSql = "CREATE TABLE items (id TEXT PRIMARY KEY,title TEXT,body TEXT,original_url TEXT NOT NULL,platform TEXT NOT NULL,platform_content_id TEXT,normalized_url TEXT NOT NULL,author TEXT,author_id TEXT,publish_date REAL,import_date REAL NOT NULL DEFAULT 0,modify_date REAL NOT NULL DEFAULT 0,content_status TEXT NOT NULL DEFAULT 'normal',archive_status TEXT NOT NULL DEFAULT 'pending',media_status TEXT NOT NULL DEFAULT 'textOnly',cover_asset_id TEXT,folder_id TEXT,remark TEXT,is_starred INTEGER NOT NULL DEFAULT 0,version INTEGER NOT NULL DEFAULT 1,deleted_at REAL,custom_platform_id TEXT);CREATE TABLE media_assets (id TEXT PRIMARY KEY,item_id TEXT NOT NULL REFERENCES items(id),type TEXT NOT NULL,local_path TEXT,remote_url TEXT,file_name TEXT NOT NULL,file_size INTEGER NOT NULL DEFAULT 0,mime_type TEXT,width INTEGER,height INTEGER,duration REAL,checksum TEXT,download_status TEXT NOT NULL DEFAULT 'pending',thumbnail_path TEXT,created_at REAL NOT NULL);CREATE TABLE folders (id TEXT PRIMARY KEY,name TEXT NOT NULL,parent_id TEXT REFERENCES folders(id),platform TEXT NOT NULL,created_at REAL NOT NULL,sort_order INTEGER NOT NULL DEFAULT 0,custom_platform_id TEXT);CREATE TABLE trash_records (id TEXT PRIMARY KEY,item_id TEXT NOT NULL REFERENCES items(id),deleted_at REAL NOT NULL,auto_delete_at REAL NOT NULL,original_folder_id TEXT,original_archive_status TEXT NOT NULL,media_paths TEXT);CREATE TABLE import_tasks (id TEXT PRIMARY KEY,original_url TEXT NOT NULL,normalized_url TEXT NOT NULL,platform TEXT,status TEXT NOT NULL DEFAULT 'pending',progress REAL NOT NULL DEFAULT 0,error_message TEXT,item_id TEXT REFERENCES items(id),created_at REAL NOT NULL,completed_at REAL,retry_count INTEGER NOT NULL DEFAULT 0);CREATE TABLE custom_platforms (id TEXT PRIMARY KEY,name TEXT NOT NULL,logo_path TEXT,created_at REAL NOT NULL,sort_order INTEGER NOT NULL DEFAULT 0);";
        var (dbPath, bytes) = CreateDbFile(dbSql, userVersion);
        var hash = HexHash(SHA256.HashData(bytes));
        var z = Path.Combine(_testDir, $"{Guid.NewGuid():N}.zip");
        using (var a = ZipFile.Open(z, ZipArchiveMode.Create)) { var de = a.CreateEntry("database/archiver.db"); var ds = de.Open(); ds.Write(bytes, 0, bytes.Length); ds.Close(); WriteManifest(a, MakeManifest(userVersion: userVersion, dbHash: hash, dbSize: bytes.Length)); }
        DeleteDbFile(dbPath);
        return z;
    }

    private string MakeZip(BackupPackageV2Manifest m, List<(string name, byte[] data)>? extraEntries = null)
    {
        var z = Path.Combine(_testDir, $"{Guid.NewGuid():N}.zip");
        using var a = ZipFile.Open(z, ZipArchiveMode.Create); WriteManifest(a, m); if (extraEntries != null) foreach (var (n, d) in extraEntries) WriteRaw(a, n, d);
        return z;
    }

    // === ArchiveVerifierTests (16) ===
    [Fact] public async Task ValidV2_Passes() => Assert.True((await BackupPackageV2Verifier.VerifyAsync(ValidZipPath(), 3)).IsValid);
    [Fact] public async Task CorruptZip() { var z = Path.Combine(_testDir, "c.zip"); File.WriteAllBytes(z, [0x50, 0x4B, 0x03, 0x04, 0xFF]); Assert.Contains((await BackupPackageV2Verifier.VerifyAsync(z, 3)).Errors, e => e.Code == BackupVerificationError.InvalidZip); }
    [Fact] public async Task MissingManifest() { var z = Path.Combine(_testDir, "m.zip"); using (var a = ZipFile.Open(z, ZipArchiveMode.Create)) WriteRaw(a, "x.txt", [1]); Assert.Contains((await BackupPackageV2Verifier.VerifyAsync(z, 3)).Errors, e => e.Code == BackupVerificationError.MissingManifest); }
    [Fact] public async Task DuplicateEntries() { var z = Path.Combine(_testDir, "dup.zip"); using (var a = ZipFile.Open(z, ZipArchiveMode.Create)) { WriteManifest(a, MakeManifest()); WriteRaw(a, "test/file.txt", [1]); WriteRaw(a, "test/file.txt", [2]); } Assert.Contains((await BackupPackageV2Verifier.VerifyAsync(z, 3)).Errors, e => e.Code == BackupVerificationError.DuplicateZipEntry); }
    [Fact] public async Task AbsolutePath() { var z = MakeZip(MakeManifest(), new List<(string, byte[])> { ("/etc/passwd", [1]) }); Assert.Contains((await BackupPackageV2Verifier.VerifyAsync(z, 3)).Errors, e => e.Code == BackupVerificationError.UnsafePackagePath); }
    [Fact] public async Task DriveLetter() { var z = MakeZip(MakeManifest(), new List<(string, byte[])> { ("C:\\test.txt", [1]) }); Assert.Contains((await BackupPackageV2Verifier.VerifyAsync(z, 3)).Errors, e => e.Code == BackupVerificationError.UnsafePackagePath); }
    [Fact] public async Task Backslash() { var z = MakeZip(MakeManifest(), new List<(string, byte[])> { ("foo\\bar.txt", [1]) }); var r = await BackupPackageV2Verifier.VerifyAsync(z, 3); Assert.False(r.IsValid); }
    [Fact] public async Task Traversal() { var z = MakeZip(MakeManifest(), new List<(string, byte[])> { ("../x.txt", [1]) }); Assert.Contains((await BackupPackageV2Verifier.VerifyAsync(z, 3)).Errors, e => e.Code == BackupVerificationError.UnsafePackagePath); }
    [Fact] public async Task CorruptManifestJson() { var z = Path.Combine(_testDir, "cj.zip"); using (var a = ZipFile.Open(z, ZipArchiveMode.Create)) WriteRaw(a, "manifest.json", System.Text.Encoding.UTF8.GetBytes("{bad")); Assert.Contains((await BackupPackageV2Verifier.VerifyAsync(z, 3)).Errors, e => e.Code == BackupVerificationError.InvalidManifestJson); }
    [Fact] public async Task SchemaFailure() { var z = MakeZip(new() { FormatVersion = 2, CreatedAt = "2026-01-01" }); Assert.Contains((await BackupPackageV2Verifier.VerifyAsync(z, 3)).Errors, e => e.Code == BackupVerificationError.ManifestSchemaInvalid); }
    [Fact] public async Task WrongFormatVersion() { var z = Path.Combine(_testDir, "wv.zip"); var m = MakeManifest(); using (var a = ZipFile.Open(z, ZipArchiveMode.Create)) { var mj = JsonSerializer.Serialize(m).Replace("\"formatVersion\":2", "\"formatVersion\":1"); WriteManifest(a, JsonSerializer.Deserialize<BackupPackageV2Manifest>(mj)!); } var r = await BackupPackageV2Verifier.VerifyAsync(z, 3); Assert.False(r.IsValid); }
    [Fact] public async Task MissingDatabase() { var z = MakeZip(MakeManifest(files: new())); Assert.Contains((await BackupPackageV2Verifier.VerifyAsync(z, 3)).Errors, e => e.Code == BackupVerificationError.MissingDatabase); }
    [Fact] public async Task UnexpectedBusinessFile() { var z = MakeZip(MakeManifest(files: new()), new List<(string, byte[])> { ("random_file.txt", [1]) }); Assert.Contains((await BackupPackageV2Verifier.VerifyAsync(z, 3)).Errors, e => e.Code == BackupVerificationError.UnexpectedBusinessFile); }
    [Fact] public async Task SizeMismatch() { var src = ValidZipPath(); var z = Path.Combine(_testDir, "sm.zip"); File.Copy(src, z); using (var a = ZipFile.Open(z, ZipArchiveMode.Update)) { var me = a.GetEntry("manifest.json")!; using var s = me.Open(); using var sr = new StreamReader(s); var j = sr.ReadToEnd(); s.Position = 0; s.SetLength(0); var doc = System.Text.Json.JsonDocument.Parse(j); var files = doc.RootElement.GetProperty("files"); if (files.GetArrayLength() > 0) { var origSize = files[0].GetProperty("size").GetInt64(); var nj = j.Replace($"\"size\":{origSize}", $"\"size\":{origSize + 999}"); var b = System.Text.Encoding.UTF8.GetBytes(nj); s.Write(b, 0, b.Length); } } Assert.Contains((await BackupPackageV2Verifier.VerifyAsync(z, 3)).Errors, e => e.Code == BackupVerificationError.SizeMismatch); }
    [Fact] public async Task Sha256Mismatch() { var src = ValidZipPath(); var z = Path.Combine(_testDir, "sh.zip"); File.Copy(src, z); using (var a = ZipFile.Open(z, ZipArchiveMode.Update)) { var me = a.GetEntry("manifest.json")!; using var s = me.Open(); using var sr = new StreamReader(s); var j = sr.ReadToEnd(); s.Position = 0; s.SetLength(0); var doc = System.Text.Json.JsonDocument.Parse(j); var files = doc.RootElement.GetProperty("files"); if (files.GetArrayLength() > 0) { var origHash = files[0].GetProperty("sha256").GetString()!; var nj = j.Replace(origHash, new string('0', 64)); var b = System.Text.Encoding.UTF8.GetBytes(nj); s.Write(b, 0, b.Length); } } Assert.Contains((await BackupPackageV2Verifier.VerifyAsync(z, 3)).Errors, e => e.Code == BackupVerificationError.Sha256Mismatch); }
    [Fact] public async Task Cancelled_Verify() { var r = await BackupPackageV2Verifier.VerifyAsync(ValidZipPath(), 3, new CancellationToken(true)); Assert.True(r.IsCancelled); Assert.Contains(r.Errors, e => e.Code == BackupVerificationError.Cancelled); }

    // === RestoreDatabaseVerifierTests (8) ===
    [Fact] public async Task ValidDb_AllChecks() { var r = await BackupPackageV2Verifier.VerifyAsync(ValidZipPath(3), 3); Assert.True(r.DatabaseIntegrityOk); Assert.True(r.DatabaseForeignKeyOk); Assert.True(r.DatabaseUserVersionOk); Assert.True(r.DatabaseTablesOk); }
    [Fact] public async Task UserVersionMismatch() { Assert.Contains((await BackupPackageV2Verifier.VerifyAsync(ValidZipPath(3), 5)).Errors, e => e.Code == BackupVerificationError.DatabaseUserVersionMismatch); }
    [Fact] public async Task MissingRequiredTable() { var (dbPath, dbBytes) = CreateDbFile("CREATE TABLE items (id TEXT PRIMARY KEY);"); var z = Path.Combine(_testDir, "nt.zip"); using (var a = ZipFile.Open(z, ZipArchiveMode.Create)) { WriteManifest(a, MakeManifest(files: new() { new() { Path = "database/archiver.db", Type = "database", Size = dbBytes.Length, Sha256 = HexHash(SHA256.HashData(dbBytes)), Required = true } })); var e = a.CreateEntry("database/archiver.db"); var s = e.Open(); s.Write(dbBytes, 0, dbBytes.Length); s.Close(); } DeleteDbFile(dbPath); Assert.Contains((await BackupPackageV2Verifier.VerifyAsync(z, 3)).Errors, e => e.Code == BackupVerificationError.DatabaseSchemaInvalid); }
    [Fact] public async Task MissingRequiredColumn() { var (dbPath, dbBytes) = CreateDbFile("CREATE TABLE items (id TEXT PRIMARY KEY,title TEXT);CREATE TABLE media_assets (id TEXT PRIMARY KEY,item_id TEXT NOT NULL,type TEXT NOT NULL,file_name TEXT NOT NULL,download_status TEXT NOT NULL DEFAULT 'pending',created_at REAL NOT NULL);CREATE TABLE folders (id TEXT PRIMARY KEY,name TEXT NOT NULL,platform TEXT NOT NULL,created_at REAL NOT NULL,sort_order INTEGER NOT NULL DEFAULT 0);CREATE TABLE trash_records (id TEXT PRIMARY KEY,item_id TEXT NOT NULL,deleted_at REAL NOT NULL,auto_delete_at REAL NOT NULL,original_archive_status TEXT NOT NULL);CREATE TABLE import_tasks (id TEXT PRIMARY KEY,original_url TEXT NOT NULL,normalized_url TEXT NOT NULL,status TEXT NOT NULL DEFAULT 'pending',created_at REAL NOT NULL);CREATE TABLE custom_platforms (id TEXT PRIMARY KEY,name TEXT NOT NULL,created_at REAL NOT NULL,sort_order INTEGER NOT NULL DEFAULT 0);"); var z = Path.Combine(_testDir, "nc.zip"); using (var a = ZipFile.Open(z, ZipArchiveMode.Create)) { WriteManifest(a, MakeManifest(files: new() { new() { Path = "database/archiver.db", Type = "database", Size = dbBytes.Length, Sha256 = HexHash(SHA256.HashData(dbBytes)), Required = true } })); var e = a.CreateEntry("database/archiver.db"); var s = e.Open(); s.Write(dbBytes, 0, dbBytes.Length); s.Close(); } DeleteDbFile(dbPath); Assert.Contains((await BackupPackageV2Verifier.VerifyAsync(z, 3)).Errors, e => e.Code == BackupVerificationError.DatabaseSchemaInvalid); }
    [Fact] public async Task CorruptDb() { var corruptBytes = new byte[] { 0x00, 0x01, 0x02 }; var z = Path.Combine(_testDir, "cdb.zip"); using (var a = ZipFile.Open(z, ZipArchiveMode.Create)) { WriteManifest(a, MakeManifest(files: new() { new() { Path = "database/archiver.db", Type = "database", Size = corruptBytes.Length, Sha256 = HexHash(SHA256.HashData(corruptBytes)), Required = true } })); var e = a.CreateEntry("database/archiver.db"); var s = e.Open(); s.Write(corruptBytes, 0, corruptBytes.Length); s.Close(); } Assert.Contains((await BackupPackageV2Verifier.VerifyAsync(z, 3)).Errors, e => e.Code == BackupVerificationError.DatabaseIntegrityFailed || e.Code == BackupVerificationError.DatabaseOpenFailed); }
    [Fact] public async Task ForeignKeyViolation() { var z = ValidZipPath(3); var r = await BackupPackageV2Verifier.VerifyAsync(z, 3); Assert.True(r.DatabaseForeignKeyOk, "FK check should pass for valid DB"); }
    [Fact] public async Task DbCancelled() { var r = await BackupPackageV2Verifier.VerifyAsync(ValidZipPath(3), 3, new CancellationToken(true)); Assert.True(r.IsCancelled); }
    [Fact] public async Task InvalidDbFile() { var corruptBytes = new byte[] { 0x00, 0x01, 0x02 }; var z = Path.Combine(_testDir, "idb.zip"); using (var a = ZipFile.Open(z, ZipArchiveMode.Create)) { WriteManifest(a, MakeManifest(files: new() { new() { Path = "database/archiver.db", Type = "database", Size = corruptBytes.Length, Sha256 = HexHash(SHA256.HashData(corruptBytes)), Required = true } })); var e = a.CreateEntry("database/archiver.db"); var s = e.Open(); s.Write(corruptBytes, 0, corruptBytes.Length); s.Close(); } Assert.Contains((await BackupPackageV2Verifier.VerifyAsync(z, 3)).Errors, e => e.Code == BackupVerificationError.DatabaseIntegrityFailed || e.Code == BackupVerificationError.DatabaseOpenFailed); }
}
