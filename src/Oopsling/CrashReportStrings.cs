namespace Oopsling;

/// <summary>
/// The window's text, and the note that starts a report too long for a link, in the host's language. Each string left
/// out stays English. Write whole sentences with the app's name in them: Oopsling fills nothing in.
/// </summary>
public sealed record CrashReportStrings
{
    /// <summary>The window's heading, and its title without the full stop. English names the app.</summary>
    public string? Closed { get; init; }

    /// <summary>Under the heading: what the report is, and that nothing was sent.</summary>
    public string Intro { get; init; } = "This is the report it made, and nothing has been sent. Report on GitHub opens it as a new "
        + "issue in your browser, where you can read and edit it before you submit it.";

    /// <summary>Under the intro, when the report is too long for a link.</summary>
    public string TooLongForLink { get; init; } = "The report is too long for a link, so Report on GitHub also copies it. Paste it "
        + "into the issue in place of the text there.";

    public string ReportOnGitHub { get; init; } = "Report on GitHub";

    public string Copy { get; init; } = "Copy";

    public string Dismiss { get; init; } = "Dismiss";

    /// <summary>The report box's name for screen readers.</summary>
    public string ReportName { get; init; } = "Crash report";

    /// <summary>After Copy works.</summary>
    public string Copied { get; init; } = "The report is on the clipboard.";

    /// <summary>After Copy fails.</summary>
    public string CopyFailed { get; init; } = "The report couldn't be copied. Select it above and copy it from there.";

    /// <summary>When a report too long for a link can't be copied, so GitHub isn't opened.</summary>
    public string CopyFailedBeforeGitHub { get; init; } = "The report couldn't be copied, so GitHub wasn't opened. Select the "
        + "report above and copy it from there.";

    /// <summary>When Report on GitHub finds no browser.</summary>
    public string NoBrowser { get; init; } = "No browser opened. Copy the report and file it on GitHub yourself.";

    /// <summary>
    /// Starts the issue of a report too long for a link, so the user reads it on GitHub. It reaches the issue if they
    /// don't paste over it.
    /// </summary>
    public string CutNote { get; init; } = "This report was too long for a link, so the end of its stack is missing here. The "
        + "whole report is on your clipboard: select all of this text and paste it in its place.";
}
