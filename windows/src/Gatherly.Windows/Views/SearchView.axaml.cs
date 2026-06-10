using Avalonia.Controls;
using Avalonia.Input;
using Gatherly.Windows.ViewModels;

namespace Gatherly.Windows.Views;

public partial class SearchView : UserControl
{
    public SearchView()
    {
        InitializeComponent();
    }

    private async void SearchTextBox_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && DataContext is SearchViewModel vm)
        {
            await vm.SearchCommand.ExecuteAsync(null);
            e.Handled = true;
        }
    }
}
