namespace Oopsling;

/// <summary>One crash as the GitHub issue it may become: the title on the first line, then the body.</summary>
internal static class Report
{
    public static string Format(Exception exception, string app, string details, Mask mask) =>
        mask.Apply($"""
            {app} crashed: {exception.GetType().Name}

            ```text
            {Describe(exception)}
            ```

            {details}

            """);

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
