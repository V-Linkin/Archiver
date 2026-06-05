using CommunityToolkit.Mvvm.ComponentModel;

namespace Gatherly.Windows.ViewModels;

/// <summary>
/// ViewModel 基类 — 提供通用状态管理
/// </summary>
public partial class ViewModelBase : ObservableObject
{
    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string? _errorMessage;

    protected void ClearError()
    {
        ErrorMessage = null;
    }

    protected void SetError(string message)
    {
        ErrorMessage = message;
    }
}
