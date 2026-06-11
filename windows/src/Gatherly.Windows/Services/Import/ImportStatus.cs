namespace Gatherly.Windows.Services.Import;

/// <summary>
/// 导入结果状态
/// </summary>
public enum ImportStatus
{
    EmptyInput,
    InvalidUrl,
    UnsupportedPlatform,
    Duplicate,
    TaskCreated,
    ParserNotImplemented,
    Failed,
    SuccessImport
}
