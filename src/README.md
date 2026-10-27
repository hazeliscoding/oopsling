Oopsling turns a crash in your .NET app into a GitHub issue the user files themselves, and sends nothing on its own.

When an unhandled exception ends your app, Oopsling writes one masked report to a file on the user's PC. At the next launch, a window shows the report, and Report on GitHub opens your repo's new-issue page with the report filled in, in the user's own browser. Nothing leaves the PC unless the user presses Submit there.

- `Oopsling`: the crash handler and the report, with no UI. Works in any .NET app.
- `Oopsling.Avalonia`: the window that shows the last report once.

## Use it in an Avalonia app

Add `Oopsling.Avalonia`. In `Program.cs`, start Oopsling first thing in `Main`, with your app's name and its GitHub repo:

```csharp
using Oopsling;

[STAThread]
public static void Main(string[] args)
{
    CrashReports.Start("My App", "owner/my-app");
    BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
}
```

In `App.axaml.cs`, show the last crash's report over your main window:

```csharp
using Oopsling.Avalonia;

// In OnFrameworkInitializationCompleted:
desktop.MainWindow = new MainWindow();
CrashReportWindow.ShowUnshown(desktop.MainWindow);
```

A tray app with no main window calls `CrashReportWindow.ShowUnshown()` with no owner instead.

The repo has the details, the decisions behind them, and the roadmap: https://github.com/hazeliscoding/oopsling
