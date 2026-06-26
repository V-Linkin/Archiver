using Gatherly.Windows.Services.Backup;

namespace Gatherly.Windows.ViewModels;

public interface IBackupPackageService
{
    Task<BackupV2Result> CreateBackupAsync(string destinationPath, bool allowOverwrite, IProgress<BackupProgress>? progress, CancellationToken cancellationToken);
}

public class BackupPackageServiceAdapter : IBackupPackageService
{
    private readonly BackupPackageV2Service _service;

    public BackupPackageServiceAdapter(BackupPackageV2Service service)
    {
        _service = service;
    }

    public Task<BackupV2Result> CreateBackupAsync(string destinationPath, bool allowOverwrite, IProgress<BackupProgress>? progress, CancellationToken cancellationToken)
    {
        return _service.CreateBackupAsync(destinationPath, allowOverwrite, progress, cancellationToken);
    }
}
