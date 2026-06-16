using Gatherly.Windows.Models.Enums;

namespace Gatherly.Windows.ViewModels;

public enum PlatformManagementEntryKind
{
    System,
    Custom
}

public sealed class PlatformManagementEntry
{
    public PlatformManagementEntryKind Kind { get; init; }
    public Platform? SystemPlatform { get; init; }
    public Guid? CustomPlatformId { get; init; }
    public string DisplayName { get; init; } = string.Empty;
    public bool HasCustomPlatform => CustomPlatformId.HasValue;
}
