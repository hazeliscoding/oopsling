using System.Globalization;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Oopsling;

/// <summary>
/// Reads one app's crashes from Windows' Application log, where Windows Error Reporting records every crash as an
/// "Application Error" event, through wevtapi, Windows' event log API.
/// </summary>
[SupportedOSPlatform("windows")]
internal sealed unsafe partial class ApplicationLog(string appPath) : ICrashLog
{
    private const int QueryChannelPath = 0x1;
    private const int QueryReverseDirection = 0x200;
    private const int RenderContextValues = 0;
    private const int RenderContextUser = 2;
    private const int RenderEventValues = 0;
    private const string Time = "yyyy-MM-dd'T'HH:mm:ss.fff'Z'";

    // The fields of an "Application Error" event, by name, so a field Windows adds can't shift the others.
    private static readonly string[] CrashFields =
    [
        "Event/System/EventRecordID",
        "Event/System/TimeCreated/@SystemTime",
        "Event/EventData/Data[@Name='AppPath']",
        "Event/EventData/Data[@Name='AppVersion']",
        "Event/EventData/Data[@Name='ModuleName']",
        "Event/EventData/Data[@Name='ModuleVersion']",
        "Event/EventData/Data[@Name='ModulePath']",
        "Event/EventData/Data[@Name='ExceptionCode']",
        "Event/EventData/Data[@Name='FaultingOffset']",
        "Event/EventData/Data[@Name='ProcessId']",
        "Event/EventData/Data[@Name='ProcessCreationTime']",
        "Event/System/Provider/@Name",
        "Event/System/EventID",
    ];

    // A filter in the query makes Windows scan the whole log, which took about 150 ms for 37,000 records, at every launch.
    // Reading every event newest first and stopping at the last look reads only what's new.
    public (IReadOnlyList<NativeCrash> Crashes, long Newest) Crashes(long after, DateTime since)
    {
        try
        {
            var rows = Read("*", CrashFields, row => (long)Number(row[0]) > after && row[1] is DateTime time && time >= since);
            List<NativeCrash> crashes = [.. rows
                .Where(row => row[11] as string == "Application Error" && Number(row[12]) == 1000
                    && string.Equals(row[2] as string, appPath, StringComparison.OrdinalIgnoreCase))
                .Select(row => new NativeCrash(
                    (long)Number(row[0]),
                    row[1] as DateTime? ?? default,
                    row[10] as DateTime? ?? DateTime.FromFileTimeUtc((long)Number(row[10])),
                    (int)Number(row[9]),
                    row[3] as string ?? "",
                    row[4] as string ?? "",
                    row[5] as string ?? "",
                    row[6] as string ?? "",
                    (uint)Number(row[7]),
                    Number(row[8])))];
            return (crashes, rows.Count > 0 ? (long)Number(rows[0][0]) : after);
        }
        catch (Exception)
        {
            // Reading Windows' log is extra: anything unexpected in it means no native crash, not a launch that fails.
            return ([], after);
        }
    }

    // The runtime logs its own event from inside the dying process, so the event's process ID is the crashed process's.
    // This one scans the whole log, but only when a crash is about to be shown.
    public string? Runtime(NativeCrash crash)
    {
        try
        {
            var query = "*[System[Provider[@Name='.NET Runtime'] and (EventID=1023 or EventID=1025 or EventID=1026)"
                + $" and Execution[@ProcessID={crash.ProcessId.ToString(CultureInfo.InvariantCulture)}]"
                + $" and TimeCreated[@SystemTime>='{crash.Started.ToString(Time, CultureInfo.InvariantCulture)}' and @SystemTime<='{crash.Time.ToString(Time, CultureInfo.InvariantCulture)}']]]";
            var text = Read(query, [], _ => true).Select(row => row.FirstOrDefault() as string).FirstOrDefault(text => text is not null);

            // The lines before the description name the app and the runtime's version, which the report has already.
            var description = text?.IndexOf("Description:", StringComparison.Ordinal) ?? -1;
            return description < 0 ? text : text![description..];
        }
        catch (Exception)
        {
            return null;
        }
    }

