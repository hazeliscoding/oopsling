using System.Globalization;
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
    private static (string Folder, string App, string Repo, string? Contact, CrashReportStrings? Strings, Mask Mask)? _started;

    /// <summary>Starts recording crashes. Call it once, first thing in <c>Main</c>.</summary>
    /// <param name="appName">
    /// The app's name as users see it. Unless a folder is given, reports go in a <c>crashes</c> folder under it in local
    /// app data.
    /// </param>
    /// <param name="repo">The GitHub repo that reports are filed in, as <c>owner/name</c>.</param>
    public static void Start(string appName, string repo) => Start(appName, repo, null, null, null);

    // 1.1's overload, kept for apps built against it. Its parameters have no defaults, or a call naming only the contact
    // would match this overload and the next one alike.
    /// <inheritdoc cref="Start(string, string, string?, CrashReportStrings?, string?)"/>
    public static void Start(string appName, string repo, string? contact, CrashReportStrings? strings) =>
        Start(appName, repo, contact, strings, null);

    /// <inheritdoc cref="Start(string, string)"/>
    /// <param name="contact">
    /// Where a user without a GitHub account sends a report: an email address, or an https link such as a support page.
    /// The window shows it under its buttons.
    /// </param>
    /// <param name="strings">The window's text in the app's language. Each string left out stays English.</param>
    /// <param name="folder">
    /// The full path of the folder reports go in, for an app whose installer empties its folder in local app data, as
    /// Velopack's Setup does when the pack ID is the app's name.
    /// </param>
    public static void Start(string appName, string repo, string? contact = null, CrashReportStrings? strings = null, string? folder = null)
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

        // The contact becomes a link the OS shell opens, so it can only be one that opens a mail draft or a web page.
        if (contact is not null && !IsContact(contact))
        {
            throw new ArgumentException($"Expected the contact as an email address or an https link, like crashes@example.com, but got \"{contact}\".", nameof(contact));
        }

        // A relative folder would land wherever the app was started from, which for a login start is often System32.
        if (folder is not null && !Path.IsPathFullyQualified(folder))
        {
            throw new ArgumentException($"Expected the folder as a full path, but got \"{folder}\".", nameof(folder));
        }

        // Everything is worked out now, so a crash only formats and writes one file.
        var version = Version();
        folder ??= Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), appName, "crashes");
        var mask = new Mask(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            Read(() => Environment.UserName),
            Read(() => Environment.MachineName),
            Read(() => Environment.UserDomainName));
        var recorder = new Recorder(folder, $"{appName} {version.Split('+')[0]}", Details(appName, version), mask);
        _started = (folder, appName, repo, contact, strings, mask);

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
    /// this returns it, so each crash is shown once. On Windows it also reads the native crashes Windows recorded for the
    /// app, such as an access violation or a stack overflow.
    /// </summary>
    public static CrashReport? TakeUnshown()
    {
        if (_started is not { } started)
        {
            throw new InvalidOperationException("CrashReports.Start hasn't been called. Call it first thing in Main, before showing reports.");
        }

        return TakeUnshown(started.Folder, started.App, started.Repo, started.Contact, started.Strings, Log(), started.Mask);
    }

    // Report names sort by time, so the newest is the last name and "shown" only remembers one name. A report older than
    // the last one shown is never shown: the newer crash already told the user. The same goes for a native crash, which
    // is newest by its time and remembered by its record number.
    internal static CrashReport? TakeUnshown(
        string folder, string app, string repo, string? contact = null, CrashReportStrings? strings = null, ICrashLog? log = null, Mask? mask = null)
    {
        try
        {
            var reports = Directory.Exists(folder)
                ? Directory.GetFiles(folder, "crash-*.md").Select(path => Path.GetFileName(path)).Order(StringComparer.Ordinal).ToList()
                : [];
            var shown = Path.Combine(folder, "shown");
            var newest = reports.LastOrDefault();
            if (newest is not null && File.Exists(shown) && string.CompareOrdinal(newest, File.ReadAllText(shown)) <= 0)
            {
                newest = null;
            }

            var crash = log is null ? null : TakeNative(folder, reports, log);
            string? text = null;
            if (newest is not null)
            {
                text = File.ReadAllText(Path.Combine(folder, newest)).ReplaceLineEndings("\n");
                File.WriteAllText(shown, newest);
            }

            if (crash is not null && (newest is null || crash.Time > Recorder.TimeOf(newest)))
            {
                var native = Report.Format(crash, log!.Runtime(crash), $"{app} {crash.AppVersion}", Details(app, crash.AppVersion), mask ?? new Mask("", ""));
                return new CrashReport(app, repo, native, contact, strings);
            }

            return string.IsNullOrWhiteSpace(text) ? null : new CrashReport(app, repo, text, contact, strings);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // The window is extra: a folder that can't be read mustn't stop the app from starting.
            return null;
        }
    }

    // The crashes logged since the last look, or in the last 7 days before the first, since the log keeps them for months.
    // The newest record read is remembered, whatever app it was about, so the next launch reads only what's new. A crash
    // whose process wrote a report is skipped: the report says more.
    private static NativeCrash? TakeNative(string folder, List<string> reports, ICrashLog log)
    {
        var looked = Path.Combine(folder, "looked");
        long? last = File.Exists(looked) && long.TryParse(File.ReadAllText(looked), NumberStyles.None, CultureInfo.InvariantCulture, out var record) ? record : null;
        var (crashes, newest) = log.Crashes(last ?? 0, last is null ? DateTime.UtcNow.AddDays(-7) : DateTime.UnixEpoch);
        if (newest > (last ?? 0))
        {
            Directory.CreateDirectory(folder);
            File.WriteAllText(looked, newest.ToString(CultureInfo.InvariantCulture));
        }

        var written = reports.Select(Recorder.TimeOf).ToList();
        return crashes.Where(crash => !written.Any(time => time >= crash.Started && time <= crash.Time)).MaxBy(crash => crash.Record);
    }

    // A host run through the dotnet muxer would see the crashes of every app run that way, so it reads none.
    private static ICrashLog? Log() =>
        OperatingSystem.IsWindows() && Environment.ProcessPath is { } path && !Path.GetFileNameWithoutExtension(path).Equals("dotnet", StringComparison.OrdinalIgnoreCase)
            ? new ApplicationLog(path)
            : null;

    internal static bool IsRepo(string? repo) => repo is not null && Repo().IsMatch(repo);

    internal static bool IsContact(string contact) =>
        Email().IsMatch(contact)
        || (contact.StartsWith("https://", StringComparison.Ordinal) && !contact.Any(char.IsWhiteSpace) && Uri.TryCreate(contact, UriKind.Absolute, out _));

    // GitHub's owner names hold letters, digits and hyphens, and repo names also dots and underscores, but aren't . or ..
    [GeneratedRegex(@"\A[A-Za-z0-9-]+/(?!\.\.?\z)[A-Za-z0-9._-]+\z", RegexOptions.CultureInvariant)]
    private static partial Regex Repo();

    // One plain ASCII address with a dotted domain. Anything else could add a recipient or a header to a mailto link, or
    // hide in the window, as a right-to-left override does.
    [GeneratedRegex(@"\A[A-Za-z0-9._+-]+@[A-Za-z0-9-]+(?:\.[A-Za-z0-9-]+)+\z", RegexOptions.CultureInvariant)]
    private static partial Regex Email();

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
