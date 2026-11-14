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
    public void The_report_carries_the_hosts_contact_and_strings()
    {
        Write("crash-20261026T010000000Z.md", "Sample 1.0 crashed: First");

        var report = CrashReports.TakeUnshown(_folder, "Sample", "hazeliscoding/staypaw", "crashes@example.com", new() { Copy = "Kopieren" });

        Assert.Equal("crashes@example.com", report?.Contact);
        Assert.Equal("Kopieren", report?.Strings.Copy);
    }

    private const string NativeTitle = "Sample 0.7.0.0 crashed: access violation (0xC0000005) in ntdll.dll";
    private static readonly DateTime Now = DateTime.UtcNow;

    [Fact]
    public void A_native_crash_is_shown_once()
    {
        var log = new FakeLog(Crash(5, Now.AddMinutes(-1)));

        Assert.Equal(NativeTitle, Take(log)?.Title);
        Assert.Null(Take(log));
    }

    // The report says more: it names the exception and the method that threw.
    [Fact]
    public void A_native_crash_whose_process_wrote_a_report_is_skipped()
    {
        var crash = Crash(5, Now.AddMinutes(-1));
        Write(Name(crash.Time.AddMilliseconds(-300)), "Sample 1.0 crashed: Managed");
        var log = new FakeLog(crash);

        Assert.Equal("Sample 1.0 crashed: Managed", Take(log)?.Title);
        Assert.Null(Take(log));
    }

    [Fact]
    public void A_native_crash_newer_than_a_report_is_shown_and_the_report_never()
    {
        Write(Name(Now.AddHours(-2)), "Sample 1.0 crashed: Older");
        var log = new FakeLog(Crash(5, Now.AddHours(-1)));

        Assert.Equal(NativeTitle, Take(log)?.Title);
        Assert.Null(Take(log));
    }

    [Fact]
    public void A_report_newer_than_a_native_crash_is_shown_and_the_native_crash_never()
    {
        Write(Name(Now.AddHours(-1)), "Sample 1.0 crashed: Newer");
        var log = new FakeLog(Crash(5, Now.AddHours(-2)));

        Assert.Equal("Sample 1.0 crashed: Newer", Take(log)?.Title);
        Assert.Null(Take(log));
    }

    // The log keeps crashes for months, and at the first look Oopsling can't tell which ones the user already knows about.
    [Fact]
    public void At_the_first_look_only_native_crashes_from_the_last_7_days_count() =>
        Assert.Null(Take(new FakeLog(Crash(5, Now.AddDays(-8)))));

    // A clock set wrong can't hide a crash logged after the last look.
    [Fact]
    public void After_a_look_native_crashes_count_by_their_place_in_the_log_not_their_time()
    {
        var log = new FakeLog(Crash(5, Now.AddDays(-1)));
        Take(log);
        log.Add(Crash(6, Now.AddDays(-30)));

        Assert.Equal(NativeTitle, Take(log)?.Title);
    }

    // Reading the 7 days before the first look took about 100 ms, so an app that never crashed mustn't do it at every launch.
    [Fact]
    public void After_a_look_that_found_no_crash_only_newer_events_are_read()
    {
        var log = new FakeLog { Newest = 40 };
        Take(log);
        Take(log);

        Assert.Equal(40, log.After);
    }

    private CrashReport? Take(ICrashLog? log = null) => CrashReports.TakeUnshown(_folder, "Sample", "hazeliscoding/staypaw", log: log);

    private static NativeCrash Crash(long record, DateTime time) =>
        new(record, time, time.AddSeconds(-5), 4242, "0.7.0.0", "ntdll.dll", "10.0.26100.1", @"C:\WINDOWS\SYSTEM32\ntdll.dll", 0xC0000005, 0x168405);

    private static string Name(DateTime time) => $"crash-{time:yyyyMMdd'T'HHmmssfff'Z'}.md";

    private sealed class FakeLog(params NativeCrash[] crashes) : ICrashLog
    {
        private readonly List<NativeCrash> _crashes = [.. crashes];

        // The newest record in the log, which other apps' events can put past this app's crashes.
        public long Newest { get; init; }

        public long After { get; private set; }

        public void Add(NativeCrash crash) => _crashes.Add(crash);

        public (IReadOnlyList<NativeCrash> Crashes, long Newest) Crashes(long after, DateTime since)
        {
            After = after;
            return ([.. _crashes.Where(crash => crash.Record > after && crash.Time >= since)], Math.Max(Newest, _crashes.Select(crash => crash.Record).DefaultIfEmpty(after).Max()));
        }

        public string? Runtime(NativeCrash crash) => null;
    }

    private void Write(string name, string title)
    {
        Directory.CreateDirectory(_folder);
        File.WriteAllText(Path.Combine(_folder, name), title.Length > 0 ? $"{title}\n\n```text\nboom\n```\n" : "");
    }
}
