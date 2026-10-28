using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Oopsling.Avalonia;

namespace Oopsling.Sample;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var window = new MainWindow();
            desktop.MainWindow = window;
            CrashReportWindow.ShowUnshown(window);
            if (Program.Option(desktop.Args ?? [], "--crash") is { } thread)
            {
                window.PressOnceOpened(backgroundThread: thread == "background");
            }
        }

        base.OnFrameworkInitializationCompleted();
    }
}
