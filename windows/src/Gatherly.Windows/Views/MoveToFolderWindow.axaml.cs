using Avalonia.Controls;
using Avalonia.Interactivity;
using Gatherly.Windows.Database;
using Gatherly.Windows.Models;

namespace Gatherly.Windows.Views;

public partial class MoveToFolderWindow : Window
{
    public FolderChoiceResult? Result { get; private set; }

    public MoveToFolderWindow(IReadOnlyList<Folder> folders, string title = "移动到文件夹")
    {
        InitializeComponent();
        Title = title;

        var entries = folders.Select(f => new FolderChoiceEntry
        {
            FolderId = f.Id,
            Name = f.Name,
        }).ToList();

        var vm = new MoveToFolderViewModel { Folders = entries, HasFolders = entries.Count > 0 };
        DataContext = vm;

        FolderListBox.ItemsSource = vm.Folders;
    }

    private void FolderListBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        MoveButton.IsEnabled = FolderListBox.SelectedItem != null;
    }

    private void CancelButton_Click(object? sender, RoutedEventArgs e)
    {
        Result = null;
        Close();
    }

    private void MoveButton_Click(object? sender, RoutedEventArgs e)
    {
        if (FolderListBox.SelectedItem is FolderChoiceEntry entry)
        {
            Result = new FolderChoiceResult { FolderId = entry.FolderId };
        }
        Close();
    }
}

public class MoveToFolderViewModel
{
    public List<FolderChoiceEntry> Folders { get; set; } = new();
    public bool HasFolders { get; set; }
}

public class FolderChoiceEntry
{
    public Guid FolderId { get; set; }
    public string Name { get; set; } = "";
}

public class FolderChoiceResult
{
    public Guid FolderId { get; set; }
}
