using Avalonia;

namespace Gatherly.Windows;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            WriteCrashLog(e.ExceptionObject as Exception ?? new Exception("Unknown"), "AppDomain");
        };
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            WriteCrashLog(e.Exception, "TaskScheduler");
            e.SetObserved();
        };

        try
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            WriteCrashLog(ex, "Main");
            throw;
        }
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();

    private static void WriteCrashLog(Exception ex, string source)
    {
        try
        {
            var logDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Gatherly", "logs");
            Directory.CreateDirectory(logDir);
            var logFile = Path.Combine(logDir, $"startup-crash-{DateTime.Now:yyyyMMdd-HHmmss}.log");
            File.WriteAllText(logFile, $"Source: {source}\nTime: {DateTime.Now}\nException: {ex.GetType().Name}\nMessage: {ex.Message}\n\nStackTrace:\n{ex.StackTrace}");
        }
        catch { }
    }
}
