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

    public void ClearSelection()
    {
        if (Content is Panel panel)
        {
            foreach (var child in panel.Children)
            {
                if (child is ListBox lb)
                {
                    lb.SelectedItem = null;
                    break;
                }
            }
        }
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
