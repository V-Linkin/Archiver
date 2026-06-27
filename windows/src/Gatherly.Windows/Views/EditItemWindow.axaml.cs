using Avalonia.Controls;
using Avalonia.Interactivity;
using Gatherly.Windows.Models;

namespace Gatherly.Windows.Views;

public partial class EditItemWindow : Window
{
    private readonly string _initialTitle;
    private readonly string _initialBody;
    private readonly string _initialAuthor;
    private readonly string _initialRemark;
    private bool _forceClose;

    public EditItemResult? Result { get; private set; }

    public EditItemWindow(Item item)
    {
        InitializeComponent();

        TitleBox.Text = item.Title ?? "";
        BodyBox.Text = item.Body ?? "";
        AuthorBox.Text = item.Author ?? "";
        RemarkBox.Text = item.Remark ?? "";

        _initialTitle = TitleBox.Text;
        _initialBody = BodyBox.Text;
        _initialAuthor = AuthorBox.Text;
        _initialRemark = RemarkBox.Text;
    }

    private bool HasChanges =>
        TitleBox.Text != _initialTitle ||
        BodyBox.Text != _initialBody ||
        AuthorBox.Text != _initialAuthor ||
        RemarkBox.Text != _initialRemark;

    private async void Window_Closing(object? sender, WindowClosingEventArgs e)
    {
        if (_forceClose || !HasChanges) return;
        e.Cancel = true;

        var dialog = new ConfirmDialogWindow(
            "放弃修改？",
            "当前内容有未保存的修改，放弃后将丢失这些更改。",
            "继续编辑", "放弃修改", isDangerConfirm: false);
        if (TopLevel.GetTopLevel(this) is Window owner)
            await dialog.ShowDialog(owner);

        if (dialog.Result == true)
        {
            _forceClose = true;
            Close();
        }
    }

    private async void CancelButton_Click(object? sender, RoutedEventArgs e)
    {
        if (HasChanges)
        {
            var dialog = new ConfirmDialogWindow(
                "放弃修改？",
                "当前内容有未保存的修改，放弃后将丢失这些更改。",
                "继续编辑", "放弃修改", isDangerConfirm: false);
            if (TopLevel.GetTopLevel(this) is Window owner)
                await dialog.ShowDialog(owner);

            if (dialog.Result == true)
            {
                _forceClose = true;
                Close();
            }
        }
        else
        {
            Close();
        }
    }

    private void SaveButton_Click(object? sender, RoutedEventArgs e)
    {
        Result = new EditItemResult
        {
            Success = true,
            Title = string.IsNullOrWhiteSpace(TitleBox.Text) ? null : TitleBox.Text,
            Body = string.IsNullOrWhiteSpace(BodyBox.Text) ? null : BodyBox.Text,
            Author = string.IsNullOrWhiteSpace(AuthorBox.Text) ? null : AuthorBox.Text,
            Remark = string.IsNullOrWhiteSpace(RemarkBox.Text) ? null : RemarkBox.Text,
        };
        _forceClose = true;
        Close();
    }
}

public class EditItemResult
{
    public bool Success { get; set; }
    public string? Title { get; set; }
    public string? Body { get; set; }
    public string? Author { get; set; }
    public string? Remark { get; set; }
}
