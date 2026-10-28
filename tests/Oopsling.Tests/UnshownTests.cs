namespace Oopsling.Tests;

public sealed class UnshownTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "oopsling-tests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    [Fact]
    public void Nothing_is_shown_when_the_app_never_crashed() => Assert.Null(Take());

    // A crash that was never shown, older than one that was, is stale: the newer crash already told the user.
    [Fact]
    public void The_newest_report_is_shown_once_and_older_ones_never()
    {
        Write("crash-20261026T010000000Z.md", "Sample 1.0 crashed: Older");
        Write("crash-20261026T020000000Z.md", "Sample 1.0 crashed: Newer");

        Assert.Equal("Sample 1.0 crashed: Newer", Take()?.Title);
        Assert.Null(Take());
    }

    [Fact]
    public void A_crash_after_one_was_shown_is_shown()
    {
        Write("crash-20261026T010000000Z.md", "Sample 1.0 crashed: First");
        Take();
        Write("crash-20261026T020000000Z.md", "Sample 1.0 crashed: Second");

        Assert.Equal("Sample 1.0 crashed: Second", Take()?.Title);
    }

    // The process can die before the report is written out.
    [Fact]
    public void An_empty_report_is_not_shown()
    {
        Write("crash-20261026T010000000Z.md", "");

        Assert.Null(Take());
    }

    [Fact]
    public void The_report_carries_the_hosts_strings()
    {
        Write("crash-20261026T010000000Z.md", "Sample 1.0 crashed: First");
        var strings = new CrashReportStrings { Copy = "Kopieren" };

        Assert.Equal("Kopieren", CrashReports.TakeUnshown(_folder, "Sample", "hazeliscoding/staypaw", strings: strings)?.Strings.Copy);
    }

    private CrashReport? Take() => CrashReports.TakeUnshown(_folder, "Sample", "hazeliscoding/staypaw");

    private void Write(string name, string title)
    {
        Directory.CreateDirectory(_folder);
        File.WriteAllText(Path.Combine(_folder, name), title.Length > 0 ? $"{title}\n\n```text\nboom\n```\n" : "");
    }
}
