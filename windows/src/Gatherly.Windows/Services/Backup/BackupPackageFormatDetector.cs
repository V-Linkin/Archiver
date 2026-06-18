using System.IO.Compression;
using System.Text.Json;

namespace Gatherly.Windows.Services.Backup;

public enum BackupPackageFormat
{
    Unknown,
    V1MacOS,
    V2
}

public static class BackupPackageFormatDetector
{
    private const int MaxManifestReadSize = 1024 * 1024;

    public static (BackupPackageFormat format, string? error) Detect(string zipPath)
    {
        if (!File.Exists(zipPath))
            return (BackupPackageFormat.Unknown, "文件不存在");

        try
        {
            using var archive = ZipFile.OpenRead(zipPath);
            var entries = archive.Entries.Select(e => e.FullName).ToHashSet(StringComparer.OrdinalIgnoreCase);

            // Check V2 first
            if (entries.Contains("manifest.json"))
            {
                var manifestEntry = archive.GetEntry("manifest.json");
                if (manifestEntry != null && manifestEntry.Length <= MaxManifestReadSize)
                {
                    using var stream = manifestEntry.Open();
                    using var reader = new StreamReader(stream);
                    var json = reader.ReadToEnd();
                    var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("formatVersion", out var ver) && ver.GetInt32() == 2)
                        return (BackupPackageFormat.V2, null);
                }
                return (BackupPackageFormat.Unknown, "manifest.json 无效或 formatVersion != 2");
            }

            // Check V1 macOS
            if (entries.Contains("archiver.db") && entries.Contains("backup_info.json"))
                return (BackupPackageFormat.V1MacOS, null);

            return (BackupPackageFormat.Unknown, "未识别的备份格式");
        }
        catch (InvalidDataException)
        {
            return (BackupPackageFormat.Unknown, "ZIP文件损坏");
        }
        catch (Exception ex)
        {
            return (BackupPackageFormat.Unknown, $"格式检测失败: {ex.Message}");
        }
    }
}
