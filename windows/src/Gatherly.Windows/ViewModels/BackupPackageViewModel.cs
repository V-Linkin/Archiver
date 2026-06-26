using System.IO;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Gatherly.Windows.Models.Enums;
using Gatherly.Windows.Services.Backup;

namespace Gatherly.Windows.ViewModels;

public interface IBackupOverwriteConfirmation
{
    Task<bool> ConfirmOverwriteAsync(string destinationPath, CancellationToken cancellationToken);
}

public partial class BackupPackageViewModel : ObservableObject
{
    private readonly Func<IBackupPackageService> _serviceFactory;
    private readonly IBackupOverwriteConfirmation? _overwriteConfirmation;
    private CancellationTokenSource? _cts;

    private static readonly Dictionary<BackupProgressStage, string> StageChineseMap = new()
    {
        [BackupProgressStage.Preparing] = "正在准备",
        [BackupProgressStage.CheckingPending] = "正在检查平台映射",
        [BackupProgressStage.SnapshottingDatabase] = "正在创建数据库快照",
        [BackupProgressStage.ExportingSettings] = "正在导出设置",
        [BackupProgressStage.CollectingFiles] = "正在收集文件",
        [BackupProgressStage.HashingFiles] = "正在校验文件",
        [BackupProgressStage.CreatingArchive] = "正在创建备份包",
        [BackupProgressStage.VerifyingArchive] = "正在验证备份包",
        [BackupProgressStage.Finalizing] = "正在完成",
        [BackupProgressStage.Completed] = "备份完成",
        [BackupProgressStage.Failed] = "备份失败",
        [BackupProgressStage.Cancelled] = "已取消",
    };

    [ObservableProperty] private bool _isBackupRunning;
    [ObservableProperty] private bool _canCreateBackup = true;
    [ObservableProperty] private int _backupProgressValue;
    [ObservableProperty] private string _backupProgressText = string.Empty;
    [ObservableProperty] private string _backupStageText = string.Empty;
    [ObservableProperty] private string _backupCurrentFileText = string.Empty;
    [ObservableProperty] private string _backupStatusMessage = string.Empty;
    [ObservableProperty] private string _backupOutputPath = string.Empty;
    [ObservableProperty] private string _backupOutputSizeText = string.Empty;

    public bool HasStatusMessage => !string.IsNullOrEmpty(BackupStatusMessage);
    public bool HasOutputPath => !string.IsNullOrEmpty(BackupOutputPath);
    public bool HasOutputSize => !string.IsNullOrEmpty(BackupOutputSizeText);

    public BackupPackageViewModel(
        Func<IBackupPackageService> serviceFactory,
        IBackupOverwriteConfirmation? overwriteConfirmation = null)
    {
        _serviceFactory = serviceFactory;
        _overwriteConfirmation = overwriteConfirmation;
    }

    public static string CreateSuggestedBackupFileName(DateTimeOffset now)
        => $"Gatherly-Backup-{now:yyyyMMdd-HHmmss}.zip";

    public static string GetStageChineseName(BackupProgressStage stage)
        => StageChineseMap.TryGetValue(stage, out var name) ? name : "正在处理";

    [RelayCommand(CanExecute = nameof(CanCreateBackup))]
    public async Task CreateBackupAsync(string? destinationPath)
    {
        if (string.IsNullOrEmpty(destinationPath)) return;
        if (IsBackupRunning) return;

        IsBackupRunning = true;
        CanCreateBackup = false;
        BackupProgressValue = 0;
        BackupStageText = "正在准备...";
        BackupStatusMessage = string.Empty;
        BackupOutputPath = string.Empty;
        BackupOutputSizeText = string.Empty;

        _cts = new CancellationTokenSource();
        var progress = new Progress<BackupProgress>(OnProgressChanged);

        try
        {
            var allowOverwrite = false;
            if (File.Exists(destinationPath))
            {
                if (_overwriteConfirmation != null)
                {
                    allowOverwrite = await _overwriteConfirmation.ConfirmOverwriteAsync(
                        destinationPath, _cts.Token);
                    if (!allowOverwrite)
                    {
                        BackupStageText = "已取消";
                        BackupStatusMessage = "用户取消覆盖";
                        return;
                    }
                }
                else
                {
                    BackupStageText = "备份失败";
                    BackupStatusMessage = "目标文件已存在，且未允许覆盖。";
                    return;
                }
            }

            var service = _serviceFactory();
            var result = await service.CreateBackupAsync(
                destinationPath, allowOverwrite, progress, _cts.Token);

            if (result.Success)
            {
                BackupProgressValue = 100;
                BackupStageText = "备份完成";
                BackupOutputPath = result.BackupPath ?? string.Empty;
                var size = File.Exists(result.BackupPath) ? new FileInfo(result.BackupPath).Length : 0;
                BackupOutputSizeText = FormatFileSize(size);
                BackupStatusMessage = "备份创建成功";
            }
            else if (result.ErrorMessage == null)
            {
                BackupStageText = "已取消";
                BackupStatusMessage = "备份已取消";
            }
            else
            {
                BackupStageText = "备份失败";
                BackupStatusMessage = result.ErrorMessage ?? "未知错误";
            }
        }
        catch (OperationCanceledException)
        {
            BackupStageText = "已取消";
            BackupStatusMessage = "备份已取消";
        }
        catch (Exception ex)
        {
            BackupStageText = "备份失败";
            BackupStatusMessage = $"备份异常: {ex.Message}";
        }
        finally
        {
            _cts?.Dispose();
            _cts = null;
            IsBackupRunning = false;
            CanCreateBackup = true;
        }
    }

    [RelayCommand]
    public void CancelBackup()
    {
        _cts?.Cancel();
    }

    private void OnProgressChanged(BackupProgress progress)
    {
        BackupStageText = GetStageChineseName(progress.Stage);
        BackupProgressValue = Math.Clamp(progress.Percentage, 0, 100);
        if (progress.TotalFiles > 0)
            BackupProgressText = $"{progress.ProcessedFiles}/{progress.TotalFiles}";
        if (progress.TotalFiles > 0)
            BackupCurrentFileText = $"正在处理第 {progress.ProcessedFiles} 个文件，共 {progress.TotalFiles} 个";
    }

    public static string FormatFileSize(long bytes)
    {
        if (bytes < 0) bytes = 0;
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        if (bytes < 1024L * 1024 * 1024) return $"{bytes / (1024.0 * 1024):F1} MB";
        return $"{bytes / (1024.0 * 1024 * 1024):F2} GB";
    }
}
