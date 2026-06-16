using System.Text.Json;
using Gatherly.Windows.Database;
using Gatherly.Windows.Models.Enums;

namespace Gatherly.Windows.Services;

/// <summary>
/// 系统平台 → CustomPlatform UUID 稳定映射
/// 文件: %LOCALAPPDATA%/Gatherly/system_platform_custom_map.json
/// </summary>
public class SystemPlatformCustomMap
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };

    private readonly string _filePath;
    private readonly object _lock = new();
    private Dictionary<string, string> _mappings;

    public SystemPlatformCustomMap(string? dataDirectory = null)
    {
        _filePath = Path.Combine(dataDirectory ?? DatabasePaths.DataDirectory, "system_platform_custom_map.json");
        _mappings = Load();
    }

    public Guid? GetCustomPlatformId(string rawValue)
    {
        lock (_lock)
        {
            if (_mappings.TryGetValue(rawValue, out var id) && Guid.TryParse(id, out var guid))
                return guid;
        }
        return null;
    }

    public void SetMapping(string rawValue, Guid customPlatformId)
    {
        lock (_lock)
        {
            _mappings[rawValue] = customPlatformId.ToString("D");
            Save();
        }
    }

    public void RemoveMapping(string rawValue)
    {
        lock (_lock)
        {
            if (_mappings.Remove(rawValue))
                Save();
        }
    }

    public void RemoveMappingsByCustomPlatformId(Guid customPlatformId)
    {
        var idStr = customPlatformId.ToString("D");
        lock (_lock)
        {
            var keys = _mappings.Where(kvp => string.Equals(kvp.Value, idStr, StringComparison.OrdinalIgnoreCase))
                .Select(kvp => kvp.Key).ToList();
            foreach (var key in keys)
                _mappings.Remove(key);
            if (keys.Count > 0)
                Save();
        }
    }

    private Dictionary<string, string> Load()
    {
        try
        {
            if (!File.Exists(_filePath))
                return new(StringComparer.OrdinalIgnoreCase);
            var json = File.ReadAllText(_filePath);
            if (string.IsNullOrWhiteSpace(json))
                return new(StringComparer.OrdinalIgnoreCase);
            var doc = JsonSerializer.Deserialize<JsonElement>(json, JsonOptions);
            var mappings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (doc.TryGetProperty("mappings", out var m) && m.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in m.EnumerateObject())
                {
                    if (!string.IsNullOrWhiteSpace(prop.Value.GetString()))
                        mappings[prop.Name] = prop.Value.GetString()!;
                }
            }
            return mappings;
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
        catch (JsonException) { }
        return new(StringComparer.OrdinalIgnoreCase);
    }

    private void Save()
    {
        var dir = Path.GetDirectoryName(_filePath);
        if (dir != null) Directory.CreateDirectory(dir);

        var obj = new { version = 1, mappings = _mappings };
        var json = JsonSerializer.Serialize(obj, JsonOptions);
        var tempFile = _filePath + ".tmp";
        File.WriteAllText(tempFile, json);
        if (File.Exists(_filePath)) File.Delete(_filePath);
        File.Move(tempFile, _filePath);
    }
}
