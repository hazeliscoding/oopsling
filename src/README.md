Oopsling turns a crash in your .NET app into a GitHub issue the user files themselves, and sends nothing on its own.

When an unhandled exception ends your app, Oopsling writes one masked report to a file on the user's PC. At the next launch, a window shows the report, and Report on GitHub opens your repo's new-issue page with the report filled in, in the user's own browser. Nothing leaves the PC unless the user presses Submit there.

- `Oopsling`: the crash handler and the report, with no UI. Works in any .NET app.
- `Oopsling.Avalonia`: the window that shows the last report once.

## Use it in an Avalonia app

Add `Oopsling.Avalonia`. Start Oopsling first thing in `Main`, with your app's name and its GitHub repo:

```csharp
public static void Main(string[] args)
{
    CrashReports.Start("My App", "owner/my-app");
    BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
}
```

Then, when you create your main window, show the last crash's report if it hasn't been shown:

```csharp
desktop.MainWindow = window;
CrashReportWindow.ShowUnshown(window);
```

An app without a main window, such as a tray app, calls `CrashReportWindow.ShowUnshown()` with no owner instead.

This is a preview, so the API may change before 1.0. The repo has the details, the decisions behind them, and the roadmap: https://github.com/hazeliscoding/oopsling
