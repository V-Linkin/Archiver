using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Gatherly.Windows.Views;

public partial class HelpWindow : Window
{
    private readonly Dictionary<string, TextBlock> _contents = new();
    private readonly Dictionary<string, TextBlock> _arrows = new();
    private readonly HashSet<string> _expanded = new();

    public HelpWindow()
    {
        InitializeComponent();
        RegisterItems();
        LoadFaqContent();
    }

    private void RegisterItems()
    {
        Register("faq1", Faq1Content, Faq1Arrow);
        Register("faq1b", Faq1bContent, Faq1bArrow);
        Register("faq2", Faq2Content, Faq2Arrow);
        Register("faq2b", Faq2bContent, Faq2bArrow);
        Register("faq2c", Faq2cContent, Faq2cArrow);
        Register("faq3", Faq3Content, Faq3Arrow);
        Register("faq3b", Faq3bContent, Faq3bArrow);
        Register("faq3c", Faq3cContent, Faq3cArrow);
        Register("faq4", Faq4Content, Faq4Arrow);
        Register("faq4b", Faq4bContent, Faq4bArrow);
        Register("faq5", Faq5Content, Faq5Arrow);
        Register("faq5b", Faq5bContent, Faq5bArrow);
        Register("faq6", Faq6Content, Faq6Arrow);
        Register("faq6b", Faq6bContent, Faq6bArrow);
    }

    private void Register(string tag, TextBlock content, TextBlock arrow)
    {
        _contents[tag] = content;
        _arrows[tag] = arrow;
    }

    private void LoadFaqContent()
    {
        _contents["faq1"].Text = "1. 复制任意平台的分享链接（支持抖音、小红书、B站等）\n2. 打开拾屿，粘贴到首页的输入框\n3. 点击「一键归档」\n4. 等待解析完成，内容自动保存到本地";
        _contents["faq1b"].Text = "支持一次粘贴多个链接，用换行或空格分隔，系统会自动识别并批量解析。";

        _contents["faq2"].Text = "• 抖音 — 支持视频、图文笔记\n• B站 — 支持视频信息、封面、简介\n• 小红书 — 支持图文笔记、视频笔记";
        _contents["faq2b"].Text = "• 微博 — 支持微博正文、图片\n• X (Twitter) — 支持推文、图片、视频\n• YouTube — 支持视频信息、封面、简介";
        _contents["faq2c"].Text = "• 知乎 — 支持问答、文章\n• 豆瓣 — 支持影评、书评、小组帖子\n• 酷安 — 支持应用动态、数码帖子\n• GitHub — 支持仓库 README、Issue、PR";

        _contents["faq3"].Text = "点击内容卡片进入详情，可编辑标题、正文、作者、备注。支持 Markdown 格式。";
        _contents["faq3b"].Text = "在平台分类下创建文件夹，右键内容卡片可移动到指定文件夹。支持多选批量移动。";
        _contents["faq3c"].Text = "顶部搜索框支持全文搜索，可按平台、状态、类型筛选结果。";

        _contents["faq4"].Text = "内容详情页顶部显示图片/视频，点击可全屏预览。支持 Shift+滚轮横向滚动浏览多图。";
        _contents["faq4b"].Text = "• 右键图片/视频 → 选择「另存为」导出单个文件\n• 工具栏「导出」按钮可批量导出所有媒体\n• 导出文件名格式：平台_文件夹_作者_序号_日期";

        _contents["faq5"].Text = "设置 → 备份与还原 → 导出备份，将数据库、媒体文件打包为 zip 文件。";
        _contents["faq5b"].Text = "选择之前备份的 zip 文件，可完整还原所有内容。还原后需要重启应用。";

        _contents["faq6"].Text = "可能是链接格式不正确，或平台暂时限制访问。请确认链接来自平台的「分享」功能。";
        _contents["faq6b"].Text = "默认存储在数据目录，可在设置 → 存储管理中查看和修改。";
    }

    private void FaqToggle_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button button) return;
        var tag = button.Tag?.ToString();
        if (string.IsNullOrEmpty(tag)) return;

        var isExpanded = _expanded.Contains(tag);
        if (_contents.TryGetValue(tag, out var content))
            content.IsVisible = !isExpanded;
        if (_arrows.TryGetValue(tag, out var arrow))
            arrow.Text = isExpanded ? "\uE70D" : "\uE70E";

        if (isExpanded) _expanded.Remove(tag);
        else _expanded.Add(tag);
    }

    private void Close_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
