using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Oopsling.Tests;

public class ReportTests
{
    private static readonly Mask Mask = new(@"C:\Users\Wren", "Wren");
    private const string Details = "- App: Sample 1.2.3+abc\n- OS: Made-up OS 1.0";

    [Fact]
    public void The_first_line_is_the_issue_title() =>
        Assert.Equal("Sample 1.2.3 crashed: InvalidOperationException", Format(Thrown(new InvalidOperationException("boom"))).Split('\n')[0]);

    // Avalonia calls Click handlers through reflection, so a UI-thread crash arrives wrapped.
    [Fact]
    public void The_title_names_the_exception_inside_a_TargetInvocationException() =>
        Assert.Equal(
            "Sample 1.2.3 crashed: InvalidOperationException",
            Format(new TargetInvocationException(new InvalidOperationException("boom"))).Split('\n')[0]);

    // Oopsling builds the details itself, and a commit hash in the version would look like a key to the mask.
    [Fact]
    public void Leaves_the_details_as_given()
    {
        const string details = "- App: Sample 1.2.3+0123456789abcdef0123456789abcdef01234567";

        Assert.Contains(details, Report.Format(new InvalidOperationException("boom"), "Sample 1.2.3", details, Mask));
    }

    [Fact]
    public void Names_the_method_that_threw() => Assert.Contains(nameof(ThrowFromHere), Format(Thrown(new InvalidOperationException("boom"))));

    [Fact]
    public void Holds_each_inner_exception_with_its_message() =>
        Assert.Contains("System.FormatException: inner detail", Format(Thrown(new InvalidOperationException("outer", new FormatException("inner detail")))));

    [Fact]
    public void Ends_with_the_details() => Assert.EndsWith(Details, Format(Thrown(new InvalidOperationException("boom"))).TrimEnd());

    [Fact]
    public void Leaves_no_trace_of_a_made_up_user_or_home_folder()
    {
        var report = Format(Thrown(new IOException(@"Could not open 'C:\Users\Wren\notes.txt' or C--Users-Wren-notes")));

        Assert.Contains("<home>", report);
        Assert.DoesNotContain("Wren", report, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Survives_an_exception_whose_ToString_throws() => Assert.Contains(nameof(BrokenException), Format(new BrokenException()));

    private static string Format(Exception exception) => Report.Format(exception, "Sample 1.2.3", Details, Mask);

    private static Exception Thrown(Exception exception)
    {
        try
        {
            ThrowFromHere(exception);
        }
        catch (Exception caught)
        {
            return caught;
        }

        throw new UnreachableException();
    }

    // Kept out of line so Release builds keep its frame in the stack.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowFromHere(Exception exception) => throw exception;

    private sealed class BrokenException : Exception
    {
        public override string ToString() => throw new InvalidOperationException("ToString is broken.");
    }
}
