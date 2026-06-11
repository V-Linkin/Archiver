using Gatherly.Windows.Models;
using Gatherly.Windows.Models.Enums;
using Microsoft.Data.Sqlite;

namespace Gatherly.Windows.Database;

/// <summary>
/// MediaAsset 数据访问层
/// </summary>
public class MediaRepository
{
    private readonly SqliteConnection _connection;

    public MediaRepository(SqliteConnection connection)
    {
        _connection = connection;
    }

    public async Task<List<MediaAsset>> GetByItemIdAsync(Guid itemId)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT * FROM media_assets WHERE item_id COLLATE NOCASE=$itemId ORDER BY created_at";
        cmd.Parameters.AddWithValue("$itemId", itemId.ToString("D"));
        using var reader = await cmd.ExecuteReaderAsync();
        var assets = new List<MediaAsset>();
        while (await reader.ReadAsync())
        {
            assets.Add(SqliteRowMapper.ReadMediaAsset(reader));
        }
        return assets;
    }

    public async Task InsertAsync(MediaAsset asset)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO media_assets (id, item_id, type, local_path, remote_url, file_name, file_size, mime_type, width, height, duration, checksum, download_status, thumbnail_path, created_at)
            VALUES ($id, $itemId, $type, $localPath, $remoteUrl, $fileName, $fileSize, $mimeType, $width, $height, $duration, $checksum, $downloadStatus, $thumbnailPath, $createdAt)";
        cmd.Parameters.AddWithValue("$id", asset.Id.ToString("D"));
        cmd.Parameters.AddWithValue("$itemId", asset.ItemId.ToString("D"));
        cmd.Parameters.AddWithValue("$type", asset.Type.ToRawValue());
        cmd.Parameters.AddWithValue("$localPath", (object?)asset.LocalPath ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$remoteUrl", (object?)asset.RemoteUrl ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$fileName", asset.FileName);
        cmd.Parameters.AddWithValue("$fileSize", asset.FileSize);
        cmd.Parameters.AddWithValue("$mimeType", (object?)asset.MimeType ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$width", asset.Width ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("$height", asset.Height ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("$duration", asset.Duration ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("$checksum", (object?)asset.Checksum ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$downloadStatus", asset.DownloadStatus.ToRawValue());
        cmd.Parameters.AddWithValue("$thumbnailPath", (object?)asset.ThumbnailPath ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$createdAt", asset.CreatedAt.ToUnixTimeSeconds());
        await cmd.ExecuteNonQueryAsync();
    }
}
