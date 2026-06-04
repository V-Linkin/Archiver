using CommunityToolkit.Mvvm.ComponentModel;

namespace Gatherly.Windows.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    [ObservableProperty]
    private string _title = "Gatherly Windows";
}
