using Gatherly.Windows.Database;
using Gatherly.Windows.Models;
using Gatherly.Windows.Models.Enums;
using Microsoft.Data.Sqlite;

namespace Gatherly.Windows.Services;

public class SystemPlatformItemMigrationService
{
    private readonly SqliteConnection _connection;
    private readonly CustomPlatformRepository _customPlatformRepo;
    private readonly SystemPlatformCustomMap _customMap;
    private readonly SystemPlatformDisplayNames _displayNames;
    private readonly MigrationBackupService? _backupService;

    private static readonly Platform[] SystemPlatforms =
    {
        Platform.douyin, Platform.xiaohongshu, Platform.coolapk,
        Platform.bilibili, Platform.github, Platform.youtube,
        Platform.x, Platform.weibo, Platform.zhihu, Platform.douban
    };

    public SystemPlatformItemMigrationService(
        SqliteConnection connection,
        CustomPlatformRepository customPlatformRepo,
        SystemPlatformCustomMap customMap,
        SystemPlatformDisplayNames displayNames,
        MigrationBackupService? backupService = null)
    {
        _connection = connection;
        _customPlatformRepo = customPlatformRepo;
        _customMap = customMap;
        _displayNames = displayNames;
        _backupService = backupService;
    }

    public MigrationResult Migrate()
    {
        var plan = BuildMigrationPlan();
        if (plan.TotalSourceItemCount == 0)
            return MigrationResult.NoWork();

        if (_backupService != null)
        {
            var backup = _backupService.CreatePreMigrationBackupAsync(plan.Items.ToDictionary(
                x => x.SystemRawValue, x => x.SourceItemCount)).GetAwaiter().GetResult();
            if (!backup.Success)
                return MigrationResult.BlockedByBackupFailure(backup.ErrorMessage!);
        }

        return ExecuteMigration(plan);
    }

    public MigrationPlan BuildMigrationPlan()
    {
        var plan = new MigrationPlan();
        var orphans = ScanSystemOrphans();
        if (orphans.Count == 0) return plan;

        var allPlatforms = _customPlatformRepo.GetAllAsync().GetAwaiter().GetResult();

        foreach (var (rawValue, count) in orphans)
        {
            var platform = PlatformExtensions.FromRawValue(rawValue);
            var defaultName = platform.GetDisplayName();
            var currentDisplayName = _displayNames.GetDisplayName(platform);

            var candidates = allPlatforms
                .Where(cp => string.Equals(cp.Name, defaultName, StringComparison.Ordinal) ||
                             string.Equals(cp.Name, defaultName, StringComparison.OrdinalIgnoreCase))
                .Select(cp => new MigrationCandidate
                {
                    Id = cp.Id, Name = cp.Name, SortOrder = cp.SortOrder,
                    IsExactCaseMatch = cp.Name == defaultName,
                    IsIgnoreCaseMatch = string.Equals(cp.Name, defaultName, StringComparison.OrdinalIgnoreCase)
                }).ToList();

            Guid? selectedId = null;
            bool willCreate = false;
            string? ambiguityWarning = null;

            // Rule 1: existing mapping
            var existing = _customMap.GetCustomPlatformId(rawValue);
            if (existing != null && allPlatforms.Any(cp => cp.Id == existing.Value))
            {
                selectedId = existing.Value;
            }
            else if (candidates.Count == 1)
            {
                selectedId = candidates[0].Id;
            }
            else if (candidates.Count > 1)
            {
                selectedId = candidates.OrderByDescending(c => c.IsExactCaseMatch)
                    .ThenBy(c => c.SortOrder).First().Id;
                ambiguityWarning = $"多个候选 ({candidates.Count})，已按确定性规则选择";
            }

            if (selectedId == null) willCreate = true;

            plan.Items.Add(new MigrationPlanItem
            {
                SystemRawValue = rawValue,
                SourceItemCount = count,
                DefaultDisplayName = defaultName,
                CurrentSystemDisplayName = currentDisplayName,
                CandidateCustomPlatforms = candidates,
                SelectedCustomPlatformId = selectedId,
                WillCreateCustomPlatform = willCreate,
                AmbiguityWarning = ambiguityWarning
            });
        }

        return plan;
    }

