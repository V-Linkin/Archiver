using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Gatherly.Windows.Database;
using Gatherly.Windows.ViewModels;

namespace Gatherly.Windows;

public class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var connection = DatabaseInitializer.Initialize();
            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel(connection)
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