    // Newest first, each row holding the values at the paths given, in their order, or with no paths, the event's own
    // values (the runtime's event has one unnamed value, which a path renders as a list). It stops at the first row
    // that isn't wanted.
    private static List<object?[]> Read(string query, string[] paths, Func<object?[], bool> wanted)
    {
        const int Batch = 64;
        var rows = new List<object?[]>();
        nint results = 0, context = 0;
        var events = stackalloc nint[Batch];
        try
        {
            results = EvtQuery(0, "Application", query, QueryChannelPath | QueryReverseDirection);
            context = results == 0 ? 0 : paths.Length == 0 ? EvtCreateRenderContext(0, null, RenderContextUser) : EvtCreateRenderContext(paths.Length, paths, RenderContextValues);
            var more = context != 0;
            while (more && EvtNext(results, Batch, events, 1000, 0, out var returned))
            {
                for (var i = 0; i < returned; i++)
                {
                    if (more)
                    {
                        var row = Render(context, events[i], paths.Length);
                        more = wanted(row);
                        if (more)
                        {
                            rows.Add(row);
                        }
                    }

                    EvtClose(events[i]);
                }
            }
        }
        finally
        {
            if (context != 0)
            {
                EvtClose(context);
            }

            if (results != 0)
            {
                EvtClose(results);
            }
        }

        return rows;
    }

    // Always as many values as the paths, so a short event can't move a field; with no paths, as many as the event has.
    private static object?[] Render(nint context, nint record, int count)
    {
        EvtRender(context, record, RenderEventValues, 0, null, out var size, out _);
        var buffer = new byte[size];
        fixed (byte* start = buffer)
        {
            if (size == 0 || !EvtRender(context, record, RenderEventValues, size, start, out _, out var rendered))
            {
                return new object?[count];
            }

            var row = new object?[count == 0 ? rendered : count];
            for (var i = 0; i < Math.Min(row.Length, rendered); i++)
            {
                row[i] = Value((Variant*)start + i);
            }

            return row;
        }
    }

    private static object? Value(Variant* value) => value->Type switch
    {
        1 => new string(value->String),
        4 => value->Number & 0xFF,
        6 => value->Number & 0xFFFF,
        8 or 20 => value->Number & 0xFFFF_FFFF,
        10 or 21 => value->Number,
        17 => DateTime.FromFileTimeUtc((long)value->Number),
        _ => null,
    };

    // Windows gives a number typed, or as hex text with or without 0x, as the crash's code and offset are.
    private static ulong Number(object? value) => value switch
    {
        ulong number => number,
        string text when ulong.TryParse(text.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? text.AsSpan(2) : text,
            NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var number) => number,
        _ => 0,
    };

    // EVT_VARIANT: a value, or a pointer to one inside the same buffer, then its count and type.
    [StructLayout(LayoutKind.Explicit, Size = 16)]
    private struct Variant
    {
        [FieldOffset(0)]
        public ulong Number;

        [FieldOffset(0)]
        public char* String;

        [FieldOffset(8)]
        public uint Count;

        [FieldOffset(12)]
        public uint Type;
    }

    [LibraryImport("wevtapi.dll", StringMarshalling = StringMarshalling.Utf16)]
    private static partial nint EvtQuery(nint session, string path, string query, int flags);

    [LibraryImport("wevtapi.dll", StringMarshalling = StringMarshalling.Utf16)]
    private static partial nint EvtCreateRenderContext(int valuePathsCount, string[]? valuePaths, int flags);

    [LibraryImport("wevtapi.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool EvtNext(nint resultSet, int eventsSize, nint* events, int timeout, int flags, out int returned);

    [LibraryImport("wevtapi.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool EvtRender(nint context, nint fragment, int flags, int bufferSize, void* buffer, out int bufferUsed, out int propertyCount);

    [LibraryImport("wevtapi.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool EvtClose(nint handle);
}
