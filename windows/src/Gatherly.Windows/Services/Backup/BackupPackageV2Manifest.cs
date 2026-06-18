using System.Text.Json.Serialization;

namespace Gatherly.Windows.Services.Backup;

public sealed class BackupPackageV2Manifest
{
    [JsonPropertyName("formatVersion")]
    public int FormatVersion { get; set; } = 2;

    [JsonPropertyName("createdAt")]
    public string CreatedAt { get; set; } = string.Empty;

    [JsonPropertyName("appVersion")]
    public string AppVersion { get; set; } = string.Empty;

    [JsonPropertyName("sourceApp")]
    public string SourceApp { get; set; } = "Gatherly";

    [JsonPropertyName("sourceOS")]
    public string SourceOS { get; set; } = "windows";

    [JsonPropertyName("databaseSchemaVersion")]
    public int DatabaseSchemaVersion { get; set; }

    [JsonPropertyName("databaseUserVersion")]
    public int DatabaseUserVersion { get; set; }

    [JsonPropertyName("counts")]
    public BackupCounts Counts { get; set; } = new();

    [JsonPropertyName("files")]
    public List<BackupFileEntry> Files { get; set; } = new();

    [JsonPropertyName("features")]
    public BackupFeatures Features { get; set; } = new();

    [JsonPropertyName("warnings")]
    public List<string> Warnings { get; set; } = new();
}

public sealed class BackupCounts
{
    [JsonPropertyName("items")]
    public int Items { get; set; }

    [JsonPropertyName("mediaAssets")]
    public int MediaAssets { get; set; }

    [JsonPropertyName("folders")]
    public int Folders { get; set; }

    [JsonPropertyName("customPlatforms")]
    public int CustomPlatforms { get; set; }

    [JsonPropertyName("importTasks")]
    public int ImportTasks { get; set; }

    [JsonPropertyName("trashRecords")]
    public int TrashRecords { get; set; }
}

public sealed class BackupFileEntry
{
    [JsonPropertyName("path")]
    public string Path { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("size")]
    public long Size { get; set; }

    [JsonPropertyName("sha256")]
    public string Sha256 { get; set; } = string.Empty;

    [JsonPropertyName("required")]
    public bool Required { get; set; }
}

public sealed class BackupFeatures
{
    [JsonPropertyName("hasDatabase")]
    public bool HasDatabase { get; set; }

    [JsonPropertyName("hasMedia")]
    public bool HasMedia { get; set; }

    [JsonPropertyName("hasPlatformLogos")]
    public bool HasPlatformLogos { get; set; }

    [JsonPropertyName("hasPlatformDisplayNames")]
    public bool HasPlatformDisplayNames { get; set; }

    [JsonPropertyName("hasSystemPlatformMappings")]
    public bool HasSystemPlatformMappings { get; set; }

    [JsonPropertyName("hasTrash")]
    public bool HasTrash { get; set; }

    [JsonPropertyName("hasFTS")]
    public bool HasFTS { get; set; }

    [JsonPropertyName("hasFolders")]
    public bool HasFolders { get; set; }
}

public enum BackupProgressStage
{
    Preparing,
    CheckingPending,
    SnapshottingDatabase,
    ExportingSettings,
    CollectingFiles,
    HashingFiles,
    CreatingArchive,
    VerifyingArchive,
    Finalizing,
    Completed,
    Failed,
    Cancelled
}

public sealed class BackupProgress
{
    public BackupProgressStage Stage { get; set; }
    public string? CurrentFile { get; set; }
    public int ProcessedFiles { get; set; }
    public int TotalFiles { get; set; }
    public long ProcessedBytes { get; set; }
    public long TotalBytes { get; set; }
    public string? Message { get; set; }

    public int Percentage => TotalFiles > 0 ? (int)(ProcessedBytes * 100 / Math.Max(TotalBytes, 1)) : 0;
}

public sealed record BackupV2Result(
    bool Success,
    string? BackupPath,
    string? ErrorMessage,
    BackupProgress? Progress = null
)
{
    public static BackupV2Result Ok(string path) => new(true, path, null);
    public static BackupV2Result Fail(string error) => new(false, null, error);
    public static BackupV2Result Cancelled() => new(false, null, null);
}
