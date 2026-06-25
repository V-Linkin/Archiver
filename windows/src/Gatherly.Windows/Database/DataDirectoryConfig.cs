using System.Text.Json;

namespace Gatherly.Windows.Database;

/// <summary>
/// 数据目录配置管理（对齐 macOS DataDirectory.swift UserDefaults 方案）
/// </summary>
public static class DataDirectoryConfig
{
    private static readonly string ConfigFile = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Gatherly", "data_directory.json");

    private static string? _cachedPath;

    /// <summary>
    /// 获取当前数据目录（支持自定义目录）
    /// </summary>
    public static string DataDirectory
    {
        get
        {
            if (_cachedPath != null && Directory.Exists(_cachedPath))
                return _cachedPath;

            var customPath = LoadCustomPath();
            if (!string.IsNullOrEmpty(customPath) && Directory.Exists(customPath))
            {
                _cachedPath = customPath;
                return customPath;
            }

            var defaultPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Gatherly");
            _cachedPath = defaultPath;
            return defaultPath;
        }
    }

    /// <summary>
    /// 是否使用了自定义目录
    /// </summary>
    public static bool IsCustom => !string.IsNullOrEmpty(LoadCustomPath());

    /// <summary>
    /// 保存自定义目录路径
    /// </summary>
    public static void SetCustom(string path)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ConfigFile)!);
            var config = new { customDataDirectory = path };
            File.WriteAllText(ConfigFile, JsonSerializer.Serialize(config));
            _cachedPath = path;
        }
        catch { }
    }

    /// <summary>
    /// 恢复默认目录
    /// </summary>
    public static void ResetToDefault()
    {
        try
        {
            if (File.Exists(ConfigFile))
                File.Delete(ConfigFile);
            _cachedPath = null;
        }
        catch { }
    }

    private static string? LoadCustomPath()
    {
        try
        {
            if (!File.Exists(ConfigFile)) return null;
            var json = File.ReadAllText(ConfigFile);
            var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("customDataDirectory", out var path))
                return path.GetString();
        }
        catch { }
        return null;
    }
}
