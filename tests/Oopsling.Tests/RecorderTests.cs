namespace Oopsling.Tests;

public sealed class RecorderTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "oopsling-tests", Guid.NewGuid().ToString("N"));

    private string Folder => Path.Combine(_root, "crashes");

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public void Writes_one_report_into_the_folder()
    {
        Recorder().Record(new InvalidOperationException("boom"));

        var file = Assert.Single(Directory.GetFiles(Folder, "crash-*.md"));
        Assert.StartsWith("Sample 1.0 crashed: InvalidOperationException", File.ReadAllText(file));
    }

    [Fact]
    public void Keeps_the_newest_10_reports()
    {
        Directory.CreateDirectory(Folder);
        for (var i = 0; i < 12; i++)
        {
            File.WriteAllText(Path.Combine(Folder, $"crash-20200101T0000{i:00}000Z.md"), "old");
        }

        Recorder().Record(new InvalidOperationException("boom"));

        var names = Directory.GetFiles(Folder).Select(Path.GetFileName).Order(StringComparer.Ordinal).ToArray();
        Assert.Equal(10, names.Length);
        Assert.Equal("crash-20200101T000003000Z.md", names[0]);
        Assert.StartsWith("Sample 1.0 crashed", File.ReadAllText(Path.Combine(Folder, names[^1]!)));
    }

    // A clock set back would name the new report before the ones already there, and pruning would delete it at once.
    [Fact]
    public void A_report_written_after_the_clock_went_back_still_sorts_last()
    {
        Directory.CreateDirectory(Folder);
        for (var i = 0; i < 10; i++)
        {
            File.WriteAllText(Path.Combine(Folder, $"crash-20990101T0000{i:00}000Z.md"), "from a clock set forward");
        }

        Recorder().Record(new InvalidOperationException("boom"));

        var names = Directory.GetFiles(Folder).Select(Path.GetFileName).Order(StringComparer.Ordinal).ToArray();
        Assert.Equal(10, names.Length);
        Assert.StartsWith("Sample 1.0 crashed", File.ReadAllText(Path.Combine(Folder, names[^1]!)));
    }

    // Two threads can crash at once, and each raises the handler.
    [Fact]
    public void Records_only_the_first_crash()
    {
        var recorder = Recorder();

        recorder.Record(new InvalidOperationException("first"));
        recorder.Record(new InvalidOperationException("second"));

        Assert.Contains("first", File.ReadAllText(Assert.Single(Directory.GetFiles(Folder, "crash-*.md"))));
    }

    // The app is already crashing: a report that can't be written must not replace that crash with another.
    [Fact]
    public void Never_throws_when_the_folder_cannot_be_written()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(Folder, "a file where the folder should be");

        Recorder().Record(new InvalidOperationException("boom"));

        Assert.True(File.Exists(Folder));
    }

    private Recorder Recorder() => new(Folder, "Sample 1.0", "- App: Sample 1.0", new Mask(@"C:\Users\Wren", "Wren"));
}
