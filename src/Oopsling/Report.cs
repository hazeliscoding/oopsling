using System.Reflection;

namespace Oopsling;

/// <summary>One crash as the GitHub issue it may become: the title on the first line, then the body.</summary>
internal static class Report
{
    // Only the exception's text is masked: the title and details are built by Oopsling from names and versions.
    public static string Format(Exception exception, string app, string details, Mask mask) => $"""
        {app} crashed: {Cause(exception).GetType().Name}

        ```text
        {mask.Apply(Describe(exception))}
        ```

        {details}

        """;

    // Avalonia calls event handlers through reflection, so the exception that ended the app often arrives wrapped.
    private static Exception Cause(Exception exception) =>
        exception is TargetInvocationException { InnerException: { } inner } ? Cause(inner) : exception;

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
