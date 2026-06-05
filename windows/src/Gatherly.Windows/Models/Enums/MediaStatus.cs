namespace Gatherly.Windows.Models.Enums;

public enum MediaStatus
{
    complete,
    partial,
    failed,
    textOnly
}

public static class MediaStatusExtensions
{
    public static string ToRawValue(this MediaStatus s) => s.ToString();

    public static MediaStatus FromRawValue(string value) =>
        Enum.TryParse<MediaStatus>(value, true, out var result) ? result : MediaStatus.textOnly;
}
