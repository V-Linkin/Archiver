namespace Gatherly.Windows.Services.Web;

public class RenderedPageResult
{
    public string? FinalUrl { get; set; }
    public string? Title { get; set; }
    public string? Html { get; set; }
    public string? InnerText { get; set; }
    public Dictionary<string, string> ScriptResults { get; set; } = new();
    public bool LoadSucceeded { get; set; }
    public string? ErrorMessage { get; set; }
    public bool TimedOut { get; set; }
    public long ElapsedMs { get; set; }
}
