namespace Oopsling;

/// <summary>
/// A crash that Windows recorded in its Application log with no managed exception behind it, such as an access violation
/// or a stack overflow.
/// </summary>
/// <param name="Record">The event's record number, which only grows, whatever the clock does.</param>
/// <param name="Time">When Windows logged the crash, just after the process ended, in UTC.</param>
/// <param name="Started">When the crashed process started, in UTC.</param>
internal sealed record NativeCrash(
    long Record,
    DateTime Time,
    DateTime Started,
    int ProcessId,
    string AppVersion,
    string Module,
    string ModuleVersion,
    string ModulePath,
    uint Code,
    ulong Offset);

/// <summary>Where native crashes are read from: Windows' Application log, or a made-up one in tests.</summary>
internal interface ICrashLog
{
    /// <summary>
    /// The app's crashes logged after the record number <paramref name="after"/> and since <paramref name="since"/>, and
    /// the newest record number read, whatever app it was about.
    /// </summary>
    (IReadOnlyList<NativeCrash> Crashes, long Newest) Crashes(long after, DateTime since);

    /// <summary>The .NET runtime's own account of the crash, from its description on, or null when it logged none.</summary>
    string? Runtime(NativeCrash crash);
}
