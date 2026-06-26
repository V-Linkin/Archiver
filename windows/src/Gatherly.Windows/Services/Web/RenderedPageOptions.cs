namespace Gatherly.Windows.Services.Web;

public class RenderedPageOptions
{
    public int TimeoutSeconds { get; set; } = 15;
    public int ExtraWaitMs { get; set; } = 3000;
    public bool GetHtml { get; set; } = true;
    public bool GetInnerText { get; set; } = true;
    public bool GetTitle { get; set; } = true;
    public string? JavaScriptExpression { get; set; }
}
