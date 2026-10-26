using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Oopsling.Avalonia;

namespace Oopsling.Tests;

public sealed class CrashReportWindowTests
{
    private static readonly HeadlessUnitTestSession Session = HeadlessUnitTestSession.StartNew(typeof(CrashReportWindowTests));

    private static readonly string ShortReport = "Sample 1.0 crashed: InvalidOperationException\n\n```text\nboom\n```\n";

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<TestApp>().UseSkia().UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });

    [Fact]
    public Task Says_what_happened_before_it_shows_the_report() => Session.Dispatch(() =>
    {
        var window = Open(ShortReport);

        Assert.Equal("Sample closed because of an error.", Texts(window)[0]);
        Assert.Equal(ShortReport, Single<TextBox>(window).Text);
        Assert.Equal(["Report on GitHub", "Copy", "Dismiss"], window.GetVisualDescendants().OfType<Button>().Select(b => b.Content));
        Assert.NotNull(window.CaptureRenderedFrame());
    }, CancellationToken.None);

    [Fact]
    public Task Says_when_the_report_is_too_long_for_a_link() => Session.Dispatch(() =>
    {
        var frames = string.Concat(Enumerable.Range(0, 400).Select(i => $"   at Sample.Deep.Frame{i}()\n"));

        var window = Open($"Sample 1.0 crashed: InvalidOperationException\n\n```text\nboom\n{frames}```\n");

        Assert.Contains(Texts(window), t => t.Contains("too long for a link", StringComparison.Ordinal));
    }, CancellationToken.None);

    [Fact]
    public Task Copy_puts_the_whole_report_on_the_clipboard() => Session.Dispatch(async () =>
    {
        var window = Open(ShortReport);

        window.GetVisualDescendants().OfType<Button>().Single(b => Equals(b.Content, "Copy")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(ShortReport, await window.Clipboard!.TryGetTextAsync());
        Assert.Contains("The report is on the clipboard.", Texts(window));

        // Dispatch awaits an async lambda only when it returns a value; one returning a bare Task is never awaited.
        return true;
    }, CancellationToken.None);

    private static CrashReportWindow Open(string text)
    {
        var window = new CrashReportWindow(new CrashReport("Sample", "hazeliscoding/staypaw", text));
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    private static List<string> Texts(Window window) =>
        [.. window.GetVisualDescendants().OfType<TextBlock>().Where(t => t.IsEffectivelyVisible).Select(t => t.Text ?? "")];

    private static T Single<T>(Window window) => window.GetVisualDescendants().OfType<T>().Single();

    private sealed class TestApp : Application
    {
        public override void Initialize() => Styles.Add(new FluentTheme());
    }
}
