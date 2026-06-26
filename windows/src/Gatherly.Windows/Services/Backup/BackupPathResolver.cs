using System.Text;

namespace Gatherly.Windows.Services.Backup;

public static class BackupPathResolver
{
    private static readonly HashSet<string> WindowsReservedNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
    };

    public static string NormalizePackagePath(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
            throw new ArgumentException("路径不能为空");

        var path = relativePath.Replace('\\', '/').Trim();

        if (path.Length == 0)
            throw new ArgumentException("路径不能为空");

        if (Path.IsPathRooted(path) || path.StartsWith("/", StringComparison.Ordinal))
            throw new ArgumentException($"禁止绝对路径: {path}");

        if (path.Contains(':'))
            throw new ArgumentException($"禁止盘符: {path}");

        if (path.Contains(".."))
            throw new ArgumentException($"禁止路径穿越: {path}");

        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0)
            throw new ArgumentException("路径不能为空");

        var normalized = new StringBuilder();
        foreach (var segment in segments)
        {
            var s = segment.Trim('.');
            if (s.Length == 0) continue;
            s = s.TrimEnd(' ');
            s = s.Replace("\0", "_");
            normalized.Append('/');
            normalized.Append(Nfc(s));
        }

        if (normalized.Length == 0)
            throw new ArgumentException("规范化后路径为空");

        return normalized.ToString()[1..];
    }

    public static string GetEquivalenceKey(string packagePath)
    {
        var normalized = NormalizePackagePath(packagePath);
        var lower = normalized.ToLowerInvariant();
        var lastDot = lower.LastIndexOf('.');
        if (lastDot > 0)
            lower = lower[..lastDot] + lower[lastDot..];
        return lower;
    }

    public static string ResolveCollision(string packagePath, string sha256Prefix)
    {
        var dir = Path.GetDirectoryName(packagePath)?.Replace('\\', '/') ?? "";
        var nameWithoutExt = Path.GetFileNameWithoutExtension(packagePath).Replace('\\', '/');
        var ext = Path.GetExtension(packagePath);
        var suffix = sha256Prefix.Length > 12 ? sha256Prefix[..12] : sha256Prefix;

        if (!string.IsNullOrEmpty(dir))
            return $"{dir}/{nameWithoutExt}_{suffix}{ext}";
        return $"{nameWithoutExt}_{suffix}{ext}";
    }

    public static bool ValidatePackagePath(string packagePath)
    {
        try
        {
            NormalizePackagePath(packagePath);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string Nfc(string s) => s.Normalize(NormalizationForm.FormC);
}
