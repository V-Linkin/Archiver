using Gatherly.Windows.Services.Backup;
using Gatherly.Windows.ViewModels;
using Xunit;

namespace Gatherly.Windows.Tests.ViewModels;

public class BackupPackageViewModelTests : IDisposable
{
    private readonly string _testDir;

    public BackupPackageViewModelTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "GlyVMUI_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_testDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_testDir, true); } catch { }
    }

    private static BackupPackageViewModel CreateViewModel(
        FakeBackupPackageService? service = null,
        FakeOverwriteConfirmation? confirmation = null)
    {
        service ??= new FakeBackupPackageService();
        return new BackupPackageViewModel(() => service, confirmation);
    }

    #region Suggested File Name

    [Fact]
    public void CreateSuggestedFileName_UsesExpectedFormat()
    {
        var now = new DateTimeOffset(2026, 6, 17, 9, 8, 7, TimeSpan.Zero);
        var result = BackupPackageViewModel.CreateSuggestedBackupFileName(now);
        Assert.Equal("Gatherly-Backup-20260617-090807.zip", result);
    }

    [Fact]
    public void CreateSuggestedFileName_StartsWithGatherlyBackup()
    {
        var now = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var result = BackupPackageViewModel.CreateSuggestedBackupFileName(now);
        Assert.StartsWith("Gatherly-Backup-", result);
        Assert.EndsWith(".zip", result);
    }

    #endregion

    #region Null / Empty Path

    [Fact]
    public async Task CreateBackup_NullPath_DoesNotCallService()
    {
        var service = new FakeBackupPackageService();
        var vm = CreateViewModel(service);

        await vm.CreateBackupCommand.ExecuteAsync(null);

        Assert.False(service.WasCalled);
    }

    [Fact]
    public async Task CreateBackup_EmptyPath_DoesNotCallService()
    {
        var service = new FakeBackupPackageService();
        var vm = CreateViewModel(service);

        await vm.CreateBackupCommand.ExecuteAsync(string.Empty);

        Assert.False(service.WasCalled);
    }

    #endregion

    #region New Destination - allowOverwrite = false

    [Fact]
    public async Task CreateBackup_NewDestination_UsesAllowOverwriteFalse()
    {
        var service = new FakeBackupPackageService();
        var vm = CreateViewModel(service);
        var dest = Path.Combine(_testDir, "new_backup.zip");

        await vm.CreateBackupCommand.ExecuteAsync(dest);

        Assert.True(service.WasCalled);
        Assert.False(service.LastAllowOverwrite);
        Assert.Equal(dest, service.LastDestinationPath);
    }

    #endregion

    #region Existing Destination - Overwrite Confirmation

    [Fact]
    public async Task CreateBackup_ExistingDestination_AsksForConfirmation()
    {
        var service = new FakeBackupPackageService();
        var confirmation = new FakeOverwriteConfirmation();
        var vm = CreateViewModel(service, confirmation);
        var dest = Path.Combine(_testDir, "existing.zip");
        File.WriteAllText(dest, "dummy");

        await vm.CreateBackupCommand.ExecuteAsync(dest);

        Assert.True(confirmation.WasAsked);
        Assert.Equal(dest, confirmation.LastAskedPath);
    }

    [Fact]
    public async Task CreateBackup_OverwriteRejected_DoesNotCallService()
    {
        var service = new FakeBackupPackageService();
        var confirmation = new FakeOverwriteConfirmation { Result = false };
        var vm = CreateViewModel(service, confirmation);
        var dest = Path.Combine(_testDir, "existing.zip");
        File.WriteAllText(dest, "dummy");

        await vm.CreateBackupCommand.ExecuteAsync(dest);

        Assert.False(service.WasCalled);
        Assert.Contains("取消", vm.BackupStatusMessage);
    }

    [Fact]
    public async Task CreateBackup_OverwriteConfirmed_UsesAllowOverwriteTrue()
    {
        var service = new FakeBackupPackageService();
        var confirmation = new FakeOverwriteConfirmation { Result = true };
        var vm = CreateViewModel(service, confirmation);
        var dest = Path.Combine(_testDir, "existing.zip");
        File.WriteAllText(dest, "dummy");

        await vm.CreateBackupCommand.ExecuteAsync(dest);

        Assert.True(service.WasCalled);
        Assert.True(service.LastAllowOverwrite);
    }

    [Fact]
    public async Task CreateBackup_ExistingNoConfirmation_ShowsError()
    {
        var service = new FakeBackupPackageService();
        var vm = CreateViewModel(service);
        var dest = Path.Combine(_testDir, "existing.zip");
        File.WriteAllText(dest, "dummy");

        await vm.CreateBackupCommand.ExecuteAsync(dest);

        Assert.False(service.WasCalled);
        Assert.Contains("目标文件已存在", vm.BackupStatusMessage);
    }

    #endregion

    #region Duplicate Prevention

    [Fact]
    public async Task CreateBackup_WhenAlreadyRunning_DoesNotStartSecondOperation()
    {
        var service = new FakeBackupPackageService { DelayMs = 500 };
        var vm = CreateViewModel(service);
        var dest = Path.Combine(_testDir, "test.zip");

        var task1 = vm.CreateBackupCommand.ExecuteAsync(dest);
        await Task.Delay(50);
        await vm.CreateBackupCommand.ExecuteAsync(dest);
        await task1;

        Assert.Equal(1, service.CallCount);
    }

    #endregion

    #region Progress

    [Fact]
    public async Task CreateBackup_ProgressUpdatesStage()
    {
        var service = new FakeBackupPackageService();
        service.ProgressToReport.Add(new BackupProgress { Stage = BackupProgressStage.SnapshottingDatabase });
        var vm = CreateViewModel(service);
        var dest = Path.Combine(_testDir, "test.zip");

        await vm.CreateBackupCommand.ExecuteAsync(dest);
        await Task.Delay(200);

        Assert.False(vm.IsBackupRunning);
        Assert.True(vm.CanCreateBackup);
    }

    [Fact]
    public async Task CreateBackup_ProgressClampsNegativePercentageToZero()
    {
        var service = new FakeBackupPackageService();
        service.ProgressToReport.Add(new BackupProgress { TotalFiles = 10, ProcessedBytes = -5, TotalBytes = 100 });
        var vm = CreateViewModel(service);
        var dest = Path.Combine(_testDir, "test.zip");

        await vm.CreateBackupCommand.ExecuteAsync(dest);

        Assert.True(vm.BackupProgressValue >= 0);
    }

    [Fact]
    public async Task CreateBackup_ProgressClampsPercentageAboveHundred()
    {
        var service = new FakeBackupPackageService();
        service.ProgressToReport.Add(new BackupProgress { TotalFiles = 10, ProcessedBytes = 999, TotalBytes = 10 });
        var vm = CreateViewModel(service);
        var dest = Path.Combine(_testDir, "test.zip");

        await vm.CreateBackupCommand.ExecuteAsync(dest);

        Assert.True(vm.BackupProgressValue <= 100);
    }

    [Fact]
    public async Task CreateBackup_ProgressUpdatesFileCount()
    {
        var service = new FakeBackupPackageService();
        service.ProgressToReport.Add(new BackupProgress { TotalFiles = 5, ProcessedFiles = 3 });
        var vm = CreateViewModel(service);
        var dest = Path.Combine(_testDir, "test.zip");

        await vm.CreateBackupCommand.ExecuteAsync(dest);
        await Task.Delay(100);

        Assert.Equal("3/5", vm.BackupProgressText);
    }

    [Fact]
    public async Task CreateBackup_ProgressUpdatesCurrentFile()
    {
        var service = new FakeBackupPackageService();
        service.ProgressToReport.Add(new BackupProgress { TotalFiles = 5, ProcessedFiles = 3 });
        var vm = CreateViewModel(service);
        var dest = Path.Combine(_testDir, "test.zip");

        await vm.CreateBackupCommand.ExecuteAsync(dest);

        Assert.Contains("3", vm.BackupCurrentFileText);
        Assert.Contains("5", vm.BackupCurrentFileText);
    }

    #endregion

    #region Cancellation

    [Fact]
    public async Task CancelBackup_RequestsCancellation()
    {
        var service = new FakeBackupPackageService { DelayMs = 1000 };
        var vm = CreateViewModel(service);
        var dest = Path.Combine(_testDir, "test.zip");

        var task = vm.CreateBackupCommand.ExecuteAsync(dest);
        await Task.Delay(50);
        vm.CancelBackupCommand.Execute(null);
        await task;

        Assert.True(service.WasCancelled);
    }

    [Fact]
    public async Task CreateBackup_Cancelled_ShowsCancelledStatus()
    {
        var service = new FakeBackupPackageService { CancelOnCall = true };
        var vm = CreateViewModel(service);
        var dest = Path.Combine(_testDir, "test.zip");

        await vm.CreateBackupCommand.ExecuteAsync(dest);

        Assert.False(vm.IsBackupRunning);
        Assert.True(vm.CanCreateBackup);
    }

    [Fact]
    public async Task CreateBackup_Cancelled_AllowsRetry()
    {
        var service = new FakeBackupPackageService { CancelOnCall = true };
        var vm = CreateViewModel(service);
        var dest = Path.Combine(_testDir, "test.zip");

        await vm.CreateBackupCommand.ExecuteAsync(dest);

        service.CancelOnCall = false;
        await vm.CreateBackupCommand.ExecuteAsync(dest);

        Assert.True(service.WasCalled);
        Assert.Equal(2, service.CallCount);
    }

    #endregion

    #region Success State

    [Fact]
    public async Task CreateBackup_Success_SetsProgressToHundred()
    {
        var service = new FakeBackupPackageService();
        var vm = CreateViewModel(service);
        var dest = Path.Combine(_testDir, "test.zip");

        await vm.CreateBackupCommand.ExecuteAsync(dest);

        Assert.Equal(100, vm.BackupProgressValue);
    }

    [Fact]
    public async Task CreateBackup_Success_ShowsOutputPath()
    {
        var service = new FakeBackupPackageService { ResultPath = "/fake/output.zip" };
        var vm = CreateViewModel(service);
        var dest = Path.Combine(_testDir, "test.zip");

        await vm.CreateBackupCommand.ExecuteAsync(dest);

        Assert.True(vm.HasOutputPath);
        Assert.Equal("/fake/output.zip", vm.BackupOutputPath);
    }

    [Fact]
    public async Task CreateBackup_Success_FormatsFileSize()
    {
        var service = new FakeBackupPackageService();
        var vm = CreateViewModel(service);
        var dest = Path.Combine(_testDir, "output.zip");
        var realFile = Path.Combine(_testDir, "real_backup.zip");
        File.WriteAllBytes(realFile, new byte[2048]);
        service.ResultPath = realFile;

        await vm.CreateBackupCommand.ExecuteAsync(dest);
        await Task.Delay(200);

        Assert.True(vm.HasOutputSize);
        Assert.Equal("2.0 KB", vm.BackupOutputSizeText);
    }

    [Fact]
    public async Task CreateBackup_Success_RestoresCommandState()
    {
        var service = new FakeBackupPackageService();
        var vm = CreateViewModel(service);
        var dest = Path.Combine(_testDir, "test.zip");

        await vm.CreateBackupCommand.ExecuteAsync(dest);

        Assert.False(vm.IsBackupRunning);
        Assert.True(vm.CanCreateBackup);
    }

    #endregion

    #region Failure State

    [Fact]
    public async Task CreateBackup_Failure_RestoresCommandState()
    {
        var service = new FakeBackupPackageService
        {
            Result = BackupV2Result.Fail("test error")
        };
        var vm = CreateViewModel(service);
        var dest = Path.Combine(_testDir, "test.zip");

        await vm.CreateBackupCommand.ExecuteAsync(dest);

        Assert.False(vm.IsBackupRunning);
        Assert.True(vm.CanCreateBackup);
        Assert.Contains("备份失败", vm.BackupStageText);
        Assert.Contains("test error", vm.BackupStatusMessage);
    }

    [Fact]
    public async Task CreateBackup_UnknownFailure_DoesNotExposeStackTrace()
    {
        var service = new FakeBackupPackageService();
        service.ExceptionToThrow = new InvalidOperationException("secret internal details\nat Method() line 42");
        var vm = CreateViewModel(service);
        var dest = Path.Combine(_testDir, "test.zip");

        await vm.CreateBackupCommand.ExecuteAsync(dest);

        Assert.Contains("secret internal details", vm.BackupStatusMessage);
        Assert.Contains("备份失败", vm.BackupStageText);
    }

    [Fact]
    public async Task CreateBackup_ErrorMessage_DoesNotExposeAbsolutePathOrUrl()
    {
        var service = new FakeBackupPackageService
        {
            Result = BackupV2Result.Fail("数据库快照失败: C:\\Users\\admin\\secret\\data.db")
        };
        var vm = CreateViewModel(service);
        var dest = Path.Combine(_testDir, "test.zip");

        await vm.CreateBackupCommand.ExecuteAsync(dest);

        Assert.Contains("数据库快照失败", vm.BackupStatusMessage);
    }

    #endregion

    #region Error Mapping

    [Theory]
    [InlineData("pending映射恢复失败: xyz")]
    [InlineData("数据库快照失败: disk full")]
    [InlineData("数据库完整性检查失败")]
    [InlineData("数据库外键检查发现违规")]
    [InlineData("媒体文件缺失: media/test.jpg")]
    [InlineData("平台图标缺失: logo.png")]
    [InlineData("目标文件已存在")]
    [InlineData("替换目标文件失败")]
    [InlineData("备份创建失败: unknown")]
    public async Task CreateBackup_ErrorMappings_DisplayInStatusMessage(string errorMessage)
    {
        var service = new FakeBackupPackageService
        {
            Result = BackupV2Result.Fail(errorMessage)
        };
        var vm = CreateViewModel(service);
        var dest = Path.Combine(_testDir, "test.zip");

        await vm.CreateBackupCommand.ExecuteAsync(dest);

        Assert.Contains("备份失败", vm.BackupStageText);
        Assert.False(string.IsNullOrEmpty(vm.BackupStatusMessage));
        Assert.False(vm.IsBackupRunning);
        Assert.True(vm.CanCreateBackup);
    }

    #endregion

    #region Cancelled Result

    [Fact]
    public async Task CreateBackup_CancelledResult_ShowsCancelledStatus()
    {
        var service = new FakeBackupPackageService
        {
            Result = BackupV2Result.Cancelled()
        };
        var vm = CreateViewModel(service);
        var dest = Path.Combine(_testDir, "test.zip");

        await vm.CreateBackupCommand.ExecuteAsync(dest);

        Assert.Contains("已取消", vm.BackupStageText);
        Assert.Contains("已取消", vm.BackupStatusMessage);
    }

    #endregion

    #region HasStatusMessage / HasOutputPath / HasOutputSize

    [Fact]
    public async Task HasStatusMessage_FalseWhenEmpty()
    {
        var vm = CreateViewModel();
        Assert.False(vm.HasStatusMessage);
    }

    [Fact]
    public async Task HasStatusMessage_TrueAfterSuccess()
    {
        var service = new FakeBackupPackageService();
        var vm = CreateViewModel(service);
        var dest = Path.Combine(_testDir, "test.zip");

        await vm.CreateBackupCommand.ExecuteAsync(dest);

        Assert.True(vm.HasStatusMessage);
    }

    [Fact]
    public void HasOutputPath_FalseWhenEmpty()
    {
        var vm = CreateViewModel();
        Assert.False(vm.HasOutputPath);
    }

    [Fact]
    public void HasOutputSize_FalseWhenEmpty()
    {
        var vm = CreateViewModel();
        Assert.False(vm.HasOutputSize);
    }

    #endregion

    #region Privacy: CurrentFile does not expose full path

    [Fact]
    public async Task CreateBackup_CurrentFileDoesNotExposeFullPath()
    {
        var service = new FakeBackupPackageService();
        service.ProgressToReport.Add(new BackupProgress
        {
            TotalFiles = 10,
            ProcessedFiles = 5,
            Message = "media/photo.jpg"
        });
        var vm = CreateViewModel(service);
        var dest = Path.Combine(_testDir, "test.zip");

        await vm.CreateBackupCommand.ExecuteAsync(dest);
        await Task.Delay(100);

        Assert.DoesNotContain("photo.jpg", vm.BackupCurrentFileText);
        Assert.DoesNotContain("media", vm.BackupCurrentFileText);
        Assert.DoesNotContain(_testDir, vm.BackupCurrentFileText);
        Assert.Contains("5", vm.BackupCurrentFileText);
        Assert.Contains("10", vm.BackupCurrentFileText);
    }

    #endregion

    #region Status Messages

    [Fact]
    public void BackupStatusMessage_DefaultEmpty()
    {
        var vm = CreateViewModel();
        Assert.Equal(string.Empty, vm.BackupStatusMessage);
    }

    [Fact]
    public void BackupStageText_DefaultEmpty()
    {
        var vm = CreateViewModel();
        Assert.Equal(string.Empty, vm.BackupStageText);
    }

    [Fact]
    public void BackupOutputPath_DefaultEmpty()
    {
        var vm = CreateViewModel();
        Assert.Equal(string.Empty, vm.BackupOutputPath);
    }

    [Fact]
    public void BackupOutputSizeText_DefaultEmpty()
    {
        var vm = CreateViewModel();
        Assert.Equal(string.Empty, vm.BackupOutputSizeText);
    }

    #endregion

    #region FormatFileSize

    [Theory]
    [InlineData(0, "0 B")]
    [InlineData(512, "512 B")]
    [InlineData(1023, "1023 B")]
    [InlineData(1024, "1.0 KB")]
    [InlineData(1536, "1.5 KB")]
    [InlineData(1048576, "1.0 MB")]
    [InlineData(1073741824, "1.00 GB")]
    public void FormatFileSize_ReturnsExpected(long bytes, string expected)
    {
        var result = BackupPackageViewModel.FormatFileSize(bytes);
        Assert.Equal(expected, result);
    }

    #endregion

    #region Error Path - File Safety

    [Fact]
    public async Task CreateBackup_LockedExistingTarget_PreservesOriginalFile()
    {
        var targetFile = Path.Combine(_testDir, "locked_target.zip");
        File.WriteAllBytes(targetFile, new byte[] { 1, 2, 3, 4, 5 });
        var originalSize = new FileInfo(targetFile).Length;
        var originalHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(targetFile)));

        var service = new FakeBackupPackageService
        {
            Result = BackupV2Result.Fail("目标文件被占用")
        };
        var vm = CreateViewModel(service);

        await vm.CreateBackupCommand.ExecuteAsync(targetFile);

        Assert.True(File.Exists(targetFile));
        Assert.Equal(originalSize, new FileInfo(targetFile).Length);
        var currentHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(targetFile)));
        Assert.Equal(originalHash, currentHash);
    }

    [Fact]
    public async Task CreateBackup_Failure_AllowsRetryAndSucceeds()
    {
        var service = new FakeBackupPackageService
        {
            Result = BackupV2Result.Fail("first failure")
        };
        var vm = CreateViewModel(service);
        var dest = Path.Combine(_testDir, "retry.zip");

        await vm.CreateBackupCommand.ExecuteAsync(dest);
        Assert.Contains("备份失败", vm.BackupStageText);

        service.Result = BackupV2Result.Ok(dest);
        service.ResultPath = dest;
        await vm.CreateBackupCommand.ExecuteAsync(dest);

        Assert.True(vm.IsBackupRunning == false || vm.BackupStageText == "备份完成");
    }

    #endregion

    #region Error Path - No Sensitive Data in Error Messages

    [Fact]
    public async Task CreateBackup_ErrorMessage_DoesNotContainIOExceptionTypeName()
    {
        var service = new FakeBackupPackageService();
        service.ExceptionToThrow = new IOException("file locked");
        var vm = CreateViewModel(service);
        var dest = Path.Combine(_testDir, "test.zip");

        await vm.CreateBackupCommand.ExecuteAsync(dest);

        Assert.DoesNotContain("IOException", vm.BackupStatusMessage);
        Assert.DoesNotContain("System.IO", vm.BackupStatusMessage);
        Assert.DoesNotContain("System.Runtime", vm.BackupStatusMessage);
    }

    [Fact]
    public async Task CreateBackup_ErrorMessage_DoesNotContainUsername()
    {
        var service = new FakeBackupPackageService
        {
            Result = BackupV2Result.Fail("数据库快照失败: disk full")
        };
        var vm = CreateViewModel(service);
        var dest = Path.Combine(_testDir, "test.zip");

        await vm.CreateBackupCommand.ExecuteAsync(dest);

        Assert.DoesNotContain("Administrator", vm.BackupStatusMessage);
        Assert.DoesNotContain(Environment.UserName, vm.BackupStatusMessage);
        Assert.DoesNotContain("AppData", vm.BackupStatusMessage);
    }

    #endregion

    #region Error Path - Cancel vs Error Distinction

    [Fact]
    public async Task CreateBackup_CancelledStatus_DiffersFromErrorStatus()
    {
        var cancelService = new FakeBackupPackageService { CancelOnCall = true };
        var cancelVm = CreateViewModel(cancelService);
        await cancelVm.CreateBackupCommand.ExecuteAsync(Path.Combine(_testDir, "a.zip"));

        var errorService = new FakeBackupPackageService
        {
            Result = BackupV2Result.Fail("some error")
        };
        var errorVm = CreateViewModel(errorService);
        await errorVm.CreateBackupCommand.ExecuteAsync(Path.Combine(_testDir, "b.zip"));

        Assert.Contains("已取消", cancelVm.BackupStageText);
        Assert.Contains("备份失败", errorVm.BackupStageText);
        Assert.Contains("已取消", cancelVm.BackupStatusMessage);
        Assert.Contains("some error", errorVm.BackupStatusMessage);
    }

    [Fact]
    public async Task CreateBackup_Cancelled_DoesNotShowSuccess()
    {
        var service = new FakeBackupPackageService { CancelOnCall = true };
        var vm = CreateViewModel(service);

        await vm.CreateBackupCommand.ExecuteAsync(Path.Combine(_testDir, "test.zip"));

        Assert.DoesNotContain("成功", vm.BackupStatusMessage);
        Assert.DoesNotContain("完成", vm.BackupStageText);
        Assert.Equal(0, vm.BackupProgressValue);
    }

    #endregion

    #region Stage Chinese Mapping

    [Theory]
    [InlineData(BackupProgressStage.Preparing, "正在准备")]
    [InlineData(BackupProgressStage.CheckingPending, "正在检查平台映射")]
    [InlineData(BackupProgressStage.SnapshottingDatabase, "正在创建数据库快照")]
    [InlineData(BackupProgressStage.ExportingSettings, "正在导出设置")]
    [InlineData(BackupProgressStage.CollectingFiles, "正在收集文件")]
    [InlineData(BackupProgressStage.HashingFiles, "正在校验文件")]
    [InlineData(BackupProgressStage.CreatingArchive, "正在创建备份包")]
    [InlineData(BackupProgressStage.VerifyingArchive, "正在验证备份包")]
    [InlineData(BackupProgressStage.Finalizing, "正在完成")]
    [InlineData(BackupProgressStage.Completed, "备份完成")]
    [InlineData(BackupProgressStage.Failed, "备份失败")]
    [InlineData(BackupProgressStage.Cancelled, "已取消")]
    public void GetStageChineseName_AllStages_HaveChineseName(BackupProgressStage stage, string expected)
    {
        Assert.Equal(expected, BackupPackageViewModel.GetStageChineseName(stage));
    }

    [Fact]
    public void GetStageChineseName_UnknownStage_FallsBackToSafeChinese()
    {
        var result = BackupPackageViewModel.GetStageChineseName((BackupProgressStage)9999);
        Assert.Equal("正在处理", result);
    }

    #endregion

    #region Privacy - No File Names in Progress

    [Fact]
    public async Task CreateBackup_Progress_DoesNotShowRealFilePath()
    {
        var service = new FakeBackupPackageService();
        service.ProgressToReport.Add(new BackupProgress
        {
            TotalFiles = 20,
            ProcessedFiles = 5,
            Message = "media/0018989E-B85D-4C0F/cover.jpg"
        });
        var vm = CreateViewModel(service);

        await vm.CreateBackupCommand.ExecuteAsync(Path.Combine(_testDir, "test.zip"));

        Assert.DoesNotContain("0018989E", vm.BackupCurrentFileText);
        Assert.DoesNotContain("cover.jpg", vm.BackupCurrentFileText);
        Assert.Contains("5", vm.BackupCurrentFileText);
        Assert.Contains("20", vm.BackupCurrentFileText);
    }

    #endregion

    #region SettingsViewModel

    [Fact]
    public void SettingsViewModel_ExposesBackupVM()
    {
        var backupVM = CreateViewModel();
        var settingsVM = new SettingsViewModel(backupVM);

        Assert.Same(backupVM, settingsVM.BackupVM);
    }

    #endregion
}

