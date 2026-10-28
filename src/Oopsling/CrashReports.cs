using System.Reflection;
using System.Runtime;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace Oopsling;

/// <summary>
/// Writes a report when an unhandled exception ends the app. Oopsling sends nothing: the user reads the report and files
/// it as a GitHub issue themselves.
/// </summary>
public static partial class CrashReports
{
    private static (string Folder, string App, string Repo, CrashReportStrings? Strings)? _started;

    /// <summary>Starts recording crashes. Call it once, first thing in <c>Main</c>.</summary>
    /// <param name="appName">The app's name as users see it. Reports go in a <c>crashes</c> folder under it in local app data.</param>
    /// <param name="repo">The GitHub repo that reports are filed in, as <c>owner/name</c>.</param>
    public static void Start(string appName, string repo) => Start(appName, repo, null);

    /// <inheritdoc cref="Start(string, string)"/>
    /// <param name="strings">The window's text in the app's language. Each string left out stays English.</param>
    public static void Start(string appName, string repo, CrashReportStrings? strings)
    {
        // A second handler would record each crash twice.
        if (_started is not null)
        {
            throw new InvalidOperationException("CrashReports.Start was already called. Call it once, first thing in Main.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(appName);
        if (appName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            throw new ArgumentException($"The app name becomes a folder name, so it can't hold path characters, but got \"{appName}\".", nameof(appName));
        }

        // The repo goes into the issue link, so anything more than owner/name would only break it at a user's next launch.
        if (!IsRepo(repo))
        {
            throw new ArgumentException($"Expected the GitHub repo as owner/name, like hazeliscoding/staypaw, but got \"{repo}\".", nameof(repo));
        }

        // Everything is worked out now, so a crash only formats and writes one file.
        var version = Version();
        var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), appName, "crashes");
        var recorder = new Recorder(
            folder,
            $"{appName} {version.Split('+')[0]}",
            Details(appName, version),
            new Mask(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                Read(() => Environment.UserName),
                Read(() => Environment.MachineName),
                Read(() => Environment.UserDomainName)));
        _started = (folder, appName, repo, strings);

        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is Exception exception)
            {
                recorder.Record(exception);
            }
        };
    }

    /// <summary>
    /// The newest report not yet shown, or null when the app hasn't crashed since one was. A report counts as shown once
    /// this returns it, so each crash is shown once.
    /// </summary>
    public static CrashReport? TakeUnshown()
    {
        if (_started is not { } started)
        {
            throw new InvalidOperationException("CrashReports.Start hasn't been called. Call it first thing in Main, before showing reports.");
        }

        return TakeUnshown(started.Folder, started.App, started.Repo, started.Strings);
    }

    // Report names sort by time, so the newest is the last name and "shown" only remembers one name. A report older than
    // the last one shown is never shown: the newer crash already told the user.
    internal static CrashReport? TakeUnshown(string folder, string app, string repo, CrashReportStrings? strings = null)
    {
        try
        {
            var newest = Path.GetFileName(Directory.GetFiles(folder, "crash-*.md").Max(StringComparer.Ordinal));
            var shown = Path.Combine(folder, "shown");
            if (newest is null || (File.Exists(shown) && string.CompareOrdinal(newest, File.ReadAllText(shown)) <= 0))
            {
                return null;
            }

            var text = File.ReadAllText(Path.Combine(folder, newest)).ReplaceLineEndings("\n");
            File.WriteAllText(shown, newest);
            return string.IsNullOrWhiteSpace(text) ? null : new CrashReport(app, repo, text, strings);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // The window is extra: a folder that can't be read mustn't stop the app from starting.
            return null;
        }
    }

    internal static bool IsRepo(string? repo) => repo is not null && Repo().IsMatch(repo);

    // GitHub's owner names hold letters, digits and hyphens, and repo names also dots and underscores, but aren't . or ..
    [GeneratedRegex(@"\A[A-Za-z0-9-]+/(?!\.\.?\z)[A-Za-z0-9._-]+\z", RegexOptions.CultureInvariant)]
    private static partial Regex Repo();

    // Start runs first thing in Main, so a name the OS can't give, as when a domain controller is out of reach, goes
    // unmasked by name rather than stopping the app. The home folder is still masked.
    private static string Read(Func<string> name)
    {
        try
        {
            return name();
        }
        catch (Exception)
        {
            return "";
        }
    }

    // A NativeAOT build has no JIT. RuntimeFeature.IsDynamicCodeSupported can't tell: PublishAot turns it off in JIT
    // builds too, so they behave alike before publishing.
    internal static string Details(string appName, string version) => $"""
        - App: {appName} {version}
        - OS: {RuntimeInformation.OSDescription}
        - Runtime: {RuntimeInformation.FrameworkDescription}
        - Architecture: {RuntimeInformation.ProcessArchitecture}
        - NativeAOT: {(JitInfo.GetCompiledMethodCount() == 0 ? "yes" : "no")}
        """;

    // The informational version carries the commit after a +, which helps match a NativeAOT stack to its build.
    private static string Version()
    {
        var entry = Assembly.GetEntryAssembly();
        return entry?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? entry?.GetName().Version?.ToString()
            ?? "unknown";
    }
}
