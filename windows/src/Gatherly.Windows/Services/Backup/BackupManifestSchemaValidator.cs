using System.Text.Json;
using System.Text.Json.Nodes;
using Json.Schema;

namespace Gatherly.Windows.Services.Backup;

public static class BackupManifestSchemaValidator
{
    private static readonly string DefaultSchemaPath = Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory,
        "backup-package-v2.schema.json");

    private static JsonSchema? _schema;
    private static string? _schemaPathOverride;

    private static string EffectiveSchemaPath => _schemaPathOverride ?? DefaultSchemaPath;

    private static JsonSchema GetSchema()
    {
        if (_schema != null) return _schema;
        if (!File.Exists(EffectiveSchemaPath))
            throw new FileNotFoundException($"V2 manifest schema not found at {EffectiveSchemaPath}");
        var json = File.ReadAllText(EffectiveSchemaPath);
        _schema = JsonSchema.FromText(json);
        return _schema;
    }

    public static void ResetSchemaCache(string? schemaPath = null)
    {
        _schema = null;
        _schemaPathOverride = schemaPath;
    }

    public static (bool valid, string? error) Validate(string manifestJson)
    {
        try
        {
            var schema = GetSchema();
            var node = JsonNode.Parse(manifestJson);
            var results = schema.Evaluate(node);
            if (results.IsValid)
                return (true, null);

            var messages = results.Errors.Select(e => $"{e.Key}: {e.Value}").ToList();
            return (false, $"Schema validation failed: {string.Join("; ", messages)}");
        }
        catch (Exception ex)
        {
            return (false, $"Schema validation error: {ex.Message}");
        }
    }

    public static (bool valid, string? error) ValidateManifestFile(string zipPath)
    {
        try
        {
            using var archive = System.IO.Compression.ZipFile.OpenRead(zipPath);
            var manifestEntry = archive.GetEntry("manifest.json");
            if (manifestEntry == null)
                return (false, "manifest.json not found in archive");
            if (manifestEntry.Length > 1024 * 1024)
                return (false, "manifest.json exceeds 1 MiB limit");

            using var stream = manifestEntry.Open();
            using var reader = new StreamReader(stream);
            var json = reader.ReadToEnd();
            return Validate(json);
        }
        catch (Exception ex)
        {
            return (false, $"Failed to read manifest: {ex.Message}");
        }
    }
}
