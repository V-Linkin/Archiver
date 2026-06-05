namespace Gatherly.Windows.Models.Enums;

public enum ContentStatus
{
    normal,
    parseFailed,
    mediaIncomplete,
    sourceDeleted,
    trashed
}

public static class ContentStatusExtensions
{
    public static string ToRawValue(this ContentStatus s) => s.ToString();

    public static ContentStatus FromRawValue(string value) =>
        Enum.TryParse<ContentStatus>(value, true, out var result) ? result : ContentStatus.normal;
}
