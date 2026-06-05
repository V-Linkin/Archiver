namespace Gatherly.Windows.Models.Enums;

public enum ArchiveStatus
{
    favorite,
    inspiration,
    pending,
    archived
}

public static class ArchiveStatusExtensions
{
    public static string ToRawValue(this ArchiveStatus s) => s.ToString();

    public static ArchiveStatus FromRawValue(string value) =>
        Enum.TryParse<ArchiveStatus>(value, true, out var result) ? result : ArchiveStatus.pending;
}
