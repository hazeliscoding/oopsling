using System.Reflection;

namespace Oopsling;

/// <summary>One crash as the GitHub issue it may become: the title on the first line, then the body.</summary>
internal static class Report
{
    // Only the exception's text is masked: the title and details are built by Oopsling from names and versions.
    public static string Format(Exception exception, string app, string details, Mask mask) => $"""
        {app} crashed: {Cause(exception).GetType().Name}

        ```text
        {mask.Apply(Cut(Describe(exception)))}
        ```

        {details}

        """;

    // Avalonia calls event handlers through reflection, so the exception that ended the app often arrives wrapped.
    private static Exception Cause(Exception exception) =>
        exception is TargetInvocationException { InnerException: { } inner } ? Cause(inner) : exception;

    // Masking a huge message would keep the dying process busy, and no issue could hold it anyway. The cut falls on a
    // space or a line's end, so it doesn't split a name the mask has to find.
    private static string Cut(string text)
    {
        const int MaxLength = 65_536;
        if (text.Length <= MaxLength)
        {
            return text;
        }

        var end = text.AsSpan(0, MaxLength).LastIndexOfAny(" \t\n");
        return text[..(end > 0 ? end : MaxLength)] + "\n   ...";
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
