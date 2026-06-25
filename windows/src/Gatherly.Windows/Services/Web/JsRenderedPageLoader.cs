using System.Diagnostics;
using System.Text.Json;

namespace Gatherly.Windows.Services.Web;

public class JsRenderedPageLoader
{
    public static bool IsRuntimeAvailable()
    {
        try { return !string.IsNullOrEmpty(Microsoft.Web.WebView2.Core.CoreWebView2Environment.GetAvailableBrowserVersionString()); }
        catch { return false; }
    }

    public async Task<RenderedPageResult> LoadAsync(string url, RenderedPageOptions? options = null, CancellationToken ct = default)
    {
        options ??= new RenderedPageOptions();
        var sw = Stopwatch.StartNew();
        if (!IsRuntimeAvailable()) { sw.Stop(); return new RenderedPageResult { ErrorMessage = "WebView2 Runtime 未安装", ElapsedMs = sw.ElapsedMilliseconds }; }
        try
        {
            var result = await Task.Run(() => LoadOnStaThread(url, options), ct);
            sw.Stop(); result.ElapsedMs = sw.ElapsedMilliseconds; return result;
        }
        catch (OperationCanceledException) { sw.Stop(); return new RenderedPageResult { TimedOut = true, ElapsedMs = sw.ElapsedMilliseconds }; }
        catch (Exception ex) { sw.Stop(); return new RenderedPageResult { ErrorMessage = ex.Message, ElapsedMs = sw.ElapsedMilliseconds }; }
    }

    private static RenderedPageResult LoadOnStaThread(string url, RenderedPageOptions options)
    {
        var finalResult = new RenderedPageResult { ErrorMessage = "未执行" };

        var thread = new Thread(() =>
        {
            try
            {
                var app = new System.Windows.Application { ShutdownMode = System.Windows.ShutdownMode.OnExplicitShutdown };
                app.Startup += (s, e) =>
                {
                    _ = System.Windows.Threading.Dispatcher.CurrentDispatcher.InvokeAsync(async () =>
                    {
                        try { finalResult = await RunWebView2WorkAsync(url, options); }
                        catch (Exception ex) { finalResult = new RenderedPageResult { ErrorMessage = ex.Message }; }
                        app.Shutdown();
                    });
                };
                app.Run();
            }
            catch (Exception ex) { finalResult = new RenderedPageResult { ErrorMessage = ex.Message }; }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();

        var timeout = options.TimeoutSeconds + options.ExtraWaitMs / 1000 + 10;
        if (!thread.Join(timeout * 1000))
            return new RenderedPageResult { TimedOut = true, ErrorMessage = "线程超时" };

        return finalResult;
    }

    private static async System.Threading.Tasks.Task<RenderedPageResult> RunWebView2WorkAsync(string url, RenderedPageOptions options)
    {
        var window = new System.Windows.Window
        {
            Width = 1280, Height = 720,
            WindowStyle = System.Windows.WindowStyle.None,
            ShowInTaskbar = false, Visibility = System.Windows.Visibility.Hidden,
            ShowActivated = false, WindowState = System.Windows.WindowState.Minimized
        };

        var webView = new Microsoft.Web.WebView2.Wpf.WebView2();
        window.Content = webView;
        window.Show();

        var env = await Microsoft.Web.WebView2.Core.CoreWebView2Environment
            .CreateAsync(null, Path.Combine(Path.GetTempPath(), "GatherlyWebView2"), null);
        await webView.EnsureCoreWebView2Async(env);

        webView.CoreWebView2.Navigate(url);

        var navOk = false;
        webView.NavigationCompleted += (s, e) => navOk = e.IsSuccess;
        var deadline = DateTime.UtcNow.AddSeconds(options.TimeoutSeconds);
        while (!navOk && DateTime.UtcNow < deadline)
            await System.Threading.Tasks.Task.Delay(100);

        if (!navOk) return new RenderedPageResult { TimedOut = true, ErrorMessage = "页面导航超时" };

        await System.Threading.Tasks.Task.Delay(options.ExtraWaitMs);

        var result = new RenderedPageResult { FinalUrl = webView.CoreWebView2.Source, LoadSucceeded = true };
        if (options.GetTitle) result.Title = await webView.CoreWebView2.ExecuteScriptAsync("document.title");
        if (options.GetInnerText) result.InnerText = await webView.CoreWebView2.ExecuteScriptAsync("document.body ? document.body.innerText : ''");
        if (options.GetHtml) result.Html = await webView.CoreWebView2.ExecuteScriptAsync("document.documentElement.outerHTML");
        if (!string.IsNullOrEmpty(options.JavaScriptExpression))
        {
            var raw = await webView.CoreWebView2.ExecuteScriptAsync(options.JavaScriptExpression);
            // WebView2 returns JSON-encoded result. If JS returned a string, outer is quoted.
            // Decode outer JSON first, then parse if needed.
            string decoded = DecodeWebView2Result(raw);
            result.ScriptResults["custom"] = decoded;
        }

        webView.Dispose();
        window.Close();
        return result;
    }

    private static string DecodeWebView2Result(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return "";
        try
        {
            using var outer = JsonDocument.Parse(raw);
            // WebView2 wraps JS string returns in outer JSON quotes
            if (outer.RootElement.ValueKind == JsonValueKind.String)
                return outer.RootElement.GetString() ?? "";
            // JS returned object/array — return as-is for further parsing
            return raw;
        }
        catch { return raw; }
    }

    private static string UnescapeJson(string? json)
    {
        if (string.IsNullOrEmpty(json)) return "";
        if (json.StartsWith("\"") && json.EndsWith("\"")) json = json[1..^1];
        return json.Replace("\\\"", "\"").Replace("\\n", "\n").Replace("\\r", "\r").Replace("\\t", "\t").Replace("\\\\", "\\");
    }
}
