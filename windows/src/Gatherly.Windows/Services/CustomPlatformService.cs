using Gatherly.Windows.Database;
using Gatherly.Windows.Models;

namespace Gatherly.Windows.Services;

/// <summary>
/// 自定义平台服务 — 校验、创建、更新、删除
/// </summary>
public class CustomPlatformService
{
    private readonly CustomPlatformRepository _repo;

    private static readonly HashSet<string> ReservedNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "未分类",
        "Uncategorized"
    };

    private const int MaxNameLength = 50;

    public CustomPlatformService(CustomPlatformRepository repo)
    {
        _repo = repo;
    }

    /// <summary>
    /// 获取全部用户自定义平台
    /// </summary>
    public async Task<List<CustomPlatform>> GetAllPlatformsAsync()
    {
        return await _repo.GetAllAsync();
    }

    /// <summary>
    /// 校验平台名称
    /// </summary>
    public async Task<ValidateNameResult> ValidateNameAsync(string name, Guid? excludeId = null)
    {
        var trimmed = name.Trim();

        if (string.IsNullOrEmpty(trimmed))
            return new ValidateNameResult { IsValid = false, ErrorMessage = "平台名称不能为空" };

        if (trimmed.Length > MaxNameLength)
            return new ValidateNameResult { IsValid = false, ErrorMessage = $"平台名称不能超过 {MaxNameLength} 个字符" };

        if (ReservedNames.Contains(trimmed))
            return new ValidateNameResult { IsValid = false, ErrorMessage = $"「{trimmed}」是保留名称，不能使用" };

        var existing = await _repo.GetByNameAsync(trimmed);
        if (existing != null && existing.Id != excludeId)
            return new ValidateNameResult { IsValid = false, ErrorMessage = $"已存在同名平台「{trimmed}」" };

        return new ValidateNameResult { IsValid = true, NormalizedName = trimmed };
    }

    /// <summary>
    /// 校验 Logo 路径
    /// </summary>
    public static ValidateLogoResult ValidateLogoPath(string? logoPath)
    {
        if (string.IsNullOrWhiteSpace(logoPath))
            return new ValidateLogoResult { IsValid = true, NormalizedPath = null };

        var trimmed = logoPath.Trim();

        // 检查绝对路径
        if (Path.IsPathRooted(trimmed))
            return new ValidateLogoResult { IsValid = false, ErrorMessage = "Logo 路径不能是绝对路径" };

        // 检查路径穿越
        if (trimmed.Contains(".."))
            return new ValidateLogoResult { IsValid = false, ErrorMessage = "Logo 路径不能包含 .." };

        // 检查 URI scheme
        if (trimmed.StartsWith("file://", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            return new ValidateLogoResult { IsValid = false, ErrorMessage = "Logo 路径不能是 URI" };

        // 统一目录分隔符
        var normalized = trimmed.Replace('\\', '/');

        return new ValidateLogoResult { IsValid = true, NormalizedPath = normalized };
    }

    /// <summary>
    /// 创建平台
    /// </summary>
    public async Task<CustomPlatform> CreatePlatformAsync(string name, string? logoPath = null)
    {
        var validation = await ValidateNameAsync(name);
        if (!validation.IsValid)
            throw new ArgumentException(validation.ErrorMessage);

        var logoValidation = ValidateLogoPath(logoPath);
        if (!logoValidation.IsValid)
            throw new ArgumentException(logoValidation.ErrorMessage);

        return await _repo.CreateAsync(validation.NormalizedName!, logoValidation.NormalizedPath);
    }

    /// <summary>
    /// 更新平台
    /// </summary>
    public async Task<CustomPlatform?> UpdatePlatformAsync(Guid id, string? name = null, string? logoPath = null, int? sortOrder = null)
    {
        if (name != null)
        {
            var validation = await ValidateNameAsync(name, excludeId: id);
            if (!validation.IsValid)
                throw new ArgumentException(validation.ErrorMessage);
        }

        if (logoPath != null)
        {
            var logoValidation = ValidateLogoPath(logoPath);
            if (!logoValidation.IsValid)
                throw new ArgumentException(logoValidation.ErrorMessage);
        }

        return await _repo.UpdateAsync(id, name, logoPath, sortOrder);
    }

    /// <summary>
    /// 删除平台
    /// </summary>
    public async Task<DeletePlatformResult> DeletePlatformAsync(Guid id)
    {
        return await _repo.DeleteAsync(id);
    }
}

/// <summary>
/// 名称校验结果
/// </summary>
public class ValidateNameResult
{
    public bool IsValid { get; set; }
    public string? NormalizedName { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Logo 路径校验结果
/// </summary>
public class ValidateLogoResult
{
    public bool IsValid { get; set; }
    public string? NormalizedPath { get; set; }
    public string? ErrorMessage { get; set; }
}
