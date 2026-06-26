using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Gatherly.Windows.Database;
using Gatherly.Windows.Models;
using Gatherly.Windows.Models.Enums;
using Gatherly.Windows.ViewModels;

namespace Gatherly.Windows.Views;

public partial class MoveToPlatformWindow : Window
{
    private readonly Item _item;
    private readonly CustomPlatformRepository _customPlatformRepo;

    public MoveToPlatformResult? Result { get; private set; }

    public MoveToPlatformWindow(Item item, CustomPlatformRepository customPlatformRepo)
    {
        _item = item;
        _customPlatformRepo = customPlatformRepo;
        InitializeComponent();
        _ = LoadPlatformsAsync();
    }

    private async Task LoadPlatformsAsync()
    {
        var platforms = await _customPlatformRepo.GetAllAsync();
        var entries = platforms.Select(cp => new PlatformChoiceEntry
        {
            Id = cp.Id,
            Name = cp.Name,
            IsCurrent = _item.Platform == Platform.custom && _item.CustomPlatformId == cp.Id
        }).ToList();

        var vm = new MoveToPlatformViewModel
        {
            Platforms = entries,
            HasPlatforms = entries.Count > 0
        };
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MoveToPlatformViewModel.SelectedPlatform))
                vm.CanMove = vm.SelectedPlatform != null && !vm.SelectedPlatform.IsCurrent;
        };
        DataContext = vm;
    }

    private void CancelButton_Click(object? sender, RoutedEventArgs e)
    {
        Result = null;
        Close();
    }

    private void MoveButton_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MoveToPlatformViewModel vm && vm.SelectedPlatform != null)
        {
            Result = new MoveToPlatformResult { CustomPlatformId = vm.SelectedPlatform.Id };
        }
        Close();
    }
}

public partial class MoveToPlatformViewModel : ViewModelBase
{
    public List<PlatformChoiceEntry> Platforms { get; set; } = new();
    public bool HasPlatforms { get; set; }

    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
    private PlatformChoiceEntry? _selectedPlatform;

    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
    private bool _canMove;
}

public class PlatformChoiceEntry
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public bool IsCurrent { get; set; }
}

public class MoveToPlatformResult
{
    public Guid CustomPlatformId { get; set; }
}
