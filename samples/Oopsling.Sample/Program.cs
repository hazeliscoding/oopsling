using System.Runtime.InteropServices;
using Avalonia;

namespace Oopsling.Sample;

internal static partial class Program
{
    // How a host gives the window its own language: whole sentences, with the app's name in them.
    private static readonly CrashReportStrings German = new()
    {
        Closed = "Oopsling Sample wurde wegen eines Fehlers beendet.",
        Intro = "Das ist der Bericht dazu, und nichts wurde gesendet. „Auf GitHub melden“ öffnet ihn als neues Issue in "
            + "deinem Browser, wo du ihn vor dem Absenden lesen und bearbeiten kannst.",
        TooLongForLink = "Der Bericht ist zu lang für einen Link, deshalb kopiert „Auf GitHub melden“ ihn auch. Füge ihn im "
            + "Issue anstelle des Textes dort ein.",
        ReportOnGitHub = "Auf GitHub melden",
        Copy = "Kopieren",
        Dismiss = "Schließen",
        ReportName = "Absturzbericht",
        Copied = "Der Bericht ist in der Zwischenablage.",
        CopyFailed = "Der Bericht konnte nicht kopiert werden. Markiere ihn oben und kopiere ihn von dort.",
        CopyFailedBeforeGitHub = "Der Bericht konnte nicht kopiert werden, deshalb wurde GitHub nicht geöffnet. Markiere ihn "
            + "oben und kopiere ihn von dort.",
        NoBrowser = "Es wurde kein Browser geöffnet. Kopiere den Bericht und melde ihn selbst auf GitHub.",
        Contact = "Kein GitHub-Konto? Schick den Bericht an",
        CopiedForContact = "Der Bericht ist in der Zwischenablage. Füge ihn in deine Nachricht ein.",
        CopyFailedBeforeContact = "Der Bericht konnte nicht kopiert werden, deshalb wurde nichts geöffnet. Markiere ihn oben "
            + "und kopiere ihn von dort.",
        NothingOpened = "Es wurde nichts geöffnet, aber der Bericht ist in der Zwischenablage.",
        CutNote = "Dieser Bericht war zu lang für einen Link, deshalb fehlt hier das Ende seines Stacks. Der ganze Bericht ist "
            + "in deiner Zwischenablage: Markiere diesen ganzen Text und füge ihn an seiner Stelle ein.",
    };

    [STAThread]
    public static void Main(string[] args)
    {
        // Checks pass --repo to file in a scratch repo, so trying the link never touches Oopsling's own issues.
        CrashReports.Start(
            "Oopsling Sample",
            Option(args, "--repo") ?? "hazeliscoding/oopsling",
            Option(args, "--contact"),
            Option(args, "--lang") == "de" ? German : null);

        // For automated checks on Windows: a native crash, which no managed handler sees. A parent's error mode can turn
        // Windows Error Reporting off for its children, as Claude Code's shell does, and then Windows records nothing.
        // Apps that users start from Explorer have it on.
        if (Option(args, "--crash") == "native" && OperatingSystem.IsWindows())
        {
            SetErrorMode(0);
            RtlFillMemory(8, 1, 0);
        }

        // For automated checks: write the report the window would show to a file and exit, since a native crash's report
        // isn't a file of its own.
        if (Option(args, "--print-report") is { } file)
        {
            File.WriteAllText(file, CrashReports.TakeUnshown()?.Text ?? "");
            return;
        }

        AppBuilder.Configure<App>().UsePlatformDetect().StartWithClassicDesktopLifetime(args);
    }

    internal static string? Option(string[] args, string name) => args.SkipWhile(arg => arg != name).Skip(1).FirstOrDefault();

    [LibraryImport("kernel32.dll")]
    private static partial uint SetErrorMode(uint mode);

    // Writes to an address no process owns, inside Windows' own code.
    [LibraryImport("kernel32.dll")]
    private static partial void RtlFillMemory(nint destination, nuint length, byte fill);
}