    private MigrationResult ExecuteMigration(MigrationPlan plan)
    {
        var result = new MigrationResult();
        var transaction = _connection.BeginTransaction();
        try
        {
            foreach (var item in plan.Items)
            {
                var targetId = item.SelectedCustomPlatformId;
                if (targetId == null)
                {
                    var displayName = item.CurrentSystemDisplayName;
                    var newCp = _customPlatformRepo.CreateAsync(displayName).GetAwaiter().GetResult();
                    targetId = newCp.Id;
                    _customMap.SetMapping(item.SystemRawValue, targetId.Value);
                }
                else if (!_customMap.GetCustomPlatformId(item.SystemRawValue).HasValue)
                {
                    _customMap.SetMapping(item.SystemRawValue, targetId.Value);
                }

                using (var cmd = _connection.CreateCommand())
                {
                    var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                    cmd.CommandText = "UPDATE items SET platform='custom', custom_platform_id=$cid, modify_date=$now WHERE lower(platform)=$raw AND custom_platform_id IS NULL";
                    cmd.Parameters.AddWithValue("$cid", targetId.Value.ToString("D"));
                    cmd.Parameters.AddWithValue("$now", now);
                    cmd.Parameters.AddWithValue("$raw", item.SystemRawValue);
                    cmd.ExecuteNonQuery();
                }

                result.MigratedItems[item.SystemRawValue] = item.SourceItemCount;
                result.Mapping[item.SystemRawValue] = targetId.Value;
            }

            transaction.Commit();
            result.IsComplete = true;
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
        finally
        {
            transaction.Dispose();
        }

        return result;
    }

    public Dictionary<string, int> ScanSystemOrphans()
    {
        var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"SELECT lower(platform) AS pk, COUNT(*) AS cnt 
            FROM items WHERE deleted_at IS NULL AND custom_platform_id IS NULL 
            AND lower(platform) IN ('douyin','xiaohongshu','coolapk','bilibili','github','youtube','x','weibo','zhihu','douban')
            GROUP BY lower(platform)";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var pk = reader.GetString(0);
            var cnt = reader.GetInt32(1);
            if (cnt > 0) result[pk] = cnt;
        }
        return result;
    }
}

public class MigrationPlan
{
    public List<MigrationPlanItem> Items { get; } = new();
    public int TotalSourceItemCount => Items.Sum(i => i.SourceItemCount);
}

public class MigrationPlanItem
{
    public string SystemRawValue { get; set; } = "";
    public int SourceItemCount { get; set; }
    public string DefaultDisplayName { get; set; } = "";
    public string CurrentSystemDisplayName { get; set; } = "";
    public List<MigrationCandidate> CandidateCustomPlatforms { get; set; } = new();
    public Guid? SelectedCustomPlatformId { get; set; }
    public bool WillCreateCustomPlatform { get; set; }
    public string? AmbiguityWarning { get; set; }
}

public class MigrationCandidate
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public int SortOrder { get; set; }
    public bool IsExactCaseMatch { get; set; }
    public bool IsIgnoreCaseMatch { get; set; }
}

public class MigrationResult
{
    public bool IsComplete { get; set; }
    public bool WasBlocked { get; set; }
    public string? BlockReason { get; set; }
    public Dictionary<string, int> MigratedItems { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, Guid> Mapping { get; } = new(StringComparer.OrdinalIgnoreCase);
    public List<string> Errors { get; } = new();

    public static MigrationResult NoWork() => new() { IsComplete = true };
    public static MigrationResult BlockedByBackupFailure(string reason) =>
        new() { WasBlocked = true, BlockReason = reason };
}
