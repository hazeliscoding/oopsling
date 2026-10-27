using System.Globalization;

namespace Oopsling;

/// <summary>Writes a crash's report and keeps the newest 10. It runs in a dying process, so it does little and never throws.</summary>
internal sealed class Recorder(string folder, string app, string details, Mask mask)
{
    private const int Kept = 10;
    private const string Time = "yyyyMMdd'T'HHmmssfff'Z'";

    private int _recorded;

    public void Record(Exception exception)
    {
        // Two threads can crash at once, and each raises the handler.
        if (Interlocked.Exchange(ref _recorded, 1) == 1)
        {
            return;
        }

        try
        {
            Directory.CreateDirectory(folder);

            // The names sort by time, so the oldest come first and the new report goes last.
            var names = Directory.GetFiles(folder, "crash-*.md").Order(StringComparer.Ordinal).ToList();
            names.Add(Path.Combine(folder, Name(names.LastOrDefault())));
            File.WriteAllText(names[^1], Report.Format(exception, app, details, mask));

            foreach (var old in names.SkipLast(Kept))
            {
                File.Delete(old);
            }
        }
        catch (Exception)
        {
            // A report that can't be written must not replace the crash it reports.
        }
    }

    // A clock set back, or set forward once, would put a new report among the old ones, where pruning could delete it
    // and the window would never show it. So it's always named after the newest report already there.
    private static string Name(string? newest)
    {
        var time = DateTime.UtcNow;
        if (newest is not null
            && DateTime.TryParseExact(Path.GetFileNameWithoutExtension(newest).AsSpan("crash-".Length), Time, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var last)
            && last >= time)
        {
            time = last.AddMilliseconds(1);
        }

        return "crash-" + time.ToString(Time, CultureInfo.InvariantCulture) + ".md";
    }
}
