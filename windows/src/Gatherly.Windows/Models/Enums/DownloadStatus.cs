namespace Gatherly.Windows.Models.Enums;

public enum DownloadStatus
{
    pending,
    downloading,
    completed,
    failed,
    skipped
}

public static class DownloadStatusExtensions
{
    public static string ToRawValue(this DownloadStatus s) => s.ToString();

    public static DownloadStatus FromRawValue(string value) =>
        Enum.TryParse<DownloadStatus>(value, true, out var result) ? result : DownloadStatus.pending;
}
