namespace Gatherly.Windows.Models.Enums;

public enum MediaType
{
    image,
    cover,
    video,
    thumbnail
}

public static class MediaTypeExtensions
{
    public static string ToRawValue(this MediaType t) => t.ToString();

    public static MediaType FromRawValue(string value) =>
        Enum.TryParse<MediaType>(value, true, out var result) ? result : MediaType.image;
}
