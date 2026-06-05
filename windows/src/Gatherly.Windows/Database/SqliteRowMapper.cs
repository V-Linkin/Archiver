using Gatherly.Windows.Models;
using Gatherly.Windows.Models.Enums;
using Microsoft.Data.Sqlite;

namespace Gatherly.Windows.Database;

/// <summary>
/// SQLite 行到 C# Model 的映射工具
/// SQLite: REAL → DateTimeOffset (Unix seconds)
/// SQLite: TEXT → string / Guid / Enum
/// SQLite: INTEGER 0/1 → bool
/// SQLite: JSON TEXT → List<string>
/// </summary>
public static class SqliteRowMapper
{
    public static DateTimeOffset? ToDateTimeOffset(object? value)
    {
        if (value == null || value == DBNull.Value) return null;
        var seconds = Convert.ToDouble(value);
        return DateTimeOffset.FromUnixTimeSeconds((long)seconds);
    }

    public static DateTimeOffset ToDateTimeOffsetRequired(object? value)
    {
        return ToDateTimeOffset(value) ?? DateTimeOffset.MinValue;
    }

    public static Guid ToGuid(object? value)
    {
        if (value == null || value == DBNull.Value) return Guid.Empty;
        return Guid.TryParse(value.ToString(), out var result) ? result : Guid.Empty;
    }

    public static Guid? ToNullableGuid(object? value)
    {
        if (value == null || value ==DBNull.Value) return null;
        return Guid.TryParse(value.ToString(), out var result) ? result : null;
    }

    public static bool ToBool(object? value)
    {
        if (value == null || value == DBNull.Value) return false;
        return Convert.ToInt64(value) != 0;
    }

    public static int ToInt32(object? value, int defaultValue = 0)
    {
        if (value == null || value == DBNull.Value) return defaultValue;
        return Convert.ToInt32(value);
    }

    public static long ToInt64(object? value, long defaultValue = 0)
    {
        if (value == null || value == DBNull.Value) return defaultValue;
        return Convert.ToInt64(value);
    }

    public static string? ToStringOrNull(object? value)
    {
        if (value == null || value == DBNull.Value) return null;
        return value.ToString();
    }

    public static string ToStringRequired(object? value, string defaultValue = "")
    {
        return value?.ToString() ?? defaultValue;
    }

    public static Item ReadItem(SqliteDataReader reader)
    {
        return new Item
        {
            Id = ToGuid(reader["id"]),
            Title = ToStringOrNull(reader["title"]),
            Body = ToStringOrNull(reader["body"]),
            OriginalUrl = ToStringRequired(reader["original_url"]),
            Platform = PlatformExtensions.FromRawValue(ToStringRequired(reader["platform"])),
            PlatformContentId = ToStringOrNull(reader["platform_content_id"]),
            NormalizedUrl = ToStringRequired(reader["normalized_url"]),
            Author = ToStringOrNull(reader["author"]),
            AuthorId = ToStringOrNull(reader["author_id"]),
            PublishDate = ToDateTimeOffset(reader["publish_date"]),
            ImportDate = ToDateTimeOffsetRequired(reader["import_date"]),
            ModifyDate = ToDateTimeOffsetRequired(reader["modify_date"]),
            ContentStatus = ContentStatusExtensions.FromRawValue(ToStringRequired(reader["content_status"])),
            ArchiveStatus = ArchiveStatusExtensions.FromRawValue(ToStringRequired(reader["archive_status"])),
            MediaStatus = MediaStatusExtensions.FromRawValue(ToStringRequired(reader["media_status"])),
            CoverAssetId = ToNullableGuid(reader["cover_asset_id"]),
            FolderId = ToNullableGuid(reader["folder_id"]),
            Remark = ToStringOrNull(reader["remark"]),
            IsStarred = ToBool(reader["is_starred"]),
            Version = ToInt32(reader["version"]),
            DeletedAt = ToDateTimeOffset(reader["deleted_at"]),
            CustomPlatformId = ToNullableGuid(reader["custom_platform_id"])
        };
    }

    public static Folder ReadFolder(SqliteDataReader reader)
    {
        return new Folder
        {
            Id = ToGuid(reader["id"]),
            Name = ToStringRequired(reader["name"]),
            ParentId = ToNullableGuid(reader["parent_id"]),
            Platform = PlatformExtensions.FromRawValue(ToStringRequired(reader["platform"])),
            CustomPlatformId = ToNullableGuid(reader["custom_platform_id"]),
            CreatedAt = ToDateTimeOffsetRequired(reader["created_at"]),
            SortOrder = ToInt32(reader["sort_order"])
        };
    }

    public static MediaAsset ReadMediaAsset(SqliteDataReader reader)
    {
        return new MediaAsset
        {
            Id = ToGuid(reader["id"]),
            ItemId = ToGuid(reader["item_id"]),
            Type = MediaTypeExtensions.FromRawValue(ToStringRequired(reader["type"])),
            LocalPath = ToStringOrNull(reader["local_path"]),
            RemoteUrl = ToStringOrNull(reader["remote_url"]),
            FileName = ToStringRequired(reader["file_name"]),
            FileSize = ToInt64(reader["file_size"]),
            MimeType = ToStringOrNull(reader["mime_type"]),
            Width = reader["width"] == DBNull.Value ? null : Convert.ToInt32(reader["width"]),
            Height = reader["height"] == DBNull.Value ? null : Convert.ToInt32(reader["height"]),
            Duration = reader["duration"] == DBNull.Value ? null : Convert.ToDouble(reader["duration"]),
            Checksum = ToStringOrNull(reader["checksum"]),
            DownloadStatus = DownloadStatusExtensions.FromRawValue(ToStringRequired(reader["download_status"])),
            ThumbnailPath = ToStringOrNull(reader["thumbnail_path"]),
            CreatedAt = ToDateTimeOffsetRequired(reader["created_at"])
        };
    }

    public static CustomPlatform ReadCustomPlatform(SqliteDataReader reader)
    {
        return new CustomPlatform
        {
            Id = ToGuid(reader["id"]),
            Name = ToStringRequired(reader["name"]),
            LogoPath = ToStringOrNull(reader["logo_path"]),
            CreatedAt = ToDateTimeOffsetRequired(reader["created_at"]),
            SortOrder = ToInt32(reader["sort_order"])
        };
    }

    public static TrashRecord ReadTrashRecord(SqliteDataReader reader)
    {
        var mediaPathsJson = ToStringOrNull(reader["media_paths"]);
        var mediaPaths = string.IsNullOrEmpty(mediaPathsJson)
            ? new List<string>()
            : System.Text.Json.JsonSerializer.Deserialize<List<string>>(mediaPathsJson) ?? new List<string>();

        return new TrashRecord
        {
            Id = ToGuid(reader["id"]),
            ItemId = ToGuid(reader["item_id"]),
            DeletedAt = ToDateTimeOffsetRequired(reader["deleted_at"]),
            AutoDeleteAt = ToDateTimeOffsetRequired(reader["auto_delete_at"]),
            OriginalFolderId = ToNullableGuid(reader["original_folder_id"]),
            OriginalArchiveStatus = ArchiveStatusExtensions.FromRawValue(ToStringRequired(reader["original_archive_status"])),
            MediaPaths = mediaPaths
        };
    }
}