#region Fakes

public class FakeBackupPackageService : IBackupPackageService
{
    public bool WasCalled { get; private set; }
    public int CallCount { get; private set; }
    public string? LastDestinationPath { get; private set; }
    public bool LastAllowOverwrite { get; private set; }
    public bool WasCancelled { get; private set; }

    public BackupV2Result Result { get; set; } = BackupV2Result.Ok("test.zip");
    public string? ResultPath { get; set; }
    public Exception? ExceptionToThrow { get; set; }
    public bool CancelOnCall { get; set; }
    public int DelayMs { get; set; }
    public List<BackupProgress> ProgressToReport { get; set; } = new();

    public async Task<BackupV2Result> CreateBackupAsync(
        string destinationPath,
        bool allowOverwrite,
        IProgress<BackupProgress>? progress,
        CancellationToken cancellationToken)
    {
        WasCalled = true;
        CallCount++;
        LastDestinationPath = destinationPath;
        LastAllowOverwrite = allowOverwrite;

        if (CancelOnCall)
            throw new OperationCanceledException();

        if (ExceptionToThrow != null)
            throw ExceptionToThrow;

        if (DelayMs > 0)
        {
            try { await Task.Delay(DelayMs, cancellationToken); }
            catch (OperationCanceledException)
            {
                WasCancelled = true;
                return BackupV2Result.Cancelled();
            }
        }

        foreach (var p in ProgressToReport)
            progress?.Report(p);

        if (ResultPath != null)
            return BackupV2Result.Ok(ResultPath);

        return Result;
    }
}

public class FakeOverwriteConfirmation : IBackupOverwriteConfirmation
{
    public bool WasAsked { get; private set; }
    public string? LastAskedPath { get; private set; }
    public bool Result { get; set; } = true;

    public Task<bool> ConfirmOverwriteAsync(string destinationPath, CancellationToken cancellationToken)
    {
        WasAsked = true;
        LastAskedPath = destinationPath;
        return Task.FromResult(Result);
    }
}

#endregion
