using Avalonia;
using Avalonia.Automation;
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

        Assert.Equal("Sample closed because of an error", window.Title);
        Assert.Equal(
            [
                "Sample closed because of an error.",
                "This is the report it made, and nothing has been sent. Report on GitHub opens it as a new issue in your "
                    + "browser, where you can read and edit it before you submit it.",
                "Report on GitHub",
                "Copy",
                "Dismiss",
            ],
            Texts(window));
        Assert.Equal(ShortReport, Single<TextBox>(window).Text);
        Assert.Equal("Crash report", AutomationProperties.GetName(Single<TextBox>(window)));
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

    [Fact]
    public Task Shows_the_hosts_strings() => Session.Dispatch(() =>
    {
        var frames = string.Concat(Enumerable.Range(0, 400).Select(i => $"   at Sample.Deep.Frame{i}()\n"));

        var window = Open($"Sample 1.0 crashed: InvalidOperationException\n\n```text\nboom\n{frames}```\n", German, "crashes@example.com");

        Assert.Equal("Sample wurde wegen eines Fehlers beendet", window.Title);
        Assert.Equal(
            [
                "Sample wurde wegen eines Fehlers beendet.",
                German.Intro,
                German.TooLongForLink,
                "Auf GitHub melden",
                "Kopieren",
                "Schließen",
                "Kein GitHub-Konto? Schick den Bericht an",
                "crashes@example.com",
            ],
            Texts(window));
        Assert.Equal("Absturzbericht", AutomationProperties.GetName(Single<TextBox>(window)));
    }, CancellationToken.None);

    [Fact]
    public Task Says_in_the_hosts_words_that_Copy_worked() => Session.Dispatch(async () =>
    {
        var window = Open(ShortReport, strings: German);

        window.GetVisualDescendants().OfType<Button>().Single(b => Equals(b.Content, "Kopieren")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(ShortReport, await window.Clipboard!.TryGetTextAsync());
        Assert.Contains("Der Bericht ist in der Zwischenablage.", Texts(window));
        return true;
    }, CancellationToken.None);

    [Fact]
    public Task Shows_the_contact_under_the_buttons() => Session.Dispatch(() =>
    {
        var window = Open(ShortReport, contact: "crashes@example.com");

        Assert.Equal(["No GitHub account? Send the report to", "crashes@example.com"], Texts(window)[^2..]);
        Assert.Equal("crashes@example.com", Single<HyperlinkButton>(window).Content);
        Assert.NotNull(window.CaptureRenderedFrame());
    }, CancellationToken.None);

    [Fact]
    public Task The_contact_copies_the_report_and_opens() => Session.Dispatch(async () =>
    {
        var opened = new List<CrashReport>();
        var window = Open(ShortReport, contact: "crashes@example.com", openContact: opened.Add);
        // The clipboard outlives each test, and others put this same report on it.
        await window.Clipboard!.ClearAsync();

        Single<HyperlinkButton>(window).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(ShortReport, await window.Clipboard!.TryGetTextAsync());
        Assert.Equal("crashes@example.com", Assert.Single(opened).Contact);
        Assert.Contains("The report is on the clipboard. Paste it into your message.", Texts(window));
        return true;
    }, CancellationToken.None);

    [Fact]
    public Task Says_the_report_is_on_the_clipboard_when_nothing_opens() => Session.Dispatch(async () =>
    {
        var window = Open(ShortReport, contact: "crashes@example.com", openContact: _ => throw new InvalidOperationException("No mail app."));
        await window.Clipboard!.ClearAsync();

        Single<HyperlinkButton>(window).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(ShortReport, await window.Clipboard!.TryGetTextAsync());
        Assert.Contains("Nothing opened, but the report is on the clipboard.", Texts(window));
        return true;
    }, CancellationToken.None);

    [Fact]
    public Task With_no_owner_it_shows_the_report_at_once() => Session.Dispatch(() =>
    {
        var opened = new List<Window>();
        using var _ = Window.WindowOpenedEvent.AddClassHandler<CrashReportWindow>((window, _) => opened.Add(window));

        CrashReportWindow.ShowUnshown(null, () => new CrashReport("Sample", "hazeliscoding/staypaw", ShortReport));
        Dispatcher.UIThread.RunJobs();

        Assert.Null(Assert.Single(opened).Owner);
    }, CancellationToken.None);

    [Fact]
    public Task Reads_the_reports_once_however_often_the_owner_opens() => Session.Dispatch(() =>
    {
        var owner = new Window();
        var takes = 0;

        CrashReportWindow.ShowUnshown(owner, () => { takes++; return null; });
        owner.Show();
        Dispatcher.UIThread.RunJobs();
        owner.Hide();
        owner.Show();
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(1, takes);
    }, CancellationToken.None);

    private static readonly CrashReportStrings German = new()
    {
        Closed = "Sample wurde wegen eines Fehlers beendet.",
        Intro = "Das ist der Bericht dazu, und nichts wurde gesendet.",
        TooLongForLink = "Der Bericht ist zu lang für einen Link.",
        ReportOnGitHub = "Auf GitHub melden",
        Copy = "Kopieren",
        Dismiss = "Schließen",
        ReportName = "Absturzbericht",
        Copied = "Der Bericht ist in der Zwischenablage.",
        Contact = "Kein GitHub-Konto? Schick den Bericht an",
    };

    // openContact stands in for the shell, so no test opens a mail app or a browser.
    private static CrashReportWindow Open(
        string text, CrashReportStrings? strings = null, string? contact = null, Action<CrashReport>? openContact = null)
    {
        var window = new CrashReportWindow(new CrashReport("Sample", "hazeliscoding/staypaw", text, contact, strings), openContact);
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
