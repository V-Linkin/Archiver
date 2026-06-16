using System.Text.Json;
using Gatherly.Windows.Database;
using Gatherly.Windows.Models.Enums;

namespace Gatherly.Windows.Services;

/// <summary>
/// 系统平台显示名称持久化存储
/// 文件路径: %LOCALAPPDATA%/Gatherly/platform_display_names.json
/// </summary>
public class SystemPlatformDisplayNames
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false
    };

    private readonly string _filePath;
    private readonly object _lock = new();
    private Dictionary<string, string> _names;

    public SystemPlatformDisplayNames(string? dataDirectory = null)
    {
        _filePath = Path.Combine(dataDirectory ?? DatabasePaths.DataDirectory, "platform_display_names.json");
        _names = Load();
    }

    public string GetDisplayName(Platform platform)
    {
        if (platform == Platform.custom)
            return platform.GetDisplayName();

        lock (_lock)
        {
            if (_names.TryGetValue(platform.ToRawValue(), out var name))
                return name;
        }
        return platform.GetDisplayName();
    }

    public void SetDisplayName(Platform platform, string displayName)
    {
        if (platform == Platform.custom)
            throw new ArgumentException("不能为自定义平台设置显示名称。");

        var trimmed = displayName.Trim();
        if (string.IsNullOrEmpty(trimmed))
            throw new ArgumentException("平台显示名称不能为空。");

        lock (_lock)
        {
            _names[platform.ToRawValue()] = trimmed;
            Save(_names);
        }
    }

    public void ResetDisplayName(Platform platform)
    {
        if (platform == Platform.custom)
            throw new ArgumentException("自定义平台不支持重置。");

        lock (_lock)
        {
            if (_names.Remove(platform.ToRawValue()))
                Save(_names);
        }
    }

    private Dictionary<string, string> Load()
    {
        try
        {
            if (!File.Exists(_filePath))
                return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            var json = File.ReadAllText(_filePath);
            if (string.IsNullOrWhiteSpace(json))
                return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            var deserialized = JsonSerializer.Deserialize<Dictionary<string, string>>(json, JsonOptions);
            if (deserialized == null)
                return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var kvp in deserialized)
            {
                if (!string.IsNullOrWhiteSpace(kvp.Value))
                    result[kvp.Key] = kvp.Value;
            }
            return result;
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
        catch (JsonException) { }
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    private void Save(Dictionary<string, string> names)
    {
        var dir = Path.GetDirectoryName(_filePath);
        if (dir != null)
            Directory.CreateDirectory(dir);

        var json = JsonSerializer.Serialize(names, JsonOptions);
        var tempFile = _filePath + ".tmp";
        File.WriteAllText(tempFile, json);

        if (File.Exists(_filePath))
            File.Delete(_filePath);
        File.Move(tempFile, _filePath);
    }
}
