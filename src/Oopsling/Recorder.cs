using System.Globalization;

namespace Oopsling;

/// <summary>Writes a crash's report and keeps the newest 10. It runs in a dying process, so it does little and never throws.</summary>
internal sealed class Recorder(string folder, string app, string details, Mask mask)
{
    private const int Kept = 10;

    public void Record(Exception exception)
    {
        try
        {
            Directory.CreateDirectory(folder);
            var name = "crash-" + DateTime.UtcNow.ToString("yyyyMMdd'T'HHmmssfff'Z'", CultureInfo.InvariantCulture) + ".md";
            File.WriteAllText(Path.Combine(folder, name), Report.Format(exception, app, details, mask));

            // The names sort by time, so the oldest come first.
            foreach (var old in Directory.GetFiles(folder, "crash-*.md").Order(StringComparer.Ordinal).SkipLast(Kept))
            {
                File.Delete(old);
            }
        }
        catch (Exception)
        {
            // A report that can't be written must not replace the crash it reports.
        }
    }
}
