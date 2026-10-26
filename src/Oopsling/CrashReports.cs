using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Oopsling;

/// <summary>
/// Writes a report when an unhandled exception ends the app. Oopsling sends nothing: the user reads the report and files
/// it as a GitHub issue themselves.
/// </summary>
public static class CrashReports
{
    /// <summary>Starts recording crashes. Call it first thing in <c>Main</c>.</summary>
    /// <param name="appName">The app's name as users see it. Reports go in a <c>crashes</c> folder under it in local app data.</param>
    /// <param name="repo">The GitHub repo that reports are filed in, as <c>owner/name</c>.</param>
    public static void Start(string appName, string repo)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(appName);
        if (appName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            throw new ArgumentException("The app name becomes a folder name, so it can't hold path characters.", nameof(appName));
        }

        if (repo?.Split('/') is not [{ Length: > 0 }, { Length: > 0 }])
        {
            throw new ArgumentException("Expected the GitHub repo as owner/name, like hazeliscoding/staypaw.", nameof(repo));
        }

        // Everything is worked out now, so a crash only formats and writes one file.
        var version = Version();
        var recorder = new Recorder(
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), appName, "crashes"),
            $"{appName} {version.Split('+')[0]}",
            Details(appName, version),
            new Mask(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), Environment.UserName));

        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is Exception exception)
            {
                recorder.Record(exception);
            }
        };
    }

    internal static string Details(string appName, string version) => $"""
        - App: {appName} {version}
        - OS: {RuntimeInformation.OSDescription}
        - Runtime: {RuntimeInformation.FrameworkDescription}
        - Architecture: {RuntimeInformation.ProcessArchitecture}
        - NativeAOT: {(RuntimeFeature.IsDynamicCodeSupported ? "no" : "yes")}
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
