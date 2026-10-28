using System.Diagnostics;
using System.Text;

namespace Oopsling;

/// <summary>A report as the window shows it and as the issue link files it.</summary>
public sealed class CrashReport
{
    // Signed out, GitHub sends the user to sign in first, and past about 6,600 characters that redirect starts failing
    // with a 500. Signed in, a link works up to 8,209. Measured on 2026-10-26; see ROADMAP.md.
    internal const int MaxLinkLength = 6000;

    internal CrashReport(string app, string repo, string text, CrashReportStrings? strings = null)
    {
        AppName = app;
        strings ??= new();
        Strings = strings.Closed is null ? strings with { Closed = $"{app} closed because of an error." } : strings;
        Text = text;
        var split = text.IndexOf("\n\n", StringComparison.Ordinal);
        Title = split < 0 ? text.TrimEnd('\n') : text[..split];
        var body = split < 0 ? "" : text[(split + 2)..];
        IssueLink = Link(repo, Title, body);
        FitsInLink = IssueLink.Length <= MaxLinkLength;
        if (!FitsInLink)
        {
            IssueLink = Link(repo, Title, Cut(repo, Title, body, Strings.CutNote));
        }
    }

    /// <summary>The app's name, as passed to <see cref="CrashReports.Start(string, string)"/>.</summary>
    public string AppName { get; }

    /// <summary>The window's text, as passed to <see cref="CrashReports.Start(string, string, CrashReportStrings)"/>, with English for each string left out.</summary>
    public CrashReportStrings Strings { get; }

    /// <summary>The issue's title: the report's first line.</summary>
    public string Title { get; }

    /// <summary>The whole report as it was written, title first: what the window shows and Copy copies.</summary>
    public string Text { get; }

    /// <summary>The host repo's new-issue page with the title and the rest of the report filled in.</summary>
    public string IssueLink { get; }

    /// <summary>
    /// False when the report is too long for a link. The link then carries the top of the stack and asks the user to
    /// paste the whole report, so put <see cref="Text"/> on the clipboard before opening it.
    /// </summary>
    public bool FitsInLink { get; }

    /// <summary>
    /// Opens <see cref="IssueLink"/> in the user's browser through the OS shell. Nothing is sent until they submit it.
    /// Throws when the OS has no way to open a link, as on a machine without a browser.
    /// </summary>
    public void OpenIssue() => Process.Start(new ProcessStartInfo(IssueLink) { UseShellExecute = true })?.Dispose();

    private static string Link(string repo, string title, string body) =>
        $"https://github.com/{repo}/issues/new?title={Uri.EscapeDataString(title)}&body={Uri.EscapeDataString(body)}";

    // Keeps the frames nearest the throw and the details after the stack, and drops the frames furthest from it.
    // Escaping works character by character, so each kept line costs exactly its own escaped length.
    private static string Cut(string repo, string title, string body, string note)
    {
        var lines = body.Split('\n');

        // A report's fence is longer than any run of backticks inside it, so the last line of backticks closes it.
        var close = Array.FindLastIndex(lines, line => line.Length >= 3 && line.All(c => c == '`'));
        var stack = close < 0 ? lines : lines[..close];
        var tail = "   ...\n" + (close < 0 ? "" : string.Join('\n', lines[close..]));
        var budget = MaxLinkLength - Link(repo, title, note + "\n\n" + tail).Length;
        var kept = new StringBuilder(note + "\n\n");
        foreach (var line in stack)
        {
            var cost = Uri.EscapeDataString(line + "\n").Length;
            if (cost > budget)
            {
                // A line too long to keep whole, like an AggregateException's messages, keeps what fits. Its "\n" costs 3.
                var room = budget - 3;
                var start = kept.Length;
                foreach (var rune in line.EnumerateRunes())
                {
                    room -= Uri.EscapeDataString(rune.ToString()).Length;
                    if (room < 0)
                    {
                        break;
                    }

                    kept.Append(rune.ToString());
                }

                if (kept.Length > start)
                {
                    kept.Append('\n');
                }

                break;
            }

            budget -= cost;
            kept.Append(line).Append('\n');
        }

        return kept.Append(tail).ToString();
    }
}
