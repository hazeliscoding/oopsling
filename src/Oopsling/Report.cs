using System.Reflection;

namespace Oopsling;

/// <summary>One crash as the GitHub issue it may become: the title on the first line, then the body.</summary>
internal static class Report
{
    // No issue could hold more.
    private const int MaxLength = 65_536;

    // Only the exception's text is masked: the title and details are built by Oopsling from names and versions.
    public static string Format(Exception exception, string app, string details, Mask mask) =>
        Format($"{app} crashed: {Cause(exception).GetType().Name}", MaskAndCut(Describe(exception), mask), details);

    // The module's name comes from Windows, not Oopsling, so the title masks it too.
    public static string Format(NativeCrash crash, string? runtime, string app, string details, Mask mask)
    {
        var code = $"0x{crash.Code:X8}";
        var name = Name(crash.Code);
        var text = $"{(name is null ? "Exception " + code : $"{char.ToUpperInvariant(name[0])}{name[1..]} ({code})")} at {crash.Module}+0x{crash.Offset:x}\n"
            + $"Module: {crash.ModulePath}, version {crash.ModuleVersion}"
            + (runtime is null ? "" : "\n\n" + runtime.ReplaceLineEndings("\n").TrimEnd());
        return Format($"{app} crashed: {(name is null ? "exception " + code : $"{name} ({code})")} in {mask.Apply(crash.Module)}", MaskAndCut(text, mask), details);
    }

    private static string Format(string title, string text, string details)
    {
        // Longer than any run of backticks in the text, so a message can't end the code block and have the rest read as
        // Markdown, where an @mention notifies someone when the issue is filed.
        var fence = new string('`', Math.Max(3, LongestRun(text, '`') + 1));
        return $"""
            {title}

            {fence}text
            {text}
            {fence}

            {details}

            """;
    }

    // Windows' names for the codes a .NET app most often ends with.
    private static string? Name(uint code) => code switch
    {
        0xC0000005 => "access violation",
        0xC00000FD => "stack overflow",
        // NativeAOT's FailFast and unhandled exceptions end this way too, through RaiseFailFastException.
        0xC0000409 => "fail fast",
        0xC0000374 => "heap corruption",
        0xC000001D => "illegal instruction",
        0xC0000094 => "integer division by zero",
        0xE0434352 => ".NET exception",
        0x80131623 => "Environment.FailFast",
        _ => null,
    };

    // Avalonia calls event handlers through reflection, so the exception that ended the app often arrives wrapped.
    private static Exception Cause(Exception exception) =>
        exception is TargetInvocationException { InnerException: { } inner } ? Cause(inner) : exception;

    // Masking a huge message would keep the dying process busy, so text far past the limit goes before masking. That cut
    // can split a name the mask then misses, but only at the very end of what's left, so that end goes after masking. The
    // final cut comes after masking too, so it can't split a name.
    private static string MaskAndCut(string text, Mask mask)
    {
        var tooLong = text.Length > 4 * MaxLength;
        text = mask.Apply(tooLong ? text[..(4 * MaxLength)] : text);
        if (tooLong)
        {
            text = text[..Math.Max(0, text.Length - 1024)];
        }
        else if (text.Length <= MaxLength)
        {
            return text;
        }

        var length = Math.Min(text.Length, MaxLength);
        var end = text.AsSpan(0, length).LastIndexOfAny(" \t\n");
        return text[..(end > 0 ? end : length)] + "\n   ...";
    }

    private static int LongestRun(string text, char c)
    {
        int longest = 0, run = 0;
        foreach (var x in text)
        {
            run = x == c ? run + 1 : 0;
            longest = Math.Max(longest, run);
        }

        return longest;
    }

    // The type, message and stack of the exception and of each inner one.
    private static string Describe(Exception exception)
    {
        try
        {
            return exception.ToString().ReplaceLineEndings("\n");
        }
        catch (Exception)
        {
            // shortcut: no message or stack when an exception's ToString throws; add them if that turns up in a real report.
            return exception.GetType().FullName + " (its ToString threw)";
        }
    }
}
